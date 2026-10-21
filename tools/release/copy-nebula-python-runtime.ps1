[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SourceDirectory,
    [Parameter(Mandatory = $true)][string]$DestinationDirectory
)
$ErrorActionPreference = 'Stop'

foreach ($requiredFile in @('main.py', 'stream_service.py', 'control_plane.py', 'generate_strm.py', 'requirements.in', 'requirements.txt', 'requirements.lock')) {
    if (-not (Test-Path -LiteralPath (Join-Path $SourceDirectory $requiredFile) -PathType Leaf)) {
        throw "Required Nebula Python runtime file missing: $requiredFile"
    }
}
foreach ($requiredFile in @('__init__.py', 'strm_downloader.py', 'mongo_admission.py')) {
    if (-not (Test-Path -LiteralPath (Join-Path $SourceDirectory "tools/$requiredFile") -PathType Leaf)) {
        throw "Required Nebula Python tool missing: $requiredFile"
    }
}

New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null
foreach ($runtimeFile in @('main.py', 'stream_service.py', 'control_plane.py', 'generate_strm.py', 'requirements.in', 'requirements.txt', 'requirements.lock')) {
    Copy-Item -LiteralPath (Join-Path $SourceDirectory $runtimeFile) -Destination $DestinationDirectory
}
foreach ($moduleDirectory in @('ftp', 'tools')) {
    $sourceModules = Join-Path $SourceDirectory $moduleDirectory
    $destinationModules = Join-Path $DestinationDirectory $moduleDirectory
    New-Item -ItemType Directory -Path $destinationModules -Force | Out-Null
    Get-ChildItem -LiteralPath $sourceModules -File -Filter '*.py' | Copy-Item -Destination $destinationModules
}
