// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Protection Status Verification Report
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace UsbShieldPro.Models
{
    /// <summary>
    /// Comprehensive audit report of the verified, live Windows OS state.
    /// Distinguishes Desired vs Configured vs Observed.
    /// </summary>
    public class ProtectionStatusReport
    {
        public ActivationState OverallState { get; set; }

        public ActivationState RemovableStorageState { get; set; }
        public ActivationState WpdState { get; set; }
        public ActivationState ReadOnlyState { get; set; }
        public ActivationState HardBlockState { get; set; }
        public ActivationState WhitelistState { get; set; }
        public ActivationState StrictPnpState { get; set; }
        public ActivationState AutoPlayGuardState { get; set; }

        public bool IsExternallyOverridden { get; set; }
        public string ExternalPolicyDetails { get; set; }

        public int PendingReconnectDevicesCount { get; set; }
        public bool PendingRestartRequired { get; set; }

        public DateTime LastVerifiedUtc { get; set; }
        public List<string> DiagnosticNotes { get; set; }

        public ProtectionStatusReport()
        {
            OverallState = ActivationState.Unprotected;
            RemovableStorageState = ActivationState.Unprotected;
            WpdState = ActivationState.Unprotected;
            ReadOnlyState = ActivationState.Unprotected;
            HardBlockState = ActivationState.Unprotected;
            WhitelistState = ActivationState.Unprotected;
            StrictPnpState = ActivationState.Unprotected;
            AutoPlayGuardState = ActivationState.Unprotected;
            LastVerifiedUtc = DateTime.UtcNow;
            DiagnosticNotes = new List<string>();
        }

        public bool IsServiceDegraded { get; set; }
        public string ServiceDegradedReason { get; set; }

        public string GetDisplayBadgeText()
        {
            if (IsServiceDegraded)
            {
                return "SERVICE DEGRADED";
            }

            switch (OverallState)
            {
                case ActivationState.AppliedAndVerified:
                    return "PROTECTED (VERIFIED)";
                case ActivationState.PendingReconnect:
                    return "RECONNECT REQUIRED";
                case ActivationState.PendingRestart:
                    return "RESTART REQUIRED";
                case ActivationState.TemporaryAccessActive:
                    return "TEMPORARY UNLOCK ACTIVE";
                case ActivationState.PendingRelock:
                    return "PENDING RE-LOCK";
                case ActivationState.ExternallyOverridden:
                    return "MANAGED BY WINDOWS POLICY";
                case ActivationState.ConfigurationConflict:
                    return "CONFIGURATION CONFLICT";
                case ActivationState.ClassPolicyConfigured:
                case ActivationState.ClassPolicyActive:
                    return "CONFIGURED — RECONNECT REQUIRED";
                case ActivationState.TrustedOnlyReactive:
                    return "TRUSTED ONLY — AUTO-EJECT";
                case ActivationState.ReactiveMonitoringActive:
                    return "REACTIVE MONITORING ACTIVE";
                case ActivationState.StrictInstallationControl:
                    return "STRICT PNP CONTROL";
                case ActivationState.RecoveryRequired:
                    return "RECOVERY REQUIRED";
                case ActivationState.Failed:
                    return "OPERATION FAILED";
                default:
                    return "UNPROTECTED";
            }
        }
    }
}
