// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Central Protection Orchestration Engine
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using UsbShieldPro.Models;
using UsbShieldPro.Windows;

namespace UsbShieldPro.Core
{
    /// <summary>
    /// Orchestrates protection policies across Windows policy backends.
    /// Invariant: Uses least invasive mechanisms.
    /// Invariant: Prevents incompatible policy combinations before writing.
    /// Invariant: Atomic execution with compensating rollback on partial failure.
    /// </summary>
    public static class ProtectionEngine
    {
        private static Func<ProtectionPolicy, List<string>> _policyApplierSeam;
        private static Func<int, string> _faultInjectionStepSeam;

        public static Func<ProtectionPolicy, List<string>> PolicyApplierSeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _policyApplierSeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _policyApplierSeam = null; return; } _policyApplierSeam = value; }
        }

        public static Func<int, string> FaultInjectionStepSeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _faultInjectionStepSeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _faultInjectionStepSeam = null; return; } _faultInjectionStepSeam = value; }
        }

        public static void ResetSeams()
        {
            _policyApplierSeam = null;
            _faultInjectionStepSeam = null;
        }

        public static bool ApplyPolicy(ProtectionPolicy policy, out List<string> errors)
        {
            return ApplyPolicy(policy, null, out errors);
        }

        public static bool ApplyPolicy(ProtectionPolicy policy, ProtectionPolicy previousPolicy, out List<string> errors)
        {
            errors = new List<string>();
            if (policy == null)
            {
                errors.Add("Policy cannot be null.");
                return false;
            }

            // ── Incompatibility Guard ────────────────────────────────────────
            // Invariant: Hard driver block (USBSTOR Start=4) prevents ALL mass storage,
            // including trusted devices. Do not enable if WhitelistMode is active.
            if (policy.HardBlockUsbStor && policy.WhitelistMode)
            {
                errors.Add("Hard driver block (USBSTOR Start=4) is incompatible with Whitelist Mode because it completely disables driver loading.");
                return false;
            }

            if (PolicyApplierSeam != null)
            {
                errors = PolicyApplierSeam(policy) ?? new List<string>();
                return errors.Count == 0;
            }

            if (previousPolicy == null)
            {
                previousPolicy = Persistence.SettingsRepository.LoadPolicy() ?? new ProtectionPolicy();
            }

            using (var tx = RegistryTransactionHelper.BeginTransaction())
            {
                string err = null;
                bool success = true;

                // 1. Global Deny_All (Master Lock)
                if (FaultInjectionStepSeam != null && (err = FaultInjectionStepSeam(1)) != null)
                {
                    errors.Add("Deny_All fault injected: " + err);
                    success = false;
                }
                else
                {
                    if (policy.BlockAllRemovable)
                    {
                        if (!RemovableStoragePolicyBackend.SetDenyAll(true, out err))
                        {
                            errors.Add("Deny_All error: " + err);
                            success = false;
                        }
                    }
                    else
                    {
                        if (!RemovableStoragePolicyBackend.SetDenyAll(false, out err))
                        {
                            errors.Add("Deny_All removal error: " + err);
                            success = false;
                        }
                    }
                }

                // 2. Removable Disks (Class GUID {53f5630d-b6bf-11d0-94f2-00a0c91efb8b})
                if (success)
                {
                    if (FaultInjectionStepSeam != null && (err = FaultInjectionStepSeam(2)) != null)
                    {
                        errors.Add("Removable Disks fault injected: " + err);
                        success = false;
                    }
                    else
                    {
                        if (policy.BlockRemovableDisks && !policy.BlockAllRemovable)
                        {
                            if (!RemovableStoragePolicyBackend.SetRemovableDiskPolicy(true, true, out err))
                            {
                                errors.Add("Removable Disks block error: " + err);
                                success = false;
                            }
                        }
                        else if (!policy.BlockAllRemovable && !policy.ReadOnlyRemovable)
                        {
                            if (!RemovableStoragePolicyBackend.SetRemovableDiskPolicy(false, false, out err))
                            {
                                errors.Add("Removable Disks unblock error: " + err);
                                success = false;
                            }
                        }
                    }
                }

                // 3. Windows Portable Devices (Phones & Portable Media Devices via MTP/PTP)
                if (success)
                {
                    if (FaultInjectionStepSeam != null && (err = FaultInjectionStepSeam(3)) != null)
                    {
                        errors.Add("WPD fault injected: " + err);
                        success = false;
                    }
                    else
                    {
                        if (policy.BlockWpdPhonesAndMedia && !policy.BlockAllRemovable)
                        {
                            if (!RemovableStoragePolicyBackend.SetWpdPolicy(true, true, out err))
                            {
                                errors.Add("WPD block error: " + err);
                                success = false;
                            }
                        }
                        else if (!policy.BlockAllRemovable)
                        {
                            if (!RemovableStoragePolicyBackend.SetWpdPolicy(false, false, out err))
                            {
                                errors.Add("WPD unblock error: " + err);
                                success = false;
                            }
                        }
                    }
                }

                // 4. Read-Only Mode (Deny Write, Allow Read)
                if (success)
                {
                    if (FaultInjectionStepSeam != null && (err = FaultInjectionStepSeam(4)) != null)
                    {
                        errors.Add("Read-Only fault injected: " + err);
                        success = false;
                    }
                    else
                    {
                        if (policy.ReadOnlyRemovable && !policy.BlockAllRemovable)
                        {
                            if (!RemovableStoragePolicyBackend.SetReadOnlyMode(true, out err))
                            {
                                errors.Add("Read-Only mode error: " + err);
                                success = false;
                            }
                        }
                        else if (!policy.BlockAllRemovable && !policy.BlockRemovableDisks)
                        {
                            if (!RemovableStoragePolicyBackend.SetReadOnlyMode(false, out err))
                            {
                                errors.Add("Read-Only removal error: " + err);
                                success = false;
                            }
                        }
                    }
                }

                // 5. Advanced Strict PnP Lockdown (DenyRemovableDevices + AllowDenyLayered)
                if (success)
                {
                    if (FaultInjectionStepSeam != null && (err = FaultInjectionStepSeam(5)) != null)
                    {
                        errors.Add("Strict Lockdown fault injected: " + err);
                        success = false;
                    }
                    else
                    {
                        if (policy.StrictPnpLockdown)
                        {
                            if (!DeviceInstallationPolicyBackend.EnableLayeredEvaluation(true, out err) ||
                                !DeviceInstallationPolicyBackend.SetStrictRemovableLockdown(true, out err))
                            {
                                errors.Add("Strict lockdown error: " + err);
                                success = false;
                            }
                        }
                        else
                        {
                            if (!DeviceInstallationPolicyBackend.SetStrictRemovableLockdown(false, out err))
                            {
                                errors.Add("Strict lockdown removal error: " + err);
                                success = false;
                            }
                        }
                    }
                }

                // 6. Optional Hard Driver Block (USBSTOR / UASPSTOR)
                if (success)
                {
                    if (FaultInjectionStepSeam != null && (err = FaultInjectionStepSeam(6)) != null)
                    {
                        errors.Add("Hard Block fault injected: " + err);
                        success = false;
                    }
                    else
                    {
                        if (policy.HardBlockUsbStor)
                        {
                            if (!RemovableStoragePolicyBackend.SetDriverServiceStart(WindowsSecurityConstants.ServiceUsbStorPath, WindowsSecurityConstants.ServiceStartDisabled, out err) ||
                                !RemovableStoragePolicyBackend.SetDriverServiceStart(WindowsSecurityConstants.ServiceUaspStorPath, WindowsSecurityConstants.ServiceStartDisabled, out err))
                            {
                                errors.Add("USBSTOR service disable error: " + err);
                                success = false;
                            }
                        }
                        else
                        {
                            // Invariant: Do NOT touch USBSTOR or UASPSTOR unless HardBlockUsbStor was previously active
                            // or has an active Shield-applied entry in JournalRepository.
                            bool wasHardBlocked = (previousPolicy != null && previousPolicy.HardBlockUsbStor);
                            RegistryOriginalValue usbStorBaseline = Persistence.JournalRepository.GetBaselineEntry("HKEY_LOCAL_MACHINE", WindowsSecurityConstants.ServiceUsbStorPath, WindowsSecurityConstants.ValueStart);
                            RegistryOriginalValue uaspStorBaseline = Persistence.JournalRepository.GetBaselineEntry("HKEY_LOCAL_MACHINE", WindowsSecurityConstants.ServiceUaspStorPath, WindowsSecurityConstants.ValueStart);

                            if (wasHardBlocked || usbStorBaseline != null || uaspStorBaseline != null)
                            {
                                int usbStorTarget = (usbStorBaseline != null && usbStorBaseline.ExistedBefore && usbStorBaseline.OriginalData != null)
                                    ? Convert.ToInt32(usbStorBaseline.OriginalData)
                                    : WindowsSecurityConstants.ServiceStartManual;

                                int uaspStorTarget = (uaspStorBaseline != null && uaspStorBaseline.ExistedBefore && uaspStorBaseline.OriginalData != null)
                                    ? Convert.ToInt32(uaspStorBaseline.OriginalData)
                                    : WindowsSecurityConstants.ServiceStartManual;

                                if (!RemovableStoragePolicyBackend.SetDriverServiceStart(WindowsSecurityConstants.ServiceUsbStorPath, usbStorTarget, out err) ||
                                    !RemovableStoragePolicyBackend.SetDriverServiceStart(WindowsSecurityConstants.ServiceUaspStorPath, uaspStorTarget, out err))
                                {
                                    errors.Add("USBSTOR service restore error: " + err);
                                    success = false;
                                }
                            }
                        }
                    }
                }

                // 7. AutoPlay Guard (DisableAutoplay master switch + NoAutoplayfornonVolume)
                if (success)
                {
                    if (FaultInjectionStepSeam != null && (err = FaultInjectionStepSeam(7)) != null)
                    {
                        errors.Add("AutoPlay Guard fault injected: " + err);
                        success = false;
                    }
                    else
                    {
                        if (policy.AutoPlayGuard)
                        {
                            if (!AutoPlayPolicyBackend.SetAutoPlayGuard(true, out err))
                            {
                                errors.Add("AutoPlay Guard enable error: " + err);
                                success = false;
                            }
                        }
                        else
                        {
                            if (!AutoPlayPolicyBackend.SetAutoPlayGuard(false, out err))
                            {
                                errors.Add("AutoPlay Guard restore error: " + err);
                                success = false;
                            }
                        }
                    }
                }

                // If any step failed, execute exact snapshot rollback in reverse order
                if (!success)
                {
                    List<string> rollbackErrors;
                    tx.Rollback(out rollbackErrors);
                    if (rollbackErrors != null && rollbackErrors.Count > 0)
                    {
                        errors.AddRange(rollbackErrors);
                    }

                    return false;
                }

                tx.Commit();
                return true;
            }
        }

        public static bool RestoreBaseline(out List<string> errors)
        {
            return RecoveryManager.RestorePreShieldState(out errors);
        }
    }
}
