import PlayArrow from '@mui/icons-material/PlayArrow';
import Stop from '@mui/icons-material/Stop';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useMutation, useQuery } from '@tanstack/react-query';
import React, { useCallback, useEffect, useState } from 'react';

import Page from 'components/Page';
import toast from 'components/toast/toast';
import type { ApiClient } from 'jellyfin-apiclient';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import { queryClient } from 'utils/query/queryClient';

type NotificationsSettings = {
    Enabled: boolean;
    IntervalSeconds: number;
    ChannelIds: string[];
    PublicServerUrl: string;
};

// The server is intentionally exposed over HTTP on the configured DuckDNS port.
// eslint-disable-next-line sonarjs/no-clear-text-protocols
const DEFAULT_PUBLIC_SERVER_URL = 'http://mulletaflix.duckdns.org:8096';
const QUERY_KEY = [ 'NotificationsSettings' ];

const getApiClient = (): ApiClient => {
    const apiClient = ServerConnections.currentApiClient();
    if (!apiClient) throw new Error('Cliente de API indisponível.');
    return apiClient as unknown as ApiClient;
};

const getErrorMessage = (error: unknown): string => {
    const requestError = error as { message?: string; responseJSON?: { message?: string; Message?: string }; responseText?: string };
    return requestError.responseJSON?.message
        || requestError.responseJSON?.Message
        || requestError.responseText
        || requestError.message
        || 'erro desconhecido';
};

const postSettings = async (settings: { Enabled: boolean; ChannelIds: string; IntervalSeconds: number; PublicServerUrl: string }) => {
    const apiClient = getApiClient();
    return apiClient.ajax({
        type: 'POST',
        url: apiClient.getUrl('NebulaFtp/Notifications'),
        data: JSON.stringify(settings),
        contentType: 'application/json'
    });
};

const getActionLabel = (isPending: boolean, enabled: boolean): string => {
    if (isPending) return 'Salvando...';
    if (enabled) return 'Pausar envio';
    return 'Iniciar envio';
};

const NotificationsPage = () => {
    const settingsQuery = useQuery({
        queryKey: QUERY_KEY,
        queryFn: () => {
            const apiClient = getApiClient();
            return apiClient.getJSON(apiClient.getUrl('NebulaFtp/Notifications')) as Promise<NotificationsSettings>;
        }
    });
    const [ enabled, setEnabled ] = useState(true);
    const [ channelIds, setChannelIds ] = useState('');
    const [ interval, setInterval ] = useState('3');
    const [ publicServerUrl, setPublicServerUrl ] = useState(DEFAULT_PUBLIC_SERVER_URL);

    useEffect(() => {
        if (!settingsQuery.data) return;
        setEnabled(settingsQuery.data.Enabled);
        setChannelIds(settingsQuery.data.ChannelIds.join(', '));
        setInterval(String(settingsQuery.data.IntervalSeconds));
        setPublicServerUrl(settingsQuery.data.PublicServerUrl || DEFAULT_PUBLIC_SERVER_URL);
    }, [ settingsQuery.data ]);

    const mutation = useMutation({
        mutationFn: postSettings,
        onSuccess: async (_result, variables) => {
            await queryClient.invalidateQueries({ queryKey: QUERY_KEY });
            toast(variables.Enabled ? 'Envio de notificações iniciado.' : 'Envio de notificações pausado.');
        },
        onError: error => toast(`Erro ao salvar notificações: ${getErrorMessage(error)}`)
    });

    const validate = useCallback(() => {
        const intervalSeconds = Number(interval);
        if (!channelIds.trim()) {
            toast('Informe ao menos um canal de notificação.');
            return null;
        }
        if (!Number.isInteger(intervalSeconds) || intervalSeconds < 1 || intervalSeconds > 60) {
            toast('O intervalo deve estar entre 1 e 60 segundos.');
            return null;
        }
        if (!publicServerUrl.trim() || /localhost|127\.0\.0\.1|\[::1\]/i.test(publicServerUrl)) {
            toast('Informe uma URL pública, sem localhost.');
            return null;
        }
        return intervalSeconds;
    }, [ channelIds, interval, publicServerUrl ]);
    const saveSettings = useCallback((event: React.FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        const intervalSeconds = validate();
        if (intervalSeconds === null) return;
        mutation.mutate({ Enabled: enabled, ChannelIds: channelIds, IntervalSeconds: intervalSeconds, PublicServerUrl: publicServerUrl });
    }, [ channelIds, enabled, mutation, publicServerUrl, validate ]);
    const toggleSettings = useCallback(() => {
        const intervalSeconds = validate();
        if (intervalSeconds === null) return;
        const nextEnabled = !enabled;
        setEnabled(nextEnabled);
        mutation.mutate({ Enabled: nextEnabled, ChannelIds: channelIds, IntervalSeconds: intervalSeconds, PublicServerUrl: publicServerUrl });
    }, [ channelIds, enabled, mutation, publicServerUrl, validate ]);
    const handleChannelIdsChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        setChannelIds(event.target.value);
    }, []);
    const handleIntervalChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        setInterval(event.target.value);
    }, []);
    const handlePublicServerUrlChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        setPublicServerUrl(event.target.value);
    }, []);
    const actionLabel = getActionLabel(mutation.isPending, enabled);

    return (
        <Page id='notificationsPage' title='Notifications' className='mainAnimatedPage type-interior'>
            <Stack spacing={3} sx={{ p: { xs: 2, md: 3 }, maxWidth: 1100, mx: 'auto' }}>
                <Box>
                    <Typography variant='h1'>Notifications</Typography>
                    <Typography color='text.secondary'>Gerencie notificações de novas mídias. Telegram é o primeiro transporte; outros canais poderão ser adicionados futuramente.</Typography>
                </Box>
                <Paper variant='outlined' sx={{ p: 2.5 }}>
                    <Stack spacing={2}>
                        <Box>
                            <Typography variant='h2' component='h2' sx={{ fontSize: '1.2rem' }}>Fila de novas mídias</Typography>
                            <Typography variant='body2' color='text.secondary'>As mensagens só entram na fila depois que o servidor conclui metadados, capa e NFO da mídia.</Typography>
                        </Box>
                        {settingsQuery.isError && <Alert severity='warning'>Não foi possível carregar a configuração: {getErrorMessage(settingsQuery.error)}</Alert>}
                        <Box component='form' onSubmit={saveSettings}>
                            <Stack spacing={1.5}>
                                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
                                    <TextField label='Canais de notificação' helperText='Separe vários IDs por vírgula. Este canal é independente do canal de upload.' value={channelIds} onChange={handleChannelIdsChange} size='small' fullWidth />
                                    <TextField label='Intervalo entre mensagens (segundos)' type='number' value={interval} onChange={handleIntervalChange} size='small' slotProps={{ htmlInput: { min: 1, max: 60 } }} sx={{ minWidth: { sm: 250 } }} />
                                </Stack>
                                <TextField label='URL pública do servidor' helperText='Usada no link direto da mídia. Ex.: http://mulletaflix.duckdns.org:8096' value={publicServerUrl} onChange={handlePublicServerUrlChange} size='small' fullWidth />
                                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} alignItems={{ sm: 'center' }}>
                                    <Button type='button' variant='contained' color={enabled ? 'warning' : 'success'} startIcon={enabled ? <Stop /> : <PlayArrow />} disabled={mutation.isPending} onClick={toggleSettings}>{actionLabel}</Button>
                                    <Button type='submit' variant='outlined' disabled={mutation.isPending}>Salvar canais e intervalo</Button>
                                    <Chip label={enabled ? 'Envio ativo' : 'Envio pausado'} color={enabled ? 'success' : 'warning'} size='small' />
                                </Stack>
                            </Stack>
                        </Box>
                    </Stack>
                </Paper>
            </Stack>
        </Page>
    );
};

export default NotificationsPage;
