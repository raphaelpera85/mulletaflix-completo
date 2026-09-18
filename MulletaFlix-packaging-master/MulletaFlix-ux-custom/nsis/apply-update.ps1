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
$logFile = Join-Path $env:TEMP 'mulletaflix-update.log'

function Log {
    param([string]$Message)
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    "$timestamp [UPDATE] $Message" | Out-File -FilePath $logFile -Append -Encoding utf8
}

Log "Starting in-place update for MulletaFlix."
Log "InstallDirectory: $InstallDirectory"
Log "UpdateSource: $UpdateSourceDirectory"
Log "Target ProcessId: $ProcessId"

# 1. Wait for target MulletaFlix process to exit
$waited = 0
while ((Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) -and ($waited -lt 30)) {
    Start-Sleep -Seconds 1
    $waited++
}

if (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
    Log "Process $ProcessId did not exit in time. Forcing termination."
    Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
}

# Also stop Tray if running to avoid locking tray binaries
Get-Process | Where-Object { $_.ProcessName -like "*MulletaFlix.Windows.Tray*" } | Stop-Process -Force -ErrorAction SilentlyContinue

Start-Sleep -Seconds 2

# 2. Backup existing previous state
$backupDir = Join-Path $InstallDirectory "backup-prev"
if (Test-Path -LiteralPath $backupDir) {
    Remove-Item -LiteralPath $backupDir -Recurse -Force -ErrorAction SilentlyContinue
}

try {
    # 3. Copy update files into InstallDirectory
    Log "Copying files from $UpdateSourceDirectory to $InstallDirectory..."
    Copy-Item -Path "$UpdateSourceDirectory\*" -Destination $InstallDirectory -Recurse -Force -ErrorAction Stop
    Log "Files copied successfully."
} catch {
    Log "ERROR during copy: $_"
}

# 4. Clean up temporary extracted update files
try {
    Remove-Item -LiteralPath $UpdateSourceDirectory -Recurse -Force -ErrorAction SilentlyContinue
} catch { }

# 5. Restart MulletaFlix
$service = Get-Service -Name "MulletaFlixServer" -ErrorAction SilentlyContinue
if ($service) {
    Log "Starting MulletaFlixServer service..."
    Start-Service -Name "MulletaFlixServer" -ErrorAction SilentlyContinue
} else {
    $exePath = Join-Path $InstallDirectory "MulletaFlix.exe"
    if (Test-Path -LiteralPath $exePath) {
        Log "Starting MulletaFlix.exe..."
        $args = if ($DataDirectory) { "--datadir `"$DataDirectory`"" } else { "" }
        Start-Process -FilePath $exePath -ArgumentList $args -WorkingDirectory $InstallDirectory -WindowStyle Hidden
    }
}

# 6. Restart Tray if available
$trayPath = Join-Path $InstallDirectory "mulletaflix-windows-tray\MulletaFlix.Windows.Tray.exe"
if (Test-Path -LiteralPath $trayPath) {
    Log "Starting MulletaFlix.Windows.Tray.exe..."
    Start-Process -FilePath $trayPath -WorkingDirectory (Join-Path $InstallDirectory "mulletaflix-windows-tray")
}

Log "In-place update completed successfully."
