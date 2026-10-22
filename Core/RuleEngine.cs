// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Deterministic Device Rule Evaluation Engine
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using UsbShieldPro.Models;
using UsbShieldPro.Persistence;

namespace UsbShieldPro.Core
{
    public class DeviceEvaluationResult
    {
        public RuleAction RecommendedAction { get; set; }
        public string EvaluationReason { get; set; }
        public string DisplayBadge { get; set; }
        public bool ShouldAutoEject { get; set; }
        public bool IsSafetyProtected { get; set; }
    }

    /// <summary>
    /// Evaluates deterministic rule precedence for an attached device.
    /// Precedence: System Safety > Temp Access > Explicit Allow (Whitelist) > Category Policy > Global Deny > Default Allow.
    /// Invariant: SystemDeviceSafetyGuard is called as the primary gate.
    /// </summary>
    public static class RuleEngine
    {
        public static DeviceEvaluationResult Evaluate(
            DeviceIdentity device,
            ProtectionPolicy currentPolicy,
            bool isTemporaryAccessActive)
        {
            DeviceEvaluationResult result = new DeviceEvaluationResult
            {
                RecommendedAction = RuleAction.AllowTrusted,
                DisplayBadge = "UNRESTRICTED",
                EvaluationReason = "No active policy applies to this device.",
                ShouldAutoEject = false,
                IsSafetyProtected = false
            };

            if (device == null) return result;

            // ── GATE 0: System Device Safety Guard (Absolute Barrier) ────────
            string safetyReason;
            SafetyValidationResult safety = SystemDeviceSafetyGuard.ValidateDevice(device, out safetyReason);
            if (safety != SafetyValidationResult.Passed)
            {
                result.IsSafetyProtected = true;
                result.DisplayBadge = "SYSTEM HARDWARE";
                result.EvaluationReason = "Protected critical hardware: " + safetyReason;
                result.RecommendedAction = RuleAction.AllowTrusted;
                result.ShouldAutoEject = false;
                return result;
            }

            // ── GATE 1: Temporary Access Override ────────────────────────────
            if (isTemporaryAccessActive)
            {
                result.DisplayBadge = "TEMP ALLOWED";
                result.EvaluationReason = "Temporary unlock is currently active.";
                result.RecommendedAction = RuleAction.AllowTrusted;
                result.ShouldAutoEject = false;
                return result;
            }

            // ── GATE 2: Explicit Whitelist Match ─────────────────────────────
            bool isTrusted = TrustedDeviceRepository.IsDeviceTrusted(device);
            if (isTrusted)
            {
                // Invariant: Windows Removable Storage class policies (Deny_All / RemovableDisks Deny_Read|Write)
                // operate inside the kernel storage stack and override all individual whitelist exceptions.
                if (currentPolicy != null && (currentPolicy.BlockAllRemovable || (currentPolicy.BlockRemovableDisks && (device.IsRemovableMedia || device.PhysicalTransport == PhysicalBusType.Usb))))
                {
                    result.DisplayBadge = "CLASS BLOCKED (TRUST OVERRIDDEN)";
                    result.EvaluationReason = "Device is trusted, but Windows class-wide Removable Storage policy is active and supersedes individual device exceptions.";
                    result.RecommendedAction = RuleAction.DenyBlocked;
                    result.ShouldAutoEject = true;
                    return result;
                }

                result.DisplayBadge = "TRUSTED";
                result.EvaluationReason = "Device matches an authorized entry in Trusted Devices.";
                result.RecommendedAction = RuleAction.AllowTrusted;
                result.ShouldAutoEject = false;
                return result;
            }

            // ── GATE 3: Whitelist & Strict PnP Modes ─────────────────────────
            if (currentPolicy != null)
            {
                if (currentPolicy.StrictPnpLockdown)
                {
                    result.DisplayBadge = "STRICT PNP INSTALLATION CONTROL";
                    result.EvaluationReason = "Device rejected under Strict PnP Installation Control.";
                    result.RecommendedAction = RuleAction.DenyBlocked;
                    result.ShouldAutoEject = true;
                    return result;
                }

                if (currentPolicy.WhitelistMode)
                {
                    result.DisplayBadge = "TRUSTED-ONLY — AUTO-EJECT";
                    result.EvaluationReason = "Unauthorized storage device connected while Normal Trusted-Only Mode is active.";
                    result.RecommendedAction = RuleAction.DenyBlocked;
                    result.ShouldAutoEject = true;
                    return result;
                }
            }

            // ── GATE 4: Category Policies ───────────────────────────────────
            if (currentPolicy != null)
            {
                // Global Deny_All
                if (currentPolicy.BlockAllRemovable && (device.IsRemovableMedia || device.PhysicalTransport == PhysicalBusType.Usb))
                {
                    result.DisplayBadge = "POLICY BLOCK";
                    result.EvaluationReason = "Global Deny_All removable storage policy applies.";
                    result.RecommendedAction = RuleAction.DenyBlocked;
                    result.ShouldAutoEject = true;
                    return result;
                }

                // Removable Disks Policy (Setup Class GUID {53f5630d-b6bf-11d0-94f2-00a0c91efb8b})
                if (currentPolicy.BlockRemovableDisks && (device.IsRemovableMedia || device.PhysicalTransport == PhysicalBusType.Usb))
                {
                    result.DisplayBadge = "POLICY BLOCK";
                    result.EvaluationReason = "Windows Removable Disks policy applies (reliably enforceable via reactive ejection).";
                    result.RecommendedAction = RuleAction.DenyBlocked;
                    result.ShouldAutoEject = true;
                    return result;
                }

                // USB Flash Drives Only (Active Sentinel Reactive Auto-Eject)
                if (currentPolicy.BlockFlashDrivesOnlyReactive && device.Category == DeviceCategory.UsbFlashDrive)
                {
                    result.DisplayBadge = "REACTIVE AUTO-EJECT";
                    result.EvaluationReason = "USB Flash Media classified by Sentinel; reactive auto-ejection applied.";
                    result.RecommendedAction = RuleAction.DenyBlocked;
                    result.ShouldAutoEject = true;
                    return result;
                }

                // WPD (Phones/Cameras) Block
                if (currentPolicy.BlockWpdPhonesAndMedia && device.WindowsClass == StorageClass.WindowsPortableDevice)
                {
                    result.DisplayBadge = "POLICY BLOCK";
                    result.EvaluationReason = "Windows Portable Devices (WPD) access policy applies.";
                    result.RecommendedAction = RuleAction.DenyBlocked;
                    result.ShouldAutoEject = false;
                    return result;
                }

                // Read-Only Mode
                if (currentPolicy.ReadOnlyRemovable && device.IsRemovableMedia)
                {
                    result.DisplayBadge = "READ-ONLY";
                    result.EvaluationReason = "Removable storage write-protection policy applies.";
                    result.RecommendedAction = RuleAction.ReadOnly;
                    result.ShouldAutoEject = false;
                    return result;
                }
            }

            return result;
        }
    }
}
