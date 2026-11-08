import { expect, test } from '@playwright/test';

import {
    getAdminCredentials,
    loginWithManualForm,
    logoutViaDashboard
} from '../support/admin-user.mjs';
import { ensureWizardCompleted } from '../support/wizard.mjs';
import { ensureMediaLibraries, getVirtualFolderByLibrary, MOVIES_LIBRARY } from '../support/media-library.mjs';

async function refreshLibrary(page) {
    await page.evaluate(async () => {
        const tasks = await window.ApiClient.getScheduledTasks();
        const refreshTask = tasks?.find(task => task?.Key === 'RefreshLibrary' && task?.Id);
        if (refreshTask?.Id) {
            await window.ApiClient.startScheduledTask(refreshTask.Id);
        }
    });
}

test.describe('21 - Media recognition diagnostic', () => {
    test.setTimeout(5 * 60 * 1000);

    test('materializes synthetic movie items after an explicit refresh', async ({ page }) => {
        const admin = getAdminCredentials();
        await ensureWizardCompleted(page, admin.username, admin.password);
        await loginWithManualForm(page, admin.username, admin.password);

        try {
            await ensureMediaLibraries(page, [ MOVIES_LIBRARY ]);
            await refreshLibrary(page);
            await page.waitForTimeout(60_000);

            const folder = await getVirtualFolderByLibrary(page, MOVIES_LIBRARY);
            const observation = await page.evaluate(async (parentId) => {
                const [ counts, result ] = await Promise.all([
                    window.ApiClient.getItemCounts(),
                    window.ApiClient.getItems(window.ApiClient.getCurrentUserId(), {
                        ParentId: parentId,
                        IncludeItemTypes: 'Movie',
                        Recursive: true,
                        Limit: 20,
                        Fields: 'Path,MediaType,Name'
                    })
                ]);

                return {
                    counts,
                    items: (result?.Items || []).map(item => ({
                        id: item.Id,
                        name: item.Name,
                        path: item.Path,
                        mediaType: item.MediaType,
                        type: item.Type
                    }))
                };
            }, folder?.ItemId || folder?.Id);

            console.log(`MEDIA_RECOGNITION_DIAGNOSTIC ${JSON.stringify(observation)}`);
            expect(observation.counts.MovieCount).toBeGreaterThan(0);
            expect(observation.items.length).toBeGreaterThanOrEqual(2);
            expect(observation.items.every(item => item.type === 'Movie' && item.mediaType === 'Video')).toBe(true);
        } finally {
            await logoutViaDashboard(page);
        }
    });
});
