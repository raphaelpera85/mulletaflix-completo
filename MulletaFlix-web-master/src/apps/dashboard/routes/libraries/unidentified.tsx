import React, { useCallback, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import Page from 'components/Page';
import globalize from 'lib/globalize';
import Box from '@mui/material/Box';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Alert from '@mui/material/Alert';
import Loading from 'components/loading/LoadingComponent';
import RefreshIcon from '@mui/icons-material/Refresh';
import IconButton from '@mui/material/IconButton';
import Chip from '@mui/material/Chip';
import { ServerConnections } from 'lib/jellyfin-apiclient';

const POLL_INTERVAL = 15000;

interface UnidentifiedItem {
    Id: string;
    Name: string;
    Path: string | null;
    Type: string;
    DateCreated: string;
}

async function fetchUnidentifiedItems(mediaType: string): Promise<UnidentifiedItem[]> {
    const api = ServerConnections.getCurrentApi();
    if (!api) throw new Error('No API available');
    const baseUrl = api.basePath;
    const token = api.accessToken;
    const resp = await fetch(baseUrl + '/Items/Unidentified?mediaType=' + mediaType, {
        headers: { Authorization: 'MediaBrowser Token="' + token + '"' }
    });
    if (!resp.ok) throw new Error('HTTP ' + resp.status);
    const result: unknown = await resp.json();
    if (!Array.isArray(result)) {
        throw new Error('Invalid unidentified media response');
    }
    return result as UnidentifiedItem[];
}

export const Component = () => {
    const [tab, setTab] = useState(0);

    const mediaType = tab === 0 ? 'Movies' : 'Series';
    const typeLabel = tab === 0 ? globalize.translate('Movies') : globalize.translate('Series');

    const onTabChange = useCallback((_event: React.SyntheticEvent, value: number): void => {
        setTab(value);
    }, []);

    // F-12: this was the only screen in the app polling manually with
    // setInterval + setItems(newArray), which forced a full re-render on
    // every 15s tick even when the API returned the exact same items. useQuery
    // with its default `structuralSharing` keeps the previous object/array
    // references for parts of the response that didn't change, so consumers
    // (and React) can bail out of re-rendering when nothing actually changed.
    // It also gives us window-focus refetch and default background-pause of
    // refetchInterval for free, replacing the manual visibilitychange/focus
    // listeners that used to be needed.
    const {
        data: items,
        isLoading: loading,
        error: queryError,
        dataUpdatedAt,
        refetch
    } = useQuery({
        queryKey: ['UnidentifiedItems', mediaType],
        queryFn: () => fetchUnidentifiedItems(mediaType),
        refetchInterval: POLL_INTERVAL
    });

    const error = queryError instanceof Error ? queryError.message : (queryError ? 'Failed to fetch items' : null);
    const lastUpdate = dataUpdatedAt ? new Date(dataUpdatedAt).toLocaleTimeString() : null;

    const onRefreshClick = useCallback(() => {
        void refetch();
    }, [refetch]);

    return (
        <Page
            id='unidentifiedMediaPage'
            title={globalize.translate('UnidentifiedMedia')}
            className='mainAnimatedPage type-interior'
        >
            <Box className='content-primary'>
                <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
                    <Typography variant='h4' sx={{ flexGrow: 1 }}>
                        {globalize.translate('UnidentifiedMedia')}
                    </Typography>
                    {lastUpdate && (
                        <Chip
                            label={'Auto-refresh: ' + lastUpdate}
                            size='small'
                            variant='outlined'
                            sx={{ mr: 1 }}
                        />
                    )}
                    <IconButton onClick={onRefreshClick} title={globalize.translate('Refresh')} size='large'>
                        <RefreshIcon />
                    </IconButton>
                </Box>
                <Typography variant='body1' sx={{ mb: 2, color: 'text.secondary' }}>
                    {globalize.translate('UnidentifiedMediaDescription')}
                </Typography>

                <Tabs
                    value={tab}
                    onChange={onTabChange}
                    sx={{ mb: 2 }}
                >
                    <Tab label={globalize.translate('Movies')} />
                    <Tab label={globalize.translate('Series')} />
                </Tabs>

                {loading && <Loading />}

                {error && (
                    <Alert
                        severity='error'
                        sx={{ mb: 2 }}
                        action={
                            <IconButton
                                color='inherit'
                                size='small'
                                aria-label={globalize.translate('Retry')}
                                onClick={onRefreshClick}
                            >
                                <RefreshIcon fontSize='small' />
                            </IconButton>
                        }
                    >
                        {error}
                    </Alert>
                )}

                {!loading && !error && (items?.length ?? 0) === 0 && (
                    <Alert severity='success'>
                        {globalize.translate('NoUnidentifiedItems', typeLabel)}
                    </Alert>
                )}

                {!loading && !error && items && items.length > 0 && (
                    <>
                        <Typography variant='subtitle1' sx={{ mb: 1 }}>
                            {items.length} {globalize.translate('ItemsFound', typeLabel)}
                        </Typography>
                        <TableContainer component={Paper}>
                            <Table size='small'>
                                <TableHead>
                                    <TableRow>
                                        <TableCell>{globalize.translate('Name')}</TableCell>
                                        <TableCell>{globalize.translate('LabelPath')}</TableCell>
                                        <TableCell>{globalize.translate('DateAdded')}</TableCell>
                                    </TableRow>
                                </TableHead>
                                <TableBody>
                                    {items.map((item) => (
                                        <TableRow key={item.Id}>
                                            <TableCell>
                                                <a
                                                    href={`#/details?id=${encodeURIComponent(item.Id)}`}
                                                    title={globalize.translate('Identify')}
                                                    style={{ cursor: 'pointer', fontWeight: 500, color: 'inherit', textDecoration: 'underline' }}
                                                >
                                                    {item.Name}
                                                </a>
                                            </TableCell>
                                            <TableCell sx={{ maxWidth: 400, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                                {item.Path ?? '-'}
                                            </TableCell>
                                            <TableCell>
                                                {new Date(item.DateCreated).toLocaleDateString()}
                                            </TableCell>
                                        </TableRow>
                                    ))}
                                </TableBody>
                            </Table>
                        </TableContainer>
                    </>
                )}
            </Box>
        </Page>
    );
};

Component.displayName = 'UnidentifiedMediaPage';
