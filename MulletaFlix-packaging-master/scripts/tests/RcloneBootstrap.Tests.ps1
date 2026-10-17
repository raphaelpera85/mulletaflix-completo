$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$nsisPath = Join-Path $repositoryRoot 'MulletaFlix-packaging-master\MulletaFlix-ux-custom\nsis\mulletaflix.nsi'
$stageBuilderPath = Join-Path $repositoryRoot 'MulletaFlix-packaging-master\build-stage-and-installer.ps1'
$stageIntegrityPath = Join-Path $repositoryRoot 'MulletaFlix-packaging-master\scripts\Assert-StageIntegrity.ps1'

$nsis = [System.IO.File]::ReadAllText($nsisPath)
$stageBuilder = [System.IO.File]::ReadAllText($stageBuilderPath)
$stageIntegrity = [System.IO.File]::ReadAllText($stageIntegrityPath)

$bootstrapIndex = $nsis.IndexOf('install-rclone-if-missing.ps1', [System.StringComparison]::OrdinalIgnoreCase)
$winfspIndex = $nsis.IndexOf('install-winfsp-if-missing.ps1', [System.StringComparison]::OrdinalIgnoreCase)
if ($bootstrapIndex -lt 0 -or $winfspIndex -lt 0 -or $bootstrapIndex -ge $winfspIndex) {
    throw 'The NSIS installer must install rclone before checking WinFsp.'
}

$bootstrapBlock = $nsis.Substring($bootstrapIndex, $winfspIndex - $bootstrapIndex)
if ($bootstrapBlock -notmatch 'ExecWait[^\r\n]*install-rclone-if-missing\.ps1') {
    throw 'The NSIS installer references the rclone helper but does not execute it.'
}
if ($bootstrapBlock -notmatch '\$0\s*<>\s*0[\s\S]*?Abort') {
    throw 'The NSIS installer must abort when rclone bootstrap fails.'
}
if ($stageBuilder -notmatch "install-rclone-if-missing\.ps1") {
    throw 'The stage builder does not copy the rclone bootstrap helper.'
}
if ($stageIntegrity -notmatch "'install-rclone-if-missing\.ps1'") {
    throw 'The stage integrity gate does not require the rclone bootstrap helper.'
}

Write-Host 'Rclone bootstrap runs before WinFsp; failure aborts install; helper is staged and required.'
