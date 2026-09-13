param(
    [int] $TimeoutSeconds = 30
)

$ErrorActionPreference = 'Stop'
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)

while ([DateTime]::UtcNow -lt $deadline) {
    $service = Get-Service -Name 'MulletaFlixServer' -ErrorAction SilentlyContinue
    if ($null -eq $service -or $service.Status -eq [System.ServiceProcess.ServiceControllerStatus]::Stopped) {
        exit 0
    }

    Start-Sleep -Milliseconds 250
}

$service = Get-Service -Name 'MulletaFlixServer' -ErrorAction SilentlyContinue
if ($service) {
    Write-Error "MulletaFlixServer did not stop within $TimeoutSeconds seconds (state: $($service.Status))."
} else {
    Write-Error 'MulletaFlixServer service state could not be confirmed.'
}
exit 1
