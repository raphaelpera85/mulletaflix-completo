[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ConfigPath
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
$Subdomain = [string]$config.subdomain
$Token = [string]$config.token

if ($Subdomain -notmatch '^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$') {
    throw 'DuckDNS subdomain must contain only lowercase letters, numbers and hyphens.'
}
if ([string]::IsNullOrWhiteSpace($Token)) { throw 'DuckDNS token is required.' }

$uri = "https://www.duckdns.org/update?domains=$([uri]::EscapeDataString($Subdomain))&token=$([uri]::EscapeDataString($Token))&ip="
try {
    # DuckDNS requires the token in the HTTPS query string. Never allow verbose
    # diagnostics or redirects to disclose/forward that credential.
    $response = Invoke-WebRequest -Uri $uri -UseBasicParsing -MaximumRedirection 0 -ErrorAction Stop -Verbose:$false
}
catch {
    throw 'DuckDNS update request failed; request details were omitted to protect credentials.'
}

if ([string]$response.Content -ne 'OK') {
    throw 'DuckDNS update failed; provider returned a non-success response.'
}
Write-Host "DuckDNS updated: $Subdomain.duckdns.org" -ForegroundColor Green
