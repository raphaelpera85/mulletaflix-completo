[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $StageDirectory,
    [Parameter(Mandatory = $true)][string] $PackagePath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression

function Get-StageFiles {
    param([string] $Root)

    $excludedDirectories = @('cache', 'log', 'data', 'temp')
    Get-ChildItem -LiteralPath $Root -File -Recurse | Where-Object {
        $normalizedRoot = $Root.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
        $relative = $_.FullName.Substring($normalizedRoot.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
        $segments = $relative -split '[\\/]'
        $excluded = $false
        foreach ($segment in $segments) {
            if ($segment -in $excludedDirectories) { $excluded = $true; break }
        }

        -not $excluded -and $_.Name -notmatch '(?i)\.(log|tmp|pdb)$'
    }
}

$stage = (Resolve-Path -LiteralPath $StageDirectory).Path
$package = (Resolve-Path -LiteralPath $PackagePath).Path
$required = @(
    'MulletaFlix.dll',
    'MulletaFlix.exe',
    'apply-update.ps1',
    'MulletaFlix-web/index.html',
    'mulletaflix-windows-tray/MulletaFlix.Windows.Tray.exe',
    'Tools/NebulaPython/stream_service.py',
    'Tools/NebulaPython/requirements.lock',
    'mariadb/bin/mysqld.exe'
)
$expectedFiles = @{}
foreach ($file in Get-StageFiles -Root $stage) {
    $relative = $file.FullName.Substring($stage.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar).Length).TrimStart([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar).Replace('\', '/')
    $expectedFiles[$relative] = $file.FullName
}

$archive = [System.IO.Compression.ZipFile]::OpenRead($package)
try {
    $entries = @{}
    foreach ($entry in $archive.Entries) {
        if ([string]::IsNullOrEmpty($entry.Name)) { continue }
        $name = $entry.FullName.Replace('\', '/')
        if ($name.StartsWith('/') -or $name.Contains(':') -or $name -match '(^|/)(\.|\.\.)(/|$)') {
            throw "Unsafe path in update package: $name"
        }
        if ($entries.ContainsKey($name)) { throw "Duplicate path in update package: $name" }
        $entries[$name] = $entry
    }

    foreach ($requiredPath in $required) {
        if (-not $entries.ContainsKey($requiredPath)) {
            throw "Required update package file is missing: $requiredPath"
        }
    }

    if ($entries.Count -ne $expectedFiles.Count) {
        throw "Update package file count mismatch. Stage has $($expectedFiles.Count), archive has $($entries.Count)."
    }

    foreach ($path in $expectedFiles.Keys) {
        if (-not $entries.ContainsKey($path)) { throw "Update package is missing staged file: $path" }
        $entryStream = $entries[$path].Open()
        try {
            $sha = [System.Security.Cryptography.SHA256]::Create()
            try { $entryHash = [BitConverter]::ToString($sha.ComputeHash($entryStream)).Replace('-', '') }
            finally { $sha.Dispose() }
        } finally { $entryStream.Dispose() }
        $stageHash = (Get-FileHash -LiteralPath $expectedFiles[$path] -Algorithm SHA256).Hash
        if ($entryHash -ne $stageHash) { throw "Update package content does not match stage: $path" }
    }

    foreach ($path in $entries.Keys) {
        if (-not $expectedFiles.ContainsKey($path)) { throw "Unexpected file in update package: $path" }
    }
} finally {
    $archive.Dispose()
}

Write-Host "Update package integrity passed: $($entries.Count) files match stage; required runtime files are present." -ForegroundColor Green
