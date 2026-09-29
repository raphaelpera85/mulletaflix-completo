import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import parseISO from 'date-fns/parseISO';
import React, { useCallback, useMemo } from 'react';
import { MaterialReactTable, useMaterialReactTable, type MRT_ColumnDef } from 'material-react-table';
import type { ActivityLogEntry } from '@jellyfin/sdk/lib/generated-client/models/activity-log-entry';
import { ActivityLogSortBy } from '@jellyfin/sdk/lib/generated-client/models/activity-log-sort-by';
import { SortOrder } from '@jellyfin/sdk/lib/generated-client/models/sort-order';

import DateTimeCell from 'apps/dashboard/components/table/DateTimeCell';
import { DEFAULT_TABLE_OPTIONS } from 'apps/dashboard/components/table/TablePage';
import { useLogEntries } from 'apps/dashboard/features/activity/api/useLogEntries';
import { useUsersDetails } from 'hooks/useUsers';
import globalize from 'lib/globalize';
import Page from 'components/Page';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import type { ApiClient } from 'jellyfin-apiclient';
import { useQuery } from '@tanstack/react-query';
import { classifyMediaRequests, type MediaCatalogTitle } from 'utils/mediaRequests';

interface UserFeedbackListPageProps {
    type: 'MediaRequest' | 'PlaybackIssue';
    titleKey: string;
}

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

    const { pending: pendingRequests, included: includedRequests } = useMemo(
        () => classifyMediaRequests(data?.Items || [], catalogQuery.data || []),
        [ catalogQuery.data, data?.Items ]
    );

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

    const retry = useCallback(() => {
        void Promise.all([ refetch(), refetchUsers(), catalogQuery.refetch() ]);
    }, [ catalogQuery, refetch, refetchUsers ]);
    const hasError = isError || isUsersError || (type === 'MediaRequest' && catalogQuery.isError);
    let content;
    if (hasError) {
        content = (
            <Alert severity='error' action={<Button color='inherit' size='small' onClick={retry}>{globalize.translate('Retry')}</Button>}>
                {globalize.translate('ActivitiesLoadError')}
            </Alert>
        );
    } else if (type === 'PlaybackIssue') {
        content = <MaterialReactTable table={pendingTable} />;
    } else {
        content = (
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
        );
    }

    return (
        <Page id={`userFeedback-${type}`} title={globalize.translate(titleKey)} className='mainAnimatedPage type-interior'>
            <Box className='content-primary' sx={{ display: 'flex', flexDirection: 'column', gap: 3, height: '100%' }}>
                <Typography variant='h1'>{globalize.translate(titleKey)}</Typography>
                {content}
            </Box>
        </Page>
    );
};

export default UserFeedbackListPage;
