param(
    [Parameter(Mandatory = $true)]
    [string] $InstallDirectory,

    [Parameter(Mandatory = $true)]
    [string] $UpdateSourceDirectory,

    [Parameter(Mandatory = $true)]
    [int] $ProcessId,

    [Parameter(Mandatory = $false)]
    [string] $DataDirectory
)

$ErrorActionPreference = 'Continue'
$logFile1 = Join-Path $InstallDirectory 'update.log'
$logFile2 = Join-Path $env:TEMP 'mulletaflix-update.log'

function Log {
    param([string]$Message)
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $line = "$timestamp [UPDATE] $Message"
    try { $line | Out-File -FilePath $logFile1 -Append -Encoding utf8 } catch {}
    try { $line | Out-File -FilePath $logFile2 -Append -Encoding utf8 } catch {}
}

Log "Starting in-place update for MulletaFlix."
Log "InstallDirectory: $InstallDirectory"
Log "UpdateSourceDirectory: $UpdateSourceDirectory"
Log "Target ProcessId: $ProcessId"

# 1. Wait for target MulletaFlix process to exit
$waited = 0
while ((Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) -and ($waited -lt 30)) {
    Start-Sleep -Seconds 1
    $waited++
}

# 2. Terminate any running MulletaFlix or Tray processes
$targetProcesses = @("MulletaFlix", "MulletaFlix.Windows.Tray", "jellyfin")
foreach ($procName in $targetProcesses) {
    Get-Process -Name $procName -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

Start-Sleep -Seconds 3

# 3. Copy update files into InstallDirectory using robocopy for robustness and retry handling
Log "Copying update files from $UpdateSourceDirectory to $InstallDirectory..."
$rollbackDirectory = Join-Path $env:TEMP "mulletaflix-update-rollback-$([guid]::NewGuid().ToString('N'))"
$rollbackRecords = [System.Collections.Generic.List[object]]::new()
$installRoot = [System.IO.Path]::GetFullPath($InstallDirectory).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$sourceRoot = [System.IO.Path]::GetFullPath($UpdateSourceDirectory).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
[void][System.IO.Directory]::CreateDirectory($rollbackDirectory)

try {
    foreach ($sourceFile in Get-ChildItem -LiteralPath $UpdateSourceDirectory -File -Recurse -Force -ErrorAction Stop) {
        if ($sourceFile.Name -ieq 'apply-update.ps1' -or $sourceFile.Extension -in @('.log', '.tmp')) { continue }
        if ($sourceFile.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
            throw "Update package contains a reparse-point file: $($sourceFile.FullName)"
        }

        $relativePath = $sourceFile.FullName.Substring($sourceRoot.Length)
        $destination = [System.IO.Path]::GetFullPath((Join-Path $InstallDirectory $relativePath))
        if (-not $destination.StartsWith($installRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Update package path escapes the installation directory: $relativePath"
        }

        $existed = [System.IO.File]::Exists($destination)
        $record = [pscustomobject]@{
            Destination = $destination
            Existed = $existed
            Backup = $null
            LastWriteTimeUtc = $null
            Attributes = $null
        }
        if ($existed) {
            $destinationFile = [System.IO.FileInfo]::new($destination)
            if ($sourceFile.Length -eq $destinationFile.Length -and $sourceFile.LastWriteTimeUtc -eq $destinationFile.LastWriteTimeUtc -and $sourceFile.Attributes -eq $destinationFile.Attributes) { continue }
            $record.Backup = Join-Path $rollbackDirectory $relativePath
            $record.LastWriteTimeUtc = [System.IO.File]::GetLastWriteTimeUtc($destination)
            $record.Attributes = [System.IO.File]::GetAttributes($destination)
            [void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($record.Backup))
            [System.IO.File]::Copy($destination, $record.Backup, $true)
        }
        $rollbackRecords.Add($record)
    }
} catch {
    Remove-Item -LiteralPath $rollbackDirectory -Recurse -Force -ErrorAction SilentlyContinue
    throw "Could not prepare update rollback snapshot: $_"
}

$robocopyArgs = @(
    "`"$UpdateSourceDirectory`"",
    "`"$InstallDirectory`"",
    "/E",
    "/R:5",
    "/W:2",
    "/NP",
    "/NFL",
    "/NDL",
    "/XF", "apply-update.ps1", "*.log", "*.tmp"
)

$robocopyProcess = Start-Process -FilePath "robocopy.exe" -ArgumentList ($robocopyArgs -join " ") -Wait -PassThru -NoNewWindow
$exitCode = $robocopyProcess.ExitCode
Log "Robocopy completed with exit code: $exitCode"

# Robocopy exit codes: 0 to 7 indicate success (files copied or identical). 8 or higher indicates failure.
if ($exitCode -lt 8) {
    Log "Files copied successfully."
    Remove-Item -LiteralPath $rollbackDirectory -Recurse -Force -ErrorAction SilentlyContinue
    # 4. Clean up temporary extracted update files and state
    try {
        Remove-Item -LiteralPath $UpdateSourceDirectory -Recurse -Force -ErrorAction SilentlyContinue
        Log "Cleaned up update source directory."
    } catch {
        Log "Could not clean update source directory: $_"
    }

    if ($DataDirectory) {
        $stateFile = Join-Path $DataDirectory "updates\update_state.json"
        $pkgZip = Join-Path $DataDirectory "updates\package.zip"
        if (Test-Path -LiteralPath $stateFile) { Remove-Item -LiteralPath $stateFile -Force -ErrorAction SilentlyContinue }
        if (Test-Path -LiteralPath $pkgZip) { Remove-Item -LiteralPath $pkgZip -Force -ErrorAction SilentlyContinue }
    }
} else {
    Log "ERROR: Robocopy failed with exit code $exitCode. Restoring the installation snapshot."
    $rollbackErrors = [System.Collections.Generic.List[string]]::new()
    foreach ($record in $rollbackRecords) {
        try {
            if ($record.Existed) {
                [void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($record.Destination))
                [System.IO.File]::SetAttributes($record.Destination, [System.IO.FileAttributes]::Normal)
                [System.IO.File]::Copy($record.Backup, $record.Destination, $true)
                [System.IO.File]::SetLastWriteTimeUtc($record.Destination, $record.LastWriteTimeUtc)
                [System.IO.File]::SetAttributes($record.Destination, $record.Attributes)
            } else {
                [System.IO.File]::Delete($record.Destination)
            }
        } catch {
            $rollbackErrors.Add("$($record.Destination): $_")
        }
    }

    if ($rollbackErrors.Count -eq 0) {
        Remove-Item -LiteralPath $rollbackDirectory -Recurse -Force -ErrorAction SilentlyContinue
        Log "Installation snapshot restored successfully. Update source retained; server will not restart."
        throw "In-place update aborted because Robocopy failed with exit code $exitCode. Existing files were restored and new files removed; the current server will not be restarted."
    }

    Log "CRITICAL: rollback incomplete. Recovery snapshot retained at $rollbackDirectory. Errors: $($rollbackErrors -join ' | ')"
    throw "In-place update failed with Robocopy exit code $exitCode and rollback was incomplete. Recovery snapshot: $rollbackDirectory. Errors: $($rollbackErrors -join ' | ')"
}

# 5. Restart MulletaFlix
$service = Get-Service -Name "MulletaFlixServer" -ErrorAction SilentlyContinue
if ($service) {
    Log "Starting MulletaFlixServer service..."
    Start-Service -Name "MulletaFlixServer" -ErrorAction SilentlyContinue
} else {
    $exePath = Join-Path $InstallDirectory "MulletaFlix.exe"
    if (Test-Path -LiteralPath $exePath) {
        Log "Starting MulletaFlix.exe from $exePath..."
        $args = if ($DataDirectory) { "--datadir `"$DataDirectory`"" } else { "" }
        Start-Process -FilePath $exePath -ArgumentList $args -WorkingDirectory $InstallDirectory -WindowStyle Hidden
    } else {
        Log "ERROR: MulletaFlix.exe not found at $exePath"
    }
}

# 6. Restart Tray if available
$trayPath = Join-Path $InstallDirectory "mulletaflix-windows-tray\MulletaFlix.Windows.Tray.exe"
if (Test-Path -LiteralPath $trayPath) {
    Log "Starting MulletaFlix.Windows.Tray.exe..."
    Start-Process -FilePath $trayPath -WorkingDirectory (Join-Path $InstallDirectory "mulletaflix-windows-tray")
}

Log "In-place update script finished."
