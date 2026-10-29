import { expect, test } from '@playwright/test';
import path from 'node:path';

import {
    getAdminCredentials,
    loginWithManualForm,
    logoutViaDashboard
} from '../support/admin-user.mjs';
import { ensureWizardCompleted } from '../support/wizard.mjs';
import { fetchStagePublicInfo, navigateStage } from '../support/stage.mjs';
import {
    ensureMediaLibrariesReady,
    getVirtualFolderByLibrary,
    MOVIES_LIBRARY,
    SERIES_LIBRARY
} from '../support/media-library.mjs';

async function getTwoPlayableItems(page, folderId) {
    return page.evaluate(async (parentId) => {
        const result = await window.ApiClient.getItems(window.ApiClient.getCurrentUserId(), {
            ParentId: parentId,
            Limit: 10,
            Recursive: true,
            Fields: 'Path,MediaType,RunTimeTicks,Name'
        });

        return (result?.Items || [])
            .filter(item => item?.Id && item?.Path && item?.MediaType === 'Video')
            .map(item => ({
                id: item.Id,
                name: item.Name || item.Id,
                path: item.Path,
                runtimeTicks: Number(item.RunTimeTicks || 0)
            }))
            .sort((left, right) => left.runtimeTicks - right.runtimeTicks || left.name.localeCompare(right.name));
    }, folderId);
}

async function updateBranding(page, patch) {
    return page.evaluate(async (brandingPatch) => {
        const current = await window.ApiClient.getNamedConfiguration('branding');
        const next = {
            ...current,
            ...brandingPatch
        };

        await window.ApiClient.updateNamedConfiguration('branding', next);
        return next;
    }, patch);
}

async function getVirtualFolderItemByPath(page, folderId, targetPath) {
    return page.evaluate(async ({ parentId, itemPath }) => {
        const result = await window.ApiClient.getItems(window.ApiClient.getCurrentUserId(), {
            ParentId: parentId,
            Limit: 1000,
            Recursive: true,
            IncludeItemTypes: 'Movie',
            MediaTypes: 'Video',
            Fields: 'Path,MediaType,RunTimeTicks,Name,Type'
        });
        const item = (result?.Items || []).find(candidate => String(candidate?.Path || '').replaceAll('\\', '/').toLowerCase() === itemPath);

        return item?.Id ? { id: item.Id, name: item.Name, path: item.Path } : null;
    }, {
        parentId: folderId,
        itemPath: targetPath.replaceAll('\\', '/').toLowerCase()
    });
}

async function prepareAdSensePlayback(page, scriptMode) {
    try {
        await fetchStagePublicInfo();
    } catch {
        // This scenario requires a running stage; skipping is an environmental result.
        test.skip(true, 'Stage server is not available for AdSense playback validation.');
    }

    const admin = getAdminCredentials();
    await ensureWizardCompleted(page, admin.username, admin.password);
    await loginWithManualForm(page, admin.username, admin.password);

    await ensureMediaLibrariesReady(page, {
        timeoutMs: 22 * 60 * 1000,
        pollIntervalMs: 45_000
    });

    const moviesFolder = await getVirtualFolderByLibrary(page, MOVIES_LIBRARY);
    const folderId = moviesFolder?.ItemId || moviesFolder?.Id;
    expect(folderId, 'movie library folder not found').toBeTruthy();

    const playableItems = await getTwoPlayableItems(page, folderId);
    expect(playableItems.length, 'need at least two playable videos for intro + main flow').toBeGreaterThanOrEqual(2);

    const introItem = playableItems[0];
    const movieItem = playableItems[1];

    const originalBranding = await page.evaluate(() => window.ApiClient.getNamedConfiguration('branding'));

    await updateBranding(page, {
        IntroEnabled: true,
        IntroPath: introItem.path,
        AdSenseEnabled: true,
        AdSenseClientId: 'ca-pub-0000000000000000',
        AdSenseSlotId: '1234567890',
        AdSenseHoldSeconds: 1,
        AdSenseShowOnLogin: false,
        AdSenseShowOnHome: false,
        AdSenseShowAfterIntro: true
    });

    await page.route('**/pagead/js/adsbygoogle.js*', async (route) => {
        if (scriptMode === 'success') {
            await route.fulfill({
                contentType: 'application/javascript',
                body: 'window.adsbygoogle = window.adsbygoogle || []; window.adsbygoogle.push = function () { return true; };'
            });
            return;
        }

        await route.abort();
    });

    try {
        await navigateStage(page, `/details?id=${movieItem.id}`);

        const playButton = page.locator('.btnPlayOrResume, .btnPlay').first();
        await expect(playButton).toBeVisible({ timeout: 60_000 });
        await playButton.click({ force: true });

        await expect(page.locator('.adsenseInterstitialOverlay')).toBeVisible({ timeout: 120_000 });
        await expect(page.locator('.btnContinueAdSense')).toBeEnabled({ timeout: 30_000 });
        await page.locator('.btnContinueAdSense').click();

        await expect.poll(async () => (await getActivePlaybackSessions(page, movieItem.id)).length, {
            timeout: 90_000,
            intervals: [ 2_000, 5_000, 10_000 ]
        }).toBe(1);

        const introSessions = await getActivePlaybackSessions(page, introItem.id);
        expect(introSessions).toHaveLength(0);
    } finally {
        await page.evaluate(async (branding) => {
            await window.ApiClient.updateNamedConfiguration('branding', branding);
        }, originalBranding);

        await logoutViaDashboard(page);
    }
}

test.describe.serial('22 - AdSense playback flow', () => {
    test.setTimeout(25 * 60 * 1000);

    test('plays the bundled intro before media with the default branding settings', async ({ page }) => {
        const realStrmFile = process.env.PW_REAL_STRM_FILE;
        test.skip(!realStrmFile, 'Set PW_REAL_STRM_FILE to validate default intro playback using a real Nebula STRM title.');

        const admin = getAdminCredentials();
        await ensureWizardCompleted(page, admin.username, admin.password);
        await loginWithManualForm(page, admin.username, admin.password);
        const realMoviePath = path.join(MOVIES_LIBRARY.path, path.basename(realStrmFile));
        await ensureMediaLibrariesReady(page, {
            timeoutMs: 22 * 60 * 1000,
            pollIntervalMs: 45_000,
            libraries: [ MOVIES_LIBRARY, SERIES_LIBRARY ]
        });

        const moviesFolder = await getVirtualFolderByLibrary(page, MOVIES_LIBRARY);
        const folderId = moviesFolder?.ItemId || moviesFolder?.Id;
        expect(folderId, 'movie library folder not found').toBeTruthy();
        let movie: { id: string; name: string; path: string } | null = null;
        await expect.poll(async () => {
            movie = await getVirtualFolderItemByPath(page, folderId, realMoviePath);
            return Boolean(movie?.id);
        }, {
            timeout: 22 * 60 * 1000,
            intervals: [ 5_000, 15_000, 30_000 ]
        }).toBe(true);

        const branding = await page.evaluate(() => window.ApiClient.getNamedConfiguration('branding'));
        expect(branding.IntroEnabled).toBe(true);
        expect(branding.IntroPath || '').toBe('');
        const intros = await page.evaluate(async (itemId) => (await window.ApiClient.getIntros(itemId))?.Items || [], movie.id);
        expect(intros, 'default bundled intro should be returned before playable media').toHaveLength(1);

        const mediaRequestItemIds: string[] = [];
        const playbackEvents: string[] = [];
        const introDiagnostics: string[] = [];
        const normalizedIntroId = intros[0].Id.replaceAll('-', '').toLowerCase();
        const normalizedMovieId = movie.id.replaceAll('-', '').toLowerCase();
        page.on('request', (request) => {
            const path = new URL(request.url()).pathname;
            const mediaRequestMatch = path.match(/^\/Videos\/([^/]+)\//i);
            if (mediaRequestMatch) {
                const itemId = mediaRequestMatch[1].replaceAll('-', '').toLowerCase();
                mediaRequestItemIds.push(itemId);
                playbackEvents.push(`media:${itemId}`);
            }
            if (request.method() === 'POST' && path === '/Sessions/Playing') {
                try {
                    const itemId = String(request.postDataJSON()?.ItemId || '').replaceAll('-', '').toLowerCase();
                    playbackEvents.push(`session:${itemId}`);
                } catch {
                    // Ignore unrelated session reports without JSON bodies.
                }
            }

            if (request.url().replaceAll('-', '').toLowerCase().includes(normalizedIntroId)) {
                introDiagnostics.push(`${request.method()} ${path}`);
            }
        });
        page.on('response', (response) => {
            const path = new URL(response.url()).pathname;
            const mediaResponseMatch = path.match(/^\/Videos\/([^/]+)\//i);
            if (mediaResponseMatch) {
                const itemId = mediaResponseMatch[1].replaceAll('-', '').toLowerCase();
                playbackEvents.push(`response:${itemId}:${response.status()}`);
            }
            if (response.url().replaceAll('-', '').toLowerCase().includes(normalizedIntroId)) {
                introDiagnostics.push(`HTTP ${response.status()} ${new URL(response.url()).pathname}`);
            }
        });
        page.on('console', (message) => {
            const text = message.text();
            if (/playing next track/i.test(text)) {
                playbackEvents.push(`next:${normalizedMovieId}`);
            }
            if (/intro playback failed|No player found|Failed to play next track/i.test(text)) {
                introDiagnostics.push(text);
            }
        });

        await navigateStage(page, `/details?id=${movie.id}`);
        const playButton = page.locator('.btnPlayOrResume, .btnPlay').first();
        await expect(playButton).toBeVisible({ timeout: 60_000 });
        await playButton.click({ force: true });
        await expect.poll(() => playbackEvents.indexOf(`next:${normalizedMovieId}`), {
            timeout: 90_000,
            intervals: [ 2_000, 5_000, 10_000 ]
        }).toBeGreaterThanOrEqual(0);
        await expect.poll(() => playbackEvents.indexOf(`response:${normalizedMovieId}:200`), {
            timeout: 90_000,
            intervals: [ 2_000, 5_000, 10_000 ]
        }).toBeGreaterThanOrEqual(0);

        expect(mediaRequestItemIds, `intro media should be requested before the selected movie; intro diagnostics: ${introDiagnostics.join(' | ')}`).toEqual(expect.arrayContaining([ normalizedIntroId, normalizedMovieId ]));
        expect(playbackEvents.indexOf(`media:${normalizedIntroId}`)).toBeGreaterThanOrEqual(0);
        expect(playbackEvents.indexOf(`session:${normalizedIntroId}`)).toBeGreaterThanOrEqual(0);
        expect(playbackEvents.indexOf(`session:${normalizedIntroId}`)).toBeLessThan(playbackEvents.indexOf(`next:${normalizedMovieId}`));
        expect(playbackEvents.indexOf(`next:${normalizedMovieId}`)).toBeLessThan(playbackEvents.lastIndexOf(`response:${normalizedMovieId}:200`));
        await logoutViaDashboard(page);
    });

    test('shows the ad after the intro and resumes the main media', async ({ page }) => {
        await prepareAdSensePlayback(page, 'success');
    });

    test('falls back when the ad script fails to load', async ({ page }) => {
        await prepareAdSensePlayback(page, 'fallback');
    });
});
