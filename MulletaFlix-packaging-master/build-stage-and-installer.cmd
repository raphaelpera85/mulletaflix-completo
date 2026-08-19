@echo off
setlocal
title MulletaFlix - Build Stage e Instalador

cd /d "%~dp0.."

echo ==================================================
echo    MulletaFlix - Build Stage e Instalador
echo ==================================================
echo.
echo Esta janela ficara aberta ao final.
echo Se quiser fechar, digite exit depois que terminar.
echo.

cmd /k powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%CD%\build-stage-and-installer.ps1" -NoPause %*
