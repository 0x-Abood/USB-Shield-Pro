// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Device Identity & Characteristics Model
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace UsbShieldPro.Models
{
    /// <summary>
    /// Encapsulates the complete hardware identity, transport characteristics,
    /// and safety evaluation of an attached or discovered device.
    /// </summary>
    public class DeviceIdentity
    {
        public string FriendlyName { get; set; }
        public string Manufacturer { get; set; }
        public string Model { get; set; }

        public string DeviceInstanceId { get; set; }
        public List<string> HardwareIds { get; set; }
        public string HardwareId
        {
            get { return (HardwareIds != null && HardwareIds.Count > 0) ? HardwareIds[0] : ""; }
            set
            {
                if (HardwareIds == null) HardwareIds = new List<string>();
                HardwareIds.Clear();
                if (!string.IsNullOrEmpty(value)) HardwareIds.Add(value);
            }
        }
        public List<string> CompatibleIds { get; set; }
        public string ParentDeviceInstanceId { get; set; }
        public string PhysicalUsbParentInstanceId { get; set; }
        public string ImmediateParentInstanceId { get; set; }
        public WpdKind WpdClassification { get; set; }
        public List<string> SuppressedProxyInstanceIds { get; set; }

        public string SerialNumber { get; set; }
        public string VendorId { get; set; }
        public string ProductId { get; set; }

        public PhysicalBusType PhysicalTransport { get; set; }
        public StorageClass WindowsClass { get; set; }
        public string SetupClassGuid { get; set; }
        public DeviceCategory Category { get; set; }
        public IdentityConfidence Confidence { get; set; }

        public int PhysicalDiskNumber { get; set; }
        public int PartitionCount { get; set; }
        public List<string> VolumeMountings { get; set; }
        public Dictionary<string, string> VolumeLabels { get; set; }
        public bool IsRemovableMedia { get; set; }
        public bool? HasSeekPenalty { get; set; }
        public long TotalSizeBytes { get; set; }

        public SafetyValidationResult SafetyResult { get; set; }
        public string SafetyRejectionReason { get; set; }

        public DiscoveryTimingSnapshot DiscoverySnapshot { get; set; }

        public DateTime FirstDiscoveredUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }

        public DeviceIdentity()
        {
            HardwareIds = new List<string>();
            CompatibleIds = new List<string>();
            VolumeMountings = new List<string>();
            VolumeLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            SuppressedProxyInstanceIds = new List<string>();
            PhysicalDiskNumber = -1;
            WpdClassification = WpdKind.None;
            Confidence = IdentityConfidence.None;
            SafetyResult = SafetyValidationResult.Passed;
            FirstDiscoveredUtc = DateTime.UtcNow;
            LastSeenUtc = DateTime.UtcNow;
        }

        public string GetCleanDisplayName()
        {
            string raw = !string.IsNullOrEmpty(FriendlyName) ? FriendlyName :
                         (!string.IsNullOrEmpty(Model) ? Model : "USB Device");

            string cleaned = raw.Replace('_', ' ').Trim();

            // If generic flash disk name and has distinct volume labels, append primary volume label
            if (cleaned.IndexOf("Generic Flash Disk", StringComparison.OrdinalIgnoreCase) >= 0 && VolumeLabels != null && VolumeLabels.Count > 0)
            {
                List<string> labels = new List<string>();
                foreach (var v in VolumeLabels.Values)
                {
                    if (!string.IsNullOrEmpty(v)) labels.Add(v);
                }
                if (labels.Count > 0)
                {
                    return cleaned + " (" + string.Join(", ", labels.ToArray()) + ")";
                }
            }

            return cleaned;
        }

        public string GetVolumesDisplayString()
        {
            if (VolumeMountings == null || VolumeMountings.Count == 0) return "";
            List<string> parts = new List<string>();
            foreach (var v in VolumeMountings)
            {
                string label;
                if (VolumeLabels != null && VolumeLabels.TryGetValue(v, out label) && !string.IsNullOrEmpty(label))
                {
                    parts.Add(v + " (" + label + ")");
                }
                else
                {
                    parts.Add(v);
                }
            }
            return string.Join("  •  ", parts.ToArray());
        }

        public bool IsSystemOrBootDrive()
        {
            return SafetyResult == SafetyValidationResult.RejectedSystemOrBootDisk;
        }

        public bool IsSystemDisk
        {
            get { return IsSystemOrBootDrive() || Category == DeviceCategory.InternalFixedDisk || Category == DeviceCategory.CriticalSystemHardware; }
        }

        public int DiskIndex
        {
            get { return PhysicalDiskNumber; }
        }

        public bool IsCriticalPeripheral()
        {
            return SafetyResult == SafetyValidationResult.RejectedCriticalPeripheral;
        }

        public bool IsSafeToTarget()
        {
            return SafetyResult == SafetyValidationResult.Passed;
        }

        public string GetConfidenceDescription()
        {
            switch (Confidence)
            {
                case IdentityConfidence.Strong:
                    return "Strong (Verified unique physical serial)";
                case IdentityConfidence.Medium:
                    return "Medium (Stable instance on port; synthetic serial)";
                case IdentityConfidence.Weak:
                    return "Weak (VID/PID only; non-unique across identical models)";
                default:
                    return "Unknown";
            }
        }
    }
}
