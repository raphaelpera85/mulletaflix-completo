import React, { useCallback } from 'react';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Skeleton from '@mui/material/Skeleton';
import Button from '@mui/material/Button';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import WarningIcon from '@mui/icons-material/Warning';
import ErrorIcon from '@mui/icons-material/Error';
import globalize from 'lib/globalize';
import { useOperationalAlerts, type OperationalAlertSeverity } from 'hooks/useOperationalAlerts';

type SeverityColor = 'success' | 'warning' | 'error';

const severityColor = (severity: OperationalAlertSeverity): SeverityColor => ({
    Info: 'success',
    Warning: 'warning',
    Critical: 'error'
} as Record<OperationalAlertSeverity, SeverityColor>)[severity];

const severityIcon = (severity: OperationalAlertSeverity) => {
    switch (severity) {
        case 'Critical':
            return <ErrorIcon color='error' fontSize='small' />;
        case 'Warning':
            return <WarningIcon color='warning' fontSize='small' />;
        default:
            return <CheckCircleIcon color='success' fontSize='small' />;
    }
};

const OperationalAlertsCard = () => {
    const { data: alerts, isLoading, isError, refetch } = useOperationalAlerts();

    const retry = useCallback(async () => {
        try {
            await refetch();
        } catch {
            // The query state remains responsible for displaying the failure.
        }
    }, [refetch]);

    if (isLoading) {
        return (
            <Paper sx={{ padding: 2 }}>
                <Skeleton variant='rectangular' width='100%' height={24} sx={{ mb: 1 }} />
                <Skeleton variant='rectangular' width='60%' height={20} />
            </Paper>
        );
    }

    if (isError) {
        return (
            <Paper sx={{ padding: 2, textAlign: 'center' }} role='alert'>
                <ErrorIcon color='error' sx={{ mb: 1 }} fontSize='large' />
                <Typography color='error'>{globalize.translate('ErrorLoadingHealthData')}</Typography>
                <Button color='error' size='small' onClick={retry} sx={{ mt: 1 }}>
                    {globalize.translate('Retry')}
                </Button>
            </Paper>
        );
    }

    if (!alerts || alerts.length === 0) {
        return (
            <Paper sx={{ padding: 2 }}>
                <Stack direction='row' spacing={1} alignItems='center'>
                    <CheckCircleIcon color='success' fontSize='small' />
                    <Typography variant='body2'>{globalize.translate('NoActiveAlerts')}</Typography>
                </Stack>
            </Paper>
        );
    }

    return (
        <Paper sx={{ padding: 2 }}>
            <Stack spacing={1}>
                {alerts.map(alert => (
                    <Stack key={alert.kind} direction='row' spacing={1} alignItems='center'>
                        {severityIcon(alert.severity)}
                        <Chip
                            label={alert.severity}
                            color={severityColor(alert.severity)}
                            size='small'
                            variant='outlined'
                        />
                        <Typography variant='body2'>{alert.message}</Typography>
                    </Stack>
                ))}
            </Stack>
        </Paper>
    );
};

export default OperationalAlertsCard;
