$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$modulePath = Join-Path $repositoryRoot 'tools\release\ReleaseArtifactValidation.psm1'
$publisherPath = Join-Path $repositoryRoot 'publish-release.ps1'

if (-not (Test-Path -LiteralPath $modulePath)) {
    throw 'Release artifact validation module is required.'
}

Import-Module $modulePath -Force

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('mflx-release-manifest-' + [guid]::NewGuid().ToString('N'))
[void][System.IO.Directory]::CreateDirectory($testRoot)
$sourceCommit = '0123456789abcdef0123456789abcdef01234567'

function Assert-Rejected([scriptblock]$Action, [string]$Case) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (-not $rejected) { throw "$Case was accepted." }
}

try {
    $zip = Join-Path $testRoot 'mulletaflix-update-win-x64.zip'
    $installer = Join-Path $testRoot 'mulletaflix_12.1.59_windows-x64.exe'
    $linuxPackage = Join-Path $testRoot 'mulletaflix_12.1.59_linux-x64.tar.gz'
    $manifestPath = Join-Path $testRoot 'mulletaflix-release-manifest.json'
    [System.IO.File]::WriteAllBytes($zip, [byte[]](1, 2, 3, 4))
    [System.IO.File]::WriteAllBytes($installer, [byte[]](5, 6, 7, 8))
    [System.IO.File]::WriteAllBytes($linuxPackage, [byte[]](9, 10, 11, 12))

    New-MulletaFlixReleaseManifest -Version '12.1.59' -SourceCommit $sourceCommit -ZipPath $zip -InstallerPath $installer -AdditionalAssetPaths @($linuxPackage) -OutputPath $manifestPath | Out-Null
    $manifest = Assert-MulletaFlixReleaseManifest -ManifestPath $manifestPath -Version '12.1.59' -SourceCommit $sourceCommit -AssetPaths @($zip, $installer, $linuxPackage)
    if ($manifest.artifacts.Count -ne 3) { throw 'Manifest must bind the ZIP, installer, and version-matched cross-platform packages.' }
    $unboundAsset = Join-Path $testRoot 'mulletaflix_12.1.59_macos-x64.dmg'
    [System.IO.File]::WriteAllBytes($unboundAsset, [byte[]](13, 14, 15, 16))
    Assert-Rejected { Assert-MulletaFlixReleaseManifest -ManifestPath $manifestPath -Version '12.1.59' -SourceCommit $sourceCommit -AssetPaths @($zip, $installer, $linuxPackage, $unboundAsset) } 'selected asset missing from manifest'

    Assert-Rejected { Assert-MulletaFlixReleaseManifest -ManifestPath $manifestPath -Version '12.1.58' -SourceCommit $sourceCommit -AssetPaths @($zip, $installer, $linuxPackage) } 'wrong release version'
    Assert-Rejected { Assert-MulletaFlixReleaseManifest -ManifestPath $manifestPath -Version '12.1.59' -SourceCommit ('f' * 40) -AssetPaths @($zip, $installer, $linuxPackage) } 'wrong source commit'

    $stageRoot = Join-Path $testRoot 'stage-order-fixture'
    [void][System.IO.Directory]::CreateDirectory((Join-Path $stageRoot 'mariadb\bin'))
    [System.IO.File]::WriteAllBytes((Join-Path $stageRoot 'MulletaFlix.dll'), [byte[]](1, 2, 3))
    [System.IO.File]::WriteAllBytes((Join-Path $stageRoot 'mariadb\bin\mariadb.exe'), [byte[]](4, 5, 6))
    [System.IO.File]::WriteAllBytes((Join-Path $stageRoot 'mariadb\bin\mariadb-admin.exe'), [byte[]](7, 8, 9))
    $stageRecords = @(Get-ChildItem -LiteralPath $stageRoot -File -Recurse | ForEach-Object {
        $relative = $_.FullName.Substring($stageRoot.Length).TrimStart('\').Replace('\', '/')
        [ordered]@{
            path = $relative
            sizeBytes = [long]$_.Length
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
    [array]::Reverse($stageRecords)
    $stageManifestPath = Get-MulletaFlixStageManifestPath -StageDirectory $stageRoot
    $stageManifest = [ordered]@{
        schemaVersion = 1
        product = 'mulletaflix-server-stage'
        version = '12.1.59'
        sourceCommit = $sourceCommit
        sourceTreeClean = $true
        # Deliberately reverse the records to prove validation doesn't rely on
        # PowerShell's culture-sensitive filesystem sort order.
        files = $stageRecords
    }
    [System.IO.File]::WriteAllText($stageManifestPath, ($stageManifest | ConvertTo-Json -Depth 8))
    $verifiedStage = Assert-MulletaFlixStageManifest -StageDirectory $stageRoot -Version '12.1.59' -SourceCommit $sourceCommit
    if ($verifiedStage.files.Count -ne 3) { throw 'Stage manifest validation must accept the same files in a different order.' }
    [System.IO.File]::AppendAllText((Join-Path $stageRoot 'mariadb\bin\mariadb.exe'), 'tamper')
    Assert-Rejected { Assert-MulletaFlixStageManifest -StageDirectory $stageRoot -Version '12.1.59' -SourceCommit $sourceCommit } 'stage file changed after its manifest'

    $gitRoot = Join-Path $testRoot 'git-source'
    [void][System.IO.Directory]::CreateDirectory($gitRoot)
    & git init -q $gitRoot
    if ($LASTEXITCODE -ne 0) { throw 'Could not create isolated Git fixture.' }
    & git -C $gitRoot config user.name 'MulletaFlix Test'
    & git -C $gitRoot config user.email 'tests@example.invalid'
    [System.IO.File]::WriteAllText((Join-Path $gitRoot 'source.txt'), 'source')
    & git -C $gitRoot add source.txt
    & git -C $gitRoot commit -q -m 'fixture'
    if ($LASTEXITCODE -ne 0) { throw 'Could not commit isolated Git fixture.' }
    $cleanFixtureCommit = Assert-MulletaFlixGitWorktreeClean -RepositoryRoot $gitRoot
    if ($cleanFixtureCommit -notmatch '^[0-9a-f]{40}$') { throw 'Clean worktree did not return the full commit id.' }
    [System.IO.File]::AppendAllText((Join-Path $gitRoot 'source.txt'), 'changed')
    Assert-Rejected { Assert-MulletaFlixGitWorktreeClean -RepositoryRoot $gitRoot } 'dirty source worktree'

    [System.IO.File]::AppendAllText($installer, 'stale')
    Assert-Rejected { Assert-MulletaFlixReleaseManifest -ManifestPath $manifestPath -Version '12.1.59' -SourceCommit $sourceCommit -AssetPaths @($zip, $installer, $linuxPackage) } 'asset whose bytes changed after packaging'
    [System.IO.File]::WriteAllBytes($installer, [byte[]](5, 6, 7, 8))

    $staleZip = Join-Path $testRoot 'stale\mulletaflix-update-win-x64.zip'
    [void][System.IO.Directory]::CreateDirectory((Split-Path -Parent $staleZip))
    [System.IO.File]::WriteAllBytes($staleZip, [byte[]](9, 9, 9, 9))
    Assert-Rejected { Select-MulletaFlixReleaseAssets -Version '12.1.59' -ZipPath $zip -AssetPaths @($installer, $staleZip) -AssetsDirectory $testRoot -ManifestPath $manifestPath -SourceCommit $sourceCommit } 'duplicate asset names with different bytes'

    Assert-Rejected { Select-MulletaFlixReleaseAssets -Version '12.1.59' -ZipPath (Join-Path $testRoot 'missing.zip') -AssetPaths @($installer) -AssetsDirectory $testRoot -ManifestPath $manifestPath -SourceCommit $sourceCommit } 'missing required update package'

    $publisher = [System.IO.File]::ReadAllText($publisherPath)
    $preflightIndex = $publisher.IndexOf('Select-MulletaFlixReleaseAssets', [System.StringComparison]::Ordinal)
    $mutationMatch = [regex]::Match($publisher, 'Invoke-RestMethod[^\r\n]*-Method\s+(?:Post|Patch|Delete)')
    $firstMutationIndex = if ($mutationMatch.Success) { $mutationMatch.Index } else { -1 }
    if ($preflightIndex -lt 0 -or $null -eq $firstMutationIndex -or $preflightIndex -gt $firstMutationIndex) {
        throw 'Release assets must be fully validated before any GitHub mutation.'
    }
    if ($publisher -notmatch 'rev-parse\s+"\$Tag\^\{commit\}"' -or $publisher -notmatch '\$tagCommit\s+-ne\s+\$sourceCommit') {
        throw 'The release tag must resolve to the exact source commit bound by the build manifest.'
    }
    if ($publisher -notmatch '\$SkipAssets\s+-and\s+\$existingRelease\)\s*\{\s*\$existingRelease\.target_commitish\s*\}') {
        throw 'Notes-only release updates must preserve the existing target commitish without relying on an uninitialized sourceCommit.'
    }
    if ($publisher -notmatch 'draft\s*=\s*\$draftState' -or $publisher -notmatch '\$draftState\s*=\s*\$true') {
        throw 'New releases must remain draft until every asset is uploaded.'
    }
    $uploadIndex = $publisher.IndexOf('$uploadResponse =', [System.StringComparison]::Ordinal)
    $publishPatchIndex = $publisher.LastIndexOf('-Method Patch', [System.StringComparison]::Ordinal)
    if ($uploadIndex -lt 0 -or $publishPatchIndex -le $uploadIndex) {
        throw 'A new release must be published only after the asset upload completes.'
    }
    if ($publisher -notmatch 'ComputeHash\(\$uploadStream\)' -or $publisher -notmatch 'remote name/size verification' -or
        $publisher.LastIndexOf('remote name/size verification', [System.StringComparison]::Ordinal) -gt $publishPatchIndex) {
        throw 'Assets must be rehashed on the upload stream and verified remotely before a draft is published.'
    }

    Write-Output 'Release manifest version, commit, hashes, duplicate names, required ZIP, and publication ordering tests passed.'
}
finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Module ReleaseArtifactValidation -ErrorAction SilentlyContinue
}
