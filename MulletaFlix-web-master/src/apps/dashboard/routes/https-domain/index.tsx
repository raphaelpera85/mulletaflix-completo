import Lock from '@mui/icons-material/Lock';
import OpenInNew from '@mui/icons-material/OpenInNew';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Link from '@mui/material/Link';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useMutation, useQuery } from '@tanstack/react-query';
import React, { useCallback, useEffect, useState } from 'react';

import Page from 'components/Page';
import Loading from 'components/loading/LoadingComponent';
import toast from 'components/toast/toast';
import type { ApiClient } from 'jellyfin-apiclient';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import { queryClient } from 'utils/query/queryClient';

/**
 * T9.4 — Domain/HTTPS management screen. Pure status display + configuration form
 * over the read-only/idempotent T9.2 endpoints (GET/POST System/Https/*). Mirrors
 * the dashboard's existing custom-endpoint pattern (see routes/nebula/index.tsx):
 * ServerConnections.currentApiClient() + apiClient.getJSON/ajax, not the generated
 * jellyfin-sdk client, since this controller is MulletaFlix-specific.
 *
 * Deliberately does NOT perform any DNS update, ACME/certificate request, or
 * firewall/proxy change -- T9.3 (provisioning/renewal) is out of scope here and
 * requires operator-authorized network changes this screen must never trigger
 * implicitly. Saving only persists the subdomain/e-mail/token server-side.
 */

type HttpsDnsStatus = 'NotConfigured' | 'Unknown';
type HttpsCertificateStatus = 'NotConfigured' | 'NotFound' | 'ReadError' | 'Active' | 'ExpiringSoon' | 'Expired';

type HttpsDomainStatus = {
    Enabled: boolean;
    Configured: boolean;
    DuckDnsSubdomain: string;
    FullDomain: string;
    AcmeEmail: string;
    TokenConfigured: boolean;
    MaskedToken: string;
    DnsStatus: HttpsDnsStatus;
    LastUpdatedUtc?: string | null;
    CertificateStatus: HttpsCertificateStatus;
    CertificateSubject?: string | null;
    CertificateExpiresUtc?: string | null;
};

const STATUS_QUERY_KEY = [ 'HttpsDomainStatus' ];

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
        responseJSON?: { error?: string; message?: string };
        responseText?: string;
    };

    return requestError.responseJSON?.error
        || requestError.responseJSON?.message
        || requestError.responseText
        || requestError.message
        || 'erro desconhecido';
};

const certificateStatusLabel: Record<HttpsCertificateStatus, { text: string; color: 'default' | 'success' | 'warning' | 'error' }> = {
    NotConfigured: { text: 'Não configurado', color: 'default' },
    NotFound: { text: 'Arquivo não encontrado', color: 'error' },
    ReadError: { text: 'Erro ao ler certificado', color: 'error' },
    Active: { text: 'Ativo', color: 'success' },
    ExpiringSoon: { text: 'Expira em breve', color: 'warning' },
    Expired: { text: 'Expirado', color: 'error' }
};

const dnsStatusLabel: Record<HttpsDnsStatus, { text: string; color: 'default' | 'success' | 'warning' | 'error' }> = {
    NotConfigured: { text: 'Não configurado', color: 'default' },
    Unknown: { text: 'Desconhecido (nenhuma verificação de rede realizada)', color: 'default' }
};

const HttpsDomainPage: React.FC = () => {
    const [ subdomain, setSubdomain ] = useState('');
    const [ email, setEmail ] = useState('');
    const [ token, setToken ] = useState('');
    const [ isInitialized, setIsInitialized ] = useState(false);

    const statusQuery = useQuery({
        queryKey: STATUS_QUERY_KEY,
        queryFn: () => {
            const apiClient = getApiClient();
            return apiClient.getJSON(apiClient.getUrl('System/Https/Status')) as Promise<HttpsDomainStatus>;
        }
    });

    useEffect(() => {
        if (statusQuery.data && !isInitialized) {
            setSubdomain(statusQuery.data.DuckDnsSubdomain || '');
            setEmail(statusQuery.data.AcmeEmail || '');
            setIsInitialized(true);
        }
    }, [ statusQuery.data, isInitialized ]);

    const configureMutation = useMutation({
        mutationFn: () => {
            const apiClient = getApiClient();
            return apiClient.ajax({
                type: 'POST',
                url: apiClient.getUrl('System/Https/Configure'),
                data: JSON.stringify({
                    DuckDnsSubdomain: subdomain,
                    AcmeEmail: email,
                    DuckDnsToken: token
                }),
                contentType: 'application/json'
            }) as Promise<HttpsDomainStatus>;
        },
        onSuccess: async () => {
            // Never keep the token value around after a successful save -- the
            // server never echoes it back and the field must not pretend to.
            setToken('');
            await queryClient.invalidateQueries({ queryKey: STATUS_QUERY_KEY });
            toast('Configuração de domínio/HTTPS salva.');
        },
        onError: error => toast(`Erro ao salvar: ${getErrorMessage(error)}`)
    });

    const handleRetry = useCallback(() => {
        void statusQuery.refetch();
    }, [ statusQuery ]);

    const handleSubmit = useCallback((e: React.FormEvent) => {
        e.preventDefault();
        configureMutation.mutate();
    }, [ configureMutation ]);

    const handleSubdomainChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
        setSubdomain(e.target.value);
    }, []);

    const handleEmailChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
        setEmail(e.target.value);
    }, []);

    const handleTokenChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
        setToken(e.target.value);
    }, []);

    if (statusQuery.isPending) return <Loading />;

    const status = statusQuery.data;

    return (
        <Page
            id='httpsDomainPage'
            title='Domínio e HTTPS'
            className='type-interior mainAnimatedPage'
        >
            <Box className='content-primary'>
                <Stack spacing={4}>
                    <Stack direction='row' spacing={1} alignItems='center'>
                        <Lock />
                        <Typography variant='h1'>Domínio público e HTTPS</Typography>
                    </Stack>

                    <Alert severity='info'>
                        Esta tela configura apenas o subdomínio DuckDNS, o e-mail usado para o certificado ACME
                        e o token de acesso. Nenhuma alteração de DNS, firewall, roteador ou certificado é feita
                        a partir daqui — isso continua sendo responsabilidade do operador (encaminhamento de
                        portas 80/443 no roteador, renovação automática do certificado, etc).
                    </Alert>

                    {statusQuery.isError ? (
                        <Alert
                            severity='error'
                            action={<Button color='inherit' size='small' onClick={handleRetry}>Tentar novamente</Button>}
                        >
                            Não foi possível carregar o status: {getErrorMessage(statusQuery.error)}
                        </Alert>
                    ) : status && (
                        <>
                            <Paper variant='outlined' sx={{ p: 3 }}>
                                <Typography variant='h2' sx={{ mb: 2 }}>Status atual</Typography>
                                <Stack spacing={1.5}>
                                    <Stack direction='row' spacing={1} alignItems='center'>
                                        <Typography sx={{ minWidth: 160 }}>Domínio completo</Typography>
                                        <Typography fontWeight={600}>
                                            {status.FullDomain || '—'}
                                        </Typography>
                                    </Stack>
                                    <Stack direction='row' spacing={1} alignItems='center'>
                                        <Typography sx={{ minWidth: 160 }}>Token DuckDNS</Typography>
                                        <Typography fontFamily='monospace'>{status.MaskedToken}</Typography>
                                        <Chip size='small' label={status.TokenConfigured ? 'Configurado' : 'Não configurado'} color={status.TokenConfigured ? 'success' : 'default'} />
                                    </Stack>
                                    <Stack direction='row' spacing={1} alignItems='center'>
                                        <Typography sx={{ minWidth: 160 }}>DNS</Typography>
                                        <Chip size='small' label={dnsStatusLabel[status.DnsStatus].text} color={dnsStatusLabel[status.DnsStatus].color} />
                                    </Stack>
                                    <Stack direction='row' spacing={1} alignItems='center'>
                                        <Typography sx={{ minWidth: 160 }}>Certificado</Typography>
                                        <Chip size='small' label={certificateStatusLabel[status.CertificateStatus].text} color={certificateStatusLabel[status.CertificateStatus].color} />
                                        {status.CertificateExpiresUtc && (
                                            <Typography variant='body2' color='text.secondary'>
                                                expira em {new Date(status.CertificateExpiresUtc).toLocaleString()}
                                            </Typography>
                                        )}
                                    </Stack>
                                    {status.LastUpdatedUtc && (
                                        <Typography variant='body2' color='text.secondary'>
                                            Última atualização: {new Date(status.LastUpdatedUtc).toLocaleString()}
                                        </Typography>
                                    )}
                                </Stack>
                            </Paper>

                            <Paper component='form' variant='outlined' sx={{ p: 3 }} onSubmit={handleSubmit}>
                                <Typography variant='h2' sx={{ mb: 1 }}>Configurar DuckDNS</Typography>
                                <Typography variant='body2' color='text.secondary' sx={{ mb: 3 }}>
                                    Crie uma conta gratuita em{' '}
                                    <Link href='https://www.duckdns.org' target='_blank' rel='noopener noreferrer'>
                                        duckdns.org <OpenInNew sx={{ fontSize: 14, verticalAlign: 'middle' }} />
                                    </Link>
                                    {' '}para obter um subdomínio e o token.
                                </Typography>

                                <Stack spacing={3}>
                                    <TextField
                                        label='Subdomínio DuckDNS'
                                        helperText='Apenas letras, números e hífen. O domínio final será "<subdomínio>.duckdns.org".'
                                        value={subdomain}
                                        onChange={handleSubdomainChange}
                                        required
                                    />

                                    <TextField
                                        label='E-mail para o certificado (ACME)'
                                        type='email'
                                        helperText="Usado apenas para avisos de expiração do certificado Let's Encrypt."
                                        value={email}
                                        onChange={handleEmailChange}
                                        required
                                    />

                                    <TextField
                                        label='Token DuckDNS'
                                        type='password'
                                        helperText={status.TokenConfigured ?
                                            `Já configurado (${status.MaskedToken}). Informe o token novamente para salvar (reenviar o mesmo valor não causa erro nem duplicidade).` :
                                            'Encontrado no painel do DuckDNS, ao lado do seu domínio.'
                                        }
                                        value={token}
                                        onChange={handleTokenChange}
                                        required
                                    />

                                    <Box>
                                        <Button
                                            type='submit'
                                            variant='contained'
                                            disabled={configureMutation.isPending}
                                        >
                                            {configureMutation.isPending ? 'Salvando...' : 'Salvar'}
                                        </Button>
                                    </Box>
                                </Stack>
                            </Paper>
                        </>
                    )}
                </Stack>
            </Box>
        </Page>
    );
};

export const Component = HttpsDomainPage;

export default HttpsDomainPage;
