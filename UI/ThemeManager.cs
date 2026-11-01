// ═══════════════════════════════════════════════════════════════════════════
// ThemeManager — Dynamic Runtime Theme Switching & Persistence for v2.1
// USB Shield Pro v2.1
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using Microsoft.Win32;
using UsbShieldPro.Persistence;

namespace UsbShieldPro.UI
{
    public static class ThemeManager
    {
        private static readonly object _syncLock = new object();

        private static readonly IThemePalette _charcoalRed = new CharcoalRedPalette();
        private static readonly IThemePalette _midnight = new MidnightPalette();
        private static readonly IThemePalette _graphite = new GraphitePalette();
        private static readonly IThemePalette _porcelain = new PorcelainPalette();
        private static readonly IThemePalette _cloud = new CloudPalette();

        private static AppTheme _currentTheme = AppTheme.CharcoalRed;
        private static IThemePalette _activePalette = _charcoalRed;

        private static Func<int> _registryReaderSeam = null;

        public static Func<int> RegistryReaderSeam
        {
            get { return Core.DeviceEnforcementLifecycleManager.IsTestMode ? _registryReaderSeam : null; }
            set { if (!Core.DeviceEnforcementLifecycleManager.IsTestMode) { _registryReaderSeam = null; return; } _registryReaderSeam = value; }
        }

        public static void ResetSeams()
        {
            _registryReaderSeam = null;
        }

        public static event EventHandler ThemeChanged;

        static ThemeManager()
        {
            InitializeFromSettings();
        }

        public static AppTheme CurrentTheme
        {
            get
            {
                lock (_syncLock)
                {
                    return _currentTheme;
                }
            }
        }

        public static IThemePalette ActivePalette
        {
            get
            {
                lock (_syncLock)
                {
                    return _activePalette;
                }
            }
        }

        public static bool IsDarkTheme
        {
            get
            {
                lock (_syncLock)
                {
                    return _activePalette == null || _activePalette.IsDark;
                }
            }
        }

        /// <summary>
        /// Initializes the active theme from persistent user settings.
        /// Defaults to Charcoal Red (Signature) if unconfigured or unrecognized.
        /// </summary>
        public static void InitializeFromSettings()
        {
            lock (_syncLock)
            {
                try
                {
                    AppSettingsData settings = SettingsRepository.Load();
                    string pref = (settings != null) ? settings.ThemePreference : null;
                    AppTheme theme = AppTheme.CharcoalRed;

                    if (!string.IsNullOrEmpty(pref))
                    {
                        try
                        {
                            theme = (AppTheme)Enum.Parse(typeof(AppTheme), pref, true);
                        }
                        catch
                        {
                            theme = AppTheme.CharcoalRed;
                        }
                    }

                    _currentTheme = theme;
                    _activePalette = ResolveTheme(theme);
                }
                catch
                {
                    _currentTheme = AppTheme.CharcoalRed;
                    _activePalette = _charcoalRed;
                }
            }
        }

        /// <summary>
        /// Switches the active theme at runtime without requiring an application restart.
        /// Optionally persists the choice to user settings.
        /// </summary>
        public static void SetTheme(AppTheme theme, bool persist = true)
        {
            EventHandler handler = null;
            lock (_syncLock)
            {
                _currentTheme = theme;
                IThemePalette newPalette = ResolveTheme(theme);
                bool changed = (_activePalette != newPalette);
                _activePalette = newPalette;

                if (persist)
                {
                    try
                    {
                        AppSettingsData settings = SettingsRepository.Load();
                        if (settings != null)
                        {
                            settings.ThemePreference = theme.ToString();
                            SettingsRepository.Save(settings);
                        }
                    }
                    catch { }
                }

                handler = ThemeChanged;
            }

            if (handler != null)
            {
                handler(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Resolves an AppTheme selection to a concrete IThemePalette.
        /// FollowWindows resolves to Porcelain (light) if AppsUseLightTheme == 1,
        /// or CharcoalRed (signature dark) if AppsUseLightTheme == 0.
        /// </summary>
        public static IThemePalette ResolveTheme(AppTheme theme)
        {
            switch (theme)
            {
                case AppTheme.CharcoalRed:
                    return _charcoalRed;
                case AppTheme.Midnight:
                    return _midnight;
                case AppTheme.Graphite:
                    return _graphite;
                case AppTheme.Porcelain:
                    return _porcelain;
                case AppTheme.Cloud:
                    return _cloud;
                case AppTheme.FollowWindows:
                    int lightMode = ReadAppsUseLightThemeRegistry(0);
                    return (lightMode == 1) ? _porcelain : _charcoalRed;
                default:
                    return _charcoalRed;
            }
        }

        /// <summary>
        /// Reads HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme once.
        /// No continuous polling timer is used.
        /// </summary>
        public static int ReadAppsUseLightThemeRegistry(int defaultValue = 0)
        {
            if (RegistryReaderSeam != null)
            {
                return RegistryReaderSeam();
            }

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("AppsUseLightTheme");
                        if (val is int) return (int)val;
                        if (val != null)
                        {
                            int parsed;
                            if (int.TryParse(val.ToString(), out parsed)) return parsed;
                        }
                    }
                }
            }
            catch { }
            return defaultValue;
        }

        /// <summary>
        /// Called when Windows signals WM_SETTINGCHANGE.
        /// If FollowWindows is active, re-reads the registry and triggers ThemeChanged if resolved palette changed.
        /// </summary>
        public static void OnWindowsSettingChanged()
        {
            EventHandler handler = null;
            lock (_syncLock)
            {
                if (_currentTheme == AppTheme.FollowWindows)
                {
                    IThemePalette newPalette = ResolveTheme(AppTheme.FollowWindows);
                    if (_activePalette != newPalette)
                    {
                        _activePalette = newPalette;
                        handler = ThemeChanged;
                    }
                }
            }

            if (handler != null)
            {
                handler(null, EventArgs.Empty);
            }
        }
    }
}
