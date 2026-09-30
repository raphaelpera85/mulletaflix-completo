# Reenvia apenas o pacote de atualização para a release já criada.
# O upload de 353 MB caiu duas vezes: uma por queda de conexão do GitHub e
# outra porque o processo foi encerrado junto com o ciclo do agente. Este
# script é idempotente: remove qualquer asset parcial antes de reenviar.
[CmdletBinding()]
param(
    [int]$ReleaseId = 399248696,
    [string]$ZipPath = "dist\mulletaflix-update-win-x64.zip",
    [string]$AssetName = "mulletaflix-update-win-x64.zip",
    [string]$Repo = "raphaelpera85/mulletaflix-completo"
)

$ErrorActionPreference = 'Stop'
$projectRoot = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
$zipFull = Join-Path $projectRoot $ZipPath

if (-not (Test-Path -LiteralPath $zipFull)) {
    throw "Zip nao encontrado: $zipFull"
}

# Mesmo caminho de autenticacao usado por publish-release.ps1.
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
    throw "Nao foi possivel obter o token do GitHub."
}

Write-Host "Token obtido. Verificando assets existentes..." -ForegroundColor Cyan

$headers = @{
    Authorization = "Bearer $token"
    'User-Agent'  = 'mulletaflix-release'
    Accept        = 'application/vnd.github+json'
}

$assets = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/$ReleaseId/assets" -Headers $headers
foreach ($asset in $assets) {
    if ($asset.name -eq $AssetName) {
        Write-Host "Removendo asset parcial id=$($asset.id) state=$($asset.state)" -ForegroundColor Yellow
        Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/assets/$($asset.id)" -Method Delete -Headers $headers | Out-Null
    }
}

$zipItem = Get-Item -LiteralPath $zipFull
Write-Host "Enviando $AssetName ($([math]::Round($zipItem.Length/1MB,2)) MB)..." -ForegroundColor Cyan

# HttpClient com timeout longo: Invoke-RestMethod derruba a conexao em uploads
# grandes, que foi exatamente a falha anterior.
Add-Type -AssemblyName System.Net.Http
$handler = New-Object System.Net.Http.HttpClientHandler
$client = New-Object System.Net.Http.HttpClient($handler)
$client.Timeout = [TimeSpan]::FromMinutes(60)
$client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", $token)
$client.DefaultRequestHeaders.UserAgent.ParseAdd('mulletaflix-release')
$client.DefaultRequestHeaders.Accept.ParseAdd('application/vnd.github+json')

$stream = [System.IO.File]::OpenRead($zipFull)
try {
    $content = New-Object System.Net.Http.StreamContent($stream)
    $content.Headers.ContentType = New-Object System.Net.Http.Headers.MediaTypeHeaderValue('application/zip')
    $uploadUrl = "https://uploads.github.com/repos/$Repo/releases/$ReleaseId/assets?name=$AssetName"

    $response = $client.PostAsync($uploadUrl, $content).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

    Write-Host "HTTP $([int]$response.StatusCode)" -ForegroundColor Cyan
    if (-not $response.IsSuccessStatusCode) {
        Write-Host $body
        throw "Upload falhou com HTTP $([int]$response.StatusCode)"
    }

    Write-Host "Upload concluido com sucesso." -ForegroundColor Green
} finally {
    $stream.Dispose()
    $client.Dispose()
}
