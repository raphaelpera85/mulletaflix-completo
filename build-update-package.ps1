<#
.SYNOPSIS
    Builds a standalone update zip package for MulletaFlix (mulletaflix-update-win-x64.zip).
.DESCRIPTION
    Creates a clean update archive containing the compiled server binaries, tray helper,
    MulletaFlix-web client, and apply-update.ps1 script. This archive can be uploaded
    directly to GitHub Releases (https://github.com/raphaelpera85/mulletaflix-completo/releases).
    MulletaFlix instances will automatically detect it and perform seamless in-place updates.
#>

[CmdletBinding()]
param(
    [string]$Version,
    [string]$OutputDir,
    [string]$StageDirectory,
    [string]$InstallerOutputDirectory,
    [switch]$SkipBuild,
    [switch]$SkipWebBuild,
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$projectRoot = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
$sharedVersionPath = Join-Path $projectRoot 'MulletaFlix-master\SharedVersion.cs'
if ([string]::IsNullOrWhiteSpace($Version)) {
    if (-not (Test-Path -LiteralPath $sharedVersionPath)) {
        throw "SharedVersion.cs not found: $sharedVersionPath"
    }

    $versionMatch = Select-String -LiteralPath $sharedVersionPath -Pattern 'AssemblyFileVersion\("([^\"]+)"\)' | Select-Object -First 1
    if (-not $versionMatch) {
        throw "Could not determine the server version from $sharedVersionPath"
    }

    $Version = $versionMatch.Matches[0].Groups[1].Value
}

if (-not $OutputDir) {
    $OutputDir = Join-Path $projectRoot 'dist'
}
$OutputDir = if ([System.IO.Path]::IsPathRooted($OutputDir)) {
    [System.IO.Path]::GetFullPath($OutputDir)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDir))
}

$stageDir = if ([string]::IsNullOrWhiteSpace($StageDirectory)) {
    Join-Path $projectRoot 'stage'
} else {
    [System.IO.Path]::GetFullPath($StageDirectory)
}
$installerOutputDir = if ([string]::IsNullOrWhiteSpace($InstallerOutputDirectory)) {
    $OutputDir
} elseif ([System.IO.Path]::IsPathRooted($InstallerOutputDirectory)) {
    [System.IO.Path]::GetFullPath($InstallerOutputDirectory)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $projectRoot $InstallerOutputDirectory))
}
$webRoot = Join-Path $projectRoot 'MulletaFlix-web-master'
$webDist = Join-Path $webRoot 'dist'
$packagingRoot = Join-Path $projectRoot 'MulletaFlix-packaging-master'
$updaterScriptSource = Join-Path $packagingRoot 'MulletaFlix-ux-custom\nsis\apply-update.ps1'
Import-Module (Join-Path $projectRoot 'tools\release\ReleaseArtifactValidation.psm1') -Force
$sourceCommit = Assert-MulletaFlixGitWorktreeClean -RepositoryRoot $projectRoot

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "   MulletaFlix In-Place Update Package Builder    " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$requestedVersion = Get-MulletaFlixReleaseSemVer -Value $Version
$buildPropsPath = Join-Path $projectRoot 'MulletaFlix-master\Directory.Build.props'
$assemblyMatch = Select-String -LiteralPath $sharedVersionPath -Pattern 'AssemblyFileVersion\("([^"]+)"\)' | Select-Object -First 1
if (-not $assemblyMatch) { throw "Could not determine AssemblyFileVersion from $sharedVersionPath" }
$assemblyVersion = Get-MulletaFlixReleaseSemVer -Value $assemblyMatch.Matches[0].Groups[1].Value
$propsMatch = Select-String -LiteralPath $buildPropsPath -Pattern '<VersionPrefix>([^<]+)</VersionPrefix>' | Select-Object -First 1
if (-not $propsMatch) { throw "Could not determine VersionPrefix from $buildPropsPath" }
$propsVersion = Get-MulletaFlixReleaseSemVer -Value $propsMatch.Matches[0].Groups[1].Value
if ($assemblyVersion -ne $requestedVersion -or $propsVersion -ne $requestedVersion) {
    throw "Source version mismatch: requested=$requestedVersion, SharedVersion=$assemblyVersion, VersionPrefix=$propsVersion. Correct and commit version files before packaging."
}

# 1. Build a fresh, complete Windows stage or validate the supplied stage manifest.
if (-not $SkipBuild) {
    Write-Host "Building a fresh complete Windows stage..." -ForegroundColor Yellow
    $stageBuilder = Join-Path $packagingRoot 'build-stage-and-installer.ps1'
    if (-not (Test-Path -LiteralPath $stageBuilder -PathType Leaf)) { throw "Windows stage builder not found: $stageBuilder" }
    $buildRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot '.build')).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $stageFullPath = [System.IO.Path]::GetFullPath($stageDir).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $stageIsInsideBuildRoot = $stageFullPath.Equals($buildRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        $stageFullPath.StartsWith($buildRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)
    if ($stageFullPath.Equals($buildRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'StageDirectory cannot be the .build root; choose a child stage directory or the default stage folder.'
    }
    $stageBuilderBuildDirectory = if ($stageIsInsideBuildRoot) {
        Join-Path $buildRoot ('package-build-' + [Guid]::NewGuid().ToString('N'))
    } else {
        $buildRoot
    }
    $stageBuilderParameters = @{ StageDirectory = $stageDir; BuildDirectory = $stageBuilderBuildDirectory; SkipInstaller = $true; NoPause = $true }
    if ($SkipWebBuild) { $stageBuilderParameters.SkipWebBuild = $true }
    & $stageBuilder @stageBuilderParameters
} elseif (-not (Test-Path -LiteralPath $stageDir) -or -not (Test-Path -LiteralPath (Join-Path $stageDir 'MulletaFlix.dll'))) {
    throw "Stage directory does not contain MulletaFlix binaries. Run build without -SkipBuild."
}

$stageServerDll = Join-Path $stageDir 'MulletaFlix.dll'
$stageVersion = Get-MulletaFlixBinaryVersion -Path $stageServerDll
if ($stageVersion -ne $requestedVersion) {
    throw "Stage version $stageVersion does not match requested package version $requestedVersion."
}
Assert-MulletaFlixStageManifest -StageDirectory $stageDir -Version $requestedVersion -SourceCommit $sourceCommit | Out-Null

# Bundle the compatible third-party middleware; ApplicationHost installs it into the
# server data plugin directory before PluginManager discovers plugins at startup.
& (Join-Path $projectRoot 'tools\release\stage-file-transformation-plugin.ps1') -ApplicationDirectory $stageDir

# Refresh web assets. Stale stage assets can hide dashboard routes behind the old service worker cache.
if ((Test-Path -LiteralPath (Join-Path $webRoot 'package.json')) -and -not $SkipWebBuild) {
    Write-Host "Building latest web client in an isolated production workspace..." -ForegroundColor Yellow
    $webBuildRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("mulletaflix-web-build-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $webBuildRoot -Force | Out-Null
    try {
        & robocopy.exe $webRoot $webBuildRoot /E /XD node_modules dist .git .vite /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) {
            throw "Could not stage web source for production build (robocopy exit code $LASTEXITCODE)."
        }

        # Start-Process -FilePath cmd.exe spawns a process tree that does not reliably inherit a
        # user-session PATH modification (e.g. a Node version manager or a per-session tool shim) --
        # it found neither 'npm' nor even the machine-wide 'C:\Program Files\nodejs\npm.cmd' in
        # practice. Resolve an absolute path to npm.cmd up front instead of trusting PATH inside the
        # spawned shell. npm.cmd itself then shells out to a bare 'node' for postinstall scripts
        # (core-js, es5-ext, esbuild), so the resolved Node directory must also be prepended to
        # $env:PATH for this process -- Start-Process inherits the calling process's environment
        # block, so this reaches the spawned npm.cmd and its node children too.
        $npmCmd = (Get-Command 'npm.cmd' -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source)
        if (-not $npmCmd) {
            foreach ($candidate in @('C:\Program Files\nodejs\npm.cmd', 'C:\Program Files (x86)\nodejs\npm.cmd')) {
                if (Test-Path -LiteralPath $candidate) {
                    $npmCmd = $candidate
                    break
                }
            }
        }

        if (-not $npmCmd) {
            throw "Could not locate npm.cmd (checked PATH and well-known Node.js install locations)."
        }

        $nodeDir = Split-Path -Parent $npmCmd
        if ($env:PATH -notlike "*$nodeDir*") {
            $env:PATH = "$nodeDir;$env:PATH"
        }

        foreach ($npmArgs in @('ci', 'run build:check', 'run build:production')) {
            $webBuildExitCode = (Start-Process -FilePath $npmCmd -ArgumentList $npmArgs -WorkingDirectory $webBuildRoot -Wait -PassThru -NoNewWindow).ExitCode
            if ($webBuildExitCode -ne 0) {
                throw "Web production command 'npm $npmArgs' failed with exit code $webBuildExitCode"
            }
        }

        $webDist = Join-Path $webBuildRoot 'dist'
        if (-not (Test-Path -LiteralPath $webDist)) {
            throw "Web build output not found: $webDist"
        }

        $stageWeb = Join-Path $stageDir 'MulletaFlix-web'
        if (Test-Path -LiteralPath $stageWeb) {
            Remove-Item -LiteralPath $stageWeb -Recurse -Force
        }
        Copy-Item -LiteralPath $webDist -Destination $stageWeb -Recurse -Force
        Write-Host "Refreshed web assets in stage." -ForegroundColor Green
    }
    finally {
        if (Test-Path -LiteralPath $webBuildRoot) {
            Remove-Item -LiteralPath $webBuildRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
elseif ($SkipWebBuild -and -not (Test-Path -LiteralPath (Join-Path $stageDir 'MulletaFlix-web\index.html'))) {
    throw "Stage directory does not contain a prebuilt web client. Build it or omit -SkipWebBuild."
}

# 2. Ensure apply-update.ps1 is in stage
if (Test-Path -LiteralPath $updaterScriptSource) {
    Copy-Item -LiteralPath $updaterScriptSource -Destination (Join-Path $stageDir 'apply-update.ps1') -Force
    Write-Host "Synced apply-update.ps1 to stage." -ForegroundColor Green
}

# 2.1 Ensure Tools are synced to stage
$toolsSource = Join-Path $projectRoot 'MulletaFlix-master\Tools'
if (Test-Path -LiteralPath $toolsSource) {
    $toolsDest = Join-Path $stageDir 'Tools'
    if (-not (Test-Path -LiteralPath $toolsDest)) {
        New-Item -ItemType Directory -Path $toolsDest -Force | Out-Null
    }
    Copy-Item -LiteralPath (Join-Path $toolsSource 'mount_drive_n.py') -Destination $toolsDest -Force

    # The Python playback stream worker (Tools/NebulaPython) was never part of this sync, so every
    # fresh/updated install was missing it entirely -- NebulaFtpManager.StartPythonStreamServiceAsync
    # throws FileNotFoundException("Runtime Python do Nebula de playback nao foi encontrado.") the
    # first time an operator starts Envio, with no earlier warning. Bundle the script sources (not
    # __pycache__/.pytest_cache/.ruff_cache/.coverage/tests/staging scratch dirs) so FindPythonStreamScript's
    # Tools/NebulaPython/stream_service.py candidate resolves on a clean install. The Python
    # interpreter itself is still resolved at runtime via python.path marker / PATH / well-known
    # paths (FindPythonExe) -- this script does not install Python or pip dependencies.
    $nebulaPythonSource = Join-Path $toolsSource 'NebulaPython'
    if (Test-Path -LiteralPath $nebulaPythonSource) {
        $nebulaPythonDest = Join-Path $toolsDest 'NebulaPython'
        if (Test-Path -LiteralPath $nebulaPythonDest) {
            Remove-Item -LiteralPath $nebulaPythonDest -Recurse -Force
        }
        & (Join-Path $projectRoot 'tools\release\copy-nebula-python-runtime.ps1') `
            -SourceDirectory $nebulaPythonSource `
            -DestinationDirectory $nebulaPythonDest

        Write-Host "Synced Tools/NebulaPython to stage." -ForegroundColor Green
    }
    else {
        Write-Host "WARNING: Tools/NebulaPython source not found; the update package will be missing the Nebula playback Python worker." -ForegroundColor Yellow
    }

    Write-Host "Synced Tools to stage." -ForegroundColor Green
}

# 3. Create clean temp directory for update archive
$verifiedCommit = Assert-MulletaFlixGitWorktreeClean -RepositoryRoot $projectRoot
if ($verifiedCommit -ne $sourceCommit) { throw 'Source commit changed while the update stage was being prepared.' }
$null = New-MulletaFlixStageManifest -StageDirectory $stageDir -Version $requestedVersion -SourceCommit $sourceCommit -SourceTreeClean $true
$tempPackageDir = Join-Path ([System.IO.Path]::GetTempPath()) "mulletaflix-update-pkg-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $tempPackageDir -Force | Out-Null

try {
    Write-Host "Staging update payload files..." -ForegroundColor Cyan
    
    # Exclude user data, logs, temporary files and cache
    $excludePatterns = @('*.log', 'cache', 'log', 'data', 'temp', '*.tmp', '*.pdb')

    Get-ChildItem -Path $stageDir -Recurse | ForEach-Object {
        $relPath = $_.FullName.Substring($stageDir.Length).TrimStart('\', '/')
        
        # Check exclusions
        $skip = $false
        foreach ($pat in $excludePatterns) {
            if ($relPath -like $pat -or $relPath -like "*\$pat" -or $relPath -like "*\$pat\*") {
                $skip = $true
                break
            }
        }

        if (-not $skip) {
            $destPath = Join-Path $tempPackageDir $relPath
            if ($_.PSIsContainer) {
                if (-not (Test-Path -LiteralPath $destPath)) {
                    New-Item -ItemType Directory -Path $destPath -Force | Out-Null
                }
            } else {
                $destParent = [System.IO.Path]::GetDirectoryName($destPath)
                if (-not (Test-Path -LiteralPath $destParent)) {
                    New-Item -ItemType Directory -Path $destParent -Force | Out-Null
                }
                Copy-Item -LiteralPath $_.FullName -Destination $destPath -Force
            }
        }
    }

    # Ensure output directory exists
    if (-not (Test-Path -LiteralPath $OutputDir)) {
        New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    }

    $zipName = "mulletaflix-update-win-x64.zip"
    $zipPath = Join-Path $OutputDir $zipName

    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Write-Host "Compressing update package to $zipPath..." -ForegroundColor Cyan
    
    # Usa explicitamente o tar do Windows. Um `tar` do MSYS/git-bash presente no
    # PATH interpreta "D:\..." como host remoto e falha com "Cannot connect to D:",
    # o que quebra o empacotamento quando o script é invocado a partir de um shell
    # POSIX em vez do PowerShell nativo.
    $systemTar = Join-Path $env:SystemRoot 'System32\tar.exe'
    $tarCmd = if (Test-Path -LiteralPath $systemTar) {
        Get-Command $systemTar -ErrorAction SilentlyContinue
    } else {
        Get-Command tar.exe -ErrorAction SilentlyContinue
    }

    if ($tarCmd) {
        $prevPwd = Get-Location
        try {
            Set-Location $tempPackageDir
            & $tarCmd.Source -a -c -f $zipPath *
        } finally {
            Set-Location $prevPwd
        }
    } else {
        Compress-Archive -Path "$tempPackageDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
    }

    if (-not (Test-Path -LiteralPath $zipPath)) {
        throw "Failed to create update zip at $zipPath"
    }

    $zipItem = Get-Item -LiteralPath $zipPath
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash

    Write-Host ""
    Write-Host "==================================================" -ForegroundColor Green
    Write-Host "Update package built successfully!" -ForegroundColor Green
    Write-Host "Archive: $zipPath" -ForegroundColor Green
    Write-Host "Size: $([Math]::Round($zipItem.Length / 1MB, 2)) MB ($($zipItem.Length) bytes)" -ForegroundColor Green
    Write-Host "SHA256: $hash" -ForegroundColor Green
    Write-Host "Attach this zip to a GitHub Release on raphaelpera85/mulletaflix-completo." -ForegroundColor Cyan
    Write-Host "==================================================" -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $tempPackageDir) {
        Remove-Item -LiteralPath $tempPackageDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# 3. Build the Windows installer executable from the same stage so every release
# ships both artifacts: the in-place update zip and the standalone installer.
# Releases 12.0.46-12.0.62 shipped without the installer because no step built it.
if ($SkipInstaller) {
    Write-Host ""
    Write-Host "Instalador ignorado (-SkipInstaller)." -ForegroundColor Yellow
    Write-Host "Aviso: publish-release.ps1 recusa publicar uma release sem o instalador Windows." -ForegroundColor Yellow
} else {
    $installerScript = Join-Path $projectRoot 'build-mulletaflix-installer.ps1'
    if (-not (Test-Path -LiteralPath $installerScript)) {
        throw "build-mulletaflix-installer.ps1 not found: $installerScript"
    }

    Write-Host ""
    Write-Host "Building Windows installer from stage..." -ForegroundColor Cyan
    & $installerScript -StageDirectory $stageDir -OutputDirectory $installerOutputDir -NoCopyToReleaseAssets
}

$installerPath = $null
if (-not $SkipInstaller) {
    $installerPath = Join-Path $installerOutputDir "mulletaflix_${requestedVersion}_windows-x64.exe"
    if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
        throw "Expected installer was not created from the selected stage: $installerPath"
    }

    $installerVersion = Get-MulletaFlixBinaryVersion -Path $installerPath
    if ($installerVersion -ne $requestedVersion) {
        throw "Installer version $installerVersion does not match requested package version $requestedVersion."
    }
}

$manifestPath = Join-Path $OutputDir 'mulletaflix-release-manifest.json'
$additionalReleaseAssets = @(Get-ChildItem -LiteralPath $OutputDir -File -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -match "(?i)(?:^|_)${requestedVersion}(?:_|-)" -and
    $_.Name -ne 'mulletaflix-update-win-x64.zip' -and
    $_.FullName -ne $installerPath
} | ForEach-Object { $_.FullName })
New-MulletaFlixReleaseManifest `
    -Version $requestedVersion `
    -SourceCommit $sourceCommit `
    -ZipPath $zipPath `
    -InstallerPath $installerPath `
    -AdditionalAssetPaths $additionalReleaseAssets `
    -OutputPath $manifestPath | Out-Null
Write-Host "Release artifact manifest written: $manifestPath" -ForegroundColor Green
