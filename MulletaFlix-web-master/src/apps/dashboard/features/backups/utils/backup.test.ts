import { describe, expect, it } from 'vitest';

import { buildBackupOptions } from './backup';

describe('buildBackupOptions', () => {
    it('keeps the database backup enabled even when the form omits it', () => {
        const formData = new FormData();
        formData.append('Metadata', 'on');
        formData.append('Subtitles', 'on');

        expect(buildBackupOptions(formData)).toEqual({
            Database: true,
            Metadata: true,
            Subtitles: true,
            Trickplay: false
        });
    });

    it('treats unchecked fields as false', () => {
        const formData = new FormData();

        expect(buildBackupOptions(formData)).toEqual({
            Database: true,
            Metadata: false,
            Subtitles: false,
            Trickplay: false
        });
    });
});
