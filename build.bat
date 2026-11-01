@echo off
echo ============================================
echo   USB Shield Pro v2.1 - Build Script
echo   (c) 2026 0xAbood. All rights reserved.
echo ============================================
echo.

setlocal
set "ROOTDIR=%~dp0"
cd /d "%ROOTDIR%"

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo ERROR: Could not find csc.exe. Ensure .NET Framework 4.x is installed.
    pause
    exit /b 1
)

echo Using compiler: %CSC%
set OUTDIR=%~1
if "%OUTDIR%"=="" set OUTDIR=.
if not exist "%OUTDIR%" mkdir "%OUTDIR%"

echo Output directory: %OUTDIR%
echo.
echo Compiling USB Shield Pro v2.1...

"%CSC%" /target:winexe /out:"%OUTDIR%\UsbShieldPro.exe" /win32manifest:app.manifest /win32icon:Assets\Brand\UsbShieldPro.ico /optimize+ /debug- /nologo ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Security.dll ^
  /reference:System.Management.dll ^
  /reference:System.Web.Extensions.dll ^
  /reference:System.ServiceProcess.dll ^
  /resource:Assets\Brand\UsbShieldPro.ico,UsbShieldPro.Brand.UsbShieldPro.ico ^
  /resource:Assets\Brand\usbshield-icon-64.png,UsbShieldPro.Brand.usbshield-icon-64.png ^
  /define:PRODUCTION ^
  /recurse:Properties\*.cs ^
  /recurse:Models\*.cs ^
  /recurse:Core\*.cs ^
  /recurse:Devices\*.cs ^
  /recurse:Persistence\*.cs ^
  /recurse:Security\*.cs ^
  /recurse:Windows\*.cs ^
  /recurse:Service\*.cs ^
  /recurse:UI\*.cs ^
  UsbBlocker.cs ^
  Localization.cs ^
  UIControls.cs ^
  TrayManager.cs ^
  MainForm.cs

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo BUILD FAILED! Check errors above.
    exit /b 1
)

echo.
echo Compiling TestRunner.exe...

"%CSC%" /target:exe /out:"%OUTDIR%\TestRunner.exe" /win32icon:Assets\Brand\UsbShieldPro.ico /optimize+ /debug- /nologo ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Security.dll ^
  /reference:System.Management.dll ^
  /reference:System.Web.Extensions.dll ^
  /reference:System.ServiceProcess.dll ^
  /resource:Assets\Brand\UsbShieldPro.ico,UsbShieldPro.Brand.UsbShieldPro.ico ^
  /resource:Assets\Brand\usbshield-icon-64.png,UsbShieldPro.Brand.usbshield-icon-64.png ^
  /recurse:Properties\*.cs ^
  /recurse:Models\*.cs ^
  /recurse:Core\*.cs ^
  /recurse:Devices\*.cs ^
  /recurse:Persistence\*.cs ^
  /recurse:Security\*.cs ^
  /recurse:Windows\*.cs ^
  /recurse:Service\*.cs ^
  /recurse:UI\*.cs ^
  UsbBlocker.cs ^
  Localization.cs ^
  UIControls.cs ^
  TrayManager.cs ^
  ScreenCaptureRunner.cs ^
  MainForm.cs

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo TESTRUNNER BUILD FAILED! Check errors above.
    exit /b 1
)

if exist App.config (
    copy /y App.config "%OUTDIR%\UsbShieldPro.exe.config" >nul
    copy /y App.config "%OUTDIR%\TestRunner.exe.config" >nul
)

echo.
echo ============================================
echo   BUILD SUCCESSFUL!
echo   Output: %OUTDIR%\UsbShieldPro.exe
echo   Target: .NET Framework 4.8 / Windows 10/11
echo ============================================
echo.
echo HOW TO USE:
echo   1. Double-click UsbShieldPro.exe or run as administrator
echo   2. CLI Diagnostic: UsbShieldPro.exe --diag-detect
echo   3. Background Service: UsbShieldPro.exe --service
echo   4. Emergency Restore: UsbShieldPro.exe --restore-defaults
echo.
