import React from 'react';
import Widget from './Widget';
import List from '@mui/material/List';
import ActivityListItem from 'apps/dashboard/features/activity/components/ActivityListItem';
import { useLogEntries } from 'apps/dashboard/features/activity/api/useLogEntries';
import Skeleton from '@mui/material/Skeleton';
import Stack from '@mui/material/Stack';

const AlertsLogWidget = () => {
    const { data: alerts, isPending } = useLogEntries({
        startIndex: 0,
        limit: 7,
        hasUserId: false
    });

    if (isPending || alerts?.Items?.length === 0) return null;

    return (
        <Widget
            title='Audit trail'
            href='/dashboard/activity?useractivity=false'
        >
            {isPending ? (
                <Stack spacing={2}>
                    <Skeleton variant='rounded' height={60} />
                    <Skeleton variant='rounded' height={60} />
                    <Skeleton variant='rounded' height={60} />
                    <Skeleton variant='rounded' height={60} />
                </Stack>
            ) : (
                <List sx={{ bgcolor: 'background.paper' }}>
                    {alerts?.Items?.map(entry => (
                        <ActivityListItem
                            key={entry.Id}
                            item={entry}
                            displayShortOverview={true}
                            to='/dashboard/activity?useractivity=false'
                        />
                    ))}
                </List>
            )}
        </Widget>
    );
};

export default AlertsLogWidget;
