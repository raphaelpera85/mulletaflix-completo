import React, { useCallback } from 'react';
import Page from 'components/Page';
import globalize from 'lib/globalize';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import { getCategories, getTasksByCategory } from '../../features/tasks/utils/tasks';
import Loading from 'components/loading/LoadingComponent';
import Tasks from '../../features/tasks/components/Tasks';
import useLiveTasks from 'apps/dashboard/features/tasks/hooks/useLiveTasks';

export const Component = () => {
    const { data: tasks, isPending, isError, refetch } = useLiveTasks({ isHidden: false });

    const handleRetry = useCallback(() => {
        void refetch().catch((error: unknown) => {
            console.error('[TasksPage] failed to retry tasks', error);
        });
    }, [refetch]);

    if (isPending && !isError) {
        return <Loading />;
    }

    if (isError || !tasks) {
        return (
            <Alert
                severity='error'
                action={(
                    <Button color='inherit' size='small' onClick={handleRetry}>
                        {globalize.translate('Retry')}
                    </Button>
                )}
            >
                {globalize.translate('ErrorLoadingData')}
            </Alert>
        );
    }

    const categories = getCategories(tasks);

    return (
        <Page
            id='scheduledTasksPage'
            title={globalize.translate('TabScheduledTasks')}
            className='mainAnimatedPage type-interior'
        >
            <Box className='content-primary'>
                <Box className='readOnlyContent'>
                    <Stack spacing={3} mt={2}>
                        {categories.map(category => {
                            return <Tasks
                                key={category}
                                category={category}
                                tasks={getTasksByCategory(tasks, category)}
                            />;
                        })}
                    </Stack>
                </Box>
            </Box>
        </Page>
    );
};

Component.displayName = 'TasksPage';
