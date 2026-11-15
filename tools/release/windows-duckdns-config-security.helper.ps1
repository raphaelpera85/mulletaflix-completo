param([Parameter(Mandatory = $true)][string]$UpdaterPath)

$ErrorActionPreference = 'Stop'
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($UpdaterPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw 'Updater PowerShell syntax is invalid.' }
$functionAst = $ast.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Assert-DuckDnsConfigSecurity'
}, $true)
if ($null -eq $functionAst) { throw 'Updater config security validator was not found.' }
Invoke-Expression $functionAst.Extent.Text

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('mflx-duckdns-security-' + [guid]::NewGuid().ToString('N'))
$configRoot = Join-Path $testRoot 'MulletaFlix-DuckDNS'
$configPath = Join-Path $configRoot 'duckdns.json'
$global:DuckDnsConfigRoot = $configRoot
$global:DuckDnsConfigPath = $configPath
[void][System.IO.Directory]::CreateDirectory($configRoot)
[System.IO.File]::WriteAllText($configPath, '{"subdomain":"test-domain","token":"test-only-secret"}')

function global:Get-Acl([string]$LiteralPath) {
    $systemSid = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-18')
    $adminsSid = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-32-544')
    $rules = @(
        [pscustomobject]@{ IdentityReference = $systemSid; AccessControlType = [System.Security.AccessControl.AccessControlType]::Allow; FileSystemRights = [System.Security.AccessControl.FileSystemRights]::FullControl },
        [pscustomobject]@{ IdentityReference = $adminsSid; AccessControlType = [System.Security.AccessControl.AccessControlType]::Allow; FileSystemRights = [System.Security.AccessControl.FileSystemAccessRule]::new($adminsSid, 'FullControl', 'Allow').FileSystemRights }
    )
    $protected = $true
    $owner = $systemSid
    if ($global:DuckDnsAclMode -eq 'untrusted-owner') { $owner = [System.Security.Principal.WindowsIdentity]::GetCurrent().User }
    if ($global:DuckDnsAclMode -eq 'unprotected' -and $LiteralPath -eq $global:DuckDnsConfigRoot) { $protected = $false }
    if ($global:DuckDnsAclMode -eq 'permissive' -and $LiteralPath -eq $global:DuckDnsConfigPath) {
        $currentSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
        $rules += [pscustomobject]@{ IdentityReference = $currentSid; AccessControlType = [System.Security.AccessControl.AccessControlType]::Allow; FileSystemRights = [System.Security.AccessControl.FileSystemRights]::Read }
    }
    $acl = [pscustomobject]@{ Access = $rules; AreAccessRulesProtected = $protected; OwnerSid = $owner }
    $acl | Add-Member -MemberType ScriptMethod -Name GetOwner -Value { param($sidType); return $this.OwnerSid }
    return $acl
}

function global:Get-Item {
    param([string]$LiteralPath, [switch]$Force, [string]$ErrorAction)
    $item = Microsoft.PowerShell.Management\Get-Item -LiteralPath $LiteralPath -Force -ErrorAction Stop
    if ($global:DuckDnsReparsePath -and [System.IO.Path]::GetFullPath($LiteralPath).Equals($global:DuckDnsReparsePath, [System.StringComparison]::OrdinalIgnoreCase)) {
        return [pscustomobject]@{ Attributes = [System.IO.FileAttributes]::ReparsePoint }
    }
    return $item
}

function Assert-Rejected([scriptblock]$Action, [string]$Case) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true; if ($_.Exception.Message -notmatch 'security validation failed') { throw "${Case}: failure was not sanitized." } }
    if (-not $rejected) { throw "${Case}: invalid configuration was accepted." }
}

try {
    $global:DuckDnsAclMode = 'valid'
    $global:DuckDnsReparsePath = $null
    $result = Assert-DuckDnsConfigSecurity -Path $configPath -CommonDataRoot $testRoot
    if (-not $result.Equals([System.IO.Path]::GetFullPath($configPath), [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Protected DuckDNS configuration path was not canonicalized.'
    }

    Assert-Rejected { Assert-DuckDnsConfigSecurity -Path (Join-Path $testRoot 'untrusted.json') -CommonDataRoot $testRoot } 'outside-root path'

    $global:DuckDnsAclMode = 'untrusted-owner'
    Assert-Rejected { Assert-DuckDnsConfigSecurity -Path $configPath -CommonDataRoot $testRoot } 'untrusted owner'

    $global:DuckDnsAclMode = 'unprotected'
    Assert-Rejected { Assert-DuckDnsConfigSecurity -Path $configPath -CommonDataRoot $testRoot } 'inherited ACL'

    $global:DuckDnsAclMode = 'permissive'
    Assert-Rejected { Assert-DuckDnsConfigSecurity -Path $configPath -CommonDataRoot $testRoot } 'additional ACL principal'

    $global:DuckDnsAclMode = 'valid'
    $global:DuckDnsReparsePath = [System.IO.Path]::GetFullPath($configRoot)
    Assert-Rejected { Assert-DuckDnsConfigSecurity -Path $configPath -CommonDataRoot $testRoot } 'reparse-point component'

    Write-Output 'DuckDNS updater config path, reparse-point, owner, and ACL tests passed.'
}
finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item Function:\Get-Acl -ErrorAction SilentlyContinue
    Remove-Item Function:\Get-Item -ErrorAction SilentlyContinue
    Remove-Variable DuckDnsAclMode -Scope Global -ErrorAction SilentlyContinue
    Remove-Variable DuckDnsReparsePath -Scope Global -ErrorAction SilentlyContinue
    Remove-Variable DuckDnsConfigRoot -Scope Global -ErrorAction SilentlyContinue
    Remove-Variable DuckDnsConfigPath -Scope Global -ErrorAction SilentlyContinue
}
