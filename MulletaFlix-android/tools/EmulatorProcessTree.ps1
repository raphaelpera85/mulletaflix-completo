function Get-EmulatorProcessTreeIds {
    param(
        [Parameter(Mandatory = $true)]
        [int] $RootProcessId,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]] $ProcessSnapshot
    )

    $processIds = [System.Collections.Generic.HashSet[int]]::new()
    [void] $processIds.Add($RootProcessId)
    do {
        $before = $processIds.Count
        foreach ($process in $ProcessSnapshot) {
            if ($processIds.Contains([int] $process.ParentProcessId)) {
                [void] $processIds.Add([int] $process.ProcessId)
            }
        }
    } while ($processIds.Count -gt $before)

    return [int[]] @($processIds)
}

function Stop-EmulatorProcessTree {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]] $ProcessHandles
    )

    foreach ($entry in $ProcessHandles) {
        $process = $entry.Process
        try {
            $process.Refresh()
            if ($process.HasExited) { continue }
            if ($process.StartTime.ToUniversalTime().Ticks -ne $entry.StartTimeUtcTicks) { continue }
            if ($process.Path -ne $entry.Path) { continue }

            Stop-Process -InputObject $process -Force -ErrorAction SilentlyContinue
        } catch {
            # The process may have exited gracefully between checks.
        }
    }
}

function Get-EmulatorProcessHandles {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [int[]] $ProcessIds
    )

    if ($ProcessIds.Count -eq 0) { return }

    foreach ($process in @(Get-Process -Id $ProcessIds -ErrorAction SilentlyContinue)) {
        try {
            $startTimeUtcTicks = $process.StartTime.ToUniversalTime().Ticks
            $path = $process.Path
            [pscustomobject]@{
                Process = $process
                StartTimeUtcTicks = $startTimeUtcTicks
                Path = $path
            }
        } catch {
            # A process that exits before identity capture is already stopped.
        }
    }
}
