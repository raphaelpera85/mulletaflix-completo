$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$builderPath = Join-Path $repositoryRoot 'tools\release\build-linux-package.ps1'
$tokens = $null
$parseErrors = $null
$builderAst = [System.Management.Automation.Language.Parser]::ParseFile($builderPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) {
    throw "Linux package builder PowerShell parse failed: $($parseErrors[0].Message)"
}

$parameters = @($builderAst.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })
if ('OutputDirectory' -notin $parameters) {
    throw 'Linux package builder must accept an explicit output directory.'
}

$source = [System.IO.File]::ReadAllText($builderPath)
if ($source -notmatch "OutputDirectory\s*=\s*'dist'" -or
    $source -notmatch 'Linux package output must be the default dist directory or stay inside \.build' -or
    $source -notmatch 'must not traverse a symbolic link or junction' -or
    $source -notmatch 'Refusing to overwrite an existing Linux package') {
    throw 'Linux package builder must preserve the default, confine isolated output, reject reparse paths, and refuse overwrite.'
}

Write-Host 'Linux package output defaults to dist and safely supports isolated .build candidates without overwrite.'
