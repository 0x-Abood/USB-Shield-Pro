// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Removable Storage Access Policy Backend
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using Microsoft.Win32;

namespace UsbShieldPro.Windows
{
    /// <summary>
    /// Enforces storage access policies via HKLM\SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices
    /// and HKLM\SYSTEM\CurrentControlSet\Control\StorageDevicePolicies.
    /// All mutations route strictly through the write-ahead journal.
    /// </summary>
    public static class RemovableStoragePolicyBackend
    {
        public static bool SetDenyAll(bool enable, out string error)
        {
            if (enable)
            {
                return RegistryTransactionHelper.SetDwordValue(
                    RegistryHive.LocalMachine,
                    WindowsSecurityConstants.RemovableStoragePoliciesPath,
                    WindowsSecurityConstants.ValueDenyAll,
                    1,
                    "Enforce global Deny_All removable storage policy",
                    out error);
            }
            else
            {
                return RegistryTransactionHelper.DeleteValue(
                    RegistryHive.LocalMachine,
                    WindowsSecurityConstants.RemovableStoragePoliciesPath,
                    WindowsSecurityConstants.ValueDenyAll,
                    "Remove global Deny_All removable storage policy",
                    out error);
            }
        }

        public static bool SetWpdPolicy(bool denyRead, bool denyWrite, out string error)
        {
            error = null;
            string keyPath = WindowsSecurityConstants.RemovableStoragePoliciesPath + "\\" + WindowsSecurityConstants.GuidWindowsPortableDevices;

            if (denyRead)
            {
                if (!RegistryTransactionHelper.SetDwordValue(RegistryHive.LocalMachine, keyPath, WindowsSecurityConstants.ValueDenyRead, 1, "Deny WPD Read access", out error))
                    return false;
            }
            else
            {
                if (!RegistryTransactionHelper.DeleteValue(RegistryHive.LocalMachine, keyPath, WindowsSecurityConstants.ValueDenyRead, "Remove WPD Deny_Read", out error))
                    return false;
            }

            if (denyWrite)
            {
                if (!RegistryTransactionHelper.SetDwordValue(RegistryHive.LocalMachine, keyPath, WindowsSecurityConstants.ValueDenyWrite, 1, "Deny WPD Write access", out error))
                    return false;
            }
            else
            {
                if (!RegistryTransactionHelper.DeleteValue(RegistryHive.LocalMachine, keyPath, WindowsSecurityConstants.ValueDenyWrite, "Remove WPD Deny_Write", out error))
                    return false;
            }

            return true;
        }

        public static bool SetRemovableDiskPolicy(bool denyRead, bool denyWrite, out string error)
        {
            error = null;
            string keyPath = WindowsSecurityConstants.RemovableStoragePoliciesPath + "\\" + WindowsSecurityConstants.GuidRemovableDisks;

            if (denyRead)
            {
                if (!RegistryTransactionHelper.SetDwordValue(RegistryHive.LocalMachine, keyPath, WindowsSecurityConstants.ValueDenyRead, 1, "Deny Removable Disks Read access", out error))
                    return false;
            }
            else
            {
                if (!RegistryTransactionHelper.DeleteValue(RegistryHive.LocalMachine, keyPath, WindowsSecurityConstants.ValueDenyRead, "Remove Removable Disks Deny_Read", out error))
                    return false;
            }

            if (denyWrite)
            {
                if (!RegistryTransactionHelper.SetDwordValue(RegistryHive.LocalMachine, keyPath, WindowsSecurityConstants.ValueDenyWrite, 1, "Deny Removable Disks Write access", out error))
                    return false;
            }
            else
            {
                if (!RegistryTransactionHelper.DeleteValue(RegistryHive.LocalMachine, keyPath, WindowsSecurityConstants.ValueDenyWrite, "Remove Removable Disks Deny_Write", out error))
                    return false;
            }

            return true;
        }

        public static bool SetReadOnlyMode(bool enable, out string error)
        {
            error = null;

            // 1. Removable Disks Class Deny_Write
            string diskKey = WindowsSecurityConstants.RemovableStoragePoliciesPath + "\\" + WindowsSecurityConstants.GuidRemovableDisks;
            if (enable)
            {
                // Clear any lingering Deny_Read from previous BlockRemovableDisks policy (SEC-05)
                if (!RegistryTransactionHelper.DeleteValue(RegistryHive.LocalMachine, diskKey, WindowsSecurityConstants.ValueDenyRead, "Clear Removable Disks Deny_Read (Read-Only)", out error))
                    return false;

                if (!RegistryTransactionHelper.SetDwordValue(RegistryHive.LocalMachine, diskKey, WindowsSecurityConstants.ValueDenyWrite, 1, "Set Removable Disks Deny_Write (Read-Only)", out error))
                    return false;
            }
            else
            {
                if (!RegistryTransactionHelper.DeleteValue(RegistryHive.LocalMachine, diskKey, WindowsSecurityConstants.ValueDenyWrite, "Remove Removable Disks Deny_Write", out error))
                    return false;
            }

            // 2. StorageDevicePolicies WriteProtect Compatibility Layer
            if (enable)
            {
                if (!RegistryTransactionHelper.SetDwordValue(RegistryHive.LocalMachine, WindowsSecurityConstants.StorageDevicePoliciesPath, WindowsSecurityConstants.ValueWriteProtect, 1, "Set StorageDevicePolicies WriteProtect", out error))
                    return false;
            }
            else
            {
                if (!RegistryTransactionHelper.DeleteValue(RegistryHive.LocalMachine, WindowsSecurityConstants.StorageDevicePoliciesPath, WindowsSecurityConstants.ValueWriteProtect, "Remove StorageDevicePolicies WriteProtect", out error))
                    return false;
            }

            return true;
        }

        public static bool SetDriverServiceStart(string serviceKeyPath, int startValue, out string error)
        {
            return RegistryTransactionHelper.SetDwordValue(
                RegistryHive.LocalMachine,
                serviceKeyPath,
                WindowsSecurityConstants.ValueStart,
                startValue,
                "Set driver service " + serviceKeyPath + " Start = " + startValue,
                out error);
        }
    }
}
