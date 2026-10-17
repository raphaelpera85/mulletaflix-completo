import { describe, expect, it } from 'vitest';
import { getIncludedBackupOptionKeys, getLatestBackupRun, getNextRun, getNextRunLabel } from './BackupOperationalSummary';
import { TaskCompletionStatus, type BackupExecutionHistoryDto } from 'apps/dashboard/features/backups/api/types';
import type { TaskInfo } from '@jellyfin/sdk/lib/generated-client/models/task-info';

describe('getIncludedBackupOptionKeys', () => {
    it('reports only the contents enabled in the archive manifest', () => {
        expect(getIncludedBackupOptionKeys({
            Database: false,
            Metadata: true,
            Subtitles: false,
            Trickplay: true
        })).toEqual([ 'Metadata', 'Trickplay' ]);
    });
});

describe('getNextRun', () => {
    it('uses the UTC schedule provided by the server', () => {
        expect(getNextRun({ NextExecutionTimeUtc: '2026-09-28T06:00:00Z' }))
            .toEqual(new Date('2026-09-28T06:00:00Z'));
    });

    it('keeps the server wall time and labels the server offset', () => {
        const task = { NextExecutionTimeUtc: '2026-09-28T06:00:00Z', NextExecutionTimeOffsetMinutes: -180 };
        expect(getNextRunLabel(getNextRun(task), task)).toBe('2026-09-28 03:00:00 (UTC−03:00)');
    });

    it('returns null when the server cannot determine an exact next execution', () => {
        expect(getNextRun({ NextExecutionTimeUtc: null })).toBeNull();
        expect(getNextRun({ NextExecutionTimeUtc: 'not-a-date' })).toBeNull();
        expect(getNextRun()).toBeNull();
    });
});

describe('backup history status contract', () => {
    it('uses the server TaskCompletionStatus.Completed value for successful backups', () => {
        expect(TaskCompletionStatus.Completed).toBe('Completed');
    });

    it('prefers the newest persisted execution and falls back to scheduled task history', () => {
        const manualRun = { Id: 'manual' } as BackupExecutionHistoryDto;
        const scheduledRun = { Id: 'scheduled' } as NonNullable<TaskInfo['LastExecutionResult']>;

        expect(getLatestBackupRun([manualRun], scheduledRun)).toBe(manualRun);
        expect(getLatestBackupRun([], scheduledRun)).toBe(scheduledRun);
    });
});
