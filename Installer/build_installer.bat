@echo off
echo ============================================
echo   USB Shield Pro v2.1.0 - Installer Build
echo   (c) 2026 0xAbood. All rights reserved.
echo ============================================
echo.

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo ERROR: Could not find csc.exe. Ensure .NET Framework 4.x is installed.
    exit /b 1
)

echo Using compiler: %CSC%
set ROOTDIR=%~dp0..
set OUTDIR=%ROOTDIR%\release

if not exist "%OUTDIR%" mkdir "%OUTDIR%"

echo Compiling USB-Shield-Pro-v2.1.0-Setup.exe...

"%CSC%" /target:winexe /out:"%OUTDIR%\USB-Shield-Pro-v2.1.0-Setup.exe" /win32manifest:"%ROOTDIR%\app.manifest" /win32icon:"%ROOTDIR%\Assets\Brand\UsbShieldPro.ico" /optimize+ /debug- /nologo ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:System.ServiceProcess.dll ^
  /resource:"%OUTDIR%\USB-Shield-Pro-v2.1\UsbShieldPro.exe",UsbShieldPro.exe ^
  /resource:"%OUTDIR%\USB-Shield-Pro-v2.1\UsbShieldPro.exe.config",UsbShieldPro.exe.config ^
  /resource:"%OUTDIR%\USB-Shield-Pro-v2.1\README.txt",README.txt ^
  "%~dp0SetupProgram.cs"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo INSTALLER BUILD FAILED!
    exit /b 1
)

echo.
echo ============================================
echo   INSTALLER BUILD SUCCESSFUL!
echo   Output: %OUTDIR%\USB-Shield-Pro-v2.1.0-Setup.exe
echo ============================================
echo.
