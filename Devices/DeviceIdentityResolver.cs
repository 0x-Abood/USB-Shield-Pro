// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Device Identity & Confidence Resolver
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Text.RegularExpressions;
using UsbShieldPro.Models;

namespace UsbShieldPro.Devices
{
    /// <summary>
    /// Evaluates device identity characteristics and assigns truthful confidence ratings.
    /// Invariant: Never claim a device has strong identity if it lacks a unique verified serial.
    /// </summary>
    public static class DeviceIdentityResolver
    {
        private static readonly Regex VidPidRegex = new Regex(@"VID_([0-9A-Fa-f]{4})&PID_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase);
        private static readonly Regex SyntheticBusPrefixRegex = new Regex(@"^[0-9A-Fa-f]{1,2}&[0-9A-Fa-f]+&", RegexOptions.Compiled);

        public static void Resolve(DeviceIdentity device)
        {
            if (device == null) return;

            // 1. Extract VID and PID
            ExtractVidPid(device);

            // 2. Extract and Validate Physical Serial Number
            ExtractAndValidateSerial(device);

            // 3. Assign Identity Confidence
            device.Confidence = EvaluateConfidence(device);
        }

        private static void ExtractVidPid(DeviceIdentity device)
        {
            string source = (device.DeviceInstanceId ?? "") + " " + (device.PhysicalUsbParentInstanceId ?? "") + " " + (device.ParentDeviceInstanceId ?? "") + " " + (device.ImmediateParentInstanceId ?? "") + " " + string.Join(" ", device.HardwareIds.ToArray());
            Match match = VidPidRegex.Match(source);
            if (match.Success)
            {
                device.VendorId = match.Groups[1].Value.ToUpperInvariant();
                device.ProductId = match.Groups[2].Value.ToUpperInvariant();
            }
        }

        private static void ExtractAndValidateSerial(DeviceIdentity device)
        {
            string candidate = ExtractSerialFromInstanceId(device.DeviceInstanceId);

            // If serial not found in disk instance ID (e.g. SCSI disk node has synthetic port ID),
            // inspect physical USB parent node first (e.g. USB\VID_xxxx&PID_xxxx\MSFT30...)
            if (string.IsNullOrEmpty(candidate) && !string.IsNullOrEmpty(device.PhysicalUsbParentInstanceId) && device.PhysicalUsbParentInstanceId != "UNKNOWN")
            {
                candidate = ExtractSerialFromInstanceId(device.PhysicalUsbParentInstanceId);
            }

            // Fallback to parent device instance ID
            if (string.IsNullOrEmpty(candidate) && !string.IsNullOrEmpty(device.ParentDeviceInstanceId))
            {
                candidate = ExtractSerialFromInstanceId(device.ParentDeviceInstanceId);
            }

            if (!string.IsNullOrEmpty(candidate) && IsValidPhysicalSerial(candidate))
            {
                device.SerialNumber = candidate;
            }
        }

        private static string ExtractSerialFromInstanceId(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return null;

            // In standard USB PnP instance IDs: USB\VID_xxxx&PID_xxxx\SerialNumber
            // Or USBSTOR\Disk&Ven_xxx&Prod_xxx&Rev_xxx\SerialNumber&0
            string[] parts = instanceId.Split('\\');
            if (parts.Length >= 3)
            {
                string candidate = parts[parts.Length - 1];

                // Check if candidate starts with synthetic bus/port location prefix like "7&2b9187a&0" or "0&1234&0"
                if (SyntheticBusPrefixRegex.IsMatch(candidate))
                {
                    return null;
                }

                // Remove trailing port/LUN suffix like "&0"
                int lastAmpersand = candidate.LastIndexOf('&');
                if (lastAmpersand > 0 && candidate.Length > lastAmpersand + 1)
                {
                    candidate = candidate.Substring(0, lastAmpersand);
                }

                // If any '&' remains after stripping the suffix, reject as a synthetic topology path
                if (candidate.Contains("&"))
                {
                    return null;
                }

                // Strip Microsoft USB Attached SCSI (UASP) "MSFT30" prefix if present
                if (candidate.StartsWith("MSFT30", StringComparison.OrdinalIgnoreCase) && candidate.Length > 6)
                {
                    candidate = candidate.Substring(6);
                }

                return candidate;
            }

            return null;
        }

        private static bool IsValidPhysicalSerial(string serial)
        {
            if (string.IsNullOrEmpty(serial)) return false;
            string s = serial.Trim();

            // Topology or location characters indicate bus instance, NOT physical serial
            if (s.Contains("&")) return false;

            // Strip formatting characters for pattern analysis
            string clean = s.Replace("-", "").Replace("_", "").Replace(".", "").Replace(" ", "").Trim();
            if (clean.Length < 8) return false; // Genuine physical serials are at least 8 chars

            string upper = clean.ToUpperInvariant();

            // Known generic dummy serials and controller keywords
            string[] prohibitedKeywords = new string[]
            {
                "DEMO", "DEFAULT", "SAMPLE", "GENERIC", "BRIDGE", "INITIO",
                "PROLIFIC", "ALCOR", "REALTEK", "CHIPSBK", "ASMEDIA", "JMICRON",
                "12345678", "01234567", "ABCDEFGH", "SERIAL", "UNKNOWN"
            };

            foreach (string kw in prohibitedKeywords)
            {
                if (upper.Contains(kw)) return false;
            }

            // Check for repeated single character (e.g. 11111111, FFFFFFFF, 00000000)
            bool allSame = true;
            char first = upper[0];
            for (int i = 1; i < upper.Length; i++)
            {
                if (upper[i] != first) { allSame = false; break; }
            }
            if (allSame) return false;

            // Count zeros and alphanumeric validity
            int zeroCount = 0;
            int nonAlphanumeric = 0;
            for (int i = 0; i < upper.Length; i++)
            {
                char c = upper[i];
                if (c == '0') zeroCount++;
                if (!char.IsLetterOrDigit(c)) nonAlphanumeric++;
            }

            if (nonAlphanumeric > 0) return false;

            // Mostly zeros test: if >= 60% zeros or dummy prefix of 4+ zeros ending with trivial small digit (e.g. 000...001)
            double zeroRatio = (double)zeroCount / upper.Length;
            if (zeroRatio >= 0.60) return false;

            if (upper.StartsWith("0000"))
            {
                string nonZeroPart = upper.TrimStart('0');
                if (nonZeroPart.Length < 6 || zeroRatio >= 0.50)
                {
                    return false;
                }
            }

            return true;
        }

        private static IdentityConfidence EvaluateConfidence(DeviceIdentity device)
        {
            // Internal disks are never candidates for removable device trust
            if (device.Category == DeviceCategory.InternalFixedDisk ||
                device.PhysicalTransport == PhysicalBusType.Nvme ||
                device.PhysicalTransport == PhysicalBusType.Sata ||
                device.PhysicalTransport == PhysicalBusType.Ata ||
                device.PhysicalTransport == PhysicalBusType.Raid ||
                device.SafetyResult != SafetyValidationResult.Passed)
            {
                return IdentityConfidence.None;
            }

            // Strong: Verified unique physical serial with zero synthetic patterns
            if (!string.IsNullOrEmpty(device.SerialNumber) && IsValidPhysicalSerial(device.SerialNumber))
            {
                return IdentityConfidence.Strong;
            }

            // Medium: Stable PnP DeviceInstanceId present, but no verified unique physical serial
            if (!string.IsNullOrEmpty(device.DeviceInstanceId) &&
                (!string.IsNullOrEmpty(device.VendorId) || !string.IsNullOrEmpty(device.Model)))
            {
                return IdentityConfidence.Medium;
            }

            // Weak: Fallback by VID/PID or Model
            if (!string.IsNullOrEmpty(device.VendorId) && !string.IsNullOrEmpty(device.ProductId))
            {
                return IdentityConfidence.Weak;
            }

            return IdentityConfidence.None;
        }
    }
}
