import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { expect, test } from '@playwright/test';

const resourceRoot = resolve(process.cwd(), '../MulletaFlix-master/Jellyfin.Api/Web/NebulaFTP');
const html = readFileSync(resolve(resourceRoot, 'configPage.html'), 'utf8');
const controller = readFileSync(resolve(resourceRoot, 'configPage.js'), 'utf8');

for (const width of [390, 1280]) {
    test(`authenticated Nebula controller activates all tabs and sends startup actions at ${width}px`, async ({ page }, testInfo) => {
        await page.setViewportSize({ width, height: 800 });
        const errors: string[] = [];
        page.on('pageerror', error => errors.push(error.message));
        await page.route('**/utils/dashboard.ts*', route => route.fulfill({
            contentType: 'application/javascript',
            body: 'export default { getPluginUrl: name => "configurationpage?name=" + encodeURIComponent(name) };'
        }));
        let authenticatedImport = false;
        await page.route('**/web/configurationpage?*', route => {
            authenticatedImport = new URL(route.request().url()).searchParams.get('api_key') === 'test-token';
            return route.fulfill({ status: authenticatedImport ? 200 : 401, contentType: 'application/javascript', body: controller });
        });
        const actions: unknown[] = [];
        await page.route('**/NebulaFtp/**', route => {
            const path = new URL(route.request().url()).pathname;
            if (path.endsWith('/StartEnvio')) {
                actions.push(route.request().postDataJSON());
                return route.fulfill({ json: true });
            }
            if (path.endsWith('/Config')) return route.fulfill({ json: { Enabled: true, MonitorPaths: [], StagePaths: [] } });
            if (path.endsWith('/Bots')) return route.fulfill({ json: [] });
            if (path.includes('/Logs')) return route.fulfill({ json: { ServerLogs: [], DownloaderLogs: [] } });
            return route.fulfill({ json: { IsEnvioRunning: false, IsDownloaderRunning: false, GlobalStatusText: 'Envio: Parado | Download: Parado' } });
        });
        await page.route('**/__nebula_fixture__', route => route.fulfill({ contentType: 'text/html', body: '<html lang="pt-BR"><head><style>body{color:#cdd6f4;background:#111}input,button{color:inherit}label{color:inherit}</style></head><body><div class="mainAnimatedPages"></div></body></html>' }));
        await page.goto('/__nebula_fixture__');
        await page.addScriptTag({ type: 'module', content: `
        import viewContainer from '/components/viewContainer.ts';
        window.ApiClient = {
            accessToken: () => 'test-token', deviceId: () => 'test', appVersion: () => 'test',
            getUrl: path => location.origin + '/' + path.replace(/^\\//, '')
        };
        viewContainer.setOnBeforeChange((view, restored, options) => new options.controllerFactory.default(view, {}));
        await viewContainer.loadView({ url: '/configurationpage?name=NebulaFTP', view: ${JSON.stringify(html)} });
    ` });
        await expect(page.locator('#globalStatusBadge')).toContainText('Envio: Parado');
        expect(authenticatedImport).toBe(true);
        for (const tab of [ 'tabDownload', 'tabBots', 'tabSupabase', 'tabNebula' ]) {
            await page.locator('.nebula-tab-btn[data-tab="' + tab + '"]').click();
            await expect(page.locator('#' + tab)).toBeVisible();
            await expect(page.locator('.nebula-tab-content.active')).toHaveCount(1);
            await expect(page.locator('.nebula-tab-btn[data-tab="' + tab + '"]')).toHaveClass(/active/);
        }
        await page.getByRole('button', { name: '🌐 Somente Streaming', exact: true }).click();
        await expect.poll(() => actions).toEqual([{ streamOnly: true }]);
        await page.locator('.nebula-tab-btn[data-tab="tabDownload"]').focus();
        await page.keyboard.press('Enter');
        await expect(page.locator('#tabDownload')).toBeVisible();
        expect(errors).toEqual([]);
        await page.screenshot({ path: testInfo.outputPath('nebula-download-navigation.png'), fullPage: true });
    });
}
