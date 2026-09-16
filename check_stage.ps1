$logFiles = Get-ChildItem 'C:\ProgramData\MulletaFlix\Server\log\log_*.log' | Sort-Object LastWriteTime -Descending
$latest = $logFiles[0].FullName
Write-Host "Log file: $latest"
Get-Content -Path $latest | Select-String "NEBULA-STAGE" -SimpleMatch | Select-Object -Last 10 | ForEach-Object { $_.Line }
Get-Content -Path $latest | Select-String "STRM" -SimpleMatch | Select-Object -Last 10 | ForEach-Object { $_.Line }
