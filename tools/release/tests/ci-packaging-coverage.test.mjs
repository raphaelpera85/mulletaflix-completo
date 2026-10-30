import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const workflow = await readFile(resolve(repositoryRoot, '.github/workflows/ci.yml'), 'utf8');
const installer = await readFile(resolve(repositoryRoot, 'MulletaFlix-packaging-master/install-python-if-missing.ps1'), 'utf8');
const windowsInstaller = await readFile(resolve(repositoryRoot, 'MulletaFlix-packaging-master/MulletaFlix-ux-custom/nsis/mulletaflix.nsi'), 'utf8');
const windowsProcessStopper = await readFile(resolve(repositoryRoot, 'MulletaFlix-packaging-master/MulletaFlix-ux-custom/nsis/stop-mulletaflix-processes.ps1'), 'utf8');
const stageBuilder = await readFile(resolve(repositoryRoot, 'MulletaFlix-packaging-master/build-stage-and-installer.ps1'), 'utf8');
const stageIntegrity = await readFile(resolve(repositoryRoot, 'MulletaFlix-packaging-master/scripts/Assert-StageIntegrity.ps1'), 'utf8');
const linuxInstaller = await readFile(resolve(repositoryRoot, 'tools/release/linux-install.sh'), 'utf8');
const linuxRuntimeCopy = await readFile(resolve(repositoryRoot, 'tools/release/copy-nebula-python-runtime.sh'), 'utf8');
const linuxDependencyTest = await readFile(resolve(repositoryRoot, 'tools/release/tests/linux-nebula-python-dependencies.test.sh'), 'utf8');
const linuxPackageTest = await readFile(resolve(repositoryRoot, 'tools/release/tests/linux-nebula-runtime-package.test.sh'), 'utf8');
const releaseWorkflow = await readFile(resolve(repositoryRoot, '.github/workflows/release.yml'), 'utf8');

test('Windows CI executes every packaging integrity script', async () => {
    const jobStart = workflow.indexOf('  windows-packaging-script-tests:');
    assert.notEqual(jobStart, -1, 'Windows packaging integrity job must exist');
    const nextJob = workflow.slice(jobStart + 1).search(/\r?\n  [a-z0-9_-]+:\r?\n/);
    const job = workflow.slice(jobStart, nextJob === -1 ? undefined : jobStart + 1 + nextJob);

    assert.match(job, /runs-on:\s*windows-latest/);
    assert.match(job, /shell:\s*pwsh/);
    assert.match(job, /timeout-minutes:\s*10/);

    const requiredTests = [
        'Assert-StageIntegrity.Tests.ps1',
        'Assert-UpdatePackageIntegrity.Tests.ps1',
        'RcloneBootstrap.Tests.ps1',
        'ServiceDataAcl.Tests.ps1',
        'ApplyUpdateFailure.Tests.ps1',
        'ApplyUpdateStartupFailure.Tests.ps1',
        'ApplyUpdateFailClosed.Tests.ps1',
        'StageBackup.Tests.ps1',
        'StageBuildOptions.Tests.ps1'
    ];

    for (const testFile of requiredTests) {
        assert.ok(job.includes(testFile), `${testFile} must run in the Windows CI job`);
        await readFile(resolve(repositoryRoot, 'MulletaFlix-packaging-master/scripts/tests', testFile), 'utf8');
    }

    assert.match(job, /shell:\s*powershell[\s\S]*ApplyUpdateFailClosed\.Tests\.ps1/);
});

test('Windows CI runs native server filesystem and restore tests', () => {
    const jobStart = workflow.indexOf('  server-windows-filesystem-tests:');
    assert.notEqual(jobStart, -1, 'Windows filesystem test job must exist');
    const nextJob = workflow.slice(jobStart + 1).search(/\r?\n  [a-z0-9_-]+:\r?\n/);
    const job = workflow.slice(jobStart, nextJob === -1 ? undefined : jobStart + 1 + nextJob);

    assert.match(job, /runs-on:\s*windows-latest/);
    assert.match(job, /timeout-minutes:\s*20/);
    assert.doesNotMatch(job, /^    (?:needs|services):/m);
    assert.match(job, /dotnet restore tests\/Jellyfin\.Server\.Implementations\.Tests\/Jellyfin\.Server\.Implementations\.Tests\.csproj/);
    assert.match(job, /FullyQualifiedName~FullSystemBackup\.BackupRestoreDirectoryTests\|FullyQualifiedName~FullSystemBackup\.BackupServiceTests/);
});

test('Release validates the actual update archive and clean installer before publishing', () => {
    const publishStep = releaseWorkflow.indexOf('- name: Publish GitHub Release and platform assets');
    const archiveValidation = releaseWorkflow.indexOf('- name: Validate update package against staged payload');
    const installerSmoke = releaseWorkflow.indexOf('- name: Smoke test clean Windows installer');
    const archiveCreation = releaseWorkflow.indexOf('- name: Create Update Archive');

    assert.ok(archiveCreation >= 0 && archiveValidation > archiveCreation, 'archive validation must follow package creation');
    assert.ok(installerSmoke > archiveValidation, 'installer smoke must follow archive integrity validation');
    assert.ok(publishStep > installerSmoke, 'publishing must run only after both artifact gates');
    assert.match(releaseWorkflow, /Assert-UpdatePackageIntegrity\.ps1 -StageDirectory stage -PackagePath dist\\mulletaflix-update-win-x64\.zip/);
    assert.match(releaseWorkflow, /Test-CleanInstaller\.ps1[\s\S]*?-ExpectedSha256 \$hash/);
    assert.match(releaseWorkflow, /Validate updater under production Windows PowerShell 5\.1[\s\S]*?shell:\s*powershell[\s\S]*?ApplyUpdateFailClosed\.Tests\.ps1/);
});

test('Nebula packaging installs and stages the hash-locked runtime dependencies', () => {
    assert.match(installer, /Tools\\NebulaPython\\requirements\.lock/);
    assert.match(installer, /--require-hashes/);
    assert.match(installer, /-m venv \$venvDirectory/);
    assert.match(installer, /Scripts\\python\.exe/);
    assert.match(installer, /& \$venvPython -m pip install/);
    assert.match(installer, /\(Join-Path \$runtimeDirectory 'python\.path'\),\s*\$venvPython/);
    assert.doesNotMatch(installer, /& \$Python -m pip install/);
    assert.doesNotMatch(installer, /LocalAppData.*Programs\\Python/s);
    assert.doesNotMatch(installer, /Get-Command python\.exe/);
    assert.match(stageBuilder, /'requirements\.in'.*'requirements\.txt'.*'requirements\.lock'/);
    assert.match(stageIntegrity, /Tools\\NebulaPython\\requirements\.lock/);
});

test('Nebula startup dependency guard never repairs a global Python interpreter', async () => {
    const guard = await readFile(resolve(repositoryRoot, 'MulletaFlix-master/Tools/NebulaPython/tools/check_deps.py'), 'utf8');
    assert.match(guard, /def _require_isolated_environment\(\)/);
    assert.match(guard, /sys\.prefix != sys\.base_prefix/);
    const dependencyGuard = guard.indexOf('def ensure_runtime_dependencies(');
    const isolatedCheck = guard.indexOf('_require_isolated_environment()', dependencyGuard);
    const lockedRepair = guard.indexOf('rc = _pip_install_locked(lock_path)', dependencyGuard);
    const compatibilityRepair = guard.indexOf('rc = _pip_install(" ".join(batchspecs))', dependencyGuard);
    assert.notEqual(dependencyGuard, -1);
    assert.ok(isolatedCheck > dependencyGuard, 'repair paths must be protected by the venv check');
    assert.ok(lockedRepair > isolatedCheck, 'locked repair must follow the venv check');
    assert.ok(compatibilityRepair > isolatedCheck, 'compatibility repair must follow the venv check');
});

test('Linux server package copies, verifies, and installs Nebula from the hash lock', () => {
    assert.match(linuxRuntimeCopy, /requirements\.in requirements\.txt requirements\.lock/);
    assert.match(linuxInstaller, /--require-hashes -r "\$nebula_python_dir\/requirements\.lock"/);
    assert.match(linuxDependencyTest, /--require-hashes/);
    assert.match(linuxDependencyTest, /requirements_lock/);
    assert.match(linuxPackageTest, /requirements\.in requirements\.txt requirements\.lock/);
    assert.match(releaseWorkflow, /server\/Tools\/NebulaPython\/requirements\.lock/);
});

test('Release workflow validates the updater and reads notes from the server documentation path', () => {
    assert.match(releaseWorkflow, /ApplyUpdateFailure\.Tests\.ps1/);
    assert.ok(releaseWorkflow.includes('-NotesPath "MulletaFlix-master/docs/releases/$tagVersion.md"'));
});

test('Release validation suites run in parallel and gate artifact publication', () => {
    const getJob = (name) => {
        const start = releaseWorkflow.indexOf(`  ${name}:`);
        assert.notEqual(start, -1, `release job ${name} must exist`);
        const remaining = releaseWorkflow.slice(start + 1);
        const next = remaining.search(/\r?\n  [a-z0-9_-]+:\r?\n/);
        return releaseWorkflow.slice(start, next === -1 ? undefined : start + 1 + next);
    };

    for (const jobName of ['validate-server', 'validate-nebula-python', 'validate-web', 'build-linux']) {
        assert.doesNotMatch(getJob(jobName), /^    needs:/m, `${jobName} must not wait for another validation job`);
    }

    assert.match(getJob('build-and-release'), /^    needs:\s*\[validate-server, validate-nebula-python, validate-web, build-linux\]/m);
});

test('Windows installer stops only processes owned by its install or data directory', () => {
    assert.equal((windowsInstaller.match(/ExecWait 'powershell\.exe[^\r\n]*stop-mulletaflix-processes\.ps1/g) ?? []).length, 2);
    assert.match(windowsProcessStopper, /ExecutablePath.*installRoot/s);
    assert.match(windowsProcessStopper, /ExecutablePath.*dataRoot/s);
    assert.match(windowsProcessStopper, /Stop-Process -Id/);
    assert.doesNotMatch(windowsInstaller, /taskkill[^\r\n]*\/(?:IM|ImageName)\s+(?:MulletaFlix|mysqld|mariadbd|rclone)/i);
});
