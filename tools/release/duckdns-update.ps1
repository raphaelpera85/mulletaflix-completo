[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ConfigPath
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Assert-DuckDnsConfigSecurity(
    [string]$Path,
    [string]$CommonDataRoot = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::CommonApplicationData)
) {
    try {
        $configRoot = [System.IO.Path]::GetFullPath((Join-Path $CommonDataRoot 'MulletaFlix-DuckDNS'))
        $expectedConfigPath = [System.IO.Path]::GetFullPath((Join-Path $configRoot 'duckdns.json'))
        $fullConfigPath = [System.IO.Path]::GetFullPath($Path)
        if (-not $fullConfigPath.Equals($expectedConfigPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'Unexpected DuckDNS configuration path.'
        }
        if (-not [System.IO.Directory]::Exists($configRoot) -or -not [System.IO.File]::Exists($fullConfigPath)) {
            throw 'DuckDNS configuration is missing.'
        }

        # Walk every component so a junction/symlink cannot redirect the protected
        # path before the credential is read.
        $cursor = $fullConfigPath
        while (-not [string]::IsNullOrWhiteSpace($cursor)) {
            $item = Get-Item -LiteralPath $cursor -Force -ErrorAction Stop
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'DuckDNS configuration path contains a reparse point.'
            }
            $parent = [System.IO.Directory]::GetParent($cursor)
            if ($null -eq $parent) { break }
            $cursor = $parent.FullName
        }

        foreach ($protectedPath in @($configRoot, $fullConfigPath)) {
            $acl = Get-Acl -LiteralPath $protectedPath -ErrorAction Stop
            $ownerSid = $acl.GetOwner([System.Security.Principal.SecurityIdentifier]).Value
            if ($ownerSid -notin @('S-1-5-18', 'S-1-5-32-544')) {
                throw 'DuckDNS configuration owner is not trusted.'
            }

            $rules = @($acl.Access)
            $sids = @($rules | ForEach-Object { $_.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value })
            if (-not $acl.AreAccessRulesProtected -or $rules.Count -ne 2 -or
                $sids.Count -ne 2 -or $sids -notcontains 'S-1-5-18' -or $sids -notcontains 'S-1-5-32-544') {
                throw 'DuckDNS configuration ACL is not restricted to SYSTEM and Administrators.'
            }
            foreach ($rule in $rules) {
                if ($rule.AccessControlType -ne [System.Security.AccessControl.AccessControlType]::Allow -or
                    $rule.FileSystemRights -ne [System.Security.AccessControl.FileSystemRights]::FullControl) {
                    throw 'DuckDNS configuration ACL contains an unexpected access rule.'
                }
            }
        }

        return $fullConfigPath
    }
    catch {
        throw 'DuckDNS configuration security validation failed; no credentials were read.'
    }
}

function Invoke-DuckDnsUpdateRequest([string]$Uri) {
    try {
        # DuckDNS requires the token in the HTTPS query string. Never allow verbose
        # diagnostics or redirects to disclose/forward that credential.
        $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -MaximumRedirection 0 -ErrorAction Stop -Verbose:$false
    }
    catch {
        throw 'DuckDNS update request failed; request details were omitted to protect credentials.'
    }

    if ([string]$response.Content -ne 'OK') {
        throw 'DuckDNS update failed; provider returned a non-success response.'
    }
    return $response
}

$validatedConfigPath = Assert-DuckDnsConfigSecurity -Path $ConfigPath
try {
    $config = [System.IO.File]::ReadAllText($validatedConfigPath) | ConvertFrom-Json
}
catch {
    throw 'DuckDNS configuration could not be read; details were omitted to protect credentials.'
}
$Subdomain = [string]$config.subdomain
$Token = [string]$config.token

if ($Subdomain -notmatch '^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$') {
    throw 'DuckDNS subdomain must contain only lowercase letters, numbers and hyphens.'
}
if ([string]::IsNullOrWhiteSpace($Token)) { throw 'DuckDNS token is required.' }

$uri = "https://www.duckdns.org/update?domains=$([uri]::EscapeDataString($Subdomain))&token=$([uri]::EscapeDataString($Token))&ip="
$null = Invoke-DuckDnsUpdateRequest -Uri $uri
Write-Host "DuckDNS updated: $Subdomain.duckdns.org" -ForegroundColor Green
