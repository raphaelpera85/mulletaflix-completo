import { expect, test } from '@playwright/test';
import crypto from 'node:crypto';
import { getAdminCredentials, loginWithManualForm } from '../support/admin-user.mjs';
import { navigateStage } from '../support/stage.mjs';
import { ensureWizardCompleted } from '../support/wizard.mjs';

test('creates an empty playlist, discovers it from navigation, and keeps it after reload', async ({ page }) => {
    const admin = getAdminCredentials();
    await ensureWizardCompleted(page, admin.username, admin.password);
    await loginWithManualForm(page, admin.username, admin.password);

    await navigateStage(page, '/playlists');
    const playlistLink = page.getByRole('button', { name: /^(Playlists|Listas de Reprodução)$/i });
    await expect(playlistLink).toBeVisible();
    await expect(page.locator('#playlistsPage')).toBeVisible();

    const playlistName = `UX playlist ${crypto.randomUUID().slice(0, 8)}`;
    await page.getByRole('button', { name: /^(New Playlist|Nova Playlist)$/i }).click();
    const dialog = page.locator('.formDialog');
    await expect(dialog).toBeVisible();
    await dialog.locator('#txtNewPlaylistName').fill(playlistName);
    await dialog.locator('.button-submit').click();

    await expect(dialog).toBeHidden({ timeout: 30_000 });
    await navigateStage(page, '/playlists');
    await expect(page.getByText(playlistName, { exact: true })).toBeVisible({ timeout: 30_000 });

    await page.reload();
    await navigateStage(page, '/playlists');
    await expect(page.getByText(playlistName, { exact: true })).toBeVisible({ timeout: 30_000 });
});
