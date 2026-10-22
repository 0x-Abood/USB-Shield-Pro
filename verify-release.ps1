<#
.SYNOPSIS
    Verifies the cryptographic integrity of USB Shield Pro release packages.
.DESCRIPTION
    Computes the SHA-256 hash of downloaded release artifacts and compares them
    against the official published gold master checksums.
.EXAMPLE
    .\verify-release.ps1 -FilePath ".\USB-Shield-Pro-v2.1.0-Setup.exe"
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [string]$FilePath = ".\release\USB-Shield-Pro-v2.1.0-Setup.exe"
)

$KnownHashes = @{
    "USB-Shield-Pro-v2.1.0-Setup.exe"    = "A8E0DB5305CD3099D0F0580B5EB5A97E54CE7352E108C199B6532CF85A203CDC"
    "USB-Shield-Pro-v2.1.0-Portable.zip"  = "64C525CE05419592516EAE76FAA8A680DF14D024D6B05BEBCB4BF2300B0102AD"
    "UsbShieldPro.exe"                   = "4F9A904E9AB5236C18D298DD78443D5021DB5781BFCF16C0C037100DFEC5AFA9"
    "UsbShieldPro.exe.config"            = "53840DABDB7510AC21965024AEC4797025C920C965D29BBAA46FFA324338C5B1"
    "README.txt"                         = "3B72B744C64DF260E54BBC5AE5B0888C9F742C320DB3C62C9D26D5CBC1DAEA4F"
}

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "   USB Shield Pro v2.1.0 -- Cryptographic Hash Verifier" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

if (-not (Test-Path $FilePath)) {
    Write-Host "Target file not found: $FilePath" -ForegroundColor Yellow
    Write-Host "Scanning release directory instead..." -ForegroundColor Yellow
    $FilePath = ".\release\USB-Shield-Pro-v2.1.0-Setup.exe"
}

if (-not (Test-Path $FilePath)) {
    Write-Host "ERROR: No release file found to verify." -ForegroundColor Red
    exit 1
}

$fileItem = Get-Item $FilePath
$fileName = $fileItem.Name
$actualHash = (Get-FileHash -Path $fileItem.FullName -Algorithm SHA256).Hash.ToUpperInvariant()

Write-Host "File Name:    $fileName" -ForegroundColor White
Write-Host "File Size:    $($fileItem.Length) bytes" -ForegroundColor White
Write-Host "Actual Hash:  $actualHash" -ForegroundColor White

if ($KnownHashes.ContainsKey($fileName)) {
    $expectedHash = $KnownHashes[$fileName]
    Write-Host "Official:     $expectedHash" -ForegroundColor White
    Write-Host "------------------------------------------------------------" -ForegroundColor Gray
    if ($actualHash -eq $expectedHash) {
        Write-Host "VERIFICATION RESULT: [PASS] 100% BITWISE MATCH" -ForegroundColor Green
        Write-Host "The binary is authentic, unmodified, and clean-room certified." -ForegroundColor Green
        exit 0
    } else {
        Write-Host "VERIFICATION RESULT: [FAIL] HASH MISMATCH!" -ForegroundColor Red
        Write-Host "WARNING: The file may have been altered or corrupted in transit." -ForegroundColor Red
        exit 2
    }
} else {
    Write-Host "Note: File is not in default release catalog. Hash computed above." -ForegroundColor Yellow
}
