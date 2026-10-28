$ErrorActionPreference = 'Stop'

function Assert-Path {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
}

function Assert-WebBuildIntegrity {
    param([string]$WebDirectory)

    $indexPath = Join-Path $WebDirectory 'index.html'
    Assert-Path $indexPath 'Web index'

    $index = Get-Content -LiteralPath $indexPath -Raw
    $assetReferences = [regex]::Matches($index, '(?:src|href)="(\.\/assets\/[^"?]+)"') |
        ForEach-Object { $_.Groups[1].Value.Substring(2) } |
        Sort-Object -Unique

    if ($assetReferences.Count -eq 0) {
        throw "Web index does not reference any assets: $indexPath"
    }

    foreach ($asset in $assetReferences) {
        $assetPath = Join-Path $WebDirectory $asset
        if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
            throw "Web index references a missing asset: $assetPath"
        }
    }
}

function Assert-StageIntegrity {
    param(
        [string]$StageDirectory,
        [string]$ProjectRoot,
        [string]$PublishDirectory
    )

    $requiredFiles = @(
        'MulletaFlix.exe',
        'MulletaFlix.dll',
        'MulletaFlix.Server.Implementations.dll',
        'MulletaFlix-web\index.html',
        'MulletaFlix-web\serviceworker.js',
        'Tools\mount_drive_n.py',
        'Tools\NebulaPython\main.py',
        'Tools\NebulaPython\upload_checkpoint.py',
        'Tools\NebulaPython\stream_service.py',
        'Tools\NebulaPython\generate_strm.py',
        'Tools\NebulaPython\tools\strm_downloader.py',
        'Tools\NebulaPython\tools\mongo_admission.py',
        'Tools\NebulaPython\ftp\pathio.py',
        'Tools\NebulaPython\requirements.in',
        'Tools\NebulaPython\requirements.txt',
        'Tools\NebulaPython\requirements.lock',
        'install-rclone-if-missing.ps1',
        'nssm.exe',
        'mulletaflix-windows-tray\MulletaFlix.Windows.Tray.exe'
    )

    foreach ($relativePath in $requiredFiles) {
        Assert-Path (Join-Path $StageDirectory $relativePath) "Required stage artifact ($relativePath)"
    }

    # The staged downloader is executable production logic, not an opaque
    # optional asset. Fail packaging if a stale or mixed stage contains a
    # different copy than the server source tree.
    $sourceDownloader = Join-Path $ProjectRoot 'MulletaFlix-master\Tools\NebulaPython\tools\strm_downloader.py'
    $stagedDownloader = Join-Path $StageDirectory 'Tools\NebulaPython\tools\strm_downloader.py'
    Assert-Path $sourceDownloader 'Nebula STRM downloader source'
    $sourceDownloaderContent = Get-Content -LiteralPath $sourceDownloader -Raw
    if ($sourceDownloaderContent -notmatch '\[STRM-WORKER-MONGO-READY\]') {
        throw 'Nebula STRM downloader source does not implement the Mongo readiness contract required by the server.'
    }
    $sourceDownloaderHash = (Get-FileHash -LiteralPath $sourceDownloader -Algorithm SHA256).Hash
    $stagedDownloaderHash = (Get-FileHash -LiteralPath $stagedDownloader -Algorithm SHA256).Hash
    if ($sourceDownloaderHash -ne $stagedDownloaderHash) {
        throw 'Stage Nebula STRM downloader differs from the current server source.'
    }

    $sourceCheckpoint = Join-Path $ProjectRoot 'MulletaFlix-master\Tools\NebulaPython\upload_checkpoint.py'
    $stagedCheckpoint = Join-Path $StageDirectory 'Tools\NebulaPython\upload_checkpoint.py'
    $sourceCheckpointHash = (Get-FileHash -LiteralPath $sourceCheckpoint -Algorithm SHA256).Hash
    $stagedCheckpointHash = (Get-FileHash -LiteralPath $stagedCheckpoint -Algorithm SHA256).Hash
    if ($sourceCheckpointHash -ne $stagedCheckpointHash) {
        throw 'Stage Nebula checkpoint module differs from the current server source.'
    }

    $sourceRequirementsLock = Join-Path $ProjectRoot 'MulletaFlix-master\Tools\NebulaPython\requirements.lock'
    $stagedRequirementsLock = Join-Path $StageDirectory 'Tools\NebulaPython\requirements.lock'
    $sourceRequirementsLockHash = (Get-FileHash -LiteralPath $sourceRequirementsLock -Algorithm SHA256).Hash
    $stagedRequirementsLockHash = (Get-FileHash -LiteralPath $stagedRequirementsLock -Algorithm SHA256).Hash
    if ($sourceRequirementsLockHash -ne $stagedRequirementsLockHash) {
        throw 'Stage Nebula dependency lock differs from the current server source.'
    }

    # Catch mixed/stale .NET publishes before NSIS packages them. The Nebula
    # implementation and model must come from the same publish operation;
    # otherwise startup can fail with MissingMethodException.
    $publishDirectory = if ([string]::IsNullOrWhiteSpace($PublishDirectory)) {
        Join-Path $ProjectRoot '.build\server-publish'
    } else {
        $PublishDirectory
    }
    if (Test-Path -LiteralPath $publishDirectory -PathType Container) {
        foreach ($assemblyName in @('MediaBrowser.Model.dll', 'MulletaFlix.Server.Implementations.dll', 'MulletaFlix.dll')) {
            $publishedAssembly = Join-Path $publishDirectory $assemblyName
            $stagedAssembly = Join-Path $StageDirectory $assemblyName
            if ((Test-Path -LiteralPath $publishedAssembly -PathType Leaf) -and (Test-Path -LiteralPath $stagedAssembly -PathType Leaf)) {
                $publishedHash = (Get-FileHash -LiteralPath $publishedAssembly -Algorithm SHA256).Hash
                $stagedHash = (Get-FileHash -LiteralPath $stagedAssembly -Algorithm SHA256).Hash
                if ($publishedHash -ne $stagedHash) {
                    throw "Stage assembly differs from the current server publish: $assemblyName"
                }
            }
        }
    }

    Assert-WebBuildIntegrity -WebDirectory (Join-Path $StageDirectory 'MulletaFlix-web')

    if (Test-Path -LiteralPath (Join-Path $StageDirectory 'nebula')) {
        throw 'Legacy Python Nebula directory must not be present in the stage.'
    }

    $leftoverBackups = Get-ChildItem -LiteralPath $ProjectRoot -Filter 'stage-backup-*' -Force -ErrorAction SilentlyContinue
    if ($leftoverBackups) {
        $names = ($leftoverBackups | Select-Object -ExpandProperty Name) -join ', '
        throw "Stage backup artifacts were left behind after the build: $names"
    }
}
