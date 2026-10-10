param(
    [string] $AvdName = 'MulletaflixApi35',
    [int] $Port = 5556
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sdkRoot = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { 'C:\Android\Sdk' }
$adb = Join-Path $sdkRoot 'platform-tools\adb.exe'
$packageName = 'org.mulletaflix.feature.itemdetail.test'
$testRunner = 'androidx.test.runner.AndroidJUnitRunner'
$testClass = 'org.mulletaflix.feature.itemdetail.BookReaderProgressProcessRestartTest'
$testId = [guid]::NewGuid().ToString('D').ToLowerInvariant()
$serial = "emulator-$Port"
$testApkInstalled = $false
$primaryFailure = $null

function Invoke-ProgressPhase {
    param([Parameter(Mandatory = $true)][string] $Method)

    $arguments = @(
        '-s', $serial, 'shell', 'am', 'instrument', '-w',
        '-e', 'class', "$testClass#$Method",
        '-e', 'expectedDeviceProfile', 'PHONE',
        '-e', 'deviceSerial', $serial,
        '-e', 'bookReaderProgressRestartTestId', $testId,
        "$packageName/$testRunner"
    )
    Write-Host "Running reader progress restart phase: $Method"
    $runnerOutput = @(& $adb @arguments)
    $runnerExitCode = $LASTEXITCODE
    $runnerText = $runnerOutput -join [Environment]::NewLine
    Write-Host $runnerText
    if ($runnerExitCode -ne 0 -or $runnerText -notmatch 'OK \(1 test\)' -or
        $runnerText -match 'FAILURES!!!|INSTRUMENTATION_FAILED') {
        throw "Instrumentation phase '$Method' did not report exactly one passing test (adb exit=$runnerExitCode)."
    }
}

function Assert-TargetPackageStopped {
    $processLines = @(& $adb -s $serial shell ps -A)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect Android processes after force-stop.' }
    $targetProcesses = @($processLines | Where-Object {
        $_ -match ('\s' + [regex]::Escape($packageName) + '(?::\S+)?\s*$')
    })
    if ($targetProcesses.Count -gt 0) {
        throw "Target package still has a live process after force-stop: $($targetProcesses -join '; ')"
    }
}

try {
    if (-not (Test-Path -LiteralPath $adb)) { throw "ADB not found: $adb" }
    $device = @(& $adb -s $serial shell getprop ro.kernel.qemu)
    if ($LASTEXITCODE -ne 0 -or $device -notcontains '1') {
        throw "Refusing process-restart test on non-emulator device '$serial'."
    }
    $avdOutput = @(& $adb -s $serial emu avd name 2>$null)
    $reportedAvd = @($avdOutput | Where-Object { $_ -and $_.Trim() -ne 'OK' } | Select-Object -Last 1)
    if ($LASTEXITCODE -ne 0 -or $reportedAvd.Count -ne 1 -or $reportedAvd[0].Trim() -ne $AvdName) {
        throw "Expected AVD '$AvdName' on $serial; emulator reported '$($reportedAvd -join ', ')'."
    }

    . (Join-Path $PSScriptRoot 'Assert-AndroidDeviceProfile.ps1')
    $features = @(& $adb -s $serial shell pm list features)
    $size = @(& $adb -s $serial shell wm size)
    $density = @(& $adb -s $serial shell wm density)
    $actualProfile = Get-AndroidDeviceProfile `
        -FeatureLines $features `
        -DisplaySizeLines $size `
        -DisplayDensityLines $density
    Assert-AndroidDeviceProfile -Expected PHONE -Actual $actualProfile

    Write-Host 'Installing the isolated item-detail AndroidTest target; user app data is not cleared.'
    $gradle = Join-Path $projectRoot 'gradlew.bat'
    & $gradle ':feature:item-detail:installDebugAndroidTest' '--no-daemon' '--console=plain'
    if ($LASTEXITCODE -ne 0) { throw "AndroidTest installation failed with code $LASTEXITCODE." }
    $testApkInstalled = $true

    Invoke-ProgressPhase -Method 'saveProgressBeforeHostProcessRestart'

    & $adb -s $serial shell am force-stop $packageName
    if ($LASTEXITCODE -ne 0) { throw "Could not force-stop isolated AndroidTest target '$packageName'." }
    Assert-TargetPackageStopped

    Invoke-ProgressPhase -Method 'freshViewModelRestoresProgressAfterHostProcessRestart'
    Write-Host "Reader progress survived Android process death for isolated test item $testId."
}
catch {
    $primaryFailure = $_
    throw
}
finally {
    if ($testApkInstalled) {
        try {
            Invoke-ProgressPhase -Method 'cleanupOnlyTheHostProcessRestartEntry'
        } catch {
            if ($null -eq $primaryFailure) {
                throw "UUID-scoped reader progress cleanup failed: $_"
            }
            Write-Warning "UUID-scoped reader progress cleanup also failed after the test failure: $_"
        } finally {
            & $adb -s $serial shell am force-stop $packageName 2>$null | Out-Null
        }
    }
}
