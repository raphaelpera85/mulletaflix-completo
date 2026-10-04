import { expect, test, type BrowserContext } from '@playwright/test';
import crypto from 'node:crypto';
import { getAdminCredentials, loginWithManualForm } from '../support/admin-user.mjs';
import { restartOwnedStage } from '../support/restart-stage.mjs';
import { getStageBaseUrl, navigateStage, openStage } from '../support/stage.mjs';
import { ensureWizardCompleted } from '../support/wizard.mjs';

test('creates an empty playlist, discovers it from the home navigation, and persists it across sessions', async ({ page }) => {
    const admin = getAdminCredentials();
    await ensureWizardCompleted(page, admin.username, admin.password);
    await loginWithManualForm(page, admin.username, admin.password);

    await navigateStage(page, '/home');
    const playlistLink = page.getByRole('link', { name: /^(Playlists|Listas de Reprodução)$/i });
    await expect(playlistLink).toBeVisible();
    await playlistLink.click();
    await expect(page.locator('#playlistsPage')).toBeVisible();

    const playlistName = `UX playlist ${crypto.randomUUID().slice(0, 8)}`;
    let playlistId: string | undefined;
    let persistedContext: BrowserContext | undefined;
    let persistedStorageState: Awaited<ReturnType<BrowserContext['storageState']>> | undefined;
    let testFailure: unknown;

    try {
        await page.getByRole('button', { name: /^(New Playlist|Nova Playlist)$/i }).click();
        const dialog = page.locator('.formDialog');
        await expect(dialog).toBeVisible();
        await dialog.locator('#txtNewPlaylistName').fill(playlistName);
        const creationResponsePromise = page.waitForResponse(response =>
            response.request().method() === 'POST'
            && new URL(response.url()).pathname.endsWith('/Playlists')
        );
        await dialog.locator('.button-submit').click();
        const creationResponse = await creationResponsePromise;
        expect(creationResponse.ok()).toBeTruthy();
        const creation = await creationResponse.json() as { Id?: string };
        playlistId = creation.Id;
        expect(playlistId).toBeTruthy();

        await expect(dialog).toBeHidden({ timeout: 30_000 });
        // Empty playlists have no details content; land on their index so the new list is discoverable.
        await expect(page.locator('#playlistsPage')).toBeVisible({ timeout: 30_000 });
        const playlistCard = page.locator('#playlistsPage').getByText(playlistName, { exact: true });
        await expect(playlistCard).toBeVisible({ timeout: 30_000 });

        const browser = page.context().browser();
        if (!browser) {
            throw new Error('Playwright browser is unavailable to verify playlist persistence in a new session.');
        }

        persistedStorageState = await page.context().storageState();
        persistedContext = await browser.newContext({ storageState: persistedStorageState });
        const persistedPage = await persistedContext.newPage();
        const listingResponsePromise = persistedPage.waitForResponse(response =>
            response.request().method() === 'GET'
            && new URL(response.url()).pathname.endsWith('/Playlists')
        );
        await openStage(persistedPage, '/playlists');
        const listingResponse = await listingResponsePromise;
        expect(listingResponse.ok()).toBeTruthy();
        const listing = await listingResponse.json() as { Items?: Array<{ Id?: string; Name?: string }> };
        expect(listing.Items?.some(item =>
            item.Id?.replaceAll('-', '').toLowerCase() === playlistId?.replaceAll('-', '').toLowerCase()
            && item.Name === playlistName
        )).toBe(true);
        await expect(persistedPage.locator('#playlistsPage').getByText(playlistName, { exact: true })).toBeVisible({ timeout: 30_000 });

        await persistedContext.close();
        persistedContext = undefined;

        await restartOwnedStage();
        persistedContext = await browser.newContext({ storageState: persistedStorageState });
        const restartedPage = await persistedContext.newPage();
        const restartedListingPromise = restartedPage.waitForResponse(response =>
            response.request().method() === 'GET'
            && new URL(response.url()).pathname.endsWith('/Playlists')
        );
        await openStage(restartedPage, '/playlists');
        const restartedListingResponse = await restartedListingPromise;
        expect(restartedListingResponse.ok()).toBeTruthy();
        const restartedListing = await restartedListingResponse.json() as { Items?: Array<{ Id?: string; Name?: string }> };
        expect(restartedListing.Items?.some(item =>
            item.Id?.replaceAll('-', '').toLowerCase() === playlistId?.replaceAll('-', '').toLowerCase()
            && item.Name === playlistName
        )).toBe(true);
        await expect(restartedPage.locator('#playlistsPage').getByText(playlistName, { exact: true })).toBeVisible({ timeout: 30_000 });
    } catch (error) {
        testFailure = error;
    } finally {
        await persistedContext?.close();
    }

    let cleanupFailure: Error | undefined;
    if (playlistId) {
        try {
            const storage = await page.evaluate(() => JSON.parse(localStorage.getItem('jellyfin_credentials') || '{}'));
            const token = storage.Servers?.find((server: { AccessToken?: string }) => server.AccessToken)?.AccessToken;
            if (!token) {
                throw new Error(`Could not clean up test playlist ${playlistId}: authenticated server token is unavailable.`);
            }

            const cleanupResponse = await fetch(`${getStageBaseUrl()}/Items/${playlistId}`, {
                method: 'DELETE',
                headers: { 'X-Emby-Token': token }
            });
            if (![200, 204, 404].includes(cleanupResponse.status)) {
                throw new Error(`Could not clean up test playlist ${playlistId}: DELETE returned HTTP ${cleanupResponse.status}.`);
            }
        } catch (error) {
            cleanupFailure = error instanceof Error ? error : new Error(String(error));
        }
    }

    if (testFailure) {
        throw testFailure;
    }

    if (cleanupFailure) {
        throw cleanupFailure;
    }
});
