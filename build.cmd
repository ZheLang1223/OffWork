@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0src\build.ps1"
if errorlevel 1 (
    echo Build failed.
    pause
    exit /b 1
)
