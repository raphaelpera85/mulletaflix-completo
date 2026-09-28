$ErrorActionPreference = "Stop"

$iosRoot = (Resolve-Path (Join-Path $PSScriptRoot ".." )).Path
$repoRoot = (Resolve-Path (Join-Path $iosRoot ".." )).Path

function Assert-Condition([bool] $condition, [string] $message) {
    if (-not $condition) { throw $message }
}

Push-Location $repoRoot
try {
    git diff --check -- MulletaFlix-iOS | Out-Null
    Assert-Condition ($LASTEXITCODE -eq 0) "git diff --check falhou."

    [xml](Get-Content (Join-Path $iosRoot "Info.plist") -Raw) | Out-Null
    [xml](Get-Content (Join-Path $iosRoot "PrivacyInfo.xcprivacy") -Raw) | Out-Null
    [xml](Get-Content (Join-Path $iosRoot "ExportOptions.plist.example") -Raw) | Out-Null

    $plist = Get-Content (Join-Path $iosRoot "Info.plist") -Raw
    Assert-Condition ($plist -match '<key>CFBundleVersion</key>\s*<string>\$\(CURRENT_PROJECT_VERSION\)</string>') "CFBundleVersion não está ligado ao target."
    Assert-Condition ($plist -match '<key>ITSAppUsesNonExemptEncryption</key>\s*<false\s*/>') "Declaração de conformidade ausente."

    $project = Get-Content (Join-Path $iosRoot "MulletaFlix.xcodeproj/project.pbxproj") -Raw
    $runtime = Get-Content (Join-Path $iosRoot "Sources/MulletaFlixCore/AppIdentity.swift") -Raw
    $marketingVersion = [regex]::Match($project, 'MARKETING_VERSION = ([^;]+);').Groups[1].Value
    $runtimeVersion = [regex]::Match($runtime, 'public static let version = "([^"]+)"').Groups[1].Value
    Assert-Condition ($marketingVersion -eq $runtimeVersion) "Versão do bundle e runtime divergentes."
    Assert-Condition ($project -match 'SWIFT_VERSION = 6\.0;') "Swift 6 não está configurado no projeto Xcode."
    Assert-Condition ($project -match 'IPHONEOS_DEPLOYMENT_TARGET = 18\.0;') "O target mínimo iOS 18 não está configurado."
    Assert-Condition ($project -match 'CURRENT_PROJECT_VERSION = 1;') "Build inicial do target não está configurado como 1."
    Assert-Condition ($project -match 'MARKETING_VERSION = 1\.0\.0;') "Marketing version inicial 1.0.0 ausente no target."
    Assert-Condition ($project -match 'defaultConfigurationName = Release;') "O projeto não possui Release como configuração padrão."

    $releaseScript = Get-Content (Join-Path $iosRoot "scripts/build-production-release.sh") -Raw
    foreach ($guard in @("SIGNING_STYLE=", "TEAM_ID=", "RUNTIME_VERSION=", "codesign --verify", "Apple Distribution", "get-task-allow=true")) {
        Assert-Condition ($releaseScript.Contains($guard)) "Guard de release ausente: $guard"
    }

    $swiftFiles = @(Get-ChildItem (Join-Path $iosRoot "Tests") -Filter "*.swift" -Recurse)
    $declaredTests = @($swiftFiles | Select-String -Pattern '\bfunc\s+test[A-Za-z0-9_]*\s*\(').Count
    $rawErrors = @(Get-ChildItem (Join-Path $iosRoot "App") -Filter "*.swift" | Select-String -Pattern 'error\.localizedDescription').Count
    $asyncNilCoalescing = @(Get-ChildItem $iosRoot -Filter "*.swift" -Recurse | Select-String -Pattern '\?\?\s*await').Count
    Assert-Condition ($rawErrors -eq 0) "A UI contém mensagens localizadas brutas."
    Assert-Condition ($asyncNilCoalescing -eq 0) "Swift contém await dentro de autoclosure de ??; separe o fallback assíncrono."

    Write-Output "IOS_DIFF_CHECK=PASS"
    Write-Output "IOS_PLIST_XML=PASS"
    Write-Output "IOS_PRIVACY_XML=PASS"
    Write-Output "IOS_EXPORT_OPTIONS_XML=PASS"
    Write-Output "IOS_VERSION_ALIGNMENT=PASS ($marketingVersion)"
    Write-Output "IOS_RELEASE_GUARDS=PASS"
    Write-Output "IOS_DECLARED_TESTS=$declaredTests"
    Write-Output "IOS_RAW_UI_ERRORS=$rawErrors"
    Write-Output "IOS_ASYNC_NIL_COALESCING=PASS"
    Write-Output "SWIFT_NATIVE_TESTS=NOT_RUN_ON_WINDOWS"
    Write-Output "XCODEBUILD=NOT_AVAILABLE_ON_WINDOWS"
}
finally {
    Pop-Location
}
