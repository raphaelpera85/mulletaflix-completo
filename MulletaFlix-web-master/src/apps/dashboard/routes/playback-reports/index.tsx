import parseISO from 'date-fns/parseISO';
import React, { useCallback, useMemo, useState } from 'react';
import { SortOrder } from '@jellyfin/sdk/lib/generated-client/models/sort-order';
import { useTheme } from '@mui/material/styles';
import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import { type MRT_ColumnDef, type MRT_Theme, type MRT_ColumnFiltersState, type MRT_SortingState, useMaterialReactTable, type MRT_TableOptions } from 'material-react-table';
import DateTimeCell from 'apps/dashboard/components/table/DateTimeCell';
import TablePage, { DEFAULT_TABLE_OPTIONS } from 'apps/dashboard/components/table/TablePage';
import UserAvatarButton from 'apps/dashboard/components/UserAvatarButton';
import { type UsersRecords, useUsersDetails } from 'hooks/useUsers';
import globalize from 'lib/globalize';
import { useSearchParams } from 'react-router-dom';
import { useApi } from 'hooks/useApi';
import Button from '@mui/material/Button';

import { usePlaybackReports, usePlaybackReportStats, downloadPlaybackReportsCsv, type PlaybackReportDto, type PlaybackReportSortBy } from 'apps/dashboard/features/playback/api/usePlaybackReports';
import type { PlaybackReportCell } from 'apps/dashboard/features/playback/types/PlaybackReportCell';
import { BarChart, LineChart } from 'apps/dashboard/features/playback/components/PlaybackCharts';

const DEFAULT_PAGE_SIZE = 25;

const enum PlaybackReportView {
    All = 'All',
    Transcoded = 'Transcoded',
    DirectPlay = 'DirectPlay',
    Errors = 'Errors'
}

const VIEW_PARAM = 'view';

const getPlaybackReportView = (param: string | null) => {
    if (param === null) return PlaybackReportView.All;
    switch (param) {
        case 'transcoded': return PlaybackReportView.Transcoded;
        case 'directplay': return PlaybackReportView.DirectPlay;
        case 'errors': return PlaybackReportView.Errors;
        default: return PlaybackReportView.All;
    }
};

const getUserCell = (users: UsersRecords) => function UserCell({ row }: PlaybackReportCell) {
    return (
        <UserAvatarButton user={row.original.UserId && users[row.original.UserId] || undefined} />
    );
};

const getWasTranscodedFilter = (view: PlaybackReportView): boolean | undefined => {
    if (view === PlaybackReportView.Transcoded) {
        return true;
    }

    if (view === PlaybackReportView.DirectPlay) {
        return false;
    }

    return undefined;
};

const getPlayMethodColor = (playMethod: string | undefined, transcode: string, directStream: string, directPlay: string): string => {
    if (playMethod === 'Transcode') {
        return transcode;
    }

    if (playMethod === 'DirectStream') {
        return directStream;
    }

    return directPlay;
};

const getViewParam = (view: PlaybackReportView): string => {
    switch (view) {
        case PlaybackReportView.Transcoded:
            return 'transcoded';
        case PlaybackReportView.DirectPlay:
            return 'directplay';
        case PlaybackReportView.Errors:
            return 'errors';
        default:
            return '';
    }
};

const formatDuration = (seconds: number | null | undefined): string => {
    if (!seconds) return '-';
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = seconds % 60;
    if (h > 0) return `${h}h ${m}m ${s}s`;
    if (m > 0) return `${m}m ${s}s`;
    return `${s}s`;
};

const formatPercentage = (value: number | null | undefined): string => {
    if (value === null || value === undefined) return '-';
    return `${value.toFixed(1)}%`;
};

const formatBitrate = (bitrate: number | null | undefined): string => {
    if (!bitrate) return '-';
    return `${(bitrate / 1000).toFixed(0)} kbps`;
};

const PLAYBACK_REPORT_SORT_BY: Record<string, PlaybackReportSortBy> = {
    StartTimeUtc: 'DateCreated',
    UserId: 'UserId',
    ItemId: 'ItemId',
    DurationSeconds: 'DurationSeconds',
    CompletionPercentage: 'CompletionPercentage',
    Bitrate: 'Bitrate'
} as const;

export const Component = () => {
    const { api } = useApi();
    const [ searchParams, setSearchParams ] = useSearchParams();
    const [ columnFilters, setColumnFilters ] = useState<MRT_ColumnFiltersState>([]);
    const [ playbackView, setPlaybackView ] = useState(
        getPlaybackReportView(searchParams.get(VIEW_PARAM)));
    const [ sorting, setSorting ] = useState<MRT_SortingState>([{ id: 'StartTimeUtc', desc: true }]);
    const [ pagination, setPagination ] = useState({
        pageIndex: 0,
        pageSize: DEFAULT_PAGE_SIZE
    });
    const [ showStats, setShowStats ] = useState(false);

    const {
        usersById: users,
        names: userNames,
        isLoading: isUsersLoading,
        isError: isUsersError
    } = useUsersDetails();

    const UserCell = getUserCell(users);

    const handlePlaybackViewChange = useCallback((_: unknown, value: unknown) => {
        if (value !== null) {
            const newView = value as PlaybackReportView;
            setPlaybackView(newView);
            setSearchParams({ [VIEW_PARAM]: getViewParam(newView) }, { replace: true });
        }
    }, [ setSearchParams ]);

    const handleToggleStats = useCallback(() => {
        setShowStats(prev => !prev);
    }, []);

    const playbackParams = useMemo(() => {
        const getFilter = (id: string) => columnFilters.find(f => f.id === id)?.value;
        const sortFields: PlaybackReportSortBy[] = [];
        const sortOrders: SortOrder[] = [];

        const mapSortField = (id: string): PlaybackReportSortBy => {
            return PLAYBACK_REPORT_SORT_BY[id] || 'DateCreated';
        };

        if (sorting.length === 0) {
            sortFields.push('DateCreated');
            sortOrders.push(SortOrder.Descending);
        } else {
            sorting.forEach(sort => {
                sortFields.push(mapSortField(sort.id));
                sortOrders.push(sort.desc ? SortOrder.Descending : SortOrder.Ascending);
            });
        }

        return {
            userId: undefined,
            itemId: undefined,
            deviceId: getFilter('DeviceName') as string || undefined,
            libraryId: undefined,
            minDate: (getFilter('StartTimeUtc') as string[] | undefined)?.[0] ?? undefined,
            maxDate: (getFilter('StartTimeUtc') as string[] | undefined)?.[1] ?? undefined,
            itemType: undefined,
            wasTranscoded: getWasTranscodedFilter(playbackView),
            playedToCompletion: undefined,
            hasError: playbackView === PlaybackReportView.Errors ? true : undefined,
            skip: pagination.pageIndex * pagination.pageSize,
            limit: pagination.pageSize,
            sortBy: sortFields,
            sortOrder: sortOrders
        };
    }, [ pagination, columnFilters, sorting, playbackView ]);

    const handleExportCsv = useCallback(() => {
        if (api) {
            downloadPlaybackReportsCsv(api, playbackParams).catch(() => undefined);
        }
    }, [ api, playbackParams ]);

    const {
        data,
        isLoading: isReportsLoading,
        isError: isReportsError
    } = usePlaybackReports(playbackParams);

    const {
        data: stats,
        isLoading: isStatsLoading
    } = usePlaybackReportStats(playbackParams);

    const playbackReports = useMemo(() => (
        data?.Items || []
    ), [ data ]);

    const rowCount = useMemo(() => (
        data?.TotalRecordCount || 0
    ), [ data ]);

    const isLoading = isUsersLoading || isReportsLoading;

    const theme = useTheme();

    const userColumn: MRT_ColumnDef<PlaybackReportDto>[] = useMemo(() => [{
        id: 'User',
        accessorFn: row => row.UserId && users[row.UserId]?.Name,
        header: globalize.translate('LabelUser'),
        size: 100,
        Cell: UserCell,
        enableResizing: false,
        muiTableBodyCellProps: {
            align: 'center'
        },
        filterVariant: 'select',
        filterSelectOptions: userNames
    }], [ userNames, users, UserCell ]);

    const columns = useMemo<MRT_ColumnDef<PlaybackReportDto>[]>(() => [
        {
            id: 'StartTimeUtc',
            accessorFn: row => row.StartTimeUtc ? parseISO(row.StartTimeUtc) : undefined,
            header: globalize.translate('LabelTime'),
            size: 160,
            Cell: DateTimeCell,
            filterVariant: 'datetime-range',
            grow: true,
            maxSize: 320
        },
        ...userColumn,
        {
            accessorKey: 'ItemName',
            header: globalize.translate('LabelName'),
            size: 270,
            grow: true,
            Cell: ({ row }) => (
                <div>
                    <div style={{ fontWeight: 500 }}>{row.original.ItemName}</div>
                    {row.original.SeriesName && (
                        <div style={{ fontSize: '0.85em', color: theme.palette.text.secondary }}>
                            {row.original.SeriesName}
                            {row.original.SeasonNumber && row.original.EpisodeNumber && (
                                <> S{row.original.SeasonNumber.toString().padStart(2, '0')}E{row.original.EpisodeNumber.toString().padStart(2, '0')}</>
                            )}
                        </div>
                    )}
                    {row.original.Artist && (
                        <div style={{ fontSize: '0.85em', color: theme.palette.text.secondary }}>
                            {row.original.Artist}
                            {row.original.Album && <span> - {row.original.Album}</span>}
                        </div>
                    )}
                </div>
            )
        },
        {
            accessorKey: 'ItemType',
            header: globalize.translate('LabelType'),
            size: 100,
            grow: true,
            filterVariant: 'select',
            filterSelectOptions: ['Movie', 'Series', 'Episode', 'Audio', 'MusicVideo', 'LiveTv', 'Photo', 'Trailer']
        },
        {
            accessorKey: 'DeviceName',
            header: globalize.translate('LabelDevice'),
            size: 150,
            grow: true,
            filterVariant: 'text'
        },
        {
            accessorKey: 'ClientName',
            header: 'Client',
            size: 120,
            grow: true,
            filterVariant: 'text'
        },
        {
            id: 'DurationSeconds',
            accessorFn: row => row.DurationSeconds,
            header: 'Duration',
            size: 100,
            Cell: ({ row }) => <span>{formatDuration(row.original.DurationSeconds)}</span>,
            filterVariant: 'range',
            enableResizing: false,
            muiTableBodyCellProps: { align: 'right' }
        },
        {
            id: 'CompletionPercentage',
            accessorFn: row => row.CompletionPercentage,
            header: 'Completion',
            size: 100,
            Cell: ({ row }) => <span>{formatPercentage(row.original.CompletionPercentage)}</span>,
            filterVariant: 'range',
            enableResizing: false,
            muiTableBodyCellProps: { align: 'right' }
        },
        {
            id: 'PlayMethod',
            accessorKey: 'PlayMethod',
            header: 'Method',
            size: 110,
            grow: true,
            filterVariant: 'select',
            filterSelectOptions: [
                { label: 'DirectPlay', value: 'DirectPlay' },
                { label: 'DirectStream', value: 'DirectStream' },
                { label: 'Transcode', value: 'Transcode' }
            ],
            Cell: ({ row }) => (
                <span style={{
                    backgroundColor: getPlayMethodColor(row.original.PlayMethod, theme.palette.error.light, theme.palette.warning.light, theme.palette.success.light),
                    color: getPlayMethodColor(row.original.PlayMethod, theme.palette.error.dark, theme.palette.warning.dark, theme.palette.success.dark),
                    padding: '2px 8px',
                    borderRadius: '4px',
                    fontSize: '0.85em',
                    fontWeight: 500
                }}>
                    {row.original.PlayMethod || '-'}
                </span>
            )
        },
        {
            id: 'Bitrate',
            accessorFn: row => row.Bitrate,
            header: 'Bitrate',
            size: 100,
            Cell: ({ row }) => <span>{formatBitrate(row.original.Bitrate)}</span>,
            filterVariant: 'range',
            enableResizing: false,
            muiTableBodyCellProps: { align: 'right' }
        },
        {
            id: 'Resolution',
            accessorFn: row => row.Width && row.Height ? `${row.Width}x${row.Height}` : '-',
            header: 'Resolution',
            size: 100,
            enableResizing: false,
            muiTableBodyCellProps: { align: 'center' }
        },
        {
            id: 'WasTranscoded',
            accessorKey: 'WasTranscoded',
            header: 'Transcoded',
            size: 90,
            Cell: ({ row }) => row.original.WasTranscoded ? '✓' : '✗',
            filterVariant: 'select',
            filterSelectOptions: [
                { label: 'Yes', value: true },
                { label: 'No', value: false }
            ],
            enableResizing: false,
            muiTableBodyCellProps: { align: 'center' }
        },
        {
            id: 'PlayedToCompletion',
            accessorKey: 'PlayedToCompletion',
            header: 'Completed',
            size: 90,
            Cell: ({ row }) => row.original.PlayedToCompletion ? '✓' : '✗',
            filterVariant: 'select',
            filterSelectOptions: [
                { label: 'Yes', value: true },
                { label: 'No', value: false }
            ],
            enableResizing: false,
            muiTableBodyCellProps: { align: 'center' }
        },
        {
            id: 'ErrorMessage',
            accessorKey: 'ErrorMessage',
            header: 'Error',
            size: 150,
            grow: true,
            Cell: ({ row }) => row.original.ErrorMessage ? (
                <span style={{ color: theme.palette.error.main, fontStyle: 'italic' }}>
                    {row.original.ErrorMessage.length > 50 ? row.original.ErrorMessage.substring(0, 50) + '...' : row.original.ErrorMessage}
                </span>
            ) : '-'
        },
        {
            id: 'Library',
            accessorKey: 'LibraryName',
            header: 'Library',
            size: 150,
            grow: true,
            filterVariant: 'text'
        }
    ], [ userColumn, users, userNames, UserCell, theme ]);

    const viewButtons = [
        { id: PlaybackReportView.All, label: 'All' },
        { id: PlaybackReportView.DirectPlay, label: 'Direct Play' },
        { id: PlaybackReportView.Transcoded, label: 'Transcoded' },
        { id: PlaybackReportView.Errors, label: 'Errors' }
    ];

    // NOTE: We need to provide a custom theme due to a MRT bug causing the initial theme to always be used
    // https://github.com/KevinVandy/material-react-table/issues/1429
    const mrtTheme = useMemo<Partial<MRT_Theme>>(() => ({
        baseBackgroundColor: theme.palette.background.paper
    }), [ theme ]);

    const table = useMaterialReactTable<PlaybackReportDto>({
        ...(DEFAULT_TABLE_OPTIONS as unknown as MRT_TableOptions<PlaybackReportDto>),
        mrtTheme,

        columns,
        data: playbackReports,

        // State
        initialState: {
            density: 'compact',
            showColumnFilters: false
        },
        state: {
            isLoading,
            columnFilters,
            pagination,
            sorting
        },

        manualFiltering: true,
        manualSorting: true,
        onColumnFiltersChange: setColumnFilters,
        onSortingChange: setSorting,
        enableMultiSort: true,
        enableGlobalFilter: false,

        // Server pagination
        manualPagination: true,
        onPaginationChange: setPagination,
        rowCount,

        // Custom toolbar contents
        renderTopToolbarCustomActions: () => (
            <>
                <ToggleButtonGroup
                    value={playbackView}
                    exclusive
                    onChange={handlePlaybackViewChange}
                    size='small'
                    aria-label='Playback report view'
                >
                    {viewButtons.map(btn => (
                        <ToggleButton key={btn.id} value={btn.id}>
                            {btn.label}
                        </ToggleButton>
                    ))}
                </ToggleButtonGroup>
                <ToggleButton
                    value={showStats}
                    onChange={handleToggleStats}
                    size='small'
                    aria-label='Show stats'
                >
                    📊 Stats
                </ToggleButton>
                <Button
                    variant='outlined'
                    size='small'
                    onClick={handleExportCsv}
                    aria-label='Export CSV'
                >
                    Export CSV
                </Button>
            </>
        )
    });

    return (
        <TablePage
            id='playbackReportsPage'
            title='Playback Reports'
            className='mainAnimatedPage type-interior'
            table={table}
            isError={isReportsError || isUsersError}
            errorMessage={globalize.translate('ActivitiesLoadError')}
        >
            {showStats && stats && !isStatsLoading ? (
                <div style={{ padding: '16px', backgroundColor: theme.palette.background.default, borderRadius: '8px' }}>
                    <h3 style={{ marginBottom: '16px' }}>Playback Statistics</h3>
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px' }}>
                        <div style={{ padding: '12px', backgroundColor: theme.palette.background.paper, borderRadius: '4px' }}>
                            <div style={{ fontSize: '2em', fontWeight: 'bold' }}>{stats.TotalPlays}</div>
                            <div style={{ color: theme.palette.text.secondary }}>Total Plays</div>
                        </div>
                        <div style={{ padding: '12px', backgroundColor: theme.palette.background.paper, borderRadius: '4px' }}>
                            <div style={{ fontSize: '2em', fontWeight: 'bold' }}>{stats.UniqueUsers}</div>
                            <div style={{ color: theme.palette.text.secondary }}>Unique Users</div>
                        </div>
                        <div style={{ padding: '12px', backgroundColor: theme.palette.background.paper, borderRadius: '4px' }}>
                            <div style={{ fontSize: '2em', fontWeight: 'bold' }}>{stats.UniqueItems}</div>
                            <div style={{ color: theme.palette.text.secondary }}>Unique Items</div>
                        </div>
                        <div style={{ padding: '12px', backgroundColor: theme.palette.background.paper, borderRadius: '4px' }}>
                            <div style={{ fontSize: '2em', fontWeight: 'bold' }}>{formatDuration(stats.TotalDurationSeconds)}</div>
                            <div style={{ color: theme.palette.text.secondary }}>Total Duration</div>
                        </div>
                        <div style={{ padding: '12px', backgroundColor: theme.palette.background.paper, borderRadius: '4px' }}>
                            <div style={{ fontSize: '2em', fontWeight: 'bold' }}>{formatPercentage(stats.AverageCompletionPercentage)}</div>
                            <div style={{ color: theme.palette.text.secondary }}>Avg Completion</div>
                        </div>
                        <div style={{ padding: '12px', backgroundColor: theme.palette.background.paper, borderRadius: '4px' }}>
                            <div style={{ fontSize: '2em', fontWeight: 'bold' }}>{stats.TranscodedPlays}</div>
                            <div style={{ color: theme.palette.text.secondary }}>Transcoded</div>
                        </div>
                        <div style={{ padding: '12px', backgroundColor: theme.palette.background.paper, borderRadius: '4px' }}>
                            <div style={{ fontSize: '2em', fontWeight: 'bold' }}>{stats.DirectPlayPlays}</div>
                            <div style={{ color: theme.palette.text.secondary }}>Direct Play</div>
                        </div>
                        <div style={{ padding: '12px', backgroundColor: theme.palette.background.paper, borderRadius: '4px' }}>
                            <div style={{ fontSize: '2em', fontWeight: 'bold' }}>{stats.DirectStreamPlays}</div>
                            <div style={{ color: theme.palette.text.secondary }}>Direct Stream</div>
                        </div>
                        <div style={{ padding: '12px', backgroundColor: theme.palette.background.paper, borderRadius: '4px' }}>
                            <div style={{ fontSize: '2em', fontWeight: 'bold' }}>{stats.ErrorPlays}</div>
                            <div style={{ color: theme.palette.text.secondary }}>Errors</div>
                        </div>
                    </div>

                    {Object.entries(stats.PlaysByItemType).length > 0 && (
                        <div style={{ marginTop: '24px' }}>
                            <h4>Plays by Item Type</h4>
                            <BarChart data={stats.PlaysByItemType} />
                        </div>
                    )}

                    {Object.entries(stats.PlaysByDate).length > 0 && (
                        <div style={{ marginTop: '24px' }}>
                            <h4>Plays per Day</h4>
                            <LineChart data={stats.PlaysByDate} />
                        </div>
                    )}

                    {Object.entries(stats.PlaysByPlayMethod).length > 0 && (
                        <div style={{ marginTop: '16px' }}>
                            <h4>Plays by Method</h4>
                            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
                                {Object.entries(stats.PlaysByPlayMethod).map(([method, count]) => (
                                    <div key={method} style={{ padding: '8px 12px', backgroundColor: theme.palette.background.paper, borderRadius: '4px' }}>
                                        <strong>{method}:</strong> {count}
                                    </div>
                                ))}
                            </div>
                        </div>
                    )}

                    {Object.entries(stats.TopUsers).length > 0 && (
                        <div style={{ marginTop: '16px' }}>
                            <h4>Top Users</h4>
                            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                                <thead>
                                    <tr style={{ borderBottom: `1px solid ${theme.palette.divider}` }}>
                                        <th style={{ textAlign: 'left', padding: '8px' }}>User</th>
                                        <th style={{ textAlign: 'right', padding: '8px' }}>Plays</th>
                                        <th style={{ textAlign: 'right', padding: '8px' }}>Duration</th>
                                        <th style={{ textAlign: 'right', padding: '8px' }}>Avg Completion</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {Object.values(stats.TopUsers).map(user => (
                                        <tr key={user.UserId} style={{ borderBottom: `1px solid ${theme.palette.divider}` }}>
                                            <td style={{ padding: '8px' }}>{user.Username}</td>
                                            <td style={{ textAlign: 'right', padding: '8px' }}>{user.PlayCount}</td>
                                            <td style={{ textAlign: 'right', padding: '8px' }}>{formatDuration(user.TotalDurationSeconds)}</td>
                                            <td style={{ textAlign: 'right', padding: '8px' }}>{formatPercentage(user.AverageCompletionPercentage)}</td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}

                    {Object.entries(stats.TopItems).length > 0 && (
                        <div style={{ marginTop: '16px' }}>
                            <h4>Top Items</h4>
                            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                                <thead>
                                    <tr style={{ borderBottom: `1px solid ${theme.palette.divider}` }}>
                                        <th style={{ textAlign: 'left', padding: '8px' }}>Item</th>
                                        <th style={{ textAlign: 'center', padding: '8px' }}>Type</th>
                                        <th style={{ textAlign: 'right', padding: '8px' }}>Plays</th>
                                        <th style={{ textAlign: 'right', padding: '8px' }}>Duration</th>
                                        <th style={{ textAlign: 'right', padding: '8px' }}>Avg Completion</th>
                                        <th style={{ textAlign: 'right', padding: '8px' }}>Users</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {Object.values(stats.TopItems).map(item => (
                                        <tr key={item.ItemId} style={{ borderBottom: `1px solid ${theme.palette.divider}` }}>
                                            <td style={{ padding: '8px' }}>{item.ItemName}</td>
                                            <td style={{ textAlign: 'center', padding: '8px' }}>{item.ItemType}</td>
                                            <td style={{ textAlign: 'right', padding: '8px' }}>{item.PlayCount}</td>
                                            <td style={{ textAlign: 'right', padding: '8px' }}>{formatDuration(item.TotalDurationSeconds)}</td>
                                            <td style={{ textAlign: 'right', padding: '8px' }}>{formatPercentage(item.AverageCompletionPercentage)}</td>
                                            <td style={{ textAlign: 'right', padding: '8px' }}>{item.UniqueUsers}</td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}
                </div>
            ) : null}
        </TablePage>
    );
};

export default Component;
