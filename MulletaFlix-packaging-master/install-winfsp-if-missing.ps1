# WinFsp is the filesystem driver required by rclone mount on Windows.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function Test-WinFspInstalled {
    $uninstallRoots = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
    )

    foreach ($root in $uninstallRoots) {
        $apps = Get-ItemProperty -Path $root -ErrorAction SilentlyContinue
        if ($apps | Where-Object { $_.DisplayName -like 'WinFsp*' }) {
            return $true
        }
    }

    return (Test-Path -LiteralPath "$env:ProgramFiles\WinFsp\bin\winfsp-x64.dll") -or
        (Test-Path -LiteralPath "${env:ProgramFiles(x86)}\WinFsp\bin\winfsp-x64.dll")
}

if (Test-WinFspInstalled) {
    Write-Host '[OK] WinFsp já está instalado.' -ForegroundColor Green
    exit 0
}

$winget = Get-Command winget.exe -ErrorAction SilentlyContinue
if ($winget) {
    Write-Host '[INFO] Instalando WinFsp via winget...' -ForegroundColor Yellow
    $process = Start-Process -FilePath $winget.Source -ArgumentList @(
        'install', '--id', 'WinFsp.WinFsp', '--exact', '--silent',
        '--accept-source-agreements', '--accept-package-agreements'
    ) -Wait -PassThru -NoNewWindow
} else {
    Write-Host '[INFO] winget não encontrado; usando o instalador oficial do WinFsp...' -ForegroundColor Yellow
    $installer = Join-Path $env:TEMP 'mulletaflix-winfsp-installer.msi'
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri 'https://github.com/winfsp/winfsp/releases/download/v2.0/winfsp-2.0.23075.msi' -OutFile $installer -UseBasicParsing
        $process = Start-Process -FilePath 'msiexec.exe' -ArgumentList @('/i', $installer, '/qn', '/norestart') -Wait -PassThru
    } finally {
        if (Test-Path -LiteralPath $installer) {
            Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
        }
    }
}

if (($process.ExitCode -ne 0 -and $process.ExitCode -ne 3010) -and -not (Test-WinFspInstalled)) {
    Write-Error "A instalação do WinFsp falhou com código $($process.ExitCode)."
    exit $process.ExitCode
}

Write-Host '[OK] WinFsp disponível para o rclone.' -ForegroundColor Green
exit 0
