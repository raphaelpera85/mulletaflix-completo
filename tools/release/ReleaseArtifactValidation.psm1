Set-StrictMode -Version Latest

function Get-MulletaFlixReleaseSemVer {
    param([Parameter(Mandatory = $true)][string]$Value)

    $match = [regex]::Match($Value, '^v?(\d+\.\d+\.\d+)$')
    if (-not $match.Success) {
        throw "Invalid MulletaFlix release version: $Value"
    }

    return $match.Groups[1].Value
}

function Get-MulletaFlixAssetRecord {
    param([Parameter(Mandatory = $true)][string]$Path)

    $item = Get-Item -LiteralPath $Path -ErrorAction Stop
    if ($item.PSIsContainer) {
        throw "Release asset is not a file: $Path"
    }

    return [ordered]@{
        name = $item.Name
        sizeBytes = [long]$item.Length
        sha256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function Get-MulletaFlixBinaryVersion {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Path)

    $item = Get-Item -LiteralPath $Path -ErrorAction Stop
    $fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($item.FullName).FileVersion
    $match = [regex]::Match($fileVersion, '^(\d+\.\d+\.\d+)(?:\.\d+)?$')
    if (-not $match.Success) {
        throw "Binary does not contain a three-part MulletaFlix version: $($item.Name)"
    }

    return $match.Groups[1].Value
}

function Get-MulletaFlixGitContext {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $commit = (& git -C $RepositoryRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'Could not resolve the full source commit.'
    }

    $changes = @(& git -C $RepositoryRoot status --porcelain --untracked-files=all)
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not inspect the source working tree.'
    }

    return [pscustomobject]@{
        Commit = $commit.ToLowerInvariant()
        IsClean = ($changes.Count -eq 0)
    }
}

function Assert-MulletaFlixArtifactMatchesManifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)]$ManifestRecord
    )

    $resolved = (Get-Item -LiteralPath $Path -ErrorAction Stop).FullName
    $stream = [System.IO.File]::Open($resolved, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    try {
        $length = [long]$stream.Length
        $algorithm = [System.Security.Cryptography.SHA256]::Create()
        try {
            $digest = [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
        }
        finally {
            $algorithm.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }

    if ($length -ne [long]$ManifestRecord.sizeBytes -or $digest -cne [string]$ManifestRecord.sha256) {
        throw "Artifact changed after manifest validation: $resolved"
    }

    return $resolved
}

function Assert-MulletaFlixGitWorktreeClean {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $context = Get-MulletaFlixGitContext -RepositoryRoot $RepositoryRoot
    if (-not $context.IsClean) {
        throw 'Release build requires a clean Git working tree; build from a clean commit checkout.'
    }

    return $context.Commit
}

function Get-MulletaFlixStageManifestPath {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$StageDirectory)

    $stage = [System.IO.Path]::GetFullPath($StageDirectory).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    return "$stage.mulletaflix-stage-manifest.json"
}

function Get-MulletaFlixStageFileRecords {
    param([Parameter(Mandatory = $true)][string]$StageDirectory)

    $root = [System.IO.Path]::GetFullPath($StageDirectory).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        throw "Stage directory does not exist: $StageDirectory"
    }

    $files = Get-ChildItem -LiteralPath $root -File -Recurse -Force -ErrorAction Stop
    $records = foreach ($file in $files) {
        if (($file.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Stage contains a reparse point: $($file.Name)"
        }

        $relative = $file.FullName.Substring($root.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar).Replace('\', '/')
        [ordered]@{
            path = $relative
            sizeBytes = [long]$file.Length
            sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }

    return @($records | Sort-Object { $_.path })
}

function New-MulletaFlixStageManifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$StageDirectory,
        [Parameter(Mandatory = $true)][string]$Version,
        [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-fA-F]{40}$')][string]$SourceCommit,
        [Parameter(Mandatory = $true)][bool]$SourceTreeClean
    )

    $normalizedVersion = Get-MulletaFlixReleaseSemVer $Version
    $serverDll = Join-Path $StageDirectory 'MulletaFlix.dll'
    $actualVersion = Get-MulletaFlixBinaryVersion -Path $serverDll
    if ($actualVersion -ne $normalizedVersion) {
        throw "Stage version $actualVersion does not match requested version $normalizedVersion."
    }

    $manifest = [ordered]@{
        schemaVersion = 1
        product = 'mulletaflix-server-stage'
        version = $normalizedVersion
        sourceCommit = $SourceCommit.ToLowerInvariant()
        sourceTreeClean = $SourceTreeClean
        files = @(Get-MulletaFlixStageFileRecords -StageDirectory $StageDirectory)
    }
    $manifestPath = Get-MulletaFlixStageManifestPath -StageDirectory $StageDirectory
    [System.IO.File]::WriteAllText($manifestPath, (($manifest | ConvertTo-Json -Depth 12) + [Environment]::NewLine), [System.Text.UTF8Encoding]::new($false))
    return Get-Item -LiteralPath $manifestPath
}

function Assert-MulletaFlixStageManifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$StageDirectory,
        [Parameter(Mandatory = $true)][string]$Version,
        [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-fA-F]{40}$')][string]$SourceCommit
    )

    $normalizedVersion = Get-MulletaFlixReleaseSemVer $Version
    $manifestPath = Get-MulletaFlixStageManifestPath -StageDirectory $StageDirectory
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    if ($manifest.schemaVersion -ne 1 -or $manifest.product -ne 'mulletaflix-server-stage') {
        throw 'Stage manifest schema or product is invalid.'
    }
    if ($manifest.version -ne $normalizedVersion -or $manifest.sourceCommit -ne $SourceCommit.ToLowerInvariant()) {
        throw 'Stage manifest version or source commit does not match this build.'
    }
    if (-not $manifest.sourceTreeClean) {
        throw 'Stage was produced from a dirty source tree and cannot be used for a release.'
    }

    $actualFiles = @(Get-MulletaFlixStageFileRecords -StageDirectory $StageDirectory)
    $expectedFiles = @($manifest.files)
    if ($actualFiles.Count -ne $expectedFiles.Count) {
        throw 'Stage file set changed after its manifest was written.'
    }

    # PowerShell's culture-aware Sort-Object ordering is not a stable identity
    # contract for file paths (punctuation and locale can reorder entries). Match
    # by normalized, case-insensitive relative path so valid stages aren't rejected
    # merely because JSON and filesystem enumeration sort names differently.
    $actualByPath = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($actual in $actualFiles) {
        $key = ([string]$actual.path).Replace('\', '/').TrimStart('/')
        if ($actualByPath.ContainsKey($key)) {
            throw "Stage contains duplicate relative paths: $key"
        }
        $actualByPath.Add($key, $actual)
    }

    $expectedPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($expected in $expectedFiles) {
        $key = ([string]$expected.path).Replace('\', '/').TrimStart('/')
        if (-not $expectedPaths.Add($key)) {
            throw "Stage manifest contains duplicate relative paths: $key"
        }
        if (-not $actualByPath.ContainsKey($key)) {
            throw "Stage file is missing after its manifest was written: $($expected.path)"
        }

        $actual = $actualByPath[$key]
        if ([long]$actual.sizeBytes -ne [long]$expected.sizeBytes -or
            ([string]$actual.sha256).ToLowerInvariant() -cne ([string]$expected.sha256).ToLowerInvariant()) {
            throw "Staged file changed after its manifest was written: $($expected.path)"
        }
    }

    foreach ($key in $actualByPath.Keys) {
        if (-not $expectedPaths.Contains($key)) {
            throw "Unexpected stage file appeared after its manifest was written: $key"
        }
    }

    return $manifest
}

function New-MulletaFlixReleaseManifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Version,
        [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-fA-F]{40}$')][string]$SourceCommit,
        [Parameter(Mandatory = $true)][string]$ZipPath,
        [string]$InstallerPath,
        [string[]]$AdditionalAssetPaths = @(),
        [Parameter(Mandatory = $true)][string]$OutputPath
    )

    $normalizedVersion = Get-MulletaFlixReleaseSemVer $Version
    $zip = Get-Item -LiteralPath $ZipPath -ErrorAction Stop
    if (-not $zip.Name.Equals('mulletaflix-update-win-x64.zip', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'The Windows update artifact must be named mulletaflix-update-win-x64.zip.'
    }

    $files = [System.Collections.Generic.List[string]]::new()
    $files.Add($zip.FullName)
    if (-not [string]::IsNullOrWhiteSpace($InstallerPath)) {
        $installer = Get-Item -LiteralPath $InstallerPath -ErrorAction Stop
        $expectedInstallerName = "mulletaflix_${normalizedVersion}_windows-x64.exe"
        if (-not $installer.Name.Equals($expectedInstallerName, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Windows installer name must match the release version: $expectedInstallerName"
        }

        $files.Add($installer.FullName)
    }
    foreach ($additionalPath in $AdditionalAssetPaths) {
        $additionalAsset = Get-Item -LiteralPath $additionalPath -ErrorAction Stop
        if ($additionalAsset.Name -notmatch "(?i)(?:^|_)${normalizedVersion}(?:_|-)" -and
            -not $additionalAsset.Name.Equals('mulletaflix-update-win-x64.zip', [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Additional release asset is not versioned for $($normalizedVersion): $($additionalAsset.Name)"
        }
        $files.Add($additionalAsset.FullName)
    }

    $uniqueFiles = @{}
    foreach ($file in $files) {
        $asset = Get-Item -LiteralPath $file -ErrorAction Stop
        if ($uniqueFiles.ContainsKey($asset.Name.ToLowerInvariant())) {
            throw "Duplicate release asset filename: $($asset.Name)"
        }
        $uniqueFiles[$asset.Name.ToLowerInvariant()] = $asset.FullName
    }

    $manifest = [ordered]@{
        schemaVersion = 1
        product = 'mulletaflix-server'
        version = $normalizedVersion
        sourceCommit = $SourceCommit.ToLowerInvariant()
        artifacts = @($files | ForEach-Object { Get-MulletaFlixAssetRecord -Path $_ })
    }

    $parent = Split-Path -Parent $OutputPath
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    $json = $manifest | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($OutputPath, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
    return Get-Item -LiteralPath $OutputPath
}

function Assert-MulletaFlixReleaseManifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$ManifestPath,
        [Parameter(Mandatory = $true)][string]$Version,
        [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-fA-F]{40}$')][string]$SourceCommit,
        [Parameter(Mandatory = $true)][string[]]$AssetPaths,
        [switch]$AllowMissingInstaller
    )

    $normalizedVersion = Get-MulletaFlixReleaseSemVer $Version
    $manifest = Get-Content -LiteralPath $ManifestPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    if ($manifest.schemaVersion -ne 1 -or $manifest.product -ne 'mulletaflix-server') {
        throw 'Release asset manifest schema or product is invalid.'
    }
    if ($manifest.version -ne $normalizedVersion) {
        throw "Release asset manifest version '$($manifest.version)' does not match '$normalizedVersion'."
    }
    if ($manifest.sourceCommit -ne $SourceCommit.ToLowerInvariant()) {
        throw 'Release asset manifest source commit does not match the publishing checkout.'
    }

    $assetsByName = @{}
    foreach ($path in $AssetPaths) {
        $item = Get-Item -LiteralPath $path -ErrorAction Stop
        $key = $item.Name.ToLowerInvariant()
        if ($assetsByName.ContainsKey($key)) {
            throw "Duplicate release asset filename: $($item.Name)"
        }
        $assetsByName[$key] = $item.FullName
    }

    $manifestNames = @{}
    foreach ($record in @($manifest.artifacts)) {
        $key = ([string]$record.name).ToLowerInvariant()
        if ($manifestNames.ContainsKey($key)) {
            throw "Duplicate filename in release asset manifest: $($record.name)"
        }
        if (-not $assetsByName.ContainsKey($key)) {
            throw "Manifest artifact is not selected for publication: $($record.name)"
        }

        $assetPath = $assetsByName[$key]
        try {
            Assert-MulletaFlixArtifactMatchesManifest -Path $assetPath -ManifestRecord $record | Out-Null
        }
        catch {
            throw "Release asset bytes do not match the build manifest: $($record.name)"
        }
        $manifestNames[$key] = $true
    }

    $zipName = 'mulletaflix-update-win-x64.zip'
    if (-not $manifestNames.ContainsKey($zipName)) {
        throw 'The build manifest must bind the Windows update ZIP.'
    }

    $installerName = "mulletaflix_${normalizedVersion}_windows-x64.exe"
    if (-not $AllowMissingInstaller -and -not $manifestNames.ContainsKey($installerName)) {
        throw "The build manifest must bind the Windows installer: $installerName"
    }

    foreach ($name in @($zipName, $installerName)) {
        if ($assetsByName.ContainsKey($name) -and -not $manifestNames.ContainsKey($name)) {
            throw "Windows release asset is not bound by the build manifest: $name"
        }
    }
    foreach ($assetName in $assetsByName.Keys) {
        if (-not $manifestNames.ContainsKey($assetName)) {
            throw "Selected release asset is not bound by the build manifest: $($assetsByName[$assetName])"
        }
    }

    return $manifest
}

function Select-MulletaFlixReleaseAssets {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Version,
        [Parameter(Mandatory = $true)][string]$ZipPath,
        [string[]]$AssetPaths = @(),
        [Parameter(Mandatory = $true)][string]$AssetsDirectory,
        [Parameter(Mandatory = $true)][string]$ManifestPath,
        [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-fA-F]{40}$')][string]$SourceCommit,
        [switch]$AllowMissingInstaller
    )

    $normalizedVersion = Get-MulletaFlixReleaseSemVer $Version
    $resolvedZip = (Get-Item -LiteralPath $ZipPath -ErrorAction Stop).FullName
    if (-not [System.IO.Path]::GetFileName($resolvedZip).Equals('mulletaflix-update-win-x64.zip', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'ZipPath must identify mulletaflix-update-win-x64.zip.'
    }

    $patterns = @(
        'mulletaflix-update-*.zip',
        'MulletaFlix_*_windows-*', 'mulletaflix_*_windows-*',
        'MulletaFlix_*_linux-*', 'mulletaflix_*_linux-*',
        'MulletaFlix_*_macos-*', 'mulletaflix_*_macos-*',
        '*.deb', '*.rpm', '*.tar.gz', '*.tar.xz', '*.AppImage', '*.dmg', '*.pkg', '*.msi'
    )
    $candidates = [System.Collections.Generic.List[string]]::new()
    foreach ($path in $AssetPaths) {
        $candidates.Add((Get-Item -LiteralPath $path -ErrorAction Stop).FullName)
    }
    $candidates.Add($resolvedZip)
    if (Test-Path -LiteralPath $AssetsDirectory -PathType Container) {
        foreach ($pattern in $patterns) {
            Get-ChildItem -LiteralPath $AssetsDirectory -File -Filter $pattern -ErrorAction SilentlyContinue |
                ForEach-Object { $candidates.Add($_.FullName) }
        }
    }

    $selectedByPath = @{}
    foreach ($path in $candidates) {
        $fullPath = [System.IO.Path]::GetFullPath($path)
        if ($selectedByPath.ContainsKey($fullPath.ToLowerInvariant())) { continue }

        $name = [System.IO.Path]::GetFileName($fullPath)
        if ($name -match '(?i)(?:^|_)(\d+\.\d+\.\d+)(?:_|-)') {
            if ($matches[1] -ne $normalizedVersion) { continue }
        }
        $selectedByPath[$fullPath.ToLowerInvariant()] = $fullPath
    }

    $assets = @($selectedByPath.Values | Sort-Object)
    if ($assets -notcontains $resolvedZip) {
        throw 'The required Windows update ZIP was excluded from the asset set.'
    }
    if ($assets.Count -eq 0) {
        throw 'No server artifacts were selected for the release.'
    }

    Assert-MulletaFlixReleaseManifest -ManifestPath $ManifestPath -Version $normalizedVersion -SourceCommit $SourceCommit -AssetPaths $assets -AllowMissingInstaller:$AllowMissingInstaller | Out-Null

    $installers = @($assets | Where-Object { [System.IO.Path]::GetFileName($_) -match '(?i)_windows-x64\.exe$' })
    if (-not $AllowMissingInstaller -and $installers.Count -ne 1) {
        throw "Expected one Windows x64 installer for $normalizedVersion; found $($installers.Count)."
    }

    return $assets
}

Export-ModuleMember -Function Get-MulletaFlixReleaseSemVer, Get-MulletaFlixBinaryVersion, Get-MulletaFlixGitContext, Assert-MulletaFlixGitWorktreeClean, Get-MulletaFlixStageManifestPath, New-MulletaFlixStageManifest, Assert-MulletaFlixStageManifest, Assert-MulletaFlixArtifactMatchesManifest, New-MulletaFlixReleaseManifest, Assert-MulletaFlixReleaseManifest, Select-MulletaFlixReleaseAssets
