[CmdletBinding()]
param(
    [string]$Tag,
    [string]$Title,
    [string]$ZipPath = "dist\mulletaflix-update-win-x64.zip",
    [string[]]$AssetPath = @(),
    [string]$AssetsDirectory = "dist",
    [switch]$SkipAssets,
    [switch]$AllowMissingInstaller
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
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

- Correção de layout do dashboard: a altura mínima da aplicação agora usa a altura da viewport, evitando que o contêiner principal colapse e oculte a grade de solicitações mesmo quando a API retorna títulos.
'@

$bodyContent = $bodyContent.Replace('__TAG__', $Tag)

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

# The Windows installer executable must ship together with the update package.
# Releases 12.0.46-12.0.62 went out with the update zip only because nothing
# enforced this step, so the check is fail-closed: an operator who really wants a
# packaging-only release must say so explicitly with -AllowMissingInstaller.
$installerAssets = @($assets | Where-Object { [System.IO.Path]::GetFileName($_) -match '(?i)_windows-x64\.exe$' })
if (-not $AllowMissingInstaller -and $installerAssets.Count -eq 0) {
    throw ("Nenhum instalador Windows (mulletaflix_{0}_windows-x64.exe) encontrado em '{1}'. Execute `.\build-mulletaflix-installer.ps1 antes de publicar, ou passe -AllowMissingInstaller para publicar sem o instalador de propósito." -f $releaseVersion, $resolvedAssetsDirectory)
}

foreach ($installerAsset in $installerAssets) {
    Write-Host "Instalador que será anexado: $([System.IO.Path]::GetFileName($installerAsset))" -ForegroundColor Green
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
