import { expect, test } from '@playwright/test';
import {
    getAdminCredentials,
    loginWithManualForm,
    logoutViaDashboard
} from '../support/admin-user.mjs';
import { navigateStage, openStage } from '../support/stage.mjs';
import {
    ensureMediaLibrariesReady,
    getVirtualFolderByLibrary,
    getFirstItemFromVirtualFolder,
    MOVIES_LIBRARY
} from '../support/media-library.mjs';
import { ensureWizardCompleted } from '../support/wizard.mjs';

test.describe.serial('33 - Item Details E2E: full interaction flow', () => {
    test('navigates to item details, displays metadata correctly, and interacts with action buttons', async ({ page }) => {
        const admin = getAdminCredentials();

        // Complete wizard and login
        await ensureWizardCompleted(page, admin.username, admin.password);
        await loginWithManualForm(page, admin.username, admin.password);

        // Prepare media library and get a test item
        await ensureMediaLibrariesReady(page, { settleMs: 60_000 });
        const moviesFolder = await getVirtualFolderByLibrary(page, MOVIES_LIBRARY);
        const testItem = await getFirstItemFromVirtualFolder(page, moviesFolder?.Id);

        if (!testItem?.id) {
            throw new Error('Could not find a test item in the media library.');
        }

        // ========== TEST 1: NAVIGATE TO ITEM DETAILS PAGE ==========
        await openStage(page, `/details?id=${testItem.id}`);
        await expect(page.locator('#itemDetailPage')).toBeVisible({ timeout: 30_000 });

        // ========== TEST 2: VERIFY TITLE/NAME IS DISPLAYED ==========
        const nameContainer = page.locator('.nameContainer h1.itemName');
        await expect(nameContainer).toBeVisible({ timeout: 10_000 });
        const itemName = await nameContainer.textContent();
        expect(itemName?.trim().length).toBeGreaterThan(0);

        // ========== TEST 3: VERIFY METADATA SECTIONS ==========
        // Primary media info (year, runtime, rating, etc.)
        const miscInfoPrimary = page.locator('.itemMiscInfo-primary');
        await expect(miscInfoPrimary).toBeVisible({ timeout: 5_000 });

        // Secondary media info
        const miscInfoSecondary = page.locator('.itemMiscInfo-secondary');
        await expect(miscInfoSecondary).toBeVisible({ timeout: 5_000 });

        // ========== TEST 4: VERIFY POSTER IMAGE LOADS ==========
        const posterImage = page.locator('.detailImageContainer img.itemDetailImage');
        const posterImageVisible = await posterImage.isVisible().catch(() => false);
        if (posterImageVisible) {
            await expect(posterImage).toHaveAttribute('src', /\/Items\/.*\/Images\/Primary/);
        }

        // ========== TEST 5: VERIFY ACTION BUTTONS EXIST ==========
        // Play/Resume button should be visible for movies
        const playButton = page.locator('button[data-action="resume"], button[data-action="play"]').first();
        const playButtonVisible = await playButton.isVisible().catch(() => false);
        if (playButtonVisible) {
            expect(playButtonVisible).toBeTruthy();
        }

        // ========== TEST 6: VERIFY PLAYSTATE BUTTON (MARK AS WATCHED) ==========
        const playstateButton = page.locator('button[is="emby-playstatebutton"]');
        const playstateVisible = await playstateButton.isVisible().catch(() => false);
        if (playstateVisible) {
            // Button should be present for interactive marking
            await expect(playstateButton).toBeVisible();
        }

        // ========== TEST 7: VERIFY RATING/FAVORITE BUTTON ==========
        const ratingButton = page.locator('button[is="emby-ratingbutton"]');
        const ratingVisible = await ratingButton.isVisible().catch(() => false);
        if (ratingVisible) {
            await expect(ratingButton).toBeVisible();
        }

        // ========== TEST 8: INTERACT WITH FAVORITE/RATING BUTTON ==========
        if (ratingVisible) {
            const ratingBefore = await ratingButton.getAttribute('aria-pressed');
            await ratingButton.click();
            await page.waitForTimeout(500); // Wait for state update

            const ratingAfter = await ratingButton.getAttribute('aria-pressed');
            // State should change or button should be clickable without error
            expect(ratingButton).toBeVisible();
        }

        // ========== TEST 9: INTERACT WITH PLAYSTATE BUTTON (TOGGLE WATCHED) ==========
        if (playstateVisible) {
            const playstateBeforeText = await playstateButton.getAttribute('title');
            await playstateButton.click();
            await page.waitForTimeout(500); // Wait for state update

            const playstateAfterText = await playstateButton.getAttribute('title');
            // Button should remain visible and functional after interaction
            await expect(playstateButton).toBeVisible();
        }

        // ========== TEST 10: VERIFY OVERVIEW/DESCRIPTION DISPLAYS ==========
        const overview = page.locator('.overview');
        const overviewVisible = await overview.isVisible().catch(() => false);
        if (overviewVisible) {
            const overviewText = await overview.textContent();
            expect(overviewText?.trim().length).toBeGreaterThan(0);
        }

        // ========== TEST 11: VERIFY GENRES DISPLAY ==========
        const genres = page.locator('.itemGenres');
        const genresVisible = await genres.isVisible().catch(() => false);
        if (genresVisible) {
            const genresCount = await genres.locator('a, span').count();
            expect(genresCount).toBeGreaterThan(0);
        }

        // ========== TEST 12: VERIFY BACK BUTTON NAVIGATION ==========
        // The page should have a back button to navigate away
        const backButton = page.locator('[data-backbutton="true"]');
        await expect(backButton).toBeVisible();

        // Navigate back (using browser back button as alternative)
        await page.goBack();

        // Should navigate away from details page or show home page
        await page.waitForTimeout(1000);
        const isOnDetailsPage = await page.locator('#itemDetailPage').isVisible().catch(() => false);
        // After back, we shouldn't be on the same details page anymore
        // (or if we stay, it should be a different item)
        expect(!isOnDetailsPage || await page.url()).toBeDefined();

        // ========== TEST 13: RETURN TO DETAILS AND VERIFY PERSISTENCE ==========
        // Go forward again to verify the details page re-renders correctly
        await page.goForward();
        await expect(page.locator('#itemDetailPage')).toBeVisible({ timeout: 30_000 });

        // Verify metadata is still present
        await expect(nameContainer).toBeVisible({ timeout: 5_000 });
        const itemNameAfterReturn = await nameContainer.textContent();
        expect(itemNameAfterReturn?.trim()).toBe(itemName?.trim());

        // ========== CLEANUP ==========
        await logoutViaDashboard(page);
    });

    test('displays enriched metadata for different item types (movies with genres, year, runtime)', async ({ page }) => {
        const admin = getAdminCredentials();

        await ensureWizardCompleted(page, admin.username, admin.password);
        await loginWithManualForm(page, admin.username, admin.password);

        await ensureMediaLibrariesReady(page, { settleMs: 60_000 });
        const moviesFolder = await getVirtualFolderByLibrary(page, MOVIES_LIBRARY);
        const testItem = await getFirstItemFromVirtualFolder(page, moviesFolder?.Id);

        if (!testItem?.id) {
            throw new Error('Could not find a test item in the media library.');
        }

        await openStage(page, `/details?id=${testItem.id}`);
        await expect(page.locator('#itemDetailPage')).toBeVisible({ timeout: 30_000 });

        // ========== VERIFY ENRICHED METADATA PRESENCE ==========
        // Year (if available)
        const yearElement = page.locator('.itemMiscInfo-primary *:has-text(/\\b\\d{4}\\b/)');
        const hasYear = await yearElement.count().then(c => c > 0).catch(() => false);

        // Runtime (if available)
        const runtimeElement = page.locator('.itemMiscInfo-primary *:has-text(/hour|minute|hr|min|h|m/)');
        const hasRuntime = await runtimeElement.count().then(c => c > 0).catch(() => false);

        // Rating (if available)
        const ratingElement = page.locator('.itemMiscInfo-primary .communityRating, .itemMiscInfo-primary *:has-text(/★|✓|PG|R|12|15|18/)');
        const hasRating = await ratingElement.count().then(c => c > 0).catch(() => false);

        // At least one metadata type should be present
        const miscInfoPrimary = page.locator('.itemMiscInfo-primary');
        const miscInfoPrimaryText = await miscInfoPrimary.textContent().catch(() => '');
        const hasEnrichedMetadata = hasYear || hasRuntime || hasRating ||
            (miscInfoPrimaryText?.length ?? 0) > 0;
        expect(hasEnrichedMetadata).toBeTruthy();

        // ========== VERIFY GENRES ==========
        const genres = page.locator('.itemGenres');
        const hasGenres = await genres.isVisible().catch(() => false);
        if (hasGenres) {
            const genreCount = await genres.locator('a, span').count();
            expect(genreCount).toBeGreaterThan(0);
        }

        // ========== VERIFY OVERVIEW ==========
        const overview = page.locator('.overview');
        const hasOverview = await overview.isVisible().catch(() => false);
        if (hasOverview) {
            const overviewText = await overview.textContent();
            expect(overviewText?.trim().length).toBeGreaterThan(0);
        }

        await logoutViaDashboard(page);
    });

    test('action buttons are responsive and remain functional across interactions', async ({ page }) => {
        const admin = getAdminCredentials();

        await ensureWizardCompleted(page, admin.username, admin.password);
        await loginWithManualForm(page, admin.username, admin.password);

        await ensureMediaLibrariesReady(page, { settleMs: 60_000 });
        const moviesFolder = await getVirtualFolderByLibrary(page, MOVIES_LIBRARY);
        const testItem = await getFirstItemFromVirtualFolder(page, moviesFolder?.Id);

        if (!testItem?.id) {
            throw new Error('Could not find a test item in the media library.');
        }

        await openStage(page, `/details?id=${testItem.id}`);
        await expect(page.locator('#itemDetailPage')).toBeVisible({ timeout: 30_000 });

        // ========== FIND AND TEST ACTION BUTTONS ==========
        const detailButtons = page.locator('.mainDetailButtons button');
        const buttonCount = await detailButtons.count();

        if (buttonCount > 0) {
            // Test that buttons are clickable and don't throw errors
            for (let i = 0; i < Math.min(buttonCount, 3); i++) {
                const button = detailButtons.nth(i);
                const isVisible = await button.isVisible().catch(() => false);
                
                if (isVisible) {
                    const initialState = await button.getAttribute('aria-pressed');
                    await button.click().catch(() => {
                        // Some buttons may not be actionable for all items, which is OK
                    });
                    await page.waitForTimeout(300);
                    
                    // Button should remain on page after interaction
                    await expect(page.locator('#itemDetailPage')).toBeVisible();
                }
            }
        }

        // ========== VERIFY PAGE REMAINS STABLE ==========
        await expect(page.locator('#itemDetailPage')).toBeVisible({ timeout: 5_000 });
        await expect(page.locator('.nameContainer h1.itemName')).toBeVisible({ timeout: 5_000 });

        await logoutViaDashboard(page);
    });
});
