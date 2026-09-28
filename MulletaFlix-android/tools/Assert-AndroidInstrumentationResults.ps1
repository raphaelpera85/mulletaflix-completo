function Assert-AndroidInstrumentationResults {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo[]] $ReportFiles,

        [Parameter(Mandatory = $true)]
        [datetime] $StartedAt
    )

    $freshReports = @($ReportFiles | Where-Object { $_.LastWriteTime -ge $StartedAt })
    if ($freshReports.Count -eq 0) {
        throw 'No fresh instrumentation result was produced for the requested connected tests.'
    }

    foreach ($reportFile in $freshReports) {
        [xml] $report = Get-Content -LiteralPath $reportFile.FullName
        $summary = if ($report.testsuites) { $report.testsuites } else { $report.testsuite }
        $testCount = [int] $summary.tests
        $failureCount = [int] $summary.failures
        $errorCount = [int] $summary.errors
        if ($testCount -eq 0) {
            throw "Instrumentation report '$($reportFile.FullName)' contains 0 tests."
        }
        if ($failureCount -gt 0 -or $errorCount -gt 0) {
            throw "Instrumentation report '$($reportFile.FullName)' has $failureCount failures and $errorCount errors."
        }
    }
}
