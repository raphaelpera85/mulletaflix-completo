import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import BackupCoverageSummary from './BackupCoverageSummary';
import type { SupabaseBackupStatus } from '../api/useSupabaseBackupStatus';

const onRetry = () => { /* fixture */ };
const render = (status?: SupabaseBackupStatus, error = false) => renderToStaticMarkup(
    <BackupCoverageSummary status={status} loading={false} error={error} onRetry={onRetry} />
);

describe('backup coverage', () => {
    it('does not interpret connection or legacy text as a successful backup', () => {
        const markup = render({ IsConfigured: true, IsConnected: true, LastBackupStatus: 'legacy text' });
        expect(markup).toContain('Supabase conectado');
        expect(markup).toContain('Resultado não informado');
        expect(markup).not.toContain('Concluído');
    });

    it('labels processed documents as this execution rather than total remote media', () => {
        const markup = render({ IsConfigured: true, IsConnected: true, LastBackupFailed: false, LastBackupProcessedFilesCount: 0, LastBackupProcessedUsersCount: 7 });
        expect(markup).toContain('0 documentos e 7 usuários FTP');
        expect(markup).toContain('apenas as alterações');
        expect(markup).toContain('Os vídeos permanecem na origem');
    });

    it('does not show successful-run counts for a failed attempt', () => {
        const markup = render({ IsConfigured: true, IsConnected: false, LastBackupFailed: true, LastBackupProcessedFilesCount: 42, LastUsersBackupFailed: true, LastUsersBackupCount: 9 });
        expect(markup).toContain('Falha');
        expect(markup).not.toContain('42 documentos');
        expect(markup).not.toContain('Usuários processados: 9');
    });

    it('keeps local coverage visible when the remote query fails and offers retry', () => {
        const markup = render(undefined, true);
        expect(markup).toContain('ZIP local do servidor');
        expect(markup).toContain('Nenhum ZIP disponível');
        expect(markup).toContain('Tentar novamente');
        expect(markup).toContain('Ainda não há registro de exercício isolado');
    });
});
