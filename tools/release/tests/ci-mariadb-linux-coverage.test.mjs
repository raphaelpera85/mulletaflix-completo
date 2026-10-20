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

test('Linux MariaDB CI executes username schema tests with case-sensitive table names', () => {
    const jobStart = workflow.indexOf('  user-mariadb-linux-tests:');
    assert.notEqual(jobStart, -1, 'Linux MariaDB test job must exist');
    const nextJob = workflow.slice(jobStart + 1).search(/\n  [a-z0-9_-]+:\n/);
    const job = workflow.slice(jobStart, nextJob === -1 ? undefined : jobStart + 1 + nextJob);

    assert.match(job, /runs-on:\s*ubuntu-latest/);
    assert.match(job, /image:\s*mariadb:11\.4/);
    assert.match(job, /33099:3306/);
    assert.match(job, /MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING:\s*"?Server=127\.0\.0\.1;Port=33099;/);
    assert.match(job, /FullyQualifiedName~UserManagerNormalizedUsernameTests\|FullyQualifiedName~BillingDatabaseTests/);
    assert.match(userManager, /BINARY `TABLE_NAME` = BINARY 'Users'/);
    assert.match(userManagerTests, /EnsureSchemaCreated_UsesEfMappedUsersTableWithBinaryUsernameCollations/);
    assert.match(userManagerTests, /@@lower_case_table_names/);
});
