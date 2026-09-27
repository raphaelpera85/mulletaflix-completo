[CmdletBinding()]
param(
    [string]$ApkPath
)

$ErrorActionPreference = 'Stop'
$script:ExpectedAndroidApkCertificateSha256 = '224F9A6BD12690E1114ACE649BBFA778D3E7E99DAE608FF711DDF9131E036273'

function Get-AndroidApkCertificateFingerprint {
    param([Parameter(Mandatory)][string]$Path)

    $resolvedApkPath = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
    $projectRoot = Split-Path -Parent $PSScriptRoot
    $sdkCandidates = @()
    foreach ($variableName in @('ANDROID_SDK_ROOT', 'ANDROID_HOME')) {
        $candidate = [Environment]::GetEnvironmentVariable($variableName)
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            $sdkCandidates += $candidate
        }
    }

    $localPropertiesPath = Join-Path $projectRoot 'MulletaFlix-android\local.properties'
    if (Test-Path -LiteralPath $localPropertiesPath) {
        $sdkLine = Select-String -LiteralPath $localPropertiesPath -Pattern '^sdk\.dir=' | Select-Object -First 1
        if ($sdkLine -and $sdkLine.Line -match '^sdk\.dir=(.*)$') {
            $sdkCandidates += [regex]::Replace($Matches[1], '\\(.)', '$1')
        }
    }

    $sdkPath = $sdkCandidates | Where-Object {
        Test-Path -LiteralPath (Join-Path $_ 'build-tools')
    } | Select-Object -First 1
    if (-not $sdkPath) {
        throw 'Android SDK build-tools não encontrados; defina ANDROID_SDK_ROOT ou ANDROID_HOME.'
    }

    $buildTools = Get-ChildItem -LiteralPath (Join-Path $sdkPath 'build-tools') -Directory |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' } |
        Sort-Object { [version]$_.Name } -Descending |
        Select-Object -First 1
    if (-not $buildTools) {
        throw 'Nenhuma versão estável de Android build-tools foi encontrada.'
    }

    $apkSigner = Join-Path $buildTools.FullName 'apksigner.bat'
    if (-not (Test-Path -LiteralPath $apkSigner)) {
        throw 'apksigner não foi encontrado na instalação do Android SDK.'
    }

    $signerOutput = & $apkSigner verify --print-certs $resolvedApkPath 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "apksigner rejeitou o APK (código $LASTEXITCODE)."
    }

    $fingerprints = @()
    foreach ($line in $signerOutput) {
        if ("$line" -match 'Signer #\d+ certificate SHA-256 digest:\s*([0-9A-Fa-f:]+)') {
            $fingerprints += ($Matches[1] -replace ':', '').ToUpperInvariant()
        }
    }
    if ($fingerprints.Count -ne 1) {
        throw "Esperado exatamente um certificado assinante; encontrados $($fingerprints.Count)."
    }

    return $fingerprints[0]
}

function Assert-AndroidApkCertificateFingerprint {
    param([Parameter(Mandatory)][string]$Fingerprint)

    $actualFingerprint = ($Fingerprint -replace ':', '').ToUpperInvariant()
    if ($actualFingerprint -ne $script:ExpectedAndroidApkCertificateSha256) {
        throw "Certificado SHA-256 do APK incompatível. Esperado $script:ExpectedAndroidApkCertificateSha256; obtido $actualFingerprint."
    }
}

function Assert-AndroidApkSignature {
    param([Parameter(Mandatory)][string]$Path)

    $actualFingerprint = Get-AndroidApkCertificateFingerprint -Path $Path
    Assert-AndroidApkCertificateFingerprint -Fingerprint $actualFingerprint

    Write-Host "Certificado de assinatura APK validado: $actualFingerprint" -ForegroundColor Green
}

if ($MyInvocation.InvocationName -ne '.') {
    if ([string]::IsNullOrWhiteSpace($ApkPath)) {
        throw 'Informe o caminho do APK para validar a assinatura.'
    }
    Assert-AndroidApkSignature -Path $ApkPath
}
