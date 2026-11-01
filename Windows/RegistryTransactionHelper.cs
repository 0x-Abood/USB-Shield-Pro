// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Safe Write-Ahead Registry Mutator
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
    /// Executes registry modifications with guaranteed write-ahead journal recording,
    /// zero-lag recovery generation, and post-write verification.
    /// Invariant: Original state is recorded ONLY ONCE and never overwritten by intermediate mutations.
    /// </summary>
    public static class RegistryTransactionHelper
    {
        private static RegistryHive? _hiveOverride = null;
        private static Func<string, string> _keyPathRewriter = null;
        private static Func<string, string> _faultInjectionMutationSeam = null;
        private static Func<string, string> _faultInjectionRollbackSeam = null;

        public static RegistryHive? HiveOverride
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _hiveOverride : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _hiveOverride = null; return; } _hiveOverride = value; }
        }

        public static Func<string, string> KeyPathRewriter
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _keyPathRewriter : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _keyPathRewriter = null; return; } _keyPathRewriter = value; }
        }

        public static Func<string, string> FaultInjectionMutationSeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _faultInjectionMutationSeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _faultInjectionMutationSeam = null; return; } _faultInjectionMutationSeam = value; }
        }

        public static Func<string, string> FaultInjectionRollbackSeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _faultInjectionRollbackSeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _faultInjectionRollbackSeam = null; return; } _faultInjectionRollbackSeam = value; }
        }

        public static void ResetSeams()
        {
            _hiveOverride = null;
            _keyPathRewriter = null;
            _faultInjectionMutationSeam = null;
            _faultInjectionRollbackSeam = null;
        }

        [ThreadStatic]
        private static RegistryTransactionScope _activeScope;

        public static RegistryTransactionScope ActiveScope
        {
            get { return _activeScope; }
        }

        public static RegistryTransactionScope BeginTransaction()
        {
            var scope = new RegistryTransactionScope();
            _activeScope = scope;
            return scope;
        }

        internal static void EndTransaction(RegistryTransactionScope scope)
        {
            if (_activeScope == scope)
            {
                _activeScope = null;
            }
        }

        public static bool SetDwordValue(
            RegistryHive hive,
            string keyPath,
            string valueName,
            int targetValue,
            string operationDescription,
            out string error)
        {
            if (HiveOverride.HasValue) hive = HiveOverride.Value;
            if (KeyPathRewriter != null) keyPath = KeyPathRewriter(keyPath);
            error = null;
            try
            {
                // 1. Discover current state
                bool existedBefore = false;
                object originalData = null;
                string valueKindStr = "DWord";

                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var subKey = baseKey.OpenSubKey(keyPath, false))
                {
                    if (subKey != null)
                    {
                        object currentVal = subKey.GetValue(valueName);
                        if (currentVal != null)
                        {
                            existedBefore = true;
                            originalData = currentVal;
                            valueKindStr = subKey.GetValueKind(valueName).ToString();
                        }
                    }
                }

                // 2. Record Baseline in Journal (Ignored if already captured previously)
                string hiveStr = hive == RegistryHive.CurrentUser ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE";
                RegistryOriginalValue baseline = JournalRepository.RecordOriginalBaseline(
                    hiveStr,
                    keyPath,
                    valueName,
                    valueKindStr,
                    existedBefore,
                    originalData);

                // 3. Prepare Operation in Journal
                JournalOperation op = JournalRepository.PrepareOperation(operationDescription, baseline);

                // 3b. Record Pre-Mutation in Active Transaction Scope (Atomic Snapshot)
                if (_activeScope != null)
                {
                    _activeScope.RecordPreMutation(hive, keyPath, valueName, existedBefore, originalData, valueKindStr, op);
                }

                // Fault Injection check
                if (FaultInjectionMutationSeam != null)
                {
                    string fiErr = FaultInjectionMutationSeam(valueName);
                    if (!string.IsNullOrEmpty(fiErr))
                    {
                        error = fiErr;
                        JournalRepository.CommitOperation(op, JournalOperationStatus.Failed, error);
                        return false;
                    }
                }

                // 4. Atomic write-ahead journal committed before mutating Windows state (Zero operation lag)

                // 5. Apply Registry Change
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var subKey = baseKey.CreateSubKey(keyPath))
                {
                    if (subKey == null)
                    {
                        error = "Failed to create or open subkey: " + keyPath;
                        JournalRepository.CommitOperation(op, JournalOperationStatus.Failed, error);
                        return false;
                    }

                    subKey.SetValue(valueName, targetValue, RegistryValueKind.DWord);
                }

                // 6. Verify Result
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var subKey = baseKey.OpenSubKey(keyPath, false))
                {
                    if (subKey == null)
                    {
                        error = "Subkey missing after write: " + keyPath;
                        JournalRepository.CommitOperation(op, JournalOperationStatus.Failed, error);
                        return false;
                    }

                    object readBack = subKey.GetValue(valueName);
                    if (readBack == null || Convert.ToInt32(readBack) != targetValue)
                    {
                        error = "Verification mismatch: expected " + targetValue + ", read " + readBack;
                        JournalRepository.CommitOperation(op, JournalOperationStatus.Failed, error);
                        return false;
                    }
                }

                // 7. Commit Operation as Verified
                JournalRepository.CommitOperation(op, JournalOperationStatus.Verified);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool SetStringValue(
            RegistryHive hive,
            string keyPath,
            string valueName,
            string targetValue,
            string operationDescription,
            out string error)
        {
            if (HiveOverride.HasValue) hive = HiveOverride.Value;
            if (KeyPathRewriter != null) keyPath = KeyPathRewriter(keyPath);
            error = null;
            try
            {
                // 1. Discover current state
                bool existedBefore = false;
                object originalData = null;
                string valueKindStr = "String";

                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var subKey = baseKey.OpenSubKey(keyPath, false))
                {
                    if (subKey != null)
                    {
                        object currentVal = subKey.GetValue(valueName);
                        if (currentVal != null)
                        {
                            existedBefore = true;
                            originalData = currentVal;
                            valueKindStr = subKey.GetValueKind(valueName).ToString();
                        }
                    }
                }

                // 2. Record Baseline in Journal (Ignored if already captured previously)
                string hiveStr = hive == RegistryHive.CurrentUser ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE";
                RegistryOriginalValue baseline = JournalRepository.RecordOriginalBaseline(
                    hiveStr,
                    keyPath,
                    valueName,
                    valueKindStr,
                    existedBefore,
                    originalData);

                // 3. Prepare Operation in Journal
                JournalOperation op = JournalRepository.PrepareOperation(operationDescription, baseline);

                // 3b. Record Pre-Mutation in Active Transaction Scope (Atomic Snapshot)
                if (_activeScope != null)
                {
                    _activeScope.RecordPreMutation(hive, keyPath, valueName, existedBefore, originalData, valueKindStr, op);
                }

                // Fault Injection check
                if (FaultInjectionMutationSeam != null)
                {
                    string fiErr = FaultInjectionMutationSeam(valueName);
                    if (!string.IsNullOrEmpty(fiErr))
                    {
                        error = fiErr;
                        JournalRepository.CommitOperation(op, JournalOperationStatus.Failed, error);
                        return false;
                    }
                }

                // 4. Atomic write-ahead journal committed before mutating Windows state

                // 5. Apply Registry Change
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var subKey = baseKey.CreateSubKey(keyPath))
                {
                    if (subKey == null)
                    {
                        error = "Failed to create or open subkey: " + keyPath;
                        JournalRepository.CommitOperation(op, JournalOperationStatus.Failed, error);
                        return false;
                    }

                    subKey.SetValue(valueName, targetValue, RegistryValueKind.String);
                }

                // 6. Verify Result
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var subKey = baseKey.OpenSubKey(keyPath, false))
                {
                    if (subKey == null)
                    {
                        error = "Subkey missing after write: " + keyPath;
                        JournalRepository.CommitOperation(op, JournalOperationStatus.Failed, error);
                        return false;
                    }

                    object readBack = subKey.GetValue(valueName);
                    if (readBack == null || !string.Equals(readBack.ToString(), targetValue, StringComparison.OrdinalIgnoreCase))
                    {
                        error = "Verification mismatch: expected " + targetValue + ", read " + readBack;
                        JournalRepository.CommitOperation(op, JournalOperationStatus.Failed, error);
                        return false;
                    }
                }

                // 7. Commit Operation as Verified
                JournalRepository.CommitOperation(op, JournalOperationStatus.Verified);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool DeleteValue(
            RegistryHive hive,
            string keyPath,
            string valueName,
            string operationDescription,
            out string error)
        {
            if (HiveOverride.HasValue) hive = HiveOverride.Value;
            if (KeyPathRewriter != null) keyPath = KeyPathRewriter(keyPath);
            error = null;
            try
            {
                // 1. Discover current state
                bool existed = false;
                object originalData = null;
                string valueKindStr = "DWord";

                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var subKey = baseKey.OpenSubKey(keyPath, false))
                {
                    if (subKey != null)
                    {
                        object currentVal = subKey.GetValue(valueName);
                        if (currentVal != null)
                        {
                            existed = true;
                            originalData = currentVal;
                            valueKindStr = subKey.GetValueKind(valueName).ToString();
                        }
                    }
                }

                if (!existed)
                {
                    // Value doesn't exist, nothing to delete
                    return true;
                }

                // 2. Record Baseline in Journal if not already recorded
                string hiveStr = hive == RegistryHive.CurrentUser ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE";
                RegistryOriginalValue baseline = JournalRepository.RecordOriginalBaseline(
                    hiveStr,
                    keyPath,
                    valueName,
                    valueKindStr,
                    existed,
                    originalData);

                // 3. Prepare Operation
                JournalOperation op = JournalRepository.PrepareOperation(operationDescription, baseline);

                // 3b. Record Pre-Mutation in Active Transaction Scope (Atomic Snapshot)
                if (_activeScope != null)
                {
                    _activeScope.RecordPreMutation(hive, keyPath, valueName, existed, originalData, valueKindStr, op);
                }

                // Fault Injection check
                if (FaultInjectionMutationSeam != null)
                {
                    string fiErr = FaultInjectionMutationSeam(valueName);
                    if (!string.IsNullOrEmpty(fiErr))
                    {
                        error = fiErr;
                        JournalRepository.CommitOperation(op, JournalOperationStatus.Failed, error);
                        return false;
                    }
                }

                // 4. Atomic write-ahead journal committed before mutating Windows state

                // 5. Delete specific value ONLY (Never delete key recursively)
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var subKey = baseKey.OpenSubKey(keyPath, true))
                {
                    if (subKey != null)
                    {
                        subKey.DeleteValue(valueName, false);
                    }
                }

                // 6. Verify Deletion
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var subKey = baseKey.OpenSubKey(keyPath, false))
                {
                    if (subKey != null && subKey.GetValue(valueName) != null)
                    {
                        error = "Value still present after deletion: " + valueName;
                        JournalRepository.CommitOperation(op, JournalOperationStatus.Failed, error);
                        return false;
                    }
                }

                // 7. Commit Operation
                JournalRepository.CommitOperation(op, JournalOperationStatus.Verified);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    /// <summary>
    /// Captures the pre-mutation state of an individual registry target within a transaction scope.
    /// </summary>
    public class RegistryMutationSnapshotEntry
    {
        public RegistryHive Hive { get; set; }
        public string KeyPath { get; set; }
        public string ValueName { get; set; }
        public string ValueKind { get; set; }
        public bool ExistedBefore { get; set; }
        public object OriginalData { get; set; }
        public JournalOperation AssociatedOperation { get; set; }
    }

    /// <summary>
    /// Manages an atomic transaction scope across registry mutations with exact state restoration on rollback.
    /// Invariant: Captures the pre-transaction baseline ONLY ONCE. Intermediate writes never overwrite the snapshot.
    /// Invariant: Rollback restores exact value data, value type, deletes originally absent values, and verifies.
    /// </summary>
    public class RegistryTransactionScope : IDisposable
    {
        private readonly List<RegistryMutationSnapshotEntry> _mutations = new List<RegistryMutationSnapshotEntry>();
        private readonly HashSet<string> _capturedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _committed = false;
        private bool _rolledBack = false;

        public bool IsCommitted { get { return _committed; } }
        public bool IsRolledBack { get { return _rolledBack; } }
        public int MutationCount { get { return _mutations.Count; } }
        public List<RegistryMutationSnapshotEntry> CapturedMutations { get { return _mutations; } }

        internal void RecordPreMutation(RegistryHive hive, string keyPath, string valueName, bool existedBefore, object originalData, string valueKind, JournalOperation op)
        {
            if (_committed || _rolledBack) return;

            string targetKey = hive.ToString() + "\\" + (keyPath ?? "") + "\\" + (valueName ?? "");
            if (_capturedTargets.Contains(targetKey))
            {
                // Invariant: Baseline before transaction begins is captured ONLY ONCE.
                // Intermediate mutations within the same transaction never overwrite pre-transaction state.
                return;
            }

            _capturedTargets.Add(targetKey);
            _mutations.Add(new RegistryMutationSnapshotEntry
            {
                Hive = hive,
                KeyPath = keyPath,
                ValueName = valueName,
                ValueKind = valueKind ?? "DWord",
                ExistedBefore = existedBefore,
                OriginalData = originalData,
                AssociatedOperation = op
            });
        }

        public void Commit()
        {
            _committed = true;
            RegistryTransactionHelper.EndTransaction(this);
        }

        public bool Rollback(out List<string> errors)
        {
            errors = new List<string>();
            _rolledBack = true;
            RegistryTransactionHelper.EndTransaction(this);

            bool allRestored = true;

            // Roll back in reverse chronological order
            for (int i = _mutations.Count - 1; i >= 0; i--)
            {
                var entry = _mutations[i];
                try
                {
                    if (RegistryTransactionHelper.FaultInjectionRollbackSeam != null)
                    {
                        string rFiErr = RegistryTransactionHelper.FaultInjectionRollbackSeam(entry.ValueName);
                        if (!string.IsNullOrEmpty(rFiErr))
                        {
                            allRestored = false;
                            errors.Add("Rollback fault injected for " + entry.ValueName + ": " + rFiErr);
                            continue;
                        }
                    }

                    RegistryHive targetHive = entry.Hive;
                    string targetKeyPath = entry.KeyPath;

                    using (var baseKey = RegistryKey.OpenBaseKey(targetHive, RegistryView.Registry64))
                    {
                        if (!entry.ExistedBefore)
                        {
                            // Value was originally absent: delete value only
                            using (var subKey = baseKey.OpenSubKey(targetKeyPath, true))
                            {
                                if (subKey != null)
                                {
                                    subKey.DeleteValue(entry.ValueName, false);
                                }
                            }

                            // Verify deletion
                            using (var verifyKey = baseKey.OpenSubKey(targetKeyPath, false))
                            {
                                if (verifyKey != null && verifyKey.GetValue(entry.ValueName) != null)
                                {
                                    allRestored = false;
                                    errors.Add("Rollback failed to remove value: " + entry.KeyPath + "\\" + entry.ValueName);
                                }
                            }
                        }
                        else
                        {
                            // Value originally existed: restore exact value and value type
                            using (var subKey = baseKey.CreateSubKey(targetKeyPath))
                            {
                                if (subKey == null)
                                {
                                    allRestored = false;
                                    errors.Add("Rollback failed to open/create subkey: " + entry.KeyPath);
                                    continue;
                                }

                                RegistryValueKind kind = ParseValueKind(entry.ValueKind);
                                subKey.SetValue(entry.ValueName, entry.OriginalData, kind);
                            }

                            // Verify restoration
                            using (var verifyKey = baseKey.OpenSubKey(targetKeyPath, false))
                            {
                                if (verifyKey == null)
                                {
                                    allRestored = false;
                                    errors.Add("Rollback verify failed: subkey missing " + entry.KeyPath);
                                }
                                else
                                {
                                    object val = verifyKey.GetValue(entry.ValueName);
                                    if (val == null || !val.ToString().Equals(entry.OriginalData != null ? entry.OriginalData.ToString() : "", StringComparison.OrdinalIgnoreCase))
                                    {
                                        allRestored = false;
                                        errors.Add("Rollback verify mismatch: " + entry.KeyPath + "\\" + entry.ValueName);
                                    }
                                }
                            }
                        }
                    }

                    // Update write-ahead journal operation status if recorded
                    if (entry.AssociatedOperation != null)
                    {
                        JournalRepository.CommitOperation(entry.AssociatedOperation, JournalOperationStatus.RolledBack, "Rolled back due to transaction failure.");
                    }
                }
                catch (Exception ex)
                {
                    allRestored = false;
                    errors.Add("Rollback exception for " + entry.KeyPath + "\\" + entry.ValueName + ": " + ex.Message);
                }
            }

            return allRestored;
        }

        public void Dispose()
        {
            if (!_committed && !_rolledBack)
            {
                List<string> rollErrors;
                Rollback(out rollErrors);
            }
            RegistryTransactionHelper.EndTransaction(this);
        }

        private static RegistryValueKind ParseValueKind(string kindStr)
        {
            if (string.IsNullOrEmpty(kindStr)) return RegistryValueKind.DWord;
            try
            {
                return (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), kindStr, true);
            }
            catch
            {
                return RegistryValueKind.DWord;
            }
        }
    }
}
