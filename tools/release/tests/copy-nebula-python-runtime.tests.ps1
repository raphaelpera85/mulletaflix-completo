Describe 'copy-nebula-python-runtime.ps1' {
    BeforeAll {
        $repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
        $copyScript = Join-Path $repoRoot 'tools/release/copy-nebula-python-runtime.ps1'
        $sourceDirectory = Join-Path $repoRoot 'MulletaFlix-master/Tools/NebulaPython'
        $testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('mulletaflix-nebula-copy-test-' + [guid]::NewGuid().ToString('N'))
        $destinationDirectory = Join-Path $testRoot 'Tools/NebulaPython'
        New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
        & $copyScript -SourceDirectory $sourceDirectory -DestinationDirectory $destinationDirectory
    }

    It 'copies required download worker files without tests and caches' {
        foreach ($relativePath in @(
            'main.py', 'stream_service.py', 'control_plane.py', 'generate_strm.py', 'requirements.txt', 'requirements.lock',
            'ftp/__init__.py', 'tools/__init__.py', 'tools/strm_downloader.py', 'tools/mongo_admission.py'
        )) {
            Test-Path -LiteralPath (Join-Path $destinationDirectory $relativePath) -PathType Leaf | Should Be $true
        }
        $sourceHash = (Get-FileHash -LiteralPath (Join-Path $sourceDirectory 'tools/strm_downloader.py') -Algorithm SHA256).Hash
        $copiedHash = (Get-FileHash -LiteralPath (Join-Path $destinationDirectory 'tools/strm_downloader.py') -Algorithm SHA256).Hash
        $copiedHash | Should Be $sourceHash
        Get-ChildItem -LiteralPath $destinationDirectory -Directory -Recurse | Where-Object Name -In @('tests', '__pycache__', '.pytest_cache') | Should BeNullOrEmpty
    }

    AfterAll {
        $resolvedRoot = [System.IO.Path]::GetFullPath($testRoot)
        $expectedParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\')
        if ([System.IO.Path]::GetDirectoryName($resolvedRoot) -ne $expectedParent -or [System.IO.Path]::GetFileName($resolvedRoot) -notlike 'mulletaflix-nebula-copy-test-*') {
            throw 'Refusing unexpected test cleanup path.'
        }
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    }
}
