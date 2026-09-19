[CmdletBinding()]
param(
    [string]$Tag = "v12.0.9",
    [string]$Title = "MulletaFlix Server v12.0.9",
    [string]$ZipPath = "dist\mulletaflix-update-win-x64.zip"
)

$ErrorActionPreference = 'Stop'
$projectRoot = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }

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

$bodyContent = @"
### MulletaFlix $Tag

- **Roteamento unico no Nebula**: toda midia descoberta para envio ao Telegram passa a terminar sob uma unica raiz de categoria - filmes em ``Filmes``, series em ``Series`` e conteudo adulto em ``Porno`` - independente da arvore de pastas de origem.
- **Fim das raizes duplicadas**: caminhos como ``Series\Series\...`` nao sao mais criados; a raiz de categoria repetida no inicio do caminho e descartada tanto no catalogo (MongoDB) quanto na geracao dos arquivos ``.strm``.
- **Classificacao corrigida**: titulo de filme com "Show", "Temporada" ou "4x100" no nome nao e mais tratado como serie (ano entre parenteses indica filme), e a pasta-raiz declarada prevalece sobre palavras do titulo (ex.: "How to Have Sex (2023)" permanece em ``Filmes``).
- **Cobertura de testes**: novos casos cobrindo filmes guardados em ``Series\Filmes``, raizes duplicadas e classificacao por pasta declarada (191 de 192 testes do Nebula aprovados; a unica falha e anterior a esta versao).
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
