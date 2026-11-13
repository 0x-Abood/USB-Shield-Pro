# Changelog

All notable changes to **USB Shield Pro** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [2.1.1] - 2026-11-13

### Security & Reliability
- **Internal Fixed Storage Device Safety Guard**:
  - Enforced strict positive USB-bus transport verification (`PositiveUsbEvidenceInvariant`).
  - Added multi-layer validation ensuring internal SATA/AHCI, RAID/RST, SCSI, and NVMe fixed disks are immune to auto-ejection, policy lockouts, or untrusted device classification even when ambiguous vendor strings or removable bits are reported.
- **DPAPI & Storage Security Architecture**:
  - Standardized credential and state isolation on Windows DPAPI LocalMachine with strict NTFS ACL enforcement (`SecurityAclHelper`), isolating `%ProgramData%\USBShieldPro` to `SYSTEM` and `Administrators`.
  - Added atomic verifier replacement with `.auth.bak` and `.recovery.bak` fallback mechanisms protecting against sudden power-loss corruption.
- **WPD / MTP Non-USB Transport Filtering**:
  - Validated hardware ancestry and transport boundaries for Windows Portable Devices (`WPD`), rejecting ambiguous or networked/Bluetooth media devices from raw PnP storage ejection routines.

### Activity Logging & Monitoring
- **UTC-Normalized Timestamp Pipeline**:
  - Eliminated the 3-hour timestamp offset in UTC+3 (Arabia Standard Time) and other local timezones by introducing an unambiguous ISO-8601 UTC contract (`TimestampIso`).
  - Formatted presentation timestamps strictly at the rendering boundary via `.LocalTimestamp` and `GetPresentationTimestamp(TimeZoneInfo)`.
  - Updated calendar date filters and grouping to evaluate against the user's local day boundaries.
- **Dynamic Multi-Language Activity Localization**:
  - Decoupled persisted event records from write-time UI language.
  - Introduced a language-neutral schema with stable programmatic event codes (`EventCode`, `BadgeKey`, `ContextKey`, `DetailParameter`).
  - Implemented dynamic on-the-fly string resolution (`GetLocalizedTitle()`, `GetLocalizedBadge()`, `GetLocalizedContext()`) that updates immediately upon toggling language between English and Arabic, while safely preserving historical legacy records.
- **Full Physical PnP Lifecycle Event Coverage**:
  - Added pre-enforcement physical arrival detection logging (`DEVICE_DETECTED`) before policy evaluation or reactive auto-ejection.
  - Ensured reliable recording of physical insertions, disconnections, multi-partition volume arrivals, and service-originated background events.
  - Eliminated duplicate event emissions across concurrent GUI and background service producers.

### User Interface & Experience
- **Flicker-Free Language & Layout Transitions**:
  - Implemented Win32 `WM_SETREDRAW` freeze and atomic `RedrawWindow` invalidation during runtime language toggles, completely eliminating transient black blank canvases and visual jitter during Right-to-Left (RTL) handle recreation.
- **Responsive Password & Recovery Dialog**:
  - Redesigned `PasswordDialog` with dynamic word-wrapping measurement (`TextRenderer.MeasureText`) and automatic height expansion on validation errors.
  - Aligned RTL button placement (Confirm on the right, Cancel on the left) with theme-aware styling.

### Installation & Uninstallation
- **Authenticated Secure Uninstaller**:
  - Added `UsbShieldPro.exe --uninstall` command-line workflow and integrated with Windows Installed Apps / Programs and Features (`SetupProgram.cs`).
  - Enforced administrative boundary verification and password/recovery key authentication before proceeding with uninstallation.
  - Guaranteed exact rollback of Windows storage policies to pre-Shield baselines via `RecoveryManager.RestorePreShieldState()`.
  - Automated safe shutdown and deregistration of `UsbShieldService` via native Win32 Service Control Manager APIs.
  - Added user prompt for optional retention or purging of application data.

### Verification & Automated Testing
- **Test Harness Expansion (Test 38)**:
  - Added permanent regression suite covering UTC/AST/MST timestamp conversions, dynamic RTL/LTR localization, responsive password dialog geometry, authenticated uninstallation gates, and codebase-wide version uniformity.
  - Suite now comprises **32 test suites** and **391 passing assertions** with 100% pass rate.

---

## [2.1.0] - 2026-11-02

### Added
- **Clean-Room Open-Source Release**: Complete architectural redesign of USB Shield Pro for Windows 10 and Windows 11 x64.
- **Dual Enforcement Models**:
  - Mode A: System-Wide Policy & Driver Lockdown (`USBSTOR\Start = 4` and Group Policy restrictions).
  - Mode B: Reactive Allowlist Enforcement (`UsbShieldService` detect-and-eject daemon).
- **Safe Passive GitHub Update Notifier**: Read-only GitHub API release checking with zero background binary downloads or executions.
- **Disaster Recovery Engine**: Write-ahead state journaling with autonomous `.bat` and `.reg` rollback scripts.
- **Bilingual Interface**: Native English and Arabic desktop interface with full PerMonitorV2 High-DPI support (100% to 200%).
