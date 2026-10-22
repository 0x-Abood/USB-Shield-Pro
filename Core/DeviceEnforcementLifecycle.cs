// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Per-Physical-Device Enforcement Lifecycle Manager
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UsbShieldPro.Devices;
using UsbShieldPro.Models;
using UsbShieldPro.Persistence;

namespace UsbShieldPro.Core
{
    /// <summary>
    /// Lifecycle states for an attached physical device under active policy enforcement.
    /// </summary>
    public enum EnforcementState
    {
        New,
        Evaluating,
        EjectPending,
        RetryWaiting,
        EjectedUntilRemoval,
        FailedTerminal,
        AllowedTrusted,
        SuspendedPolicyOff,
        Removed
    }

    /// <summary>
    /// Tracks the per-physical-key enforcement lifecycle, generation, and notification state.
    /// </summary>
    public class DeviceLifecycleRecord
    {
        public string PhysicalKey { get; set; }
        public string PhysicalUsbParentInstanceId { get; set; }
        public string ImmediateParentInstanceId { get; set; }
        public string PhysicalParentInstanceId { get; set; }
        public string StorageDeviceInstanceId { get; set; }
        public string SerialNumber { get; set; }
        public int DiskNumber { get; set; }
        public bool HadUsableMediaBeforeAttempt { get; set; }

        public int GenerationId { get; set; }
        public int EnforcementCycleId { get; set; }
        public EnforcementState State { get; set; }
        public int AttemptCount { get; set; }
        public bool NotificationEmitted { get; set; }

        public long FirstObservedTicks { get; set; }
        public long LastUpdatedTicks { get; set; }

        public CancellationTokenSource ActiveCts { get; set; }
    }

    /// <summary>
    /// Win32 PnP helper for parent device discovery, bounded ancestor walk, and presence queries.
    /// </summary>
    public static class PnpHelper
    {
        private const int CR_SUCCESS = 0;

        [DllImport("cfgmgr32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int CM_Locate_DevNode(out uint pdnDevInst, string pDeviceID, int ulFlags);

        [DllImport("cfgmgr32.dll", SetLastError = true)]
        private static extern int CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, int ulFlags);

        [DllImport("cfgmgr32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int CM_Get_Device_ID(uint dnDevInst, StringBuilder Buffer, int BufferLen, int ulFlags);

        public static string GetParentDeviceInstanceId(string deviceInstanceId)
        {
            return GetImmediateParentDeviceInstanceId(deviceInstanceId);
        }

        public static string GetImmediateParentDeviceInstanceId(string deviceInstanceId)
        {
            if (string.IsNullOrEmpty(deviceInstanceId)) return null;
            try
            {
                uint devInst;
                if (CM_Locate_DevNode(out devInst, deviceInstanceId, 0) != CR_SUCCESS) return null;
                uint parentInst;
                if (CM_Get_Parent(out parentInst, devInst, 0) != CR_SUCCESS) return null;
                StringBuilder sb = new StringBuilder(512);
                if (CM_Get_Device_ID(parentInst, sb, sb.Capacity, 0) != CR_SUCCESS) return null;
                return sb.ToString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Bounded ancestor walk up to maxDepth levels with cycle protection.
        /// Searches for the actual physical USB ancestor node (starts with USB\ and has VID_ & PID_).
        /// Returns "UNKNOWN" if no physical USB ancestor node can be established.
        /// </summary>
        public static string GetPhysicalUsbAncestorInstanceId(string deviceInstanceId, int maxDepth = 10)
        {
            if (string.IsNullOrEmpty(deviceInstanceId)) return "UNKNOWN";

            if (DeviceEnforcementLifecycleManager.IsTestMode && AncestorWalkSeam != null)
            {
                var testAncestors = AncestorWalkSeam(deviceInstanceId);
                if (testAncestors != null)
                {
                    foreach (var a in testAncestors)
                    {
                        if (a != null && a.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) &&
                            a.IndexOf("VID_", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            a.IndexOf("PID_", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return a;
                        }
                    }
                }
            }

            try
            {
                uint currentInst;
                if (CM_Locate_DevNode(out currentInst, deviceInstanceId, 0) != CR_SUCCESS) return "UNKNOWN";

                HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                visited.Add(deviceInstanceId);

                for (int depth = 0; depth < maxDepth; depth++)
                {
                    uint parentInst;
                    if (CM_Get_Parent(out parentInst, currentInst, 0) != CR_SUCCESS) break;

                    StringBuilder sb = new StringBuilder(512);
                    if (CM_Get_Device_ID(parentInst, sb, sb.Capacity, 0) != CR_SUCCESS) break;

                    string parentId = sb.ToString();
                    if (string.IsNullOrEmpty(parentId) || visited.Contains(parentId)) break;
                    visited.Add(parentId);

                    if (parentId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) &&
                        parentId.IndexOf("VID_", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        parentId.IndexOf("PID_", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return parentId;
                    }

                    currentInst = parentInst;
                }

                return "UNKNOWN";
            }
            catch
            {
                return "UNKNOWN";
            }
        }

        public static Func<string, bool> StorageBackedWpdSeam = null;
        public static Func<string, List<string>> AncestorWalkSeam = null;

        /// <summary>
        /// Retrieves the list of ancestor device instance IDs up to maxDepth levels.
        /// </summary>
        public static List<string> GetAncestorDeviceInstanceIds(string deviceInstanceId, int maxDepth = 10)
        {
            if (DeviceEnforcementLifecycleManager.IsTestMode && AncestorWalkSeam != null)
            {
                return AncestorWalkSeam(deviceInstanceId) ?? new List<string>();
            }

            List<string> ancestors = new List<string>();
            if (string.IsNullOrEmpty(deviceInstanceId)) return ancestors;
            try
            {
                uint currentInst;
                if (CM_Locate_DevNode(out currentInst, deviceInstanceId, 0) != CR_SUCCESS) return ancestors;

                HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                visited.Add(deviceInstanceId);

                for (int depth = 0; depth < maxDepth; depth++)
                {
                    uint parentInst;
                    if (CM_Get_Parent(out parentInst, currentInst, 0) != CR_SUCCESS) break;

                    StringBuilder sb = new StringBuilder(512);
                    if (CM_Get_Device_ID(parentInst, sb, sb.Capacity, 0) != CR_SUCCESS) break;

                    string parentId = sb.ToString();
                    if (string.IsNullOrEmpty(parentId) || visited.Contains(parentId)) break;
                    visited.Add(parentId);
                    ancestors.Add(parentId);
                    currentInst = parentInst;
                }

                return ancestors;
            }
            catch
            {
                return ancestors;
            }
        }

        /// <summary>
        /// Inspects PnP ancestry to determine if a WPD node is backed by a storage volume (STORAGE\Volume or volmgr)
        /// rather than being an independent physical portable device.
        /// </summary>
        public static bool IsStorageBackedWpdProxy(string deviceInstanceId, int maxDepth = 10)
        {
            if (DeviceEnforcementLifecycleManager.IsTestMode && StorageBackedWpdSeam != null)
            {
                return StorageBackedWpdSeam(deviceInstanceId);
            }

            if (string.IsNullOrEmpty(deviceInstanceId)) return false;

            // Direct instance ID indicators (e.g. SWD\WPDBUSENUM\_??_USBSTOR#DISK&... or containing STORAGE#VOLUME)
            if (deviceInstanceId.IndexOf("STORAGE#VOLUME", StringComparison.OrdinalIgnoreCase) >= 0 ||
                deviceInstanceId.IndexOf("STORAGE\\VOLUME", StringComparison.OrdinalIgnoreCase) >= 0 ||
                deviceInstanceId.IndexOf("USBSTOR#DISK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                deviceInstanceId.IndexOf("USBSTOR\\DISK", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            List<string> ancestors = GetAncestorDeviceInstanceIds(deviceInstanceId, maxDepth);
            foreach (var ancestor in ancestors)
            {
                if (ancestor.StartsWith("STORAGE\\VOLUME", StringComparison.OrdinalIgnoreCase) ||
                    ancestor.IndexOf("STORAGE#VOLUME", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ancestor.StartsWith("ROOT\\VOLMGR", StringComparison.OrdinalIgnoreCase) ||
                    ancestor.IndexOf("VOLMGR", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsDevNodePresent(string deviceInstanceId)
        {
            if (string.IsNullOrEmpty(deviceInstanceId)) return false;
            try
            {
                uint devInst;
                return CM_Locate_DevNode(out devInst, deviceInstanceId, 0) == CR_SUCCESS;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Central manager for per-physical-device lifecycle tracking, bounded retry coordination,
    /// fine-grained lock management, and idempotent notification dispatch.
    /// Invariant: External OS queries (cfgmgr32, PnP, Seams) are NEVER invoked under _syncLock.
    /// Invariant: All async lifecycle mutations require matching (GenerationId, EnforcementCycleId).
    /// </summary>
    public static class DeviceEnforcementLifecycleManager
    {
        private static readonly object _syncLock = new object();
        private static readonly Dictionary<string, DeviceLifecycleRecord> _activeLifecycles =
            new Dictionary<string, DeviceLifecycleRecord>(StringComparer.OrdinalIgnoreCase);

        // Bounded Retry Configuration
        public static int MaxEjectAttempts = 3;

        // Distinct retry delay schedules
        public static int BusyInitialRetryDelayMs = 250;
        public static int BusyMaxRetryDelayMs = 500;

        public static int AccessDeniedInitialRetryDelayMs = 500;
        public static int AccessDeniedMaxRetryDelayMs = 1000;

        public static int PnpVetoDeviceRetryTargetAgeMs = 10000;

        // Backwards compatibility aliases
        public static int InitialRetryDelayMs
        {
            get { return AccessDeniedInitialRetryDelayMs; }
            set { AccessDeniedInitialRetryDelayMs = value; }
        }
        public static int MaxRetryDelayMs
        {
            get { return AccessDeniedMaxRetryDelayMs; }
            set { AccessDeniedMaxRetryDelayMs = value; }
        }

        // ─── TEST SEAMS (Guarded: only mutable when IsTestMode == true) ───
        public static bool IsTestMode = false;
        internal static Func<DeviceIdentity, SentinelArrivalLatencyRecord, DeviceEjectResult> EjectExecutionSeam = null;
        internal static Action<DeviceIdentity, bool, string> NotificationSinkSeam = null;
        internal static Func<int, CancellationToken, Task> DelaySeam = null;
        internal static Func<string, bool> PhysicalPresenceSeam = null;

        public static void ResetForTest()
        {
            lock (_syncLock)
            {
                foreach (var rec in _activeLifecycles.Values)
                {
                    if (rec.ActiveCts != null)
                    {
                        try { rec.ActiveCts.Cancel(); rec.ActiveCts.Dispose(); } catch { }
                    }
                }
                _activeLifecycles.Clear();
                IsTestMode = true;
                EjectExecutionSeam = null;
                NotificationSinkSeam = null;
                DelaySeam = null;
                PhysicalPresenceSeam = null;
                PnpHelper.StorageBackedWpdSeam = null;
                PnpHelper.AncestorWalkSeam = null;
                TrustedDeviceRepository.IsDeviceTrustedSeam = null;
                DeviceDiscovery.ResetSeams();
                ActiveSentinel.ResetSeams();
                DeviceEjector.ResetSeams();
                PnpSafeEjectBackend.ResetSeams();
                StorageNotificationManager.ResetForTest();
                MaxEjectAttempts = 3;
                BusyInitialRetryDelayMs = 250;
                BusyMaxRetryDelayMs = 500;
                AccessDeniedInitialRetryDelayMs = 500;
                AccessDeniedMaxRetryDelayMs = 1000;
                PnpVetoDeviceRetryTargetAgeMs = 10000;
            }
        }

        public static void DisableTestMode()
        {
            lock (_syncLock)
            {
                _activeLifecycles.Clear();
                IsTestMode = false;
                EjectExecutionSeam = null;
                NotificationSinkSeam = null;
                DelaySeam = null;
                PhysicalPresenceSeam = null;
                PnpHelper.StorageBackedWpdSeam = null;
                PnpHelper.AncestorWalkSeam = null;
                TrustedDeviceRepository.IsDeviceTrustedSeam = null;
                DeviceDiscovery.ResetSeams();
                ActiveSentinel.ResetSeams();
                DeviceEjector.ResetSeams();
                PnpSafeEjectBackend.ResetSeams();
                StorageNotificationManager.ResetForTest();
                SystemDeviceSafetyGuard.ResetForTest();
                ProtectionEngine.ResetSeams();
                EnforcementOwnerResolver.ResetSeams();
                TemporaryAccessManager.ResetSeams();
                UsbShieldPro.Service.ServiceManager.ResetSeams();
                UsbShieldPro.Persistence.JournalRepository.ResetSeams();
                UsbShieldPro.Persistence.SettingsRepository.ResetSeams();
                UsbShieldPro.Windows.RegistryTransactionHelper.ResetSeams();
                UsbShieldPro.UI.ThemeManager.ResetSeams();
                UsbShieldPro.Security.PasswordManager.ResetSeams();
                UsbShieldPro.Security.RecoveryKeyManager.ResetSeams();
                UsbShieldPro.Windows.AutoPlayPolicyBackend.ResetSeams();
                PnpVetoDeviceRetryTargetAgeMs = 10000;
            }
        }

        public static void EnableTestMode()
        {
            lock (_syncLock)
            {
                IsTestMode = true;
            }
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Trim().Replace("/", "\\").ToUpperInvariant();
        }

        /// <summary>
        /// Builds the strongest available durable physical identity key.
        /// Hierarchy:
        /// 1. Verified Serial + Stable Physical USB Parent ID
        /// 2. Verified Serial + Canonical Storage Disk ID
        /// 3. Verified Serial only
        /// 4. Physical USB Parent ID only
        /// 5. Storage Disk ID only
        /// 6. Hardware IDs
        /// 7. Ephemeral fallback bounded strictly to current presence epoch
        /// </summary>
        public static string BuildPhysicalKey(DeviceIdentity dev)
        {
            if (dev == null) return "UNKNOWN";

            string serial = Normalize(dev.SerialNumber);
            string usbParent = (!string.IsNullOrEmpty(dev.PhysicalUsbParentInstanceId) && dev.PhysicalUsbParentInstanceId != "UNKNOWN")
                ? Normalize(dev.PhysicalUsbParentInstanceId)
                : ((!string.IsNullOrEmpty(dev.ParentDeviceInstanceId) && Normalize(dev.ParentDeviceInstanceId).StartsWith("USB"))
                    ? Normalize(dev.ParentDeviceInstanceId)
                    : null);
            string diskId = Normalize(dev.DeviceInstanceId);

            // 1. Preferred: Verified Serial + Stable Physical USB Parent ID
            if (!string.IsNullOrEmpty(serial) && !string.IsNullOrEmpty(usbParent) && usbParent.StartsWith("USB"))
            {
                return "SER:" + serial + "|USB:" + usbParent;
            }

            // 2. Verified Serial + Storage Disk ID (e.g. USBSTOR)
            if (!string.IsNullOrEmpty(serial) && !string.IsNullOrEmpty(diskId))
            {
                return "SER:" + serial + "|DISK:" + diskId;
            }

            // 3. Verified Serial only
            if (!string.IsNullOrEmpty(serial))
            {
                return "SER:" + serial;
            }

            // 4. Physical USB Parent ID only
            if (!string.IsNullOrEmpty(usbParent) && usbParent.StartsWith("USB"))
            {
                return "USB:" + usbParent;
            }

            // 5. Storage Disk ID only
            if (!string.IsNullOrEmpty(diskId))
            {
                return "ID:" + diskId;
            }

            // 6. Hardware IDs
            if (dev.HardwareIds != null && dev.HardwareIds.Count > 0)
            {
                return "HW:" + Normalize(dev.HardwareIds[0]);
            }

            // 7. Ephemeral fallback bounded to current presence epoch
            return "EPHEMERAL:DISK" + dev.PhysicalDiskNumber;
        }

        /// <summary>
        /// Checks if a device is physically present using its strongest available anchor.
        /// ALWAYS executed outside _syncLock.
        /// </summary>
        public static bool CheckPhysicalPresence(string physicalParentId, string storageId)
        {
            string anchor = (!string.IsNullOrEmpty(physicalParentId) && physicalParentId != "UNKNOWN") ? physicalParentId : storageId;
            if (string.IsNullOrEmpty(anchor)) return false;

            if (IsTestMode && PhysicalPresenceSeam != null)
            {
                return PhysicalPresenceSeam(anchor);
            }

            return PnpHelper.IsDevNodePresent(anchor);
        }

        /// <summary>
        /// Determines whether enforcement evaluation and ejection should be suppressed.
        /// Evaluated before calling RuleEngine or DeviceEjector.
        /// Adheres to fine-grained locking: OS presence query runs outside _syncLock.
        /// </summary>
        public static bool ShouldSuppressEnforcement(DeviceIdentity dev, ProtectionPolicy policy, out string reason)
        {
            reason = null;
            if (dev == null)
            {
                reason = "Device reference is null.";
                return true;
            }

            if (dev.WpdClassification == WpdKind.StorageVolumeProxy)
            {
                reason = "Storage volume proxy suppressed as duplicate representation of mounted disk.";
                return true;
            }

            if (dev.WpdClassification == WpdKind.StorageVolumeProxyUnresolved)
            {
                reason = "Unresolved storage volume proxy; virtual SWD node suppressed from direct eject awaiting disk owner reconciliation.";
                return true;
            }

            string key = BuildPhysicalKey(dev);

            // Step A: Briefly lock and snapshot state & anchor
            bool recordFound = false;
            string physicalParentId = null;
            string storageId = null;
            int genId = 0;
            int cycleId = 0;
            EnforcementState state = EnforcementState.New;

            lock (_syncLock)
            {
                DeviceLifecycleRecord rec;
                if (_activeLifecycles.TryGetValue(key, out rec))
                {
                    recordFound = true;
                    physicalParentId = (!string.IsNullOrEmpty(rec.PhysicalUsbParentInstanceId) && rec.PhysicalUsbParentInstanceId != "UNKNOWN")
                        ? rec.PhysicalUsbParentInstanceId
                        : (rec.PhysicalParentInstanceId ?? rec.StorageDeviceInstanceId);
                    storageId = rec.StorageDeviceInstanceId;
                    genId = rec.GenerationId;
                    cycleId = rec.EnforcementCycleId;
                    state = rec.State;
                }
            }

            if (!recordFound)
            {
                return false; // Brand new device, not suppressed
            }

            // Step B & C: Query physical presence and trust state OUTSIDE lock
            bool isPresent = CheckPhysicalPresence(physicalParentId, storageId);

            bool isCurrentlyTrusted = false;
            bool isAutoEjectRequired = false;
            if (state == EnforcementState.AllowedTrusted && policy != null)
            {
                if (policy.WhitelistMode)
                {
                    isCurrentlyTrusted = TrustedDeviceRepository.IsDeviceTrusted(dev);
                }
                var eval = RuleEngine.Evaluate(dev, policy, TemporaryAccessManager.IsActive);
                isAutoEjectRequired = eval.ShouldAutoEject;
            }

            // Step D, E, F: Re-acquire lock, verify generation & cycle, commit transition
            lock (_syncLock)
            {
                DeviceLifecycleRecord rec;
                if (!_activeLifecycles.TryGetValue(key, out rec))
                {
                    return false;
                }

                // Verify that record is still in the same generation and cycle
                if (rec.GenerationId != genId || rec.EnforcementCycleId != cycleId)
                {
                    reason = "Lifecycle generation or cycle moved concurrently.";
                    return true;
                }

                if (!isPresent)
                {
                    rec.State = EnforcementState.Removed;
                    return false; // Physically removed; eligible for fresh generation upon arrival
                }

                // Check dynamic trust revocation or policy escalation on AllowedTrusted (SEC-04)
                if (rec.State == EnforcementState.AllowedTrusted)
                {
                    if ((policy != null && policy.WhitelistMode && !isCurrentlyTrusted) || isAutoEjectRequired)
                    {
                        // Trust rule removed or class-wide blocking policy activated while device remains connected!
                        // Eligible for a new enforcement cycle (lease will be acquired in TryStartEnforcement).
                        reason = null;
                        return false;
                    }

                    reason = "Device evaluated as trusted for this physical presence.";
                    return true;
                }

                // If currently in terminal successful eject state
                if (rec.State == EnforcementState.EjectedUntilRemoval)
                {
                    reason = "Device already successfully ejected for this physical presence generation.";
                    return true; // Suppress
                }

                // If currently mid-cycle
                if (rec.State == EnforcementState.Evaluating ||
                    rec.State == EnforcementState.EjectPending ||
                    rec.State == EnforcementState.RetryWaiting)
                {
                    reason = "Enforcement cycle already active in state " + rec.State;
                    return true; // Suppress concurrent race
                }

                // If failed terminally
                if (rec.State == EnforcementState.FailedTerminal)
                {
                    reason = "Enforcement failed terminally; suppressed to prevent polling hammering.";
                    return true; // Suppress perpetual retries
                }

                // If suspended because policy was turned OFF
                if (rec.State == EnforcementState.SuspendedPolicyOff)
                {
                    if (policy != null && policy.WhitelistMode)
                    {
                        // Policy is now back ON: eligible for exactly one new logical enforcement cycle!
                        return false;
                    }
                    reason = "Protection policy is currently OFF.";
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Attempts to acquire the enforcement execution lease for a physical device.
        /// Guarantees that only ONE worker owns the active lifecycle.
        /// OS presence query is performed OUTSIDE _syncLock.
        /// </summary>
        public static bool TryStartEnforcement(DeviceIdentity dev, out DeviceLifecycleRecord record)
        {
            record = null;
            if (dev == null) return false;
            if (dev.WpdClassification == WpdKind.StorageVolumeProxy ||
                dev.WpdClassification == WpdKind.StorageVolumeProxyUnresolved)
            {
                return false;
            }

            string key = BuildPhysicalKey(dev);

            // Step A: Briefly lock and snapshot
            bool exists = false;
            string physicalParentId = null;
            string storageId = null;
            int genId = 0;
            int cycleId = 0;

            lock (_syncLock)
            {
                DeviceLifecycleRecord rec;
                if (_activeLifecycles.TryGetValue(key, out rec))
                {
                    exists = true;
                    physicalParentId = (!string.IsNullOrEmpty(rec.PhysicalUsbParentInstanceId) && rec.PhysicalUsbParentInstanceId != "UNKNOWN")
                        ? rec.PhysicalUsbParentInstanceId
                        : (rec.PhysicalParentInstanceId ?? rec.StorageDeviceInstanceId);
                    storageId = rec.StorageDeviceInstanceId;
                    genId = rec.GenerationId;
                    cycleId = rec.EnforcementCycleId;
                }
            }

            // Step B: Query physical presence OUTSIDE lock if record already exists
            bool isPresent = true;
            if (exists)
            {
                isPresent = CheckPhysicalPresence(physicalParentId, storageId);
            }

            bool hasUsableMedia = (dev.VolumeMountings != null && dev.VolumeMountings.Count > 0) || dev.TotalSizeBytes > 0;

            // Step C: Re-acquire lock and commit
            lock (_syncLock)
            {
                DeviceLifecycleRecord rec;
                if (exists && _activeLifecycles.TryGetValue(key, out rec))
                {
                    // Verify lease consistency
                    if (rec.GenerationId != genId || rec.EnforcementCycleId != cycleId)
                    {
                        return false;
                    }

                    if (!isPresent || rec.State == EnforcementState.Removed)
                    {
                        // Genuine new arrival generation after confirmed physical removal
                        rec.GenerationId++;
                        rec.EnforcementCycleId = 1;
                        rec.State = EnforcementState.Evaluating;
                        rec.AttemptCount = 0;
                        rec.NotificationEmitted = false;
                        rec.HadUsableMediaBeforeAttempt = hasUsableMedia;
                        rec.FirstObservedTicks = Stopwatch.GetTimestamp();
                        rec.LastUpdatedTicks = rec.FirstObservedTicks;
                        rec.PhysicalUsbParentInstanceId = dev.PhysicalUsbParentInstanceId ?? rec.PhysicalUsbParentInstanceId;
                        rec.ImmediateParentInstanceId = dev.ImmediateParentInstanceId ?? rec.ImmediateParentInstanceId;
                        rec.PhysicalParentInstanceId = (!string.IsNullOrEmpty(dev.PhysicalUsbParentInstanceId) && dev.PhysicalUsbParentInstanceId != "UNKNOWN")
                            ? dev.PhysicalUsbParentInstanceId
                            : (dev.ParentDeviceInstanceId ?? rec.PhysicalParentInstanceId);
                        rec.StorageDeviceInstanceId = dev.DeviceInstanceId ?? rec.StorageDeviceInstanceId;
                        rec.DiskNumber = dev.PhysicalDiskNumber;
                        record = rec;
                        return true;
                    }

                    if (rec.State == EnforcementState.SuspendedPolicyOff || rec.State == EnforcementState.AllowedTrusted)
                    {
                        // Policy toggled back ON or dynamic trust revocation/escalation: start new enforcement cycle within same presence generation
                        rec.EnforcementCycleId++;
                        rec.State = EnforcementState.Evaluating;
                        rec.AttemptCount = 0;
                        rec.NotificationEmitted = false;
                        rec.HadUsableMediaBeforeAttempt = hasUsableMedia;
                        rec.LastUpdatedTicks = Stopwatch.GetTimestamp();
                        record = rec;
                        return true;
                    }

                    // Already active (including Evaluating or EjectPending), ejected, or failed terminally while present: deny lease
                    return false;
                }

                // Brand new physical device lifecycle
                rec = new DeviceLifecycleRecord
                {
                    PhysicalKey = key,
                    PhysicalUsbParentInstanceId = dev.PhysicalUsbParentInstanceId,
                    ImmediateParentInstanceId = dev.ImmediateParentInstanceId,
                    PhysicalParentInstanceId = (!string.IsNullOrEmpty(dev.PhysicalUsbParentInstanceId) && dev.PhysicalUsbParentInstanceId != "UNKNOWN")
                        ? dev.PhysicalUsbParentInstanceId
                        : dev.ParentDeviceInstanceId,
                    StorageDeviceInstanceId = dev.DeviceInstanceId,
                    SerialNumber = dev.SerialNumber,
                    DiskNumber = dev.PhysicalDiskNumber,
                    HadUsableMediaBeforeAttempt = hasUsableMedia,
                    GenerationId = 1,
                    EnforcementCycleId = 1,
                    State = EnforcementState.Evaluating,
                    AttemptCount = 0,
                    NotificationEmitted = false,
                    FirstObservedTicks = Stopwatch.GetTimestamp(),
                    LastUpdatedTicks = Stopwatch.GetTimestamp()
                };

                _activeLifecycles[key] = rec;
                record = rec;
                return true;
            }
        }

        /// <summary>
        /// Records an allowed trusted device in the lifecycle repository.
        /// </summary>
        public static void RecordAllowedTrusted(DeviceIdentity dev)
        {
            if (dev == null) return;
            string key = BuildPhysicalKey(dev);
            lock (_syncLock)
            {
                DeviceLifecycleRecord rec;
                if (_activeLifecycles.TryGetValue(key, out rec))
                {
                    rec.State = EnforcementState.AllowedTrusted;
                    rec.NotificationEmitted = false;
                    rec.AttemptCount = 0;
                    rec.LastUpdatedTicks = Stopwatch.GetTimestamp();
                }
                else
                {
                    rec = new DeviceLifecycleRecord
                    {
                        PhysicalKey = key,
                        PhysicalUsbParentInstanceId = dev.PhysicalUsbParentInstanceId,
                        ImmediateParentInstanceId = dev.ImmediateParentInstanceId,
                        PhysicalParentInstanceId = (!string.IsNullOrEmpty(dev.PhysicalUsbParentInstanceId) && dev.PhysicalUsbParentInstanceId != "UNKNOWN")
                            ? dev.PhysicalUsbParentInstanceId
                            : dev.ParentDeviceInstanceId,
                        StorageDeviceInstanceId = dev.DeviceInstanceId,
                        SerialNumber = dev.SerialNumber,
                        DiskNumber = dev.PhysicalDiskNumber,
                        HadUsableMediaBeforeAttempt = (dev.VolumeMountings != null && dev.VolumeMountings.Count > 0) || dev.TotalSizeBytes > 0,
                        GenerationId = 1,
                        EnforcementCycleId = 1,
                        State = EnforcementState.AllowedTrusted,
                        AttemptCount = 0,
                        NotificationEmitted = false,
                        FirstObservedTicks = Stopwatch.GetTimestamp(),
                        LastUpdatedTicks = Stopwatch.GetTimestamp()
                    };
                    _activeLifecycles[key] = rec;
                }
            }
        }

        /// <summary>
        /// Validates that a worker lease is still active and owned by the specified generation and cycle.
        /// </summary>
        public static bool ValidateWorkerLease(string key, int generationId, int cycleId)
        {
            lock (_syncLock)
            {
                DeviceLifecycleRecord rec;
                if (_activeLifecycles.TryGetValue(key, out rec))
                {
                    return rec.GenerationId == generationId && rec.EnforcementCycleId == cycleId;
                }
                return false;
            }
        }

        /// <summary>
        /// Atomically transitions the lifecycle state.
        /// Rejects if generationId or cycleId do not match.
        /// </summary>
        public static bool TransitionState(string key, int generationId, int cycleId, EnforcementState newState)
        {
            lock (_syncLock)
            {
                DeviceLifecycleRecord rec;
                if (_activeLifecycles.TryGetValue(key, out rec))
                {
                    if (rec.GenerationId == generationId && rec.EnforcementCycleId == cycleId)
                    {
                        rec.State = newState;
                        rec.LastUpdatedTicks = Stopwatch.GetTimestamp();
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Atomically transitions to terminal state and claims notification ownership.
        /// Exactly ONE worker wins the notification claim per physical generation and cycle.
        /// Invariant: Losing worker must NEVER mutate state.
        /// </summary>
        public static bool TryClaimNotification(
            string physicalKey,
            int generationId,
            int cycleId,
            EnforcementState terminalState,
            out bool isSuccess)
        {
            isSuccess = false;
            lock (_syncLock)
            {
                DeviceLifecycleRecord rec;
                if (!_activeLifecycles.TryGetValue(physicalKey, out rec))
                {
                    return false;
                }

                if (rec.GenerationId != generationId || rec.EnforcementCycleId != cycleId)
                {
                    return false;
                }

                if (rec.NotificationEmitted)
                {
                    // Already claimed: return false WITHOUT MUTATING STATE!
                    return false;
                }

                rec.State = terminalState;
                rec.NotificationEmitted = true;
                rec.LastUpdatedTicks = Stopwatch.GetTimestamp();
                isSuccess = (terminalState == EnforcementState.EjectedUntilRemoval);
                return true;
            }
        }

        /// <summary>
        /// Handles dynamic policy or whitelist change during active backoff.
        /// Cleanly cancels pending retries without emitting false notifications.
        /// Rejects if generationId or cycleId do not match.
        /// </summary>
        public static bool HandlePolicyOrTrustChange(string key, int generationId, int cycleId, bool isNowTrusted, bool isPolicyOff)
        {
            CancellationTokenSource ctsToCancel = null;

            lock (_syncLock)
            {
                DeviceLifecycleRecord rec;
                if (_activeLifecycles.TryGetValue(key, out rec))
                {
                    if (rec.GenerationId == generationId && rec.EnforcementCycleId == cycleId)
                    {
                        if (isNowTrusted)
                        {
                            rec.State = EnforcementState.AllowedTrusted;
                        }
                        else if (isPolicyOff)
                        {
                            rec.State = EnforcementState.SuspendedPolicyOff;
                        }

                        rec.LastUpdatedTicks = Stopwatch.GetTimestamp();
                        ctsToCancel = rec.ActiveCts;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            if (ctsToCancel != null)
            {
                try { ctsToCancel.Cancel(); } catch { }
            }
            return true;
        }

        /// <summary>
        /// Handles physical removal of device during active backoff or retry.
        /// Transitions state to Removed and cancels pending retry.
        /// </summary>
        public static bool HandlePhysicalRemoval(string key, int generationId)
        {
            CancellationTokenSource ctsToCancel = null;

            lock (_syncLock)
            {
                DeviceLifecycleRecord rec;
                if (_activeLifecycles.TryGetValue(key, out rec))
                {
                    if (rec.GenerationId == generationId)
                    {
                        rec.State = EnforcementState.Removed;
                        rec.LastUpdatedTicks = Stopwatch.GetTimestamp();
                        ctsToCancel = rec.ActiveCts;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            if (ctsToCancel != null)
            {
                try { ctsToCancel.Cancel(); } catch { }
            }
            return true;
        }

        /// <summary>
        /// Cancels all in-flight enforcement workers and retry delays across all active lifecycles.
        /// Used when Temporary Access is activated or global policy transition requires immediate worker abatement (SEC-06).
        /// </summary>
        public static void CancelAllPendingWorkers(string reason)
        {
            List<CancellationTokenSource> toCancel = new List<CancellationTokenSource>();

            lock (_syncLock)
            {
                foreach (var kvp in _activeLifecycles)
                {
                    var rec = kvp.Value;
                    if (rec != null)
                    {
                        if (rec.State == EnforcementState.Evaluating ||
                            rec.State == EnforcementState.EjectPending ||
                            rec.State == EnforcementState.RetryWaiting)
                        {
                            rec.State = EnforcementState.SuspendedPolicyOff;
                            rec.LastUpdatedTicks = Stopwatch.GetTimestamp();
                        }
                        if (rec.ActiveCts != null)
                        {
                            toCancel.Add(rec.ActiveCts);
                        }
                    }
                }
            }

            foreach (var cts in toCancel)
            {
                try { cts.Cancel(); } catch { }
            }
        }

        private struct ReconcileItem
        {
            public string Key;
            public string Anchor;
            public int GenerationId;
            public int EnforcementCycleId;
        }

        /// <summary>
        /// Reconciles physically removed devices.
        /// ONLY clears lifecycle when physical parent anchor is confirmed absent from PnP.
        /// OS presence checks are executed OUTSIDE _syncLock.
        /// </summary>
        public static void ReconcileRemovedDevices(List<DeviceIdentity> currentlyAttached = null)
        {
            // Step 1: Snapshot active lifecycles under lock
            List<ReconcileItem> items = new List<ReconcileItem>();
            lock (_syncLock)
            {
                foreach (var kvp in _activeLifecycles)
                {
                    var rec = kvp.Value;
                    if (rec.State != EnforcementState.Removed)
                    {
                        items.Add(new ReconcileItem
                        {
                            Key = kvp.Key,
                            Anchor = (!string.IsNullOrEmpty(rec.PhysicalUsbParentInstanceId) && rec.PhysicalUsbParentInstanceId != "UNKNOWN")
                                ? rec.PhysicalUsbParentInstanceId
                                : (rec.PhysicalParentInstanceId ?? rec.StorageDeviceInstanceId),
                            GenerationId = rec.GenerationId,
                            EnforcementCycleId = rec.EnforcementCycleId
                        });
                    }
                }
            }

            // Step 2: Query presence OUTSIDE lock
            List<ReconcileItem> toMarkRemoved = new List<ReconcileItem>();
            foreach (var item in items)
            {
                bool isPresent = CheckPhysicalPresence(item.Anchor, null);
                if (!isPresent)
                {
                    toMarkRemoved.Add(item);
                }
            }

            // Step 3: Re-lock and mark removed if still current
            if (toMarkRemoved.Count > 0)
            {
                lock (_syncLock)
                {
                    foreach (var item in toMarkRemoved)
                    {
                        DeviceLifecycleRecord rec;
                        if (_activeLifecycles.TryGetValue(item.Key, out rec))
                        {
                            if (rec.GenerationId == item.GenerationId && rec.EnforcementCycleId == item.EnforcementCycleId)
                            {
                                rec.State = EnforcementState.Removed;
                                if (rec.ActiveCts != null)
                                {
                                    try { rec.ActiveCts.Cancel(); } catch { }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns diagnostic snapshot of a device's lifecycle state.
        /// </summary>
        public static DeviceLifecycleRecord GetLifecycleSnapshot(string physicalKey)
        {
            lock (_syncLock)
            {
                DeviceLifecycleRecord rec;
                if (_activeLifecycles.TryGetValue(physicalKey, out rec))
                {
                    return new DeviceLifecycleRecord
                    {
                        PhysicalKey = rec.PhysicalKey,
                        PhysicalUsbParentInstanceId = rec.PhysicalUsbParentInstanceId,
                        ImmediateParentInstanceId = rec.ImmediateParentInstanceId,
                        PhysicalParentInstanceId = rec.PhysicalParentInstanceId,
                        StorageDeviceInstanceId = rec.StorageDeviceInstanceId,
                        SerialNumber = rec.SerialNumber,
                        DiskNumber = rec.DiskNumber,
                        HadUsableMediaBeforeAttempt = rec.HadUsableMediaBeforeAttempt,
                        GenerationId = rec.GenerationId,
                        EnforcementCycleId = rec.EnforcementCycleId,
                        State = rec.State,
                        AttemptCount = rec.AttemptCount,
                        NotificationEmitted = rec.NotificationEmitted,
                        FirstObservedTicks = rec.FirstObservedTicks,
                        LastUpdatedTicks = rec.LastUpdatedTicks
                    };
                }
                return null;
            }
        }
    }
}
