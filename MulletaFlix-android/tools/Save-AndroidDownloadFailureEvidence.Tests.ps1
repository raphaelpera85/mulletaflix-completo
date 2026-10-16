. (Join-Path $PSScriptRoot 'Save-AndroidDownloadFailureEvidence.ps1')

Describe 'Save-AndroidDownloadFailureEvidence' {
    BeforeEach {
        $evidenceDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "mulletaflix-download-failure-$([guid]::NewGuid().ToString('N'))"
    }

    AfterEach {
        if (Test-Path -LiteralPath $evidenceDirectory) {
            Remove-Item -LiteralPath $evidenceDirectory -Recurse -Force
        }
    }

    It 'preserves the runner result, fixture snapshot, and filtered crash diagnostics' {
        $resultPath = Save-AndroidDownloadFailureEvidence `
            -Directory $evidenceDirectory `
            -Phase 'preparePartialDownloadForHostProcessRestart' `
            -RunnerExitCode 0 `
            -RunnerOutput @('INSTRUMENTATION_RESULT: shortMsg=Process crashed', 'INSTRUMENTATION_CODE: 0') `
            -FixtureStatusJson '{"requests":[],"completedRequests":[],"rangeStarts":[]}' `
            -CaptureStatus 'mainLogcatExitCode=0; crashBufferExitCode=0' `
            -DiagnosticLines @('E/AndroidRuntime: FATAL EXCEPTION: main', 'E/AndroidRuntime: Caused by: java.lang.IllegalStateException')

        $resultPath | Should Exist
        $result = Get-Content -LiteralPath $resultPath -Raw
        $result | Should Match 'exitCode=0'
        $result | Should Match 'Process crashed'

        $fixturePath = Join-Path $evidenceDirectory 'preparePartialDownloadForHostProcessRestart-fixture.json'
        $fixturePath | Should Exist
        (Get-Content -LiteralPath $fixturePath -Raw | ConvertFrom-Json).requests.Count | Should Be 0

        $logcatPath = Join-Path $evidenceDirectory 'preparePartialDownloadForHostProcessRestart-logcat.txt'
        $logcatPath | Should Exist
        $logcat = Get-Content -LiteralPath $logcatPath -Raw
        $logcat | Should Match 'captureStatus=mainLogcatExitCode=0; crashBufferExitCode=0'
        $logcat | Should Match 'IllegalStateException'
    }

    It 'writes evidence when the failed runner produces no text and log capture is unavailable' {
        $resultPath = Save-AndroidDownloadFailureEvidence `
            -Directory $evidenceDirectory `
            -Phase 'preparePartialDownloadForHostProcessRestart' `
            -RunnerExitCode 0 `
            -RunnerOutput '' `
            -FixtureStatusJson '' `
            -CaptureStatus 'mainLogcatExitCode=1; crashBufferExitCode=1' `
            -DiagnosticLines @()

        $resultPath | Should Exist
        (Get-Content -LiteralPath $resultPath -Raw) | Should Match 'exitCode=0'
        $fixturePath = Join-Path $evidenceDirectory 'preparePartialDownloadForHostProcessRestart-fixture.json'
        (Get-Content -LiteralPath $fixturePath -Raw | ConvertFrom-Json).available | Should Be $false
        $logcatPath = Join-Path $evidenceDirectory 'preparePartialDownloadForHostProcessRestart-logcat.txt'
        $logcat = Get-Content -LiteralPath $logcatPath -Raw
        $logcat | Should Match 'mainLogcatExitCode=1'
        $logcat | Should Match 'No matching Android download/player diagnostics'
    }
}
