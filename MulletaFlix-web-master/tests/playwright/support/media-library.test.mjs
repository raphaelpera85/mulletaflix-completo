import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';

// eslint-disable-next-line sonarjs/publicly-writable-directories -- isolated disposable fixture
const scratchRoot = mkdtempSync(path.join(process.env.TMPDIR || os.tmpdir(), 'mflx-media-library-'));
const moviesPath = path.join(scratchRoot, 'movies');
const seriesPath = path.join(scratchRoot, 'series');

mkdirSync(moviesPath);
mkdirSync(seriesPath);
process.env.MFLX_MOVIES_PATH = moviesPath;
process.env.MFLX_SERIES_PATH = seriesPath;

const {
    ensureMediaLibraries,
    ensureMediaLibrariesReady,
    waitForMediaRecognition,
    MOVIES_LIBRARY
} = await import('./media-library.mjs');

test.after(() => rmSync(scratchRoot, { recursive: true, force: true }));

test('waits for media recognition before returning from ensureMediaLibrariesReady', async () => {
    let itemCountCalls = 0;
    const page = {
        async evaluate(fn) {
            const source = fn.toString();
            if (source.includes('getItemCounts')) {
                itemCountCalls += 1;
                return itemCountCalls === 1 ?
                    { MovieCount: 0, SeriesCount: 0 } :
                    { MovieCount: 1, SeriesCount: 1 };
            }
            if (source.includes('addVirtualFolder')) {
                return { existingCount: 0, created: [ 'Filmes', 'Series' ] };
            }
            if (source.includes('getScheduledTasks')) {
                return [];
            }
            return true;
        },
        async waitForTimeout() {
            await Promise.resolve();
        }
    };

    await ensureMediaLibrariesReady(page, {
        timeoutMs: 100,
        pollIntervalMs: 1,
        settleMs: 0
    });

    assert.equal(itemCountCalls, 2);
});

test('does not require series recognition when only movies are requested', async () => {
    let itemCountCalls = 0;
    const page = {
        async evaluate(fn) {
            const source = fn.toString();
            if (source.includes('getItemCounts')) {
                itemCountCalls += 1;
                return { MovieCount: 1, SeriesCount: 0 };
            }
            if (source.includes('addVirtualFolder')) {
                return { existingCount: 0, created: [ 'Filmes' ] };
            }
            if (source.includes('getScheduledTasks')) {
                return [];
            }
            return true;
        },
        async waitForTimeout() {
            await Promise.resolve();
        }
    };

    await ensureMediaLibrariesReady(page, {
        libraries: [ MOVIES_LIBRARY ],
        timeoutMs: 100,
        pollIntervalMs: 1,
        settleMs: 0
    });

    assert.equal(itemCountCalls, 1);
});

test('includes the last observed counts when media recognition times out', async () => {
    const page = {
        async evaluate(fn) {
            if (fn.toString().includes('getItemCounts')) {
                return { MovieCount: 0, SeriesCount: 0 };
            }

            return true;
        },
        async waitForTimeout() {
            await Promise.resolve();
        }
    };

    await assert.rejects(
        () => waitForMediaRecognition(page, { timeoutMs: 5, pollIntervalMs: 0, libraries: [ MOVIES_LIBRARY ] }),
        /Last counts: MovieCount=0, SeriesCount=0\./
    );
});

test('creates libraries without starting a concurrent scan before the explicit refresh', async () => {
    const addVirtualFolderCalls = [];
    const page = {
        async evaluate(fn, args) {
            const source = fn.toString();
            if (source.includes('addVirtualFolder')) {
                globalThis.window = {
                    ApiClient: {
                        async getVirtualFolders() {
                            return [];
                        },
                        async addVirtualFolder(...callArgs) {
                            addVirtualFolderCalls.push(callArgs);
                        }
                    }
                };

                try {
                    return await fn(args);
                } finally {
                    delete globalThis.window;
                }
            }

            return true;
        },
        async waitForTimeout() {
            await Promise.resolve();
        }
    };

    await ensureMediaLibraries(page, [ MOVIES_LIBRARY ]);

    assert.equal(addVirtualFolderCalls.length, 1);
    assert.equal(addVirtualFolderCalls[0][2], false);
});
