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

try {
    $serialPattern = '^\s*' + [regex]::Escape($serial) + '\s'
    $existing = (& $adb devices) | Where-Object { $_ -match $serialPattern }
    if (-not $existing) {
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
    do {
        Start-Sleep -Seconds 3
        if (Get-BootState) { break }
    } while ((Get-Date) -lt $deadline)

    if (-not (Get-BootState)) {
        throw "Emulator $AvdName did not finish booting on $serial."
    }

    & $Command @CommandArgument
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
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
