$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$builderPath = Join-Path $repositoryRoot 'MulletaFlix-packaging-master\build-stage-and-installer.ps1'
$source = [System.IO.File]::ReadAllText($builderPath)

$expectedProjectPath = "Join-Path `$projectRoot 'mulletaflix-server-windows-mulletaflix-installer-branding\Jellyfin.Windows.Tray\Jellyfin.Windows.Tray.csproj'"
if (-not $source.Contains($expectedProjectPath)) {
    throw 'Windows release builder must compile the tracked MulletaFlix tray source, not a separate ignored submodule checkout.'
}

Write-Output 'Windows tray build uses the tracked release source.'
