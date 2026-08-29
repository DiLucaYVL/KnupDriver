@echo off
setlocal EnableDelayedExpansion
title Instalador do Driver Knup 360

:: 1. Auto-elevacao para Administrador
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Solicitando privilegios de Administrador...
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

cd /d "%~dp0"
echo ============================================================
echo   INSTALADOR DO DRIVER KNUP 360 (XBOX 360 NATIVO)
echo ============================================================
echo.

set "TARGET_DIR=C:\Program Files\KnupDriver360"
set "CONFIG_DIR=C:\ProgramData\KnupXbox360"

echo [1/5] Criando pastas do sistema...
if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"
if not exist "%CONFIG_DIR%" mkdir "%CONFIG_DIR%"

echo [2/5] Copiando arquivos do driver e painel...
copy /Y "%~dp0Files\KnupDriverService.exe" "%TARGET_DIR%\KnupDriverService.exe" >nul
copy /Y "%~dp0Files\KnupControlPanel.exe" "%TARGET_DIR%\KnupControlPanel.exe" >nul

echo [3/5] Configurando Servico do Windows em Segundo Plano...
sc stop KnupDriverService >nul 2>&1
timeout /t 1 /nobreak >nul
sc delete KnupDriverService >nul 2>&1
timeout /t 1 /nobreak >nul

sc create KnupDriverService binPath= "\"%TARGET_DIR%\KnupDriverService.exe\"" start= auto DisplayName= "Knup Xbox 360 Controller Driver"
sc description KnupDriverService "Driver em segundo plano para conversao do controle Knup/Twin USB em Xbox 360 nativo com vibracao e HidHide."

echo [4/5] Iniciando o Driver Knup 360...
sc start KnupDriverService

echo [5/5] Criando atalhos do Painel de Controle...
powershell -Command "$s=(New-Object -COM WScript.Shell).CreateShortcut('C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Knup 360 Painel de Controle.lnk');$s.TargetPath='%TARGET_DIR%\KnupControlPanel.exe';$s.WorkingDirectory='%TARGET_DIR%';$s.Save()"
powershell -Command "$s=(New-Object -COM WScript.Shell).CreateShortcut([Environment]::GetFolderPath('CommonDesktopDirectory') + '\Knup 360 Painel de Controle.lnk');$s.TargetPath='%TARGET_DIR%\KnupControlPanel.exe';$s.WorkingDirectory='%TARGET_DIR%';$s.Save()"

echo.
echo ============================================================
echo   DRIVER INSTALADO E ATIVO COM SUCESSO!
echo ============================================================
echo   - O controle agora funciona automaticamente como Xbox 360.
echo   - O servico inicia sozinho com o Windows em segundo plano.
echo   - Nao e necessario deixar nenhum programa aberto para jogar!
echo   - O 'Knup 360 Painel de Controle' foi adicionado a sua Area
echo     de Trabalho caso queira remapear botoes futuramente.
echo ============================================================
echo.
pause