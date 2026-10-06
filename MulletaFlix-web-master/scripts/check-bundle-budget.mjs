#!/usr/bin/env node
/**
 * W3.2 - Initial bundle budget gate.
 *
 * Parses the built dist/index.html to find the JS/CSS actually loaded eagerly
 * on first paint (script[src]/link[rel=modulepreload|stylesheet] referencing
 * dist/assets/*), sums their gzip size, and fails the build if that initial
 * payload exceeds the agreed budget.
 *
 * This does NOT walk the whole dependency graph or Rollup's manifest -- it
 * deliberately checks what the browser downloads before any route code runs,
 * which is the thing users actually wait on. Heavy per-feature chunks (PDF
 * reader, EPUB reader, the admin dashboard, MUI) are expected to be absent
 * from this list because every route in this app is loaded through
 * AsyncRoute's dynamic import()/React.lazy and the two book/PDF players
 * import pdfjs-dist/epubjs with `import()` rather than a static import --
 * if one of those regresses to an eager import, this script's "unexpected
 * heavy chunk in initial load" check below will catch it even if the total
 * budget still has headroom.
 *
 * Usage: node scripts/check-bundle-budget.mjs [--dist ../dist]
 */
import { readFileSync, existsSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { resolve, dirname, basename } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));

// Measured on a clean `npm run build:production` run (2026-10-06). Keep this
// a hair above the real number so routine dependency bumps don't flap the
// build; the gate's job is to catch a real regression, not nag on every KB.
const INITIAL_BUDGET_GZIP_BYTES = 420 * 1024; // 420 KB

// Chunk name fragments that must NEVER appear in the eagerly-loaded set.
// Each one is deliberately lazy-loaded (see file comment above); if one
// shows up here, something started importing it eagerly.
const FORBIDDEN_IN_INITIAL_LOAD = [ 'vendor-pdf', 'vendor-epub', 'vendor-mui', 'vendor-admin', 'vendor-hls' ];

function parseArgs(argv) {
    const args = { dist: resolve(__dirname, '..', 'dist') };
    for (let i = 0; i < argv.length; i++) {
        if (argv[i] === '--dist' && argv[i + 1]) {
            args.dist = resolve(process.cwd(), argv[i + 1]);
            i++;
        }
    }
    return args;
}

export function extractInitialAssetNames(indexHtml) {
    const names = new Set();
    const pattern = /(?:src|href)="\.\/assets\/([^"]+\.(?:js|css))"/g;
    let match;
    while ((match = pattern.exec(indexHtml)) !== null) {
        names.add(match[1]);
    }
    return [ ...names ];
}

function collectAssetStats(dist, assetNames) {
    let totalGzip = 0;
    const rows = [];
    const violations = [];

    for (const name of assetNames) {
        const assetPath = resolve(dist, 'assets', name);
        if (!existsSync(assetPath)) {
            console.error(`[bundle-budget] index.html references ${name}, but it is missing from dist/assets.`);
            process.exit(1);
        }

        const raw = readFileSync(assetPath);
        const gzip = gzipSync(raw, { level: 9 }).length;
        totalGzip += gzip;
        rows.push({ name, gzip });

        for (const forbidden of FORBIDDEN_IN_INITIAL_LOAD) {
            if (name.includes(forbidden)) {
                violations.push(`${name} matches forbidden pattern "${forbidden}" but is referenced directly in index.html (eager load)`);
            }
        }
    }

    return { totalGzip, rows, violations };
}

function printReport(rows, totalGzip) {
    rows.sort((a, b) => b.gzip - a.gzip);
    console.log('[bundle-budget] Initial (eager) assets:');
    for (const row of rows) {
        console.log(`  ${basename(row.name).padEnd(40)} ${(row.gzip / 1024).toFixed(1).padStart(8)} KB gzip`);
    }
    console.log(`  ${'TOTAL'.padEnd(40)} ${(totalGzip / 1024).toFixed(1).padStart(8)} KB gzip (budget: ${(INITIAL_BUDGET_GZIP_BYTES / 1024).toFixed(0)} KB)`);
}

function main() {
    const { dist } = parseArgs(process.argv.slice(2));
    const indexPath = resolve(dist, 'index.html');

    if (!existsSync(indexPath)) {
        console.error(`[bundle-budget] dist/index.html not found at ${indexPath}. Run npm run build:production first.`);
        process.exit(1);
    }

    const indexHtml = readFileSync(indexPath, 'utf-8');
    const assetNames = extractInitialAssetNames(indexHtml);

    if (assetNames.length === 0) {
        console.error('[bundle-budget] Found zero asset references in dist/index.html -- parser or build output likely changed shape.');
        process.exit(1);
    }

    const { totalGzip, rows, violations } = collectAssetStats(dist, assetNames);
    printReport(rows, totalGzip);

    if (violations.length > 0) {
        console.error('\n[bundle-budget] FAIL: heavy chunk(s) loaded eagerly instead of on-demand:');
        for (const violation of violations) {
            console.error(`  - ${violation}`);
        }
    }

    if (totalGzip > INITIAL_BUDGET_GZIP_BYTES) {
        console.error(`\n[bundle-budget] FAIL: initial payload ${(totalGzip / 1024).toFixed(1)} KB gzip exceeds budget ${(INITIAL_BUDGET_GZIP_BYTES / 1024).toFixed(0)} KB gzip.`);
    }

    if (violations.length > 0 || totalGzip > INITIAL_BUDGET_GZIP_BYTES) {
        process.exit(1);
    }

    console.log('\n[bundle-budget] OK: initial payload within budget and no heavy chunks loaded eagerly.');
}

export { INITIAL_BUDGET_GZIP_BYTES, FORBIDDEN_IN_INITIAL_LOAD };

// Only run when executed directly (`node check-bundle-budget.mjs`), not when
// imported by a test for its pure helper functions. Compares basenames rather
// than full file:// URLs since Windows' drive-letter URL form
// (file:///D:/...) doesn't round-trip cleanly through process.argv[1].
const isMainModule = process.argv[1] && basename(process.argv[1]) === basename(fileURLToPath(import.meta.url));
if (isMainModule) {
    main();
}
