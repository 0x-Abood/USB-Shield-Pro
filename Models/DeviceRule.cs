// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Device Rule Model
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;

namespace UsbShieldPro.Models
{
    /// <summary>
    /// Represents a persistent whitelist or blacklist rule for a specific device.
    /// </summary>
    public class DeviceRule
    {
        public string RuleId { get; set; }
        public string TargetInstanceId { get; set; }
        public string TargetHardwareId { get; set; }
        public string TargetSerial { get; set; }
        public string FriendlyName { get; set; }
        public RuleMatchType MatchType { get; set; }
        public IdentityConfidence Confidence { get; set; }
        public RuleAction Action { get; set; }
        public DateTime CreatedUtc { get; set; }
        public int? AllocatedPolicyIndex { get; set; }

        public DeviceRule()
        {
            RuleId = Guid.NewGuid().ToString("N");
            MatchType = RuleMatchType.DeviceInstanceId;
            Action = RuleAction.AllowTrusted;
            Confidence = IdentityConfidence.Weak;
            CreatedUtc = DateTime.UtcNow;
        }

        public bool Matches(DeviceIdentity device)
        {
            if (device == null) return false;

            bool hasTargetSerial = !string.IsNullOrWhiteSpace(TargetSerial);
            bool hasTargetInstance = !string.IsNullOrWhiteSpace(TargetInstanceId);
            bool hasTargetHardware = !string.IsNullOrWhiteSpace(TargetHardwareId);

            // Fail safe on unconfigured, empty, or whitespace-only target definitions
            if (!hasTargetSerial && !hasTargetInstance && !hasTargetHardware)
            {
                return false;
            }

            // ── 1. SERIAL-BOUND RULE ────────────────────────────────────────────────
            // When a unique physical serial number is specified (or MatchType is SerialNumber),
            // the rule is bound to a specific physical unit.
            if (hasTargetSerial || MatchType == RuleMatchType.SerialNumber)
            {
                // 1a. Rule must define a valid non-empty serial
                if (!hasTargetSerial) return false;

                // 1b. Candidate device MUST provide a non-empty physical serial
                if (string.IsNullOrWhiteSpace(device.SerialNumber)) return false;

                // 1c. Serial numbers MUST match exactly (case-insensitive, trimmed)
                if (!string.Equals(TargetSerial.Trim(), device.SerialNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    // Serial mismatch MUST NEVER fall through to instance or hardware matching!
                    return false;
                }

                // 1d. Composite Identity Consistency: If the rule also specifies TargetHardwareId,
                // the candidate device must present consistent hardware identification.
                if (hasTargetHardware)
                {
                    if (!MatchesHardwareId(device))
                    {
                        // Conflicting hardware identity -> prevent cross-device trust!
                        return false;
                    }
                }

                return true;
            }

            // ── 2. INSTANCE-BOUND RULE ──────────────────────────────────────────────
            // When the rule specifies a device instance ID (and has no physical serial),
            // or MatchType is explicitly DeviceInstanceId without serial.
            if (hasTargetInstance || MatchType == RuleMatchType.DeviceInstanceId)
            {
                // 2a. Rule must define a valid non-empty instance ID
                if (!hasTargetInstance) return false;

                // 2b. Candidate device MUST provide a non-empty instance ID
                if (string.IsNullOrWhiteSpace(device.DeviceInstanceId)) return false;

                // 2c. Instance ID must match (exact devnode or USB parent path)
                if (!MatchesInstanceId(device))
                {
                    // Instance mismatch MUST NEVER fall through to generic hardware matching!
                    return false;
                }

                // 2d. Composite Identity Consistency: If the rule also specifies TargetHardwareId,
                // verify hardware ID consistency.
                if (hasTargetHardware)
                {
                    if (!MatchesHardwareId(device))
                    {
                        return false;
                    }
                }

                return true;
            }

            // ── 3. EXPLICIT HARDWARE-ONLY RULE ──────────────────────────────────────
            // Preserved strictly for explicit or legacy model/hardware-level rules
            // (MatchType is HardwareId or ModelFallback with no strong serial/instance binding).
            if (MatchType == RuleMatchType.HardwareId || MatchType == RuleMatchType.ModelFallback)
            {
                if (hasTargetHardware)
                {
                    return MatchesHardwareId(device);
                }
            }

            return false;
        }

        private bool MatchesHardwareId(DeviceIdentity device)
        {
            if (string.IsNullOrWhiteSpace(TargetHardwareId)) return false;
            if (device.HardwareIds == null || device.HardwareIds.Count == 0) return false;

            string targetHw = TargetHardwareId.Trim();
            foreach (string hid in device.HardwareIds)
            {
                if (!string.IsNullOrWhiteSpace(hid) &&
                    string.Equals(targetHw, hid.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsValidUsbCompositeParent(string parentInstanceId)
        {
            if (string.IsNullOrWhiteSpace(parentInstanceId)) return false;
            string p = parentInstanceId.Trim();
            if (!p.StartsWith(@"USB\VID_", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string upper = p.ToUpperInvariant();
            if (upper.Contains("HUB") || upper.Contains("ROOT_HUB"))
            {
                return false;
            }
            return true;
        }

        private bool MatchesInstanceId(DeviceIdentity device)
        {
            if (string.IsNullOrWhiteSpace(TargetInstanceId)) return false;
            string target = TargetInstanceId.Trim();

            // 1. Direct device instance ID match (exact devnode)
            if (!string.IsNullOrWhiteSpace(device.DeviceInstanceId) &&
                string.Equals(target, device.DeviceInstanceId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 2. Parent matching strictly limited to verified device-specific composite USB parent
            // Prohibit trust inheritance from USB Hubs, Root Hubs, or Host Controllers
            if (IsValidUsbCompositeParent(device.PhysicalUsbParentInstanceId) &&
                string.Equals(target, device.PhysicalUsbParentInstanceId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (IsValidUsbCompositeParent(device.ParentDeviceInstanceId) &&
                string.Equals(target, device.ParentDeviceInstanceId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (IsValidUsbCompositeParent(device.ImmediateParentInstanceId) &&
                string.Equals(target, device.ImmediateParentInstanceId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
