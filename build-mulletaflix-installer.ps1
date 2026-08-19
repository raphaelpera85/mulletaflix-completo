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
    [switch]$VerboseOutput
)

$ErrorActionPreference = 'Stop'

$projectRoot = $PSScriptRoot
$packagingRoot = Join-Path $projectRoot 'MulletaFlix-packaging-master'
$uxCustomRoot = Join-Path $packagingRoot 'MulletaFlix-ux-custom'
$stageDir = Join-Path $projectRoot 'stage'
$nsisScript = Join-Path $uxCustomRoot 'nsis\mulletaflix.nsi'
$outputDir = Join-Path $packagingRoot 'jellyfin-server-windows\nsis'

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
Assert-Path $stageDir 'Stage directory (run build-stage-and-installer.ps1 first)'
Assert-Path $nsisScript 'NSIS script'

$makensis = Find-Makensis
Write-Host "Using makensis: $makensis" -ForegroundColor Green

# Verify stage has required files
$requiredFiles = @('MulletaFlix.exe', 'MulletaFlix.dll', 'mulletaflix-windows-tray', 'icon.ico')
foreach ($file in $requiredFiles) {
    $path = Join-Path $stageDir $file
    if (-not (Test-Path -LiteralPath $path)) {
        Write-Warning "Required file missing from stage: $path"
    }
}

# Ensure output directory exists
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

# Build the installer
Write-Step 'Compiling NSIS installer...'

$uxPath = $uxCustomRoot

# Run makensis from output directory so OutFile lands there
Set-Location $outputDir
$cmd = & $makensis `
    /Dx64 `
    /DUXPATH="$uxPath" `
    /DInstallLocation="$stageDir" `
    "$nsisScript"
Set-Location $projectRoot

if ($LASTEXITCODE -ne 0) {
    throw "NSIS compilation failed with exit code $LASTEXITCODE"
}

# Find the generated installer (NSIS puts it next to the .nsi script)
$installer = Get-ChildItem -LiteralPath (Join-Path $uxCustomRoot 'nsis') -Filter 'mulletaflix_*_windows-x64.exe' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $installer) {
    throw "Installer executable not found in $outputDir after compilation"
}

Write-Host ""
Write-Host '==================================================' -ForegroundColor Green
Write-Host 'Installer build completed successfully!' -ForegroundColor Green
Write-Host "Output: $($installer.FullName)" -ForegroundColor Green
Write-Host "Size: $([Math]::Round($installer.Length / 1MB, 2)) MB" -ForegroundColor Green
Write-Host '==================================================' -ForegroundColor Green