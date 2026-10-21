import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const builder = await readFile(resolve(repositoryRoot, 'build-update-package.ps1'), 'utf8');

test('update package output path is rooted before changing to the temporary archive directory', () => {
    const normalization = builder.indexOf('$OutputDir = if ([System.IO.Path]::IsPathRooted($OutputDir))');
    const workingDirectoryChange = builder.indexOf('Set-Location $tempPackageDir');

    assert.notEqual(normalization, -1, 'OutputDir must be normalized to an absolute path');
    assert.ok(workingDirectoryChange > normalization, 'normalization must happen before Set-Location');
    assert.match(builder, /GetFullPath\(\(Join-Path \$projectRoot \$OutputDir\)\)/);
    assert.match(builder, /\$zipPath = Join-Path \$OutputDir \$zipName/);
});

test('update package uses the curated Nebula runtime copier and replaces staged scratch files', () => {
    const destinationCleanup = builder.indexOf('Remove-Item -LiteralPath $nebulaPythonDest -Recurse -Force');
    const runtimeCopy = builder.indexOf("tools\\release\\copy-nebula-python-runtime.ps1");

    assert.notEqual(destinationCleanup, -1, 'stale staged runtime files must be removed before copying');
    assert.ok(runtimeCopy > destinationCleanup, 'the curated copier must run after removing stale runtime files');
    assert.match(builder, /-SourceDirectory \$nebulaPythonSource/);
    assert.match(builder, /-DestinationDirectory \$nebulaPythonDest/);
    assert.doesNotMatch(builder, /robocopy\.exe \$nebulaPythonSource/);
    assert.match(builder, /\.ruff_cache\/\.coverage/);
});
