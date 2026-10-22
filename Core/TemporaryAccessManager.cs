// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Persistent Temporary Access Lifecycle Manager
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Web.Script.Serialization;
using UsbShieldPro.Models;
using UsbShieldPro.Persistence;

namespace UsbShieldPro.Core
{
    public enum TemporaryAccessStatus
    {
        Inactive = 0,
        Active = 1,
        RelockPending = 2,
        Relocked = 3,
        RelockFailed = 4
    }

    public class TemporaryAccessState
    {
        public TemporaryAccessStatus Status { get; set; }
        public bool IsActive { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime ExpirationUtc { get; set; }
        public ProtectionPolicy OriginalDesiredPolicy { get; set; }
        public string GenerationId { get; set; }
        public int TotalMinutes { get; set; }
        public string LastRelockError { get; set; }
        public int RelockAttempts { get; set; }
        public DateTime? LastRelockAttemptUtc { get; set; }

        public TemporaryAccessState()
        {
            Status = TemporaryAccessStatus.Inactive;
            IsActive = false;
        }
    }

    /// <summary>
    /// Manages timed temporary unlock with persistent disk state.
    /// Invariant: Resistant to application restarts and system reboots.
    /// Invariant: Never reports temporary unlock as successful before durable lease is committed.
    /// Invariant: Retains visible pending / failed re-lock obligation until observed success.
    /// Invariant: Clock tampering (backwards adjustment) immediately triggers deterministic re-lock.
    /// </summary>
    public static class TemporaryAccessManager
    {
        private static readonly object _syncLock = new object();
        private static TemporaryAccessState _cachedState;

        private static Func<ProtectionPolicy, System.Collections.Generic.List<string>> _policyApplier;
        private static Func<string> _faultInjectionSaveStateSeam;
        private static Func<ProtectionPolicy, string> _faultInjectionRelockSeam;

        public static Func<ProtectionPolicy, System.Collections.Generic.List<string>> PolicyApplier
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _policyApplier : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _policyApplier = null; return; } _policyApplier = value; }
        }

        public static Func<string> FaultInjectionSaveStateSeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _faultInjectionSaveStateSeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _faultInjectionSaveStateSeam = null; return; } _faultInjectionSaveStateSeam = value; }
        }

        public static Func<ProtectionPolicy, string> FaultInjectionRelockSeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _faultInjectionRelockSeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _faultInjectionRelockSeam = null; return; } _faultInjectionRelockSeam = value; }
        }

        public static void ResetSeams()
        {
            _policyApplier = null;
            _faultInjectionSaveStateSeam = null;
            _faultInjectionRelockSeam = null;
        }

        public static bool IsActive
        {
            get
            {
                lock (_syncLock)
                {
                    return PeekRemainingSeconds() > 0;
                }
            }
        }

        private static string GetFilePath()
        {
            return Path.Combine(SecurityAclHelper.GetProgramDataDirectory(), "temp_access.json");
        }

        private static string GetBackupFilePath()
        {
            return Path.Combine(SecurityAclHelper.GetProgramDataDirectory(), "temp_access.bak");
        }

        public static TemporaryAccessState LoadState()
        {
            lock (_syncLock)
            {
                if (_cachedState != null) return _cachedState;

                string path = GetFilePath();
                string bakPath = GetBackupFilePath();
                JavaScriptSerializer serializer = new JavaScriptSerializer();

                // 1. Try Primary File
                if (File.Exists(path))
                {
                    try
                    {
                        string json = File.ReadAllText(path);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            _cachedState = serializer.Deserialize<TemporaryAccessState>(json);
                        }
                    }
                    catch { }
                }

                // 2. Fallback to Backup File if primary missing or corrupted
                if (_cachedState == null && File.Exists(bakPath))
                {
                    try
                    {
                        string bakJson = File.ReadAllText(bakPath);
                        if (!string.IsNullOrWhiteSpace(bakJson))
                        {
                            _cachedState = serializer.Deserialize<TemporaryAccessState>(bakJson);
                        }
                    }
                    catch { }
                }

                // 3. Normalization and backward-compatibility mapping
                if (_cachedState != null)
                {
                    if (_cachedState.Status == TemporaryAccessStatus.Inactive && _cachedState.IsActive)
                    {
                        _cachedState.Status = TemporaryAccessStatus.Active;
                    }
                    else if (_cachedState.Status == TemporaryAccessStatus.Active)
                    {
                        _cachedState.IsActive = true;
                    }
                    else
                    {
                        _cachedState.IsActive = false;
                    }
                    return _cachedState;
                }

                _cachedState = new TemporaryAccessState { Status = TemporaryAccessStatus.Inactive, IsActive = false };
                return _cachedState;
            }
        }

        public static void ResetCache()
        {
            lock (_syncLock)
            {
                _cachedState = null;
            }
        }

        private static bool ApplyPolicyInternal(ProtectionPolicy policy, out string error)
        {
            error = null;
            System.Collections.Generic.List<string> errors;
            if (PolicyApplier != null)
            {
                errors = PolicyApplier(policy);
            }
            else
            {
                ProtectionEngine.ApplyPolicy(policy, out errors);
            }

            if (errors != null && errors.Count > 0)
            {
                error = string.Join("; ", errors.ToArray());
                return false;
            }
            return true;
        }

        public static bool SaveState(TemporaryAccessState state, out string error)
        {
            error = null;
            if (FaultInjectionSaveStateSeam != null)
            {
                string fiErr = FaultInjectionSaveStateSeam();
                if (!string.IsNullOrEmpty(fiErr))
                {
                    error = fiErr;
                    return false;
                }
            }

            lock (_syncLock)
            {
                _cachedState = state ?? new TemporaryAccessState { Status = TemporaryAccessStatus.Inactive, IsActive = false };
                _cachedState.IsActive = (_cachedState.Status == TemporaryAccessStatus.Active);

                string path = GetFilePath();
                string temp = path + ".tmp";
                string bak = GetBackupFilePath();

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(_cachedState);

                try
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    // Flush bytes to disk sector
                    using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var sw = new StreamWriter(fs))
                    {
                        sw.Write(json);
                        sw.Flush();
                        fs.Flush(true);
                    }

                    // Atomic replace with backup preservation
                    if (File.Exists(path))
                    {
                        try
                        {
                            File.Replace(temp, path, bak, true);
                        }
                        catch
                        {
                            try { File.Copy(path, bak, true); } catch { }
                            File.Delete(path);
                            File.Move(temp, path);
                        }
                    }
                    else
                    {
                        File.Move(temp, path);
                        try { File.Copy(path, bak, true); } catch { }
                    }

                    if (string.IsNullOrEmpty(SecurityAclHelper.BaseDirectoryOverride))
                    {
                        SecurityAclHelper.ApplySensitiveFileSecurity(path);
                        SecurityAclHelper.ApplySensitiveFileSecurity(bak);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }
        }

        public static void SaveState(TemporaryAccessState state)
        {
            string err;
            SaveState(state, out err);
        }

        /// <summary>
        /// Starts a persistent temporary access window.
        /// INVARIANT: Pre-commits durable lease to disk BEFORE relaxing Windows storage policies.
        /// INVARIANT: If persistence fails, registry is NEVER unlocked.
        /// INVARIANT: If policy unlock fails, lease intent is rolled back on disk.
        /// INVARIANT: Preserves existing administrator policy baseline when extending active sessions.
        /// </summary>
        public static bool StartTemporaryAccess(int minutes, ProtectionPolicy currentActivePolicy, out string error)
        {
            error = null;
            if (minutes <= 0)
            {
                error = "Duration must be greater than zero.";
                return false;
            }

            lock (_syncLock)
            {
                TemporaryAccessState existing = LoadState();
                ProtectionPolicy baselineToPreserve;

                // Invariant: Do not silently destroy a pre-existing administrator policy
                if (existing != null && (existing.IsActive || existing.Status == TemporaryAccessStatus.Active || existing.Status == TemporaryAccessStatus.RelockPending || existing.Status == TemporaryAccessStatus.RelockFailed))
                {
                    baselineToPreserve = (existing.OriginalDesiredPolicy != null)
                        ? existing.OriginalDesiredPolicy.Clone()
                        : (currentActivePolicy != null ? currentActivePolicy.Clone() : new ProtectionPolicy());
                }
                else
                {
                    baselineToPreserve = (currentActivePolicy != null) ? currentActivePolicy.Clone() : new ProtectionPolicy();
                }

                TemporaryAccessState newState = new TemporaryAccessState
                {
                    Status = TemporaryAccessStatus.Active,
                    IsActive = true,
                    StartUtc = DateTime.UtcNow,
                    ExpirationUtc = DateTime.UtcNow.AddMinutes(minutes),
                    TotalMinutes = minutes,
                    OriginalDesiredPolicy = baselineToPreserve,
                    GenerationId = Guid.NewGuid().ToString("N"),
                    RelockAttempts = 0,
                    LastRelockError = null
                };

                // STEP 1: Pre-commit durable lease intent to disk BEFORE touching Windows security policies!
                bool saved = SaveState(newState, out error);
                if (!saved)
                {
                    error = "Failed to persist temporary access lease: " + error;
                    return false;
                }

                // STEP 2: Temporarily unlock Windows storage policies
                ProtectionPolicy unlockedPolicy = new ProtectionPolicy(); // All false
                bool ok = ApplyPolicyInternal(unlockedPolicy, out error);
                if (!ok)
                {
                    // Rollback the lease on disk so system does not remain in phantom active state
                    newState.Status = TemporaryAccessStatus.Inactive;
                    newState.IsActive = false;
                    newState.LastRelockError = "Unlock failed: " + error;
                    string rollbackSaveErr;
                    SaveState(newState, out rollbackSaveErr);
                    return false;
                }

                // STEP 3: Cancel all pending in-flight enforcement workers and retry delays (SEC-06)
                DeviceEnforcementLifecycleManager.CancelAllPendingWorkers("Temporary access activated");

                return true;
            }
        }

        /// <summary>
        /// Reconciles state upon application or service startup.
        /// Automatically attempts re-lock if lease expired, was interrupted, or previously failed.
        /// Never claims relock complete unless policy application was observed to succeed.
        /// </summary>
        public static void ReconcileStartupState(out bool wasExpiredAndRelocked, out string relockError)
        {
            wasExpiredAndRelocked = false;
            relockError = null;

            lock (_syncLock)
            {
                TemporaryAccessState state = LoadState();
                if (state == null) return;

                bool needsRelock = false;

                if (state.Status == TemporaryAccessStatus.RelockPending || state.Status == TemporaryAccessStatus.RelockFailed)
                {
                    needsRelock = true;
                }
                else if (state.IsActive || state.Status == TemporaryAccessStatus.Active)
                {
                    DateTime now = DateTime.UtcNow;
                    if (now >= state.ExpirationUtc ||
                        now < state.StartUtc.AddSeconds(-60) ||
                        state.ExpirationUtc > state.StartUtc.AddMinutes(state.TotalMinutes + 5) ||
                        state.ExpirationUtc <= DateTime.MinValue)
                    {
                        needsRelock = true;
                    }
                }

                if (needsRelock)
                {
                    bool ok = ExpireNow(state.GenerationId, out relockError);
                    if (ok)
                    {
                        wasExpiredAndRelocked = true;
                    }
                }
            }
        }

        public static void ReconcileStartupState(out bool wasExpiredAndRelocked)
        {
            string relockErr;
            ReconcileStartupState(out wasExpiredAndRelocked, out relockErr);
        }

        /// <summary>
        /// Pure read-only inspection of remaining seconds for active lease.
        /// Zero side-effects: never calls ExpireNow(), never mutates registry or journal.
        /// Safe for 1Hz UI timer calls without blocking the UI thread.
        /// </summary>
        public static int PeekRemainingSeconds()
        {
            lock (_syncLock)
            {
                TemporaryAccessState state = LoadState();
                if (state == null) return 0;

                if (state.Status == TemporaryAccessStatus.RelockPending || state.Status == TemporaryAccessStatus.RelockFailed)
                {
                    return 0;
                }

                if (!state.IsActive && state.Status != TemporaryAccessStatus.Active) return 0;

                DateTime now = DateTime.UtcNow;

                // Clock tampering guard: clock shifted backwards by more than 60 seconds
                if (now < state.StartUtc.AddSeconds(-60))
                {
                    return 0;
                }

                // Timestamp corruption guard
                if (state.ExpirationUtc > state.StartUtc.AddMinutes(state.TotalMinutes + 5) || state.ExpirationUtc <= DateTime.MinValue)
                {
                    return 0;
                }

                TimeSpan diff = state.ExpirationUtc - now;
                if (diff.TotalSeconds <= 0)
                {
                    return 0;
                }

                return (int)diff.TotalSeconds;
            }
        }

        /// <summary>
        /// Evaluates lease expiration, clock rollback, corruption, or retry requirements and triggers ExpireNow() if due.
        /// </summary>
        public static bool CheckAndExpireIfDue(out string error)
        {
            error = null;
            lock (_syncLock)
            {
                TemporaryAccessState state = LoadState();
                if (state == null) return false;

                if (state.Status == TemporaryAccessStatus.RelockPending || state.Status == TemporaryAccessStatus.RelockFailed)
                {
                    return ExpireNow(state.GenerationId, out error);
                }

                if (!state.IsActive && state.Status != TemporaryAccessStatus.Active) return false;

                DateTime now = DateTime.UtcNow;

                if (now < state.StartUtc.AddSeconds(-60) ||
                    state.ExpirationUtc > state.StartUtc.AddMinutes(state.TotalMinutes + 5) ||
                    state.ExpirationUtc <= DateTime.MinValue ||
                    (state.ExpirationUtc - now).TotalSeconds <= 0)
                {
                    return ExpireNow(state.GenerationId, out error);
                }

                return false;
            }
        }

        /// <summary>
        /// Returns remaining seconds for active lease, checking for expiration first.
        /// INVARIANT: Clock rollback (backwards shift) triggers immediate expiration and re-lock.
        /// INVARIANT: Corrupted expiration timestamps trigger immediate deterministic re-lock.
        /// INVARIANT: Pending or failed re-lock attempts are automatically retried.
        /// </summary>
        public static int GetRemainingSeconds()
        {
            lock (_syncLock)
            {
                string ignoredErr;
                CheckAndExpireIfDue(out ignoredErr);
                return PeekRemainingSeconds();
            }
        }

        /// <summary>
        /// Expires and re-locks the temporary access session.
        /// INVARIANT: If re-lock fails, state transitions to RelockFailed and remains observable for retry.
        /// INVARIANT: Never silently clears the lease on failed re-lock.
        /// </summary>
        public static bool ExpireNow(string expectedGenerationId, out string error)
        {
            error = null;
            lock (_syncLock)
            {
                TemporaryAccessState state = LoadState();
                if (state == null || (!state.IsActive &&
                                      state.Status != TemporaryAccessStatus.Active &&
                                      state.Status != TemporaryAccessStatus.RelockPending &&
                                      state.Status != TemporaryAccessStatus.RelockFailed))
                {
                    return true;
                }

                if (expectedGenerationId != null && !string.Equals(state.GenerationId, expectedGenerationId, StringComparison.Ordinal))
                {
                    // Generation mismatch: a newer session exists or stale event fired
                    return true;
                }

                // Mark intent to relock
                state.Status = TemporaryAccessStatus.RelockPending;
                state.IsActive = false;
                state.LastRelockAttemptUtc = DateTime.UtcNow;
                string savePendingErr;
                SaveState(state, out savePendingErr);

                // Check fault injection seam
                if (FaultInjectionRelockSeam != null)
                {
                    string fiErr = FaultInjectionRelockSeam(state.OriginalDesiredPolicy);
                    if (!string.IsNullOrEmpty(fiErr))
                    {
                        error = fiErr;
                        state.Status = TemporaryAccessStatus.RelockFailed;
                        state.LastRelockError = error;
                        state.RelockAttempts++;
                        SaveState(state);
                        return false;
                    }
                }

                // Apply the original desired policy
                ProtectionPolicy policyToRestore = state.OriginalDesiredPolicy ?? new ProtectionPolicy();
                bool ok = ApplyPolicyInternal(policyToRestore, out error);
                if (!ok)
                {
                    // Re-lock failed! Preserve failed state and retry obligation
                    state.Status = TemporaryAccessStatus.RelockFailed;
                    state.LastRelockError = error;
                    state.RelockAttempts++;
                    SaveState(state);
                    return false;
                }

                // Re-lock successfully applied and verified
                state.Status = TemporaryAccessStatus.Relocked;
                state.IsActive = false;
                state.LastRelockError = null;
                SaveState(state);
                return true;
            }
        }

        public static void ExpireNow(string expectedGenerationId = null)
        {
            string err;
            ExpireNow(expectedGenerationId, out err);
        }

        public static bool CancelTemporaryAccess(out string error)
        {
            return ExpireNow(null, out error);
        }

        public static void CancelTemporaryAccess()
        {
            string err;
            CancelTemporaryAccess(out err);
        }
    }
}
