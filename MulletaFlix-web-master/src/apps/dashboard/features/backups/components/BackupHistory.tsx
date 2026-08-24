import Box from '@mui/material/Box';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemText from '@mui/material/ListItemText';
import ListItemSecondaryAction from '@mui/material/ListItemSecondaryAction';
import Chip from '@mui/material/Chip';
import Typography from '@mui/material/Typography';
import IconButton from '@mui/material/IconButton';
import RefreshIcon from '@mui/icons-material/Refresh';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorIcon from '@mui/icons-material/Error';
import WarningIcon from '@mui/icons-material/Warning';
import globalize from 'lib/globalize';
import React from 'react';
import { useBackupHistory } from 'apps/dashboard/features/backups/api/useBackupHistory';
import { TaskCompletionStatus, type BackupExecutionHistoryDto } from 'apps/dashboard/features/backups/api/types';

const getStatusIcon = (status: TaskCompletionStatus) => {
  switch (status) {
    case TaskCompletionStatus.Success:
      return <CheckCircleIcon color="success" fontSize="small" />;
    case TaskCompletionStatus.Failed:
      return <ErrorIcon color="error" fontSize="small" />;
    case TaskCompletionStatus.Aborted:
    case TaskCompletionStatus.Cancelled:
      return <WarningIcon color="warning" fontSize="small" />;
    default:
      return <Chip label={status} size="small" />;
  }
};

const getStatusChip = (status: TaskCompletionStatus) => {
  switch (status) {
    case TaskCompletionStatus.Success:
      return <Chip label={globalize.translate('LabelSuccess')} color="success" size="small" variant="outlined" />;
    case TaskCompletionStatus.Failed:
      return <Chip label={globalize.translate('LabelFailed')} color="error" size="small" variant="outlined" />;
    case TaskCompletionStatus.Aborted:
      return <Chip label={globalize.translate('LabelAborted')} color="warning" size="small" variant="outlined" />;
    case TaskCompletionStatus.Cancelled:
      return <Chip label={globalize.translate('LabelCancelled')} color="default" size="small" variant="outlined" />;
    default:
      return <Chip label={status} size="small" variant="outlined" />;
  }
};

const formatDuration = (seconds: number) => {
  if (seconds < 60) {
    return `${Math.round(seconds)}s`;
  }
  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = Math.round(seconds % 60);
  return `${minutes}m ${remainingSeconds}s`;
};

const formatDate = (dateString: string) => {
  try {
    return new Date(dateString).toLocaleString();
  } catch {
    return dateString;
  }
};

export const BackupHistory = () => {
  const { data: history, isPending, isError, refetch } = useBackupHistory();

  if (isPending) {
    return (
      <Box sx={{ p: 2, textAlign: 'center' }}>
        <Typography variant="body2" color="text.secondary">
          {globalize.translate('Loading')}
        </Typography>
      </Box>
    );
  }

  if (isError || !history || history.length === 0) {
    return (
      <Box sx={{ p: 2, textAlign: 'center' }}>
        <Typography variant="body2" color="text.secondary">
          {globalize.translate('LabelNoBackupHistoryAvailable')}
        </Typography>
      </Box>
    );
  }

  return (
    <Box sx={{ p: 2 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h6">{globalize.translate('HeaderBackupHistory')}</Typography>
        <IconButton onClick={() => refetch()} aria-label={globalize.translate('ButtonRefresh')}>
          <RefreshIcon />
        </IconButton>
      </Box>
      <List sx={{ bgcolor: 'background.paper' }}>
        {history.map((entry: BackupExecutionHistoryDto) => (
          <ListItem key={entry.Id} sx={{ px: 2, py: 1.5 }}>
            <ListItemText
              primary={
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                  <Typography variant="body1" component="span">
                    {entry.Name}
                  </Typography>
                  {getStatusChip(entry.Status)}
                </Box>
              }
              secondary={
                <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}>
                  <Typography variant="body2" color="text.secondary">
                    {globalize.translate('LabelStarted')}: {formatDate(entry.StartTimeUtc)}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    {globalize.translate('LabelEnded')}: {formatDate(entry.EndTimeUtc)}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    {globalize.translate('LabelDuration')}: {formatDuration(entry.DurationSeconds)}
                  </Typography>
                  {entry.ErrorMessage && (
                    <Typography variant="body2" color="error">
                      {globalize.translate('LabelError')}: {entry.ErrorMessage}
                    </Typography>
                  )}
                </Box>
              }
            />
            <ListItemSecondaryAction>
              {getStatusIcon(entry.Status)}
            </ListItemSecondaryAction>
          </ListItem>
        ))}
      </List>
    </Box>
  );
};

export default BackupHistory;