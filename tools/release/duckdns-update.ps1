[CmdletBinding()]
param(
    [string]$ConfigPath,

    [string]$Subdomain,

    [Parameter(Mandatory = $true)]
    [string]$Token
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

if (-not [string]::IsNullOrWhiteSpace($ConfigPath)) {
    $config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
    $Subdomain = [string]$config.subdomain
    $Token = [string]$config.token
}

if ($Subdomain -notmatch '^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$') {
    throw 'DuckDNS subdomain must contain only lowercase letters, numbers and hyphens.'
}
if ([string]::IsNullOrWhiteSpace($Token)) { throw 'DuckDNS token is required.' }

$uri = "https://www.duckdns.org/update?domains=$([uri]::EscapeDataString($Subdomain))&token=$([uri]::EscapeDataString($Token))&ip="
$result = (Invoke-WebRequest -Uri $uri -UseBasicParsing).Content.Trim()
if ($result -ne 'OK') { throw "DuckDNS update failed: $result" }
Write-Host "DuckDNS updated: $Subdomain.duckdns.org" -ForegroundColor Green
