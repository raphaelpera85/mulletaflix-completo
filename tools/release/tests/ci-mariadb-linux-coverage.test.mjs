import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const workflow = await readFile(resolve(repositoryRoot, '.github/workflows/ci.yml'), 'utf8');
const userManager = await readFile(
    resolve(repositoryRoot, 'MulletaFlix-master/Jellyfin.Server.Implementations/Users/UserManager.cs'),
    'utf8');
const userManagerTests = await readFile(
    resolve(repositoryRoot, 'MulletaFlix-master/tests/Jellyfin.Server.Implementations.Tests/Users/UserManagerNormalizedUsernameTests.cs'),
    'utf8');

function getJob(name) {
    const jobStart = workflow.indexOf(`  ${name}:`);
    assert.notEqual(jobStart, -1, `${name} job must exist`);
    const bodyStart = workflow.indexOf('\n', jobStart) + 1;
    const nextJob = workflow.slice(bodyStart).search(/\r?\n  [a-z0-9_-]+:\r?\n/);
    return workflow.slice(jobStart, nextJob === -1 ? undefined : bodyStart + nextJob);
}

test('Linux MariaDB CI executes username schema tests with case-sensitive table names', () => {
    const job = getJob('user-mariadb-linux-tests');

    assert.match(job, /runs-on:\s*ubuntu-latest/);
    assert.match(job, /image:\s*mariadb:11\.4/);
    assert.match(job, /33099:3306/);
    assert.match(job, /MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING:\s*"?Server=127\.0\.0\.1;Port=33099;/);
    assert.match(job, /FullyQualifiedName~UserManagerNormalizedUsernameTests\|FullyQualifiedName~BillingDatabaseTests/);
    assert.match(userManager, /BINARY `TABLE_NAME` = BINARY 'Users'/);
    assert.match(userManagerTests, /EnsureSchemaCreated_UsesEfMappedUsersTableWithBinaryUsernameCollations/);
    assert.match(userManagerTests, /@@lower_case_table_names/);
});

test('Server integration suite runs in CI against isolated MariaDB', () => {
    const job = getJob('server-mariadb-integration-tests');
    const project = 'tests/Jellyfin.Server.Integration.Tests/Jellyfin.Server.Integration.Tests.csproj';

    assert.match(job, /runs-on:\s*ubuntu-latest/);
    assert.match(job, /image:\s*mariadb:11\.4\.13/);
    assert.match(job, /MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING:/);
    assert.match(job, new RegExp(project.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')));
    assert.match(job, /dotnet test[\s\S]*Jellyfin\.Server\.Integration\.Tests\.csproj[\s\S]*--verbosity minimal/);
    assert.doesNotMatch(job, /--filter\b/, 'The MariaDB job must execute the entire integration project');
});

test('Complete MariaDB, MongoDB, and filesystem integration suite runs in CI', () => {
    const job = getJob('full-system-recovery-exercise');
    const project = 'tests/IntroSkipper.Integration.Tests/IntroSkipper.Integration.Tests.csproj';

    assert.match(job, /runs-on:\s*ubuntu-latest/);
    assert.match(job, /image:\s*mongo:7\.0/);
    assert.match(job, /image:\s*mariadb:11\.4\.13/);
    assert.match(job, /MULLETAFLIX_TEST_MONGODB_CONNECTION_STRING:/);
    assert.match(job, /MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING:/);
    assert.match(job, /dotnet test[\s\S]*IntroSkipper\.Integration\.Tests\.csproj[\s\S]*--verbosity minimal/);
    assert.doesNotMatch(job, /--filter\b/, 'The integration job must execute every test in the project');
    assert.match(job, /timeout-minutes:\s*30/);
    assert.match(workflow, new RegExp(project.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')));
});
