import { expect, test } from '@playwright/test';
import { resolve } from 'node:path';

test('real search results render cards with visible navigation controls', async ({ page }) => {
    await page.addInitScript(() => {
        (window as Window & { __WEBPACK_SERVE__?: boolean }).__WEBPACK_SERVE__ = false;
    });
    await page.setViewportSize({ width: 1280, height: 800 });
    const baseUrl = String(test.info().project.use.baseURL);
    await page.route('**/apps/stable/features/search/api/useSearchItems.ts*', route => route.fulfill({
        contentType: 'application/javascript',
        body: `export const useSearchItems = () => ({
            data: [{ title: 'Movies', items: Array.from({ length: 18 }, (_, index) => ({
                Id: 'search-card-' + index, Name: 'Search result ' + index, Type: 'Movie'
            })) }], isPending: false, isError: false, refetch: async () => {}
        });`
    }));
    await page.route('**/components/cardbuilder/cardBuilder.ts*', route => route.fulfill({
        contentType: 'application/javascript',
        body: `export function buildCards(items, { itemsContainer }) {
            itemsContainer.style.cssText = 'display:flex;width:max-content;gap:12px';
            itemsContainer.replaceChildren(...items.map(item => {
                const card = document.createElement('button');
                card.className = 'card';
                card.dataset.id = item.Id;
                card.textContent = item.Name;
                card.style.cssText = 'flex:0 0 180px;width:180px;height:120px';
                return card;
            }));
        }
        export default { buildCards };`
    }));
    await page.route('**/lib/globalize/index.ts*', route => route.fulfill({
        contentType: 'application/javascript',
        body: `const isRtl = () => document.documentElement.dir === 'rtl';
        export default {
            translate: key => ({ Movies: 'Movies', Previous: 'Previous', Next: 'Next' }[key] || key),
            getIsRTL: isRtl,
            getIsElementRTL: isRtl
        };`
    }));
    const modulePath = resolve('tests/playwright/fixtures/searchresults.tsx').replaceAll('\\', '/');
    const fixtureUrl = new URL('/__playwright_searchresults_fixture__', baseUrl).toString();
    await page.route('**/__playwright_searchresults_fixture__*', route => {
        const direction = new URL(route.request().url()).searchParams.get('dir') === 'rtl' ? 'rtl' : 'ltr';
        return route.fulfill({
            status: 200,
            contentType: 'text/html',
            body: `<!doctype html><html lang="en" dir="${direction}"><head><meta charset="utf-8"><style>
            body{margin:16px;background:#111;color:white}.searchResults{width:calc(100vw - 32px)}
            .verticalSection{height:220px}.emby-scroller{position:relative;width:100%;height:180px;overflow:hidden}
            .emby-scrollbuttons{position:absolute;top:0;right:0;display:flex;gap:4px;z-index:2}
        </style></head><body><div id="root"></div><script type="module" src="/@fs/${modulePath}"></script></body></html>`
        });
    });
    const pageErrors: string[] = [];
    page.on('pageerror', error => pageErrors.push(error.message));
    await page.goto(fixtureUrl);

    const cards = page.locator('.searchResults .itemsContainer .card');
    const previous = page.locator('.searchResults .emby-scrollbuttons-button[data-direction="left"]');
    const next = page.locator('.searchResults .emby-scrollbuttons-button[data-direction="right"]');
    await expect(page.getByRole('heading', { name: 'Movies' })).toBeVisible();
    await expect(cards).toHaveCount(18);
    await expect(page.locator('.searchResults [is="emby-scroller"][class~="padded-top-focusscale"]')).toHaveCount(1);
    await expect(page.locator('.searchResults .itemsContainer.scrollSlider')).toHaveCount(1);
    await expect(previous).toBeVisible({ timeout: 10_000 });
    await expect(previous).toBeDisabled();
    await expect(next).toBeEnabled();
    await expect(next).toHaveAttribute('aria-disabled', 'false');

    const scroller = page.locator('.searchResults [is="emby-scroller"]');
    const getScrollPosition = () => scroller.evaluate(element => (element as HTMLElement & {
        getScrollPosition: () => number;
    }).getScrollPosition());

    await page.keyboard.press('Tab');
    await expect(next).toBeFocused();
    expect(await next.evaluate(button => button.matches(':focus-visible'))).toBe(true);
    await page.keyboard.press('Enter');
    await expect.poll(getScrollPosition).toBeGreaterThan(0);
    await expect(previous).toBeEnabled();

    for (let attempt = 0; attempt < 20 && await next.isEnabled(); attempt++) {
        const previousPosition = await getScrollPosition();
        await page.keyboard.press('Enter');
        await expect.poll(getScrollPosition).not.toBe(previousPosition);
    }
    await expect(next).toBeDisabled();
    await expect(next).toHaveAttribute('aria-disabled', 'true');

    await previous.focus();
    for (let attempt = 0; attempt < 20 && await previous.isEnabled(); attempt++) {
        const previousPosition = await getScrollPosition();
        await page.keyboard.press('Enter');
        await expect.poll(getScrollPosition).not.toBe(previousPosition);
    }
    await expect.poll(getScrollPosition).toBe(0);
    await expect(previous).toBeDisabled();
    await expect(previous).toHaveAttribute('aria-disabled', 'true');

    // The real search row must remain navigable after a narrow viewport resize.
    await page.setViewportSize({ width: 390, height: 800 });
    await expect(next).toBeVisible();
    await expect(next).toBeEnabled();
    await next.focus();
    await page.keyboard.press('Enter');
    await expect.poll(getScrollPosition).toBeGreaterThan(0);
    await expect(previous).toBeEnabled();
    await previous.focus();
    await page.keyboard.press('Enter');
    await expect.poll(getScrollPosition).toBe(0);

    // Reload with RTL set before SearchResults initializes its scroller.
    const rtlUrl = new URL(fixtureUrl);
    rtlUrl.searchParams.set('dir', 'rtl');
    await page.goto(rtlUrl.toString());
    const rtlScroller = page.locator('.searchResults [is="emby-scroller"]');
    const rtlPrevious = page.locator('.searchResults .emby-scrollbuttons-button[data-direction="left"]');
    const rtlNext = page.locator('.searchResults .emby-scrollbuttons-button[data-direction="right"]');
    const getRtlScrollPosition = () => rtlScroller.evaluate(element => (element as HTMLElement & {
        getScrollPosition: () => number;
    }).getScrollPosition());
    await expect(page.locator('.searchResults .itemsContainer .card')).toHaveCount(18);
    await expect(rtlPrevious).toBeVisible({ timeout: 10_000 });
    await expect(rtlPrevious).toBeDisabled();
    await expect(rtlNext).toBeEnabled();
    await rtlNext.focus();
    await page.keyboard.press('Enter');
    await expect.poll(getRtlScrollPosition).toBeLessThan(0);
    await expect(rtlPrevious).toBeEnabled();

    for (let attempt = 0; attempt < 20 && await rtlNext.isEnabled(); attempt++) {
        const previousPosition = await getRtlScrollPosition();
        await page.keyboard.press('Enter');
        await expect.poll(getRtlScrollPosition).not.toBe(previousPosition);
    }
    await expect(rtlNext).toBeDisabled();
    await rtlPrevious.focus();
    for (let attempt = 0; attempt < 20 && await rtlPrevious.isEnabled(); attempt++) {
        const previousPosition = await getRtlScrollPosition();
        await page.keyboard.press('Enter');
        await expect.poll(getRtlScrollPosition).not.toBe(previousPosition);
    }
    await expect.poll(async () => Math.abs(await getRtlScrollPosition())).toBe(0);
    await expect(rtlPrevious).toBeDisabled();
    expect(pageErrors).toEqual([]);
});

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
        scroller.style.width = 'min(720px, calc(100vw - 32px))';
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
    await expect(previous).toHaveAttribute('aria-disabled', 'true');
    await expect(next).toBeEnabled();
    await expect(next).toHaveAttribute('aria-disabled', 'false');
    // A narrow viewport must remeasure the frame and keep both arrows usable.
    await page.setViewportSize({ width: 390, height: 800 });
    await expect(previous).toBeVisible();
    await expect(next).toBeVisible();
    await expect(next).toBeEnabled();

    const getScrollPosition = () => scroller.evaluate(element => (element as HTMLElement & {
        getScrollPosition: () => number;
    }).getScrollPosition());

    await page.keyboard.press('Tab');
    await expect(next).toBeFocused();
    expect(await next.evaluate(button => button.matches(':focus-visible'))).toBe(true);
    await page.keyboard.press('Enter');
    await expect.poll(getScrollPosition).toBeGreaterThan(0);
    await expect(previous).toBeEnabled();

    for (let attempt = 0; attempt < 10 && await next.isEnabled(); attempt++) {
        const previousPosition = await getScrollPosition();
        await page.keyboard.press('Enter');
        await page.waitForTimeout(300);
        if (await getScrollPosition() === previousPosition) break;
    }
    const endState = await scroller.evaluate(element => {
        const frame = element as HTMLElement & { scroller?: { _pos?: { end: number }; getScrollSize: () => number } };
        const slider = frame.firstElementChild as HTMLElement;
        return { position: (element as HTMLElement & { getScrollPosition: () => number }).getScrollPosition(), end: frame.scroller?._pos?.end, frameWidth: frame.clientWidth, sliderWidth: slider.scrollWidth };
    });
    await expect(next, `carousel end state: ${JSON.stringify(endState)}`).toBeDisabled();
    await expect(next).toHaveAttribute('aria-disabled', 'true');

    await previous.focus();
    for (let attempt = 0; attempt < 10 && await previous.isEnabled(); attempt++) {
        const previousPosition = await getScrollPosition();
        await page.keyboard.press('Enter');
        await expect.poll(getScrollPosition).not.toBe(previousPosition);
    }
    await expect.poll(getScrollPosition).toBe(0);
    await expect(previous).toBeDisabled();
    await expect(previous).toHaveAttribute('aria-disabled', 'true');
});
