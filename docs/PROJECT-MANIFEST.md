# IMPORTANT FOR FUTURE AI/DEVELOPER:
> Read [`09-V2.0-BASELINE.md`](./09-V2.0-BASELINE.md) and [`01-TECHNICAL-REFERENCE.md`](./01-TECHNICAL-REFERENCE.md) before modifying production behavior.

---

# USB Shield Pro v2.0 — Machine-Readable Project Manifest

## 1. Product Metadata

| Key | Value |
|---|---|
| **Product** | USB Shield Pro |
| **Version** | 2.0 (Frozen Release Baseline) |
| **Release Status** | READY FOR DISTRIBUTION |
| **Copyright** | (c) 2026 x0abood. All rights reserved. |
| **Target OS** | Windows 10 / Windows 11 (x64) |
| **Framework** | Microsoft .NET Framework 4.8 |
| **Architecture** | 64-bit (`/platform:x64`) |
| **Binary Output** | `UsbShieldPro.exe` (812,544 bytes) |
| **Signing Status** | Unsigned (Legitimate; no fake certs) |
| **Elevation** | `requireAdministrator` (app.manifest) |
| **Total Test Baseline** | 147 test cases / 147 Passed / 0 Failed |
| **Total Production Files** | 44 C# source files |

---

## 2. Directory & Storage Paths

| Identifier | Path | Notes |
|---|---|---|
| **Repository Root** | `./` | Source, manifests, and build scripts |
| **Documentation Archive** | `./docs\` | Complete engineering archive |
| **Release Directory** | `./release\USB-Shield-Pro-v2.0\` | Packaged distribution executable and README |
| **Release Checksum** | `./release\USB-Shield-Pro-v2.0-SHA256.txt` | SHA-256 cryptographic hashes |
| **Persistent Data Dir** | `C:\ProgramData\USBShieldPro\` | Protected NTFS ACLs (`BUILTIN\Administrators`) |
| **Journal File** | `C:\ProgramData\USBShieldPro\journal.json` | Write-ahead baseline and operations |
| **Settings File** | `C:\ProgramData\USBShieldPro\settings.json` | UI preferences and settings |
| **Trusted Whitelist** | `C:\ProgramData\USBShieldPro\trusted_devices.json` | Whitelisted device serial numbers |
| **Auth File** | `C:\ProgramData\USBShieldPro\.auth` | DPAPI PBKDF2 password hash |
| **Recovery Key File** | `C:\ProgramData\USBShieldPro\.recovery` | DPAPI PBKDF2 recovery key hash |
| **Offline Script** | `C:\ProgramData\USBShieldPro\USBShield_Restore.bat` | Offline disaster rollback batch file |
| **Offline Reg** | `C:\ProgramData\USBShieldPro\USBShield_Restore.reg` | Offline disaster rollback registry file |
| **Startup Log** | `%TEMP%\UsbShieldPro_startup.log` | Early boot trace log |

---

## 3. Windows Registry Policies

| Hive | Key Path | Value Name | Type | Value ON | Value OFF | Notes |
|---|---|---|---|:---:|:---:|---|
| **HKLM** | `SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices` | `Deny_All` | `REG_DWORD` | 1 | Deleted | Global storage lockdown |
| **HKLM** | `SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices\{53f5630d-b6bf-11d0-94f2-00a0c91efb8b}` | `Deny_Write` | `REG_DWORD` | 1 | Deleted | Removable disks read-only |
| **HKLM** | `SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices\{53f5630d-b6bf-11d0-94f2-00a0c91efb8b}` | `Deny_Read` | `REG_DWORD` | 1 | Deleted | Removable disks deny read |
| **HKLM** | `SYSTEM\CurrentControlSet\Control\StorageDevicePolicies` | `WriteProtect` | `REG_DWORD` | 1 | Deleted | Legacy storage write-protect |
| **HKLM** | `SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices\{6AC27878-A6FA-4155-BA85-F98F491D4F33}` | `Deny_Write` | `REG_DWORD` | 1 | Deleted | WPD smartphones deny write |
| **HKLM** | `SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices\{6AC27878-A6FA-4155-BA85-F98F491D4F33}` | `Deny_Read` | `REG_DWORD` | 1 | Deleted | WPD smartphones deny read |
| **HKLM** | `SOFTWARE\Policies\Microsoft\Windows\DeviceInstall\Restrictions` | `AllowDenyLayered` | `REG_DWORD` | 1 | Deleted | Layered evaluation |
| **HKLM** | `SOFTWARE\Policies\Microsoft\Windows\DeviceInstall\Restrictions` | `DenyRemovableDevices` | `REG_DWORD` | 1 | Deleted | Strict installation deny |
| **HKLM** | `SOFTWARE\Policies\Microsoft\Windows\DeviceInstall\Restrictions\AllowInstanceIDs` | Sequential (`1`..) | `REG_SZ` | Instance ID | Deleted | Whitelisted PnP instance IDs |
| **HKCU** | `Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers` | `DisableAutoplay` | `REG_DWORD` | 1 | Baseline | AutoPlay Guard master switch |
| **HKLM** | `SOFTWARE\Policies\Microsoft\Windows\Explorer` | `NoAutoplayfornonVolume` | `REG_DWORD` | 1 | Baseline | AutoPlay Guard non-volume |
| **HKLM** | `Software\Microsoft\Windows\CurrentVersion\Policies\Explorer` | `NoDriveTypeAutoRun` | `REG_DWORD` | *None* | *None* | **PRESERVED**: Never modified |
| **HKLM** | `SYSTEM\CurrentControlSet\Services\USBSTOR` | `Start` | `REG_DWORD` | 3 or 4 | 3 | USB mass storage service driver |
| **HKLM** | `SYSTEM\CurrentControlSet\Services\uaspstor` | `Start` | `REG_DWORD` | 3 or 4 | 3 | USB Attached SCSI service driver |

---

## 4. Key Classes & Architecture Responsibilities

| Class | Namespace | Role / Invariant |
|---|---|---|
| `SystemDeviceSafetyGuard` | `UsbShieldPro.Core` | Absolute barrier: Disk 0, boot drive, and internal buses immune. |
| `PnpSafeEjectBackend` | `UsbShieldPro.Devices` | Microsoft-native `CM_Request_Device_EjectW`; zero FSCTL fallback. |
| `ActiveSentinel` | `UsbShieldPro.Devices` | Bounded retry (~10s); max 2 CM calls; PnP event consumer. |
| `DeviceEnforcementLifecycleManager` | `UsbShieldPro.Core` | Multi-generation lease manager; thread-safe state machine. |
| `RegistryTransactionHelper` | `UsbShieldPro.Windows` | Atomic write-ahead journal recorder; baseline capture once. |
| `RecoveryManager` | `UsbShieldPro.Core` | Standalone `.bat` and `.reg` rollback generation. |
| `EnforcementOwnerResolver` | `UsbShieldPro.Core` | Mutually exclusive owner: Service (Session 0) vs GUI (Session 1). |
| `AutoPlayPolicyBackend` | `UsbShieldPro.Windows` | AutoPlay Guard manager; preserves `NoDriveTypeAutoRun`. |
| `PasswordManager` | `UsbShieldPro.Security` | PBKDF2-SHA256 (100k rounds) with DPAPI local machine encryption. |
| `RecoveryKeyManager` | `UsbShieldPro.Security` | 128-bit Crockford Base32 key (`SHLD-...`); DPAPI hash storage. |

---

## 5. Command-Line Switch Reference

| Command Switch | Mode / Target | Required Privilege | Production Status |
|---|---|---|---|
| `(none)` | Interactive bilingual GUI | Administrator | Production |
| `--service` | Headless Windows Service host | SYSTEM / LocalService | Production |
| `--diag-detect` | Non-destructive CLI device & safety audit | Administrator | Production |
| `--diag-security` | NTFS ACL and directory ownership audit | Administrator | Production |
| `--restore-exact` | Programmatic rollback to pre-shield baseline | Administrator | Production / Emergency |
| `--install-service` | Registers `UsbShieldService` in Windows SCM | Administrator | Production |
| `--uninstall-service`| Unregisters `UsbShieldService` from Windows SCM| Administrator | Production |
| `--test-harness` | Runs isolated in-memory regression suite | Administrator | Developer / CI only |
| `--capture-pass3` | Generates 12 QA screenshots in temp folder | Administrator | Developer / QA only |
| `--force` / `--no-mutex`| Bypasses single-instance application mutex | Administrator | Diagnostic |

---

## 6. Build, Test & Recovery Commands

```powershell
# Clean Build Production Executable
cd .
.\build.bat

# Run In-Memory Automated Regression Suite (147 test cases)
.\TestRunner.exe --test-harness --out test_results.txt

# Run Non-Destructive CLI Hardware Diagnostic
.\TestRunner.exe --diag-detect

# Execute Offline Emergency Rollback (no executable required)
cmd /c "C:\ProgramData\USBShieldPro\USBShield_Restore.bat"

# Recompute Release Hashes
Get-FileHash -Algorithm SHA256 release\USB-Shield-Pro-v2.0\UsbShieldPro.exe
Get-FileHash -Algorithm SHA256 release\USB-Shield-Pro-v2.0\README.txt
```

---

## 7. Frozen Baseline Invariants Checklist

- [x] **Disk 0 Hard Invariant**: Physical Disk 0 and Windows system boot volume are permanently immune.
- [x] **PnP Safe-Eject Primary**: Primary ejection is strictly `CM_Request_Device_EjectW`.
- [x] **Zero FSCTL Fallback**: Strictly 0 legacy `FSCTL_DISMOUNT_VOLUME` or `FSCTL_LOCK_VOLUME` calls.
- [x] **Bounded Retry**: Exactly 2 CM calls maximum per cycle; ~10s total age target.
- [x] **Volume Correlation**: `STORAGE\Volume` devnodes correlated by physical disk number.
- [x] **Code 47 Verification**: `CM_PROB_HELD_FOR_EJECT` accepted as successful post-verification.
- [x] **Single Owner**: Service (Session 0) and GUI (Session 1) never enforce concurrently.
- [x] **Exact Baseline Restore**: Offline scripts restore exact previous values; never defaults.
- [x] **AutoPlay Guard**: Toggles `DisableAutoplay` & `NoAutoplayfornonVolume`; preserves `NoDriveTypeAutoRun`.
- [x] **Production Seams Null**: All simulation seams disabled in production (`IsTestMode = false`).
- [x] **UI Non-Intrusive**: `TopMost = false` across all UI forms and dialogs.
- [x] **Non-Destructive Operations**: Zero formatting, zero chkdsk, zero Explorer killing.
