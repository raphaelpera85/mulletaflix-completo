. (Join-Path $PSScriptRoot 'EmulatorProcessTree.ps1')

Describe 'Emulator process tree lifecycle helpers' {
    It 'captures descendants before shutdown and excludes unrelated AVD processes' {
        $snapshot = @(
            [pscustomobject]@{ ProcessId = 100; ParentProcessId = 1 }
            [pscustomobject]@{ ProcessId = 101; ParentProcessId = 100 }
            [pscustomobject]@{ ProcessId = 102; ParentProcessId = 101 }
            [pscustomobject]@{ ProcessId = 201; ParentProcessId = 2 }
        )

        $ids = Get-EmulatorProcessTreeIds -RootProcessId 100 -ProcessSnapshot $snapshot

        ($ids -contains 100) | Should Be $true
        ($ids -contains 101) | Should Be $true
        ($ids -contains 102) | Should Be $true
        ($ids -contains 201) | Should Be $false
    }

    It 'retains the root when no child process remains at snapshot time' {
        $ids = Get-EmulatorProcessTreeIds -RootProcessId 300 -ProcessSnapshot @()

        $ids.Count | Should Be 1
        $ids[0] | Should Be 300
    }

    It 'stops only the captured process handle and leaves another process running' {
        $ownedProcess = Start-Process -FilePath 'powershell.exe' `
            -ArgumentList @('-NoProfile', '-Command', 'Start-Sleep -Seconds 60') `
            -WindowStyle Hidden -PassThru
        $otherProcess = Start-Process -FilePath 'powershell.exe' `
            -ArgumentList @('-NoProfile', '-Command', 'Start-Sleep -Seconds 60') `
            -WindowStyle Hidden -PassThru

        try {
            $handles = @(Get-EmulatorProcessHandles -ProcessIds @($ownedProcess.Id))
            $handles.Count | Should Be 1

            Stop-EmulatorProcessTree -ProcessHandles $handles

            (Get-Process -Id $ownedProcess.Id -ErrorAction SilentlyContinue) | Should Be $null
            (Get-Process -Id $otherProcess.Id -ErrorAction SilentlyContinue) | Should Not Be $null
        } finally {
            Stop-Process -Id $ownedProcess.Id,$otherProcess.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
