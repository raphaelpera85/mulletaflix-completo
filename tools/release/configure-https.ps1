[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InstallRoot,

    [string]$HostName,

    [string]$EmailAddress,

    [string]$DuckDnsSubdomain,

    [string]$DuckDnsTokenFile,

    [switch]$PrepareDuckDnsTokenFile,

    [switch]$DisableDuckDns,

    [int]$ServerPort = 8096,
    [switch]$SkipCertificate
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$nginxRoot = Join-Path $InstallRoot 'nginx'
$nginxExe = Join-Path $nginxRoot 'nginx.exe'
$nginxConfig = Join-Path $nginxRoot 'conf\nginx.conf'
$nginxCandidateConfig = "$nginxConfig.candidate"
$nginxRollbackConfig = "$nginxConfig.rollback"
$acmeRoot = Join-Path $nginxRoot 'acme'
$certRoot = Join-Path $nginxRoot 'certs'
$wacsExe = Join-Path $InstallRoot 'win-acme\wacs.exe'
$nssmExe = Join-Path $InstallRoot 'nssm.exe'
$reloadScript = Join-Path $nginxRoot 'reload-nginx.ps1'
$nginxService = 'MulletaFlixNginx'
$certPrefix = Join-Path $certRoot $HostName
$duckDnsScript = Join-Path $InstallRoot 'duckdns-update.ps1'
$programDataRoot = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::CommonApplicationData)
if ([string]::IsNullOrWhiteSpace($programDataRoot)) { throw 'The system-wide application data directory could not be resolved.' }
$duckDnsConfigRoot = Join-Path $programDataRoot 'MulletaFlix-DuckDNS'
$duckDnsConfig = Join-Path $duckDnsConfigRoot 'duckdns.json'
$legacyDuckDnsConfig = Join-Path $programDataRoot 'MulletaFlix\duckdns.json'
$duckDnsTask = 'MulletaFlix DuckDNS Update'

if (-not ('MulletaFlix.DuckDns.SafeTokenFile' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace MulletaFlix.DuckDns
{
    public static class SafeTokenFile
    {
        private const uint GenericRead = 0x80000000;
        private const uint OpenExisting = 3;
        private const uint OpenReparsePoint = 0x00200000;
        private const uint FileAttributeNormal = 0x80;
        private const uint FileAttributeDirectory = 0x10;
        private const uint FileAttributeReparsePoint = 0x400;

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation information);

        public static string ReadAllText(string path)
        {
            using (var handle = CreateFile(path, GenericRead, 0, IntPtr.Zero, OpenExisting, OpenReparsePoint | FileAttributeNormal, IntPtr.Zero))
            {
                if (handle.IsInvalid)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the DuckDNS token file securely.");
                }

                ByHandleFileInformation information;
                if (!GetFileInformationByHandle(handle, out information))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not inspect the DuckDNS token file handle.");
                }

                if ((information.FileAttributes & FileAttributeReparsePoint) != 0)
                {
                    throw new InvalidDataException("DuckDNS token file cannot be a reparse point.");
                }

                if ((information.FileAttributes & FileAttributeDirectory) != 0)
                {
                    throw new InvalidDataException("DuckDNS token path must be a regular file.");
                }

                using (var stream = new FileStream(handle, FileAccess.Read))
                using (var reader = new StreamReader(stream, new UTF8Encoding(false, true), true))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
'@
}

function Assert-File([string]$Path, [string]$Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description not found: $Path"
    }
}

function Assert-DuckDnsAdministrator {
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object System.Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'DuckDNS credential setup must run from an elevated Administrator process.'
    }
}

function Read-DuckDnsTokenFile([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'Protected DuckDNS token file is required.' }
    $tokenFilePath = [System.IO.Path]::GetFullPath($Path)
    $configRoot = [System.IO.Path]::GetFullPath($duckDnsConfigRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $tokenFilePath.StartsWith($configRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'DuckDNS token file must be inside the protected credential directory.' }
    $safeToDelete = $false
    try {
        if (-not (Test-Path -LiteralPath $tokenFilePath -PathType Leaf)) { throw 'Protected DuckDNS token file was not found.' }
        $tokenDirectory = Split-Path -Parent $tokenFilePath
        Assert-NoDuckDnsReparsePoints -Path $tokenFilePath
        Assert-ProtectedDuckDnsAcl -Path $tokenDirectory
        Assert-ProtectedDuckDnsAcl -Path $tokenFilePath
        $safeToDelete = $true
        $token = [MulletaFlix.DuckDns.SafeTokenFile]::ReadAllText($tokenFilePath).TrimEnd([char[]]"`r`n")
        if ([string]::IsNullOrWhiteSpace($token)) { throw 'DuckDNS token is required.' }
        return $token
    }
    finally {
        if ($safeToDelete) {
            Remove-Item -LiteralPath $tokenFilePath -Force -ErrorAction SilentlyContinue
        }
    }
}

function Set-ProtectedDuckDnsAcl([string]$Path, [switch]$Directory) {
    if ($Directory) { $acl = New-Object System.Security.AccessControl.DirectorySecurity }
    else { $acl = New-Object System.Security.AccessControl.FileSecurity }
    $acl.SetAccessRuleProtection($true, $false)
    $systemSid = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-18')
    $administratorsSid = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-32-544')
    $acl.SetOwner($systemSid)
    if ($Directory) {
        $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($systemSid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
        $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($administratorsSid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
    }
    else {
        $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($systemSid, 'FullControl', 'Allow')))
        $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($administratorsSid, 'FullControl', 'Allow')))
    }
    Set-Acl -LiteralPath $Path -AclObject $acl
}

function Assert-NoDuckDnsReparsePoints([string]$Path) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $boundary = [System.IO.Path]::GetFullPath($programDataRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    if (-not $fullPath.StartsWith($boundary + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'DuckDNS files must remain under ProgramData.'
    }
    $cursor = $fullPath
    while ($true) {
        $item = Get-Item -LiteralPath $cursor -Force -ErrorAction Stop
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'DuckDNS paths cannot contain reparse points.'
        }
        if ($cursor.Equals($boundary, [System.StringComparison]::OrdinalIgnoreCase)) { break }
        $cursor = Split-Path -Parent $cursor
    }
}

function Assert-ProtectedDuckDnsAcl([string]$Path) {
    $acl = Get-Acl -LiteralPath $Path
    $ownerSid = $acl.GetOwner([System.Security.Principal.SecurityIdentifier]).Value
    if ($ownerSid -notin @('S-1-5-18', 'S-1-5-32-544')) {
        throw 'DuckDNS credential owner is not trusted.'
    }
    $sids = @($acl.Access | ForEach-Object { $_.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value })
    if (-not $acl.AreAccessRulesProtected -or $sids.Count -ne 2 -or $sids -notcontains 'S-1-5-18' -or $sids -notcontains 'S-1-5-32-544') {
        throw 'DuckDNS credential ACL is not restricted to SYSTEM and Administrators.'
    }
    foreach ($rule in $acl.Access) {
        if ($rule.AccessControlType -ne [System.Security.AccessControl.AccessControlType]::Allow -or $rule.FileSystemRights -ne [System.Security.AccessControl.FileSystemRights]::FullControl) {
            throw 'DuckDNS credential ACL contains an unexpected access rule.'
        }
    }
}

function Ensure-ProtectedDuckDnsConfigRoot {
    if (-not [System.IO.Directory]::Exists($duckDnsConfigRoot)) {
        $secureDirectoryAcl = New-Object System.Security.AccessControl.DirectorySecurity
        $secureDirectoryAcl.SetAccessRuleProtection($true, $false)
        $systemSid = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-18')
        $administratorsSid = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-32-544')
        $secureDirectoryAcl.SetOwner($systemSid)
        $secureDirectoryAcl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($systemSid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
        $secureDirectoryAcl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($administratorsSid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
        [void][System.IO.Directory]::CreateDirectory($duckDnsConfigRoot, $secureDirectoryAcl)
    }
    Assert-ProtectedDuckDnsAcl -Path $duckDnsConfigRoot
    Assert-NoDuckDnsReparsePoints -Path $duckDnsConfigRoot
}

function Prepare-DuckDnsTokenFile {
    Ensure-ProtectedDuckDnsConfigRoot
    $tokenPath = Join-Path $duckDnsConfigRoot 'installer-token.txt'
    if ([System.IO.File]::Exists($tokenPath)) {
        Assert-NoDuckDnsReparsePoints -Path $tokenPath
        Assert-ProtectedDuckDnsAcl -Path $tokenPath
        [System.IO.File]::Delete($tokenPath)
    }

    $stream = [System.IO.File]::Open($tokenPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    $stream.Dispose()
    Set-ProtectedDuckDnsAcl -Path $tokenPath
    Assert-ProtectedDuckDnsAcl -Path $duckDnsConfigRoot
    Assert-ProtectedDuckDnsAcl -Path $tokenPath
    Assert-NoDuckDnsReparsePoints -Path $tokenPath
}

function Protect-LegacyDuckDnsConfig {
    if (-not [System.IO.File]::Exists($legacyDuckDnsConfig)) { return }
    Assert-NoDuckDnsReparsePoints -Path $legacyDuckDnsConfig
    $legacyItem = Get-Item -LiteralPath $legacyDuckDnsConfig
    if (($legacyItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Legacy DuckDNS configuration cannot be a reparse point.' }
    Set-ProtectedDuckDnsAcl -Path $legacyDuckDnsConfig
}

function Remove-LegacyDuckDnsConfig {
    if (-not [System.IO.File]::Exists($legacyDuckDnsConfig)) { return }
    Assert-NoDuckDnsReparsePoints -Path $legacyDuckDnsConfig
    $legacyAcl = Get-Acl -LiteralPath $legacyDuckDnsConfig
    $legacySids = @($legacyAcl.Access | ForEach-Object { $_.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value })
    if (-not $legacyAcl.AreAccessRulesProtected -or $legacySids.Count -ne 2 -or $legacySids -notcontains 'S-1-5-18' -or $legacySids -notcontains 'S-1-5-32-544') {
        throw 'Legacy DuckDNS configuration is not protected; refusing to remove or migrate it.'
    }
    foreach ($rule in $legacyAcl.Access) {
        if ($rule.AccessControlType -ne [System.Security.AccessControl.AccessControlType]::Allow -or $rule.FileSystemRights -ne [System.Security.AccessControl.FileSystemRights]::FullControl) {
            throw 'Legacy DuckDNS configuration has an unexpected access rule; refusing to remove it.'
        }
    }
    $task = Get-ScheduledTask -TaskName $duckDnsTask -ErrorAction SilentlyContinue
    if ($null -eq $task) { throw 'Updated DuckDNS scheduled task could not be verified; legacy configuration was retained.' }
    $actions = @($task.Actions)
    $expectedArguments = "-NoProfile -ExecutionPolicy Bypass -File `"$duckDnsScript`" -ConfigPath `"$duckDnsConfig`""
    $principal = [string]$task.Principal.UserId
    if (-not $task.Settings.Enabled -or $actions.Count -ne 1 -or [System.IO.Path]::GetFileName([string]$actions[0].Execute) -ine 'powershell.exe' -or [string]$actions[0].Arguments -ine $expectedArguments -or $principal -notin @('SYSTEM', 'NT AUTHORITY\SYSTEM', 'S-1-5-18')) {
        throw 'Updated DuckDNS scheduled task does not match the expected SYSTEM updater; legacy configuration was retained.'
    }
    if ($task.State -eq 'Running') {
        Write-Warning 'Legacy DuckDNS configuration is protected and retained because the previous scheduled update is still running.'
        return
    }
    Remove-Item -LiteralPath $legacyDuckDnsConfig -Force
}

function Disable-DuckDns {
    $task = Get-ScheduledTask -TaskName $duckDnsTask -ErrorAction SilentlyContinue
    if ($null -ne $task) {
        if ($task.State -eq 'Running') {
            Stop-ScheduledTask -TaskName $duckDnsTask -ErrorAction Stop
            for ($attempt = 0; $attempt -lt 40; $attempt++) {
                Start-Sleep -Milliseconds 250
                $task = Get-ScheduledTask -TaskName $duckDnsTask -ErrorAction SilentlyContinue
                if ($null -eq $task -or $task.State -ne 'Running') { break }
            }
            if ($null -ne $task -and $task.State -eq 'Running') { throw 'DuckDNS updater did not stop; credentials were retained.' }
        }
        Unregister-ScheduledTask -TaskName $duckDnsTask -Confirm:$false -ErrorAction Stop
        if ($null -ne (Get-ScheduledTask -TaskName $duckDnsTask -ErrorAction SilentlyContinue)) {
            throw 'DuckDNS scheduled task remains registered; credentials were retained.'
        }
    }

    foreach ($configPath in @($duckDnsConfig, $legacyDuckDnsConfig)) {
        if (-not [System.IO.File]::Exists($configPath)) { continue }
        Assert-NoDuckDnsReparsePoints -Path $configPath
        $item = Get-Item -LiteralPath $configPath -Force
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'DuckDNS configuration cannot be a reparse point; credential cleanup stopped.' }
        try {
            Assert-ProtectedDuckDnsAcl -Path $configPath
        }
        catch {
            Set-ProtectedDuckDnsAcl -Path $configPath
        }
        Assert-ProtectedDuckDnsAcl -Path $configPath
        Remove-Item -LiteralPath $configPath -Force
    }
}

if ($PrepareDuckDnsTokenFile) {
    Assert-DuckDnsAdministrator
    Prepare-DuckDnsTokenFile
    Write-Host 'Protected DuckDNS token staging is ready.'
    exit 0
}

if ($DisableDuckDns) {
    Assert-DuckDnsAdministrator
    Disable-DuckDns
    Write-Host 'DuckDNS updater disabled and stored credentials removed.'
    exit 0
}

Assert-DuckDnsAdministrator

if ($HostName -notmatch '^(?=.{1,253}$)[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*$') {
    throw 'Invalid public hostname.'
}
if ($EmailAddress -notmatch '^[^\s@]+@[^\s@]+\.[^\s@]+$') {
    throw 'A valid ACME notification email is required.'
}

function Configure-DuckDns {
    if ([string]::IsNullOrWhiteSpace($DuckDnsSubdomain) -and [string]::IsNullOrWhiteSpace($DuckDnsTokenFile)) { Disable-DuckDns; return }
    if ([string]::IsNullOrWhiteSpace($DuckDnsTokenFile)) { throw 'Protected DuckDNS token file is required when DuckDNS is enabled.' }
    if ($DuckDnsSubdomain -notmatch '^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$') { throw 'Invalid DuckDNS subdomain.' }
    Ensure-ProtectedDuckDnsConfigRoot
    Assert-File $duckDnsScript 'Bundled DuckDNS updater'
    $DuckDnsToken = Read-DuckDnsTokenFile $DuckDnsTokenFile
    if ([string]::IsNullOrWhiteSpace($DuckDnsToken)) { throw 'DuckDNS token is required when DuckDNS is enabled.' }
    Protect-LegacyDuckDnsConfig
    $configTempFile = Join-Path $duckDnsConfigRoot ([System.IO.Path]::GetRandomFileName() + '.tmp')
    $configBackupFile = "$configTempFile.bak"
    try {
        $file = [System.IO.File]::Open($configTempFile, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        $file.Dispose()
        Assert-NoDuckDnsReparsePoints -Path $configTempFile
        Set-ProtectedDuckDnsAcl -Path $configTempFile
        $configJson = @{ subdomain = $DuckDnsSubdomain; token = $DuckDnsToken } | ConvertTo-Json -Depth 3 -Compress
        [System.IO.File]::WriteAllText($configTempFile, $configJson, [System.Text.UTF8Encoding]::new($false))
        if ([System.IO.File]::Exists($duckDnsConfig)) {
            Assert-NoDuckDnsReparsePoints -Path $duckDnsConfig
            Set-ProtectedDuckDnsAcl -Path $duckDnsConfig
            Assert-ProtectedDuckDnsAcl -Path $duckDnsConfig
            [System.IO.File]::Replace($configTempFile, $duckDnsConfig, $configBackupFile)
        }
        else {
            [System.IO.File]::Move($configTempFile, $duckDnsConfig)
        }
    }
    finally {
        Remove-Item -LiteralPath $configTempFile -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $configBackupFile -Force -ErrorAction SilentlyContinue
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $duckDnsScript -ConfigPath $duckDnsConfig
    if ($LASTEXITCODE -ne 0) { throw 'DuckDNS could not be updated.' }
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$duckDnsScript`" -ConfigPath `"$duckDnsConfig`""
    $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(5) -RepetitionInterval (New-TimeSpan -Minutes 5) -RepetitionDuration (New-TimeSpan -Days 3650)
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    Register-ScheduledTask -TaskName $duckDnsTask -Action $action -Trigger $trigger -Principal $principal -Force | Out-Null
    Remove-LegacyDuckDnsConfig
}

function Write-NginxConfig([bool]$TlsEnabled) {
    New-Item -ItemType Directory -Force -Path $acmeRoot, $certRoot, (Split-Path -Parent $nginxConfig) | Out-Null
    $acmePath = $acmeRoot.Replace('\', '/')
    $certificateChainPath = "$certPrefix-chain.pem".Replace('\', '/')
    $certificateKeyPath = "$certPrefix-key.pem".Replace('\', '/')
    $tlsServer = ''
    if ($TlsEnabled) {
        $tlsServer = @"

    server {
        listen 443 ssl;
        server_name $HostName;

        ssl_certificate "$certificateChainPath";
        ssl_certificate_key "$certificateKeyPath";
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

    $httpAction = if ($TlsEnabled) { "return 301 https://`$host`$request_uri;" } else { "proxy_pass http://127.0.0.1:$ServerPort;" }
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

    $configText = @"
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
            root "$acmePath";
            try_files `$uri =404;
        }

        location / {
            $httpAction
$httpProxy
        }
    }
$tlsServer
}
"@
    [System.IO.File]::WriteAllText($nginxCandidateConfig, $configText, [System.Text.UTF8Encoding]::new($false))
}

function Ensure-NginxService {
    Assert-File $nginxExe 'Bundled Nginx executable'
    Assert-File $nssmExe 'NSSM service wrapper'
    $status = & $nssmExe status $nginxService 2>$null
    if ($LASTEXITCODE -ne 0) {
        & $nssmExe install $nginxService $nginxExe | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Nginx service registration failed.' }
    }
    & $nssmExe set $nginxService AppDirectory $nginxRoot | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Nginx service directory configuration failed.' }
    & $nssmExe set $nginxService Application $nginxExe | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Nginx service executable configuration failed.' }
    & $nssmExe set $nginxService AppParameters "-p `"$nginxRoot`" -c conf/nginx.conf" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Nginx service arguments configuration failed.' }
    & $nssmExe set $nginxService DisplayName 'MulletaFlix HTTPS Proxy (Nginx)' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Nginx service display name configuration failed.' }
    & $nssmExe set $nginxService Start SERVICE_AUTO_START | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Nginx service startup mode configuration failed.' }
    $service = Get-Service -Name $nginxService -ErrorAction Stop
    if ($service.Status -ne [System.ServiceProcess.ServiceControllerStatus]::Running) {
        Start-Service -Name $nginxService -ErrorAction Stop
    }
    $service = Get-Service -Name $nginxService -ErrorAction Stop
    if ($service.Status -ne [System.ServiceProcess.ServiceControllerStatus]::Running) {
        throw 'Nginx service did not reach the running state.'
    }
}

function Ensure-FirewallRules {
    foreach ($port in 80, 443) {
        $name = "MulletaFlix Nginx HTTPS $port"
        if (-not (Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue)) {
            New-NetFirewallRule -DisplayName $name -Direction Inbound -Protocol TCP -LocalPort $port -Action Allow -Profile Any | Out-Null
        }
    }
}

function Test-NginxConfig([string]$ConfigPath = $nginxCandidateConfig) {
    & $nginxExe -p $nginxRoot -c $ConfigPath -t
    if ($LASTEXITCODE -ne 0) { throw 'Nginx configuration validation failed.' }
}

function Restore-PendingNginxConfig {
    if (-not [System.IO.File]::Exists($nginxRollbackConfig)) { return }
    if ([System.IO.File]::Exists($nginxConfig)) {
        [System.IO.File]::Delete($nginxConfig)
    }
    [System.IO.File]::Move($nginxRollbackConfig, $nginxConfig)
}

function Install-NginxConfig {
    if (-not [System.IO.File]::Exists($nginxCandidateConfig)) { throw 'Validated Nginx candidate configuration was not found.' }
    if ([System.IO.File]::Exists($nginxRollbackConfig)) { throw 'A pending Nginx rollback must be recovered before installing another configuration.' }
    Test-NginxConfig -ConfigPath $nginxCandidateConfig

    $hadPreviousConfig = [System.IO.File]::Exists($nginxConfig)
    if ($hadPreviousConfig) {
        [System.IO.File]::Replace($nginxCandidateConfig, $nginxConfig, $nginxRollbackConfig)
    }
    else {
        [System.IO.File]::Move($nginxCandidateConfig, $nginxConfig)
    }
    return [pscustomobject]@{ HadPreviousConfig = $hadPreviousConfig }
}

function Restore-NginxConfig($Transaction) {
    if ($null -eq $Transaction) { return }
    if ([System.IO.File]::Exists($nginxRollbackConfig)) {
        Restore-PendingNginxConfig
    }
    elseif (-not $Transaction.HadPreviousConfig -and [System.IO.File]::Exists($nginxConfig)) {
        [System.IO.File]::Delete($nginxConfig)
    }
}

function Complete-NginxConfig($Transaction) {
    if ($null -eq $Transaction) { return }
    if ([System.IO.File]::Exists($nginxRollbackConfig)) {
        [System.IO.File]::Delete($nginxRollbackConfig)
    }
}

function Write-ReloadScript {
    @"
`$ErrorActionPreference = 'Stop'
& '$nginxExe' -p '$nginxRoot' -c conf/nginx.conf -s reload
if (`$LASTEXITCODE -ne 0) { throw 'Nginx reload failed after certificate renewal.' }
"@ | Set-Content -LiteralPath $reloadScript -Encoding utf8
}

Restore-PendingNginxConfig
Configure-DuckDns
Ensure-FirewallRules
Write-ReloadScript
Write-NginxConfig $false
$httpConfigTransaction = Install-NginxConfig
try {
    Ensure-NginxService
    Complete-NginxConfig $httpConfigTransaction
}
catch {
    Restore-NginxConfig $httpConfigTransaction
    throw
}

if (-not $SkipCertificate) {
    Assert-File $wacsExe 'Bundled win-acme executable'
    if ([string]::IsNullOrWhiteSpace($EmailAddress)) { throw 'An ACME notification email is required.' }
    & $wacsExe --source manual --host $HostName --validation filesystem --webroot $acmeRoot --store pemfiles --pemfilespath $certRoot --pemfilesname $HostName --emailaddress $EmailAddress --accepttos --installation script --script $reloadScript
    if ($LASTEXITCODE -ne 0) { throw 'win-acme could not issue or configure the certificate.' }
    Assert-File "${certPrefix}-chain.pem" 'Issued certificate chain'
    Assert-File "${certPrefix}-key.pem" 'Issued certificate private key'
    Write-NginxConfig $true
    $tlsConfigTransaction = Install-NginxConfig
    try {
        & $nginxExe -p $nginxRoot -c conf/nginx.conf -s reload
        if ($LASTEXITCODE -ne 0) { throw 'Nginx could not reload with the issued certificate.' }
        Complete-NginxConfig $tlsConfigTransaction
    }
    catch {
        Restore-NginxConfig $tlsConfigTransaction
        throw
    }
}

Write-Host "HTTPS setup completed for $HostName" -ForegroundColor Green
