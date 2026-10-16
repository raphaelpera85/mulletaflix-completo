. (Join-Path $PSScriptRoot 'Save-AndroidDownloadFailureEvidence.ps1')
. (Join-Path $PSScriptRoot 'Save-AndroidDownloadPhaseEvidence.ps1')

Describe 'Save-AndroidDownloadPhaseEvidence' {
    BeforeEach {
        $evidenceDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "mulletaflix-download-phase-$([guid]::NewGuid().ToString('N'))"
    }

    AfterEach {
        if (Test-Path -LiteralPath $evidenceDirectory) {
            Remove-Item -LiteralPath $evidenceDirectory -Recurse -Force
        }
    }

    It 'persists a passed runner result and its matching fixture snapshot as auditable evidence' {
        $result = Save-AndroidDownloadPhaseEvidence `
            -Directory $evidenceDirectory `
            -Phase 'verifyOfflinePlayback' `
            -Outcome 'passed' `
            -RunnerExitCode 0 `
            -RunnerOutput @('Tests run: 1, Failures: 0, Errors: 0, Skipped: 0', 'OK (1 test)') `
            -FixtureStatusJson '{"rangeStarts":[1034976],"completedRequests":[{"bytes":3852679}],"requests":[]}' `
            -CaptureStatus 'mainLogcatExitCode=0; crashBufferExitCode=0' `
            -DiagnosticLines @('I/DownloadService: completed')

        $result.outcome | Should Be 'passed'
        $result.runnerPath | Should Exist
        (Get-Content -LiteralPath $result.runnerPath -Raw) | Should Match 'OK \(1 test\)'
        (Get-Content -LiteralPath $result.statusPath -Raw) | Should Match 'outcome=passed'
        $fixture = Get-Content -LiteralPath $result.fixturePath -Raw | ConvertFrom-Json
        $fixture.rangeStarts[0] | Should Be 1034976
        $fixture.completedRequests[0].bytes | Should Be 3852679
        $fixture.requests.Count | Should Be 0
        (Get-Content -LiteralPath $result.logcatPath -Raw) | Should Match 'DownloadService: completed'
    }

    It 'refuses to label a failed AndroidJUnit result as passed' {
        $thrown = $false
        try {
            Save-AndroidDownloadPhaseEvidence `
                -Directory $evidenceDirectory `
                -Phase 'verifyOfflinePlayback' `
                -Outcome 'passed' `
                -RunnerExitCode 0 `
                -RunnerOutput @('FAILURES!!!', 'Tests run: 1, Failures: 1, Errors: 0, Skipped: 0') `
                -FixtureStatusJson '{"requests":[]}' `
                -CaptureStatus 'mainLogcatExitCode=0; crashBufferExitCode=0' `
                -DiagnosticLines @()
        } catch {
            $thrown = $_.Exception.Message -match "Evidence outcome 'passed' conflicts with the AndroidJUnit runner result"
        }
        $thrown | Should Be $true
    }
}
