import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import { dirname, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const installerPath = resolve(repositoryRoot, 'MulletaFlix-packaging-master/MulletaFlix-ux-custom/nsis/mulletaflix.nsi');
const configuratorPath = resolve(repositoryRoot, 'tools/release/configure-https.ps1');
const updaterPath = resolve(repositoryRoot, 'tools/release/duckdns-update.ps1');

const [installer, configurator, updater] = await Promise.all([
    readFile(installerPath, 'utf8'),
    readFile(configuratorPath, 'utf8'),
    readFile(updaterPath, 'utf8')
]);

test('scheduled DuckDNS updater accepts protected config without a Token argument', async () => {
    const tempRoot = await mkdtemp(resolve(tmpdir(), 'mulletaflix-duckdns-bind-'));
    const configPath = resolve(tempRoot, 'config.json');
    try {
        await writeFile(configPath, JSON.stringify({ subdomain: 'INVALID_SUBDOMAIN', token: 'test-secret' }), 'utf8');
        const result = spawnSync('powershell.exe', [
            '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', updaterPath,
            '-ConfigPath', configPath
        ], { encoding: 'utf8', timeout: 15000 });

        assert.equal(result.error, undefined, result.error?.message);
        assert.notEqual(result.status, 0, 'invalid test subdomain must stop before the network request');
        assert.match(`${result.stdout}\n${result.stderr}`, /subdomain must contain only lowercase/i,
            'script must bind ConfigPath and reach validation instead of failing on missing Token');
        assert.doesNotMatch(`${result.stdout}\n${result.stderr}`, /test-secret/);
    } finally {
        await rm(tempRoot, { recursive: true, force: true });
    }
});

test('direct DuckDNS updater mode binds credentials and requires a token without config', () => {
    assert.doesNotMatch(updater, /\[Parameter\(Mandatory\s*=\s*\$true\)\][\s\S]*\[string\]\$Token/i,
        'mandatory Token binding would prevent scheduled ConfigPath-only execution');
    assert.match(updater, /elseif \(\[string\]::IsNullOrWhiteSpace\(\$Token\)\)[\s\S]*?DuckDNS token is required when no protected configuration file is provided/);

    const validCredentialBinding = spawnSync('powershell.exe', [
        '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', updaterPath,
        '-Subdomain', 'INVALID_SUBDOMAIN', '-Token', 'test-secret'
    ], { encoding: 'utf8', timeout: 15000 });
    assert.equal(validCredentialBinding.error, undefined, validCredentialBinding.error?.message);
    assert.notEqual(validCredentialBinding.status, 0);
    assert.match(`${validCredentialBinding.stdout}\n${validCredentialBinding.stderr}`, /subdomain must contain only lowercase/i,
        'direct Subdomain and Token parameters must bind before validation');

    const missingToken = spawnSync('powershell.exe', [
        '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', updaterPath,
        '-Subdomain', 'valid-subdomain'
    ], { encoding: 'utf8', timeout: 15000 });
    assert.notEqual(missingToken.status, 0);
    assert.match(`${missingToken.stdout}\n${missingToken.stderr}`, /token is required when no protected configuration/i);
});

test('Windows installer never passes the DuckDNS token as a process argument', () => {
    assert.doesNotMatch(installer, /-DuckDnsToken\s+"\$_DUCKDNSTOKEN_"/i);
    assert.match(installer, /-DuckDnsTokenFile\s+"\$_DUCKDNSTOKENFILE_"/i);
});

test('installer prepares protected ProgramData token staging before writing the token and clears it after use', () => {
    const prepareIndex = installer.indexOf('-PrepareDuckDnsTokenFile');
    const openIndex = installer.indexOf('FileOpen $R9 "$_DUCKDNSTOKENFILE_" w');
    const processIndex = installer.indexOf('-DuckDnsTokenFile "$_DUCKDNSTOKENFILE_"');
    const deleteIndex = installer.indexOf('Delete "$_DUCKDNSTOKENFILE_"');
    const clearIndex = installer.indexOf('StrCpy $_DUCKDNSTOKEN_ ""', deleteIndex);

    assert.notEqual(prepareIndex, -1, 'configurator must securely prepare the protected token file');
    assert.ok(prepareIndex < openIndex, 'protected token staging must be prepared before writing the secret');
    assert.ok(openIndex < processIndex, 'token file must be written before the configurator starts');
    assert.match(installer, /FileWriteUTF16LE \/BOM \$R9 \$_DUCKDNSTOKEN_/, 'token staging must use BOM-marked UTF-16LE for the PowerShell reader');
    assert.match(installer, /FileClose \$R9\s+IfErrors ConfigureHttpsTokenWriteFailed/);
    assert.ok(processIndex < deleteIndex, 'token file must be deleted after the configurator exits');
    assert.ok(deleteIndex < clearIndex, 'installer memory copy must be cleared after use');
    assert.match(installer, /\$COMMONPROGRAMDATA\\MulletaFlix-DuckDNS\\installer-token\.txt/);
    assert.match(installer, /ClearErrors\s+Delete "\$_DUCKDNSTOKENFILE_"\s+IfErrors ConfigureHttpsTokenCleanupFailed/);
    assert.match(installer, /ConfigureHttpsTokenFileFailed:[\s\S]*?ClearErrors\s+Delete "\$_DUCKDNSTOKENFILE_"\s+IfErrors ConfigureHttpsTokenCleanupFailed/);
    assert.doesNotMatch(installer, /icacls\.exe.*mflx-duckdns/i);
    assert.match(installer, /ConfigureHttpsTokenFileFailed:[\s\S]*?\$R8 == "Yes"[\s\S]*?Delete "\$_DUCKDNSTOKENFILE_"/);
});

test('HTTPS configurator accepts only a protected credential-root token file and removes it after reading', () => {
    const parameterBlock = configurator.match(/param\(([\s\S]*?)\n\)/i)?.[1] ?? '';
    const readIndex = configurator.indexOf('[MulletaFlix.DuckDns.SafeTokenFile]::ReadAllText($tokenFilePath)');
    const cleanupIndex = configurator.indexOf('Remove-Item -LiteralPath $tokenFilePath -Force -ErrorAction SilentlyContinue');

    assert.match(parameterBlock, /\[string\]\$DuckDnsTokenFile/i);
    assert.match(parameterBlock, /\[switch\]\$PrepareDuckDnsTokenFile/i);
    assert.doesNotMatch(parameterBlock, /\$DuckDnsToken\s*,/i);
    assert.match(configurator, /\$tokenFilePath\.StartsWith\(\$configRoot/);
    assert.match(configurator, /FileAttributes\]::ReparsePoint/);
    assert.notEqual(readIndex, -1, 'configurator must read the token file');
    assert.ok(readIndex < cleanupIndex, 'token file cleanup must follow the read');
    assert.match(configurator.slice(readIndex, cleanupIndex), /finally\s*\{/);
    assert.match(configurator, /Assert-ProtectedDuckDnsAcl -Path \$tokenFilePath/);
    assert.match(configurator, /ReadAllText\(\$tokenFilePath\)\.TrimEnd\(\[char\[\]\]"`r`n"\)/);
});

test('credential storage rejects non-elevated setup and refuses to harden a pre-created root in place', () => {
    const prepareBranch = configurator.indexOf('if ($PrepareDuckDnsTokenFile)');
    const administratorGuard = configurator.indexOf('Assert-DuckDnsAdministrator', prepareBranch);

    assert.notEqual(prepareBranch, -1, 'prepare-only execution branch must exist');
    assert.ok(administratorGuard > prepareBranch, 'admin elevation must be checked before creating credential storage');
    assert.match(configurator, /function Assert-DuckDnsAdministrator[\s\S]*?IsInRole\([\s\S]*?Administrator[\s\S]*?throw/);
    assert.match(configurator, /if \(-not \[System\.IO\.Directory\]::Exists\(\$duckDnsConfigRoot\)\)[\s\S]*?CreateDirectory\(\$duckDnsConfigRoot, \$secureDirectoryAcl\)/);
    assert.match(configurator, /Assert-ProtectedDuckDnsAcl -Path \$duckDnsConfigRoot/);
    assert.match(configurator, /ownerSid -notin @\('S-1-5-18', 'S-1-5-32-544'\)/);
    assert.match(configurator, /\$acl\.SetOwner\(\$systemSid\)/);
    assert.match(configurator, /\$secureDirectoryAcl\.SetOwner\(\$systemSid\)/);
});

test('DuckDNS configuration is protected before secret bytes are written and replaced atomically', () => {
    const rootAclIndex = configurator.indexOf('[System.IO.Directory]::CreateDirectory($duckDnsConfigRoot, $secureDirectoryAcl)');
    const createIndex = configurator.indexOf('[System.IO.File]::Open($configTempFile');
    const aclIndex = configurator.indexOf('Set-ProtectedDuckDnsAcl -Path $configTempFile');
    const writeIndex = configurator.indexOf('[System.IO.File]::WriteAllText($configTempFile, $configJson');
    const destinationAclIndex = configurator.match(/Set-ProtectedDuckDnsAcl -Path \$duckDnsConfig(?=\r?\n)/)?.index ?? -1;
    const replaceIndex = configurator.indexOf('[System.IO.File]::Replace($configTempFile, $duckDnsConfig, $configBackupFile)');
    const moveIndex = configurator.indexOf('[System.IO.File]::Move($configTempFile, $duckDnsConfig)');

    assert.ok(rootAclIndex < createIndex, 'dedicated DuckDNS directory must be atomically created with protected ACLs before creating files');
    assert.match(configurator, /Environment\]::GetFolderPath\(\[System\.Environment\+SpecialFolder\]::CommonApplicationData\)/);
    assert.match(configurator, /Join-Path \$programDataRoot 'MulletaFlix-DuckDNS'/);
    assert.doesNotMatch(configurator, /\$\{env:ProgramData\}/);
    assert.ok(createIndex < aclIndex, 'empty staging file must exist before ACL hardening');
    assert.ok(aclIndex < writeIndex, 'secret must not be written before ACL hardening');
    assert.ok(writeIndex < destinationAclIndex && destinationAclIndex < replaceIndex, 'existing destination ACL must be hardened before replacement');
    assert.ok(writeIndex < moveIndex, 'new configuration must be moved after complete write');
    assert.match(configurator, /ConvertTo-Json -Depth 3 -Compress/);
    assert.match(configurator, /Remove-Item -LiteralPath \$configTempFile -Force -ErrorAction SilentlyContinue/);
    assert.match(configurator, /Remove-Item -LiteralPath \$configBackupFile -Force -ErrorAction SilentlyContinue/);
});

test('DuckDNS subdomain validation runs before the destructive one-time token read', () => {
    const configureStart = configurator.indexOf('function Configure-DuckDns');
    const configureEnd = configurator.indexOf('\nfunction ', configureStart + 1);
    const configureBlock = configurator.slice(configureStart, configureEnd === -1 ? undefined : configureEnd);
    const validationIndex = configureBlock.indexOf('if ($DuckDnsSubdomain -notmatch');
    const prerequisiteIndex = configureBlock.indexOf("Assert-File $duckDnsScript 'Bundled DuckDNS updater'");
    const consumeIndex = configureBlock.indexOf('Read-DuckDnsTokenFile $DuckDnsTokenFile');

    assert.notEqual(validationIndex, -1);
    assert.notEqual(prerequisiteIndex, -1);
    assert.notEqual(consumeIndex, -1);
    assert.ok(validationIndex < prerequisiteIndex && prerequisiteIndex < consumeIndex, 'invalid subdomain or missing updater must not consume and delete the staged token');
});

test('legacy DuckDNS secret is protected before migration and removed only after task verification', () => {
    const configureIndex = configurator.indexOf('function Configure-DuckDns');
    const ensureRootIndex = configurator.indexOf('Ensure-ProtectedDuckDnsConfigRoot', configureIndex);
    const protectLegacyIndex = configurator.indexOf('Protect-LegacyDuckDnsConfig', ensureRootIndex);
    const writeConfigIndex = configurator.indexOf('[System.IO.File]::WriteAllText($configTempFile', protectLegacyIndex);
    const registerTaskIndex = configurator.indexOf('Register-ScheduledTask -TaskName $duckDnsTask');
    const removeLegacyIndex = configurator.indexOf('Remove-LegacyDuckDnsConfig', registerTaskIndex);

    assert.match(configurator, /\$legacyDuckDnsConfig = Join-Path \$programDataRoot 'MulletaFlix\\duckdns\.json'/);
    assert.match(configurator, /function Protect-LegacyDuckDnsConfig[\s\S]*?Set-ProtectedDuckDnsAcl -Path \$legacyDuckDnsConfig/);
    assert.match(configurator, /function Remove-LegacyDuckDnsConfig[\s\S]*?Get-ScheduledTask -TaskName \$duckDnsTask/);
    assert.match(configurator, /\$expectedArguments = "-NoProfile -ExecutionPolicy Bypass -File `"\$duckDnsScript`" -ConfigPath `"\$duckDnsConfig`""/);
    assert.match(configurator, /\$task\.Settings\.Enabled[\s\S]*\$actions\.Count -ne 1[\s\S]*\$actions\[0\]\.Execute[\s\S]*\$actions\[0\]\.Arguments -ine \$expectedArguments[\s\S]*\$principal -notin/);
    assert.match(configurator, /if \(\$task\.State -eq 'Running'\)[\s\S]*?return/);
    assert.ok(ensureRootIndex !== -1 && ensureRootIndex < protectLegacyIndex && protectLegacyIndex < writeConfigIndex, 'protected root and legacy credential must be hardened before writing the replacement');
    assert.ok(registerTaskIndex !== -1 && removeLegacyIndex > registerTaskIndex, 'legacy credential cleanup must follow task registration');
});

test('disabling HTTPS also removes the DuckDNS updater and stored credential', () => {
    const disabledSection = installer.slice(installer.indexOf('Section "-configure HTTPS"'), installer.indexOf('SectionEnd', installer.indexOf('Section "-configure HTTPS"')));
    const disableCallIndex = disabledSection.indexOf('-DisableDuckDns');
    const cleanupTaskIndex = configurator.indexOf('function Disable-DuckDns');
    const unregisterIndex = configurator.indexOf('Unregister-ScheduledTask -TaskName $duckDnsTask');
    const cleanupConfigIndex = configurator.indexOf('foreach ($configPath in @($duckDnsConfig, $legacyDuckDnsConfig))');

    assert.notEqual(disableCallIndex, -1, 'unchecked HTTPS setup must invoke DuckDNS cleanup');
    assert.notEqual(cleanupTaskIndex, -1, 'configurator must provide an explicit DuckDNS disable path');
    assert.ok(unregisterIndex > cleanupTaskIndex && cleanupConfigIndex > unregisterIndex, 'task removal must precede credential cleanup');
});

test('token read opens the same leaf without following reparse points or allowing replacement', () => {
    assert.match(configurator, /CreateFile\(path, GenericRead, 0,[\s\S]*OpenReparsePoint/);
    assert.match(configurator, /GetFileInformationByHandle\(handle, out information\)/);
    assert.match(configurator, /information\.FileAttributes & FileAttributeReparsePoint/);
    assert.match(configurator, /Assert-ProtectedDuckDnsAcl -Path \$tokenDirectory/);
    assert.match(configurator, /\[MulletaFlix\.DuckDns\.SafeTokenFile\]::ReadAllText\(\$tokenFilePath\)/);
});
