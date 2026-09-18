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
    [string]$Version = "12.0.2",
    [string]$OutputDir,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$projectRoot = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
if (-not $OutputDir) {
    $OutputDir = Join-Path $projectRoot 'dist'
}

$stageDir = Join-Path $projectRoot 'stage'
$packagingRoot = Join-Path $projectRoot 'MulletaFlix-packaging-master'
$updaterScriptSource = Join-Path $packagingRoot 'MulletaFlix-ux-custom\nsis\apply-update.ps1'

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "   MulletaFlix In-Place Update Package Builder    " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. Build server binaries to stage
if (-not $SkipBuild) {
    Write-Host "Building and publishing latest server binaries to stage..." -ForegroundColor Yellow
    & dotnet publish (Join-Path $projectRoot 'MulletaFlix-master\Jellyfin.Server') `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -o $stageDir `
        -p:DebugSymbols=false `
        -p:DebugType=none `
        -p:GenerateDocumentationFile=false `
        -p:RunAnalyzersDuringBuild=false `
        -p:RunAnalyzers=false
    if ($LASTEXITCODE -ne 0) {
        throw "Server publish failed with exit code $LASTEXITCODE"
    }
} elseif (-not (Test-Path -LiteralPath $stageDir) -or -not (Test-Path -LiteralPath (Join-Path $stageDir 'MulletaFlix.dll'))) {
    throw "Stage directory does not contain MulletaFlix binaries. Run build without -SkipBuild."
}

# 2. Ensure apply-update.ps1 is in stage
if (Test-Path -LiteralPath $updaterScriptSource) {
    Copy-Item -LiteralPath $updaterScriptSource -Destination (Join-Path $stageDir 'apply-update.ps1') -Force
    Write-Host "Synced apply-update.ps1 to stage." -ForegroundColor Green
}

# 3. Create clean temp directory for update archive
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
    
    $tarCmd = Get-Command tar.exe -ErrorAction SilentlyContinue
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
