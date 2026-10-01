import test from 'node:test';
import assert from 'node:assert/strict';

import {
    buildMarkdownReport,
    evaluatePlaywrightGate,
    extractPlaywrightStats
} from './report.mjs';

test('Playwright stats keep retried tests separate from clean passes', () => {
    const stats = extractPlaywrightStats({
        stats: { expected: 4, unexpected: 1, flaky: 2, skipped: 3, duration: 1234 }
    });

    assert.deepEqual(stats, {
        total: 10,
        passed: 4,
        failed: 1,
        flaky: 2,
        skipped: 3,
        durationMs: 1234
    });
});

test('Playwright gate rejects flaky results even when the test process exits zero', () => {
    const stats = extractPlaywrightStats({ stats: { expected: 1, flaky: 1 } });

    assert.deepEqual(evaluatePlaywrightGate(0, stats, true), {
        status: 'flaky',
        exitCode: 1
    });
});

test('Playwright gate rejects failed, missing and empty reports', () => {
    const passed = extractPlaywrightStats({ stats: { expected: 1 } });
    const failed = extractPlaywrightStats({ stats: { unexpected: 1 } });
    const empty = extractPlaywrightStats({ stats: {} });
    const skipped = extractPlaywrightStats({ stats: { skipped: 2 } });

    assert.deepEqual(evaluatePlaywrightGate(1, passed, true), { status: 'failed', exitCode: 1 });
    assert.deepEqual(evaluatePlaywrightGate(0, failed, true), { status: 'failed', exitCode: 1 });
    assert.deepEqual(evaluatePlaywrightGate(0, passed, false), { status: 'unverified', exitCode: 1 });
    assert.deepEqual(evaluatePlaywrightGate(0, empty, true), { status: 'no-tests', exitCode: 1 });
    assert.deepEqual(evaluatePlaywrightGate(0, skipped, true), { status: 'no-tests', exitCode: 1 });
    assert.deepEqual(evaluatePlaywrightGate(0, passed, true), { status: 'success', exitCode: 0 });
});

test('Playwright summary tells the operator to investigate flaky attempts', () => {
    const markdown = buildMarkdownReport({
        title: 'Playwright',
        generatedAt: '2026-10-01T00:00:00Z',
        baseUrl: 'http://127.0.0.1:8096',
        status: 'flaky',
        specCount: 1,
        tests: { total: 2, passed: 1, failed: 0, flaky: 1, skipped: 0, durationMs: 10 },
        stageProbe: { reachable: true, startupWizardCompleted: true },
        stageChecks: { wizard: false, admin: true, user: true, login: true },
        assumptions: []
    });

    assert.match(markdown, /Instáveis: 1/);
    assert.match(markdown, /trace e screenshot/);
});

test('Playwright summary does not claim success when the process fails or all tests are skipped', () => {
    const base = {
        title: 'Playwright',
        generatedAt: '2026-10-01T00:00:00Z',
        baseUrl: 'http://127.0.0.1:8096',
        specCount: 1,
        stageProbe: { reachable: true, startupWizardCompleted: true },
        stageChecks: { wizard: false, admin: true, user: true, login: true },
        assumptions: []
    };
    const failed = buildMarkdownReport({
        ...base,
        status: 'failed',
        tests: { total: 1, passed: 1, failed: 0, flaky: 0, skipped: 0, durationMs: 10 }
    });
    const skipped = buildMarkdownReport({
        ...base,
        status: 'no-tests',
        tests: { total: 2, passed: 0, failed: 0, flaky: 0, skipped: 2, durationMs: 10 }
    });

    assert.match(failed, /A suíte falhou/);
    assert.match(skipped, /Nenhum teste foi executado/);
});
