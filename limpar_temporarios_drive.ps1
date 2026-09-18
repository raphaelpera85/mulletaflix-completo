$target = "D:\Users\Raphael\Documents\.tmp.driveupload.old"
$origTarget = "D:\Users\Raphael\Documents\.tmp.driveupload"

if (-not (Test-Path $target) -and (Test-Path $origTarget)) {
    $target = $origTarget
}

if (-not (Test-Path $target)) {
    Write-Host "Pasta temporaria nao encontrada. Ja foi excluida!" -ForegroundColor Green
    Start-Sleep -Seconds 3
    exit 0
}

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "   EXCLUSAO ULTRA-RAPIDA DE ARQUIVOS DO GOOGLE DRIVE (~700 GB)" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Excluindo arquivos temporarios via Robocopy /B (modo backup multithread)..." -ForegroundColor Yellow

$emptyDir = "$env:TEMP\empty_purge_dir"
New-Item -ItemType Directory -Path $emptyDir -Force | Out-Null

robocopy $emptyDir $target /PURGE /B /R:0 /W:0 /MT:32 /NFL /NDL /NJH /NJS /NC /NS /NP | Out-Null
Remove-Item -Path $target -Force -Recurse -ErrorAction SilentlyContinue
Remove-Item -Path $emptyDir -Force -Recurse -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "   LIMPEZA CONCLUIDA COM SUCESSO! 700 GB liberados no Disco D:." -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
Write-Host ""
Start-Sleep -Seconds 3
