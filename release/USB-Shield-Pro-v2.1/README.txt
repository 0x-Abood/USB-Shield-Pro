================================================================================
  USB Shield Pro v2.1.0 (Official Gold Release)
  (c) 2026 0xAbood. All rights reserved.
================================================================================

1. SYSTEM REQUIREMENTS
--------------------------------------------------------------------------------
- Operating System: Windows 10 / Windows 11 x64 (64-bit)
- Runtime: Microsoft .NET Framework 4.8 (built-in on Windows 10 v1903+ and Windows 11)
- Privileges: Administrator privileges required (enforced via application manifest)

2. PRODUCT OVERVIEW & SCOPE
--------------------------------------------------------------------------------
USB Shield Pro provides endpoint removable storage policy enforcement, physical
USB port monitoring, device authorization (whitelisting), and reactive safe-eject
lifecycle control for Windows workstations.

NOTICE: USB Shield Pro is an administrative endpoint access control utility.
It is NOT an antivirus or anti-malware solution, and does not perform virus
signature scanning or malicious payload analysis.

3. INTERFACE & WORKSPACE FEATURES
--------------------------------------------------------------------------------
- Production Enterprise Desktop UI/UX:
  * Restrained, cohesive enterprise desktop ergonomics across all surfaces
  * High-DPI support with automatic PerMonitorV2 scaling (100% to 200%)
  * Fully responsive window architecture verified from 1024x640 up to 2560x1440
  * Zero-flicker double-buffered GDI+ rendering engine
- Authoritative Theme Architecture (Selected exclusively through Settings):
  * Charcoal Red (signature dark theme with balanced 90% neutral palette)
  * Midnight (deep dark workspace theme)
  * Graphite (neutral dark workstation theme)
  * Porcelain (signature high-contrast light theme with ClearType optimization)
  * Cloud (soft balanced light theme)
  * Follow Windows (dynamic automatic light/dark switching matching Windows system theme)
  * Top-bar theme switcher removed; Settings serves as sole theme selector
- Native Arabic RTL & LTR Technical Precision:
  * Comprehensive right-to-left layout mirroring and bidirectional typography
  * Strict left-to-right alignment preserved for technical keys, paths, and IDs
  * Zero text or glyph bottom clipping across all pages and dialogs
  * Device Details non-modal responsive drawer with correct RTL orientation
  * Approved overflow handling with clean ellipsis and hover tooltips
  * Full-value clipboard copy affordances for device identifiers
- Dedicated 7-Page Workspace Suite:
  * Dashboard: Real-time system posture, summary metrics, quick policy toggles,
    attention banner with structural text/action reservation, and recent activity
  * Connected Devices: Live PnP device discovery with technical details drawer
  * Protection: Structured policy cards, clear status badges, and collapsible
    Protection accordion hierarchy
  * Trusted Devices: Comprehensive whitelist management with serial verification,
    hardware token tracking, neutral structural headings, and clear badge levels
  * Activity: Granular audit logging with category filtering, search, and details
  * Recovery: Authoritative pre-Shield posture card, non-destructive disaster
    rollback, and technical scope
  * Settings: Synchronized appearance preferences, background service management,
    and application password security
- Official Brand Identity & Application Branding:
  * Official USB Shield Pro application identity and brand mark
  * Multi-resolution Windows application icon (16x16 through 256x256)
  * Native window titlebar, taskbar, Alt+Tab, and notification tray integration
  * Settings -> About card official brand mark with pixel-perfect rendering

4. PRODUCTION RELEASE HARDENING
--------------------------------------------------------------------------------
- Clean Production Binary:
  * Complete exclusion of test harnesses, mock data, and capture runners via #if !PRODUCTION guards
  * Zero hardcoded development paths or machine-specific environment switches
  * Standard application runtime paths under %ProgramData%\USBShieldPro
  * Strict WinExe target with requireAdministrator manifest and no console window

5. IMPORTANT SECURITY & BEHAVIORAL ARCHITECTURE
--------------------------------------------------------------------------------
- Persistent Windows Policies: Static Windows registry policies (Removable Storage
  Deny_All, Write-Protect, AutoPlay Guard) remain enforced at the operating system
  level even after the GUI application is closed.
- Reactive Monitoring & Auto-Eject Enforcement: Reactive monitoring and Auto-Eject
  require an active enforcer process. When the background Windows Service is running,
  the service owns reactive enforcement. Otherwise, GUI fallback monitoring operates
  while the GUI application is running.
- Auto-Eject Semantics: Auto-Eject provides reactive safe removal (via Windows PnP
  CM_Request_Device_EjectW) upon unauthorized device insertion; it is not kernel-level
  pre-access blocking.
- Trusted Device Behavior: Whitelist evaluation follows strict RuleEngine and Sentinel
  semantics based on device hardware ID, vendor ID, product ID, and serial number.
- Internal System Disks Safeguard: NVMe and SATA fixed internal storage disks,
  operating system boot partitions (C:), page files, and input peripherals (keyboards
  and mice) are permanently excluded and safeguarded at Gate 0 against ejection
  or blocking.
- Disaster Recovery Baseline: Recovery restores the exact pre-Shield Windows state
  recorded at first launch; absent registry keys remain absent on rollback, and no
  synthetic defaults are invented.
- Application Password Recovery Key: The application Recovery Key recovers the
  application administrative password only; it does not alter system policy state.

6. DISASTER RECOVERY & MANUAL ROLLBACK
--------------------------------------------------------------------------------
USB Shield Pro maintains an atomic write-ahead journal of all Windows registry
modifications under %ProgramData%\USBShieldPro\journal.json.

If emergency rollback is required without the GUI interface:
Run from an elevated command prompt:
UsbShieldPro.exe --restore-defaults
This restores the exact pre-Shield baseline configuration recorded in journal.json.

7. COMMAND-LINE USAGE
--------------------------------------------------------------------------------
- UsbShieldPro.exe                  : Interactive desktop management interface
- UsbShieldPro.exe --diag-detect     : Non-destructive CLI device inspection & audit
- UsbShieldPro.exe --diag-security   : Security ACL and file permissions audit
- UsbShieldPro.exe --restore-defaults: Command-line rollback to pre-Shield baseline
- UsbShieldPro.exe --restore-exact   : Command-line rollback to pre-Shield baseline
- UsbShieldPro.exe --service         : Background Windows Service mode
================================================================================
