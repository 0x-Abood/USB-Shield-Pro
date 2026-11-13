// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.1 — Persistent Activity Repository
// (c) 2026 0xAbood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using UsbShieldPro.Core;
using UsbShieldPro.Models;

namespace UsbShieldPro.Persistence
{
    /// <summary>
    /// Persistent, bounded, access-controlled event store for activity logs.
    /// Bridging Session 0 background Windows Service and Session 1+ interactive GUI.
    /// Invariant: Cache never leads disk state (disk is authoritative).
    /// Invariant: Atomic flush and replacement with backup copy (.bak).
    /// Invariant: Bounded capacity (max 500 records) to prevent unbounded growth.
    /// Invariant: Corrupted files are preserved and never silently overwritten.
    /// Invariant: Access denied is NEVER treated as corruption and NEVER triggers backup fallback.
    /// Invariant: Hard runtime safety guard prevents test mode from touching production paths.
    /// Invariant: Deduplication suppresses rapid duplicate arrival/enforcement events.
    /// </summary>
    public static class ActivityRepository
    {
        public const int DefaultMaxRecords = 500;
        public const string ActivityFileName = "activity.json";
        public const string ActivityBackupFileName = "activity.bak";
        private const string MutexName = @"Global\UsbShieldPro_Activity_Mutex";

        private static readonly object _syncLock = new object();
        private static List<ActivityRecord> _cachedRecords;
        private static DateTime _lastKnownDiskWriteUtc = DateTime.MinValue;
        private static string _filePathOverride;
        private static int _maxRecords = DefaultMaxRecords;

        public static string LastLoadError { get; private set; }
        public static RepositoryLoadStatus LastLoadStatus { get; private set; }
        public static bool IsTestMode { get; set; }

        public static int MaxRecords
        {
            get { return _maxRecords; }
            set { _maxRecords = value > 0 ? value : DefaultMaxRecords; }
        }

        public static void ResetCache()
        {
            lock (_syncLock)
            {
                _cachedRecords = null;
                _lastKnownDiskWriteUtc = DateTime.MinValue;
                LastLoadError = null;
                LastLoadStatus = RepositoryLoadStatus.Success;
            }
        }

        public static void AssertNotProductionInTestMode(string path)
        {
            if (IsTestMode)
            {
                string prodDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string prodPath = Path.Combine(prodDir, "USBShieldPro", ActivityFileName);
                if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(prodPath), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("TEST_MODE_INVARIANT_VIOLATION: Test executed against production Activity path: " + path);
                }
            }
        }

        public static IDisposable CreateTestScope(string testDirectory)
        {
            IsTestMode = true;
            ResetCache();
            if (!Directory.Exists(testDirectory))
            {
                Directory.CreateDirectory(testDirectory);
            }
            _filePathOverride = Path.Combine(testDirectory, ActivityFileName);
            return new IsolatedRepositoryScope();
        }

        private class IsolatedRepositoryScope : IDisposable
        {
            public void Dispose()
            {
                lock (_syncLock)
                {
                    ResetCache();
                    _filePathOverride = null;
                    IsTestMode = false;
                }
            }
        }

        public static string GetFilePath()
        {
            if (!string.IsNullOrEmpty(_filePathOverride))
            {
                return _filePathOverride;
            }
            return Path.Combine(SecurityAclHelper.GetProgramDataDirectory(), ActivityFileName);
        }

        public static string GetBackupFilePath()
        {
            string primary = GetFilePath();
            return primary + ".bak";
        }

        public static DateTime GetLastWriteUtc()
        {
            try
            {
                string path = GetFilePath();
                if (File.Exists(path))
                {
                    return File.GetLastWriteTimeUtc(path);
                }
            }
            catch { }
            return DateTime.MinValue;
        }

        public static bool HasNewRecords(DateTime sinceUtc)
        {
            DateTime currentWrite = GetLastWriteUtc();
            return currentWrite > sinceUtc;
        }

        public static List<ActivityRecord> Load()
        {
            lock (_syncLock)
            {
                string path = GetFilePath();
                AssertNotProductionInTestMode(path);

                DateTime currentWriteUtc = DateTime.MinValue;
                try
                {
                    if (File.Exists(path))
                    {
                        currentWriteUtc = File.GetLastWriteTimeUtc(path);
                    }
                }
                catch { }

                // Return cache only if disk timestamp matches our last known write
                if (_cachedRecords != null && currentWriteUtc != DateTime.MinValue && currentWriteUtc == _lastKnownDiskWriteUtc)
                {
                    return CloneRecords(_cachedRecords);
                }

                if (!File.Exists(path))
                {
                    _cachedRecords = new List<ActivityRecord>();
                    _lastKnownDiskWriteUtc = DateTime.MinValue;
                    LastLoadStatus = RepositoryLoadStatus.FileNotFound;
                    LastLoadError = null;
                    return CloneRecords(_cachedRecords);
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
                    // Access denied is not corruption!
                    _cachedRecords = new List<ActivityRecord>();
                    LastLoadStatus = RepositoryLoadStatus.AccessDenied;
                    LastLoadError = "ACCESS_DENIED: Current process token lacks permission to read " + path + ": " + uex.Message;
                    return CloneRecords(_cachedRecords);
                }
                catch (IOException ioEx)
                {
                    _cachedRecords = new List<ActivityRecord>();
                    LastLoadStatus = RepositoryLoadStatus.IoError;
                    LastLoadError = "IO_ERROR: Failed to read " + path + ": " + ioEx.Message;
                    return CloneRecords(_cachedRecords);
                }

                try
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = 10 * 1024 * 1024; // 10MB headroom for up to 500 events
                    var deserialized = serializer.Deserialize<List<ActivityRecord>>(json);
                    if (deserialized != null)
                    {
                        _cachedRecords = deserialized;
                        _lastKnownDiskWriteUtc = currentWriteUtc;
                        LastLoadStatus = RepositoryLoadStatus.Success;
                        LastLoadError = null;
                        return CloneRecords(_cachedRecords);
                    }

                    throw new FormatException("JSON deserializer returned null for activity records.");
                }
                catch (Exception jsonEx)
                {
                    // Corruption encountered: quarantine corrupt file and recover from .bak
                    HandleCorruptFile(path, jsonEx);

                    string bak = GetBackupFilePath();
                    if (File.Exists(bak))
                    {
                        try
                        {
                            string bakJson;
                            using (FileStream fs = new FileStream(bak, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                            {
                                bakJson = sr.ReadToEnd();
                            }

                            JavaScriptSerializer serializer = new JavaScriptSerializer();
                            serializer.MaxJsonLength = 10 * 1024 * 1024;
                            var bakRecords = serializer.Deserialize<List<ActivityRecord>>(bakJson);
                            if (bakRecords != null)
                            {
                                _cachedRecords = bakRecords;
                                _lastKnownDiskWriteUtc = currentWriteUtc;
                                LastLoadStatus = RepositoryLoadStatus.CorruptRecoveredBackup;
                                LastLoadError = "CORRUPT_RECOVERED: Primary activity.json was corrupt (" + jsonEx.Message + "); safely restored from .bak.";
                                return CloneRecords(_cachedRecords);
                            }
                        }
                        catch (Exception bakEx)
                        {
                            _cachedRecords = new List<ActivityRecord>();
                            LastLoadStatus = RepositoryLoadStatus.CorruptBackupFailed;
                            LastLoadError = "CORRUPT_BACKUP_FAILED: Both primary and backup activity files were unusable: " + bakEx.Message;
                            return CloneRecords(_cachedRecords);
                        }
                    }

                    _cachedRecords = new List<ActivityRecord>();
                    LastLoadStatus = RepositoryLoadStatus.CorruptBackupFailed;
                    LastLoadError = "CORRUPT_NO_BACKUP: Primary activity.json was corrupt and no valid .bak existed.";
                    return CloneRecords(_cachedRecords);
                }
            }
        }

        public static bool AppendRecord(ActivityRecord record, out string error)
        {
            error = null;
            if (record == null) return true;

            lock (_syncLock)
            {
                // Force fresh read from disk to maintain true chronological consistency across processes
                ResetCache();
                List<ActivityRecord> current = Load();

                // Deduplication check: Suppress duplicate event if title and badge match the latest event logged within 1.5 seconds
                if (current.Count > 0)
                {
                    ActivityRecord latest = current[0];
                    if (string.Equals(latest.Title, record.Title, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(latest.ResultBadge, record.ResultBadge, StringComparison.OrdinalIgnoreCase) &&
                        Math.Abs((record.Timestamp.ToUniversalTime() - latest.Timestamp.ToUniversalTime()).TotalSeconds) < 1.5)
                    {
                        return true; // Cleanly suppressed duplicate
                    }
                }

                // Insert newest at beginning
                current.Insert(0, record);

                // Enforce bounded storage
                if (current.Count > _maxRecords)
                {
                    current.RemoveRange(_maxRecords, current.Count - _maxRecords);
                }

                bool saveOk = SaveRecordsInternal(current, out error);
                if (!saveOk)
                {
                    return false;
                }

                _cachedRecords = current;
                return true;
            }
        }

        public static bool AppendRecord(ActivityRecord record)
        {
            string err;
            return AppendRecord(record, out err);
        }

        public static bool AppendRecords(IEnumerable<ActivityRecord> records, out string error)
        {
            error = null;
            if (records == null) return true;

            lock (_syncLock)
            {
                ResetCache();
                List<ActivityRecord> current = Load();

                foreach (var rec in records)
                {
                    if (rec == null) continue;
                    // Deduplication check
                    if (current.Count > 0)
                    {
                        ActivityRecord latest = current[0];
                        if (string.Equals(latest.Title, rec.Title, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(latest.ResultBadge, rec.ResultBadge, StringComparison.OrdinalIgnoreCase) &&
                            Math.Abs((rec.Timestamp.ToUniversalTime() - latest.Timestamp.ToUniversalTime()).TotalSeconds) < 1.5)
                        {
                            continue;
                        }
                    }
                    current.Insert(0, rec);
                }

                if (current.Count > _maxRecords)
                {
                    current.RemoveRange(_maxRecords, current.Count - _maxRecords);
                }

                bool saveOk = SaveRecordsInternal(current, out error);
                if (!saveOk) return false;

                _cachedRecords = current;
                return true;
            }
        }

        public static bool Clear(out string error)
        {
            error = null;
            lock (_syncLock)
            {
                List<ActivityRecord> empty = new List<ActivityRecord>();
                bool ok = SaveRecordsInternal(empty, out error);
                if (ok)
                {
                    _cachedRecords = empty;
                }
                return ok;
            }
        }

        private static bool SaveRecordsInternal(List<ActivityRecord> records, out string error)
        {
            error = null;
            string path = GetFilePath();
            AssertNotProductionInTestMode(path);

            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    if (string.IsNullOrEmpty(_filePathOverride) && string.IsNullOrEmpty(SecurityAclHelper.BaseDirectoryOverride))
                    {
                        string secErr;
                        SecurityAclHelper.ApplyProgramDataDirectorySecurity(dir, out secErr);
                    }
                }
                catch (Exception dirEx)
                {
                    error = "Failed to create directory " + dir + ": " + dirEx.Message;
                    return false;
                }
            }

            string temp = path + ".tmp";
            string bak = GetBackupFilePath();

            Mutex crossProcessMutex = null;
            bool acquiredMutex = false;

            try
            {
                // Attempt cross-process synchronization via named mutex
                try
                {
                    bool createdNew;
                    MutexSecurity mSec = new MutexSecurity();
                    SecurityIdentifier sidSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
                    SecurityIdentifier sidAdmins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                    mSec.AddAccessRule(new MutexAccessRule(sidSystem, MutexRights.FullControl, AccessControlType.Allow));
                    mSec.AddAccessRule(new MutexAccessRule(sidAdmins, MutexRights.FullControl, AccessControlType.Allow));

                    crossProcessMutex = new Mutex(false, MutexName, out createdNew, mSec);
                    acquiredMutex = crossProcessMutex.WaitOne(3000, false);
                }
                catch
                {
                    // Fallback to local lock if named mutex is unsupported in current session context
                }

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 10 * 1024 * 1024;
                string json = serializer.Serialize(records ?? new List<ActivityRecord>());

                // 1. Write temp file with full buffer flush to physical disk
                using (FileStream fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
                {
                    sw.Write(json);
                    sw.Flush();
                    fs.Flush(true);
                }

                // 2. Atomic replacement with backup
                if (File.Exists(path))
                {
                    try
                    {
                        File.Copy(path, bak, true);
                    }
                    catch { }

                    try
                    {
                        File.Replace(temp, path, bak, true);
                    }
                    catch
                    {
                        // Fallback replacement if File.Replace is not supported by filesystem
                        File.Delete(path);
                        File.Move(temp, path);
                    }
                }
                else
                {
                    File.Move(temp, path);
                }

                if (string.IsNullOrEmpty(_filePathOverride) && string.IsNullOrEmpty(SecurityAclHelper.BaseDirectoryOverride))
                {
                    SecurityAclHelper.ApplySensitiveFileSecurity(path);
                    if (File.Exists(bak))
                    {
                        SecurityAclHelper.ApplySensitiveFileSecurity(bak);
                    }
                }

                try
                {
                    _lastKnownDiskWriteUtc = File.GetLastWriteTimeUtc(path);
                }
                catch
                {
                    _lastKnownDiskWriteUtc = DateTime.UtcNow;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = "SaveRecordsInternal failed: " + ex.Message;
                return false;
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                if (acquiredMutex && crossProcessMutex != null)
                {
                    try { crossProcessMutex.ReleaseMutex(); } catch { }
                }
                if (crossProcessMutex != null)
                {
                    try { crossProcessMutex.Close(); } catch { }
                }
            }
        }

        private static void HandleCorruptFile(string primaryPath, Exception ex)
        {
            try
            {
                string corruptArchive = string.Format("{0}.corrupt_{1:yyyyMMdd_HHmmss}", primaryPath, DateTime.UtcNow);
                if (File.Exists(primaryPath))
                {
                    File.Move(primaryPath, corruptArchive);
                    if (string.IsNullOrEmpty(_filePathOverride) && string.IsNullOrEmpty(SecurityAclHelper.BaseDirectoryOverride))
                    {
                        SecurityAclHelper.ApplySensitiveFileSecurity(corruptArchive);
                    }
                }
            }
            catch { }
        }

        private static List<ActivityRecord> CloneRecords(List<ActivityRecord> source)
        {
            if (source == null) return new List<ActivityRecord>();
            List<ActivityRecord> clone = new List<ActivityRecord>(source.Count);
            foreach (var r in source)
            {
                if (r == null) continue;
                ActivityRecord copy = new ActivityRecord
                {
                    Id = r.Id,
                    Timestamp = r.Timestamp,
                    Title = r.Title,
                    Context = r.Context,
                    Category = r.Category,
                    ResultBadge = r.ResultBadge,
                    BadgeColorHex = r.BadgeColorHex
                };
                if (!string.IsNullOrEmpty(r.BadgeColorHex))
                {
                    try { copy.BadgeColor = ColorTranslator.FromHtml(r.BadgeColorHex); } catch { }
                }
                clone.Add(copy);
            }
            return clone;
        }
    }
}
