import React, { useCallback, useMemo, useState } from 'react';
import type { Api } from '@jellyfin/sdk';
import type { AxiosRequestConfig } from 'axios';
import globalize from 'lib/globalize';
import { useQuery } from '@tanstack/react-query';
import { useApi } from 'hooks/useApi';

import Widget from 'apps/dashboard/components/widgets/Widget';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import TablePagination from '@mui/material/TablePagination';
import TableSortLabel from '@mui/material/TableSortLabel';
import Toolbar from '@mui/material/Toolbar';
import Box from '@mui/material/Box';
import TextField from '@mui/material/TextField';
import InputAdornment from '@mui/material/InputAdornment';
import SearchIcon from '@mui/icons-material/Search';
import FilterListIcon from '@mui/icons-material/FilterList';
import Chip from '@mui/material/Chip';
import IconButton from '@mui/material/IconButton';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import Select, { type SelectChangeEvent } from '@mui/material/Select';
import FormControl from '@mui/material/FormControl';
import InputLabel from '@mui/material/InputLabel';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorIcon from '@mui/icons-material/Error';
import DownloadIcon from '@mui/icons-material/Download';
import { useNavigate } from 'react-router-dom';

const ACTION_TYPES = [
    'UserCreated', 'UserUpdated', 'UserDeleted',
    'PluginInstalled', 'PluginUninstalled', 'PluginEnabled', 'PluginDisabled', 'PluginUpdated',
    'BackupCreated', 'BackupRestored', 'BackupFailed',
    'LibraryCreated', 'LibraryUpdated', 'LibraryDeleted', 'LibraryScanned',
    'TaskCreated', 'TaskUpdated', 'TaskDeleted', 'TaskExecuted',
    'SystemRestarted', 'SystemShutdown', 'SystemUpdated',
    'ScheduledTaskCreated', 'ScheduledTaskUpdated', 'ScheduledTaskDeleted', 'ScheduledTaskExecuted',
    'UserLogin', 'UserLogout', 'UserPasswordChanged',
    'MetadataRefreshed', 'ImageUpdated',
    'PluginConfigurationChanged'
];

const ENTITY_TYPES = [
    'User', 'Plugin', 'Backup', 'Library', 'Task', 'ScheduledTask',
    'System', 'Metadata', 'Image', 'PluginConfiguration'
];

const CATEGORIES = [
    'UserManagement', 'PluginManagement', 'BackupRestore', 'LibraryManagement',
    'TaskManagement', 'SystemConfiguration', 'ScheduledTask', 'Authentication',
    'PluginConfiguration', 'MetadataManagement'
];

interface ActionLogDto {
    id: number;
    actionType: string;
    entityType: string;
    entityId: string | null;
    userId: string;
    username: string;
    dateCreated: string;
    details: string | null;
    oldValues: string | null;
    newValues: string | null;
    ipAddress: string | null;
    userAgent: string | null;
    isSuccess: boolean;
    errorMessage: string | null;
    category: string;
}

interface ActionLogQueryResult {
    items: ActionLogDto[];
    totalRecordCount: number;
    startIndex: number;
}

interface ActionLogQuery {
    startIndex?: number;
    limit?: number;
    minDate?: string;
    maxDate?: string;
    actionType?: string;
    entityType?: string;
    userId?: string;
    username?: string;
    isSuccess?: boolean;
    category?: string;
}

const fetchActionLogs = async (api: Api, query: ActionLogQuery, options?: AxiosRequestConfig) => {
    const params = new URLSearchParams();
    const queryEntries: Array<[string, string | number | boolean | undefined]> = [
        [ 'startIndex', query.startIndex ],
        [ 'limit', query.limit ],
        [ 'minDate', query.minDate ],
        [ 'maxDate', query.maxDate ],
        [ 'actionType', query.actionType ],
        [ 'entityType', query.entityType ],
        [ 'userId', query.userId ],
        [ 'username', query.username ],
        [ 'isSuccess', query.isSuccess ],
        [ 'category', query.category ]
    ];

    for (const [ key, value ] of queryEntries) {
        if (value !== undefined && value !== '') {
            params.set(key, String(value));
        }
    }

    const response = await api.axiosInstance.request({
        url: `/ActionLog/Entries?${params.toString()}`,
        method: 'GET',
        signal: options?.signal as AbortSignal | undefined,
        headers: { 'Cache-Control': 'no-cache', ...options?.headers }
    });
    return response.data as ActionLogQueryResult;
};

const ActionLogPage = () => {
    const navigate = useNavigate();
    const { api } = useApi();
    const [query, setQuery] = useState<ActionLogQuery>({
        startIndex: 0,
        limit: 25,
        minDate: '',
        maxDate: '',
        actionType: '',
        entityType: '',
        userId: '',
        username: '',
        isSuccess: undefined,
        category: ''
    });
    const [sortBy, setSortBy] = useState<string>('dateCreated');
    const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');
    const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
    const [detailRow, setDetailRow] = useState<ActionLogDto | null>(null);

    const handleSearch = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
        setQuery(current => ({ ...current, username: e.target.value, startIndex: 0 }));
    }, []);

    const handleFilterChange = useCallback((field: keyof ActionLogQuery, value: ActionLogQuery[keyof ActionLogQuery]) => {
        setQuery(current => ({ ...current, [field]: value, startIndex: 0 }));
    }, []);

    const handleSort = useCallback((field: string) => {
        if (sortBy === field) {
            setSortOrder(sortOrder === 'asc' ? 'desc' : 'asc');
        } else {
            setSortBy(field);
            setSortOrder('desc');
        }
    }, [ sortBy, sortOrder ]);

    const handlePageChange = useCallback((_: unknown, page: number) => {
        setQuery(current => ({ ...current, startIndex: page * (current.limit ?? 25) }));
    }, []);

    const handleRowsPerPageChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
        setQuery(current => ({ ...current, limit: parseInt(e.target.value, 10), startIndex: 0 }));
    }, []);

    const handleSelectFilterChange = useCallback((event: SelectChangeEvent<unknown>) => {
        const field = event.target.name as keyof ActionLogQuery;
        let value: ActionLogQuery[keyof ActionLogQuery] = String(event.target.value);
        if (field === 'isSuccess') {
            value = event.target.value === '' ? undefined : event.target.value === 'true';
        }
        handleFilterChange(field, value);
    }, [ handleFilterChange ]);

    const handleSortClick = useCallback((event: React.MouseEvent<HTMLElement>) => {
        const field = event.currentTarget.dataset.sortField;
        if (field) {
            handleSort(field);
        }
    }, [ handleSort ]);

    const openDetailMenu = useCallback((event: React.MouseEvent<HTMLElement>, row: ActionLogDto) => {
        setAnchorEl(event.currentTarget);
        setDetailRow(row);
    }, []);

    const closeDetailMenu = useCallback(() => {
        setAnchorEl(null);
        setDetailRow(null);
    }, []);

    const handleExport = useCallback(() => {
        void navigate('/dashboard/action-log/export');
    }, [ navigate ]);

    const { data, isLoading, isError, refetch } = useQuery({
        queryKey: ['ActionLog', 'Entries', api?.basePath, JSON.stringify(query)],
        queryFn: ({ signal }) => fetchActionLogs(api!, query, { signal, headers: { 'Cache-Control': 'no-cache' } }),
        enabled: !!api,
        placeholderData: { items: [], totalRecordCount: 0, startIndex: 0 }
    });

    const handleRetry = useCallback(() => {
        void refetch();
    }, [ refetch ]);

    const items = useMemo(() => data?.items ?? [], [ data?.items ]);
    const totalCount = data?.totalRecordCount ?? 0;

    const handleDetailClick = useCallback((event: React.MouseEvent<HTMLElement>) => {
        const rowId = Number(event.currentTarget.dataset.rowId);
        const row = items.find(item => item.id === rowId);
        if (row) {
            openDetailMenu(event, row);
        }
    }, [ items, openDetailMenu ]);

    let stateContent: React.ReactNode = null;
    if (isLoading) {
        stateContent = (
            <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
                <Typography>{globalize.translate('Loading')}</Typography>
            </Box>
        );
    } else if (isError) {
        stateContent = (
            <Alert
                severity='error'
                action={
                    <Button color='inherit' size='small' onClick={handleRetry}>
                        {globalize.translate('Retry')}
                    </Button>
                }
            >
                {globalize.translate('ErrorLoadingData')}
            </Alert>
        );
    } else if (items.length === 0) {
        stateContent = (
            <Paper sx={{ p: 3, textAlign: 'center' }}>
                <Typography color='text.secondary'>{globalize.translate('NoActionLogsFound')}</Typography>
            </Paper>
        );
    }

    const getStatusChip = (isSuccess: boolean) => (
        <Chip
            label={isSuccess ? globalize.translate('Success') : globalize.translate('Failed')}
            icon={isSuccess ? <CheckCircleIcon fontSize='small' /> : <ErrorIcon fontSize='small' />}
            color={isSuccess ? 'success' : 'error'}
            size='small'
            variant='outlined'
        />
    );

    const getCategoryChip = (category: string) => (
        <Chip label={globalize.translate(`ActionLogCategory${category}`) || category} size='small' variant='outlined' />
    );

    return (
        <Widget title={globalize.translate('ActionLog')} href='/dashboard/action-log'>
            <Toolbar sx={{ mb: 2, flexWrap: 'wrap', gap: 1 }}>
                <TextField
                    placeholder={globalize.translate('SearchByUsername')}
                    value={query.username}
                    onChange={handleSearch}
                    size='small'
                    sx={{ minWidth: 250 }}
                    slotProps={{
                        input: {
                            startAdornment: <InputAdornment position='start'><SearchIcon /></InputAdornment>
                        }
                    }}
                />
                <FormControl size='small' sx={{ minWidth: 180 }}>
                    <InputLabel id='action-type-label'>{globalize.translate('ActionType')}</InputLabel>
                    <Select
                        name='actionType'
                        label={globalize.translate('ActionType')}
                        value={query.actionType}
                        labelId='action-type-label'
                        onChange={handleSelectFilterChange}
                    >
                        <MenuItem value=''>{globalize.translate('All')}</MenuItem>
                        {ACTION_TYPES.map(type => (
                            <MenuItem key={type} value={type}>{globalize.translate(`ActionType${type}`) || type}</MenuItem>
                        ))}
                    </Select>
                </FormControl>
                <FormControl size='small' sx={{ minWidth: 180 }}>
                    <InputLabel id='entity-type-label'>{globalize.translate('EntityType')}</InputLabel>
                    <Select
                        name='entityType'
                        label={globalize.translate('EntityType')}
                        value={query.entityType}
                        labelId='entity-type-label'
                        onChange={handleSelectFilterChange}
                    >
                        <MenuItem value=''>{globalize.translate('All')}</MenuItem>
                        {ENTITY_TYPES.map(type => (
                            <MenuItem key={type} value={type}>{globalize.translate(`EntityType${type}`) || type}</MenuItem>
                        ))}
                    </Select>
                </FormControl>
                <FormControl size='small' sx={{ minWidth: 180 }}>
                    <InputLabel id='category-label'>{globalize.translate('Category')}</InputLabel>
                    <Select
                        name='category'
                        label={globalize.translate('Category')}
                        value={query.category}
                        labelId='category-label'
                        onChange={handleSelectFilterChange}
                    >
                        <MenuItem value=''>{globalize.translate('All')}</MenuItem>
                        {CATEGORIES.map(cat => (
                            <MenuItem key={cat} value={cat}>{globalize.translate(`ActionLogCategory${cat}`) || cat}</MenuItem>
                        ))}
                    </Select>
                </FormControl>
                <FormControl size='small' sx={{ minWidth: 150 }}>
                    <InputLabel id='status-label'>{globalize.translate('Status')}</InputLabel>
                    <Select
                        name='isSuccess'
                        label={globalize.translate('Status')}
                        value={query.isSuccess === undefined ? '' : query.isSuccess.toString()}
                        labelId='status-label'
                        onChange={handleSelectFilterChange}
                    >
                        <MenuItem value=''>{globalize.translate('All')}</MenuItem>
                        <MenuItem value='true'>{globalize.translate('Success')}</MenuItem>
                        <MenuItem value='false'>{globalize.translate('Failed')}</MenuItem>
                    </Select>
                </FormControl>
                <Box sx={{ flexGrow: 1 }} />
                <IconButton onClick={handleExport} color='primary'>
                    <DownloadIcon />
                </IconButton>
            </Toolbar>

            {stateContent}
            {!isLoading && !isError && items.length > 0 && (
                <>
                    <TableContainer>
                        <Table>
                            <TableHead>
                                <TableRow>
                                    <TableCell>
                                        <TableSortLabel
                                            data-sort-field='dateCreated'
                                            active={sortBy === 'dateCreated'}
                                            direction={sortBy === 'dateCreated' ? sortOrder : 'asc'}
                                            onClick={handleSortClick}
                                        >
                                            {globalize.translate('Date')}
                                        </TableSortLabel>
                                    </TableCell>
                                    <TableCell>
                                        <TableSortLabel
                                            data-sort-field='actionType'
                                            active={sortBy === 'actionType'}
                                            direction={sortBy === 'actionType' ? sortOrder : 'asc'}
                                            onClick={handleSortClick}
                                        >
                                            {globalize.translate('ActionType')}
                                        </TableSortLabel>
                                    </TableCell>
                                    <TableCell>
                                        <TableSortLabel
                                            data-sort-field='entityType'
                                            active={sortBy === 'entityType'}
                                            direction={sortBy === 'entityType' ? sortOrder : 'asc'}
                                            onClick={handleSortClick}
                                        >
                                            {globalize.translate('EntityType')}
                                        </TableSortLabel>
                                    </TableCell>
                                    <TableCell>
                                        <TableSortLabel
                                            data-sort-field='username'
                                            active={sortBy === 'username'}
                                            direction={sortBy === 'username' ? sortOrder : 'asc'}
                                            onClick={handleSortClick}
                                        >
                                            {globalize.translate('User')}
                                        </TableSortLabel>
                                    </TableCell>
                                    <TableCell>
                                        <TableSortLabel
                                            data-sort-field='category'
                                            active={sortBy === 'category'}
                                            direction={sortBy === 'category' ? sortOrder : 'asc'}
                                            onClick={handleSortClick}
                                        >
                                            {globalize.translate('Category')}
                                        </TableSortLabel>
                                    </TableCell>
                                    <TableCell>
                                        <TableSortLabel
                                            data-sort-field='isSuccess'
                                            active={sortBy === 'isSuccess'}
                                            direction={sortBy === 'isSuccess' ? sortOrder : 'asc'}
                                            onClick={handleSortClick}
                                        >
                                            {globalize.translate('Status')}
                                        </TableSortLabel>
                                    </TableCell>
                                    <TableCell align='right'>{globalize.translate('Actions')}</TableCell>
                                </TableRow>
                            </TableHead>
                            <TableBody>
                                {items.map((row) => (
                                    <TableRow key={row.id} hover>
                                        <TableCell>{new Date(row.dateCreated).toLocaleString()}</TableCell>
                                        <TableCell>{globalize.translate(`ActionType${row.actionType}`) || row.actionType}</TableCell>
                                        <TableCell>{globalize.translate(`EntityType${row.entityType}`) || row.entityType}</TableCell>
                                        <TableCell>{row.username}</TableCell>
                                        <TableCell>{getCategoryChip(row.category)}</TableCell>
                                        <TableCell>{getStatusChip(row.isSuccess)}</TableCell>
                                        <TableCell align='right'>
                                            <IconButton
                                                size='small'
                                                data-row-id={row.id}
                                                onClick={handleDetailClick}
                                                aria-label={globalize.translate('ViewDetails')}
                                            >
                                                <FilterListIcon />
                                            </IconButton>
                                        </TableCell>
                                    </TableRow>
                                ))}
                            </TableBody>
                        </Table>
                    </TableContainer>
                    <TablePagination
                        rowsPerPageOptions={[10, 25, 50, 100]}
                        component='div'
                        count={totalCount}
                        rowsPerPage={query.limit ?? 25}
                        page={(query.startIndex ?? 0) / (query.limit ?? 25)}
                        onPageChange={handlePageChange}
                        onRowsPerPageChange={handleRowsPerPageChange}
                    />
                </>
            )}

            <Menu
                anchorEl={anchorEl}
                open={Boolean(anchorEl)}
                onClose={closeDetailMenu}
                transformOrigin={{ horizontal: 'right', vertical: 'top' }}
                anchorOrigin={{ horizontal: 'right', vertical: 'bottom' }}
            >
                {detailRow && (
                    <>
                        <MenuItem disabled>
                            <Typography variant='subtitle1' fontWeight='bold'>
                                {globalize.translate('ActionLogDetails')}
                            </Typography>
                        </MenuItem>
                        <MenuItem disabled>
                            <Typography variant='body2'>
                                <strong>{globalize.translate('ActionType')}:</strong> {globalize.translate(`ActionType${detailRow.actionType}`) || detailRow.actionType}
                            </Typography>
                        </MenuItem>
                        <MenuItem disabled>
                            <Typography variant='body2'>
                                <strong>{globalize.translate('EntityType')}:</strong> {globalize.translate(`EntityType${detailRow.entityType}`) || detailRow.entityType}
                            </Typography>
                        </MenuItem>
                        <MenuItem disabled>
                            <Typography variant='body2'>
                                <strong>{globalize.translate('EntityId')}:</strong> {detailRow.entityId || '—'}
                            </Typography>
                        </MenuItem>
                        <MenuItem disabled>
                            <Typography variant='body2'>
                                <strong>{globalize.translate('User')}:</strong> {detailRow.username}
                            </Typography>
                        </MenuItem>
                        <MenuItem disabled>
                            <Typography variant='body2'>
                                <strong>{globalize.translate('Date')}:</strong> {new Date(detailRow.dateCreated).toLocaleString()}
                            </Typography>
                        </MenuItem>
                        <MenuItem disabled>
                            <Typography variant='body2'>
                                <strong>{globalize.translate('Status')}:</strong> {detailRow.isSuccess ? globalize.translate('Success') : globalize.translate('Failed')}
                            </Typography>
                        </MenuItem>
                        {detailRow.details && (
                            <MenuItem disabled>
                                <Typography variant='body2'>
                                    <strong>{globalize.translate('Details')}:</strong> {detailRow.details}
                                </Typography>
                            </MenuItem>
                        )}
                        {detailRow.oldValues && (
                            <MenuItem disabled>
                                <Typography variant='body2'>
                                    <strong>{globalize.translate('OldValues')}:</strong> {detailRow.oldValues}
                                </Typography>
                            </MenuItem>
                        )}
                        {detailRow.newValues && (
                            <MenuItem disabled>
                                <Typography variant='body2'>
                                    <strong>{globalize.translate('NewValues')}:</strong> {detailRow.newValues}
                                </Typography>
                            </MenuItem>
                        )}
                        {detailRow.errorMessage && (
                            <MenuItem disabled>
                                <Typography variant='body2' color='error'>
                                    <strong>{globalize.translate('Error')}:</strong> {detailRow.errorMessage}
                                </Typography>
                            </MenuItem>
                        )}
                        {detailRow.ipAddress && (
                            <MenuItem disabled>
                                <Typography variant='body2'>
                                    <strong>{globalize.translate('IPAddress')}:</strong> {detailRow.ipAddress}
                                </Typography>
                            </MenuItem>
                        )}
                        {detailRow.userAgent && (
                            <MenuItem disabled>
                                <Typography variant='body2'>
                                    <strong>{globalize.translate('UserAgent')}:</strong> {detailRow.userAgent}
                                </Typography>
                            </MenuItem>
                        )}
                    </>
                )}
            </Menu>
        </Widget>
    );
};

export default ActionLogPage;
