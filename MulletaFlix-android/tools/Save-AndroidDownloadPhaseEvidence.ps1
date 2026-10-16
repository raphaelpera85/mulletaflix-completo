function Save-AndroidDownloadPhaseEvidence {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Directory,

        [Parameter(Mandatory = $true)]
        [ValidatePattern('^[A-Za-z0-9-]+$')]
        [string] $Phase,

        [Parameter(Mandatory = $true)]
        [ValidateSet('passed', 'failed')]
        [string] $Outcome,

        [Parameter(Mandatory = $true)]
        [int] $RunnerExitCode,

        [AllowEmptyCollection()]
        [AllowEmptyString()]
        [Parameter(Mandatory = $true)]
        [string[]] $RunnerOutput,

        [AllowEmptyString()]
        [Parameter(Mandatory = $true)]
        [string] $FixtureStatusJson,

        [Parameter(Mandatory = $true)]
        [string] $CaptureStatus,

        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)]
        [string[]] $DiagnosticLines
    )

    $runnerText = $RunnerOutput -join [Environment]::NewLine
    $runnerPassed = $RunnerExitCode -eq 0 -and
        $runnerText -match 'OK \(1 test\)' -and
        $runnerText -notmatch 'FAILURES!!!|INSTRUMENTATION_FAILED'
    if (($Outcome -eq 'passed') -ne $runnerPassed) {
        throw "Evidence outcome '$Outcome' conflicts with the AndroidJUnit runner result for phase '$Phase'."
    }

    $runnerPath = Save-AndroidDownloadFailureEvidence `
        -Directory $Directory `
        -Phase $Phase `
        -RunnerExitCode $RunnerExitCode `
        -RunnerOutput $RunnerOutput `
        -FixtureStatusJson $FixtureStatusJson `
        -CaptureStatus $CaptureStatus `
        -DiagnosticLines $DiagnosticLines
    $statusPath = Join-Path $Directory "$Phase-status.txt"
    @("phase=$Phase", "outcome=$Outcome", "runnerExitCode=$RunnerExitCode") |
        Set-Content -LiteralPath $statusPath -Encoding UTF8

    return [pscustomobject]@{
        phase = $Phase
        outcome = $Outcome
        runnerPath = $runnerPath
        fixturePath = Join-Path $Directory "$Phase-fixture.json"
        logcatPath = Join-Path $Directory "$Phase-logcat.txt"
        statusPath = $statusPath
    }
}
