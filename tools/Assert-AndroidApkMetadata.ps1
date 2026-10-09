[CmdletBinding()]
param(
    [string]$ApkPath,
    [string]$ProjectRoot,
    [string]$ExpectedPackageName,
    [string]$ExpectedVersionName,
    [int]$ExpectedVersionCode
)

$ErrorActionPreference = 'Stop'

function Get-AndroidApkMetadata {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Root
    )

    $resolvedApkPath = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
    $sdkCandidates = @()
    foreach ($variableName in @('ANDROID_SDK_ROOT', 'ANDROID_HOME')) {
        $candidate = [Environment]::GetEnvironmentVariable($variableName)
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            $sdkCandidates += $candidate
        }
    }

    $localPropertiesPath = Join-Path $Root 'MulletaFlix-android\local.properties'
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

    $aapt = Join-Path $buildTools.FullName 'aapt.exe'
    if (-not (Test-Path -LiteralPath $aapt)) {
        throw 'aapt não foi encontrado na instalação do Android SDK.'
    }

    $badging = & $aapt dump badging $resolvedApkPath 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Não foi possível ler o manifesto do APK (aapt código $LASTEXITCODE)."
    }

    $packageLine = $badging | Where-Object { "$_" -match "^package: name='([^']+)' versionCode='([^']+)' versionName='([^']*)'" } | Select-Object -First 1
    if (-not $packageLine -or "$packageLine" -notmatch "^package: name='([^']+)' versionCode='([^']+)' versionName='([^']*)'") {
        throw 'O APK não contém metadados package/versionCode/versionName legíveis.'
    }

    return [pscustomobject]@{
        PackageName = $Matches[1]
        VersionCode = [int]$Matches[2]
        VersionName = $Matches[3]
    }
}

function Assert-AndroidApkMetadata {
    param(
        [Parameter(Mandatory)][psobject]$Metadata,
        [Parameter(Mandatory)][string]$ExpectedPackageName,
        [Parameter(Mandatory)][string]$ExpectedVersionName,
        [Parameter(Mandatory)][int]$ExpectedVersionCode
    )

    if ($Metadata.PackageName -ne $ExpectedPackageName) {
        throw "applicationId do APK incompatível. Esperado '$ExpectedPackageName'; obtido '$($Metadata.PackageName)'."
    }
    if ($Metadata.VersionName -ne $ExpectedVersionName) {
        throw "versionName do APK incompatível. Esperado '$ExpectedVersionName'; obtido '$($Metadata.VersionName)'."
    }
    if ([int]$Metadata.VersionCode -ne $ExpectedVersionCode) {
        throw "versionCode do APK incompatível. Esperado '$ExpectedVersionCode'; obtido '$($Metadata.VersionCode)'."
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    foreach ($required in @($ApkPath, $ProjectRoot, $ExpectedPackageName, $ExpectedVersionName)) {
        if ([string]::IsNullOrWhiteSpace($required)) {
            throw 'Informe APK, raiz do projeto, applicationId e versionName esperados.'
        }
    }
    if ($ExpectedVersionCode -le 0) {
        throw 'Informe um versionCode esperado maior que zero.'
    }

    $metadata = Get-AndroidApkMetadata -Path $ApkPath -Root $ProjectRoot
    Assert-AndroidApkMetadata -Metadata $metadata -ExpectedPackageName $ExpectedPackageName -ExpectedVersionName $ExpectedVersionName -ExpectedVersionCode $ExpectedVersionCode
    Write-Host "Metadados do APK validados: $($metadata.PackageName) $($metadata.VersionName) ($($metadata.VersionCode))" -ForegroundColor Green
}
