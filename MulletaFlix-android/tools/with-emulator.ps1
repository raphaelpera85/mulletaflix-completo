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

function Get-BootState {
    (& $adb -s $serial shell getprop sys.boot_completed 2>$null) -contains "1"
}

try {
    $existing = (& $adb devices) | Select-String -SimpleMatch $serial
    if (-not $existing) {
        Start-Process -FilePath $emulator -ArgumentList @(
            "-avd", $AvdName,
            "-port", $Port,
            "-no-snapshot",
            "-no-boot-anim",
            "-gpu", "swiftshader_indirect"
        ) -WindowStyle Hidden | Out-Null
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
        & $adb -s $serial emu kill 2>$null | Out-Null
    }
}
