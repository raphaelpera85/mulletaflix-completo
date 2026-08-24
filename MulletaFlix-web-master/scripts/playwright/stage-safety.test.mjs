import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { readFile } from 'node:fs/promises';

import {
    assertSafeRemovalTargets,
    assertStagePidFileAbsent,
    buildStageKillArgs,
    buildStageRemovalTargets,
    parseStagePid
} from './stage-safety.mjs';

test('buildStageRemovalTargets only returns workspace stage paths', () => {
    const workspaceRoot = path.resolve('D:/workspace/mulletaflix');
    const stageDataDir = path.join(workspaceRoot, 'stage-data');
    const legacyStageDataDir = path.join(workspaceRoot, 'stage', 'data', 'jellyfin-test');

    const targets = buildStageRemovalTargets({ workspaceRoot, stageDataDir, legacyStageDataDir });

    assert.deepEqual(targets, [
        path.join(workspaceRoot, 'stage', 'data'),
        stageDataDir,
        legacyStageDataDir
    ]);
    assert.equal(targets.some(target => target.includes('AppData')), false);
});

test('assertSafeRemovalTargets rejects paths outside stage roots', () => {
    const workspaceRoot = path.resolve('D:/workspace/mulletaflix');
    const allowedRoots = [
        path.join(workspaceRoot, 'stage'),
        path.join(workspaceRoot, 'stage-data')
    ];

    assert.throws(
        () => assertSafeRemovalTargets([
            path.resolve('C:/Users/test/AppData/Local/MulletaFlix')
        ], allowedRoots),
        /outside allowed stage roots/
    );
});

test('parseStagePid accepts only positive integer process ids', () => {
    assert.equal(parseStagePid('1234\n'), 1234);
    assert.equal(parseStagePid('0'), null);
    assert.equal(parseStagePid('not-a-pid'), null);
});

test('buildStageKillArgs targets only the registered process tree', () => {
    assert.deepEqual(buildStageKillArgs(1234), [ '/PID', '1234', '/T', '/F' ]);
    assert.throws(() => buildStageKillArgs(0), /valid stage pid/);
});

test('assertStagePidFileAbsent refuses to reset an ambiguously owned stage', () => {
    assert.doesNotThrow(() => assertStagePidFileAbsent(false));
    assert.throws(() => assertStagePidFileAbsent(true), /existing stage pid file/);
});

test('run-suite never removes local app data or kills database processes by image name', async () => {
    const source = await readFile(new URL('./run-suite.mjs', import.meta.url), 'utf8');

    assert.doesNotMatch(source, /LOCALAPPDATA/);
    assert.doesNotMatch(source, /\[\s*['"]\/IM['"]/);
    assert.match(source, /buildStageRemovalTargets/);
    assert.match(source, /buildStageKillArgs/);
    assert.match(source, /assertStagePidFileAbsent\(existsSync\(stagePidFile\)\)/);
    assert.match(source, /if \(ownsStageProcess\) \{\s*await stopTrackedStageProcess\(\)/);
});

test('run-suite starts the published dll through the installed dotnet host with visible logs', async () => {
    const source = await readFile(new URL('./run-suite.mjs', import.meta.url), 'utf8');

    assert.match(source, /const stageDll = .*MulletaFlix\.dll/);
    assert.match(source, /spawn\('dotnet', \[ stageDll,/);
    assert.match(source, /stdio: \[ 'ignore', 'inherit', 'inherit' \]/);
});
