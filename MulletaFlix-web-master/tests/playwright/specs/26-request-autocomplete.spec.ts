import { expect, test } from '@playwright/test';

test('autocomplete selects title/category/year by keyboard and submits the chosen identity', async ({ page }) => {
    await page.route('**/components/dialogHelper/dialogHelper.ts*', route => route.fulfill({
        contentType: 'application/javascript',
        body: `export default {
            createDialog: () => { const dialog = document.createElement('div'); document.body.append(dialog); return dialog; },
            open: async () => {},
            close: dialog => { dialog.dispatchEvent(new Event('close')); dialog.hidden = true; }
        };`
    }));
    await page.route('**/components/toast/toast.ts*', route => route.fulfill({ contentType: 'application/javascript', body: 'export default () => {};' }));
    await page.route('**/lib/globalize/index.ts*', route => route.fulfill({ contentType: 'application/javascript', body: 'export default { translate: key => key };' }));
    let errorOnce = true;
    await page.route('**/test-api/UserFeedback/MediaSuggestions?*', route => {
        const query = new URL(route.request().url()).searchParams.get('query');
        if (query === 'NoMatch') return route.fulfill({ json: [] });
        if (query === 'ErrorOnce' && errorOnce) {
            errorOnce = false;
            return route.fulfill({ status: 503, contentType: 'text/plain', body: 'Catalog unavailable' });
        }
        return route.fulfill({ json: query === 'At' ?
            [{ Title: 'Atomic', MediaType: 'Series', Year: 2024 }] :
            [{ Title: "Let's Play", MediaType: 'Animation' }] });
    });
    await page.route('**/test-api/UserFeedback/MediaSuggestions/Status', route => route.fulfill({ json: {
        State: 'Ready', IsIndexing: false, FailedRootCount: 0
    } }));
    let submitted: unknown;
    await page.route('**/test-api/UserFeedback/MediaRequests', route => {
        submitted = route.request().postDataJSON();
        return route.fulfill({ status: 204 });
    });
    await page.route('**/__autocomplete_fixture__', route => route.fulfill({ contentType: 'text/html', body: '<html lang="pt-BR"><body></body></html>' }));
    await page.goto('/__autocomplete_fixture__');
    await page.addScriptTag({ type: 'module', content: `
        import { showMediaRequestDialog } from '/components/userFeedback/userFeedback.ts';
        showMediaRequestDialog({
            getUrl: url => '/test-api/' + url,
            getJSON: url => fetch(url).then(response => response.json()),
            ajax: options => fetch(options.url, { method: options.type, body: options.data, headers: { 'Content-Type': options.contentType } })
        });
    ` });

    const title = page.locator('input[name="title"]');
    const year = page.locator('input[name="year"]');
    const category = page.locator('select[name="mediaType"]');
    await title.fill('At');
    await expect(page.getByRole('option', { name: 'Atomic — Series · 2024' })).toBeVisible();
    await title.press('ArrowDown');
    await title.press('Enter');
    await expect(title).toHaveValue('Atomic');
    await expect(year).toHaveValue('2024');
    await expect(title).toHaveAttribute('aria-expanded', 'false');

    await title.fill('Le');
    await expect(page.getByRole('option', { name: "Let's Play — Animation" })).toBeVisible();
    await title.press('ArrowDown');
    await title.press('Enter');
    await expect(title).toHaveValue("Let's Play");
    await expect(category).toHaveValue('Animation');
    await expect(year).toHaveValue('');

    await title.fill('NoMatch');
    await expect(page.getByRole('status')).toHaveText('MediaRequestSuggestionsEmpty');
    await title.fill('ErrorOnce');
    await expect(page.getByRole('status')).toContainText('MediaRequestSuggestionsFailed');
    await title.press('Tab');
    const retryButton = page.getByRole('button', { name: 'Retry', exact: true });
    await expect(retryButton).toBeFocused();
    await page.keyboard.press('Enter');
    const retrySuggestion = page.getByRole('option', { name: "Let's Play — Animation" });
    await expect(retrySuggestion).toBeVisible();
    await title.press('ArrowDown');
    await title.press('Enter');

    await page.getByRole('button', { name: 'ButtonSend', exact: true }).click();
    await expect.poll(() => submitted).toEqual({ Title: "Let's Play", MediaType: 'Animation' });
});

test('autocomplete debounces input and ignores suggestions returned for an older query', async ({ page }) => {
    await page.route('**/components/dialogHelper/dialogHelper.ts*', route => route.fulfill({
        contentType: 'application/javascript',
        body: `export default {
            createDialog: () => { const dialog = document.createElement('div'); document.body.append(dialog); return dialog; },
            open: async () => {},
            close: dialog => { dialog.dispatchEvent(new Event('close')); dialog.hidden = true; }
        }`
    }));
    await page.route('**/components/toast/toast.ts*', route => route.fulfill({ contentType: 'application/javascript', body: 'export default () => {};' }));
    await page.route('**/lib/globalize/index.ts*', route => route.fulfill({ contentType: 'application/javascript', body: 'export default { translate: key => key };' }));

    let olderQueryCalls = 0;
    let olderQueryFulfilled = false;
    let releaseOlderQuery: (() => void) | undefined;
    const olderQueryGate = new Promise<void>(resolve => {
        releaseOlderQuery = resolve;
    });
    await page.route('**/test-api/UserFeedback/MediaSuggestions?*', async route => {
        const query = new URL(route.request().url()).searchParams.get('query');
        if (query === 'Older') {
            olderQueryCalls++;
            await olderQueryGate;
            await route.fulfill({ json: [{ Title: 'Older Result', MediaType: 'Series' }] });
            olderQueryFulfilled = true;
            return;
        }

        await route.fulfill({ json: [{ Title: 'Current Result', MediaType: 'Series' }] });
    });
    await page.route('**/__autocomplete_race_fixture__', route => route.fulfill({
        contentType: 'text/html',
        body: '<html lang="pt-BR"><body></body></html>'
    }));
    await page.goto('/__autocomplete_race_fixture__');
    await page.addScriptTag({ type: 'module', content: `
        import { showMediaRequestDialog } from '/components/userFeedback/userFeedback.ts';
        showMediaRequestDialog({
            getUrl: url => '/test-api/' + url,
            getJSON: url => fetch(url).then(response => response.json()),
            ajax: options => fetch(options.url, { method: options.type, body: options.data, headers: { 'Content-Type': options.contentType } })
        });
    ` });

    const title = page.locator('input[name="title"]');

    // The first keystrokes are replaced before the debounce expires; only the final query is sent.
    await title.fill('Ol');
    await title.fill('Current');
    await expect(page.getByRole('option', { name: 'Current Result — Series' })).toBeVisible();
    expect(olderQueryCalls).toBe(0);

    // A slow response for an older query must not replace the currently displayed results.
    await title.fill('Older');
    await expect.poll(() => olderQueryCalls).toBe(1);
    await title.fill('Current');
    const currentResult = page.getByRole('option', { name: 'Current Result — Series' });
    await expect(currentResult).toBeVisible();

    releaseOlderQuery?.();
    await expect.poll(() => olderQueryFulfilled).toBe(true);
    await expect(currentResult).toBeVisible();
    await expect(page.getByRole('option', { name: 'Older Result — Series' })).toHaveCount(0);
});
