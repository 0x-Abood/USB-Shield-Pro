// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Settings & Policy Configuration Repository
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Web.Script.Serialization;
using UsbShieldPro.Models;

namespace UsbShieldPro.Persistence
{
    public class AppSettingsData
    {
        public bool AutoStartEnabled { get; set; }
        public bool AutoLockOnStartup { get; set; }
        public bool MinimizeToTray { get; set; }
        public string LanguagePreference { get; set; }
        public string ThemePreference { get; set; }
        public bool OnboardingCompleted { get; set; }
        public bool FullProtectionModeEnabled { get; set; }
        public bool SidebarCompact { get; set; }
        public ProtectionPolicy ActivePolicy { get; set; }

        public AppSettingsData()
        {
            AutoStartEnabled = false;
            AutoLockOnStartup = false;
            MinimizeToTray = true;
            LanguagePreference = "ar";
            ThemePreference = "CharcoalRed";
            OnboardingCompleted = false;
            FullProtectionModeEnabled = false;
            SidebarCompact = false;
            ActivePolicy = new ProtectionPolicy();
        }

        public AppSettingsData Clone()
        {
            return new AppSettingsData
            {
                AutoStartEnabled = this.AutoStartEnabled,
                AutoLockOnStartup = this.AutoLockOnStartup,
                MinimizeToTray = this.MinimizeToTray,
                LanguagePreference = this.LanguagePreference,
                ThemePreference = this.ThemePreference,
                OnboardingCompleted = this.OnboardingCompleted,
                FullProtectionModeEnabled = this.FullProtectionModeEnabled,
                SidebarCompact = this.SidebarCompact,
                ActivePolicy = this.ActivePolicy != null ? this.ActivePolicy.Clone() : new ProtectionPolicy()
            };
        }
    }

    /// <summary>
    /// Manages application settings and active protection policy with durable, crash-safe persistence.
    /// INVARIANT: Cache is committed ONLY AFTER durable disk persistence succeeds.
    /// INVARIANT: Memory and durable settings do not diverge silently upon write failure.
    /// INVARIANT: Recovers from backup (settings.bak) if primary file is missing or corrupted.
    /// </summary>
    public static class SettingsRepository
    {
        private static readonly object _syncLock = new object();
        private static AppSettingsData _cachedSettings;

        private static Func<AppSettingsData, string> _faultInjectionSaveSeam = null;
        private static Func<AppSettingsData> _faultInjectionLoadSeam = null;

        public static Func<AppSettingsData, string> FaultInjectionSaveSeam
        {
            get { return Core.DeviceEnforcementLifecycleManager.IsTestMode ? _faultInjectionSaveSeam : null; }
            set { if (!Core.DeviceEnforcementLifecycleManager.IsTestMode) { _faultInjectionSaveSeam = null; return; } _faultInjectionSaveSeam = value; }
        }

        public static Func<AppSettingsData> FaultInjectionLoadSeam
        {
            get { return Core.DeviceEnforcementLifecycleManager.IsTestMode ? _faultInjectionLoadSeam : null; }
            set { if (!Core.DeviceEnforcementLifecycleManager.IsTestMode) { _faultInjectionLoadSeam = null; return; } _faultInjectionLoadSeam = value; }
        }

        public static void ResetSeams()
        {
            _faultInjectionSaveSeam = null;
            _faultInjectionLoadSeam = null;
        }

        public static string GetFilePath()
        {
            return Path.Combine(SecurityAclHelper.GetProgramDataDirectory(), "settings.json");
        }

        public static string GetBackupFilePath()
        {
            return Path.Combine(SecurityAclHelper.GetProgramDataDirectory(), "settings.bak");
        }

        public static string GetTempFilePath()
        {
            return Path.Combine(SecurityAclHelper.GetProgramDataDirectory(), "settings.tmp");
        }

        public static void ResetCache()
        {
            lock (_syncLock)
            {
                _cachedSettings = null;
            }
        }

        /// <summary>
        /// Loads settings from primary file, falls back to backup file on corruption/absence,
        /// and returns an isolated clone to prevent uncontrolled in-memory mutation.
        /// </summary>
        public static AppSettingsData Load()
        {
            if (FaultInjectionLoadSeam != null)
            {
                return FaultInjectionLoadSeam();
            }

            lock (_syncLock)
            {
                if (_cachedSettings != null) return _cachedSettings.Clone();

                string path = GetFilePath();
                string bakPath = GetBackupFilePath();
                JavaScriptSerializer serializer = new JavaScriptSerializer();

                // 1. Try primary settings file
                if (File.Exists(path))
                {
                    try
                    {
                        string json = File.ReadAllText(path);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            AppSettingsData loaded = serializer.Deserialize<AppSettingsData>(json);
                            if (loaded != null)
                            {
                                if (loaded.ActivePolicy == null) loaded.ActivePolicy = new ProtectionPolicy();
                                _cachedSettings = loaded;
                                return _cachedSettings.Clone();
                            }
                        }
                    }
                    catch
                    {
                        // Primary file read or deserialize failed -> fall through to backup
                    }
                }

                // 2. Try backup settings file
                if (File.Exists(bakPath))
                {
                    try
                    {
                        string bakJson = File.ReadAllText(bakPath);
                        if (!string.IsNullOrWhiteSpace(bakJson))
                        {
                            AppSettingsData bakLoaded = serializer.Deserialize<AppSettingsData>(bakJson);
                            if (bakLoaded != null)
                            {
                                if (bakLoaded.ActivePolicy == null) bakLoaded.ActivePolicy = new ProtectionPolicy();
                                _cachedSettings = bakLoaded;
                                return _cachedSettings.Clone();
                            }
                        }
                    }
                    catch { }
                }

                // 3. Fallback to clean default settings
                _cachedSettings = new AppSettingsData();
                return _cachedSettings.Clone();
            }
        }

        /// <summary>
        /// Durably persists settings to disk and advances in-memory cache ONLY upon verified success.
        /// Surfaces typed errors and preserves last known-good state if persistence fails.
        /// </summary>
        public static bool Save(AppSettingsData settings, out string error)
        {
            error = null;
            if (settings == null)
            {
                error = "Cannot persist null settings.";
                return false;
            }

            if (FaultInjectionSaveSeam != null)
            {
                string fiErr = FaultInjectionSaveSeam(settings);
                if (!string.IsNullOrEmpty(fiErr))
                {
                    error = fiErr;
                    return false;
                }
            }

            lock (_syncLock)
            {
                string path = GetFilePath();
                string temp = GetTempFilePath();
                string bak = GetBackupFilePath();

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json;
                try
                {
                    json = serializer.Serialize(settings);
                }
                catch (Exception ex)
                {
                    error = "Failed to serialize settings: " + ex.Message;
                    return false;
                }

                try
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                        SecurityAclHelper.EnsureProgramDataAcls();
                    }

                    // 1. Write to temporary file and flush sectors
                    using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var sw = new StreamWriter(fs))
                    {
                        sw.Write(json);
                        sw.Flush();
                        fs.Flush(true);
                    }

                    // 2. Atomic replacement with automatic backup creation
                    if (File.Exists(path))
                    {
                        try
                        {
                            File.Replace(temp, path, bak, true);
                        }
                        catch
                        {
                            // If Replace fails, preserve backup first before delete/move
                            try { File.Copy(path, bak, true); } catch { }
                            File.Delete(path);
                            File.Move(temp, path);
                        }
                    }
                    else
                    {
                        File.Move(temp, path);
                    }

                    // 3. Apply security permissions
                    if (string.IsNullOrEmpty(SecurityAclHelper.BaseDirectoryOverride))
                    {
                        SecurityAclHelper.ApplySensitiveFileSecurity(path);
                        if (File.Exists(bak))
                        {
                            SecurityAclHelper.ApplySensitiveFileSecurity(bak);
                        }
                    }

                    // 4. CRITICAL INVARIANT: Advance cache ONLY after durable persistence succeeds!
                    _cachedSettings = settings.Clone();
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    // Invariant: Do NOT advance cache on failure!
                    try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                    return false;
                }
            }
        }

        public static void Save(AppSettingsData settings)
        {
            string err;
            Save(settings, out err);
        }

        public static ProtectionPolicy LoadPolicy()
        {
            return Load().ActivePolicy ?? new ProtectionPolicy();
        }

        public static bool SavePolicy(ProtectionPolicy policy, out string error)
        {
            error = null;
            if (policy == null)
            {
                error = "Cannot persist null policy.";
                return false;
            }

            lock (_syncLock)
            {
                ResetCache();
                var s = Load();
                s.ActivePolicy = policy;
                return Save(s, out error);
            }
        }

        public static void SavePolicy(ProtectionPolicy policy)
        {
            string err;
            SavePolicy(policy, out err);
        }
    }
}
