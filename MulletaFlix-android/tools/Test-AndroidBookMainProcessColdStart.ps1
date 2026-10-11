param(
    [string] $AvdName = 'MulletaflixApi35',
    [int] $Port = 5556
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sdkRoot = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { 'C:\Android\Sdk' }
$adb = Join-Path $sdkRoot 'platform-tools\adb.exe'
$node = (Get-Command node.exe -ErrorAction Stop).Source
$appPackage = 'org.mulletaflix.android.debug'
$testPackage = 'org.mulletaflix.android.debug.test'
$testRunner = 'androidx.test.runner.AndroidJUnitRunner'
$testClass = 'org.mulletaflix.android.MainActivityBookColdStartTest'
$serial = "emulator-$Port"
$testId = [guid]::NewGuid().ToString('N')
$itemId = "cold-start-$testId"
$serverId = "cold-start-server-$testId"
$tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$tempDirectory = Join-Path $tempRoot "mulletaflix-book-cold-start-$testId"
$fixtureProcess = $null
$testApksInstalled = $false
$fixturePort = 0
$script:sessionSnapshotBase64 = $null

function Invoke-TestPhase {
    param([Parameter(Mandatory = $true)][string] $Method)

    $arguments = @(
        '-s', $serial, 'shell', 'am', 'instrument', '-w',
        '-e', 'class', "$testClass#$Method",
        '-e', 'expectedDeviceProfile', 'PHONE'
    )
    if ($Method -eq 'prepareAuthenticatedSessionForColdStart') {
        $arguments += @(
            '-e', 'bookColdStartServerUrl', "http://10.0.2.2:$fixturePort",
            '-e', 'bookColdStartServerId', $serverId
        )
    }
    if ($Method -eq 'authenticatedBookDeepLinkRendersAfterMainProcessRestart') {
        $arguments += @(
            '-e', 'bookColdStartItemId', $itemId,
            '-e', 'bookColdStartServerId', $serverId
        )
    }
    if ($Method -ne 'prepareAuthenticatedSessionForColdStart' -and $script:sessionSnapshotBase64) {
        $arguments += @('-e', 'bookColdStartSessionSnapshot', $script:sessionSnapshotBase64)
    }
    $arguments += "$testPackage/$testRunner"
    Write-Host "Running cold-start instrumentation phase: $Method"
    $output = @(& $adb @arguments)
    $exitCode = $LASTEXITCODE
    $text = $output -join [Environment]::NewLine
    if ($Method -eq 'prepareAuthenticatedSessionForColdStart') {
        $snapshotMatch = [regex]::Match($text, 'BOOK_COLD_START_SESSION_SNAPSHOT=(?<snapshot>[A-Za-z0-9+/=]+)')
        if ($snapshotMatch.Success) { $script:sessionSnapshotBase64 = $snapshotMatch.Groups['snapshot'].Value }
        $text = [regex]::Replace($text, 'BOOK_COLD_START_SESSION_SNAPSHOT=[A-Za-z0-9+/=]+', 'BOOK_COLD_START_SESSION_SNAPSHOT=[REDACTED]')
    }
    Write-Host $text
    if ($exitCode -ne 0 -or $text -notmatch 'OK \(1 test\)' -or
        $text -match 'FAILURES!!!|INSTRUMENTATION_FAILED') {
        throw "Instrumentation phase '$Method' did not report exactly one passing test (adb exit=$exitCode)."
    }
}

function Get-AppProcessIds {
    $output = @(& $adb -s $serial shell pidof $appPackage 2>$null)
    if ($LASTEXITCODE -ne 0 -or $output.Count -eq 0) { return @() }
    return @($output -join ' ' -split '\s+' | Where-Object { $_ -match '^\d+$' })
}

function Assert-AppProcessStopped {
    $deadline = (Get-Date).AddSeconds(15)
    do {
        if (@(Get-AppProcessIds).Count -eq 0) { return }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    throw "Target app process remained after force-stop: $(@(Get-AppProcessIds) -join ', ')."
}

try {
    if (-not (Test-Path -LiteralPath $adb)) { throw "ADB not found: $adb" }
    if (-not (Test-Path -LiteralPath $projectRoot)) { throw "Android project root not found: $projectRoot" }
    New-Item -ItemType Directory -Path $tempDirectory | Out-Null

    $device = @(& $adb -s $serial shell getprop ro.kernel.qemu)
    if ($LASTEXITCODE -ne 0 -or $device -notcontains '1') {
        throw "Refusing process-restart test on non-emulator device '$serial'."
    }
    $reportedAvd = @(& $adb -s $serial emu avd name 2>$null | Where-Object { $_ -and $_.Trim() -ne 'OK' } | Select-Object -Last 1)
    if ($LASTEXITCODE -ne 0 -or $reportedAvd.Count -ne 1 -or $reportedAvd[0].Trim() -ne $AvdName) {
        throw "Expected AVD '$AvdName' on $serial; emulator reported '$($reportedAvd -join ', ')'."
    }
    . (Join-Path $PSScriptRoot 'Assert-AndroidDeviceProfile.ps1')
    $features = @(& $adb -s $serial shell pm list features)
    $size = @(& $adb -s $serial shell wm size)
    $density = @(& $adb -s $serial shell wm density)
    $actualProfile = Get-AndroidDeviceProfile -FeatureLines $features -DisplaySizeLines $size -DisplayDensityLines $density
    Assert-AndroidDeviceProfile -Expected PHONE -Actual $actualProfile

    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Any, 0)
    $listener.Start()
    $fixturePort = [int]$listener.LocalEndpoint.Port
    $listener.Stop()
    $fixtureLog = Join-Path $tempDirectory 'fixture.log'
    $fixtureErrorLog = Join-Path $tempDirectory 'fixture-error.log'
    $fixturePath = Join-Path $PSScriptRoot 'BookReaderColdStartFixture.mjs'
    $fixtureProcess = Start-Process -FilePath $node `
        -ArgumentList @("`"$fixturePath`"", [string]$fixturePort, $itemId, $serverId) `
        -RedirectStandardOutput $fixtureLog `
        -RedirectStandardError $fixtureErrorLog `
        -WindowStyle Hidden -PassThru
    $ready = $false
    $readyDeadline = (Get-Date).AddSeconds(20)
    do {
        if ($fixtureProcess.HasExited) {
            $fixtureErrors = Get-Content -LiteralPath $fixtureErrorLog -Raw -ErrorAction SilentlyContinue
            throw "Book reader fixture exited early: $fixtureErrors"
        }
        try {
            if ((Invoke-RestMethod -Uri "http://127.0.0.1:$fixturePort/health" -TimeoutSec 2) -eq 'ready') { $ready = $true }
        } catch { Start-Sleep -Milliseconds 250 }
    } while (-not $ready -and (Get-Date) -lt $readyDeadline)
    if (-not $ready) { throw 'Book reader cold-start fixture did not become ready.' }

    $gradle = Join-Path $projectRoot 'gradlew.bat'
    Write-Host 'Installing debug APK and AndroidTest APK without clearing app data.'
    & $gradle ':app:installDebug' ':app:installDebugAndroidTest' '--no-daemon' '--console=plain'
    if ($LASTEXITCODE -ne 0) { throw "Gradle installation failed with code $LASTEXITCODE." }
    $testApksInstalled = $true

    Invoke-TestPhase -Method 'prepareAuthenticatedSessionForColdStart'
    $pidBefore = @(Get-AppProcessIds)
    if ($pidBefore.Count -ne 1) {
        throw "Expected one live target app PID after session preparation; found: $($pidBefore -join ', ')."
    }

    & $adb -s $serial shell am force-stop $appPackage
    if ($LASTEXITCODE -ne 0) { throw 'Could not force-stop the main app before its cold start.' }
    Assert-AppProcessStopped
    Write-Host "Confirmed process death: previous PID $($pidBefore[0]) stopped."

    Invoke-TestPhase -Method 'authenticatedBookDeepLinkRendersAfterMainProcessRestart'
    $pidAfter = @(Get-AppProcessIds)
    if ($pidAfter.Count -ne 1 -or $pidAfter[0] -eq $pidBefore[0]) {
        throw "Cold start did not create a different app process; before=$($pidBefore -join ', '), after=$($pidAfter -join ', ')."
    }

    $status = Invoke-RestMethod -Uri "http://127.0.0.1:$fixturePort/status" -TimeoutSec 5
    $paths = @($status.requests | ForEach-Object { $_.path })
    foreach ($requiredPath in @(
        "/Users/book-cold-start-user/Items/$itemId",
        "/BookReader/Items/$itemId/BookReader/Status",
        "/BookReader/Items/$itemId/BookReader/Epub"
    )) {
        if ($requiredPath -notin $paths) { throw "Cold-start fixture did not receive required request '$requiredPath'." }
    }
    Write-Host "Main app cold-start rendered the book; PID $($pidBefore[0]) -> $($pidAfter[0]); required routes were requested."
}
finally {
    if ($testApksInstalled -and $script:sessionSnapshotBase64) {
        try { Invoke-TestPhase -Method 'restoreOriginalSessionAfterColdStartFixture' }
        catch { Write-Warning "Session restoration instrumentation failed: $_" }
    }
    if ($testApksInstalled) {
        & $adb -s $serial shell am force-stop $appPackage 2>$null | Out-Null
    }
    if ($fixtureProcess -and -not $fixtureProcess.HasExited) {
        Stop-Process -Id $fixtureProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $tempDirectory) {
        $resolvedTempRoot = [System.IO.Path]::GetFullPath($tempRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
        $resolvedTempDirectory = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $tempDirectory).Path)
        if (-not $resolvedTempDirectory.StartsWith($resolvedTempRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove temporary path outside the OS temp directory: $resolvedTempDirectory"
        }
        Remove-Item -LiteralPath $resolvedTempDirectory -Recurse -Force
    }
}
