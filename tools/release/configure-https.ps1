[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InstallRoot,

    [Parameter(Mandatory = $true)]
    [string]$HostName,

    [Parameter(Mandatory = $true)]
    [string]$EmailAddress,

    [string]$DuckDnsSubdomain,

    [string]$DuckDnsToken,

    [int]$ServerPort = 8096,
    [switch]$SkipCertificate
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$nginxRoot = Join-Path $InstallRoot 'nginx'
$nginxExe = Join-Path $nginxRoot 'nginx.exe'
$nginxConfig = Join-Path $nginxRoot 'conf\nginx.conf'
$acmeRoot = Join-Path $nginxRoot 'acme'
$certRoot = Join-Path $nginxRoot 'certs'
$wacsExe = Join-Path $InstallRoot 'win-acme\wacs.exe'
$nssmExe = Join-Path $InstallRoot 'nssm.exe'
$reloadScript = Join-Path $nginxRoot 'reload-nginx.ps1'
$nginxService = 'MulletaFlixNginx'
$certPrefix = Join-Path $certRoot $HostName
$duckDnsScript = Join-Path $InstallRoot 'duckdns-update.ps1'
$duckDnsConfigRoot = Join-Path ${env:ProgramData} 'MulletaFlix'
$duckDnsConfig = Join-Path $duckDnsConfigRoot 'duckdns.json'
$duckDnsTask = 'MulletaFlix DuckDNS Update'

function Assert-File([string]$Path, [string]$Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description not found: $Path"
    }
}

if ($HostName -notmatch '^(?=.{1,253}$)[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*$') {
    throw 'Invalid public hostname.'
}
if ($EmailAddress -notmatch '^[^\s@]+@[^\s@]+\.[^\s@]+$') {
    throw 'A valid ACME notification email is required.'
}

function Configure-DuckDns {
    if ([string]::IsNullOrWhiteSpace($DuckDnsSubdomain) -and [string]::IsNullOrWhiteSpace($DuckDnsToken)) { return }
    if ($DuckDnsSubdomain -notmatch '^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$') { throw 'Invalid DuckDNS subdomain.' }
    if ([string]::IsNullOrWhiteSpace($DuckDnsToken)) { throw 'DuckDNS token is required when DuckDNS is enabled.' }
    Assert-File $duckDnsScript 'Bundled DuckDNS updater'
    New-Item -ItemType Directory -Force -Path $duckDnsConfigRoot | Out-Null
    @{ subdomain = $DuckDnsSubdomain; token = $DuckDnsToken } | ConvertTo-Json | Set-Content -LiteralPath $duckDnsConfig -Encoding utf8
    $acl = Get-Acl -LiteralPath $duckDnsConfig
    $acl.SetAccessRuleProtection($true, $false)
    $acl.Access | ForEach-Object { [void]$acl.RemoveAccessRule($_) }
    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule('SYSTEM', 'FullControl', 'Allow')))
    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule('BUILTIN\Administrators', 'FullControl', 'Allow')))
    Set-Acl -LiteralPath $duckDnsConfig -AclObject $acl
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $duckDnsScript -ConfigPath $duckDnsConfig
    if ($LASTEXITCODE -ne 0) { throw 'DuckDNS could not be updated.' }
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$duckDnsScript`" -ConfigPath `"$duckDnsConfig`""
    $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(5) -RepetitionInterval (New-TimeSpan -Minutes 5) -RepetitionDuration (New-TimeSpan -Days 3650)
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    Register-ScheduledTask -TaskName $duckDnsTask -Action $action -Trigger $trigger -Principal $principal -Force | Out-Null
}

function Write-NginxConfig([bool]$TlsEnabled) {
    New-Item -ItemType Directory -Force -Path $acmeRoot, $certRoot, (Split-Path -Parent $nginxConfig) | Out-Null
    $tlsServer = ''
    if ($TlsEnabled) {
        $tlsServer = @"

    server {
        listen 443 ssl;
        server_name $HostName;

        ssl_certificate ${certPrefix}-chain.pem;
        ssl_certificate_key ${certPrefix}-key.pem;
        ssl_protocols TLSv1.2 TLSv1.3;

        location / {
            proxy_pass http://127.0.0.1:$ServerPort;
            proxy_http_version 1.1;
            proxy_set_header Host `$host;
            proxy_set_header X-Real-IP `$remote_addr;
            proxy_set_header X-Forwarded-For `$proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto https;
            proxy_set_header Upgrade `$http_upgrade;
            proxy_set_header Connection `$connection_upgrade;
            proxy_read_timeout 3600;
            proxy_send_timeout 3600;
            proxy_buffering off;
        }
    }
"@
    }

    $httpAction = if ($TlsEnabled) { 'return 301 https://`$host`$request_uri;' } else { "proxy_pass http://127.0.0.1:$ServerPort;" }
    $httpProxy = if ($TlsEnabled) { '' } else { @"
            proxy_http_version 1.1;
            proxy_set_header Host `$host;
            proxy_set_header X-Real-IP `$remote_addr;
            proxy_set_header X-Forwarded-For `$proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto http;
            proxy_set_header Upgrade `$http_upgrade;
            proxy_set_header Connection upgrade;
            proxy_read_timeout 3600;
            proxy_send_timeout 3600;
            proxy_buffering off;
"@ }

    @"
worker_processes 1;

events {
    worker_connections 1024;
}

http {
    include mime.types;
    default_type application/octet-stream;
    sendfile on;
    keepalive_timeout 65;

    map `$http_upgrade `$connection_upgrade {
        default upgrade;
        '' close;
    }

    server {
        listen 80;
        server_name $HostName;

        location /.well-known/acme-challenge/ {
            root $acmeRoot;
            try_files `$uri =404;
        }

        location / {
            $httpAction
$httpProxy
        }
    }
$tlsServer
}
"@ | Set-Content -LiteralPath $nginxConfig -Encoding utf8
}

function Ensure-NginxService {
    Assert-File $nginxExe 'Bundled Nginx executable'
    Assert-File $nssmExe 'NSSM service wrapper'
    $status = & $nssmExe status $nginxService 2>$null
    if ($LASTEXITCODE -ne 0) {
        & $nssmExe install $nginxService $nginxExe | Out-Null
    }
    & $nssmExe set $nginxService AppDirectory $nginxRoot | Out-Null
    & $nssmExe set $nginxService Application $nginxExe | Out-Null
    & $nssmExe set $nginxService AppParameters "-p `"$nginxRoot`" -c conf/nginx.conf" | Out-Null
    & $nssmExe set $nginxService DisplayName 'MulletaFlix HTTPS Proxy (Nginx)' | Out-Null
    & $nssmExe set $nginxService Start SERVICE_AUTO_START | Out-Null
    Start-Service -Name $nginxService -ErrorAction SilentlyContinue
}

function Ensure-FirewallRules {
    foreach ($port in 80, 443) {
        $name = "MulletaFlix Nginx HTTPS $port"
        if (-not (Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue)) {
            New-NetFirewallRule -DisplayName $name -Direction Inbound -Protocol TCP -LocalPort $port -Action Allow -Profile Any | Out-Null
        }
    }
}

function Test-NginxConfig {
    & $nginxExe -p $nginxRoot -c conf/nginx.conf -t
    if ($LASTEXITCODE -ne 0) { throw 'Nginx configuration validation failed.' }
}

function Write-ReloadScript {
    @"
`$ErrorActionPreference = 'Stop'
& '$nginxExe' -p '$nginxRoot' -c conf/nginx.conf -s reload
if (`$LASTEXITCODE -ne 0) { throw 'Nginx reload failed after certificate renewal.' }
"@ | Set-Content -LiteralPath $reloadScript -Encoding utf8
}

Configure-DuckDns
Ensure-FirewallRules
Write-ReloadScript
Write-NginxConfig $false
Test-NginxConfig
Ensure-NginxService

if (-not $SkipCertificate) {
    Assert-File $wacsExe 'Bundled win-acme executable'
    if ([string]::IsNullOrWhiteSpace($EmailAddress)) { throw 'An ACME notification email is required.' }
    & $wacsExe --source manual --host $HostName --validation filesystem --webroot $acmeRoot --store pemfiles --pemfilespath $certRoot --pemfilesname $HostName --emailaddress $EmailAddress --accepttos --installation script --script $reloadScript
    if ($LASTEXITCODE -ne 0) { throw 'win-acme could not issue or configure the certificate.' }
    Assert-File "${certPrefix}-chain.pem" 'Issued certificate chain'
    Assert-File "${certPrefix}-key.pem" 'Issued certificate private key'
    Write-NginxConfig $true
    Test-NginxConfig
    & $nginxExe -p $nginxRoot -c conf/nginx.conf -s reload
    if ($LASTEXITCODE -ne 0) { throw 'Nginx could not reload with the issued certificate.' }
}

Write-Host "HTTPS setup completed for $HostName" -ForegroundColor Green
