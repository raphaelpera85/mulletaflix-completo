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
