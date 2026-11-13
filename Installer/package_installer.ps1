param(
    [Parameter(Mandatory=$false)]
    [string]$SourceExe,

    [Parameter(Mandatory=$false)]
    [string]$OutDir,

    [Parameter(Mandatory=$false)]
    [string]$OutName = "USB-Shield-Pro-v2.1.1-Setup.exe"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = (Resolve-Path "$scriptDir\..").Path

if (-not $SourceExe) {
    if (Test-Path "$rootDir\UsbShieldPro.exe") {
        $SourceExe = "$rootDir\UsbShieldPro.exe"
    } else {
        $SourceExe = "$rootDir\staging\USB-Shield-Pro-v2.1.1-Candidate\UsbShieldPro.exe"
    }
}
if (-not $OutDir) {
    $OutDir = "$rootDir\staging\USB-Shield-Pro-v2.1.1-Candidate"
}

if (-not (Test-Path $SourceExe)) {
    Write-Error "Source executable does not exist: $SourceExe"
    exit 1
}

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $csc)) {
    Write-Error "csc.exe not found"
    exit 1
}

# 1. Derive Length and SHA-256 automatically from the EXACT executable being packaged
$fileInfo = Get-Item $SourceExe
$exeLength = $fileInfo.Length
$exeHash = (Get-FileHash -Path $SourceExe -Algorithm SHA256).Hash.ToUpperInvariant()

Write-Host "Deriving payload manifest from exact target binary:"
Write-Host "  File:    $SourceExe"
Write-Host "  Length:  $exeLength bytes"
Write-Host "  SHA-256: $exeHash"

# 2. Write dynamic PayloadManifest.cs
$manifestContent = @"
// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro — Installer Embedded Payload Manifest
// Automatically generated during packaging. Do not edit manually.
// ═══════════════════════════════════════════════════════════════════════════

namespace UsbShieldPro.Installer
{
    internal static class PayloadManifest
    {
        public static long ExpectedExeLength = ${exeLength}L;
        public static string ExpectedExeSha256 = "$exeHash";
    }
}
"@

$manifestPath = "$scriptDir\PayloadManifest.cs"
[System.IO.File]::WriteAllText($manifestPath, $manifestContent, [System.Text.Encoding]::UTF8)

# 3. Compile Setup Program
if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}

$outSetupPath = Join-Path $OutDir $OutName
$configPath = "$SourceExe.config"
$readmePath = Join-Path (Split-Path -Parent $SourceExe) "README.txt"
if (-not (Test-Path $configPath)) { $configPath = "$rootDir\UsbShieldPro.exe.config" }
if (-not (Test-Path $readmePath)) { $readmePath = "$rootDir\README.md" }

Write-Host "Compiling setup executable: $outSetupPath..."

$cscArgs = @(
    "/target:winexe",
    "/out:$outSetupPath",
    "/win32manifest:$rootDir\app.manifest",
    "/win32icon:$rootDir\Assets\Brand\UsbShieldPro.ico",
    "/optimize+",
    "/debug-",
    "/nologo",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.ServiceProcess.dll",
    "/resource:$SourceExe,UsbShieldPro.exe",
    "/resource:$configPath,UsbShieldPro.exe.config",
    "/resource:$readmePath,README.txt",
    "$scriptDir\PayloadManifest.cs",
    "$scriptDir\SetupProgram.cs"
)

$process = Start-Process -FilePath $csc -ArgumentList $cscArgs -NoNewWindow -Wait -PassThru
if ($process.ExitCode -ne 0) {
    Write-Error "Setup compilation failed with exit code $($process.ExitCode)"
    exit $process.ExitCode
}

# 4. Post-build Integrity Verification of the Embedded Resource
Write-Host "Performing post-build verification of embedded resource inside setup binary..."
$bytes = [System.IO.File]::ReadAllBytes($outSetupPath)
$asm = [System.Reflection.Assembly]::Load($bytes)
$resStream = $asm.GetManifestResourceStream("UsbShieldPro.exe")
if ($null -eq $resStream) {
    Remove-Item $outSetupPath -Force -ErrorAction SilentlyContinue
    Write-Error "FATAL: Embedded resource 'UsbShieldPro.exe' not found in compiled setup!"
    exit 1
}

$resBytes = New-Object byte[] $resStream.Length
$resStream.Read($resBytes, 0, $resBytes.Length) | Out-Null
$resStream.Close()

$sha = [System.Security.Cryptography.SHA256]::Create()
$embeddedHash = [System.BitConverter]::ToString($sha.ComputeHash($resBytes)).Replace("-", "").ToUpperInvariant()
$sha.Dispose()

if ($resBytes.Length -ne $exeLength -or $embeddedHash -ne $exeHash) {
    Remove-Item $outSetupPath -Force -ErrorAction SilentlyContinue
    Write-Error "FATAL INTEGRITY FAILURE: Embedded resource ($($resBytes.Length) bytes, $embeddedHash) does not match source binary ($exeLength bytes, $exeHash)!"
    exit 1
}

Write-Host "POST-BUILD INTEGRITY VERIFICATION SUCCESSFUL!" -ForegroundColor Green
Write-Host "  Setup Binary:  $outSetupPath"
Write-Host "  Embedded Size: $($resBytes.Length) bytes (Verified matching $exeLength)"
Write-Host "  Embedded SHA:  $embeddedHash (Verified matching $exeHash)"
