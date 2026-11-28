$ErrorActionPreference = 'Stop'

$configPath = Join-Path $PSScriptRoot '..\configure-https.ps1'
$tokens = $null
$parseErrors = $null
$configAst = [System.Management.Automation.Language.Parser]::ParseFile($configPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw "HTTPS configurator has PowerShell parse errors: $($parseErrors[0].Message)" }

$requiredFunctions = @(
    'Invoke-Nssm',
    'Get-NssmServiceSettings',
    'Set-NssmServiceSettings',
    'Ensure-FirewallRules',
    'Remove-StagedFirewallRules',
    'Remove-ObsoleteNginxFirewallRules',
    'Restore-NginxServiceTransaction',
    'Ensure-NginxService'
)
$functionDefinitions = $configAst.FindAll({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst]
}, $true)
foreach ($functionName in $requiredFunctions) {
    $definition = $functionDefinitions | Where-Object Name -eq $functionName | Select-Object -First 1
    if ($null -eq $definition) { throw "Nginx transaction function is missing: $functionName" }
    Invoke-Expression $definition.Extent.Text
}

$script:nginxExe = 'C:\Program Files\MulletaFlix\Server\nginx\nginx.exe'
$script:nginxRoot = Split-Path -Parent $script:nginxExe
$script:nssmExe = 'mock-nssm'
$script:nginxService = 'MulletaFlixNginx'
$script:nginxStartupTimeoutSeconds = 0
$script:settings = [ordered]@{}
$script:serviceExists = $true
$script:serviceState = 'Running'
$script:failureAtSet = ''
$script:failureTriggered = $false
$script:failRollbackAtSet = ''
$script:rollbackFailureTriggered = $false
$script:failInstall = $false
$script:failRestartCount = 0
$script:failStartCount = 0
$script:simulateProcess = $true
$script:nssmCalls = [System.Collections.Generic.List[object]]::new()
$script:settingSetCalls = [System.Collections.Generic.List[string]]::new()
$script:serviceEvents = [System.Collections.Generic.List[string]]::new()
$script:firewallRules = [System.Collections.Generic.List[object]]::new()
$script:removedFirewallRules = [System.Collections.Generic.List[string]]::new()
$script:fakeConfigTransaction = [pscustomobject]@{ HadPreviousConfig = $true }

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -is [System.Collections.IDictionary] -and $Actual -is [System.Collections.IDictionary]) {
        foreach ($key in $Expected.Keys) {
            if ([string]$Expected[$key] -cne [string]$Actual[$key]) { throw "$Message (setting $key)" }
        }
        return
    }
    if ($Expected -is [array] -and $Actual -is [array]) {
        if (($Expected -join '|') -cne ($Actual -join '|')) { throw $Message }
        return
    }
    if ([string]$Expected -cne [string]$Actual) { throw $Message }
}

function Reset-Fakes {
    $script:settings = [ordered]@{
        Application = 'C:\Legacy\nginx\nginx.exe'
        AppDirectory = 'C:\Legacy\nginx'
        AppParameters = '-p C:\Legacy\nginx -c conf/nginx.conf'
        DisplayName = 'Legacy MulletaFlix Nginx'
        Start = 'SERVICE_DEMAND_START'
    }
    $script:serviceExists = $true
    $script:serviceState = 'Running'
    $script:failureAtSet = ''
    $script:failureTriggered = $false
    $script:failRollbackAtSet = ''
    $script:rollbackFailureTriggered = $false
    $script:failInstall = $false
    $script:failRestartCount = 0
    $script:failStartCount = 0
    $script:simulateProcess = $true
    $script:nssmCalls = [System.Collections.Generic.List[object]]::new()
    $script:settingSetCalls = [System.Collections.Generic.List[string]]::new()
    $script:serviceEvents = [System.Collections.Generic.List[string]]::new()
    $script:firewallRules = [System.Collections.Generic.List[object]]::new()
    $script:removedFirewallRules = [System.Collections.Generic.List[string]]::new()
    $script:fakeConfigTransaction = [pscustomobject]@{ HadPreviousConfig = $true }
    $script:firewallRules.Add([pscustomobject]@{
        Name = 'legacy-broad'; DisplayName = 'MulletaFlix Nginx HTTPS 80'; Program = $script:settings.Application
        Enabled = 'True'; Direction = 'Inbound'; Action = 'Allow'; Profile = 'Public'; Protocol = 'Any'
        LocalPort = 'Any'; LocalAddress = 'Any'; RemoteAddress = 'Any'
    })
    $script:firewallRules.Add([pscustomobject]@{
        Name = 'unrelated-old-path'; DisplayName = 'Operator custom Nginx rule'; Program = $script:settings.Application
        Enabled = 'True'; Direction = 'Inbound'; Action = 'Allow'; Profile = 'Private'; Protocol = 'TCP'
        LocalPort = '9443'; LocalAddress = 'Any'; RemoteAddress = 'Any'
    })
    $script:firewallRules.Add([pscustomobject]@{
        Name = 'new-candidate-broad'; DisplayName = 'nginx.exe'; Program = $script:nginxExe
        Enabled = 'True'; Direction = 'Inbound'; Action = 'Allow'; Profile = 'Public'; Protocol = 'UDP'
        LocalPort = 'Any'; LocalAddress = 'Any'; RemoteAddress = 'Any'
    })
    $script:firewallRules.Add([pscustomobject]@{
        Name = 'generic-nginx-tcp-any'; DisplayName = 'nginx'; Program = $script:nginxExe
        Enabled = 'True'; Direction = 'Inbound'; Action = 'Allow'; Profile = 'Public'; Protocol = 'TCP'
        LocalPort = 'Any'; LocalAddress = 'Any'; RemoteAddress = 'Any'
    })
    $script:firewallRules.Add([pscustomobject]@{
        Name = 'generic-nginx-udp-any'; DisplayName = 'nginx'; Program = $script:nginxExe
        Enabled = 'True'; Direction = 'Inbound'; Action = 'Allow'; Profile = 'Public'; Protocol = 'UDP'
        LocalPort = 'Any'; LocalAddress = 'Any'; RemoteAddress = 'Any'
    })
    $script:firewallRules.Add([pscustomobject]@{
        Name = 'generic-nginx-specific-port'; DisplayName = 'nginx'; Program = $script:nginxExe
        Enabled = 'True'; Direction = 'Inbound'; Action = 'Allow'; Profile = 'Public'; Protocol = 'TCP'
        LocalPort = '9443'; LocalAddress = 'Any'; RemoteAddress = 'Any'
    })
}

# External NSSM calls are replaced with an in-memory service registry.
function Invoke-Nssm {
    param([string[]]$Arguments)
    $script:nssmCalls.Add(@($Arguments))
    switch ($Arguments[0]) {
        'get' {
            $value = [string]$script:settings[$Arguments[2]]
            return [pscustomobject]@{ ExitCode = 0; Output = ([string][char]0 + $value + [string][char]0) }
        }
        'set' {
            $settingName = $Arguments[2]
            $script:settingSetCalls.Add($settingName)
            if ($script:failureAtSet -eq $settingName -and -not $script:failureTriggered) {
                $script:failureTriggered = $true
                return [pscustomobject]@{ ExitCode = 1; Output = '' }
            }
            if ($script:failRollbackAtSet -eq $settingName -and $script:failureTriggered -and -not $script:rollbackFailureTriggered) {
                $script:rollbackFailureTriggered = $true
                return [pscustomobject]@{ ExitCode = 1; Output = '' }
            }
            $script:settings[$settingName] = $Arguments[3]
            return [pscustomobject]@{ ExitCode = 0; Output = '' }
        }
        'install' {
            if ($script:failInstall) { return [pscustomobject]@{ ExitCode = 1; Output = '' } }
            $script:serviceExists = $true
            $script:serviceState = 'Stopped'
            $script:settings = [ordered]@{ Application = $Arguments[2]; AppDirectory = ''; AppParameters = ''; DisplayName = ''; Start = '' }
            return [pscustomobject]@{ ExitCode = 0; Output = '' }
        }
        'remove' {
            $script:serviceExists = $false
            $script:serviceState = 'Missing'
            return [pscustomobject]@{ ExitCode = 0; Output = '' }
        }
        default { throw "Unexpected NSSM action in test: $($Arguments[0])" }
    }
}

function Assert-File { param([string]$Path, [string]$Description) }
function Get-Service {
    param([string]$Name, [string]$ErrorAction)
    if (-not $script:serviceExists) { return $null }
    return [pscustomobject]@{ Name = $Name; Status = $script:serviceState }
}
function Start-Service {
    param([string]$Name, [string]$ErrorAction)
    $script:serviceEvents.Add('start')
    if ($script:failStartCount -gt 0) { $script:failStartCount--; $script:serviceState = 'Starting'; throw 'simulated start failure' }
    $script:serviceState = 'Running'
}
function Restart-Service {
    param([string]$Name, [switch]$Force, [string]$ErrorAction)
    $script:serviceEvents.Add('restart')
    if ($script:failRestartCount -gt 0) { $script:failRestartCount--; $script:serviceState = 'Stopped'; throw 'simulated restart failure' }
    $script:serviceState = 'Running'
}
function Stop-Service {
    param([string]$Name, [string]$ErrorAction)
    $script:serviceEvents.Add('stop')
    $script:serviceState = 'Stopped'
}
function Start-Sleep { param([int]$Milliseconds) }
function Restore-NginxConfig { param($Transaction) $script:serviceEvents.Add('config-restored') }
function Complete-NginxConfig { param($Transaction) $script:serviceEvents.Add('config-committed') }
function Invoke-TestNginxService { $null = Ensure-NginxService -NginxConfigTransaction $script:fakeConfigTransaction }
function Get-CimInstance {
    param([string]$ClassName, [string]$Filter, [string]$ErrorAction)
    if ($script:simulateProcess -and $script:serviceExists -and $script:serviceState -eq 'Running' -and $script:settings.Application -eq $script:nginxExe) {
        $script:serviceEvents.Add('process-verified')
        return @([pscustomobject]@{ ExecutablePath = [string]$script:settings.Application })
    }
    return @()
}

# Firewall cmdlets are isolated in memory; no host firewall is read or changed.
function Get-NetFirewallRule {
    param([string]$DisplayName, [string]$Enabled, [string]$Direction, [string]$Action, [string]$ErrorAction)
    $items = @($script:firewallRules)
    if ($PSBoundParameters.ContainsKey('DisplayName')) { $items = @($items | Where-Object DisplayName -eq $DisplayName) }
    if ($PSBoundParameters.ContainsKey('Enabled')) { $items = @($items | Where-Object Enabled -eq $Enabled) }
    if ($PSBoundParameters.ContainsKey('Direction')) { $items = @($items | Where-Object Direction -eq $Direction) }
    if ($PSBoundParameters.ContainsKey('Action')) { $items = @($items | Where-Object Action -eq $Action) }
    return $items
}
function Get-NetFirewallApplicationFilter { param([Parameter(ValueFromPipeline = $true)]$InputObject) process { [pscustomobject]@{ Program = $InputObject.Program } } }
function Get-NetFirewallPortFilter { param([Parameter(ValueFromPipeline = $true)]$InputObject) process { [pscustomobject]@{ Protocol = $InputObject.Protocol; LocalPort = $InputObject.LocalPort } } }
function Get-NetFirewallAddressFilter { param([Parameter(ValueFromPipeline = $true)]$InputObject) process { [pscustomobject]@{ LocalAddress = $InputObject.LocalAddress; RemoteAddress = $InputObject.RemoteAddress } } }
function New-NetFirewallRule {
    param([string]$DisplayName, [string]$Program, [string]$Direction, [string]$Protocol, [string]$LocalPort, [string]$LocalAddress, [string]$RemoteAddress, [string]$Action, [string]$Profile, [switch]$PassThru)
    $rule = [pscustomobject]@{ Name = [guid]::NewGuid().ToString('N'); DisplayName = $DisplayName; Program = $Program; Direction = $Direction; Protocol = $Protocol; LocalPort = $LocalPort; LocalAddress = $LocalAddress; RemoteAddress = $RemoteAddress; Action = $Action; Profile = $Profile; Enabled = 'True' }
    $script:firewallRules.Add($rule)
    $script:serviceEvents.Add("firewall-add:$LocalPort")
    return $rule
}
function Remove-NetFirewallRule {
    param($InputObject, [string]$ErrorAction)
    [void]$script:firewallRules.Remove($InputObject)
    $script:removedFirewallRules.Add([string]$InputObject.Name)
    $script:serviceEvents.Add("firewall-remove:$($InputObject.Name)")
}

$candidateSettings = [ordered]@{
    Application = $script:nginxExe
    AppDirectory = $script:nginxRoot
    AppParameters = "-p `"$($script:nginxRoot)`" -c conf/nginx.conf"
    DisplayName = 'MulletaFlix HTTPS Proxy (Nginx)'
    Start = 'SERVICE_AUTO_START'
}

Reset-Fakes
$setNames = @('AppDirectory', 'Application', 'AppParameters', 'DisplayName', 'Start')
foreach ($setName in $setNames) {
    Reset-Fakes
    $before = [ordered]@{}; foreach($key in $script:settings.Keys){$before[$key]=$script:settings[$key]}
    $script:failureAtSet = $setName
    $failed = $false
    $failureMessage = ''
    try { Invoke-TestNginxService } catch { $failed = $true; $failureMessage = $_.Exception.Message }
    if (-not $failed) { throw "NSSM set failure '$setName' did not fail the transaction." }
    Assert-Equal $before $script:settings "NSSM setting '$setName' was not restored."
    Assert-Equal 'Running' $script:serviceState "Service state was not restored after NSSM set '$setName' failed."
    $stagedRemaining = @($script:firewallRules | Where-Object { $_.Program -eq $script:nginxExe -and $_.Protocol -eq 'TCP' -and $_.LocalPort -in @('80','443') }).Count
    if ($stagedRemaining -ne 0) { throw "Staged firewall rules survived rollback after NSSM set '$setName' failed." }
    if (@($script:firewallRules | Where-Object Name -eq 'new-candidate-broad').Count -ne 1) { throw "Pre-existing candidate rule changed before service health after NSSM set '$setName' failed." }
    if (@($script:firewallRules | Where-Object Program -eq $before.Application).Count -ne 2) { throw "Old firewall rules changed before a healthy migration after NSSM set '$setName' failed." }
    if (-not $script:failureTriggered) { throw "NSSM failure injection did not run for '$setName'. Unexpected failure: $failureMessage" }
    $configRestoreIndex = $script:serviceEvents.IndexOf('config-restored')
    $serviceRestoreStartIndex = $script:serviceEvents.LastIndexOf('start')
    if ($configRestoreIndex -lt 0 -or $serviceRestoreStartIndex -lt 0 -or $configRestoreIndex -ge $serviceRestoreStartIndex) { throw "Configuration rollback did not precede service restoration after NSSM set '$setName' failed." }
}
Write-Output 'NSSM set failures: all five parameters rolled back with old service and firewall preserved.'

Reset-Fakes
$script:failureAtSet = 'AppDirectory'
$script:failRollbackAtSet = 'Application'
$rollbackIncomplete = $false
try { Invoke-TestNginxService } catch { $rollbackIncomplete = $_.Exception.Message -match 'rollback was incomplete' }
if (-not $rollbackIncomplete -or -not $script:rollbackFailureTriggered) { throw 'NSSM rollback failure was not reported.' }
$failedRestoreIndex = $script:settingSetCalls.IndexOf('Application')
foreach ($laterSetting in @('AppParameters', 'DisplayName', 'Start')) {
    $laterRestoreIndex = $script:settingSetCalls.LastIndexOf($laterSetting)
    if ($laterRestoreIndex -le $failedRestoreIndex) { throw "NSSM rollback stopped before attempting '$laterSetting'." }
}
Assert-Equal 'Running' $script:serviceState 'Service state restoration was skipped after one NSSM setting restore failed.'
if (@($script:firewallRules | Where-Object { $_.Program -eq $script:nginxExe -and $_.Protocol -eq 'TCP' -and $_.LocalPort -in @('80','443') }).Count -ne 0) { throw 'Staged firewall rules survived an incomplete NSSM rollback.' }
Write-Output 'Rollback setter failure: remaining NSSM values and original service state were still attempted.'

Reset-Fakes
$script:failRestartCount = 1
$before = [ordered]@{}; foreach($key in $script:settings.Keys){$before[$key]=$script:settings[$key]}
$failed = $false
try { Invoke-TestNginxService } catch { $failed = $true }
if (-not $failed) { throw 'Restart failure did not fail the migration transaction.' }
Assert-Equal $before $script:settings 'NSSM settings were not restored after restart failure.'
Assert-Equal 'Running' $script:serviceState 'Previously running service was not restored after restart failure.'
if (@($script:firewallRules | Where-Object { $_.Program -eq $script:nginxExe -and $_.Protocol -eq 'TCP' -and $_.LocalPort -in @('80','443') }).Count -ne 0) { throw 'Staged firewall rules survived rollback after restart failure.' }
if (@($script:firewallRules | Where-Object Name -eq 'new-candidate-broad').Count -ne 1) { throw 'Pre-existing candidate rule changed before service health after restart failure.' }
if (@($script:firewallRules | Where-Object Program -eq $before.Application).Count -ne 2) { throw 'Old firewall rules changed before a healthy migration after restart failure.' }
$configRestoreIndex = $script:serviceEvents.IndexOf('config-restored')
$serviceRestoreStartIndex = $script:serviceEvents.LastIndexOf('start')
if ($configRestoreIndex -lt 0 -or $serviceRestoreStartIndex -lt 0 -or $configRestoreIndex -ge $serviceRestoreStartIndex) { throw 'Configuration rollback did not precede service restoration after restart failure.' }
Write-Output 'Restart failure: old NSSM settings/state and firewall rules restored.'

Reset-Fakes
$script:simulateProcess = $false
$before = [ordered]@{}; foreach($key in $script:settings.Keys){$before[$key]=$script:settings[$key]}
$failed = $false
try { Invoke-TestNginxService } catch { $failed = $true }
if (-not $failed) { throw 'Process verification failure did not fail the migration transaction.' }
Assert-Equal $before $script:settings 'NSSM settings were not restored after process verification failure.'
Assert-Equal 'Running' $script:serviceState 'Previously running service state was not restored after process verification failure.'
if (@($script:firewallRules | Where-Object { $_.Program -eq $script:nginxExe -and $_.Protocol -eq 'TCP' -and $_.LocalPort -in @('80','443') }).Count -ne 0) { throw 'Staged firewall rules survived rollback after process verification failure.' }
if (@($script:firewallRules | Where-Object Name -eq 'new-candidate-broad').Count -ne 1) { throw 'Pre-existing candidate rule changed before service health after process verification failure.' }
$configRestoreIndex = $script:serviceEvents.IndexOf('config-restored')
$serviceRestoreStartIndex = $script:serviceEvents.LastIndexOf('start')
if ($configRestoreIndex -lt 0 -or $serviceRestoreStartIndex -lt 0 -or $configRestoreIndex -ge $serviceRestoreStartIndex) { throw 'Configuration rollback did not precede service restoration after process verification failure.' }
Write-Output 'Process verification failure: NSSM transaction and staged rules rolled back.'

Reset-Fakes
$script:serviceState = 'Stopped'
$script:failStartCount = 1
$before = [ordered]@{}; foreach($key in $script:settings.Keys){$before[$key]=$script:settings[$key]}
$failed = $false
try { Invoke-TestNginxService } catch { $failed = $true }
if (-not $failed) { throw 'Start failure did not fail the migration transaction.' }
Assert-Equal $before $script:settings 'NSSM settings were not restored after start failure.'
Assert-Equal 'Stopped' $script:serviceState 'Previously stopped service was started during rollback.'
if (@($script:firewallRules | Where-Object { $_.Program -eq $script:nginxExe -and $_.Protocol -eq 'TCP' -and $_.LocalPort -in @('80','443') }).Count -ne 0) { throw 'Staged firewall rules survived rollback after start failure.' }
if (@($script:firewallRules | Where-Object Name -eq 'new-candidate-broad').Count -ne 1) { throw 'Pre-existing candidate rule changed before service health after start failure.' }
Write-Output 'Start failure: original stopped state restored and staged rules removed.'

Reset-Fakes
$script:serviceExists = $false
$script:serviceState = 'Missing'
$script:settings = [ordered]@{}
$script:failureAtSet = 'Application'
$failed = $false
try { Invoke-TestNginxService } catch { $failed = $true }
if (-not $failed) { throw 'New-service setup failure did not fail the migration transaction.' }
Assert-Equal $false $script:serviceExists 'New service remained installed after rollback.'
Assert-Equal 'Missing' $script:serviceState 'New service remained running after rollback.'
if (@($script:firewallRules | Where-Object { $_.Program -eq $script:nginxExe -and $_.Protocol -eq 'TCP' -and $_.LocalPort -in @('80','443') }).Count -ne 0) { throw 'Staged firewall rules survived new-service rollback.' }
if (@($script:firewallRules | Where-Object Name -eq 'new-candidate-broad').Count -ne 1) { throw 'Pre-existing candidate rule changed before new service health.' }
Write-Output 'New-service failure: created service and staged rules removed.'

Reset-Fakes
$script:serviceExists = $false
$script:serviceState = 'Missing'
$script:settings = [ordered]@{}
$script:failInstall = $true
$failed = $false
try { Invoke-TestNginxService } catch { $failed = $true; if ($_.Exception.Message -match 'rollback was incomplete') { throw } }
if (-not $failed) { throw 'NSSM install failure did not fail the migration.' }
Assert-Equal $false $script:serviceExists 'Failed service installation left a service registered.'
if (@($script:firewallRules | Where-Object { $_.Program -eq $script:nginxExe -and $_.Protocol -eq 'TCP' -and $_.LocalPort -in @('80','443') }).Count -ne 0) { throw 'Staged firewall rules survived NSSM install failure.' }
Write-Output 'Install failure before service creation: transaction failed cleanly without false rollback error.'

Reset-Fakes
$script:serviceEvents.Clear()
Invoke-TestNginxService
Assert-Equal $candidateSettings $script:settings 'Successful migration did not apply all expected NSSM settings.'
Assert-Equal 'Running' $script:serviceState 'Successful migration did not leave the service running.'
if (@($script:firewallRules | Where-Object { $_.Program -eq $script:nginxExe -and $_.Protocol -eq 'TCP' -and $_.LocalPort -in @('80','443') -and $_.Profile -eq 'Any' -and $_.LocalAddress -eq 'Any' -and $_.RemoteAddress -eq 'Any' }).Count -ne 2) { throw 'Successful migration did not retain exactly two exact candidate TCP web rules.' }
if (@($script:firewallRules | Where-Object Program -eq 'C:\Legacy\nginx\nginx.exe').Count -ne 1 -or
    @($script:firewallRules | Where-Object Name -eq 'unrelated-old-path').Count -ne 1) { throw 'Successful migration did not remove only the MulletaFlix-owned old executable rule.' }
if (@($script:firewallRules | Where-Object Name -eq 'new-candidate-broad').Count -ne 1) { throw 'Successful migration removed a broad candidate firewall rule without MulletaFlix ownership.' }
if (@($script:firewallRules | Where-Object Name -in @('generic-nginx-tcp-any', 'generic-nginx-udp-any')).Count -ne 0) { throw 'Successful migration left broad generic firewall rules attached to the validated MulletaFlix Nginx executable.' }
if (@($script:firewallRules | Where-Object Name -eq 'generic-nginx-specific-port').Count -ne 1) { throw 'Successful migration removed a port-specific generic rule attached to the validated Nginx executable.' }
$healthyIndex = $script:serviceEvents.IndexOf('process-verified')
$commitIndex = $script:serviceEvents.IndexOf('config-committed')
$oldRuleCleanupIndex = $script:serviceEvents.IndexOf('firewall-remove:legacy-broad')
if ($healthyIndex -lt 0 -or $commitIndex -le $healthyIndex -or $oldRuleCleanupIndex -le $commitIndex) { throw 'Owned old-path firewall cleanup ran before health and config commit.' }
Write-Output 'Success: unowned rules remain; owned old-path rules are removed after health and config commit.'

Reset-Fakes
$script:serviceState = 'Stopped'
$script:serviceExists = $false
$script:settings = [ordered]@{}
$script:failStartCount = 0
Invoke-TestNginxService
Assert-Equal $true $script:serviceExists 'New service was not registered on success.'
Assert-Equal 'Running' $script:serviceState 'New service was not started on success.'
Write-Output 'Success: new service is registered and running.'
