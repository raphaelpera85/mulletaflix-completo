$ErrorActionPreference = 'Stop'

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "mflx-update-startup-failure-$([guid]::NewGuid().ToString('N'))"
$installDirectory = Join-Path $tempRoot 'install'
$sourceDirectory = Join-Path $tempRoot 'extracted'
$dataDirectory = Join-Path $tempRoot 'data'
$updatesDirectory = Join-Path $dataDirectory 'updates'
$harnessPath = Join-Path $tempRoot 'run-update.ps1'
$stopProcessCallMarker = Join-Path $tempRoot 'stop-process-called.txt'
$updateScript = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\MulletaFlix-ux-custom\nsis\apply-update.ps1'))

try {
    foreach ($directory in @($installDirectory, $sourceDirectory, $updatesDirectory)) {
        [void][System.IO.Directory]::CreateDirectory($directory)
    }

    [System.IO.File]::WriteAllText((Join-Path $installDirectory 'MulletaFlix.exe'), 'fixture')
    [System.IO.File]::WriteAllText((Join-Path $installDirectory 'MulletaFlix.dll'), 'old server')
    [System.IO.File]::WriteAllText((Join-Path $sourceDirectory 'MulletaFlix.dll'), 'new server')
    [System.IO.File]::SetLastWriteTimeUtc((Join-Path $sourceDirectory 'MulletaFlix.dll'), [DateTime]::UtcNow.AddMinutes(1))
    [System.IO.File]::WriteAllText((Join-Path $sourceDirectory 'new-component.dll'), 'new component')
    [System.IO.File]::WriteAllText((Join-Path $updatesDirectory 'package.zip'), 'package')
    [System.IO.File]::WriteAllText((Join-Path $updatesDirectory 'update_state.json'), '{}')

    $quotedInstallDirectory = $installDirectory.Replace("'", "''")
    $quotedSourceDirectory = $sourceDirectory.Replace("'", "''")
    $quotedDataDirectory = $dataDirectory.Replace("'", "''")
$quotedStopProcessCallMarker = $stopProcessCallMarker.Replace("'", "''")
    $quotedUpdateScript = $updateScript.Replace("'", "''")
    $harness = @"
`$ErrorActionPreference = 'Stop'
`$env:TEMP = '$tempRoot'
`$script:serviceStatus = [System.ServiceProcess.ServiceControllerStatus]::Stopped
function Get-Process {
    param([string]`$Name, [int]`$Id, [string]`$ErrorAction)
    if (`$Name) { return [pscustomobject]@{ Id = 999; ProcessName = `$Name } }
    return `$null
}
function Stop-Process {
    param([int]`$Id, [object]`$InputObject, [switch]`$Force, [string]`$ErrorAction)
    [System.IO.File]::WriteAllText('$quotedStopProcessCallMarker', "Id=`$Id")
}
function Stop-Service { `$script:serviceStatus = [System.ServiceProcess.ServiceControllerStatus]::Stopped }
function Start-Sleep {}
function Get-Service {
    `$service = [pscustomobject]@{ Status = `$script:serviceStatus }
    `$service | Add-Member -MemberType ScriptMethod -Name WaitForStatus -Value { param(`$status, `$timeout) `$this.Status = `$status; `$script:serviceStatus = `$status }
    return `$service
}
function Start-Service { throw 'simulated service startup failure' }
function Start-Process {
    param([string]`$FilePath, [string]`$ArgumentList, [switch]`$Wait, [switch]`$PassThru, [switch]`$NoNewWindow, [string]`$WorkingDirectory, [string]`$WindowStyle)
    if (`$FilePath -eq 'robocopy.exe') {
        [System.IO.File]::WriteAllText((Join-Path '$quotedInstallDirectory' 'MulletaFlix.dll'), 'new server')
        [System.IO.File]::WriteAllText((Join-Path '$quotedInstallDirectory' 'new-component.dll'), 'new component')
        return [pscustomobject]@{ ExitCode = 0 }
    }
    throw "Unexpected process launch during startup-failure test: `$FilePath"
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
        throw "Expected the update helper to fail after service startup failure. Output: $($output -join [Environment]::NewLine)"
    }
    if ([System.IO.File]::ReadAllText((Join-Path $installDirectory 'MulletaFlix.dll')) -ne 'old server') {
        throw 'The pre-update server binary was not restored after service startup failure.'
    }
    if (Test-Path -LiteralPath (Join-Path $installDirectory 'new-component.dll')) {
        throw 'A newly installed file was not removed after service startup failure.'
    }
    if (-not (Test-Path -LiteralPath $sourceDirectory)) {
        throw 'The extracted update source was removed before startup was confirmed.'
    }
    foreach ($file in @((Join-Path $updatesDirectory 'package.zip'), (Join-Path $updatesDirectory 'update_state.json'))) {
        if (-not (Test-Path -LiteralPath $file)) { throw "Update state was removed after startup failure: $file" }
    }
    if (Test-Path -LiteralPath $stopProcessCallMarker) {
        throw 'The updater called Stop-Process during a service startup failure.'
    }

    Write-Host 'PASS: service startup failure restores the previous server, retains update files, and does not call Stop-Process.' -ForegroundColor Green
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
