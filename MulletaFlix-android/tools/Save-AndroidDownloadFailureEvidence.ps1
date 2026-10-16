function Save-AndroidDownloadFailureEvidence {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Directory,

        [Parameter(Mandatory = $true)]
        [ValidatePattern('^[A-Za-z0-9-]+$')]
        [string] $Phase,

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

    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    $runnerPath = Join-Path $Directory "$Phase-runner.txt"
    $fixturePath = Join-Path $Directory "$Phase-fixture.json"
    $logcatPath = Join-Path $Directory "$Phase-logcat.txt"

    @("exitCode=$RunnerExitCode") + $RunnerOutput | Set-Content -LiteralPath $runnerPath -Encoding UTF8
    if ([string]::IsNullOrWhiteSpace($FixtureStatusJson)) {
        '{"available":false}' | Set-Content -LiteralPath $fixturePath -Encoding UTF8
    } else {
        $FixtureStatusJson | Set-Content -LiteralPath $fixturePath -Encoding UTF8
    }
    if ($DiagnosticLines.Count -eq 0) {
        @("captureStatus=$CaptureStatus", 'No matching Android download/player diagnostics were present in the captured log buffers.') |
            Set-Content -LiteralPath $logcatPath -Encoding UTF8
    } else {
        @("captureStatus=$CaptureStatus") + $DiagnosticLines | Set-Content -LiteralPath $logcatPath -Encoding UTF8
    }

    return $runnerPath
}
