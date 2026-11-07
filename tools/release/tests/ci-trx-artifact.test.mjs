import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const workflow = await readFile(resolve(repositoryRoot, '.github/workflows/ci.yml'), 'utf8');

function getJob(name) {
    const jobStart = workflow.indexOf(`  ${name}:`);
    assert.notEqual(jobStart, -1, `${name} job must exist`);
    const bodyStart = workflow.indexOf('\n', jobStart) + 1;
    const nextJob = workflow.slice(bodyStart).search(/\r?\n  [a-z0-9_-]+:\r?\n/);
    return workflow.slice(jobStart, nextJob === -1 ? undefined : bodyStart + nextJob);
}

test('every .NET server test command publishes TRX even when a test fails', () => {
    const jobs = [
        'server-build-test',
        'server-windows-filesystem-tests',
        'nebula-representative-tests',
        'nebula-mongo-integration-tests',
        'user-mariadb-linux-tests',
        'server-mariadb-integration-tests',
        'full-system-recovery-exercise'
    ];
    const artifactNames = new Set();

    for (const name of jobs) {
        const job = getJob(name);
        const testCommands = job.match(/^\s+dotnet test\b/gm) ?? [];
        const trxLoggers = job.match(/^\s+--logger "trx;LogFilePrefix=[a-z0-9-]+"/gm) ?? [];

        assert.ok(testCommands.length > 0, `${name} must run .NET tests`);
        assert.equal(trxLoggers.length, testCommands.length, `${name} must add one unique TRX logger per dotnet test command`);
        assert.equal((job.match(/^\s+--results-directory TestResults\s*$/gm) ?? []).length, testCommands.length);
        assert.match(job, /- name: Upload [^\r\n]+ test results\r?\n\s+if: always\(\)/);
        assert.match(job, /uses: actions\/upload-artifact@v4/);
        assert.match(job, /path: MulletaFlix-master\/TestResults\/\*\*\/\*\.trx/);
        assert.match(job, /if-no-files-found: warn/);

        const artifactName = job.match(/^\s+name: ([^\r\n]+test-results[^\r\n]*)$/m)?.[1];
        assert.ok(artifactName, `${name} must use a distinct TRX artifact name`);
        assert.ok(!artifactNames.has(artifactName), `${artifactName} must be unique across jobs`);
        artifactNames.add(artifactName);
    }
});
