# Instala o rclone localmente quando o executável não foi incluído no pacote.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$toolsDir = Join-Path $PSScriptRoot 'Tools'
$target = Join-Path $toolsDir 'rclone.exe'
$rcloneVersion = '1.75.1'
$rcloneArchiveName = "rclone-v$rcloneVersion-windows-amd64.zip"
$rcloneUrl = "https://github.com/rclone/rclone/releases/download/v$rcloneVersion/$rcloneArchiveName"
$rcloneSha256 = '200EB602C126D82AA38B51E0F6B9AE837473FF99B51278D3F6F837574C494D6E'

if (Test-Path -LiteralPath $target -PathType Leaf) {
    Write-Host "[OK] rclone encontrado em $target." -ForegroundColor Green
    exit 0
}

$archive = Join-Path $env:TEMP "mulletaflix-$rcloneArchiveName"
$extractDir = Join-Path $env:TEMP 'mulletaflix-rclone-extract'
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
    Remove-Item -LiteralPath $extractDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "[INFO] rclone não encontrado. Baixando a versão oficial v$rcloneVersion..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri $rcloneUrl -OutFile $archive -UseBasicParsing
    $actualSha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    if ($actualSha256 -ne $rcloneSha256) {
        throw "O SHA-256 do pacote rclone não confere. Esperado $rcloneSha256, obtido $actualSha256."
    }
    Expand-Archive -LiteralPath $archive -DestinationPath $extractDir -Force
    $downloaded = Get-ChildItem -LiteralPath $extractDir -Filter 'rclone.exe' -Recurse -File | Select-Object -First 1
    if (-not $downloaded) {
        throw 'O pacote baixado não contém rclone.exe.'
    }
    Copy-Item -LiteralPath $downloaded.FullName -Destination $target -Force
    & $target version | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'O rclone instalado não respondeu ao comando version.'
    }
    Write-Host "[OK] rclone disponível em $target." -ForegroundColor Green
    exit 0
}
catch {
    Write-Error "Falha ao instalar rclone: $($_.Exception.Message)"
    exit 1
}
finally {
    Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $extractDir -Recurse -Force -ErrorAction SilentlyContinue
}
