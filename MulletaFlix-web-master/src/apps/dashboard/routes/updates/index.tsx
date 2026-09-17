import React from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import LinearProgress from '@mui/material/LinearProgress';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import DownloadIcon from '@mui/icons-material/Download';
import RefreshIcon from '@mui/icons-material/Refresh';
import RestartAltIcon from '@mui/icons-material/RestartAlt';
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';
import Page from 'components/Page';
import globalize from 'lib/globalize';
import {
    useServerUpdateInfo,
    useUpdateStatus,
    useInstallUpdate,
    useApplyUpdate,
    type UpdateInfoDto
} from 'apps/dashboard/features/updates/api/useServerUpdateInfo';
import { useApi } from 'hooks/useApi';
import { EmptyState } from 'components/EmptyState';

const formatBytes = (bytes?: number) => {
    if (!bytes || bytes <= 0) {
        return null;
    }
    const mb = bytes / (1024 * 1024);
    return `${mb.toFixed(1)} MB`;
};

const Component = () => {
    const { api } = useApi();
    const { data: initialInfo, isLoading, isError, refetch } = useServerUpdateInfo();

    const [isApplying, setIsApplying] = React.useState(false);
    const [reconnectAttempts, setReconnectAttempts] = React.useState(0);

    const installMutation = useInstallUpdate();
    const applyMutation = useApplyUpdate();

    // Poll status only when downloading, extracting, or applying
    const isProgressState = initialInfo?.InstallState === 'Downloading' || initialInfo?.InstallState === 'Extracting' || isApplying;
    const { data: statusInfo } = useUpdateStatus(isProgressState, isProgressState ? 1500 : false);

    const updateInfo = React.useMemo(() => {
        if (!initialInfo && !statusInfo) {
            return undefined;
        }
        return {
            ...initialInfo,
            ...statusInfo,
            CurrentVersion: statusInfo?.CurrentVersion || initialInfo?.CurrentVersion || '',
            AvailableVersion: statusInfo?.AvailableVersion || initialInfo?.AvailableVersion,
            UpdateAvailable: statusInfo?.UpdateAvailable ?? initialInfo?.UpdateAvailable ?? false,
            Changelog: statusInfo?.Changelog || initialInfo?.Changelog,
            PackageSize: statusInfo?.PackageSize ?? initialInfo?.PackageSize,
            InstallState: statusInfo?.InstallState || (statusInfo as any)?.State || initialInfo?.InstallState || 'Idle',
            InstallProgress: statusInfo?.InstallProgress ?? (statusInfo as any)?.Progress ?? initialInfo?.InstallProgress ?? 0,
            ErrorMessage: statusInfo?.ErrorMessage || (statusInfo as any)?.ErrorMessage || initialInfo?.ErrorMessage
        } as UpdateInfoDto;
    }, [initialInfo, statusInfo]);

    const installState = isApplying ? 'Applying' : (updateInfo?.InstallState || 'Idle');
    const installProgress = updateInfo?.InstallProgress ?? 0;

    const handleRetry = React.useCallback(() => {
        void refetch();
    }, [refetch]);

    const handleStartInstall = React.useCallback(() => {
        installMutation.mutate(undefined, {
            onSuccess: () => {
                void refetch();
            }
        });
    }, [installMutation, refetch]);

    const handleApplyUpdate = React.useCallback(() => {
        setIsApplying(true);
        applyMutation.mutate(undefined, {
            onSuccess: () => {
                // Server will shut down to apply files, begin pinging until it restarts
                let attempts = 0;
                const interval = setInterval(() => {
                    attempts++;
                    setReconnectAttempts(attempts);
                    if (!api?.basePath) {
                        return;
                    }
                    fetch(`${api.basePath}/System/Info/Public`, { cache: 'no-store' })
                        .then((res) => {
                            if (res.ok) {
                                clearInterval(interval);
                                setTimeout(() => {
                                    window.location.reload();
                                }, 1000);
                            }
                        })
                        .catch(() => {
                            // Still restarting, keep waiting
                        });
                }, 2500);
            },
            onError: () => {
                setIsApplying(false);
            }
        });
    }, [applyMutation, api]);

    return (
        <Page
            id='updateCenterPage'
            title='Centro de Atualizações'
            className='mainAnimatedPage type-interior'
        >
            <Box className='content-primary'>
                <Stack spacing={3}>
                    <Stack direction='row' justifyContent='space-between' alignItems='center' flexWrap='wrap' gap={2}>
                        <Typography variant='h1'>
                            Centro de Atualizações
                        </Typography>
                        <Button
                            variant='outlined'
                            startIcon={<RefreshIcon />}
                            onClick={handleRetry}
                            disabled={isLoading || installState === 'Downloading' || installState === 'Extracting' || isApplying}
                        >
                            Verificar novamente
                        </Button>
                    </Stack>

                    {isLoading && <CircularProgress />}

                    {isError && (
                        <Alert
                            severity='error'
                            action={
                                <Button color='inherit' size='small' onClick={handleRetry}>
                                    {globalize.translate('Retry')}
                                </Button>
                            }
                        >
                            {globalize.translate('ErrorLoadingUpdateInfo')}
                        </Alert>
                    )}

                    {!isLoading && !isError && updateInfo && (
                        <>
                            <Paper variant='outlined' sx={{ p: 2.5, borderRadius: 3 }}>
                                <Stack spacing={2}>
                                    <Stack direction='row' spacing={2} alignItems='center'>
                                        <Typography variant='body1' sx={{ minWidth: 160 }}>
                                            {globalize.translate('LabelCurrentVersion')}:
                                        </Typography>
                                        <Chip label={updateInfo.CurrentVersion} color='info' variant='outlined' />
                                    </Stack>

                                    <Stack direction='row' spacing={2} alignItems='center'>
                                        <Typography variant='body1' sx={{ minWidth: 160 }}>
                                            {globalize.translate('LabelLatestVersion')}:
                                        </Typography>
                                        {updateInfo.AvailableVersion ? (
                                            <Chip
                                                label={updateInfo.AvailableVersion}
                                                color={updateInfo.UpdateAvailable ? 'success' : 'default'}
                                            />
                                        ) : (
                                            <Typography variant='body2' color='text.secondary'>
                                                —
                                            </Typography>
                                        )}
                                    </Stack>

                                    {updateInfo.PackageSize && (
                                        <Stack direction='row' spacing={2} alignItems='center'>
                                            <Typography variant='body1' sx={{ minWidth: 160 }}>
                                                Tamanho do pacote:
                                            </Typography>
                                            <Typography variant='body2' color='text.secondary'>
                                                {formatBytes(updateInfo.PackageSize)}
                                            </Typography>
                                        </Stack>
                                    )}

                                    {updateInfo.LastCheckedAt && (
                                        <Stack direction='row' spacing={2} alignItems='center'>
                                            <Typography variant='body1' sx={{ minWidth: 160 }}>
                                                Última verificação:
                                            </Typography>
                                            <Typography variant='body2' color='text.secondary'>
                                                {new Date(updateInfo.LastCheckedAt).toLocaleString()}
                                            </Typography>
                                        </Stack>
                                    )}
                                </Stack>
                            </Paper>

                            {/* Status and Action Banners */}
                            {installState === 'Applying' && (
                                <Alert severity='info' icon={<CircularProgress size={20} />}>
                                    <Typography variant='subtitle1' sx={{ fontWeight: 'bold' }}>
                                        Aplicando atualização no MulletaFlix...
                                    </Typography>
                                    <Typography variant='body2'>
                                        Os arquivos do sistema estão sendo substituídos de forma segura. O serviço será reiniciado automaticamente.
                                        {reconnectAttempts > 0 && ` Aguardando retorno do servidor (tentativa ${reconnectAttempts})...`}
                                    </Typography>
                                </Alert>
                            )}

                            {installState === 'ReadyToApply' && (
                                <Alert
                                    severity='success'
                                    icon={<CheckCircleOutlineIcon />}
                                    action={
                                        <Button
                                            color='success'
                                            variant='contained'
                                            size='small'
                                            startIcon={<RestartAltIcon />}
                                            onClick={handleApplyUpdate}
                                            disabled={applyMutation.isPending || isApplying}
                                        >
                                            Reiniciar e Aplicar
                                        </Button>
                                    }
                                >
                                    <Typography variant='subtitle1' sx={{ fontWeight: 'bold' }}>
                                        Atualização pronta para aplicação!
                                    </Typography>
                                    <Typography variant='body2'>
                                        Os binários mais recentes foram baixados e verificados. Clique no botão ao lado para reiniciar o MulletaFlix e concluir o processo sem perda de dados.
                                    </Typography>
                                </Alert>
                            )}

                            {(installState === 'Downloading' || installState === 'Extracting') && (
                                <Paper variant='outlined' sx={{ p: 2.5, borderRadius: 3 }}>
                                    <Stack spacing={1.5}>
                                        <Stack direction='row' justifyContent='space-between'>
                                            <Typography variant='body1' sx={{ fontWeight: 'medium' }}>
                                                {installState === 'Downloading'
                                                    ? `Baixando atualização do GitHub (${installProgress}%)...`
                                                    : 'Extraindo e validando integridade do pacote...'}
                                            </Typography>
                                            <Typography variant='body2' color='text.secondary'>
                                                {installProgress}%
                                            </Typography>
                                        </Stack>
                                        <LinearProgress
                                            variant={installState === 'Downloading' ? 'determinate' : 'indeterminate'}
                                            value={installProgress}
                                            sx={{ height: 10, borderRadius: 2 }}
                                        />
                                        <Typography variant='caption' color='text.secondary'>
                                            O MulletaFlix continua funcionando normalmente durante o download em segundo plano.
                                        </Typography>
                                    </Stack>
                                </Paper>
                            )}

                            {installState === 'Failed' && (
                                <Alert
                                    severity='error'
                                    action={
                                        <Button color='inherit' size='small' onClick={handleStartInstall}>
                                            Tentar novamente
                                        </Button>
                                    }
                                >
                                    <Typography variant='subtitle1' sx={{ fontWeight: 'bold' }}>
                                        Falha na instalação da atualização
                                    </Typography>
                                    <Typography variant='body2'>
                                        {updateInfo.ErrorMessage || 'Ocorreu um erro ao baixar ou extrair os arquivos de atualização.'}
                                    </Typography>
                                </Alert>
                            )}

                            {installState === 'Idle' && updateInfo.UpdateAvailable && (
                                <Alert
                                    severity='info'
                                    action={
                                        <Button
                                            color='primary'
                                            variant='contained'
                                            size='small'
                                            startIcon={<DownloadIcon />}
                                            onClick={handleStartInstall}
                                            disabled={installMutation.isPending}
                                        >
                                            Instalar Atualização Diretamente
                                        </Button>
                                    }
                                >
                                    <Typography variant='subtitle1' sx={{ fontWeight: 'bold' }}>
                                        Nova versão disponível ({updateInfo.AvailableVersion})
                                    </Typography>
                                    <Typography variant='body2'>
                                        Você pode atualizar diretamente a partir dos lançamentos no GitHub sem necessitar reinstalar o sistema ou perder suas configurações.
                                    </Typography>
                                </Alert>
                            )}

                            {installState === 'Idle' && !updateInfo.UpdateAvailable && updateInfo.AvailableVersion && (
                                <Alert severity='success'>
                                    O MulletaFlix está atualizado com a versão mais recente ({updateInfo.CurrentVersion}).
                                </Alert>
                            )}

                            {!updateInfo.AvailableVersion && (
                                <EmptyState
                                    title='Nenhuma informação de atualização'
                                    description='O servidor não encontrou novos pacotes no repositório configurado.'
                                />
                            )}

                            {updateInfo.Changelog && (
                                <Paper variant='outlined' sx={{ p: 2.5, borderRadius: 3 }}>
                                    <Typography variant='h2' sx={{ mb: 1.5 }}>
                                        Notas de Lançamento / Novidades
                                    </Typography>
                                    <Typography
                                        variant='body2'
                                        color='text.secondary'
                                        component='pre'
                                        sx={{
                                            whiteSpace: 'pre-wrap',
                                            fontFamily: 'inherit',
                                            margin: 0,
                                            lineHeight: 1.6
                                        }}
                                    >
                                        {updateInfo.Changelog}
                                    </Typography>
                                </Paper>
                            )}
                        </>
                    )}
                </Stack>
            </Box>
        </Page>
    );
};

Component.displayName = 'UpdateCenterPage';

export default Component;

