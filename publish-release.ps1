[CmdletBinding()]
param(
    [string]$Tag = "v12.0.9",
    [string]$Title = "MulletaFlix Server v12.0.9",
    [string]$ZipPath = "dist\mulletaflix-update-win-x64.zip",
    [string[]]$AssetPath = @(),
    [string]$AssetsDirectory = "dist"
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

- **Capas, imagens e NFO preservados no cache**: poster, fanart, logo, thumb, ``.nfo``/``.xml`` e legendas nunca sao removidos do catalogo - nem quando o registro entra em erro - porque sao eles que mantem a biblioteca instantanea no web e no aplicativo.
- **Staging e fila transitoria**: tudo que ja foi enviado ao Telegram sai da pasta de staging, inclusive capas e NFO, e pastas que ficam vazias sao removidas automaticamente.
- **Ja enviado nao volta ao staging**: o exportador de metadados nao recria sidecars de uma midia que ja esta no Telegram, acabando com as capas e ``.nfo`` duplicados na pasta de envio.
- **Sincronizacao por delta**: a varredura do staging passa a processar somente arquivos novos ou alterados (diario de tamanho+data mais indice em uma unica consulta por passada), e a sincronizacao com o Supabase envia apenas o delta: o que mudou desde o ultimo backup mais o que ainda nao foi enviado (fila, staging, envio ou falha).
- **Restauracao nao sobrescreve o cache**: a restauracao do Supabase mescla campo a campo (`$set) em vez de substituir o documento, entao um registro remoto mais pobre nunca apaga partes do Telegram, caminho local ou metadados locais.
- **Cobertura de testes**: novos testes para a regra de conteudo protegido e para a limpeza do staging (833 testes aprovados no projeto de implementacoes).
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

# 3. Upload all server artifacts for this release.
# Builds from other platforms can place their installers/packages in dist or pass
# them explicitly with -AssetPath. Android APKs and evidence images are excluded.
$resolvedAssetsDirectory = if ([System.IO.Path]::IsPathRooted($AssetsDirectory)) { $AssetsDirectory } else { Join-Path $projectRoot $AssetsDirectory }
$assetCandidates = [System.Collections.Generic.List[string]]::new()

if ($AssetPath.Count -gt 0) {
    foreach ($path in $AssetPath) {
        $resolved = if ([System.IO.Path]::IsPathRooted($path)) { $path } else { Join-Path $projectRoot $path }
        if (Test-Path -LiteralPath $resolved -PathType Leaf) {
            $assetCandidates.Add((Get-Item -LiteralPath $resolved).FullName)
        } else {
            throw "Asset informado não encontrado: $resolved"
        }
    }
}

# Preserve the existing -ZipPath contract and always include the update package.
$resolvedZipPath = if ([System.IO.Path]::IsPathRooted($ZipPath)) { $ZipPath } else { Join-Path $projectRoot $ZipPath }
if (Test-Path -LiteralPath $resolvedZipPath -PathType Leaf) {
    $assetCandidates.Add((Get-Item -LiteralPath $resolvedZipPath).FullName)
}

$serverAssetPatterns = @(
    'mulletaflix-update-*.zip',
    'MulletaFlix_*_windows-*.exe',
    'mulletaflix_*_windows-*.exe',
    'MulletaFlix_*_linux-*',
    'mulletaflix_*_linux-*',
    'MulletaFlix_*_macos-*',
    'mulletaflix_*_macos-*',
    '*.deb', '*.rpm', '*.tar.gz', '*.tar.xz', '*.AppImage', '*.dmg', '*.pkg', '*.msi'
)

if (Test-Path -LiteralPath $resolvedAssetsDirectory -PathType Container) {
    foreach ($pattern in $serverAssetPatterns) {
        Get-ChildItem -LiteralPath $resolvedAssetsDirectory -File -Filter $pattern -ErrorAction SilentlyContinue |
            ForEach-Object { $assetCandidates.Add($_.FullName) }
    }
}

$assets = $assetCandidates | Sort-Object -Unique
if ($assets.Count -eq 0) {
    throw "Nenhum artefato de servidor encontrado para anexar à release."
}

$contentTypes = @{
    '.zip' = 'application/zip'; '.exe' = 'application/vnd.microsoft.portable-executable';
    '.deb' = 'application/vnd.debian.binary-package'; '.rpm' = 'application/x-rpm';
    '.gz' = 'application/gzip'; '.xz' = 'application/x-xz'; '.appimage' = 'application/octet-stream';
    '.dmg' = 'application/x-apple-diskimage'; '.pkg' = 'application/octet-stream'; '.msi' = 'application/x-msi'
}

foreach ($assetPath in $assets) {
    $asset = Get-Item -LiteralPath $assetPath
    $assetName = $asset.Name
    $extension = [System.IO.Path]::GetExtension($assetName).ToLowerInvariant()
    $contentType = if ($contentTypes.ContainsKey($extension)) { $contentTypes[$extension] } else { 'application/octet-stream' }

    if ($release.assets) {
        foreach ($existingAsset in @($release.assets)) {
            if ($existingAsset.name -eq $assetName) {
                Write-Host "Removendo asset antigo $($existingAsset.name) (ID: $($existingAsset.id))..." -ForegroundColor Yellow
                Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/assets/$($existingAsset.id)" -Method Delete -Headers $headers | Out-Null
            }
        }
    }

    Write-Host "Enviando asset $assetName ($([Math]::Round($asset.Length / 1MB, 2)) MB)..." -ForegroundColor Cyan
    $uploadUrl = $release.upload_url -replace '\{\?name,label\}', "?name=$([uri]::EscapeDataString($assetName))"
    $uploadHeaders = @{
        "Authorization" = "Bearer $token"
        "Content-Type" = $contentType
        "User-Agent" = "MulletaFlix-Release-Script"
    }

    $uploadResult = Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $uploadHeaders -InFile $asset.FullName
    Write-Host "Asset enviado: $($uploadResult.browser_download_url)" -ForegroundColor Green
}



Write-Host "`n==================================================" -ForegroundColor Green
Write-Host "Release $Tag publicada com sucesso!" -ForegroundColor Green
Write-Host "URL: $($release.html_url)" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
