[CmdletBinding()]
param(
    [string]$StageDirectory,
    [string]$ProjectRoot
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($StageDirectory)) {
    $StageDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) '..\stage'
}

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
}

. (Join-Path $PSScriptRoot 'Assert-StageIntegrity.ps1')
Assert-StageIntegrity -StageDirectory $StageDirectory -ProjectRoot $ProjectRoot
Write-Host "Stage integrity validation passed: $StageDirectory" -ForegroundColor Green
