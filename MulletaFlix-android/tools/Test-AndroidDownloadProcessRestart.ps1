param(
    [string] $AvdName = 'MulletaflixApi35',
    [int] $Port = 5556
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$adb = Join-Path $(if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { 'C:\Android\Sdk' }) 'platform-tools\adb.exe'
$node = (Get-Command node.exe -ErrorAction Stop).Source
$packageName = 'org.mulletaflix.android.debug'
$testPackageName = 'org.mulletaflix.android.debug.test'
$testRunner = 'androidx.test.runner.AndroidJUnitRunner'
$testClass = 'org.mulletaflix.android.service.DownloadProcessRestartIntegrationTest'
$testId = [guid]::NewGuid().ToString()
$serial = "emulator-$Port"
$tempDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "mulletaflix-process-restart-$testId"
$fixtureProcess = $null
$testApksInstalled = $false
$ErrorActionPreference = 'Stop'

function Invoke-GradlePhase {
    param(
        [string] $Method,
        [long] $ResumedOffset = 0,
        [long] $CompletedBytes = 0
    )

    $arguments = @(
        '-s', $serial, 'shell', 'am', 'instrument', '-w',
        '-e', 'class', "$testClass#$Method",
        '-e', 'expectedDeviceProfile', 'PHONE',
        '-e', 'deviceSerial', $serial,
        '-e', 'downloadRestartTestId', $testId,
        '-e', 'downloadRestartMediaUrl', "http://10.0.2.2:$script:fixturePort/media"
    )
    if ($Method -eq 'verifyPartialDownloadResumedByFreshApplicationProcess') {
        $arguments += @('-e', 'downloadRestartObservedOffset', [string]$ResumedOffset,
            '-e', 'downloadRestartCompletedBytes', [string]$CompletedBytes)
    }
    $arguments += "$testPackageName/$testRunner"
    Write-Host "Running Android instrumentation phase via ADB: $Method"
    $runnerOutput = @(& $adb @arguments)
    $runnerExitCode = $LASTEXITCODE
    $runnerText = $runnerOutput -join [Environment]::NewLine
    Write-Host $runnerText
    if ($runnerExitCode -ne 0 -or $runnerText -notmatch 'OK \(1 test\)' -or $runnerText -match 'FAILURES!!!|INSTRUMENTATION_FAILED') {
        throw "Instrumentation phase '$Method' did not report exactly one passing test (adb exit=$runnerExitCode)."
    }
}

try {
    if (-not (Test-Path -LiteralPath $adb)) { throw "ADB not found: $adb" }
    New-Item -ItemType Directory -Path $tempDirectory | Out-Null
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Any, 0)
    $listener.Start()
    $script:fixturePort = $listener.LocalEndpoint.Port
    $listener.Stop()

    $fixtureLog = Join-Path $tempDirectory 'fixture.log'
    $fixtureProcess = Start-Process -FilePath $node `
        -ArgumentList @("`"$(Join-Path $PSScriptRoot 'DownloadProcessRestartFixture.mjs')`"", [string]$script:fixturePort) `
        -RedirectStandardOutput $fixtureLog -RedirectStandardError (Join-Path $tempDirectory 'fixture-error.log') `
        -WindowStyle Hidden -PassThru
    $fixtureReady = $false
    $readyDeadline = (Get-Date).AddSeconds(20)
    do {
        if ($fixtureProcess.HasExited) { throw "Node fixture exited: $(Get-Content -LiteralPath $fixtureLog -Raw -ErrorAction SilentlyContinue)" }
        try {
            if ((Invoke-RestMethod -Uri "http://127.0.0.1:$script:fixturePort/health" -TimeoutSec 2) -eq 'ready') { $fixtureReady = $true }
        } catch { Start-Sleep -Milliseconds 250 }
    } while (-not $fixtureReady -and (Get-Date) -lt $readyDeadline)
    if (-not $fixtureReady) { throw 'The local streaming fixture did not become ready.' }

    $gradle = Join-Path $projectRoot 'gradlew.bat'
    Write-Host 'Installing only the debug app and its instrumentation APK; no test-runner cleanup or data wipe.'
    & $gradle ':app:installDebug' ':app:installDebugAndroidTest' '--no-daemon' '--console=plain'
    if ($LASTEXITCODE -ne 0) { throw "Gradle install tasks failed with code $LASTEXITCODE." }
    $testApksInstalled = $true

    $device = @(& $adb -s $serial shell getprop ro.kernel.qemu)
    if ($LASTEXITCODE -ne 0 -or $device -notcontains '1') { throw "Refusing test on non-emulator device '$serial'." }
    $features = @(& $adb -s $serial shell pm list features)
    $size = @(& $adb -s $serial shell wm size)
    $density = @(& $adb -s $serial shell wm density)
    . (Join-Path $PSScriptRoot 'Assert-AndroidDeviceProfile.ps1')
    $actualProfile = Get-AndroidDeviceProfile -FeatureLines $features -DisplaySizeLines $size -DisplayDensityLines $density
    Assert-AndroidDeviceProfile -Expected PHONE -Actual $actualProfile

    Invoke-GradlePhase -Method 'preparePartialDownloadForHostProcessRestart'

    & $adb -s $serial shell am force-stop $packageName
    if ($LASTEXITCODE -ne 0) { throw 'Could not force-stop the isolated debug app after the first Gradle runner exited.' }
    & $adb -s $serial shell monkey -p $packageName -c android.intent.category.LAUNCHER 1
    if ($LASTEXITCODE -ne 0) { throw 'Could not relaunch the debug app after process death.' }

    $restartDeadline = (Get-Date).AddSeconds(35)
    $restartStatus = $null
    do {
        $restartStatus = Invoke-RestMethod -Uri "http://127.0.0.1:$script:fixturePort/status" -TimeoutSec 5
        if (@($restartStatus.rangeStarts).Count -gt 0) { break }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $restartDeadline)
    if ($restartStatus) {
        Write-Host "Fixture after app relaunch: requestStarts=$(@($restartStatus.requests | ForEach-Object { $_.rangeStart }) -join ', '); resumedOffsets=$(@($restartStatus.rangeStarts) -join ', ')."
    }
    $resumedOffset = [long](@($restartStatus.rangeStarts | Select-Object -Last 1)[0])
    if ($resumedOffset -lt 524288) { throw "No partial byte range was resumed after process death; observed offset=$resumedOffset." }

    $completedBytes = 0L
    $completionDeadline = (Get-Date).AddSeconds(90)
    do {
        $status = Invoke-RestMethod -Uri "http://127.0.0.1:$script:fixturePort/status" -TimeoutSec 5
        $finishedRange = @($status.completedRequests | Where-Object { [long]$_.rangeStart -eq $resumedOffset } | Select-Object -Last 1)
        if ($finishedRange.Count -gt 0) { $completedBytes = [long]$finishedRange[0].bytes; break }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $completionDeadline)
    $expectedRemainingBytes = 16MB - $resumedOffset
    if ($completedBytes -ne $expectedRemainingBytes) { throw "Resumed media response did not complete; rangeOffset=$resumedOffset, expectedBytes=$expectedRemainingBytes, actualBytes=$completedBytes." }

    Invoke-GradlePhase -Method 'verifyPartialDownloadResumedByFreshApplicationProcess' -ResumedOffset $resumedOffset -CompletedBytes $completedBytes
    Write-Host "Process restart resumed at byte $resumedOffset and completed $completedBytes bytes."
}
finally {
    if ($testApksInstalled) {
        try { Invoke-GradlePhase -Method 'cleanupIsolatedHostProcessFixture' }
        catch { Write-Warning "Isolated download cleanup runner failed: $_" }
    }
    if ($fixtureProcess -and -not $fixtureProcess.HasExited) {
        Stop-Process -Id $fixtureProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $tempDirectory) {
        Remove-Item -LiteralPath $tempDirectory -Recurse -Force
    }
}
