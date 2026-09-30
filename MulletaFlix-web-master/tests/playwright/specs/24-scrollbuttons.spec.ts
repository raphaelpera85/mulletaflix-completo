import { expect, test } from '@playwright/test';

test('search-style scroller reveals and updates navigation after cards mount', async ({ page }) => {
    await page.addInitScript(() => {
        (window as Window & { __WEBPACK_SERVE__?: boolean }).__WEBPACK_SERVE__ = false;
    });
    await page.setViewportSize({ width: 1280, height: 800 });
    const baseUrl = String(test.info().project.use.baseURL);
    const fixtureUrl = new URL('/__playwright_scrollbuttons_fixture__', baseUrl).toString();
    await page.route(fixtureUrl, route => route.fulfill({
        status: 200,
        contentType: 'text/html',
        body: '<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"></head><body></body></html>'
    }));
    await page.goto(fixtureUrl);
    await page.addScriptTag({
        type: 'module',
        url: new URL('/elements/emby-scroller/emby-scroller.ts', baseUrl).toString()
    });
    await page.waitForFunction(() => typeof (document as Document & { registerElement?: unknown }).registerElement === 'function');

    await page.evaluate(() => {
        const createLegacyElement = document.createElement as unknown as (tagName: string, extension: string) => HTMLDivElement;
        const scroller = createLegacyElement('div', 'emby-scroller');
        scroller.setAttribute('data-horizontal', 'true');
        scroller.setAttribute('data-scrollbuttons', 'true');
        scroller.style.width = '720px';
        scroller.style.height = '180px';
        scroller.style.overflow = 'hidden';

        const slider = document.createElement('div');
        slider.className = 'itemsContainer scrollSlider';
        slider.style.display = 'flex';
        slider.style.width = 'max-content';

        const appendCard = (index: number) => {
            const card = document.createElement('div');
            card.textContent = `Card ${index}`;
            card.style.cssText = 'flex: 0 0 180px; width: 180px; height: 120px; margin-right: 12px; background: #333;';
            slider.append(card);
        };

        // Start with content that fits so the initial arrow measurement hides
        // controls. SearchResultsRow populates its cards after the scroller mounts.
        for (let index = 0; index < 2; index++) appendCard(index);
        scroller.append(slider);
        document.body.append(scroller);

        window.setTimeout(() => {
            for (let index = 2; index < 12; index++) appendCard(index);
        }, 100);
    });

    const scroller = page.locator('.emby-scroller');
    const previous = page.locator('.emby-scrollbuttons-button[data-direction="left"]');
    const next = page.locator('.emby-scrollbuttons-button[data-direction="right"]');

    await expect(scroller).toBeVisible();
    await expect(previous).toBeVisible({ timeout: 10_000 });
    await expect(next).toBeVisible();
    await expect(previous).toBeDisabled();
    await expect(next).toBeEnabled();
    // Shrinking the viewport changes how many cards fit; the controls must
    // re-evaluate their end state instead of retaining the old measurement.
    await page.setViewportSize({ width: 520, height: 800 });
    await expect(previous).toBeVisible();
    await expect(next).toBeVisible();
    await expect(next).toBeEnabled();
});
