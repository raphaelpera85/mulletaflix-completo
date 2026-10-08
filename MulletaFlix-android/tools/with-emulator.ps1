param(
    [Parameter(Mandatory = $true)]
    [string] $AvdName,

    [Parameter(Mandatory = $true)]
    [int] $Port,

    [ValidateSet('host', 'auto', 'software', 'swiftshader', 'lavapipe', 'swangle')]
    [string] $GpuMode = 'host',

    [ValidateRange(4, 20)]
    [int] $BootTimeoutMinutes = 4,

    [Parameter(Mandatory = $true, Position = 0)]
    [string] $Command,

    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]] $CommandArgument = @()
)

$ErrorActionPreference = "Stop"
$sdkRoot = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { "C:\Android\Sdk" }
$adb = Join-Path $sdkRoot "platform-tools\adb.exe"
$emulator = Join-Path $sdkRoot "emulator\emulator.exe"
$serial = "emulator-$Port"
$startedHere = $false
$emulatorProcess = $null
$androidProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
. (Join-Path $PSScriptRoot 'Assert-AndroidInstrumentationResults.ps1')
. (Join-Path $PSScriptRoot 'Assert-AndroidDeviceProfile.ps1')
. (Join-Path $PSScriptRoot 'Resolve-AndroidAvdHome.ps1')

$avdSearchDirectories = Get-AndroidAvdSearchDirectories `
    -AndroidAvdHome $env:ANDROID_AVD_HOME `
    -AndroidUserHome $env:ANDROID_USER_HOME `
    -UserProfile $env:USERPROFILE `
    -HomeDirectory $env:HOME `
    -AndroidSdkHome $env:ANDROID_SDK_HOME
$env:ANDROID_AVD_HOME = Resolve-AndroidAvdHome `
    -AvdName $AvdName `
    -SearchDirectories $avdSearchDirectories

function Stop-StartedEmulatorTree {
    param([System.Diagnostics.Process] $RootProcess)

    if (-not $RootProcess) { return }

    # emulator.exe starts qemu-system-x86_64.exe as a child.  Stop only this
    # process tree so a developer's other running AVDs are left untouched.
    $processIds = [System.Collections.Generic.HashSet[int]]::new()
    [void] $processIds.Add($RootProcess.Id)
    do {
        $before = $processIds.Count
        Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
            Where-Object { $processIds.Contains([int]$_.ParentProcessId) } |
            ForEach-Object { [void] $processIds.Add([int]$_.ProcessId) }
    } while ($processIds.Count -gt $before)

    Get-Process -Id @($processIds) -ErrorAction SilentlyContinue |
        Sort-Object Id -Descending |
        Stop-Process -Force -ErrorAction SilentlyContinue
}

function Get-BootState {
    try {
        $state = (& $adb -s $serial shell getprop sys.boot_completed 2>$null)
        return ($state -contains "1")
    } catch {
        # ADB reports the emulator as "offline" briefly while it finishes booting.
        return $false
    }
}

function Get-PackageManagerReady {
    try {
        $packages = (& $adb -s $serial shell cmd package list packages 2>$null)
        return ($LASTEXITCODE -eq 0) -and (@($packages | Where-Object { $_ -match '^package:' }).Count -gt 0)
    } catch {
        return $false
    }
}

function Set-HighPerformanceGpuPreference {
    param([string] $ExecutablePath)

    $preferencesPath = 'HKCU:\Software\Microsoft\DirectX\UserGpuPreferences'
    if (-not (Test-Path $preferencesPath)) {
        New-Item -Path $preferencesPath -Force | Out-Null
    }

    $current = (Get-ItemProperty -Path $preferencesPath -Name $ExecutablePath -ErrorAction SilentlyContinue).$ExecutablePath
    if ($current -match 'GpuPreference=\d+;') {
        $updated = $current -replace 'GpuPreference=\d+;', 'GpuPreference=2;'
    } else {
        $updated = "$current;GpuPreference=2;".TrimStart(';')
    }
    New-ItemProperty -Path $preferencesPath -Name $ExecutablePath -Value $updated -PropertyType String -Force | Out-Null
}

function Assert-EmulatorUsesHighPerformanceNvidiaGpu {
    param([string] $AvdName, [int] $Port, [System.Diagnostics.Process] $RootProcess)

    $reportedAvd = @(& $adb -s $serial emu avd name 2>$null) |
        Where-Object { $_ -and $_.Trim() -notin @('OK') } |
        Select-Object -Last 1
    if ($LASTEXITCODE -ne 0 -or $reportedAvd.Trim() -ne $AvdName) {
        throw "ADB serial $serial is not confirmed as AVD $AvdName (reported '$reportedAvd')."
    }

    $nvidiaSmi = Get-Command 'nvidia-smi' -ErrorAction SilentlyContinue
    if (-not $nvidiaSmi) {
        throw 'nvidia-smi is unavailable; refusing to run Android tests without verifying the dedicated NVIDIA GPU.'
    }

    $gpuNames = & $nvidiaSmi.Source '--query-gpu=name' '--format=csv,noheader' 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw 'nvidia-smi could not identify the installed GPU.'
    }
    if (-not ($gpuNames | Where-Object { $_ -match '^NVIDIA\b' })) {
        throw 'No NVIDIA adapter detected; refusing to run Android tests on an unverified or integrated GPU.'
    }

    $allProcesses = @(Get-CimInstance Win32_Process -ErrorAction Stop)
    $expectedProcessIds = [System.Collections.Generic.HashSet[int]]::new()
    if ($RootProcess) {
        [void] $expectedProcessIds.Add($RootProcess.Id)
        do {
            $before = $expectedProcessIds.Count
            $allProcesses |
                Where-Object { $expectedProcessIds.Contains([int]$_.ParentProcessId) } |
                ForEach-Object { [void] $expectedProcessIds.Add([int]$_.ProcessId) }
        } while ($expectedProcessIds.Count -gt $before)
    }

    $avdArgumentPattern = '(?:^|\s)-avd\s+"?' + [regex]::Escape($AvdName) + '"?(?:\s|$)'
    $portArgumentPattern = '(?:^|\s)-port\s+"?' + [regex]::Escape([string]$Port) + '"?(?:\s|$)'
    $qemuProcesses = @($allProcesses | Where-Object {
        $_.Name -eq 'qemu-system-x86_64.exe' -and
        $_.CommandLine -match $avdArgumentPattern -and
        $_.CommandLine -match $portArgumentPattern
    })
    if ($RootProcess) {
        $targetQemuProcesses = @($qemuProcesses | Where-Object { $expectedProcessIds.Contains([int]$_.ProcessId) })
    } else {
        $targetQemuProcesses = $qemuProcesses
    }
    if ($targetQemuProcesses.Count -ne 1) {
        throw "Could not uniquely identify the QEMU process for AVD $AvdName; close other emulators and run this test through the wrapper."
    }
    $targetQemuPid = [int] $targetQemuProcesses[0].ProcessId

    $deadline = (Get-Date).AddSeconds(30)
    do {
        $gpuStatus = & $nvidiaSmi.Source 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw 'nvidia-smi could not verify Android Emulator GPU usage.'
        }
        foreach ($line in $gpuStatus) {
            if ($line -match '^\s*\|.*\s(?<pid>\d+)\s+(?:C\+)?G\s+.*qemu-system-x86_64\.exe' -and [int]$Matches.pid -eq $targetQemuPid) {
                Write-Host "Verified: AVD $AvdName QEMU PID $targetQemuPid is using the NVIDIA GPU."
                return
            }
        }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)

    throw "NVIDIA GPU is available, but AVD $AvdName QEMU PID $targetQemuPid is not using it. Check Windows Graphics preferences and the NVIDIA driver."
}

$instrumentedTasks = @(Get-AndroidInstrumentationTasks -CommandArguments $CommandArgument)
if ($instrumentedTasks.Count -gt 0 -and $GpuMode -ne 'host') {
    throw 'Instrumented Android tests require -GpuMode host so they cannot silently run on software rendering.'
}

try {
    $serialPattern = '^\s*' + [regex]::Escape($serial) + '\s'
    $existing = (& $adb devices) | Where-Object { $_ -match $serialPattern }
    $existingReady = $existing | Where-Object { $_ -match '\sdevice\s*$' }
    if ($existing -and -not $existingReady) {
        $state = ($existing | ForEach-Object { ($_ -split '\s+')[-1] }) -join ', '
        throw "ADB serial $serial already exists in state '$state'; resolve that device before starting $AvdName."
    }
    if (-not $existingReady) {
        if ($GpuMode -eq 'host') {
            # Prefer the Windows high-performance adapter for emulator graphics only.
            Set-HighPerformanceGpuPreference -ExecutablePath $emulator
            $qemu = Join-Path $sdkRoot 'emulator\qemu\windows-x86_64\qemu-system-x86_64.exe'
            if (Test-Path $qemu) {
                Set-HighPerformanceGpuPreference -ExecutablePath $qemu
            }
        }
        $emulatorProcess = Start-Process -FilePath $emulator -ArgumentList @(
            "-avd", $AvdName,
            "-port", $Port,
            "-no-snapshot",
            "-no-boot-anim",
            "-gpu", $GpuMode
        ) -WindowStyle Normal -PassThru
        $startedHere = $true
    }

    $deadline = (Get-Date).AddMinutes($BootTimeoutMinutes)
    $bootCompleted = $false
    $packageManagerReady = $false
    do {
        Start-Sleep -Seconds 3
        $bootCompleted = Get-BootState
        if ($bootCompleted) {
            $packageManagerReady = Get-PackageManagerReady
            if ($packageManagerReady) { break }
        }
    } while ((Get-Date) -lt $deadline)

    if (-not $bootCompleted) {
        throw "Emulator $AvdName did not finish booting on $serial."
    }
    if (-not $packageManagerReady) {
        throw "Emulator $AvdName booted, but Package Manager did not become ready on $serial."
    }
    if ($GpuMode -eq 'host') {
        Assert-EmulatorUsesHighPerformanceNvidiaGpu -AvdName $AvdName -Port $Port -RootProcess $emulatorProcess
    }

    $instrumentedTestRequested = $instrumentedTasks.Count -gt 0
    $expectedProfile = Get-ExpectedAndroidDeviceProfile `
        -CommandArguments $CommandArgument `
        -Required $instrumentedTestRequested
    if ($expectedProfile) {
        $deviceFeatures = & $adb -s $serial shell pm list features
        if ($LASTEXITCODE -ne 0) { throw "Could not read Android features from $serial." }
        $displaySize = & $adb -s $serial shell wm size
        if ($LASTEXITCODE -ne 0) { throw "Could not read Android display size from $serial." }
        $displayDensity = & $adb -s $serial shell wm density
        if ($LASTEXITCODE -ne 0) { throw "Could not read Android display density from $serial." }
        $actualProfile = Get-AndroidDeviceProfile `
            -FeatureLines $deviceFeatures `
            -DisplaySizeLines $displaySize `
            -DisplayDensityLines $displayDensity
        Assert-AndroidDeviceProfile -Expected $expectedProfile -Actual $actualProfile
    }

    $commandStartedAt = Get-Date
    & $Command @CommandArgument
    $commandExitCode = $LASTEXITCODE
    if ($commandExitCode -ne 0) { exit $commandExitCode }

    foreach ($instrumentedTask in $instrumentedTasks) {
        $modulePath = $instrumentedTask -replace '^:', '' -replace ':connectedDebugAndroidTest$', ''
        $moduleRoot = Join-Path $androidProjectRoot ($modulePath -replace ':', '\')
        $resultDirectory = Join-Path $moduleRoot 'build\outputs\androidTest-results\connected\debug'
        $moduleReports = @(
            Get-ChildItem -LiteralPath $resultDirectory -Filter 'TEST-*.xml' -File -ErrorAction SilentlyContinue |
                Where-Object { $_.LastWriteTime -ge $commandStartedAt }
        )
        Assert-AndroidInstrumentationResults -ReportFiles $moduleReports -StartedAt $commandStartedAt
    }
}
finally {
    if ($startedHere) {
        # Capture and stop descendants before the emulator exits, otherwise
        # the parent relationship can disappear before cleanup is collected.
        Stop-StartedEmulatorTree -RootProcess $emulatorProcess
        try {
            # The emulator may already have exited after Gradle disconnects;
            # cleanup is idempotent and must not turn a successful test run
            # into a false-negative wrapper failure.
            & $adb -s $serial emu kill 2>$null | Out-Null
        } catch {
            # The process tree cleanup above is authoritative in this case.
        }
        Start-Sleep -Milliseconds 500
        Stop-StartedEmulatorTree -RootProcess $emulatorProcess
    }
}
