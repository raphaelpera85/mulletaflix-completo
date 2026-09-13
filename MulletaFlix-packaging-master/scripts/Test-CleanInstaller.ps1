[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $InstallerPath,

    [string] $InstallDirectory = "$env:ProgramFiles\MulletaFlix\Server",
    [string] $DataDirectory = "$env:ProgramData\MulletaFlix\Server",
    [int] $HttpPort = 8096,
    [string] $ExpectedSha256,
    [switch] $AutoElevate,
    [switch] $AllowExistingPaths
)

$ErrorActionPreference = 'Stop'
$serviceName = 'MulletaFlixServer'
$installer = (Resolve-Path -LiteralPath $InstallerPath).Path
$installDirectory = [System.IO.Path]::GetFullPath($InstallDirectory)
$dataDirectory = [System.IO.Path]::GetFullPath($DataDirectory)

function Assert-Condition {
    param([bool] $Condition, [string] $Message)
    if (-not $Condition) {
        throw "Clean installer smoke test failed: $Message"
    }
}

function Test-ModifyAccess {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $ExpectedSid
    )

    $acl = Get-Acl -LiteralPath $Path
    foreach ($rule in $acl.Access) {
        $identitySid = $null
        try {
            $identitySid = $rule.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value
        } catch {
            $identitySid = $rule.IdentityReference.Value
        }

        $rights = [System.Security.AccessControl.FileSystemRights]$rule.FileSystemRights
        $hasModify = ($rights -band [System.Security.AccessControl.FileSystemRights]::Modify) -eq [System.Security.AccessControl.FileSystemRights]::Modify
        if ($identitySid -eq $ExpectedSid -and $rule.AccessControlType -eq 'Allow' -and $hasModify) {
            return $true
        }
    }

    return $false
}

function Test-NebulaUrlAcl {
    $urlAcl = & netsh.exe http show urlacl url=http://+:2123/ 2>&1
    if ($LASTEXITCODE -ne 0) {
        return $false
    }

    $aclText = $urlAcl -join "`n"
    return $aclText -match 'S-1-5-20' -and
        $aclText -match 'S-1-5-18' -and
        $aclText -match 'S-1-5-19' -and
        $aclText -notmatch ';;;WD'
}

function Write-FailureDiagnostics {
    param([string] $Reason)

    Write-Host "--- Clean installer diagnostics: $Reason ---" -ForegroundColor Yellow
    try {
        $serviceSnapshot = Get-CimInstance -ClassName Win32_Service -Filter "Name='$serviceName'" -ErrorAction SilentlyContinue
        if ($serviceSnapshot) {
            Write-Host "Service: state=$($serviceSnapshot.State); start=$($serviceSnapshot.StartName); exit=$($serviceSnapshot.ExitCode); path=$($serviceSnapshot.PathName)"
        } else {
            Write-Host 'Service: not registered'
        }
    } catch {
        Write-Host "Service diagnostics failed: $($_.Exception.Message)"
    }

    $logDirectory = Join-Path $dataDirectory 'log'
    if (Test-Path -LiteralPath $logDirectory) {
        Get-ChildItem -LiteralPath $logDirectory -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 2 |
            ForEach-Object {
                Write-Host "Log: $($_.FullName)"
                Get-Content -LiteralPath $_.FullName -Tail 25 -ErrorAction SilentlyContinue |
                    ForEach-Object { Write-Host "  $_" }
            }
    } else {
        Write-Host "Log directory not found: $logDirectory"
    }
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    if ($AutoElevate) {
        $currentPowerShell = (Get-Process -Id $PID).Path
        $forwardedArguments = @(
            '-NoProfile',
            '-ExecutionPolicy', 'Bypass',
            '-File', $PSCommandPath,
            '-InstallerPath', $installer,
            '-InstallDirectory', $installDirectory,
            '-DataDirectory', $dataDirectory,
            '-HttpPort', $HttpPort.ToString()
        )
        if ($ExpectedSha256) {
            $forwardedArguments += @('-ExpectedSha256', $ExpectedSha256)
        }
        if ($AllowExistingPaths) {
            $forwardedArguments += '-AllowExistingPaths'
        }

        Write-Host 'Relançando o smoke test com privilégios administrativos (UAC)...'
        $elevated = Start-Process -FilePath $currentPowerShell -Verb RunAs -ArgumentList $forwardedArguments -Wait -PassThru
        exit $elevated.ExitCode
    }

    Assert-Condition $false 'run this script from an elevated PowerShell session, or pass -AutoElevate.'
}

if (-not $AllowExistingPaths) {
    $existingRegistryKeys = @(
        'HKLM:\Software\MulletaFlix\Server',
        'HKLM:\Software\WOW6432Node\MulletaFlix\Server'
    )
    foreach ($registryKey in $existingRegistryKeys) {
        Assert-Condition (-not (Test-Path -LiteralPath $registryKey)) "existing installation registry key detected: $registryKey. Use an isolated Windows VM or pass -AllowExistingPaths explicitly."
    }

    foreach ($port in @(8096, 3306)) {
        $listener = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
        Assert-Condition ($null -eq $listener) "port $port is already in use. Stop the owning application or use an isolated Windows VM."
    }

    Assert-Condition (-not (Test-Path -LiteralPath $installDirectory)) "install directory already exists: $installDirectory. Use an isolated path or pass -AllowExistingPaths explicitly."
    Assert-Condition (-not (Test-Path -LiteralPath $dataDirectory)) "data directory already exists: $dataDirectory. Use an isolated path or pass -AllowExistingPaths explicitly."
    Assert-Condition (-not (Get-Service -Name $serviceName -ErrorAction SilentlyContinue)) "service $serviceName is already registered. Remove it or pass -AllowExistingPaths explicitly."
}

$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
if ($ExpectedSha256) {
    Assert-Condition ($hash -eq $ExpectedSha256.ToUpperInvariant()) "installer SHA-256 mismatch. Expected $ExpectedSha256, got $hash."
}

Write-Host "Installing $installer"
$arguments = "/S /TESTMODE /D=`"$installDirectory`" /DATA=`"$dataDirectory`""

try {
    $process = Start-Process -FilePath $installer -ArgumentList $arguments -Wait -PassThru
    Assert-Condition ($process.ExitCode -eq 0) "installer exited with code $($process.ExitCode)."

    Assert-Condition (Test-Path -LiteralPath (Join-Path $installDirectory 'MulletaFlix.exe')) 'server executable was not installed.'
    Assert-Condition (Test-Path -LiteralPath (Join-Path $installDirectory 'nssm.exe')) 'NSSM was not installed.'
    Assert-Condition (Test-Path -LiteralPath (Join-Path $installDirectory 'mulletaflix-windows-tray\MulletaFlix.Windows.Tray.exe')) 'tray executable was not installed.'

    $service = Get-Service -Name $serviceName -ErrorAction Stop
    Assert-Condition ($service.Status -eq 'Running') "service status is $($service.Status), not Running."

    $serviceDetails = Get-CimInstance -ClassName Win32_Service -Filter "Name='$serviceName'"
    Assert-Condition ($null -ne $serviceDetails) 'service details could not be queried.'
    $serviceSid = switch -Regex ($serviceDetails.StartName) {
        'NetworkService$' { 'S-1-5-20'; break }
        'LocalService$' { 'S-1-5-19'; break }
        'LocalSystem$' { 'S-1-5-18'; break }
        default { throw "Clean installer smoke test failed: unsupported service account '$($serviceDetails.StartName)'." }
    }
    Write-Host "Service account: $($serviceDetails.StartName) [$serviceSid]"

    Assert-Condition (Test-ModifyAccess -Path $dataDirectory -ExpectedSid $serviceSid) "service account $($serviceDetails.StartName) does not have Modify access to the data directory."
    $configFile = Join-Path $dataDirectory 'config\system.xml'
    Assert-Condition (Test-Path -LiteralPath $configFile) 'config/system.xml was not created during first startup.'
    Assert-Condition (Test-ModifyAccess -Path $configFile -ExpectedSid $serviceSid) "service account $($serviceDetails.StartName) does not have Modify access to config/system.xml."
    Assert-Condition (Test-NebulaUrlAcl) 'Nebula HTTP URL ACL is missing, too broad, or does not grant all supported service identities.'
    Assert-Condition (-not (Get-NetFirewallRule -DisplayName 'MulletaFlix MariaDB' -ErrorAction SilentlyContinue)) 'embedded MariaDB must not have an inbound firewall rule.'
    Assert-Condition (-not (Get-NetFirewallRule -DisplayName 'MulletaFlix MongoDB' -ErrorAction SilentlyContinue)) 'local MongoDB must not have an inbound firewall rule.'

    # /ready represents the core server readiness. Nebula is an optional
    # integration and must not make a healthy server fail this installer gate.
    $healthUri = "http://127.0.0.1:$HttpPort/ready"
    $health = $null
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        try {
            $health = Invoke-RestMethod -Uri $healthUri -TimeoutSec 2
            break
        } catch {
            Start-Sleep -Seconds 2
        }
    }
    Assert-Condition ($null -ne $health) "health endpoint $healthUri did not respond."
    Write-Host "Health endpoint responded: $healthUri"
} catch {
    Write-FailureDiagnostics $_.Exception.Message
    throw
} finally {
    $uninstaller = Join-Path $installDirectory 'Uninstall.exe'
    if (Test-Path -LiteralPath $uninstaller) {
        Write-Host 'Uninstalling clean smoke-test installation'
        $uninstall = Start-Process -FilePath $uninstaller -ArgumentList '/S' -Wait -PassThru
        Assert-Condition ($uninstall.ExitCode -eq 0) "uninstaller exited with code $($uninstall.ExitCode)."
    }
}

Assert-Condition (-not (Get-Service -Name $serviceName -ErrorAction SilentlyContinue)) 'service still exists after uninstall.'
Assert-Condition (-not (Test-Path -LiteralPath $installDirectory)) 'install directory still exists after uninstall.'
Write-Host "Clean installer smoke test passed. SHA-256: $hash"
