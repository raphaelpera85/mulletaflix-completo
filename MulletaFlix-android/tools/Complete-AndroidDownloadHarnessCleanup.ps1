function Complete-AndroidDownloadHarnessCleanup {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [bool] $PrimaryFailureOccurred,

        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)]
        [string[]] $CleanupFailures
    )

    if ($CleanupFailures.Count -eq 0) { return }

    $message = 'Android download harness cleanup failed: ' + ($CleanupFailures -join '; ')
    if ($PrimaryFailureOccurred) {
        Write-Warning "$message (preserving the original test failure)."
        return
    }

    throw $message
}
