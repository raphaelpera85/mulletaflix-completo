import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import type { BackupManifestDto } from '@jellyfin/sdk/lib/generated-client/models/backup-manifest-dto';
import type { BackupOptionsDto } from '@jellyfin/sdk/lib/generated-client/models/backup-options-dto';
import type { TaskInfo } from '@jellyfin/sdk/lib/generated-client/models/task-info';
import { TaskTriggerInfoType } from '@jellyfin/sdk/lib/generated-client/models/task-trigger-info-type';
import globalize from 'lib/globalize';
import { getReadableSize } from 'utils/file';

type BackupWithSize = BackupManifestDto & { SizeBytes?: number };
type BackupOptionKey = keyof BackupOptionsDto;

const backupContentLabels: Record<BackupOptionKey, string> = {
    Database: 'LabelDatabase',
    Metadata: 'LabelMetadata',
    Subtitles: 'Subtitles',
    Trickplay: 'Trickplay'
};

const getIncludedBackupOptionKeys = (options?: BackupOptionsDto) => {
    const keys: BackupOptionKey[] = [ 'Database', 'Metadata', 'Subtitles', 'Trickplay' ];
    return keys.filter(key => options?.[key] === true);
};

type TaskInfoWithNextExecution = TaskInfo & {
    NextExecutionTimeUtc?: string | null;
    NextExecutionTimeOffsetMinutes?: number | null;
};

const getNextRun = (task?: TaskInfoWithNextExecution) => {
    const value = task?.NextExecutionTimeUtc;
    if (!value) return null;
    const nextRun = new Date(value);
    return Number.isNaN(nextRun.getTime()) ? null : nextRun;
};

const getRunColor = (status?: string) => {
    if (status === 'Failed') return 'error';
    if (status === 'Completed') return 'success';
    return 'default';
};

const getRunLabel = (status?: string) => {
    if (status === 'Completed') return globalize.translate('LabelSuccess');
    if (status === 'Failed') return globalize.translate('LabelFailed');
    if (status === 'Cancelled') return globalize.translate('LabelCancelled');
    if (status === 'Aborted') return globalize.translate('LabelAborted');
    return globalize.translate('LabelUnknown');
};

const getNextRunLabel = (nextRun: Date | null, task?: TaskInfoWithNextExecution) => {
    if (nextRun) {
        const offsetMinutes = task?.NextExecutionTimeOffsetMinutes;
        if (typeof offsetMinutes !== 'number') return nextRun.toLocaleString();

        const offsetHours = Math.floor(Math.abs(offsetMinutes) / 60).toString().padStart(2, '0');
        const offsetRemainder = (Math.abs(offsetMinutes) % 60).toString().padStart(2, '0');
        const offsetSign = offsetMinutes < 0 ? '−' : '+';
        const serverWallTime = new Date(nextRun.getTime() + offsetMinutes * 60_000);
        const serverWallTimeText = serverWallTime.toISOString().slice(0, 19).replace('T', ' ');
        return `${serverWallTimeText} (UTC${offsetSign}${offsetHours}:${offsetRemainder})`;
    }
    if (task?.Triggers?.some(trigger => trigger.Type === TaskTriggerInfoType.StartupTrigger)) {
        return globalize.translate('LabelNextServerStart');
    }
    return globalize.translate('LabelBackupScheduleUnavailable');
};

const formatBackupSize = (size?: number) => {
    if (typeof size === 'number') return getReadableSize(size);
    return globalize.translate('LabelBackupSizeUnavailable');
};

const formatBackupContents = (contents: string[]) => {
    if (contents.length > 0) return contents.join(' · ');
    return globalize.translate('LabelNoBackupContents');
};

type Props = {
    task?: TaskInfo;
    latestBackup?: BackupWithSize;
};

const BackupOperationalSummary = ({ task, latestBackup }: Props) => {
    const lastRun = task?.LastExecutionResult;
    const nextRun = getNextRun(task);
    const backupContents = latestBackup?.Options;
    const includedBackupContents = getIncludedBackupOptionKeys(backupContents)
        .map(key => globalize.translate(backupContentLabels[key]));
    const backupSize = formatBackupSize(latestBackup?.SizeBytes);

    return (
        <Paper
            component='section'
            aria-labelledby='backupOperationalSummaryTitle'
            variant='outlined'
            sx={{ p: { xs: 2, md: 3 }, borderLeft: 4, borderLeftColor: 'primary.main' }}
        >
            <Stack spacing={2}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                    <Typography id='backupOperationalSummaryTitle' variant='h2'>
                        {globalize.translate('HeaderBackupStatus')}
                    </Typography>
                    <Chip
                        size='small'
                        color={getRunColor(lastRun?.Status) as 'error' | 'success' | 'default'}
                        label={getRunLabel(lastRun?.Status)}
                    />
                </Box>

                {lastRun?.Status === 'Failed' && lastRun.ErrorMessage && (
                    <Alert severity='error' role='status'>
                        {lastRun.ErrorMessage}
                    </Alert>
                )}

                <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' }, gap: 2 }}>
                    <Box>
                        <Typography variant='subtitle2'>{globalize.translate('LabelLastBackupRun')}</Typography>
                        <Typography color='text.secondary'>
                            {lastRun?.EndTimeUtc ? new Date(lastRun.EndTimeUtc).toLocaleString() : globalize.translate('LabelNoBackupHistoryAvailable')}
                        </Typography>
                    </Box>
                    <Box>
                        <Typography variant='subtitle2'>{globalize.translate('LabelNextBackupRun')}</Typography>
                        <Typography color='text.secondary'>
                            {getNextRunLabel(nextRun, task)}
                        </Typography>
                    </Box>
                </Box>

                {latestBackup ? (
                    <Box sx={{ minWidth: 0 }}>
                        <Typography variant='subtitle2'>{globalize.translate('LabelLatestBackup')}</Typography>
                        <Typography color='text.secondary' sx={{ overflowWrap: 'anywhere' }}>
                            {latestBackup.DateCreated} · {latestBackup.Path} · {globalize.translate('LabelSize')}: {backupSize}
                        </Typography>
                        <Typography variant='body2' color='text.secondary'>
                            {formatBackupContents(includedBackupContents)}
                        </Typography>
                    </Box>
                ) : (
                    <Typography color='text.secondary'>{globalize.translate('LabelBackupsUnavailable')}</Typography>
                )}
            </Stack>
        </Paper>
    );
};

export { getIncludedBackupOptionKeys, getNextRun, getNextRunLabel };
export default BackupOperationalSummary;
