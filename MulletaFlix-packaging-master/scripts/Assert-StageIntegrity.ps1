$ErrorActionPreference = 'Stop'

function Assert-Path {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
}

function Assert-WebBuildIntegrity {
    param([string]$WebDirectory)

    $indexPath = Join-Path $WebDirectory 'index.html'
    Assert-Path $indexPath 'Web index'

    $index = Get-Content -LiteralPath $indexPath -Raw
    $assetReferences = [regex]::Matches($index, '(?:src|href)="(\.\/assets\/[^"?]+)"') |
        ForEach-Object { $_.Groups[1].Value.Substring(2) } |
        Sort-Object -Unique

    if ($assetReferences.Count -eq 0) {
        throw "Web index does not reference any assets: $indexPath"
    }

    foreach ($asset in $assetReferences) {
        $assetPath = Join-Path $WebDirectory $asset
        if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
            throw "Web index references a missing asset: $assetPath"
        }
    }
}

function Assert-StageIntegrity {
    param(
        [string]$StageDirectory,
        [string]$ProjectRoot
    )

    $requiredFiles = @(
        'MulletaFlix.exe',
        'MulletaFlix.dll',
        'MulletaFlix.Server.Implementations.dll',
        'MulletaFlix-web\index.html',
        'MulletaFlix-web\serviceworker.js',
        'Tools\mount_drive_n.py',
        'nssm.exe',
        'mulletaflix-windows-tray\MulletaFlix.Windows.Tray.exe'
    )

    foreach ($relativePath in $requiredFiles) {
        Assert-Path (Join-Path $StageDirectory $relativePath) "Required stage artifact ($relativePath)"
    }

    # Catch mixed/stale .NET publishes before NSIS packages them. The Nebula
    # implementation and model must come from the same publish operation;
    # otherwise startup can fail with MissingMethodException.
    $publishDirectory = Join-Path $ProjectRoot '.build\server-publish'
    if (Test-Path -LiteralPath $publishDirectory -PathType Container) {
        foreach ($assemblyName in @('MediaBrowser.Model.dll', 'MulletaFlix.Server.Implementations.dll', 'MulletaFlix.dll')) {
            $publishedAssembly = Join-Path $publishDirectory $assemblyName
            $stagedAssembly = Join-Path $StageDirectory $assemblyName
            if ((Test-Path -LiteralPath $publishedAssembly -PathType Leaf) -and (Test-Path -LiteralPath $stagedAssembly -PathType Leaf)) {
                $publishedHash = (Get-FileHash -LiteralPath $publishedAssembly -Algorithm SHA256).Hash
                $stagedHash = (Get-FileHash -LiteralPath $stagedAssembly -Algorithm SHA256).Hash
                if ($publishedHash -ne $stagedHash) {
                    throw "Stage assembly differs from the current server publish: $assemblyName"
                }
            }
        }
    }

    Assert-WebBuildIntegrity -WebDirectory (Join-Path $StageDirectory 'MulletaFlix-web')

    if (Test-Path -LiteralPath (Join-Path $StageDirectory 'nebula')) {
        throw 'Legacy Python Nebula directory must not be present in the stage.'
    }

    $leftoverBackups = Get-ChildItem -LiteralPath $ProjectRoot -Filter 'stage-backup-*' -Force -ErrorAction SilentlyContinue
    if ($leftoverBackups) {
        $names = ($leftoverBackups | Select-Object -ExpandProperty Name) -join ', '
        throw "Stage backup artifacts were left behind after the build: $names"
    }
}
