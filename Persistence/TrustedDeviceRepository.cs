// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Trusted Device Repository
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using UsbShieldPro.Core;
using UsbShieldPro.Models;
using UsbShieldPro.Windows;

namespace UsbShieldPro.Persistence
{
    public enum RepositoryLoadStatus
    {
        Success,               // Valid primary parsed successfully (with 0 or more rules)
        FileNotFound,          // Primary file does not exist on disk
        AccessDenied,          // UnauthorizedAccessException encountered; NOT corruption, NO fallback
        CorruptRecoveredBackup,// Primary had syntax/format error and was safely backed up; recovered from .bak
        CorruptBackupFailed,   // Primary had syntax/format error, and .bak missing or also corrupt
        IoError                // General I/O error
    }

    /// <summary>
    /// Manages the persistent whitelist of trusted devices in %ProgramData%\USBShieldPro\trusted_devices.json.
    /// Invariant: Cache never leads disk state (disk is authoritative).
    /// Invariant: Atomic flush and replacement with backup copy.
    /// Invariant: Corrupted files are preserved and never silently overwritten with [].
    /// Invariant: Access denied is NEVER treated as corruption and NEVER triggers backup fallback.
    /// Invariant: Valid empty [] is authoritative zero-rule state and NEVER triggers backup fallback.
    /// Invariant: Hard runtime safety guard prevents test mode from touching production paths.
    /// </summary>
    public static class TrustedDeviceRepository
    {
        private static readonly object _syncLock = new object();
        private static List<DeviceRule> _cachedRules;
        private static string _filePathOverride;

        public static string LastLoadError { get; private set; }
        public static RepositoryLoadStatus LastLoadStatus { get; private set; }
        public static bool IsTestMode { get; set; }

        public static Func<DeviceIdentity, bool> IsDeviceTrustedSeam = null;

        public static void ResetCache()
        {
            lock (_syncLock)
            {
                _cachedRules = null;
                IsDeviceTrustedSeam = null;
            }
        }

        public static void AssertNotProductionInTestMode(string path)
        {
            if (IsTestMode)
            {
                string full = Path.GetFullPath(path);
                string progData = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
                if (full.StartsWith(progData, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("CRITICAL SAFETY VIOLATION: Test mode attempted to access production repository path: " + full);
                }
            }
        }

        public static IDisposable UseIsolatedRepository(string isolatedFilePath)
        {
            lock (_syncLock)
            {
                ResetCache();
                _filePathOverride = isolatedFilePath;
                return new IsolatedRepositoryScope();
            }
        }

        private class IsolatedRepositoryScope : IDisposable
        {
            public void Dispose()
            {
                lock (_syncLock)
                {
                    ResetCache();
                    _filePathOverride = null;
                }
            }
        }

        public static string GetFilePath()
        {
            if (!string.IsNullOrEmpty(_filePathOverride))
            {
                return _filePathOverride;
            }
            return Path.Combine(SecurityAclHelper.GetProgramDataDirectory(), "trusted_devices.json");
        }

        public static List<DeviceRule> LoadRules()
        {
            lock (_syncLock)
            {
                if (_cachedRules != null) return _cachedRules;

                string path = GetFilePath();
                AssertNotProductionInTestMode(path);

                if (!File.Exists(path))
                {
                    // First run: file legitimately absent
                    _cachedRules = new List<DeviceRule>();
                    LastLoadStatus = RepositoryLoadStatus.FileNotFound;
                    LastLoadError = null;
                    return _cachedRules;
                }

                string json = null;
                try
                {
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        json = sr.ReadToEnd();
                    }
                }
                catch (UnauthorizedAccessException uex)
                {
                    // ACCESS DENIED IS NOT CORRUPTION!
                    // Do NOT rename file.
                    // Do NOT classify as corruption.
                    // Do NOT silently switch state to backup!
                    _cachedRules = new List<DeviceRule>();
                    LastLoadStatus = RepositoryLoadStatus.AccessDenied;
                    LastLoadError = "ACCESS_DENIED: Current process token lacks permission to read " + path + ": " + uex.Message;
                    return _cachedRules;
                }
                catch (IOException ioEx)
                {
                    _cachedRules = new List<DeviceRule>();
                    LastLoadStatus = RepositoryLoadStatus.IoError;
                    LastLoadError = "IO_ERROR: Failed to read " + path + ": " + ioEx.Message;
                    return _cachedRules;
                }

                try
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    var deserialized = serializer.Deserialize<List<DeviceRule>>(json);
                    if (deserialized != null)
                    {
                        // VALID JSON (including empty "[]"):
                        // Authoritative valid configuration. Zero configured rules.
                        // Do NOT fall back to backup merely because backup contains data!
                        _cachedRules = deserialized;
                        LastLoadStatus = RepositoryLoadStatus.Success;
                        LastLoadError = null;
                        return _cachedRules;
                    }

                    throw new FormatException("JSON deserializer returned null for trusted device rules.");
                }
                catch (Exception jsonEx)
                {
                    // ACTUAL CORRUPTION (FormatException, ArgumentException, syntax errors):
                    HandleCorruptFile(path, jsonEx);

                    // Attempt recovery from last known-good backup (.bak)
                    string bak = path + ".bak";
                    if (File.Exists(bak))
                    {
                        try
                        {
                            string bakJson = File.ReadAllText(bak, Encoding.UTF8);
                            JavaScriptSerializer serializer = new JavaScriptSerializer();
                            var bakRules = serializer.Deserialize<List<DeviceRule>>(bakJson);
                            if (bakRules != null)
                            {
                                _cachedRules = bakRules;
                                LastLoadStatus = RepositoryLoadStatus.CorruptRecoveredBackup;
                                LastLoadError = "Recovered from backup (.bak) after primary corruption: " + jsonEx.Message;
                                return _cachedRules;
                            }
                        }
                        catch { }
                    }

                    _cachedRules = new List<DeviceRule>();
                    LastLoadStatus = RepositoryLoadStatus.CorruptBackupFailed;
                    LastLoadError = "CONFIGURATION RECOVERY REQUIRED: Corrupt trusted_devices.json preserved. " + jsonEx.Message;
                    return _cachedRules;
                }
            }
        }

        private static void HandleCorruptFile(string path, Exception ex)
        {
            try
            {
                if (File.Exists(path))
                {
                    string dir = Path.GetDirectoryName(path);
                    string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                    string corruptBackup = Path.Combine(dir, "trusted_devices_corrupt_" + timestamp + ".json");
                    File.Copy(path, corruptBackup, true);
                    if (string.IsNullOrEmpty(_filePathOverride))
                    {
                        SecurityAclHelper.ApplySensitiveFileSecurity(corruptBackup);
                    }
                }
            }
            catch { }
        }

        public static void SaveRules(List<DeviceRule> rules)
        {
            string err;
            SaveRulesInternal(rules, out err);
        }

        private static bool SaveRulesInternal(List<DeviceRule> rules, out string error)
        {
            error = null;
            lock (_syncLock)
            {
                try
                {
                    string path = GetFilePath();
                    AssertNotProductionInTestMode(path);

                    string dir = Path.GetDirectoryName(path);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    string temp = path + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
                    string bak = path + ".bak";

                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    string json = serializer.Serialize(rules ?? new List<DeviceRule>());

                    // 1. Write temp file with full buffer flush to physical disk
                    using (FileStream fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
                    {
                        sw.Write(json);
                        sw.Flush();
                        fs.Flush(true); // Force hardware sector flush
                    }

                    // 2. Transactional Replacement
                    if (File.Exists(path))
                    {
                        // Atomic replacement via File.Replace:
                        // In Win32 ReplaceFile, 'path' is atomically replaced with 'temp', and old 'path' is saved to 'bak'.
                        // If File.Replace fails, 'path' remains completely intact (fail closed).
                        // Under NO circumstances fall back to File.Delete(path) + File.Move!
                        try
                        {
                            File.Replace(temp, path, bak, true);
                        }
                        catch (Exception repEx)
                        {
                            // Fail closed: clean up candidate temp file, preserve old primary and old backup
                            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                            error = "Atomic repository replacement failed: " + repEx.Message;
                            return false;
                        }
                    }
                    else
                    {
                        // Initial creation: atomic same-volume rename
                        File.Move(temp, path);
                        // Initialize baseline backup with first valid configuration
                        try
                        {
                            File.Copy(path, bak, true);
                        }
                        catch { }
                    }

                    // 3. Read-back verification to guarantee committed file integrity
                    List<DeviceRule> verifyRules = null;
                    try
                    {
                        string verifyJson = File.ReadAllText(path, Encoding.UTF8);
                        verifyRules = serializer.Deserialize<List<DeviceRule>>(verifyJson);
                        int expectedCount = (rules != null) ? rules.Count : 0;
                        int actualCount = (verifyRules != null) ? verifyRules.Count : -1;
                        if (actualCount != expectedCount)
                        {
                            error = "Read-back verification failed: Expected rule count " + expectedCount + ", got " + actualCount;
                            return false;
                        }
                    }
                    catch (Exception readEx)
                    {
                        error = "Read-back verification threw: " + readEx.Message;
                        return false;
                    }

                    // 4. Apply sensitive file security to both primary and backup
                    if (string.IsNullOrEmpty(_filePathOverride))
                    {
                        SecurityAclHelper.ApplySensitiveFileSecurity(path);
                        if (File.Exists(bak))
                        {
                            SecurityAclHelper.ApplySensitiveFileSecurity(bak);
                        }
                    }

                    _cachedRules = (verifyRules != null) ? new List<DeviceRule>(verifyRules) : new List<DeviceRule>();
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }
        }

        public static bool AddTrustedDevice(DeviceIdentity device, out string error)
        {
            error = null;
            if (device == null)
            {
                error = "Device cannot be null.";
                return false;
            }

            lock (_syncLock)
            {
                // Ensure fresh state from disk
                ResetCache();
                var currentRules = LoadRules();

                // Check if already trusted
                foreach (var r in currentRules)
                {
                    if (r.Matches(device))
                    {
                        return true; // Already trusted
                    }
                }

                // Add to Windows Device Installation Restriction Allow list
                int allocatedIndex = -1;
                RuleMatchType matchType = RuleMatchType.DeviceInstanceId;

                if (!string.IsNullOrEmpty(device.DeviceInstanceId))
                {
                    matchType = RuleMatchType.DeviceInstanceId;
                    DeviceInstallationPolicyBackend.AddAllowedDeviceInstanceId(
                        device.DeviceInstanceId,
                        out allocatedIndex,
                        out error);
                }
                else if (device.HardwareIds != null && device.HardwareIds.Count > 0)
                {
                    matchType = RuleMatchType.HardwareId;
                    DeviceInstallationPolicyBackend.AddAllowedDeviceId(
                        device.HardwareIds[0],
                        out allocatedIndex,
                        out error);
                }

                DeviceRule newRule = new DeviceRule
                {
                    FriendlyName = device.FriendlyName,
                    TargetInstanceId = device.DeviceInstanceId,
                    TargetSerial = device.SerialNumber,
                    MatchType = matchType,
                    Confidence = device.Confidence,
                    Action = RuleAction.AllowTrusted,
                    AllocatedPolicyIndex = allocatedIndex >= 0 ? allocatedIndex : (int?)null
                };

                if (device.HardwareIds != null && device.HardwareIds.Count > 0)
                {
                    newRule.TargetHardwareId = device.HardwareIds[0];
                }

                // TRANSACTIONAL PERSISTENCE:
                // Cache does not lead disk. Clone current rules and append candidate.
                List<DeviceRule> candidateRules = new List<DeviceRule>(currentRules);
                candidateRules.Add(newRule);

                string saveErr;
                bool saveOk = SaveRulesInternal(candidateRules, out saveErr);
                if (!saveOk)
                {
                    error = "Disk persistence failed: " + saveErr;
                    // Rollback policy allocation
                    if (newRule.AllocatedPolicyIndex.HasValue)
                    {
                        string rbErr;
                        if (newRule.MatchType == RuleMatchType.HardwareId)
                            DeviceInstallationPolicyBackend.RemoveAllowedDeviceId(newRule.AllocatedPolicyIndex.Value, out rbErr);
                        else
                            DeviceInstallationPolicyBackend.RemoveAllowedDeviceInstanceId(newRule.AllocatedPolicyIndex.Value, out rbErr);
                    }
                    return false;
                }

                // Verify read-back from disk
                ResetCache();
                List<DeviceRule> verifiedRules = LoadRules();
                bool verified = false;
                foreach (var r in verifiedRules)
                {
                    if (r.Matches(device))
                    {
                        verified = true;
                        break;
                    }
                }

                if (!verified)
                {
                    error = "Disk verification failed: Saved rule was not verified in disk read-back.";
                    return false;
                }

                _cachedRules = verifiedRules;
                return true;
            }
        }

        public static bool RemoveTrustedDevice(string ruleId, out string error)
        {
            error = null;
            lock (_syncLock)
            {
                ResetCache();
                var currentRules = LoadRules();
                DeviceRule target = null;
                foreach (var r in currentRules)
                {
                    if (string.Equals(r.RuleId, ruleId, StringComparison.OrdinalIgnoreCase))
                    {
                        target = r;
                        break;
                    }
                }

                if (target == null) return true;

                // Remove from registry policy if it had an allocated index
                if (target.AllocatedPolicyIndex.HasValue)
                {
                    string err;
                    if (target.MatchType == RuleMatchType.HardwareId)
                    {
                        DeviceInstallationPolicyBackend.RemoveAllowedDeviceId(target.AllocatedPolicyIndex.Value, out err);
                    }
                    else
                    {
                        DeviceInstallationPolicyBackend.RemoveAllowedDeviceInstanceId(target.AllocatedPolicyIndex.Value, out err);
                    }
                }

                List<DeviceRule> candidateRules = new List<DeviceRule>(currentRules);
                candidateRules.Remove(target);

                string saveErr;
                bool saveOk = SaveRulesInternal(candidateRules, out saveErr);
                if (!saveOk)
                {
                    error = "Disk persistence failed during removal: " + saveErr;
                    return false;
                }

                // Verify read-back from disk
                ResetCache();
                List<DeviceRule> verifiedRules = LoadRules();
                foreach (var r in verifiedRules)
                {
                    if (string.Equals(r.RuleId, ruleId, StringComparison.OrdinalIgnoreCase))
                    {
                        error = "Disk verification failed: Rule still present on disk after removal.";
                        return false;
                    }
                }

                _cachedRules = verifiedRules;
                return true;
            }
        }

        public static bool IsDeviceTrusted(DeviceIdentity device)
        {
            if (device == null) return false;
            if (DeviceEnforcementLifecycleManager.IsTestMode && IsDeviceTrustedSeam != null)
            {
                return IsDeviceTrustedSeam(device);
            }
            lock (_syncLock)
            {
                var rules = LoadRules();
                foreach (var r in rules)
                {
                    if (r.Action == RuleAction.AllowTrusted && r.Matches(device))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public static DeviceRule FindMatchingRule(DeviceIdentity device)
        {
            if (device == null) return null;
            lock (_syncLock)
            {
                var rules = LoadRules();
                foreach (var r in rules)
                {
                    if (r.Matches(device))
                    {
                        return r;
                    }
                }
                return null;
            }
        }
    }
}
