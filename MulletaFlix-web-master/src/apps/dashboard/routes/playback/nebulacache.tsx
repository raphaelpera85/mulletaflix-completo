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
import Grid from '@mui/material/Grid';
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
    cachePath: string;
    cachedFilesCount: number;
    totalSizeBytes: number;
    totalSizeFormatted: string;
    activeLeasesCount: number;
    freeSpaceBytes?: number | null;
    totalSpaceBytes?: number | null;
    usagePercentage?: number | null;
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
            setCachePathInput(data.cachePath || '');
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
        if (!cachePathInput.trim()) {
            toast('O caminho da pasta não pode ser vazio.');
            return;
        }

        setIsSaving(true);
        setSuccessMessage(null);
        setError(null);

        try {
            const apiClient = getApiClient();
            const url = apiClient.getUrl('NebulaFtp/PlaybackCache/Path');
            const updated = await (apiClient.ajax({
                type: 'POST',
                url,
                data: JSON.stringify({ CachePath: cachePathInput.trim() }),
                contentType: 'application/json'
            }) as Promise<NebulaPlaybackCacheStatus>);

            setStatus(updated);
            setCachePathInput(updated.cachePath || '');
            setSuccessMessage('Caminho do cache do Nebula atualizado com sucesso.');
            toast('Caminho do cache atualizado!');
        } catch (err: unknown) {
            const msg = (err as Error)?.message || 'Erro ao salvar novo caminho do cache';
            setError(msg);
            toast(`Erro: ${msg}`);
        } finally {
            setIsSaving(false);
        }
    }, [ cachePathInput ]);

    const handleClearCache = useCallback(async () => {
        if (!confirm('Deseja realmente limpar o cache de reprodução do Nebula? Apenas arquivos que não estão sendo executados no momento serão removidos.')) {
            return;
        }

        setIsClearing(true);
        setSuccessMessage(null);
        setError(null);

        try {
            const apiClient = getApiClient();
            const url = apiClient.getUrl('NebulaFtp/PlaybackCache/Clear');
            const result = await (apiClient.ajax({
                type: 'POST',
                url,
                contentType: 'application/json'
            }) as Promise<{ freedFormatted: string; deletedCount: number; status: NebulaPlaybackCacheStatus }>);

            if (result.status) {
                setStatus(result.status);
                setCachePathInput(result.status.cachePath || '');
            } else {
                void loadStatus();
            }

            const msg = `Cache limpo com sucesso! ${result.deletedCount || 0} arquivo(s) removido(s) (${result.freedFormatted || '0 B'} liberados).`;
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

    if (isLoading && !status) {
        return <Loading />;
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
                            onClick={() => void loadStatus()}
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
                        <Alert severity='error' onClose={() => setError(null)}>
                            {error}
                        </Alert>
                    )}

                    {successMessage && (
                        <Alert severity='success' onClose={() => setSuccessMessage(null)}>
                            {successMessage}
                        </Alert>
                    )}

                    {/* Status Overview Cards */}
                    <Grid container spacing={2}>
                        <Grid item xs={12} sm={4}>
                            <Card variant='outlined'>
                                <CardContent>
                                    <Stack direction='row' alignItems='center' spacing={1} sx={{ mb: 1 }}>
                                        <Storage color='primary' />
                                        <Typography variant='subtitle2' color='text.secondary'>
                                            Espaço Ocupado
                                        </Typography>
                                    </Stack>
                                    <Typography variant='h4'>
                                        {status?.totalSizeFormatted || '0 B'}
                                    </Typography>
                                    <Typography variant='caption' color='text.secondary'>
                                        {status?.cachedFilesCount || 0} arquivo(s) em cache
                                    </Typography>
                                </CardContent>
                            </Card>
                        </Grid>

                        <Grid item xs={12} sm={4}>
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

                        <Grid item xs={12} sm={4}>
                            <Card variant='outlined'>
                                <CardContent>
                                    <Typography variant='subtitle2' color='text.secondary' sx={{ mb: 1 }}>
                                        Uso do Disco do Cache
                                    </Typography>
                                    <Typography variant='h4'>
                                        {status?.usagePercentage != null ? `${status.usagePercentage.toFixed(1)}%` : 'N/D'}
                                    </Typography>
                                    {status?.usagePercentage != null && (
                                        <LinearProgress
                                            variant='determinate'
                                            value={Math.min(status.usagePercentage, 100)}
                                            sx={{ mt: 1, borderRadius: 1 }}
                                            color={status.usagePercentage > 90 ? 'error' : status.usagePercentage > 75 ? 'warning' : 'primary'}
                                        />
                                    )}
                                </CardContent>
                            </Card>
                        </Grid>
                    </Grid>

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
                                        onChange={e => setCachePathInput(e.target.value)}
                                        helperText='Pasta onde os arquivos temporários são salvos. Caso o campo fique vazio, o servidor utiliza a pasta padrão do sistema de streaming.'
                                        fullWidth
                                        InputProps={{
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
                                        }}
                                    />

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
                                onClick={() => void handleClearCache()}
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
