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
const state = new URLSearchParams(window.location.search).get('state');
const getProps = () => {
    if (state === 'loading') return { loading: true, error: false };
    if (state === 'empty') return { loading: false, error: false };
    return {
        latestBackup: { Path: 'D:/Backups/' + 'biblioteca-grande-'.repeat(20) + '.zip', Options: { Database: false, Metadata: true, Subtitles: false, Trickplay: false } },
        status: { IsConfigured: true, IsConnected: false, LastBackupFailed: true, LastBackupStatus: 'Falha de conexão ao sincronizar o catálogo', LastUsersBackupFailed: false, LastUsersBackupCount: 8 },
        error: true,
        loading: false
    };
};

createRoot(document.getElementById('root')!).render(
    <ThemeProvider theme={theme} defaultMode='dark'>
        <BackupCoverageSummary {...getProps()} onRetry={retry} />
    </ThemeProvider>
);
