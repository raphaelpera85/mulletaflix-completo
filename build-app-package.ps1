<#
.SYNOPSIS
    Builds the Android application release APK for MulletaFlix (mulletaflix-app-v<Version>.apk).
.DESCRIPTION
    Runs Gradle assembleRelease in MulletaFlix-android, extracts the generated APK,
    and copies it to the dist/ directory.
#>

[CmdletBinding()]
param(
    [string]$Version,
    [string]$OutputDir,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$projectRoot = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
$androidDir = Join-Path $projectRoot 'MulletaFlix-android'

function Normalize-AppVersion {
    param([Parameter(Mandatory)][string]$Value)

    if ($Value -notmatch '^([0-9]+)\.([0-9]+)\.([0-9]+)$') {
        throw "Versão inválida '$Value'. Use o formato semântico X.Y.Z."
    }

    $major = [int64]$Matches[1]
    $minor = [int64]$Matches[2]
    $patch = [int64]$Matches[3]

    # O APK segue SemVer: não criamos versões 1.0.100; fazemos o carry para 1.1.0.
    if ($patch -ge 100) {
        $minor += [math]::Floor($patch / 100)
        $patch = $patch % 100
    }
    if ($minor -ge 100) {
        $major += [math]::Floor($minor / 100)
        $minor = $minor % 100
    }

    return "$major.$minor.$patch"
}

if (-not $Version) {
    $gradleFile = Join-Path $androidDir 'app\build.gradle.kts'
    $versionCatalogFile = Join-Path $androidDir 'gradle\libs.versions.toml'
    if (Test-Path -LiteralPath $gradleFile) {
        $content = Get-Content -LiteralPath $gradleFile -Raw
        if ($content -match 'versionName\s*=\s*"([^"]+)"') {
            $Version = $Matches[1]
        }
    }
    # The app uses the version catalog indirection (`libs.versions.appVersion`)
    # instead of a literal versionName. Read that source of truth before the
    # legacy fallback, otherwise a valid release is mislabeled as 1.0.0.
    if (-not $Version -and (Test-Path -LiteralPath $versionCatalogFile)) {
        $catalog = Get-Content -LiteralPath $versionCatalogFile -Raw
        if ($catalog -match '(?m)^appVersion\s*=\s*"([^"]+)"\s*$') {
            $Version = $Matches[1]
        }
    }
    if (-not $Version) {
        $Version = "1.0.0"
    }
}
$Version = Normalize-AppVersion $Version

if (-not $OutputDir) {
    $OutputDir = Join-Path $projectRoot 'dist'
}

if (-not (Test-Path -LiteralPath $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$apkSource = Join-Path $androidDir 'app\build\outputs\apk\release\app-release.apk'

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "      MulletaFlix Android APK Package Builder     " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

if (-not $SkipBuild) {
    Write-Host "Compilando aplicativo Android (assembleRelease)..." -ForegroundColor Yellow
    $gradleCmd = Join-Path $androidDir 'gradlew.bat'
    $process = Start-Process -FilePath $gradleCmd -ArgumentList ":app:assembleRelease" -WorkingDirectory $androidDir -Wait -PassThru -NoNewWindow
    if ($process.ExitCode -ne 0) {
        throw "Compilação do Android falhou com código de saída $($process.ExitCode)"
    }
}

if (-not (Test-Path -LiteralPath $apkSource)) {
    throw "Arquivo APK não encontrado em: $apkSource"
}

$destApkVersioned = Join-Path $OutputDir "mulletaflix-app-v$Version.apk"
$destApkLatest = Join-Path $OutputDir "mulletaflix-app.apk"

Copy-Item -LiteralPath $apkSource -Destination $destApkVersioned -Force
Copy-Item -LiteralPath $apkSource -Destination $destApkLatest -Force

$item = Get-Item -LiteralPath $destApkVersioned
$hash = (Get-FileHash -Path $destApkVersioned -Algorithm SHA256).Hash

Write-Host "`n==================================================" -ForegroundColor Green
Write-Host "Pacote Android APK gerado com sucesso!" -ForegroundColor Green
Write-Host "Arquivo: $destApkVersioned" -ForegroundColor Cyan
Write-Host "Tamanho: $([Math]::Round($item.Length / 1MB, 2)) MB ($($item.Length) bytes)" -ForegroundColor Cyan
Write-Host "SHA256:  $hash" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Green
