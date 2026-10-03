# Cross-publish a production Linux package from Windows using an already built web payload.
[CmdletBinding()]
param([string]$WebDist = 'stage/MulletaFlix-web')
$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$versionText = [System.IO.File]::ReadAllText((Join-Path $repoRoot 'MulletaFlix-master/SharedVersion.cs'))
$version = [regex]::Match($versionText, 'AssemblyFileVersion\("([^"]+)"\)').Groups[1].Value
if (-not $version) { throw 'Server version not found.' }
$webPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $WebDist))
if (-not (Test-Path -LiteralPath (Join-Path $webPath 'index.html'))) { throw 'Built production web payload required.' }
$workDir = Join-Path ([System.IO.Path]::GetTempPath()) ('mulletaflix-linux-package-' + [guid]::NewGuid().ToString('N'))
$appDir = Join-Path $workDir 'mulletaflix-linux-x64'
$serverDir = Join-Path $appDir 'server'
New-Item -ItemType Directory -Path $serverDir -Force | Out-Null
try {
    & dotnet publish (Join-Path $repoRoot 'MulletaFlix-master/Jellyfin.Server/Jellyfin.Server.csproj') -c Release -r linux-x64 --self-contained true -o $serverDir -p:DebugSymbols=false -p:DebugType=none -p:GenerateDocumentationFile=false -p:RunAnalyzersDuringBuild=false -p:RunAnalyzers=false
    if ($LASTEXITCODE -ne 0) { throw "Linux publish failed: $LASTEXITCODE" }
    & (Join-Path $PSScriptRoot 'stage-file-transformation-plugin.ps1') -ApplicationDirectory $serverDir
    Copy-Item -LiteralPath $webPath -Destination (Join-Path $serverDir 'MulletaFlix-web') -Recurse
    foreach ($mapping in @(@('linux-install.sh','install.sh'), @('duckdns-update.sh','duckdns-update.sh'), @('mulletaflix.service','mulletaflix.service'), @('LINUX-README.md','README.md'))) {
        # Linux shell files must have LF endings even when the checkout uses CRLF.
        $content = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot $mapping[0])).Replace("`r`n", "`n")
        [System.IO.File]::WriteAllText((Join-Path $appDir $mapping[1]), $content, [System.Text.UTF8Encoding]::new($false))
    }
    $dist = Join-Path $repoRoot 'dist'
    New-Item -ItemType Directory -Path $dist -Force | Out-Null
    $archive = Join-Path $dist "mulletaflix_${version}_linux-x64.tar.gz"
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
