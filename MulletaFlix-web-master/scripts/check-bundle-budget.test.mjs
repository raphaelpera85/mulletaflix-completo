import test from 'node:test';
import assert from 'node:assert/strict';

import { extractInitialAssetNames, FORBIDDEN_IN_INITIAL_LOAD } from './check-bundle-budget.mjs';

test('extractInitialAssetNames finds every script src and stylesheet href under ./assets', () => {
    const html = `
        <script type="module" src="./assets/index-ABC123.js"></script>
        <link rel="modulepreload" href="./assets/vendor-react-DEF456.js" />
        <link rel="stylesheet" href="./assets/index-GHI789.css" />
        <link rel="icon" href="./favicon.ico" />
    `;

    const names = extractInitialAssetNames(html);

    assert.deepEqual(names.sort(), [
        'index-ABC123.js',
        'index-GHI789.css',
        'vendor-react-DEF456.js'
    ]);
});

test('extractInitialAssetNames returns an empty list for an index.html with no asset refs', () => {
    assert.deepEqual(extractInitialAssetNames('<html><body>empty</body></html>'), []);
});

test('extractInitialAssetNames deduplicates repeated references', () => {
    const html = `
        <script src="./assets/index-ABC123.js"></script>
        <link rel="modulepreload" href="./assets/index-ABC123.js" />
    `;

    assert.deepEqual(extractInitialAssetNames(html), [ 'index-ABC123.js' ]);
});

// Regression guard for the actual defect class this script exists to catch:
// if someone statically imports the PDF/EPUB reader or the admin dashboard
// bundle from a module that ends up in the initial chunk graph, this is the
// pattern list that must flag it.
test('FORBIDDEN_IN_INITIAL_LOAD still covers the known heavy/lazy chunks', () => {
    for (const expected of [ 'vendor-pdf', 'vendor-epub', 'vendor-mui', 'vendor-admin', 'vendor-hls' ]) {
        assert.ok(FORBIDDEN_IN_INITIAL_LOAD.includes(expected), `expected ${expected} in FORBIDDEN_IN_INITIAL_LOAD`);
    }
});
