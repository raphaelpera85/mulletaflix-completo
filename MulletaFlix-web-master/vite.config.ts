/// <reference types="vitest" />
/// <reference types="vite/client" />
import { defineConfig } from 'vite';
import tsconfigPaths from 'vite-tsconfig-paths';
import path from 'path';
import fs from 'fs';

const vendorChunkRules: Array<[string[], string]> = [
    [['pdfjs-dist'], 'vendor-pdf'],
    [['hls.js'], 'vendor-hls'],
    [['flv.js'], 'vendor-flv'],
    [['swiper'], 'vendor-swiper'],
    [['react-router', '@remix-run/router'], 'vendor-router'],
    [['/node_modules/react/', '/node_modules/react-dom/'], 'vendor-react'],
    [['@jellyfin/sdk', 'jellyfin-apiclient'], 'vendor-jellyfin'],
    [['epubjs'], 'vendor-epub'],
    [['libarchive.js'], 'vendor-archive'],
    [['lodash-es'], 'vendor-lodash'],
    [['markdown-it'], 'vendor-markdown'],
    [['dompurify'], 'vendor-dompurify'],
    [['/axios/'], 'vendor-axios'],
    [['@tanstack/react-query'], 'vendor-react-query'],
    [['/date-fns/'], 'vendor-date-fns'],
    [['@mui/icons-material'], 'vendor-mui-icons'],
    [['@mui/x-date-pickers'], 'vendor-mui-pickers'],
    [['@emotion'], 'vendor-emotion'],
    [['@mui'], 'vendor-mui'],
    [['jstree', 'sortablejs'], 'vendor-admin']
];

function getVendorChunk(id: string): string | undefined {
    if (id.includes('/date-fns/locale/')) return undefined;
    return vendorChunkRules.find(([markers]) => markers.some((marker) => id.includes(marker)))?.[1];
}

const serviceWorkerOutputPlugin = () => ({
    name: 'service-worker-output',
    apply: 'build' as const,
    async generateBundle(this: { emitFile: (file: { type: 'asset'; fileName: string; source: string }) => void }) {
        const serviceWorkerPath = path.resolve(process.cwd(), 'src/serviceworker.ts');
        const source = fs.readFileSync(serviceWorkerPath, 'utf-8');
        const { transformWithEsbuild } = await import('vite');
        const transformed = await transformWithEsbuild(source, serviceWorkerPath, {
            loader: 'ts',
            format: 'iife',
            target: 'es2020',
            sourcemap: false
        });

        this.emitFile({
            type: 'asset',
            fileName: 'serviceworker.js',
            source: transformed.code
        });
    }
});

const htmlPlugin = () => ({
    name: 'html-transform',
    enforce: 'pre' as const,
    async resolveId(source: string, importer: string | undefined) {
        // Redireciona imports de HTML para ?html-string
        if (source.endsWith('.html') && !source.endsWith('index.html')) {
            const resolved = await this.resolve(source, importer, { skipSelf: true });
            if (resolved) {
                return resolved.id + '?html-string';
            }
        }
        // Redireciona imports de Workers para ?worker nativo do Vite
        if (source.endsWith('.worker.ts') || source.endsWith('.worker.js')) {
            if (!source.includes('?')) {
                const resolved = await this.resolve(source, importer, { skipSelf: true });
                if (resolved) {
                    return resolved.id + '?worker';
                }
            }
        }
        return null;
    },
    load(id: string) {
        if (id.endsWith('?html-string')) {
            const filePath = id.replace(/\?html-string$/, '');
            const root = process.cwd();
            if (!path.resolve(filePath).startsWith(root)) {
                throw new Error(`[html-plugin] path traversal blocked: ${filePath}`);
            }
            try {
                const content = fs.readFileSync(filePath, 'utf-8');
                return `export default ${JSON.stringify(content)};`;
            } catch (err) {
                this.warn(`[html-plugin] failed to read ${filePath}: ${err}`);
                return 'export default "";';
            }
        }
        return null;
    }
});

export default defineConfig({
    resolve: {
        alias: [
            { find: /^~/, replacement: '' },
            // Keep the legacy player imports stable while bundling the smaller
            // official HLS light build (the player only uses the core API).
            { find: 'hls.js/dist/hls.js', replacement: 'hls.js/light' }
        ]
    },
    base: './',
    root: 'src',
    plugins: [ tsconfigPaths(), htmlPlugin(), serviceWorkerOutputPlugin() ],
    define: {
        __COMMIT_SHA__: JSON.stringify('release'),
        __JF_BUILD_VERSION__: JSON.stringify('Release'),
        __PACKAGE_JSON_NAME__: JSON.stringify('MulletaFlix-web'),
        __PACKAGE_JSON_VERSION__: JSON.stringify('12.0.0'),
        __USE_SYSTEM_FONTS__: 'false',
        __WEBPACK_SERVE__: 'false'
    },
    optimizeDeps: {
        // The legacy HTML templates are loaded through htmlPlugin and are not
        // JavaScript entry points. Restrict the dev scan to the real shell so
        // Vite does not try to parse every controller template as a module.
        entries: [ 'index.html' ]
    },
    build: {
        outDir: '../dist',
        emptyOutDir: true,
        rollupOptions: {
            output: {
                manualChunks(id) {
                    if (!id.includes('node_modules')) {
                        return undefined;
                    }
                    return getVendorChunk(id);
                }
            }
        }
    },
    test: {
        coverage: {
            include: [ '**/*.{ts,tsx,js,jsx}' ],
            exclude: [ '**/*.spec.*', '**/*.test.*', '**/tests/**', '**/*.d.ts' ]
        },
        environment: 'jsdom',
        restoreMocks: true
    }
});
