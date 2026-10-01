import { expect, test } from '@playwright/test';
import { resolve } from 'node:path';

for (const width of [390, 1280]) {
    test(`request grids and retry remain usable at ${width}px`, async ({ page }) => {
        await page.setViewportSize({ width, height: 800 });
        await page.route('**/hooks/api/useMediaRequests.ts*', route => route.fulfill({
            contentType: 'application/javascript',
            body: `export const useMyClassifiedMediaRequests = () => ({
                pending: [{ Id: 1, Name: 'Solicitação de mídia: Um título pendente muito longo para verificar a quebra de texto na tela pequena', Overview: 'Séries · 2026' }],
                included: [{ Id: 2, Name: 'Solicitação de mídia: Atomic', Overview: 'Incluído' }],
                priorityRequestIds: new Set([1]), isPending: false, isError: false,
                hasNextPage: true, isFetchingNextPage: false, isFetchNextPageError: true,
                fetchNextPage: async () => { window.__loadMoreCalls = (window.__loadMoreCalls || 0) + 1; },
                refetch: async () => {}
            });`
        }));
        await page.route('**/components/Page.tsx*', route => route.fulfill({
            contentType: 'application/javascript',
            body: 'export default function Page({ children }) { return children; }'
        }));
        await page.route('**/lib/globalize/index.ts*', route => route.fulfill({
            contentType: 'application/javascript',
            body: `export default { translate: key => ({
                MediaRequestsPendingTitle: 'Fila de solicitações', MediaRequestsIncludedTitle: 'Títulos incluídos',
                MyMediaRequestsTitle: 'Minhas solicitações', MyMediaRequestPriority: 'Prioridade solicitada',
                MyMediaRequestPriorityDescription: 'Registrada na prioridade do Nebula',
                Retry: 'Tentar novamente', ErrorDefault: 'Não foi possível carregar mais solicitações'
            }[key] || key) };`
        }));
        const modulePath = resolve('tests/playwright/fixtures/myrequests.tsx').replaceAll('\\', '/');
        await page.route('**/__requests_fixture__', route => route.fulfill({
            contentType: 'text/html',
            body: `<html lang="pt-BR"><body style="background:#111;color:white;margin:16px"><div id="root"></div><script type="module" src="/@fs/${modulePath}"></script></body></html>`
        }));
        await page.goto('/__requests_fixture__');

        const pending = page.getByRole('region', { name: 'Fila de solicitações' });
        const included = page.getByRole('region', { name: 'Títulos incluídos' });
        await expect(pending).toBeVisible();
        await expect(included).toBeVisible();
        await expect(pending.getByText('Prioridade solicitada', { exact: true })).toBeVisible();
        await expect(included.getByText('Atomic', { exact: true })).toBeVisible();
        await expect(page.getByRole('alert')).toContainText('Não foi possível carregar mais solicitações');
        const retry = page.getByRole('button', { name: 'Tentar novamente' });
        await retry.focus();
        await page.keyboard.press('Enter');
        await expect.poll(() => page.evaluate(() => (window as Window & { __loadMoreCalls?: number }).__loadMoreCalls)).toBe(1);
        expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
        const title = await pending.locator('.MuiListItemText-root').boundingBox();
        const chips = await pending.locator('.MuiListItem-root').first().locator(':scope > .MuiStack-root').first().boundingBox();
        expect(title).not.toBeNull();
        expect(chips).not.toBeNull();
        expect(chips!.y).toBeGreaterThanOrEqual(title!.y + title!.height);
        await page.screenshot({ path: test.info().outputPath('requests.png'), fullPage: true });
    });
}
