function Get-AndroidDeviceProfile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]] $FeatureLines,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]] $DisplaySizeLines,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]] $DisplayDensityLines
    )

    if ($FeatureLines -match 'feature:android\.software\.leanback\b') {
        return 'TV'
    }

    $sizeMatches = @(
        foreach ($line in $DisplaySizeLines) {
            if ($line -match '(?:Physical|Override) size:\s*(\d+)x(\d+)') {
                [pscustomobject]@{ Width = [int]$Matches[1]; Height = [int]$Matches[2] }
            }
        }
    )
    $densityMatches = @(
        foreach ($line in $DisplayDensityLines) {
            if ($line -match '(?:Physical|Override) density:\s*(\d+)') {
                [int]$Matches[1]
            }
        }
    )
    if ($sizeMatches.Count -eq 0 -or $densityMatches.Count -eq 0) {
        throw 'Could not determine Android device size and density for profile validation.'
    }

    # wm reports physical first and an override second; the last values are active.
    $activeSize = $sizeMatches[-1]
    $activeDensity = $densityMatches[-1]
    if ($activeDensity -le 0) {
        throw 'Android display density must be greater than zero.'
    }

    $smallestWidthDp = [math]::Floor(([math]::Min($activeSize.Width, $activeSize.Height) * 160.0) / $activeDensity)
    if ($smallestWidthDp -ge 600) { return 'TABLET' }
    return 'PHONE'
}

function Assert-AndroidDeviceProfile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('PHONE', 'TABLET', 'TV')]
        [string] $Expected,

        [Parameter(Mandatory = $true)]
        [ValidateSet('PHONE', 'TABLET', 'TV')]
        [string] $Actual
    )

    if ($Actual -ne $Expected) {
        throw "Expected Android device profile '$Expected', but connected AVD is '$Actual'."
    }
}

function Get-ExpectedAndroidDeviceProfile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]] $CommandArguments,

        [Parameter(Mandatory = $true)]
        [bool] $Required
    )

    $profileArguments = @($CommandArguments | Where-Object {
        $_ -match '^-Pandroid\.testInstrumentationRunnerArguments\.expectedDeviceProfile='
    })
    if ($profileArguments.Count -gt 1) {
        throw 'Set expectedDeviceProfile only once for an emulator test command.'
    }
    if ($profileArguments.Count -eq 0) {
        if ($Required) {
            throw 'Instrumented emulator tests require expectedDeviceProfile=PHONE, TABLET or TV.'
        }
        return $null
    }

    $profile = ($profileArguments[0] -split '=', 2)[1].ToUpperInvariant()
    if ($profile -notin @('PHONE', 'TABLET', 'TV')) {
        throw "Unsupported expectedDeviceProfile '$profile'; use PHONE, TABLET or TV."
    }
    return $profile
}

function Get-AndroidInstrumentationTasks {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]] $CommandArguments
    )

    $connectedTestTasks = @($CommandArguments | Where-Object {
        $_ -match '^(?::[^:]+)*:connected[A-Za-z0-9]*$' -or
            $_ -match '^connected[A-Za-z0-9]*$'
    })
    $supportedTasks = @($connectedTestTasks | Where-Object {
        $_ -match '^:[^:]+(?::[^:]+)*:connectedDebugAndroidTest$'
    })
    $unsupportedTasks = @($connectedTestTasks | Where-Object { $_ -notin $supportedTasks })
    $abbreviatedInstrumentationTasks = @($CommandArguments | Where-Object {
        $_ -cmatch '^:[^:]+(?::[^:]+)*:c[A-Z][A-Za-z0-9]*$'
    })
    if ($abbreviatedInstrumentationTasks.Count -gt 0) {
        throw "Gradle task abbreviations are not allowed by the emulator wrapper: $($abbreviatedInstrumentationTasks -join ', '). Use :module:connectedDebugAndroidTest."
    }
    if ($unsupportedTasks.Count -gt 0) {
        throw "Unsupported connected instrumentation task(s): $($unsupportedTasks -join ', '). Use module-qualified :module:connectedDebugAndroidTest tasks."
    }
    return $supportedTasks
}
