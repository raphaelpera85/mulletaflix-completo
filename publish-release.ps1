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

- **Intro inicia sem esperar o pré-buffer**: buscar e preparar o cache da mídia principal não bloqueia mais a resposta da intro nativa; falhas de cache ficam isoladas e não retiram a intro da sequência. Os logs agora registram quando a intro é fornecida e quantas intros foram resolvidas para cada mídia.

- **Backup MongoDB → Supabase a cada hora**: sincronização automática agora executa em intervalo fixo de 60 minutos. Configurações antigas que impunham 24 horas são migradas para o ciclo horário; usuários do aplicativo continuam fora deste backup.
- **Pré-buffer de reprodução valida a resposta**: a fonte da mídia só é mantida para o player depois que o endpoint de streaming responde com sucesso e entrega bytes; respostas HTTP de erro deixam o player buscar uma fonte nova, em vez de provocar “não foi possível encontrar uma fonte de mídia válida”. A sondagem usa apenas 64 KB e cancela o restante.
- **Falha na intro não bloqueia a mídia**: quando o servidor rejeita a fonte de vídeo da intro nativa, a reprodução não exibe o erro genérico nem engole a rejeição; o player tenta iniciar imediatamente o episódio ou filme já pré-carregado.
- **Reprodução da intro corrigida**: intros nativas resolvidas por caminho agora são registradas como itens transitórios quando a API as entrega ao player. A consulta posterior de PlaybackInfo pelo ID e o streaming usam o mesmo registro, evitando o erro de “não foi possível encontrar uma fonte de mídia válida”.
- **PlaybackInfo POST reconhece a intro transitória**: o endpoint usado pelo player web agora procura a intro no registro temporário antes de tentar resolver um caminho. Isso evita a falha de fonte de mídia para a intro nativa que não está cadastrada na biblioteca persistente.
- **Intro nativa automática antes de cada mídia**: o servidor usa a intro incluída no pacote por padrão, mesmo quando a configuração antiga ainda a marcava como desativada. A tela de Marca agora informa que a execução é automática e mantém o caminho apenas para uma intro personalizada.
- **Pré-cache começa junto com a intro**: ao iniciar a reprodução, o servidor resolve o arquivo STRM da mídia selecionada e baixa em segundo plano todas as partes do Telegram para o cache local temporário, reduzindo esperas e travamentos durante o vídeo. Falhas no pré-cache não interrompem a reprodução.
- **Cache local durante a reprodução Nebula**: ao iniciar uma mídia, o servidor mantém o streaming normal e pré-carrega os blocos do Telegram em `cache\nebula-playback`, servindo os próximos blocos do disco quando disponíveis. O cache é compartilhado entre STRM HTTP e a montagem FTP N:, protegido por sessão ativa e removido após uma hora sem atividade.
- **Dois arquivos nesta release**: o instalador executável `mulletaflix_<versao>_windows-x64.exe`, para instalação limpa em uma máquina nova, e o pacote de atualização in-place `mulletaflix-update-win-x64.zip`, para quem já tem o servidor instalado. Desde a 12.0.63 o instalador executável é publicado junto de toda release de servidor.
- **Varredura de séries não perde mais metadados**: ao regravar um item que já existia e cuja metadata mudou (provedores, campos travados ou trailers), as linhas filhas eram mapeadas apontando de volta para a instância nova do item. O EF seguia essa navegação, tentava rastrear uma segunda instância com o mesmo `Id` de uma linha já carregada do banco e abortava tudo com "cannot be tracked because another instance with the same key value for {'Id'} is already being tracked". O item deixava de ser salvo e a varredura registrava "Error while performing a library operation". A navegação agora é substituída pela chave estrangeira explícita antes da reinserção.
- **Fim do esgotamento do pool do MariaDB**: cada arquivo indexado disparava uma verificação própria que esperava 10 segundos e depois rodava um `RefreshMetadata` completo. Numa varredura de milhares de séries, isso virava milhares de atualizações simultâneas e o pool batia no teto. O sintoma era "Connect Timeout expired. All pooled connections are in use.". As verificações agora passam por um limite de 4 simultâneas.
- **Falha transitória de conexão deixa de descartar o item**: erros de conexão do MariaDB (pool esgotado, timeout, socket resetado) agora entram no retry com backoff maior, em vez de descartar o salvamento do item silenciosamente.
- **MyDramaList para de inundar o log**: o provider agora reporta a primeira recusa 403 uma única vez e fica quieto por 30 minutos, devolvendo resultado vazio sem tocar na rede.
- **Boot do cliente web quase 2 MB mais leve**: o logo de boot era um PNG de 1.003 KB; virou WebP de 53 KB. Tráfego antes do primeiro render: 4.139 KB → 2.243 KB.
- **Log do banco corrigido**: o servidor dizia "MySQL database: mulletaflix" na inicialização; o motor é o MariaDB 11.4 embutido.

---

## 🆕 Novidades desta versão

### 🗄️ MariaDB como único banco de dados

- **SQLite removido por completo** do servidor. O MulletaFlix agora roda exclusivamente em MariaDB.
- Eliminadas todas as referências ao SQLite: provider, NuGet packages, migrações legadas e helpers.
- **IntroSkipper corrigido para MariaDB**: queries reescritas para `IReadOnlySet` — resolvido erro em produção *"ReadOnlySpan<Guid>.op_Implicit could not be translated"*.
- **Ferramenta de migração incluída** (`tools/MulletaFlix.IntroSkipperMigration`): importa bancos SQLite legados para o MariaDB. Idempotente, nunca sobrescreve dados existentes.
- **MariaDB otimizado**: `max_connections 300` · `innodb_io_capacity 2000` · `lock-wait-timeout 25s` · buffer pool 256 MB · redo log 128 MB.

### 📁 Nebula: pastas STRM separadas por categoria

A pasta Nebula agora organiza as mídias em quatro raízes distintas, nesta sequência de download:

| # | Categoria | Pasta STRM |
|---|-----------|-----------|
| 1 | Filmes | `Nebula\Filmes\` |
| 2 | Animações | `Nebula\Animações\` |
| 3 | Séries | `Nebula\Series\` |
| 4 | Novelas | `Nebula\Novelas\` |

- **Novelas e Animações ganham raízes próprias** separadas de Séries.
- **Migração automática no startup**: novelas dentro de `Series` no MongoDB são detectadas e movidas para `Novelas`. Também disponível via `POST /Nebula/Actions/ScanNovelas`.
- **Ciclo de cleanup ajustado de 30 s → 15 min**: elimina a principal causa de pressão de memória em máquinas com ~16 GB.
- **Correção: duplo-upload de arquivos**: a chave de dedupe da fila não era removida ao reenfileirar sidecars, permitindo upload duplicado do mesmo arquivo.
- **Download com envio imediato**: mídias encontradas pelo download que já estão disponíveis localmente e precisam somente de envio agora são registradas no MongoDB e colocadas diretamente na fila de upload do Nebula, sem aguardar o scanner periódico.
- **Identificação automática corrige nome e capa**: GoodShort, ShortMax, DramaFinds e DramaBox agora substituem os metadados e a imagem antigos ao aplicar uma identificação confirmada, evitando que uma capa ou título de outra mídia permaneça no item reconhecido.
- **Título e capa permanecem da mesma fonte reconhecida**: quando a identificação traz um `ProviderId`, o provider correspondente passa a ser a autoridade para nome e imagens; os demais providers só enriquecem campos e completam artes ausentes, evitando título antigo com capa nova ou nova sobrescrita da capa correta.
- **Dashboard informa atualização disponível**: o bloco Servidor agora consulta o status de atualização, mostra a versão disponível ao lado da versão instalada e abre o Centro de Atualizações ao clicar no aviso.

### 🧪 Cobertura de testes

- 254 testes unitários Nebula passando (0 falhas).
- 18 testes de integração IntroSkipper cobrindo registro do plugin e migração SQLite→MariaDB.
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
