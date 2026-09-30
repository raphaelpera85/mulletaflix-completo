import { expect, test } from '@playwright/test';
import { resolve } from 'node:path';

for (const width of [390, 1280]) {
    test(`backup coverage distinguishes destinations and failures at ${width}px`, async ({ page }, testInfo) => {
        await page.setViewportSize({ width, height: 800 });
        const modulePath = resolve('tests/playwright/fixtures/backupCoverage.tsx').replaceAll('\\', '/');
        await page.route('**/__backup_coverage__', route => route.fulfill({
            contentType: 'text/html',
            body: `<html lang="pt-BR"><body style="background:#111;color:white;margin:16px"><div id="root"></div><script type="module" src="/@fs/${modulePath}"></script></body></html>`
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
        await page.screenshot({ path: testInfo.outputPath('backup-coverage.png'), fullPage: true });
    });
}
