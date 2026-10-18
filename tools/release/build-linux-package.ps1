# Cross-publish a production Linux package from Windows using an already built web payload.
[CmdletBinding()]
param(
    [string]$WebDist = 'stage/MulletaFlix-web',
    [string]$OutputDirectory = 'dist'
)
$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$versionText = [System.IO.File]::ReadAllText((Join-Path $repoRoot 'MulletaFlix-master/SharedVersion.cs'))
$version = [regex]::Match($versionText, 'AssemblyFileVersion\("([^"]+)"\)').Groups[1].Value
if (-not $version) { throw 'Server version not found.' }
$buildRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot '.build')).TrimEnd('\', '/')
$defaultDist = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'dist')).TrimEnd('\', '/')
$dist = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
}
$dist = $dist.TrimEnd('\', '/')
$insideBuild = $dist.StartsWith($buildRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)
if (-not $dist.Equals($defaultDist, [System.StringComparison]::OrdinalIgnoreCase) -and -not $insideBuild) {
    throw "Linux package output must be the default dist directory or stay inside .build: $dist"
}
if ($insideBuild -and (Test-Path -LiteralPath $buildRoot) -and ((Get-Item -LiteralPath $buildRoot -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
    throw "Build root must not be a symbolic link or junction: $buildRoot"
}
if ($insideBuild) {
    $currentPath = $buildRoot
    foreach ($segment in $dist.Substring($buildRoot.Length + 1).Split([System.IO.Path]::DirectorySeparatorChar)) {
        $currentPath = Join-Path $currentPath $segment
        if ((Test-Path -LiteralPath $currentPath) -and ((Get-Item -LiteralPath $currentPath -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
            throw "Linux package output must not traverse a symbolic link or junction: $currentPath"
        }
    }
}
$expectedArchive = Join-Path $dist "mulletaflix_${version}_linux-x64.tar.gz"
if (Test-Path -LiteralPath $expectedArchive) { throw "Refusing to overwrite an existing Linux package: $expectedArchive" }
$webPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $WebDist))
if (-not (Test-Path -LiteralPath (Join-Path $webPath 'index.html'))) { throw 'Built production web payload required.' }
$workDir = Join-Path ([System.IO.Path]::GetTempPath()) ('mulletaflix-linux-package-' + [guid]::NewGuid().ToString('N'))
$appDir = Join-Path $workDir 'mulletaflix-linux-x64'
$serverDir = Join-Path $appDir 'server'
New-Item -ItemType Directory -Path $serverDir -Force | Out-Null
try {
    & dotnet publish (Join-Path $repoRoot 'MulletaFlix-master/Jellyfin.Server/Jellyfin.Server.csproj') -c Release -r linux-x64 --self-contained true -o $serverDir -p:DebugSymbols=false -p:DebugType=none -p:GenerateDocumentationFile=false -p:RunAnalyzersDuringBuild=false -p:RunAnalyzers=false
    if ($LASTEXITCODE -ne 0) { throw "Linux publish failed: $LASTEXITCODE" }
    & (Join-Path $PSScriptRoot 'copy-nebula-python-runtime.ps1') `
        -SourceDirectory (Join-Path $repoRoot 'MulletaFlix-master/Tools/NebulaPython') `
        -DestinationDirectory (Join-Path $serverDir 'Tools/NebulaPython')
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Nebula Python runtime packaging failed: $LASTEXITCODE" }
    & (Join-Path $PSScriptRoot 'stage-file-transformation-plugin.ps1') -ApplicationDirectory $serverDir
    Copy-Item -LiteralPath $webPath -Destination (Join-Path $serverDir 'MulletaFlix-web') -Recurse
    foreach ($mapping in @(@('linux-install.sh','install.sh'), @('duckdns-update.sh','duckdns-update.sh'), @('mulletaflix.service','mulletaflix.service'), @('LINUX-README.md','README.md'))) {
        # Linux shell files must have LF endings even when the checkout uses CRLF.
        $content = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot $mapping[0])).Replace("`r`n", "`n")
        [System.IO.File]::WriteAllText((Join-Path $appDir $mapping[1]), $content, [System.Text.UTF8Encoding]::new($false))
    }
    New-Item -ItemType Directory -Path $dist -Force | Out-Null
    $archive = Join-Path $dist "mulletaflix_${version}_linux-x64.tar.gz"
    if (Test-Path -LiteralPath $archive) { throw "Refusing to overwrite an existing Linux package: $archive" }
    $output = [System.IO.File]::Create($archive)
    $gzip = [System.IO.Compression.GZipStream]::new($output, [System.IO.Compression.CompressionLevel]::Optimal)
    $writer = [System.Formats.Tar.TarWriter]::new($gzip, $true)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $appDir -File -Recurse) {
            $relative = [System.IO.Path]::GetRelativePath($workDir, $file.FullName).Replace('\', '/')
            $entry = [System.Formats.Tar.PaxTarEntry]::new([System.Formats.Tar.TarEntryType]::RegularFile, $relative)
            $entry.Mode = [System.IO.UnixFileMode]420 # 0644
            if ($relative -match '/(?:install\.sh|server/MulletaFlix)$') { $entry.Mode = [System.IO.UnixFileMode]493 } # 0755
            $input = [System.IO.File]::OpenRead($file.FullName)
            try { $entry.DataStream = $input; $writer.WriteEntry($entry) } finally { $input.Dispose() }
        }
    } finally { $writer.Dispose(); $gzip.Dispose(); $output.Dispose() }
    Get-FileHash -LiteralPath $archive -Algorithm SHA256
} finally {
    $resolvedWork = [System.IO.Path]::GetFullPath($workDir)
    $expectedParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\')
    if ([System.IO.Path]::GetDirectoryName($resolvedWork) -ne $expectedParent -or [System.IO.Path]::GetFileName($resolvedWork) -notlike 'mulletaflix-linux-package-*') { throw 'Refusing unexpected cleanup path.' }
    Remove-Item -LiteralPath $resolvedWork -Recurse -Force
}
