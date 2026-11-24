[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ServerExecutablePath,
    [switch]$RemoveAllProfiles,
    [switch]$Preview
)

$ErrorActionPreference = 'Stop'

function Get-MulletaFlixPublicRuleUpdates {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Rules,
        [Parameter(Mandatory = $true)]
        [string]$ServerExecutablePath,
        [switch]$RemoveAllProfiles
    )

    $targetPath = [System.IO.Path]::GetFullPath($ServerExecutablePath).TrimEnd('\')
    foreach ($rule in $Rules) {
        if (-not $rule.Enabled -or $rule.Direction -ne 'Inbound' -or $rule.Action -ne 'Allow') {
            continue
        }

        try {
            $ruleProgram = [System.IO.Path]::GetFullPath([string]$rule.Program).TrimEnd('\')
        }
        catch {
            continue
        }

        if (-not [string]::Equals($ruleProgram, $targetPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $profiles = @(([string]$rule.Profile -split '\s*,\s*') | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        if ($RemoveAllProfiles) {
            [pscustomobject]@{ Rule = $rule.Rule; Remove = $true; Profile = $null }
            continue
        }

        if ($profiles -notcontains 'Public' -and $profiles -notcontains 'Any') {
            continue
        }

        if ($profiles -contains 'Any') {
            $remainingProfiles = @('Domain', 'Private')
        }
        else {
            $remainingProfiles = @($profiles | Where-Object { $_ -ne 'Public' })
        }

        if ($remainingProfiles.Count -eq 0) {
            [pscustomobject]@{ Rule = $rule.Rule; Remove = $true; Profile = $null }
        }
        else {
            [pscustomobject]@{ Rule = $rule.Rule; Remove = $false; Profile = [string[]]$remainingProfiles }
        }
    }
}

if ($Preview) {
    return
}

if (-not (Test-Path -LiteralPath $ServerExecutablePath -PathType Leaf)) {
    throw "MulletaFlix server executable was not found: $ServerExecutablePath"
}

$firewallRules = Get-NetFirewallRule -PolicyStore PersistentStore -Enabled True -Direction Inbound -Action Allow
$records = foreach ($rule in $firewallRules) {
    $application = $rule | Get-NetFirewallApplicationFilter
    [pscustomobject]@{
        Rule = $rule
        Program = [string]$application.Program
        Direction = [string]$rule.Direction
        Action = [string]$rule.Action
        Enabled = [bool]$rule.Enabled
        Profile = [string]$rule.Profile
    }
}

$updates = @(Get-MulletaFlixPublicRuleUpdates -Rules $records -ServerExecutablePath $ServerExecutablePath -RemoveAllProfiles:$RemoveAllProfiles)
foreach ($update in $updates) {
    if ($update.Remove) {
        Remove-NetFirewallRule -InputObject $update.Rule
    }
    else {
        Set-NetFirewallRule -InputObject $update.Rule -Profile $update.Profile
    }
}

Write-Output "Updated $($updates.Count) inbound firewall rule(s) for the installed MulletaFlix server executable."
