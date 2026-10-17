$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $scriptRoot 'StageBackup.ps1')

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) "mfx-stage-backup-tests-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $testRoot | Out-Null

try {
    $stageDirectory = Join-Path $testRoot 'stage'
    New-Item -ItemType Directory -Path $stageDirectory | Out-Null
    $buildRoot = Join-Path $testRoot '.build'
    $backupRoot = Initialize-StageBackupRoot -BuildRoot $buildRoot -StageDirectory $stageDirectory
    $runDirectory = New-StageBackupRunDirectory -BackupRoot $backupRoot
    $otherStage = Join-Path $testRoot 'other-stage'
    $otherBackupRoot = Initialize-StageBackupRoot -BuildRoot $buildRoot -StageDirectory $otherStage
    if ($backupRoot -eq $otherBackupRoot) {
        throw 'Different stage outputs must not share orphan-recovery directories.'
    }

    $backupLock = Enter-StageBackupLock -BackupRoot $buildRoot
    try {
        $lockTimedOut = $false
        try {
            $unexpectedLock = Enter-StageBackupLock -BackupRoot $buildRoot -TimeoutSeconds 0
            $unexpectedLock.Dispose()
        }
        catch [System.Exception] {
            $lockTimedOut = $_.Exception.Message -like '*Timed out waiting for another stage build*'
        }
        if (-not $lockTimedOut) {
            throw 'A second build acquired the same stage backup lock.'
        }
    }
    finally {
        $backupLock.Dispose()
    }

    $unsafeBuildRoot = Join-Path $testRoot 'unsafe-build-root'
    New-Item -ItemType Directory -Path $unsafeBuildRoot | Out-Null
    $externalTarget = Join-Path $testRoot 'external-backup-target'
    New-Item -ItemType Directory -Path $externalTarget | Out-Null
    $unsafeBackupParent = Join-Path $unsafeBuildRoot 'stage-preserve-backups'
    New-Item -ItemType Junction -Path $unsafeBackupParent -Target $externalTarget | Out-Null
    $junctionRejected = $false
    try {
        Initialize-StageBackupRoot -BuildRoot $unsafeBuildRoot -StageDirectory (Join-Path $unsafeBuildRoot 'candidate') | Out-Null
    }
    catch {
        $junctionRejected = $_.Exception.Message -like '*symbolic link or junction*'
    }
    if (-not $junctionRejected -or (Get-ChildItem -LiteralPath $externalTarget -Force | Select-Object -First 1)) {
        throw 'A junction in the backup path must be rejected without writing to its external target.'
    }

    $orphanRun = Join-Path $backupRoot 'interrupted-run'
    New-Item -ItemType Directory -Path $orphanRun | Out-Null
    Set-Content -LiteralPath (Join-Path $orphanRun 'stage-backup-ffmpeg.exe') -Value 'preserved-file'
    $orphanFolder = Join-Path $orphanRun 'stage-backup-MulletaFlix-web'
    New-Item -ItemType Directory -Path $orphanFolder | Out-Null
    Set-Content -LiteralPath (Join-Path $orphanFolder 'index.html') -Value 'preserved-web'

    $existingPath = Join-Path $stageDirectory 'icon.ico'
    Set-Content -LiteralPath $existingPath -Value 'current-stage'
    Set-Content -LiteralPath (Join-Path $orphanRun 'stage-backup-icon.ico') -Value 'orphan-copy'

    Restore-OrphanedStageBackups -BackupRoot $backupRoot -StageDirectory $stageDirectory -Items @('ffmpeg.exe', 'MulletaFlix-web', 'icon.ico') -CurrentRunDirectory $runDirectory

    if ((Get-Content -LiteralPath (Join-Path $stageDirectory 'ffmpeg.exe') -Raw).Trim() -ne 'preserved-file') {
        throw 'Orphaned file backup was not restored.'
    }
    if ((Get-Content -LiteralPath (Join-Path $stageDirectory 'MulletaFlix-web\index.html') -Raw).Trim() -ne 'preserved-web') {
        throw 'Orphaned directory backup was not restored.'
    }
    if ((Get-Content -LiteralPath $existingPath -Raw).Trim() -ne 'current-stage') {
        throw 'Recovery overwrote content that already existed in stage.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $orphanRun 'stage-backup-icon.ico'))) {
        throw 'Skipped orphan backup was deleted even though stage already had content.'
    }

    $targetDirectory = Join-Path $testRoot 'junction-target'
    $maliciousBackup = Join-Path $orphanRun 'stage-backup-nginx'
    New-Item -ItemType Directory -Path $targetDirectory | Out-Null
    New-Item -ItemType Directory -Path $maliciousBackup | Out-Null
    Set-Content -LiteralPath (Join-Path $targetDirectory 'external.txt') -Value 'do-not-copy'
    New-Item -ItemType Junction -Path (Join-Path $maliciousBackup 'linked-directory') -Target $targetDirectory | Out-Null
    $reparseRejected = $false
    try {
        Restore-StageBackupItem -BackupPath $maliciousBackup -DestinationPath (Join-Path $stageDirectory 'nginx') | Out-Null
    }
    catch {
        $reparseRejected = $_.Exception.Message -like '*symbolic link or junction*'
    }
    if (-not $reparseRejected -or (Test-Path -LiteralPath (Join-Path $stageDirectory 'nginx'))) {
        throw 'Nested reparse points in a backup must be rejected before restoring any content.'
    }

    Write-Host 'Stage backups isolate build outputs, serialize concurrent builds, recover orphan files safely, preserve existing data, and reject nested junctions.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
