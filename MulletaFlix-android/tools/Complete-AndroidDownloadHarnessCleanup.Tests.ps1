. (Join-Path $PSScriptRoot 'Complete-AndroidDownloadHarnessCleanup.ps1')

Describe 'Complete-AndroidDownloadHarnessCleanup' {
    It 'does not fail when no cleanup errors occurred' {
        { Complete-AndroidDownloadHarnessCleanup -PrimaryFailureOccurred $false -CleanupFailures @() } | Should Not Throw
    }

    It 'fails a successful test run when cleanup failed' {
        { Complete-AndroidDownloadHarnessCleanup -PrimaryFailureOccurred $false -CleanupFailures @('fixture cleanup did not pass') } |
            Should Throw 'Android download harness cleanup failed'
    }

    It 'preserves the primary failure and reports secondary cleanup failures as warnings' {
        { Complete-AndroidDownloadHarnessCleanup -PrimaryFailureOccurred $true -CleanupFailures @('temporary evidence directory could not be removed') } |
            Should Not Throw
    }
}
