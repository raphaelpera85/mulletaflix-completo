. (Join-Path $PSScriptRoot 'Save-AndroidEmulatorBootEvidence.ps1')

Describe 'Save-AndroidEmulatorBootEvidence' {
    BeforeEach {
        $evidenceDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "mulletaflix-emulator-boot-$([guid]::NewGuid().ToString('N'))"
    }

    AfterEach {
        if (Test-Path -LiteralPath $evidenceDirectory) {
            Remove-Item -LiteralPath $evidenceDirectory -Recurse -Force
        }
    }

    It 'stores scoped boot state, matching services and filtered logs for an incomplete boot' {
        $result = Save-AndroidEmulatorBootEvidence `
            -Directory $evidenceDirectory `
            -AvdName 'MulletaFlixAndroid17Api37_20261016' `
            -Serial 'emulator-5560' `
            -BootCompleted $false `
            -PackageManagerReady $true `
            -CaptureStatus 'getprop=0; services=0; logcat=0' `
            -PropertyLines @(
                '[sys.boot_completed]: []'
                '[sys.system_server.start_count]: [3]'
                '[sys.bootstat.first_boot_completed]: [0]'
            ) `
            -ServiceLines @(
                '12 package: [android.content.pm.IPackageManager]'
                '15 SurfaceFlinger: [android.ui.ISurfaceComposer]'
                '99 unrelated: [example]'
            ) `
            -LogcatLines @(
                'F DEBUG: crash context before signal'
                'F libc: Fatal signal 6 (SIGABRT) in surfaceflinger'
                'F DEBUG: #00 native frame before abort cause'
                'F DEBUG: #01 native frame in RegionSamplingThread'
                'I/NoiseTag: filler 01'
                'I/NoiseTag: filler 02'
                'I/NoiseTag: filler 03'
                'I/NoiseTag: filler 04'
                'I/NoiseTag: filler 05'
                'I/NoiseTag: filler 06'
                'I/NoiseTag: filler 07'
                'I/NoiseTag: filler 08'
                'I/NoiseTag: filler 09'
                'I/NoiseTag: filler 10'
                'I/NoiseTag: filler 11'
                'I/NoiseTag: filler 12'
                'I/NoiseTag: filler 13'
                'I/UnrelatedTag: omit this line'
            )

        $result.statusPath | Should Exist
        $result.propertiesPath | Should Exist
        $result.servicesPath | Should Exist
        $result.logcatPath | Should Exist
        (Get-Content -LiteralPath $result.statusPath -Raw) | Should Match 'bootCompleted=False'
        (Get-Content -LiteralPath $result.statusPath -Raw) | Should Match 'packageManagerReady=True'
        (Get-Content -LiteralPath $result.propertiesPath -Raw) | Should Match 'sys\.system_server\.start_count'
        (Get-Content -LiteralPath $result.servicesPath -Raw) | Should Match 'IPackageManager'
        (Get-Content -LiteralPath $result.servicesPath -Raw) | Should Not Match 'unrelated'
        (Get-Content -LiteralPath $result.logcatPath -Raw) | Should Match 'Fatal signal'
        (Get-Content -LiteralPath $result.logcatPath -Raw) | Should Match '#00 native frame'
        (Get-Content -LiteralPath $result.logcatPath -Raw) | Should Match '#01 native frame'
        (Get-Content -LiteralPath $result.logcatPath -Raw) | Should Not Match 'UnrelatedTag'
    }

    It 'creates a truthful record when ADB returns no boot data' {
        $result = Save-AndroidEmulatorBootEvidence `
            -Directory $evidenceDirectory `
            -AvdName 'MulletaflixLanPermApi37_20261013' `
            -Serial 'emulator-5562' `
            -BootCompleted $false `
            -PackageManagerReady $false `
            -CaptureStatus 'getprop=1; services=1; logcat=1' `
            -PropertyLines @() `
            -ServiceLines @() `
            -LogcatLines @()

        (Get-Content -LiteralPath $result.statusPath -Raw) | Should Match 'bootCompleted=False'
        (Get-Content -LiteralPath $result.servicesPath -Raw) | Should Match 'No matching Android services'
        (Get-Content -LiteralPath $result.logcatPath -Raw) | Should Match 'No matching Android boot diagnostics'
    }

    It 'redacts secrets from diagnostic lines before persisting them' {
        $result = Save-AndroidEmulatorBootEvidence `
            -Directory $evidenceDirectory `
            -AvdName 'MulletaflixLanPermApi37_20261013' `
            -Serial 'emulator-5562' `
            -BootCompleted $false `
            -PackageManagerReady $false `
            -CaptureStatus 'getprop=0; services=0; logcat=0' `
            -PropertyLines @() `
            -ServiceLines @() `
            -LogcatLines @('E/PackageManager: token=secret-token Authorization: Bearer private-key')

        $log = Get-Content -LiteralPath $result.logcatPath -Raw
        $log | Should Match 'token=\[REDACTED\]'
        $log | Should Not Match 'secret-token|private-key'
    }
}
