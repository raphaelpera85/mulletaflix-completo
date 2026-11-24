$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$builderPath = Join-Path $repositoryRoot 'MulletaFlix-packaging-master\build-stage-and-installer.ps1'
$source = [System.IO.File]::ReadAllText($builderPath)
$functionMatch = [regex]::Match($source, '(?s)function Copy-DirectoryContents\s*\{(?<body>.*?)\n\}')
if (-not $functionMatch.Success) {
    throw 'Stage builder must define Copy-DirectoryContents.'
}

$body = $functionMatch.Groups['body'].Value
if ($body -notmatch '(?s)if\s*\(\$robocopyExitCode\s+-ge\s+8\).*?\$global:LASTEXITCODE\s*=\s*0') {
    throw 'Accepted Robocopy success codes must not leak as the stage builder process exit code.'
}

Write-Output 'Accepted Robocopy status codes are normalized before the stage builder returns.'
