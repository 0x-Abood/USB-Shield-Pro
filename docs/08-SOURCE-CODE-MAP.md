# USB Shield Pro v2.0 — Source Code Map

This document maps all **44 production C# source files** compiled into USB Shield Pro v2.0.

---

## 1. High-Risk Critical Files Summary

The following files represent the core safety and security foundation. **Extreme caution** must be exercised when inspecting or editing these components:

| File | Risk Level | Primary Reason for High Risk |
|---|:---:|---|
| [`Core/SystemDeviceSafetyGuard.cs`](../Core/SystemDeviceSafetyGuard.cs) | **CRITICAL** | Protects Disk 0 and host boot partitions. A defect here can cause an immediate system BSOD. |
| [`Devices/PnpSafeEjectBackend.cs`](../Devices/PnpSafeEjectBackend.cs) | **CRITICAL** | Executes native kernel PnP ejection via `CM_Request_Device_EjectW`. Correlates volume devnodes. |
| [`Core/DeviceEnforcementLifecycle.cs`](../Core/DeviceEnforcementLifecycle.cs) | **HIGH** | Manages generation leases, physical device keys, and concurrent state machine transitions. |
| [`Devices/ActiveSentinel.cs`](../Devices/ActiveSentinel.cs) | **HIGH** | Controls Attempt 1/Attempt 2 bounded retry timing (~10s) and notification dispatching. |
| [`Windows/RegistryTransactionHelper.cs`](../Windows/RegistryTransactionHelper.cs) | **HIGH** | Manages atomic write-ahead registry operations and baseline recording. Prevents system corruption. |
| [`Core/RecoveryManager.cs`](../Core/RecoveryManager.cs) | **HIGH** | Generates standalone offline `.bat` and `.reg` rollback scripts from journaled baselines. |
| [`Devices/DeviceDiscovery.cs`](../Devices/DeviceDiscovery.cs) | **MEDIUM** | Enumerates SetupAPI and WMI hardware; resolves physical disk numbers and USB ancestry. |

---

## 2. Source File Inventory by Layer

### 2.1 Root Directory (Presentation & Entry Point)

#### 1. [`UsbBlocker.cs`](../UsbBlocker.cs)
- **Primary Classes**: `UsbShieldPro.Program`
- **Purpose**: Main executable entry point (`Main`). Handles CLI switch parsing, elevated security checks, single-instance global mutex, and GUI / Service / TestRunner routing.
- **Callers**: Windows OS (process creation).
- **Dependencies**: `SystemDeviceSafetyGuard`, `SecurityAclHelper`, `SettingsRepository`, `MainForm`, `UsbShieldService`.
- **Safety Relevance**: High. Manifest specifies `requireAdministrator`. Ensures single-instance mutex and ACL enforcement on startup.
- **Safe to Modify?**: Low risk for adding CLI flags; high risk for mutex or startup logic.
- **Regression Area**: CLI modes (`--diag-detect`, `--restore-exact`, `--test-harness`).

#### 2. [`Localization.cs`](../Localization.cs)
- **Primary Classes**: `UsbShieldPro.Loc`, `UsbShieldPro.Language`
- **Purpose**: In-memory bilingual dictionary (English and Arabic) with RTL layout metadata.
- **Callers**: `MainForm`, `UIControls`, `TrayManager`, `ActiveSentinel`.
- **Dependencies**: None.
- **Safety Relevance**: Low. Strictly presentation text.
- **Safe to Modify?**: Yes. Safe for translations or new language keys.
- **Regression Area**: UI text rendering, badge alignment in Arabic RTL mode.

#### 3. [`MainForm.cs`](../MainForm.cs)
- **Primary Classes**: `UsbShieldPro.MainForm`
- **Purpose**: Primary administrative dashboard. Coordinates tabs (Devices, Rules, Activity, Settings), UI timers, rule toggling, and tray balloons.
- **Callers**: `Program.Main`.
- **Dependencies**: `UIControls`, `ProtectionEngine`, `RuleEngine`, `ActiveSentinel`, `SettingsRepository`, `PasswordManager`.
- **Safety Relevance**: Medium. Dispatches user policy toggles and whitelisting commands.
- **Safe to Modify?**: Medium risk. UI marshaling must use `BeginInvoke` to avoid deadlocks.
- **Regression Area**: Screen captures, tab switching, card refresh, minimize-to-tray.

#### 4. [`UIControls.cs`](../UIControls.cs)
- **Primary Classes**: `DeviceCardControl`, `RuleToggleCard`, `ModernButton`, `Theme`, `StatusBadge`
- **Purpose**: Custom GDI+ double-buffered UI components, cards, buttons, toggles, and status badges.
- **Callers**: `MainForm`.
- **Dependencies**: `Localization`, `DeviceEnforcementLifecycleManager`.
- **Safety Relevance**: Low. Pure presentation layer.
- **Safe to Modify?**: Yes. Safe for visual polish, font tuning, or DPI adjustments.
- **Regression Area**: High-DPI scaling, card rendering under dark/light themes.

#### 5. [`TrayManager.cs`](../TrayManager.cs)
- **Primary Classes**: `UsbShieldPro.TrayManager`
- **Purpose**: Manages Windows notification tray icon (`NotifyIcon`), context menu, and balloon alerts.
- **Callers**: `MainForm`.
- **Dependencies**: `Localization`.
- **Safety Relevance**: Low. System tray presentation.
- **Safe to Modify?**: Yes. Ensure `Dispose()` cleanly removes icon from notification area.
- **Regression Area**: Balloon tip display, quick-lock / quick-unlock menu actions.

#### 6. [`ScreenCaptureRunner.cs`](../ScreenCaptureRunner.cs)
- **Primary Classes**: `UsbShieldPro.ScreenCaptureRunner`
- **Purpose**: Automated native UI screenshot generator (`--capture-pass3`). Renders 12 production screens in English and Arabic for QA verification.
- **Callers**: `Program.Main` via `--capture-pass3`.
- **Dependencies**: `MainForm`, `Localization`, `SettingsRepository`.
- **Safety Relevance**: Low. QA / test tool only.
- **Safe to Modify?**: Yes.
- **Regression Area**: Visual artifact generation.

---

### 2.2 Core Layer (`Core/`)

#### 7. [`Core/DeviceEnforcementLifecycle.cs`](../Core/DeviceEnforcementLifecycle.cs)
- **Primary Classes**: `DeviceEnforcementLifecycleManager`, `DeviceLifecycleRecord`, `EnforcementState`, `PnpHelper`
- **Purpose**: State machine orchestrating per-device enforcement cycles, generation leases, and PnP parent tree walks.
- **Callers**: `ActiveSentinel`, `UIControls`.
- **Dependencies**: `Models/Enums.cs`, `PnpSafeEjectBackend`.
- **Safety Relevance**: **HIGH**. Prevents duplicate or runaway enforcement cycles.
- **Safe to Modify?**: High risk. Lock discipline (`_syncLock`) must be preserved.
- **Regression Area**: Test suites 9DP through 9DX; concurrent lease validation.

#### 8. [`Core/DiagnosticsTiming.cs`](../Core/DiagnosticsTiming.cs)
- **Primary Classes**: `LatencyTracker`, `ArrivalLatencyRecord`
- **Purpose**: High-resolution performance monitoring (`Stopwatch.GetTimestamp`) tracking arrival-to-eject elapsed times.
- **Callers**: `ActiveSentinel`, `PnpSafeEjectBackend`.
- **Dependencies**: None.
- **Safety Relevance**: Low. Diagnostic telemetry only.
- **Safe to Modify?**: Yes.
- **Regression Area**: Timing logs and benchmark CLI mode (`--diagnostic-latency`).

#### 9. [`Core/EnforcementOwnerResolver.cs`](../Core/EnforcementOwnerResolver.cs)
- **Primary Classes**: `EnforcementOwnerResolver`, `EnforcementOwnerState`
- **Purpose**: Enforces single-enforcement-owner guarantee between background Windows Service (Session 0) and interactive GUI (Session 1).
- **Callers**: `ActiveSentinel`, `MainForm`, `UsbShieldService`.
- **Dependencies**: `Service/ServiceManager.cs`.
- **Safety Relevance**: **HIGH**. Prevents simultaneous enforcement thrashing.
- **Safe to Modify?**: Medium risk.
- **Regression Area**: Service start/stop transitions.

#### 10. [`Core/ProtectionEngine.cs`](../Core/ProtectionEngine.cs)
- **Primary Classes**: `ProtectionEngine`
- **Purpose**: Coordinates high-level policy application across Windows backends and performs state reconciliation.
- **Callers**: `MainForm`, `UsbShieldService`.
- **Dependencies**: `RemovableStoragePolicyBackend`, `AutoPlayPolicyBackend`, `StateVerifier`.
- **Safety Relevance**: **HIGH**. Dispatches Windows policy writes.
- **Safe to Modify?**: Medium risk.
- **Regression Area**: Test suites 3 and 9; policy reconciliation.

#### 11. [`Core/RecoveryManager.cs`](../Core/RecoveryManager.cs)
- **Primary Classes**: `RecoveryManager`
- **Purpose**: Generates standalone `USBShield_Restore.bat` and `USBShield_Restore.reg` files from `journal.json`.
- **Callers**: `RegistryTransactionHelper`, `Program.Main` (`--restore-exact`).
- **Dependencies**: `JournalRepository`, `SecurityAclHelper`.
- **Safety Relevance**: **HIGH**. Guarantees system rollback.
- **Safe to Modify?**: High risk. Must never emit default values when original state was absent.
- **Regression Area**: Test suite 2; offline rollback execution.

#### 12. [`Core/RuleEngine.cs`](../Core/RuleEngine.cs)
- **Primary Classes**: `RuleEngine`
- **Purpose**: Evaluates candidate devices against whitelist rules and active policies.
- **Callers**: `ActiveSentinel`, `MainForm`.
- **Dependencies**: `SystemDeviceSafetyGuard`, `TrustedDeviceRepository`.
- **Safety Relevance**: **HIGH**. Decides whether a device is allowed or blocked.
- **Safe to Modify?**: Medium risk.
- **Regression Area**: Whitelist matching tiers.

#### 13. [`Core/StateVerifier.cs`](../Core/StateVerifier.cs)
- **Primary Classes**: `StateVerifier`
- **Purpose**: Audits live 64-bit Windows registry keys to verify that configured policies match reality.
- **Callers**: `ProtectionEngine`.
- **Dependencies**: `WindowsSecurityConstants`.
- **Safety Relevance**: Medium. Ensures system is not in an inconsistent state.
- **Safe to Modify?**: Yes.
- **Regression Area**: Policy verification reporting.

#### 14. [`Core/SystemDeviceSafetyGuard.cs`](../Core/SystemDeviceSafetyGuard.cs)
- **Primary Classes**: `SystemDeviceSafetyGuard`, `SafetyValidationResult`
- **Purpose**: Absolute barrier preventing operations on Disk 0, boot volumes, internal buses, and input peripherals.
- **Callers**: `RuleEngine`, `PnpSafeEjectBackend`, `ActiveSentinel`.
- **Dependencies**: `Models/DeviceIdentity.cs`.
- **Safety Relevance**: **CRITICAL**. Fail-open safety foundation.
- **Safe to Modify?**: **EXTREME RISK**. Never relax checks.
- **Regression Area**: Test suite 7.

#### 15. [`Core/TemporaryAccessManager.cs`](../Core/TemporaryAccessManager.cs)
- **Primary Classes**: `TemporaryAccessManager`, `TemporaryAccessState`
- **Purpose**: Manages timed temporary access window with persistent disk state (`temp_access.json`).
- **Callers**: `MainForm`, `ProtectionEngine`.
- **Dependencies**: `SecurityAclHelper`, `ProtectionPolicy`.
- **Safety Relevance**: Medium. Handles temporary policy relaxation and auto-relock.
- **Safe to Modify?**: Medium risk.
- **Regression Area**: Test suite 8.

#### 16. [`Core/TestHarness.cs`](../Core/TestHarness.cs)
- **Primary Classes**: `TestHarness`
- **Purpose**: In-memory test execution suite covering 147 test cases.
- **Callers**: `Program.Main` (`--test-harness`).
- **Dependencies**: Exercises entire codebase.
- **Safety Relevance**: Developer / QA only.
- **Safe to Modify?**: Yes, when adding new regression test cases.
- **Regression Area**: Lineage check against `test_results_v22.txt`.

---

### 2.3 Devices Layer (`Devices/`)

#### 17. [`Devices/ActiveSentinel.cs`](../Devices/ActiveSentinel.cs)
- **Primary Classes**: `ActiveSentinel`, `DeviceInterceptEventArgs`
- **Purpose**: Real-time arrival consumer, bounded retry orchestrator (~10s total age), and trace persistence.
- **Callers**: `MainForm`, `UsbShieldService`.
- **Dependencies**: `PnpSafeEjectBackend`, `DeviceEnforcementLifecycleManager`, `SystemDeviceSafetyGuard`.
- **Safety Relevance**: **HIGH**. Enforces max 2 CM calls and 0 legacy FSCTL dismount calls.
- **Safe to Modify?**: High risk.
- **Regression Area**: Test suite 9; production hardware validation.

#### 18. [`Devices/DeviceClassifier.cs`](../Devices/DeviceClassifier.cs)
- **Primary Classes**: `DeviceClassifier`
- **Purpose**: Differentiates flash drives, SSDs, HDDs, and WPD proxies.
- **Callers**: `DeviceDiscovery`.
- **Dependencies**: `Models/Enums.cs`.
- **Safety Relevance**: Medium.
- **Safe to Modify?**: Yes.
- **Regression Area**: Device classification accuracy.

#### 19. [`Devices/DeviceDiscovery.cs`](../Devices/DeviceDiscovery.cs)
- **Primary Classes**: `DeviceDiscovery`
- **Purpose**: Native SetupAPI device enumerator. Queries disk geometries, volumes, and instance IDs.
- **Callers**: `MainForm`, `ActiveSentinel`.
- **Dependencies**: `DeviceClassifier`, `DeviceIdentityResolver`.
- **Safety Relevance**: **HIGH**. Queries physical hardware attributes.
- **Safe to Modify?**: Medium risk.
- **Regression Area**: Hardware discovery on diverse USB controllers.

#### 20. [`Devices/DeviceEjector.cs`](../Devices/DeviceEjector.cs)
- **Primary Classes**: `DeviceEjector`
- **Purpose**: Historical / test dismount interface. (Production uses `PnpSafeEjectBackend`).
- **Callers**: Test suites / historical comparisons.
- **Dependencies**: Win32 IOCTLs.
- **Safety Relevance**: Low in v2.0 (bypassed in production).
- **Safe to Modify?**: Low risk.
- **Regression Area**: Legacy dismount test suites.

#### 21. [`Devices/DeviceIdentityResolver.cs`](../Devices/DeviceIdentityResolver.cs)
- **Primary Classes**: `DeviceIdentityResolver`
- **Purpose**: Normalizes instance IDs and extracts unique physical serial numbers.
- **Callers**: `DeviceDiscovery`.
- **Dependencies**: `Models/DeviceIdentity.cs`.
- **Safety Relevance**: Medium. Essential for reliable whitelisting.
- **Safe to Modify?**: Yes.
- **Regression Area**: Test suite 5.

#### 22. [`Devices/PnpSafeEjectBackend.cs`](../Devices/PnpSafeEjectBackend.cs)
- **Primary Classes**: `PnpSafeEjectBackend`, `PNP_VETO_TYPE`, `CallerPrivilegeContext`
- **Purpose**: Microsoft-native PnP Safe-Eject engine (`CM_Request_Device_EjectW`). Implements veto inspection, target devnode tree resolution, and `STORAGE\Volume` disk mapping.
- **Callers**: `ActiveSentinel`.
- **Dependencies**: `SystemDeviceSafetyGuard`, `DeviceEnforcementLifecycleManager`.
- **Safety Relevance**: **CRITICAL**. Interacts directly with Windows PnP manager.
- **Safe to Modify?**: **EXTREME RISK**. Must maintain zero FSCTL fallback and Disk 0 protection.
- **Regression Area**: Test suites 9DB through 9DX; hardware ejection tests.

#### 23. [`Devices/StorageNotificationManager.cs`](../Devices/StorageNotificationManager.cs)
- **Primary Classes**: `StorageNotificationManager`
- **Purpose**: Hidden Win32 message window receiving `WM_DEVICECHANGE` hardware events.
- **Callers**: `ActiveSentinel`.
- **Dependencies**: Win32 user32.dll P/Invoke.
- **Safety Relevance**: Medium. Must maintain clean STA message pumping.
- **Safe to Modify?**: Medium risk.
- **Regression Area**: Arrival event responsiveness.

---

### 2.4 Models Layer (`Models/`)

#### 24. [`Models/DeviceEjectOperationResult.cs`](../Models/DeviceEjectOperationResult.cs)
- **Primary Classes**: `DeviceEjectOperationResult`
- **Purpose**: Encapsulates PnP ejection return codes, veto names, attempt counts, and telemetry.
- **Callers**: `PnpSafeEjectBackend`, `ActiveSentinel`.

#### 25. [`Models/DeviceIdentity.cs`](../Models/DeviceIdentity.cs)
- **Primary Classes**: `DeviceIdentity`
- **Purpose**: Core domain entity representing a physical storage device.
- **Callers**: Entire solution.

#### 26. [`Models/DeviceRule.cs`](../Models/DeviceRule.cs)
- **Primary Classes**: `DeviceRule`
- **Purpose**: Represents a whitelisting or restriction rule.
- **Callers**: `RuleEngine`, `TrustedDeviceRepository`.

#### 27. [`Models/DiscoveryTimingSnapshot.cs`](../Models/DiscoveryTimingSnapshot.cs)
- **Primary Classes**: `DiscoveryTimingSnapshot`
- **Purpose**: Records millisecond timestamps during hardware discovery stages.

#### 28. [`Models/Enums.cs`](../Models/Enums.cs)
- **Primary Classes**: `DeviceCategory`, `StorageClass`, `PhysicalBusType`, `IdentityConfidence`, `ActivationState`, `SafetyValidationResult`, `DeviceEjectResult`, `WpdKind`
- **Purpose**: Authoritative domain enumerations.

#### 29. [`Models/ProtectionPolicy.cs`](../Models/ProtectionPolicy.cs)
- **Primary Classes**: `ProtectionPolicy`
- **Purpose**: Data contract of active administrative protection rules.

#### 30. [`Models/ProtectionStatusReport.cs`](../Models/ProtectionStatusReport.cs)
- **Primary Classes**: `ProtectionStatusReport`
- **Purpose**: Summary entity for UI status indicators and health reports.

#### 31. [`Models/StateJournalModels.cs`](../Models/StateJournalModels.cs)
- **Primary Classes**: `RegistryOriginalValue`, `JournalOperation`, `StateJournal`
- **Purpose**: Data models for write-ahead baseline recording and operation logs.

---

### 2.5 Persistence Layer (`Persistence/`)

#### 32. [`Persistence/JournalRepository.cs`](../Persistence/JournalRepository.cs)
- **Primary Classes**: `JournalRepository`
- **Purpose**: Manages `journal.json`. Captures original baseline values once only.
- **Callers**: `RegistryTransactionHelper`, `RecoveryManager`.
- **Safety Relevance**: **HIGH**. Basis of disaster recovery.

#### 33. [`Persistence/SecurityAclHelper.cs`](../Persistence/SecurityAclHelper.cs)
- **Primary Classes**: `SecurityAclHelper`
- **Purpose**: Enforces NTFS ACLs and `BUILTIN\Administrators` ownership on `%ProgramData%\USBShieldPro\`.
- **Callers**: `Program.Main`, repositories.
- **Safety Relevance**: **HIGH**. Prevents tampering by standard users.

#### 34. [`Persistence/SettingsRepository.cs`](../Persistence/SettingsRepository.cs)
- **Primary Classes**: `SettingsRepository`, `AppSettingsData`
- **Purpose**: Persists user settings (language, minimize-to-tray, UI scale) to `settings.json`.

#### 35. [`Persistence/TrustedDeviceRepository.cs`](../Persistence/TrustedDeviceRepository.cs)
- **Primary Classes**: `TrustedDeviceRepository`
- **Purpose**: Thread-safe persistence of whitelisted devices in `trusted_devices.json` with `.bak` redundancy.
- **Safety Relevance**: **HIGH**. Authoritative source of trusted device identities.

---

### 2.6 Security Layer (`Security/`)

#### 36. [`Security/PasswordManager.cs`](../Security/PasswordManager.cs)
- **Primary Classes**: `PasswordManager`
- **Purpose**: PBKDF2-HMAC-SHA256 password hashing (100k iterations) with DPAPI machine storage in `.auth`.
- **Safety Relevance**: **HIGH**. Prevents unauthorized GUI changes.

#### 37. [`Security/RecoveryKeyManager.cs`](../Security/RecoveryKeyManager.cs)
- **Primary Classes**: `RecoveryKeyManager`
- **Purpose**: Generates and verifies 128-bit Crockford Base32 Master Recovery Keys for password resets.
- **Safety Relevance**: **HIGH**. Administrative password recovery mechanism.

---

### 2.7 Service Layer (`Service/`)

#### 38. [`Service/ServiceManager.cs`](../Service/ServiceManager.cs)
- **Primary Classes**: `ServiceManager`
- **Purpose**: Controls Windows Service registration (`sc.exe`), startup, stop, and local named-pipe IPC.

#### 39. [`Service/UsbShieldService.cs`](../Service/UsbShieldService.cs)
- **Primary Classes**: `UsbShieldService`
- **Purpose**: Windows Service host (`ServiceBase`) providing continuous Session 0 background enforcement.

---

### 2.8 Windows Policy Layer (`Windows/`)

#### 40. [`Windows/AutoPlayPolicyBackend.cs`](../Windows/AutoPlayPolicyBackend.cs)
- **Primary Classes**: `AutoPlayPolicyBackend`
- **Purpose**: Manages `DisableAutoplay` and `NoAutoplayfornonVolume` while preserving `NoDriveTypeAutoRun`.

#### 41. [`Windows/DeviceInstallationPolicyBackend.cs`](../Windows/DeviceInstallationPolicyBackend.cs)
- **Primary Classes**: `DeviceInstallationPolicyBackend`
- **Purpose**: Configures Windows Device Installation Restrictions (`AllowInstanceIDs`, `AllowDenyLayered`).

#### 42. [`Windows/RegistryTransactionHelper.cs`](../Windows/RegistryTransactionHelper.cs)
- **Primary Classes**: `RegistryTransactionHelper`
- **Purpose**: Atomic journaled registry mutator with write-ahead recording and post-write verification.
- **Safety Relevance**: **HIGH**. Foundational mutator for all Windows policies.

#### 43. [`Windows/RemovableStoragePolicyBackend.cs`](../Windows/RemovableStoragePolicyBackend.cs)
- **Primary Classes**: `RemovableStoragePolicyBackend`
- **Purpose**: Configures `RemovableStorageDevices` policies (`Deny_All`, `Deny_Write`, `Deny_Read`, WPD).

#### 44. [`Windows/WindowsSecurityConstants.cs`](../Windows/WindowsSecurityConstants.cs)
- **Primary Classes**: `WindowsSecurityConstants`
- **Purpose**: Centralized authoritative repository of Windows registry paths and device setup class GUIDs.
