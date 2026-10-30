$ErrorActionPreference = 'Stop'

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "mflx-update-failure-$([guid]::NewGuid().ToString('N'))"
$installDirectory = Join-Path $tempRoot 'install'
$sourceDirectory = Join-Path $tempRoot 'extracted'
$dataDirectory = Join-Path $tempRoot 'data'
$updatesDirectory = Join-Path $dataDirectory 'updates'
$harnessPath = Join-Path $tempRoot 'run-update.ps1'
$restartMarker = Join-Path $tempRoot 'server-restarted.txt'
$updateScript = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\MulletaFlix-ux-custom\nsis\apply-update.ps1'))

try {
    foreach ($directory in @($installDirectory, $sourceDirectory, $dataDirectory, $updatesDirectory)) {
        [void][System.IO.Directory]::CreateDirectory($directory)
    }

    [System.IO.File]::WriteAllText((Join-Path $installDirectory 'MulletaFlix.exe'), 'fixture')
    [System.IO.File]::WriteAllText((Join-Path $installDirectory 'MulletaFlix.dll'), 'old server')
    [System.IO.File]::WriteAllText((Join-Path $sourceDirectory 'MulletaFlix.dll'), 'new server')
    [System.IO.File]::SetLastWriteTimeUtc((Join-Path $sourceDirectory 'MulletaFlix.dll'), [DateTime]::UtcNow.AddMinutes(1))
    [System.IO.File]::WriteAllText((Join-Path $sourceDirectory 'new-component.dll'), 'new component')
    [System.IO.File]::WriteAllText((Join-Path $updatesDirectory 'package.zip'), 'package')
    [System.IO.File]::WriteAllText((Join-Path $updatesDirectory 'update_state.json'), '{}')

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
    if (`$FilePath -eq 'robocopy.exe') {
        [System.IO.File]::WriteAllText((Join-Path '$quotedInstallDirectory' 'MulletaFlix.dll'), 'partially copied server')
        [System.IO.File]::WriteAllText((Join-Path '$quotedInstallDirectory' 'new-component.dll'), 'partially copied component')
        return [pscustomobject]@{ ExitCode = 11 }
    }
    [System.IO.File]::WriteAllText('$quotedRestartMarker', `$FilePath)
    return [pscustomobject]@{ ExitCode = 0 }
}
& '$quotedUpdateScript' -InstallDirectory '$quotedInstallDirectory' -UpdateSourceDirectory '$quotedSourceDirectory' -ProcessId 4242 -DataDirectory '$quotedDataDirectory'
"@
    [System.IO.File]::WriteAllText($harnessPath, $harness)

    $childPowerShell = Join-Path $PSHOME $(if ($PSVersionTable.PSEdition -eq 'Desktop') { 'powershell.exe' } else { 'pwsh.exe' })
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = & $childPowerShell -NoProfile -File $harnessPath 2>&1
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -eq 0) {
        throw "Expected the update helper to fail after robocopy error 11. Output: $($output -join [Environment]::NewLine)"
    }
    if (Test-Path -LiteralPath $restartMarker) {
        throw 'Server or tray restart was attempted after robocopy failed.'
    }
    if (-not (Test-Path -LiteralPath $sourceDirectory)) {
        throw 'Update source was removed after robocopy failed.'
    }
    if ([System.IO.File]::ReadAllText((Join-Path $installDirectory 'MulletaFlix.dll')) -ne 'old server') {
        throw 'An existing installation file was not restored after a partial copy.'
    }
    if (Test-Path -LiteralPath (Join-Path $installDirectory 'new-component.dll')) {
        throw 'A new installation file was not removed after a partial copy.'
    }
    foreach ($file in @((Join-Path $updatesDirectory 'package.zip'), (Join-Path $updatesDirectory 'update_state.json'))) {
        if (-not (Test-Path -LiteralPath $file)) { throw "Update state was removed after copy failure: $file" }
    }

    Write-Host 'PASS: partial copy failure restores replaced files, removes new files, preserves update data, and does not restart server/tray.' -ForegroundColor Green
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

exit 0
