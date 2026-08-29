@echo off
setlocal EnableDelayedExpansion
title Desinstalador do Driver Knup 360

:: Auto-elevacao para Administrador
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Solicitando privilegios de Administrador...
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

echo ============================================================
echo   DESINSTALADOR DO DRIVER KNUP 360
echo ============================================================
echo.

set "TARGET_DIR=C:\Program Files\KnupDriver360"

echo [1/3] Parando e removendo Servico do Windows...
sc stop KnupDriverService >nul 2>&1
timeout /t 1 /nobreak >nul
sc delete KnupDriverService >nul 2>&1

echo [2/3] Removendo arquivos do sistema...
if exist "%TARGET_DIR%" rmdir /S /Q "%TARGET_DIR%"

echo [3/3] Removendo atalhos...
del /F /Q "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Knup 360 Painel de Controle.lnk" >nul 2>&1
del /F /Q "%PUBLIC%\Desktop\Knup 360 Painel de Controle.lnk" >nul 2>&1

echo.
echo ============================================================
echo   DRIVER DESINSTALADO COM SUCESSO!
echo ============================================================
echo.
pause