function Initialize-StageBackupRoot {
    param(
        [Parameter(Mandatory)][string]$BuildRoot,
        [Parameter(Mandatory)][string]$StageDirectory
    )

    $normalizedStagePath = [System.IO.Path]::GetFullPath($StageDirectory).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $stagePathBytes = [System.Text.Encoding]::UTF8.GetBytes($normalizedStagePath.ToUpperInvariant())
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stageKey = [System.BitConverter]::ToString($sha256.ComputeHash($stagePathBytes)).Replace('-', '').Substring(0, 16)
    }
    finally {
        $sha256.Dispose()
    }
    $normalizedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
    if (Test-Path -LiteralPath $normalizedBuildRoot) {
        if ((Get-Item -LiteralPath $normalizedBuildRoot -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
            throw "Build root must not be a symbolic link or junction: $normalizedBuildRoot"
        }
    } else {
        New-Item -ItemType Directory -Force -Path $normalizedBuildRoot | Out-Null
    }

    $backupParent = Join-Path $normalizedBuildRoot 'stage-preserve-backups'
    if (Test-Path -LiteralPath $backupParent) {
        if ((Get-Item -LiteralPath $backupParent -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
            throw "Stage backup parent must not be a symbolic link or junction: $backupParent"
        }
    } else {
        New-Item -ItemType Directory -Force -Path $backupParent | Out-Null
    }

    $backupRoot = Join-Path $backupParent $stageKey
    if (Test-Path -LiteralPath $backupRoot) {
        if ((Get-Item -LiteralPath $backupRoot -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
            throw "Stage backup directory must not be a symbolic link or junction: $backupRoot"
        }
    } else {
        New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
    }

    return [System.IO.Path]::GetFullPath($backupRoot)
}

function Enter-StageBackupLock {
    param(
        [Parameter(Mandatory)][string]$BackupRoot,
        [ValidateRange(0, 600)][int]$TimeoutSeconds = 300
    )

    if ((Get-Item -LiteralPath $BackupRoot -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
        throw "Stage build lock root must not be a symbolic link or junction: $BackupRoot"
    }

    $lockPath = Join-Path $BackupRoot 'stage-build.lock'
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ($true) {
        try {
            return [System.IO.File]::Open($lockPath, [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        }
        catch [System.IO.IOException] {
            if ([DateTime]::UtcNow -ge $deadline) {
                throw "Timed out waiting for another stage build to release its backup lock: $lockPath"
            }
            Start-Sleep -Milliseconds 200
        }
    }
}

function Assert-NoStageBackupReparsePoints {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $rootItem = Get-Item -LiteralPath $Path -Force
    if ($rootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
        throw "Stage backup tree contains a symbolic link or junction: $Path"
    }
    if (-not $rootItem.PSIsContainer) {
        return
    }

    $pendingDirectories = [System.Collections.Generic.Stack[string]]::new()
    $pendingDirectories.Push($rootItem.FullName)
    while ($pendingDirectories.Count -gt 0) {
        $currentDirectory = $pendingDirectories.Pop()
        foreach ($child in Get-ChildItem -LiteralPath $currentDirectory -Force) {
            if ($child.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                throw "Stage backup tree contains a symbolic link or junction: $($child.FullName)"
            }
            if ($child.PSIsContainer) {
                $pendingDirectories.Push($child.FullName)
            }
        }
    }
}

function New-StageBackupRunDirectory {
    param([Parameter(Mandatory)][string]$BackupRoot)

    if ((Get-Item -LiteralPath $BackupRoot -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
        throw "Stage backup directory must not be a symbolic link or junction: $BackupRoot"
    }

    $runDirectory = Join-Path $BackupRoot ([Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $runDirectory -ErrorAction Stop | Out-Null
    return $runDirectory
}

function Restore-StageBackupItem {
    param(
        [Parameter(Mandatory)][string]$BackupPath,
        [Parameter(Mandatory)][string]$DestinationPath
    )

    if (-not (Test-Path -LiteralPath $BackupPath)) {
        return $false
    }

    $backupItem = Get-Item -LiteralPath $BackupPath -Force
    Assert-NoStageBackupReparsePoints -Path $BackupPath

    if (Test-Path -LiteralPath $DestinationPath) {
        return $false
    }

    $destinationParent = Split-Path -Parent $DestinationPath
    New-Item -ItemType Directory -Force -Path $destinationParent | Out-Null
    Copy-Item -LiteralPath $BackupPath -Destination $DestinationPath -Recurse -Force -ErrorAction Stop
    return $true
}

function Restore-OrphanedStageBackups {
    param(
        [Parameter(Mandatory)][string]$BackupRoot,
        [Parameter(Mandatory)][string]$StageDirectory,
        [Parameter(Mandatory)][string[]]$Items,
        [Parameter(Mandatory)][string]$CurrentRunDirectory
    )

    $backupRuns = @(Get-ChildItem -LiteralPath $BackupRoot -Directory -Force)
    foreach ($backupRun in $backupRuns) {
        if ($backupRun.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
            throw "Stage backup run must not be a symbolic link or junction: $($backupRun.FullName)"
        }
        if ([string]::Equals($backupRun.FullName, $CurrentRunDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        foreach ($item in $Items) {
            $backupPath = Join-Path $backupRun.FullName "stage-backup-$item"
            $destinationPath = Join-Path $StageDirectory $item
            if (Restore-StageBackupItem -BackupPath $backupPath -DestinationPath $destinationPath) {
                Remove-Item -LiteralPath $backupPath -Recurse -Force -ErrorAction Stop
                Write-Host "Recovered $item from an interrupted stage build." -ForegroundColor Yellow
            }
        }

        if (-not (Get-ChildItem -LiteralPath $backupRun.FullName -Force | Select-Object -First 1)) {
            Remove-Item -LiteralPath $backupRun.FullName -Force -ErrorAction SilentlyContinue
        }
    }

    foreach ($item in $Items) {
        $legacyBackup = Join-Path $BackupRoot "stage-backup-$item"
        $destinationPath = Join-Path $StageDirectory $item
        if (Restore-StageBackupItem -BackupPath $legacyBackup -DestinationPath $destinationPath) {
            Remove-Item -LiteralPath $legacyBackup -Recurse -Force -ErrorAction Stop
            Write-Host "Recovered legacy backup for $item." -ForegroundColor Yellow
        }
    }
}
