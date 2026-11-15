import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const serverRoot = resolve(repositoryRoot, 'MulletaFlix-master');
const workflow = readFileSync(resolve(repositoryRoot, '.github/workflows/ci.yml'), 'utf8');
const solution = readFileSync(resolve(serverRoot, 'MulletaFlix.sln'), 'utf8');

function findProjects(directory, results = []) {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
        if (entry.isDirectory()) {
            if (!['bin', 'obj', '.git'].includes(entry.name)) {
                findProjects(resolve(directory, entry.name), results);
            }
        } else if (entry.name.endsWith('.csproj')) {
            results.push(resolve(directory, entry.name));
        }
    }

    return results;
}

const solutionProjects = new Set(
    [...solution.matchAll(/^Project\("[^"]+"\)\s*=\s*"[^"]+",\s*"([^"]+\.csproj)"/gmi)]
        .map(([, project]) => resolve(serverRoot, project.split(/[\\/]/).join(sep))));
const outsideSolutionProjects = findProjects(serverRoot)
    .filter((project) => !solutionProjects.has(project))
    .map((project) => relative(serverRoot, project).split(sep).join('/'))
    .sort();

test('CI audits every server csproj, including projects outside the solution', () => {
    const jobStart = workflow.indexOf('  server-build-test:');
    assert.notEqual(jobStart, -1, 'server build and test job must exist');
    const nextJob = workflow.slice(jobStart + 1).search(/\n  [a-z0-9_-]+:\n/);
    const job = workflow.slice(jobStart, nextJob === -1 ? undefined : jobStart + 1 + nextJob);

    assert.match(job, /dotnet restore MulletaFlix\.sln '-p:WarningsAsErrors=NU1900%3BNU1901%3BNU1902%3BNU1903%3BNU1904%3BNU1905'/);
    assert.match(job, /dotnet list MulletaFlix\.sln package --vulnerable --include-transitive/);

    const supplementalStep = job.match(/- name: Audit NuGet dependencies outside solution[\s\S]*?(?=\n      - name:|$)/)?.[0] ?? '';
    assert.notEqual(supplementalStep, '', 'out-of-solution audit step must exist');

    const loopBody = supplementalStep.match(/; do\r?\n([\s\S]*?)\r?\n\s*done/)?.[1] ?? '';
    assert.match(supplementalStep, /find \. -type f -name ['"]\*\.csproj['"]/);
    assert.match(supplementalStep, /solution_projects=\$\(sed .*MulletaFlix\.sln\)/);
    assert.match(supplementalStep, /grep -Fqi .*normalized.*solution_projects/);
    assert.match(supplementalStep, /normalized=\$\{project#\.\/\}/);
    assert.deepEqual(outsideSolutionProjects, [
        'Compatibility/Jellyfin.Data/Jellyfin.Data.csproj',
        'Compatibility/Jellyfin.Database.Implementations/Jellyfin.Database.Implementations.csproj',
        'Compatibility/Jellyfin.Extensions/Jellyfin.Extensions.csproj',
        'fuzz/Emby.Server.Implementations.Fuzz/Emby.Server.Implementations.Fuzz.csproj',
        'fuzz/Jellyfin.Api.Fuzz/Jellyfin.Api.Fuzz.csproj',
        'tests/Jellyfin.Server.StartupRecoveryProbe/Jellyfin.Server.StartupRecoveryProbe.csproj'
    ]);
    assert.match(loopBody, /dotnet restore "\$project" '-p:WarningsAsErrors=NU1900%3BNU1901%3BNU1902%3BNU1903%3BNU1904%3BNU1905'/);
    assert.match(loopBody, /dotnet list "\$project" package --vulnerable --include-transitive --no-restore/);
});
