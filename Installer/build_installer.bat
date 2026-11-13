@echo off
setlocal
echo ============================================
echo   USB Shield Pro - Dynamic Installer Build
echo   (c) 2026 0xAbood. All rights reserved.
echo ============================================
echo.

set SCRIPT_DIR=%~dp0
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%package_installer.ps1" %*
set EXIT_CODE=%ERRORLEVEL%

if %EXIT_CODE% NEQ 0 (
    echo.
    echo ============================================
    echo   INSTALLER BUILD FAILED! (Exit Code: %EXIT_CODE%)
    echo ============================================
    exit /b %EXIT_CODE%
)

echo.
echo ============================================
echo   INSTALLER BUILD COMPLETED SUCCESSFULLY!
echo ============================================
exit /b 0
