@echo off
cd /d "%~dp0"
if exist "%~dp07K Downloader.exe" (
  start "" "%~dp07K Downloader.exe"
) else (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build.ps1"
  if errorlevel 1 exit /b 1
  start "" "%~dp07K Downloader.exe"
  pause
)
