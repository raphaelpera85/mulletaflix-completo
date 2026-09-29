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

import { useMyClassifiedMediaRequests } from 'hooks/api/useMediaRequests';
import Loading from 'components/loading/LoadingComponent';
import Page from 'components/Page';
import globalize from 'lib/globalize';

const requestTitlePrefix = /^Solicitação de mídia:\s*/i;

const getRequestTitle = (entry: ActivityLogEntry) => (entry.Name || '').replace(requestTitlePrefix, '').trim();

const RequestGroup: FC<{ titleKey: string; entries: ActivityLogEntry[]; emptyKey: string; chipColor: 'default' | 'success' }> = ({
    titleKey,
    entries,
    emptyKey,
    chipColor
}) => (
    <Box>
        <Typography variant='h2' sx={{ mb: 1 }}>
            {globalize.translate(titleKey)} ({entries.length})
        </Typography>
        {entries.length === 0 ? (
            <Typography variant='body2' color='text.secondary'>
                {globalize.translate(emptyKey)}
            </Typography>
        ) : (
            <List dense disablePadding>
                {entries.map((entry, index) => (
                    <ListItem
                        key={`${entry.Id ?? index}`}
                        divider={index < entries.length - 1}
                        secondaryAction={<Chip size='small' color={chipColor} label={entry.Overview || ''} />}
                    >
                        <ListItemText
                            primary={getRequestTitle(entry)}
                            secondary={entry.ShortOverview || undefined}
                        />
                    </ListItem>
                ))}
            </List>
        )}
    </Box>
);

const MyMediaRequestsPage: FC = () => {
    const { pending, included, isPending, isError, refetch } = useMyClassifiedMediaRequests();

    const handleRetry = useCallback(() => {
        refetch().catch(() => undefined);
    }, [ refetch ]);

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
                />
                <RequestGroup
                    titleKey='MediaRequestsIncludedTitle'
                    entries={included}
                    emptyKey='MyMediaRequestsIncludedEmpty'
                    chipColor='success'
                />
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
