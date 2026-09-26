<#
.SYNOPSIS
    Publishes the MulletaFlix Android App release to GitHub Releases.
.DESCRIPTION
    Creates or updates the release for tag app-v<Version> (or attaches to v<Version>),
    and uploads the release APK (mulletaflix-app-v<Version>.apk).
#>

[CmdletBinding()]
param(
    [string]$Version,
    [string]$Tag,
    [string]$Title,
    [string]$ApkPath,
    [string]$Notes
)

$ErrorActionPreference = 'Stop'
$projectRoot = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }

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
    $gradleFile = Join-Path $projectRoot 'MulletaFlix-android\app\build.gradle.kts'
    if (Test-Path -LiteralPath $gradleFile) {
        $content = Get-Content -LiteralPath $gradleFile -Raw
        if ($content -match 'versionName\s*=\s*"([^"]+)"') {
            $Version = $Matches[1]
        }
    }
    if (-not $Version) {
        $Version = "1.0.0"
    }
}
$Version = Normalize-AppVersion $Version

if ([string]::IsNullOrWhiteSpace($Notes)) {
    throw "Notas específicas da release são obrigatórias. Informe -Notes com as melhorias e correções incluídas no APK."
}

if (-not $Tag) {
    $Tag = "app-v$Version"
}
if (-not $Title) {
    $Title = "MulletaFlix Android v$Version"
}
if (-not $ApkPath) {
    $ApkPath = Join-Path $projectRoot "dist\mulletaflix-app-v$Version.apk"
}

# 1. Get token from git credential helper
$token = $env:GITHUB_TOKEN
if (-not $token) {
    $gcm = "C:\Program Files\Git\mingw64\bin\git-credential-manager.exe"
    if (Test-Path -LiteralPath $gcm) {
        $credOutput = @('protocol=https', 'host=github.com', '') | & $gcm get 2>$null
        foreach ($line in $credOutput) {
            if ($line.Trim() -like "password=*") {
                $token = $line.Trim().Substring(9).Trim()
                break
            }
        }
    }
}
if (-not $token) {
    $credOutput = "protocol=https`nhost=github.com`n`n" | git credential fill 2>$null
    foreach ($line in $credOutput) {
        if ($line.Trim() -like "password=*") {
            $token = $line.Trim().Substring(9).Trim()
            break
        }
    }
}

if (-not $token) {
    throw "Não foi possível obter o token do GitHub pelo git credential helper."
}

Write-Host "Autenticado no GitHub com token obtido do Git Credential Manager." -ForegroundColor Green

$headers = @{
    "Authorization" = "Bearer $token"
    "Accept" = "application/vnd.github.v3+json"
    "User-Agent" = "MulletaFlix-Release-Script"
}

$repo = "raphaelpera85/mulletaflix-completo"

# 2. Check if release already exists for this tag
$existingRelease = $null
try {
    $existingRelease = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/tags/$Tag" -Method Get -Headers $headers -ErrorAction Stop
    Write-Host "Release já existe para a tag $Tag (ID: $($existingRelease.id)). Atualizando..." -ForegroundColor Yellow
} catch {
    Write-Host "Criando nova release para a tag $Tag..." -ForegroundColor Cyan
}

# Release notes are supplied by the caller and must describe only changes in
# this APK. Do not prepend generic product copy or a duplicate release heading.
$bodyContent = $Notes.Trim()

$releasePayloadJson = @{
    tag_name = $Tag
    name = $Title
    body = $bodyContent
    draft = $false
    prerelease = $false
    make_latest = "false"
} | ConvertTo-Json

$payloadBytes = [System.Text.Encoding]::UTF8.GetBytes($releasePayloadJson)

if ($existingRelease) {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/$($existingRelease.id)" -Method Patch -Headers $headers -Body $payloadBytes -ContentType "application/json; charset=utf-8"
    Write-Host "Release atualizada com sucesso! ID: $($release.id)" -ForegroundColor Green
} else {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases" -Method Post -Headers $headers -Body $payloadBytes -ContentType "application/json; charset=utf-8"
    Write-Host "Release criada com sucesso! ID: $($release.id)" -ForegroundColor Green
}

# 3. Upload APK asset
$resolvedApkPath = if ([System.IO.Path]::IsPathRooted($ApkPath)) { $ApkPath } else { Join-Path $projectRoot $ApkPath }
if (-not (Test-Path -LiteralPath $resolvedApkPath)) {
    throw "Arquivo APK não encontrado em: $resolvedApkPath"
}

$apkItem = Get-Item -LiteralPath $resolvedApkPath
$assetName = $apkItem.Name

# Check if asset already exists on this release
if ($release.assets) {
    foreach ($asset in $release.assets) {
        if ($asset.name -eq $assetName) {
            Write-Host "Removendo asset antigo $($asset.name) (ID: $($asset.id))..." -ForegroundColor Yellow
            Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/assets/$($asset.id)" -Method Delete -Headers $headers | Out-Null
        }
    }
}

Write-Host "Enviando arquivo $assetName ($([Math]::Round($apkItem.Length / 1MB, 2)) MB) para o GitHub Releases..." -ForegroundColor Cyan

$uploadUrl = $release.upload_url -replace '\{\?name,label\}', "?name=$assetName"

$uploadHeaders = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/vnd.android.package-archive"
    "User-Agent" = "MulletaFlix-Release-Script"
}

$uploadResult = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $uploadHeaders -InFile $resolvedApkPath
Write-Host "Asset APK enviado com sucesso! Download URL: $($uploadResult.browser_download_url)" -ForegroundColor Green

Write-Host "`n==================================================" -ForegroundColor Green
Write-Host "Release Android $Tag publicada com sucesso!" -ForegroundColor Green
Write-Host "URL: $($release.html_url)" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
