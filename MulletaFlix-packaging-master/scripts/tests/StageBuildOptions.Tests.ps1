$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$builderPath = Join-Path $repositoryRoot 'MulletaFlix-packaging-master\build-stage-and-installer.ps1'
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($builderPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) {
    throw "Stage builder PowerShell parse failed: $($parseErrors[0].Message)"
}

$parameterNames = @($ast.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })
foreach ($requiredParameter in @('StageDirectory', 'BuildDirectory')) {
    if ($requiredParameter -notin $parameterNames) {
        throw "Stage builder is missing isolated output parameter: $requiredParameter"
    }
}

$source = [System.IO.File]::ReadAllText($builderPath)
if ($source -notmatch 'Build output must stay inside \.build' -or
    $source -notmatch 'StageDirectory and BuildDirectory must be separate, non-overlapping directories' -or
    $source -notmatch 'Build root must not be a symbolic link or junction' -or
    $source -notmatch 'Output path must not traverse a symbolic link or junction') {
    throw 'Stage builder does not confine destructive build outputs to isolated, non-overlapping .build directories.'
}
if ($source -notmatch 'New-Item -ItemType Directory -Force -Path \$stageDir' -or
    $source -notmatch 'Initialize-StageBackupRoot\s+-BuildRoot\s+\$buildRoot\s+-StageDirectory\s+\$stageDir' -or
    $source -match 'Join-Path \$projectRoot "stage-backup-\$item"' -or
    $source -notmatch 'New-StageBackupRunDirectory\s+-BackupRoot\s+\$stageBackupRoot' -or
    $source -notmatch 'Restore-OrphanedStageBackups\s+-BackupRoot\s+\$stageBackupRoot' -or
    $source -notmatch 'Enter-StageBackupLock\s+-BackupRoot\s+\$buildRoot' -or
    $source -notmatch '\$backupLock\.Dispose\(\)') {
    throw 'Stage builder must create a missing isolated stage and use isolated, locked, recoverable backups under .build.'
}
$lockPosition = $source.IndexOf('$backupLock = Enter-StageBackupLock -BackupRoot $buildRoot')
$stageFinishPosition = $source.IndexOf("Write-Host 'Stage build finished successfully.'")
$lockReleasePosition = $source.IndexOf('$backupLock.Dispose()')
if ($lockPosition -lt 0 -or $stageFinishPosition -lt $lockPosition -or $lockReleasePosition -lt $stageFinishPosition) {
    throw 'The global stage-build lock must remain held through runtime extras, validation, installer packaging, and final stage completion.'
}
if ($source -notmatch 'Assert-StageIntegrity\s+-StageDirectory\s+\$stageDir\s+-ProjectRoot\s+\$projectRoot\s+-PublishDirectory\s+\$serverPublishDir') {
    throw 'Stage builder does not validate the selected server publish directory.'
}
if ($source -notmatch '\$installerScript\s+-StageDirectory\s+\$stageDir\s+-OutputDirectory\s+\$installerOutputDir' -or
    $source -notmatch 'Installer output must stay inside the selected build directory' -or
    $source -notmatch 'foreach \(\$outputPath in @\(\$normalizedStageDir, \$normalizedBuildDir, \$normalizedInstallerOutputDir\)\)') {
    throw 'Stage builder does not pass and confine the selected installer output directory.'
}
if ($source -notmatch "'mongo_admission\.py'") {
    throw 'Stage builder does not package the Python Mongo admission module imported by main.py.'
}

$installerBuilderPath = Join-Path $repositoryRoot 'build-mulletaflix-installer.ps1'
$installerTokens = $null
$installerParseErrors = $null
$installerAst = [System.Management.Automation.Language.Parser]::ParseFile($installerBuilderPath, [ref]$installerTokens, [ref]$installerParseErrors)
if ($installerParseErrors.Count -gt 0) {
    throw "Installer builder PowerShell parse failed: $($installerParseErrors[0].Message)"
}
$installerParameters = @($installerAst.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })
if ('StageDirectory' -notin $installerParameters -or 'OutputDirectory' -notin $installerParameters -or 'NoCopyToReleaseAssets' -notin $installerParameters) {
    throw 'Installer builder does not accept explicit isolated output and no-publish options.'
}
$installerSource = [System.IO.File]::ReadAllText($installerBuilderPath)
if ($installerSource -notmatch '\[System\.IO\.Path\]::GetFullPath\(\$StageDirectory\)' -or
    $installerSource -notmatch 'Installer stage must stay inside the project root' -or
    $installerSource -notmatch 'Refusing to overwrite an existing installer' -or
    $installerSource -notmatch 'Refusing to overwrite an existing release asset' -or
    $installerSource -notmatch '/DINSTALLER_OUTFILE=') {
    throw 'Installer builder does not normalize its stage, set a deterministic output, or refuse to overwrite existing artifacts.'
}
$versionSource = [System.IO.File]::ReadAllText((Join-Path $repositoryRoot 'MulletaFlix-master\SharedVersion.cs'))
$buildProps = [System.IO.File]::ReadAllText((Join-Path $repositoryRoot 'MulletaFlix-master\Directory.Build.props'))
if ($versionSource -notmatch 'AssemblyVersion\("12\.1\.23"\)' -or
    $versionSource -notmatch 'AssemblyFileVersion\("12\.1\.23"\)' -or
    $buildProps -notmatch '<VersionPrefix>12\.1\.23</VersionPrefix>') {
    throw 'Server assembly and package version metadata are not aligned at 12.1.23.'
}

$validatorPath = Join-Path $repositoryRoot 'MulletaFlix-packaging-master\scripts\validate-stage.ps1'
$validatorSource = [System.IO.File]::ReadAllText($validatorPath)
$validatorTokens = $null
$validatorErrors = $null
$validatorAst = [System.Management.Automation.Language.Parser]::ParseFile($validatorPath, [ref]$validatorTokens, [ref]$validatorErrors)
if ($validatorErrors.Count -gt 0 -or 'PublishDirectory' -notin @($validatorAst.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })) {
    throw 'Stage validator does not expose a valid PublishDirectory parameter.'
}
if ($validatorSource -notmatch 'Assert-StageIntegrity\s+-StageDirectory\s+\$StageDirectory\s+-ProjectRoot\s+\$ProjectRoot\s+-PublishDirectory\s+\$PublishDirectory') {
    throw 'Stage validator does not forward the selected publish directory.'
}

Write-Host 'Stage builder and validator parse, support isolated outputs, constrain destructive outputs to .build, and validate the selected publish.'
