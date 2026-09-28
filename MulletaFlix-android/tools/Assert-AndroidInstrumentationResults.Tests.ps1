. (Join-Path $PSScriptRoot 'Assert-AndroidInstrumentationResults.ps1')

Describe 'Assert-AndroidInstrumentationResults' {
    It 'rejects a missing report' {
        $thrown = $false
        try {
            Assert-AndroidInstrumentationResults -ReportFiles @() -StartedAt (Get-Date)
        } catch {
            $thrown = $true
        }

        $thrown | Should Be $true
    }

    It 'rejects an empty report with zero executed tests' {
        $reportPath = Join-Path $TestDrive 'empty.xml'
        Set-Content -LiteralPath $reportPath -Value '<testsuites tests="0" failures="0" errors="0" skipped="0" />'

        $thrown = $false
        try {
            Assert-AndroidInstrumentationResults -ReportFiles @(Get-Item $reportPath) -StartedAt (Get-Date).AddMinutes(-1)
        } catch {
            $thrown = $_.Exception.Message -like '*contains 0 tests*'
        }

        $thrown | Should Be $true
    }

    It 'rejects failures and errors even when tests ran' {
        $reportPath = Join-Path $TestDrive 'failed.xml'
        Set-Content -LiteralPath $reportPath -Value '<testsuites tests="2" failures="1" errors="1" skipped="0" />'

        $thrown = $false
        try {
            Assert-AndroidInstrumentationResults -ReportFiles @(Get-Item $reportPath) -StartedAt (Get-Date).AddMinutes(-1)
        } catch {
            $thrown = $_.Exception.Message -like '*has 1 failures and 1 errors*'
        }

        $thrown | Should Be $true
    }

    It 'rejects a report when every test was skipped for a mismatched profile' {
        $reportPath = Join-Path $TestDrive 'skipped.xml'
        Set-Content -LiteralPath $reportPath -Value '<testsuites tests="2" failures="0" errors="0" skipped="2" />'

        $message = $null
        try {
            Assert-AndroidInstrumentationResults -ReportFiles @(Get-Item $reportPath) -StartedAt (Get-Date).AddMinutes(-1)
        } catch {
            $message = $_.Exception.Message
        }

        $message -like '*contains no executed tests (2 skipped)*' | Should Be $true
    }

    It 'accepts fresh reports with executed tests and no failures' {
        $reportPath = Join-Path $TestDrive 'passed.xml'
        Set-Content -LiteralPath $reportPath -Value '<testsuites tests="3" failures="0" errors="0" skipped="0" />'

        { Assert-AndroidInstrumentationResults -ReportFiles @(Get-Item $reportPath) -StartedAt (Get-Date).AddMinutes(-1) } |
            Should Not Throw
    }
}
