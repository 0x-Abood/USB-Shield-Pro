// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Audited Windows Security & Policy Constants
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

namespace UsbShieldPro.Windows
{
    /// <summary>
    /// Authoritative Windows registry paths and class GUIDs verified against official documentation.
    /// Invariant: No hardcoded magic strings scattered across business logic.
    /// </summary>
    public static class WindowsSecurityConstants
    {
        // ── Removable Storage Access Policies ────────────────────────────────
        public const string RemovableStoragePoliciesPath = @"SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices";
        public const string ValueDenyAll = "Deny_All";
        public const string ValueDenyRead = "Deny_Read";
        public const string ValueDenyWrite = "Deny_Write";
        public const string ValueDenyExecute = "Deny_Execute";

        // Audited Removable Device Class GUIDs
        public const string GuidRemovableDisks = "{53f5630d-b6bf-11d0-94f2-00a0c91efb8b}";
        public const string GuidWindowsPortableDevices = "{6AC27878-A6FA-4155-BA85-F98F491D4F33}"; // Verified F98F
        public const string GuidCdDvdDevices = "{53f56308-b6bf-11d0-94f2-00a0c91efb8b}";
        public const string GuidFloppyDevices = "{53f56311-b6bf-11d0-94f2-00a0c91efb8b}";

        // ── Device Installation Restrictions ─────────────────────────────────
        public const string DeviceInstallRestrictionsPath = @"SOFTWARE\Policies\Microsoft\Windows\DeviceInstall\Restrictions";
        public const string SubkeyAllowInstanceIDs = "AllowInstanceIDs";
        public const string ValueAllowInstanceIDs = "AllowInstanceIDs";
        public const string SubkeyAllowDeviceIDs = "AllowDeviceIDs";
        public const string ValueAllowDeviceIDs = "AllowDeviceIDs";
        public const string SubkeyDenyDeviceClasses = "DenyDeviceClasses";
        public const string ValueDenyRemovableDevices = "DenyRemovableDevices";
        public const string ValueAllowDenyLayered = "AllowDenyLayered";
        public const string ValueDenyDeviceClassesRetroactive = "DenyDeviceClassesRetroactive";

        // Setup Class GUIDs
        public const string GuidWpdSetupClass = "{eec5ad98-8080-425f-922a-dab33962f594}";
        // INVARIANT: DiskDrive setup class {4d36e967-e325-11ce-bfc1-08002be10318} MUST NEVER be added to DenyDeviceClasses!

        // ── Storage Device Policies (Read-Only) ──────────────────────────────
        public const string StorageDevicePoliciesPath = @"SYSTEM\CurrentControlSet\Control\StorageDevicePolicies";
        public const string ValueWriteProtect = "WriteProtect";

        // ── Storage Driver Service Stacks ────────────────────────────────────
        public const string ServiceUsbStorPath = @"SYSTEM\CurrentControlSet\Services\USBSTOR";
        public const string ServiceUaspStorPath = @"SYSTEM\CurrentControlSet\Services\UASPSTOR";
        public const string ValueStart = "Start";

        public const int ServiceStartManual = 3;   // Enabled / Demand Start
        public const int ServiceStartDisabled = 4; // Disabled

        // ── AutoPlay Guard Policies ──────────────────────────────────────────
        public const string AutoPlayHandlersUserPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers";
        public const string ValueDisableAutoplay = "DisableAutoplay";

        public const string ExplorerPoliciesPath = @"SOFTWARE\Policies\Microsoft\Windows\Explorer";
        public const string ValueNoAutoplayfornonVolume = "NoAutoplayfornonVolume";

        public const string ExplorerSystemPoliciesPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
        public const string ValueNoDriveTypeAutoRun = "NoDriveTypeAutoRun";
    }
}
