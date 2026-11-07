. (Join-Path $PSScriptRoot 'Prepare-AndroidDeveloperVerification.ps1') -RegistrationSnippet 'test'

Describe 'Prepare-AndroidDeveloperVerification' {
    It 'rejects an empty registration snippet' {
        $exception = $null
        try {
            Assert-AndroidDeveloperVerificationSnippet -Value '   '
        } catch {
            $exception = $_.Exception
        }

        $exception.Message | Should Be 'Registration snippet cannot be empty.'
    }

    It 'accepts the MulletaFlix package name' {
        { Assert-AndroidDeveloperVerificationPackageName -PackageName 'org.mulletaflix.android' } | Should Not Throw
    }

    It 'rejects a different package name' {
        $exception = $null
        try {
            Assert-AndroidDeveloperVerificationPackageName -PackageName 'org.example.other'
        } catch {
            $exception = $_.Exception
        }

        $exception.Message | Should Be "Unexpected Android package 'org.example.other'. Expected 'org.mulletaflix.android'."
    }

    It 'writes the registration snippet exactly as UTF-8 text' {
        $path = Join-Path $TestDrive 'assets\adi-registration.properties'
        $snippet = 'registration-token=abc123'

        Write-AdiRegistrationAsset -Path $path -Content $snippet

        [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8) | Should Be $snippet
    }
}
