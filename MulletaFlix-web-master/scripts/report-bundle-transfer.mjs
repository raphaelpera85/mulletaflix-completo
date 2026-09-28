import { readdir, readFile } from 'node:fs/promises';
import { brotliCompressSync, constants, gzipSync } from 'node:zlib';
import { fileURLToPath } from 'node:url';
import { join, relative, resolve, sep } from 'node:path';

const distDirectory = fileURLToPath(new URL('../dist/', import.meta.url));
const indexPath = join(distDirectory, 'index.html');
const assetsDirectory = resolve(distDirectory, 'assets');

async function listFiles(directory) {
    const entries = await readdir(directory, { withFileTypes: true });
    const files = [];

    for (const entry of entries) {
        const entryPath = join(directory, entry.name);
        if (entry.isDirectory()) files.push(...await listFiles(entryPath));
        else files.push(entryPath);
    }

    return files.sort();
}

function measure(buffer) {
    return {
        raw: buffer.length,
        gzip: gzipSync(buffer, { level: 6 }).length,
        brotli: brotliCompressSync(buffer, {
            params: {
                [constants.BROTLI_PARAM_QUALITY]: 5,
                [constants.BROTLI_PARAM_MODE]: constants.BROTLI_MODE_GENERIC
            }
        }).length
    };
}

function total(measurements) {
    return measurements.reduce((sum, current) => ({
        raw: sum.raw + current.raw,
        gzip: sum.gzip + current.gzip,
        brotli: sum.brotli + current.brotli
    }), { raw: 0, gzip: 0, brotli: 0 });
}

const htmlBuffer = await readFile(indexPath);
const html = htmlBuffer.toString('utf8');
const referencedAssets = [...html.matchAll(/(?:src|href)=["']\.\/(assets\/[^"'?#]+)(?:[?#][^"']*)?["']/g)]
    .map((match) => match[1])
    .filter((asset) => /\.(?:js|css)$/i.test(asset));
const initialAssets = [...new Set(referencedAssets)].map((asset) => {
    const assetPath = resolve(distDirectory, asset);
    if (!assetPath.startsWith(`${assetsDirectory}${sep}`)) {
        throw new Error(`Referência de asset fora de dist/assets: ${asset}`);
    }

    return assetPath;
});

if (initialAssets.length === 0) {
    throw new Error('Nenhum asset JS/CSS inicial foi encontrado em dist/index.html.');
}

const allAssets = (await listFiles(assetsDirectory))
    .filter((assetPath) => /\.(?:js|css)$/i.test(assetPath));
const initialMeasurements = await Promise.all(initialAssets.map(async (assetPath) => measure(await readFile(assetPath))));
const allMeasurements = await Promise.all(allAssets.map(async (assetPath) => measure(await readFile(assetPath))));
const htmlMeasurement = measure(htmlBuffer);
const initialAssetTotals = total(initialMeasurements);

console.log(JSON.stringify({
    compression: {
        gzipLevel: 6,
        brotliQuality: 5,
        brotliMode: 'generic',
        note: 'Tamanhos comprimidos simulados; não representam negociação ou resposta HTTP do servidor.'
    },
    html: htmlMeasurement,
    initialAssets: {
        count: initialAssets.length,
        names: initialAssets.map((assetPath) => relative(distDirectory, assetPath).replaceAll('\\', '/')),
        totals: initialAssetTotals
    },
    htmlPlusInitialAssets: total([htmlMeasurement, initialAssetTotals]),
    allJavaScriptAndCssAssets: {
        count: allAssets.length,
        totals: total(allMeasurements)
    }
}, null, 2));
