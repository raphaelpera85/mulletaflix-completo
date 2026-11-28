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

function Get-DuckDnsTaskSnapshot {
    $task = Get-ScheduledTask -TaskName $duckDnsTask -ErrorAction SilentlyContinue
    if ($null -eq $task) { return [pscustomobject]@{ Exists = $false; Xml = $null } }
    $xml = Export-ScheduledTask -TaskName $duckDnsTask -ErrorAction Stop
    if ([string]::IsNullOrWhiteSpace([string]$xml)) { throw 'Existing DuckDNS task could not be snapshotted; configuration was not changed.' }
    return [pscustomobject]@{ Exists = $true; Xml = [string]$xml }
}

function Restore-DuckDnsTaskSnapshot($Snapshot) {
    $current = Get-ScheduledTask -TaskName $duckDnsTask -ErrorAction SilentlyContinue
    if ($Snapshot.Exists) {
        Register-ScheduledTask -TaskName $duckDnsTask -Xml ([string]$Snapshot.Xml) -Force -ErrorAction Stop | Out-Null
        if ($null -eq (Get-ScheduledTask -TaskName $duckDnsTask -ErrorAction SilentlyContinue)) { throw 'Previous DuckDNS scheduled task did not return after rollback.' }
        return
    }
    if ($null -ne $current) {
        if ($current.State -eq 'Running') { Stop-ScheduledTask -TaskName $duckDnsTask -ErrorAction Stop }
        Unregister-ScheduledTask -TaskName $duckDnsTask -Confirm:$false -ErrorAction Stop
        if ($null -ne (Get-ScheduledTask -TaskName $duckDnsTask -ErrorAction SilentlyContinue)) { throw 'New DuckDNS scheduled task remained after rollback.' }
    }
}

function Assert-DuckDnsScheduledTask {
    $task = Get-ScheduledTask -TaskName $duckDnsTask -ErrorAction Stop
    $actions = @($task.Actions)
    $expectedArguments = "-NoProfile -ExecutionPolicy Bypass -File `"$duckDnsScript`" -ConfigPath `"$duckDnsConfig`""
    $principal = [string]$task.Principal.UserId
    if (-not $task.Settings.Enabled -or $actions.Count -ne 1 -or
        [System.IO.Path]::GetFileName([string]$actions[0].Execute) -ine 'powershell.exe' -or
        [string]$actions[0].Arguments -ine $expectedArguments -or
        $principal -notin @('SYSTEM', 'NT AUTHORITY\SYSTEM', 'S-1-5-18')) {
        throw 'DuckDNS scheduled task verification failed.'
    }
}

function Invoke-DuckDnsUpdater([string]$ConfigPath) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $duckDnsScript -ConfigPath $ConfigPath
    if ($LASTEXITCODE -ne 0) { throw 'DuckDNS could not be updated.' }
}

function Invoke-WacsCertificateSetup {
    param([string]$HostName, [string]$EmailAddress)

    & $wacsExe --source manual --host $HostName --validation filesystem --webroot $acmeRoot --store pemfiles --pemfilespath $certRoot --pemfilesname $HostName --emailaddress $EmailAddress --accepttos --installation script --script $reloadScript | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'win-acme could not issue or configure the certificate.' }
}

function Invoke-WacsTaskSetup {
    & $wacsExe --setuptaskscheduler | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'win-acme could not configure the scheduled renewal task.' }
}

function Assert-HttpsPrerequisites {
    param(
        [switch]$SkipCertificate,
        [string]$DuckDnsSubdomain,
        [string]$DuckDnsTokenFile
    )

    Assert-File $nginxExe 'Bundled Nginx executable'
    Assert-File $nssmExe 'NSSM service wrapper'
    if (-not $SkipCertificate) { Assert-File $wacsExe 'Bundled win-acme executable' }

    $hasSubdomain = -not [string]::IsNullOrWhiteSpace($DuckDnsSubdomain)
    $hasTokenFile = -not [string]::IsNullOrWhiteSpace($DuckDnsTokenFile)
    if ($hasSubdomain -ne $hasTokenFile) { throw 'DuckDNS subdomain and protected token file must be configured together.' }
    if ($hasSubdomain) {
        if ($DuckDnsSubdomain -notmatch '^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$') { throw 'Invalid DuckDNS subdomain.' }
        Assert-File $duckDnsScript 'Bundled DuckDNS updater'
        Assert-File $DuckDnsTokenFile 'Protected DuckDNS token file'
    }
    if ($ServerPort -lt 1 -or $ServerPort -gt 65535) { throw 'Server port must be between 1 and 65535.' }
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
function Configure-DuckDns {
    if ([string]::IsNullOrWhiteSpace($DuckDnsSubdomain) -and [string]::IsNullOrWhiteSpace($DuckDnsTokenFile)) { Disable-DuckDns; return }
    if ([string]::IsNullOrWhiteSpace($DuckDnsTokenFile)) { throw 'Protected DuckDNS token file is required when DuckDNS is enabled.' }
    if ($DuckDnsSubdomain -notmatch '^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$') { throw 'Invalid DuckDNS subdomain.' }
    Ensure-ProtectedDuckDnsConfigRoot
    Assert-File $duckDnsScript 'Bundled DuckDNS updater'
    $DuckDnsToken = Read-DuckDnsTokenFile $DuckDnsTokenFile
    if ([string]::IsNullOrWhiteSpace($DuckDnsToken)) { throw 'DuckDNS token is required when DuckDNS is enabled.' }
    Protect-LegacyDuckDnsConfig
    $taskSnapshot = Get-DuckDnsTaskSnapshot
    $configTempFile = Join-Path $duckDnsConfigRoot ([System.IO.Path]::GetRandomFileName() + '.tmp')
    $configBackupFile = "$configTempFile.bak"
    $failedConfigFile = "$configTempFile.failed"
    $hadPreviousConfig = [System.IO.File]::Exists($duckDnsConfig)
    $taskMutationAttempted = $false
    $committed = $false
    try {
        $file = [System.IO.File]::Open($configTempFile, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        $file.Dispose()
        Assert-NoDuckDnsReparsePoints -Path $configTempFile
        Set-ProtectedDuckDnsAcl -Path $configTempFile
        $configJson = @{ subdomain = $DuckDnsSubdomain; token = $DuckDnsToken } | ConvertTo-Json -Depth 3 -Compress
        [System.IO.File]::WriteAllText($configTempFile, $configJson, [System.Text.UTF8Encoding]::new($false))
        if ($hadPreviousConfig) {
            Assert-NoDuckDnsReparsePoints -Path $duckDnsConfig
            Set-ProtectedDuckDnsAcl -Path $duckDnsConfig
            Assert-ProtectedDuckDnsAcl -Path $duckDnsConfig
            [System.IO.File]::Replace($configTempFile, $duckDnsConfig, $configBackupFile)
        }
        else {
            [System.IO.File]::Move($configTempFile, $duckDnsConfig)
        }
        Invoke-DuckDnsUpdater -ConfigPath $duckDnsConfig
        $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$duckDnsScript`" -ConfigPath `"$duckDnsConfig`""
        $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(5) -RepetitionInterval (New-TimeSpan -Minutes 5) -RepetitionDuration (New-TimeSpan -Days 3650)
        $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
        $taskMutationAttempted = $true
        Register-ScheduledTask -TaskName $duckDnsTask -Action $action -Trigger $trigger -Principal $principal -Force -ErrorAction Stop | Out-Null
        Assert-DuckDnsScheduledTask
        Remove-LegacyDuckDnsConfig
        $committed = $true
    }
    catch {
        $failure = $_
        $rollbackErrors = [System.Collections.Generic.List[string]]::new()
        try {
            if ([System.IO.File]::Exists($configBackupFile)) {
                if ([System.IO.File]::Exists($duckDnsConfig)) {
                    [System.IO.File]::Replace($configBackupFile, $duckDnsConfig, $failedConfigFile)
                    Remove-Item -LiteralPath $failedConfigFile -Force -ErrorAction SilentlyContinue
                }
                else {
                    [System.IO.File]::Move($configBackupFile, $duckDnsConfig)
                }
            }
            elseif (-not $hadPreviousConfig -and [System.IO.File]::Exists($duckDnsConfig)) {
                Assert-NoDuckDnsReparsePoints -Path $duckDnsConfig
                [System.IO.File]::Delete($duckDnsConfig)
            }
        }
        catch { $rollbackErrors.Add('credential configuration could not be restored') }
        if ($taskMutationAttempted) {
            try { Restore-DuckDnsTaskSnapshot -Snapshot $taskSnapshot }
            catch { $rollbackErrors.Add('scheduled task could not be restored') }
        }
        if ($rollbackErrors.Count -gt 0) {
            $recoveryDetail = if ([System.IO.File]::Exists($configBackupFile)) { " Protected recovery copy retained at '$configBackupFile'." } else { '' }
            throw "DuckDNS setup failed and rollback was incomplete: $($rollbackErrors -join '; ').$recoveryDetail Original failure: $($failure.Exception.Message)"
        }
        throw $failure
    }
    finally {
        Remove-Item -LiteralPath $configTempFile -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $failedConfigFile -Force -ErrorAction SilentlyContinue
        if ($committed) { Remove-Item -LiteralPath $configBackupFile -Force -ErrorAction SilentlyContinue }
    }
}

function Write-NginxConfig([bool]$TlsEnabled, [bool]$AcmeBootstrapOnly = $false) {
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

    $httpAction = if ($TlsEnabled) { "return 301 https://`$host`$request_uri;" } elseif ($AcmeBootstrapOnly) { 'return 503;' } else { "proxy_pass http://127.0.0.1:$ServerPort;" }
    $httpProxy = if ($TlsEnabled -or $AcmeBootstrapOnly) { '' } else { @"
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

function Invoke-Nssm {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $output = @(& $nssmExe @Arguments 2>$null)
    $exitCode = 0
    if ($null -ne $LASTEXITCODE) { $exitCode = [int]$LASTEXITCODE }
    $nullCharacter = [string][char]0
    $text = (($output | ForEach-Object { [string]$_ }) -join [Environment]::NewLine).Replace($nullCharacter, [string]::Empty).Trim()
    return [pscustomobject]@{ ExitCode = $exitCode; Output = $text }
}

function Get-NssmServiceSettings {
    $settings = [ordered]@{}
    foreach ($name in @('Application', 'AppDirectory', 'AppParameters', 'DisplayName', 'Start')) {
        $result = Invoke-Nssm -Arguments @('get', $nginxService, $name)
        if ($result.ExitCode -ne 0) { throw "Could not snapshot Nginx NSSM setting '$name'." }
        $settings[$name] = ([string]$result.Output).Replace([string][char]0, [string]::Empty).Trim()
    }
    return $settings
}

function Set-NssmServiceSettings {
    param([Parameter(Mandatory = $true)][System.Collections.IDictionary]$Settings)

    foreach ($name in @('Application', 'AppDirectory', 'AppParameters', 'DisplayName', 'Start')) {
        $result = Invoke-Nssm -Arguments @('set', $nginxService, $name, [string]$Settings[$name])
        if ($result.ExitCode -ne 0) { throw "Could not set Nginx NSSM setting '$name'." }
    }
}

function Ensure-FirewallRules {
    $existingRuleNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $createdRules = [System.Collections.Generic.List[object]]::new()
    $allAllows = @(Get-NetFirewallRule -Enabled True -Direction Inbound -Action Allow -ErrorAction SilentlyContinue)
    foreach ($rule in $allAllows) {
        if ($rule.Name) { [void]$existingRuleNames.Add([string]$rule.Name) }
    }

    try {
        foreach ($port in 80, 443) {
            $name = "MulletaFlix Nginx HTTPS $port"
            $rules = @(Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue)
            $hasCorrectRule = $false
            foreach ($rule in $rules) {
                $application = $rule | Get-NetFirewallApplicationFilter
                $portFilter = $rule | Get-NetFirewallPortFilter
                $addressFilter = $rule | Get-NetFirewallAddressFilter
                $isCorrect = $rule.Enabled -eq 'True' -and $rule.Direction -eq 'Inbound' -and $rule.Action -eq 'Allow' -and
                    [string]::Equals([string]$application.Program, $nginxExe, [System.StringComparison]::OrdinalIgnoreCase) -and
                    $rule.Profile -eq 'Any' -and $portFilter.Protocol -eq 'TCP' -and [string]$portFilter.LocalPort -eq [string]$port -and
                    [string]$addressFilter.LocalAddress -eq 'Any' -and [string]$addressFilter.RemoteAddress -eq 'Any'
                if ($isCorrect) { $hasCorrectRule = $true; break }
            }

            if (-not $hasCorrectRule) {
                $created = New-NetFirewallRule -DisplayName $name -Program $nginxExe -Direction Inbound -Protocol TCP -LocalPort $port -LocalAddress Any -RemoteAddress Any -Action Allow -Profile Any -PassThru
                if ($null -eq $created -or -not $created.Name) { throw "Could not track staged Nginx firewall rule for TCP $port." }
                if ($existingRuleNames.Contains([string]$created.Name)) { throw "Nginx firewall rule identifier was not unique for TCP $port." }
                $createdRules.Add($created)
            }
        }
    }
    catch {
        Remove-StagedFirewallRules -Transaction ([pscustomobject]@{ CreatedRules = @($createdRules); ExistingRuleNames = $existingRuleNames })
        throw
    }

    return [pscustomobject]@{ CreatedRules = @($createdRules); ExistingRuleNames = $existingRuleNames }
}

function Remove-StagedFirewallRules {
    param([Parameter(Mandatory = $true)]$Transaction)

    foreach ($rule in @($Transaction.CreatedRules)) {
        if ($null -eq $rule -or -not $rule.Name) { continue }
        if ($Transaction.ExistingRuleNames.Contains([string]$rule.Name)) { continue }
        try { Remove-NetFirewallRule -InputObject $rule -ErrorAction Stop }
        catch { Write-Warning 'A staged MulletaFlix Nginx firewall rule could not be removed during rollback.' }
    }
}

function Remove-ObsoleteNginxFirewallRules {
    param([string]$PreviousExecutablePath)

    $ownedRulePattern = '^MulletaFlix Nginx HTTPS\s+(80|443)$'
    try {
        $allAllows = @(Get-NetFirewallRule -Enabled True -Direction Inbound -Action Allow -ErrorAction Stop)
        foreach ($rule in $allAllows) {
            $isNamedManagedRule = [string]$rule.DisplayName -match $ownedRulePattern
            $isGenericNginxRule = [string]::Equals([string]$rule.DisplayName, 'nginx', [System.StringComparison]::OrdinalIgnoreCase)
            if (-not $isNamedManagedRule -and -not $isGenericNginxRule) { continue }
            $application = $rule | Get-NetFirewallApplicationFilter
            $portFilter = $rule | Get-NetFirewallPortFilter
            $addressFilter = $rule | Get-NetFirewallAddressFilter
            $isCurrentExecutable = [string]::Equals([string]$application.Program, $nginxExe, [System.StringComparison]::OrdinalIgnoreCase)
            $isPreviousExecutable = -not [string]::IsNullOrWhiteSpace($PreviousExecutablePath) -and
                -not [string]::Equals([string]$PreviousExecutablePath, $nginxExe, [System.StringComparison]::OrdinalIgnoreCase) -and
                [string]::Equals([string]$application.Program, $PreviousExecutablePath, [System.StringComparison]::OrdinalIgnoreCase)
            if (-not $isCurrentExecutable -and -not $isPreviousExecutable) { continue }

            # Windows may retain generic nginx allow rules from the app prompt. Only clean
            # wildcard-port/protocol rules for the exact Nginx executable validated above;
            # specific-port generic rules and every unrelated executable remain untouched.
            if ($isGenericNginxRule) {
                $isBroadRule = [string]$portFilter.LocalPort -eq 'Any' -or [string]$portFilter.Protocol -eq 'Any'
                if ($isBroadRule) {
                    try { Remove-NetFirewallRule -InputObject $rule -ErrorAction Stop }
                    catch { Write-Warning 'A broad generic Nginx firewall rule could not be removed after service health verification.' }
                }
                continue
            }

            $isStrictWebRule = $rule.Profile -eq 'Any' -and $portFilter.Protocol -eq 'TCP' -and
                [string]$portFilter.LocalPort -in @('80', '443') -and [string]$addressFilter.LocalAddress -eq 'Any' -and
                [string]$addressFilter.RemoteAddress -eq 'Any'

            if ($isPreviousExecutable -or ($isCurrentExecutable -and -not $isStrictWebRule)) {
                try { Remove-NetFirewallRule -InputObject $rule -ErrorAction Stop }
                catch { Write-Warning 'An obsolete MulletaFlix Nginx firewall rule could not be removed after service health verification.' }
            }
        }
    }
    catch { Write-Warning 'MulletaFlix Nginx firewall cleanup could not be completed; existing rules were left in place.' }
}

function Restore-NginxServiceTransaction {
    param([Parameter(Mandatory = $true)]$Transaction)

    $errors = [System.Collections.Generic.List[string]]::new()
    if ($Transaction.ServiceExisted) {
        try {
            $service = Get-Service -Name $nginxService -ErrorAction SilentlyContinue
            if ($null -ne $service -and [string]$service.Status -ne 'Stopped') {
                Stop-Service -Name $nginxService -ErrorAction Stop
            }
        }
        catch { $errors.Add('could not stop the partially migrated service') }

        foreach ($name in @('Application', 'AppDirectory', 'AppParameters', 'DisplayName', 'Start')) {
            try {
                $result = Invoke-Nssm -Arguments @('set', $nginxService, $name, [string]$Transaction.PreviousSettings[$name])
                if ($result.ExitCode -ne 0) { $errors.Add("could not restore previous NSSM setting '$name'") }
            }
            catch { $errors.Add("could not restore previous NSSM setting '$name'") }
        }

        try {
            $service = Get-Service -Name $nginxService -ErrorAction SilentlyContinue
            if ($Transaction.WasRunning) {
                if ($null -ne $service -and [string]$service.Status -eq 'Running') {
                    Restart-Service -Name $nginxService -Force -ErrorAction Stop
                }
                else {
                    Start-Service -Name $nginxService -ErrorAction Stop
                }
            }
            elseif ($null -ne $service -and [string]$service.Status -ne 'Stopped') {
                Stop-Service -Name $nginxService -ErrorAction Stop
            }
        }
        catch { $errors.Add('could not restore the original service state') }
    }
    elseif ($Transaction.ServiceMutationStarted) {
        try {
            $service = Get-Service -Name $nginxService -ErrorAction SilentlyContinue
            if ($null -ne $service) {
                Stop-Service -Name $nginxService -ErrorAction Stop
                $result = Invoke-Nssm -Arguments @('remove', $nginxService, 'confirm')
                if ($result.ExitCode -ne 0) { $errors.Add('could not remove the newly registered service') }
            }
        }
        catch { $errors.Add('could not remove the newly registered service') }
    }

    if ($errors.Count -gt 0) { return ($errors -join '; ') }
    return ''
}

function Ensure-NginxService {
    param($NginxConfigTransaction)

    $transaction = [pscustomobject]@{
        ServiceExisted = $false
        WasRunning = $false
        PreviousSettings = $null
        ServiceMutationStarted = $false
        Firewall = $null
    }

    try {
        Assert-File $nginxExe 'Bundled Nginx executable'
        Assert-File $nssmExe 'NSSM service wrapper'

        $originalService = Get-Service -Name $nginxService -ErrorAction SilentlyContinue
        $transaction.ServiceExisted = $null -ne $originalService
        if ($transaction.ServiceExisted) {
            $transaction.WasRunning = [string]$originalService.Status -eq 'Running'
            $transaction.PreviousSettings = Get-NssmServiceSettings
        }

        $transaction.Firewall = Ensure-FirewallRules
        $transaction.ServiceMutationStarted = $true

        if (-not $transaction.ServiceExisted) {
            $install = Invoke-Nssm -Arguments @('install', $nginxService, $nginxExe)
            if ($install.ExitCode -ne 0) { throw 'Nginx service registration failed.' }
        }

        $newSettings = [ordered]@{
            Application = $nginxExe
            AppDirectory = $nginxRoot
            AppParameters = "-p `"$nginxRoot`" -c conf/nginx.conf"
            DisplayName = 'MulletaFlix HTTPS Proxy (Nginx)'
            Start = 'SERVICE_AUTO_START'
        }
        Set-NssmServiceSettings -Settings $newSettings

        $configured = Invoke-Nssm -Arguments @('get', $nginxService, 'Application')
        $nullCharacter = [string][char]0
        $configuredApplication = ([string]$configured.Output).Replace($nullCharacter, [string]::Empty).Trim()
        if ($configured.ExitCode -ne 0 -or -not [string]::Equals([System.IO.Path]::GetFullPath($configuredApplication), [System.IO.Path]::GetFullPath($nginxExe), [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'Nginx service is not configured to use the bundled executable.'
        }

        if ($transaction.WasRunning) {
            Restart-Service -Name $nginxService -Force -ErrorAction Stop
        }
        else {
            Start-Service -Name $nginxService -ErrorAction Stop
        }

        $timeoutSeconds = 20
        if ($null -ne $nginxStartupTimeoutSeconds) { $timeoutSeconds = [Math]::Max(0, [int]$nginxStartupTimeoutSeconds) }
        $deadline = [DateTime]::UtcNow.AddSeconds($timeoutSeconds)
        do {
            $service = Get-Service -Name $nginxService -ErrorAction Stop
            $nginxProcesses = @(Get-CimInstance Win32_Process -Filter "Name='nginx.exe'" -ErrorAction SilentlyContinue | Where-Object {
                $_.ExecutablePath -and [string]::Equals([System.IO.Path]::GetFullPath([string]$_.ExecutablePath), [System.IO.Path]::GetFullPath($nginxExe), [System.StringComparison]::OrdinalIgnoreCase)
            })
            if ([string]$service.Status -eq 'Running' -and $nginxProcesses.Count -gt 0) { break }
            if ($timeoutSeconds -eq 0) { break }
            Start-Sleep -Milliseconds 500
        } while ([DateTime]::UtcNow -lt $deadline)

        if ([string]$service.Status -ne 'Running' -or $nginxProcesses.Count -eq 0) {
            throw 'Nginx service did not start from the bundled executable.'
        }

        if ($null -ne $NginxConfigTransaction) { Complete-NginxConfig $NginxConfigTransaction }
        $previousPath = if ($transaction.ServiceExisted) { [string]$transaction.PreviousSettings.Application } else { '' }
        Remove-ObsoleteNginxFirewallRules -PreviousExecutablePath $previousPath
        return $transaction
    }
    catch {
        $failure = $_
        $rollbackProblems = [System.Collections.Generic.List[string]]::new()

        if ($null -ne $NginxConfigTransaction) {
            try { Restore-NginxConfig $NginxConfigTransaction }
            catch { $rollbackProblems.Add('could not restore the previous Nginx configuration') }
        }

        if ($transaction.ServiceMutationStarted) {
            $serviceRollbackError = Restore-NginxServiceTransaction -Transaction $transaction
            if (-not [string]::IsNullOrWhiteSpace($serviceRollbackError)) { $rollbackProblems.Add($serviceRollbackError) }
        }

        if ($null -ne $transaction.Firewall) { Remove-StagedFirewallRules -Transaction $transaction.Firewall }
        if ($rollbackProblems.Count -gt 0) {
            throw "Nginx migration failed and rollback was incomplete: $($rollbackProblems -join '; '). Original failure: $($failure.Exception.Message)"
        }
        throw $failure
    }
}

function Test-CanPreserveExistingTls {
    param(
        [string]$HostName,
        [switch]$SkipCertificate,
        [string]$CertificateChainPath,
        [string]$PrivateKeyPath
    )

    return -not $SkipCertificate -and
        -not [string]::IsNullOrWhiteSpace($HostName) -and
        [System.IO.File]::Exists($CertificateChainPath) -and
        [System.IO.File]::Exists($PrivateKeyPath)
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

function ConvertTo-PowerShellSingleQuotedLiteral([string]$Value) {
    return "'" + $Value.Replace("'", "''") + "'"
}

function Write-ReloadScript {
    $nginxExeLiteral = ConvertTo-PowerShellSingleQuotedLiteral $nginxExe
    $nginxRootLiteral = ConvertTo-PowerShellSingleQuotedLiteral $nginxRoot

    @"
`$ErrorActionPreference = 'Stop'
& $nginxExeLiteral -p $nginxRootLiteral -c conf/nginx.conf -s reload
if (`$LASTEXITCODE -ne 0) { throw 'Nginx reload failed after certificate renewal.' }
"@ | Set-Content -LiteralPath $reloadScript -Encoding utf8
}

function Invoke-HttpsSetup {
    param(
        [Parameter(Mandatory = $true)][string]$InstallRoot,
        [Parameter(Mandatory = $true)][string]$HostName,
        [string]$EmailAddress,
        [string]$DuckDnsSubdomain,
        [string]$DuckDnsTokenFile,
        [int]$ServerPort = 8096,
        [switch]$SkipCertificate
    )

    # Perform all read-only dependency and argument checks before recovery, credential,
    # firewall, Nginx, service, or certificate operations can mutate machine state.
    Assert-HttpsPrerequisites -SkipCertificate:$SkipCertificate -DuckDnsSubdomain $DuckDnsSubdomain -DuckDnsTokenFile $DuckDnsTokenFile
    if (-not $SkipCertificate -and $EmailAddress -notmatch '^[^\s@]+@[^\s@]+\.[^\s@]+$') { throw 'A valid ACME notification email is required.' }

    Restore-PendingNginxConfig
    Configure-DuckDns
    Write-ReloadScript
    $preserveExistingTls = Test-CanPreserveExistingTls -HostName $HostName -SkipCertificate:$SkipCertificate -CertificateChainPath "${certPrefix}-chain.pem" -PrivateKeyPath "${certPrefix}-key.pem"
    $acmeBootstrapOnly = -not $preserveExistingTls -and -not $SkipCertificate
    Write-NginxConfig $preserveExistingTls $acmeBootstrapOnly
    $httpConfigTransaction = Install-NginxConfig
    $null = Ensure-NginxService -NginxConfigTransaction $httpConfigTransaction

    if (-not $SkipCertificate) {
        Invoke-WacsCertificateSetup -HostName $HostName -EmailAddress $EmailAddress
        Invoke-WacsTaskSetup
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
}

Invoke-HttpsSetup -InstallRoot $InstallRoot -HostName $HostName -EmailAddress $EmailAddress -DuckDnsSubdomain $DuckDnsSubdomain -DuckDnsTokenFile $DuckDnsTokenFile -ServerPort $ServerPort -SkipCertificate:$SkipCertificate
