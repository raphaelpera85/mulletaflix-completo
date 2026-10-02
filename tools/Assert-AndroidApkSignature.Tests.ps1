. (Join-Path $PSScriptRoot 'Assert-AndroidApkSignature.ps1')

Describe 'Assert-AndroidApkSignature' {
    It 'accepts the official production certificate' {
        { Assert-AndroidApkCertificateFingerprint -Fingerprint '48:90:d8:0b:87:fe:27:c8:04:ff:87:5b:54:9a:cd:57:b9:46:fe:6f:9b:ba:0b:9d:77:18:98:9e:c5:a0:a2:4c' } | Should Not Throw
    }

    It 'rejects a different signing certificate' {
        $thrown = $false
        try {
            Assert-AndroidApkCertificateFingerprint -Fingerprint '224F9A6BD12690E1114ACE649BBFA778D3E7E99DAE608FF711DDF9131E036273'
        } catch {
            $thrown = $true
        }

        $thrown | Should Be $true
    }
}
