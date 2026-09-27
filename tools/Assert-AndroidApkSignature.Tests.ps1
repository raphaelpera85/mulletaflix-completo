. (Join-Path $PSScriptRoot 'Assert-AndroidApkSignature.ps1')

Describe 'Assert-AndroidApkSignature' {
    It 'accepts the official production certificate' {
        { Assert-AndroidApkCertificateFingerprint -Fingerprint '22:4f:9a:6b:d1:26:90:e1:11:4a:ce:64:9b:bf:a7:78:d3:e7:e9:9d:ae:60:8f:f7:11:dd:f9:13:1e:03:62:73' } | Should Not Throw
    }

    It 'rejects a different signing certificate' {
        $thrown = $false
        try {
            Assert-AndroidApkCertificateFingerprint -Fingerprint '4890D80B87FE27C804FF875B549ACD57B946FE6F9BBA0B9D7718989EC5A0A24C'
        } catch {
            $thrown = $true
        }

        $thrown | Should Be $true
    }
}
