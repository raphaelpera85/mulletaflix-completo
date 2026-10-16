function Save-AndroidEmulatorBootEvidence {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Directory,

        [Parameter(Mandatory = $true)]
        [ValidatePattern('^[A-Za-z0-9_-]+$')]
        [string] $AvdName,

        [Parameter(Mandatory = $true)]
        [ValidatePattern('^emulator-\d+$')]
        [string] $Serial,

        [Parameter(Mandatory = $true)]
        [bool] $BootCompleted,

        [Parameter(Mandatory = $true)]
        [bool] $PackageManagerReady,

        [Parameter(Mandatory = $true)]
        [string] $CaptureStatus,

        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)]
        [string[]] $PropertyLines,

        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)]
        [string[]] $ServiceLines,

        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)]
        [string[]] $LogcatLines
    )

    $propertyPattern = '\[(sys\.boot_completed|sys\.system_server\.start_count|sys\.bootstat\.first_boot_completed|init\.svc\.(system_server|surfaceflinger|bootanim))\]'
    $servicePattern = '(?i)package|activity|window|surfaceflinger|boot|system_server|input'
    $logPattern = '(?i)system_server|SystemServer|PackageManager|Package Manager|ActivityManager|SurfaceFlinger|InputFlinger|boot_completed|BOOT_PROGRESS|FATAL EXCEPTION|AndroidRuntime|Fatal signal|SIGABRT|SIGSEGV|Watchdog|panic|avc: denied'
    $crashContextPattern = '(?i)FATAL EXCEPTION|Fatal signal|SIGABRT|SIGSEGV|Watchdog|system_server is dead'

    $properties = @($PropertyLines | Where-Object { $_ -match $propertyPattern })
    $services = @($ServiceLines | Where-Object { $_ -match $servicePattern })
    $selectedLogIndices = [System.Collections.Generic.List[int]]::new()
    for ($index = 0; $index -lt $LogcatLines.Count; $index++) {
        if ($LogcatLines[$index] -match $logPattern) {
            $selectedLogIndices.Add($index)
        }
        if ($LogcatLines[$index] -match $crashContextPattern) {
            $contextStart = [Math]::Max(0, $index - 8)
            $contextEnd = [Math]::Min($LogcatLines.Count - 1, $index + 12)
            for ($contextIndex = $contextStart; $contextIndex -le $contextEnd; $contextIndex++) {
                $selectedLogIndices.Add($contextIndex)
            }
        }
    }
    $diagnostics = @($selectedLogIndices | Sort-Object -Unique | ForEach-Object { $LogcatLines[$_] } | ForEach-Object {
        $line = $_ -replace '(?i)(authorization\s*[:=]\s*)(?:bearer\s+)?[^\s,;]+', '$1[REDACTED]'
        $line = $line -replace '(?i)(\b(?:access[_-]?token|token|password|api[_-]?key)\b\s*[:=]\s*)[^\s,;&]+', '$1[REDACTED]'
        $line
    })

    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    $statusPath = Join-Path $Directory 'boot-status.txt'
    $propertiesPath = Join-Path $Directory 'boot-properties.txt'
    $servicesPath = Join-Path $Directory 'boot-services.txt'
    $logcatPath = Join-Path $Directory 'boot-logcat.txt'

    @(
        "capturedAt=$([DateTimeOffset]::Now.ToString('o'))"
        "avd=$AvdName"
        "serial=$Serial"
        "bootCompleted=$BootCompleted"
        "packageManagerReady=$PackageManagerReady"
        "captureStatus=$CaptureStatus"
    ) | Set-Content -LiteralPath $statusPath -Encoding UTF8

    if ($properties.Count -eq 0) { $properties = @('No matching Android boot properties were returned.') }
    if ($services.Count -eq 0) { $services = @('No matching Android services were returned.') }
    if ($diagnostics.Count -eq 0) { $diagnostics = @('No matching Android boot diagnostics were returned.') }
    $properties | Set-Content -LiteralPath $propertiesPath -Encoding UTF8
    $services | Set-Content -LiteralPath $servicesPath -Encoding UTF8
    $diagnostics | Set-Content -LiteralPath $logcatPath -Encoding UTF8

    return [pscustomobject]@{
        statusPath = $statusPath
        propertiesPath = $propertiesPath
        servicesPath = $servicesPath
        logcatPath = $logcatPath
    }
}
