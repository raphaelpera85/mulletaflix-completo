import React, { useCallback, useMemo, useState } from 'react';
import { useQueries } from '@tanstack/react-query';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import FormControl from '@mui/material/FormControl';
import InputLabel from '@mui/material/InputLabel';
import MenuItem from '@mui/material/MenuItem';
import Paper from '@mui/material/Paper';
import Select, { SelectChangeEvent } from '@mui/material/Select';
import Stack from '@mui/material/Stack';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Typography from '@mui/material/Typography';
import type { ChipProps } from '@mui/material/Chip';

import Page from 'components/Page';
import confirm from 'components/confirm/confirm';
import Loading from 'components/loading/LoadingComponent';
import toast from 'components/toast/toast';
import { fetchUserLicense, type UserLicenseDto, USER_LICENSE_QUERY_KEY, useRevokeUserLicense, useSetUserLicense } from 'apps/dashboard/features/users/api/useUserLicense';
import { useLogEntries } from 'apps/dashboard/features/activity/api/useLogEntries';
import type { ActivityLogEntry } from '@jellyfin/sdk/lib/generated-client/models/activity-log-entry';
import { ActivityLogSortBy } from '@jellyfin/sdk/lib/generated-client/models/activity-log-sort-by';
import { SortOrder } from '@jellyfin/sdk/lib/generated-client/models/sort-order';
import { useUsers } from 'hooks/useUsers';
import { useApi } from 'hooks/useApi';

interface ApiError {
    message?: string;
    response?: {
        status?: number;
    };
}

type LicenseRowStatus = {
    label: string;
    color: ChipProps['color'];
    details: string;
};

type LicenseDurationOption = {
    value: string;
    label: string;
    helper: string;
};

const LICENSE_DURATION_OPTIONS: LicenseDurationOption[] = [
    {
        value: '0',
        label: '30 minutos',
        helper: 'Período de teste para clientes'
    },
    {
        value: '730',
        label: '1 mês',
        helper: 'Aproximadamente 30 dias'
    },
    {
        value: '2190',
        label: '3 meses',
        helper: 'Aproximadamente 90 dias'
    },
    {
        value: '4380',
        label: '6 meses',
        helper: 'Aproximadamente 180 dias'
    },
    {
        value: '8760',
        label: '12 meses',
        helper: 'Aproximadamente 1 ano'
    },
    {
        value: '-1',
        label: 'Ilimitado',
        helper: 'Sem expiração'
    }
];

const getStatus = (
    license: UserLicenseDto | undefined,
    isAdmin: boolean,
    isNoLicense: boolean,
    isLoading: boolean,
    hasError: boolean,
    errorMessage?: string
): LicenseRowStatus => {
    if (isLoading) {
        return {
            label: 'Carregando',
            color: 'warning',
            details: 'Buscando os dados da licença.'
        };
    }

    if (hasError) {
        return {
            label: 'Erro',
            color: 'error',
            details: errorMessage || 'Não foi possível carregar a licença.'
        };
    }

    if (!license || isNoLicense) {
        if (isAdmin) {
            return {
                label: 'Administrador',
                color: 'info',
                details: 'A conta é administrativa e será aplicada como ilimitada.'
            };
        }

        return {
            label: 'Sem licença',
            color: 'default',
            details: 'O usuário ainda não possui uma licença ativa.'
        };
    }

    if (license.IsUnlimited) {
        return {
            label: 'Ilimitada',
            color: 'info',
            details: 'A licença não tem data de expiração.'
        };
    }

    if (license.IsExpired) {
        return {
            label: 'Expirada',
            color: 'error',
            details: license.ExpirationDate
                ? `Expirou em ${new Date(license.ExpirationDate).toLocaleString()}.`
                : 'A licença expirou.'
        };
    }

    return {
        label: 'Ativa',
        color: 'success',
        details: license.TimeRemaining ? `Tempo restante: ${license.TimeRemaining}.` : 'Licença ativa.'
    };
};

export const Component = () => {
    const { api } = useApi();
    const { data: users, isPending, isError } = useUsers();
    const setLicenseMutation = useSetUserLicense();
    const revokeLicenseMutation = useRevokeUserLicense();
    const [ durationDrafts, setDurationDrafts ] = useState<Record<string, string>>({});

    const licenseQueries = useQueries({
        queries: useMemo(() => (
            (users ?? []).map(user => ({
                queryKey: [USER_LICENSE_QUERY_KEY, user.Id],
                queryFn: ({ signal }: { signal: AbortSignal }) => fetchUserLicense(api!, user.Id!, { signal }),
                enabled: !!api && !!user.Id,
                retry: false
            }))
        ), [api, users])
    });

    const rows = useMemo(() => (
        (users ?? []).map((user, index) => {
            const licenseQuery = licenseQueries[index];
            const license = licenseQuery?.data;
            const licenseError = licenseQuery?.error as ApiError | undefined;
            const isAdmin = !!user.Policy?.IsAdministrator;
            const isNoLicense = licenseQuery?.isError && licenseError?.response?.status === 404;
            const hasError = !!licenseQuery?.isError && !isNoLicense;

            return {
                user,
                license,
                isAdmin,
                isNoLicense,
                isLoading: !!licenseQuery?.isPending,
                status: getStatus(license, isAdmin, isNoLicense, !!licenseQuery?.isPending, hasError, licenseError?.message)
            };
        })
    ), [users, licenseQueries]);

    const summary = useMemo(() => {
        const total = rows.length;
        const admins = rows.filter(r => r.isAdmin).length;
        const unlimited = rows.filter(r => r.license?.IsUnlimited).length;
        const expired = rows.filter(r => r.license?.IsExpired).length;
        const withoutLicense = rows.filter(r => !r.isAdmin && (r.isNoLicense || !r.license)).length;
        const active = total - admins - unlimited - expired - withoutLicense;
        return { total, admins, unlimited, expired, withoutLicense, active };
    }, [rows]);

    const timelineParams = useMemo(() => ({
        startIndex: 0,
        limit: 15,
        type: 'UserLicense',
        sortBy: [ActivityLogSortBy.DateCreated],
        sortOrder: [SortOrder.Descending]
    }), []);

    const {
        data: timelineData,
        isPending: isTimelineLoading
    } = useLogEntries(timelineParams);

    const timelineEntries = useMemo<ActivityLogEntry[]>(() => (
        timelineData?.Items || []
    ), [timelineData]);

    const handleDurationChange = useCallback((userId: string, value: string) => {
        setDurationDrafts(prev => ({
            ...prev,
            [userId]: value
        }));
    }, []);

    const handleSave = useCallback((userId: string, isAdmin: boolean) => {
        const selectedDuration = isAdmin ? null : durationDrafts[userId];

        if (!isAdmin && (selectedDuration === undefined || selectedDuration === '')) {
            toast('Selecione uma duração para aplicar.');
            return;
        }

        if (!isAdmin && Number.isNaN(Number.parseInt(selectedDuration ?? '', 10))) {
            toast('Selecione uma duração válida.');
            return;
        }

        setLicenseMutation.mutate({
            userId,
            request: {
                durationHours: isAdmin ? null : Number.parseInt(selectedDuration ?? '', 10)
            }
        }, {
            onSuccess: () => {
                toast(isAdmin ? 'Licença aplicada como ilimitada.' : 'Licença atualizada com sucesso.');
                setDurationDrafts(prev => {
                    const next = { ...prev };
                    delete next[userId];
                    return next;
                });
            },
            onError: (error) => {
                toast(`Erro ao salvar licença: ${(error as ApiError)?.message || 'erro desconhecido'}`);
            }
        });
    }, [durationDrafts, setLicenseMutation]);

    const handleRevoke = useCallback((userId: string, userName?: string | null) => {
        confirm({
            title: 'Revogar licença',
            text: userName
                ? `Tem certeza que deseja revogar a licença de ${userName}?`
                : 'Tem certeza que deseja revogar esta licença?',
            confirmText: 'Revogar',
            cancelText: 'Cancelar'
        }).then(() => {
            revokeLicenseMutation.mutate(userId, {
                onSuccess: () => {
                    toast('Licença revogada com sucesso.');
                },
                onError: (error) => {
                    toast(`Erro ao revogar licença: ${(error as ApiError)?.message || 'erro desconhecido'}`);
                }
            });
        }).catch(() => {
            // Cancelado pelo usuário.
        });
    }, [revokeLicenseMutation]);

    if (isPending) {
        return <Loading />;
    }

    return (
        <Page
            id='userLicensesPage'
            className='mainAnimatedPage type-interior'
            title='Licenças'
        >
            <Box className='content-primary'>
                <Stack spacing={2} sx={{ mb: 3 }}>
                    <Typography variant='h1'>
                        Licenças
                    </Typography>
                    <Typography color='text.secondary'>
                        Grid centralizada para acompanhar a data de início da licença e aplicar o tempo selecionado por usuário.
                    </Typography>
                    {isError && (
                        <Alert severity='error'>
                            Não foi possível carregar a lista de usuários.
                        </Alert>
                    )}
                </Stack>

                <Paper variant='outlined' sx={{ p: 2, mb: 3, borderRadius: 3 }}>
                    <Typography variant='h2' sx={{ mb: 1.5 }}>
                        Resumo de impacto
                    </Typography>
                    <Stack direction='row' spacing={1} flexWrap='wrap' useFlexGap>
                        <Chip label={`${summary.total} usuários`} variant='outlined' />
                        <Chip label={`${summary.admins} administradores`} color='info' />
                        <Chip label={`${summary.active} ativas`} color='success' />
                        <Chip label={`${summary.unlimited} ilimitadas`} color='info' variant='outlined' />
                        <Chip label={`${summary.expired} expiradas`} color='error' />
                        <Chip label={`${summary.withoutLicense} sem licença`} color='warning' variant='outlined' />
                    </Stack>
                    {summary.expired > 0 && (
                        <Alert severity='warning' sx={{ mt: 1.5 }}>
                            {summary.expired} usuário(s) com licença expirada. Eles serão desabilitados até que uma nova licença seja aplicada.
                        </Alert>
                    )}
                </Paper>

                <TableContainer component={Paper} sx={{ borderRadius: 3, overflowX: 'auto', overflowY: 'hidden' }}>
                    <Table stickyHeader size='small' sx={{ tableLayout: 'fixed', minWidth: 0 }}>
                        <TableHead>
                            <TableRow>
                                <TableCell sx={{ width: 190 }}>Usuário</TableCell>
                                <TableCell sx={{ width: 110 }}>Perfil</TableCell>
                                <TableCell sx={{ width: 210 }}>Situação</TableCell>
                                <TableCell sx={{ width: 150 }}>Tempo para adicionar</TableCell>
                                <TableCell sx={{ width: 150 }}>Ações</TableCell>
                            </TableRow>
                        </TableHead>
                        <TableBody>
                            {rows.map(({ user, license, isAdmin, isNoLicense, isLoading: rowIsLoading, status }) => {
                                const userId = user.Id || '';
                                const draftValue = durationDrafts[userId] ?? '';
                                const effectiveValue = isAdmin ? '-1' : draftValue;
                                const canSave = Boolean(!rowIsLoading && userId && (isAdmin || draftValue !== '') && !setLicenseMutation.isPending);

                                return (
                                    <TableRow key={userId} hover>
                                        <TableCell>
                                            <Stack spacing={0.5}>
                                                <Typography fontWeight='medium'>
                                                    {user.Name || userId}
                                                </Typography>
                                                <Typography variant='body2' color='text.secondary'>
                                                    {userId}
                                                </Typography>
                                            </Stack>
                                        </TableCell>
                                        <TableCell>
                                            <Stack direction='row' spacing={1} flexWrap='wrap' useFlexGap>
                                                {isAdmin ? (
                                                    <Chip label='Administrador' color='info' size='small' />
                                                ) : (
                                                    <Chip label='Usuário' variant='outlined' size='small' />
                                                )}
                                            </Stack>
                                        </TableCell>
                                        <TableCell>
                                            <Stack spacing={0.75}>
                                                <Chip label={status.label} color={status.color} size='small' />
                                                <Typography variant='body2' color='text.secondary'>
                                                    {status.details}
                                                </Typography>
                                                {license?.AdminNotes && !isNoLicense && (
                                                    <Typography variant='caption' color='text.secondary'>
                                                        Observação: {license.AdminNotes}
                                                    </Typography>
                                                )}
                                            </Stack>
                                        </TableCell>
                                        <TableCell>
                                            <FormControl size='small' sx={{ width: '100%', maxWidth: 140 }}>
                                                <InputLabel id={`license-duration-label-${userId}`}>Duração</InputLabel>
                                                <Select
                                                    labelId={`license-duration-label-${userId}`}
                                                    value={effectiveValue}
                                                    label='Duração'
                                                    onChange={(event: SelectChangeEvent) => handleDurationChange(userId, event.target.value)}
                                                    disabled={rowIsLoading || isAdmin}
                                                    MenuProps={{
                                                        PaperProps: {
                                                            sx: {
                                                                maxWidth: 260
                                                            }
                                                        }
                                                    }}
                                                >
                                                    {!isAdmin && (
                                                        <MenuItem value=''>
                                                            <em>Selecione...</em>
                                                        </MenuItem>
                                                    )}
                                                    {LICENSE_DURATION_OPTIONS.map(option => (
                                                        <MenuItem
                                                            key={option.value}
                                                            value={option.value}
                                                        >
                                                            <Stack spacing={0.25}>
                                                                <Typography variant='body2'>
                                                                    {option.label}
                                                                </Typography>
                                                                <Typography variant='caption' color='text.secondary'>
                                                                    {option.helper}
                                                                </Typography>
                                                            </Stack>
                                                        </MenuItem>
                                                    ))}
                                                </Select>
                                            </FormControl>
                                        </TableCell>
                                        <TableCell>
                                            <Stack direction='row' spacing={1} flexWrap='wrap' useFlexGap sx={{ minWidth: 0 }}>
                                                <Button
                                                    variant='contained'
                                                    onClick={() => handleSave(userId, isAdmin)}
                                                    disabled={!canSave}
                                                    size='small'
                                                >
                                                    {setLicenseMutation.isPending ? 'Salvando...' : 'Aplicar'}
                                                </Button>
                                                <Button
                                                    variant='outlined'
                                                    color='error'
                                                    onClick={() => handleRevoke(userId, user.Name)}
                                                    disabled={rowIsLoading || isNoLicense || revokeLicenseMutation.isPending}
                                                    size='small'
                                                >
                                                    Revogar
                                                </Button>
                                            </Stack>
                                        </TableCell>
                                    </TableRow>
                                );
                            })}
                        </TableBody>
                    </Table>
                </TableContainer>

                <Paper variant='outlined' sx={{ p: 2, mt: 3, borderRadius: 3 }}>
                    <Typography variant='h2' sx={{ mb: 1.5 }}>
                        Alterações recentes
                    </Typography>
                    {isTimelineLoading ? (
                        <CircularProgress size={24} />
                    ) : timelineEntries.length === 0 ? (
                        <Typography color='text.secondary'>
                            Nenhuma alteração de licença registrada ainda.
                        </Typography>
                    ) : (
                        <Stack spacing={1}>
                            {timelineEntries.map(entry => (
                                <Stack
                                    key={entry.Id}
                                    direction='row'
                                    spacing={2}
                                    alignItems='baseline'
                                    sx={{ borderBottom: '1px solid', borderColor: 'divider', py: 0.5 }}
                                >
                                    <Typography variant='caption' color='text.secondary' sx={{ minWidth: 160 }}>
                                        {entry.Date ? new Date(entry.Date).toLocaleString() : ''}
                                    </Typography>
                                    <Typography variant='body2' sx={{ flexGrow: 1 }}>
                                        {entry.Name}
                                    </Typography>
                                    {entry.Overview && (
                                        <Typography variant='caption' color='text.secondary'>
                                            {entry.Overview}
                                        </Typography>
                                    )}
                                </Stack>
                            ))}
                        </Stack>
                    )}
                </Paper>
            </Box>
        </Page>
    );
};

Component.displayName = 'UserLicensesPage';
