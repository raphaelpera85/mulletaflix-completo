[CmdletBinding()]
param(
    [string]$Tag,
    [string]$Title,
    [string]$ZipPath = "dist\mulletaflix-update-win-x64.zip",
    [string[]]$AssetPath = @(),
    [string]$AssetsDirectory = "dist",
    [switch]$SkipAssets
)

$ErrorActionPreference = 'Stop'
$projectRoot = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }

# Default the release identity to the server version the checkout currently builds.
# A stale hardcoded tag silently republished an old release, so the version is read
# from SharedVersion.cs unless the caller overrides it explicitly.
if (-not $Tag -or -not $Title) {
    $sharedVersionPath = Join-Path $projectRoot 'MulletaFlix-master\SharedVersion.cs'
    if (-not (Test-Path -LiteralPath $sharedVersionPath)) {
        throw "SharedVersion.cs not found: $sharedVersionPath. Pass -Tag and -Title explicitly."
    }

    $versionMatch = Select-String -LiteralPath $sharedVersionPath -Pattern 'AssemblyFileVersion\("([^"]+)"\)' | Select-Object -First 1
    if (-not $versionMatch) {
        throw "Could not determine the server version from $sharedVersionPath"
    }

    $serverVersion = $versionMatch.Matches[0].Groups[1].Value
    if (-not $Tag) { $Tag = "v$serverVersion" }
    if (-not $Title) { $Title = "MulletaFlix Server v$serverVersion" }
}

Write-Host "Publishing server release $Tag ($Title)" -ForegroundColor Cyan

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

# Single-quoted here-string: release notes are literal text, so nothing here can be
# eaten by backtick escaping or interpolated as a variable.
$bodyContent = @'
### MulletaFlix __TAG__

- **MariaDB como banco unico do servidor**: o SQLite foi removido por completo. O servidor nao cria mais nenhum arquivo `.db` local, e os assemblies do SQLite (Microsoft.Data.Sqlite, Microsoft.EntityFrameworkCore.Sqlite, e_sqlite3) deixaram de ser publicados.
- **IntroSkipper em MariaDB**: o plugin abandonou `introskipper-v2.db` e `introskipper-cache.db`. Segmentos, estado de temporada, registros de analise, fila de projecao e o cache de deteccao passam a viver no schema `mulletaflix_introskipper`, criado automaticamente pelo provider do servidor.
- **Criacao de schema deterministica**: o EF so cria tabelas quando o schema esta totalmente vazio, entao os dois contextos do plugin passaram a criar apenas as suas proprias tabelas, em qualquer ordem de inicializacao.
- **Consultas traduziveis pelo Pomelo**: toda operacao por conjunto de itens (apagar por modo, limpar estado obsoleto, cache) foi reescrita para a forma que o provider MariaDB realmente traduz; sem isso, `ExecuteDelete` falhava em tempo de execucao.
- **Cobertura de testes**: novo projeto `IntroSkipper.Integration.Tests` roda contra MariaDB real e cobre criacao de schema, apagamento por conjunto, cache e wire-up do plugin; a suite completa segue verde.
- **Migracao dos dados antigos**: a release `tools-v1.0.0` traz `mulletaflix-introskipper-migration-tool.zip`, a ferramenta que copia segmentos, tombstones, estado de temporada e cache dos arquivos `introskipper-v2.db` / `introskipper-cache.db` para o schema MariaDB. Comece com `--dry-run`.
- **Banco ajustado para concorrencia**: o MariaDB embutido passa a subir com `max_connections=300` (acima das 200 conexoes que o pool permite abrir), `innodb_io_capacity=2000` (o padrao 200 e de disco mecanico; o diretorio esta em SSD), `innodb_lock_wait_timeout=25` (era 50: o thread travado esperava quase um minuto antes do retry), `innodb_buffer_pool_size=256M` (era 128M) e `innodb_log_file_size=128M` (era 96M). Os valores de memoria sao deliberadamente contidos porque a maquina opera com pouca RAM livre.
- **Log limpo**: o servidor nao reporta mais erro ao nao encontrar o `library.db` legado (o arquivo e do Jellyfin original e nao existe mais), e a tarefa de otimizacao deixou de dizer que faz VACUUM.
'@

$bodyContent = $bodyContent.Replace('__TAG__', $Tag, [System.StringComparison]::Ordinal)

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
# -SkipAssets updates only the release notes; use it when the binaries did not change.
if ($SkipAssets) {
    Write-Host "Assets ignorados (-SkipAssets): apenas os dados da release foram atualizados." -ForegroundColor Yellow
    Write-Host "`n==================================================" -ForegroundColor Green
    Write-Host "Release $Tag atualizada com sucesso!" -ForegroundColor Green
    Write-Host "URL: $($release.html_url)" -ForegroundColor Green
    Write-Host "==================================================" -ForegroundColor Green
    return
}

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

$releaseVersion = $Tag.TrimStart('v')
$assets = $assetCandidates |
    Sort-Object -Unique |
    Where-Object {
        # Do not mix an installer/package from another server release into the
        # current release when dist contains leftovers from a previous build.
        $candidateName = [System.IO.Path]::GetFileName($_)
        if ($candidateName -match '(?i)(?:^|_)(\d+\.\d+\.\d+)(?:_|-)') {
            return $matches[1] -eq $releaseVersion
        }

        return $true
    }
if ($assets.Count -eq 0) {
    throw "Nenhum artefato de servidor encontrado para anexar à release."
}

# Remove versioned server artifacts from an older release left on the same tag.
# Keep unversioned cross-platform packages because they may have been built elsewhere.
if ($release.assets) {
    foreach ($existingAsset in @($release.assets)) {
        $existingVersion = $null
        $existingAssetName = [System.IO.Path]::GetFileName($existingAsset.name)
        if ($existingAssetName -match '(?i)(?:^|_)(\d+\.\d+\.\d+)(?:_|-)') {
            $existingVersion = $matches[1]
        }

        if ($existingVersion -and $existingVersion -ne $releaseVersion) {
            Write-Host "Removendo asset de versão antiga $($existingAsset.name)..." -ForegroundColor Yellow
            Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/assets/$($existingAsset.id)" -Method Delete -Headers $headers | Out-Null
        }
    }
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
