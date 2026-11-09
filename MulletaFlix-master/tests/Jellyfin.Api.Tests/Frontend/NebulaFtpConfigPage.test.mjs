import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';

const sourcePath = new URL('../../../Jellyfin.Api/Web/NebulaFTP/configPage.js', import.meta.url);
const source = await fs.readFile(sourcePath, 'utf8');
const { getSupabaseStatusViewModel } = await import(`data:text/javascript,${encodeURIComponent(source)}`);

test('Supabase status view model exposes persisted backup failure', () => {
    assert.deepEqual(
        getSupabaseStatusViewModel({
            IsConfigured: true,
            IsConnected: false,
            LastBackupStatus: 'Falha no backup: chave ausente'
        }),
        {
            configured: true,
            connected: false,
            lastBackupStatus: 'Falha no backup: chave ausente'
        }
    );
});

test('Supabase status view model falls back to legacy configuration fields', () => {
    assert.deepEqual(
        getSupabaseStatusViewModel({ SupabaseUrl: 'https://example.supabase.co', SupabaseKey: 'fixture-key', SupabaseLastBackupStatus: 'Concluído' }),
        {
            configured: true,
            connected: false,
            lastBackupStatus: 'Concluído'
        }
    );
});
