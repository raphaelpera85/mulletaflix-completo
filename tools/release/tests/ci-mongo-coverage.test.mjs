import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const workflow = await readFile(resolve(repositoryRoot, '.github/workflows/ci.yml'), 'utf8');
const testRoot = resolve(repositoryRoot, 'MulletaFlix-master/tests/Jellyfin.Server.Implementations.Tests');

async function findCSharpFiles(directory) {
    const entries = await readdir(directory, { withFileTypes: true });
    const nested = await Promise.all(entries.map(entry => {
        const path = resolve(directory, entry.name);
        return entry.isDirectory() ? findCSharpFiles(path) : entry.isFile() && entry.name.endsWith('.cs') ? [path] : [];
    }));

    return nested.flat();
}

const mongoTestFiles = await findCSharpFiles(testRoot);
const knownMongoTestClasses = [
    'MongoTopologyCapabilityTests',
    'NebulaPlaybackRecoveryMongoTests',
    'NebulaQueueConcurrencyMongoTests',
    'NebulaSupabaseBackupBotTokensMongoTests',
    'NebulaSupabaseRestoreMongoTests'
];

test('Mongo CI selects every test tagged RequiresMongo instead of a partial class list', async () => {
    const jobStart = workflow.indexOf('  nebula-mongo-integration-tests:');
    assert.notEqual(jobStart, -1, 'Mongo integration job must exist');
    const nextJob = workflow.slice(jobStart + 1).search(/\n  [a-z0-9_-]+:\n/);
    const job = workflow.slice(jobStart, nextJob === -1 ? undefined : jobStart + 1 + nextJob);

    assert.match(job, /timeout-minutes:\s*15/);
    assert.match(job, /image:\s*mongo:7\.0/);
    assert.match(job, /MULLETAFLIX_TEST_MONGODB_CONNECTION_STRING:\s*mongodb:\/\/127\.0\.0\.1:27099/);
    assert.match(job, /--filter\s+"Category=RequiresMongo"/);

    const mongoSources = await Promise.all(mongoTestFiles.map(path => readFile(path, 'utf8')));
    const taggedClasses = mongoSources.flatMap(source =>
        [...source.matchAll(/\[Trait\("Category",\s*"RequiresMongo"\)\]\s*(?:\[[^\]]+\]\s*)*public\s+(?:sealed\s+|abstract\s+)?class\s+(\w+)/g)].map(match => match[1]));
    for (const className of knownMongoTestClasses) {
        assert.ok(taggedClasses.includes(className), `${className} must remain tagged RequiresMongo`);
    }
});
