# Builds MulletaFlix, updates the local stage folder, and creates the Windows installer.
# Run from anywhere:
#   powershell -ExecutionPolicy Bypass -File .\MulletaFlix-packaging-master\build-stage-and-installer.ps1

[CmdletBinding()]
param(
    [switch]$SkipWebBuild,
    [switch]$SkipServerBuild,
    [switch]$SkipTrayBuild,
    [switch]$SkipInstaller,
    [switch]$NoPause
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$projectRoot = Split-Path -Parent $PSScriptRoot
$serverRoot = Join-Path $projectRoot 'MulletaFlix-master'
$webRoot = Join-Path $projectRoot 'MulletaFlix-web-master'
$packagingRoot = Join-Path $projectRoot 'MulletaFlix-packaging-master'
$stageDir = Join-Path $projectRoot 'stage'
$buildDir = Join-Path $projectRoot '.build'
$serverPublishDir = Join-Path $buildDir 'server-publish'
$installerScript = Join-Path $projectRoot 'build-mulletaflix-installer.ps1'
$trayProject = Join-Path $packagingRoot 'jellyfin-server-windows\Jellyfin.Windows.Tray\Jellyfin.Windows.Tray.csproj'
$trayProjectDir = Split-Path -Parent $trayProject
$webDist = Join-Path $webRoot 'dist'

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Resolve-DotNet {
    $candidates = @(
        (Join-Path $projectRoot 'dotnet-sdk-11.0.100-preview.5.26302.115-win-x64\dotnet.exe'),
        (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'),
        'D:\Users\Raphael\.dotnet\dotnet.exe',
        'C:\Users\Raphael\.dotnet\dotnet.exe'
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }

    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    throw 'dotnet SDK/runtime was not found. Install .NET SDK or add dotnet.exe to PATH.'
}

function Assert-Path {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
}

function Copy-DirectoryContents {
    param(
        [string]$Source,
        [string]$Destination
    )

    Assert-Path $Source 'Source directory'
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null

    $process = Start-Process -FilePath "robocopy.exe" -ArgumentList @("`"$Source`"", "`"$Destination`"", "/E", "/R:2", "/W:1", "/NFL", "/NDL", "/NJH", "/NJS", "/nc", "/ns", "/np") -Wait -PassThru -NoNewWindow
    if ($process.ExitCode -ge 8) {
        throw "robocopy failed from $Source to $Destination with exit code $($process.ExitCode)"
    }
}

function Build-Web {
    Write-Step 'Building web client'
    Assert-Path $webRoot 'Web source'

    Push-Location $webRoot
    try {
        $npm = Get-Command npm.cmd -ErrorAction SilentlyContinue
        if (-not $npm) {
            $npm = Get-Command npm -ErrorAction SilentlyContinue
        }

        if (-not $npm) {
            throw 'npm was not found. Install Node.js or add npm to PATH.'
        }

        # Run npm build via cmd.exe to avoid PowerShell treating Vite stderr warnings as errors
        $exitCode = (Start-Process -FilePath 'cmd.exe' -ArgumentList "/c npm run build:production" -WorkingDirectory $webRoot -Wait -PassThru).ExitCode
        if ($exitCode -ne 0) {
            throw "npm build failed with exit code $exitCode"
        }
    }
    finally {
        Pop-Location
    }

    Assert-Path $webDist 'Web build output'

    $stageWeb = Join-Path $stageDir 'MulletaFlix-web'
    if (Test-Path -LiteralPath $stageWeb) {
        Remove-Item -LiteralPath $stageWeb -Recurse -Force
    }

    Copy-DirectoryContents -Source $webDist -Destination $stageWeb
    Write-Host "Web copied to: $stageWeb" -ForegroundColor Green
}

function Build-Server {
    param([string]$DotNet)

    Write-Step 'Publishing server to stage'
    Assert-Path $serverRoot 'Server source'

    $backupItems = @()
    $preserves = @('mariadb', 'MulletaFlix-web', 'ffmpeg.exe', 'ffprobe.exe', 'nssm.exe')

    foreach ($item in $preserves) {
        $sourcePath = Join-Path $stageDir $item
        if (Test-Path -LiteralPath $sourcePath) {
            $tempBackupPath = Join-Path $projectRoot "stage-backup-$item"
            if (Test-Path -LiteralPath $tempBackupPath) {
                Remove-Item -LiteralPath $tempBackupPath -Recurse -Force -ErrorAction SilentlyContinue
            }
            Copy-Item -LiteralPath $sourcePath -Destination $tempBackupPath -Recurse -Force
            $backupItems += [PSCustomObject]@{
                Item = $item
                BackupPath = $tempBackupPath
            }
            Write-Host "Backed up $item from stage..." -ForegroundColor Gray
        }
    }

    if (Test-Path -LiteralPath $stageDir) {
        Get-ChildItem -LiteralPath $stageDir -Force -ErrorAction SilentlyContinue | ForEach-Object {
            Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    if (Test-Path -LiteralPath $serverPublishDir) {
        Remove-Item -LiteralPath $serverPublishDir -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
    New-Item -ItemType Directory -Force -Path $serverPublishDir | Out-Null

    $serverProject = Join-Path $serverRoot 'Jellyfin.Server\Jellyfin.Server.csproj'
    Assert-Path $serverProject 'Server project'

    & $DotNet publish $serverProject `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -o $serverPublishDir `
        -p:DebugSymbols=false `
        -p:DebugType=none `
        -p:GenerateDocumentationFile=false `
        -p:RunAnalyzersDuringBuild=false `
        -p:RunAnalyzers=false

    New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
    Copy-DirectoryContents -Source $serverPublishDir -Destination $stageDir
    Write-Host "Server copied to: $stageDir" -ForegroundColor Green

    # Restore backups
    foreach ($backup in $backupItems) {
        $destPath = Join-Path $stageDir $backup.Item
        if (Test-Path -LiteralPath $destPath) {
            Remove-Item -LiteralPath $destPath -Recurse -Force -ErrorAction SilentlyContinue
        }

        try {
            if (Test-Path -LiteralPath $backup.BackupPath -PathType Container) {
                Copy-DirectoryContents -Source $backup.BackupPath -Destination $destPath
            } else {
                Copy-Item -LiteralPath $backup.BackupPath -Destination $destPath -Force -ErrorAction Stop
            }
            Remove-Item -LiteralPath $backup.BackupPath -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "Restored $($backup.Item) to stage." -ForegroundColor Gray
        }
        catch {
            Write-Host "Notice: $($backup.Item) could not be restored: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }

    # Safety net: if a preserve item had a backup folder already on disk from a
    # previous interrupted build, restore it even when it wasn't present at the
    # beginning of this run.
    foreach ($item in $preserves) {
        $destPath = Join-Path $stageDir $item
        $tempBackupPath = Join-Path $projectRoot "stage-backup-$item"
        if ((Test-Path -LiteralPath $tempBackupPath) -and (-not (Test-Path -LiteralPath $destPath))) {
            try {
                if (Test-Path -LiteralPath $tempBackupPath -PathType Container) {
                    Copy-DirectoryContents -Source $tempBackupPath -Destination $destPath
                } else {
                    Copy-Item -LiteralPath $tempBackupPath -Destination $destPath -Force -ErrorAction Stop
                }
                Remove-Item -LiteralPath $tempBackupPath -Recurse -Force -ErrorAction SilentlyContinue
                Write-Host "Recovered $item from previous backup." -ForegroundColor Yellow
            }
            catch {
                Write-Host "Notice: Could not recover $item from previous backup." -ForegroundColor Yellow
            }
        }
    }
}

function Build-Tray {
    param([string]$DotNet)

    Write-Step 'Building tray application'
    Assert-Path $trayProject 'Tray project'

    $sourceIcon = Join-Path $serverRoot 'Jellyfin.Server\wwwroot\branding\mulletaflix_icon.ico'
    if (-not (Test-Path -LiteralPath $sourceIcon)) {
        $sourceIcon = Join-Path $webRoot 'src\assets\branding\mulletaflix_icon.ico'
    }

    if (Test-Path -LiteralPath $sourceIcon) {
        Copy-Item -LiteralPath $sourceIcon -Destination (Join-Path $trayProjectDir 'JellyfinIcon.ico') -Force
        Copy-Item -LiteralPath $sourceIcon -Destination (Join-Path $trayProjectDir 'Resources\JellyfinIcon.ico') -Force
    }

    & $DotNet build $trayProject -c Release -f net472 -r win-x64

    $trayOutput = Join-Path $trayProjectDir 'bin\Release\net472\win-x64'
    Assert-Path $trayOutput 'Tray build output'

    $stageTray = Join-Path $stageDir 'mulletaflix-windows-tray'
    if (Test-Path -LiteralPath $stageTray) {
        Remove-Item -LiteralPath $stageTray -Recurse -Force
    }

    Copy-DirectoryContents -Source $trayOutput -Destination $stageTray

    $legacyExe = Join-Path $stageTray 'Jellyfin.Windows.Tray.exe'
    $mulletaExe = Join-Path $stageTray 'MulletaFlix.Windows.Tray.exe'
    if (Test-Path -LiteralPath $legacyExe) {
        Move-Item -LiteralPath $legacyExe -Destination $mulletaExe -Force
    }

    $legacyConfig = Join-Path $stageTray 'Jellyfin.Windows.Tray.exe.config'
    $mulletaConfig = Join-Path $stageTray 'MulletaFlix.Windows.Tray.exe.config'
    if (Test-Path -LiteralPath $legacyConfig) {
        Move-Item -LiteralPath $legacyConfig -Destination $mulletaConfig -Force
    }

    Remove-Item -LiteralPath (Join-Path $stageTray 'Jellyfin.Windows.Tray.pdb') -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath (Join-Path $stageTray 'Jellyfin.Windows.Tray.xml') -Force -ErrorAction SilentlyContinue

    Write-Host "Tray copied to: $stageTray" -ForegroundColor Green
}

function Copy-RuntimeExtras {
    Write-Step 'Copying runtime extras'

    $supportLicense = Join-Path $packagingRoot 'jellyfin-server-windows\Support Files\LICENSE'
    if (Test-Path -LiteralPath $supportLicense) {
        Copy-Item -LiteralPath $supportLicense -Destination (Join-Path $stageDir 'LICENSE') -Force
    }

    $mongoScript = Join-Path $packagingRoot 'jellyfin-server-windows\Support Files\install-mongodb-if-missing.ps1'
    if (Test-Path -LiteralPath $mongoScript) {
        Copy-Item -LiteralPath $mongoScript -Destination (Join-Path $stageDir 'install-mongodb-if-missing.ps1') -Force
        Write-Host "Copied install-mongodb-if-missing.ps1 to stage." -ForegroundColor Green
    }

    foreach ($binary in @('ffmpeg.exe', 'ffprobe.exe')) {
        $existing = Join-Path $stageDir $binary
        if (Test-Path -LiteralPath $existing) {
            Write-Host "Found $binary in stage." -ForegroundColor Gray
        } else {
            # Try to resolve from PATH or default winget packages
            $foundPath = $null
            $cmd = Get-Command $binary -ErrorAction SilentlyContinue
            if ($cmd) {
                $foundPath = $cmd.Source
            } else {
                $wingetPath = "C:\Users\Raphael\AppData\Local\Microsoft\WinGet\Packages\Jellyfin.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\$binary"
                if (Test-Path -LiteralPath $wingetPath) {
                    $foundPath = $wingetPath
                }
            }

            if ($foundPath -and (Test-Path -LiteralPath $foundPath)) {
                Write-Host "Resolving $binary from: $foundPath" -ForegroundColor Gray
                Copy-Item -LiteralPath $foundPath -Destination $stageDir -Force
                Write-Host "Copied $binary to stage." -ForegroundColor Green
            } else {
                Write-Warning "$binary was not found in stage and could not be resolved."
            }
        }
    }

    # For nssm.exe, just print standard message
    $nssmPath = Join-Path $stageDir 'nssm.exe'
    if (Test-Path -LiteralPath $nssmPath) {
        Write-Host "Found nssm.exe in stage." -ForegroundColor Gray
    } else {
        Write-Host "nssm.exe not found in stage. The installer builder will download it if needed." -ForegroundColor Gray
    }

    # Copy icon.ico for installer
    $iconSource = Join-Path $serverRoot 'Jellyfin.Server\wwwroot\branding\mulletaflix_icon.ico'
    $iconDest = Join-Path $stageDir 'icon.ico'
    if (Test-Path -LiteralPath $iconSource) {
        Copy-Item -LiteralPath $iconSource -Destination $iconDest -Force
        Write-Host "Copied icon.ico to stage." -ForegroundColor Green
    } else {
        Write-Warning "icon.ico not found at $iconSource"
    }

    # Download and extract MariaDB portable if not present
    $mariaDbDir = Join-Path $stageDir 'mariadb'
    $mariaDbExe = Join-Path $mariaDbDir 'bin\mysqld.exe'
    if (-not (Test-Path -LiteralPath $mariaDbExe)) {
        Write-Step 'Downloading MariaDB portable'
        # Use official MariaDB download API
        $mariaDbUrl = 'http://downloads.mariadb.org/rest-api/mariadb/11.4.4/mariadb-11.4.4-winx64.zip'
        $zipPath = Join-Path $env:TEMP 'mariadb-portable.zip'
        try {
            Write-Host "Downloading from $mariaDbUrl ..." -ForegroundColor Gray
            Invoke-WebRequest -Uri $mariaDbUrl -OutFile $zipPath -UseBasicParsing
            if (Test-Path -LiteralPath $mariaDbDir) {
                Remove-Item -LiteralPath $mariaDbDir -Recurse -Force
            }
            Expand-Archive -Path $zipPath -DestinationPath $stageDir -Force
            # The extracted folder is named mariadb-11.4.4-winx64, rename to mariadb
            $extractedDir = Join-Path $stageDir 'mariadb-11.4.4-winx64'
            if (Test-Path -LiteralPath $extractedDir) {
                Move-Item -LiteralPath $extractedDir -Destination $mariaDbDir -Force
            }
            Write-Host "MariaDB portable extracted to $mariaDbDir" -ForegroundColor Green
        } catch {
            Write-Warning "Failed to download/extract MariaDB portable: $_"
        } finally {
            if (Test-Path -LiteralPath $zipPath) {
                Remove-Item -LiteralPath $zipPath -Force
            }
        }
    } else {
        Write-Host "MariaDB portable already present in stage." -ForegroundColor Gray
    }

    # Remove any legacy Python/Nebula directories from stage (Nebula is now 100% C# .NET native)
    $legacyNebula = Join-Path $stageDir 'nebula'
    if (Test-Path -LiteralPath $legacyNebula) {
        Remove-Item -LiteralPath $legacyNebula -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Removed legacy Python nebula directory from stage." -ForegroundColor Green
    }
}

function Build-Installer {
    Write-Step 'Creating installer'
    Assert-Path $installerScript 'Installer script'
    & $installerScript
}

Write-Host '==================================================' -ForegroundColor Cyan
Write-Host '   MulletaFlix Stage + Installer Release Builder   ' -ForegroundColor Cyan
Write-Host '==================================================' -ForegroundColor Cyan

Assert-Path $projectRoot 'Project root'
Assert-Path $stageDir 'Stage directory'

$dotnet = Resolve-DotNet
Write-Host "Using dotnet: $dotnet" -ForegroundColor Green

if (-not $SkipServerBuild) {
    Build-Server -DotNet $dotnet
} else {
    Write-Host 'Skipping server build.' -ForegroundColor Yellow
}

if (-not $SkipWebBuild) {
    Build-Web
} else {
    Write-Host 'Skipping web build.' -ForegroundColor Yellow
}

if (-not $SkipTrayBuild) {
    Build-Tray -DotNet $dotnet
} else {
    Write-Host 'Skipping tray build.' -ForegroundColor Yellow
}

Copy-RuntimeExtras

if (-not $SkipInstaller) {
    Build-Installer
} else {
    Write-Host 'Skipping installer creation.' -ForegroundColor Yellow
}

$stageExe = Join-Path $stageDir 'MulletaFlix.exe'
$installer = Get-ChildItem -LiteralPath (Join-Path $packagingRoot 'jellyfin-server-windows\nsis') -Filter 'mulletaflix_*_windows-x64.exe' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

Write-Host ''
Write-Host '==================================================' -ForegroundColor Green
Write-Host 'Release build finished.' -ForegroundColor Green
Write-Host "Stage executable: $stageExe" -ForegroundColor Green
if ($installer) {
    Write-Host "Installer: $($installer.FullName)" -ForegroundColor Green
    Write-Host "Installer size: $([Math]::Round($installer.Length / 1MB, 2)) MB" -ForegroundColor Green
}
Write-Host 'To run stage with visible logs: .\stage-start.ps1' -ForegroundColor Green
Write-Host '==================================================' -ForegroundColor Green
