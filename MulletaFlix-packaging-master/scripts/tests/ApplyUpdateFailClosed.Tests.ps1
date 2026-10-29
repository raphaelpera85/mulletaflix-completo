$ErrorActionPreference = 'Stop'

$updateScript = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\MulletaFlix-ux-custom\nsis\apply-update.ps1'))

function Invoke-UpdaterScenario {
    param([Parameter(Mandatory = $true)][ValidateSet('old-process-running', 'service-not-ready', 'service-https-not-ready', 'service-listener-conflict', 'service-ready', 'service-stop-timeout', 'process-not-ready', 'process-listener-conflict', 'process-ready', 'process-stop-timeout')][string] $Scenario)

    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "mflx-update-fail-closed-$([guid]::NewGuid().ToString('N'))"
    $installDirectory = Join-Path $tempRoot 'install'
    $sourceDirectory = Join-Path $tempRoot 'extracted'
    $dataDirectory = Join-Path $tempRoot 'data'
    $configDirectory = Join-Path $dataDirectory 'config'
    $updatesDirectory = Join-Path $dataDirectory 'updates'
    $harnessPath = Join-Path $tempRoot 'run-update.ps1'
    $robocopyMarker = Join-Path $tempRoot 'robocopy-called.txt'
    $readinessMarker = Join-Path $tempRoot 'readiness-uri.txt'
    $stopProcessMarker = Join-Path $tempRoot 'process-stop-called.txt'
    $certificatePath = Join-Path $tempRoot 'readiness-test.pfx'
    $certificatePassword = 'local-readiness-fixture-only'
    $testCertificate = $null

    try {
        foreach ($directory in @($installDirectory, $sourceDirectory, $configDirectory, $updatesDirectory)) {
            [void][System.IO.Directory]::CreateDirectory($directory)
        }
        [System.IO.File]::WriteAllText((Join-Path $installDirectory 'MulletaFlix.exe'), 'fixture')
        [System.IO.File]::WriteAllText((Join-Path $installDirectory 'MulletaFlix.dll'), 'old server')
        [System.IO.File]::WriteAllText((Join-Path $sourceDirectory 'MulletaFlix.dll'), 'new server')
        [System.IO.File]::SetLastWriteTimeUtc((Join-Path $sourceDirectory 'MulletaFlix.dll'), [DateTime]::UtcNow.AddMinutes(1))
        [System.IO.File]::WriteAllText((Join-Path $sourceDirectory 'new-component.dll'), 'new component')
        [System.IO.File]::WriteAllText((Join-Path $updatesDirectory 'package.zip'), 'package')
        [System.IO.File]::WriteAllText((Join-Path $updatesDirectory 'update_state.json'), '{}')
        if ($Scenario -eq 'service-not-ready') {
            [System.IO.File]::WriteAllText((Join-Path $configDirectory 'network.xml'), '<NetworkConfiguration><InternalHttpPort>8123</InternalHttpPort><BaseUrl>media</BaseUrl></NetworkConfiguration>')
        }
        if ($Scenario -eq 'service-https-not-ready') {
            $testCertificate = New-SelfSignedCertificate -DnsName 'server-test.invalid' -CertStoreLocation 'Cert:\CurrentUser\My'
            $securePassword = ConvertTo-SecureString -String $certificatePassword -Force -AsPlainText
            Export-PfxCertificate -Cert $testCertificate -FilePath $certificatePath -Password $securePassword | Out-Null
            $xmlCertificatePath = [System.Security.SecurityElement]::Escape($certificatePath)
            [System.IO.File]::WriteAllText((Join-Path $configDirectory 'network.xml'), "<NetworkConfiguration><InternalHttpsPort>8443</InternalHttpsPort><BaseUrl>media</BaseUrl><EnableHttps>true</EnableHttps><RequireHttps>true</RequireHttps><CertificatePath>$xmlCertificatePath</CertificatePath><CertificatePassword>$certificatePassword</CertificatePassword></NetworkConfiguration>")
        }

        $quoted = @{}
        foreach ($entry in @{
            Scenario = $Scenario
            TempRoot = $tempRoot
            InstallDirectory = $installDirectory
            SourceDirectory = $sourceDirectory
            DataDirectory = $dataDirectory
            RobocopyMarker = $robocopyMarker
            ReadinessMarker = $readinessMarker
            StopProcessMarker = $stopProcessMarker
            CertificatePath = $certificatePath
            CertificatePassword = $certificatePassword
            UpdateScript = $updateScript
        }.GetEnumerator()) {
            $quoted[$entry.Key] = $entry.Value.Replace("'", "''")
        }

        $harness = @"
`$ErrorActionPreference = 'Stop'
`$env:TEMP = '$($quoted.TempRoot)'
`$script:serviceStatus = [System.ServiceProcess.ServiceControllerStatus]::Stopped
function Get-Process {
    param([int]`$Id, [string]`$Name, [string]`$ErrorAction)
    if ('$($quoted.Scenario)' -eq 'old-process-running' -and `$Id -eq 4242) { return [pscustomobject]@{ Id = 4242 } }
    if ('$($quoted.Scenario)' -like 'process-*' -and `$Id -eq 8888 -and ('$($quoted.Scenario)' -eq 'process-stop-timeout' -or -not (Test-Path -LiteralPath '$($quoted.StopProcessMarker)'))) { return [pscustomobject]@{ Id = 8888 } }
    return `$null
}
function Stop-Process {
    param([int]`$Id, [switch]`$Force, [string]`$ErrorAction)
    if ('$($quoted.Scenario)' -ne 'process-stop-timeout') { [System.IO.File]::WriteAllText('$($quoted.StopProcessMarker)', "`$Id") }
}
function Stop-Service {
    if ('$($quoted.Scenario)' -ne 'service-stop-timeout') { `$script:serviceStatus = [System.ServiceProcess.ServiceControllerStatus]::Stopped }
}
function Start-Sleep {}
function Get-Service {
    if ('$($quoted.Scenario)' -notlike 'service-*') { return `$null }
    `$service = [pscustomobject]@{ Status = `$script:serviceStatus }
    `$service | Add-Member -MemberType ScriptMethod -Name WaitForStatus -Value {
        param(`$status, `$timeout)
        if ('$($quoted.Scenario)' -eq 'service-stop-timeout' -and `$status -eq [System.ServiceProcess.ServiceControllerStatus]::Stopped) { throw 'simulated service stop timeout' }
        `$this.Status = `$status
        `$script:serviceStatus = `$status
    }
    return `$service
}
function Start-Service { `$script:serviceStatus = [System.ServiceProcess.ServiceControllerStatus]::Running }
function Get-CimInstance {
    param([string]`$ClassName, [string]`$Filter, [string]`$ErrorAction)
    if (`$ClassName -eq 'Win32_Service') { return [pscustomobject]@{ ProcessId = 700 } }
    if (`$ClassName -eq 'Win32_Process') { return @([pscustomobject]@{ ProcessId = 701; ParentProcessId = 700 }) }
}
function Get-NetTCPConnection {
    param([string]`$State, [int]`$LocalPort, [string]`$ErrorAction)
    `$owner = if ('$($quoted.Scenario)' -like '*listener-conflict') { 999 } elseif ('$($quoted.Scenario)' -like 'service-*') { 701 } else { 8888 }
    return [pscustomobject]@{ OwningProcess = `$owner; LocalPort = `$LocalPort }
}
function Invoke-WebRequest {
    param([string]`$Uri, [int]`$TimeoutSec, [switch]`$UseBasicParsing, [string]`$ErrorAction)
    [System.IO.File]::WriteAllText('$($quoted.ReadinessMarker)', `$Uri)
    if (`$Uri.StartsWith('https://')) {
        `$callback = [System.Net.ServicePointManager]::ServerCertificateValidationCallback
        if (`$null -eq `$callback) { throw 'Loopback HTTPS did not install its configured-certificate pin.' }
        `$remoteCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new('$($quoted.CertificatePath)', '$($quoted.CertificatePassword)')
        try {
            `$sender = [pscustomobject]@{ RequestUri = [uri]`$Uri }
            `$accepted = `$callback.Invoke(`$sender, `$remoteCertificate, `$null, [System.Net.Security.SslPolicyErrors]::RemoteCertificateNameMismatch)
            if (-not `$accepted) { throw 'Configured certificate pin did not accept the loopback certificate.' }
        } finally { `$remoteCertificate.Dispose() }
    }
    `$statusCode = if ('$($quoted.Scenario)' -like '*listener-conflict' -or '$($quoted.Scenario)' -in @('service-ready', 'process-ready')) { 200 } else { 503 }
    return [pscustomobject]@{ StatusCode = `$statusCode }
}
function Start-Process {
    param([string]`$FilePath, [string]`$ArgumentList, [switch]`$Wait, [switch]`$PassThru, [switch]`$NoNewWindow, [string]`$WorkingDirectory, [string]`$WindowStyle)
    if (`$FilePath -eq 'robocopy.exe') {
        [System.IO.File]::WriteAllText('$($quoted.RobocopyMarker)', 'called')
        [System.IO.File]::WriteAllText((Join-Path '$($quoted.InstallDirectory)' 'MulletaFlix.dll'), 'new server')
        [System.IO.File]::WriteAllText((Join-Path '$($quoted.InstallDirectory)' 'new-component.dll'), 'new component')
        return [pscustomobject]@{ ExitCode = 0 }
    }
    if ('$($quoted.Scenario)' -like 'process-*') { return [pscustomobject]@{ Id = 8888 } }
    throw "Unexpected process launch: `$FilePath"
}
& '$($quoted.UpdateScript)' -InstallDirectory '$($quoted.InstallDirectory)' -UpdateSourceDirectory '$($quoted.SourceDirectory)' -ProcessId 4242 -DataDirectory '$($quoted.DataDirectory)' -ReadinessTimeoutSeconds 0 -ShutdownTimeoutSeconds 0
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

        if ($Scenario -in @('service-ready', 'process-ready')) {
            if ($exitCode -ne 0) { throw "Expected updater scenario '$Scenario' to succeed. Output: $($output -join [Environment]::NewLine)" }
            if (-not (Test-Path -LiteralPath $readinessMarker) -or (Test-Path -LiteralPath $sourceDirectory)) {
                throw 'Updater did not commit and clean up after the owned listener passed readiness.'
            }
            if ([System.IO.File]::ReadAllText((Join-Path $installDirectory 'MulletaFlix.dll')) -ne 'new server') {
                throw 'Updater did not keep the new files after readiness and listener ownership were confirmed.'
            }
            return
        }
        if ($exitCode -eq 0) {
            throw "Expected updater scenario '$Scenario' to fail closed. Output: $($output -join [Environment]::NewLine)"
        }

        if ($Scenario -eq 'old-process-running') {
            if (Test-Path -LiteralPath $robocopyMarker) { throw 'Updater copied files while the previous server process was still running.' }
            if ([System.IO.File]::ReadAllText((Join-Path $installDirectory 'MulletaFlix.dll')) -ne 'old server') { throw 'Installation changed although the old server process was still running.' }
        } elseif ($Scenario -in @('service-stop-timeout', 'process-stop-timeout')) {
            if ([System.IO.File]::ReadAllText((Join-Path $installDirectory 'MulletaFlix.dll')) -ne 'new server') { throw 'Updater replaced files while the failed new server was still running.' }
            if (-not (Get-ChildItem -LiteralPath $tempRoot -Directory -Filter 'mulletaflix-update-rollback-*')) { throw 'Recovery snapshot was not retained when the new server could not be stopped.' }
        } else {
            if (-not (Test-Path -LiteralPath $readinessMarker)) { throw "Updater did not reach the readiness probe. Child output: $($output -join [Environment]::NewLine)" }
            $expectedReadinessUri = switch ($Scenario) {
                'service-not-ready' { 'http://127.0.0.1:8123/media/ready' }
                { $_ -in @('service-listener-conflict', 'service-ready') } { 'http://127.0.0.1:8096/ready' }
                default { 'http://127.0.0.1:8096/ready' }
            }
            if ($Scenario -eq 'service-https-not-ready') { $expectedReadinessUri = 'https://127.0.0.1:8443/media/ready' }
            if ([System.IO.File]::ReadAllText($readinessMarker) -ne $expectedReadinessUri) { throw "Updater probed an unexpected readiness URL; expected $expectedReadinessUri." }
            if ([System.IO.File]::ReadAllText((Join-Path $installDirectory 'MulletaFlix.dll')) -ne 'old server') { throw 'Updater did not restore the installation after readiness failure.' }
            if (Test-Path -LiteralPath (Join-Path $installDirectory 'new-component.dll')) { throw 'Updater left a newly installed file after readiness failure.' }
        }

        if (-not (Test-Path -LiteralPath $sourceDirectory)) { throw 'Update source was removed after an aborted update.' }
        foreach ($file in @((Join-Path $updatesDirectory 'package.zip'), (Join-Path $updatesDirectory 'update_state.json'))) {
            if (-not (Test-Path -LiteralPath $file)) { throw "Update state was removed after an aborted update: $file" }
        }
    } finally {
        if ($null -ne $testCertificate) {
            Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($testCertificate.Thumbprint)" -Force -ErrorAction SilentlyContinue
        }
        if (Test-Path -LiteralPath $tempRoot) {
            $resolvedTempRoot = [System.IO.Path]::GetFullPath($tempRoot)
            $tempPrefix = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
            if (-not $resolvedTempRoot.StartsWith($tempPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Refusing to clean outside the temporary directory: $resolvedTempRoot"
            }
            Remove-Item -LiteralPath $resolvedTempRoot -Recurse -Force
        }
    }
}

Invoke-UpdaterScenario 'old-process-running'
Invoke-UpdaterScenario 'service-not-ready'
Invoke-UpdaterScenario 'service-https-not-ready'
Invoke-UpdaterScenario 'service-listener-conflict'
Invoke-UpdaterScenario 'service-ready'
Invoke-UpdaterScenario 'service-stop-timeout'
Invoke-UpdaterScenario 'process-not-ready'
Invoke-UpdaterScenario 'process-listener-conflict'
Invoke-UpdaterScenario 'process-ready'
Invoke-UpdaterScenario 'process-stop-timeout'
Write-Host 'PASS: updater verifies process identity/readiness, rolls back stopped failures, and retains recovery data if the failed server cannot stop.' -ForegroundColor Green
exit 0
