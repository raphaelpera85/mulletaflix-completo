$ErrorActionPreference = 'Stop'

$configPath = Join-Path $PSScriptRoot '..\configure-https.ps1'
$tokens = $null
$parseErrors = $null
$configAst = [System.Management.Automation.Language.Parser]::ParseFile($configPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) {
    throw "HTTPS configurator has PowerShell parse errors: $($parseErrors[0].Message)"
}

$safeReaderBootstrap = $configAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.IfStatementAst] -and $node.Extent.Text.Contains('MulletaFlix.DuckDns.SafeTokenFile')
}, $true)
if ($null -eq $safeReaderBootstrap) {
    throw 'Safe DuckDNS token reader bootstrap was not found.'
}
Invoke-Expression $safeReaderBootstrap.Extent.Text

$nginxConfigDefinition = $configAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Write-NginxConfig'
}, $true)
if ($null -eq $nginxConfigDefinition) {
    throw 'Nginx config writer was not found.'
}
Invoke-Expression $nginxConfigDefinition.Extent.Text
$nginxValidationDefinition = $configAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Test-NginxConfig'
}, $true)
if ($null -eq $nginxValidationDefinition -or
    $nginxValidationDefinition.Extent.Text -notmatch '&\s+\$nginxExe\s+-p\s+\$nginxRoot\s+-c\s+\$ConfigPath\s+-t' -or
    $nginxValidationDefinition.Extent.Text.Contains('GetRelativePath')) {
    throw 'Nginx candidate validation must pass its absolute path without .NET 6-only path APIs.'
}
$nginxTransactionFunctionNames = @('Restore-PendingNginxConfig', 'Install-NginxConfig', 'Restore-NginxConfig', 'Complete-NginxConfig')
$nginxTransactionDefinitions = $configAst.FindAll({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst]
}, $true)
foreach ($functionName in $nginxTransactionFunctionNames) {
    $definition = $nginxTransactionDefinitions | Where-Object { $_.Name -eq $functionName } | Select-Object -First 1
    if ($null -eq $definition) { throw "Nginx transaction function was not found: $functionName" }
    Invoke-Expression $definition.Extent.Text
}
$tlsConfigTestRoot = Join-Path ([System.IO.Path]::GetTempPath()) "mflx nginx config-$([guid]::NewGuid().ToString('N'))"
$nginxRoot = $tlsConfigTestRoot
$nginxConfig = Join-Path $tlsConfigTestRoot 'conf\nginx.conf'
$nginxCandidateConfig = "$nginxConfig.candidate"
$acmeRoot = Join-Path $tlsConfigTestRoot 'acme'
$certRoot = Join-Path $tlsConfigTestRoot 'certs'
$certPrefix = Join-Path $certRoot 'media.example.test'
$HostName = 'media.example.test'
$ServerPort = 8096
$nginxRollbackConfig = "$nginxConfig.rollback"
$script:simulateNginxTestFailure = $false
function Test-NginxConfig([string]$ConfigPath = $nginxCandidateConfig) {
    if ($script:simulateNginxTestFailure) { throw 'Simulated invalid Nginx candidate.' }
    if (-not [System.IO.File]::Exists($ConfigPath)) { throw 'Nginx candidate was not created for validation.' }
}
try {
    [void][System.IO.Directory]::CreateDirectory((Split-Path -Parent $nginxConfig))
    [System.IO.File]::WriteAllText($nginxConfig, 'previous-valid-config')
    Write-NginxConfig $false
    $httpOnlyConfig = [System.IO.File]::ReadAllText($nginxCandidateConfig)
    if (-not $httpOnlyConfig.Contains('proxy_pass http://127.0.0.1:8096;')) {
        throw 'HTTP-only Nginx config did not preserve the local streaming proxy.'
    }

    $script:simulateNginxTestFailure = $true
    $candidateRejected = $false
    try { $null = Install-NginxConfig } catch { $candidateRejected = $true }
    if (-not $candidateRejected -or [System.IO.File]::ReadAllText($nginxConfig) -ne 'previous-valid-config' -or [System.IO.File]::Exists($nginxRollbackConfig)) {
        throw 'Invalid Nginx candidate replaced the active configuration or created a rollback file.'
    }

    $script:simulateNginxTestFailure = $false
    $httpTransaction = Install-NginxConfig
    if (-not [System.IO.File]::ReadAllText($nginxConfig).Contains('proxy_pass http://127.0.0.1:8096;') -or
        [System.IO.File]::ReadAllText($nginxRollbackConfig) -ne 'previous-valid-config') {
        throw 'HTTP configuration activation did not retain the previous config for rollback.'
    }
    Restore-NginxConfig $httpTransaction
    if ([System.IO.File]::ReadAllText($nginxConfig) -ne 'previous-valid-config' -or [System.IO.File]::Exists($nginxRollbackConfig)) {
        throw 'Nginx rollback did not restore the prior configuration.'
    }

    Write-NginxConfig $true
    $tlsConfig = [System.IO.File]::ReadAllText($nginxCandidateConfig)
    if (-not $tlsConfig.Contains('return 301 https://$host$request_uri;')) {
        throw 'TLS Nginx config did not emit a valid literal host/request redirect.'
    }
    $acmePathForTest = $acmeRoot.Replace('\', '/')
    $chainPathForTest = "$certPrefix-chain.pem".Replace('\', '/')
    $keyPathForTest = "$certPrefix-key.pem".Replace('\', '/')
    if (-not $tlsConfig.Contains("root `"$acmePathForTest`";") -or
        -not $tlsConfig.Contains("ssl_certificate `"$chainPathForTest`";") -or
        -not $tlsConfig.Contains("ssl_certificate_key `"$keyPathForTest`";")) {
        throw 'Nginx file paths were not rendered with quoted forward-slash paths.'
    }
    $configBytes = [System.IO.File]::ReadAllBytes($nginxCandidateConfig)
    if ($configBytes.Length -ge 3 -and $configBytes[0] -eq 0xEF -and $configBytes[1] -eq 0xBB -and $configBytes[2] -eq 0xBF) {
        throw 'Nginx candidate unexpectedly contains a UTF-8 BOM.'
    }
    if ($tlsConfig.Contains('`$host') -or $tlsConfig.Contains('`$request_uri')) {
        throw 'TLS Nginx config contains PowerShell backticks in a directive.'
    }
    $tlsTransaction = Install-NginxConfig
    Complete-NginxConfig $tlsTransaction
    if ([System.IO.File]::Exists($nginxRollbackConfig) -or -not [System.IO.File]::ReadAllText($nginxConfig).Contains('return 301 https://$host$request_uri;')) {
        throw 'Successful TLS activation did not commit the candidate and remove its rollback file.'
    }

    [System.IO.File]::WriteAllText($nginxRollbackConfig, 'recoverable-prior-config')
    [System.IO.File]::WriteAllText($nginxConfig, 'interrupted-new-config')
    Restore-PendingNginxConfig
    if ([System.IO.File]::ReadAllText($nginxConfig) -ne 'recoverable-prior-config' -or [System.IO.File]::Exists($nginxRollbackConfig)) {
        throw 'Startup recovery did not restore a rollback file left by interruption.'
    }

    [System.IO.File]::Delete($nginxConfig)
    Write-NginxConfig $false
    $initialTransaction = Install-NginxConfig
    Restore-NginxConfig $initialTransaction
    if ([System.IO.File]::Exists($nginxConfig)) {
        throw 'Rollback of a first-time configuration did not remove the newly activated file.'
    }
    Write-Host 'HTTP/TLS rendering, candidate validation, commit, rollback, and crash recovery passed.'
}
finally {
    Remove-Item Function:\Test-NginxConfig -ErrorAction SilentlyContinue
    $script:simulateNginxTestFailure = $false
    if ([System.IO.Directory]::Exists($tlsConfigTestRoot)) {
        [System.IO.Directory]::Delete($tlsConfigTestRoot, $true)
    }
}

$utf16Fixture = Join-Path ([System.IO.Path]::GetTempPath()) "mflx-duckdns-utf16-$([guid]::NewGuid().ToString('N')).txt"
try {
    [System.IO.File]::WriteAllText($utf16Fixture, 'installer-token-roundtrip', [System.Text.Encoding]::Unicode)
    $utf16FixtureText = [MulletaFlix.DuckDns.SafeTokenFile]::ReadAllText($utf16Fixture)
    if ($utf16FixtureText -ne 'installer-token-roundtrip') {
        throw 'DuckDNS reader did not decode a BOM-marked UTF-16LE NSIS token fixture.'
    }
}
finally {
    if ([System.IO.File]::Exists($utf16Fixture)) { [System.IO.File]::Delete($utf16Fixture) }
}

$readerDefinition = $configAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Read-DuckDnsTokenFile'
}, $true)
if ($null -eq $readerDefinition) {
    throw 'Read-DuckDnsTokenFile was not found.'
}
Invoke-Expression $readerDefinition.Extent.Text
$aclDefinition = $configAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Set-ProtectedDuckDnsAcl'
}, $true)
if ($null -eq $aclDefinition) {
    throw 'Set-ProtectedDuckDnsAcl was not found.'
}
Invoke-Expression $aclDefinition.Extent.Text

$ensureConfigRootDefinition = $configAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Ensure-ProtectedDuckDnsConfigRoot'
}, $true)
if ($null -eq $ensureConfigRootDefinition) {
    throw 'Ensure-ProtectedDuckDnsConfigRoot was not found.'
}
Invoke-Expression $ensureConfigRootDefinition.Extent.Text

$prepareTokenDefinition = $configAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Prepare-DuckDnsTokenFile'
}, $true)
if ($null -eq $prepareTokenDefinition) {
    throw 'Prepare-DuckDnsTokenFile was not found.'
}
Invoke-Expression $prepareTokenDefinition.Extent.Text

$pathGuardDefinition = $configAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Assert-NoDuckDnsReparsePoints'
}, $true)
if ($null -eq $pathGuardDefinition) {
    throw 'Assert-NoDuckDnsReparsePoints was not found.'
}
Invoke-Expression $pathGuardDefinition.Extent.Text

$aclAssertionDefinition = $configAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Assert-ProtectedDuckDnsAcl'
}, $true)
if ($null -eq $aclAssertionDefinition) {
    throw 'Assert-ProtectedDuckDnsAcl was not found.'
}
Invoke-Expression $aclAssertionDefinition.Extent.Text

$protectLegacyDefinition = $configAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Protect-LegacyDuckDnsConfig'
}, $true)
if ($null -eq $protectLegacyDefinition) {
    throw 'Protect-LegacyDuckDnsConfig was not found.'
}
Invoke-Expression $protectLegacyDefinition.Extent.Text

$removeLegacyDefinition = $configAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Remove-LegacyDuckDnsConfig'
}, $true)
if ($null -eq $removeLegacyDefinition) {
    throw 'Remove-LegacyDuckDnsConfig was not found.'
}
Invoke-Expression $removeLegacyDefinition.Extent.Text

$disableDefinition = $configAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Disable-DuckDns'
}, $true)
if ($null -eq $disableDefinition) {
    throw 'Disable-DuckDns was not found.'
}
Invoke-Expression $disableDefinition.Extent.Text

$administratorDefinition = $configAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Assert-DuckDnsAdministrator'
}, $true)
if ($null -eq $administratorDefinition) {
    throw 'Assert-DuckDnsAdministrator was not found.'
}
Invoke-Expression $administratorDefinition.Extent.Text

$currentIdentity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$currentPrincipal = New-Object System.Security.Principal.WindowsPrincipal($currentIdentity)
if (-not $currentPrincipal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $nonElevatedSetupRejected = $false
    try { Assert-DuckDnsAdministrator } catch { $nonElevatedSetupRejected = $true }
    if (-not $nonElevatedSetupRejected) {
        throw 'DuckDNS credential setup did not require an elevated Administrator token.'
    }
    $originalProgramData = $programDataRoot
    $testId = [guid]::NewGuid().ToString('N')
    $isolatedRoot = Join-Path ([System.IO.Path]::GetTempPath()) "mflx-duckdns-secure-create-$testId"
    $duckDnsConfigRoot = Join-Path $isolatedRoot 'MulletaFlix-DuckDNS'
    $programDataRoot = $isolatedRoot
    $testUserSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
    try {
        [void][System.IO.Directory]::CreateDirectory($isolatedRoot)
        [void][System.IO.Directory]::CreateDirectory($duckDnsConfigRoot)
        $precreatedRejected = $false
        try { Ensure-ProtectedDuckDnsConfigRoot } catch { $precreatedRejected = $true }
        $precreatedAcl = Get-Acl -LiteralPath $duckDnsConfigRoot
        if (-not $precreatedRejected -or $precreatedAcl.AreAccessRulesProtected) {
            throw 'Secure root creation accepted or modified a directory that existed before protected creation.'
        }

        [System.IO.Directory]::Delete($duckDnsConfigRoot, $true)
        $unprivilegedRootCreationRejected = $false
        try { Ensure-ProtectedDuckDnsConfigRoot } catch { $unprivilegedRootCreationRejected = $true }
        if (-not $unprivilegedRootCreationRejected) {
            throw 'A non-elevated process created the SYSTEM-owned credential root.'
        }

        Write-Host 'DuckDNS non-elevated guard, pre-created-root rejection, and privileged-creation denial passed.'
    }
    finally {
        $programDataRoot = $originalProgramData
        if ([System.IO.Directory]::Exists($duckDnsConfigRoot)) {
            & icacls.exe $duckDnsConfigRoot /grant "*$($testUserSid.Value):(OI)(CI)F" | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'Could not restore cleanup access to the protected-root integration fixture.' }
        }
        $tempBoundary = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
        $isolatedFullPath = [System.IO.Path]::GetFullPath($isolatedRoot)
        if (-not $isolatedFullPath.StartsWith($tempBoundary, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'Refusing to remove DuckDNS test data outside the unique temporary directory.'
        }
        if ([System.IO.Directory]::Exists($isolatedRoot)) { [System.IO.Directory]::Delete($isolatedRoot, $true) }
    }
    exit 0
}

$originalTemp = $env:TEMP
$originalProgramData = $programDataRoot
$testId = [guid]::NewGuid().ToString('N')
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) "mflx-duckdns-reader-$testId"
$outsideRoot = Join-Path ([System.IO.Path]::GetTempPath()) "mflx-duckdns-outside-$testId"
[void][System.IO.Directory]::CreateDirectory($testRoot)
[void][System.IO.Directory]::CreateDirectory($outsideRoot)
$aclFile = Join-Path $testRoot 'protected-file.json'
$aclDirectory = Join-Path $testRoot 'protected-directory'
$legacyRoot = Join-Path $testRoot 'legacy'
$newRoot = Join-Path $testRoot 'secure'
$tokenDirectory = Join-Path $newRoot 'token-input'
$duckDnsConfigRoot = $newRoot
$duckDnsScript = Join-Path $testRoot 'duckdns-update.ps1'
$duckDnsTask = 'MulletaFlix DuckDNS Update Test'
$legacyDuckDnsConfig = Join-Path $legacyRoot 'duckdns.json'
$duckDnsConfig = Join-Path $newRoot 'duckdns.json'
$testUserSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
$env:TEMP = $testRoot
$programDataRoot = $testRoot

try {
    [void][System.IO.Directory]::CreateDirectory($newRoot)
    $precreatedAcl = Get-Acl -LiteralPath $newRoot
    $precreatedAcl.SetAccessRuleProtection($false, $true)
    $precreatedAcl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($testUserSid, 'Modify', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
    Set-Acl -LiteralPath $newRoot -AclObject $precreatedAcl
    $rejectedPrecreatedRoot = $false
    try { Ensure-ProtectedDuckDnsConfigRoot } catch { $rejectedPrecreatedRoot = $true }
    $precreatedAfter = Get-Acl -LiteralPath $newRoot
    $precreatedSids = @($precreatedAfter.Access | ForEach-Object { $_.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value })
    if (-not $rejectedPrecreatedRoot -or $precreatedSids -notcontains $testUserSid.Value) {
        throw 'Config root preparation accepted or silently hardened a directory prepared before the protected create.'
    }
    $canonicalTestRoot = [System.IO.Path]::GetFullPath($testRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not [System.IO.Path]::GetFullPath($newRoot).StartsWith($canonicalTestRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to remove a DuckDNS test directory outside the isolated test root.'
    }
    [System.IO.Directory]::Delete($newRoot, $true)
    Ensure-ProtectedDuckDnsConfigRoot
    Assert-ProtectedDuckDnsAcl -Path $newRoot

    [void][System.IO.Directory]::CreateDirectory($tokenDirectory)
    $tokenPath = Join-Path $tokenDirectory 'token.txt'
    [System.IO.File]::WriteAllText($tokenPath, 'unit-test-token')
    $looseAclRejected = $false
    try {
        Assert-ProtectedDuckDnsAcl -Path $tokenDirectory
    }
    catch {
        $looseAclRejected = $true
    }
    if (-not $looseAclRejected) {
        throw 'DuckDNS ACL verifier accepted a directory writable by the current user.'
    }
    $tokenAclRejected = $false
    try {
        $null = Read-DuckDnsTokenFile -Path $tokenPath
    }
    catch {
        $tokenAclRejected = $true
    }
    if (-not $tokenAclRejected -or -not [System.IO.File]::Exists($tokenPath)) {
        throw 'Token reader did not fail closed when its parent directory ACL was not protected.'
    }

    Set-ProtectedDuckDnsAcl -Path $tokenDirectory -Directory
    Set-ProtectedDuckDnsAcl -Path $tokenPath
    $token = Read-DuckDnsTokenFile -Path $tokenPath
    if ($token -ne 'unit-test-token' -or [System.IO.File]::Exists($tokenPath)) {
        throw 'Successful token read did not return the value and remove its input file.'
    }

    $emptyTokenPath = Join-Path $tokenDirectory 'empty-token.txt'
    [System.IO.File]::WriteAllText($emptyTokenPath, '')
    Set-ProtectedDuckDnsAcl -Path $emptyTokenPath
    $emptyRejected = $false
    try {
        $null = Read-DuckDnsTokenFile -Path $emptyTokenPath
    }
    catch {
        $emptyRejected = $true
    }
    if (-not $emptyRejected -or [System.IO.File]::Exists($emptyTokenPath)) {
        throw 'Empty token input was not rejected and cleaned up.'
    }

    Prepare-DuckDnsTokenFile
    $preparedTokenFile = Join-Path $duckDnsConfigRoot 'installer-token.txt'
    if (-not [System.IO.File]::Exists($preparedTokenFile)) {
        throw 'Secure token staging did not create its empty token file.'
    }
    Assert-ProtectedDuckDnsAcl -Path $preparedTokenFile
    if ([System.IO.File]::ReadAllText($preparedTokenFile) -ne '') {
        throw 'Secure token staging unexpectedly wrote data before the installer supplied the secret.'
    }
    [System.IO.File]::Delete($preparedTokenFile)

    $outsidePath = Join-Path $outsideRoot 'keep.txt'
    [System.IO.File]::WriteAllText($outsidePath, 'do-not-delete')
    $outsideRejected = $false
    try {
        $null = Read-DuckDnsTokenFile -Path $outsidePath
    }
    catch {
        $outsideRejected = $true
    }
    if (-not $outsideRejected -or -not [System.IO.File]::Exists($outsidePath)) {
        throw 'Out-of-temp token path was not rejected without deleting the target.'
    }
    $outsideProgramDataRejected = $false
    try {
        Assert-NoDuckDnsReparsePoints -Path $outsidePath
    }
    catch {
        $outsideProgramDataRejected = $true
    }
    if (-not $outsideProgramDataRejected) {
        throw 'DuckDNS path guard accepted a path outside ProgramData.'
    }

    [System.IO.File]::WriteAllText($aclFile, 'non-secret-test-data')
    Set-ProtectedDuckDnsAcl -Path $aclFile
    $fileAcl = Get-Acl -LiteralPath $aclFile
    $fileSids = @($fileAcl.Access | ForEach-Object { $_.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value })
    if (-not $fileAcl.AreAccessRulesProtected -or $fileSids.Count -ne 2 -or $fileSids -notcontains 'S-1-5-18' -or $fileSids -notcontains 'S-1-5-32-544') {
        throw 'Protected file ACL does not contain only SYSTEM and Administrators.'
    }
    Assert-ProtectedDuckDnsAcl -Path $aclFile

    [void][System.IO.Directory]::CreateDirectory($aclDirectory)
    Set-ProtectedDuckDnsAcl -Path $aclDirectory -Directory
    $directoryAcl = Get-Acl -LiteralPath $aclDirectory
    $directorySids = @($directoryAcl.Access | ForEach-Object { $_.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value })
    if (-not $directoryAcl.AreAccessRulesProtected -or $directorySids.Count -ne 2 -or $directorySids -notcontains 'S-1-5-18' -or $directorySids -notcontains 'S-1-5-32-544') {
        throw 'Protected directory ACL does not contain only SYSTEM and Administrators.'
    }
    Assert-ProtectedDuckDnsAcl -Path $aclDirectory
    foreach ($rule in $directoryAcl.Access) {
        if (($rule.InheritanceFlags -band [System.Security.AccessControl.InheritanceFlags]::ContainerInherit) -eq 0 -or ($rule.InheritanceFlags -band [System.Security.AccessControl.InheritanceFlags]::ObjectInherit) -eq 0) {
            throw 'Protected directory ACL does not propagate to child files and directories.'
        }
    }

    $replaceSource = Join-Path $testRoot 'replacement-source.tmp'
    $replaceDestination = Join-Path $testRoot 'replacement-destination.json'
    $replaceBackup = Join-Path $testRoot 'replacement-backup.tmp'
    [System.IO.File]::WriteAllText($replaceSource, 'replacement-content')
    [System.IO.File]::WriteAllText($replaceDestination, 'original-content')
    [System.IO.File]::Replace($replaceSource, $replaceDestination, $replaceBackup)
    if ([System.IO.File]::ReadAllText($replaceDestination) -ne 'replacement-content' -or [System.IO.File]::ReadAllText($replaceBackup) -ne 'original-content' -or [System.IO.File]::Exists($replaceSource)) {
        throw 'Atomic replacement did not preserve the new destination and previous backup contents.'
    }
    [System.IO.File]::Delete($replaceBackup)

    [void][System.IO.Directory]::CreateDirectory($legacyRoot)
    [void][System.IO.Directory]::CreateDirectory($newRoot)
    [System.IO.File]::WriteAllText($legacyDuckDnsConfig, '{"token":"legacy-test"}')
    [System.IO.File]::WriteAllText($duckDnsConfig, '{"token":"new-test"}')
    Protect-LegacyDuckDnsConfig
    $legacyAcl = Get-Acl -LiteralPath $legacyDuckDnsConfig
    $legacySids = @($legacyAcl.Access | ForEach-Object { $_.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value })
    if (-not $legacyAcl.AreAccessRulesProtected -or $legacySids.Count -ne 2 -or $legacySids -notcontains 'S-1-5-18' -or $legacySids -notcontains 'S-1-5-32-544') {
        throw 'Legacy DuckDNS configuration was not protected before migration.'
    }

    function New-TestDuckDnsTask {
        param(
            [string]$Arguments,
            [string]$State = 'Ready',
            [string]$Execute = 'powershell.exe',
            [bool]$Enabled = $true,
            [string]$UserId = 'SYSTEM'
        )
        return [pscustomobject]@{
            Actions = @([pscustomobject]@{ Execute = $Execute; Arguments = $Arguments })
            Settings = [pscustomobject]@{ Enabled = $Enabled }
            Principal = [pscustomobject]@{ UserId = $UserId }
            State = $State
        }
    }
    function Get-ScheduledTask {
        param([string]$TaskName, [string]$ErrorAction)
        return $script:testDuckDnsTask
    }
    $expectedTaskArguments = "-NoProfile -ExecutionPolicy Bypass -File `"$duckDnsScript`" -ConfigPath `"$duckDnsConfig`""
    $script:testDuckDnsTask = New-TestDuckDnsTask -Arguments "-ConfigPath `"$legacyDuckDnsConfig`""
    $wrongTaskRejected = $false
    try {
        Remove-LegacyDuckDnsConfig
    }
    catch {
        $wrongTaskRejected = $true
    }
    if (-not $wrongTaskRejected -or -not [System.IO.File]::Exists($legacyDuckDnsConfig)) {
        throw 'Legacy configuration was removed before verifying the task target.'
    }

    $invalidTasks = @(
        (New-TestDuckDnsTask -Arguments $expectedTaskArguments -Execute 'cmd.exe'),
        (New-TestDuckDnsTask -Arguments $expectedTaskArguments -Enabled $false),
        (New-TestDuckDnsTask -Arguments $expectedTaskArguments -UserId 'BUILTIN\Users')
    )
    $multipleActionTask = New-TestDuckDnsTask -Arguments $expectedTaskArguments
    $multipleActionTask.Actions += [pscustomobject]@{ Execute = 'powershell.exe'; Arguments = $expectedTaskArguments }
    $invalidTasks += $multipleActionTask
    foreach ($invalidTask in $invalidTasks) {
        $script:testDuckDnsTask = $invalidTask
        $invalidTaskRejected = $false
        try {
            Remove-LegacyDuckDnsConfig
        }
        catch {
            $invalidTaskRejected = $true
        }
        if (-not $invalidTaskRejected -or -not [System.IO.File]::Exists($legacyDuckDnsConfig)) {
            throw 'Legacy credential cleanup accepted an invalid scheduled task.'
        }
    }

    $script:testDuckDnsTask = New-TestDuckDnsTask -Arguments $expectedTaskArguments -State 'Running'
    Remove-LegacyDuckDnsConfig
    if (-not [System.IO.File]::Exists($legacyDuckDnsConfig)) {
        throw 'Legacy configuration was removed while its old scheduled task was running.'
    }

    $script:testDuckDnsTask.State = 'Ready'
    $script:removedLegacyDuckDnsConfig = $null
    $script:allowCredentialRemoval = $false
    function Remove-Item {
        param([string]$LiteralPath, [switch]$Force)
        if ($LiteralPath -in @($script:legacyDuckDnsConfig, $script:duckDnsConfig)) {
            $script:removedLegacyDuckDnsConfig = $LiteralPath
            if (-not $script:allowCredentialRemoval) { return }
            if ($null -ne $script:testDuckDnsTask) {
                throw 'Credential deletion was attempted before the updater task was unregistered.'
            }
            & icacls.exe $LiteralPath /grant "*$($script:testUserSid.Value):F" | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw 'Could not grant the isolated test process access to remove its protected credential fixture.'
            }
        }
        Microsoft.PowerShell.Management\Remove-Item -LiteralPath $LiteralPath -Force:$Force
    }
    Remove-LegacyDuckDnsConfig
    if ($script:removedLegacyDuckDnsConfig -ne $legacyDuckDnsConfig -or -not [System.IO.File]::Exists($legacyDuckDnsConfig)) {
        throw 'Legacy configuration cleanup was not requested after verifying the new idle task target.'
    }

    $script:testDuckDnsTask = New-TestDuckDnsTask -Arguments $expectedTaskArguments -State 'Running'
    $script:stopDuckDnsTask = $false
    $script:unregisteredDuckDnsTask = $false
    $script:keepDuckDnsTaskRunning = $true
    $script:allowCredentialRemoval = $true
    function Stop-ScheduledTask {
        param([string]$TaskName, [string]$ErrorAction)
        $script:stopDuckDnsTask = $true
        if (-not $script:keepDuckDnsTaskRunning) { $script:testDuckDnsTask.State = 'Ready' }
    }
    function Start-Sleep {
        param([int]$Milliseconds)
    }
    function Unregister-ScheduledTask {
        param([string]$TaskName, [switch]$Confirm, [string]$ErrorAction)
        $script:unregisteredDuckDnsTask = $true
        $script:testDuckDnsTask = $null
    }
    $stopTimeoutRejected = $false
    try {
        Disable-DuckDns
    }
    catch {
        $stopTimeoutRejected = $true
    }
    if (-not $stopTimeoutRejected -or $null -eq $script:testDuckDnsTask -or -not [System.IO.File]::Exists($duckDnsConfig) -or -not [System.IO.File]::Exists($legacyDuckDnsConfig)) {
        throw 'DuckDNS disable removed credentials before the running updater stopped.'
    }

    $script:keepDuckDnsTaskRunning = $false
    $script:stopDuckDnsTask = $false
    Disable-DuckDns
    if (-not $script:stopDuckDnsTask -or -not $script:unregisteredDuckDnsTask -or $null -ne $script:testDuckDnsTask) {
        throw 'Disabling DuckDNS did not stop and unregister the updater before credential cleanup.'
    }
    if ([System.IO.File]::Exists($duckDnsConfig) -or [System.IO.File]::Exists($legacyDuckDnsConfig)) {
        throw 'DuckDNS credentials remained after the updater was removed.'
    }

    Write-Output 'DuckDNS credential cleanup and ACL behavior tests passed.'
}
finally {
    $env:TEMP = $originalTemp
    $programDataRoot = $originalProgramData
    $currentUserSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    if ([System.IO.Directory]::Exists($aclDirectory)) {
        & icacls.exe $aclDirectory /grant "*$($currentUserSid):(OI)(CI)F" | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not restore cleanup access to the isolated ACL test directory.'
        }
    }
    if ([System.IO.Directory]::Exists($tokenDirectory)) {
        & icacls.exe $tokenDirectory /grant "*$($currentUserSid):(OI)(CI)F" | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not restore cleanup access to the isolated token directory.'
        }
    }
    if ([System.IO.Directory]::Exists($newRoot)) {
        & icacls.exe $newRoot /grant "*$($currentUserSid):(OI)(CI)F" | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not restore cleanup access to the isolated secure-root test directory.'
        }
    }
    if ([System.IO.File]::Exists($legacyDuckDnsConfig)) {
        & icacls.exe $legacyDuckDnsConfig /grant "*$($currentUserSid):F" | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not restore cleanup access to the isolated legacy-config test file.'
        }
    }
    if ([System.IO.Directory]::Exists($testRoot)) {
        [System.IO.Directory]::Delete($testRoot, $true)
    }
    if ([System.IO.Directory]::Exists($outsideRoot)) {
        [System.IO.Directory]::Delete($outsideRoot, $true)
    }
}
