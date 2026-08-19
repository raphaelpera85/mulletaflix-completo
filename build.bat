@echo off
REM ================================================================
REM MulletaFlix - Build Completo (Windows Batch Wrapper)
REM ================================================================
REM Executa build-stage-and-installer.ps1 com logs e tratamento de erros
REM Uso: build.bat [--skip-web] [--skip-server] [--skip-tray] [--skip-installer] [--no-pause]
REM ================================================================

setlocal enabledelayedexpansion

REM Configuracao
set "PROJECT_ROOT=%~dp0"
set "PS_SCRIPT=%PROJECT_ROOT%MulletaFlix-packaging-master\build-stage-and-installer.ps1"
set "LOG_DIR=%PROJECT_ROOT%logs"
set "LOG_FILE=%LOG_DIR%\build-%DATE:/=-%_%TIME::=-%.log"
set "POWERSHELL_EXE=powershell.exe"

REM Criar pasta de logs
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

REM Argumentos para o script PowerShell
set "PS_ARGS="

:PARSE_ARGS
if "%~1"=="" goto :RUN_BUILD
if "%~1"=="--skip-web" set "PS_ARGS=!PS_ARGS! -SkipWebBuild"
if "%~1"=="--skip-server" set "PS_ARGS=!PS_ARGS! -SkipServerBuild"
if "%~1"=="--skip-tray" set "PS_ARGS=!PS_ARGS! -SkipTrayBuild"
if "%~1"=="--skip-installer" set "PS_ARGS=!PS_ARGS! -SkipInstaller"
if "%~1"=="--no-pause" set "PS_ARGS=!PS_ARGS! -NoPause"
shift /1
goto :PARSE_ARGS

:RUN_BUILD
echo ================================================================
echo   MulletaFlix - Build Completo
echo ================================================================
echo.
echo Projeto: %PROJECT_ROOT%
echo Script:  %PS_SCRIPT%
echo Log:     %LOG_FILE%
echo Args:    %PS_ARGS%
echo.
echo Iniciando build...
echo.

REM Verificar se PowerShell existe
where %POWERSHELL_EXE% >nul 2>&1
if errorlevel 1 (
    echo ERRO: PowerShell nao encontrado no PATH
    echo Instale o PowerShell 5.1+ ou PowerShell 7+
    goto :ERROR_EXIT
)

REM Verificar se script existe
if not exist "%PS_SCRIPT%" (
    echo ERRO: Script nao encontrado: %PS_SCRIPT%
    goto :ERROR_EXIT
)

REM Executar com log (arquivo + console via Tee-Object)
%POWERSHELL_EXE% -NoProfile -ExecutionPolicy Bypass -Command "& '%PS_SCRIPT%' %PS_ARGS% 2>&1 | Tee-Object -FilePath '%LOG_FILE%'"

set "EXIT_CODE=%ERRORLEVEL%"

echo.
echo ================================================================
if %EXIT_CODE% equ 0 (
    echo BUILD CONCLUIDO COM SUCESSO
    echo Log salvo em: %LOG_FILE%
) else (
    echo BUILD FALHOU (codigo: %EXIT_CODE%)
    echo Verifique o log: %LOG_FILE%
)
echo ================================================================

if "%PS_ARGS%" neq "" (
    echo.
    echo Argumentos usados: %PS_ARGS%
)

REM Pausar apenas se nao passou --no-pause
echo %PS_ARGS% | findstr /C:"--no-pause" >nul
if errorlevel 1 (
    echo.
    echo Pressione qualquer tecla para sair...
    pause >nul
)

exit /b %EXIT_CODE%

:ERROR_EXIT
echo.
echo Pressione qualquer tecla para sair...
pause >nul
exit /b 1