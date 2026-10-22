// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Device Installation Policy Backend
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using Microsoft.Win32;
using UsbShieldPro.Core;
using UsbShieldPro.Models;
using UsbShieldPro.Persistence;

namespace UsbShieldPro.Windows
{
    /// <summary>
    /// Manages Windows Device Installation Restrictions safely.
    /// Invariant: Generic DiskDrive setup class is NEVER denied.
    /// Invariant: Allocates sequential numbered values without index collision.
    /// Invariant: Preserves pre-existing administrator/GPO configurations.
    /// </summary>
    public static class DeviceInstallationPolicyBackend
    {
        public static bool CheckForPreExistingPolicies(out string detectedPolicySummary)
        {
            detectedPolicySummary = null;
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var subKey = baseKey.OpenSubKey(WindowsSecurityConstants.DeviceInstallRestrictionsPath, false))
                {
                    if (subKey != null)
                    {
                        var names = subKey.GetValueNames();
                        if (names.Length > 0)
                        {
                            detectedPolicySummary = "DeviceInstall\\Restrictions contains existing values: " + string.Join(", ", names);
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public static bool EnableLayeredEvaluation(bool enable, out string error)
        {
            if (enable)
            {
                return RegistryTransactionHelper.SetDwordValue(
                    RegistryHive.LocalMachine,
                    WindowsSecurityConstants.DeviceInstallRestrictionsPath,
                    WindowsSecurityConstants.ValueAllowDenyLayered,
                    1,
                    "Enable Device Installation layered evaluation (AllowDenyLayered)",
                    out error);
            }
            else
            {
                return RegistryTransactionHelper.DeleteValue(
                    RegistryHive.LocalMachine,
                    WindowsSecurityConstants.DeviceInstallRestrictionsPath,
                    WindowsSecurityConstants.ValueAllowDenyLayered,
                    "Remove AllowDenyLayered",
                    out error);
            }
        }

        public static bool SetStrictRemovableLockdown(bool enable, out string error)
        {
            // Strict Mode only: DenyRemovableDevices = 1
            if (enable)
            {
                return RegistryTransactionHelper.SetDwordValue(
                    RegistryHive.LocalMachine,
                    WindowsSecurityConstants.DeviceInstallRestrictionsPath,
                    WindowsSecurityConstants.ValueDenyRemovableDevices,
                    1,
                    "Enforce strict removable device installation lockdown (DenyRemovableDevices)",
                    out error);
            }
            else
            {
                return RegistryTransactionHelper.DeleteValue(
                    RegistryHive.LocalMachine,
                    WindowsSecurityConstants.DeviceInstallRestrictionsPath,
                    WindowsSecurityConstants.ValueDenyRemovableDevices,
                    "Remove DenyRemovableDevices",
                    out error);
            }
        }

        /// <summary>
        /// Adds a trusted Device Instance ID to AllowInstanceIDs without index collision.
        /// Also asserts the required AllowInstanceIDs enable DWORD in Restrictions (SEC-02).
        /// </summary>
        public static bool AddAllowedDeviceInstanceId(string deviceInstanceId, out int allocatedIndex, out string error)
        {
            allocatedIndex = -1;
            string enableErr;
            if (!RegistryTransactionHelper.SetDwordValue(
                RegistryHive.LocalMachine,
                WindowsSecurityConstants.DeviceInstallRestrictionsPath,
                WindowsSecurityConstants.ValueAllowInstanceIDs,
                1,
                "Enable AllowInstanceIDs policy DWORD",
                out enableErr))
            {
                error = "Failed to set AllowInstanceIDs enable DWORD: " + enableErr;
                return false;
            }

            return AddSequentialAllowedEntry(
                WindowsSecurityConstants.SubkeyAllowInstanceIDs,
                deviceInstanceId,
                "Allow device instance " + deviceInstanceId,
                out allocatedIndex,
                out error);
        }

        /// <summary>
        /// Removes a specific allocated device instance entry without affecting other entries.
        /// Cleans up AllowInstanceIDs enable DWORD if no entries remain (SEC-02).
        /// </summary>
        public static bool RemoveAllowedDeviceInstanceId(int allocatedIndex, out string error)
        {
            error = null;
            string subkeyPath = WindowsSecurityConstants.DeviceInstallRestrictionsPath + "\\" + WindowsSecurityConstants.SubkeyAllowInstanceIDs;
            bool ok = RegistryTransactionHelper.DeleteValue(
                RegistryHive.LocalMachine,
                subkeyPath,
                allocatedIndex.ToString(),
                "Remove allowed device instance at index " + allocatedIndex,
                out error);

            if (ok)
            {
                CheckAndCleanAllowEnableDword(subkeyPath, WindowsSecurityConstants.ValueAllowInstanceIDs);
            }
            return ok;
        }

        /// <summary>
        /// Adds a broader Hardware ID to AllowDeviceIDs without index collision.
        /// Also asserts the required AllowDeviceIDs enable DWORD in Restrictions (SEC-02).
        /// </summary>
        public static bool AddAllowedDeviceId(string hardwareId, out int allocatedIndex, out string error)
        {
            allocatedIndex = -1;
            string enableErr;
            if (!RegistryTransactionHelper.SetDwordValue(
                RegistryHive.LocalMachine,
                WindowsSecurityConstants.DeviceInstallRestrictionsPath,
                WindowsSecurityConstants.ValueAllowDeviceIDs,
                1,
                "Enable AllowDeviceIDs policy DWORD",
                out enableErr))
            {
                error = "Failed to set AllowDeviceIDs enable DWORD: " + enableErr;
                return false;
            }

            return AddSequentialAllowedEntry(
                WindowsSecurityConstants.SubkeyAllowDeviceIDs,
                hardwareId,
                "Allow hardware ID " + hardwareId,
                out allocatedIndex,
                out error);
        }

        /// <summary>
        /// Removes a specific allocated hardware ID entry without affecting other entries.
        /// Cleans up AllowDeviceIDs enable DWORD if no entries remain (SEC-02).
        /// </summary>
        public static bool RemoveAllowedDeviceId(int allocatedIndex, out string error)
        {
            error = null;
            string subkeyPath = WindowsSecurityConstants.DeviceInstallRestrictionsPath + "\\" + WindowsSecurityConstants.SubkeyAllowDeviceIDs;
            bool ok = RegistryTransactionHelper.DeleteValue(
                RegistryHive.LocalMachine,
                subkeyPath,
                allocatedIndex.ToString(),
                "Remove allowed hardware ID at index " + allocatedIndex,
                out error);

            if (ok)
            {
                CheckAndCleanAllowEnableDword(subkeyPath, WindowsSecurityConstants.ValueAllowDeviceIDs);
            }
            return ok;
        }

        private static void CheckAndCleanAllowEnableDword(string subkeyPath, string enableValueName)
        {
            try
            {
                RegistryHive hive = RegistryTransactionHelper.HiveOverride.HasValue ? RegistryTransactionHelper.HiveOverride.Value : RegistryHive.LocalMachine;
                string rewrittenSubkey = RegistryTransactionHelper.KeyPathRewriter != null ? RegistryTransactionHelper.KeyPathRewriter(subkeyPath) : subkeyPath;
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var subKey = baseKey.OpenSubKey(rewrittenSubkey, false))
                {
                    if (subKey != null && subKey.ValueCount == 0)
                    {
                        string ignoredErr;
                        RegistryTransactionHelper.DeleteValue(
                            RegistryHive.LocalMachine,
                            WindowsSecurityConstants.DeviceInstallRestrictionsPath,
                            enableValueName,
                            "Clean up empty " + enableValueName + " policy DWORD",
                            out ignoredErr);
                    }
                }
            }
            catch { }
        }

        private static bool AddSequentialAllowedEntry(
            string subkeyName,
            string targetValue,
            string description,
            out int allocatedIndex,
            out string error)
        {
            allocatedIndex = -1;
            error = null;
            if (string.IsNullOrEmpty(targetValue))
            {
                error = "Target identifier cannot be empty.";
                return false;
            }

            string subkeyPath = WindowsSecurityConstants.DeviceInstallRestrictionsPath + "\\" + subkeyName;

            try
            {
                // 1. Enumerate existing values to find next sequential integer index
                int nextIndex = 1;
                RegistryHive hive = RegistryTransactionHelper.HiveOverride.HasValue ? RegistryTransactionHelper.HiveOverride.Value : RegistryHive.LocalMachine;
                string rewrittenSubkey = RegistryTransactionHelper.KeyPathRewriter != null ? RegistryTransactionHelper.KeyPathRewriter(subkeyPath) : subkeyPath;
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var subKey = baseKey.OpenSubKey(rewrittenSubkey, false))
                {
                    if (subKey != null)
                    {
                        foreach (string valName in subKey.GetValueNames())
                        {
                            int parsed;
                            if (int.TryParse(valName, out parsed) && parsed >= nextIndex)
                            {
                                nextIndex = parsed + 1;
                            }
                            // If already present, don't duplicate
                            object existing = subKey.GetValue(valName);
                            if (existing != null && string.Equals(existing.ToString(), targetValue, StringComparison.OrdinalIgnoreCase))
                            {
                                allocatedIndex = parsed;
                                return true;
                            }
                        }
                    }
                }

                allocatedIndex = nextIndex;
                string valueName = nextIndex.ToString();

                // 2. Set string value through atomic write-ahead RegistryTransactionHelper
                return RegistryTransactionHelper.SetStringValue(
                    RegistryHive.LocalMachine,
                    subkeyPath,
                    valueName,
                    targetValue,
                    description + " at index " + valueName,
                    out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
