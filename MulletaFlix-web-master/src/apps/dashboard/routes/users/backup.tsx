import Backup from '@mui/icons-material/Backup';
import CloudDone from '@mui/icons-material/CloudDone';
import Restore from '@mui/icons-material/Restore';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useMutation, useQuery } from '@tanstack/react-query';
import React, { useCallback, useState } from 'react';

import ConfirmDialog from 'components/ConfirmDialog';
import Page from 'components/Page';
import toast from 'components/toast/toast';
import type { ApiClient } from 'jellyfin-apiclient';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import { queryClient } from 'utils/query/queryClient';

type SupabaseStatus = {
    IsConfigured: boolean;
    IsConnected: boolean;
    AutoBackupEnabled: boolean;
    AutoBackupIntervalHours: number;
    LastBackupTime?: string;
    LastBackupStatus?: string;
    TotalRemoteFiles: number;
    TotalLocalFiles: number;
    Message?: string;
};

type SupabaseConfig = {
    SupabaseUrl?: string;
    SupabaseKey?: string;
    SupabaseProjectRef?: string;
};

type OperationResult = {
    Success?: boolean;
    Message?: string;
};

const STATUS_QUERY_KEY = [ 'SupabaseBackupStatus' ];
const CONFIG_QUERY_KEY = [ 'NebulaFtpConfig' ];

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

const postAction = async (path: string): Promise<OperationResult> => {
    const apiClient = getApiClient();
    return apiClient.ajax({
        type: 'POST',
        url: apiClient.getUrl(path),
        headers: { 'X-Idempotency-Key': crypto.randomUUID() }
    }) as Promise<OperationResult>;
};

const formatDate = (value?: string): string => {
    if (!value) return 'Nenhum backup realizado ainda';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
};

const BackupRestorePage = () => {
    const [ isRestoreDialogOpen, setIsRestoreDialogOpen ] = useState(false);
    const [ projectRef, setProjectRef ] = useState('');
    const [ managementToken, setManagementToken ] = useState('');
    const statusQuery = useQuery({
        queryKey: STATUS_QUERY_KEY,
        queryFn: () => getApiClient().getJSON(getApiClient().getUrl('NebulaFtp/Supabase/Status')) as Promise<SupabaseStatus>,
        refetchInterval: 10000
    });
    const configQuery = useQuery({
        queryKey: CONFIG_QUERY_KEY,
        queryFn: () => getApiClient().getJSON(getApiClient().getUrl('NebulaFtp/Config')) as Promise<SupabaseConfig>
    });
    React.useEffect(() => {
        if (configQuery.data?.SupabaseProjectRef && !projectRef) setProjectRef(configQuery.data.SupabaseProjectRef);
    }, [ configQuery.data?.SupabaseProjectRef, projectRef ]);
    const provisionMutation = useMutation({
        mutationFn: async () => {
            const apiClient = getApiClient();
            return apiClient.ajax({
                type: 'POST',
                url: apiClient.getUrl('NebulaFtp/Supabase/Provision'),
                data: JSON.stringify({
                    Url: configQuery.data?.SupabaseUrl,
                    Key: configQuery.data?.SupabaseKey,
                    ProjectRef: projectRef,
                    ManagementToken: managementToken
                }),
                contentType: 'application/json'
            }) as Promise<OperationResult>;
        },
        onSuccess: result => {
            toast(result.Message || 'Estrutura do Supabase criada/validada.');
            setManagementToken('');
            void queryClient.invalidateQueries({ queryKey: CONFIG_QUERY_KEY });
            void queryClient.invalidateQueries({ queryKey: STATUS_QUERY_KEY });
        },
        onError: error => toast(`Erro ao provisionar Supabase: ${getErrorMessage(error)}`)
    });
    const backupMutation = useMutation({
        mutationFn: () => postAction('NebulaFtp/Supabase/Users/Backup'),
        onSuccess: result => {
            toast(result.Message || 'Backup delta iniciado.');
            void queryClient.invalidateQueries({ queryKey: STATUS_QUERY_KEY });
        },
        onError: error => toast(`Erro ao iniciar backup: ${getErrorMessage(error)}`)
    });
    const restoreMutation = useMutation({
        mutationFn: () => postAction('NebulaFtp/Supabase/Users/Restore'),
        onSuccess: result => {
            toast(result.Success === false ? `Restore não concluído: ${result.Message || 'erro desconhecido'}` : result.Message || 'Restore concluído.');
            void queryClient.invalidateQueries({ queryKey: STATUS_QUERY_KEY });
        },
        onError: error => toast(`Erro ao restaurar: ${getErrorMessage(error)}`)
    });
    const openRestoreDialog = useCallback(() => setIsRestoreDialogOpen(true), []);
    const closeRestoreDialog = useCallback(() => setIsRestoreDialogOpen(false), []);
    const confirmRestore = useCallback(() => {
        setIsRestoreDialogOpen(false);
        restoreMutation.mutate();
    }, [ restoreMutation ]);
    const startBackup = useCallback(() => backupMutation.mutate(), [ backupMutation ]);
    const updateProjectRef = useCallback((event: React.ChangeEvent<HTMLInputElement>) => setProjectRef(event.target.value), []);
    const updateManagementToken = useCallback((event: React.ChangeEvent<HTMLInputElement>) => setManagementToken(event.target.value), []);
    const provisionSchema = useCallback(() => provisionMutation.mutate(), [ provisionMutation ]);
    const isBusy = backupMutation.isPending || restoreMutation.isPending;
    const status = statusQuery.data;

    return (
        <Page id='usersBackupPage' title='Backup & Restore' className='mainAnimatedPage type-interior'>
            <Stack spacing={3} sx={{ p: { xs: 2, md: 3 }, maxWidth: 1000, mx: 'auto' }}>
                <Box>
                    <Typography variant='h1'>Backup &amp; Restore</Typography>
                    <Typography color='text.secondary'>Backup somente dos usuários para restauração manual após uma reinstalação. Mídias, NFOs, imagens e cache não são incluídos.</Typography>
                </Box>

                {statusQuery.isError && <Alert severity='error'>Não foi possível consultar o status: {getErrorMessage(statusQuery.error)}</Alert>}
                {!status?.IsConfigured && <Alert severity='warning'>Configure a URL e a chave do Supabase na configuração do Nebula antes de executar o backup ou o restore.</Alert>}

                <Paper variant='outlined' sx={{ p: 2.5 }}>
                    <Stack spacing={2}>
                        <Typography variant='h2' component='h2' sx={{ fontSize: '1.25rem' }}>Provisionamento automático do Supabase</Typography>
                        <Typography variant='body2' color='text.secondary'>Na primeira instalação, informe o Project ID e um token escopado da Management API com permissão de banco. O servidor executará o schema completo de forma idempotente e removerá o token da configuração após o sucesso. Para o backup diário, a configuração do Nebula deve usar a Secret key do Supabase (service_role), nunca a chave publishable/anon.</Typography>
                        <TextField label='Project ID' value={projectRef} onChange={updateProjectRef} placeholder='ex.: abcdefghijklmnopqrst' fullWidth size='small' />
                        <TextField label='Token da Management API' value={managementToken} onChange={updateManagementToken} placeholder='sbp_fc_...' type='password' fullWidth size='small' autoComplete='new-password' />
                        <Alert severity='info'>Use um token escopado somente para este projeto, com Database write. Ele não será usado nos backups diários.</Alert>
                        <Button variant='outlined' disabled={provisionMutation.isPending || !projectRef.trim() || !managementToken.trim() || !status?.IsConfigured} onClick={provisionSchema}>
                            {provisionMutation.isPending ? 'Criando estrutura...' : 'Testar e criar estrutura'}
                        </Button>
                    </Stack>
                </Paper>

                <Paper variant='outlined' sx={{ p: 2.5 }}>
                    <Stack spacing={2}>
                        <Stack direction='row' spacing={1} alignItems='center'>
                            <CloudDone color='primary' />
                            <Typography variant='h2' component='h2' sx={{ fontSize: '1.25rem' }}>Sincronização automática</Typography>
                            <Chip label={status?.IsConnected ? 'Conectado' : 'Não conectado'} color={status?.IsConnected ? 'success' : 'default'} size='small' />
                        </Stack>
                        <Typography variant='body2' color='text.secondary'>O servidor executa um backup delta de usuários automaticamente a cada 24 horas. Usuários removidos localmente pelo administrador também são removidos do backup remoto.</Typography>
                        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)' }, gap: 1.5 }}>
                            <Typography variant='body2'>Intervalo: <strong>24 horas</strong></Typography>
                            <Typography variant='body2'>Último backup: <strong>{formatDate(status?.LastBackupTime)}</strong></Typography>
                            <Typography variant='body2'>Status: <strong>{status?.LastBackupStatus || 'Aguardando execução'}</strong></Typography>
                            <Typography variant='body2'>Mídia incluída: <strong>Não</strong></Typography>
                        </Box>
                    </Stack>
                </Paper>

                <Paper variant='outlined' sx={{ p: 2.5 }}>
                    <Stack spacing={2}>
                        <Typography variant='h2' component='h2' sx={{ fontSize: '1.25rem' }}>Ações manuais</Typography>
                        <Typography variant='body2' color='text.secondary'>Use o backup delta para sincronizar usuários agora. Após reinstalar o servidor, use o restore completo para recuperar usuários, permissões, licenças e tokens. O acervo permanece local.</Typography>
                        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
                            <Button variant='contained' startIcon={<Backup />} disabled={isBusy || !status?.IsConfigured} onClick={startBackup}>
                                {backupMutation.isPending ? 'Iniciando backup...' : 'Executar backup delta'}
                            </Button>
                            <Button variant='outlined' color='warning' startIcon={<Restore />} disabled={isBusy || !status?.IsConfigured} onClick={openRestoreDialog}>
                                {restoreMutation.isPending ? 'Restaurando...' : 'Restaurar backup completo'}
                            </Button>
                        </Stack>
                    </Stack>
                </Paper>
            </Stack>
            <ConfirmDialog
                open={isRestoreDialogOpen}
                title='Confirmar restauração completa'
                text='A restauração atualizará os dados locais com o backup do Supabase. Deseja continuar?'
                confirmButtonColor='warning'
                confirmButtonText='Restaurar'
                onCancel={closeRestoreDialog}
                onConfirm={confirmRestore}
            />
        </Page>
    );
};

export default BackupRestorePage;
