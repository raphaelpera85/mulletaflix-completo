$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot '..\..\MulletaFlix-ux-custom\nsis\apply-update.ps1'
$scriptPath = [System.IO.Path]::GetFullPath($scriptPath)
$registryPath = 'HKCU:\Software\MulletaFlix\UpdateVersionMetadataTests\' + [guid]::NewGuid().ToString('N')
$tempRoot = Join-Path $env:TEMP ('mflx-update-version-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
New-Item -Path $registryPath -Force | Out-Null
Set-ItemProperty -Path $registryPath -Name DisplayName -Value 'MulletaFlix Server 12.1.5'
Set-ItemProperty -Path $registryPath -Name DisplayVersion -Value '12.1.5'

try {
    $scriptText = Get-Content -LiteralPath $scriptPath -Raw
    if ($scriptText -match '\$TestOnly|TestOnly|UninstallRegistryPath') {
        throw 'The packaged updater must not expose registry override or test-only parameters.'
    }

    $tokens = $null
    $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count -gt 0) { throw 'The packaged updater did not parse.' }
    $functionNames = @('ConvertTo-DisplayVersion', 'Set-InstalledVersionMetadata', 'Sync-InstalledVersionMetadata', 'Assert-UpdateVersion', 'Copy-UpdateToStage', 'Copy-InstalledFilesToStage', 'Move-StageIntoInstall', 'Restore-PreviousInstall', 'Get-LocalLivenessUri', 'Get-ServerLivenessResponse', 'Wait-ForServerLiveness', 'Get-InstalledProcesses', 'Stop-InstalledProcesses')
    $functionDefinitions = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $functionNames -contains $node.Name }, $true))
    if ($functionDefinitions.Count -ne $functionNames.Count) { throw 'Version helper functions could not be isolated for tests.' }
    . ([scriptblock]::Create(($functionDefinitions | ForEach-Object { $_.Extent.Text }) -join [Environment]::NewLine))

    Set-InstalledVersionMetadata -TargetVersion '12.1.58.0' -RegistryPath $registryPath | Out-Null
    $metadata = Get-ItemProperty -Path $registryPath
    if ($metadata.DisplayVersion -ne '12.1.58' -or $metadata.DisplayName -ne 'MulletaFlix Server 12.1.58') {
        throw 'Installed version metadata was not synchronized to the verified release version.'
    }

    $rejected = $false
    try { Set-InstalledVersionMetadata -TargetVersion 'not-a-version' -RegistryPath $registryPath }
    catch { $rejected = $true }
    if (-not $rejected) { throw 'Malformed target versions must not be written to the uninstall registry.' }
    $metadata = Get-ItemProperty -Path $registryPath
    if ($metadata.DisplayVersion -ne '12.1.58' -or $metadata.DisplayName -ne 'MulletaFlix Server 12.1.58') {
        throw 'Rejected version metadata changed the existing uninstall registry values.'
    }

    Set-Content -LiteralPath (Join-Path $tempRoot 'MulletaFlix.dll') -Value 'not a versioned binary' -Encoding ascii
    $syncRejected = $false
    try { Sync-InstalledVersionMetadata -InstallDirectory $tempRoot -TargetVersion '12.1.59' -RegistryPath $registryPath }
    catch { $syncRejected = $true }
    if (-not $syncRejected) { throw 'An installed assembly without matching ProductVersion must not update registry metadata.' }
    $metadata = Get-ItemProperty -Path $registryPath
    if ($metadata.DisplayVersion -ne '12.1.58') { throw 'A failed assembly version check changed the uninstall registry.' }

    $stageCopy = $scriptText.IndexOf('Copy-UpdateToStage -SourceDirectory $UpdateSourceDirectory -StageDirectory $stageDirectory', [System.StringComparison]::Ordinal)
    $stageValidation = $scriptText.IndexOf('Assert-UpdateVersion -InstallDirectory $stageDirectory -TargetVersion $TargetVersion', [System.StringComparison]::Ordinal)
    $serviceStop = $scriptText.IndexOf('Stop-Service -Name "MulletaFlixServer"', [System.StringComparison]::Ordinal)
    $stageAssembly = $scriptText.IndexOf('Copy-InstalledFilesToStage -InstallDirectory $InstallDirectory -StageDirectory $assembledDirectory', [System.StringComparison]::Ordinal)
    $assembledValidation = $scriptText.IndexOf('Assert-UpdateVersion -InstallDirectory $assembledDirectory -TargetVersion $TargetVersion', [System.StringComparison]::Ordinal)
    $installSwap = $scriptText.IndexOf('Move-StageIntoInstall -InstallDirectory $InstallDirectory -StageDirectory $assembledDirectory', [System.StringComparison]::Ordinal)
    if ($stageCopy -lt 0 -or $stageValidation -le $stageCopy -or $serviceStop -le $stageValidation -or $stageAssembly -le $serviceStop -or $assembledValidation -le $stageAssembly -or $installSwap -le $assembledValidation) {
        throw 'The complete update must be staged and version-validated before stopping the installed service.'
    }
    if (-not $scriptText.Contains('Restore-PreviousInstall') -or -not $scriptText.Contains('the installed server remains active')) {
        throw 'Update failure must retain or restore the previous installation and keep the service recoverable.'
    }
    $targetExitCheck = $scriptText.IndexOf('Target server process $ProcessId did not exit within 30 seconds', [System.StringComparison]::Ordinal)
    $healthCheck = $scriptText.IndexOf('Wait-ForServerLiveness -LivenessUri $livenessUri', [System.StringComparison]::Ordinal)
    $backupCleanup = $scriptText.IndexOf('Remove-Item -LiteralPath $backupDirectory -Recurse -Force', [System.StringComparison]::Ordinal)
    if ($targetExitCheck -lt 0 -or $healthCheck -lt 0 -or $backupCleanup -le $healthCheck -or $scriptText.Contains('Get-Process -Name $procName')) {
        throw 'Updater must stop safely on a live target PID, scope process cleanup, and retain backup until HTTP health passes.'
    }

    $serviceStart = $scriptText.IndexOf('Start-Service -Name ''MulletaFlixServer''', [System.StringComparison]::Ordinal)
    $serverStart = $scriptText.IndexOf('Start-Process -FilePath $exePath', [System.StringComparison]::Ordinal)
    $trayStart = $scriptText.IndexOf('Start-Process -FilePath $trayPath', [System.StringComparison]::Ordinal)
    if ($serviceStart -lt 0 -or $serverStart -lt 0 -or $trayStart -lt 0) {
        throw 'Service, server, and tray restart actions must remain available after the directory swap.'
    }
    if (-not $scriptText.Contains('Start-Service -Name ''MulletaFlixServer'' -ErrorAction Stop') -or
        -not $scriptText.Contains('$runningService.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Running, [TimeSpan]::FromSeconds(30))') -or
        $scriptText.Contains('Start-Service -Name ''MulletaFlixServer'' -ErrorAction SilentlyContinue')) {
        throw 'A service start failure must be visible and the updater must wait for Running.'
    }
    if (-not $scriptText.Contains('$serverProcess = Start-Process -FilePath $exePath -ArgumentList $arguments -WorkingDirectory $InstallDirectory -WindowStyle Hidden -PassThru -ErrorAction Stop') -or
        -not $scriptText.Contains('if ($serverProcess.HasExited)') -or
        -not $scriptText.Contains('$null = Start-Process -FilePath $trayPath -WorkingDirectory (Join-Path $InstallDirectory ''mulletaflix-windows-tray'') -PassThru -ErrorAction Stop')) {
        throw 'Tray-mode server and tray process start failures must be observable.'
    }

    $transactionRoot = Join-Path $tempRoot 'transaction'
    $installPath = Join-Path $transactionRoot 'install'
    $stagePath = Join-Path $transactionRoot 'stage'
    $backupPath = Join-Path $transactionRoot 'backup'
    $failedPath = Join-Path $transactionRoot 'failed'
    New-Item -ItemType Directory -Path $installPath -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $installPath 'version.txt') -Value 'previous'
    Set-Content -LiteralPath (Join-Path $installPath 'Uninstall.exe') -Value 'managed installer'

    $stageFailureObserved = $false
    try { Copy-UpdateToStage -SourceDirectory (Join-Path $transactionRoot 'missing-payload') -StageDirectory (Join-Path $transactionRoot 'failed-stage') }
    catch { $stageFailureObserved = $true }
    if (-not $stageFailureObserved -or (Get-Content -LiteralPath (Join-Path $installPath 'version.txt') -Raw).Trim() -ne 'previous') {
        throw 'A failed staging copy changed the active installation.'
    }

    New-Item -ItemType Directory -Path $stagePath -Force | Out-Null
    $updatePayloadPath = Join-Path $transactionRoot 'payload'
    New-Item -ItemType Directory -Path $updatePayloadPath -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $updatePayloadPath 'version.txt') -Value 'candidate'
    Copy-InstalledFilesToStage -InstallDirectory $installPath -StageDirectory $stagePath -UpdateStageDirectory $updatePayloadPath
    if ((Get-Content -LiteralPath (Join-Path $stagePath 'Uninstall.exe') -Raw).Trim() -ne 'managed installer' -or
        (Get-Content -LiteralPath (Join-Path $stagePath 'version.txt') -Raw).Trim() -ne 'candidate') {
        throw 'Stage assembly did not preserve installer-owned files while overlaying the update payload.'
    }
    Move-StageIntoInstall -InstallDirectory $installPath -StageDirectory $stagePath -BackupDirectory $backupPath
    if ((Get-Content -LiteralPath (Join-Path $installPath 'version.txt') -Raw).Trim() -ne 'candidate' -or
        (Get-Content -LiteralPath (Join-Path $backupPath 'version.txt') -Raw).Trim() -ne 'previous') {
        throw 'The directory swap did not keep both the candidate and previous installation intact.'
    }

    Restore-PreviousInstall -InstallDirectory $installPath -BackupDirectory $backupPath -FailedDirectory $failedPath
    if ((Get-Content -LiteralPath (Join-Path $installPath 'version.txt') -Raw).Trim() -ne 'previous' -or
        (Get-Content -LiteralPath (Join-Path $failedPath 'version.txt') -Raw).Trim() -ne 'candidate') {
        throw 'Rollback did not restore the previous installation and preserve the failed candidate.'
    }

    $swapFailureBackup = Join-Path $transactionRoot 'swap-failure-backup'
    $swapFailureObserved = $false
    try { Move-StageIntoInstall -InstallDirectory $installPath -StageDirectory (Join-Path $transactionRoot 'missing-stage') -BackupDirectory $swapFailureBackup }
    catch { $swapFailureObserved = $true }
    if (-not $swapFailureObserved -or (Get-Content -LiteralPath (Join-Path $installPath 'version.txt') -Raw).Trim() -ne 'previous' -or
        (Test-Path -LiteralPath $swapFailureBackup)) {
        throw 'A failed directory swap did not restore the active installation automatically.'
    }

    $networkConfigDirectory = Join-Path $transactionRoot 'config'
    New-Item -ItemType Directory -Path $networkConfigDirectory -Force | Out-Null
    $networkConfigurationPath = Join-Path $networkConfigDirectory 'network.xml'
    Set-Content -LiteralPath $networkConfigurationPath -Value '<NetworkConfiguration><InternalHttpPort>12345</InternalHttpPort><LocalNetworkAddresses><string>127.0.0.1/32</string></LocalNetworkAddresses><BaseUrl>/mflix/</BaseUrl></NetworkConfiguration>'
    $livenessUris = @(Get-LocalLivenessUri -DataDirectory $transactionRoot)
    if ($livenessUris.Count -ne 1 -or $livenessUris[0] -ne 'http://127.0.0.1:12345/mflix/System/Ping') { throw "Liveness URI ignored the configured bind address, current HTTP port, or base URL: $($livenessUris -join ', ')" }

    $activeInterface = [System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() | Where-Object {
        $_.OperationalStatus -eq [System.Net.NetworkInformation.OperationalStatus]::Up -and
        @($_.GetIPProperties().UnicastAddresses | Where-Object { $_.Address.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork }).Count -gt 0 -and
        @($_.GetIPProperties().UnicastAddresses | Where-Object { $_.Address.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetworkV6 }).Count -gt 0
    } | Select-Object -First 1
    if ($null -eq $activeInterface) { throw 'The Windows updater tests require an active interface with IPv4 and IPv6 addresses.' }
    $expectedAdapterAddress = $activeInterface.GetIPProperties().UnicastAddresses | Where-Object {
        $_.Address.AddressFamily -in @([System.Net.Sockets.AddressFamily]::InterNetwork, [System.Net.Sockets.AddressFamily]::InterNetworkV6)
    } | Select-Object -First 1
    $escapedAdapterName = [System.Security.SecurityElement]::Escape($activeInterface.Name)
    Set-Content -LiteralPath $networkConfigurationPath -Value "<NetworkConfiguration><InternalHttpPort>12345</InternalHttpPort><EnableIPv4>true</EnableIPv4><EnableIPv6>true</EnableIPv6><LocalNetworkAddresses><string>$escapedAdapterName</string></LocalNetworkAddresses></NetworkConfiguration>"
    $livenessUris = @(Get-LocalLivenessUri -DataDirectory $transactionRoot)
    $actualAdapterUri = if ($livenessUris.Count -eq 1) { [System.Uri]$livenessUris[0] } else { $null }
    $actualAdapterAddress = $null
    if ($null -eq $actualAdapterUri -or $actualAdapterUri.Port -ne 12345 -or $actualAdapterUri.AbsolutePath -ne '/System/Ping' -or
        -not [System.Net.IPAddress]::TryParse($actualAdapterUri.DnsSafeHost, [ref]$actualAdapterAddress) -or
        -not $actualAdapterAddress.Equals($expectedAdapterAddress.Address)) {
        throw "Liveness URI did not mirror NetworkManager's first active address for adapter '$($activeInterface.Name)': $($livenessUris -join ', ')"
    }
    $virtualPrefix = if ($activeInterface.Name.Length -gt 4) { $activeInterface.Name.Substring(0, 4) } else { $activeInterface.Name }
    $escapedVirtualPrefix = [System.Security.SecurityElement]::Escape($virtualPrefix)
    Set-Content -LiteralPath $networkConfigurationPath -Value "<NetworkConfiguration><InternalHttpPort>12345</InternalHttpPort><EnableIPv4>true</EnableIPv4><EnableIPv6>true</EnableIPv6><IgnoreVirtualInterfaces>true</IgnoreVirtualInterfaces><VirtualInterfaceNames><string>$escapedVirtualPrefix</string></VirtualInterfaceNames><LocalNetworkAddresses><string>$escapedAdapterName</string></LocalNetworkAddresses></NetworkConfiguration>"
    $ignoredAdapterUris = @(Get-LocalLivenessUri -DataDirectory $transactionRoot)
    if ($ignoredAdapterUris[0] -ne 'http://127.0.0.1:12345/System/Ping') { throw "Liveness probe did not mirror NetworkManager's fallback after excluding a virtual adapter: $($ignoredAdapterUris -join ', ')" }

    Set-Content -LiteralPath $networkConfigurationPath -Value '<NetworkConfiguration><HttpServerPortNumber>23456</HttpServerPortNumber></NetworkConfiguration>'
    $livenessUris = @(Get-LocalLivenessUri -DataDirectory $transactionRoot)
    if ($livenessUris[0] -ne 'http://127.0.0.1:23456/System/Ping') { throw "Liveness URI ignored the legacy HTTP port: $($livenessUris -join ', ')" }
    Remove-Item -LiteralPath $networkConfigurationPath -Force
    $livenessUris = @(Get-LocalLivenessUri -DataDirectory $transactionRoot)
    if ($livenessUris[0] -ne 'http://127.0.0.1:8096/System/Ping') { throw "Liveness URI did not use the default HTTP port: $($livenessUris -join ', ')" }

    Set-Content -LiteralPath $networkConfigurationPath -Value '<NetworkConfiguration><InternalHttpPort>invalid</InternalHttpPort><HttpServerPortNumber>23456</HttpServerPortNumber></NetworkConfiguration>'
    $invalidPortObserved = $false
    try { Get-LocalLivenessUri -DataDirectory $transactionRoot | Out-Null }
    catch { $invalidPortObserved = $true }
    if (-not $invalidPortObserved) { throw 'Liveness URI silently accepted an invalid current HTTP port.' }
    Set-Content -LiteralPath $networkConfigurationPath -Value '<NetworkConfiguration><LocalNetworkAddresses><string>mulletaflix-invalid-adapter</string></LocalNetworkAddresses></NetworkConfiguration>'
    $livenessUris = @(Get-LocalLivenessUri -DataDirectory $transactionRoot)
    if ($livenessUris[0] -ne 'http://127.0.0.1:8096/System/Ping') { throw "Liveness URI did not match the server's all-interface fallback for an unknown adapter: $($livenessUris -join ', ')" }
    Remove-Item -LiteralPath $networkConfigurationPath -Force

    Set-Content -LiteralPath $networkConfigurationPath -Value '<NetworkConfiguration><InternalHttpPort>12345</InternalHttpPort><EnableIPv4>false</EnableIPv4><EnableIPv6>true</EnableIPv6><LocalNetworkAddresses><string>::1</string></LocalNetworkAddresses></NetworkConfiguration>'
    $ipv6LivenessUris = @(Get-LocalLivenessUri -DataDirectory $transactionRoot)
    $parsedIpv6Uri = [System.Uri]$ipv6LivenessUris[0]
    $parsedIpv6Address = $null
    if ($parsedIpv6Uri.Scheme -ne 'http' -or $parsedIpv6Uri.Port -ne 12345 -or $parsedIpv6Uri.AbsolutePath -ne '/System/Ping' -or
        -not [System.Net.IPAddress]::TryParse($parsedIpv6Uri.DnsSafeHost, [ref]$parsedIpv6Address) -or
        $parsedIpv6Address.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetworkV6 -or -not [System.Net.IPAddress]::IsLoopback($parsedIpv6Address)) {
        throw "Liveness URI did not format the IPv6 bind address correctly: $($ipv6LivenessUris -join ', ')"
    }
    Remove-Item -LiteralPath $networkConfigurationPath -Force

    Set-Content -LiteralPath $networkConfigurationPath -Value '<NetworkConfiguration><InternalHttpPort>12345</InternalHttpPort><LocalNetworkAddresses><string>0.0.0.0</string></LocalNetworkAddresses></NetworkConfiguration>'
    $ipv4WildcardUris = @(Get-LocalLivenessUri -DataDirectory $transactionRoot)
    if ($ipv4WildcardUris[0] -ne 'http://127.0.0.1:12345/System/Ping') { throw "IPv4 wildcard was not converted to a loopback probe target: $($ipv4WildcardUris -join ', ')" }
    Set-Content -LiteralPath $networkConfigurationPath -Value '<NetworkConfiguration><InternalHttpPort>12345</InternalHttpPort><EnableIPv4>false</EnableIPv4><EnableIPv6>true</EnableIPv6><LocalNetworkAddresses><string>::</string></LocalNetworkAddresses></NetworkConfiguration>'
    $ipv6WildcardWithoutListenerObserved = $false
    try { Get-LocalLivenessUri -DataDirectory $transactionRoot | Out-Null }
    catch { $ipv6WildcardWithoutListenerObserved = $true }
    if (-not $ipv6WildcardWithoutListenerObserved) { throw 'IPv6-only wildcard was accepted despite having no known local listener address.' }
    Remove-Item -LiteralPath $networkConfigurationPath -Force

    if (-not ('MflxUpdateTests.RedirectResponder' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MflxUpdateTests
{
    public sealed class RedirectResponder : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _responseTask;
        public int Port { get; private set; }

        public RedirectResponder()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _responseTask = Task.Run((Action)(() =>
            {
                try
                {
                    using (TcpClient client = _listener.AcceptTcpClient())
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] request = new byte[4096];
                        stream.Read(request, 0, request.Length);
                        byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 302 Found\r\nLocation: https://mulletaflix.example/System/Ping\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                        stream.Write(response, 0, response.Length);
                        stream.Flush();
                    }
                }
                catch (ObjectDisposedException) { }
                catch (SocketException) { }
            }));
        }

        public void Dispose()
        {
            _listener.Stop();
            try { _responseTask.Wait(TimeSpan.FromSeconds(3)); }
            catch (AggregateException) { }
        }
    }
}
'@
    }
    $redirectResponder = [MflxUpdateTests.RedirectResponder]::new()
    try {
        $actualHttpResponse = Get-ServerLivenessResponse -Uri "http://127.0.0.1:$($redirectResponder.Port)/System/Ping"
        if ($actualHttpResponse.StatusCode -ne 302 -or $actualHttpResponse.Location -ne 'https://mulletaflix.example/System/Ping') {
            throw 'Liveness HTTP client did not return the original HTTPS redirect response.'
        }
    } finally {
        $redirectResponder.Dispose()
    }

    function Start-Sleep { [CmdletBinding()] param([int]$Seconds) }
    $script:livenessStatusCodes = @(503, 200)
    $script:livenessLocations = @('', '')
    $script:livenessRequestCount = 0
    function Get-ServerLivenessResponse {
        [CmdletBinding()]
        param([string]$Uri)
        $index = $script:livenessRequestCount
        $script:livenessRequestCount++
        return [pscustomobject]@{ StatusCode = $script:livenessStatusCodes[$index]; Location = $script:livenessLocations[$index] }
    }
    Wait-ForServerLiveness -LivenessUri $livenessUris -MaximumAttempts 3
    if ($script:livenessRequestCount -ne 2) { throw 'Liveness probe did not retry until the server returned HTTP 200.' }
    $script:livenessStatusCodes = @(302)
    $script:livenessLocations = @('https://mulletaflix.example/System/Ping')
    $script:livenessRequestCount = 0
    Wait-ForServerLiveness -LivenessUri $livenessUris -MaximumAttempts 1
    $script:livenessStatusCodes = @(302)
    $script:livenessLocations = @('http://other.example/System/Ping')
    $script:livenessRequestCount = 0
    $livenessFailureObserved = $false
    try { Wait-ForServerLiveness -LivenessUri $livenessUris -MaximumAttempts 1 }
    catch { $livenessFailureObserved = $true }
    if (-not $livenessFailureObserved) { throw 'Liveness probe accepted a redirect that did not require HTTPS.' }

    $script:mockProcesses = @(
        [pscustomobject]@{ Name = 'MulletaFlix.Windows.Tray.exe'; ExecutablePath = (Join-Path $installPath 'mulletaflix-windows-tray\MulletaFlix.Windows.Tray.exe'); ProcessId = 111 },
        [pscustomobject]@{ Name = 'jellyfin.exe'; ExecutablePath = 'C:\OtherServer\jellyfin.exe'; ProcessId = 222 }
    )
    $script:stoppedProcessIds = @()
    function Get-CimInstance { [CmdletBinding()] param([string]$ClassName); return $script:mockProcesses }
    function Stop-Process {
        [CmdletBinding()]
        param([int]$Id, [switch]$Force)
        $script:stoppedProcessIds += $Id
        $script:mockProcesses = @($script:mockProcesses | Where-Object { $_.ProcessId -ne $Id })
    }
    Stop-InstalledProcesses -InstallDirectory $installPath
    if (($script:stoppedProcessIds -join ',') -ne '111' -or @($script:mockProcesses).Count -ne 1 -or $script:mockProcesses[0].ProcessId -ne 222) {
        throw 'Process cleanup did not restrict termination to the selected installation directory.'
    }

    Write-Output 'Updater staging/rollback, scoped process cleanup, HTTP liveness, and version metadata checks passed.'
}
finally {
    Remove-Item -LiteralPath $registryPath -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
