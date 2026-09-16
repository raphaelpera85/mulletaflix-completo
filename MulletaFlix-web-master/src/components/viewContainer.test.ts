import { beforeEach, describe, expect, it, vi } from 'vitest';

import viewContainer from './viewContainer';

describe('viewContainer', () => {
    beforeEach(() => {
        document.body.innerHTML = '<div class="mainAnimatedPages"></div>';
        viewContainer.reset();
        (window as unknown as { ApiClient?: { getUrl: (path: string) => string } }).ApiClient = {
            getUrl: (path: string) => `http://localhost:8096/${path}`
        };
    });

    it('attaches and executes inline scripts in dynamic views', () => {
        const testVarName = '__test_view_container_script_run__';
        (window as unknown as Record<string, unknown>)[testVarName] = false;

        const html = `
            <div id="testConfigPage" data-role="page" class="page">
                <div id="content">Hello Test</div>
                <script>
                    window.${testVarName} = true;
                </script>
            </div>
        `;

        viewContainer.loadView({
            url: '/web/#/configurationpage?name=TestPlugin',
            view: html
        });

        expect((window as unknown as Record<string, unknown>)[testVarName]).toBe(true);
        expect(document.getElementById('testConfigPage')).not.toBeNull();
    });

    it('resolves relative URLs for external scripts and modules', () => {
        const html = `
            <div id="IntroSkipperConfigPage" data-role="page" class="page">
                <div id="intro-skipper-dashboard-root"></div>
                <script type="module" src="configurationpage?name=introskipper.js"></script>
                <link rel="stylesheet" href="configurationpage?name=introskipper.css">
            </div>
        `;

        viewContainer.loadView({
            url: '/web/#/configurationpage?name=Intro%20Skipper',
            view: html
        });

        const page = document.getElementById('IntroSkipperConfigPage');
        expect(page).not.toBeNull();

        const script = page?.querySelector('script');
        expect(script).not.toBeNull();
        expect(script?.type).toBe('module');
        expect(script?.src).toBe('http://localhost:8096/web/configurationpage?name=introskipper.js');

        const link = page?.querySelector('link[rel="stylesheet"]');
        expect(link).not.toBeNull();
        expect(link?.getAttribute('href')).toBe('http://localhost:8096/web/configurationpage?name=introskipper.css');
    });

    it('preserves scripts and stylesheets placed outside the page div in wrapper', () => {
        const html = `
            <div id="outsideTestPage" data-role="page" class="page">
                <div>Page Body</div>
            </div>
            <script type="module" src="configurationpage?name=outside.js"></script>
            <link rel="stylesheet" href="configurationpage?name=outside.css">
        `;

        viewContainer.loadView({
            url: '/web/#/configurationpage?name=Outside',
            view: html
        });

        const page = document.getElementById('outsideTestPage');
        expect(page).not.toBeNull();

        const script = page?.querySelector('script');
        expect(script).not.toBeNull();
        expect(script?.src).toBe('http://localhost:8096/web/configurationpage?name=outside.js');

        const link = page?.querySelector('link[rel="stylesheet"]');
        expect(link).not.toBeNull();
        expect(link?.getAttribute('href')).toBe('http://localhost:8096/web/configurationpage?name=outside.css');
    });
});
