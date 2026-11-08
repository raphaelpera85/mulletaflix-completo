import assert from 'node:assert/strict';
import test from 'node:test';

import { getFixtureBaseUrl, navigateStage } from './stage.mjs';

test('getFixtureBaseUrl prefers the disposable Vite origin when configured', () => {
    const previousFixtureBaseUrl = process.env.PW_FIXTURE_BASE_URL;
    try {
        process.env.PW_FIXTURE_BASE_URL = 'http://127.0.0.1:18197/';
        assert.equal(getFixtureBaseUrl('http://127.0.0.1:8096/'), 'http://127.0.0.1:18197');
    } finally {
        if (previousFixtureBaseUrl === undefined) delete process.env.PW_FIXTURE_BASE_URL;
        else process.env.PW_FIXTURE_BASE_URL = previousFixtureBaseUrl;
    }
});

test('navigateStage skips a duplicate route that can leave Dashboard pending', async () => {
    const previousWindow = globalThis.window;
    let navigationCalled = false;
    let evaluateCalls = 0;

    globalThis.window = {
        location: { hash: '#/home' },
        Dashboard: {
            navigate: () => {
                navigationCalled = true;
                return new Promise(() => undefined);
            }
        }
    };

    const page = {
        evaluate: async (callback, value) => {
            evaluateCalls++;
            if (evaluateCalls === 1) return true;
            return callback(value);
        },
        goto: async () => undefined
    };

    try {
        const result = await Promise.race([
            navigateStage(page, '/home').then(() => 'resolved'),
            new Promise(resolve => setTimeout(() => resolve('timeout'), 50))
        ]);
        assert.equal(result, 'resolved');
        assert.equal(navigationCalled, false);
    } finally {
        globalThis.window = previousWindow;
    }
});

test('navigateStage resolves when Dashboard changes the hash before its promise settles', async () => {
    const previousWindow = globalThis.window;
    let evaluateCalls = 0;

    globalThis.window = {
        location: { hash: '#/home' },
        Dashboard: {
            navigate: () => {
                globalThis.window.location.hash = '#/search?query=test';
                return new Promise(() => undefined);
            }
        }
    };

    const page = {
        evaluate: async (callback, value) => {
            evaluateCalls++;
            if (evaluateCalls === 1) return true;
            return callback(value);
        },
        waitForFunction: async (callback, value) => {
            for (let attempt = 0; attempt < 20; attempt++) {
                if (callback(value)) return;
                await new Promise(resolve => setTimeout(resolve, 5));
            }
            throw new Error('route did not settle');
        },
        goto: async () => undefined
    };

    try {
        await navigateStage(page, '/search?query=test');
        assert.equal(globalThis.window.location.hash, '#/search?query=test');
    } finally {
        globalThis.window = previousWindow;
    }
});
