import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import parseISO from 'date-fns/parseISO';
import React, { useMemo } from 'react';
import type { MRT_ColumnDef } from 'material-react-table';
import type { ActivityLogEntry } from '@jellyfin/sdk/lib/generated-client/models/activity-log-entry';
import { ActivityLogSortBy } from '@jellyfin/sdk/lib/generated-client/models/activity-log-sort-by';
import { SortOrder } from '@jellyfin/sdk/lib/generated-client/models/sort-order';

import DateTimeCell from 'apps/dashboard/components/table/DateTimeCell';
import { DEFAULT_TABLE_OPTIONS } from 'apps/dashboard/components/table/TablePage';
import { useLogEntries } from 'apps/dashboard/features/activity/api/useLogEntries';
import { useUsersDetails } from 'hooks/useUsers';
import globalize from 'lib/globalize';
import { useMaterialReactTable, MaterialReactTable } from 'material-react-table';
import Page from 'components/Page';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import type { ApiClient } from 'jellyfin-apiclient';
import { useQuery } from '@tanstack/react-query';

interface UserFeedbackListPageProps {
    type: 'MediaRequest' | 'PlaybackIssue';
    titleKey: string;
}

interface MediaCatalogTitle {
    Title: string;
    MediaType: string;
    Year?: number;
}

const normalizeTitle = (value: string) => value.normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLocaleLowerCase()
    .replace(/[^\p{L}\p{N}]+/gu, ' ')
    .trim();

const getMediaCatalog = async (): Promise<MediaCatalogTitle[]> => {
    const apiClient = ServerConnections.currentApiClient() as unknown as ApiClient | null;
    if (!apiClient) throw new Error('Cliente da API indisponível.');
    return await apiClient.getJSON(apiClient.getUrl('UserFeedback/MediaRequestCatalog')) as MediaCatalogTitle[];
};

const UserFeedbackListPage = ({ type, titleKey }: UserFeedbackListPageProps) => {
    const { usersById, isLoading: isUsersLoading, isError: isUsersError, refetch: refetchUsers } = useUsersDetails();
    const { data, isLoading, isError, refetch } = useLogEntries({
        type,
        startIndex: 0,
        limit: 1000,
        sortBy: [ ActivityLogSortBy.DateCreated ],
        sortOrder: [ SortOrder.Descending ]
    });
    const catalogQuery = useQuery({
        queryKey: [ 'MediaRequestCatalog' ],
        queryFn: getMediaCatalog,
        enabled: type === 'MediaRequest',
        staleTime: 60_000
    });

    const catalogTitles = useMemo(() => new Set((catalogQuery.data || []).map(item => normalizeTitle(item.Title))), [ catalogQuery.data ]);
    const categorizedRequests = useMemo(() => (data?.Items || []).map(entry => ({
        entry,
        title: entry.Name?.replace(/^Solicitação de mídia:\s*/i, '') || '',
        included: catalogTitles.has(normalizeTitle(entry.Name?.replace(/^Solicitação de mídia:\s*/i, '') || ''))
    })), [ catalogTitles, data?.Items ]);
    const pendingRequests = useMemo(() => categorizedRequests.filter(item => !item.included).map(item => item.entry), [ categorizedRequests ]);
    const includedRequests = useMemo(() => categorizedRequests.filter(item => item.included).map(item => item.entry), [ categorizedRequests ]);

    const columns = useMemo<MRT_ColumnDef<ActivityLogEntry>[]>(() => [
        {
            id: 'Date',
            accessorFn: row => row.Date ? parseISO(row.Date) : undefined,
            header: globalize.translate('LabelTime'),
            Cell: DateTimeCell,
            size: 180
        },
        {
            id: 'User',
            accessorFn: row => row.UserId ? usersById[row.UserId]?.Name || row.UserId : globalize.translate('LabelSystem'),
            header: globalize.translate('LabelUser'),
            size: 160
        },
        {
            accessorFn: row => row.Name?.replace(/^Solicitação de mídia:\s*/i, '') || row.Name,
            id: 'Name',
            header: globalize.translate('LabelName'),
            size: 240,
            grow: true
        },
        { accessorKey: 'Overview', header: globalize.translate('LabelType'), size: 140 },
        { accessorKey: 'ShortOverview', header: globalize.translate('LabelOverview'), size: 320, grow: true },
        ...(type === 'PlaybackIssue' ? [{ accessorKey: 'ItemId' as const, header: 'Item ID', size: 220 }] : [])
    ], [ type, usersById ]);

    const tableState = { isLoading: isLoading || isUsersLoading || (type === 'MediaRequest' && catalogQuery.isLoading) };
    const allRows = data?.Items || [];
    const pendingTable = useMaterialReactTable({
        ...DEFAULT_TABLE_OPTIONS,
        columns,
        data: type === 'MediaRequest' ? pendingRequests : allRows,
        state: tableState,
        enableGlobalFilter: false,
        enableColumnFilters: false,
        enableSorting: false,
        enablePagination: true,
        initialState: { density: 'compact', pagination: { pageIndex: 0, pageSize: 25 } }
    });
    const includedTable = useMaterialReactTable({
        ...DEFAULT_TABLE_OPTIONS,
        columns,
        data: includedRequests,
        state: tableState,
        enableGlobalFilter: false,
        enableColumnFilters: false,
        enableSorting: false,
        enablePagination: true,
        initialState: { density: 'compact', pagination: { pageIndex: 0, pageSize: 25 } }
    });

    const retry = () => { void Promise.all([ refetch(), refetchUsers(), catalogQuery.refetch() ]); };
    const hasError = isError || isUsersError || (type === 'MediaRequest' && catalogQuery.isError);

    return (
        <Page id={`userFeedback-${type}`} title={globalize.translate(titleKey)} className='mainAnimatedPage type-interior'>
            <Box className='content-primary' sx={{ display: 'flex', flexDirection: 'column', gap: 3, height: '100%' }}>
                <Typography variant='h1'>{globalize.translate(titleKey)}</Typography>
                {hasError ? (
                    <Alert severity='error' action={<Button color='inherit' size='small' onClick={retry}>{globalize.translate('Retry')}</Button>}>
                        {globalize.translate('ActivitiesLoadError')}
                    </Alert>
                ) : type === 'PlaybackIssue' ? (
                    <MaterialReactTable table={pendingTable} />
                ) : (
                    <Stack spacing={4} sx={{ minHeight: 0 }}>
                        <Box>
                            <Typography variant='h2' sx={{ mb: 1 }}>{globalize.translate('MediaRequestsPendingTitle')} ({pendingRequests.length})</Typography>
                            <MaterialReactTable table={pendingTable} />
                        </Box>
                        <Box>
                            <Typography variant='h2' sx={{ mb: 1 }}>{globalize.translate('MediaRequestsIncludedTitle')} ({includedRequests.length})</Typography>
                            <MaterialReactTable table={includedTable} />
                        </Box>
                    </Stack>
                )}
            </Box>
        </Page>
    );
};

export default UserFeedbackListPage;
