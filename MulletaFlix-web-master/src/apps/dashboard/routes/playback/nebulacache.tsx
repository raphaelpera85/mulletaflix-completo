import React, { useState, useCallback, useEffect } from 'react';
import Page from 'components/Page';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import DirectoryBrowser from 'components/directorybrowser/directorybrowser';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import CircularProgress from '@mui/material/CircularProgress';
import Grid from '@mui/material/Grid2';
import IconButton from '@mui/material/IconButton';
import InputAdornment from '@mui/material/InputAdornment';
import LinearProgress from '@mui/material/LinearProgress';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import FolderOpen from '@mui/icons-material/FolderOpen';
import DeleteSweep from '@mui/icons-material/DeleteSweep';
import Refresh from '@mui/icons-material/Refresh';
import Save from '@mui/icons-material/Save';
import Storage from '@mui/icons-material/Storage';
import PlayCircleOutline from '@mui/icons-material/PlayCircleOutline';
import toast from 'components/toast/toast';
import Loading from 'components/loading/LoadingComponent';

interface NebulaPlaybackCacheStatus {
    configuredPath: string;
    effectivePath: string;
    cachedFilesCount: number;
    totalSizeBytes: number;
    formattedSize: string;
    activeLeasesCount: number;
    freeSpaceGb: number;
    totalSpaceGb: number;
    maxCacheSizeBytes: number;
    minimumFreeSpaceBytes: number;
    cacheHits: number;
    cacheMisses: number;
    telegramFetchCount: number;
    telegramFetchFailures: number;
    telegramFetchCancellations: number;
    averageTelegramFetchLatencyMs: number;
    activePrefetchCount: number;
    queuedPrefetchCount: number;
    cacheErrors: number;
    cacheCleanupRuns: number;
    cacheCleanupFailures: number;
    lastCacheCleanupDurationMs: number;
    lastCacheCleanupUtc: string | null;
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
const getApiClient = (): any => {
    const client = ServerConnections.currentApiClient();
    if (!client) {
        throw new Error('No API client available');
    }
    return client;
};

export const Component = () => {
    const [ status, setStatus ] = useState<NebulaPlaybackCacheStatus | null>(null);
    const [ cachePathInput, setCachePathInput ] = useState('');
    const [ maxCacheSizeGb, setMaxCacheSizeGb ] = useState(50);
    const [ minimumFreeSpaceGb, setMinimumFreeSpaceGb ] = useState(2);
    const [ isLoading, setIsLoading ] = useState(true);
    const [ isSaving, setIsSaving ] = useState(false);
    const [ isClearing, setIsClearing ] = useState(false);
    const [ error, setError ] = useState<string | null>(null);
    const [ successMessage, setSuccessMessage ] = useState<string | null>(null);

    const loadStatus = useCallback(async () => {
        setIsLoading(true);
        setError(null);
        try {
            const apiClient = getApiClient();
            const url = apiClient.getUrl('NebulaFtp/PlaybackCache');
            const data = await (apiClient.getJSON(url) as Promise<NebulaPlaybackCacheStatus>);
            setStatus(data);
            setCachePathInput(data.configuredPath || '');
            setMaxCacheSizeGb(Math.max(1, Math.round(data.maxCacheSizeBytes / (1024 ** 3))));
            setMinimumFreeSpaceGb(Math.max(0, Math.round(data.minimumFreeSpaceBytes / (1024 ** 3))));
        } catch (err: unknown) {
            const msg = (err as Error)?.message || 'Erro ao carregar status do cache do Nebula';
            setError(msg);
        } finally {
            setIsLoading(false);
        }
    }, []);

    useEffect(() => {
        void loadStatus();
    }, [ loadStatus ]);

    const handleBrowseDirectory = useCallback(() => {
        const picker = new DirectoryBrowser();
        picker.show({
            path: cachePathInput,
            callback: function (path: string) {
                if (path) {
                    setCachePathInput(path);
                }
                picker.close();
            },
            validateWriteable: true,
            header: 'Selecionar pasta para Cache Nebula',
            instruction: 'Selecione ou digite o caminho da pasta onde serão armazenados os buffers e arquivos temporários de reprodução do catálogo Nebula.'
        });
    }, [ cachePathInput ]);

    const handleSavePath = useCallback(async (e: React.FormEvent) => {
        e.preventDefault();
        setIsSaving(true);
        setSuccessMessage(null);
        setError(null);

        try {
            const apiClient = getApiClient();
            const url = apiClient.getUrl('NebulaFtp/PlaybackCache/Path');
            const updated = await (apiClient.ajax({
                type: 'POST',
                url,
                dataType: 'json',
                data: JSON.stringify({
                    CachePath: cachePathInput.trim(),
                    MaxCacheSizeGb: maxCacheSizeGb,
                    MinimumFreeSpaceGb: minimumFreeSpaceGb
                }),
                contentType: 'application/json'
            }) as Promise<NebulaPlaybackCacheStatus>);

            setStatus(updated);
            setCachePathInput(updated.configuredPath || '');
            setSuccessMessage('Configurações do cache do Nebula atualizadas com sucesso.');
            toast('Configurações do cache atualizadas!');
        } catch (err: unknown) {
            const msg = (err as Error)?.message || 'Erro ao salvar novo caminho do cache';
            setError(msg);
            toast(`Erro: ${msg}`);
        } finally {
            setIsSaving(false);
        }
    }, [ cachePathInput, maxCacheSizeGb, minimumFreeSpaceGb ]);

    const handleClearCache = useCallback(async () => {
        if (!window.confirm('Deseja realmente limpar o cache de reprodução do Nebula? Apenas arquivos que não estão sendo executados no momento serão removidos.')) {
            return;
        }

        setIsClearing(true);
        setSuccessMessage(null);
        setError(null);

        try {
            const apiClient = getApiClient();
            const url = apiClient.getUrl('NebulaFtp/PlaybackCache/Clear');
            const cleared = await (apiClient.ajax({
                type: 'POST',
                url,
                dataType: 'json',
                contentType: 'application/json'
            }) as Promise<boolean>);

            if (!cleared) {
                throw new Error('O servidor não conseguiu limpar o cache.');
            }

            await loadStatus();
            const msg = 'Cache limpo. Arquivos em reprodução foram preservados.';
            setSuccessMessage(msg);
            toast(msg);
        } catch (err: unknown) {
            const msg = (err as Error)?.message || 'Erro ao limpar cache';
            setError(msg);
            toast(`Erro: ${msg}`);
        } finally {
            setIsClearing(false);
        }
    }, [ loadStatus ]);

    const handleRefresh = useCallback(async () => {
        await loadStatus();
    }, [ loadStatus ]);

    const handleDismissError = useCallback(() => setError(null), []);
    const handleDismissSuccess = useCallback(() => setSuccessMessage(null), []);
    const handleCachePathChange = useCallback((event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
        setCachePathInput(event.target.value);
    }, []);
    const handleMaxCacheSizeChange = useCallback((event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
        setMaxCacheSizeGb(Number(event.target.value));
    }, []);
    const handleMinimumFreeSpaceChange = useCallback((event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
        setMinimumFreeSpaceGb(Number(event.target.value));
    }, []);

    if (isLoading && !status) {
        return <Loading />;
    }

    const maxCacheSizeBytes = status?.maxCacheSizeBytes ?? 0;
    const cacheUsagePercentage = maxCacheSizeBytes > 0 ?
        Math.min((status?.totalSizeBytes ?? 0) / maxCacheSizeBytes * 100, 100) :
        0;
    let cacheUsageColor: 'error' | 'warning' | 'primary' = 'primary';
    if (cacheUsagePercentage > 90) {
        cacheUsageColor = 'error';
    } else if (cacheUsagePercentage > 75) {
        cacheUsageColor = 'warning';
    }

    return (
        <Page
            id='nebulaPlaybackCachePage'
            title='Cache de Reprodução Nebula'
            className='mainAnimatedPage type-interior'
        >
            <Box className='content-primary' sx={{ maxWidth: 900, mx: 'auto', p: 3 }}>
                <Stack spacing={3}>
                    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <Typography variant='h1'>
                            Cache de Reprodução Nebula
                        </Typography>
                        <IconButton
                            onClick={handleRefresh}
                            disabled={isLoading}
                            title='Atualizar Status'
                        >
                            <Refresh />
                        </IconButton>
                    </Box>

                    <Typography variant='body1' color='text.secondary'>
                        Configure o local em disco onde os arquivos de streaming sob demanda e buffers de mídia do catálogo Nebula são gravados durante a reprodução.
                    </Typography>

                    {error && (
                        <Alert severity='error' onClose={handleDismissError}>
                            {error}
                        </Alert>
                    )}

                    {successMessage && (
                        <Alert severity='success' onClose={handleDismissSuccess}>
                            {successMessage}
                        </Alert>
                    )}

                    {/* Status Overview Cards */}
                    <Grid container spacing={2}>
                        <Grid size={{ xs: 12, sm: 4 }}>
                            <Card variant='outlined'>
                                <CardContent>
                                    <Stack direction='row' alignItems='center' spacing={1} sx={{ mb: 1 }}>
                                        <Storage color='primary' />
                                        <Typography variant='subtitle2' color='text.secondary'>
                                            Espaço Ocupado
                                        </Typography>
                                    </Stack>
                                    <Typography variant='h4'>
                                        {status?.formattedSize || '0 B'}
                                    </Typography>
                                    <Typography variant='caption' color='text.secondary'>
                                        {status?.cachedFilesCount || 0} arquivo(s) em cache
                                    </Typography>
                                </CardContent>
                            </Card>
                        </Grid>

                        <Grid size={{ xs: 12, sm: 4 }}>
                            <Card variant='outlined'>
                                <CardContent>
                                    <Stack direction='row' alignItems='center' spacing={1} sx={{ mb: 1 }}>
                                        <PlayCircleOutline color='info' />
                                        <Typography variant='subtitle2' color='text.secondary'>
                                            Reproduções Ativas
                                        </Typography>
                                    </Stack>
                                    <Typography variant='h4'>
                                        {status?.activeLeasesCount || 0}
                                    </Typography>
                                    <Typography variant='caption' color='text.secondary'>
                                        {status?.activeLeasesCount ? 'Arquivos protegidos contra exclusão' : 'Nenhuma mídia executando agora'}
                                    </Typography>
                                </CardContent>
                            </Card>
                        </Grid>

                        <Grid size={{ xs: 12, sm: 4 }}>
                            <Card variant='outlined'>
                                <CardContent>
                                    <Typography variant='subtitle2' color='text.secondary' sx={{ mb: 1 }}>
                                        Uso da Cota do Cache
                                    </Typography>
                                    <Typography variant='h4'>
                                        {maxCacheSizeBytes ? `${cacheUsagePercentage.toFixed(1)}%` : 'N/D'}
                                    </Typography>
                                    {maxCacheSizeBytes > 0 && (
                                        <LinearProgress
                                            variant='determinate'
                                            value={cacheUsagePercentage}
                                            sx={{ mt: 1, borderRadius: 1 }}
                                            color={cacheUsageColor}
                                        />
                                    )}
                                    <Typography variant='caption' color='text.secondary'>
                                        Limite: {Math.round(maxCacheSizeBytes / (1024 ** 3))} GiB
                                    </Typography>
                                    <Typography variant='caption' color='text.secondary' display='block'>
                                        Livre: {status?.freeSpaceGb ?? 0} GiB · reserva: {Math.round((status?.minimumFreeSpaceBytes ?? 0) / (1024 ** 3))} GiB
                                    </Typography>
                                </CardContent>
                            </Card>
                        </Grid>
                    </Grid>

                    <Card variant='outlined'>
                        <CardContent>
                            <Typography variant='h6' sx={{ mb: 2 }}>
                                Diagnóstico da reprodução (desde a inicialização)
                            </Typography>
                            <Grid container spacing={2}>
                                <Grid size={{ xs: 6, sm: 3 }}>
                                    <Typography variant='body2' color='text.secondary'>Acertos / misses no cache em disco</Typography>
                                    <Typography variant='h6'>{status?.cacheHits ?? 0} / {status?.cacheMisses ?? 0}</Typography>
                                </Grid>
                                <Grid size={{ xs: 6, sm: 3 }}>
                                    <Typography variant='body2' color='text.secondary'>Downloads Telegram</Typography>
                                    <Typography variant='h6'>{status?.telegramFetchCount ?? 0}</Typography>
                                    <Typography variant='caption' color='text.secondary'>Falhas: {status?.telegramFetchFailures ?? 0} · Cancelados: {status?.telegramFetchCancellations ?? 0}</Typography>
                                </Grid>
                                <Grid size={{ xs: 6, sm: 3 }}>
                                    <Typography variant='body2' color='text.secondary'>Latência média Telegram</Typography>
                                    <Typography variant='h6'>{(status?.averageTelegramFetchLatencyMs ?? 0).toFixed(0)} ms</Typography>
                                </Grid>
                                <Grid size={{ xs: 6, sm: 3 }}>
                                    <Typography variant='body2' color='text.secondary'>Pré-cache ativo / fila</Typography>
                                    <Typography variant='h6'>{status?.activePrefetchCount ?? 0} / {status?.queuedPrefetchCount ?? 0}</Typography>
                                </Grid>
                                <Grid size={{ xs: 6, sm: 3 }}>
                                    <Typography variant='body2' color='text.secondary'>Erros de persistência</Typography>
                                    <Typography variant='h6'>{status?.cacheErrors ?? 0}</Typography>
                                </Grid>
                                <Grid size={{ xs: 6, sm: 3 }}>
                                    <Typography variant='body2' color='text.secondary'>Limpezas do cache</Typography>
                                    <Typography variant='h6'>{status?.cacheCleanupRuns ?? 0}</Typography>
                                    <Typography variant='caption' color='text.secondary'>Falhas: {status?.cacheCleanupFailures ?? 0}</Typography>
                                </Grid>
                                <Grid size={{ xs: 6, sm: 3 }}>
                                    <Typography variant='body2' color='text.secondary'>Última limpeza</Typography>
                                    <Typography variant='h6'>{(status?.lastCacheCleanupDurationMs ?? 0).toFixed(0)} ms</Typography>
                                    <Typography variant='caption' color='text.secondary'>
                                        {status?.lastCacheCleanupUtc ? new Date(status.lastCacheCleanupUtc).toLocaleString() : 'Ainda não executada'}
                                    </Typography>
                                </Grid>
                            </Grid>
                        </CardContent>
                    </Card>

                    {/* Path Configuration Form */}
                    <Card variant='outlined'>
                        <CardContent>
                            <Typography variant='h6' sx={{ mb: 2 }}>
                                Localização do Cache
                            </Typography>
                            <Box component='form' onSubmit={handleSavePath}>
                                <Stack spacing={2}>
                                    <TextField
                                        label='Pasta de Cache'
                                        value={cachePathInput}
                                        onChange={handleCachePathChange}
                                        helperText='Pasta onde os arquivos temporários são salvos. Caso o campo fique vazio, o servidor utiliza a pasta padrão do sistema de streaming.'
                                        fullWidth
                                        slotProps={{ input: {
                                            endAdornment: (
                                                <InputAdornment position='end'>
                                                    <IconButton
                                                        onClick={handleBrowseDirectory}
                                                        edge='end'
                                                        title='Procurar diretório'
                                                    >
                                                        <FolderOpen />
                                                    </IconButton>
                                                </InputAdornment>
                                            )
                                        } }}
                                    />

                                    <Grid container spacing={2}>
                                        <Grid size={{ xs: 12, sm: 6 }}>
                                            <TextField
                                                type='number'
                                                label='Limite máximo do cache (GiB)'
                                                value={maxCacheSizeGb}
                                                onChange={handleMaxCacheSizeChange}
                                                slotProps={{ htmlInput: { min: 1, max: 4096 } }}
                                                helperText='Ao alcançar o limite, o servidor remove mídias inativas primeiro e preserva a reprodução atual.'
                                                fullWidth
                                            />
                                        </Grid>
                                        <Grid size={{ xs: 12, sm: 6 }}>
                                            <TextField
                                                type='number'
                                                label='Espaço livre a preservar (GiB)'
                                                value={minimumFreeSpaceGb}
                                                onChange={handleMinimumFreeSpaceChange}
                                                slotProps={{ htmlInput: { min: 0, max: 1024 } }}
                                                helperText='Sem espaço disponível, o bloco é transmitido normalmente, mas não fica em cache.'
                                                fullWidth
                                            />
                                        </Grid>
                                    </Grid>

                                    <Box sx={{ display: 'flex', gap: 2, pt: 1 }}>
                                        <Button
                                            type='submit'
                                            variant='contained'
                                            startIcon={isSaving ? <CircularProgress size={20} color='inherit' /> : <Save />}
                                            disabled={isSaving}
                                        >
                                            Salvar Caminho
                                        </Button>
                                    </Box>
                                </Stack>
                            </Box>
                        </CardContent>
                    </Card>

                    {/* Cache Cleanup Section */}
                    <Card variant='outlined'>
                        <CardContent>
                            <Typography variant='h6' sx={{ mb: 1 }}>
                                Limpeza de Cache
                            </Typography>
                            <Typography variant='body2' color='text.secondary' sx={{ mb: 2 }}>
                                Remove do disco todos os arquivos de buffer do Nebula que não estão sendo executados no momento. Mídias em reprodução continuam ativas sem interrupção.
                            </Typography>
                            <Button
                                variant='outlined'
                                color='error'
                                startIcon={isClearing ? <CircularProgress size={20} color='inherit' /> : <DeleteSweep />}
                                onClick={handleClearCache}
                                disabled={isClearing || !status?.cachedFilesCount}
                            >
                                Limpar Cache Agora
                            </Button>
                        </CardContent>
                    </Card>
                </Stack>
            </Box>
        </Page>
    );
};

Component.displayName = 'NebulaPlaybackCachePage';
