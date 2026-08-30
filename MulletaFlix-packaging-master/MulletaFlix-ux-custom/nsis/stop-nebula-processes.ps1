$nebulaRoot = Join-Path $env:ProgramFiles 'MulletaFlix\Server\nebula'

Get-CimInstance Win32_Process |
    Where-Object {
        $_.ExecutablePath -and
        $_.ExecutablePath.StartsWith($nebulaRoot, [System.StringComparison]::OrdinalIgnoreCase)
    } |
    ForEach-Object {
        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    }
