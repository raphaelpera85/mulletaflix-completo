. (Join-Path $PSScriptRoot 'Assert-AndroidApkMetadata.ps1')

Describe 'Assert-AndroidApkMetadata' {
    It 'accepts metadata matching package, version name, and version code' {
        $metadata = [pscustomobject]@{
            PackageName = 'org.mulletaflix.android'
            VersionName = '1.3.82'
            VersionCode = 382
        }

        { Assert-AndroidApkMetadata -Metadata $metadata -ExpectedPackageName 'org.mulletaflix.android' -ExpectedVersionName '1.3.82' -ExpectedVersionCode 382 } | Should Not Throw
    }

    It 'rejects an APK whose embedded version name is stale' {
        $metadata = [pscustomobject]@{
            PackageName = 'org.mulletaflix.android'
            VersionName = '1.3.81'
            VersionCode = 381
        }

        $thrown = $false
        try {
            Assert-AndroidApkMetadata -Metadata $metadata -ExpectedPackageName 'org.mulletaflix.android' -ExpectedVersionName '1.3.82' -ExpectedVersionCode 382
        } catch {
            $thrown = $true
        }
        $thrown | Should Be $true
    }

    It 'rejects an APK with a different application id' {
        $metadata = [pscustomobject]@{
            PackageName = 'org.example.other'
            VersionName = '1.3.82'
            VersionCode = 382
        }

        $thrown = $false
        try {
            Assert-AndroidApkMetadata -Metadata $metadata -ExpectedPackageName 'org.mulletaflix.android' -ExpectedVersionName '1.3.82' -ExpectedVersionCode 382
        } catch {
            $thrown = $true
        }
        $thrown | Should Be $true
    }

    It 'rejects an APK with a different version code' {
        $metadata = [pscustomobject]@{
            PackageName = 'org.mulletaflix.android'
            VersionName = '1.3.82'
            VersionCode = 381
        }

        $thrown = $false
        try {
            Assert-AndroidApkMetadata -Metadata $metadata -ExpectedPackageName 'org.mulletaflix.android' -ExpectedVersionName '1.3.82' -ExpectedVersionCode 382
        } catch {
            $thrown = $true
        }
        $thrown | Should Be $true
    }
}
