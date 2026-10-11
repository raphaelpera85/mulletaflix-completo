Describe 'build-app-package integration' {
    It 'preserves existing APK outputs when the SDK rejects a synthetic APK' {
        $temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) "MulletaFlix-AppPackage-$([Guid]::NewGuid().ToString('N'))"
        $previousAndroidSdkRoot = $env:ANDROID_SDK_ROOT
        $previousAndroidHome = $env:ANDROID_HOME

        try {
            $toolsDirectory = Join-Path $temporaryRoot 'tools'
            $androidAppDirectory = Join-Path $temporaryRoot 'MulletaFlix-android\app\build\outputs\apk\release'
            $outputDirectory = Join-Path $temporaryRoot 'dist'
            foreach ($directory in @($toolsDirectory, $androidAppDirectory, $outputDirectory)) {
                New-Item -ItemType Directory -Path $directory -Force | Out-Null
            }

            Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\build-app-package.ps1') -Destination $temporaryRoot
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Assert-AndroidApkMetadata.ps1') -Destination $toolsDirectory
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Assert-AndroidApkSignature.ps1') -Destination $toolsDirectory

            $gradleAppDirectory = Join-Path $temporaryRoot 'MulletaFlix-android\app'
            New-Item -ItemType Directory -Path $gradleAppDirectory -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $gradleAppDirectory 'build.gradle.kts') -Value @'
android {
    defaultConfig {
        applicationId = "org.mulletaflix.android"
        versionCode = 383
    }
}
'@

            $apkSource = Join-Path $androidAppDirectory 'app-release.apk'
            $versionedOutput = Join-Path $outputDirectory 'mulletaflix-app-v1.3.83.apk'
            $latestOutput = Join-Path $outputDirectory 'mulletaflix-app.apk'
            Set-Content -LiteralPath $versionedOutput -Value 'previous-versioned-apk' -NoNewline
            Set-Content -LiteralPath $latestOutput -Value 'previous-latest-apk' -NoNewline
            $versionedOutputHash = (Get-FileHash -LiteralPath $versionedOutput -Algorithm SHA256).Hash
            $latestOutputHash = (Get-FileHash -LiteralPath $latestOutput -Algorithm SHA256).Hash

            $repositoryRoot = Split-Path -Parent $PSScriptRoot
            $localProperties = Join-Path $repositoryRoot 'MulletaFlix-android\local.properties'
            $sdkLine = Select-String -LiteralPath $localProperties -Pattern '^sdk\.dir=' | Select-Object -First 1
            if (-not $sdkLine -or $sdkLine.Line -notmatch '^sdk\.dir=(.*)$') {
                throw 'Could not resolve the installed Android SDK from local.properties.'
            }
            $env:ANDROID_SDK_ROOT = [regex]::Replace($Matches[1], '\\(.)', '$1')
            $env:ANDROID_HOME = ''
            if (-not (Test-Path -LiteralPath (Join-Path $env:ANDROID_SDK_ROOT 'build-tools'))) {
                throw "Android SDK build-tools not found at $env:ANDROID_SDK_ROOT."
            }
            $builderPath = Join-Path $temporaryRoot 'build-app-package.ps1'
            Set-Content -LiteralPath $apkSource -Value 'synthetic-invalid-apk' -NoNewline
            $rejected = $false
            $rejectionMessage = ''
            try {
                & $builderPath -Version '1.3.83' -OutputDir $outputDirectory -SkipBuild
            } catch {
                $rejected = $true
                $rejectionMessage = $_.Exception.Message
            }

            $rejected | Should Be $true
            $rejectionMessage | Should Match 'aapt'
            (Get-Content -LiteralPath $versionedOutput -Raw) | Should Be 'previous-versioned-apk'
            (Get-Content -LiteralPath $latestOutput -Raw) | Should Be 'previous-latest-apk'
            (Get-FileHash -LiteralPath $versionedOutput -Algorithm SHA256).Hash | Should Be $versionedOutputHash
            (Get-FileHash -LiteralPath $latestOutput -Algorithm SHA256).Hash | Should Be $latestOutputHash
            @(Get-ChildItem -LiteralPath $outputDirectory -File).Count | Should Be 2
        } finally {
            $env:ANDROID_SDK_ROOT = $previousAndroidSdkRoot
            $env:ANDROID_HOME = $previousAndroidHome
            $resolvedTemporaryRoot = [System.IO.Path]::GetFullPath($temporaryRoot)
            $resolvedTempBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
            if (-not $resolvedTemporaryRoot.StartsWith($resolvedTempBase, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Refusing to remove test fixture outside the temporary directory: $resolvedTemporaryRoot"
            }
            Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force
        }
    }
}
