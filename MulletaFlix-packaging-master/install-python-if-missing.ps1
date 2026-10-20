# Python is required by the independent MulletaFlix Nebula mount helper.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function Get-PythonPath {
    $candidates = @(
        (Join-Path $env:ProgramFiles 'Python313\python.exe'),
        (Join-Path $env:ProgramFiles 'Python312\python.exe'),
        (Join-Path $env:ProgramFiles 'Python311\python.exe'),
        'C:\Python313\python.exe',
        'C:\Python312\python.exe',
        'C:\Python311\python.exe'
    )

    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    return $null
}

function Assert-ValidAuthenticodeSignature {
    param([Parameter(Mandatory = $true)][string] $Path)

    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid') {
        throw "A assinatura do instalador do Python não é válida: $($signature.Status)."
    }

    if ($signature.SignerCertificate.Subject -notmatch 'Python Software Foundation') {
        throw "O instalador do Python não foi assinado pela Python Software Foundation: $($signature.SignerCertificate.Subject)."
    }
}

function Install-NebulaRuntimeDependencies {
    param([Parameter(Mandatory = $true)][string] $Python)

    $requirementsLock = Join-Path $PSScriptRoot 'Tools\NebulaPython\requirements.lock'
    if (-not (Test-Path -LiteralPath $requirementsLock -PathType Leaf)) {
        throw "Lock de dependências Python do Nebula não encontrado: $requirementsLock"
    }

    $runtimeDirectory = Join-Path $PSScriptRoot 'Tools\NebulaPython'
    $venvDirectory = Join-Path $runtimeDirectory '.venv'
    $venvPython = Join-Path $venvDirectory 'Scripts\python.exe'
    New-Item -ItemType Directory -Force -Path $runtimeDirectory | Out-Null

    if (-not (Test-Path -LiteralPath $venvPython -PathType Leaf)) {
        Write-Host '[INFO] Criando ambiente Python isolado do Nebula...' -ForegroundColor Yellow
        & $Python -m venv $venvDirectory
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $venvPython -PathType Leaf)) {
            throw "Não foi possível criar a venv do Nebula (código $LASTEXITCODE)."
        }
    }

    Write-Host '[INFO] Instalando dependências no ambiente isolado do Nebula...' -ForegroundColor Yellow
    & $venvPython -m pip install --disable-pip-version-check --quiet --require-hashes -r $requirementsLock
    if ($LASTEXITCODE -ne 0) {
        throw "A instalação das dependências Python do Nebula falhou (código $LASTEXITCODE)."
    }

    [System.IO.File]::WriteAllText(
        (Join-Path $runtimeDirectory 'python.path'),
        $venvPython,
        [System.Text.UTF8Encoding]::new($false))
    Write-Host '[OK] Dependências Python do Nebula instaladas pelo lock com hashes.' -ForegroundColor Green
}

$python = Get-PythonPath
if ($python) {
    Write-Host "[OK] Python encontrado em $python." -ForegroundColor Green
    Install-NebulaRuntimeDependencies -Python $python
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
    Assert-ValidAuthenticodeSignature -Path $installer

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

    Install-NebulaRuntimeDependencies -Python $python
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
