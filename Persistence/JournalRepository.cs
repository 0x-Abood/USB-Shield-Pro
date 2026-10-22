// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Write-Ahead Crash-Safe State Journal Repository
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using UsbShieldPro.Models;

namespace UsbShieldPro.Persistence
{
    public enum JournalLoadStatus
    {
        Pristine,
        LoadedPrimary,
        LoadedBackup,
        Corrupted,
        AccessDenied
    }

    public class JournalLoadResult
    {
        public StateJournal Journal { get; set; }
        public JournalLoadStatus Status { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsRecoverable { get; set; }
    }

    /// <summary>
    /// Manages the canonical, persistent State Journal on disk with crash-safe write-ahead semantics.
    /// Invariant: Original values are captured ONLY ONCE. Intermediate values never overwrite the baseline.
    /// Invariant: Missing primary checks backup before treating as pristine.
    /// Invariant: Corrupted recovery data is never silently treated as an empty successful baseline.
    /// </summary>
    public static class JournalRepository
    {
        private static readonly object _syncLock = new object();
        private static StateJournal _cachedJournal;
        private static JournalLoadStatus? _cachedStatus;
        private static string _cachedErrorMessage;

        private static Func<JournalLoadResult> _faultInjectionLoadSeam = null;
        private static Func<StateJournal, string> _faultInjectionSaveSeam = null;

        public static Func<JournalLoadResult> FaultInjectionLoadSeam
        {
            get { return Core.DeviceEnforcementLifecycleManager.IsTestMode ? _faultInjectionLoadSeam : null; }
            set { if (!Core.DeviceEnforcementLifecycleManager.IsTestMode) { _faultInjectionLoadSeam = null; return; } _faultInjectionLoadSeam = value; }
        }

        public static Func<StateJournal, string> FaultInjectionSaveSeam
        {
            get { return Core.DeviceEnforcementLifecycleManager.IsTestMode ? _faultInjectionSaveSeam : null; }
            set { if (!Core.DeviceEnforcementLifecycleManager.IsTestMode) { _faultInjectionSaveSeam = null; return; } _faultInjectionSaveSeam = value; }
        }

        public static void ResetSeams()
        {
            _faultInjectionLoadSeam = null;
            _faultInjectionSaveSeam = null;
        }

        private static string GetJournalDirectory()
        {
            return SecurityAclHelper.GetProgramDataDirectory();
        }

        public static string GetJournalFilePath()
        {
            return Path.Combine(GetJournalDirectory(), "journal.json");
        }

        public static string GetJournalBackupPath()
        {
            return Path.Combine(GetJournalDirectory(), "journal.bak");
        }

        public static string GetJournalTempPath()
        {
            return Path.Combine(GetJournalDirectory(), "journal.tmp");
        }

        public static void ResetCache()
        {
            lock (_syncLock)
            {
                _cachedJournal = null;
                _cachedStatus = null;
                _cachedErrorMessage = null;
            }
        }

        /// <summary>
        /// Loads the canonical journal with explicit verification of primary and backup integrity.
        /// INVARIANT: Missing primary checks backup.
        /// INVARIANT: Corrupted primary checks backup.
        /// INVARIANT: If both unreadable/corrupted, reports Corrupted status and NEVER returns an empty journal as valid baseline.
        /// INVARIANT: If neither exists, reports Pristine status.
        /// </summary>
        public static JournalLoadResult LoadWithStatus()
        {
            if (FaultInjectionLoadSeam != null)
            {
                return FaultInjectionLoadSeam();
            }

            lock (_syncLock)
            {
                if (_cachedJournal != null && _cachedStatus.HasValue)
                {
                    return new JournalLoadResult
                    {
                        Journal = _cachedJournal,
                        Status = _cachedStatus.Value,
                        ErrorMessage = _cachedErrorMessage,
                        IsRecoverable = (_cachedStatus.Value == JournalLoadStatus.LoadedPrimary || _cachedStatus.Value == JournalLoadStatus.LoadedBackup)
                    };
                }

                string path = GetJournalFilePath();
                string bakPath = GetJournalBackupPath();
                JavaScriptSerializer serializer = new JavaScriptSerializer();

                bool primaryExists = File.Exists(path);
                bool bakExists = File.Exists(bakPath);

                if (!primaryExists && !bakExists)
                {
                    _cachedJournal = new StateJournal();
                    _cachedStatus = JournalLoadStatus.Pristine;
                    _cachedErrorMessage = null;
                    return new JournalLoadResult
                    {
                        Journal = _cachedJournal,
                        Status = JournalLoadStatus.Pristine,
                        ErrorMessage = null,
                        IsRecoverable = false
                    };
                }

                Exception primaryEx = null;

                if (primaryExists)
                {
                    try
                    {
                        string json = File.ReadAllText(path);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            StateJournal loaded = serializer.Deserialize<StateJournal>(json);
                            if (loaded != null && loaded.BaselineEntries != null)
                            {
                                _cachedJournal = loaded;
                                _cachedStatus = JournalLoadStatus.LoadedPrimary;
                                _cachedErrorMessage = null;
                                return new JournalLoadResult
                                {
                                    Journal = _cachedJournal,
                                    Status = JournalLoadStatus.LoadedPrimary,
                                    ErrorMessage = null,
                                    IsRecoverable = true
                                };
                            }
                        }
                        primaryEx = new FormatException("Primary journal is empty or deserialized to invalid object.");
                    }
                    catch (UnauthorizedAccessException uex)
                    {
                        _cachedJournal = null;
                        _cachedStatus = JournalLoadStatus.AccessDenied;
                        _cachedErrorMessage = "Access denied reading primary journal: " + uex.Message;
                        return new JournalLoadResult
                        {
                            Journal = null,
                            Status = JournalLoadStatus.AccessDenied,
                            ErrorMessage = _cachedErrorMessage,
                            IsRecoverable = false
                        };
                    }
                    catch (Exception ex)
                    {
                        primaryEx = ex;
                    }
                }

                if (bakExists)
                {
                    try
                    {
                        string bakJson = File.ReadAllText(bakPath);
                        if (!string.IsNullOrWhiteSpace(bakJson))
                        {
                            StateJournal bakLoaded = serializer.Deserialize<StateJournal>(bakJson);
                            if (bakLoaded != null && bakLoaded.BaselineEntries != null)
                            {
                                _cachedJournal = bakLoaded;
                                _cachedStatus = JournalLoadStatus.LoadedBackup;
                                _cachedErrorMessage = primaryEx != null
                                    ? ("Primary journal unreadable (" + primaryEx.Message + "); recovered from backup.")
                                    : null;
                                return new JournalLoadResult
                                {
                                    Journal = _cachedJournal,
                                    Status = JournalLoadStatus.LoadedBackup,
                                    ErrorMessage = _cachedErrorMessage,
                                    IsRecoverable = true
                                };
                            }
                        }
                    }
                    catch (UnauthorizedAccessException uex)
                    {
                        _cachedJournal = null;
                        _cachedStatus = JournalLoadStatus.AccessDenied;
                        _cachedErrorMessage = "Access denied reading backup journal: " + uex.Message;
                        return new JournalLoadResult
                        {
                            Journal = null,
                            Status = JournalLoadStatus.AccessDenied,
                            ErrorMessage = _cachedErrorMessage,
                            IsRecoverable = false
                        };
                    }
                    catch { }
                }

                _cachedJournal = null;
                _cachedStatus = JournalLoadStatus.Corrupted;
                _cachedErrorMessage = primaryEx != null
                    ? ("State recovery journal corrupted: " + primaryEx.Message)
                    : "State recovery journal files exist but could not be parsed.";

                return new JournalLoadResult
                {
                    Journal = null,
                    Status = JournalLoadStatus.Corrupted,
                    ErrorMessage = _cachedErrorMessage,
                    IsRecoverable = false
                };
            }
        }

        /// <summary>
        /// Loads the canonical journal from disk, or initializes a new one if absent.
        /// </summary>
        public static StateJournal Load()
        {
            JournalLoadResult result = LoadWithStatus();
            if (result.Journal != null)
            {
                return result.Journal;
            }
            return new StateJournal();
        }

        /// <summary>
        /// Atomically saves the journal using crash-safe write-ahead semantics.
        /// Handles initial creation vs File.Replace on existing destination.
        /// </summary>
        public static bool Save(StateJournal journal, out string error)
        {
            error = null;
            if (journal == null)
            {
                error = "Cannot persist null StateJournal.";
                return false;
            }

            if (FaultInjectionSaveSeam != null)
            {
                string fiErr = FaultInjectionSaveSeam(journal);
                if (!string.IsNullOrEmpty(fiErr))
                {
                    error = fiErr;
                    return false;
                }
            }

            lock (_syncLock)
            {
                try
                {
                    journal.LastUpdatedUtc = DateTime.UtcNow;
                    _cachedJournal = journal;
                    _cachedStatus = JournalLoadStatus.LoadedPrimary;
                    _cachedErrorMessage = null;

                    string dir = GetJournalDirectory();
                    string targetPath = GetJournalFilePath();
                    string tempPath = GetJournalTempPath();
                    string bakPath = GetJournalBackupPath();

                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    string json = serializer.Serialize(journal);

                    // 1. Write to temporary file on same volume and flush buffers
                    using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var sw = new StreamWriter(fs))
                    {
                        sw.Write(json);
                        sw.Flush();
                        fs.Flush(true); // Commit to disk sectors
                    }

                    // 2. Atomic replacement
                    if (!File.Exists(targetPath))
                    {
                        // First creation: atomic move
                        File.Move(tempPath, targetPath);
                    }
                    else
                    {
                        // Subsequent writes: File.Replace with automated backup
                        try
                        {
                            File.Replace(tempPath, targetPath, bakPath, true);
                        }
                        catch
                        {
                            try { File.Copy(targetPath, bakPath, true); } catch { }
                            File.Delete(targetPath);
                            File.Move(tempPath, targetPath);
                        }
                    }

                    if (string.IsNullOrEmpty(SecurityAclHelper.BaseDirectoryOverride))
                    {
                        SecurityAclHelper.ApplySensitiveFileSecurity(targetPath);
                        SecurityAclHelper.ApplySensitiveFileSecurity(bakPath);
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

        public static void Save(StateJournal journal)
        {
            string err;
            Save(journal, out err);
        }

        /// <summary>
        /// Captures an original baseline value. If a baseline entry already exists for this
        /// key/value target, it is NEVER overwritten.
        /// </summary>
        public static RegistryOriginalValue RecordOriginalBaseline(
            string hive,
            string keyPath,
            string valueName,
            string valueKind,
            bool existedBefore,
            object originalData,
            int? allocatedIndex = null)
        {
            lock (_syncLock)
            {
                StateJournal journal = Load();
                if (journal == null) journal = new StateJournal();
                if (journal.BaselineEntries == null) journal.BaselineEntries = new List<RegistryOriginalValue>();
                string targetId = hive + "\\" + keyPath + "\\" + valueName;

                // Check if baseline was already recorded previously
                foreach (var entry in journal.BaselineEntries)
                {
                    if (entry == null) continue;
                    if (string.Equals(entry.GetTargetKeyIdentifier(), targetId, StringComparison.OrdinalIgnoreCase))
                    {
                        // Invariant: Do not alter original baseline
                        return entry;
                    }
                }

                // New entry
                RegistryOriginalValue newEntry = new RegistryOriginalValue
                {
                    Hive = hive,
                    KeyPath = keyPath,
                    ValueName = valueName,
                    ValueKind = valueKind,
                    ExistedBefore = existedBefore,
                    OriginalData = originalData,
                    AllocatedNumberedIndex = allocatedIndex,
                    FirstCapturedUtc = DateTime.UtcNow
                };

                journal.BaselineEntries.Add(newEntry);
                Save(journal);
                return newEntry;
            }
        }

        /// <summary>
        /// Retrieves an existing baseline entry for a given hive, keyPath, and valueName,
        /// or null if no baseline has been recorded for it yet.
        /// </summary>
        public static RegistryOriginalValue GetBaselineEntry(string hive, string keyPath, string valueName)
        {
            lock (_syncLock)
            {
                StateJournal journal = Load();
                if (journal == null || journal.BaselineEntries == null) return null;
                string targetId = (hive ?? "") + "\\" + (keyPath ?? "") + "\\" + (valueName ?? "");
                foreach (var entry in journal.BaselineEntries)
                {
                    if (entry == null) continue;
                    string entryId = entry.GetTargetKeyIdentifier();
                    if (string.Equals(entryId, targetId, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry;
                    }

                    // Also check normalized HKLM / HKCU equivalence
                    string entryNorm = NormalizeHivePrefix(entryId);
                    string targetNorm = NormalizeHivePrefix(targetId);
                    if (string.Equals(entryNorm, targetNorm, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry;
                    }
                }
                return null;
            }
        }

        private static string NormalizeHivePrefix(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            if (id.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase))
                return "HKEY_CURRENT_USER\\" + id.Substring(5);
            if (id.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase))
                return "HKEY_LOCAL_MACHINE\\" + id.Substring(5);
            return id;
        }

        /// <summary>
        /// Records an individual mutation operation into the write-ahead journal.
        /// </summary>
        public static JournalOperation PrepareOperation(string description, RegistryOriginalValue target)
        {
            lock (_syncLock)
            {
                StateJournal journal = Load();
                JournalOperation op = new JournalOperation
                {
                    OperationId = journal.Operations.Count + 1,
                    TargetDescription = description,
                    Status = JournalOperationStatus.Prepared,
                    PreparedUtc = DateTime.UtcNow,
                    TargetRegistryValue = target
                };

                journal.Operations.Add(op);
                Save(journal);
                return op;
            }
        }

        public static void CommitOperation(JournalOperation op, JournalOperationStatus finalStatus, string error = null)
        {
            if (op == null) return;

            lock (_syncLock)
            {
                StateJournal journal = Load();
                op.Status = finalStatus;
                op.AppliedUtc = DateTime.UtcNow;
                op.VerifiedUtc = (finalStatus == JournalOperationStatus.Verified) ? DateTime.UtcNow : (DateTime?)null;
                op.ErrorMessage = error;

                Save(journal);
            }
        }
    }
}
