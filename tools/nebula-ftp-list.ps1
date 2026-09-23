<#
.SYNOPSIS
    Lista a árvore virtual do Nebula via FTP nativo (fonte autoritativa do drive N:).

.DESCRIPTION
    Usa as credenciais do nebulaftp.xml somente em loopback para conferir o que o
    servidor expõe hoje, independentemente do cache de diretórios do rclone.

.EXAMPLE
    pwsh -File tools/nebula-ftp-list.ps1 -Path '/'
#>
[CmdletBinding()]
param(
    [string]$Path = '/',
    [string]$Server = '127.0.0.1',
    [int]$Port = 2121,
    [string]$ConfigPath = 'C:\Users\Raphael\AppData\Local\MulletaFlix\config\nebulaftp.xml',
    [switch]$Content
)

$ErrorActionPreference = 'Stop'

[xml]$config = Get-Content $ConfigPath -Raw
$user = $config.NebulaFtpConfiguration.Username
$password = $config.NebulaFtpConfiguration.Password

$uri = "ftp://${Server}:${Port}$Path"
$request = [System.Net.FtpWebRequest]::Create($uri)
$request.Method = [System.Net.WebRequestMethods+Ftp]::ListDirectoryDetails
$request.Credentials = New-Object System.Net.NetworkCredential($user, $password)
$request.UsePassive = $true
$request.UseBinary = $true
$request.KeepAlive = $false
$request.Timeout = 120000

$response = $request.GetResponse()
$reader = New-Object System.IO.StreamReader($response.GetResponseStream())
$lines = @()
while (-not $reader.EndOfStream) { $lines += $reader.ReadLine() }
$reader.Close()
$response.Close()

if ($Content) {
    $lines | ForEach-Object { $_ }
    return
}

$directories = @($lines | Where-Object { $_ -match '^d' })
$files = @($lines | Where-Object { $_ -match '^-' })
Write-Host "FTP $Path -> $($lines.Count) entradas ($($directories.Count) pastas, $($files.Count) arquivos)"
$directories | ForEach-Object { ($_ -split '\s+')[-1] } | Sort-Object | ForEach-Object { "  [dir] $_" }
