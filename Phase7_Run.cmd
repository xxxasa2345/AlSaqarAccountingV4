@echo off
setlocal
cd /d "%~dp0"
echo ============================================================
echo AlSaqarAccounting - Phase 7.2
 echo ============================================================
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Phase7_Install.ps1"
echo.
echo ============================================================
echo Exit code: %ERRORLEVEL%
echo Log: %~dp0phase7-install.log
echo ============================================================
pause
