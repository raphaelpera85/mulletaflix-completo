. (Join-Path $PSScriptRoot 'Assert-AndroidDeviceProfile.ps1')

Describe 'Get-AndroidDeviceProfile' {
    It 'identifies Android TV from the leanback system feature' {
        Get-AndroidDeviceProfile `
            -FeatureLines @('feature:android.software.leanback') `
            -DisplaySizeLines @('Physical size: 1920x1080') `
            -DisplayDensityLines @('Physical density: 320') |
            Should Be 'TV'
    }

    It 'identifies a tablet from its smallest screen width in dp' {
        Get-AndroidDeviceProfile `
            -FeatureLines @('feature:android.hardware.touchscreen') `
            -DisplaySizeLines @('Physical size: 2560x1600') `
            -DisplayDensityLines @('Physical density: 320') |
            Should Be 'TABLET'
    }

    It 'identifies a phone from its smallest screen width in dp' {
        Get-AndroidDeviceProfile `
            -FeatureLines @('feature:android.hardware.touchscreen') `
            -DisplaySizeLines @('Physical size: 1080x2400') `
            -DisplayDensityLines @('Physical density: 420') |
            Should Be 'PHONE'
    }

    It 'uses active override size and density when Android reports them' {
        Get-AndroidDeviceProfile `
            -FeatureLines @('feature:android.hardware.touchscreen') `
            -DisplaySizeLines @('Physical size: 1080x2400', 'Override size: 1200x1920') `
            -DisplayDensityLines @('Physical density: 420', 'Override density: 320') |
            Should Be 'TABLET'
    }

    It 'rejects missing display metrics instead of guessing a profile' {
        $message = $null
        try {
            Get-AndroidDeviceProfile `
                -FeatureLines @('feature:android.hardware.touchscreen') `
                -DisplaySizeLines @() `
                -DisplayDensityLines @()
        } catch {
            $message = $_.Exception.Message
        }
        $message -like '*Could not determine Android device size and density*' | Should Be $true
    }
}

Describe 'Assert-AndroidDeviceProfile' {
    It 'rejects a test run on the wrong device profile' {
        $message = $null
        try {
            Assert-AndroidDeviceProfile -Expected TV -Actual PHONE
        } catch {
            $message = $_.Exception.Message
        }
        $message -like "*Expected Android device profile 'TV', but connected AVD is 'PHONE'*" | Should Be $true
    }

    It 'accepts a matching device profile' {
        { Assert-AndroidDeviceProfile -Expected TABLET -Actual TABLET } | Should Not Throw
    }
}

Describe 'Get-ExpectedAndroidDeviceProfile' {
    It 'requires a profile for instrumentation commands' {
        $message = $null
        try {
            Get-ExpectedAndroidDeviceProfile -CommandArguments @(':app:connectedDebugAndroidTest') -Required $true
        } catch {
            $message = $_.Exception.Message
        }
        $message -like '*require expectedDeviceProfile=PHONE, TABLET or TV*' | Should Be $true
    }

    It 'allows commands without instrumentation to omit a profile' {
        Get-ExpectedAndroidDeviceProfile -CommandArguments @(':app:assembleDebug') -Required $false |
            Should Be $null
    }

    It 'normalizes the expected profile' {
        Get-ExpectedAndroidDeviceProfile `
            -CommandArguments @(':feature:home:connectedDebugAndroidTest', '-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=tv') `
            -Required $true | Should Be 'TV'
    }

    It 'rejects duplicate or unsupported profile arguments' {
        $duplicateMessage = $null
        try {
            Get-ExpectedAndroidDeviceProfile `
                -CommandArguments @('-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=TV', '-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=TV') `
                -Required $true
        } catch {
            $duplicateMessage = $_.Exception.Message
        }

        $unsupportedMessage = $null
        try {
            Get-ExpectedAndroidDeviceProfile `
                -CommandArguments @('-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=watch') `
                -Required $true
        } catch {
            $unsupportedMessage = $_.Exception.Message
        }

        $duplicateMessage -like '*Set expectedDeviceProfile only once*' | Should Be $true
        $unsupportedMessage -like '*Unsupported expectedDeviceProfile*' | Should Be $true
    }
}

Describe 'Get-AndroidInstrumentationTasks' {
    It 'identifies every module-qualified connected debug instrumentation task' {
        Get-AndroidInstrumentationTasks `
            -CommandArguments @(':feature:home:connectedDebugAndroidTest', ':app:connectedDebugAndroidTest') |
            Should Be @(':feature:home:connectedDebugAndroidTest', ':app:connectedDebugAndroidTest')
    }

    It 'returns no tasks for non-instrumented Gradle work' {
        @(Get-AndroidInstrumentationTasks -CommandArguments @(':app:assembleDebug')).Count | Should Be 0
    }

    It 'rejects aggregate and non-debug connected instrumentation tasks' {
        $message = $null
        try {
            Get-AndroidInstrumentationTasks -CommandArguments @('connectedCheck')
        } catch {
            $message = $_.Exception.Message
        }
        $message -like '*Unsupported connected instrumentation task(s)*' | Should Be $true

        $message = $null
        try {
            Get-AndroidInstrumentationTasks -CommandArguments @(':feature:home:connectedAndroidTest')
        } catch {
            $message = $_.Exception.Message
        }
        $message -like '*Unsupported connected instrumentation task(s)*' | Should Be $true
    }

    It 'rejects abbreviated instrumentation task names' {
        $message = $null
        try {
            Get-AndroidInstrumentationTasks -CommandArguments @(':app:cDAT')
        } catch {
            $message = $_.Exception.Message
        }
        $message -like '*Gradle task abbreviations are not allowed*' | Should Be $true

        $message = $null
        try {
            Get-AndroidInstrumentationTasks -CommandArguments @(':app:connectedD')
        } catch {
            $message = $_.Exception.Message
        }
        $message -like '*Unsupported connected instrumentation task(s)*' | Should Be $true
    }
}
