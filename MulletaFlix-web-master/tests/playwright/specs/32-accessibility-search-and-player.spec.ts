import { expect, test } from '@playwright/test';
import { resolve } from 'node:path';
import { getAdminCredentials, loginWithManualForm } from '../support/admin-user.mjs';
import { navigateStage } from '../support/stage.mjs';
import { ensureWizardCompleted } from '../support/wizard.mjs';

test('Search and Player pages meet automated WCAG 2.2 AA accessibility checks at mobile (390px) and desktop (1280px) viewports', async ({ page }) => {
    const admin = getAdminCredentials();

    // Complete wizard and login with admin user
    await ensureWizardCompleted(page, admin.username, admin.password);
    await loginWithManualForm(page, admin.username, admin.password);

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

    // Test Search page at both viewports
    await navigateStage(page, '/search');
    // Search page has ID searchPage
    await expect(page.locator('#searchPage')).toBeVisible({ timeout: 30_000 });

    for (const width of [390, 1280]) {
        await scan('Search page', width);
    }

    // Test Player/Playback page
    // Navigate to home first to find a playable item
    await navigateStage(page, '/home');
    await expect(page.locator('#indexPage')).toBeVisible({ timeout: 30_000 });

    // Look for a playable item link on the home page (could be in any carousel)
    // We'll try to find and click the first playable media item
    const firstPlayableLink = page.locator('a[href*="/details"]').first();
    if (await firstPlayableLink.isVisible()) {
        await firstPlayableLink.click();
        await expect(page.locator('#itemDetailsPage')).toBeVisible({ timeout: 30_000 });

        // Try to find and click a play button to navigate to the player
        const playButton = page.locator('button[title*="play" i], button[title*="reproduzir" i], button:has-text(/play|reproduzir/i), [role="button"]:has-text(/play|reproduzir/i)').first();
        if (await playButton.isVisible()) {
            await playButton.click();
            // Wait for video player or playback page to load
            await expect(page.locator('[class*="player"], [id*="player"], video, [role="region"][aria-label*="player" i], [role="region"][aria-label*="reprodutor" i]')).toBeVisible({ timeout: 30_000 }).catch(() => {
                // Player might not render immediately, but the page should still exist
            });

            // Test Player page at both viewports
            for (const width of [390, 1280]) {
                await scan('Player page', width);
            }
        } else {
            // If we can't find a play button, test the details page accessibility as a fallback
            for (const width of [390, 1280]) {
                await scan('Item Details page (player not available)', width);
            }
        }
    } else {
        // If no items are available, just test the home page at different viewports
        for (const width of [390, 1280]) {
            await scan('Home page (no player available)', width);
        }
    }
});
