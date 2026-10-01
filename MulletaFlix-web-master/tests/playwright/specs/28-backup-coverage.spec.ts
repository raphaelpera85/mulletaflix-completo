import { expect, test } from '@playwright/test';
import { resolve } from 'node:path';

const expectAxeClean = async (page: import('@playwright/test').Page, width: number) => {
    await page.addScriptTag({ path: resolve('node_modules/axe-core/axe.min.js') });
    const violations = await page.evaluate(async () => {
        const axe = (window as Window & { axe?: { run: (context: Document, options: object) => Promise<{ violations: Array<{ id: string; impact: string; help: string; nodes: Array<{ target: string[]; failureSummary?: string }> }> }> } }).axe;
        if (!axe) throw new Error('axe-core did not load');
        const results = await axe.run(document, { runOnly: { type: 'tag', values: [ 'wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa' ] } });
        return results.violations.map(({ id, impact, help, nodes }) => ({
            id,
            impact,
            help,
            nodes: nodes.map(node => ({ target: node.target, failureSummary: node.failureSummary }))
        }));
    });
    expect(violations, `axe-core violations at ${width}px: ${JSON.stringify(violations, null, 2)}`).toEqual([]);
};

for (const width of [390, 1280]) {
    test(`backup coverage distinguishes destinations and failures at ${width}px`, async ({ page }, testInfo) => {
        await page.setViewportSize({ width, height: 800 });
        const modulePath = resolve('tests/playwright/fixtures/backupCoverage.tsx').replaceAll('\\', '/');
        await page.route('**/__backup_coverage__**', route => route.fulfill({
            contentType: 'text/html',
            body: `<html lang="pt-BR"><head><title>Cobertura dos backups</title></head><body style="background:#111;color:white;margin:16px"><div id="root"></div><script type="module" src="/@fs/${modulePath}"></script></body></html>`
        }));
        await page.goto('/__backup_coverage__');
        const local = page.getByRole('region', { name: 'ZIP local do servidor' });
        const catalog = page.getByRole('region', { name: 'Catálogo Nebula no Supabase' });
        const users = page.getByRole('region', { name: 'Usuários do aplicativo no Supabase' });
        await expect(local).toBeVisible();
        await expect(catalog).toBeVisible();
        await expect(users).toBeVisible();
        await expect(local).toContainText('Banco relacional e usuários: não selecionado');
        await expect(catalog.getByText('Falha', { exact: true })).toBeVisible();
        await expect(users).toContainText('Usuários processados: 8');
        const retry = page.getByRole('button', { name: 'Tentar novamente' });
        await retry.focus();
        await page.keyboard.press('Enter');
        await expect.poll(() => page.evaluate(() => (window as Window & { backupRetryCalls?: number }).backupRetryCalls)).toBe(1);
        expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);

        await expectAxeClean(page, width);

        for (const state of [ 'loading', 'empty' ]) {
            await page.goto(`/__backup_coverage__?state=${state}`);
            if (state === 'loading') {
                await expect(page.getByRole('status')).toContainText('Carregando destinos remotos');
            } else {
                await expect(page.getByRole('region', { name: 'ZIP local do servidor' })).toContainText('Nenhum ZIP disponível');
            }
            expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
            await expectAxeClean(page, width);
        }
        await page.screenshot({ path: testInfo.outputPath('backup-coverage.png'), fullPage: true });
    });
}
