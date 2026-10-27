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
    assert.match(job, /MULLETAFLIX_TEST_MARIADB_CONFIRM_DISPOSABLE:\s*I_ACKNOWLEDGE_TESTS_CREATE_AND_DROP_SCHEMAS/);
    assert.match(job, /FullyQualifiedName~UserManagerNormalizedUsernameTests\|FullyQualifiedName~BillingDatabaseTests/);
    assert.match(userManager, /BINARY `TABLE_NAME` = BINARY 'Users'/);
    assert.match(userManagerTests, /EnsureSchemaCreated_UsesEfMappedUsersTableWithBinaryUsernameCollations/);
    assert.match(userManagerTests, /@@lower_case_table_names/);
});

test('Linux MariaDB CI executes restore crash-recovery integration test', () => {
    const jobStart = workflow.indexOf('  user-mariadb-linux-tests:');
    const nextJob = workflow.slice(jobStart + 1).search(/\n  [a-z0-9_-]+:\n/);
    const job = workflow.slice(jobStart, nextJob === -1 ? undefined : jobStart + 1 + nextJob);

    assert.match(job, /dotnet restore tests\/IntroSkipper\.Integration\.Tests\/IntroSkipper\.Integration\.Tests\.csproj/);
    assert.match(job, /FullyQualifiedName~RestoreBackupAsync_ReplaysAfterProcessTerminationBetweenDatabaseAndFileCommit/);
    assert.match(job, /FullyQualifiedName~RestoreBackupAsync_RestoresDatabaseRowsOnDisposableMariaDb/);
    assert.match(job, /FullyQualifiedName~IntroSkipperMariaDbTests\.SchemaCreationTargetsTheDedicatedDatabase/);
    assert.match(job, /FullyQualifiedName~IntroSkipperBackupParticipantTests/);
    assert.match(job, /FullyQualifiedName~SaveImagesAsync_ConcurrentRefreshesForSameItem_PersistOneCompleteImageSet/);
    assert.match(job, /FullyQualifiedName~RefreshImages_PersistsSelectedPortugueseCandidateToMariaDb/);
    assert.match(job, /FullyQualifiedName~SaveImagesAsync_WaitsForDatabaseAdvisoryLockHeldByAnotherSession/);
    assert.match(job, /FullyQualifiedName~UpdateInheritedValues_WaitsForDatabaseAdvisoryLockHeldByAnotherSession/);
    assert.match(job, /FullyQualifiedName~DeleteItem_RemovesPeopleRowsMadeOrphanByDeletedItem/);
    assert.match(job, /FullyQualifiedName~DeleteItem_ConcurrentWithUpdatePeople_DoesNotLeaveOrphanPeople/);
    assert.match(job, /FullyQualifiedName~DeleteItem_WaitsForDatabaseAdvisoryLockHeldByAnotherSession/);
    assert.match(job, /FullyQualifiedName~FixDatesImageMigration_WaitsForAllImageAdvisoryLocks/);
    assert.match(job, /FullyQualifiedName~GetNextUpSeriesKeys_WithEqualLastPlayedDates_AppliesStableTieBreakBeforeLimit/);
});

test('Linux MariaDB CI boots the full server after interrupted-restore replay', () => {
    const jobStart = workflow.indexOf('  user-mariadb-linux-tests:');
    const nextJob = workflow.slice(jobStart + 1).search(/\n  [a-z0-9_-]+:\n/);
    const job = workflow.slice(jobStart, nextJob === -1 ? undefined : jobStart + 1 + nextJob);

    assert.match(job, /apt-get install --yes mariadb-client/);
    assert.match(job, /dotnet restore tests\/Jellyfin\.Server\.Integration\.Tests\/Jellyfin\.Server\.Integration\.Tests\.csproj/);
    assert.match(job, /FullyQualifiedName~StartupBackupRecoveryTests\.CreateHost_ReplaysInterruptedRestoreBeforeMigrationsAndCompletesStartup/);
});

test('Linux MariaDB CI restarts only its service container during image persistence', () => {
    const jobStart = workflow.indexOf('  user-mariadb-linux-tests:');
    const nextJob = workflow.slice(jobStart + 1).search(/\n  [a-z0-9_-]+:\n/);
    const job = workflow.slice(jobStart, nextJob === -1 ? undefined : jobStart + 1 + nextJob);

    assert.match(job, /docker ps --filter publish=33099/);
    assert.match(job, /Expected one MariaDB service container on port 33099/);
    assert.match(job, /\$image\" != \"mariadb:11\.4\"/);
    assert.match(job, /MULLETAFLIX_TEST_MARIADB_RESTART_READY_MARKER/);
    assert.match(job, /FullyQualifiedName~SaveImagesAsync_RetriesAfterMariaDbProcessRestartDuringDelete/);
    assert.match(job, /docker kill --signal KILL \"\$container\"/);
    assert.match(job, /docker start \"\$container\"/);
    assert.match(job, /mariadb-admin --user=root ping --silent/);
    assert.match(job, /MULLETAFLIX_TEST_MARIADB_RESTART_MARKER/);
});
