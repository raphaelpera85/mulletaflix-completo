. (Join-Path $PSScriptRoot 'Get-AndroidDownloadDiagnosticLogLines.ps1')

Describe 'Get-AndroidDownloadDiagnosticLogLines' {
    It 'returns no diagnostics when logcat is empty' {
        $selected = @(Get-AndroidDownloadDiagnosticLogLines -LogLines @())

        $selected.Count | Should Be 0
    }

    It 'retains Media3, Android runtime, instrumentation and process crash evidence' {
        $inputLines = @(
            '10-16 15:08:09.019 E/AndroidRuntime: FATAL EXCEPTION: main'
            '10-16 15:08:09.020 E/AndroidRuntime: Process: org.mulletaflix.android.debug.test, PID: 3513'
            '10-16 15:08:09.021 I/ActivityManager: Process org.mulletaflix.android.debug.test (pid 3513) has died'
            '10-16 15:08:09.022 W/ActivityManager: Background start not allowed: DownloadService'
            '10-16 15:08:09.023 E/DEBUG: Fatal signal 11 (SIGSEGV) in tid 3513'
            '10-16 15:08:09.024 I/AndroidJUnitRunner: instrumentation process stopped'
            '10-16 15:08:09.025 I/DownloadManager: Download state changed'
            '10-16 15:08:09.026 I/ExoPlayerImplInternal: Playback state changed'
            '10-16 15:08:09.027 I/UnrelatedTag: harmless unrelated message'
        )

        $selected = @(Get-AndroidDownloadDiagnosticLogLines -LogLines $inputLines)

        ($selected -contains $inputLines[0]) | Should Be $true
        ($selected -contains $inputLines[1]) | Should Be $true
        ($selected -contains $inputLines[2]) | Should Be $true
        ($selected -contains $inputLines[3]) | Should Be $true
        ($selected -contains $inputLines[4]) | Should Be $true
        ($selected -contains $inputLines[5]) | Should Be $true
        ($selected -contains $inputLines[6]) | Should Be $true
        ($selected -contains $inputLines[7]) | Should Be $true
        ($selected -contains $inputLines[8]) | Should Be $false
    }

    It 'redacts credentials from selected diagnostic lines' {
        $selected = @(Get-AndroidDownloadDiagnosticLogLines -LogLines @(
            'I/DownloadService: token=token-secret Authorization: Bearer bearer-secret image?api_key=query-secret'
        ))

        $selected.Count | Should Be 1
        $selected[0] | Should Be 'I/DownloadService: token=[REDACTED] Authorization: [REDACTED] image?api_key=[REDACTED]'
    }
}
