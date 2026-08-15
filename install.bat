@echo off
REM Double-click this file to install the Revit add-in automatically.
REM No need to open Visual Studio or copy any files manually.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-revit-addin.ps1"

echo.
echo ======================================================
echo   This window is intentionally kept open.
echo   If there is an error message above, screenshot or
echo   copy the text, and also check install-log.txt in
echo   this folder.
echo ======================================================
pause
