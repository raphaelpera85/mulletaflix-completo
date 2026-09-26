// Temporary verification harness (round 26).
// Loads the web client over the production origin so the API calls keep working, serving the locally
// built `dist` through request interception. `WEB_MODE=installed` skips the interception and loads the
// bundle the server actually has, which is the control run.
//
// Why this exists: the boot ordering in src/index.tsx (F-1/F-3) cannot be verified by unit tests, and
// the repository's Playwright suite targets the installed bundle and creates users.
import { chromium } from 'playwright';
import fs from 'node:fs';
import path from 'node:path';

const DIST = path.resolve('dist');
const BASE = process.env.WEB_BASE ?? 'http://127.0.0.1:8096';
const SETTLE_MS = Number(process.env.WEB_SETTLE_MS ?? 12000);
const LATENCY_MS = Number(process.env.WEB_LATENCY_MS ?? 0);
const MODE = process.env.WEB_MODE ?? 'dist';

const MIME = {
    '.js': 'text/javascript',
    '.mjs': 'text/javascript',
    '.css': 'text/css',
    '.html': 'text/html',
    '.json': 'application/json',
    '.svg': 'image/svg+xml',
    '.png': 'image/png',
    '.jpg': 'image/jpeg',
    '.webp': 'image/webp',
    '.ico': 'image/x-icon',
    '.woff': 'font/woff',
    '.woff2': 'font/woff2',
    '.ttf': 'font/ttf',
    '.wasm': 'application/wasm',
    '.map': 'application/json'
};

const browser = await chromium.launch();
const context = await browser.newContext();
const page = await context.newPage();

// The init script runs before the document exists, so it must observe `document` rather than
// documentElement, and it must not throw: an exception here is reported as a page error.
//
// #reactRoot already contains a static loading splash from index.html, so "has children" is not a
// render. The marker therefore waits for reactRoot's markup to change from the splash it started with,
// which is what renderApp() causes.
await page.addInitScript(() => {
    window.__marks = {};
    let splashLength = null;
    const check = () => {
        const root = document.getElementById('reactRoot');
        if (!root) {
            return;
        }

        if (splashLength === null) {
            splashLength = root.innerHTML.length;
            return;
        }

        if (window.__marks.appRendered === undefined && root.children.length > 0 && root.innerHTML.length !== splashLength) {
            window.__marks.appRendered = performance.now();
        }
    };

    new MutationObserver(check).observe(document, { childList: true, subtree: true, characterData: true });
    document.addEventListener('DOMContentLoaded', () => {
        check();
        // renderApp() runs after several awaited bootstrap steps, so keep sampling past DOMContentLoaded.
        const timer = setInterval(() => {
            check();
            if (window.__marks.appRendered !== undefined) {
                clearInterval(timer);
            }
        }, 5);
    });
});

const consoleLines = [];
const pageErrors = [];
const requestedAssets = [];
const requestedApi = [];
let servedFromDist = 0;
const fellThrough = [];

page.on('console', (message) => consoleLines.push(`${message.type()}: ${message.text()}`));
page.on('pageerror', (error) => pageErrors.push(error.message));
page.on('request', (request) => {
    const url = new URL(request.url());
    if (url.pathname.startsWith('/web/assets/')) {
        requestedAssets.push(url.pathname.replace('/web/assets/', ''));
    } else if (url.origin === BASE && !url.pathname.startsWith('/web/')) {
        requestedApi.push(url.pathname);
    }
});

if (MODE === 'dist') {
    await page.route('**/web/**', async (route) => {
        const url = new URL(route.request().url());
        let relative = url.pathname.replace(/^\/web\/?/, '');
        if (relative === '') {            relative = 'index.html';
        }

        const file = path.join(DIST, relative);
        if (fs.existsSync(file) && fs.statSync(file).isFile()) {
            servedFromDist++;

            // Serving from disk is far faster than the real network, which hides exactly the kind of
            // overlap F-1 is about. Optional latency makes the two orderings comparable.
            if (LATENCY_MS > 0) {
                await new Promise((resolve) => setTimeout(resolve, LATENCY_MS));
            }

            await route.fulfill({
                status: 200,
                contentType: MIME[path.extname(file).toLowerCase()] ?? 'application/octet-stream',
                body: fs.readFileSync(file)
            });
        } else {
            // Falling through would mix the local bundle with whatever the installed server has, which
            // silently invalidates the comparison. Record it instead of hiding it.
            fellThrough.push(url.pathname);
            await route.continue();
        }
    });
}

const started = Date.now();
await page.goto(`${BASE}/web/`, { waitUntil: 'load', timeout: 60000 });
const loadedMs = Date.now() - started;
await page.waitForTimeout(SETTLE_MS);

const state = await page.evaluate(() => {
    const root = document.getElementById('reactRoot');
    return {
        rootExists: Boolean(root),
        rootChildren: root ? root.children.length : 0,
        rootHtmlLength: root ? root.innerHTML.length : 0,
        bodyClasses: document.body.className,
        hasSkinHeader: Boolean(document.querySelector('.skinHeader')),
        title: document.title
    };
});

// version banner
const versionBanner = consoleLines.filter((line) => /version:|commit:|build:/.test(line));
versionBanner.forEach((line) => console.log(`   [client] ${line}`));

const bootstrapWarnings = consoleLines.filter(
    (line) => line.includes('[bootstrap]') && (line.includes('falhou') || line.includes('excedeu'))
);
const consoleErrors = consoleLines.filter((line) => line.startsWith('error:'));

const timing = await page.evaluate(() => ({
    marks: window.__marks ?? {},
    entries: performance.getEntriesByType('resource').map((entry) => ({
        file: entry.name.split('/').pop(),
        start: Math.round(entry.startTime),
        end: Math.round(entry.responseEnd),
        kb: Math.round((entry.transferSize || 0) / 1024)
    }))
}));

const renderAt = timing.marks.appRendered;

console.log(`=== BOOT VERIFICATION (mode=${MODE}) ===`);
console.log(`served from local dist : ${servedFromDist} files`);
console.log(`fell through to server : ${fellThrough.length}${fellThrough.length ? ` -> ${fellThrough.slice(0, 6).join(', ')}` : ''}`);
console.log(`load event             : ${loadedMs} ms`);
console.log(`first render           : ${renderAt === undefined ? 'NEVER' : `${Math.round(renderAt)} ms`}`);
console.log(`reactRoot              : exists=${state.rootExists} children=${state.rootChildren} html=${state.rootHtmlLength}`);
console.log(`skinHeader rendered    : ${state.hasSkinHeader}`);
console.log(`body classes           : ${state.bodyClasses}`);
console.log(`bootstrap warnings     : ${bootstrapWarnings.length}`);
bootstrapWarnings.slice(0, 6).forEach((line) => console.log(`   ! ${line}`));
console.log(`page errors            : ${pageErrors.length}`);
pageErrors.slice(0, 6).forEach((line) => console.log(`   ! ${line}`));
console.log(`console errors         : ${consoleErrors.length}`);
consoleErrors.slice(0, 8).forEach((line) => console.log(`   ! ${line}`));

const uniqueAssets = [...new Set(requestedAssets)];
console.log(`asset requests         : ${requestedAssets.length} (${uniqueAssets.length} unique)`);

if (process.env.WEB_DUMP) {
    fs.writeFileSync(process.env.WEB_DUMP, uniqueAssets.slice().sort().join('\n'), 'utf8');
}
console.log('=== critical path (resource end vs first render) ===');
const interesting = ['vendor-date-fns', 'vendor-mui', 'vendor-emotion'];
for (const entry of timing.entries) {
    if (!interesting.some((prefix) => entry.file.startsWith(prefix))) {
        continue;
    }

    const verdict = renderAt === undefined
        ? 'unknown'
        : (entry.end <= renderAt ? 'BEFORE render' : 'after render');
    console.log(`   ${entry.file.padEnd(32)} start=${String(entry.start).padStart(6)} end=${String(entry.end).padStart(6)} ${String(entry.kb).padStart(5)}KB  ${verdict}`);
}

const apiCalls = [...new Set(requestedApi)];
console.log(`api calls              : ${apiCalls.length} unique`);
apiCalls.slice(0, 15).forEach((a) => console.log(`    ${a}`));

// Byte accounting for the critical path. `transferSize` is 0 on a cache hit, so the size on disk is
// used as the fallback: a warm-cache run would otherwise report an empty critical path.
const sizeOnDisk = (file) => {
    try {
        return Math.round(fs.statSync(path.join(DIST, 'assets', file)).size / 1024);
    } catch {
        return 0;
    }
};

if (renderAt !== undefined) {
    const beforeRender = timing.entries.filter((entry) => entry.end > 0 && entry.end <= renderAt);
    const totalKb = beforeRender.reduce((sum, entry) => sum + (entry.kb || sizeOnDisk(entry.file)), 0);
    console.log(`=== bytes before first render ===`);
    console.log(`assets ending before render: ${beforeRender.length}`);
    console.log(`total transferred          : ${totalKb} KB`);
    console.log('top 12 by size:');
    beforeRender
        .map((entry) => ({ ...entry, size: entry.kb || sizeOnDisk(entry.file) }))
        .sort((a, b) => b.size - a.size)
        .slice(0, 12)
        .forEach((entry) => console.log(`   ${String(entry.size).padStart(5)}KB  end=${String(entry.end).padStart(5)}  ${entry.file}`));
}

await browser.close();

const rendered = state.rootExists && state.rootChildren > 0;
const noRuntimeErrors = pageErrors.length === 0;
const noFallthrough = MODE !== 'dist' || fellThrough.length === 0;
const ok = rendered && noRuntimeErrors && noFallthrough;
console.log(`=== RESULT: ${ok ? 'BOOT OK' : 'BOOT FAILED'} (rendered=${rendered} noPageErrors=${noRuntimeErrors} noFallthrough=${noFallthrough}) ===`);
process.exit(ok ? 0 : 1);

