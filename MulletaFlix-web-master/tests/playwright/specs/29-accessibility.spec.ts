import { expect, test } from '@playwright/test';
import { resolve } from 'node:path';

test('My Media Requests meets automated WCAG 2.1 AA checks on mobile and desktop', async ({ page }) => {
    await page.route('**/hooks/api/useMediaRequests.ts*', route => route.fulfill({
        contentType: 'application/javascript',
        body: `export const useMyClassifiedMediaRequests = () => ({
            pending: [{ Id: 1, Name: 'Solicitação de mídia: Um título solicitado', Overview: 'Séries · 2026' }],
            included: [{ Id: 2, Name: 'Solicitação de mídia: Atomic', Overview: 'Incluído' }],
            priorityRequestIds: new Set([1]), isPending: false, isError: false,
            hasNextPage: true, isFetchingNextPage: false, isFetchNextPageError: false,
            fetchNextPage: async () => {}, refetch: async () => {}
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
            Retry: 'Tentar novamente', ShowMore: 'Mostrar mais'
        }[key] || key) };`
    }));

    const modulePath = resolve('tests/playwright/fixtures/myrequests.tsx').replaceAll('\\', '/');
    await page.route('**/__accessibility_requests_fixture__', route => route.fulfill({
        contentType: 'text/html',
        body: `<html lang="pt-BR"><head><meta charset="utf-8"><title>Minhas solicitações</title><style>
            html,body{margin:0;min-height:100%;background:#121212;color:#fff}
        </style></head><body><div id="root"></div><script type="module" src="/@fs/${modulePath}"></script></body></html>`
    }));
    await page.goto('/__accessibility_requests_fixture__');
    await expect(page.getByRole('heading', { name: 'Minhas solicitações' })).toBeVisible();
    await page.addScriptTag({ path: resolve('node_modules/axe-core/axe.min.js') });

    for (const width of [390, 1280]) {
        await page.setViewportSize({ width, height: 900 });
        const violations = await page.evaluate(async () => {
            const axe = (window as Window & { axe?: { run: (context: Document, options: object) => Promise<{ violations: Array<{ id: string; impact: string; help: string; nodes: Array<{ target: string[]; failureSummary?: string }> }> }> } }).axe;
            if (!axe) throw new Error('axe-core did not load');
            const results = await axe.run(document, { runOnly: { type: 'tag', values: [ 'wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa' ] } });
            return results.violations.map(({ id, impact, help, nodes }) => ({
                id,
                impact,
                help,
                nodes: nodes.map(node => ({ target: node.target, failureSummary: node.failureSummary }))
            }));
        });
        expect(violations, `axe-core violations at ${width}px: ${JSON.stringify(violations, null, 2)}`).toEqual([]);
    }
});
