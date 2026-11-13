<p align="center">
  <img src="Assets/Brand/usbshield-icon-128.png" alt="USB Shield Pro Logo" width="100" height="100">
</p>

<h1 align="center">USB Shield Pro</h1>

<p align="center">
  <strong>Production-ready Windows USB storage control utility for workstations, labs, and administrative environments</strong>
</p>

<p align="center">
  <a href="README.md"><strong>English</strong></a> &bull;
  <a href="README.ar.md"><strong>العربية</strong></a>
</p>

<p align="center">
  <a href="https://github.com/0x-Abood/USB-Shield-Pro/releases/tag/v2.1.1"><img src="https://img.shields.io/badge/Release-v2.1.1-00b4d8.svg" alt="Release v2.1.1"></a>
  <a href="https://github.com/0x-Abood/USB-Shield-Pro/actions/workflows/ci.yml"><img src="https://github.com/0x-Abood/USB-Shield-Pro/actions/workflows/ci.yml/badge.svg" alt="Build & Test Harness"></a>
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20x64-0077b6.svg" alt="Platform: Windows 10 / 11">
  <img src="https://img.shields.io/badge/Runtime-.NET%20Framework%204.8-2b9348.svg" alt="Runtime: .NET 4.8">
  <img src="https://img.shields.io/badge/Architecture-Native%20Win32%20%2F%20C%23-e76f51.svg" alt="Architecture: Native Win32">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-7209b7.svg" alt="License: MIT"></a>
</p>

---

## Overview

**USB Shield Pro** is an open-source, production-ready Windows USB storage control utility engineered for Windows 10 and 11 workstations, labs, and administrative environments. It provides dedicated **USB Mass Storage & WPD/MTP Endpoint Control** to protect systems against unauthorized USB storage peripherals, data exfiltration, and automated drive-by payloads.

USB Shield Pro operates through two distinct, complementary enforcement models:

1. **Mode A: System-Wide Policy & Driver Lockdown:** Proactive restriction combining driver-layer enumeration disabling (`USBSTOR\Start = 4`) and Windows Group Policy boundaries (`RemovableStorageDevices\Deny_All`). This blocks standard USB mass storage and removable UASP devices machine-wide without allowlist exceptions (fixed-disk UASP enclosures require Mode B reactive ejection).
2. **Mode B: Reactive Allowlist Enforcement (Detect-and-Eject):** Real-time hardware identity verification (`VID`/`PID`/`Serial`) managed by the autonomous `UsbShieldService` daemon (`Start = 3`). Approved peripherals remain mounted, while unapproved devices (including fixed-disk UASP enclosures) are immediately isolated and ejected via synchronized multi-partition dismounting upon connection.

---

## Threat Model & Architecture Scope

### Protected Attack Vectors
- **Unauthorized Removable Storage Peripherals:** Enforces default-deny isolation against unapproved USB flash drives, external HDDs, and external SSDs. In Mode A, standard `USBSTOR` and removable `UASP` devices are blocked pre-emptively; in Mode B, any non-allowlisted peripheral (including fixed-disk UASP enclosures) is reactively isolated and safely ejected upon connection.
- **Storage-Based Data Exfiltration:** Restricts file writes via Windows Group Policy (`StorageDevicePolicies\WriteProtect = 1` and `RemovableStorageDevices\Deny_Write`), preventing unauthorized data theft while optionally preserving read-only file inspection.
- **Smartphone & Media Device Transfer Channels (WPD / MTP):** Eliminates secondary MTP and PTP file transfer bypass channels utilized by smartphones, portable digital cameras, and media players via `WPD Devices\Deny_Read` and `Deny_Write`.
- **Automated Drive-by Payloads (AutoRun / AutoPlay):** Suppresses AutoRun and AutoPlay execution across all removable media types (`NoDriveTypeAutoRun = 0xFF`), eliminating automated payload triggers upon device insertion.

### Enforcement Modes & Technical Interactions
- **System-Wide Driver Hard Block (`USBSTOR\Start = 4`):** Disables storage device enumeration at the Windows driver initialization layer for standard `usbstor.sys`. When active, this operates as a **System-Wide Lockdown** where all standard USB mass storage devices—including those on the allowlist—are blocked from enumerating.
- **UASP vs. USBSTOR Driver Boundary:** Modern high-speed USB 3.x and external NVMe storage enclosures frequently bind to the USB Attached SCSI Protocol driver (`uaspstor.sys`) rather than `usbstor.sys`. While `Driver Start=4` directly targets standard `USBSTOR`, removable UASP peripherals are restricted via `RemovableStorageDevices\Deny_All`. UASP enclosures that report to Windows as fixed disks bypass removable media policies and are governed exclusively through `UsbShieldService`'s real-time devnode detection and reactive dismount/ejection engine (`FSCTL_LOCK_VOLUME` &rarr; `FSCTL_DISMOUNT_VOLUME` &rarr; `CM_Request_Device_EjectW`) in Mode B.
- **Lockdown Modes vs. Allowlist Enforcement Mode:** System-wide blocking policies (`USBSTOR\Start = 4` or `RemovableStorageDevices\Deny_All`) enforce a global, blanket restriction across the entire workstation without device-level exceptions. Conversely, **Allowlist Enforcement Mode** operates when storage drivers remain interactive (`Start = 3`): the background daemon monitors bus arrivals, verifies hardware identities (`VID`/`PID`/`Serial`), permits trusted hardware to stay mounted, and programmatically ejects untrusted devices immediately upon connection.
- **Reactive Ejection & The Enumeration Window:** In interactive Allowlist Enforcement Mode, an untrusted peripheral undergoes a brief hardware enumeration window at the Windows PnP subsystem layer before `UsbShieldService` captures the arrival event, validates identity, and triggers isolation. This window is defensively mitigated by:
  1. Completely disabling AutoRun/AutoPlay (`NoDriveTypeAutoRun = 0xFF`) to prevent automatic code execution upon arrival.
  2. Sequential Volume Lock and Forceful Dismount Fallback: The ejection engine first attempts an exclusive volume lock via `FSCTL_LOCK_VOLUME` (with up to 3 consecutive retries). If locking fails due to open handles from active background processes (such as Windows Search indexing or antivirus scanning), the engine deterministically falls back to `FSCTL_DISMOUNT_VOLUME`, forcefully invalidating all open handles, and immediately issues `IOCTL_STORAGE_MEDIA_REMOVAL (false)` and `IOCTL_STORAGE_EJECT_MEDIA` on that open handle prior to closing it and invoking `CM_Request_Device_EjectW`. In the event of an initial lock failure, a brief sub-second race window exists where persistent host processes or filter drivers could attempt to re-open the volume before the PnP subsystem completes devnode teardown.
- **Administrative & State Cryptography:** Cryptographic algorithms are strictly dedicated to administrative authentication, state integrity, and disaster recovery: master passwords are keyed using PBKDF2 with HMAC-SHA256, local configuration data is encrypted with Windows DPAPI, and offline emergency recovery utilizes 128-bit cryptographic key representations.

### Known Limitations & Out-of-Scope Vectors
- **User-Mode Architecture:** USB Shield Pro operates as a user-mode Windows service (`UsbShieldService`) and registry/PnP policy coordinator, **not** as a kernel-mode filesystem minifilter driver.
- **Mount-Time Filesystem Parsing:** In reactive allowlist mode, Windows briefly mounts and parses filesystem headers and metadata (directory structures, volume labels, indexing caches) before reactive ejection completes. This mode substantially mitigates interactive file copying and casual data exfiltration, though pre-existing automated host processes could still attempt rapid reads during the sub-second enumeration window before ejection completes. Furthermore, it does not defend against transient kernel-level filesystem driver parsing vulnerabilities triggered instantaneously upon mount.
- **UASP Fixed-Disk Enclosures:** Certain high-speed USB 3.x and external NVMe enclosures report to Windows as "Fixed Disks" (Basic Disks) rather than "Removable Media". These enclosures can bypass registry-based `RemovableStorageDevices` policies and rely strictly on `UsbShieldService`'s devnode detection and reactive dismount/ejection engine.
- **Blank or Non-Unique Hardware Serial Numbers:** Low-cost, counterfeit, or promotional USB flash drives occasionally possess blank or duplicate factory serial numbers. For devices lacking a manufacturer serial, Windows generates a port-derived instance ID tied to the physical USB root port/hub location. USB Shield Pro uses this combined identity profile (`VID` + `PID` + instance path); consequently, moving that specific flash drive to a different physical USB port will change its instance path and require re-enrolling/approving the device on the new port.
- **Tamper Resistance & Local Administrator Boundary:** Administrative password protection (`PBKDF2 + DPAPI`) and registry ACL restrictions protect against standard, unprivileged users (`Standard Users`) and unauthorized modifications in unlocked console sessions. However, within the Windows security architecture, any user with elevated local administrator privileges (`Local Administrator / SYSTEM`) possesses the inherent architectural authority to terminate services, modify registry permissions, or bypass user-mode controls.
- **Enterprise Policy Coexistence (Active Directory GPO / Microsoft Intune):** USB Shield Pro configures and manages Local Machine Policies (`HKLM`). In managed enterprise domains, centralized Domain Group Policies (GPO) or Microsoft Intune profiles configured to enforce conflicting storage policies may overwrite local registry settings during periodic background policy refresh cycles (`gpupdate /force`).
- **Human Interface Devices (HID / BadUSB Injections):** Malicious hardware implants that present themselves as keyboard or mouse controllers (such as Rubber Ducky or BadUSB microcontrollers) load HID drivers rather than `USBSTOR` and are strictly outside the operational scope of storage controllers. Mitigating rogue HID devices requires physical USB port locks or bus-level device class filters.
- **Hardware Identifier Spoofing:** Allowlisting is based on vendor-reported hardware descriptors (`VID`, `PID`, and serial number). Hardware microcontrollers specifically programmed to duplicate the exact descriptors and serial string of an enrolled drive cannot be distinguished at the user-mode bus query layer without hardware-level cryptographic attestation.
- **Automated CI Test Harness Scope:** The automated CI test suite running on GitHub Actions (32 suites / 391 assertions) validates rules engine logic, atomic registry transaction safety, state journal rollback consistency, installer packaging, and Disk 0 safety invariants programmatically (Unit & Integration Logic). It does not emulate physical USB hardware bus signaling on virtualized cloud runner instances.

---

## Key Features

- **Hardware Identifier Allowlisting (VID/PID/Serial):** Grant access exclusively to approved flash drives and external disks by matching hardware serial numbers, vendor IDs, and product IDs.
- **Multi-Partition Safe Eject:** Engineered to reliably dismount and eject partitioned drives (e.g., Ventoy multi-boot drives, EFI system partitions, Windows To Go) without `PNP_VetoDevice` errors.
- **Native Win32 SCM API Integration (Zero Subprocess Spawning):** Clean execution with zero script generation (`.bat`, `.cmd`, `.ps1`). Service lifecycle operations run directly through native Win32 `advapi32.dll` APIs (`OpenSCManagerW`, `CreateServiceW`, `OpenServiceW`, `DeleteService`) without spawning external subprocesses (`sc.exe` / `cmd.exe`).
- **Autonomous Windows Service:** 24/7 background enforcement daemon (`UsbShieldService`) that monitors hardware arrival events and applies security boundaries even before a user logs in.
- **Fail-Closed Safety Invariant:** Physical Disk 0, Windows boot drives, and active paging file volumes are hard-coded as protected system boundaries that can never be locked or ejected.
- **Disaster Recovery Baseline:** Atomic state recording captures the exact pre-Shield system configuration, enabling instant rollback via GUI or CLI (`--restore-defaults`).
- **Bilingual High-DPI Interface:** Native English (LTR) and Arabic (RTL) desktop UI with complete Right-to-Left layout mirroring and full PerMonitorV2 High-DPI support (100% to 200%).

---

## User Interface

<p align="center">
  <img src="Assets/Screenshots/dashboard-dark-en.png" alt="USB Shield Pro Dashboard - Dark Theme" width="49%">
  <img src="Assets/Screenshots/dashboard-dark-ar.png" alt="USB Shield Pro Dashboard - Arabic RTL" width="49%">
</p>
<p align="center">
  <img src="Assets/Screenshots/devices-dark-en.png" alt="Connected USB Peripherals & Multi-Partition Dismount" width="49%">
  <img src="Assets/Screenshots/trusted-devices.png" alt="Trusted Devices Hardware Allowlist" width="49%">
</p>
<p align="center">
  <img src="Assets/Screenshots/protection-policies.png" alt="Protection Policies & Storage Restrictions" width="49%">
  <img src="Assets/Screenshots/settings-dark-en.png" alt="Settings & Developer Attribution" width="49%">
</p>

---

## Technical Architecture

```mermaid
flowchart TD
    subgraph ModeA ["Mode A: System-Wide Policy & Driver Lockdown"]
        A1["USB Storage Connected"] --> A2{"Driver / Policy State"}
        A2 -- "USBSTOR Start = 4" --> A3["Driver Enumeration Disabled at Kernel Layer"]
        A2 -- "RemovableStorage Deny_All" --> A4["Windows Group Policy Blocks All Storage Access"]
        A3 --> A5["USBSTOR & Removable UASP Blocked Without Allowlist<br>(Fixed UASP Requires Mode B Ejection)"]
        A4 --> A5
    end

    subgraph ModeB ["Mode B: Reactive Allowlist Mode (Detect & Eject - Start=3)"]
        B1["USB Storage Connected"] --> B2["PnP Subsystem Event (WM_DEVICECHANGE)"]
        B2 --> B3{"System Safety Floor Guard"}
        B3 -- "System Disk / Disk 0 / Boot" --> B4["Hard Floor Pass: Protected Machine Volume"]
        B3 -- "Removable / External Peripheral" --> B5["Hardware Identity Resolver (VID / PID / Serial)"]
        B5 --> B6{"RuleEngine Allowlist Match"}
        B6 -- "Trusted (Allowlisted)" --> B7["Permit Read / Write File Access"]
        B6 -- "Untrusted (Non-Allowlisted)" --> B8["Sequential Volume Lock & Dismount (FSCTL)"]
        B8 --> B9["Native CM_Request_Device_EjectW"]
        B9 --> B10["Device Safely Ejected Until Re-plugged"]
    end
```

---

## Core Security Capabilities

| Capability | Mechanism | Covers UASP (`uaspstor.sys`)? | Allows Trusted Drives? | Security Value |
| :--- | :--- | :--- | :--- | :--- |
| **Driver Hard Lock** | `USBSTOR\Start = 4` | **No** *(usbstor.sys only)* | **No** *(blocks all USBSTOR)* | Disables USB storage enumeration at the driver layer machine-wide. |
| **Removable Storage Deny** | `RemovableStorageDevices\Deny_All` | **Partial** *(covers removable UASP; fixed enclosures require service eject)* | **No** *(blocks all removable)* | Enforces Windows Group Policy to block read/write access to removable drives. |
| **Reactive Allowlist Service** | Native Win32 Service (`UsbShieldService`) | **Yes** *(volume & devnode eject)* | **Yes** *(VID/PID/Serial match)* | Proactive real-time background monitoring, allowlist enforcement, and instant safe ejection. |
| **Write Protection Mode** | `StorageDevicePolicies\WriteProtect = 1` | **Partial** *(Removable media only; fixed-disk UASP enclosures ignore removable policy)* | **No** *(enforces read-only globally)* | Permits read-only inspection while blocking malware infection and data exfiltration. |
| **Portable Device Guard (WPD)** | `WPD Devices\Deny_Read / Deny_Write` | **N/A** *(covers MTP/PTP)* | **No** *(blocks all WPD media)* | Closes secondary file transfer channels used by smartphones and media players. |
| **PnP Installation Control** | `DeviceInstall\Restrictions\DenyRemovableDevices` | **Partial** *(removable storage)* | **No** *(blocks driver staging)* | Prevents staging and installation of removable storage device drivers. |
| **AutoPlay Neutralization** | `NoDriveTypeAutoRun = 0xFF` | **Yes** *(all media types)* | **N/A** *(system-wide policy)* | Disables AutoRun / AutoPlay execution across all removable media. |
| **Multi-Partition Safe Eject** | `FSCTL_LOCK` &rarr; `FSCTL_DISMOUNT` &rarr; `CM_Request_Device_EjectW` | **Yes** *(targets all volume devnodes)* | **Yes** *(executes on untrusted)* | Synchronously unmounts all dependent volumes, preventing data corruption. |
| **Fail-Closed System Floor** | Disk 0 / OS Root / Paging Guard | **Yes** *(hard floor check)* | **Yes** *(mandatory pass)* | Prevents accidental ejection of system disks and host OS boot partitions. |
| **Cryptographic Protection** | PBKDF2 (HMAC-SHA256) + DPAPI | **N/A** *(state security)* | **N/A** *(administrative auth)* | Guards administrative UI authentication, policy modifications, and 128-bit master recovery keys. |

---

## Verified Hardware & Behavior Matrix

Empirical behavior observed and verified across standard Windows 10 (v1903+) and Windows 11 x64 workstation configurations:

| Hardware Category | Mode A Behavior (System Lockdown) | Mode B Behavior (Reactive Allowlist) | Fail-Safe & Driver Semantics |
| :--- | :--- | :--- | :--- |
| **Internal NVMe Boot Drive**<br>*(Disk 0 / Host OS `C:`)* | **Bypassed / Immune**<br>System drive remains fully active and unrestricted. | **Bypassed / Immune**<br>Permanent kernel floor bypass; protected from locking or dismounting. | Hard-coded `SystemDeviceSafetyGuard` invariant prevents accidental locking or ejection. |
| **Internal SATA / AHCI / RAID Fixed Disk**<br>*(Non-OS internal secondary storage)* | **Bypassed / Immune**<br>Internal storage bus and disks remain fully accessible. | **Bypassed / Immune**<br>Positive USB-bus evidence invariant strictly protects internal drives from ejection. | Hardware bus query verifies non-USB ancestry (`PositiveUsbEvidenceInvariant`). |
| **Standard USB 2.0 / 3.0 Flash Drive**<br>*(Standard `USBSTOR`)* | **Fully Blocked**<br>Driver enumeration stopped (`Start = 4`) or blocked via `Deny_All`. | **Enforced via Allowlist**<br>Allowlisted drives remain mounted; unapproved drives instantly ejected. | Evaluated via `VID`/`PID`/`Serial` within the sub-second PnP arrival window. |
| **Multi-Partition Bootable USB**<br>*(e.g., Ventoy EFI + ExFAT data)* | **Fully Blocked**<br>All partitions blocked from loading at the bus/policy layer. | **Enforced via Allowlist**<br>All sibling partitions (e.g., `D:`, `F:`) sequentially locked and unmounted in parallel. | Prevents `PNP_VetoDevice` errors through synchronized multi-volume dismounting. |
| **External USB 3.x / NVMe UASP Enclosure**<br>*(Fixed-Disk Reporting)* | **Bypasses Start=4 / Deny_All**<br>Bypasses standalone USBSTOR and removable media policy if reported as fixed. | **Enforced via Allowlist**<br>Captured by `UsbShieldService` via PnP devnode discovery and reactively dismounted/ejected. | Governed through service-level devnode teardown rather than passive group policy. |
| **Smartphones & Digital Cameras**<br>*(MTP / PTP over WPD)* | **File Transfers Blocked**<br>Device connection permitted for charging, but file browsing is blocked via WPD policy. | **File Transfers Blocked**<br>Governed by active `WPD Devices\Deny_Read` and `Deny_Write` policy boundaries. | Eliminates storage bypass vector without interfering with USB power negotiation. |

---

## Downloads

| Package | Format | Size | Download Link | Integrity & Antivirus |
| :--- | :--- | :--- | :--- | :--- |
| **Native Installer** | `.exe` | `1,311,744 bytes` | [Download Setup.exe](https://github.com/0x-Abood/USB-Shield-Pro/releases/download/v2.1.1/USB-Shield-Pro-v2.1.1-Setup.exe) | Verified SHA-256 Manifest |
| **Portable Archive** | `.zip` | `403,139 bytes` | [Download Portable.zip](https://github.com/0x-Abood/USB-Shield-Pro/releases/download/v2.1.1/USB-Shield-Pro-v2.1.1-Portable.zip) | Verified SHA-256 Manifest |
| **SHA-256 Manifest** | `.txt` | `817 bytes` | [Download Checksums](https://github.com/0x-Abood/USB-Shield-Pro/releases/download/v2.1.1/USB-Shield-Pro-v2.1.1-SHA256.txt) | Verified via PowerShell `Get-FileHash` |

---

## Command-Line Interface (CLI)

```cmd
:: Launch administrative desktop GUI (default)
UsbShieldPro.exe

:: Run hardware bus discovery diagnostic
UsbShieldPro.exe --diag-detect

:: Run as background Windows service daemon
UsbShieldPro.exe --service

:: Emergency rollback to pre-Shield baseline configuration
UsbShieldPro.exe --restore-defaults

:: Exact restoration from write-ahead state journal
UsbShieldPro.exe --restore-exact

:: Authenticated secure uninstallation with policy rollback
UsbShieldPro.exe --uninstall
```

---

## Update Notification & In-Place Upgrades

USB Shield Pro includes a passive, non-intrusive update notification engine designed to keep workstations current without compromising security or executing background payloads:

- **GUI-Only Read-Only Metadata Inspection:** Update checks are performed exclusively within the graphical management interface via a single HTTPS GET request (`TLS 1.2`) to the official GitHub API (`https://api.github.com/repos/0x-Abood/USB-Shield-Pro/releases/latest`).
- **Zero-Dropper / AV-Safe Invariant:** The application never downloads, writes, or executes binary payloads in the background. It solely alerts the administrator through an in-app banner and directs them to the official GitHub release page.
- **Strict Domain Whitelist:** External links strictly validate the target destination against `https://github.com/0x-Abood/USB-Shield-Pro/` before opening the system's default browser.
- **Strict Service Network Isolation:** The background Windows Service daemon (`UsbShieldService` / `--service`) is 100% network-isolated and never initiates any network sockets.
- **Air-Gapped Workstations & Throttling:** Automatic checks occur at most once every 12 hours after a 2.5-second non-blocking startup window. Administrators of air-gapped networks can permanently disable all update queries under **Settings &rarr; Application Behavior &rarr; Check for updates automatically**.
- **In-Place Upgrade Fidelity:** Upgrading via `USB-Shield-Pro-v2.1.1-Setup.exe` or replacing the standalone binary preserves all protection configurations, trusted device enrollments, and pre-Shield recovery baselines under `%ProgramData%\USBShieldPro` with 100% backward compatibility.

---

## Official Release Checksums (SHA-256)

All production binaries are clean-room compiled (`/optimize+ /debug-`) without local machine paths, debug symbols, or external script dependencies.

### v2.1.1 (Current Release)

| File Name | Target Type | Size (Bytes) | Official SHA-256 Checksum |
| :--- | :--- | :--- | :--- |
| **`USB-Shield-Pro-v2.1.1-Setup.exe`** | Native Installer | `1,312,256` | `F0F47E5C3ECA7CA72549FE4890D8A095D2E07299072F5C1C6345AA3FBBBFA795` |
| **`USB-Shield-Pro-v2.1.1-Portable.zip`** | Portable Archive | `400,432` | `C1B9854AD498AC2CD1414C03C597527ABAE7A447DC0167737F23163A43AB2732` |
| **`UsbShieldPro.exe`** | Standalone Executable | `1,107,968` | `781A0F63E5529691E0D94D8663DC7E081C37D7B28383674DF6C99E74097E6C87` |
| **`UsbShieldPro.exe.config`** | High-DPI Manifest | `598` | `53840DABDB7510AC21965024AEC4797025C920C965D29BBAA46FFA324338C5B1` |
| **`README.txt`** | Release Notes | `7,007` | `21E5B2F4E286BCC1E49B4F7529DA4ACDD352A21628F368501B8CD9F17021B6E3` |

### v2.1.0 (Previous Gold Release)

| File Name | Target Type | Size (Bytes) | Official SHA-256 Checksum |
| :--- | :--- | :--- | :--- |
| **`USB-Shield-Pro-v2.1.0-Setup.exe`** | Native Installer | `1,253,376` | `8D675C4CA6B26D8E29F9EBB2F9B4431DF7D347E2C476B6C090DAD84B6619872D` |
| **`USB-Shield-Pro-v2.1.0-Portable.zip`** | Portable Archive | `387,108` | `8D991350ABCE42915E8C0B87F49017A4A347A9212592AF778090EB7C89A29F6B` |
| **`UsbShieldPro.exe`** | Standalone Executable | `1,076,736` | `C0EDC176CCAD5CB490933F8FCE8D94141D67E31B49E6A80DCAF88E772ACE8311` |

### Integrity Verification (PowerShell)

> [!NOTE]
> Official binary packages are currently distributed as an **unsigned open-source release** without a commercial Authenticode code-signing certificate. Users and system administrators are strongly encouraged to verify downloaded package integrity against the official published SHA-256 checksums before installation.

Verify the integrity of downloaded binaries directly using PowerShell's built-in `Get-FileHash` cmdlet:

```powershell
# Verify the Native Installer
Get-FileHash .\USB-Shield-Pro-v2.1.1-Setup.exe -Algorithm SHA256

# Verify the Portable Archive
Get-FileHash .\USB-Shield-Pro-v2.1.1-Portable.zip -Algorithm SHA256
```

---

## Building from Source

### System Requirements
- Windows 10 (v1903+) or Windows 11 (64-bit)
- Microsoft .NET Framework 4.8 Developer Pack or Runtime (`csc.exe` in `%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319`)

### Build Steps
1. Clone the repository:
   ```cmd
   git clone https://github.com/0x-Abood/USB-Shield-Pro.git
   cd USB-Shield-Pro
   ```
2. Build the core executable and regression test harness:
   ```cmd
   build.bat
   ```
3. Build the self-contained native installer:
   ```cmd
   Installer\build_installer.bat
   ```
4. Run the regression test suite (32 Suites, 391 Assertions):
   ```cmd
   TestRunner.exe --test-harness
   ```

---

## License & Attribution

Distributed under the **MIT License**. See [`LICENSE`](LICENSE) for complete details.

Developed with &hearts; by **0xAbood**  
Official Repository: [https://github.com/0x-Abood/USB-Shield-Pro](https://github.com/0x-Abood/USB-Shield-Pro)
