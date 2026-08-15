@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-revit-addin.ps1"

echo.
echo ======================================================
echo   Check install-log.txt in this folder for details.
echo ======================================================
pause
