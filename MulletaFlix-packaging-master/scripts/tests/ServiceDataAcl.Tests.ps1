$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$installerPath = Join-Path $repositoryRoot 'MulletaFlix-packaging-master\MulletaFlix-ux-custom\nsis\mulletaflix.nsi'
$smokePath = Join-Path $repositoryRoot 'MulletaFlix-packaging-master\scripts\Test-CleanInstaller.ps1'
$installer = [System.IO.File]::ReadAllText($installerPath)
$smoke = [System.IO.File]::ReadAllText($smokePath)

if ($installer -notmatch '!define MUI_PAGE_CUSTOMFUNCTION_LEAVE ValidateDataDirectory' -or
    $installer -notmatch '(?s)Function ValidateDataDirectory\s+.*?GetFullPathName\s+\$R1\s+"\$_MULLETAFLIXDATADIR_".*?\$\{GetRoot\}.*?lstrcmpiW.*?InvalidDataDirectory.*?FunctionEnd' -or
    $installer -match 'StrCmp\s+/I') {
    throw 'Installer must reject a data directory that resolves to a drive or share root.'
}

if ($installer -notmatch '(?s)Function RejectReparseDataDirectory\s+.*?REPARSE_POINT.*?FunctionEnd') {
    throw 'Installer must reject a data-directory reparse point before changing its ACL.'
}

$serviceSection = [regex]::Match($installer, '(?s)Section "MulletaFlix Server Service" InstallService(.*?)SectionEnd').Groups[1].Value
if (-not $serviceSection -or
    $serviceSection -notmatch '/inheritance:r' -or
    $serviceSection -notmatch '/remove:g\s+\*S-1-5-32-545\s+\*S-1-1-0\s+\*S-1-5-11' -or
    $serviceSection -notmatch '\*S-1-5-32-544:\(OI\)\(CI\)F' -or
    $serviceSection -notmatch '\*S-1-5-18:\(OI\)\(CI\)F' -or
    $serviceSection -match '/T') {
    throw 'Service data ACL must remove inherited/broad grants, retain administrator/system access, and avoid recursive traversal.'
}

$basicBlock = [regex]::Match($installer, '(?s)Basic installs run as the interactive user(.*?)\$\{EndIf\}').Groups[1].Value
if (-not $basicBlock -or $basicBlock -match '/T') {
    throw 'Basic data ACL must not recursively traverse the selected directory tree.'
}

if ($smoke -notmatch 'function Test-NoBroadWriteAccess' -or
    $smoke -notmatch 'Test-NoBroadWriteAccess -Path \$dataDirectory' -or
    $smoke -notmatch 'Test-NoBroadWriteAccess -Path \$configFile') {
    throw 'Clean installer smoke test must reject broad write ACEs at the data root and in config.'
}

$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "mfx-service-acl-$([guid]::NewGuid().ToString('N'))"
$parent = Join-Path $fixtureRoot 'parent'
$dataRoot = Join-Path $parent 'server-data'
$profileDirectory = Join-Path $dataRoot 'config\users\profile'
$profileImage = Join-Path $profileDirectory 'profile.jpg'
$outside = Join-Path $parent 'outside-sentinel'
$currentSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$broadSids = @('S-1-1-0', 'S-1-5-11', 'S-1-5-32-545')
$serviceSids = @('S-1-5-18', 'S-1-5-19', 'S-1-5-20')
$writeRights = [System.Security.AccessControl.FileSystemRights]::Write -bor
    [System.Security.AccessControl.FileSystemRights]::WriteData -bor
    [System.Security.AccessControl.FileSystemRights]::AppendData -bor
    [System.Security.AccessControl.FileSystemRights]::Delete -bor
    [System.Security.AccessControl.FileSystemRights]::ChangePermissions -bor
    [System.Security.AccessControl.FileSystemRights]::TakeOwnership

try {
    New-Item -ItemType Directory -Path $profileDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $outside -Force | Out-Null
    [System.IO.File]::WriteAllText($profileImage, 'fixture')
    [System.IO.File]::WriteAllText((Join-Path $outside 'sentinel.txt'), 'outside')

    & icacls.exe $parent '/grant' '*S-1-5-32-545:(OI)(CI)M' '*S-1-1-0:(OI)(CI)M' '*S-1-5-11:(OI)(CI)M' '/Q' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not seed broad inherited fixture ACLs.' }

    & icacls.exe $dataRoot '/grant' ("*$currentSid`:(OI)(CI)F") '*S-1-5-32-545:(OI)(CI)M' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-18:(OI)(CI)F' '*S-1-5-19:(OI)(CI)M' '*S-1-5-20:(OI)(CI)M' '/Q' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not seed explicit fixture ACLs.' }

    & icacls.exe $dataRoot '/inheritance:r' '/remove:g' '*S-1-5-32-545' '*S-1-1-0' '*S-1-5-11' '/grant' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-18:(OI)(CI)F' '*S-1-5-20:(OI)(CI)M' '*S-1-5-19:(OI)(CI)M' '/Q' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The non-recursive service ACL operation failed.' }

    foreach ($path in @($dataRoot, (Join-Path $dataRoot 'config'), (Join-Path $dataRoot 'config\users'), $profileDirectory, $profileImage)) {
        foreach ($rule in (Get-Acl -LiteralPath $path).Access) {
            $sid = $null
            try {
                $sid = $rule.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value
            } catch {
                $sid = $rule.IdentityReference.Value
            }

            $rights = [System.Security.AccessControl.FileSystemRights]$rule.FileSystemRights
            if ($sid -in $broadSids -and $rule.AccessControlType -eq 'Allow' -and (($rights -band $writeRights) -ne 0)) {
                throw "Broad write ACE survived on '$path' for SID '$sid'."
            }
        }
    }

    foreach ($path in @($dataRoot, $profileImage)) {
        $aclSids = foreach ($rule in (Get-Acl -LiteralPath $path).Access) {
            try { $rule.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value }
            catch { $rule.IdentityReference.Value }
        }
        foreach ($sid in $serviceSids) {
            if ($sid -notin $aclSids) { throw "Supported service SID '$sid' is missing from '$path'." }
        }
    }

    $outsideSids = foreach ($rule in (Get-Acl -LiteralPath $outside).Access) {
        try { $rule.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value }
        catch { $rule.IdentityReference.Value }
    }
    if ('S-1-5-32-545' -notin $outsideSids) {
        throw 'The ACL operation changed the outside sentinel instead of staying on the selected data root.'
    }

    Write-Host 'Service data ACL tests passed: broad inherited and explicit root grants removed; child inheritance preserved; outside sentinel unchanged.'
} finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        $resolvedFixtureRoot = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $fixtureRoot).Path)
        $resolvedTempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        if (-not $resolvedFixtureRoot.StartsWith($resolvedTempRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'Refusing to remove a fixture outside the system temporary directory.'
        }
        Remove-Item -LiteralPath $resolvedFixtureRoot -Recurse -Force
    }
}
