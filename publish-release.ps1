[CmdletBinding()]
param(
    [string]$Tag,
    [string]$Title,
    [string]$NotesPath,
    [string]$TargetCommitish,
    [string]$ZipPath = "dist\mulletaflix-update-win-x64.zip",
    [string[]]$AssetPath = @(),
    [string]$AssetsDirectory = "dist",
    [string]$ManifestPath,
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
if (-not $NotesPath) {
    throw 'NotesPath is required; refusing to publish the default legacy release notes.'
}

Import-Module (Join-Path $projectRoot 'tools\release\ReleaseArtifactValidation.psm1') -Force
$releaseVersion = Get-MulletaFlixReleaseSemVer -Value $Tag
$resolvedAssetsDirectory = if ([System.IO.Path]::IsPathRooted($AssetsDirectory)) { $AssetsDirectory } else { Join-Path $projectRoot $AssetsDirectory }
$resolvedZipPath = if ([System.IO.Path]::IsPathRooted($ZipPath)) { $ZipPath } else { Join-Path $projectRoot $ZipPath }
$resolvedManifestPath = if ($ManifestPath) {
    if ([System.IO.Path]::IsPathRooted($ManifestPath)) { $ManifestPath } else { Join-Path $projectRoot $ManifestPath }
} else {
    Join-Path $resolvedAssetsDirectory 'mulletaflix-release-manifest.json'
}

if (-not $SkipAssets) {
    if ($PSBoundParameters.ContainsKey('ZipPath') -and
        -not $PSBoundParameters.ContainsKey('AssetsDirectory') -and
        -not ([System.IO.Path]::GetFullPath($resolvedZipPath).StartsWith([System.IO.Path]::GetFullPath($resolvedAssetsDirectory).TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase))) {
        throw 'When ZipPath is outside the default assets directory, pass AssetsDirectory for the same build output.'
    }

    $sourceCommit = (& git -C $projectRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'Could not resolve the publishing checkout commit.'
    }
    $tagCommit = (& git -C $projectRoot rev-parse "$Tag^{commit}").Trim()
    if ($LASTEXITCODE -ne 0 -or $tagCommit -ne $sourceCommit) {
        throw "Release tag '$Tag' does not point to the validated source commit."
    }
    $targetRef = if ($TargetCommitish) { $TargetCommitish } else { $sourceCommit }
    $targetCommit = (& git -C $projectRoot rev-parse "$targetRef^{commit}").Trim()
    if ($LASTEXITCODE -ne 0 -or $targetCommit -ne $sourceCommit) {
        throw "Release target '$targetRef' does not resolve to the source commit bound by the build manifest."
    }

    $resolvedExplicitAssetPaths = @($AssetPath | ForEach-Object {
        if ([System.IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path $projectRoot $_ }
    })
    $assets = @(Select-MulletaFlixReleaseAssets `
        -Version $releaseVersion `
        -ZipPath $resolvedZipPath `
        -AssetPaths $resolvedExplicitAssetPaths `
        -AssetsDirectory $resolvedAssetsDirectory `
        -ManifestPath $resolvedManifestPath `
        -SourceCommit $sourceCommit `
        -AllowMissingInstaller:$AllowMissingInstaller)
    $manifest = Get-Content -LiteralPath $resolvedManifestPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    $installerAssets = @($assets | Where-Object { [System.IO.Path]::GetFileName($_) -match '(?i)_windows-x64\.exe$' })
    foreach ($installerAsset in $installerAssets) {
        Write-Host "Installer validated by manifest: $([System.IO.Path]::GetFileName($installerAsset))" -ForegroundColor Green
    }
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
    $statusCode = 0
    if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
        $statusCode = [int]$_.Exception.Response.StatusCode
    }
    if ($statusCode -ne 404) { throw }
    $existingRelease = $null
    Write-Host "Nenhuma release encontrada para a tag $Tag." -ForegroundColor Cyan
}

if ($SkipAssets -and -not $existingRelease) {
    throw "-SkipAssets exige uma release já existente para a tag $Tag."
}
if (-not $SkipAssets -and $existingRelease -and -not $existingRelease.draft) {
    throw "A release $Tag já é pública. Para substituir assets sem expor um estado parcial, use uma nova versão/tag."
}

# Single-quoted here-string: release notes are literal text, so nothing here can be
# eaten by backtick escaping or interpolated as a variable.
$bodyContent = @'
### MulletaFlix __TAG__

Conclusão da **Fase 2 do roadmap — Nebula: fila, concorrência e recuperação**. Esta versão corrige duas causas de perda de progresso em envios concorrentes, elimina uma fonte de bloqueio pelo Telegram e torna a limpeza de arquivos retomável.

#### Correções de concorrência

- **Envio simultâneo podia sobrescrever o progresso de outro trabalhador**: a verificação de posse aceitava um documento sem dono como prova de propriedade. Um trabalhador que travava e voltava depois — já tendo o item sido reatribuído a outro — regravava partes antigas sobre as novas. A posse passou a exigir correspondência explícita, e a adoção de um item sem dono ficou concentrada em uma única operação atômica.
- **Envio interrompido ficava retido por até uma hora**: o único indicador de atividade era a data de modificação, com janela fixa. Agora cada reivindicação registra uma concessão com prazo próprio, renovada a cada parte confirmada: um trabalhador realmente parado libera o item em minutos, enquanto uma transferência longa em andamento não é tomada no meio do caminho.
- **Mídia concluída continuava marcada como reivindicada**: a conclusão não limpava os dados de posse e concessão, o que fazia um prazo vencido sugerir recuperação de trabalho já finalizado.

#### Correções de fila

- **Prioridade era perdida ao reiniciar o servidor**: a preferência de títulos solicitados vivia apenas na memória do processo. Após um reinício, a fila restaurada tratava um título pedido como trabalho comum. A prioridade passou a ser gravada no catálogo e é respeitada na reconciliação da fila.
- **Bloqueios por excesso de chamadas ao Telegram**: limitar apenas quantas transferências ocorrem ao mesmo tempo não impedia disparos instantâneos em sequência no mesmo robô, o que provoca bloqueio temporário de dezenas de segundos. Foi adicionado um controle de taxa por robô, com janela deslizante, que distribui as chamadas entre os robôs disponíveis.
- **Ordem de categorias podia divergir do que a interface exibia**: a sequência estava repetida em três lugares do código. Passou a ter uma fonte única, de modo que alterá-la atualiza ordem e rótulos juntos.

#### Correções de limpeza

- **Limpeza recomeçava do zero após cada interrupção**: a varredura carregava a árvore de arquivos inteira antes de tratar o primeiro item e não guardava o ponto de parada, então encerrar o servidor descartava todo o avanço — pastas muito grandes podiam nunca terminar. A varredura agora é incremental, guarda onde parou e retoma dali, informando quantos arquivos já foram verificados.
- **Escolha do disco de preparação descartava volumes válidos**: a pasta era criada antes de consultar o espaço livre, e uma falha na criação eliminava o disco inteiro da escolha.

#### Validação

1.468 testes automatizados aprovados, 0 falhas (Server 52, Implementations 1.135, LiveTv 69, Api 212). Solução compilada em Release sem erros. A concorrência de fila foi exercitada contra um MongoDB dedicado, com trabalhadores reais disputando o mesmo item, e a atualização da listagem virtual foi verificada contra uma instância real do rclone.

#### Limitações conhecidas

O ciclo com resposta incerta ou tempo esgotado do próprio Telegram não foi exercitado nesta versão: exige credenciais e tráfego de produção.

#### Artefatos

Pacotes de produção Windows x64: atualização (`mulletaflix-update-win-x64.zip`) e instalador (`mulletaflix___TAG___windows-x64.exe`).
'@

$bodyContent = $bodyContent.Replace('__TAG__', $Tag)
if ($NotesPath) {
    $resolvedNotesPath = if ([System.IO.Path]::IsPathRooted($NotesPath)) { $NotesPath } else { Join-Path $projectRoot $NotesPath }
    $bodyContent = [System.IO.File]::ReadAllText($resolvedNotesPath, [System.Text.Encoding]::UTF8)
    if ([string]::IsNullOrWhiteSpace($bodyContent)) { throw 'Release notes must not be empty.' }
}

$draftState = $true
if ($SkipAssets -and $existingRelease) { $draftState = [bool]$existingRelease.draft }
$releaseTarget = if ($TargetCommitish) { $TargetCommitish } elseif ($SkipAssets -and $existingRelease) { $existingRelease.target_commitish } else { $sourceCommit }
$releasePayloadJson = @{
    tag_name = $Tag
    target_commitish = $releaseTarget
    name = $Title
    body = $bodyContent
    draft = $draftState
    prerelease = $false
    make_latest = "true"
} | ConvertTo-Json

$payloadBytes = [System.Text.Encoding]::UTF8.GetBytes($releasePayloadJson)

if ($existingRelease) {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/$($existingRelease.id)" -Method Patch -Headers $headers -Body $payloadBytes -ContentType "application/json; charset=utf-8"
    Write-Host "Release atualizada; draft=$($release.draft). ID: $($release.id)" -ForegroundColor Green
} else {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases" -Method Post -Headers $headers -Body $payloadBytes -ContentType "application/json; charset=utf-8"
    Write-Host "Release criada como rascunho. ID: $($release.id)" -ForegroundColor Green
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

    # Invoke-RestMethod derruba a conexão em uploads grandes ("uma conexão que
    # deveria ser mantida ativa foi fechada pelo servidor"), o que já impediu o
    # zip de ~354 MB de subir em releases anteriores. HttpClient com timeout
    # longo e streaming do arquivo sustenta o envio até o fim.
    Add-Type -AssemblyName System.Net.Http
    $uploadClient = New-Object System.Net.Http.HttpClient
    $uploadClient.Timeout = [TimeSpan]::FromMinutes(60)
    $uploadClient.DefaultRequestHeaders.Authorization =
        New-Object System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", $token)
    $uploadClient.DefaultRequestHeaders.UserAgent.ParseAdd('MulletaFlix-Release-Script')
    $uploadClient.DefaultRequestHeaders.Accept.ParseAdd('application/vnd.github+json')

    $uploadStream = [System.IO.File]::Open($asset.FullName, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    try {
        $record = @($manifest.artifacts | Where-Object { $_.name -ceq $assetName } | Select-Object -First 1)
        if ($record.Count -ne 1) {
            throw "No unique manifest record exists for $assetName."
        }
        $assetLength = [long]$uploadStream.Length
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            $assetHash = [BitConverter]::ToString($sha256.ComputeHash($uploadStream)).Replace('-', '').ToLowerInvariant()
        }
        finally {
            $sha256.Dispose()
        }
        if ($assetLength -ne [long]$record[0].sizeBytes -or $assetHash -cne [string]$record[0].sha256) {
            throw "Asset changed after preflight and was not uploaded: $assetName"
        }
        $uploadStream.Position = 0

        $streamContent = New-Object System.Net.Http.StreamContent($uploadStream)
        $streamContent.Headers.ContentType =
            New-Object System.Net.Http.Headers.MediaTypeHeaderValue($contentType)

        $uploadResponse = $uploadClient.PostAsync($uploadUrl, $streamContent).GetAwaiter().GetResult()
        $uploadBody = $uploadResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $uploadResponse.IsSuccessStatusCode) {
            throw "Falha ao enviar $assetName : HTTP $([int]$uploadResponse.StatusCode) - $uploadBody"
        }

        $uploadResult = $uploadBody | ConvertFrom-Json
        Write-Host "Asset enviado: $($uploadResult.browser_download_url)" -ForegroundColor Green
    } finally {
        $uploadStream.Dispose()
        $uploadClient.Dispose()
    }
}



if (-not $SkipAssets) {
    $verifiedRelease = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/$($release.id)" -Method Get -Headers $headers
    $remoteByName = @{}
    foreach ($remoteAsset in @($verifiedRelease.assets)) {
        $remoteKey = ([string]$remoteAsset.name).ToLowerInvariant()
        if ($remoteByName.ContainsKey($remoteKey)) {
            throw "GitHub release contains duplicate asset name after upload: $($remoteAsset.name)"
        }
        $remoteByName[$remoteKey] = $remoteAsset
    }
    if ($remoteByName.Count -ne $assets.Count) {
        throw "Uploaded release asset count mismatch: expected $($assets.Count), found $($remoteByName.Count). Release remains a draft."
    }
    foreach ($assetPath in $assets) {
        $localAsset = Get-Item -LiteralPath $assetPath -ErrorAction Stop
        $remoteKey = $localAsset.Name.ToLowerInvariant()
        if (-not $remoteByName.ContainsKey($remoteKey) -or [long]$remoteByName[$remoteKey].size -ne [long]$localAsset.Length) {
            throw "Uploaded release asset failed remote name/size verification: $($localAsset.Name). Release remains a draft."
        }
        if ($remoteByName[$remoteKey].digest -and $remoteByName[$remoteKey].digest -cne "sha256:$((Get-FileHash -LiteralPath $localAsset.FullName -Algorithm SHA256).Hash.ToLowerInvariant())") {
            throw "Uploaded release asset SHA-256 mismatch: $($localAsset.Name). Release remains a draft."
        }
    }
    $release = $verifiedRelease
}

Write-Host "`n==================================================" -ForegroundColor Green
if ($release.draft) {
    $publishPayload = @{
        draft = $false
        prerelease = $false
        make_latest = 'true'
    } | ConvertTo-Json
    $publishBytes = [System.Text.Encoding]::UTF8.GetBytes($publishPayload)
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/$($release.id)" -Method Patch -Headers $headers -Body $publishBytes -ContentType "application/json; charset=utf-8"
}

Write-Host "Release $Tag publicada com sucesso!" -ForegroundColor Green
Write-Host "URL: $($release.html_url)" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
