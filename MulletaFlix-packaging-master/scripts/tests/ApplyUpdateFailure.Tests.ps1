$ErrorActionPreference = 'Stop'

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "mflx-update-failure-$([guid]::NewGuid().ToString('N'))"
$installDirectory = Join-Path $tempRoot 'install'
$sourceDirectory = Join-Path $tempRoot 'extracted'
$dataDirectory = Join-Path $tempRoot 'data'
$harnessPath = Join-Path $tempRoot 'run-update.ps1'
$restartMarker = Join-Path $tempRoot 'server-restarted.txt'
$updateScript = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\MulletaFlix-ux-custom\nsis\apply-update.ps1'))

try {
    foreach ($directory in @($installDirectory, $sourceDirectory, $dataDirectory, (Join-Path $dataDirectory 'updates'))) {
        [void][System.IO.Directory]::CreateDirectory($directory)
    }

    foreach ($file in @((Join-Path $installDirectory 'MulletaFlix.exe'), (Join-Path $sourceDirectory 'MulletaFlix.dll'))) {
        [System.IO.File]::WriteAllText($file, 'fixture')
    }

    $quotedUpdateScript = $updateScript.Replace("'", "''")
    $quotedInstallDirectory = $installDirectory.Replace("'", "''")
    $quotedSourceDirectory = $sourceDirectory.Replace("'", "''")
    $quotedDataDirectory = $dataDirectory.Replace("'", "''")
    $quotedRestartMarker = $restartMarker.Replace("'", "''")
    $harness = @"
`$ErrorActionPreference = 'Stop'
`$env:TEMP = '$tempRoot'
function Get-Process { return `$null }
function Stop-Process {}
function Start-Sleep {}
function Get-Service { return `$null }
function Start-Process {
    param([string]`$FilePath, [string]`$ArgumentList, [switch]`$Wait, [switch]`$PassThru, [switch]`$NoNewWindow, [string]`$WorkingDirectory, [string]`$WindowStyle)
    if (`$FilePath -eq 'robocopy.exe') { return [pscustomobject]@{ ExitCode = 11 } }
    [System.IO.File]::WriteAllText('$quotedRestartMarker', `$FilePath)
    return [pscustomobject]@{ ExitCode = 0 }
}
& '$quotedUpdateScript' -InstallDirectory '$quotedInstallDirectory' -UpdateSourceDirectory '$quotedSourceDirectory' -ProcessId 4242 -DataDirectory '$quotedDataDirectory'
"@
    [System.IO.File]::WriteAllText($harnessPath, $harness)

    $childPowerShell = Join-Path $PSHOME 'pwsh.exe'
    $output = & $childPowerShell -NoProfile -File $harnessPath 2>&1
    $exitCode = $LASTEXITCODE

    if ($exitCode -eq 0) {
        throw "Expected the update helper to fail after robocopy error 11. Output: $($output -join [Environment]::NewLine)"
    }
    if (Test-Path -LiteralPath $restartMarker) {
        throw 'Server or tray restart was attempted after robocopy failed.'
    }
    if (-not (Test-Path -LiteralPath $sourceDirectory)) {
        throw 'Update source was removed after robocopy failed.'
    }

    Write-Host 'PASS: failed copy preserves the package, returns its error code, and does not restart server/tray.' -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        $resolvedTempRoot = [System.IO.Path]::GetFullPath($tempRoot)
        $tempPrefix = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
        if (-not $resolvedTempRoot.StartsWith($tempPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean outside the temporary directory: $resolvedTempRoot"
        }

        Remove-Item -LiteralPath $resolvedTempRoot -Recurse -Force
    }
}
