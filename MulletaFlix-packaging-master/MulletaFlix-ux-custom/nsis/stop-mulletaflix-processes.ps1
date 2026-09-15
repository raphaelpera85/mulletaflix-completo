param(
    [Parameter(Mandatory = $true)]
    [string] $InstallDirectory,

    [Parameter(Mandatory = $true)]
    [string] $DataDirectory
)

$ErrorActionPreference = 'Stop'
$installRoot = [IO.Path]::GetFullPath($InstallDirectory).TrimEnd('\') + '\'
$dataRoot = [IO.Path]::GetFullPath($DataDirectory).TrimEnd('\') + '\'
$processNames = @('MulletaFlix.exe', 'MulletaFlix.Windows.Tray.exe', 'mysqld.exe', 'mariadbd.exe', 'rclone.exe', 'ffmpeg.exe')

function Test-UnderRoot {
    param([string] $Path, [string] $Root)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    try {
        $fullPath = [IO.Path]::GetFullPath($Path)
        return $fullPath.StartsWith($Root, [StringComparison]::OrdinalIgnoreCase)
    } catch {
        return $false
    }
}

$targets = Get-CimInstance Win32_Process |
    Where-Object { $processNames -contains $_.Name } |
    Where-Object {
        (Test-UnderRoot $_.ExecutablePath $installRoot) -or
        (Test-UnderRoot $_.ExecutablePath $dataRoot) -or
        ($_.CommandLine -and $_.CommandLine.IndexOf($dataRoot, [StringComparison]::OrdinalIgnoreCase) -ge 0)
    }

foreach ($target in $targets) {
    try {
        Stop-Process -Id ([int]$target.ProcessId) -Force -ErrorAction Stop
        Write-Host "Stopped MulletaFlix-owned process $($target.ProcessId) ($($target.Name))."
    } catch [System.Management.Automation.ItemNotFoundException] {
        # The process exited between the CIM query and Stop-Process.
    }
}

if ($targets) {
    Wait-Process -Id @($targets.ProcessId) -Timeout 15 -ErrorAction SilentlyContinue
}
