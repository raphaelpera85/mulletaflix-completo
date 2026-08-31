# Python is required by the independent MulletaFlix Nebula mount helper.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function Get-PythonPath {
    $candidates = @(
        (Join-Path $env:ProgramFiles 'Python313\python.exe'),
        (Join-Path $env:ProgramFiles 'Python312\python.exe'),
        (Join-Path $env:ProgramFiles 'Python311\python.exe'),
        (Join-Path $env:LocalAppData 'Programs\Python\Python313\python.exe'),
        (Join-Path $env:LocalAppData 'Programs\Python\Python312\python.exe'),
        (Join-Path $env:LocalAppData 'Programs\Python\Python311\python.exe')
    )

    $command = Get-Command python.exe -ErrorAction SilentlyContinue
    if ($command -and (Test-Path -LiteralPath $command.Source)) {
        $candidates += $command.Source
    }

    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    return $null
}

$python = Get-PythonPath
if ($python) {
    Write-Host "[OK] Python encontrado em $python." -ForegroundColor Green
    exit 0
}

$installer = Join-Path $env:TEMP 'mulletaflix-python-installer.exe'
$pythonUrl = 'https://www.python.org/ftp/python/3.13.7/python-3.13.7-amd64.exe'

try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Write-Host '[INFO] Python não encontrado. Baixando o runtime oficial...' -ForegroundColor Yellow
    Invoke-WebRequest -Uri $pythonUrl -OutFile $installer -UseBasicParsing
    if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
        throw 'O instalador do Python não foi baixado.'
    }

    $arguments = @(
        '/quiet',
        'InstallAllUsers=1',
        'PrependPath=1',
        'Include_pip=1',
        'Include_launcher=1',
        'SimpleInstall=1',
        'Include_test=0'
    )
    $process = Start-Process -FilePath $installer -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -ne 0 -and $process.ExitCode -ne 3010) {
        throw "O instalador do Python terminou com código $($process.ExitCode)."
    }

    $python = Get-PythonPath
    if (-not $python) {
        throw 'Python foi instalado, mas o executável não foi localizado.'
    }

    Write-Host "[OK] Python disponível em $python." -ForegroundColor Green
    exit 0
}
catch {
    Write-Error "Falha ao instalar Python: $($_.Exception.Message)"
    exit 1
}
finally {
    if (Test-Path -LiteralPath $installer) {
        Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
    }
}
