import React, { useMemo, useState } from 'react';
import globalize from 'lib/globalize';
import { useQuery } from '@tanstack/react-query';
import { useApi } from 'hooks/useApi';

import Widget from 'apps/dashboard/components/widgets/Widget';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
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
import Select from '@mui/material/Select';
import FormControl from '@mui/material/FormControl';
import InputLabel from '@mui/material/InputLabel';
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

const fetchActionLogs = async (api: any, query: ActionLogQuery, options?: any) => {
    const params = new URLSearchParams();
    if (query.startIndex !== undefined) params.set('startIndex', query.startIndex.toString());
    if (query.limit !== undefined) params.set('limit', query.limit.toString());
    if (query.minDate) params.set('minDate', query.minDate);
    if (query.maxDate) params.set('maxDate', query.maxDate);
    if (query.actionType) params.set('actionType', query.actionType);
    if (query.entityType) params.set('entityType', query.entityType);
    if (query.userId) params.set('userId', query.userId);
    if (query.username) params.set('username', query.username);
    if (query.isSuccess !== undefined) params.set('isSuccess', query.isSuccess.toString());
    if (query.category) params.set('category', query.category);

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

    const handleSearch = (e: React.ChangeEvent<HTMLInputElement>) => {
        setQuery({ ...query, username: e.target.value, startIndex: 0 });
    };

    const handleFilterChange = (field: string, value: any) => {
        setQuery({ ...query, [field]: value, startIndex: 0 });
    };

    const handleSort = (field: string) => {
        if (sortBy === field) {
            setSortOrder(sortOrder === 'asc' ? 'desc' : 'asc');
        } else {
            setSortBy(field);
            setSortOrder('desc');
        }
    };

    const handlePageChange = (_: unknown, page: number) => {
        setQuery({ ...query, startIndex: page * (query.limit ?? 25) });
    };

    const handleRowsPerPageChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        setQuery({ ...query, limit: parseInt(e.target.value, 10), startIndex: 0 });
    };

    const openDetailMenu = (event: React.MouseEvent<HTMLElement>, row: ActionLogDto) => {
        setAnchorEl(event.currentTarget);
        setDetailRow(row);
    };

    const closeDetailMenu = () => {
        setAnchorEl(null);
        setDetailRow(null);
    };

    const { data, isLoading, isError } = useQuery({
        queryKey: ['ActionLog', 'Entries', api?.basePath, JSON.stringify(query)],
        queryFn: ({ signal }) => fetchActionLogs(api!, query, { signal, headers: { 'Cache-Control': 'no-cache' } }),
        enabled: !!api,
        placeholderData: { items: [], totalRecordCount: 0, startIndex: 0 }
    });

    const items = data?.items ?? [];
    const totalCount = data?.totalRecordCount ?? 0;

    const getStatusChip = (isSuccess: boolean) => (
        <Chip
            label={isSuccess ? globalize.translate('Success') : globalize.translate('Failed')}
            icon={isSuccess ? <CheckCircleIcon fontSize="small" /> : <ErrorIcon fontSize="small" />}
            color={isSuccess ? 'success' : 'error'}
            size="small"
            variant="outlined"
        />
    );

    const getCategoryChip = (category: string) => (
        <Chip label={globalize.translate(`ActionLogCategory${category}`) || category} size="small" variant="outlined" />
    );

    return (
        <Widget title={globalize.translate('ActionLog')} href="/dashboard/action-log">
            <Toolbar sx={{ mb: 2, flexWrap: 'wrap', gap: 1 }}>
                <TextField
                    placeholder={globalize.translate('SearchByUsername')}
                    value={query.username}
                    onChange={handleSearch}
                    size="small"
                    sx={{ minWidth: 250 }}
                    InputProps={{
                        startAdornment: <InputAdornment position="start"><SearchIcon /></InputAdornment>
                    }}
                />
                <FormControl size="small" sx={{ minWidth: 180 }}>
                    <InputLabel id="action-type-label">{globalize.translate('ActionType')}</InputLabel>
                    <Select
                        label={globalize.translate('ActionType')}
                        value={query.actionType}
                        labelId="action-type-label"
                        onChange={(e) => handleFilterChange('actionType', e.target.value)}
                    >
                        <MenuItem value="">{globalize.translate('All')}</MenuItem>
                        {ACTION_TYPES.map(type => (
                            <MenuItem key={type} value={type}>{globalize.translate(`ActionType${type}`) || type}</MenuItem>
                        ))}
                    </Select>
                </FormControl>
                <FormControl size="small" sx={{ minWidth: 180 }}>
                    <InputLabel id="entity-type-label">{globalize.translate('EntityType')}</InputLabel>
                    <Select
                        label={globalize.translate('EntityType')}
                        value={query.entityType}
                        labelId="entity-type-label"
                        onChange={(e) => handleFilterChange('entityType', e.target.value)}
                    >
                        <MenuItem value="">{globalize.translate('All')}</MenuItem>
                        {ENTITY_TYPES.map(type => (
                            <MenuItem key={type} value={type}>{globalize.translate(`EntityType${type}`) || type}</MenuItem>
                        ))}
                    </Select>
                </FormControl>
                <FormControl size="small" sx={{ minWidth: 180 }}>
                    <InputLabel id="category-label">{globalize.translate('Category')}</InputLabel>
                    <Select
                        label={globalize.translate('Category')}
                        value={query.category}
                        labelId="category-label"
                        onChange={(e) => handleFilterChange('category', e.target.value)}
                    >
                        <MenuItem value="">{globalize.translate('All')}</MenuItem>
                        {CATEGORIES.map(cat => (
                            <MenuItem key={cat} value={cat}>{globalize.translate(`ActionLogCategory${cat}`) || cat}</MenuItem>
                        ))}
                    </Select>
                </FormControl>
                <FormControl size="small" sx={{ minWidth: 150 }}>
                    <InputLabel id="status-label">{globalize.translate('Status')}</InputLabel>
                    <Select
                        label={globalize.translate('Status')}
                        value={query.isSuccess === undefined ? '' : query.isSuccess.toString()}
                        labelId="status-label"
                        onChange={(e) => handleFilterChange('isSuccess', e.target.value === '' ? undefined : e.target.value === 'true')}
                    >
                        <MenuItem value="">{globalize.translate('All')}</MenuItem>
                        <MenuItem value="true">{globalize.translate('Success')}</MenuItem>
                        <MenuItem value="false">{globalize.translate('Failed')}</MenuItem>
                    </Select>
                </FormControl>
                <Box sx={{ flexGrow: 1 }} />
                <IconButton onClick={() => navigate('/dashboard/action-log/export')} color="primary">
                    <DownloadIcon />
                </IconButton>
            </Toolbar>

            {isLoading ? (
                <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
                    <Typography>{globalize.translate('Loading')}</Typography>
                </Box>
            ) : isError ? (
                <Paper sx={{ p: 3, textAlign: 'center', color: 'error' }}>
                    {globalize.translate('ErrorLoadingData')}
                </Paper>
            ) : items.length === 0 ? (
                <Paper sx={{ p: 3, textAlign: 'center' }}>
                    <Typography color="text.secondary">{globalize.translate('NoActionLogsFound')}</Typography>
                </Paper>
            ) : (
                <>
                    <TableContainer>
                        <Table>
                            <TableHead>
                                <TableRow>
                                    <TableCell>
                                        <TableSortLabel
                                            active={sortBy === 'dateCreated'}
                                            direction={sortBy === 'dateCreated' ? sortOrder : 'asc'}
                                            onClick={() => handleSort('dateCreated')}
                                        >
                                            {globalize.translate('Date')}
                                        </TableSortLabel>
                                    </TableCell>
                                    <TableCell>
                                        <TableSortLabel
                                            active={sortBy === 'actionType'}
                                            direction={sortBy === 'actionType' ? sortOrder : 'asc'}
                                            onClick={() => handleSort('actionType')}
                                        >
                                            {globalize.translate('ActionType')}
                                        </TableSortLabel>
                                    </TableCell>
                                    <TableCell>
                                        <TableSortLabel
                                            active={sortBy === 'entityType'}
                                            direction={sortBy === 'entityType' ? sortOrder : 'asc'}
                                            onClick={() => handleSort('entityType')}
                                        >
                                            {globalize.translate('EntityType')}
                                        </TableSortLabel>
                                    </TableCell>
                                    <TableCell>
                                        <TableSortLabel
                                            active={sortBy === 'username'}
                                            direction={sortBy === 'username' ? sortOrder : 'asc'}
                                            onClick={() => handleSort('username')}
                                        >
                                            {globalize.translate('User')}
                                        </TableSortLabel>
                                    </TableCell>
                                    <TableCell>
                                        <TableSortLabel
                                            active={sortBy === 'category'}
                                            direction={sortBy === 'category' ? sortOrder : 'asc'}
                                            onClick={() => handleSort('category')}
                                        >
                                            {globalize.translate('Category')}
                                        </TableSortLabel>
                                    </TableCell>
                                    <TableCell>
                                        <TableSortLabel
                                            active={sortBy === 'isSuccess'}
                                            direction={sortBy === 'isSuccess' ? sortOrder : 'asc'}
                                            onClick={() => handleSort('isSuccess')}
                                        >
                                            {globalize.translate('Status')}
                                        </TableSortLabel>
                                    </TableCell>
                                    <TableCell align="right">{globalize.translate('Actions')}</TableCell>
                                </TableRow>
                            </TableHead>
                            <TableBody>
                                {items.map((row, index) => (
                                    <TableRow key={row.id} hover>
                                        <TableCell>{new Date(row.dateCreated).toLocaleString()}</TableCell>
                                        <TableCell>{globalize.translate(`ActionType${row.actionType}`) || row.actionType}</TableCell>
                                        <TableCell>{globalize.translate(`EntityType${row.entityType}`) || row.entityType}</TableCell>
                                        <TableCell>{row.username}</TableCell>
                                        <TableCell>{getCategoryChip(row.category)}</TableCell>
                                        <TableCell>{getStatusChip(row.isSuccess)}</TableCell>
                                        <TableCell align="right">
                                            <IconButton
                                                size="small"
                                                onClick={(e) => openDetailMenu(e, row)}
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
                        component="div"
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
                            <Typography variant="subtitle1" fontWeight="bold">
                                {globalize.translate('ActionLogDetails')}
                            </Typography>
                        </MenuItem>
                        <MenuItem disabled>
                            <Typography variant="body2">
                                <strong>{globalize.translate('ActionType')}:</strong> {globalize.translate(`ActionType${detailRow.actionType}`) || detailRow.actionType}
                            </Typography>
                        </MenuItem>
                        <MenuItem disabled>
                            <Typography variant="body2">
                                <strong>{globalize.translate('EntityType')}:</strong> {globalize.translate(`EntityType${detailRow.entityType}`) || detailRow.entityType}
                            </Typography>
                        </MenuItem>
                        <MenuItem disabled>
                            <Typography variant="body2">
                                <strong>{globalize.translate('EntityId')}:</strong> {detailRow.entityId || '—'}
                            </Typography>
                        </MenuItem>
                        <MenuItem disabled>
                            <Typography variant="body2">
                                <strong>{globalize.translate('User')}:</strong> {detailRow.username}
                            </Typography>
                        </MenuItem>
                        <MenuItem disabled>
                            <Typography variant="body2">
                                <strong>{globalize.translate('Date')}:</strong> {new Date(detailRow.dateCreated).toLocaleString()}
                            </Typography>
                        </MenuItem>
                        <MenuItem disabled>
                            <Typography variant="body2">
                                <strong>{globalize.translate('Status')}:</strong> {detailRow.isSuccess ? globalize.translate('Success') : globalize.translate('Failed')}
                            </Typography>
                        </MenuItem>
                        {detailRow.details && (
                            <MenuItem disabled>
                                <Typography variant="body2">
                                    <strong>{globalize.translate('Details')}:</strong> {detailRow.details}
                                </Typography>
                            </MenuItem>
                        )}
                        {detailRow.oldValues && (
                            <MenuItem disabled>
                                <Typography variant="body2">
                                    <strong>{globalize.translate('OldValues')}:</strong> {detailRow.oldValues}
                                </Typography>
                            </MenuItem>
                        )}
                        {detailRow.newValues && (
                            <MenuItem disabled>
                                <Typography variant="body2">
                                    <strong>{globalize.translate('NewValues')}:</strong> {detailRow.newValues}
                                </Typography>
                            </MenuItem>
                        )}
                        {detailRow.errorMessage && (
                            <MenuItem disabled>
                                <Typography variant="body2" color="error">
                                    <strong>{globalize.translate('Error')}:</strong> {detailRow.errorMessage}
                                </Typography>
                            </MenuItem>
                        )}
                        {detailRow.ipAddress && (
                            <MenuItem disabled>
                                <Typography variant="body2">
                                    <strong>{globalize.translate('IPAddress')}:</strong> {detailRow.ipAddress}
                                </Typography>
                            </MenuItem>
                        )}
                        {detailRow.userAgent && (
                            <MenuItem disabled>
                                <Typography variant="body2">
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