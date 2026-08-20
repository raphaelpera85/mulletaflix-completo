import React, { useMemo } from 'react';
import globalize from 'lib/globalize';
import Widget from './Widget';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Skeleton from '@mui/material/Skeleton';
import { TaskState } from '@jellyfin/sdk/lib/generated-client/models/task-state';
import type { TaskInfo } from '@jellyfin/sdk/lib/generated-client/models/task-info';
import subSeconds from 'date-fns/subSeconds';

import { useSystemInfo } from 'hooks/useSystemInfo';
import { useLogEntries } from 'apps/dashboard/features/activity/api/useLogEntries';

type ServerHealthWidgetProps = {
    tasks?: TaskInfo[];
};

const ServerHealthWidget = ({ tasks }: ServerHealthWidgetProps) => {
    const { data: systemInfo, isPending: isSystemInfoPending } = useSystemInfo();
    const weekBefore = useMemo(() => (
        subSeconds(new Date(), 7 * 24 * 60 * 60).toISOString()
    ), []);

    const { data: alerts, isPending: isAlertsPending } = useLogEntries({
        startIndex: 0,
        limit: 4,
        minDate: weekBefore,
        hasUserId: false
    });

    const runningTasks = useMemo(() => (
        tasks?.filter(task => task.State === TaskState.Running) ?? []
    ), [tasks]);

    const refreshLibraryTaskRunning = useMemo(() => (
        runningTasks.some(task => task.Key === 'RefreshLibrary')
    ), [runningTasks]);

    const recentAlertsCount = alerts?.Items?.length ?? 0;
    const isHealthy = !refreshLibraryTaskRunning && runningTasks.length === 0 && recentAlertsCount === 0;

    return (
        <Widget
            title='Server health'
            href='/dashboard/tasks'
        >
            <Paper sx={{ padding: 2 }}>
                <Stack spacing={1.5}>
                    <Stack direction='row' spacing={1} alignItems='center' flexWrap='wrap'>
                        {isSystemInfoPending ? (
                            <Skeleton width={180} />
                        ) : (
                            <Typography fontWeight='bold'>
                                {systemInfo?.ServerName || globalize.translate('LabelServerName')}
                            </Typography>
                        )}
                        <Chip
                            color={isHealthy ? 'success' : 'warning'}
                            label={isHealthy ? 'Healthy' : 'Attention needed'}
                            size='small'
                        />
                    </Stack>

                    <Typography>
                        {isAlertsPending
                            ? <Skeleton width={220} />
                            : `${runningTasks.length} running task(s), ${recentAlertsCount} recent alert(s)`}
                    </Typography>

                    <Typography>
                        {refreshLibraryTaskRunning
                            ? 'Library refresh is in progress'
                            : 'No library refresh running'}
                    </Typography>

                    <Typography>
                        {isSystemInfoPending
                            ? <Skeleton width={180} />
                            : `${globalize.translate('LabelServerVersion')}: ${systemInfo?.Version ?? '—'}`}
                    </Typography>
                </Stack>
            </Paper>
        </Widget>
    );
};

export default ServerHealthWidget;
