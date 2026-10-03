. (Join-Path $PSScriptRoot 'Prepare-AndroidDeveloperVerification.ps1') -RegistrationSnippet 'test'

Describe 'Prepare-AndroidDeveloperVerification' {
    It 'rejects an empty registration snippet' {
        { Assert-AndroidDeveloperVerificationSnippet -Value '   ' } | Should Throw
    }

    It 'accepts the MulletaFlix package name' {
        { Assert-AndroidDeveloperVerificationPackageName -PackageName 'org.mulletaflix.android' } | Should Not Throw
    }

    It 'rejects a different package name' {
        { Assert-AndroidDeveloperVerificationPackageName -PackageName 'org.example.other' } | Should Throw
    }

    It 'writes the registration snippet exactly as UTF-8 text' {
        $path = Join-Path $TestDrive 'assets\adi-registration.properties'
        $snippet = 'registration-token=abc123'

        Write-AdiRegistrationAsset -Path $path -Content $snippet

        [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8) | Should Be $snippet
    }
}
