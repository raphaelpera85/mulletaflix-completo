import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { readFile } from 'node:fs/promises';

import {
    assertSafeRemovalTargets,
    assertStagePidFileAbsent,
    buildRestrictedDirectoryAclArgs,
    buildStageKillArgs,
    buildStageRemovalTargets,
    isDedicatedStageDataPath,
    assertSafeStageBaseUrl,
    parseStagePid,
    parseWindowsUserSid,
    resolveDotnetExecutable
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

test('isolated stage data accepts only dedicated paths under workspace or temp', () => {
    const workspaceRoot = path.resolve('D:/workspace/mulletaflix');
    const tempRoot = path.resolve('C:/temp');

    assert.equal(isDedicatedStageDataPath(path.join(workspaceRoot, 'mflx-playwright-stage-run1'), workspaceRoot, tempRoot), true);
    assert.equal(isDedicatedStageDataPath(path.join(tempRoot, 'mflx-playwright-stage-run2'), workspaceRoot, tempRoot), true);
    assert.equal(isDedicatedStageDataPath(path.join(workspaceRoot, 'stage-data'), workspaceRoot, tempRoot), false);
    assert.equal(isDedicatedStageDataPath(path.resolve('C:/Users/test/AppData/mflx-playwright-stage-run3'), workspaceRoot, tempRoot), false);
});

test('Playwright stage URL is restricted to loopback ports 18096-18196', () => {
    assert.equal(assertSafeStageBaseUrl('http://127.0.0.1:18096'), 'http://127.0.0.1:18096');
    assert.equal(assertSafeStageBaseUrl('http://localhost:18196'), 'http://localhost:18196');
    assert.throws(() => assertSafeStageBaseUrl('http://127.0.0.1:8096'), /isolated port/);
    assert.throws(() => assertSafeStageBaseUrl('http://192.168.1.10:18096'), /loopback HTTP/);
    assert.throws(() => assertSafeStageBaseUrl('https://127.0.0.1:18096'), /loopback HTTP/);
});

test('default Playwright URL avoids the server default port', async () => {
    const source = await readFile(new URL('../../tests/playwright/support/stage.mjs', import.meta.url), 'utf8');
    assert.match(source, /DEFAULT_STAGE_BASE_URL = 'http:\/\/127\.0\.0\.1:18096'/);
    assert.doesNotMatch(source, /DEFAULT_STAGE_BASE_URL = 'http:\/\/127\.0\.0\.1:8096'/);
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

test('stage data ACL grants write only to the current user, SYSTEM and administrators', () => {
    const userSid = parseWindowsUserSid('"USER", "S-1-5-21-11-22-33-1001"');
    assert.equal(userSid, 'S-1-5-21-11-22-33-1001');
    assert.deepEqual(buildRestrictedDirectoryAclArgs('C:\\Temp\\mflx-stage', userSid), [
        'C:\\Temp\\mflx-stage',
        '/inheritance:r',
        '/grant:r',
        '*S-1-5-21-11-22-33-1001:(OI)(CI)F',
        '*S-1-5-18:(OI)(CI)F',
        '*S-1-5-32-544:(OI)(CI)F'
    ]);
    assert.throws(() => buildRestrictedDirectoryAclArgs('C:\\Temp\\mflx-stage', null), /valid Windows user SID/);
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

test('run-suite starts the published dll through the platform dotnet host with visible logs', async () => {
    const source = await readFile(new URL('./run-suite.mjs', import.meta.url), 'utf8');

    assert.match(source, /const stageDll = process\.env\.PW_STAGE_DLL \? path\.resolve\(process\.env\.PW_STAGE_DLL\) : path\.join\(workspaceRoot, 'stage', 'MulletaFlix\.dll'\)/);
    assert.match(source, /spawn\(dotnetExecutable, \[ stageDll,/);
    assert.match(source, /resolveDotnetExecutable\(process\.platform, process\.env\.ProgramFiles\)/);
    assert.match(source, /spawnSync\(taskkillExecutable, buildStageKillArgs\(pid\)/);
    assert.match(source, /process\.kill\(-pid, 'SIGTERM'\)/);
    assert.match(source, /stdio: \[ 'ignore', 'inherit', 'inherit' \]/);
});

test('run-suite disables Nebula notifications and remote updater calls for isolated stages', async () => {
    const source = await readFile(new URL('./run-suite.mjs', import.meta.url), 'utf8');

    assert.match(source, /<NotificationsEnabled>false<\/NotificationsEnabled>/);
    assert.match(source, /<TelegramNotificationsEnabled>false<\/TelegramNotificationsEnabled>/);
    assert.match(source, /<ChatId \/>/);
    assert.match(source, /MulletaFlix_UPDATE_MANIFEST_URL: 'http:\/\/127\.0\.0\.1:1\/e2e-disabled'/);
    assert.match(source, /assertSafeStageBaseUrl\(args\.baseUrl\)/);
    assert.match(source, /18096-18196; a porta 8096 não é permitida para stage/);
    assert.match(source, /AbortSignal\.timeout\(1500\)/);
    assert.match(source, /child\.once\('exit'/);
});

test('resolveDotnetExecutable uses the native host on each platform', () => {
    assert.equal(resolveDotnetExecutable('linux'), 'dotnet');
    assert.equal(resolveDotnetExecutable('darwin'), 'dotnet');
    assert.equal(
        resolveDotnetExecutable('win32', 'C:\\Program Files'),
        path.join('C:\\Program Files', 'dotnet', 'dotnet.exe')
    );
});
