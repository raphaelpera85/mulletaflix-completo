import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const workflow = readFileSync(resolve(repositoryRoot, '.github/workflows/ci.yml'), 'utf8');
const packagingScript = readFileSync(resolve(repositoryRoot, 'MulletaFlix-packaging-master/build-stage-and-installer.ps1'), 'utf8');
const lifecycleTest = readFileSync(resolve(repositoryRoot, 'MulletaFlix-master/tests/Jellyfin.Server.Tests/Helpers/MariaDbProcessManagerLifecycleTests.cs'), 'utf8');
const processManager = readFileSync(resolve(repositoryRoot, 'MulletaFlix-master/Jellyfin.Server/Helpers/MariaDbProcessManager.cs'), 'utf8');

test('CI runs the real embedded MariaDB lifecycle test on Windows and requires Passed', () => {
    const job = workflow.match(/  embedded-mariadb-lifecycle-windows:[\s\S]*?(?=\n  [a-z0-9-]+:|$)/)?.[0] ?? '';

    assert.match(job, /runs-on: windows-latest/);
    assert.match(job, /\$runtimeVersion = '11\.4\.4'/);
    assert.match(job, /mariadb-\$runtimeVersion-winx64\.zip/);
    assert.match(job, /rest-api\/mariadb\/\$runtimeVersion\/mariadb-\$runtimeVersion-winx64\.zip/);
    assert.match(job, /mysqld\.exe/);
    assert.match(job, /mysql_install_db\.exe/);
    assert.match(job, /mysqladmin\.exe/);
    assert.match(job, /MariaDbProcessManagerLifecycleTests\.StartStopStart_UsesTheSameIsolatedDataDirectory/);
    assert.match(job, /--no-build --no-restore -m:1/);
    assert.match(job, /mariadb-lifecycle\.trx/);
    assert.match(job, /outcome -ne 'Passed'/);
    assert.match(lifecycleTest, /Assert\.SkipUnless\(OperatingSystem\.IsWindows\(\)/);
    assert.match(lifecycleTest, /Path\.GetTempPath\(\)/);
    assert.match(lifecycleTest, /MulletaFlix_DATABASE_NAME/);
    assert.match(lifecycleTest, /SELECT COUNT\(\*\)[\s\S]*markerTable/);
    assert.match(lifecycleTest, /requireEmbeddedProcess: true/);
    assert.match(processManager, /requireEmbeddedProcess && _mariaDbProcess is null[\s\S]*refusing to initialize databases/);
    assert.match(lifecycleTest, /StartMariaDbAsync[\s\S]*StopMariaDbAsync[\s\S]*StartMariaDbAsync[\s\S]*StopMariaDbAsync/);
    assert.match(packagingScript, /mariadb\/11\.4\.4\/mariadb-11\.4\.4-winx64\.zip/);
});
