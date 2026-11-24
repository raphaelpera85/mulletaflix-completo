$ErrorActionPreference = 'Stop'

$scriptsRoot = Split-Path -Parent $PSScriptRoot
$packagingRoot = Split-Path -Parent $scriptsRoot
$repositoryRoot = Split-Path -Parent $packagingRoot
$helperPath = Join-Path $packagingRoot 'remove-public-server-firewall-rules.ps1'
$builderPath = Join-Path $packagingRoot 'build-stage-and-installer.ps1'
$installerPath = Join-Path $packagingRoot 'MulletaFlix-ux-custom\nsis\mulletaflix.nsi'
$smokePath = Join-Path $scriptsRoot 'Test-CleanInstaller.ps1'

if (-not (Test-Path -LiteralPath $helperPath -PathType Leaf)) {
    throw 'A PowerShell helper must remove or restrict public inbound rules for the installed server executable.'
}

$null = . $helperPath -ServerExecutablePath 'C:\Program Files\MulletaFlix\Server\MulletaFlix.exe' -Preview

$target = 'C:\Program Files\MulletaFlix\Server\MulletaFlix.exe'
$rules = @(
    [pscustomobject]@{ Rule = 'public-tcp'; Program = $target; Direction = 'Inbound'; Action = 'Allow'; Enabled = $true; Profile = 'Public' },
    [pscustomobject]@{ Rule = 'public-udp'; Program = $target; Direction = 'Inbound'; Action = 'Allow'; Enabled = $true; Profile = 'Any' },
    [pscustomobject]@{ Rule = 'mixed-profile'; Program = $target; Direction = 'Inbound'; Action = 'Allow'; Enabled = $true; Profile = 'Domain, Private, Public' },
    [pscustomobject]@{ Rule = 'private-only'; Program = $target; Direction = 'Inbound'; Action = 'Allow'; Enabled = $true; Profile = 'Private' },
    [pscustomobject]@{ Rule = 'other-path'; Program = 'D:\staging\MulletaFlix.exe'; Direction = 'Inbound'; Action = 'Allow'; Enabled = $true; Profile = 'Public' },
    [pscustomobject]@{ Rule = 'outbound'; Program = $target; Direction = 'Outbound'; Action = 'Allow'; Enabled = $true; Profile = 'Public' },
    [pscustomobject]@{ Rule = 'block'; Program = $target; Direction = 'Inbound'; Action = 'Block'; Enabled = $true; Profile = 'Public' }
)

$updates = @(Get-MulletaFlixPublicRuleUpdates -Rules $rules -ServerExecutablePath $target)
if ($updates.Count -ne 3) {
    throw "Expected three public or mixed-profile allow rules to be restricted; got $($updates.Count)."
}

$byName = @{}
foreach ($update in $updates) { $byName[$update.Rule] = $update }
if (-not $byName['public-tcp'].Remove) {
    throw 'Rules limited to Public must be removed for the installed server executable.'
}
if ($byName['public-udp'].Remove -or ($byName['public-udp'].Profile -join ',') -ne 'Domain,Private') {
    throw 'Rules enabled on Any must be restricted to Domain and Private profiles.'
}
if ($byName['mixed-profile'].Remove -or ($byName['mixed-profile'].Profile -join ',') -ne 'Domain,Private') {
    throw 'Rules shared with private/domain networks must retain those profiles after Public is removed.'
}
if ('private-only' -in $byName.Keys -or 'other-path' -in $byName.Keys -or 'outbound' -in $byName.Keys -or 'block' -in $byName.Keys) {
    throw 'Unrelated paths, private-only rules, outbound rules, and block rules must be preserved.'
}

$uninstallUpdates = @(Get-MulletaFlixPublicRuleUpdates -Rules $rules -ServerExecutablePath $target -RemoveAllProfiles)
if ($uninstallUpdates.Count -ne 4 -or @($uninstallUpdates | Where-Object { -not $_.Remove }).Count -ne 0) {
    throw 'Uninstall cleanup must remove every inbound allow rule for only the installed executable.'
}

$builder = [System.IO.File]::ReadAllText($builderPath)
$installer = [System.IO.File]::ReadAllText($installerPath)
$smoke = [System.IO.File]::ReadAllText($smokePath)
if ($builder -notmatch '(?s)remove-public-server-firewall-rules\.ps1.*?stageDir') {
    throw 'The release stage must include the firewall remediation helper.'
}
if ($installer -notmatch '(?s)remove-public-server-firewall-rules\.ps1.*?-ServerExecutablePath.*?\$INSTDIR\\MulletaFlix\.exe') {
    throw 'The installer must target the installed executable with the firewall remediation helper.'
}
if ($installer -notmatch '(?s)Remove-PublicServerFirewallRules.*?\$0\s*<>\s*0') {
    throw 'The installer must stop if it cannot apply the firewall remediation.'
}
if ($smoke -notmatch 'PublicServerFirewallRules') {
    throw 'The clean installer smoke test must assert that no Public allow rule remains for the installed server.'
}

Write-Output 'Public inbound server firewall rules are restricted by exact executable path; private access is preserved.'
