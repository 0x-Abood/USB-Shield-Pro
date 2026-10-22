# USB Shield Pro v2.0 — Build & Development Guide

This guide provides complete, reproducible instructions for compiling, testing, developing, and releasing USB Shield Pro v2.0 on a clean Windows machine.

---

## 1. Development Environment & Prerequisites

USB Shield Pro is intentionally engineered with **zero external NuGet or third-party dependencies**. It compiles directly against Microsoft .NET Framework 4.8 reference assemblies using the native 64-bit C# compiler pre-installed on every Windows system.

### Minimum System Requirements
- **Host Operating System**: Windows 10 (version 1903 or later) or Windows 11 (64-bit / x64).
- **C# Compiler**: `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` (ships with .NET Framework 4.8).
- **IDE / Editor**: Visual Studio 2019/2022, VS Code, or any plain text editor. (Visual Studio is completely optional; compilation does NOT depend on MSBuild or `.csproj` project files).
- **Execution Shell**: Windows PowerShell 5.1+ or standard Windows Command Prompt (`cmd.exe`).
- **Administrative Privileges**: Required to build with manifests that specify `requireAdministrator` and to execute local tests.

---

## 2. Repository Structure

```
./
├── Core\                               # Domain orchestration & lifecycle management
│   ├── DeviceEnforcementLifecycle.cs   # State machine, generations, leases, PnpHelper
│   ├── DiagnosticsTiming.cs            # Arrival and latency diagnostic tracking
│   ├── EnforcementOwnerResolver.cs     # Single-enforcement-owner arbitration (Service vs GUI)
│   ├── ProtectionEngine.cs             # Policy coordination and reconciliation
│   ├── RecoveryManager.cs              # .bat/.reg disaster recovery package generation
│   ├── RuleEngine.cs                   # Device-to-rule evaluation and decision matrix
│   ├── StateVerifier.cs                # Live 64-bit registry state audit engine
│   ├── SystemDeviceSafetyGuard.cs      # Disk 0, internal bus, and peripheral safety barrier
│   ├── TemporaryAccessManager.cs       # Timed temporary unlock lifecycle & persistence
│   └── TestHarness.cs                  # Comprehensive in-memory test runner (147 test cases)
│
├── Devices\                            # Physical device detection & PnP interaction
│   ├── ActiveSentinel.cs               # Event consumer, bounded retry orchestrator, trace
│   ├── DeviceClassifier.cs             # Device categorization (Flash, SSD, HDD, Phone)
│   ├── DeviceDiscovery.cs              # SetupAPI and WMI disk discovery & correlation
│   ├── DeviceEjector.cs                # Legacy dismount interface (Historical / Seam)
│   ├── DeviceIdentityResolver.cs       # Unique identity extraction (serial, instance ID)
│   ├── PnpSafeEjectBackend.cs          # Native CM_Request_Device_EjectW PnP safe-eject engine
│   └── StorageNotificationManager.cs   # Win32 WM_DEVICECHANGE hidden window message pump
│
├── Models\                             # Domain entities and contracts
│   ├── DeviceEjectOperationResult.cs   # Structured PnP eject telemetry & diagnostics
│   ├── DeviceIdentity.cs               # Normalized physical device metadata
│   ├── DeviceRule.cs                   # Whitelist / policy rule definition
│   ├── DiscoveryTimingSnapshot.cs      # Millisecond-level hardware discovery checkpoints
│   ├── Enums.cs                        # Domain enumerations (States, Classes, Results)
│   ├── ProtectionPolicy.cs             # Requested administrative protection rules
│   ├── ProtectionStatusReport.cs       # Status overview & health report
│   └── StateJournalModels.cs           # Write-ahead baseline and operation journal models
│
├── Persistence\                        # Local disk persistence and security
│   ├── JournalRepository.cs            # Atomic write-ahead journal (journal.json)
│   ├── SecurityAclHelper.cs            # ProgramData NTFS ACLs and ownership enforcement
│   ├── SettingsRepository.cs           # User preferences persistence (settings.json)
│   └── TrustedDeviceRepository.cs      # Whitelist repository (trusted_devices.json)
│
├── Security\                           # Cryptographic authentication engines
│   ├── PasswordManager.cs              # PBKDF2-SHA256 password engine (100,000 iterations)
│   └── RecoveryKeyManager.cs           # 128-bit Crockford Base32 Master Recovery Key engine
│
├── Service\                            # Background Windows Service host
│   ├── ServiceManager.cs               # Service installation, start/stop, IPC client
│   └── UsbShieldService.cs             # Session 0 headless enforcement worker
│
├── Windows\                            # Windows policy and registry mutators
│   ├── AutoPlayPolicyBackend.cs        # AutoPlay Guard registry policy manager
│   ├── DeviceInstallationPolicyBackend.cs # Windows DeviceInstall Restrictions policy manager
│   ├── RegistryTransactionHelper.cs    # Atomic journaled registry mutator
│   ├── RemovableStoragePolicyBackend.cs# RemovableStorageDevices policy manager
│   └── WindowsSecurityConstants.cs     # Authoritative registry paths and Class GUIDs
│
├── docs\                               # Engineering-grade project documentation archive
├── release\                            # Packaged release binaries and checksums
│   ├── USB-Shield-Pro-v2.0\
│   │   ├── UsbShieldPro.exe            # Production standalone executable
│   │   └── README.txt                  # End-user release documentation
│   └── USB-Shield-Pro-v2.0-SHA256.txt  # Authoritative SHA-256 release checksums
│
├── app.manifest                        # UAC execution level (requireAdministrator)
├── build.bat                           # Authoritative one-click build script
├── Localization.cs                     # English and Arabic bilingual dictionary
├── MainForm.cs                         # Primary WinForms interface and event coordinator
├── ScreenCaptureRunner.cs              # Native UI screenshot automation (QA pass)
├── shield.ico                          # Multi-resolution application icon
├── TrayManager.cs                      # System tray icon, notifications, and menu
├── UIControls.cs                       # Custom high-DPI GDI+ cards, buttons, badges
└── UsbBlocker.cs                       # Primary application entry point (`Program.Main`)
```

---

## 3. Clean Build Procedure

To compile a clean release binary from source:

### Step 1: Open an Elevated Command Prompt
Open `cmd.exe` or PowerShell as Administrator and change directory to the repository root:
```powershell
cd .
```

### Step 2: Remove Stale Executables
```powershell
if (Test-Path UsbShieldPro.exe) { Remove-Item UsbShieldPro.exe -Force }
if (Test-Path TestRunner.exe) { Remove-Item TestRunner.exe -Force }
```

### Step 3: Run the Build Script
Execute [`build.bat`](../build.bat):
```cmd
build.bat
```

### Step 4: Verify Compiler Flags & Output
`build.bat` invokes `csc.exe` with these exact flags:
```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
  /target:winexe ^
  /out:UsbShieldPro.exe ^
  /win32manifest:app.manifest ^
  /win32icon:shield.ico ^
  /optimize+ ^
  /nologo ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Security.dll ^
  /reference:System.Management.dll ^
  /reference:System.Web.Extensions.dll ^
  /reference:System.ServiceProcess.dll ^
  /recurse:Models\*.cs ^
  /recurse:Core\*.cs ^
  /recurse:Devices\*.cs ^
  /recurse:Persistence\*.cs ^
  /recurse:Security\*.cs ^
  /recurse:Windows\*.cs ^
  /recurse:Service\*.cs ^
  UsbBlocker.cs Localization.cs UIControls.cs TrayManager.cs ScreenCaptureRunner.cs MainForm.cs
```

It also compiles `TestRunner.exe` (`/target:exe /optimize+`) using the identical codebase for CLI test execution.

---

## 4. Test Harness Execution & Regression Baseline

The test harness exercises all components in memory using isolated registry hives and simulation seams without modifying the host computer's registry policies.

### Running the Test Suite:
```powershell
.\TestRunner.exe --test-harness --out test_results.txt
```

### Expected Output:
- **Baseline Test Cases**: Exactly **147 test cases** across 146 test suites.
- **Failures Allowed for Release**: **Strictly 0**.
- **Exit Code**: `0` on 100% pass; non-zero if any test fails.

### Major Test Suites in `TestHarness.cs`:
1. **Suite 1**: Security ACL and ownership enforcement on `%ProgramData%`.
2. **Suite 2**: Write-ahead journal atomic recording and post-write verification.
3. **Suite 3**: Removable storage policy toggles and Group Policy integrity.
4. **Suite 4**: Device Installation Restrictions (`AllowInstanceIDs` sequenced allocation).
5. **Suite 5**: Device identity resolution, serial numbers, and confidence tiers.
6. **Suite 6**: PBKDF2 password hashing (100k iterations) and 128-bit Master Recovery Keys.
7. **Suite 7**: System device safety guard (Disk 0 protection, internal buses).
8. **Suite 8**: Temporary access lifecycle, persistent reload, and auto-relock.
9. **Suite 9**: PnP Safe-Eject backend, native veto buffers, bounded retry (~10s), `STORAGE\Volume` correlation, and AutoPlay Guard baselines.

---

## 5. Test Seams & Production Integrity Rules

To allow comprehensive automated testing of hardware interactions and kernel PnP calls, several classes contain internal simulation delegates ("test seams"):

| Component | Test Seam Name | Production State | Danger if Shipped Enabled |
|---|---|---|---|
| `DeviceEnforcementLifecycleManager` | `IsTestMode` | `false` | Bypasses actual hardware verification. |
| `DeviceEnforcementLifecycleManager` | `PresenceSeam` | `null` | Fakes physical device presence. |
| `DeviceEnforcementLifecycleManager` | `DelaySeam` | `null` | Short-circuits the 10-second retry backoff. |
| `DeviceEnforcementLifecycleManager` | `NotificationSinkSeam` | `null` | Suppresses real UI balloons. |
| `PnpSafeEjectBackend` | `CmRequestDeviceEjectSeam` | `null` | Simulates PnP ejection without calling kernel. |
| `PnpSafeEjectBackend` | `VetoTargetTreeSeam` | `null` | Bypasses real volume-to-disk correlation. |
| `AutoPlayPolicyBackend` | `ApplySeam` / `RestoreSeam` | `null` | Fakes AutoPlay Guard registry application. |

> [!CAUTION]
> **Production Seams Rule**: All test seams are guarded by `if (DeviceEnforcementLifecycleManager.IsTestMode)` and default to `null`. In production builds, `IsTestMode` is unconditionally `false`. Never enable test seams in distributed binaries.

---

## 6. Hardware Validation Methodology

> [!NOTE]
> **HARDWARE TESTS SHOULD ONLY BE RUN WHEN DEVICE ENFORCEMENT CODE CHANGES.**  
> Routine software updates and documentation tasks do NOT require hardware tests.

When device enforcement, volume correlation, or retry timing is modified, execute validation on physical hardware using an untrusted USB flash drive (e.g., SanDisk Ultra USB 3.0):

1. **Verify Baseline State**:
   - `UsbShieldPro.exe` running elevated as Administrator.
   - Protection Mode: ON (Flash Sentinel / Whitelist reactive mode).
   - Test USB drive is physically disconnected.
   - System Disk 0 is verified protected.
2. **Insert Physical Media**:
   - Connect the untrusted USB drive ONCE.
3. **Observed Expected Sequence**:
   - **Attempt 1**: Device arrives; `CM_Request_Device_EjectW` returns `CR_REMOVE_VETOED` with `PNP_VetoDevice` on target volume/SCSI devnode.
   - **UI Feedback**: System tray displays `"Preparing safe removal…"`.
   - **Backoff**: Engine sleeps until total connection age reaches ~10 seconds.
   - **Attempt 2**: Fresh safety and target validation passes; second `CM_Request_Device_EjectW` call issues.
   - **Outcome**: Returns `CR_SUCCESS`. Volume mount points disappear. Child disk ceases activity. Parent devnode enters `CM_PROB_HELD_FOR_EJECT` (Code 47).
   - **UI Feedback**: System tray displays `"Device safely ejected"`.
   - **Telemetry**: Exactly 2 CM calls; exactly 0 legacy FSCTL dismount calls.

---

## 7. Safe Development Rules

Future maintainers must adhere to these non-negotiable development safety rules:

1. **NEVER Execute Destructive Disk Commands**: Do not use `diskpart clean`, format commands, or partition table writes in any test or production code.
2. **NEVER Run `chkdsk` on Test Media**: Running filesystem repairs alters drive state and invalidates timing baselines.
3. **NEVER Kill `explorer.exe`**: Terminating Windows Explorer to force file handle releases is dangerous and forbidden.
4. **NEVER Modify `USBSTOR` / `uaspstor` Blindly**: Changing driver start values without a verified rollback journal risks disabling all USB peripherals.
5. **NEVER Target Physical Disk 0**: All drive operations must strictly verify `PhysicalDiskNumber > 0` and confirm the device does not host the Windows system directory.
6. **NEVER Assume Drive Letters Identify Devices**: Drive letters are volatile mount points. Always correlate devices by physical serial number and PnP instance ID.
7. **NEVER Bypass `SystemDeviceSafetyGuard`**: Every device must pass safety validation before policy execution.

---

## 8. Modifying Auto-Eject Logic: Developer Checklist

Before touching `PnpSafeEjectBackend.cs`, `ActiveSentinel.cs`, or `DeviceEnforcementLifecycle.cs`, you MUST verify:

- [ ] Will Attempt 1 still call `CM_Request_Device_EjectW`?
- [ ] Is retry eligibility strictly limited to `PNP_VetoDevice` on devnodes correlated to the target physical disk?
- [ ] Is total retry timing targeting ~10s total device age preserved?
- [ ] Is the maximum number of CM calls per cycle capped at exactly 2?
- [ ] Is legacy FSCTL fallback strictly prohibited after PnP veto?
- [ ] Does `STORAGE\Volume` correlation still verify `PhysicalDiskNumber == target.PhysicalDiskNumber` and reject Disk 0?
- [ ] Are all 5 pre-retry checks (presence, dynamic trust, dynamic policy, fresh target resolution, fresh safety guard) executed before Attempt 2?
- [ ] Does full test regression pass: `TestRunner.exe --test-harness` (147/147 PASS)?

---

## 9. Adding a New Protection Feature (Architecture Template)

When adding a new policy or feature, follow the pattern established by AutoPlay Guard:

1. **Define Domain Model**: Add properties to [`ProtectionPolicy.cs`](../Models/ProtectionPolicy.cs).
2. **Add Constants**: Declare authoritative registry keys and values in [`WindowsSecurityConstants.cs`](../Windows/WindowsSecurityConstants.cs).
3. **Create Policy Backend**: Implement a dedicated backend in `Windows/` (e.g., `NewFeaturePolicyBackend.cs`).
4. **Enforce Write-Ahead Journaling**: Route all writes through `RegistryTransactionHelper.SetDwordValue` or `SetStringValue`.
5. **Implement Exact Baseline Restore**: Ensure toggling OFF restores the pre-shield baseline rather than default values.
6. **Update Offline Recovery**: Ensure `RecoveryManager` captures the new settings in `.bat` and `.reg` scripts.
7. **Expose in Engine**: Wire into `ProtectionEngine.ApplyPolicy` and `StateVerifier.VerifyActivePolicies`.
8. **Add UI Controls**: Add a toggle card in `MainForm.cs` using `UIControls.RuleToggleCard`.
9. **Add Bilingual Strings**: Add English and Arabic dictionary entries in `Localization.cs`.
10. **Write Test Suite**: Add isolated unit tests in `TestHarness.cs` validating absent-to-ON-to-OFF and existing-to-ON-to-OFF flows.

---

## 10. Code Signing Policy

- **Current Repository State**: `UsbShieldPro.exe` is distributed **legitimately unsigned**.
- **Authenticode Requirements**: For enterprise deployment, sign the release executable using a valid corporate Code Signing Certificate issued by a trusted public Certificate Authority (e.g., DigiCert, Sectigo):
  ```cmd
  signtool.exe sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 /a "release\USB-Shield-Pro-v2.0\UsbShieldPro.exe"
  ```
- **Prohibition on Dummy Certs**: Never generate self-signed or dummy root certificates and install them into the user's Trusted Root Certification Authorities store. Distributing an unsigned binary with documented SmartScreen bypass is significantly safer than compromising the host certificate trust store.
