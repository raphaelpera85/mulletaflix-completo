$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$installerPath = Join-Path $repositoryRoot 'MulletaFlix-packaging-master\MulletaFlix-ux-custom\nsis\mulletaflix.nsi'
$installer = [System.IO.File]::ReadAllText($installerPath, [System.Text.Encoding]::UTF8)
$lines = $installer -split "`r?`n"

if ($lines[0] -ne '# -*- coding: utf-8 -*-') {
    throw 'NSIS source must declare UTF-8 encoding before localized strings.'
}

foreach ($text in @(
    ('Atualizar endere' + [char]0x00E7 + 'o DuckDNS automaticamente'),
    ('Subdom' + [char]0x00ED + 'nio DuckDNS:'),
    'Token DuckDNS:'
)) {
    if (-not $installer.Contains($text)) {
        throw "Installer is missing correctly encoded localized text: $text"
    }
}

if ($installer -notmatch '(?s)\$\{NSD_CreateButton\}.*?Abrir DuckDNS.*?\$\{NSD_OnClick\}.*?OpenDuckDnsPage' -or
    $installer -notmatch '(?s)Function OpenDuckDnsPage\s+ExecShell "open" "https://www\.duckdns\.org/"\s+FunctionEnd') {
    throw 'HTTPS page must expose a fixed DuckDNS creation link.'
}

if ($installer -notmatch '\$\{NSD_CreatePassword\}[^\r\n]*\$_DUCKDNSTOKEN_' -or
    $installer -notmatch '\$\{NSD_GetText\} \$hCtl_duckdns_Token \$_DUCKDNSTOKEN_') {
    throw 'HTTPS page must show and collect the DuckDNS token in a password field.'
}
$passwordControl = [regex]::Match($installer, '\$\{NSD_CreatePassword\}\s+\d+u\s+(?<top>\d+)u\s+\d+u\s+(?<height>\d+)u\s+\$_DUCKDNSTOKEN_')
if (-not $passwordControl.Success -or
    ([int]$passwordControl.Groups['top'].Value + [int]$passwordControl.Groups['height'].Value) -gt 145) {
    throw 'DuckDNS token field must stay inside the visible HTTPS page area.'
}

Write-Host 'Installer localization and DuckDNS controls passed.' -ForegroundColor Green
