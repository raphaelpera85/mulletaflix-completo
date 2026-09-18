<#
.SYNOPSIS
    Publishes the MulletaFlix Android App release to GitHub Releases.
.DESCRIPTION
    Creates or updates the release for tag app-v<Version> (or attaches to v<Version>),
    and uploads the release APK (mulletaflix-app-v<Version>.apk).
#>

[CmdletBinding()]
param(
    [string]$Tag = "app-v12.0.2",
    [string]$Title = "MulletaFlix Android v12.0.2",
    [string]$ApkPath = "dist\mulletaflix-app-v12.0.2.apk"
)

$ErrorActionPreference = 'Stop'
$projectRoot = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }

# 1. Get token from git credential helper
$inputStr = "protocol=https`nhost=github.com`n`n"
$process = New-Object System.Diagnostics.Process
$process.StartInfo.FileName = "git.exe"
$process.StartInfo.Arguments = "credential fill"
$process.StartInfo.UseShellExecute = $false
$process.StartInfo.RedirectStandardInput = $true
$process.StartInfo.RedirectStandardOutput = $true
$process.StartInfo.CreateNoWindow = $true
$process.Start() | Out-Null
$process.StandardInput.Write($inputStr)
$process.StandardInput.Close()
$output = $process.StandardOutput.ReadToEnd()
$process.WaitForExit()

$token = ""
foreach ($line in ($output -split "`n")) {
    if ($line -like "password=*") {
        $token = $line.Substring(9).Trim()
        break
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

$bodyContent = @"
### MulletaFlix Android $Tag

Aplicativo oficial MulletaFlix para dispositivos Android e Android TV / Box.

#### ✨ Destaques:
- **ExoPlayer & Media3**: Reprodução de alto desempenho para HLS, DASH, MKV e MP4 com suporte a áudio multi-canal e legendas integradas.
- **Descoberta Automática de Servidor**: Detecção de instâncias do MulletaFlix na rede local (LAN) com fallback dinâmico para acesso remoto.
- **Interface Moderna**: Jetpack Compose + Material 3 com 8 temas visuais integrados (Dark, Light, Netflix, Apple TV, Purple Haze, etc.).
- **Autenticação Rápida**: Login tradicional e emparelhamento sem senha via Quick Connect de 6 dígitos.
- **Reprodução Offline & Live TV**: Suporte a download de itens para reprodução offline e canais de Live TV com guia de programação (EPG).
- **Pacote Compacto**: Binário otimizado e minificado via R8 Proguard (~6.96 MB).
"@

$releasePayloadJson = @{
    tag_name = $Tag
    name = $Title
    body = $bodyContent
    draft = $false
    prerelease = $false
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
