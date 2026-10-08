function Get-AndroidAvdSearchDirectories {
    [CmdletBinding()]
    param(
        [AllowEmptyString()]
        [string] $AndroidAvdHome,

        [AllowEmptyString()]
        [string] $AndroidUserHome,

        [AllowEmptyString()]
        [string] $UserProfile,

        [AllowEmptyString()]
        [string] $HomeDirectory,

        [AllowEmptyString()]
        [string] $AndroidSdkHome
    )

    $directories = [System.Collections.Generic.List[string]]::new()
    if (-not [string]::IsNullOrWhiteSpace($AndroidAvdHome)) {
        $directories.Add($AndroidAvdHome)
    }
    if (-not [string]::IsNullOrWhiteSpace($AndroidUserHome)) {
        $directories.Add((Join-Path $AndroidUserHome 'avd'))
    }
    if (-not [string]::IsNullOrWhiteSpace($UserProfile)) {
        $directories.Add((Join-Path $UserProfile '.android\avd'))
    }
    if (-not [string]::IsNullOrWhiteSpace($HomeDirectory)) {
        $directories.Add((Join-Path $HomeDirectory '.android\avd'))
    }
    if (-not [string]::IsNullOrWhiteSpace($AndroidSdkHome)) {
        $directories.Add((Join-Path $AndroidSdkHome '.android\avd'))
    }

    return $directories.ToArray()
}

function Resolve-AndroidAvdHome {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $AvdName,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]] $SearchDirectories
    )

    $visited = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($directory in $SearchDirectories) {
        if ([string]::IsNullOrWhiteSpace($directory) -or -not $visited.Add($directory)) {
            continue
        }

        $configuration = Join-Path $directory "$AvdName.ini"
        if (Test-Path -LiteralPath $configuration -PathType Leaf) {
            return [System.IO.Path]::GetFullPath($directory)
        }
    }

    throw "Could not find AVD configuration '$AvdName.ini'. Check ANDROID_AVD_HOME, ANDROID_USER_HOME, and the user profile paths."
}
