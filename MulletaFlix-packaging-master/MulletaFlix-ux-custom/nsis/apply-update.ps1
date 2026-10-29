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
    [ValidateRange(0, 600)]
    [int] $ReadinessTimeoutSeconds = 180,

    [Parameter(Mandatory = $false)]
    [ValidateRange(1, 30)]
    [int] $ReadinessPollIntervalSeconds = 2,

    [Parameter(Mandatory = $false)]
    [ValidateRange(0, 300)]
    [int] $ShutdownTimeoutSeconds = 30
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

function Test-ReadinessListenerBelongsToUpdatedServer {
    param(
        [Parameter(Mandatory = $true)][int] $Port,
        [Parameter(Mandatory = $false)][int] $StandaloneProcessId,
        [Parameter(Mandatory = $false)][bool] $IsWindowsService
    )

    $updatedProcessIds = [System.Collections.Generic.HashSet[int]]::new()
    if ($StandaloneProcessId -gt 0) {
        [void]$updatedProcessIds.Add($StandaloneProcessId)
    } elseif ($IsWindowsService) {
        $serviceProcess = Get-CimInstance -ClassName Win32_Service -Filter "Name='MulletaFlixServer'" -ErrorAction Stop
        $rootProcessId = [int]$serviceProcess.ProcessId
        if ($rootProcessId -le 0) { return $false }
        [void]$updatedProcessIds.Add($rootProcessId)

        $processes = @(Get-CimInstance -ClassName Win32_Process -ErrorAction Stop)
        $addedProcess = $true
        while ($addedProcess) {
            $addedProcess = $false
            foreach ($process in $processes) {
                if ($updatedProcessIds.Contains([int]$process.ParentProcessId) -and $updatedProcessIds.Add([int]$process.ProcessId)) {
                    $addedProcess = $true
                }
            }
        }
    } else {
        return $false
    }

    foreach ($listener in (Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction Stop)) {
        if ($updatedProcessIds.Contains([int]$listener.OwningProcess)) { return $true }
    }
    return $false
}

Log "Starting in-place update for MulletaFlix."
Log "InstallDirectory: $InstallDirectory"
Log "UpdateSourceDirectory: $UpdateSourceDirectory"
Log "Target ProcessId: $ProcessId"

# 1. Wait for target MulletaFlix process to exit
$waited = 0
while ((Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) -and ($waited -lt 30)) {
    Start-Sleep -Seconds 1
    $waited++
}
if (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
    throw "In-place update aborted because MulletaFlix process $ProcessId is still running after 30 seconds. No installation files were changed; update data was retained."
}

# 2. Copy update files into InstallDirectory using robocopy for robustness and retry handling
Log "Copying update files from $UpdateSourceDirectory to $InstallDirectory..."
$rollbackDirectory = Join-Path $env:TEMP "mulletaflix-update-rollback-$([guid]::NewGuid().ToString('N'))"
$rollbackRecords = [System.Collections.Generic.List[object]]::new()
$installRoot = [System.IO.Path]::GetFullPath($InstallDirectory).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$sourceRoot = [System.IO.Path]::GetFullPath($UpdateSourceDirectory).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
[void][System.IO.Directory]::CreateDirectory($rollbackDirectory)

try {
    foreach ($sourceFile in Get-ChildItem -LiteralPath $UpdateSourceDirectory -File -Recurse -Force -ErrorAction Stop) {
        if ($sourceFile.Name -ieq 'apply-update.ps1' -or $sourceFile.Extension -in @('.log', '.tmp')) { continue }
        if ($sourceFile.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
            throw "Update package contains a reparse-point file: $($sourceFile.FullName)"
        }

        $relativePath = $sourceFile.FullName.Substring($sourceRoot.Length)
        $destination = [System.IO.Path]::GetFullPath((Join-Path $InstallDirectory $relativePath))
        if (-not $destination.StartsWith($installRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Update package path escapes the installation directory: $relativePath"
        }

        $existed = [System.IO.File]::Exists($destination)
        $record = [pscustomobject]@{
            Destination = $destination
            Existed = $existed
            Backup = $null
            LastWriteTimeUtc = $null
            Attributes = $null
        }
        if ($existed) {
            $destinationFile = [System.IO.FileInfo]::new($destination)
            if ($sourceFile.Length -eq $destinationFile.Length -and $sourceFile.LastWriteTimeUtc -eq $destinationFile.LastWriteTimeUtc -and $sourceFile.Attributes -eq $destinationFile.Attributes) { continue }
            $record.Backup = Join-Path $rollbackDirectory $relativePath
            $record.LastWriteTimeUtc = [System.IO.File]::GetLastWriteTimeUtc($destination)
            $record.Attributes = [System.IO.File]::GetAttributes($destination)
            [void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($record.Backup))
            [System.IO.File]::Copy($destination, $record.Backup, $true)
        }
        $rollbackRecords.Add($record)
    }
} catch {
    Remove-Item -LiteralPath $rollbackDirectory -Recurse -Force -ErrorAction SilentlyContinue
    throw "Could not prepare update rollback snapshot: $_"
}

$robocopyArgs = @(
    "`"$UpdateSourceDirectory`"",
    "`"$InstallDirectory`"",
    "/E",
    "/R:5",
    "/W:2",
    "/NP",
    "/NFL",
    "/NDL",
    "/XF", "apply-update.ps1", "*.log", "*.tmp"
)

$robocopyProcess = Start-Process -FilePath "robocopy.exe" -ArgumentList ($robocopyArgs -join " ") -Wait -PassThru -NoNewWindow
$exitCode = $robocopyProcess.ExitCode
Log "Robocopy completed with exit code: $exitCode"

# Robocopy exit codes: 0 to 7 indicate success (files copied or identical). 8 or higher indicates failure.
if ($exitCode -lt 8) {
    Log "Files copied successfully."
} else {
    Log "ERROR: Robocopy failed with exit code $exitCode. Restoring the installation snapshot."
    $rollbackErrors = [System.Collections.Generic.List[string]]::new()
    foreach ($record in $rollbackRecords) {
        try {
            if ($record.Existed) {
                [void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($record.Destination))
                [System.IO.File]::SetAttributes($record.Destination, [System.IO.FileAttributes]::Normal)
                [System.IO.File]::Copy($record.Backup, $record.Destination, $true)
                [System.IO.File]::SetLastWriteTimeUtc($record.Destination, $record.LastWriteTimeUtc)
                [System.IO.File]::SetAttributes($record.Destination, $record.Attributes)
            } else {
                [System.IO.File]::Delete($record.Destination)
            }
        } catch {
            $rollbackErrors.Add("$($record.Destination): $_")
        }
    }

    if ($rollbackErrors.Count -eq 0) {
        Remove-Item -LiteralPath $rollbackDirectory -Recurse -Force -ErrorAction SilentlyContinue
        Log "Installation snapshot restored successfully. Update source retained; server will not restart."
        throw "In-place update aborted because Robocopy failed with exit code $exitCode. Existing files were restored and new files removed; the current server will not be restarted."
    }

    Log "CRITICAL: rollback incomplete. Recovery snapshot retained at $rollbackDirectory. Errors: $($rollbackErrors -join ' | ')"
    throw "In-place update failed with Robocopy exit code $exitCode and rollback was incomplete. Recovery snapshot: $rollbackDirectory. Errors: $($rollbackErrors -join ' | ')"
}

# 5. Restart MulletaFlix while retaining the rollback snapshot.
$serverProcess = $null
$service = $null
$expectedCertificateThumbprint = $null
try {
    $service = Get-Service -Name "MulletaFlixServer" -ErrorAction SilentlyContinue
    if ($service) {
        Log "Starting MulletaFlixServer service..."
        Start-Service -Name "MulletaFlixServer" -ErrorAction Stop
        $service = Get-Service -Name "MulletaFlixServer" -ErrorAction Stop
        $service.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Running, [TimeSpan]::FromSeconds(30))
        if ($service.Status -ne [System.ServiceProcess.ServiceControllerStatus]::Running) {
            throw "MulletaFlixServer did not reach the Running state."
        }
    } else {
        $exePath = Join-Path $InstallDirectory "MulletaFlix.exe"
        if (-not (Test-Path -LiteralPath $exePath)) {
            throw "MulletaFlix.exe not found at $exePath"
        }

        Log "Starting MulletaFlix.exe from $exePath..."
        $args = if ($DataDirectory) { "--datadir `"$DataDirectory`"" } else { "" }
        $serverProcess = Start-Process -FilePath $exePath -ArgumentList $args -WorkingDirectory $InstallDirectory -WindowStyle Hidden -PassThru -ErrorAction Stop
        if (-not $serverProcess) { throw "MulletaFlix.exe could not be started." }
        Start-Sleep -Seconds 10
        if (-not (Get-Process -Id $serverProcess.Id -ErrorAction SilentlyContinue)) {
            throw "MulletaFlix.exe exited during the startup check."
        }
    }

    $httpPort = 8096
    $scheme = 'http'
    $baseUrl = ''
    if ($DataDirectory) {
        $networkConfigPath = Join-Path $DataDirectory 'config\network.xml'
        if (Test-Path -LiteralPath $networkConfigPath) {
            try {
                [xml]$networkConfig = Get-Content -LiteralPath $networkConfigPath -Raw -ErrorAction Stop
                $configuredPortText = [string]$networkConfig.NetworkConfiguration.InternalHttpPort
                if ([string]::IsNullOrWhiteSpace($configuredPortText)) {
                    $configuredPortText = [string]$networkConfig.NetworkConfiguration.HttpServerPortNumber
                }
                if ([string]::IsNullOrWhiteSpace($configuredPortText)) { $configuredPortText = '8096' }
                $configuredPort = [int]$configuredPortText
                if ($configuredPort -lt 1 -or $configuredPort -gt 65535) {
                    throw "Configured HTTP port is outside the valid range: $configuredPort"
                }
                $httpPort = $configuredPort
                $baseUrl = [string]$networkConfig.NetworkConfiguration.BaseUrl
                $baseUrl = $baseUrl.Trim('/')
                if ($baseUrl) { $baseUrl = "/$baseUrl" }
                if ($networkConfig.NetworkConfiguration.RequireHttps -eq 'true' -and
                    $networkConfig.NetworkConfiguration.EnableHttps -eq 'true' -and
                    -not [string]::IsNullOrWhiteSpace([string]$networkConfig.NetworkConfiguration.CertificatePath)) {
                    $httpsPortText = [string]$networkConfig.NetworkConfiguration.InternalHttpsPort
                    if ([string]::IsNullOrWhiteSpace($httpsPortText)) { $httpsPortText = [string]$networkConfig.NetworkConfiguration.HttpsPortNumber }
                    if ([string]::IsNullOrWhiteSpace($httpsPortText)) { $httpsPortText = '8920' }
                    $httpsPort = [int]$httpsPortText
                    if ($httpsPort -lt 1 -or $httpsPort -gt 65535) {
                        throw "Configured HTTPS port is outside the valid range: $httpsPort"
                    }
                    $httpPort = $httpsPort
                    $scheme = 'https'

                    $certificatePath = [string]$networkConfig.NetworkConfiguration.CertificatePath
                    if (-not (Test-Path -LiteralPath $certificatePath -PathType Leaf)) {
                        throw "Configured HTTPS certificate was not found."
                    }
                    $configuredCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
                        $certificatePath,
                        [string]$networkConfig.NetworkConfiguration.CertificatePassword)
                    try { $expectedCertificateThumbprint = $configuredCertificate.Thumbprint } finally { $configuredCertificate.Dispose() }
                }
            } catch {
                throw "Could not read the configured MulletaFlix readiness endpoint from $networkConfigPath : $_"
            }
        }
    }

    $readinessUri = "${scheme}://127.0.0.1:$httpPort$baseUrl/ready"
    $readinessDeadline = [DateTime]::UtcNow.AddSeconds($ReadinessTimeoutSeconds)
    $ready = $false
    $readinessError = 'The readiness endpoint did not return HTTP 2xx.'
    do {
        try {
            $previousCertificateCallback = [System.Net.ServicePointManager]::ServerCertificateValidationCallback
            try {
                if ($scheme -eq 'https') {
                    # Pin the loopback probe to the certificate configured by this server. This preserves
                    # certificate identity validation while allowing a domain certificate on 127.0.0.1.
                    $pinnedThumbprint = $expectedCertificateThumbprint
                    $certificateCallback = {
                        param($sender, $certificate, $chain, $sslPolicyErrors)
                        try {
                            $requestUri = $sender.RequestUri
                            $remoteCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certificate)
                            try {
                                return $requestUri.IsLoopback -and
                                    $remoteCertificate.Thumbprint -eq $pinnedThumbprint -and
                                    $remoteCertificate.NotBefore.ToUniversalTime() -le [DateTime]::UtcNow -and
                                    $remoteCertificate.NotAfter.ToUniversalTime() -gt [DateTime]::UtcNow
                            } finally { $remoteCertificate.Dispose() }
                        } catch { return $false }
                    }.GetNewClosure()
                    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = $certificateCallback
                }

                $readinessResponse = Invoke-WebRequest -Uri $readinessUri -TimeoutSec 3 -UseBasicParsing -ErrorAction Stop
            } finally {
                [System.Net.ServicePointManager]::ServerCertificateValidationCallback = $previousCertificateCallback
            }
            $statusCode = [int]$readinessResponse.StatusCode
            if ($statusCode -ge 200 -and $statusCode -lt 300) {
                $standalonePid = if ($serverProcess) { [int]$serverProcess.Id } else { 0 }
                $isService = $null -ne $service
                if (Test-ReadinessListenerBelongsToUpdatedServer -Port $httpPort -StandaloneProcessId $standalonePid -IsWindowsService $isService) {
                    $ready = $true
                    break
                }
                $readinessError = 'HTTP 2xx came from a listener that is not owned by the updated MulletaFlix service/process.'
            } else {
                $readinessError = "The readiness endpoint returned HTTP $statusCode."
            }
        } catch {
            $readinessError = $_.Exception.Message
        }

        if ([DateTime]::UtcNow -ge $readinessDeadline) { break }
        Start-Sleep -Seconds ([Math]::Min($ReadinessPollIntervalSeconds, [Math]::Max(1, [int][Math]::Ceiling(($readinessDeadline - [DateTime]::UtcNow).TotalSeconds))))
    } while ($true)

    if (-not $ready) {
        throw "MulletaFlix did not become ready at $readinessUri within $ReadinessTimeoutSeconds seconds. Last error: $readinessError"
    }
    Log "MulletaFlix readiness confirmed at $readinessUri."
} catch {
    $startupError = $_
    Log "ERROR: updated server failed to start: $startupError. Restoring the installation snapshot."
    $rollbackErrors = [System.Collections.Generic.List[string]]::new()
    if ($service) {
        try {
            Stop-Service -Name "MulletaFlixServer" -Force -ErrorAction Stop
            $service = Get-Service -Name "MulletaFlixServer" -ErrorAction Stop
            $service.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds($ShutdownTimeoutSeconds))
            if ($service.Status -ne [System.ServiceProcess.ServiceControllerStatus]::Stopped) {
                throw "MulletaFlixServer remains in state $($service.Status)."
            }
        } catch {
            $rollbackErrors.Add("Could not stop MulletaFlixServer before rollback: $_")
        }
    }
    if ($serverProcess) {
        Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue
        $stopDeadline = [DateTime]::UtcNow.AddSeconds($ShutdownTimeoutSeconds)
        while ((Get-Process -Id $serverProcess.Id -ErrorAction SilentlyContinue) -and [DateTime]::UtcNow -lt $stopDeadline) {
            Start-Sleep -Seconds 1
        }
        if (Get-Process -Id $serverProcess.Id -ErrorAction SilentlyContinue) {
            $rollbackErrors.Add("Standalone server process $($serverProcess.Id) is still running; installation files were left untouched.")
        }
    }

    if ($rollbackErrors.Count -eq 0) {
      foreach ($record in $rollbackRecords) {
        try {
            if ($record.Existed) {
                [void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($record.Destination))
                [System.IO.File]::SetAttributes($record.Destination, [System.IO.FileAttributes]::Normal)
                [System.IO.File]::Copy($record.Backup, $record.Destination, $true)
                [System.IO.File]::SetLastWriteTimeUtc($record.Destination, $record.LastWriteTimeUtc)
                [System.IO.File]::SetAttributes($record.Destination, $record.Attributes)
            } else {
                [System.IO.File]::Delete($record.Destination)
            }
        } catch {
            $rollbackErrors.Add("$($record.Destination): $_")
        }
      }
    }

    if ($rollbackErrors.Count -eq 0) {
        Remove-Item -LiteralPath $rollbackDirectory -Recurse -Force -ErrorAction SilentlyContinue
        Log "Installation snapshot restored after startup failure. Update source retained."
        throw "In-place update aborted because the updated server failed to start. Existing files were restored; update data was retained. Startup error: $startupError"
    }

    Log "CRITICAL: rollback after startup failure was incomplete. Recovery snapshot retained at $rollbackDirectory. Errors: $($rollbackErrors -join ' | ')"
    throw "Updated server failed to start and rollback was incomplete. Recovery snapshot: $rollbackDirectory. Errors: $($rollbackErrors -join ' | '). Startup error: $startupError"
}

# 6. Restart Tray if available
$trayPath = Join-Path $InstallDirectory "mulletaflix-windows-tray\MulletaFlix.Windows.Tray.exe"
if (Test-Path -LiteralPath $trayPath) {
    Log "Starting MulletaFlix.Windows.Tray.exe..."
    Start-Process -FilePath $trayPath -WorkingDirectory (Join-Path $InstallDirectory "mulletaflix-windows-tray")
}

# The update is committed only after the service/process startup check succeeds.
Remove-Item -LiteralPath $rollbackDirectory -Recurse -Force -ErrorAction SilentlyContinue

# 7. Clean up temporary extracted update files and state.
try {
    Remove-Item -LiteralPath $UpdateSourceDirectory -Recurse -Force -ErrorAction SilentlyContinue
    Log "Cleaned up update source directory."
} catch {
    Log "Could not clean update source directory: $_"
}

if ($DataDirectory) {
    $stateFile = Join-Path $DataDirectory "updates\update_state.json"
    $pkgZip = Join-Path $DataDirectory "updates\package.zip"
    if (Test-Path -LiteralPath $stateFile) { Remove-Item -LiteralPath $stateFile -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $pkgZip) { Remove-Item -LiteralPath $pkgZip -Force -ErrorAction SilentlyContinue }
}

Log "In-place update script finished."
