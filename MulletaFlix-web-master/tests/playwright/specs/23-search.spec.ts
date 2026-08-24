import { expect, test } from '@playwright/test';

import {
    getAdminCredentials,
    loginWithManualForm,
    logoutViaDashboard
} from '../support/admin-user.mjs';
import { navigateStage } from '../support/stage.mjs';
import { ensureWizardCompleted } from '../support/wizard.mjs';

const SEARCH_QUERY = 'mflx-search-no-match';
const SCOPED_SEARCH_ROUTE = `/search?query=${SEARCH_QUERY}&collectionType=movies`;

test.describe.serial('23 - Unified search', () => {
    test('keeps the query while clearing collection scope and retrying globally', async ({ page }) => {
        const admin = getAdminCredentials();
        await ensureWizardCompleted(page, admin.username, admin.password);
        await loginWithManualForm(page, admin.username, admin.password);

        await navigateStage(page, SCOPED_SEARCH_ROUTE);

        const searchPage = page.locator('#searchPage');
        await expect(searchPage).toBeVisible();
        await expect(searchPage).toHaveAttribute('data-title', 'Search - Movies');
        await expect(searchPage.getByText('Scoped to Movies').first()).toBeVisible();

        const clearScope = searchPage.getByRole('link', { name: 'Clear scope' });
        await expect(clearScope).toBeVisible();
        await clearScope.click();
        await expect(page).toHaveURL(new RegExp(`#\\/search\\?query=${SEARCH_QUERY}$`));
        await expect(searchPage).toHaveAttribute('data-title', 'Search');
        await expect(searchPage.getByText('Scoped to Movies')).toHaveCount(0);

        await navigateStage(page, SCOPED_SEARCH_ROUTE);
        const retryGlobal = searchPage.getByRole('link', { name: /Retry.*Global Search/i });
        await expect(retryGlobal).toBeVisible({ timeout: 30_000 });
        await retryGlobal.click();
        await expect(page).toHaveURL(new RegExp(`#\\/search\\?query=${SEARCH_QUERY}$`));
        await expect(searchPage).toHaveAttribute('data-title', 'Search');

        await logoutViaDashboard(page);
    });
});
