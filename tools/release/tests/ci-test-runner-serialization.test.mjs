import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const workflow = readFileSync(resolve(repositoryRoot, '.github/workflows/ci.yml'), 'utf8');

test('CI runs the solution test projects serially', () => {
    const step = workflow.match(/- name: Run unit tests \(exclude integration\)[\s\S]*?(?=\n      - name:|$)/)?.[0] ?? '';

    assert.match(step, /dotnet test MulletaFlix\.sln/);
    assert.match(step, /--no-build/);
    assert.match(step, /-m:1/);
    assert.match(step, /FullyQualifiedName!~Integration/);
});

test('CI requires every privileged Linux restore boundary test to pass', () => {
    const step = workflow.match(/- name: Verify privileged Linux restore filesystem boundaries[\s\S]*?(?=\n      - name:|$)/)?.[0] ?? '';

    assert.match(step, /sudo .*dotnet/);
    assert.match(step, /--filter/);
    assert.match(step, /if missing or duplicates or unexpected or not_passed/);
    assert.match(step, /duplicates = \{/);
    assert.match(step, /outcome != "Passed"/);
    assert.match(step, /len\(actual\) != len\(expected\)/);

    const filter = step.match(/--filter '([^']+)'/)?.[1] ?? '';
    const selected = [...filter.matchAll(/FullyQualifiedName~([A-Za-z0-9_]+)/g)].map(([, name]) => name);
    const expectedBlock = step.match(/expected = \{([\s\S]*?)\n\s+\}/)?.[1] ?? '';
    const expected = [...expectedBlock.matchAll(/"([A-Za-z0-9_]+)"/g)].map(([, name]) => name);

    assert.equal(selected.length, 14);
    assert.equal(new Set(selected).size, selected.length, 'the Linux test filter must not select duplicate method names');
    assert.deepEqual([...selected].sort(), [...expected].sort(), 'the Linux filter and exact TRX expectation must stay in sync');

    const testDirectory = resolve(repositoryRoot, 'MulletaFlix-master/tests/Jellyfin.Server.Implementations.Tests/FullSystemBackup');
    const testSources = [
        'BackupRestoreDirectoryTests.cs',
        'BackupServiceTests.cs',
    ].map((file) => readFileSync(resolve(testDirectory, file), 'utf8'));
    const testDeclarations = testSources.flatMap((source) =>
        [...source.matchAll(/^((?:\s*\[[^\]\r\n]+\]\s*\r?\n)+)\s*public\s+(?:async\s+)?(?:void|Task(?:<[^>]+>)?)\s+([A-Za-z_]\w*)\s*\(/gm)]
            .filter(([, attributes]) => /(?:^|\r?\n)\s*\[(?:Fact|Theory)(?:Attribute)?(?:\([^\r\n]*\))?\]/.test(attributes))
            .map(([, , name]) => name));
    const declaredTests = new Set(testDeclarations);

    assert.deepEqual(
        expected.filter((name) => !declaredTests.has(name)),
        [],
        'every privileged Linux restore test must be a declared xUnit Fact or Theory',
    );
});
