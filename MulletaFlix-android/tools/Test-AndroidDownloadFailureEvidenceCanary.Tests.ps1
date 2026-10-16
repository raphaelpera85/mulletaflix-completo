Describe 'Android download failure-evidence canary wiring' {
    BeforeAll {
        $harnessPath = Join-Path $PSScriptRoot 'Test-AndroidDownloadProcessRestart.ps1'
        $testPath = Join-Path $PSScriptRoot '..\app\src\androidTest\java\org\mulletaflix\android\service\DownloadProcessRestartIntegrationTest.kt'
        $harness = Get-Content -LiteralPath $harnessPath -Raw
        $androidTest = Get-Content -LiteralPath $testPath -Raw
    }

    It 'targets the exact opt-in AndroidJUnit method from the host harness' {
        $method = [regex]::Match($harness, 'Invoke-GradlePhase -Method ''([^'']+)'' -FixtureBytes \$script:fixtureBytes\s+throw ''Failure-evidence canary').Groups[1].Value
        $method | Should Be 'failureEvidenceCanaryFailsOnlyWhenExplicitlyRequested'
        $androidTest | Should Match "fun $method\(\)"
    }

    It 'keeps the intentional assertion gated by the explicit instrumentation argument' {
        $androidTest | Should Match 'getString\("downloadRestartFailureEvidenceCanary"\)'
        $androidTest | Should Match 'if \(requested\)\s*\{\s*throw AssertionError\("Intentional failure-evidence canary'
        $harness | Should Match 'if \(\$Method -eq ''failureEvidenceCanaryFailsOnlyWhenExplicitlyRequested''\) \{\s*\$arguments \+= @\(''-e'', ''downloadRestartFailureEvidenceCanary'', ''true''\)'
        $harness | Should Match "-e', 'downloadRestartFailureEvidenceCanary', 'true'"
    }

    It 'requires exactly one intentional runner failure before accepting evidence' {
        $harness | Should Match ([regex]::Escape('Tests run:\s*1,\s*Failures:\s*1'))
        $harness | Should Match 'must contain exactly one executed test and one intentional failure'
    }

    It 'rejects any request recorded by the media fixture during the canary' {
        $harness | Should Match '@\(\$fixtureSnapshot\.requests\)\.Count -ne 0'
        $harness | Should Match 'unexpectedly issued media fixture requests'
    }
}
