// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — AutoPlay Guard Policy Backend
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
    /// Enforces AutoPlay Guard policies to disable Windows AutoPlay for newly connected
    /// media and portable devices, with guaranteed write-ahead baseline capture and exact restoration.
    /// Invariant: Captures baseline ONCE only.
    /// Invariant: Restores exact previous state (absent => delete created value; existing => restore exact previous data).
    /// Invariant: Never modifies USBSTOR, uaspstor, RemovableStorageDevices, or DeviceInstall restrictions.
    /// Invariant: Preserves existing NoDriveTypeAutoRun administrator policy.
    /// Invariant: Does NOT kill Explorer or reboot.
    /// </summary>
    public static class AutoPlayPolicyBackend
    {
        // Configurable hives and key paths (defaults to production constants, overridable in tests)
        public static RegistryHive AutoPlayHandlersHive = RegistryHive.CurrentUser;
        public static string AutoPlayHandlersKeyPath = WindowsSecurityConstants.AutoPlayHandlersUserPath;

        public static RegistryHive ExplorerPoliciesHive = RegistryHive.LocalMachine;
        public static string ExplorerPoliciesKeyPath = WindowsSecurityConstants.ExplorerPoliciesPath;

        // Test seams (active only when DeviceEnforcementLifecycleManager.IsTestMode == true)
        internal static Func<bool, bool> ApplySeam = null;
        internal static Func<bool, bool> RestoreSeam = null;

        public static void ResetSeams()
        {
            ApplySeam = null;
            RestoreSeam = null;
            AutoPlayHandlersHive = RegistryHive.CurrentUser;
            AutoPlayHandlersKeyPath = WindowsSecurityConstants.AutoPlayHandlersUserPath;
            ExplorerPoliciesHive = RegistryHive.LocalMachine;
            ExplorerPoliciesKeyPath = WindowsSecurityConstants.ExplorerPoliciesPath;
        }

        /// <summary>
        /// Applies or restores AutoPlay Guard policy.
        /// When enable == true:
        ///   1. Sets HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers\DisableAutoplay = 1 (REG_DWORD)
        ///   2. Sets HKLM\SOFTWARE\Policies\Microsoft\Windows\Explorer\NoAutoplayfornonVolume = 1 (REG_DWORD)
        ///   3. Preserves existing NoDriveTypeAutoRun policy without modification.
        /// When enable == false:
        ///   Restores the exact pre-shield baseline for both values.
        /// </summary>
        public static bool SetAutoPlayGuard(bool enable, out string error)
        {
            error = null;
            if (DeviceEnforcementLifecycleManager.IsTestMode && ApplySeam != null)
            {
                if (!ApplySeam(enable))
                {
                    error = "Seam forced AutoPlay Guard failure";
                    return false;
                }
                return true;
            }

            if (enable)
            {
                // 1. HKCU DisableAutoplay = 1
                if (!RegistryTransactionHelper.SetDwordValue(
                    AutoPlayHandlersHive,
                    AutoPlayHandlersKeyPath,
                    WindowsSecurityConstants.ValueDisableAutoplay,
                    1,
                    "Enable AutoPlay Guard - Master DisableAutoplay switch",
                    out error))
                {
                    return false;
                }

                // 2. HKLM NoAutoplayfornonVolume = 1
                if (!RegistryTransactionHelper.SetDwordValue(
                    ExplorerPoliciesHive,
                    ExplorerPoliciesKeyPath,
                    WindowsSecurityConstants.ValueNoAutoplayfornonVolume,
                    1,
                    "Enable AutoPlay Guard - NoAutoplayfornonVolume policy",
                    out error))
                {
                    return false;
                }

                // 3. Preserve NoDriveTypeAutoRun if already present (do not overwrite)
                // (No mutation performed on NoDriveTypeAutoRun)

                return true;
            }
            else
            {
                // Restore exact original baseline for both values
                return RestoreAutoPlayBaseline(out error);
            }
        }

        /// <summary>
        /// Restores the exact pre-shield baseline state for AutoPlay values.
        /// - If absent before: deletes only the value created by USB Shield Pro.
        /// - If existed before: restores the exact previous DWORD value.
        /// </summary>
        public static bool RestoreAutoPlayBaseline(out string error)
        {
            error = null;
            if (DeviceEnforcementLifecycleManager.IsTestMode && RestoreSeam != null)
            {
                if (!RestoreSeam(false))
                {
                    error = "Seam forced AutoPlay Guard restore failure";
                    return false;
                }
                return true;
            }

            List<string> errors = new List<string>();

            // 1. Restore HKCU DisableAutoplay
            string err1;
            if (!RestoreSingleValue(
                AutoPlayHandlersHive,
                AutoPlayHandlersKeyPath,
                WindowsSecurityConstants.ValueDisableAutoplay,
                "Restore original AutoPlay Handlers DisableAutoplay state",
                out err1))
            {
                errors.Add(err1);
            }

            // 2. Restore HKLM NoAutoplayfornonVolume
            string err2;
            if (!RestoreSingleValue(
                ExplorerPoliciesHive,
                ExplorerPoliciesKeyPath,
                WindowsSecurityConstants.ValueNoAutoplayfornonVolume,
                "Restore original Explorer NoAutoplayfornonVolume state",
                out err2))
            {
                errors.Add(err2);
            }

            if (errors.Count > 0)
            {
                error = string.Join("; ", errors.ToArray());
                return false;
            }
            return true;
        }

        private static bool RestoreSingleValue(
            RegistryHive hive,
            string keyPath,
            string valueName,
            string operationDesc,
            out string error)
        {
            error = null;
            string hiveStr = hive == RegistryHive.CurrentUser ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE";
            RegistryOriginalValue baseline = JournalRepository.GetBaselineEntry(hiveStr, keyPath, valueName);

            if (baseline == null)
            {
                // No baseline recorded, value was never modified by AutoPlay Guard
                return true;
            }

            if (!baseline.ExistedBefore)
            {
                // Value was absent before => delete only the created value
                return RegistryTransactionHelper.DeleteValue(hive, keyPath, valueName, operationDesc, out error);
            }
            else
            {
                // Value existed before => restore exact previous data
                if (string.Equals(baseline.ValueKind, "DWord", StringComparison.OrdinalIgnoreCase))
                {
                    int origDword = Convert.ToInt32(baseline.OriginalData);
                    return RegistryTransactionHelper.SetDwordValue(hive, keyPath, valueName, origDword, operationDesc, out error);
                }
                else
                {
                    string origStr = baseline.OriginalData != null ? baseline.OriginalData.ToString() : "";
                    return RegistryTransactionHelper.SetStringValue(hive, keyPath, valueName, origStr, operationDesc, out error);
                }
            }
        }

        /// <summary>
        /// Reads back the live registry state for AutoPlay values.
        /// Returns true only if both DisableAutoplay == 1 and NoAutoplayfornonVolume == 1.
        /// </summary>
        public static bool IsAutoPlayGuardActive(out int? disableAutoplayVal, out int? noAutoplayForNonVolumeVal)
        {
            disableAutoplayVal = ReadDword(AutoPlayHandlersHive, AutoPlayHandlersKeyPath, WindowsSecurityConstants.ValueDisableAutoplay);
            noAutoplayForNonVolumeVal = ReadDword(ExplorerPoliciesHive, ExplorerPoliciesKeyPath, WindowsSecurityConstants.ValueNoAutoplayfornonVolume);

            return disableAutoplayVal == 1 && noAutoplayForNonVolumeVal == 1;
        }

        private static int? ReadDword(RegistryHive hive, string keyPath, string valueName)
        {
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
