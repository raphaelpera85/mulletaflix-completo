param(
    [Parameter(Mandatory = $true)]
    [string] $InstallDirectory,

    [Parameter(Mandatory = $true)]
    [string] $UpdateSourceDirectory,

    [Parameter(Mandatory = $true)]
    [int] $ProcessId,

    [Parameter(Mandatory = $false)]
    [string] $DataDirectory,

    [Parameter(Mandatory = $false)]
    [string] $TargetVersion
)

$ErrorActionPreference = 'Continue'
$logFile1 = Join-Path $InstallDirectory 'update.log'
$logFile2 = Join-Path $env:TEMP 'mulletaflix-update.log'

function Log {
    param([string]$Message)
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $line = "$timestamp [UPDATE] $Message"
    try { $line | Out-File -FilePath $logFile1 -Append -Encoding utf8 } catch {}
    try { $line | Out-File -FilePath $logFile2 -Append -Encoding utf8 } catch {}
}

# BEGIN VERSION METADATA HELPERS
function ConvertTo-DisplayVersion {
    param([Parameter(Mandatory = $true)][string]$Version)

    $parsedVersion = [version]::MinValue
    if (-not [version]::TryParse($Version, [ref]$parsedVersion) -or $parsedVersion.Build -lt 0) {
        throw 'Update target version is not a valid three-part version.'
    }

    return '{0}.{1}.{2}' -f $parsedVersion.Major, $parsedVersion.Minor, $parsedVersion.Build
}

function Set-InstalledVersionMetadata {
    param(
        [Parameter(Mandatory = $true)][string]$TargetVersion,
        [Parameter(Mandatory = $false)][string]$RegistryPath = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MulletaFlixServer'
    )

    $displayVersion = ConvertTo-DisplayVersion -Version $TargetVersion
    if (-not (Test-Path -LiteralPath $RegistryPath)) {
        throw 'MulletaFlix uninstall registry key was not found; version metadata was not changed.'
    }

    Set-ItemProperty -LiteralPath $RegistryPath -Name DisplayVersion -Value $displayVersion -ErrorAction Stop
    Set-ItemProperty -LiteralPath $RegistryPath -Name DisplayName -Value "MulletaFlix Server $displayVersion" -ErrorAction Stop
    return $displayVersion
}

function Sync-InstalledVersionMetadata {
    param(
        [string]$InstallDirectory,
        [string]$TargetVersion,
        [string]$RegistryPath = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MulletaFlixServer'
    )

    $targetDisplayVersion = Assert-UpdateVersion -InstallDirectory $InstallDirectory -TargetVersion $TargetVersion
    Set-InstalledVersionMetadata -TargetVersion $targetDisplayVersion -RegistryPath $RegistryPath | Out-Null
    return $targetDisplayVersion
}

function Assert-UpdateVersion {
    param([Parameter(Mandatory = $true)][string]$InstallDirectory, [Parameter(Mandatory = $true)][string]$TargetVersion)

    $targetDisplayVersion = ConvertTo-DisplayVersion -Version $TargetVersion
    $serverAssembly = Join-Path $InstallDirectory 'MulletaFlix.dll'
    if (-not (Test-Path -LiteralPath $serverAssembly -PathType Leaf)) {
        throw 'Updated MulletaFlix assembly was not found in the staged payload.'
    }

    $installedProductVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($serverAssembly).ProductVersion
    $installedDisplayVersion = ConvertTo-DisplayVersion -Version $installedProductVersion
    if ($installedDisplayVersion -ne $targetDisplayVersion) {
        throw 'Staged assembly version does not match the verified update target.'
    }

    return $targetDisplayVersion
}
# END VERSION METADATA HELPERS

function Copy-UpdateToStage {
    param([Parameter(Mandatory = $true)][string]$SourceDirectory, [Parameter(Mandatory = $true)][string]$StageDirectory)

    New-Item -ItemType Directory -Path $StageDirectory -Force -ErrorAction Stop | Out-Null
    $arguments = @("`"$SourceDirectory`"", "`"$StageDirectory`"", '/E', '/XJ', '/R:5', '/W:2', '/NP', '/NFL', '/NDL', '/XF', 'apply-update.ps1', '*.log', '*.tmp')
    $copy = Start-Process -FilePath 'robocopy.exe' -ArgumentList ($arguments -join ' ') -Wait -PassThru -NoNewWindow -ErrorAction Stop
    if ($copy.ExitCode -ge 8) {
        throw "Robocopy could not stage the update payload (exit code $($copy.ExitCode))."
    }
}

function Copy-InstalledFilesToStage {
    param([Parameter(Mandatory = $true)][string]$InstallDirectory, [Parameter(Mandatory = $true)][string]$StageDirectory, [Parameter(Mandatory = $true)][string]$UpdateStageDirectory)

    $arguments = @("`"$InstallDirectory`"", "`"$StageDirectory`"", '/E', '/XJ', '/R:5', '/W:2', '/NP', '/NFL', '/NDL', '/XF', '*.log', '*.tmp')
    $copy = Start-Process -FilePath 'robocopy.exe' -ArgumentList ($arguments -join ' ') -Wait -PassThru -NoNewWindow -ErrorAction Stop
    if ($copy.ExitCode -ge 8) {
        throw "Robocopy could not preserve the installed files in the stage (exit code $($copy.ExitCode))."
    }

    $overlayArguments = @("`"$UpdateStageDirectory`"", "`"$StageDirectory`"", '/E', '/XJ', '/R:5', '/W:2', '/NP', '/NFL', '/NDL', '/XF', '*.log', '*.tmp')
    $overlay = Start-Process -FilePath 'robocopy.exe' -ArgumentList ($overlayArguments -join ' ') -Wait -PassThru -NoNewWindow -ErrorAction Stop
    if ($overlay.ExitCode -ge 8) {
        throw "Robocopy could not apply the staged update (exit code $($overlay.ExitCode))."
    }
}

function Move-StageIntoInstall {
    param([Parameter(Mandatory = $true)][string]$InstallDirectory, [Parameter(Mandatory = $true)][string]$StageDirectory, [Parameter(Mandatory = $true)][string]$BackupDirectory)

    Move-Item -LiteralPath $InstallDirectory -Destination $BackupDirectory -ErrorAction Stop
    try {
        Move-Item -LiteralPath $StageDirectory -Destination $InstallDirectory -ErrorAction Stop
    } catch {
        if (-not (Test-Path -LiteralPath $InstallDirectory) -and (Test-Path -LiteralPath $BackupDirectory)) {
            Move-Item -LiteralPath $BackupDirectory -Destination $InstallDirectory -ErrorAction Stop
        }
        throw
    }
}

function Restore-PreviousInstall {
    param([Parameter(Mandatory = $true)][string]$InstallDirectory, [Parameter(Mandatory = $true)][string]$BackupDirectory, [Parameter(Mandatory = $true)][string]$FailedDirectory)

    if (Test-Path -LiteralPath $InstallDirectory) {
        Move-Item -LiteralPath $InstallDirectory -Destination $FailedDirectory -ErrorAction Stop
    }
    Move-Item -LiteralPath $BackupDirectory -Destination $InstallDirectory -ErrorAction Stop
}

function Start-InstalledServer {
    param([Parameter(Mandatory = $true)][string]$InstallDirectory, [string]$DataDirectory, [bool]$ServiceMode)

    if ($ServiceMode) {
        Log 'Starting MulletaFlixServer service...'
        Start-Service -Name 'MulletaFlixServer' -ErrorAction Stop
        $runningService = Get-Service -Name 'MulletaFlixServer' -ErrorAction Stop
        $runningService.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Running, [TimeSpan]::FromSeconds(30))
        Log 'MulletaFlixServer service is running.'
        return
    }

    $exePath = Join-Path $InstallDirectory 'MulletaFlix.exe'
    if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) { throw "MulletaFlix.exe not found at $exePath" }
    $arguments = if ($DataDirectory) { "--datadir `"$DataDirectory`"" } else { '' }
    $serverProcess = Start-Process -FilePath $exePath -ArgumentList $arguments -WorkingDirectory $InstallDirectory -WindowStyle Hidden -PassThru -ErrorAction Stop
    Start-Sleep -Seconds 2
    if ($serverProcess.HasExited) { throw "MulletaFlix.exe exited immediately after start with exit code $($serverProcess.ExitCode)." }

    $trayPath = Join-Path $InstallDirectory 'mulletaflix-windows-tray\MulletaFlix.Windows.Tray.exe'
    if (Test-Path -LiteralPath $trayPath -PathType Leaf) {
        try {
            $null = Start-Process -FilePath $trayPath -WorkingDirectory (Join-Path $InstallDirectory 'mulletaflix-windows-tray') -PassThru -ErrorAction Stop
        } catch {
            Log "WARNING: MulletaFlix.Windows.Tray.exe failed to start: $($_.Exception.Message)"
        }
    }
    Log 'MulletaFlix.exe remains running after startup.'
}

function Get-LocalLivenessUri {
    param([string]$DataDirectory)

    $port = 8096
    $baseUrl = ''
    $bindAddresses = @()
    $enableIpv4 = $true
    $enableIpv6 = $false
    $hasExplicitBindAddresses = $false
    if ($DataDirectory) {
        $networkConfigurationPath = Join-Path $DataDirectory 'config\network.xml'
        if (Test-Path -LiteralPath $networkConfigurationPath -PathType Leaf) {
            [xml]$networkConfiguration = Get-Content -LiteralPath $networkConfigurationPath -Raw -ErrorAction Stop
            $configuration = $networkConfiguration.NetworkConfiguration
            $baseUrl = ([string]$configuration.BaseUrl).Trim('/')
            $portNodes = @($configuration.InternalHttpPort, $configuration.HttpServerPortNumber)
            foreach ($portNode in $portNodes) {
                if ($null -eq $portNode -or [string]::IsNullOrWhiteSpace([string]$portNode)) { continue }
                $configuredPort = 0
                if (-not [int]::TryParse([string]$portNode, [ref]$configuredPort) -or $configuredPort -lt 1 -or $configuredPort -gt 65535) {
                    throw "Configured HTTP port '$portNode' in $networkConfigurationPath is invalid."
                }
                $port = $configuredPort
                break
            }

            $enableIpv4 = $configuration.EnableIPv4 -ne 'false'
            $enableIpv6 = $configuration.EnableIPv6 -eq 'true'
            $ignoreVirtualInterfaces = $configuration.IgnoreVirtualInterfaces -ne 'false'
            $configuredVirtualPrefixes = @($configuration.VirtualInterfaceNames.string | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
            if ($configuredVirtualPrefixes.Count -eq 0) {
                $virtualInterfacePrefixes = @('veth')
            } else {
                $virtualInterfacePrefixes = @($configuredVirtualPrefixes | ForEach-Object { ([string]$_).Replace('*', '') })
            }
            $configuredAddresses = @($configuration.LocalNetworkAddresses.string)
            $hasExplicitBindAddresses = $configuredAddresses.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace([string]$configuredAddresses[0])
            foreach ($addressNode in $configuredAddresses) {
                $configuredAddress = ([string]$addressNode).Trim()
                if ([string]::IsNullOrWhiteSpace($configuredAddress)) { continue }
                $addressText = $configuredAddress.Split('/')[0].Trim()
                $parsedAddress = $null
                if ([System.Net.IPAddress]::TryParse($addressText, [ref]$parsedAddress)) {
                    if ($parsedAddress.Equals([System.Net.IPAddress]::Any)) {
                        if ($enableIpv4) { $bindAddresses += [System.Net.IPAddress]::Loopback }
                        continue
                    }
                    if ($parsedAddress.Equals([System.Net.IPAddress]::IPv6Any)) {
                        # NetworkManager does not resolve IPv6Any to a known listener in IPv6-only mode.
                        # In dual-stack mode, IPv4 remains the reliable local liveness endpoint.
                        if ($enableIpv6 -and $enableIpv4) { $bindAddresses += [System.Net.IPAddress]::IPv6Loopback }
                        continue
                    }

                    $familyEnabled = ($parsedAddress.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork -and $enableIpv4) -or
                        ($parsedAddress.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetworkV6 -and $enableIpv6)
                    if (-not $familyEnabled) { continue }

                    if ([System.Net.IPAddress]::IsLoopback($parsedAddress)) {
                        $loopbackIsIgnored = $ignoreVirtualInterfaces -and @($virtualInterfacePrefixes | Where-Object {
                            'lo'.StartsWith([string]$_, [System.StringComparison]::OrdinalIgnoreCase)
                        }).Count -gt 0
                        if (-not $loopbackIsIgnored) { $bindAddresses += $parsedAddress }
                        continue
                    }

                    foreach ($candidateInterface in [System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() | Where-Object {
                        $_.OperationalStatus -eq [System.Net.NetworkInformation.OperationalStatus]::Up
                    }) {
                        $isIgnoredInterface = $ignoreVirtualInterfaces -and @($virtualInterfacePrefixes | Where-Object {
                            $candidateInterface.Name.StartsWith([string]$_, [System.StringComparison]::OrdinalIgnoreCase)
                        }).Count -gt 0
                        if ($isIgnoredInterface) { continue }
                        if (@($candidateInterface.GetIPProperties().UnicastAddresses | Where-Object { $_.Address.Equals($parsedAddress) }).Count -gt 0) {
                            $bindAddresses += $parsedAddress
                            break
                        }
                    }
                    continue
                }

                $networkInterface = [System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() | Where-Object {
                    $_.OperationalStatus -eq [System.Net.NetworkInformation.OperationalStatus]::Up -and
                    [string]::Equals($_.Name, $configuredAddress, [System.StringComparison]::OrdinalIgnoreCase)
                } | Select-Object -First 1
                if ($null -ne $networkInterface) {
                    $isIgnoredInterface = $ignoreVirtualInterfaces -and @($virtualInterfacePrefixes | Where-Object {
                        $networkInterface.Name.StartsWith([string]$_, [System.StringComparison]::OrdinalIgnoreCase)
                    }).Count -gt 0
                    if ($isIgnoredInterface) { continue }
                    $unicastAddress = $networkInterface.GetIPProperties().UnicastAddresses | Where-Object {
                        ($_.Address.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork -and $enableIpv4) -or
                        ($_.Address.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetworkV6 -and $enableIpv6)
                    } | Select-Object -First 1
                    if ($null -ne $unicastAddress) {
                        $bindAddresses += $unicastAddress.Address
                    }
                }
            }

            if ($bindAddresses.Count -eq 0) {
                if ($enableIpv4) {
                    # NetworkManager falls back to an all-interface IPv4 listener when no configured address resolves.
                    $bindAddresses += [System.Net.IPAddress]::Loopback
                } elseif ($enableIpv6 -and -not $hasExplicitBindAddresses) {
                    # IPv6-only with no explicit bind uses the active IPv6 interfaces; mirror that listener set.
                    $activeInterfaces = @([System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() | Where-Object {
                        $_.OperationalStatus -eq [System.Net.NetworkInformation.OperationalStatus]::Up
                    })
                    $activeIpv6AddressCount = 0
                    foreach ($networkInterface in $activeInterfaces) {
                        $isIgnoredInterface = $ignoreVirtualInterfaces -and @($virtualInterfacePrefixes | Where-Object {
                            $networkInterface.Name.StartsWith([string]$_, [System.StringComparison]::OrdinalIgnoreCase)
                        }).Count -gt 0
                        if ($isIgnoredInterface) { continue }
                        $addresses = @($networkInterface.GetIPProperties().UnicastAddresses | Where-Object {
                            $_.Address.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetworkV6
                        } | ForEach-Object { $_.Address })
                        $activeIpv6AddressCount += $addresses.Count
                        $bindAddresses += $addresses
                    }
                    if ($bindAddresses.Count -eq 0) {
                        $hasActiveIpv6Address = @($activeInterfaces | Where-Object {
                            @($_.GetIPProperties().UnicastAddresses | Where-Object { $_.Address.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetworkV6 }).Count -gt 0
                        }).Count -gt 0
                        $fallbackLoopbackIsIgnored = $ignoreVirtualInterfaces -and @($virtualInterfacePrefixes | Where-Object {
                            'lo'.StartsWith([string]$_, [System.StringComparison]::OrdinalIgnoreCase)
                        }).Count -gt 0
                        if ($hasActiveIpv6Address) {
                            throw "All active IPv6 bind interfaces are excluded by $networkConfigurationPath."
                        } elseif (-not $fallbackLoopbackIsIgnored) {
                            $bindAddresses += [System.Net.IPAddress]::IPv6Loopback
                        } else {
                            throw "No configured IPv6 bind address in $networkConfigurationPath resolves to an enabled local listener."
                        }
                    }
                } elseif ($enableIpv6) {
                    throw "No configured bind address in $networkConfigurationPath resolves to an enabled local listener."
                }
            }
        }
    }

    if ($bindAddresses.Count -eq 0) { $bindAddresses += [System.Net.IPAddress]::Loopback }
    $path = if ($baseUrl) { "/$baseUrl/System/Ping" } else { '/System/Ping' }
    return @($bindAddresses | Select-Object -Unique | ForEach-Object {
        $builder = [System.UriBuilder]::new('http', $_.ToString(), $port, $path)
        $builder.Uri.AbsoluteUri
    })
}

function Get-ServerLivenessResponse {
    param([Parameter(Mandatory = $true)][string]$Uri)

    $response = $null
    try {
        $request = [System.Net.HttpWebRequest]::Create($Uri)
        $request.AllowAutoRedirect = $false
        $request.Timeout = 3000
        $response = [System.Net.HttpWebResponse]$request.GetResponse()
        return [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Location = [string]$response.Headers['Location'] }
    } finally {
        if ($null -ne $response) { $response.Dispose() }
    }
}

function Wait-ForServerLiveness {
    param([Parameter(Mandatory = $true)][string[]]$LivenessUri, [int]$MaximumAttempts = 45)

    for ($attempt = 1; $attempt -le $MaximumAttempts; $attempt++) {
        foreach ($uri in $LivenessUri) {
            try {
                $response = Get-ServerLivenessResponse -Uri $uri
                if ([int]$response.StatusCode -eq 200) { return }
                $location = [string]$response.Location
                $redirectUri = $null
                if ([int]$response.StatusCode -in @(301, 302, 307, 308) -and [System.Uri]::TryCreate($location, [System.UriKind]::Absolute, [ref]$redirectUri) -and $redirectUri.Scheme -eq 'https') { return }
            } catch {
                # Kestrel and API routing may take several attempts; log only the final failure.
            }
        }

        if ($attempt -lt $MaximumAttempts) { Start-Sleep -Seconds 2 }
    }

    throw "MulletaFlix did not return HTTP 200 or a valid HTTPS redirect from $($LivenessUri -join ', ') after $MaximumAttempts attempts."
}

function Get-InstalledProcesses {
    param([Parameter(Mandatory = $true)][string]$InstallDirectory)

    $installRoot = [System.IO.Path]::GetFullPath($InstallDirectory).TrimEnd('\') + '\'
    $processNames = @('MulletaFlix.exe', 'MulletaFlix.Windows.Tray.exe', 'jellyfin.exe')
    return @(Get-CimInstance -ClassName Win32_Process -ErrorAction Stop | Where-Object {
        $_.Name -in $processNames -and
        -not [string]::IsNullOrWhiteSpace($_.ExecutablePath) -and
        ([System.IO.Path]::GetFullPath($_.ExecutablePath)).StartsWith($installRoot, [System.StringComparison]::OrdinalIgnoreCase)
    })
}

function Stop-InstalledProcesses {
    param([Parameter(Mandatory = $true)][string]$InstallDirectory)

    $processes = @(Get-InstalledProcesses -InstallDirectory $InstallDirectory)
    foreach ($process in $processes) {
        Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
    }

    if ($processes.Count -gt 0) {
        Start-Sleep -Seconds 2
        $remaining = @(Get-InstalledProcesses -InstallDirectory $InstallDirectory)
        if ($remaining.Count -gt 0) {
            throw "Processes from this installation did not exit: $($remaining.ProcessId -join ', ')."
        }
    }
}

Log "Starting in-place update for MulletaFlix."
Log "InstallDirectory: $InstallDirectory"
Log "UpdateSourceDirectory: $UpdateSourceDirectory"
Log "Target ProcessId: $ProcessId"

$installFullPath = [System.IO.Path]::GetFullPath($InstallDirectory).TrimEnd('\')
if ($DataDirectory) {
    $dataFullPath = [System.IO.Path]::GetFullPath($DataDirectory).TrimEnd('\')
    if ([string]::Equals($dataFullPath, $installFullPath, [System.StringComparison]::OrdinalIgnoreCase) -or
        $dataFullPath.StartsWith($installFullPath + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        Log 'ERROR: DataDirectory is inside InstallDirectory; refusing a transactional swap that could duplicate or move user data.'
        exit 1
    }
}

# 1. Wait for target MulletaFlix process to exit
$waited = 0
while ((Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) -and ($waited -lt 30)) {
    Start-Sleep -Seconds 1
    $waited++
}
if (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
    Log "ERROR: Target server process $ProcessId did not exit within 30 seconds; leaving the active installation untouched."
    exit 1
}

# 2. Copy and validate the full update payload beside the installation first.
$installParent = Split-Path -Parent ([System.IO.Path]::GetFullPath($InstallDirectory))
$transactionId = [guid]::NewGuid().ToString('N')
$stageDirectory = Join-Path $installParent "mfx-update-payload-$transactionId"
$assembledDirectory = Join-Path $installParent "mfx-update-stage-$transactionId"
$backupDirectory = Join-Path $installParent "mfx-update-backup-$transactionId"
$failedDirectory = Join-Path $installParent "mfx-update-failed-$transactionId"
$service = $null
try {
    Log "Staging update files at $stageDirectory..."
    Copy-UpdateToStage -SourceDirectory $UpdateSourceDirectory -StageDirectory $stageDirectory
    $null = Assert-UpdateVersion -InstallDirectory $stageDirectory -TargetVersion $TargetVersion
} catch {
    Log "ERROR: Update staging or version validation failed; the installed server remains active: $($_.Exception.Message)"
    Remove-Item -LiteralPath $stageDirectory -Recurse -Force -ErrorAction SilentlyContinue
    exit 1
}

# 3. Stop the Windows service only after the staged payload has passed validation.
$service = Get-Service -Name "MulletaFlixServer" -ErrorAction SilentlyContinue
if ($service) {
    try {
        $service = Get-Service -Name "MulletaFlixServer" -ErrorAction Stop
        if ($service.Status -ne 'Stopped') {
            Log "Stopping MulletaFlixServer service before replacing files..."
            Stop-Service -Name "MulletaFlixServer" -Force -ErrorAction Stop
            $waited = 0
            do {
                Start-Sleep -Seconds 1
                $waited++
                $service = Get-Service -Name "MulletaFlixServer" -ErrorAction Stop
            } while ($service.Status -ne 'Stopped' -and $waited -lt 30)
        }

        if ($service.Status -ne 'Stopped') {
            throw 'MulletaFlixServer did not stop within 30 seconds.'
        }
    } catch {
        Log "ERROR: Could not stop MulletaFlixServer; update was not copied: $($_.Exception.Message)"
        try { Start-InstalledServer -InstallDirectory $InstallDirectory -DataDirectory $DataDirectory -ServiceMode ([bool]$service) } catch { Log "ERROR: Existing server recovery failed: $($_.Exception.Message)" }
        Remove-Item -LiteralPath $stageDirectory -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $assembledDirectory -Recurse -Force -ErrorAction SilentlyContinue
        exit 1
    }
}

# 4. Stop only processes whose executable path belongs to this installation.
try {
    Stop-InstalledProcesses -InstallDirectory $InstallDirectory
} catch {
    Log "ERROR: Could not stop processes for this installation: $($_.Exception.Message)"
    try { Start-InstalledServer -InstallDirectory $InstallDirectory -DataDirectory $DataDirectory -ServiceMode ([bool]$service) } catch { Log "ERROR: Existing server recovery failed: $($_.Exception.Message)" }
    Remove-Item -LiteralPath $stageDirectory -Recurse -Force -ErrorAction SilentlyContinue
    exit 1
}

try {
    New-Item -ItemType Directory -Path $assembledDirectory -Force -ErrorAction Stop | Out-Null
    Copy-InstalledFilesToStage -InstallDirectory $InstallDirectory -StageDirectory $assembledDirectory -UpdateStageDirectory $stageDirectory
    $null = Assert-UpdateVersion -InstallDirectory $assembledDirectory -TargetVersion $TargetVersion
    $installAcl = Get-Acl -LiteralPath $InstallDirectory -ErrorAction Stop
    Set-Acl -LiteralPath $assembledDirectory -AclObject $installAcl -ErrorAction Stop
} catch {
    Log "ERROR: Could not assemble a complete update beside the installation; the previous server remains intact: $($_.Exception.Message)"
    try { Start-InstalledServer -InstallDirectory $InstallDirectory -DataDirectory $DataDirectory -ServiceMode ([bool]$service) } catch { Log "ERROR: Existing server recovery failed: $($_.Exception.Message)" }
    Remove-Item -LiteralPath $stageDirectory -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $assembledDirectory -Recurse -Force -ErrorAction SilentlyContinue
    exit 1
}

try {
    Move-StageIntoInstall -InstallDirectory $InstallDirectory -StageDirectory $assembledDirectory -BackupDirectory $backupDirectory
    Log "Validated update stage moved into place; previous installation retained at $backupDirectory."
} catch {
    Log "ERROR: Could not switch to staged update: $($_.Exception.Message)"
    if (Test-Path -LiteralPath $backupDirectory) {
        try {
            Restore-PreviousInstall -InstallDirectory $InstallDirectory -BackupDirectory $backupDirectory -FailedDirectory $failedDirectory
        } catch {
            Log "CRITICAL: Installation rollback failed; previous installation is retained at ${backupDirectory}: $($_.Exception.Message)"
        }
    }
    try { Start-InstalledServer -InstallDirectory $InstallDirectory -DataDirectory $DataDirectory -ServiceMode ([bool]$service) } catch { Log "ERROR: Server recovery failed: $($_.Exception.Message)" }
    exit 1
}

try {
    Start-InstalledServer -InstallDirectory $InstallDirectory -DataDirectory $DataDirectory -ServiceMode ([bool]$service)
    $livenessUri = Get-LocalLivenessUri -DataDirectory $DataDirectory
    Wait-ForServerLiveness -LivenessUri $livenessUri
    Log "MulletaFlix returned HTTP 200 from $livenessUri."
    try {
        $installedVersion = Sync-InstalledVersionMetadata -InstallDirectory $InstallDirectory -TargetVersion $TargetVersion
        Log "Uninstall version metadata updated to $installedVersion."
    } catch {
        Log "WARNING: Could not synchronize uninstall version metadata: $($_.Exception.Message)"
    }
} catch {
    Log "ERROR: Updated server did not start; attempting to restore the previous installation: $($_.Exception.Message)"
    if ($service) { Stop-Service -Name 'MulletaFlixServer' -Force -ErrorAction SilentlyContinue }
    try { Stop-InstalledProcesses -InstallDirectory $InstallDirectory } catch { Log "WARNING: Could not stop every process from the failed installation before rollback: $($_.Exception.Message)" }
    try {
        Restore-PreviousInstall -InstallDirectory $InstallDirectory -BackupDirectory $backupDirectory -FailedDirectory $failedDirectory
        Start-InstalledServer -InstallDirectory $InstallDirectory -DataDirectory $DataDirectory -ServiceMode ([bool]$service)
        Log 'Previous installation restored and restarted.'
    } catch {
        Log "CRITICAL: Update rollback could not restore and start the previous installation. Backup retained at ${backupDirectory}: $($_.Exception.Message)"
    }
    exit 1
}

# 5. Clean up temporary update files only after the new server passed the health check.
try {
    Remove-Item -LiteralPath $backupDirectory -Recurse -Force -ErrorAction Stop
    Log 'Previous installation backup removed after successful startup.'
} catch {
    Log "WARNING: Previous installation backup retained at ${backupDirectory}: $($_.Exception.Message)"
}
try {
    Remove-Item -LiteralPath $UpdateSourceDirectory -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $stageDirectory -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $assembledDirectory -Recurse -Force -ErrorAction SilentlyContinue
} catch {
    Log "Could not clean staging files: $($_.Exception.Message)"
}
if ($DataDirectory) {
    $stateFile = Join-Path $DataDirectory "updates\update_state.json"
    $pkgZip = Join-Path $DataDirectory "updates\package.zip"
    if (Test-Path -LiteralPath $stateFile) { Remove-Item -LiteralPath $stateFile -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $pkgZip) { Remove-Item -LiteralPath $pkgZip -Force -ErrorAction SilentlyContinue }
}

Log "In-place update script finished."
