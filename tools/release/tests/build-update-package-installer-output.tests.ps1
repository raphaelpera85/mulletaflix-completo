$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$builderPath = Join-Path $repositoryRoot 'build-update-package.ps1'
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($builderPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) {
    throw "Update package builder parse failed: $($parseErrors[0].Message)"
}

$parameterNames = @($ast.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })
if ('InstallerOutputDirectory' -notin $parameterNames) {
    throw 'Update package builder must accept an isolated installer output directory.'
}

$source = [System.IO.File]::ReadAllText($builderPath)
if ($source -notmatch '\$installerOutputDir\s*=\s*if\s*\(\[string\]::IsNullOrWhiteSpace\(\$InstallerOutputDirectory\)\)' -or
    $source -notmatch '& \$installerScript -StageDirectory \$stageDir -OutputDirectory \$installerOutputDir -NoCopyToReleaseAssets') {
    throw 'Installer must be built from the selected stage and output directory without copying to a second implicit destination.'
}

Write-Output 'Update ZIP and installer use the same selected stage and output directory.'
