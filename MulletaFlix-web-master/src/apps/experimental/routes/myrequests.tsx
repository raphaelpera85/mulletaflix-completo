import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemText from '@mui/material/ListItemText';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import React, { type FC, useCallback } from 'react';
import type { ActivityLogEntry } from '@jellyfin/sdk/lib/generated-client/models/activity-log-entry';

import { useMyClassifiedMediaRequests, type MediaRequestQueuePosition, type MediaRequestQueueStatus } from 'hooks/api/useMediaRequests';
import Loading from 'components/loading/LoadingComponent';
import Page from 'components/Page';
import globalize from 'lib/globalize';

const requestTitlePrefix = /^Solicitação de mídia:\s*/i;

const getRequestTitle = (entry: ActivityLogEntry) => (entry.Name || '').replace(requestTitlePrefix, '').trim();

const getQueueStatusText = (queue: MediaRequestQueuePosition | undefined) => {
    if (!queue?.SnapshotAvailable) return globalize.translate('MediaRequestQueueUnavailable');

    const segments: string[] = [];
    const currentCount = queue.CurrentItemCount || 0;
    if (queue.IsPriority) {
        segments.push(globalize.translate('MediaRequestQueuePriority'));
    }
    if (currentCount > 0) {
        segments.push(`${globalize.translate('MediaRequestQueueCurrent')}: ${currentCount}`);
    }

    if (queue.Position != null) {
        segments.push(globalize.translate('MediaRequestQueuePosition')
            .replace('{position}', String(queue.Position))
            .replace('{count}', String(queue.QueueItemCount || 0)));
    } else if (currentCount === 0) {
        segments.push(globalize.translate(queue.IsRunning === false ? 'MediaRequestQueueAbsentFromSnapshot' : 'MediaRequestQueueAwaiting'));
    }

    const snapshotPrefix = queue.IsRunning === false ? `${globalize.translate('MediaRequestQueueLastSnapshot')}: ` : '';
    return snapshotPrefix + segments.join(' · ');
};

const QueueStatus: FC<{ titleKey: string; queue: MediaRequestQueuePosition | undefined }> = ({ titleKey, queue }) => (
    <Stack spacing={0.25} sx={{ minWidth: 0 }}>
        <Chip
            size='small'
            variant='outlined'
            label={`${globalize.translate(titleKey)}: ${getQueueStatusText(queue)}`}
            sx={{ maxWidth: '100%', height: 'auto', '& .MuiChip-label': { whiteSpace: 'normal', overflowWrap: 'anywhere', py: 0.5 } }}
        />
        {queue?.SnapshotAvailable && (queue.MatchingItemCount || 0) > 0 && (
            <Typography variant='caption' color='text.secondary' sx={{ overflowWrap: 'anywhere' }}>
                {globalize.translate('MediaRequestQueueMatches').replace('{count}', String(queue.MatchingItemCount))}
            </Typography>
        )}
        {queue?.SnapshotAvailable && queue.SnapshotAtUtc && (
            <Typography variant='caption' color='text.secondary' sx={{ overflowWrap: 'anywhere' }}>
                {globalize.translate('MediaRequestQueueUpdated')}: {new Date(queue.SnapshotAtUtc).toLocaleString()}
            </Typography>
        )}
    </Stack>
);

const RequestGroup: FC<{ titleKey: string; entries: ActivityLogEntry[]; emptyKey: string; chipColor: 'default' | 'success'; priorityRequestIds: ReadonlySet<number>; queueStatuses: ReadonlyMap<number, MediaRequestQueueStatus>; showQueueStatus?: boolean }> = ({
    titleKey,
    entries,
    emptyKey,
    chipColor,
    priorityRequestIds,
    queueStatuses,
    showQueueStatus = false
}) => (
    <Box component='section' aria-label={globalize.translate(titleKey)}>
        <Typography variant='h2' sx={{ mb: 1 }}>
            {globalize.translate(titleKey)} ({entries.length})
        </Typography>
        {entries.length === 0 ? (
            <Typography variant='body2' color='text.secondary'>
                {globalize.translate(emptyKey)}
            </Typography>
        ) : (
            <List dense disablePadding sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 20rem), 1fr))', gap: 2 }}>
                {entries.map((entry, index) => (
                    <ListItem
                        key={`${entry.Id ?? index}`}
                        sx={{ flexDirection: 'column', alignItems: 'flex-start', gap: 1, p: 2, border: 1, borderColor: 'divider', borderRadius: 1, minWidth: 0 }}
                    >
                        <ListItemText
                            primary={getRequestTitle(entry)}
                            secondary={entry.ShortOverview || undefined}
                            sx={{ m: 0, width: '100%', overflowWrap: 'anywhere' }}
                        />
                        <Stack direction='row' sx={{ maxWidth: '100%', flexWrap: 'wrap', gap: 0.5, '& .MuiChip-root': { maxWidth: '100%', height: 'auto' }, '& .MuiChip-label': { whiteSpace: 'normal', overflowWrap: 'anywhere', py: 0.5 } }}>
                            {chipColor === 'default' && entry.Id !== undefined && priorityRequestIds.has(entry.Id) && (
                                <Chip
                                    size='small'
                                    color='warning'
                                    label={globalize.translate('MyMediaRequestPriority')}
                                    title={globalize.translate('MyMediaRequestPriorityDescription')}
                                    aria-label={`${globalize.translate('MyMediaRequestPriority')}. ${globalize.translate('MyMediaRequestPriorityDescription')}`}
                                />
                            )}
                            <Chip size='small' color={chipColor} label={entry.Overview || ''} />
                        </Stack>
                        {showQueueStatus && entry.Id !== undefined && (
                            <Stack direction={{ xs: 'column', sm: 'row' }} sx={{ width: '100%', flexWrap: 'wrap', gap: 1 }}>
                                <QueueStatus titleKey='MediaRequestQueueDownload' queue={queueStatuses.get(entry.Id)?.Download} />
                                <QueueStatus titleKey='MediaRequestQueueUpload' queue={queueStatuses.get(entry.Id)?.Upload} />
                            </Stack>
                        )}
                    </ListItem>
                ))}
            </List>
        )}
    </Box>
);

const MyMediaRequestsPage: FC = () => {
    const {
        pending, included, priorityRequestIds, queueStatuses = new Map(), isPending, isError, refetch,
        hasNextPage, fetchNextPage, isFetchingNextPage, isFetchNextPageError
    } = useMyClassifiedMediaRequests();

    const handleRetry = useCallback(() => {
        refetch().catch(() => undefined);
    }, [ refetch ]);

    const handleLoadMore = useCallback(() => {
        fetchNextPage().catch(() => undefined);
    }, [ fetchNextPage ]);

    let content;
    if (isError) {
        content = (
            <Alert
                severity='error'
                action={(
                    <Button color='inherit' size='small' onClick={handleRetry}>
                        {globalize.translate('Retry')}
                    </Button>
                )}
            >
                {globalize.translate('ErrorDefault')}
            </Alert>
        );
    } else if (isPending) {
        content = <Loading />;
    } else if (pending.length === 0 && included.length === 0) {
        content = (
            <Typography variant='body1' color='text.secondary'>
                {globalize.translate('MyMediaRequestsEmpty')}
            </Typography>
        );
    } else {
        content = (
            <Stack spacing={4}>
                <RequestGroup
                    titleKey='MediaRequestsPendingTitle'
                    entries={pending}
                    emptyKey='MyMediaRequestsPendingEmpty'
                    chipColor='default'
                    priorityRequestIds={priorityRequestIds}
                    queueStatuses={queueStatuses}
                    showQueueStatus
                />
                <RequestGroup
                    titleKey='MediaRequestsIncludedTitle'
                    entries={included}
                    emptyKey='MyMediaRequestsIncludedEmpty'
                    chipColor='success'
                    priorityRequestIds={priorityRequestIds}
                    queueStatuses={queueStatuses}
                />
                {isFetchNextPageError && <Alert severity='error'>{globalize.translate('ErrorDefault')}</Alert>}
                {hasNextPage && (
                    <Button
                        onClick={handleLoadMore}
                        disabled={isFetchingNextPage}
                        aria-busy={isFetchingNextPage}
                        sx={{ alignSelf: 'flex-start' }}
                    >
                        {globalize.translate(isFetchNextPageError ? 'Retry' : 'ShowMore')}
                    </Button>
                )}
            </Stack>
        );
    }

    return (
        <Page
            id='myMediaRequestsPage'
            title={globalize.translate('MyMediaRequestsTitle')}
            className='mainAnimatedPage libraryPage noSecondaryNavPage'
        >
            <Box className='padded-left padded-right padded-bottom-page' sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
                <Typography variant='h1'>{globalize.translate('MyMediaRequestsTitle')}</Typography>
                {content}
            </Box>
        </Page>
    );
};

export default MyMediaRequestsPage;
