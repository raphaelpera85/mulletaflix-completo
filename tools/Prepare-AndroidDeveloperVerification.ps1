[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$RegistrationSnippet,
    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'
$script:ExpectedPackageName = 'org.mulletaflix.android'

function Assert-AndroidDeveloperVerificationSnippet {
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw 'Registration snippet cannot be empty.'
    }
}

function Write-AdiRegistrationAsset {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Content
    )

    Assert-AndroidDeveloperVerificationSnippet -Value $Content
    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    [System.IO.File]::WriteAllText(
        $Path,
        $Content,
        [System.Text.UTF8Encoding]::new($false)
    )
}

function Assert-AndroidDeveloperVerificationPackageName {
    param([Parameter(Mandatory)][string]$PackageName)

    if ($PackageName -ne $script:ExpectedPackageName) {
        throw "Unexpected Android package '$PackageName'. Expected '$script:ExpectedPackageName'."
    }
}

function Get-AndroidSdkRoot {
    param([Parameter(Mandatory)][string]$ProjectRoot)

    $candidates = @()
    foreach ($variableName in @('ANDROID_SDK_ROOT', 'ANDROID_HOME')) {
        $candidate = [Environment]::GetEnvironmentVariable($variableName)
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            $candidates += $candidate
        }
    }

    $localPropertiesPath = Join-Path $ProjectRoot 'MulletaFlix-android\local.properties'
    if (Test-Path -LiteralPath $localPropertiesPath) {
        $sdkLine = Select-String -LiteralPath $localPropertiesPath -Pattern '^sdk\.dir=' | Select-Object -First 1
        if ($sdkLine -and $sdkLine.Line -match '^sdk\.dir=(.*)$') {
            $candidates += [regex]::Replace($Matches[1], '\\(.)', '$1')
        }
    }

    $sdkRoot = $candidates | Where-Object {
        Test-Path -LiteralPath (Join-Path $_ 'build-tools')
    } | Select-Object -First 1

    if (-not $sdkRoot) {
        throw 'Android SDK build-tools were not found.'
    }

    return $sdkRoot
}

function Get-LatestAndroidBuildTool {
    param(
        [Parameter(Mandatory)][string]$SdkRoot,
        [Parameter(Mandatory)][string]$ToolName
    )

    $buildTools = Get-ChildItem -LiteralPath (Join-Path $SdkRoot 'build-tools') -Directory |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' } |
        Sort-Object { [version]$_.Name } -Descending

    foreach ($buildTool in $buildTools) {
        $toolPath = Join-Path $buildTool.FullName $ToolName
        if (Test-Path -LiteralPath $toolPath) {
            return $toolPath
        }
    }

    throw "Android build tool '$ToolName' was not found."
}

function Get-ApkPackageName {
    param(
        [Parameter(Mandatory)][string]$AaptPath,
        [Parameter(Mandatory)][string]$ApkPath
    )

    $badging = & $AaptPath dump badging $ApkPath 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "aapt failed to inspect the APK (exit code $LASTEXITCODE)."
    }

    $packageLine = $badging | Where-Object { "$_" -match '^package:' } | Select-Object -First 1
    if (-not $packageLine -or "$packageLine" -notmatch "name='([^']+)'" ) {
        throw 'Could not read the package name from the APK.'
    }

    return $Matches[1]
}

function Invoke-AndroidDeveloperVerificationBuild {
    param(
        [Parameter(Mandatory)][string]$Snippet,
        [string]$DestinationDirectory
    )

    Assert-AndroidDeveloperVerificationSnippet -Value $Snippet

    $projectRoot = Split-Path -Parent $PSScriptRoot
    $androidRoot = Join-Path $projectRoot 'MulletaFlix-android'
    $assetPath = Join-Path $androidRoot 'app\src\main\assets\adi-registration.properties'
    $apkPath = Join-Path $androidRoot 'app\build\outputs\apk\release\app-release.apk'
    $signatureVerifier = Join-Path $projectRoot 'tools\Assert-AndroidApkSignature.ps1'
    $gradle = Join-Path $androidRoot 'gradlew.bat'

    if (Test-Path -LiteralPath $assetPath) {
        throw "Registration asset already exists: $assetPath"
    }

    if (-not $DestinationDirectory) {
        $DestinationDirectory = Join-Path $projectRoot 'dist\android-developer-verification'
    }
    if (-not (Test-Path -LiteralPath $DestinationDirectory)) {
        New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null
    }

    Write-AdiRegistrationAsset -Path $assetPath -Content $Snippet

    try {
        Push-Location $androidRoot
        try {
            & $gradle ':app:verifyProductionSigningCertificate' ':app:assembleRelease'
            if ($LASTEXITCODE -ne 0) {
                throw "Gradle verification build failed (exit code $LASTEXITCODE)."
            }
        }
        finally {
            Pop-Location
        }

        if (-not (Test-Path -LiteralPath $apkPath)) {
            throw "Release APK was not generated: $apkPath"
        }

        & $signatureVerifier -ApkPath $apkPath
        if ($LASTEXITCODE -ne 0) {
            throw "APK signature verification failed (exit code $LASTEXITCODE)."
        }

        $sdkRoot = Get-AndroidSdkRoot -ProjectRoot $projectRoot
        $aapt = Get-LatestAndroidBuildTool -SdkRoot $sdkRoot -ToolName 'aapt.exe'
        $packageName = Get-ApkPackageName -AaptPath $aapt -ApkPath $apkPath
        Assert-AndroidDeveloperVerificationPackageName -PackageName $packageName

        $destinationApk = Join-Path $DestinationDirectory 'mulletaflix-android-developer-verification.apk'
        Copy-Item -LiteralPath $apkPath -Destination $destinationApk -Force

        Write-Host "Verification APK ready: $destinationApk" -ForegroundColor Green
        Write-Host "Package: $packageName" -ForegroundColor Green
        return $destinationApk
    }
    finally {
        Remove-Item -LiteralPath $assetPath -Force -ErrorAction SilentlyContinue
        $assetDirectory = Split-Path -Parent $assetPath
        if ((Test-Path -LiteralPath $assetDirectory) -and -not (Get-ChildItem -LiteralPath $assetDirectory -Force | Select-Object -First 1)) {
            Remove-Item -LiteralPath $assetDirectory -Force -ErrorAction SilentlyContinue
        }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    Invoke-AndroidDeveloperVerificationBuild -Snippet $RegistrationSnippet -DestinationDirectory $OutputDir | Out-Null
}
