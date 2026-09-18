[CmdletBinding()]
param(
    [string]$Tag = "v12.0.7",
    [string]$Title = "MulletaFlix Server v12.0.7",
    [string]$ZipPath = "dist\mulletaflix-update-win-x64.zip"
)

$ErrorActionPreference = 'Stop'
$projectRoot = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }

# 1. Get token from git credential helper
$credOutput = "protocol=https`nhost=github.com`n`n" | git credential fill 2>$null
foreach ($line in $credOutput) {
    if ($line.Trim() -like "password=*") {
        $token = $line.Trim().Substring(9).Trim()
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
### MulletaFlix $Tag

- **Resiliência de Metadados e Concorrência MySQL**: Tratamento de conflitos transitórios (erros 1062, 1452, 1213 e 1205) com retry progressivo e lookup seguro em `ItemPersistenceService`, eliminando falhas durante tarefas em segundo plano (como `StrmProbeScheduledTask`).
- **Validação Completa de Streaming e Transcodificação**: Testes reais executados ponta a ponta com Direct Play/Stream e transcodificação dinâmica HLS em mídias de rede e locais com aceleração por hardware (NVENC).
- **Nebula Downloader & Upload**: Pipeline de upload nativo via Telegram bot pool, MongoDB context e regra de 10% de espaço livre em disco com alternância dinâmica entre unidades de stage.
- **MulletaFlix Android App**: Aplicativo oficial para Android e Android TV / Box com ExoPlayer/Media3, fallback inteligente para transcodificação e reporte contínuo de progresso de reprodução.
"@

$releasePayloadJson = @{
    tag_name = $Tag
    name = $Title
    body = $bodyContent
    draft = $false
    prerelease = $false
    make_latest = "true"
} | ConvertTo-Json

$payloadBytes = [System.Text.Encoding]::UTF8.GetBytes($releasePayloadJson)

if ($existingRelease) {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/$($existingRelease.id)" -Method Patch -Headers $headers -Body $payloadBytes -ContentType "application/json; charset=utf-8"
    Write-Host "Release atualizada com sucesso! ID: $($release.id)" -ForegroundColor Green
} else {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases" -Method Post -Headers $headers -Body $payloadBytes -ContentType "application/json; charset=utf-8"
    Write-Host "Release criada com sucesso! ID: $($release.id)" -ForegroundColor Green
}

# 3. Upload zip asset
$resolvedZipPath = if ([System.IO.Path]::IsPathRooted($ZipPath)) { $ZipPath } else { Join-Path $projectRoot $ZipPath }
if (Test-Path -LiteralPath $resolvedZipPath) {
    $zipItem = Get-Item -LiteralPath $resolvedZipPath
    $assetName = "mulletaflix-update-win-x64.zip"

    if ($release.assets) {
        foreach ($asset in $release.assets) {
            if ($asset.name -eq $assetName) {
                Write-Host "Removendo asset antigo $($asset.name) (ID: $($asset.id))..." -ForegroundColor Yellow
                Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/assets/$($asset.id)" -Method Delete -Headers $headers | Out-Null
            }
        }
    }

    Write-Host "Enviando arquivo $assetName ($([Math]::Round($zipItem.Length / 1MB, 2)) MB) para o GitHub Releases..." -ForegroundColor Cyan
    $uploadUrl = $release.upload_url -replace '\{\?name,label\}', "?name=$assetName"
    $uploadHeaders = @{
        "Authorization" = "Bearer $token"
        "Content-Type" = "application/zip"
        "User-Agent" = "MulletaFlix-Release-Script"
    }

    $uploadResult = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $uploadHeaders -InFile $resolvedZipPath
    Write-Host "Asset zip enviado com sucesso! Download URL: $($uploadResult.browser_download_url)" -ForegroundColor Green
} else {
    Write-Warning "Arquivo zip de atualização não encontrado em: $resolvedZipPath"
}



Write-Host "`n==================================================" -ForegroundColor Green
Write-Host "Release $Tag publicada com sucesso!" -ForegroundColor Green
Write-Host "URL: $($release.html_url)" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
