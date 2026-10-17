$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot '..\Assert-StageIntegrity.ps1')

$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "mflx-stage-integrity-$([guid]::NewGuid().ToString('N'))"
$stageDirectory = Join-Path $fixtureRoot 'stage'
$projectRoot = $fixtureRoot
$sourceDownloader = Join-Path $projectRoot 'MulletaFlix-master\Tools\NebulaPython\tools\strm_downloader.py'
$stagedDownloader = Join-Path $stageDirectory 'Tools\NebulaPython\tools\strm_downloader.py'
$publishDirectory = Join-Path $fixtureRoot 'candidate-publish'
$requiredFiles = @(
    'MulletaFlix.exe',
    'MulletaFlix.dll',
    'MulletaFlix.Server.Implementations.dll',
    'MulletaFlix-web\index.html',
    'MulletaFlix-web\serviceworker.js',
    'Tools\mount_drive_n.py',
    'Tools\NebulaPython\main.py',
    'Tools\NebulaPython\stream_service.py',
    'Tools\NebulaPython\generate_strm.py',
    'Tools\NebulaPython\ftp\pathio.py',
    'Tools\NebulaPython\tools\mongo_admission.py',
    'Tools\NebulaPython\requirements.txt',
    'install-rclone-if-missing.ps1',
    'nssm.exe',
    'mulletaflix-windows-tray\MulletaFlix.Windows.Tray.exe'
)

try {
    foreach ($relativePath in $requiredFiles) {
        $path = Join-Path $stageDirectory $relativePath
        [void][System.IO.Directory]::CreateDirectory((Split-Path -Parent $path))
        [System.IO.File]::WriteAllText($path, 'fixture')
    }
    $webAssets = Join-Path $stageDirectory 'MulletaFlix-web\assets'
    [void][System.IO.Directory]::CreateDirectory($webAssets)
    [System.IO.File]::WriteAllText((Join-Path $stageDirectory 'MulletaFlix-web\index.html'), '<script src="./assets/app.js"></script>')
    [System.IO.File]::WriteAllText((Join-Path $webAssets 'app.js'), 'fixture')
    [void][System.IO.Directory]::CreateDirectory((Split-Path -Parent $sourceDownloader))
    [void][System.IO.Directory]::CreateDirectory((Split-Path -Parent $stagedDownloader))
    [System.IO.File]::WriteAllText($sourceDownloader, 'current-source')
    [System.IO.File]::WriteAllText($stagedDownloader, 'current-source')
    $sourceAdmission = Join-Path $projectRoot 'MulletaFlix-master\Tools\NebulaPython\tools\mongo_admission.py'
    $stagedAdmission = Join-Path $stageDirectory 'Tools\NebulaPython\tools\mongo_admission.py'
    [void][System.IO.Directory]::CreateDirectory((Split-Path -Parent $sourceAdmission))
    [void][System.IO.Directory]::CreateDirectory((Split-Path -Parent $stagedAdmission))
    [System.IO.File]::WriteAllText($sourceAdmission, 'current-admission')
    [System.IO.File]::WriteAllText($stagedAdmission, 'current-admission')
    [void][System.IO.Directory]::CreateDirectory($publishDirectory)
    [void][System.IO.Directory]::CreateDirectory((Join-Path $projectRoot '.build\server-publish'))
    foreach ($assemblyName in @('MediaBrowser.Model.dll', 'MulletaFlix.Server.Implementations.dll', 'MulletaFlix.dll')) {
        [System.IO.File]::WriteAllText((Join-Path $publishDirectory $assemblyName), 'candidate-publish')
        [System.IO.File]::WriteAllText((Join-Path $stageDirectory $assemblyName), 'candidate-publish')
        [System.IO.File]::WriteAllText((Join-Path (Join-Path $projectRoot '.build\server-publish') $assemblyName), 'stale-default-publish')
    }

    Assert-StageIntegrity -StageDirectory $stageDirectory -ProjectRoot $projectRoot -PublishDirectory $publishDirectory

    [System.IO.File]::WriteAllText($stagedDownloader, 'stale-stage-copy')
    $mismatchRejected = $false
    try {
        Assert-StageIntegrity -StageDirectory $stageDirectory -ProjectRoot $projectRoot -PublishDirectory $publishDirectory
    } catch {
        $mismatchRejected = $_.Exception.Message -like '*downloader differs from the current server source*'
        if (-not $mismatchRejected) { throw }
    }
    if (-not $mismatchRejected) {
        throw 'Stage integrity accepted a stale Nebula STRM downloader.'
    }

    [System.IO.File]::Delete($stagedAdmission)
    $missingAdmissionRejected = $false
    try {
        Assert-StageIntegrity -StageDirectory $stageDirectory -ProjectRoot $projectRoot -PublishDirectory $publishDirectory
    } catch {
        $missingAdmissionRejected = $_.Exception.Message -like '*mongo_admission.py*'
        if (-not $missingAdmissionRejected) { throw }
    }
    if (-not $missingAdmissionRejected) {
        throw 'Stage integrity accepted a Python worker package missing mongo_admission.py.'
    }

    Write-Host 'Stage integrity accepted matching Nebula modules and rejected stale downloader or missing admission module.'
}
finally {
    if ([System.IO.Directory]::Exists($fixtureRoot)) {
        [System.IO.Directory]::Delete($fixtureRoot, $true)
    }
}
