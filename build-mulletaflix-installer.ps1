<#>
.SYNOPSIS
    Builds the MulletaFlix Windows NSIS installer.
.DESCRIPTION
    Compiles the NSIS installer script using makensis with MulletaFlix branding assets.
    This script is called by build-stage-and-installer.ps1 after the stage folder is prepared.
.NOTES
    Run from the project root or anywhere - it resolves paths relative to the project root.
#>

[CmdletBinding()]
param(
    [switch]$VerboseOutput,
    [string]$StageDirectory,
    [string]$OutputDirectory,
    [switch]$NoCopyToReleaseAssets
)

$ErrorActionPreference = 'Stop'

$projectRoot = $PSScriptRoot
$packagingRoot = Join-Path $projectRoot 'MulletaFlix-packaging-master'
$uxCustomRoot = Join-Path $packagingRoot 'MulletaFlix-ux-custom'
$projectRootFullPath = [System.IO.Path]::GetFullPath($projectRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
$stageDir = if ([string]::IsNullOrWhiteSpace($StageDirectory)) {
    Join-Path $projectRoot 'stage'
} else {
    [System.IO.Path]::GetFullPath($StageDirectory).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
}
$nsisScript = Join-Path $uxCustomRoot 'nsis\mulletaflix.nsi'
$outputDir = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Join-Path $packagingRoot 'jellyfin-server-windows\nsis'
} else {
    [System.IO.Path]::GetFullPath($OutputDirectory)
}

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
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

function Find-Makensis {
    $candidates = @(
        'C:\Program Files (x86)\NSIS\makensis.exe',
        'C:\Program Files\NSIS\makensis.exe',
        (Get-Command makensis -ErrorAction SilentlyContinue).Source
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path -LiteralPath $c)) {
            return $c
        }
    }
    throw "makensis.exe not found. Install NSIS 3.x and ensure it's in PATH or at default location."
}

Write-Host '==================================================' -ForegroundColor Cyan
Write-Host '   MulletaFlix NSIS Installer Builder              ' -ForegroundColor Cyan
Write-Host '==================================================' -ForegroundColor Cyan

Assert-Path $projectRoot 'Project root'
if ($stageDir.Equals($projectRootFullPath, [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $stageDir.StartsWith($projectRootFullPath + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Installer stage must stay inside the project root: $stageDir"
}
Assert-Path $stageDir 'Stage directory (run build-stage-and-installer.ps1 first)'
Assert-Path $nsisScript 'NSIS script'

$makensis = Find-Makensis
Write-Host "Using makensis: $makensis" -ForegroundColor Green

# Verify stage has required files
$requiredFiles = @('MulletaFlix.exe', 'MulletaFlix.dll', 'mulletaflix-windows-tray', 'icon.ico')
foreach ($file in $requiredFiles) {
    $path = Join-Path $stageDir $file
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required file missing from stage: $path"
    }
}

# Compute output from the staged assembly version before running makensis so an existing artifact is never overwritten.
$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $stageDir 'MulletaFlix.dll'))
$versionSource = if (-not [string]::IsNullOrWhiteSpace($versionInfo.ProductVersion)) { $versionInfo.ProductVersion } else { $versionInfo.FileVersion }
if ($versionSource -notmatch '^(\d+)\.(\d+)\.(\d+)') {
    throw "Unable to determine a valid staged MulletaFlix version: $versionSource"
}
$version = "$($Matches[1]).$($Matches[2]).$($Matches[3])"
$installerName = "mulletaflix_$version`_windows-x64.exe"
$installer = Join-Path $outputDir $installerName
if (Test-Path -LiteralPath $installer) {
    throw "Refusing to overwrite an existing installer: $installer"
}

# Ensure output directory exists
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

# Build the installer
Write-Step 'Compiling NSIS installer...'

$uxPath = $uxCustomRoot
$cmd = & $makensis `
    /Dx64 `
    /DUXPATH="$uxPath" `
    /DInstallLocation="$stageDir" `
    /DINSTALLER_OUTFILE="$installer" `
    "$nsisScript"

if ($LASTEXITCODE -ne 0) {
    throw "NSIS compilation failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $installer)) {
    throw "Installer executable was not generated at the requested isolated output: $installer"
}

$releaseAsset = $null
if (-not $NoCopyToReleaseAssets) {
    $releaseAssetsDir = Join-Path $projectRoot 'dist'
    New-Item -ItemType Directory -Force -Path $releaseAssetsDir | Out-Null
    $releaseAsset = Join-Path $releaseAssetsDir $installerName
    if (Test-Path -LiteralPath $releaseAsset) {
        throw "Refusing to overwrite an existing release asset: $releaseAsset"
    }
    Copy-Item -LiteralPath $installer -Destination $releaseAsset
}

Write-Host ""
Write-Host '==================================================' -ForegroundColor Green
Write-Host 'Installer build completed successfully!' -ForegroundColor Green
Write-Host "Output: $installer" -ForegroundColor Green
if ($releaseAsset) {
    Write-Host "Release asset: $releaseAsset" -ForegroundColor Green
}
Write-Host "Size: $([Math]::Round((Get-Item -LiteralPath $installer).Length / 1MB, 2)) MB" -ForegroundColor Green
Write-Host '==================================================' -ForegroundColor Green
