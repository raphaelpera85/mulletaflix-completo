$ErrorActionPreference = 'Stop'
$configPath = Join-Path $PSScriptRoot '..\configure-https.ps1'
$tokens = $null
$parseErrors = $null
$configAst = [System.Management.Automation.Language.Parser]::ParseFile($configPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw "HTTPS configurator has PowerShell parse errors: $($parseErrors[0].Message)" }
$definitions = $configAst.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true)
$required = @('Assert-HttpsPrerequisites', 'Invoke-HttpsSetup', 'Get-DuckDnsTaskSnapshot', 'Restore-DuckDnsTaskSnapshot', 'Assert-DuckDnsScheduledTask')
foreach ($name in $required) {
    if (-not ($definitions | Where-Object Name -eq $name)) { throw "TDD RED: required transaction function is missing: $name" }
}

# Load just the functions under test; all host and provider boundaries are mocked below.
foreach ($name in $required + @('Configure-DuckDns')) {
    $definition = $definitions | Where-Object Name -eq $name | Select-Object -First 1
    if ($definition) { Invoke-Expression $definition.Extent.Text }
}
$script:InstallRoot = 'C:\fake\server'
$script:ServerPort = 8096
$script:nginxExe = 'C:\fake\server\nginx\nginx.exe'
$script:nssmExe = 'C:\fake\server\nssm.exe'
$script:wacsExe = 'C:\fake\server\win-acme\wacs.exe'
$script:duckDnsScript = 'C:\fake\server\duckdns-update.ps1'
$script:duckDnsConfig = Join-Path ([IO.Path]::GetTempPath()) ('mflx-duckdns-transaction-' + [guid]::NewGuid().ToString('N') + '.json')
$script:legacyDuckDnsConfig = "$script:duckDnsConfig.legacy"
$script:duckDnsTask = 'MulletaFlix DuckDNS Update'
$script:duckDnsConfigRoot = Split-Path -Parent $script:duckDnsConfig
$script:DuckDnsSubdomain = 'test-only'
$script:DuckDnsTokenFile = 'C:\fake\token.txt'
$script:DuckDnsToken = 'sentinel-token-do-not-log'
$script:configureEvents = [Collections.Generic.List[string]]::new()
$script:taskXml = '<Task><RegistrationInfo><Description>prior</Description></RegistrationInfo></Task>'
$script:taskExists = $true
$script:failUpdater = $false
$script:failRegister = $false
$script:failVerify = $false

function Assert-File { param([string]$Path,[string]$Description) if ($script:missingPath -eq $Path) { throw "missing $Description" } }
function Restore-PendingNginxConfig { $script:configureEvents.Add('restore-pending') }
function Configure-DuckDns { $script:configureEvents.Add('configure-duckdns') }
function Write-ReloadScript { $script:configureEvents.Add('reload-script') }
function Test-CanPreserveExistingTls { return [bool]$script:hasExistingTls }
function Write-NginxConfig { param([bool]$TlsEnabled,[bool]$AcmeBootstrapOnly) $script:configureEvents.Add('write-nginx'); if($TlsEnabled){$script:activeConfigMode='tls'}elseif($AcmeBootstrapOnly){$script:activeConfigMode='bootstrap-503'}else{$script:activeConfigMode='http'} }
function Install-NginxConfig { $script:configureEvents.Add('install-nginx'); return [pscustomobject]@{HadPreviousConfig=$true} }
function Ensure-NginxService { param($NginxConfigTransaction) $script:configureEvents.Add('ensure-service') }
function Get-ScheduledTask { param([string]$TaskName,[string]$ErrorAction) if ($script:taskExists) { $taskArgs='-NoProfile -ExecutionPolicy Bypass -File "'+$script:duckDnsScript+'" -ConfigPath "'+$script:duckDnsConfig+'"'; if($script:failVerify){$taskArgs='wrong arguments'}; return [pscustomobject]@{TaskName=$TaskName; State='Ready';Settings=[pscustomobject]@{Enabled=$true};Actions=@([pscustomobject]@{Execute='powershell.exe';Arguments=$taskArgs});Principal=[pscustomobject]@{UserId='SYSTEM'}} }; return $null }
function Export-ScheduledTask { param([string]$TaskName,[string]$ErrorAction) return $script:taskXml }
function Register-ScheduledTask { param([string]$TaskName,[string]$Xml,[object]$Action,[object]$Trigger,[object]$Principal,[switch]$Force,[string]$ErrorAction) $script:configureEvents.Add('register-task'); if ($script:failRegister -and $null -eq $Xml) { $script:taskExists=$false; throw 'registration failed' }; $script:taskExists=$true; if ($Xml) { $script:taskXml=$Xml }; return [pscustomobject]@{TaskName=$TaskName} }
function Unregister-ScheduledTask { param([string]$TaskName,[switch]$Confirm,[string]$ErrorAction) $script:configureEvents.Add('unregister-task'); $script:taskExists=$false }
function New-ScheduledTaskAction { param([string]$Execute,[string]$Argument) return [pscustomobject]@{Execute=$Execute;Arguments=$Argument} }
function New-ScheduledTaskTrigger { param([object]$Once,[datetime]$At,[timespan]$RepetitionInterval,[timespan]$RepetitionDuration) return [pscustomobject]@{RepetitionInterval=$RepetitionInterval} }
function New-ScheduledTaskPrincipal { param([string]$UserId,[string]$LogonType,[string]$RunLevel) return [pscustomobject]@{UserId=$UserId} }
function Invoke-DuckDnsUpdater { param([string]$ConfigPath) $script:configureEvents.Add('update-duckdns'); if ($script:failUpdater) { throw 'provider update failed' } }
function Ensure-ProtectedDuckDnsConfigRoot { }
function Read-DuckDnsTokenFile { param([string]$Path) return $script:DuckDnsToken }
function Protect-LegacyDuckDnsConfig { }
function Set-ProtectedDuckDnsAcl { param([string]$Path) }
function Assert-ProtectedDuckDnsAcl { param([string]$Path) }
function Assert-NoDuckDnsReparsePoints { param([string]$Path) }
function Remove-LegacyDuckDnsConfig { $script:configureEvents.Add('remove-legacy') }

# Orchestration: all missing binaries must fail before recovery, credential, or service mutation.
foreach ($missing in @($script:nginxExe, $script:nssmExe, $script:wacsExe)) {
    $script:missingPath = $missing
    $script:configureEvents.Clear()
    $failed = $false
    try { Invoke-HttpsSetup -InstallRoot $script:InstallRoot -HostName 'media.example.test' -EmailAddress 'test@example.test' -DuckDnsSubdomain '' -DuckDnsTokenFile '' -SkipCertificate:$false } catch { $failed = $true }
    if (-not $failed -or $script:configureEvents.Count -ne 0) { throw "Missing prerequisite '$missing' was not rejected before all HTTPS setup side effects." }
}
Write-Output 'Preflight: missing Nginx, NSSM, or WACS is rejected before any setup mutation.'

# SkipCertificate permits HTTP-only setup without requiring WACS; Nginx/NSSM remain mandatory.
$script:missingPath = ''
$script:assertedPaths = [Collections.Generic.List[string]]::new()
function Assert-File { param([string]$Path,[string]$Description) $script:assertedPaths.Add($Path); if ($script:missingPath -eq $Path) { throw "missing $Description" } }
Assert-HttpsPrerequisites -SkipCertificate -DuckDnsSubdomain '' -DuckDnsTokenFile ''
if ($script:assertedPaths -contains $script:wacsExe) { throw 'SkipCertificate incorrectly required the WACS executable.' }
$script:assertedPaths.Clear()
Assert-HttpsPrerequisites -DuckDnsSubdomain 'test-only' -DuckDnsTokenFile 'C:\fake\token.txt'
if ($script:assertedPaths -notcontains $script:wacsExe -or $script:assertedPaths -notcontains $script:duckDnsScript) { throw 'HTTPS prerequisites omitted required WACS or DuckDNS artifacts.' }
$script:assertedPaths.Clear()
$script:missingPath = $script:duckDnsScript
$failed = $false
try { Invoke-HttpsSetup -InstallRoot $script:InstallRoot -HostName 'media.example.test' -EmailAddress 'test@example.test' -DuckDnsSubdomain 'test-only' -DuckDnsTokenFile 'C:\fake\token.txt' } catch { $failed = $true }
if (-not $failed -or $script:configureEvents.Count -ne 0) { throw 'Missing DuckDNS updater was not rejected before setup mutations.' }
Write-Output 'Preflight: SkipCertificate omits WACS; enabled DuckDNS artifacts are preflighted.'

# ACME failures keep the deliberate fail-closed bootstrap config; TLS renewal failures
# keep the previously active TLS listener, and errors never include the DuckDNS token.
function Invoke-WacsCertificateSetup { param([string]$HostName,[string]$EmailAddress) $script:configureEvents.Add('wacs-source'); if($script:failWacsSource){throw 'simulated certificate issue failure'} }
function Invoke-WacsTaskSetup { $script:configureEvents.Add('wacs-task'); if($script:failWacsTask){throw 'simulated renewal task failure'} }
$script:certPrefix = Join-Path $script:InstallRoot 'certs\media.example.test'
$script:acmeRoot = Join-Path $script:InstallRoot 'nginx\acme'
$script:certRoot = Join-Path $script:InstallRoot 'certs'
$script:reloadScript = Join-Path $script:InstallRoot 'nginx\reload-nginx.ps1'
$script:hasExistingTls = $false
$script:failWacsSource = $true
$script:failWacsTask = $false
$script:activeConfigMode = ''
$script:DuckDnsToken = 'sentinel-token-do-not-log'
$script:configureEvents.Clear()
$wacsError = ''
try { Invoke-HttpsSetup -InstallRoot $script:InstallRoot -HostName 'media.example.test' -EmailAddress 'test@example.test' -DuckDnsSubdomain '' -DuckDnsTokenFile '' } catch { $wacsError=$_.Exception.Message }
if ($script:activeConfigMode -ne 'bootstrap-503' -or $script:configureEvents -notcontains 'wacs-source' -or $script:configureEvents -contains 'wacs-task') { throw 'First ACME issue failure did not preserve fail-closed bootstrap behavior.' }
if ($wacsError -match 'sentinel-token-do-not-log') { throw 'WACS issuance failure exposed a DuckDNS token.' }

$script:failWacsSource = $false
$script:failWacsTask = $true
$script:configureEvents.Clear()
$wacsError = ''
try { Invoke-HttpsSetup -InstallRoot $script:InstallRoot -HostName 'media.example.test' -EmailAddress 'test@example.test' -DuckDnsSubdomain '' -DuckDnsTokenFile '' } catch { $wacsError=$_.Exception.Message }
if ($script:activeConfigMode -ne 'bootstrap-503' -or $script:configureEvents -notcontains 'wacs-task') { throw 'First task setup failure did not preserve fail-closed bootstrap behavior.' }
if ($wacsError -match 'sentinel-token-do-not-log') { throw 'WACS scheduler failure exposed a DuckDNS token.' }

$script:hasExistingTls = $true
$script:failWacsSource = $true
$script:failWacsTask = $false
$script:configureEvents.Clear()
$wacsError = ''
try { Invoke-HttpsSetup -InstallRoot $script:InstallRoot -HostName 'media.example.test' -EmailAddress 'test@example.test' -DuckDnsSubdomain '' -DuckDnsTokenFile '' } catch { $wacsError=$_.Exception.Message }
if ($script:activeConfigMode -ne 'tls' -or $script:configureEvents -notcontains 'wacs-source') { throw 'TLS renewal failure did not preserve the existing TLS listener.' }
if ($wacsError -match 'sentinel-token-do-not-log') { throw 'TLS renewal failure exposed a DuckDNS token.' }
Write-Output 'WACS failures: bootstrap stays 503/no proxy; failed renewal preserves TLS; messages exclude tokens.'

# Update and task failures must restore the prior protected JSON and scheduled task.
$configureDefinition = $definitions | Where-Object Name -eq 'Configure-DuckDns' | Select-Object -First 1
Invoke-Expression $configureDefinition.Extent.Text
$script:missingPath = ''
$tempDir = Join-Path ([IO.Path]::GetTempPath()) ('mflx-duckdns-tdd-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($tempDir)
$script:duckDnsConfig = Join-Path $tempDir 'duckdns.json'
$script:legacyDuckDnsConfig = Join-Path $tempDir 'legacy.json'
$script:duckDnsConfigRoot = $tempDir
$script:duckDnsScript = 'C:\fake\server\duckdns-update.ps1'
$script:duckDnsTask = 'MulletaFlix DuckDNS Update'
$script:assertedPaths = [Collections.Generic.List[string]]::new()
[IO.File]::WriteAllText($script:duckDnsConfig, '{"subdomain":"old","token":"prior-token"}')
$original = [IO.File]::ReadAllText($script:duckDnsConfig)
try {
    foreach ($failure in @('update','register','verify')) {
        $script:failUpdater = $failure -eq 'update'
        $script:failRegister = $failure -eq 'register'
        $script:failVerify = $failure -eq 'verify'
        $script:taskExists = $true
        $script:taskXml = '<Task><RegistrationInfo><Description>prior</Description></RegistrationInfo></Task>'
        $script:configureEvents.Clear()
        $failed = $false
        $failureMessage = ''
        try { Configure-DuckDns } catch { $failed = $true; $failureMessage = $_.Exception.Message }
        if (-not $failed) { throw "DuckDNS $failure failure injection did not fail configuration. Events: $($script:configureEvents -join '|')" }
        if ([IO.File]::ReadAllText($script:duckDnsConfig) -cne $original) { throw "DuckDNS $failure failure did not restore the prior credential config. Message=$failureMessage Content=$([IO.File]::ReadAllText($script:duckDnsConfig)) Events=$($script:configureEvents -join '|')" }
        if (-not $script:taskExists -or $script:taskXml -cne '<Task><RegistrationInfo><Description>prior</Description></RegistrationInfo></Task>') { throw "DuckDNS $failure failure did not restore the prior scheduled task." }
        if ($failureMessage -match 'sentinel-token-do-not-log') { throw 'DuckDNS transaction exposed the credential in its failure message.' }
        if (($script:configureEvents -join '|') -match 'sentinel-token-do-not-log') { throw 'DuckDNS transaction leaked the credential in diagnostics.' }
    }
    Write-Output 'DuckDNS transaction: updater, registration, and task-verification failures restored prior config and task.'

    # A failed first-time configuration removes both the new config and task.
    [IO.File]::Delete($script:duckDnsConfig)
    $script:taskExists = $false
    $script:failUpdater = $false
    $script:failRegister = $true
    $script:failVerify = $false
    try { Configure-DuckDns } catch { }
    if ([IO.File]::Exists($script:duckDnsConfig) -or $script:taskExists) { throw 'Failed first-time DuckDNS setup left a credential file or task behind.' }
    Write-Output 'DuckDNS first setup: failed task registration removed newly created credential and task.'
}
finally { [IO.Directory]::Delete($tempDir,$true) }
