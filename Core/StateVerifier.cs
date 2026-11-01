// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Live State Audit & Verification Engine
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using Microsoft.Win32;
using UsbShieldPro.Models;
using UsbShieldPro.Persistence;
using UsbShieldPro.Windows;

namespace UsbShieldPro.Core
{
    /// <summary>
    /// Audits live Windows registry and OS state against desired configuration and baseline journal.
    /// Invariant: Never assume "Protected" merely because a button was clicked.
    /// Invariant: Truthful status badges: AppliedAndVerified, PendingReconnect, PendingRestart, ExternallyOverridden.
    /// </summary>
    public static class StateVerifier
    {
        public static ProtectionStatusReport Verify(ProtectionPolicy desiredPolicy)
        {
            ProtectionStatusReport report = new ProtectionStatusReport();
            report.LastVerifiedUtc = DateTime.UtcNow;

            if (desiredPolicy == null)
            {
                report.OverallState = ActivationState.Unprotected;
                return report;
            }

            bool anyDesired = desiredPolicy.IsAnyProtectionActive();
            if (!anyDesired)
            {
                report.OverallState = ActivationState.Unprotected;
                return report;
            }

            bool allAppliedAndVerified = true;
            bool anyPendingReconnect = false;
            bool anyPendingRestart = false;

            // 1. Audit Global Deny_All
            if (desiredPolicy.BlockAllRemovable)
            {
                int? val = ReadDword(RegistryHive.LocalMachine, WindowsSecurityConstants.RemovableStoragePoliciesPath, WindowsSecurityConstants.ValueDenyAll);
                if (val == 1)
                {
                    report.RemovableStorageState = ActivationState.ClassPolicyConfigured;
                    anyPendingReconnect = true; // Removable storage policies bind upon device reconnect
                }
                else
                {
                    report.RemovableStorageState = ActivationState.ConfigurationConflict;
                    allAppliedAndVerified = false;
                    report.DiagnosticNotes.Add("Expected Deny_All = 1, observed " + (val.HasValue ? val.Value.ToString() : "absent"));
                }
            }

            // 1b. Audit Removable Disks Policy ({53f5630d-b6bf-11d0-94f2-00a0c91efb8b})
            if (desiredPolicy.BlockRemovableDisks && !desiredPolicy.BlockAllRemovable)
            {
                string diskKey = WindowsSecurityConstants.RemovableStoragePoliciesPath + "\\" + WindowsSecurityConstants.GuidRemovableDisks;
                int? r = ReadDword(RegistryHive.LocalMachine, diskKey, WindowsSecurityConstants.ValueDenyRead);
                int? w = ReadDword(RegistryHive.LocalMachine, diskKey, WindowsSecurityConstants.ValueDenyWrite);

                if (r == 1 && w == 1)
                {
                    report.RemovableStorageState = ActivationState.ClassPolicyConfigured;
                    anyPendingReconnect = true;
                }
                else
                {
                    report.RemovableStorageState = ActivationState.ConfigurationConflict;
                    allAppliedAndVerified = false;
                    report.DiagnosticNotes.Add("Expected Removable Disks Deny_Read=1 and Deny_Write=1, observed mismatch.");
                }
            }

            // 2. Audit WPD (Phones/Media)
            if (desiredPolicy.BlockWpdPhonesAndMedia && !desiredPolicy.BlockAllRemovable)
            {
                string wpdKey = WindowsSecurityConstants.RemovableStoragePoliciesPath + "\\" + WindowsSecurityConstants.GuidWindowsPortableDevices;
                int? r = ReadDword(RegistryHive.LocalMachine, wpdKey, WindowsSecurityConstants.ValueDenyRead);
                int? w = ReadDword(RegistryHive.LocalMachine, wpdKey, WindowsSecurityConstants.ValueDenyWrite);

                if (r == 1 && w == 1)
                {
                    report.WpdState = ActivationState.AppliedAndVerified;
                    anyPendingReconnect = true;
                }
                else
                {
                    report.WpdState = ActivationState.ConfigurationConflict;
                    allAppliedAndVerified = false;
                    report.DiagnosticNotes.Add("Expected WPD Deny_Read=1 and Deny_Write=1, observed mismatch.");
                }
            }

            // 3. Audit Read-Only Mode
            if (desiredPolicy.ReadOnlyRemovable && !desiredPolicy.BlockAllRemovable)
            {
                string diskKey = WindowsSecurityConstants.RemovableStoragePoliciesPath + "\\" + WindowsSecurityConstants.GuidRemovableDisks;
                int? dw = ReadDword(RegistryHive.LocalMachine, diskKey, WindowsSecurityConstants.ValueDenyWrite);
                int? wp = ReadDword(RegistryHive.LocalMachine, WindowsSecurityConstants.StorageDevicePoliciesPath, WindowsSecurityConstants.ValueWriteProtect);

                if (dw == 1 || wp == 1)
                {
                    report.ReadOnlyState = ActivationState.AppliedAndVerified;
                    anyPendingReconnect = true;
                }
                else
                {
                    report.ReadOnlyState = ActivationState.ConfigurationConflict;
                    allAppliedAndVerified = false;
                }
            }

            // 4. Audit Hard Driver Block (USBSTOR / UASPSTOR)
            if (desiredPolicy.HardBlockUsbStor)
            {
                int? usbStart = ReadDword(RegistryHive.LocalMachine, WindowsSecurityConstants.ServiceUsbStorPath, WindowsSecurityConstants.ValueStart);
                if (usbStart == WindowsSecurityConstants.ServiceStartDisabled)
                {
                    report.HardBlockState = ActivationState.AppliedAndVerified;
                    anyPendingRestart = true; // Service Start changes often take full effect upon next load/restart
                }
                else
                {
                    report.HardBlockState = ActivationState.ConfigurationConflict;
                    allAppliedAndVerified = false;
                }
            }

            // 5. Audit Strict PnP Lockdown
            if (desiredPolicy.StrictPnpLockdown)
            {
                int? denyRem = ReadDword(RegistryHive.LocalMachine, WindowsSecurityConstants.DeviceInstallRestrictionsPath, WindowsSecurityConstants.ValueDenyRemovableDevices);
                int? layered = ReadDword(RegistryHive.LocalMachine, WindowsSecurityConstants.DeviceInstallRestrictionsPath, WindowsSecurityConstants.ValueAllowDenyLayered);

                if (denyRem == 1 && layered == 1)
                {
                    report.StrictPnpState = ActivationState.AppliedAndVerified;
                }
                else
                {
                    report.StrictPnpState = ActivationState.ConfigurationConflict;
                    allAppliedAndVerified = false;
                }
            }

            // 5b. Audit AutoPlay Guard
            if (desiredPolicy.AutoPlayGuard)
            {
                int? d = ReadDword(AutoPlayPolicyBackend.AutoPlayHandlersHive, AutoPlayPolicyBackend.AutoPlayHandlersKeyPath, WindowsSecurityConstants.ValueDisableAutoplay);
                int? n = ReadDword(AutoPlayPolicyBackend.ExplorerPoliciesHive, AutoPlayPolicyBackend.ExplorerPoliciesKeyPath, WindowsSecurityConstants.ValueNoAutoplayfornonVolume);

                if (d == 1 && n == 1)
                {
                    report.AutoPlayGuardState = ActivationState.AppliedAndVerified;
                }
                else
                {
                    report.AutoPlayGuardState = ActivationState.ConfigurationConflict;
                    allAppliedAndVerified = false;
                    report.DiagnosticNotes.Add("Expected AutoPlay Guard DisableAutoplay=1 and NoAutoplayfornonVolume=1, observed mismatch.");
                }
            }

            // 6. Final Evaluation
            if (!allAppliedAndVerified)
            {
                report.OverallState = ActivationState.ConfigurationConflict;
            }
            else if (anyPendingRestart)
            {
                report.OverallState = ActivationState.PendingRestart;
                report.PendingRestartRequired = true;
            }
            else if (desiredPolicy.BlockAllRemovable || desiredPolicy.BlockRemovableDisks)
            {
                report.OverallState = ActivationState.ClassPolicyConfigured;
            }
            else if (desiredPolicy.StrictPnpLockdown)
            {
                report.OverallState = ActivationState.StrictInstallationControl;
            }
            else if (desiredPolicy.WhitelistMode)
            {
                report.OverallState = ActivationState.TrustedOnlyReactive;
            }
            else if (desiredPolicy.BlockFlashDrivesOnlyReactive)
            {
                report.OverallState = ActivationState.ReactiveMonitoringActive;
            }
            else if (anyPendingReconnect)
            {
                report.OverallState = ActivationState.PendingReconnect;
            }
            else if (desiredPolicy.AutoPlayGuard)
            {
                report.OverallState = ActivationState.AppliedAndVerified;
            }
            else
            {
                report.OverallState = ActivationState.AppliedAndVerified;
            }

            return report;
        }

        private static int? ReadDword(RegistryHive hive, string keyPath, string valueName)
        {
            if (RegistryTransactionHelper.HiveOverride.HasValue) hive = RegistryTransactionHelper.HiveOverride.Value;
            if (RegistryTransactionHelper.KeyPathRewriter != null) keyPath = RegistryTransactionHelper.KeyPathRewriter(keyPath);
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var subKey = baseKey.OpenSubKey(keyPath, false))
                {
                    if (subKey != null)
                    {
                        object val = subKey.GetValue(valueName);
                        if (val != null)
                        {
                            return Convert.ToInt32(val);
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
