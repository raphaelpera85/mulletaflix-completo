import Cached from '@mui/icons-material/Cached';
import Cloud from '@mui/icons-material/Cloud';
import CloudDownload from '@mui/icons-material/CloudDownload';
import CloudUpload from '@mui/icons-material/CloudUpload';
import DeleteSweep from '@mui/icons-material/DeleteSweep';
import Delete from '@mui/icons-material/Delete';
import Download from '@mui/icons-material/Download';
import PlayArrow from '@mui/icons-material/PlayArrow';
import Restore from '@mui/icons-material/Restore';
import Replay from '@mui/icons-material/Replay';
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
import { getUploadQueueCountPresentation } from './queueCountPresentation';

type NebulaWorker = {
    Name?: string;
    DisplayName?: string;
    Status?: string;
    WorkerId?: string;
    Percentage?: number;
    UploadedBytes?: number;
    Size?: number;
    InfoText?: string;
    QueuePosition?: number;
    IsPriority?: boolean;
    PriorityReason?: string;
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
    UploadQueueSnapshotAvailable?: boolean;
    CurrentDownload: {
        Name?: string;
        StageStep?: string;
        Percentage?: number;
        DoneMb?: number;
        TotalMb?: number;
        Speed?: string;
        DetailText?: string;
        QueuePosition?: number;
        QueueCount?: number;
        PriorityReason?: string;
        NextItemName?: string;
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

type NebulaDatabaseHealth = {
    available: boolean;
    healthy: boolean;
    status: string;
};

type NebulaPlaybackCacheStatus = {
    isAvailable: boolean;
    formattedSize: string;
    cachedFilesCount: number;
    activeLeasesCount: number;
    activePrefetchCount: number;
    queuedPrefetchCount: number;
    freeSpaceGb: number;
    totalSpaceGb: number;
    maxCacheSizeBytes: number;
    totalSizeBytes: number;
};

type NebulaUploadQueueSummary = {
    isAvailable: boolean;
    windowStartUtc: string;
    pendingCount: number;
    retryCount: number;
    oldestPendingName: string;
    oldestPendingAtUtc: string | null;
    completedCountLastHour: number;
    uploadedBytesLastHour: number;
    recentFailureCount: number;
    failuresByStage: { stage: string; count: number }[];
};

type NebulaFailedUpload = {
    id: string;
    name: string;
    status: string;
    failureReason: string;
    failureStage: string;
    retryCount: number;
    failedAtUtc: string | null;
    sourceAvailable: boolean;
};

type NebulaFailedUploadRetryResult = {
    success: boolean;
    message: string;
};

type NebulaCancellableUpload = {
    id: string;
    name: string;
    status: string;
    uploadedBytes: number;
    totalBytes: number;
    cancellationRequested: boolean;
};

type NebulaUploadCancellationResult = {
    success: boolean;
    cancellationPending: boolean;
    message: string;
};

const STATUS_QUERY_KEY = [ 'NebulaStatus' ];
const LOGS_QUERY_KEY = [ 'NebulaLogs' ];
const BOTS_QUERY_KEY = [ 'NebulaBots' ];
const HEALTH_QUERY_KEY = [ 'NebulaHealth' ];
const DATABASE_HEALTH_QUERY_KEY = [ 'NebulaDatabaseHealth' ];
const PLAYBACK_CACHE_QUERY_KEY = [ 'NebulaPlaybackCacheStatus' ];
const UPLOAD_QUEUE_SUMMARY_QUERY_KEY = [ 'NebulaUploadQueueSummary' ];
const TERMINAL_FAILED_UPLOADS_QUERY_KEY = [ 'NebulaTerminalFailedUploads' ];
const CANCELLABLE_UPLOADS_QUERY_KEY = [ 'NebulaCancellableUploads' ];

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

const formatBytes = (value: number) => {
    if (!Number.isFinite(value) || value < 0) return '—';
    if (value < 1024) return `${value} B`;
    const units = [ 'KB', 'MB', 'GB', 'TB' ];
    let scaled = value / 1024;
    let unitIndex = 0;
    while (scaled >= 1024 && unitIndex < units.length - 1) {
        scaled /= 1024;
        unitIndex++;
    }
    return `${scaled.toFixed(1)} ${units[unitIndex]}`;
};

const uploadFailureStageLabel = new Map<string, string>([
    [ 'telegram_availability', 'Telegram indisponível' ],
    [ 'telegram_transfer', 'Transferência ao Telegram' ],
    [ 'upload_integrity', 'Integridade do upload' ],
    [ 'unexpected_error', 'Erro inesperado' ],
    [ 'unknown', 'Etapa não identificada' ]
]);

const getUploadStateLabel = (status: string): string => {
    switch (status) {
        case 'uploading': return 'Enviando';
        case 'staging': return 'Aguardando arquivo';
        default: return 'Na fila';
    }
};

const getTerminalActionLabel = (upload: NebulaFailedUpload): string => {
    if (!upload.sourceAvailable) return 'Arquivo de origem ausente';
    return upload.status === 'cancelled' ? 'Retomar envio' : 'Reprocessar';
};

const DatabaseHealthContent = ({
    health,
    isError,
    error,
    onRetry
}: {
    health?: NebulaDatabaseHealth;
    isError: boolean;
    error: unknown;
    onRetry: () => void;
}) => {
    if (isError) {
        return (
            <Alert severity='warning' action={<Button color='inherit' size='small' onClick={onRetry}>Tentar novamente</Button>}>
                Não foi possível consultar o MariaDB: {getErrorMessage(error)}
            </Alert>
        );
    }

    if (!health) return <Typography variant='body2' color='text.secondary'>Consultando MariaDB...</Typography>;

    const availabilityLabel = health.available ? 'MariaDB indisponível' : 'MariaDB sem check';
    return (
        <Stack direction='row' alignItems='center' spacing={1}>
            {stateChip(health.available && health.healthy, 'MariaDB conectado', availabilityLabel)}
            <Typography variant='caption' color='text.secondary'>{health.status}</Typography>
        </Stack>
    );
};

const UploadQueueSummaryContent = ({
    summary,
    isError,
    error,
    onRetry
}: {
    summary?: NebulaUploadQueueSummary;
    isError: boolean;
    error: unknown;
    onRetry: () => void;
}) => {
    if (isError) {
        return (
            <Alert severity='warning' action={<Button color='inherit' size='small' onClick={onRetry}>Tentar novamente</Button>}>
                Não foi possível consultar a fila: {getErrorMessage(error)}
            </Alert>
        );
    }

    if (!summary) return <Typography color='text.secondary'>Consultando fila...</Typography>;
    if (!summary.isAvailable) return <Alert severity='info'>Resumo indisponível: MongoDB ainda não está conectado.</Alert>;

    return (
        <Stack direction={{ xs: 'column', sm: 'row' }} gap={2} flexWrap='wrap'>
            <Typography>{summary.pendingCount} item(ns) pendente(s)</Typography>
            <Typography>{summary.retryCount} item(ns) aguardando retry</Typography>
            <Typography color='text.secondary'>
                Mais antigo: {summary.oldestPendingName || 'nenhum'}
                {summary.oldestPendingAtUtc && ` · ${new Date(summary.oldestPendingAtUtc).toLocaleString()}`}
            </Typography>
            <Divider flexItem sx={{ width: '100%' }} />
            <Typography variant='caption' color='text.secondary'>Atividade nos últimos 60 minutos</Typography>
            <Typography>{summary.completedCountLastHour} upload(s) concluído(s) · {formatBytes(summary.uploadedBytesLastHour)} enviados</Typography>
            <Typography>{summary.recentFailureCount} mídia(s) com falha recente</Typography>
            {summary.failuresByStage.length > 0 ? (
                <Stack direction={{ xs: 'column', sm: 'row' }} gap={1} flexWrap='wrap'>
                    {summary.failuresByStage.map(failure => (
                        <Chip
                            key={failure.stage}
                            size='small'
                            color='warning'
                            label={`${uploadFailureStageLabel.get(failure.stage) || 'Outras etapas'}: ${failure.count}`}
                        />
                    ))}
                </Stack>
            ) : (
                <Typography variant='body2' color='text.secondary'>Nenhuma falha registrada nessa janela.</Typography>
            )}
        </Stack>
    );
};

const EffectiveUploadQueue = ({ status }: { status: NebulaStatus }) => {
    if (!status.UploadQueueSnapshotAvailable) return null;
    const nextUpload = status.QueuedUploads[0];

    return (
        <Box>
            <Typography variant='h3' component='h3' sx={{ fontSize: '1rem', mb: 0.5 }}>Próximos uploads — ordem efetiva</Typography>
            <Typography variant='body2' color='text.secondary' sx={{ mb: 1 }}>
                Ordem do snapshot atual dos workers; itens ainda aguardando admissão ficam contabilizados no resumo MongoDB.
            </Typography>
            {status.QueuedUploads.length > 0 ? (
                <Stack spacing={0.75}>
                    {status.QueuedUploads.slice(0, 5).map(upload => (
                        <Stack key={`${upload.QueuePosition}-${upload.Name}`} direction={{ xs: 'column', sm: 'row' }} justifyContent='space-between' alignItems={{ xs: 'stretch', sm: 'center' }} gap={0.75}>
                            <Typography sx={{ overflowWrap: 'anywhere' }}>
                                {upload.QueuePosition}. {upload.DisplayName || upload.Name || 'Mídia sem nome'}
                            </Typography>
                            <Chip
                                size='small'
                                color={upload.IsPriority ? 'primary' : 'default'}
                                label={upload.PriorityReason || 'Fila padrão'}
                            />
                        </Stack>
                    ))}
                </Stack>
            ) : (
                <Typography variant='body2' color='text.secondary'>Nenhum upload está carregado na fila dos workers.</Typography>
            )}
            {nextUpload && (
                <Typography variant='body2' sx={{ mt: 1 }}>Próximo item: {nextUpload.DisplayName || nextUpload.Name}</Typography>
            )}
        </Box>
    );
};

const CancellableUploadsContent = ({
    uploads,
    isLoading,
    isError,
    error,
    isCancelling,
    onCancel
}: {
    uploads?: NebulaCancellableUpload[];
    isLoading: boolean;
    isError: boolean;
    error: unknown;
    isCancelling: boolean;
    onCancel: (event: React.MouseEvent<HTMLButtonElement>) => void;
}) => {
    if (isError) return <Alert severity='warning'>Não foi possível consultar uploads canceláveis: {getErrorMessage(error)}</Alert>;
    if (isLoading) return <Typography color='text.secondary'>Consultando uploads...</Typography>;
    if (!uploads?.length) return <Typography variant='body2' color='text.secondary'>Nenhum upload pendente ou em andamento.</Typography>;

    return (
        <Stack spacing={1}>
            {uploads.map(upload => (
                <Paper key={upload.id} variant='outlined' sx={{ p: 1.5 }}>
                    <Stack direction={{ xs: 'column', md: 'row' }} justifyContent='space-between' alignItems={{ xs: 'stretch', md: 'center' }} gap={1.5}>
                        <Box sx={{ minWidth: 0 }}>
                            <Typography fontWeight={600} sx={{ overflowWrap: 'anywhere' }}>{upload.name}</Typography>
                            <Typography variant='body2' color='text.secondary'>
                                {getUploadStateLabel(upload.status)}
                                {upload.totalBytes > 0 && ` · ${Math.round(upload.uploadedBytes / upload.totalBytes * 100)}% concluído`}
                            </Typography>
                        </Box>
                        <Button
                            variant='outlined'
                            color='warning'
                            startIcon={<Stop />}
                            disabled={upload.cancellationRequested || isCancelling}
                            data-upload-id={upload.id}
                            onClick={onCancel}
                        >
                            {upload.cancellationRequested ? 'Cancelamento pendente' : 'Cancelar upload'}
                        </Button>
                    </Stack>
                </Paper>
            ))}
        </Stack>
    );
};

const FailedUploadsContent = ({
    uploads,
    isLoading,
    isError,
    error,
    isRetrying,
    onRetry
}: {
    uploads?: NebulaFailedUpload[];
    isLoading: boolean;
    isError: boolean;
    error: unknown;
    isRetrying: boolean;
    onRetry: (event: React.MouseEvent<HTMLButtonElement>) => void;
}) => {
    if (isError) return <Alert severity='warning'>Não foi possível consultar falhas terminais: {getErrorMessage(error)}</Alert>;
    if (isLoading) return <Typography color='text.secondary'>Consultando falhas terminais...</Typography>;
    if (!uploads?.length) return <Typography variant='body2' color='text.secondary'>Nenhum upload em falha terminal.</Typography>;

    return (
        <Stack spacing={1}>
            {uploads.map(upload => (
                <Paper key={upload.id} variant='outlined' sx={{ p: 1.5 }}>
                    <Stack direction={{ xs: 'column', md: 'row' }} justifyContent='space-between' alignItems={{ xs: 'stretch', md: 'center' }} gap={1.5}>
                        <Box sx={{ minWidth: 0 }}>
                            <Typography fontWeight={600} sx={{ overflowWrap: 'anywhere' }}>{upload.name}</Typography>
                            <Typography variant='body2' color='text.secondary'>
                                {upload.failureReason || 'Falha sem detalhe'} · {upload.retryCount} tentativas
                                {upload.failedAtUtc && ` · ${new Date(upload.failedAtUtc).toLocaleString()}`}
                            </Typography>
                            <Chip
                                size='small'
                                color={upload.status === 'cancelled' ? 'default' : 'warning'}
                                label={upload.status === 'cancelled' ? 'Cancelado' : uploadFailureStageLabel.get(upload.failureStage) || 'Outras etapas'}
                                sx={{ mt: 0.75 }}
                            />
                        </Box>
                        <Button
                            variant='outlined'
                            startIcon={<Replay />}
                            disabled={!upload.sourceAvailable || isRetrying}
                            data-upload-id={upload.id}
                            onClick={onRetry}
                        >
                            {getTerminalActionLabel(upload)}
                        </Button>
                    </Stack>
                </Paper>
            ))}
        </Stack>
    );
};

const PlaybackCacheContent = ({
    cache,
    isError,
    error,
    onRetry
}: {
    cache?: NebulaPlaybackCacheStatus;
    isError: boolean;
    error: unknown;
    onRetry: () => void;
}) => {
    if (isError) {
        return (
            <Alert severity='warning' action={<Button color='inherit' size='small' onClick={onRetry}>Tentar novamente</Button>}>
                Não foi possível consultar o cache: {getErrorMessage(error)}
            </Alert>
        );
    }

    if (!cache) return <Typography color='text.secondary'>Consultando cache...</Typography>;
    if (!cache.isAvailable) return <Alert severity='info'>O componente de cache não está inicializado; métricas de ocupação ainda não estão disponíveis.</Alert>;

    const usagePercent = cache.maxCacheSizeBytes > 0 ?
        Math.min(100, cache.totalSizeBytes / cache.maxCacheSizeBytes * 100) :
        0;
    return (
        <Stack spacing={1}>
            <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent='space-between' gap={1}>
                <Typography>{cache.formattedSize} em {cache.cachedFilesCount} arquivo(s)</Typography>
                <Typography color='text.secondary'>{cache.activeLeasesCount} reprodução(ões) protegida(s)</Typography>
            </Stack>
            <LinearProgress variant='determinate' value={usagePercent} aria-label='Uso da cota do cache de reprodução' />
            <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent='space-between' gap={1}>
                <Typography variant='caption' color='text.secondary'>
                    {cache.freeSpaceGb.toFixed(1)} GB livres de {cache.totalSpaceGb.toFixed(1)} GB no volume
                </Typography>
                <Typography variant='caption' color='text.secondary'>
                    Pré-cache: {cache.activePrefetchCount} ativo(s), {cache.queuedPrefetchCount} na fila
                </Typography>
            </Stack>
        </Stack>
    );
};

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
    const databaseHealthQuery = useQuery({
        queryKey: DATABASE_HEALTH_QUERY_KEY,
        queryFn: () => {
            const apiClient = getApiClient();
            return apiClient.getJSON(apiClient.getUrl('NebulaFtp/DatabaseHealth')) as Promise<NebulaDatabaseHealth>;
        },
        refetchInterval: 30000
    });
    const playbackCacheQuery = useQuery({
        queryKey: PLAYBACK_CACHE_QUERY_KEY,
        queryFn: () => {
            const apiClient = getApiClient();
            return apiClient.getJSON(apiClient.getUrl('NebulaFtp/PlaybackCache')) as Promise<NebulaPlaybackCacheStatus>;
        },
        // Cache occupancy is maintained incrementally; poll alongside operational summaries.
        refetchInterval: 30000
    });
    const uploadQueueSummaryQuery = useQuery({
        queryKey: UPLOAD_QUEUE_SUMMARY_QUERY_KEY,
        queryFn: () => {
            const apiClient = getApiClient();
            return apiClient.getJSON(apiClient.getUrl('NebulaFtp/UploadQueueSummary')) as Promise<NebulaUploadQueueSummary>;
        },
        refetchInterval: 30000
    });
    const terminalFailedUploadsQuery = useQuery({
        queryKey: TERMINAL_FAILED_UPLOADS_QUERY_KEY,
        queryFn: () => {
            const apiClient = getApiClient();
            return apiClient.getJSON(apiClient.getUrl('NebulaFtp/FailedUploads')) as Promise<NebulaFailedUpload[]>;
        },
        refetchInterval: 30000
    });
    const cancellableUploadsQuery = useQuery({
        queryKey: CANCELLABLE_UPLOADS_QUERY_KEY,
        queryFn: () => {
            const apiClient = getApiClient();
            return apiClient.getJSON(apiClient.getUrl('NebulaFtp/CancellableUploads')) as Promise<NebulaCancellableUpload[]>;
        },
        refetchInterval: 10000
    });
    const retryTerminalUploadMutation = useMutation({
        mutationFn: (id: string) => postAction<NebulaFailedUploadRetryResult>(`NebulaFtp/FailedUploads/${encodeURIComponent(id)}/Retry`),
        onSuccess: async result => {
            toast(result.message || 'Upload recolocado na fila.');
            await queryClient.invalidateQueries({ queryKey: TERMINAL_FAILED_UPLOADS_QUERY_KEY });
            await queryClient.invalidateQueries({ queryKey: UPLOAD_QUEUE_SUMMARY_QUERY_KEY });
            await queryClient.invalidateQueries({ queryKey: STATUS_QUERY_KEY });
            await queryClient.invalidateQueries({ queryKey: LOGS_QUERY_KEY });
        },
        onError: error => toast(`Erro ao reprocessar upload: ${getErrorMessage(error)}`)
    });
    const cancelUploadMutation = useMutation({
        mutationFn: (id: string) => postAction<NebulaUploadCancellationResult>(`NebulaFtp/Uploads/${encodeURIComponent(id)}/Cancel`),
        onSuccess: async result => {
            toast(result.message || 'Cancelamento solicitado.');
            await queryClient.invalidateQueries({ queryKey: CANCELLABLE_UPLOADS_QUERY_KEY });
            await queryClient.invalidateQueries({ queryKey: UPLOAD_QUEUE_SUMMARY_QUERY_KEY });
            await queryClient.invalidateQueries({ queryKey: STATUS_QUERY_KEY });
            await queryClient.invalidateQueries({ queryKey: LOGS_QUERY_KEY });
        },
        onError: error => toast(`Erro ao cancelar upload: ${getErrorMessage(error)}`)
    });

    const retryStatus = useCallback(() => {
        void statusQuery.refetch();
    }, [ statusQuery ]);
    const retryHealth = useCallback(() => {
        void healthQuery.refetch();
    }, [ healthQuery ]);
    const retryDatabaseHealth = useCallback(() => {
        void databaseHealthQuery.refetch();
    }, [ databaseHealthQuery ]);
    const retryPlaybackCache = useCallback(() => {
        void playbackCacheQuery.refetch();
    }, [ playbackCacheQuery ]);
    const retryUploadQueueSummary = useCallback(() => {
        void uploadQueueSummaryQuery.refetch();
    }, [ uploadQueueSummaryQuery ]);
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
    const queueCounts = getUploadQueueCountPresentation(
        status?.UploadQueueCount ?? status?.QueuedUploads?.length,
        uploadQueueSummaryQuery.data
    );
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
    const handleTabChange = useCallback((_event: React.SyntheticEvent, value: number) => {
        setActiveTab(value);
    }, []);
    const handleBotDeleteClick = useCallback((event: React.MouseEvent<HTMLButtonElement>) => {
        const index = Number(event.currentTarget.dataset.botIndex);
        if (Number.isInteger(index)) handleBotDelete(index);
    }, [ handleBotDelete ]);
    const handleCancelUploadClick = useCallback((event: React.MouseEvent<HTMLButtonElement>) => {
        const uploadId = event.currentTarget.dataset.uploadId;
        if (uploadId) cancelUploadMutation.mutate(uploadId);
    }, [ cancelUploadMutation ]);
    const handleRetryUploadClick = useCallback((event: React.MouseEvent<HTMLButtonElement>) => {
        const uploadId = event.currentTarget.dataset.uploadId;
        if (uploadId) retryTerminalUploadMutation.mutate(uploadId);
    }, [ retryTerminalUploadMutation ]);

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
                    onChange={handleTabChange}
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
                                detail={queueCounts.totalDescription}
                                percentage={activeUploads[0]?.Percentage || 0}
                                secondary={queueCounts.loadedDescription}
                            />
                        </Stack>
                        {currentDownload?.QueueCount ? (
                            <Paper variant='outlined' sx={{ p: 1.5 }}>
                                <Stack direction={{ xs: 'column', sm: 'row' }} gap={1} flexWrap='wrap'>
                                    <Typography variant='body2'>Posição planejada na varredura: {currentDownload.QueuePosition} de {currentDownload.QueueCount}</Typography>
                                    <Typography variant='body2' color='text.secondary'>Prioridade: {currentDownload.PriorityReason || 'ordem padrão'}</Typography>
                                    <Typography variant='body2' color='text.secondary'>Próximo título: {currentDownload.NextItemName || 'nenhum nesta varredura'}</Typography>
                                </Stack>
                            </Paper>
                        ) : null}

                        <Paper variant='outlined' sx={{ p: 2 }}>
                            <Stack spacing={1.5}>
                                <Box>
                                    <Typography variant='h2' component='h2' sx={{ fontSize: '1.2rem' }}>Saúde dos componentes</Typography>
                                    <Typography variant='body2' color='text.secondary'>Checks individuais sem expor credenciais ou URLs sensíveis.</Typography>
                                </Box>
                                <HealthContent isError={healthQuery.isError} error={healthQuery.error} health={componentHealth} onRetry={retryHealth} />
                                <DatabaseHealthContent
                                    health={databaseHealthQuery.data}
                                    isError={databaseHealthQuery.isError}
                                    error={databaseHealthQuery.error}
                                    onRetry={retryDatabaseHealth}
                                />
                            </Stack>
                        </Paper>

                        <Paper variant='outlined' sx={{ p: 2 }}>
                            <Stack spacing={1.5}>
                                <Box>
                                    <Typography variant='h2' component='h2' sx={{ fontSize: '1.2rem' }}>Resumo da fila de envio</Typography>
                                    <Typography variant='body2' color='text.secondary'>Consulta somente contagens e o item pendente mais antigo; atualização a cada 30 segundos.</Typography>
                                </Box>
                                <UploadQueueSummaryContent
                                    summary={uploadQueueSummaryQuery.data}
                                    isError={uploadQueueSummaryQuery.isError}
                                    error={uploadQueueSummaryQuery.error}
                                    onRetry={retryUploadQueueSummary}
                                />
                                <EffectiveUploadQueue status={status} />
                                <Divider />
                                <Box>
                                    <Typography variant='h3' component='h3' sx={{ fontSize: '1rem' }}>Uploads em andamento ou na fila</Typography>
                                    <Typography variant='body2' color='text.secondary'>Cancelamentos durante envio param após a parte atual; partes confirmadas permanecem salvas.</Typography>
                                </Box>
                                <CancellableUploadsContent
                                    uploads={cancellableUploadsQuery.data}
                                    isLoading={cancellableUploadsQuery.isLoading}
                                    isError={cancellableUploadsQuery.isError}
                                    error={cancellableUploadsQuery.error}
                                    isCancelling={cancelUploadMutation.isPending}
                                    onCancel={handleCancelUploadClick}
                                />
                                <Divider />
                                <Box>
                                    <Typography variant='h3' component='h3' sx={{ fontSize: '1rem' }}>Falhas e uploads cancelados</Typography>
                                    <Typography variant='body2' color='text.secondary'>Após 8 tentativas automáticas, uma falha para de repetir. Reprocessar preserva as partes confirmadas.</Typography>
                                </Box>
                                <FailedUploadsContent
                                    uploads={terminalFailedUploadsQuery.data}
                                    isLoading={terminalFailedUploadsQuery.isLoading}
                                    isError={terminalFailedUploadsQuery.isError}
                                    error={terminalFailedUploadsQuery.error}
                                    isRetrying={retryTerminalUploadMutation.isPending}
                                    onRetry={handleRetryUploadClick}
                                />
                            </Stack>
                        </Paper>

                        <Paper variant='outlined' sx={{ p: 2 }}>
                            <Stack spacing={1.5}>
                                <Box>
                                    <Typography variant='h2' component='h2' sx={{ fontSize: '1.2rem' }}>Cache de reprodução</Typography>
                                    <Typography variant='body2' color='text.secondary'>Uso, espaço livre e sessões protegidas; atualização a cada 30 segundos.</Typography>
                                </Box>
                                <PlaybackCacheContent
                                    cache={playbackCacheQuery.data}
                                    isError={playbackCacheQuery.isError}
                                    error={playbackCacheQuery.error}
                                    onRetry={retryPlaybackCache}
                                />
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
                                        <TextField label='Nova Secret key do Supabase (service_role)' type='password' value={supabaseKey} onChange={handleSupabaseKeyChange} autoComplete='new-password' size='small' fullWidth helperText='Use sb_secret_... ou uma chave JWT com role service_role. Não use publishable/anon.' />
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
                                        {backupMutation.isPending ? 'Executando backup MongoDB...' : 'Backup MongoDB → Supabase'}
                                    </Button>
                                    <Button
                                        variant='outlined'
                                        startIcon={<Restore />}
                                        disabled={isBusy}
                                        aria-busy={restoreMutation.isPending}
                                        onClick={openRestoreDialog}
                                    >
                                        {restoreMutation.isPending ? 'Restaurando MongoDB...' : 'Restore Supabase → MongoDB'}
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
