import { expect, test } from '@playwright/test';

test.describe('Bootstrap', () => {
    test('removes the splash screen after loading the web client', async ({ page }) => {
        await page.goto('/web/', { waitUntil: 'domcontentloaded' });

        // A module-evaluation error or a bootstrap promise stuck forever leaves
        // this element in place, which is the user-visible failure we need to
        // catch in the release gate.
        await expect(page.locator('.splashLogo')).toBeHidden({ timeout: 30_000 });
    });
});
