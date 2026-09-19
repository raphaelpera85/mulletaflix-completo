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

type TelegramNotificationSettings = {
    Enabled: boolean;
    IntervalSeconds: number;
    ChatIds: string[];
};

const QUERY_KEY = [ 'NebulaTelegramNotifications' ];

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

const postSettings = async (settings: { Enabled: boolean; ChatIds: string; IntervalSeconds: number }) => {
    const apiClient = getApiClient();
    return apiClient.ajax({
        type: 'POST',
        url: apiClient.getUrl('NebulaFtp/Telegram/Notifications'),
        data: JSON.stringify(settings),
        contentType: 'application/json'
    });
};

const getActionLabel = (isPending: boolean, enabled: boolean): string => {
    if (isPending) return 'Salvando...';
    if (enabled) return 'Pausar envio';
    return 'Iniciar envio';
};

const TelegramNotificationsPage = () => {
    const settingsQuery = useQuery({
        queryKey: QUERY_KEY,
        queryFn: () => {
            const apiClient = getApiClient();
            return apiClient.getJSON(apiClient.getUrl('NebulaFtp/Telegram/Notifications')) as Promise<TelegramNotificationSettings>;
        }
    });
    const [ enabled, setEnabled ] = useState(true);
    const [ chatIds, setChatIds ] = useState('');
    const [ interval, setInterval ] = useState('3');

    useEffect(() => {
        if (!settingsQuery.data) return;
        setEnabled(settingsQuery.data.Enabled);
        setChatIds(settingsQuery.data.ChatIds.join(', '));
        setInterval(String(settingsQuery.data.IntervalSeconds));
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
        if (!chatIds.trim()) {
            toast('Informe ao menos um canal ou chat do Telegram.');
            return null;
        }
        if (!Number.isInteger(intervalSeconds) || intervalSeconds < 1 || intervalSeconds > 60) {
            toast('O intervalo deve estar entre 1 e 60 segundos.');
            return null;
        }
        return intervalSeconds;
    }, [ chatIds, interval ]);
    const saveSettings = useCallback((event: React.FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        const intervalSeconds = validate();
        if (intervalSeconds === null) return;
        mutation.mutate({ Enabled: enabled, ChatIds: chatIds, IntervalSeconds: intervalSeconds });
    }, [ chatIds, enabled, mutation, validate ]);
    const toggleSettings = useCallback(() => {
        const intervalSeconds = validate();
        if (intervalSeconds === null) return;
        const nextEnabled = !enabled;
        setEnabled(nextEnabled);
        mutation.mutate({ Enabled: nextEnabled, ChatIds: chatIds, IntervalSeconds: intervalSeconds });
    }, [ chatIds, enabled, mutation, validate ]);
    const handleChatIdsChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        setChatIds(event.target.value);
    }, []);
    const handleIntervalChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        setInterval(event.target.value);
    }, []);
    const actionLabel = getActionLabel(mutation.isPending, enabled);

    return (
        <Page id='telegramNotificationsPage' title='Telegram Notifications' className='mainAnimatedPage type-interior'>
            <Stack spacing={3} sx={{ p: { xs: 2, md: 3 }, maxWidth: 1100, mx: 'auto' }}>
                <Box>
                    <Typography variant='h1'>Telegram Notifications</Typography>
                    <Typography color='text.secondary'>Configure a fila de mensagens para novas mídias sem misturar com as operações do Nebula.</Typography>
                </Box>
                <Paper variant='outlined' sx={{ p: 2.5 }}>
                    <Stack spacing={2}>
                        <Box>
                            <Typography variant='h2' component='h2' sx={{ fontSize: '1.2rem' }}>Fila de novas mídias</Typography>
                            <Typography variant='body2' color='text.secondary'>As mensagens são enviadas uma por vez, fora do upload, para evitar Too Many Requests do Telegram.</Typography>
                        </Box>
                        {settingsQuery.isError && <Alert severity='warning'>Não foi possível carregar a configuração: {getErrorMessage(settingsQuery.error)}</Alert>}
                        <Box component='form' onSubmit={saveSettings}>
                            <Stack spacing={1.5}>
                                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
                                    <TextField label='Canais ou chats do Telegram' helperText='Separe vários IDs por vírgula. Ex.: -100123, -100456' value={chatIds} onChange={handleChatIdsChange} size='small' fullWidth />
                                    <TextField label='Intervalo entre mensagens (segundos)' type='number' value={interval} onChange={handleIntervalChange} size='small' slotProps={{ htmlInput: { min: 1, max: 60 } }} sx={{ minWidth: { sm: 250 } }} />
                                </Stack>
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

export default TelegramNotificationsPage;
