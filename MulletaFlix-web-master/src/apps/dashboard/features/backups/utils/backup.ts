import type { BackupOptionsDto } from '@jellyfin/sdk/lib/generated-client/models/backup-options-dto';

export const buildBackupOptions = (formData: FormData): BackupOptionsDto => {
    const readFlag = (name: string) => formData.get(name)?.toString() === 'on';

    return {
        Database: true,
        Metadata: readFlag('Metadata'),
        Subtitles: readFlag('Subtitles'),
        Trickplay: readFlag('Trickplay')
    };
};
