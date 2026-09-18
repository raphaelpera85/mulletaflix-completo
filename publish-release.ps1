[CmdletBinding()]
param(
    [string]$Tag = "v12.0.2",
    [string]$Title = "MulletaFlix v12.0.2",
    [string]$ZipPath = "dist\mulletaflix-update-win-x64.zip",
    [string]$ApkPath = ""
)

$ErrorActionPreference = 'Stop'
$projectRoot = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }

if (-not $ApkPath) {
    $versionClean = $Tag.TrimStart('v')
    $candidateApk = Join-Path $projectRoot "dist\mulletaflix-app-v$versionClean.apk"
    if (Test-Path -LiteralPath $candidateApk) {
        $ApkPath = $candidateApk
    } else {
        $candidateLatest = Join-Path $projectRoot "dist\mulletaflix-app.apk"
        if (Test-Path -LiteralPath $candidateLatest) {
            $ApkPath = $candidateLatest
        }
    }
}

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

- **Nebula Downloader & Upload**: Pipeline de upload nativo via Telegram bot pool, MongoDB context e regra de 10% de espaço livre em disco com alternância dinâmica entre unidades de stage.
- **Limpeza Inteligente de Staging**: Remoção automática de diretórios vazios até a raiz de staging quando mídias/sidecars concluídos são removidos.
- **Ingestão de Mídias Físicas**: Suporte aprimorado a mídias locais (.mkv, .mp4) e sidecars associados com proteção contra concorrência e throttle de CPU.
- **Centro de Atualizações Resiliente**: Verificação inteligente de identidade de arquivos (se os arquivos instalados forem idênticos aos da atualização, nenhuma atualização desnecessária é exibida).
- **Correção no Inicializador In-Place**: Execução do processo pós-atualização com WorkingDirectory configurado corretamente para evitar falhas de inicialização.
- **Sync Delta Incremental**: Otimização no backup e restauração do Supabase para nós do Nebula.
- **MulletaFlix Android App**: Aplicativo oficial para Android e Android TV / Box com ExoPlayer/Media3, Material 3 e descoberta automática na LAN.
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

# 4. Upload APK asset (if available)
if ($ApkPath -and (Test-Path -LiteralPath $ApkPath)) {
    $apkItem = Get-Item -LiteralPath $ApkPath
    $apkAssetName = $apkItem.Name

    # Re-fetch release to get updated assets list
    $currentRelease = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/$($release.id)" -Method Get -Headers $headers
    if ($currentRelease.assets) {
        foreach ($asset in $currentRelease.assets) {
            if ($asset.name -eq $apkAssetName) {
                Write-Host "Removendo asset APK antigo $($asset.name) (ID: $($asset.id))..." -ForegroundColor Yellow
                Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/assets/$($asset.id)" -Method Delete -Headers $headers | Out-Null
            }
        }
    }

    Write-Host "Enviando arquivo APK $apkAssetName ($([Math]::Round($apkItem.Length / 1MB, 2)) MB) para o GitHub Releases..." -ForegroundColor Cyan
    $apkUploadUrl = $currentRelease.upload_url -replace '\{\?name,label\}', "?name=$apkAssetName"
    $apkUploadHeaders = @{
        "Authorization" = "Bearer $token"
        "Content-Type" = "application/vnd.android.package-archive"
        "User-Agent" = "MulletaFlix-Release-Script"
    }

    $apkUploadResult = Invoke-RestMethod -Uri $apkUploadUrl -Method Post -Headers $apkUploadHeaders -InFile $ApkPath
    Write-Host "Asset APK enviado com sucesso! Download URL: $($apkUploadResult.browser_download_url)" -ForegroundColor Green
}

Write-Host "`n==================================================" -ForegroundColor Green
Write-Host "Release $Tag publicada com sucesso!" -ForegroundColor Green
Write-Host "URL: $($release.html_url)" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
