[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ApplicationDirectory
)

$ErrorActionPreference = 'Stop'
$pluginVersion = '3.0.1.0'
$pluginFolder = "FileTransformation_$pluginVersion"
$pluginDllSha256 = 'BB941BFD775369F6AC1659CB64AF12DD62AD8F6D74981EB67179D44F26F79590'
$releaseUri = 'https://github.com/IAmParadox27/jellyfin-plugin-file-transformation/releases/download/3.0.1.0/Release-12.1.0.zip'
$expectedSha256 = 'C1318B2438F4C0DBFD46850BCBD3A5C18ECAF6F873A4790318693E0D3DBAA7B3'
$licenseUri = 'https://raw.githubusercontent.com/IAmParadox27/jellyfin-plugin-file-transformation/3.0.1.0/LICENSE'
$licenseSha256 = '3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986'
$applicationDirectory = [System.IO.Path]::GetFullPath($ApplicationDirectory)
$destination = Join-Path $applicationDirectory "bundled-plugins/$pluginFolder"
$downloadDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('mulletaflix-filetransformation-' + [guid]::NewGuid().ToString('N'))
$stagingDirectory = Join-Path $applicationDirectory ('.filetransformation-stage-' + [guid]::NewGuid().ToString('N'))

if (Test-Path -LiteralPath (Join-Path $destination 'meta.json')) {
    $stagedDll = Join-Path $destination 'Jellyfin.Plugin.FileTransformation.dll'
    $stagedFiles = @(
        $stagedDll,
        (Join-Path $destination 'Jellyfin.Plugin.FileTransformation.deps.json'),
        (Join-Path $destination 'logo.png'),
        (Join-Path $destination 'LICENSE-GPL-3.0.txt')
    )
    foreach ($file in $stagedFiles) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            throw "Existing staged File Transformation payload is incomplete: $file"
        }
    }
    $stagedDllHash = if (Test-Path -LiteralPath $stagedDll) { (Get-FileHash -LiteralPath $stagedDll -Algorithm SHA256).Hash } else { '' }
    if ($stagedDllHash -ne $pluginDllSha256) {
        throw "Existing staged File Transformation payload failed integrity verification: $destination"
    }
    if ((Get-FileHash -LiteralPath (Join-Path $destination 'LICENSE-GPL-3.0.txt') -Algorithm SHA256).Hash -ne $licenseSha256) {
        throw "Existing staged File Transformation license failed integrity verification: $destination"
    }
    $stagedManifest = Get-Content -LiteralPath (Join-Path $destination 'meta.json') -Raw | ConvertFrom-Json
    if ($stagedManifest.guid -ne '5e87cc92-571a-4d8d-8d98-d2d4147f9f90' -or $stagedManifest.version -ne $pluginVersion) {
        throw "Existing staged File Transformation manifest failed validation: $destination"
    }
    Write-Host "File Transformation $pluginVersion already staged."
}
else {
New-Item -ItemType Directory -Path $downloadDirectory -Force | Out-Null
try {
    $archive = Join-Path $downloadDirectory 'plugin.zip'
    Invoke-WebRequest -Uri $releaseUri -OutFile $archive
    $actualSha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    if ($actualSha256 -ne $expectedSha256) {
        throw "File Transformation archive checksum mismatch: $actualSha256"
    }
    $licensePath = Join-Path $downloadDirectory 'LICENSE'
    Invoke-WebRequest -Uri $licenseUri -OutFile $licensePath
    if ((Get-FileHash -LiteralPath $licensePath -Algorithm SHA256).Hash -ne $licenseSha256) {
        throw 'File Transformation license checksum mismatch.'
    }

    $extracted = Join-Path $downloadDirectory 'extracted'
    Expand-Archive -LiteralPath $archive -DestinationPath $extracted
    $requiredFiles = @(
        'Jellyfin.Plugin.FileTransformation.dll',
        'Jellyfin.Plugin.FileTransformation.deps.json',
        'logo.png'
    )
    foreach ($file in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $extracted $file))) {
            throw "Official File Transformation package is missing $file"
        }
    }
    if ((Get-FileHash -LiteralPath (Join-Path $extracted $requiredFiles[0]) -Algorithm SHA256).Hash -ne $pluginDllSha256) {
        throw 'File Transformation assembly checksum mismatch.'
    }

    New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
    foreach ($file in $requiredFiles) {
        Copy-Item -LiteralPath (Join-Path $extracted $file) -Destination $stagingDirectory
    }
    Copy-Item -LiteralPath $licensePath -Destination (Join-Path $stagingDirectory 'LICENSE-GPL-3.0.txt')

    $manifest = [ordered]@{
        category = 'General'
        changelog = 'Bundled with MulletaFlix; package source: jellyfin-plugin-file-transformation 3.0.1.0 (Release-12.1.0).'
        description = 'Applies supported transformations to the Jellyfin web interface. Required by Intro Skipper web controls.'
        guid = '5e87cc92-571a-4d8d-8d98-d2d4147f9f90'
        name = 'File Transformation'
        overview = 'Web interface transformation middleware used by Intro Skipper.'
        owner = 'IAmParadox27'
        targetAbi = '12.1.0.0'
        timestamp = [DateTime]::UtcNow.ToString('o')
        version = $pluginVersion
        status = 'Active'
        autoUpdate = $false
        imagePath = 'logo.png'
        assemblies = @('Jellyfin.Plugin.FileTransformation.dll')
    }
    $json = $manifest | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText((Join-Path $stagingDirectory 'meta.json'), $json, [System.Text.UTF8Encoding]::new($false))
    if (Test-Path -LiteralPath $destination) {
        throw "Refusing to replace existing staged plugin folder: $destination"
    }
    $bundleRoot = Split-Path -Parent $destination
    New-Item -ItemType Directory -Path $bundleRoot -Force | Out-Null
    Move-Item -LiteralPath $stagingDirectory -Destination $destination
    Write-Host "Staged verified File Transformation plugin $pluginVersion under $destination."
}
finally {
    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
    $resolvedDownload = [System.IO.Path]::GetFullPath($downloadDirectory)
    $expectedParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\')
    if (([System.IO.Path]::GetDirectoryName($resolvedDownload) -ne $expectedParent) -or ([System.IO.Path]::GetFileName($resolvedDownload) -notlike 'mulletaflix-filetransformation-*')) {
        throw 'Refusing unexpected cleanup path.'
    }
    Remove-Item -LiteralPath $resolvedDownload -Recurse -Force
}
}
