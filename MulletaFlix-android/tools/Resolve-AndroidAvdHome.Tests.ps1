. (Join-Path $PSScriptRoot 'Resolve-AndroidAvdHome.ps1')

Describe 'Get-AndroidAvdSearchDirectories' {
    It 'returns emulator search paths in documented precedence order' {
        $directories = Get-AndroidAvdSearchDirectories `
            -AndroidAvdHome 'D:\avd-data' `
            -AndroidUserHome 'C:\android-user' `
            -UserProfile 'C:\Users\Raphael' `
            -HomeDirectory '' `
            -AndroidSdkHome 'C:\legacy-sdk-home'

        $directories | Should Be @(
            'D:\avd-data',
            'C:\android-user\avd',
            'C:\Users\Raphael\.android\avd',
            'C:\legacy-sdk-home\.android\avd'
        )
    }
}

Describe 'Resolve-AndroidAvdHome' {
    It 'skips a stale override and finds the AVD ini in the user directory' {
        $staleOverride = Join-Path $TestDrive 'avd-data'
        $userAvdDirectory = Join-Path $TestDrive 'user\.android\avd'
        New-Item -ItemType Directory -Path $staleOverride -Force | Out-Null
        New-Item -ItemType Directory -Path $userAvdDirectory -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $userAvdDirectory 'MulletaflixTvApi34.ini') -Value 'path=D:\Android\avd\MulletaflixTvApi34.avd'

        Resolve-AndroidAvdHome `
            -AvdName 'MulletaflixTvApi34' `
            -SearchDirectories @($staleOverride, $userAvdDirectory) |
            Should Be ([System.IO.Path]::GetFullPath($userAvdDirectory))
    }

    It 'prefers an explicit override when it contains the requested AVD ini' {
        $explicitAvdDirectory = Join-Path $TestDrive 'explicit'
        $fallbackDirectory = Join-Path $TestDrive 'fallback'
        New-Item -ItemType Directory -Path $explicitAvdDirectory,$fallbackDirectory -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $explicitAvdDirectory 'MulletaflixTvApi34.ini') -Value 'path=D:\Android\avd\MulletaflixTvApi34.avd'
        Set-Content -LiteralPath (Join-Path $fallbackDirectory 'MulletaflixTvApi34.ini') -Value 'path=D:\Android\avd\MulletaflixTvApi34.avd'

        Resolve-AndroidAvdHome `
            -AvdName 'MulletaflixTvApi34' `
            -SearchDirectories @($explicitAvdDirectory, $fallbackDirectory) |
            Should Be ([System.IO.Path]::GetFullPath($explicitAvdDirectory))
    }

    It 'fails immediately when no search directory contains the requested AVD ini' {
        $missingDirectory = Join-Path $TestDrive 'missing'
        New-Item -ItemType Directory -Path $missingDirectory -Force | Out-Null

        $message = $null
        try {
            Resolve-AndroidAvdHome -AvdName 'MissingAvd' -SearchDirectories @($missingDirectory)
        } catch {
            $message = $_.Exception.Message
        }

        $message -like "*Could not find AVD configuration 'MissingAvd.ini'*" | Should Be $true
    }
}
