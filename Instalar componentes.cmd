@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\setup-runtime.ps1"
if errorlevel 1 echo Falha na instalacao. Consulte a mensagem acima e tente novamente.
pause
