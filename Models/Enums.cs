// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Core Domain Enumerations
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

namespace UsbShieldPro.Models
{
    /// <summary>
    /// Explicit technical classification of an enforcement capability.
    /// </summary>
    public enum EnforcementClassification
    {
        ReliablyEnforceable,
        ReliablyEnforceableAfterReconnect,
        ReliablyEnforceableAfterRestart,
        PartiallyEnforceable,
        ReactiveOnly,
        UnsupportedForDevice,
        NotImplementedForSafety
    }

    /// <summary>
    /// Friendly device categorization (decoupled from physical transport).
    /// </summary>
    public enum DeviceCategory
    {
        Unknown,
        UsbFlashDrive,
        ExternalUsbSsd,
        ExternalUsbHdd,
        MobilePhoneOrMediaDevice,
        OpticalDrive,
        InternalFixedDisk,
        CriticalSystemHardware
    }

    /// <summary>
    /// Classification of Windows Portable Device (WPD) nodes:
    /// separates genuine MTP/PTP portable devices from mounted storage volume proxies.
    /// </summary>
    public enum WpdKind
    {
        None = 0,
        PortableDevice = 1,
        StorageVolumeProxy = 2,
        StorageVolumeProxyUnresolved = 3
    }

    /// <summary>
    /// Windows storage or PnP class.
    /// </summary>
    public enum StorageClass
    {
        Unknown,
        DiskDrive,
        WindowsPortableDevice,
        CdRom,
        Floppy,
        UsbHubOrController,
        InputDevice,
        NetworkAdapter
    }

    /// <summary>
    /// Physical transport bus.
    /// </summary>
    public enum PhysicalBusType
    {
        Unknown = 0,
        Scsi = 1,
        Atapi = 2,
        Ata = 3,
        OneThreeNineFour = 4,
        Ssa = 5,
        Fibre = 6,
        Usb = 7,
        Raid = 8,
        IScsi = 9,
        Sas = 10,
        Sata = 11,
        Sd = 12,
        Mmc = 13,
        Virtual = 14,
        FileBackedVirtual = 15,
        Spaces = 16,
        Nvme = 17,
        Scm = 18,
        Ufs = 19
    }

    /// <summary>
    /// Quality and uniqueness level of a device identity.
    /// </summary>
    public enum IdentityConfidence
    {
        None,
        /// <summary>VID/PID and model only. Shared across thousands of units.</summary>
        Weak,
        /// <summary>Stable PnP instance ID on current port, but missing physical serial.</summary>
        Medium,
        /// <summary>Confirmed unique physical serial number in device instance path.</summary>
        Strong
    }

    /// <summary>
    /// Real-world observed activation state on the Windows OS.
    /// </summary>
    public enum ActivationState
    {
        Unprotected,
        AppliedAndVerified,
        ClassPolicyConfigured,
        ClassPolicyActive, // Retained for compatibility; mapped to ClassPolicyConfigured
        PendingReconnect,
        PendingRestart,
        TemporaryAccessActive,
        PendingRelock,
        ExternallyOverridden,
        ConfigurationConflict,
        UnsupportedForDevice,
        RecoveryRequired,
        ReactiveMonitoringActive,
        TrustedOnlyReactive,
        StrictInstallationControl,
        Failed
    }

    /// <summary>
    /// Safety assessment result for a device.
    /// </summary>
    public enum SafetyValidationResult
    {
        Passed,
        RejectedSystemOrBootDisk,
        RejectedInternalBus,
        RejectedCriticalPeripheral,
        RejectedAmbiguousTransport
    }

    /// <summary>
    /// Action associated with a device rule.
    /// </summary>
    public enum RuleAction
    {
        AllowTrusted,
        DenyBlocked,
        ReadOnly
    }

    /// <summary>
    /// Operational status of a journaled transaction.
    /// </summary>
    public enum JournalOperationStatus
    {
        Prepared,
        Applied,
        Verified,
        RollbackPending,
        RolledBack,
        Failed
    }

    /// <summary>
    /// Execution mode of the application.
    /// </summary>
    public enum AppExecutionMode
    {
        PortableMode,
        FullProtectionModeClient,
        BackgroundService
    }

    /// <summary>
    /// Matching scope for a trusted device rule.
    /// </summary>
    public enum RuleMatchType
    {
        DeviceInstanceId,
        HardwareId,
        SerialNumber,
        ModelFallback
    }

    /// <summary>
    /// Structured result of a safe volume ejection attempt.
    /// </summary>
    public enum DeviceEjectResult
    {
        Success,
        Busy,
        AccessDenied,
        SafetyRejected,
        NotEjectable,
        DeviceGone,
        IncompleteEject,
        Failed,
        VerificationIndeterminate,
        RetryableAccessDenied
    }

    /// <summary>
    /// Tri-state classification of a volume's post-eject availability.
    /// Invariant: Unknown/unrecognized IOCTL errors must strictly yield Indeterminate (fail-closed).
    /// </summary>
    public enum VolumeAvailabilityResult
    {
        Available,
        UnavailableConfirmed,
        Indeterminate
    }
}
