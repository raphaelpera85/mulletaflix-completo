import { readdir, stat } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { join, relative } from 'node:path';

const distDir = fileURLToPath(new URL('../dist/', import.meta.url));
const maxAssetBytes = 1536 * 1024;

async function walk(directory) {
    const entries = await readdir(directory, { withFileTypes: true });
    const files = [];
    for (const entry of entries) {
        const path = join(directory, entry.name);
        if (entry.isDirectory()) files.push(...await walk(path));
        else files.push(path);
    }
    return files;
}

const files = await walk(distDir);
if (!files.some((file) => file.endsWith('index.html'))) {
    throw new Error('O build não contém dist/index.html.');
}

if (!files.some((file) => file.endsWith('serviceworker.js'))) {
    throw new Error('O build não contém dist/serviceworker.js, embora o cliente registre esse worker.');
}

const oversized = [];
for (const file of files) {
    const size = (await stat(file)).size;
    const relativePath = relative(distDir, file).replaceAll('\\', '/');
    if (!relativePath.startsWith('assets/') || !/\.(?:js|css)$/.test(relativePath)) continue;
    if (size > maxAssetBytes) {
        oversized.push(`${relativePath} (${Math.ceil(size / 1024)} KiB)`);
    }
}

if (oversized.length > 0) {
    throw new Error(`Artefatos acima do limite de 1536 KiB:\n${oversized.join('\n')}`);
}

console.log(`Artefatos válidos: ${files.length} arquivos; limite individual 1536 KiB.`);
