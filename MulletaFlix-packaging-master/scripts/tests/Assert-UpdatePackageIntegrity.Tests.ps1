$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$validator = Join-Path $PSScriptRoot '..\Assert-UpdatePackageIntegrity.ps1'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "mfx-update-package-$([guid]::NewGuid().ToString('N'))"
$stage = Join-Path $fixtureRoot 'stage'
$package = Join-Path $fixtureRoot 'package.zip'

function New-FixturePackage {
    if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package -Force }
    [System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $package)
}

function Add-ArchiveEntry {
    param([string] $Name)
    $archive = [System.IO.Compression.ZipFile]::Open($package, [System.IO.Compression.ZipArchiveMode]::Update)
    try { $null = $archive.CreateEntry($Name) }
    finally { $archive.Dispose() }
}

function Assert-Rejected {
    param([string] $Description)
    $rejected = $false
    try { & $validator -StageDirectory $stage -PackagePath $package | Out-Null }
    catch { $rejected = $true }
    if (-not $rejected) { throw "Validator accepted invalid fixture: $Description" }
}

try {
    $files = @(
        'MulletaFlix.dll', 'MulletaFlix.exe', 'apply-update.ps1',
        'MulletaFlix-web\index.html',
        'mulletaflix-windows-tray\MulletaFlix.Windows.Tray.exe',
        'Tools\NebulaPython\stream_service.py', 'Tools\NebulaPython\requirements.lock',
        'mariadb\bin\mysqld.exe'
    )
    foreach ($file in $files) {
        $path = Join-Path $stage $file
        New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
        [System.IO.File]::WriteAllText($path, "fixture:$file")
    }
    New-FixturePackage
    & $validator -StageDirectory $stage -PackagePath $package

    [System.IO.File]::AppendAllText((Join-Path $stage 'MulletaFlix.dll'), '-changed')
    Assert-Rejected 'staged file hash differs from packaged payload'

    New-FixturePackage
    Add-ArchiveEntry '../escape.txt'
    Assert-Rejected 'archive contains a traversal path'

    New-FixturePackage
    Add-ArchiveEntry '/MulletaFlix.dll'
    Assert-Rejected 'archive contains an absolute path'

    New-FixturePackage
    Add-ArchiveEntry 'MulletaFlix.dll'
    Assert-Rejected 'archive contains a duplicate file path'

    Remove-Item -LiteralPath (Join-Path $stage 'mariadb\bin\mysqld.exe')
    New-FixturePackage
    Assert-Rejected 'required MariaDB server executable is absent'

    Write-Host 'Update package integrity tests passed: valid archive accepted; payload mismatch, traversal, absolute path, duplicate path, and missing runtime rejected.' -ForegroundColor Green
} finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        $resolved = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $fixtureRoot).Path)
        $temp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        if (-not $resolved.StartsWith($temp, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove fixture outside temp root: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
