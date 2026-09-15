import Cached from '@mui/icons-material/Cached';
import Cloud from '@mui/icons-material/Cloud';
import CloudDownload from '@mui/icons-material/CloudDownload';
import CloudUpload from '@mui/icons-material/CloudUpload';
import DeleteSweep from '@mui/icons-material/DeleteSweep';
import Delete from '@mui/icons-material/Delete';
import Download from '@mui/icons-material/Download';
import PlayArrow from '@mui/icons-material/PlayArrow';
import Restore from '@mui/icons-material/Restore';
import Stop from '@mui/icons-material/Stop';
import SmartToy from '@mui/icons-material/SmartToy';
import VpnKey from '@mui/icons-material/VpnKey';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Divider from '@mui/material/Divider';
import IconButton from '@mui/material/IconButton';
import LinearProgress from '@mui/material/LinearProgress';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useMutation, useQuery } from '@tanstack/react-query';
import React, { useCallback, useMemo, useState } from 'react';

import ConfirmDialog from 'components/ConfirmDialog';
import Page from 'components/Page';
import Loading from 'components/loading/LoadingComponent';
import toast from 'components/toast/toast';
import type { ApiClient } from 'jellyfin-apiclient';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import { queryClient } from 'utils/query/queryClient';

type NebulaWorker = {
    Name?: string;
    DisplayName?: string;
    Status?: string;
    WorkerId?: string;
    Percentage?: number;
    UploadedBytes?: number;
    Size?: number;
    InfoText?: string;
};

type NebulaStatus = {
    IsEnvioRunning: boolean;
    StreamOnly: boolean;
    IsDownloaderRunning: boolean;
    TurboActive: boolean;
    GlobalStatusText: string;
    IsDriveNMounted: boolean;
    DriveNStatus: string;
    ActiveUploads: NebulaWorker[];
    QueuedUploads: NebulaWorker[];
    UploadQueueCount: number;
    CurrentDownload: {
        Name?: string;
        StageStep?: string;
        Percentage?: number;
        DoneMb?: number;
        TotalMb?: number;
        Speed?: string;
        DetailText?: string;
    };
    StageDisks: {
        Path?: string;
        FreeGb?: number;
        TotalGb?: number;
        FreePercent?: number;
        Formatted?: string;
    }[];
    MaintenanceOperation?: {
        Name?: string;
        State?: string;
        StartedAtUtc?: string;
        FinishedAtUtc?: string;
        DurationMs?: number;
        Error?: string;
        ProgressPercent?: number;
        ProgressText?: string;
    };
};

type NebulaLogs = {
    ServerLogs: string[];
    DownloaderLogs: string[];
};

type NebulaBot = {
    Index: number;
    Name?: string;
    MaskedToken?: string;
    SessionExists?: boolean;
    Enabled?: boolean;
};

type NebulaComponentHealth = {
    MongoConfigured: boolean;
    MongoConnected: boolean;
    MongoStatus: string;
    TelegramConfigured: boolean;
    TelegramReady: boolean;
    TelegramAvailableBots: number;
    FtpListenerRunning: boolean;
    HttpListenerRunning: boolean;
};

const STATUS_QUERY_KEY = [ 'NebulaStatus' ];
const LOGS_QUERY_KEY = [ 'NebulaLogs' ];
const BOTS_QUERY_KEY = [ 'NebulaBots' ];
const HEALTH_QUERY_KEY = [ 'NebulaHealth' ];

const getApiClient = (): ApiClient => {
    const apiClient = ServerConnections.currentApiClient();
    if (!apiClient) {
        throw new Error('Cliente de API indisponível.');
    }

    return apiClient as unknown as ApiClient;
};

const getErrorMessage = (error: unknown): string => {
    const requestError = error as {
        message?: string;
        responseJSON?: { message?: string; Message?: string };
        responseText?: string;
    };

    return requestError.responseJSON?.message
        || requestError.responseJSON?.Message
        || requestError.responseText
        || requestError.message
        || 'erro desconhecido';
};

const operationStateLabel: Record<string, string> = {
    idle: 'Nenhuma operação executada',
    running: 'Em execução',
    succeeded: 'Concluída',
    failed: 'Falhou',
    cancelled: 'Cancelada'
};

const postAction = async <T,>(path: string, body?: unknown, idempotencyKey?: string): Promise<T> => {
    const apiClient = getApiClient();
    return apiClient.ajax({
        type: 'POST',
        url: apiClient.getUrl(path),
        data: body ? JSON.stringify(body) : undefined,
        contentType: 'application/json',
        headers: idempotencyKey ? { 'X-Idempotency-Key': idempotencyKey } : undefined
    }) as Promise<T>;
};

const deleteAction = async <T,>(path: string): Promise<T> => {
    const apiClient = getApiClient();
    return apiClient.ajax({
        type: 'DELETE',
        url: apiClient.getUrl(path)
    }) as Promise<T>;
};

const formatMegabytes = (value = 0) => `${value.toFixed(1)} MB`;

const stateChip = (active: boolean, activeLabel: string, inactiveLabel: string) => (
    <Chip
        label={active ? activeLabel : inactiveLabel}
        color={active ? 'success' : 'default'}
        size='small'
    />
);

const getOperationColor = (state: string): 'success' | 'error' | 'default' => {
    if (state === 'failed') return 'error';
    if (state === 'succeeded' || state === 'running') return 'success';
    return 'default';
};

const getBackupMessage = (result: { Success?: boolean; Message?: string }): string => {
    if (result.Success === false) return `Backup não concluído: ${result.Message || 'erro desconhecido'}`;
    return result.Message || 'Backup concluído.';
};

const getBotDisplayName = (bot: NebulaBot): string => bot.Name || `Bot ${bot.Index + 1}`;

const getBotAriaLabel = (bot: NebulaBot): string => `Remover ${getBotDisplayName(bot)}`;

type NebulaCredentials = {
    Password?: string;
    HttpStreamToken?: string;
    SupabaseKey?: string;
    ApiHash?: string;
};

const useNebulaMutations = () => {
    const actionMutation = useMutation({
        mutationFn: ({ path, body, idempotencyKey }: { path: string; body?: unknown; idempotencyKey?: string }) => postAction<boolean>(path, body, idempotencyKey),
        onSuccess: async result => {
            if (!result) {
                toast('A operação Nebula não foi concluída. Consulte os logs.');
                return;
            }

            await queryClient.invalidateQueries({ queryKey: STATUS_QUERY_KEY });
            await queryClient.invalidateQueries({ queryKey: LOGS_QUERY_KEY });
            await queryClient.invalidateQueries({ queryKey: HEALTH_QUERY_KEY });
            toast('Operação Nebula concluída.');
        },
        onError: error => toast(`Erro na operação Nebula: ${getErrorMessage(error)}`)
    });
    const backupMutation = useMutation({
        mutationFn: (idempotencyKey: string) => postAction('NebulaFtp/Supabase/Backup', undefined, idempotencyKey),
        onSuccess: async result => {
            await queryClient.invalidateQueries({ queryKey: STATUS_QUERY_KEY });
            await queryClient.invalidateQueries({ queryKey: LOGS_QUERY_KEY });
            toast(getBackupMessage(result as { Success?: boolean; Message?: string }));
        },
        onError: error => toast(`Erro no backup: ${getErrorMessage(error)}`)
    });
    const restoreMutation = useMutation({
        mutationFn: (idempotencyKey: string) => postAction('NebulaFtp/Supabase/Restore', undefined, idempotencyKey),
        onSuccess: async result => {
            const typedResult = result as { Success?: boolean; Message?: string };
            if (typedResult.Success === false) {
                toast(`Restore não concluído: ${typedResult.Message || 'erro desconhecido'}`);
                return;
            }

            await queryClient.invalidateQueries({ queryKey: STATUS_QUERY_KEY });
            await queryClient.invalidateQueries({ queryKey: LOGS_QUERY_KEY });
            toast(typedResult.Message || 'Restore concluído.');
        },
        onError: error => toast(`Erro no restore: ${getErrorMessage(error)}`)
    });
    return { actionMutation, backupMutation, restoreMutation };
};

const useNebulaConfigMutations = ({
    setBotName,
    setBotToken,
    setFtpPassword,
    setHttpStreamToken,
    setSupabaseKey,
    setApiHash
}: {
    setBotName: React.Dispatch<React.SetStateAction<string>>;
    setBotToken: React.Dispatch<React.SetStateAction<string>>;
    setFtpPassword: React.Dispatch<React.SetStateAction<string>>;
    setHttpStreamToken: React.Dispatch<React.SetStateAction<string>>;
    setSupabaseKey: React.Dispatch<React.SetStateAction<string>>;
    setApiHash: React.Dispatch<React.SetStateAction<string>>;
}) => {
    const botMutation = useMutation({
        mutationFn: ({ token, name }: { token: string; name?: string }) => postAction<NebulaBot[]>('NebulaFtp/Bots', {
            Token: token,
            Name: name || undefined
        }),
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: BOTS_QUERY_KEY });
            setBotToken('');
            setBotName('');
            toast('Token do bot salvo com segurança.');
        },
        onError: error => toast(`Erro ao salvar bot: ${getErrorMessage(error)}`)
    });
    const deleteBotMutation = useMutation({
        mutationFn: (index: number) => deleteAction<NebulaBot[]>(`NebulaFtp/Bots/${index}`),
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: BOTS_QUERY_KEY });
            toast('Bot removido.');
        },
        onError: error => toast(`Erro ao remover bot: ${getErrorMessage(error)}`)
    });
    const credentialsMutation = useMutation({
        mutationFn: (credentials: NebulaCredentials) => postAction('NebulaFtp/Config/Secrets', credentials),
        onSuccess: () => {
            setFtpPassword('');
            setHttpStreamToken('');
            setSupabaseKey('');
            setApiHash('');
            toast('Credenciais Nebula rotacionadas com segurança.');
        },
        onError: error => toast(`Erro ao rotacionar credenciais: ${getErrorMessage(error)}`)
    });

    return { botMutation, deleteBotMutation, credentialsMutation };
};

const operationChip = (state = 'idle') => (
    <Chip
        label={operationStateLabel[state] || state}
        color={getOperationColor(state)}
        size='small'
    />
);

const ProgressCard = ({
    title,
    icon,
    name,
    detail,
    percentage,
    secondary
}: {
    title: string;
    icon: React.ReactNode;
    name: string;
    detail: string;
    percentage: number;
    secondary?: string;
}) => {
    const normalizedPercentage = Math.max(0, Math.min(100, percentage || 0));

    return (
        <Paper variant='outlined' sx={{ p: 2, flex: 1, minWidth: 280 }}>
            <Stack spacing={1.25}>
                <Stack direction='row' alignItems='center' spacing={1}>
                    {icon}
                    <Typography variant='h2' component='h2' sx={{ fontSize: '1.1rem' }}>
                        {title}
                    </Typography>
                </Stack>
                <Typography variant='body1' noWrap title={name}>{name}</Typography>
                <Typography variant='body2' color='text.secondary'>{detail}</Typography>
                <LinearProgress variant='determinate' value={normalizedPercentage} />
                <Stack direction='row' justifyContent='space-between'>
                    <Typography variant='caption' color='text.secondary'>{secondary || 'Aguardando'}</Typography>
                    <Typography variant='caption'>{normalizedPercentage.toFixed(1)}%</Typography>
                </Stack>
            </Stack>
        </Paper>
    );
};

const HealthContent = ({
    isError,
    error,
    health,
    onRetry
}: {
    isError: boolean;
    error: unknown;
    health?: NebulaComponentHealth;
    onRetry: () => void;
}) => {
    if (isError) {
        return (
            <Alert severity='warning' action={<Button color='inherit' size='small' onClick={onRetry}>Tentar novamente</Button>}>
                Não foi possível consultar os checks individuais: {getErrorMessage(error)}
            </Alert>
        );
    }
    if (!health) return <Typography color='text.secondary'>Consultando componentes...</Typography>;

    return (
        <Stack direction={{ xs: 'column', sm: 'row' }} gap={1} flexWrap='wrap'>
            {stateChip(health.MongoConnected, 'MongoDB conectado', health.MongoConfigured ? 'MongoDB indisponível' : 'MongoDB não configurado')}
            {stateChip(health.TelegramReady, `${health.TelegramAvailableBots} bot(s) disponível(is)`, health.TelegramConfigured ? 'Telegram não pronto' : 'Telegram não configurado')}
            {stateChip(health.FtpListenerRunning, 'Listener FTP ativo', 'Listener FTP parado')}
            {stateChip(health.HttpListenerRunning, 'Listener HTTP ativo', 'Listener HTTP parado')}
            <Typography variant='caption' color='text.secondary' sx={{ alignSelf: 'center' }}>
                {health.MongoStatus}
            </Typography>
        </Stack>
    );
};

const LogsContent = ({
    isLoading,
    isError,
    error,
    logs,
    onRetry
}: {
    isLoading: boolean;
    isError: boolean;
    error: unknown;
    logs?: NebulaLogs;
    onRetry: () => void;
}) => {
    if (isLoading) return <Typography color='text.secondary'>Carregando logs...</Typography>;
    if (isError) {
        return (
            <Alert severity='warning' action={<Button color='inherit' size='small' onClick={onRetry}>Tentar novamente</Button>}>
                Não foi possível carregar os logs: {getErrorMessage(error)}
            </Alert>
        );
    }

    return (
        <Box component='pre' sx={{ maxHeight: 320, overflow: 'auto', m: 0, p: 1.5, bgcolor: 'rgba(0,0,0,.28)', whiteSpace: 'pre-wrap', fontSize: '.78rem' }}>
            {[ ...(logs?.ServerLogs ?? []), ...(logs?.DownloaderLogs ?? []) ].slice(-80).join('\n') || 'Nenhum log disponível.'}
        </Box>
    );
};

const NebulaPage = () => {
    const statusQuery = useQuery({
        queryKey: STATUS_QUERY_KEY,
        queryFn: () => {
            const apiClient = getApiClient();
            return apiClient.getJSON(apiClient.getUrl('NebulaFtp/Status')) as Promise<NebulaStatus>;
        },
        refetchInterval: 3000
    });
    const logsQuery = useQuery({
        queryKey: LOGS_QUERY_KEY,
        queryFn: () => {
            const apiClient = getApiClient();
            return apiClient.getJSON(apiClient.getUrl('NebulaFtp/Logs?serverOffset=0&downloaderOffset=0')) as Promise<NebulaLogs>;
        },
        refetchInterval: 5000
    });
    const botsQuery = useQuery({
        queryKey: BOTS_QUERY_KEY,
        queryFn: () => {
            const apiClient = getApiClient();
            return apiClient.getJSON(apiClient.getUrl('NebulaFtp/Bots')) as Promise<NebulaBot[]>;
        },
        refetchInterval: 10000
    });
    const healthQuery = useQuery({
        queryKey: HEALTH_QUERY_KEY,
        queryFn: () => {
            const apiClient = getApiClient();
            return apiClient.getJSON(apiClient.getUrl('NebulaFtp/Health')) as Promise<NebulaComponentHealth>;
        },
        refetchInterval: 10000
    });

    const retryStatus = useCallback(() => {
        void statusQuery.refetch();
    }, [ statusQuery ]);
    const retryHealth = useCallback(() => {
        void healthQuery.refetch();
    }, [ healthQuery ]);
    const retryBots = useCallback(() => {
        void botsQuery.refetch();
    }, [ botsQuery ]);
    const retryLogs = useCallback(() => {
        void logsQuery.refetch();
    }, [ logsQuery ]);

    const [ activeTab, setActiveTab ] = useState(0);
    const [ isRestoreDialogOpen, setIsRestoreDialogOpen ] = useState(false);
    const [ botName, setBotName ] = useState('');
    const [ botToken, setBotToken ] = useState('');
    const [ ftpPassword, setFtpPassword ] = useState('');
    const [ httpStreamToken, setHttpStreamToken ] = useState('');
    const [ supabaseKey, setSupabaseKey ] = useState('');
    const [ apiHash, setApiHash ] = useState('');

    const {
        actionMutation,
        backupMutation,
        restoreMutation
    } = useNebulaMutations();
    const {
        botMutation,
        deleteBotMutation,
        credentialsMutation
    } = useNebulaConfigMutations({ setBotName, setBotToken, setFtpPassword, setHttpStreamToken, setSupabaseKey, setApiHash });

    const status = statusQuery.data;
    const logs = logsQuery.data;
    const componentHealth = healthQuery.data;
    const activeUploads = useMemo(() => status?.ActiveUploads ?? [], [ status?.ActiveUploads ]);
    const queuedUploads = status?.UploadQueueCount ?? status?.QueuedUploads?.length ?? 0;
    const currentDownload = status?.CurrentDownload;
    const isBusy = actionMutation.isPending
        || backupMutation.isPending
        || restoreMutation.isPending
        || botMutation.isPending
        || deleteBotMutation.isPending
        || credentialsMutation.isPending;

    const runAction = useCallback((path: string, body?: unknown) => {
        const isMaintenanceAction = path.includes('/Actions/GenerateStrm') || path.includes('/Actions/PruneCompleted');
        actionMutation.mutate({ path, body, idempotencyKey: isMaintenanceAction ? crypto.randomUUID() : undefined });
    }, [ actionMutation ]);
    const handleBotSubmit = useCallback((event: React.FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        const token = botToken.trim();
        if (!token) {
            toast('Informe o token do bot.');
            return;
        }

        botMutation.mutate({ token, name: botName.trim() || undefined });
    }, [ botMutation, botName, botToken ]);
    const handleBotDelete = useCallback((index: number) => {
        deleteBotMutation.mutate(index);
    }, [ deleteBotMutation ]);
    const handleCredentialsSubmit = useCallback((event: React.FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        const credentials = {
            Password: ftpPassword.trim() || undefined,
            HttpStreamToken: httpStreamToken.trim() || undefined,
            SupabaseKey: supabaseKey.trim() || undefined,
            ApiHash: apiHash.trim() || undefined
        };
        if (!Object.values(credentials).some(Boolean)) {
            toast('Informe ao menos uma credencial nova.');
            return;
        }

        credentialsMutation.mutate(credentials);
    }, [ apiHash, credentialsMutation, ftpPassword, httpStreamToken, supabaseKey ]);

    const refreshStatus = useCallback(() => {
        void queryClient.invalidateQueries({ queryKey: STATUS_QUERY_KEY });
    }, []);
    const startEnvio = useCallback(() => runAction('NebulaFtp/Actions/StartEnvio', { StreamOnly: false }), [ runAction ]);
    const stopEnvio = useCallback(() => runAction('NebulaFtp/Actions/StopEnvio'), [ runAction ]);
    const startDownloader = useCallback(() => runAction('NebulaFtp/Actions/StartDownloader'), [ runAction ]);
    const stopDownloader = useCallback(() => runAction('NebulaFtp/Actions/StopDownloader'), [ runAction ]);
    const generateStrm = useCallback(() => runAction('NebulaFtp/Actions/GenerateStrm'), [ runAction ]);
    const pruneCompleted = useCallback(() => runAction('NebulaFtp/Actions/PruneCompleted'), [ runAction ]);
    const startBackup = useCallback(() => backupMutation.mutate(crypto.randomUUID()), [ backupMutation ]);
    const openRestoreDialog = useCallback(() => setIsRestoreDialogOpen(true), []);
    const closeRestoreDialog = useCallback(() => setIsRestoreDialogOpen(false), []);
    const confirmRestore = useCallback(() => {
        setIsRestoreDialogOpen(false);
        restoreMutation.mutate(crypto.randomUUID());
    }, [ restoreMutation ]);
    const handleBotNameChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        setBotName(event.target.value);
    }, []);
    const handleBotTokenChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        setBotToken(event.target.value);
    }, []);
    const handleFtpPasswordChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        setFtpPassword(event.target.value);
    }, []);
    const handleHttpStreamTokenChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        setHttpStreamToken(event.target.value);
    }, []);
    const handleSupabaseKeyChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        setSupabaseKey(event.target.value);
    }, []);
    const handleApiHashChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        setApiHash(event.target.value);
    }, []);
    const handleBotDeleteClick = useCallback((event: React.MouseEvent<HTMLButtonElement>) => {
        const index = Number(event.currentTarget.dataset.botIndex);
        if (Number.isInteger(index)) handleBotDelete(index);
    }, [ handleBotDelete ]);

    if (statusQuery.isLoading && !statusQuery.isError) {
        return <Loading />;
    }

    return (
        <Page id='nebulaPage' title='Nebula' className='mainAnimatedPage type-interior'>
            <Stack
                className='nebula-page-content'
                spacing={3}
                sx={{
                    p: { xs: 2, md: 3 },
                    maxWidth: 1580,
                    mx: 'auto',
                    '& .MuiPaper-root': {
                        bgcolor: '#202020',
                        borderColor: '#3a3a3a',
                        borderRadius: '4px',
                        boxShadow: '0 1px 2px rgba(0, 0, 0, .24)'
                    },
                    '& .MuiDivider-root': {
                        borderColor: '#3a3a3a'
                    },
                    '& .MuiTypography-h1': {
                        color: '#f5f5f5',
                        fontWeight: 500,
                        letterSpacing: '-.02em'
                    },
                    '& .MuiTypography-h2': {
                        color: '#f5f5f5',
                        fontWeight: 500
                    },
                    '& .MuiTypography-body1': {
                        color: '#f0f0f0'
                    },
                    '& .MuiTypography-body2, & .MuiTypography-caption': {
                        color: '#b9b9b9'
                    },
                    '& .MuiButton-root': {
                        minHeight: 42,
                        borderRadius: '4px',
                        fontWeight: 500,
                        px: 2
                    },
                    '& .MuiButton-contained': {
                        bgcolor: '#00a4dc',
                        color: '#07151b',
                        '&:hover': { bgcolor: '#33b6e3' }
                    },
                    '& .MuiButton-outlined': {
                        borderColor: '#00a4dc',
                        color: '#00b7ef',
                        '&:hover': {
                            borderColor: '#33b6e3',
                            bgcolor: 'rgba(0, 164, 220, .12)'
                        }
                    },
                    '& .MuiButton-outlined.MuiButton-colorWarning': {
                        borderColor: '#e39a19',
                        color: '#ffab1a',
                        '&:hover': { bgcolor: 'rgba(255, 171, 26, .12)' }
                    },
                    '& .MuiChip-colorSuccess': {
                        bgcolor: '#69c36b',
                        color: '#102313',
                        fontWeight: 500
                    },
                    '& .MuiLinearProgress-root': {
                        height: 5,
                        borderRadius: 0,
                        bgcolor: '#07526c'
                    },
                    '& .MuiLinearProgress-bar': {
                        bgcolor: '#00a4dc'
                    },
                    '& .MuiInputBase-root': {
                        bgcolor: 'rgba(255, 255, 255, .07)',
                        borderRadius: '4px'
                    },
                    '& .MuiInputLabel-root': {
                        color: '#b9b9b9'
                    },
                    '& .MuiInputBase-input': {
                        color: '#fff'
                    }
                }}
            >
                <Stack direction={{ xs: 'column', md: 'row' }} justifyContent='space-between' gap={2}>
                    <Box>
                        <Typography variant='h1'>Operações Nebula</Typography>
                        <Typography color='text.secondary'>Status de envio, downloader, armazenamento e manutenção.</Typography>
                    </Box>
                    <Button
                        variant='outlined'
                        startIcon={<Cached />}
                        onClick={refreshStatus}
                    >
                        Atualizar
                    </Button>
                </Stack>

                {statusQuery.isError && (
                    <Alert
                        severity='error'
                        action={<Button color='inherit' size='small' onClick={retryStatus}>Tentar novamente</Button>}
                    >
                        Não foi possível carregar o status: {getErrorMessage(statusQuery.error)}
                    </Alert>
                )}

                <Tabs
                    value={activeTab}
                    onChange={(_event, val) => setActiveTab(val)}
                    sx={{
                        borderBottom: 1,
                        borderColor: '#3a3a3a',
                        '& .MuiTab-root': {
                            textTransform: 'none',
                            fontWeight: 500,
                            fontSize: '0.95rem',
                            minHeight: 44,
                            color: '#b9b9b9',
                            '&.Mui-selected': {
                                color: '#00a4dc'
                            }
                        },
                        '& .MuiTabs-indicator': {
                            bgcolor: '#00a4dc'
                        }
                    }}
                >
                    <Tab label='Geral' />
                    <Tab
                        label={
                            <Stack direction='row' alignItems='center' spacing={1}>
                                <span>Bots e Tokens</span>
                                {(botsQuery.data?.length ?? 0) > 0 && (
                                    <Chip
                                        size='small'
                                        label={botsQuery.data?.length}
                                        sx={{
                                            height: 20,
                                            fontSize: '0.75rem',
                                            bgcolor: activeTab === 1 ? '#00a4dc' : 'rgba(255,255,255,0.12)',
                                            color: activeTab === 1 ? '#07151b' : '#f5f5f5'
                                        }}
                                    />
                                )}
                            </Stack>
                        }
                    />
                </Tabs>

                {activeTab === 0 && status && (
                    <>
                        <Paper variant='outlined' sx={{ p: 2 }}>
                            <Stack spacing={1.5}>
                                <Stack direction={{ xs: 'column', md: 'row' }} gap={1} alignItems={{ md: 'center' }}>
                                    <Typography variant='h2' component='h2' sx={{ flex: 1, fontSize: '1.2rem' }}>
                                        {status.GlobalStatusText || 'Status indisponível'}
                                    </Typography>
                                    {stateChip(status.IsEnvioRunning, 'Envio ativo', 'Envio parado')}
                                    {stateChip(status.IsDownloaderRunning, 'Downloader ativo', 'Downloader parado')}
                                    {stateChip(status.IsDriveNMounted, 'Disco N montado', 'Disco N desconectado')}
                                </Stack>
                                <Divider />
                                <Stack direction={{ xs: 'column', sm: 'row' }} gap={1} flexWrap='wrap'>
                                    <Button
                                        variant='contained'
                                        startIcon={<PlayArrow />}
                                        disabled={isBusy || status.IsEnvioRunning}
                                        aria-busy={actionMutation.isPending}
                                        onClick={startEnvio}
                                    >
                                        {actionMutation.isPending ? 'Iniciando...' : 'Iniciar envio'}
                                    </Button>
                                    <Button
                                        variant='outlined'
                                        color='warning'
                                        startIcon={<Stop />}
                                        disabled={isBusy || !status.IsEnvioRunning}
                                        onClick={stopEnvio}
                                    >
                                        Parar envio
                                    </Button>
                                    <Button
                                        variant='contained'
                                        startIcon={<Download />}
                                        disabled={isBusy || status.IsDownloaderRunning}
                                        aria-busy={actionMutation.isPending}
                                        onClick={startDownloader}
                                    >
                                        {actionMutation.isPending ? 'Iniciando...' : 'Iniciar downloader'}
                                    </Button>
                                    <Button
                                        variant='outlined'
                                        color='warning'
                                        startIcon={<Stop />}
                                        disabled={isBusy || !status.IsDownloaderRunning}
                                        onClick={stopDownloader}
                                    >
                                        Parar downloader
                                    </Button>
                                </Stack>
                            </Stack>
                        </Paper>

                        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
                            <ProgressCard
                                title='Download atual'
                                icon={<CloudDownload color='primary' />}
                                name={currentDownload?.Name || 'Nenhum download em andamento'}
                                detail={currentDownload?.StageStep || 'Aguardando'}
                                percentage={currentDownload?.Percentage || 0}
                                secondary={currentDownload?.Speed || formatMegabytes(currentDownload?.DoneMb)}
                            />
                            <ProgressCard
                                title='Uploads ativos'
                                icon={<CloudUpload color='primary' />}
                                name={`${activeUploads.length} upload(s) ativo(s)`}
                                detail={`${queuedUploads} item(ns) na fila`}
                                percentage={activeUploads[0]?.Percentage || 0}
                                secondary={activeUploads[0]?.InfoText || 'Fila monitorada pelo servidor'}
                            />
                        </Stack>

                        <Paper variant='outlined' sx={{ p: 2 }}>
                            <Stack spacing={1.5}>
                                <Box>
                                    <Typography variant='h2' component='h2' sx={{ fontSize: '1.2rem' }}>Saúde dos componentes</Typography>
                                    <Typography variant='body2' color='text.secondary'>Checks individuais sem expor credenciais ou URLs sensíveis.</Typography>
                                </Box>
                                <HealthContent isError={healthQuery.isError} error={healthQuery.error} health={componentHealth} onRetry={retryHealth} />
                            </Stack>
                        </Paper>

                        <Paper variant='outlined' sx={{ p: 2 }}>
                            <Stack spacing={1.5}>
                                <Box>
                                    <Typography variant='h2' component='h2' sx={{ fontSize: '1.2rem' }}>Rotação de credenciais</Typography>
                                    <Typography variant='body2' color='text.secondary'>Os campos nunca são preenchidos pelo servidor. Envie somente os segredos que deseja substituir.</Typography>
                                </Box>
                                <Box component='form' onSubmit={handleCredentialsSubmit}>
                                    <Stack spacing={1}>
                                        <TextField label='Nova senha FTP' type='password' value={ftpPassword} onChange={handleFtpPasswordChange} autoComplete='new-password' size='small' fullWidth />
                                        <TextField label='Novo token HTTP' type='password' value={httpStreamToken} onChange={handleHttpStreamTokenChange} autoComplete='new-password' size='small' fullWidth />
                                        <TextField label='Nova chave Supabase' type='password' value={supabaseKey} onChange={handleSupabaseKeyChange} autoComplete='new-password' size='small' fullWidth />
                                        <TextField label='Novo API hash do Telegram (32 caracteres)' type='password' value={apiHash} onChange={handleApiHashChange} autoComplete='new-password' size='small' fullWidth slotProps={{ htmlInput: { maxLength: 32 } }} />
                                        <Button type='submit' variant='outlined' disabled={isBusy} sx={{ alignSelf: 'flex-start' }}>
                                            {credentialsMutation.isPending ? 'Rotacionando...' : 'Rotacionar credenciais'}
                                        </Button>
                                    </Stack>
                                </Box>
                            </Stack>
                        </Paper>

                        <Paper variant='outlined' sx={{ p: 2 }}>
                            <Stack spacing={1}>
                                <Typography variant='h2' component='h2' sx={{ fontSize: '1.2rem' }}>Última operação de manutenção</Typography>
                                <Stack direction={{ xs: 'column', sm: 'row' }} gap={1} alignItems={{ sm: 'center' }}>
                                    <Typography sx={{ flex: 1 }}>
                                        {status.MaintenanceOperation?.Name || 'Nenhuma operação registrada'}
                                    </Typography>
                                    {operationChip(status.MaintenanceOperation?.State)}
                                    {status.MaintenanceOperation?.DurationMs != null && (
                                        <Typography variant='caption' color='text.secondary'>
                                            {Math.round(status.MaintenanceOperation.DurationMs / 1000)}s
                                        </Typography>
                                    )}
                                </Stack>
                                {!!status.MaintenanceOperation?.ProgressText && (
                                    <Typography variant='caption' color='text.secondary' noWrap title={status.MaintenanceOperation.ProgressText}>
                                        {status.MaintenanceOperation.ProgressText}
                                    </Typography>
                                )}
                                {status.MaintenanceOperation?.ProgressPercent != null && status.MaintenanceOperation.State === 'running' && (
                                    <LinearProgress variant='determinate' value={Math.max(0, Math.min(100, status.MaintenanceOperation.ProgressPercent))} />
                                )}
                                {!!status.MaintenanceOperation?.Error && (
                                    <Alert severity='error'>{status.MaintenanceOperation.Error}</Alert>
                                )}
                            </Stack>
                        </Paper>

                        <Paper variant='outlined' sx={{ p: 2 }}>
                            <Stack spacing={1.5}>
                                <Stack direction='row' alignItems='center' spacing={1}>
                                    <Cloud color='primary' />
                                    <Typography variant='h2' component='h2' sx={{ fontSize: '1.2rem' }}>Manutenção</Typography>
                                </Stack>
                                <Stack direction={{ xs: 'column', sm: 'row' }} gap={1} flexWrap='wrap'>
                                    <Button variant='outlined' startIcon={<PlayArrow />} disabled={isBusy} onClick={generateStrm}>
                                        Gerar STRM
                                    </Button>
                                    <Button variant='outlined' startIcon={<DeleteSweep />} disabled={isBusy} onClick={pruneCompleted}>
                                        Limpar concluídos
                                    </Button>
                                    <Button
                                        variant='outlined'
                                        startIcon={<CloudUpload />}
                                        disabled={isBusy}
                                        aria-busy={backupMutation.isPending}
                                        onClick={startBackup}
                                    >
                                        {backupMutation.isPending ? 'Executando backup...' : 'Backup Supabase'}
                                    </Button>
                                    <Button
                                        variant='outlined'
                                        startIcon={<Restore />}
                                        disabled={isBusy}
                                        aria-busy={restoreMutation.isPending}
                                        onClick={openRestoreDialog}
                                    >
                                        {restoreMutation.isPending ? 'Restaurando...' : 'Restore Supabase'}
                                    </Button>
                                </Stack>
                                      </Stack>
                        </Paper>

                        <Paper variant='outlined' sx={{ p: 2 }}>
                            <Typography variant='h2' component='h2' sx={{ fontSize: '1.2rem', mb: 1 }}>Discos de stage</Typography>
                            {status.StageDisks.length === 0 ? (
                                <Typography color='text.secondary'>Nenhum disco de stage informado.</Typography>
                            ) : (
                                <Stack spacing={1}>
                                    {status.StageDisks.map(disk => (
                                        <Stack key={disk.Path} direction={{ xs: 'column', sm: 'row' }} justifyContent='space-between' gap={1}>
                                            <Typography>{disk.Formatted || disk.Path}</Typography>
                                            <Typography color='text.secondary'>{disk.FreePercent ?? 0}% livre</Typography>
                                        </Stack>
                                    ))}
                                </Stack>
                            )}
                        </Paper>

                        <Paper variant='outlined' sx={{ p: 2 }}>
                            <Typography variant='h2' component='h2' sx={{ fontSize: '1.2rem', mb: 1 }}>Logs recentes</Typography>
                            <LogsContent isLoading={logsQuery.isLoading} isError={logsQuery.isError} error={logsQuery.error} logs={logs} onRetry={retryLogs} />
                        </Paper>
                    </>
                )}

                {activeTab === 1 && (
                    <Stack spacing={3}>
                        <Paper variant='outlined' sx={{ p: 2.5 }}>
                            <Stack spacing={2}>
                                <Box>
                                    <Stack direction='row' alignItems='center' spacing={1} sx={{ mb: 0.5 }}>
                                        <VpnKey color='primary' />
                                        <Typography variant='h2' component='h2' sx={{ fontSize: '1.2rem' }}>
                                            Novo Bot / Token
                                        </Typography>
                                    </Stack>
                                    <Typography variant='body2' color='text.secondary'>
                                        Tokens nunca são exibidos novamente após salvos. Cadastrar um bot existente rotaciona o segredo de sessão no servidor.
                                    </Typography>
                                </Box>

                                <Box component='form' onSubmit={handleBotSubmit}>
                                    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} alignItems={{ sm: 'flex-start' }}>
                                        <TextField
                                            label='Nome do Bot (opcional)'
                                            placeholder='Ex: Nebula_Bot_1'
                                            value={botName}
                                            onChange={handleBotNameChange}
                                            size='small'
                                            sx={{ flex: { xs: '1 1 auto', sm: '1 1 35%' } }}
                                        />
                                        <TextField
                                            label='Token do Bot no Telegram'
                                            placeholder='123456789:ABCdefGhIJKlmNoPQRsTUVwxyZ'
                                            type='password'
                                            value={botToken}
                                            onChange={handleBotTokenChange}
                                            size='small'
                                            required
                                            autoComplete='new-password'
                                            sx={{ flex: { xs: '1 1 auto', sm: '1 1 65%' } }}
                                        />
                                        <Button
                                            type='submit'
                                            variant='contained'
                                            disabled={isBusy}
                                            startIcon={<VpnKey />}
                                            sx={{ minWidth: 140, whiteSpace: 'nowrap' }}
                                        >
                                            {botMutation.isPending ? 'Salvando...' : 'Salvar Token'}
                                        </Button>
                                    </Stack>
                                </Box>
                            </Stack>
                        </Paper>

                        {botsQuery.isError && (
                            <Alert
                                severity='warning'
                                action={<Button color='inherit' size='small' onClick={retryBots}>Tentar novamente</Button>}
                            >
                                Não foi possível carregar os bots: {getErrorMessage(botsQuery.error)}
                            </Alert>
                        )}

                        <Box>
                            <Stack direction='row' justifyContent='space-between' alignItems='center' sx={{ mb: 2 }}>
                                <Typography variant='h2' component='h2' sx={{ fontSize: '1.15rem' }}>
                                    Bots Configurados ({(botsQuery.data ?? []).length})
                                </Typography>
                                <Button
                                    size='small'
                                    variant='outlined'
                                    startIcon={<Cached />}
                                    onClick={retryBots}
                                    disabled={botsQuery.isFetching}
                                >
                                    Atualizar lista
                                </Button>
                            </Stack>

                            {(botsQuery.data ?? []).length > 0 ? (
                                <Box
                                    sx={{
                                        display: 'grid',
                                        gridTemplateColumns: {
                                            xs: '1fr',
                                            sm: 'repeat(auto-fill, minmax(320px, 1fr))'
                                        },
                                        gap: 2
                                    }}
                                >
                                    {(botsQuery.data ?? []).map(bot => {
                                        const botDisplayName = bot.Name || `Bot ${bot.Index + 1}`;
                                        const isSessionActive = Boolean(bot.SessionExists);

                                        return (
                                            <Paper
                                                key={bot.Index}
                                                variant='outlined'
                                                sx={{
                                                    p: 2,
                                                    display: 'flex',
                                                    flexDirection: 'column',
                                                    justifyContent: 'space-between',
                                                    gap: 1.5,
                                                    bgcolor: '#252525 !important',
                                                    borderColor: isSessionActive ? 'rgba(0, 164, 220, 0.4) !important' : '#3a3a3a !important',
                                                    transition: 'transform 0.15s ease, border-color 0.15s ease, box-shadow 0.15s ease',
                                                    '&:hover': {
                                                        transform: 'translateY(-2px)',
                                                        borderColor: '#00a4dc !important',
                                                        boxShadow: '0 4px 12px rgba(0, 0, 0, .45)'
                                                    }
                                                }}
                                            >
                                                <Stack direction='row' justifyContent='space-between' alignItems='flex-start'>
                                                    <Stack direction='row' spacing={1.5} alignItems='center' sx={{ minWidth: 0, flex: 1, pr: 1 }}>
                                                        <Box
                                                            sx={{
                                                                width: 40,
                                                                height: 40,
                                                                borderRadius: '8px',
                                                                bgcolor: isSessionActive ? 'rgba(0, 164, 220, 0.16)' : 'rgba(255, 255, 255, 0.05)',
                                                                display: 'flex',
                                                                alignItems: 'center',
                                                                justifyContent: 'center',
                                                                flexShrink: 0
                                                            }}
                                                        >
                                                            <SmartToy sx={{ color: isSessionActive ? '#00b7ef' : '#888' }} />
                                                        </Box>
                                                        <Box sx={{ minWidth: 0 }}>
                                                            <Typography
                                                                variant='body1'
                                                                fontWeight={600}
                                                                noWrap
                                                                title={botDisplayName}
                                                            >
                                                                {botDisplayName}
                                                            </Typography>
                                                            <Typography variant='caption' color='text.secondary'>
                                                                Índice #{bot.Index}
                                                            </Typography>
                                                        </Box>
                                                    </Stack>

                                                    <IconButton
                                                        aria-label={getBotAriaLabel(bot)}
                                                        color='error'
                                                        disabled={isBusy}
                                                        data-bot-index={bot.Index}
                                                        onClick={handleBotDeleteClick}
                                                        size='small'
                                                        sx={{
                                                            color: '#ff6b6b',
                                                            '&:hover': { bgcolor: 'rgba(255, 77, 77, 0.12)' }
                                                        }}
                                                    >
                                                        <Delete fontSize='small' />
                                                    </IconButton>
                                                </Stack>

                                                <Divider sx={{ my: 0.5 }} />

                                                <Stack spacing={1}>
                                                    <Box
                                                        sx={{
                                                            p: 1,
                                                            borderRadius: '4px',
                                                            bgcolor: 'rgba(0, 0, 0, 0.35)',
                                                            fontFamily: 'monospace',
                                                            fontSize: '0.82rem',
                                                            color: '#e0e0e0',
                                                            display: 'flex',
                                                            alignItems: 'center',
                                                            gap: 1
                                                        }}
                                                    >
                                                        <VpnKey sx={{ fontSize: '0.95rem', color: '#888' }} />
                                                        <span style={{ letterSpacing: '0.05em' }}>
                                                            {bot.MaskedToken || '••••••••••••••••'}
                                                        </span>
                                                    </Box>

                                                    <Stack direction='row' justifyContent='space-between' alignItems='center'>
                                                        <Chip
                                                            size='small'
                                                            label={isSessionActive ? 'Sessão ativa' : 'Sessão pendente'}
                                                            sx={{
                                                                fontWeight: 500,
                                                                fontSize: '0.75rem',
                                                                bgcolor: isSessionActive ? 'rgba(105, 195, 107, 0.2)' : 'rgba(255, 171, 26, 0.15)',
                                                                color: isSessionActive ? '#69c36b' : '#ffab1a',
                                                                border: '1px solid',
                                                                borderColor: isSessionActive ? 'rgba(105, 195, 107, 0.4)' : 'rgba(255, 171, 26, 0.3)'
                                                            }}
                                                        />
                                                        <Typography variant='caption' color='text.secondary'>
                                                            Telegram Bot
                                                        </Typography>
                                                    </Stack>
                                                </Stack>
                                            </Paper>
                                        );
                                    })}
                                </Box>
                            ) : (
                                <Paper variant='outlined' sx={{ p: 4, textAlign: 'center' }}>
                                    <SmartToy sx={{ fontSize: 48, color: '#666', mb: 1 }} />
                                    <Typography variant='body1' color='text.secondary'>
                                        Nenhum bot configurado até o momento.
                                    </Typography>
                                    <Typography variant='caption' color='text.secondary'>
                                        Utilize o formulário acima para adicionar os tokens dos seus bots do Telegram.
                                    </Typography>
                                </Paper>
                            )}
                        </Box>
                    </Stack>
                )}
            </Stack>
            <ConfirmDialog
                open={isRestoreDialogOpen}
                title='Confirmar restore do Supabase'
                text='O restore pode substituir dados locais do Nebula. Deseja continuar?'
                confirmButtonColor='warning'
                confirmButtonText='Restaurar'
                onCancel={closeRestoreDialog}
                onConfirm={confirmRestore}
            />
        </Page>
    );
};

export default NebulaPage;
