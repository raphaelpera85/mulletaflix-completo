# Builds MulletaFlix, updates the local stage folder, and creates the Windows installer.
# Run from anywhere:
#   powershell -ExecutionPolicy Bypass -File .\MulletaFlix-packaging-master\build-stage-and-installer.ps1

[CmdletBinding()]
param(
    [switch]$SkipWebBuild,
    [switch]$SkipServerBuild,
    [switch]$SkipTrayBuild,
    [switch]$SkipInstaller,
    [switch]$NoPause,
    [string]$StageDirectory,
    [string]$BuildDirectory,
    [string]$InstallerOutputDirectory
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$projectRoot = Split-Path -Parent $PSScriptRoot
$serverRoot = Join-Path $projectRoot 'MulletaFlix-master'
$webRoot = Join-Path $projectRoot 'MulletaFlix-web-master'
$packagingRoot = Join-Path $projectRoot 'MulletaFlix-packaging-master'
$projectRootFullPath = [System.IO.Path]::GetFullPath($projectRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
$buildRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot '.build')).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
$defaultStageDir = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'stage')).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
$stageDir = if ([string]::IsNullOrWhiteSpace($StageDirectory)) {
    Join-Path $projectRoot 'stage'
} else {
    [System.IO.Path]::GetFullPath($StageDirectory)
}
$buildDir = if ([string]::IsNullOrWhiteSpace($BuildDirectory)) {
    Join-Path $projectRoot '.build'
} else {
    [System.IO.Path]::GetFullPath($BuildDirectory)
}
$installerOutputDir = if ([string]::IsNullOrWhiteSpace($InstallerOutputDirectory)) {
    Join-Path $buildDir 'installer-output'
} else {
    [System.IO.Path]::GetFullPath($InstallerOutputDirectory)
}
if ((Test-Path -LiteralPath $buildRoot) -and ((Get-Item -LiteralPath $buildRoot -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
    throw "Build root must not be a symbolic link or junction: $buildRoot"
}
$normalizedStageDir = [System.IO.Path]::GetFullPath($stageDir).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
$normalizedBuildDir = [System.IO.Path]::GetFullPath($buildDir).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
$normalizedInstallerOutputDir = [System.IO.Path]::GetFullPath($installerOutputDir).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
$stageInsideBuild = $normalizedStageDir.StartsWith($buildRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)
$buildInsideBuild = $normalizedBuildDir.Equals($buildRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    $normalizedBuildDir.StartsWith($buildRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)
if (-not $stageInsideBuild -and -not $normalizedStageDir.Equals($defaultStageDir, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Stage output must use the dedicated default stage directory or stay inside .build: $normalizedStageDir"
}
if (-not $buildInsideBuild) {
    throw "Build output must stay inside .build: $normalizedBuildDir"
}
if (-not $normalizedInstallerOutputDir.StartsWith($normalizedBuildDir + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Installer output must stay inside the selected build directory: $normalizedInstallerOutputDir"
}
foreach ($outputPath in @($normalizedStageDir, $normalizedBuildDir, $normalizedInstallerOutputDir)) {
    if ($outputPath.Equals($buildRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        continue
    }

    if ($outputPath.Equals($defaultStageDir, [System.StringComparison]::OrdinalIgnoreCase)) {
        $currentPath = $projectRootFullPath
        $relativeSegments = @('stage')
    } else {
        $currentPath = $buildRoot
        $relativeSegments = $outputPath.Substring($buildRoot.Length + 1).Split([System.IO.Path]::DirectorySeparatorChar)
    }

    foreach ($segment in $relativeSegments) {
        $currentPath = Join-Path $currentPath $segment
        if ((Test-Path -LiteralPath $currentPath) -and ((Get-Item -LiteralPath $currentPath -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
            throw "Output path must not traverse a symbolic link or junction: $currentPath"
        }
    }
}
if ($normalizedStageDir.Equals($normalizedBuildDir, [System.StringComparison]::OrdinalIgnoreCase) -or
    $normalizedStageDir.StartsWith($normalizedBuildDir + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -or
    $normalizedBuildDir.StartsWith($normalizedStageDir + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'StageDirectory and BuildDirectory must be separate, non-overlapping directories.'
}
$serverPublishDir = Join-Path $buildDir 'server-publish'
$installerScript = Join-Path $projectRoot 'build-mulletaflix-installer.ps1'
$trayProject = Join-Path $packagingRoot 'jellyfin-server-windows\Jellyfin.Windows.Tray\Jellyfin.Windows.Tray.csproj'
$trayProjectDir = Split-Path -Parent $trayProject
$webDist = Join-Path $webRoot 'dist'
$stageIntegrityScript = Join-Path $packagingRoot 'scripts\Assert-StageIntegrity.ps1'
$stageBackupRoot = Join-Path $buildRoot 'stage-preserve-backups'
$stageBackupHelper = Join-Path $packagingRoot 'scripts\StageBackup.ps1'

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

if (-not (Test-Path -LiteralPath $stageIntegrityScript)) {
    throw "Stage integrity validator not found: $stageIntegrityScript"
}
. $stageIntegrityScript
Assert-Path $stageBackupHelper 'Stage backup safety helpers'
. $stageBackupHelper
$stageBackupRoot = Initialize-StageBackupRoot -BuildRoot $buildRoot -StageDirectory $stageDir

function Copy-DirectoryContents {
    param(
        [string]$Source,
        [string]$Destination
    )

    Assert-Path $Source 'Source directory'
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null

    & robocopy.exe $Source $Destination /E /R:2 /W:1 /NFL /NDL /NJH /NJS /nc /ns /np
    $robocopyExitCode = $LASTEXITCODE
    if ($robocopyExitCode -ge 8) {
        throw "robocopy failed from $Source to $Destination with exit code $robocopyExitCode"
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
    Assert-WebBuildIntegrity -WebDirectory $stageWeb
    Write-Host "Web copied to: $stageWeb" -ForegroundColor Green
}

function Build-Server {
    param([string]$DotNet)

    Write-Step 'Publishing server to stage'
    Assert-Path $serverRoot 'Server source'

    $backupItems = @()
    $preserves = @('mariadb', 'MulletaFlix-web', 'mulletaflix-windows-tray', 'ffmpeg.exe', 'ffprobe.exe', 'nssm.exe', 'icon.ico', 'LICENSE', 'nginx', 'win-acme', 'configure-https.ps1')
    $backupRunDirectory = New-StageBackupRunDirectory -BackupRoot $stageBackupRoot
    Restore-OrphanedStageBackups -BackupRoot $stageBackupRoot -StageDirectory $stageDir -Items $preserves -CurrentRunDirectory $backupRunDirectory

    foreach ($item in $preserves) {
        $sourcePath = Join-Path $stageDir $item
        if (Test-Path -LiteralPath $sourcePath) {
            Assert-NoStageBackupReparsePoints -Path $sourcePath
            $tempBackupPath = Join-Path $backupRunDirectory "stage-backup-$item"
            Copy-Item -LiteralPath $sourcePath -Destination $tempBackupPath -Recurse -Force
            $backupItems += [PSCustomObject]@{
                Item = $item
                BackupPath = $tempBackupPath
            }
            Write-Host "Backed up $item from stage..." -ForegroundColor Gray
        }
    }

    try {
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

    & $DotNet restore $serverProject `
        -r win-x64 `
        --nologo `
        -v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Server restore failed with exit code $LASTEXITCODE"
    }

    & $DotNet clean $serverProject `
        -c Release `
        -r win-x64 `
        --nologo `
        -v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Server clean failed with exit code $LASTEXITCODE"
    }

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

    $publishedImplementations = Join-Path $serverPublishDir 'MulletaFlix.Server.Implementations.dll'
    $stagedImplementations = Join-Path $stageDir 'MulletaFlix.Server.Implementations.dll'
    Assert-Path $publishedImplementations 'Published server implementation'
    Assert-Path $stagedImplementations 'Staged server implementation'
    $publishedHash = (Get-FileHash -LiteralPath $publishedImplementations -Algorithm SHA256).Hash
    $stagedHash = (Get-FileHash -LiteralPath $stagedImplementations -Algorithm SHA256).Hash
    if ($publishedHash -ne $stagedHash) {
        throw "Server implementation was not copied intact to stage (publish=$publishedHash, stage=$stagedHash)"
    }

        Write-Host "Server copied to: $stageDir" -ForegroundColor Green
    }
    finally {
        # Always restore preserved stage content, including when restore,
        # publish, or the file copy fails. This keeps a failed build from
        # destroying the last usable local stage.
        Write-Host 'Restoring preserved stage content...' -ForegroundColor Gray

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

        if ((Test-Path -LiteralPath $backupRunDirectory) -and
            -not (Get-ChildItem -LiteralPath $backupRunDirectory -Force | Select-Object -First 1)) {
            Remove-Item -LiteralPath $backupRunDirectory -Force -ErrorAction SilentlyContinue
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

    & $DotNet restore $trayProject `
        -r win-x64 `
        --nologo `
        -v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Tray restore failed with exit code $LASTEXITCODE"
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

    $nebulaPythonSource = Join-Path $serverRoot 'Tools\NebulaPython'
    $nebulaPythonStage = Join-Path $stageDir 'Tools\NebulaPython'
    if (-not (Test-Path -LiteralPath (Join-Path $nebulaPythonSource 'stream_service.py') -PathType Leaf)) {
        throw "Python Nebula playback worker not found under $nebulaPythonSource"
    }
    if (Test-Path -LiteralPath $nebulaPythonStage) {
        Remove-Item -LiteralPath $nebulaPythonStage -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $nebulaPythonStage | Out-Null
    foreach ($runtimeFile in @('main.py', 'stream_service.py', 'control_plane.py', 'generate_strm.py', 'requirements.in', 'requirements.txt', 'requirements.lock')) {
        Copy-Item -LiteralPath (Join-Path $nebulaPythonSource $runtimeFile) -Destination $nebulaPythonStage -Force
    }
    $stagePythonFtp = Join-Path $nebulaPythonStage 'ftp'
    New-Item -ItemType Directory -Force -Path $stagePythonFtp | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $nebulaPythonSource 'ftp') -File -Filter '*.py' |
        Copy-Item -Destination $stagePythonFtp -Force
    $stagePythonTools = Join-Path $nebulaPythonStage 'tools'
    New-Item -ItemType Directory -Force -Path $stagePythonTools | Out-Null
    foreach ($runtimeTool in @('__init__.py', 'check_deps.py', 'strm_downloader.py', 'feed_ftp.py', 'supabase_sync.py', 'mongo_admission.py')) {
        $runtimeToolPath = Join-Path $nebulaPythonSource (Join-Path 'tools' $runtimeTool)
        if (Test-Path -LiteralPath $runtimeToolPath -PathType Leaf) {
            Copy-Item -LiteralPath $runtimeToolPath -Destination $stagePythonTools -Force
        }
    }
    Write-Host 'Copied Python playback worker and runtime modules to stage.' -ForegroundColor Green

    $mountScript = Join-Path $serverRoot 'Tools\mount_drive_n.py'
    if (Test-Path -LiteralPath $mountScript -PathType Leaf) {
        $stageTools = Join-Path $stageDir 'Tools'
        New-Item -ItemType Directory -Force -Path $stageTools | Out-Null
        Copy-Item -LiteralPath $mountScript -Destination (Join-Path $stageTools 'mount_drive_n.py') -Force
        Remove-Item -LiteralPath (Join-Path $stageDir 'mount_drive_n.py') -Force -ErrorAction SilentlyContinue
        Write-Host 'Copied Python Nebula mount helper to stage.' -ForegroundColor Green
    } else {
        throw "Python Nebula mount helper not found at $mountScript"
    }

    $supportLicense = Join-Path $packagingRoot 'jellyfin-server-windows\Support Files\LICENSE'
    if (Test-Path -LiteralPath $supportLicense) {
        Copy-Item -LiteralPath $supportLicense -Destination (Join-Path $stageDir 'LICENSE') -Force
    } else {
        throw "Support license not found at $supportLicense"
    }

    $mongoScript = Join-Path $packagingRoot 'jellyfin-server-windows\Support Files\install-mongodb-if-missing.ps1'
    if (Test-Path -LiteralPath $mongoScript) {
        Copy-Item -LiteralPath $mongoScript -Destination (Join-Path $stageDir 'install-mongodb-if-missing.ps1') -Force
        Write-Host "Copied install-mongodb-if-missing.ps1 to stage." -ForegroundColor Green
    }

    $winfspScript = Join-Path $packagingRoot 'install-winfsp-if-missing.ps1'
    if (Test-Path -LiteralPath $winfspScript) {
        Copy-Item -LiteralPath $winfspScript -Destination (Join-Path $stageDir 'install-winfsp-if-missing.ps1') -Force
        Write-Host 'Copied WinFsp prerequisite checker to stage.' -ForegroundColor Green
    } else {
        throw "WinFsp prerequisite checker not found at $winfspScript"
    }

    $pythonScript = Join-Path $packagingRoot 'install-python-if-missing.ps1'
    if (Test-Path -LiteralPath $pythonScript) {
        Copy-Item -LiteralPath $pythonScript -Destination (Join-Path $stageDir 'install-python-if-missing.ps1') -Force
        Write-Host 'Copied Python prerequisite installer to stage.' -ForegroundColor Green
    } else {
        throw "Python prerequisite installer not found at $pythonScript"
    }

    $rcloneScript = Join-Path $packagingRoot 'install-rclone-if-missing.ps1'
    if (Test-Path -LiteralPath $rcloneScript) {
        Copy-Item -LiteralPath $rcloneScript -Destination (Join-Path $stageDir 'install-rclone-if-missing.ps1') -Force
        Write-Host 'Copied rclone prerequisite installer to stage.' -ForegroundColor Green
    }

    foreach ($binary in @('ffmpeg.exe', 'ffprobe.exe', 'rclone.exe')) {
        $binaryDestination = if ($binary -eq 'rclone.exe') { Join-Path $stageTools $binary } else { Join-Path $stageDir $binary }
        $existing = $binaryDestination
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
                Copy-Item -LiteralPath $foundPath -Destination $binaryDestination -Force
                Write-Host "Copied $binary to stage." -ForegroundColor Green
            } else {
                if ($binary -eq 'rclone.exe') {
                    Write-Warning 'rclone.exe não foi encontrado. O instalador fará o download durante a instalação.'
                } else {
                    Write-Warning "$binary was not found in stage and could not be resolved."
                }
            }
        }
    }

    # NSSM is required for the Windows service. Do not generate an installer
    # that can copy the server but cannot register/start its service.
    $nssmPath = Join-Path $stageDir 'nssm.exe'
    if (-not (Test-Path -LiteralPath $nssmPath -PathType Leaf)) {
        Write-Step 'Downloading NSSM service wrapper'
        $nssmVersion = '2.24'
        $nssmUrl = "https://nssm.cc/release/nssm-$nssmVersion.zip"
        $nssmZip = Join-Path $env:TEMP "mulletaflix-nssm-$nssmVersion.zip"
        $nssmExtract = Join-Path $env:TEMP "mulletaflix-nssm-$nssmVersion"
        try {
            Invoke-WebRequest -Uri $nssmUrl -OutFile $nssmZip -UseBasicParsing
            if (Test-Path -LiteralPath $nssmExtract) {
                Remove-Item -LiteralPath $nssmExtract -Recurse -Force
            }
            Expand-Archive -LiteralPath $nssmZip -DestinationPath $nssmExtract -Force
            $downloadedNssm = Get-ChildItem -LiteralPath $nssmExtract -Filter 'nssm.exe' -Recurse -File |
                Where-Object { $_.FullName -match '[\\/]win64[\\/]nssm\.exe$' } |
                Select-Object -First 1
            if (-not $downloadedNssm) {
                throw "NSSM win64 executable was not found in $nssmUrl"
            }
            Copy-Item -LiteralPath $downloadedNssm.FullName -Destination $nssmPath -Force
            Write-Host "Copied NSSM $nssmVersion to stage." -ForegroundColor Green
        } catch {
            throw "NSSM is required for service installation but could not be prepared: $($_.Exception.Message)"
        } finally {
            Remove-Item -LiteralPath $nssmZip -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $nssmExtract -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Assert-Path $nssmPath 'NSSM service wrapper'

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
        $mariaDbUrl = 'https://archive.mariadb.org/mariadb-11.4.13/winx64-packages/mariadb-11.4.13-winx64.zip'
        $zipPath = Join-Path $env:TEMP 'mariadb-portable.zip'
        try {
            Write-Host "Downloading from $mariaDbUrl ..." -ForegroundColor Gray
            Invoke-WebRequest -Uri $mariaDbUrl -OutFile $zipPath -UseBasicParsing
            if (Test-Path -LiteralPath $mariaDbDir) {
                Remove-Item -LiteralPath $mariaDbDir -Recurse -Force
            }
            Expand-Archive -Path $zipPath -DestinationPath $stageDir -Force
            # The extracted folder is named mariadb-11.4.13-winx64, rename to mariadb
            $extractedDir = Join-Path $stageDir 'mariadb-11.4.13-winx64'
            if (Test-Path -LiteralPath $extractedDir) {
                Move-Item -LiteralPath $extractedDir -Destination $mariaDbDir -Force
            }
            Write-Host "MariaDB portable extracted to $mariaDbDir" -ForegroundColor Green
        } catch {
            Write-Warning "Failed to download/extract MariaDB portable: $_"
            throw "MariaDB não pôde ser incluído no instalador. Verifique a rede e gere o instalador novamente."
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
    & $installerScript -StageDirectory $stageDir -OutputDirectory $installerOutputDir
}

function Prepare-HttpsTools {
    Write-Step 'Preparing Nginx and ACME HTTPS tools'

    $configureScript = Join-Path $projectRoot 'tools\release\configure-https.ps1'
    $duckDnsScript = Join-Path $projectRoot 'tools\release\duckdns-update.ps1'
    Assert-Path $configureScript 'HTTPS configurator'
    Assert-Path $duckDnsScript 'DuckDNS updater'
    Copy-Item -LiteralPath $configureScript -Destination (Join-Path $stageDir 'configure-https.ps1') -Force
    Copy-Item -LiteralPath $duckDnsScript -Destination (Join-Path $stageDir 'duckdns-update.ps1') -Force

    $nginxDir = Join-Path $stageDir 'nginx'
    if (-not (Test-Path -LiteralPath (Join-Path $nginxDir 'nginx.exe'))) {
        $nginxVersion = '1.30.5'
        $nginxZip = Join-Path $env:TEMP "mulletaflix-nginx-$nginxVersion.zip"
        $nginxExtract = Join-Path $env:TEMP "mulletaflix-nginx-$nginxVersion"
        try {
            Invoke-WebRequest -Uri "https://nginx.org/download/nginx-$nginxVersion.zip" -OutFile $nginxZip -UseBasicParsing
            if (Test-Path -LiteralPath $nginxExtract) { Remove-Item -LiteralPath $nginxExtract -Recurse -Force }
            Expand-Archive -LiteralPath $nginxZip -DestinationPath $nginxExtract
            $source = Join-Path $nginxExtract "nginx-$nginxVersion"
            if (Test-Path -LiteralPath $nginxDir) { Remove-Item -LiteralPath $nginxDir -Recurse -Force }
            Move-Item -LiteralPath $source -Destination $nginxDir
        } finally {
            Remove-Item -LiteralPath $nginxZip -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $nginxExtract -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    $wacsDir = Join-Path $stageDir 'win-acme'
    if (-not (Test-Path -LiteralPath (Join-Path $wacsDir 'wacs.exe'))) {
        $wacsVersion = '2.2.9.1701'
        $wacsZip = Join-Path $env:TEMP "mulletaflix-win-acme-$wacsVersion.zip"
        New-Item -ItemType Directory -Force -Path $wacsDir | Out-Null
        try {
            Invoke-WebRequest -Uri "https://github.com/win-acme/win-acme/releases/download/v$wacsVersion/win-acme.v$wacsVersion.x64.trimmed.zip" -OutFile $wacsZip -UseBasicParsing
            Expand-Archive -LiteralPath $wacsZip -DestinationPath $wacsDir -Force
        } finally {
            Remove-Item -LiteralPath $wacsZip -Force -ErrorAction SilentlyContinue
        }
    }

    Assert-Path (Join-Path $nginxDir 'nginx.exe') 'Nginx payload'
    Assert-Path (Join-Path $wacsDir 'wacs.exe') 'win-acme payload'
    Write-Host 'Nginx, win-acme and HTTPS configurator are included in the stage.' -ForegroundColor Green
}

Write-Host '==================================================' -ForegroundColor Cyan
Write-Host '   MulletaFlix Stage + Installer Release Builder   ' -ForegroundColor Cyan
Write-Host '==================================================' -ForegroundColor Cyan

Assert-Path $projectRoot 'Project root'
New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
Assert-Path $stageDir 'Stage directory'

$dotnet = Resolve-DotNet
Write-Host "Using dotnet: $dotnet" -ForegroundColor Green

$backupLock = Enter-StageBackupLock -BackupRoot $buildRoot
try {
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
    Prepare-HttpsTools
    Write-Step 'Validating stage integrity'
    Assert-StageIntegrity -StageDirectory $stageDir -ProjectRoot $projectRoot -PublishDirectory $serverPublishDir

    if (-not $SkipInstaller) {
        Build-Installer
    } else {
        Write-Host 'Skipping installer creation.' -ForegroundColor Yellow
    }

    $stageExe = Join-Path $stageDir 'MulletaFlix.exe'
    $installer = Get-ChildItem -LiteralPath $installerOutputDir -Filter 'mulletaflix_*_windows-x64.exe' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    Write-Host ''
    Write-Host '==================================================' -ForegroundColor Green
    Write-Host 'Stage build finished successfully.' -ForegroundColor Green
    Write-Host "Stage executable: $stageExe" -ForegroundColor Green
    if (-not $SkipInstaller -and $installer) {
        Write-Host "Installer: $($installer.FullName)" -ForegroundColor Green
        Write-Host "Installer size: $([Math]::Round($installer.Length / 1MB, 2)) MB" -ForegroundColor Green
    }
    Write-Host 'To run stage with visible logs: .\stage\MulletaFlix.exe --datadir .\stage-data' -ForegroundColor Green
    Write-Host '==================================================' -ForegroundColor Green
}
finally {
    $backupLock.Dispose()
}
