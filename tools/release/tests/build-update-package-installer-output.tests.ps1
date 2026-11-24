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

$manifestModule = Join-Path $repositoryRoot 'tools\release\ReleaseArtifactValidation.psm1'
$manifestSource = [System.IO.File]::ReadAllText($manifestModule)
$stageBuilderPath = Join-Path $repositoryRoot 'MulletaFlix-packaging-master\build-stage-and-installer.ps1'
$stageBuilderSource = [System.IO.File]::ReadAllText($stageBuilderPath)
$releaseWorkflowSource = [System.IO.File]::ReadAllText((Join-Path $repositoryRoot '.github\workflows\release.yml'))
if ($source -notmatch 'Get-MulletaFlixBinaryVersion\s+-Path\s+\$stageServerDll' -or
    $source -notmatch 'if\s*\(\$stageVersion\s+-ne\s+\$requestedVersion\)' -or
    $source -notmatch 'if\s*\(\$installerVersion\s+-ne\s+\$requestedVersion\)' -or
    $source -notmatch 'Assert-MulletaFlixStageManifest\s+-StageDirectory\s+\$stageDir' -or
    $source -notmatch "build-stage-and-installer\.ps1" -or
    $source -notmatch 'BuildDirectory\s*=\s*\$stageBuilderBuildDirectory' -or
    $source -notmatch '\$stageIsInsideBuildRoot' -or
    $source -notmatch 'New-MulletaFlixReleaseManifest' -or
    $source -notmatch 'mulletaflix-release-manifest\.json' -or
    $manifestSource -notmatch 'sourceCommit\s*=\s*\$SourceCommit' -or
    $manifestSource -notmatch 'sha256\s*=\s*\(Get-FileHash' -or
    $stageBuilderSource -notmatch '\$SkipServerBuild\s+-or\s+\$SkipWebBuild' -or
    $stageBuilderSource -notmatch 'restore --worktree -- ''MulletaFlix-web-master/stats\.html''' -or
    $stageBuilderSource -notmatch 'Assert-MulletaFlixStageManifest\s+-StageDirectory\s+\$stageDir' -or
    $stageBuilderSource -notmatch 'New-MulletaFlixStageManifest\s+-StageDirectory\s+\$stageDir' -or
    $releaseWorkflowSource -notmatch '(?s)Publish Server Binaries.{0,700}New-MulletaFlixStageManifest') {
    throw 'Release packaging must bind the selected stage, ZIP, installer, source commit, and SHA-256 values.'
}

Write-Output 'Update ZIP and installer use the selected stage and emit a source-commit/hash manifest.'
