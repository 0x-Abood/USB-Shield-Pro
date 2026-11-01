# USB Shield Pro v2.0 — Testing & Validation Archive

This document archives the test framework architecture, regression methodology, historical development milestones, and final hardware validation results for USB Shield Pro v2.0.

---

## 1. Test Harness Architecture

The test harness is implemented in [`Core/TestHarness.cs`](../Core/TestHarness.cs) and invoked via:
```powershell
.\TestRunner.exe --test-harness --out test_results.txt
```

### Architectural Principles:
1. **Host Isolation**: Tests execute against temporary, isolated registry keys and simulated filesystem directories. Production registry policies (`HKLM\SOFTWARE\Policies...`) are never mutated during automated regression.
2. **Deterministic Seams**: In-memory delegates allow testing edge cases that cannot be reliably created on demand in hardware (e.g., driver crashes, corrupt buffers, privilege escalation failures, concurrent physical unplugs).
3. **Lineage Verification**: Regression runs must be verified not merely by total count, but by character-exact comparison of test labels to ensure no historical tests were dropped or skipped.

---

## 2. Test Suites & Coverage Breakdown

The v2.0 release baseline comprises **147 test cases** distributed across 9 core suites:

| Suite ID | Focus Area | Test Count | Key Invariants Verified |
|---|---|:---:|---|
| **Suite 1** | Security ACLs & Permissions | 14 | Restrictive NTFS ACLs, inheritance stripping, ownership by `BUILTIN\Administrators`. |
| **Suite 2** | Journaling & Transactions | 18 | Atomic write-ahead logging, baseline recorded once, verified post-write checks. |
| **Suite 3** | Removable Storage Policies | 16 | `Deny_All`, `Deny_Write`, `Deny_Read`, and WPD policy enforcement. |
| **Suite 4** | Device Installation Control | 18 | `AllowInstanceIDs` sequenced allocation without index collision, `AllowDenyLayered`. |
| **Suite 5** | Device Identity & Matching | 14 | Confidence tiers (`Strong`, `Medium`, `Weak`), serial parsing, and PnP paths. |
| **Suite 6** | Authentication & Crypto | 14 | PBKDF2-SHA256 (100k rounds), constant-time equality, 128-bit Master Recovery Key. |
| **Suite 7** | System Device Safety Guard | 15 | Physical Disk 0 permanent barrier, internal bus rejection (NVMe/SATA), peripheral checks. |
| **Suite 8** | Temporary Access Lifecycle | 12 | Timed unlock expiration, reboot persistence, automatic policy re-engagement. |
| **Suite 9** | PnP Eject, Retry & AutoPlay | 26 | Native `CM_Request_Device_EjectW`, target volume correlation, bounded retry (~10s), AutoPlay Guard. |
| **Total** | **Full Regression Baseline** | **147** | **147 Passed / 0 Failed (100% Pass Rate)** |

---

## 3. Major v2.0 Validation Milestones

### Milestone 1: PnP Safe-Eject Engine Transition
- **Problem**: Previous iterations relied on legacy FSCTL volume locking (`FSCTL_LOCK_VOLUME` / `FSCTL_DISMOUNT_VOLUME`). If a background process held an open handle, force-dismounting risked corrupting file data.
- **Architectural Fix**: Replaced legacy dismount with Microsoft-native Plug and Play ejection (`CM_Request_Device_EjectW` from `cfgmgr32.dll`).
- **Result**: Devices are removed cooperatively through standard Windows kernel safe removal.
- **Significance**: Zero risk of filesystem corruption.

### Milestone 2: Native Veto Buffer Hardening
- **Problem**: Calling `CM_Request_Device_EjectW` with uninitialized or improperly sized unmanaged buffers could lead to memory corruption or buffer overruns if Windows returned a non-null-terminated string.
- **Architectural Fix**: Implemented an explicit 520-byte (`MAX_PATH * sizeof(WCHAR)`) native buffer allocation with double-NUL zeroing and safe UTF-16 truncation.
- **Result**: Validated in Tests 9DN and 9DO; malformed buffers fail closed safely without buffer overrun.
- **Significance**: Eliminates memory corruption risks in native P/Invoke calls.

### Milestone 3: Bounded Target-Veto Retry Architecture
- **Problem**: On modern Windows 10/11 systems, newly inserted USB thumb drives are actively cataloged by Windows Search, volume manager, and antivirus filters for several seconds after arrival. An immediate eject call on arrival is frequently vetoed (`CR_REMOVE_VETOED`).
- **Architectural Fix**: Implemented bounded retry logic. If Attempt 1 receives `PNP_VetoDevice` on a devnode belonging to the target device tree, the engine transitions to `RetryWaiting` and schedules a single retry targeting ~10,000 ms total device age.
- **Result**: Validated in Test 9DP; retry executes cleanly with max 2 CM calls.
- **Significance**: Avoids premature ejection failures while strictly bounding resource usage.

### Milestone 4: Storage Volume Devnode Correlation
- **Problem**: Production hardware tests showed Windows vetoing ejections with devnodes named `STORAGE\Volume...#0000000000100000`. Because volume devnodes are parented under `ROOT\volmgr\0000` rather than the physical USB bus, naive parent-child tree walks rejected the veto as "unrelated", failing closed immediately.
- **Architectural Fix**: Enhanced [`PnpSafeEjectBackend.IsVetoInTargetDeviceTree`](../Devices/PnpSafeEjectBackend.cs#L587) with authoritative storage mapping:
  1. Detects `STORAGE\Volume` devnodes.
  2. Queries `IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS` to extract `PhysicalDiskNumber`.
  3. Confirms the disk number matches the target USB drive.
  4. Explicitly verifies the disk number is NOT Disk 0.
- **Result**: Validated in Test 9DW (target volume permits retry) and Test 9DX (unrelated volume denies retry).
- **Significance**: Solved the final hardware failure mode observed in production.

### Milestone 5: AutoPlay Guard Implementation & Regression
- **Problem**: Windows AutoPlay prompts user interaction upon drive insertion, providing an avenue for social engineering and accidental execution of untrusted media handlers.
- **Architectural Fix**: Created [`AutoPlayPolicyBackend`](../Windows/AutoPlayPolicyBackend.cs) managing `DisableAutoplay` (HKCU) and `NoAutoplayfornonVolume` (HKLM), with strict preservation of existing `NoDriveTypeAutoRun`.
- **Result**: Validated across Tests 9DY through 9EG; exact baseline restoration confirmed.
- **Significance**: Provides hardened media insertion suppression with zero collateral policy damage.

---

## 4. Final Production Hardware Retest Evidence

A final live hardware test was conducted on physical Windows hardware using an untrusted SanDisk Ultra USB 3.0 flash drive.

### Production Execution Summary:
- **Preconditions**: `UsbShieldPro.exe` running elevated as Administrator; `IsTestMode = false`; all seams `null`; Windows background service absent; internal Disk 0 protected.
- **Attempt 1**:
  - `CM_Request_Device_EjectW` returned: `CR_REMOVE_VETOED`
  - Veto Type: `PNP_VetoDevice` (31)
  - Veto Name: `STORAGE\Volume{...}`
  - Target Tree Correlation: **Matched target physical disk** (`PhysicalDiskNumber == 1`)
  - Retry Scheduled: **YES** (Delay calculated to reach ~10,000 ms total age)
  - System Tray Status: `"Preparing safe removal…"`
- **Attempt 2**:
  - Executed at total device age of ~10.1 seconds.
  - Pre-retry checks verified physical presence, untrusted status, and Disk 0 safety.
  - `CM_Request_Device_EjectW` returned: **`CR_SUCCESS` (0)**
  - Volumes F: and G: unmounted and disappeared.
  - Child disk node removed from active device tree.
  - Physical USB parent devnode entered `CM_PROB_HELD_FOR_EJECT` (Code 47).
  - Total CM calls: **2**
  - Legacy FSCTL fallback calls: **0**
  - Final Lifecycle State: **`EjectedUntilRemoval`**
  - System Tray Status: `"Device safely ejected"`
- **Final Verdict**: **PASS**

---

## 5. Release Package Smoke Verification

The final packaged binary in `release\USB-Shield-Pro-v2.0\UsbShieldPro.exe` was subjected to an independent non-destructive launch smoke check:

```
Release EXE launch: PASS (Process spawned, MainForm shown)
Standalone dependency check: PASS (104 modules from Windows/GAC only; 0 repo dependencies)
Startup policy mutation: NONE (Registry identical before and after startup)
Tray: PASS (TrayManager initialized in MainForm constructor)
Normal exit: PASS (Process handled clean termination; returned exit code 0)
SHA-256 match: PASS (0492e602f0e471b7efeb661690584c92f062a525118aff53af55ff72372ec622)

FINAL PACKAGE VERDICT: READY FOR DISTRIBUTION
```
