[CmdletBinding()]
param(
    [string]$Tag = "v12.0.1",
    [string]$Title = "MulletaFlix v12.0.1",
    [string]$ZipPath = "dist\mulletaflix-update-win-x64.zip"
)

$ErrorActionPreference = 'Stop'

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
### MulletaFlix $Tag

- **Nebula Downloader**: Regra de 10% de espaço livre em disco implementada com alternância automática entre unidades de stage.
- **Centro de Atualizações**: Visualização e instalação direta de pacotes pelo dashboard (`/dashboard/updates`).
- **Resiliência de Disco**: Proteção contra estouro de disco antes do download de arquivos `.strm`.
"@

$releasePayload = @{
    tag_name = $Tag
    name = $Title
    body = $bodyContent
    draft = $false
    prerelease = $false
} | ConvertTo-Json

if ($existingRelease) {
    $release = $existingRelease
} else {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases" -Method Post -Headers $headers -Body $releasePayload -ContentType "application/json"
    Write-Host "Release criada com sucesso! ID: $($release.id)" -ForegroundColor Green
}

# 3. Upload zip asset
if (-not (Test-Path -LiteralPath $ZipPath)) {
    throw "Arquivo zip de atualização não encontrado em: $ZipPath"
}

$zipItem = Get-Item -LiteralPath $ZipPath
$assetName = "mulletaflix-update-win-x64.zip"

# Check if asset already exists on this release
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

$uploadResult = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $uploadHeaders -InFile $ZipPath
Write-Host "Asset enviado com sucesso! Download URL: $($uploadResult.browser_download_url)" -ForegroundColor Green

Write-Host "`n==================================================" -ForegroundColor Green
Write-Host "Release $Tag publicada com sucesso!" -ForegroundColor Green
Write-Host "URL: $($release.html_url)" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
