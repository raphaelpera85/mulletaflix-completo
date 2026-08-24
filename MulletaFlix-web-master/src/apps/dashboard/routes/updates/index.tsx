import React from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Page from 'components/Page';
import globalize from 'lib/globalize';
import { useServerUpdateInfo } from 'apps/dashboard/features/updates/api/useServerUpdateInfo';
import { EmptyState } from 'components/EmptyState';

const Component = () => {
    const { data: updateInfo, isLoading, isError } = useServerUpdateInfo();

    return (
        <Page
            id='updateCenterPage'
            title='Update Center'
            className='mainAnimatedPage type-interior'
        >
            <Box className='content-primary'>
                <Stack spacing={3}>
                    <Typography variant='h1'>
                        Centro de atualizações
                    </Typography>

                    {isLoading && <CircularProgress />}

                    {isError && (
                        <Alert severity='error'>
                            {globalize.translate('ErrorLoadingUpdateInfo')}
                        </Alert>
                    )}

                    {!isLoading && !isError && updateInfo && (
                        <>
                            <Paper variant='outlined' sx={{ p: 2, borderRadius: 3 }}>
                                <Stack spacing={1.5}>
                                    <Stack direction='row' spacing={2} alignItems='center'>
                                        <Typography variant='body1'>
                                            {globalize.translate('LabelCurrentVersion')}:
                                        </Typography>
                                        <Chip label={updateInfo.CurrentVersion} color='info' />
                                    </Stack>
                                    <Stack direction='row' spacing={2} alignItems='center'>
                                        <Typography variant='body1'>
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
                                </Stack>
                            </Paper>

                            {updateInfo.UpdateAvailable ? (
                                <Alert severity='info'>
                                    Uma nova versão ({updateInfo.AvailableVersion}) está disponível.
                                </Alert>
                            ) : null}

                            {!updateInfo.UpdateAvailable && updateInfo.AvailableVersion ? (
                                <Alert severity='success'>
                                    Você está na versão mais recente.
                                </Alert>
                            ) : null}

                            {!updateInfo.AvailableVersion ? (
                                <EmptyState
                                    title='Nenhuma informação de atualização'
                                    description='O servidor não está configurado para checar atualizações remotas.'
                                />
                            ) : null}

                            {updateInfo.Changelog && (
                                <Paper variant='outlined' sx={{ p: 2, borderRadius: 3 }}>
                                    <Typography variant='h2' sx={{ mb: 1 }}>
                                        Novidades
                                    </Typography>
                                    <Typography
                                        variant='body2'
                                        color='text.secondary'
                                        component='pre'
                                        sx={{ whiteSpace: 'pre-wrap', fontFamily: 'inherit', margin: 0 }}
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
