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

$setupTypePage = [regex]::Match($installer, '(?s)Function ShowSetupTypePage(?<body>.*?)FunctionEnd').Groups['body'].Value
foreach ($text in @(
    'Basic Install (Desktop; current user)',
    'Runs only after you sign in; the server stops when the session ends.',
    'Install as a Service (Always-on server)',
    'Runs without a user sign-in. Network shares require extra setup.',
    'Recommended for always-on servers.'
)) {
    if (-not $setupTypePage.Contains($text)) {
        throw "Setup type page must explain the startup behavior for: $text"
    }
}
if ($setupTypePage -match 'Basic Install \(Recommended\)|Service Install is not recommended') {
    throw 'Setup type page must not recommend interactive Basic mode as the default for server deployments.'
}

if ($installer -notmatch '(?m)^\s*Page custom ShowSetupTypePage SetupTypePage_Config\s*$') {
    throw 'Setup type page must keep its display and configuration callbacks wired.'
}

$setupTypeCreateIndex = $setupTypePage.IndexOf('Call fnc_setuptype_Create', [System.StringComparison]::Ordinal)
$setupTypeShowIndex = $setupTypePage.IndexOf('nsDialogs::Show', [System.StringComparison]::Ordinal)
if ($setupTypeCreateIndex -lt 0 -or $setupTypeShowIndex -lt 0 -or $setupTypeCreateIndex -ge $setupTypeShowIndex) {
    throw 'Setup type controls must be created before the page is displayed.'
}

$setupTypeLabels = @(
    '$hCtl_setuptype_BasicInstall',
    '$hCtl_setuptype_BasicInstallLabel',
    '$hCtl_setuptype_InstallasaService',
    '$hCtl_setuptype_InstallasaServiceLabel',
    '$hCtl_setuptype_Label1'
)
foreach ($control in $setupTypeLabels) {
    $textIndex = $setupTypePage.IndexOf("`${NSD_SetText} $control ", [System.StringComparison]::Ordinal)
    if ($textIndex -lt $setupTypeCreateIndex -or $textIndex -gt $setupTypeShowIndex) {
        throw "Setup type control $control must receive its runtime text after creation and before display."
    }
}
if (-not $setupTypePage.Contains('; Keep the installed-server guidance here because generated dialog text is stale.')) {
    throw 'Setup type page must document why its runtime labels override the generated dialog text.'
}

$setupTypeConfig = [regex]::Match($installer, '(?s)Function SetupTypePage_Config(?<body>.*?)FunctionEnd').Groups['body'].Value
if ($setupTypeConfig -notmatch '\$\{NSD_GetState\}\s+\$hCtl_setuptype_BasicInstall\s+\$BasicInstall' -or
    $setupTypeConfig -notmatch '\$BasicInstall\s*==\s*1') {
    throw 'Setup type configuration must keep reading the Basic radio control and selecting the matching install path.'
}

$passwordControl = [regex]::Match($installer, '\$\{NSD_CreatePassword\}\s+\d+u\s+(?<top>\d+)u\s+\d+u\s+(?<height>\d+)u\s+\$_DUCKDNSTOKEN_')
if (-not $passwordControl.Success -or
    ([int]$passwordControl.Groups['top'].Value + [int]$passwordControl.Groups['height'].Value) -gt 145) {
    throw 'DuckDNS token field must stay inside the visible HTTPS page area.'
}

Write-Host 'Installer localization and DuckDNS controls passed.' -ForegroundColor Green
