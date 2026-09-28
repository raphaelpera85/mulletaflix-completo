param(
    [Parameter(Mandatory = $true)]
    [string] $AvdName,

    [Parameter(Mandatory = $true)]
    [int] $Port,

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

try {
    $serialPattern = '^\s*' + [regex]::Escape($serial) + '\s'
    $existing = (& $adb devices) | Where-Object { $_ -match $serialPattern }
    $existingReady = $existing | Where-Object { $_ -match '\sdevice\s*$' }
    if ($existing -and -not $existingReady) {
        $state = ($existing | ForEach-Object { ($_ -split '\s+')[-1] }) -join ', '
        throw "ADB serial $serial already exists in state '$state'; resolve that device before starting $AvdName."
    }
    if (-not $existingReady) {
        $emulatorProcess = Start-Process -FilePath $emulator -ArgumentList @(
            "-avd", $AvdName,
            "-port", $Port,
            "-no-snapshot",
            "-no-boot-anim",
            "-gpu", "swiftshader_indirect"
        ) -WindowStyle Normal -PassThru
        $startedHere = $true
    }

    $deadline = (Get-Date).AddMinutes(4)
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

    $instrumentedTasks = @(Get-AndroidInstrumentationTasks -CommandArguments $CommandArgument)
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
