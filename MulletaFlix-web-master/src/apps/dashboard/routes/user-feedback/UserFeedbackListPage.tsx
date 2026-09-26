import parseISO from 'date-fns/parseISO';
import React, { useMemo } from 'react';
import type { MRT_ColumnDef } from 'material-react-table';
import type { ActivityLogEntry } from '@jellyfin/sdk/lib/generated-client/models/activity-log-entry';
import { ActivityLogSortBy } from '@jellyfin/sdk/lib/generated-client/models/activity-log-sort-by';
import { SortOrder } from '@jellyfin/sdk/lib/generated-client/models/sort-order';

import DateTimeCell from 'apps/dashboard/components/table/DateTimeCell';
import TablePage, { DEFAULT_TABLE_OPTIONS } from 'apps/dashboard/components/table/TablePage';
import { useLogEntries } from 'apps/dashboard/features/activity/api/useLogEntries';
import { useUsersDetails } from 'hooks/useUsers';
import globalize from 'lib/globalize';
import { useMaterialReactTable } from 'material-react-table';

interface UserFeedbackListPageProps {
    type: 'MediaRequest' | 'PlaybackIssue';
    titleKey: string;
}

const UserFeedbackListPage = ({ type, titleKey }: UserFeedbackListPageProps) => {
    const { usersById, isLoading: isUsersLoading, isError: isUsersError, refetch: refetchUsers } = useUsersDetails();
    const { data, isLoading, isError, refetch } = useLogEntries({
        type,
        startIndex: 0,
        limit: 200,
        sortBy: [ ActivityLogSortBy.DateCreated ],
        sortOrder: [ SortOrder.Descending ]
    });

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
            accessorKey: 'Name',
            header: globalize.translate('LabelName'),
            size: 240,
            grow: true
        },
        {
            accessorKey: 'Overview',
            header: globalize.translate('LabelType'),
            size: 140
        },
        {
            accessorKey: 'ShortOverview',
            header: globalize.translate('LabelOverview'),
            size: 320,
            grow: true
        },
        ...(type === 'PlaybackIssue' ? [{
            accessorKey: 'ItemId' as const,
            header: 'Item ID',
            size: 220
        }] : [])
    ], [ type, usersById ]);

    const table = useMaterialReactTable({
        ...DEFAULT_TABLE_OPTIONS,
        columns,
        data: data?.Items || [],
        state: { isLoading: isLoading || isUsersLoading },
        enableGlobalFilter: false,
        enableColumnFilters: false,
        enableSorting: false,
        enablePagination: true,
        initialState: { density: 'compact', pagination: { pageIndex: 0, pageSize: 25 } }
    });

    const retry = () => { void Promise.all([ refetch(), refetchUsers() ]); };

    return (
        <TablePage
            id={`userFeedback-${type}`}
            title={globalize.translate(titleKey)}
            className='mainAnimatedPage type-interior'
            table={table}
            isError={isError || isUsersError}
            errorMessage={globalize.translate('ActivitiesLoadError')}
            onRetry={retry}
        />
    );
};

export default UserFeedbackListPage;
