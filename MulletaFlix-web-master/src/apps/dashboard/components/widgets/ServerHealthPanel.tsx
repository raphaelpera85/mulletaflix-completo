import React, { useMemo } from 'react';
import globalize from 'lib/globalize';
import Widget from './Widget';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Grid from '@mui/material/Grid';
import Skeleton from '@mui/material/Skeleton';
import Box from '@mui/material/Box';
import StorageIcon from '@mui/icons-material/Storage';
import TaskIcon from '@mui/icons-material/Task';
import ExtensionIcon from '@mui/icons-material/Extension';
import BackupTableIcon from '@mui/icons-material/BackupTable';
import ComputerIcon from '@mui/icons-material/Computer';
import WarningIcon from '@mui/icons-material/Warning';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorIcon from '@mui/icons-material/Error';
import InfoIcon from '@mui/icons-material/Info';
import { useServerHealth } from 'hooks/useServerHealth';

type HealthStatus = 'Ok' | 'Warning' | 'Critical';

interface StorageHealthDto {
  totalFreeSpace: number;
  totalUsedSpace: number;
  criticalCount: number;
  warningCount: number;
  healthyCount: number;
  criticalPaths: string[];
  warningPaths: string[];
  status: HealthStatus;
}

interface TaskHealthDto {
  totalCount: number;
  runningCount: number;
  failedCount: number;
  overdueCount: number;
  failedTaskNames: string[];
  overdueTaskNames: string[];
  runningTaskNames: string[];
  status: HealthStatus;
}

interface PluginHealthDto {
  totalCount: number;
  enabledCount: number;
  disabledCount: number;
  incompatibleCount: number;
  updateAvailableCount: number;
  incompatibleNames: string[];
  updateAvailableNames: string[];
  status: HealthStatus;
}

interface BackupHealthDto {
  totalBackups: number;
  lastBackupTime: string | null;
  lastSuccessfulBackupTime: string | null;
  lastBackupResult: string | null;
  lastBackupSize: string;
  status: HealthStatus;
}

interface SystemHealthDto {
  hasPendingRestart: boolean;
  hasUpdateAvailable: boolean;
  startupWizardCompleted: boolean;
  status: HealthStatus;
}

interface ServerHealthSummaryDto {
  timestamp: string;
  serverName: string;
  version: string;
  hasPendingRestart: boolean;
  isShuttingDown: boolean;
  storage: StorageHealthDto;
  tasks: TaskHealthDto;
  plugins: PluginHealthDto;
  backup: BackupHealthDto;
  system: SystemHealthDto;
  overallStatus: HealthStatus;
}

const HealthStatusIcon = ({ status }: { status: HealthStatus }) => {
  switch (status) {
    case 'Ok':
      return <CheckCircleIcon color="success" fontSize="small" />;
    case 'Warning':
      return <WarningIcon color="warning" fontSize="small" />;
    case 'Critical':
      return <ErrorIcon color="error" fontSize="small" />;
    default:
      return <InfoIcon color="info" fontSize="small" />;
  }
};

type HealthStatusColor = 'success' | 'warning' | 'error' | 'info';

const HealthStatusChip = ({ status, label }: { status: HealthStatus; label: string }) => {
  const colors: Record<HealthStatus, HealthStatusColor> = {
    Ok: 'success',
    Warning: 'warning',
    Critical: 'error'
  };

  return (
    <Chip
      label={label}
      icon={<HealthStatusIcon status={status} />}
      color={colors[status]}
      size="small"
      variant="outlined"
    />
  );
};

const HealthCard = ({ 
  title, 
  icon, 
  status, 
  children, 
  href 
}: { 
  title: string; 
  icon: React.ReactNode; 
  status: HealthStatus; 
  children: React.ReactNode;
  href?: string;
}) => {
  const statusColors: Record<HealthStatus, string> = {
    Ok: 'success.light',
    Warning: 'warning.light',
    Critical: 'error.light'
  };

  return (
    <Grid item xs={12} sm={6} md={4}>
      <Paper 
        elevation={2} 
        sx={{ 
          padding: 2, 
          height: '100%',
          borderLeft: `4px solid ${statusColors[status]}`,
          backgroundColor: `${statusColors[status]}08`
        }}
      >
        <Stack direction="row" spacing={1} alignItems="flex-start">
          <Box sx={{ color: 'primary.main' }}>{icon}</Box>
          <Stack flexGrow={1} spacing={0.5}>
            <Typography variant="subtitle2" fontWeight={600}>{title}</Typography>
            <HealthStatusChip status={status} label={globalize.translate(`HealthStatus${status}`)} />
          </Stack>
        </Stack>
        <Box sx={{ mt: 1, pt: 1, borderTop: 1, borderColor: 'divider' }}>
          {children}
        </Box>
        {href && (
          <Typography variant="caption" color="primary" sx={{ mt: 1, display: 'block', cursor: 'pointer' }}>
            {globalize.translate('LabelViewDetails')} →
          </Typography>
        )}
      </Paper>
    </Grid>
  );
};

const formatBytes = (bytes: number) => {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
};

const ServerHealthPanel = () => {
  const { data: health, isLoading, isError } = useServerHealth();

  if (isLoading) {
    return (
      <Widget title={globalize.translate('ServerHealth')} href="/dashboard/server-health">
        <Stack spacing={2} sx={{ minHeight: 200 }}>
          {[1, 2, 3].map(i => (
            <Paper key={i} sx={{ padding: 2 }}>
              <Skeleton variant="rectangular" width="100%" height={40} />
              <Skeleton variant="rectangular" width="60%" height={24} />
              <Skeleton variant="rectangular" width="40%" height={24} />
            </Paper>
          ))}
        </Stack>
      </Widget>
    );
  }

  if (isError || !health) {
    return (
      <Widget title={globalize.translate('ServerHealth')} href="/dashboard/server-health">
        <Paper sx={{ padding: 2, textAlign: 'center' }}>
          <ErrorIcon color="error" sx={{ mb: 1 }} fontSize="large" />
          <Typography color="error">{globalize.translate('ErrorLoadingHealthData')}</Typography>
        </Paper>
      </Widget>
    );
  }

  const overallStatusColors: Record<HealthStatus, string> = {
    Ok: 'success.main',
    Warning: 'warning.main',
    Critical: 'error.main'
  };

  return (
    <Widget 
      title={globalize.translate('ServerHealth')}
      href="/dashboard/server-health"
    >
      <Paper sx={{ padding: 2 }}>
        <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 2, pb: 1, borderBottom: 1, borderColor: 'divider' }}>
          <Typography variant="body2" color="text.secondary">
            {globalize.translate('LastUpdated')}: {new Date(health.timestamp).toLocaleString()}
          </Typography>
          <Box sx={{ flexGrow: 1 }} />
          <Chip 
            label={globalize.translate('OverallStatus')} 
            icon={<HealthStatusIcon status={health.overallStatus} />}
            color={health.overallStatus === 'Ok' ? 'success' : health.overallStatus === 'Warning' ? 'warning' : 'error'}
            variant="outlined"
            size="small"
          />
        </Stack>
        
        <Grid container spacing={2}>
          {/* Storage Card */}
          <HealthCard
            title={globalize.translate('Storage')}
            icon={<StorageIcon />}
            status={health.storage.status}
            href="/dashboard/settings?tab=storage"
          >
            <Stack spacing={0.5}>
              <Typography variant="body2">
                {formatBytes(health.storage.totalFreeSpace)} {globalize.translate('FreeOf')} {formatBytes(health.storage.totalFreeSpace + health.storage.totalUsedSpace)}
              </Typography>
              <Stack direction="row" spacing={0.5}>
                <Chip label={`${health.storage.healthyCount} OK`} color="success" size="small" variant="outlined" />
                {health.storage.warningCount > 0 && <Chip label={`${health.storage.warningCount} Warning`} color="warning" size="small" variant="outlined" />}
                {health.storage.criticalCount > 0 && <Chip label={`${health.storage.criticalCount} Critical`} color="error" size="small" variant="outlined" />}
              </Stack>
            </Stack>
          </HealthCard>

          {/* Tasks Card */}
          <HealthCard
            title={globalize.translate('ScheduledTasks')}
            icon={<TaskIcon />}
            status={health.tasks.status}
            href="/dashboard/tasks"
          >
            <Stack spacing={0.5}>
              <Typography variant="body2">
                {health.tasks.totalCount} {globalize.translate('Total')}, {health.tasks.runningCount} {globalize.translate('Running')}
              </Typography>
              <Stack direction="row" spacing={0.5}>
                <Chip label={`${health.tasks.failedCount} Failed`} color={health.tasks.failedCount > 0 ? 'error' : 'success'} size="small" variant="outlined" />
                <Chip label={`${health.tasks.overdueCount} Overdue`} color={health.tasks.overdueCount > 0 ? 'warning' : 'success'} size="small" variant="outlined" />
              </Stack>
            </Stack>
          </HealthCard>

          {/* Plugins Card */}
          <HealthCard
            title={globalize.translate('Plugins')}
            icon={<ExtensionIcon />}
            status={health.plugins.status}
            href="/dashboard/plugins"
          >
            <Stack spacing={0.5}>
              <Typography variant="body2">
                {health.plugins.enabledCount} {globalize.translate('Enabled')}, {health.plugins.disabledCount} {globalize.translate('Disabled')}
              </Typography>
              <Stack direction="row" spacing={0.5}>
                <Chip label={`${health.plugins.incompatibleCount} Incompatible`} color={health.plugins.incompatibleCount > 0 ? 'error' : 'success'} size="small" variant="outlined" />
                <Chip label={`${health.plugins.updateAvailableCount} Updates`} color={health.plugins.updateAvailableCount > 0 ? 'warning' : 'success'} size="small" variant="outlined" />
              </Stack>
            </Stack>
          </HealthCard>

          {/* Backup Card */}
          <HealthCard
            title={globalize.translate('Backups')}
            icon={<BackupTableIcon />}
            status={health.backup.status}
            href="/dashboard/backups"
          >
            <Stack spacing={0.5}>
              <Typography variant="body2">
                {health.backup.lastSuccessfulBackupTime 
                  ? `${globalize.translate('LastSuccess')}: ${new Date(health.backup.lastSuccessfulBackupTime).toLocaleDateString()}` 
                  : globalize.translate('NoSuccessfulBackups')}
              </Typography>
              <Stack direction="row" spacing={0.5}>
                <Chip label={`${health.backup.totalBackups} Total`} size="small" variant="outlined" />
                {health.backup.lastBackupResult && (
                  <Chip 
                    label={health.backup.lastBackupResult === 'Completed' ? globalize.translate('Success') : globalize.translate('Failed')}
                    color={health.backup.lastBackupResult === 'Completed' ? 'success' : 'error'} 
                    size="small" 
                    variant="outlined" 
                  />
                )}
              </Stack>
            </Stack>
          </HealthCard>

          {/* System Card */}
          <HealthCard
            title={globalize.translate('System')}
            icon={<ComputerIcon />}
            status={health.system.status}
            href="/dashboard/system"
          >
            <Stack spacing={0.5}>
              <Typography variant="body2">
                {health.system.hasPendingRestart ? globalize.translate('RestartRequired') : globalize.translate('RunningNormally')}
              </Typography>
              <Stack direction="row" spacing={0.5}>
                <Chip label={health.system.startupWizardCompleted ? globalize.translate('SetupComplete') : globalize.translate('SetupIncomplete')} color={health.system.startupWizardCompleted ? 'success' : 'warning'} size="small" variant="outlined" />
                {health.system.hasUpdateAvailable && <Chip label={globalize.translate('UpdateAvailable')} color="primary" size="small" variant="outlined" />}
              </Stack>
            </Stack>
          </HealthCard>
        </Grid>
      </Paper>
    </Widget>
  );
};

export default ServerHealthPanel;