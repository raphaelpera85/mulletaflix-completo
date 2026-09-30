import React from 'react';
import { createRoot } from 'react-dom/client';
import { extendTheme, ThemeProvider } from '@mui/material/styles';
import BackupCoverageSummary from '../../../src/apps/dashboard/features/backups/components/BackupCoverageSummary';
import { DEFAULT_COLOR_SCHEME, DEFAULT_THEME_OPTIONS } from '../../../src/themes/_base/theme';

const theme = extendTheme({ ...DEFAULT_THEME_OPTIONS, colorSchemes: { dark: DEFAULT_COLOR_SCHEME } });
const retry = () => {
    const target = window as Window & { backupRetryCalls?: number };
    target.backupRetryCalls = (target.backupRetryCalls ?? 0) + 1;
};

createRoot(document.getElementById('root')!).render(
    <ThemeProvider theme={theme} defaultMode='dark'>
        <BackupCoverageSummary
            latestBackup={{ Path: 'D:/Backups/' + 'biblioteca-grande-'.repeat(20) + '.zip', Options: { Database: false, Metadata: true, Subtitles: false, Trickplay: false } }}
            status={{ IsConfigured: true, IsConnected: false, LastBackupFailed: true, LastBackupStatus: 'Falha de conexão ao sincronizar o catálogo', LastUsersBackupFailed: false, LastUsersBackupCount: 8 }}
            error
            loading={false}
            onRetry={retry}
        />
    </ThemeProvider>
);
