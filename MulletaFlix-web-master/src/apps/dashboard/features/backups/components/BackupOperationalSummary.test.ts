import { describe, expect, it } from 'vitest';
import { getIncludedBackupOptionKeys, getNextRun, getNextRunLabel } from './BackupOperationalSummary';

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
