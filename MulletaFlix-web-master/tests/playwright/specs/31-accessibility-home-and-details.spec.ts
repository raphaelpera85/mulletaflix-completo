import { expect, test } from '@playwright/test';
import { resolve } from 'node:path';
import { getAdminCredentials, loginWithManualForm } from '../support/admin-user.mjs';
import { navigateStage, openStage } from '../support/stage.mjs';
import { ensureMediaLibrariesReady, getVirtualFolderByLibrary, getFirstItemFromVirtualFolder, MOVIES_LIBRARY } from '../support/media-library.mjs';

test('Home and Item Details pages meet automated WCAG 2.2 AA accessibility checks at mobile (390px) and desktop (1280px) viewports', async ({ page }) => {
    const admin = getAdminCredentials();

    // Login with admin user
    try {
        await loginWithManualForm(page, admin.username, admin.password);
    } catch (error) {
        throw new Error(`Failed to login with admin user ${admin.username}. The server must be running and properly configured. Error: ${error.message}`);
    }

    // Prepare media library: ensure it exists and is ready for browsing
    await ensureMediaLibrariesReady(page);

    // Get the first item from the Movies library to test the Details page
    const moviesFolder = await getVirtualFolderByLibrary(page, MOVIES_LIBRARY);
    const testItem = await getFirstItemFromVirtualFolder(page, moviesFolder?.Id);

    if (!testItem) {
        throw new Error('Could not find a test item in the media library. Ensure media is properly configured and discovered.');
    }

    /**
     * Run axe-core accessibility scan.
     * Filters for WCAG 2.2 AA and related standards.
     * Fails if any critical or serious violations are found.
     */
    const scan = async (pageName: string, width: number) => {
        await page.setViewportSize({ width, height: 900 });
        await page.addScriptTag({ path: resolve('node_modules/axe-core/axe.min.js') });

        const violations = await page.evaluate(async () => {
            const axe = (window as Window & {
                axe?: {
                    run: (context: Document, options: object) => Promise<{
                        violations: Array<{
                            id: string;
                            impact: string;
                            help: string;
                            nodes: Array<{ target: string[]; failureSummary?: string }>;
                        }>;
                    }>;
                };
            }).axe;

            if (!axe) {
                throw new Error('axe-core did not load');
            }

            const results = await axe.run(document, {
                runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] }
            });

            // Filter for critical and serious violations only
            return results.violations
                .filter(violation => ['critical', 'serious'].includes(violation.impact))
                .map(({ id, impact, help, nodes }) => ({
                    id,
                    impact,
                    help,
                    nodes: nodes.map(node => ({ target: node.target, failureSummary: node.failureSummary }))
                }));
        });

        expect(violations, `axe-core violations on ${pageName} at ${width}px: ${JSON.stringify(violations, null, 2)}`).toEqual([]);
    };

    // Test Home page at both viewports
    await navigateStage(page, '/home');
    await expect(page.locator('#indexPage')).toBeVisible({ timeout: 30_000 });

    for (const width of [390, 1280]) {
        await scan('Home page', width);
    }

    // Test Item Details page at both viewports
    if (testItem?.id) {
        // Navigate to the item details page
        await openStage(page, `/details?id=${testItem.id}`);
        await expect(page.locator('#itemDetailsPage')).toBeVisible({ timeout: 30_000 });

        for (const width of [390, 1280]) {
            await scan('Item Details page', width);
        }
    }
});
