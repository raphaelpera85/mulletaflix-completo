@echo off
setlocal
title MulletaFlix - Build Completo

cd /d "%~dp0.."

echo ==================================================
echo    MulletaFlix - Build Completo
echo ==================================================
echo.
echo Este arquivo NAO fecha sozinho.
echo.
echo Etapas:
echo  1. Build do servidor
echo  2. Build do web
echo  3. Copia para stage
echo  4. Build do tray
echo  5. Criacao do instalador
echo.
echo Log:
echo  %CD%\logs\build-stage-and-installer.log
echo.
echo Iniciando...
echo.

cmd /k powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%CD%\build-stage-and-installer.ps1" -NoPause
