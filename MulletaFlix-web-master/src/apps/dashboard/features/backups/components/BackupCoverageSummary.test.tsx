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

    it('shows typed Mongo restore counts without treating an operational restore as a full exercise', () => {
        const markup = render({
            IsConfigured: true,
            IsConnected: true,
            LastRestoreTime: '2026-10-04T10:00:00Z',
            LastRestoreFailed: false,
            LastRestoreFilesRestored: 240,
            LastRestoreUsersRestored: 8,
            LastRestoreFtpUsersRestored: 5,
            LastRestoreAppUsersRestored: 3,
            LastRestoreStatus: 'Restauração do MongoDB realizada com sucesso'
        });

        expect(markup).toContain('240 arquivos do catálogo; total de usuários: 8');
        expect(markup).toContain('FTP: 5 e aplicativo: 3');
        expect(markup).toContain('não representa, por si só, um exercício isolado');
    });

    it('does not fabricate zero restore counts when the server cannot report them', () => {
        const markup = render({
            IsConfigured: true,
            IsConnected: true,
            LastRestoreTime: '2026-10-04T10:00:00Z',
            LastRestoreFailed: true,
            LastRestoreFilesRestored: null,
            LastRestoreUsersRestored: null,
            LastRestoreFtpUsersRestored: 0,
            LastRestoreAppUsersRestored: null
        });

        expect(markup).toContain('não informada arquivos do catálogo; total de usuários: não informado; FTP: 0 e aplicativo: não informado');
        expect(markup).not.toContain('0 arquivos do catálogo');
    });
});
