// ═══════════════════════════════════════════════════════════════════════════
// MainForm — USB Shield Pro v2.0
// Complete 3-Layer UI/UX Architecture
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using UsbShieldPro.Core;
using UsbShieldPro.Devices;
using UsbShieldPro.Models;
using UsbShieldPro.Persistence;
using UsbShieldPro.Security;
using UsbShieldPro.Service;
using UsbShieldPro.UI;

namespace UsbShieldPro
{
    internal class MainForm : Form
    {
        private float _scale = 1.0f;

        // Native drag support
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        // Core UI components (v2.1 Integrated Modern Shell)
        private AppTopBar _topBar;
        private AppSidebar _sidebar;
        private Panel _contentPanel;
        private bool _userPreferredCompactSidebar = false;
        private bool _isAutoCompacting = false;

        public Panel ContentContainer { get { return _contentPanel; } }

        public bool IsSidebarCompact
        {
            get { return _sidebar != null && _sidebar.IsCompact; }
            set { if (_sidebar != null) _sidebar.IsCompact = value; }
        }

        public AppSidebar Sidebar
        {
            get { return _sidebar; }
        }

        public AppTopBar TopBar
        {
            get { return _topBar; }
        }

        // Tab Views (0: Dashboard, 1: Devices, 2: Protection, 3: Trusted, 4: Activity, 5: Recovery, 6: Settings)
        private Panel _tabDashboard;
        private Panel _tabDevices;
        private Panel _tabRules;
        private Panel _tabWhitelist;
        private Panel _tabActivity;
        private Panel _tabRecovery;
        private Panel _tabSettings;

        // Dashboard controls (v2.1 Redesigned)
        private DarkScrollPanel _tabDashboardScroll;
        private ProtectionStatusCard _protectionStatusCard;
        private CompactTemporaryAccessModule _tempAccessModule;
        private SummaryMetricCard _metricConnectedDevices;
        private SummaryMetricCard _metricActiveRules;
        private SummaryMetricCard _metricTrustedDevices;
        private RecentActivityPanel _recentActivityPanel;
        private ProtectionOverviewPanel _protectionOverviewPanel;
        private SafetyMessageBar _safetyMessageBar;
        private Label _dashboardPageTitle;
        private Label _dashboardPageSub;

        internal ProtectionStatusCard ProtectionStatusCard { get { return _protectionStatusCard; } }
        internal CompactTemporaryAccessModule TempAccessModule { get { return _tempAccessModule; } }
        internal SummaryMetricCard MetricConnectedDevices { get { return _metricConnectedDevices; } }
        internal SummaryMetricCard MetricActiveRules { get { return _metricActiveRules; } }
        internal SummaryMetricCard MetricTrustedDevices { get { return _metricTrustedDevices; } }
        internal RecentActivityPanel RecentActivityPanel { get { return _recentActivityPanel; } }
        internal ProtectionOverviewPanel ProtectionOverviewPanel { get { return _protectionOverviewPanel; } }
        internal SafetyMessageBar SafetyMessageBar { get { return _safetyMessageBar; } }
        internal DarkScrollPanel TabDashboardScroll { get { return _tabDashboardScroll; } }
        internal void TriggerLayoutDashboard() { LayoutDashboard(true); }

        // Devices controls (v2.1 Phase 4.2 Redesigned)
        private DarkScrollPanel _devicesScroll;
        private Panel _devicesHeaderPanel;
        private Panel _devicesBodyPanel;
        private DeviceDetailsDrawer _devicesDrawer;
        private DeviceEmptyState _devicesEmptyState;
        private AppSearchBox _devicesSearchBox;
        private AppDropdown _devicesFilterDropdown;
        private AppButton _btnEjectAllDevices;
        private IconButton _btnRefreshDevices;
        private Label _devicesPageTitle;
        private Label _devicesPageSub;
        private List<DeviceIdentity> _cachedDiscoveredDevices = new List<DeviceIdentity>();
        private List<DeviceRow> _activeDeviceRows = new List<DeviceRow>();
        private DeviceIdentity _selectedDevice = null;

        public List<DeviceIdentity> TestIsolatedDevices { get; set; }
        public Panel DevicesHeaderPanel { get { return _devicesHeaderPanel; } }
        public AppButton BtnEjectAllDevices { get { return _btnEjectAllDevices; } }
        public IconButton BtnRefreshDevices { get { return _btnRefreshDevices; } }
        public DeviceDetailsDrawer DevicesDrawer { get { return _devicesDrawer; } }
        public TextBox DevicesSearchBox { get { return (_devicesSearchBox != null) ? _devicesSearchBox.InnerTextBox : null; } }
        public AppSearchBox DevicesSearchControl { get { return _devicesSearchBox; } }
        public AppDropdown DevicesFilterDropdown { get { return _devicesFilterDropdown; } }
        public DeviceEmptyState DevicesEmptyState { get { return _devicesEmptyState; } }
        public List<DeviceRow> ActiveDeviceRows { get { return _activeDeviceRows; } }
        public DarkScrollPanel TabDevicesScroll { get { return _devicesScroll; } }

        // Protection controls (v2.1 Phase 4.3 Redesign)
        private DarkScrollPanel _tabRulesScroll;
        private Label _protectionPageTitle;
        private Label _protectionPageSub;
        private AppButton _btnRefreshProtection;
        private ProtectionPostureCard _postureCard;
        private SafetyMessageBar _mismatchBar;
        private ProtectionSectionCard _secStorageCard;
        private ProtectionSectionCard _secDeviceCard;
        private ProtectionSectionCard _secAutoCard;
        private ProtectionSectionCard _secAdvancedCard;
        private ProtectionSettingRow _rowRemovableDisks;
        private ProtectionSettingRow _rowReadOnly;
        private ProtectionSettingRow _rowPhones;
        private ProtectionSettingRow _rowWhitelist;
        private ProtectionSettingRow _rowFlashSentinel;
        private ProtectionSettingRow _rowAutoPlayGuard;
        private ProtectionSettingRow _rowStrictPnp;
        private ProtectionSettingRow _rowHardBlock;
        private AppButton _btnManageTrusted;

        public DarkScrollPanel TabRulesScroll { get { return _tabRulesScroll; } }
        public ProtectionPostureCard PostureCard { get { return _postureCard; } }
        public SafetyMessageBar MismatchBar { get { return _mismatchBar; } }
        public bool IsMismatchActive { get { return _mismatchBar != null && _mismatchBar.IsActive; } }
        public ProtectionSectionCard SecStorageCard { get { return _secStorageCard; } }
        public ProtectionSectionCard SecDeviceCard { get { return _secDeviceCard; } }
        public ProtectionSectionCard SecDeviceControlSectionCard { get { return _secDeviceCard; } }
        public ProtectionSectionCard SecAutoCard { get { return _secAutoCard; } }
        public ProtectionSectionCard SecAdvancedCard { get { return _secAdvancedCard; } }
        public ProtectionSettingRow RowRemovableDisks { get { return _rowRemovableDisks; } }
        public ProtectionSettingRow RowReadOnly { get { return _rowReadOnly; } }
        public ProtectionSettingRow RowPhones { get { return _rowPhones; } }
        public ProtectionSettingRow RowWhitelist { get { return _rowWhitelist; } }
        public ProtectionSettingRow RowFlashSentinel { get { return _rowFlashSentinel; } }
        public ProtectionSettingRow RowAutoPlayGuard { get { return _rowAutoPlayGuard; } }
        public ProtectionSettingRow RowStrictPnp { get { return _rowStrictPnp; } }
        public ProtectionSettingRow RowHardBlock { get { return _rowHardBlock; } }
        public AppButton BtnRefreshProtection { get { return _btnRefreshProtection; } }
        public AppButton BtnManageTrusted { get { return _btnManageTrusted; } }
        public int ActiveTabIndex { get { return _sidebar != null ? _sidebar.SelectedIndex : 0; } }
        public void TriggerSyncRulesWithSettings() { SyncRuleTogglesWithSettings(); }
        public void TriggerRuleToggled() { OnRuleToggled(); }
        public void TriggerLayoutProtectionCards() { LayoutProtectionCards(); }

        // Trusted Devices controls (v2.1 Phase 4.4 Redesign)
        private DarkScrollPanel _trustedScroll;
        private Panel _trustedHeaderPanel;
        private Panel _trustedSummaryStrip;
        private Panel _trustedBodyPanel;
        private TrustedDeviceDetailsDrawer _trustedDrawer;
        private TrustedEmptyState _trustedEmptyState;
        private AppSearchBox _trustedSearchBox;
        private AppDropdown _trustedFilterDropdown;
        private AppButton _btnAddTrustedDevice;
        private IconButton _btnRefreshTrusted;
        private Label _trustedPageTitle;
        private Label _trustedPageSub;
        private SummaryMetricCard _metricTotalTrusted;
        private SummaryMetricCard _metricConnectedTrusted;
        private List<TrustedDeviceRow> _activeTrustedRows = new List<TrustedDeviceRow>();
        private DeviceRule _selectedTrustedRule = null;
        private List<DeviceRule> _cachedRules = new List<DeviceRule>();

        public Panel TrustedHeaderPanel { get { return _trustedHeaderPanel; } }
        public Panel TrustedSummaryStrip { get { return _trustedSummaryStrip; } }
        public TrustedDeviceDetailsDrawer TrustedDrawer { get { return _trustedDrawer; } }
        public TextBox TrustedSearchBox { get { return (_trustedSearchBox != null) ? _trustedSearchBox.InnerTextBox : null; } }
        public AppSearchBox TrustedSearchControl { get { return _trustedSearchBox; } }
        public AppDropdown TrustedFilterDropdown { get { return _trustedFilterDropdown; } }
        public TrustedEmptyState TrustedEmptyState { get { return _trustedEmptyState; } }
        public List<TrustedDeviceRow> ActiveTrustedRows { get { return _activeTrustedRows; } }
        public DarkScrollPanel TabTrustedScroll { get { return _trustedScroll; } }
        public SummaryMetricCard MetricTotalTrusted { get { return _metricTotalTrusted; } }
        public SummaryMetricCard MetricConnectedTrusted { get { return _metricConnectedTrusted; } }
        public AppButton BtnAddTrustedDevice { get { return _btnAddTrustedDevice; } }
        public IconButton BtnRefreshTrusted { get { return _btnRefreshTrusted; } }
        public Label TrustedPageTitle { get { return _trustedPageTitle; } }
        public Label TrustedPageSub { get { return _trustedPageSub; } }
        public void TriggerSelectTrustedRow(TrustedDeviceRow row) { OnTrustedRowSelected(row); }
        public void TriggerApplyTrustedFilters() { ApplyTrustedFilters(); }
        public void TriggerLayoutWhitelist()
        {
            if (_trustedHeaderPanel != null) _trustedHeaderPanel.PerformLayout();
            if (_trustedSummaryStrip != null) _trustedSummaryStrip.PerformLayout();
            if (_trustedBodyPanel != null) _trustedBodyPanel.PerformLayout();
            ResizeTrustedRows();
        }

        // Activity controls (v2.1 Phase 4.5 Production Redesign)
        private Panel _activityHeaderPanel;
        private Label _activityPageTitle;
        private Label _activityPageSub;
        private AppButton _btnClearActivity;
        private Panel _activityToolbarPanel;
        private Panel _activityBodyPanel;
        private DarkScrollPanel _activityScroll;
        private AppSearchBox _activitySearchBox;
        private AppDropdown _activityCatDropdown;
        private AppDropdown _activityDateDropdown;
        private Panel _customDatePanel;
        private DateTimePicker _dateFromPicker;
        private DateTimePicker _dateToPicker;
        private AppButton _btnApplyDateFilter;
        private ActivityEmptyState _activityEmptyState;
        private ActivityDetailsDrawer _activityDrawer;
        private List<ActivityRow> _activeActivityRows = new List<ActivityRow>();
        private ActivityRecord _selectedActivityRecord;
        private List<ActivityRecord> _activityRecords = new List<ActivityRecord>();

        // Isolated test-only properties (zero disk writes, purely in-memory for testing/captures)
        public List<DeviceRule> TestIsolatedWhitelistRules { get; set; }
        public List<ActivityRecord> TestIsolatedActivityRecords { get; set; }
        public ProtectionStatusReport TestIsolatedReport { get; set; }

        public Panel ActivityHeaderPanel { get { return _activityHeaderPanel; } }
        public Panel ActivityToolbarPanel { get { return _activityToolbarPanel; } }
        public Panel ActivityBodyPanel { get { return _activityBodyPanel; } }
        public Panel ActivitySearchContainer { get { return _activitySearchBox; } }
        public AppDropdown ActivityCategoryDropdown { get { return _activityCatDropdown; } }
        public AppDropdown ActivityDateDropdown { get { return _activityDateDropdown; } }
        public TextBox ActivitySearchBox { get { return (_activitySearchBox != null) ? _activitySearchBox.InnerTextBox : null; } }
        public AppSearchBox ActivitySearchControl { get { return _activitySearchBox; } }
        public ActivityDetailsDrawer ActivityDrawer { get { return _activityDrawer; } }
        public ActivityEmptyState ActivityEmptyState { get { return _activityEmptyState; } }
        public List<ActivityRow> ActiveActivityRows { get { return _activeActivityRows; } }
        public AppButton ClearActivityButton { get { return _btnClearActivity; } }
        public Label ActivityPageTitle { get { return _activityPageTitle; } }
        public Label ActivityPageSub { get { return _activityPageSub; } }
        public void TriggerSelectActivityRow(ActivityRow row) { OnActivityRowSelected(row); }
        public void TriggerLayoutActivity()
        {
            if (_activityDrawer != null)
            {
                _activityDrawer.Dock = Loc.IsRtl ? DockStyle.Left : DockStyle.Right;
                _activityDrawer.RightToLeft = Loc.RtlValue;
                _activityDrawer.SendToBack();
            }
        }
        public void TriggerSwitchTab(int tabIndex) { SwitchTab(tabIndex); }
        public void TriggerToggleLanguage() { ToggleLanguage(); }
        public void TriggerRefreshDevices() { RefreshDevicesList(); }
        public void TriggerRefreshWhitelist() { RefreshWhitelistView(); }
        public void TriggerRefreshActivity() { RefreshActivityView(); }
        public void TriggerSelectDeviceRow(DeviceRow row) { OnDeviceRowSelected(row); }
        public void TriggerLayoutDevices()
        {
            if (_devicesDrawer != null)
            {
                _devicesDrawer.Dock = Loc.IsRtl ? DockStyle.Left : DockStyle.Right;
                _devicesDrawer.RightToLeft = Loc.RtlValue;
                _devicesDrawer.SendToBack();
            }
            if (_devicesBodyPanel != null) _devicesBodyPanel.RightToLeft = Loc.RtlValue;
            if (_devicesScroll != null)
            {
                _devicesScroll.RightToLeft = Loc.RtlValue;
                _devicesScroll.BringToFront();
            }
            ResizeDeviceRows();
        }
        public void ScrollSettingsToBottom()
        {
            if (_tabSettingsScroll != null)
            {
                _tabSettingsScroll.ScrollToBottom();
            }
        }

        // Recovery controls (v2.1 Phase 4.6 Redesign)
        private DarkScrollPanel _tabRecoveryScroll;
        private Label _recoveryPageTitle;
        private Label _recoveryPageSub;
        private RecoveryPostureCard _recoveryPostureCard;
        private RecoverySemanticsMessageBar _recoverySemanticsBar;
        private AppButton _btnRestoreState;
        private AppButton _btnOpenFolder;
        private AppButton _btnExportPackage;
        private RecoveryArtifactsCard _recoveryArtifactsCard;
        private RecoveryTechnicalAccordion _recoveryAccordion;

        // Recovery accessors for testing and automation
        public RecoveryPostureCard RecoveryPostureCard { get { return _recoveryPostureCard; } }
        public RecoverySemanticsMessageBar RecoverySemanticsBar { get { return _recoverySemanticsBar; } }
        public AppButton BtnRestoreState { get { return _btnRestoreState; } }
        public AppButton BtnOpenFolder { get { return _btnOpenFolder; } }
        public AppButton BtnExportPackage { get { return _btnExportPackage; } }
        public RecoveryArtifactsCard RecoveryArtifactsCard { get { return _recoveryArtifactsCard; } }
        public RecoveryTechnicalAccordion RecoveryAccordion { get { return _recoveryAccordion; } }
        public DarkScrollPanel TabRecoveryScroll { get { return _tabRecoveryScroll; } }
        public void TriggerLayoutRecovery() { LayoutRecoveryTab(); }

        // Settings controls (v2.1 Redesigned)
        private DarkScrollPanel _tabSettingsScroll;
        private Label _settingsPageTitle;
        private Label _settingsPageSub;
        private SettingsCard _cardAppearance;
        private AppDropdown _ddTheme;
        private AppDropdown _ddLanguage;
        private AppToggle _toggleSidebar;
        private SettingsCard _cardBehavior;
        private AppToggle _toggleAutoStart;
        private AppToggle _toggleAutoLock;
        private AppToggle _toggleTray;
        private SettingsSecurityCard _cardSecurity;
        private SettingsServiceCard _cardService;
        private SettingsAboutCard _cardAbout;
        private bool _isSyncingTheme = false;
        private bool _isSyncingLanguage = false;

        public DarkScrollPanel TabSettingsScroll { get { return _tabSettingsScroll; } }
        public Label SettingsPageTitle { get { return _settingsPageTitle; } }
        public Label SettingsPageSub { get { return _settingsPageSub; } }
        public SettingsCard CardAppearance { get { return _cardAppearance; } }
        public SettingsCard CardBehavior { get { return _cardBehavior; } }
        public SettingsSecurityCard CardSecurity { get { return _cardSecurity; } }
        public SettingsServiceCard CardService { get { return _cardService; } }
        public SettingsAboutCard CardAbout { get { return _cardAbout; } }
        public AppDropdown SettingsThemeDropdown { get { return _ddTheme; } }
        public AppDropdown SettingsLanguageDropdown { get { return _ddLanguage; } }
        public AppToggle SettingsSidebarToggle { get { return _toggleSidebar; } }
        public AppToggle SettingsAutoStartToggle { get { return _toggleAutoStart; } }
        public AppToggle SettingsAutoLockToggle { get { return _toggleAutoLock; } }
        public AppToggle SettingsTrayToggle { get { return _toggleTray; } }
        public void TriggerRefreshSettings() { RefreshSettingsTab(); }
        public void TriggerLayoutSettings() { LayoutSettingsTab(); }
        public void ScrollSettingsToTop()
        {
            if (_tabSettingsScroll != null)
            {
                _tabSettingsScroll.ScrollToTop();
            }
        }
        public void ScrollSettingsTo(int y)
        {
            if (_tabSettingsScroll != null)
            {
                _tabSettingsScroll.ScrollTo(y);
            }
        }

        // Timers & Background
        private Timer _clockTimer;
        private TrayManager _tray;
        private bool _silentStart;
        private int _deviceRefreshCounter = 0;
        private int _isExpiringTempAccess = 0;
        private System.Threading.EventWaitHandle _trayWakeupEvent;
        private System.Threading.RegisteredWaitHandle _registeredTrayWakeup;
        public static bool IsCaptureMode = false;
#if !PRODUCTION
        public static bool PreserveSimulatedDpi = false;
#endif

        public MainForm()
        {
            _silentStart = Environment.CommandLine.Contains("--silent");

            // Load settings
            AppSettingsData settings = SettingsRepository.Load();
            if (settings.LanguagePreference == "ar")
                Loc.Current = Language.Arabic;
            else
                Loc.Current = Language.English;

            try
            {
                var culture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
                culture.NumberFormat.DigitSubstitution = System.Globalization.DigitShapes.None;
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;

                var uiCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentUICulture.Clone();
                uiCulture.NumberFormat.DigitSubstitution = System.Globalization.DigitShapes.None;
                System.Threading.Thread.CurrentThread.CurrentUICulture = uiCulture;
            }
            catch { }

            // DPI Scaling
            using (Graphics g = CreateGraphics())
            {
#if !PRODUCTION
                if (!PreserveSimulatedDpi)
                {
                    _scale = g.DpiX / 96f;
                    DpiManager.CurrentDpiScale = _scale;
                }
                else
                {
                    _scale = DpiManager.CurrentDpiScale;
                }
#else
                _scale = g.DpiX / 96f;
                DpiManager.CurrentDpiScale = _scale;
#endif
            }

            InitializeWindow();
            // Production startup: zero fake/seed activity records (Activity Log reflects real events only)
            BuildUI();

            ThemeManager.ThemeChanged += OnThemeChanged;

            if (!IsCaptureMode)
            {
                _tray = new TrayManager(this);

                // Register Sentinel Event
                ActiveSentinel.DeviceIntercepted += OnDeviceIntercepted;
                ActiveSentinel.DeviceEnforcementPending += OnDeviceEnforcementPending;

                // Reconcile startup state (e.g. if temporary access expired while PC was off)
                bool wasExpired;
                TemporaryAccessManager.ReconcileStartupState(out wasExpired);
                if (wasExpired)
                {
                    LogActivity(Loc.Get("EventTempAccessExpired"));
                }

                // Vector 10A: AutoLockOnStartup early silent enforcement (if enabled and temp access not active)
                AppSettingsData startupSettings = SettingsRepository.Load();
                if (startupSettings != null && startupSettings.AutoLockOnStartup && !TemporaryAccessManager.IsActive)
                {
                    if (EnforcementOwnerResolver.IsGuiEnforcementAllowed())
                    {
                        bool policyModified = false;
                        ProtectionPolicy pol = startupSettings.ActivePolicy ?? new ProtectionPolicy();
                        if (!pol.IsAnyProtectionActive())
                        {
                            pol.BlockRemovableDisks = true;
                            policyModified = true;
                        }
                        List<string> applyErrors;
                        ProtectionEngine.ApplyPolicy(pol, null, out applyErrors);
                        if (policyModified)
                        {
                            startupSettings.ActivePolicy = pol;
                            SettingsRepository.Save(startupSettings);
                        }
                        LogActivity(Loc.Get("EventAutoLockEnforced"), ActivityCategory.PolicyChanges, Loc.Get("BadgeEnforced"), Theme.AccentGreen);
                    }
                }

                // Vector 10B: Register background wait on tray wakeup event
                try
                {
                    bool createdEvent;
                    _trayWakeupEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, "Global\\UsbShieldPro_TrayWakeup_Event", out createdEvent);
                    _registeredTrayWakeup = System.Threading.ThreadPool.RegisterWaitForSingleObject(_trayWakeupEvent, (state, timedOut) =>
                    {
                        if (!timedOut)
                        {
                            try
                            {
                                if (this.IsHandleCreated && !this.IsDisposed)
                                {
                                    this.BeginInvoke(new MethodInvoker(delegate { ShowFromTray(); }));
                                }
                            }
                            catch { }
                        }
                    }, null, -1, false);
                }
                catch { }
            }

            // Verify live policy state
            RefreshLiveStatus();
            RefreshDevicesList();
            RefreshWhitelistView();

            if (!IsCaptureMode)
            {
                // Setup clock timer (1 second interval)
                _clockTimer = new Timer();
                _clockTimer.Interval = 1000;
                _clockTimer.Tick += ClockTimer_Tick;
                _clockTimer.Start();
            }

            if (_silentStart)
            {
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
                Visible = false;
            }
        }

        private void InitializeWindow()
        {
            Text = Loc.Get("AppTitle");
            ShowIcon = true;
            Icon = BrandAssets.AppIcon;
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            int w = DpiManager.ScaleInt(1366);
            int h = DpiManager.ScaleInt(768);
            Size = new Size(w, h);
            MinimumSize = new Size(DpiManager.ScaleInt(1024), DpiManager.ScaleInt(640));
            int b = Math.Max(5, DpiManager.ScaleInt(6));
            Padding = new Padding(b);
            BackColor = ThemeManager.ActivePalette.BackgroundPrimary;
            ForeColor = ThemeManager.ActivePalette.TextPrimary;
            RightToLeft = Loc.RtlValue;
            DoubleBuffered = true;
        }

        public void EnsureWindowWithinWorkingArea()
        {
            if (WindowState == FormWindowState.Minimized || IsCaptureMode) return;
            try
            {
                Screen scr = Screen.FromHandle(Handle);
                if (scr == null) scr = Screen.PrimaryScreen;
                if (scr == null) return;
                Rectangle work = scr.WorkingArea;
                Rectangle cur = Bounds;

                int maxAllowW = work.Width;
                int maxAllowH = work.Height;

                int clampedMinW = Math.Min(MinimumSize.Width, maxAllowW);
                int clampedMinH = Math.Min(MinimumSize.Height, maxAllowH);
                if (MinimumSize.Width > clampedMinW || MinimumSize.Height > clampedMinH)
                {
                    MinimumSize = new Size(clampedMinW, clampedMinH);
                }

                int newW = cur.Width;
                int newH = cur.Height;

                if (newW > maxAllowW || newH > maxAllowH)
                {
                    newW = Math.Min(DpiManager.ScaleInt(1280), (int)(maxAllowW * 0.85f));
                    newH = Math.Min(DpiManager.ScaleInt(720), (int)(maxAllowH * 0.85f));
                }

                newW = Math.Max(clampedMinW, Math.Min(newW, maxAllowW));
                newH = Math.Max(clampedMinH, Math.Min(newH, maxAllowH));

                int newX = Math.Max(work.Left, Math.Min(cur.Left, work.Right - newW));
                int newY = Math.Max(work.Top, Math.Min(cur.Top, work.Bottom - newH));

                if (cur.Left < work.Left || cur.Top < work.Top || cur.Right > work.Right || cur.Bottom > work.Bottom)
                {
                    newX = work.Left + Math.Max(0, (work.Width - newW) / 2);
                    newY = work.Top + Math.Max(0, (work.Height - newH) / 2);
                }

                if (newX != cur.Left || newY != cur.Top || newW != cur.Width || newH != cur.Height)
                {
                    SetBounds(newX, newY, newW, newH);
                }
            }
            catch { }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            EnsureWindowWithinWorkingArea();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
                int DWMWCP_ROUND = 2;
                DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref DWMWCP_ROUND, sizeof(int));
                UpdateDwmImmersiveDarkMode();
            }
            catch { }

            if (!IsCaptureMode)
            {
                try
                {
                    StorageNotificationManager.Register(Handle);
                    StorageNotificationManager.TopologyWakeAction = () =>
                    {
                        OnHardwareDeviceChanged(DBT_DEVICEARRIVAL, "TopologyConfirmationWake");
                    };
                }
                catch { }
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            try
            {
                StorageNotificationManager.TopologyWakeAction = null;
                StorageNotificationManager.Unregister();
            }
            catch { }
            base.OnHandleDestroyed(e);
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                if (_clockTimer != null)
                {
                    try { _clockTimer.Stop(); _clockTimer.Dispose(); } catch { }
                    _clockTimer = null;
                }
                if (_tray != null)
                {
                    try { _tray.Dispose(); } catch { }
                    _tray = null;
                }
                try
                {
                    ActiveSentinel.DeviceIntercepted -= OnDeviceIntercepted;
                    ActiveSentinel.DeviceEnforcementPending -= OnDeviceEnforcementPending;
                }
                catch { }
                try
                {
                    StorageNotificationManager.TopologyWakeAction = null;
                    StorageNotificationManager.Unregister();
                }
                catch { }
            }
            base.Dispose(disposing);
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct NCCALCSIZE_PARAMS
        {
            public RECT rgrc0;
            public RECT rgrc1;
            public RECT rgrc2;
            public IntPtr lppos;
        }

        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WS_SYSMENU = 0x00080000;
        private const int CS_DROPSHADOW = 0x00020000;

        private const int WM_ERASEBKGND = 0x0014;
        private const int WM_GETMINMAXINFO = 0x0024;
        private const int WM_NCCALCSIZE = 0x0083;
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_NCPAINT = 0x0085;
        private const int WM_NCACTIVATE = 0x0086;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int WM_NCLBUTTONDBLCLK = 0x00A3;

        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;
        private const int HTMAXBUTTON = 9;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        public void ToggleMaximizeRestore()
        {
            if (WindowState == FormWindowState.Maximized)
            {
                WindowState = FormWindowState.Normal;
                int b = Math.Max(5, DpiManager.ScaleInt(6));
                Padding = new Padding(b);
            }
            else
            {
                WindowState = FormWindowState.Maximized;
                Padding = Padding.Empty;
            }
            if (_topBar != null)
            {
                _topBar.UpdateMaximizeState(WindowState == FormWindowState.Maximized);
            }
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!IsCaptureMode && WindowState == FormWindowState.Minimized && SettingsRepository.Load().MinimizeToTray)
            {
                Hide();
                ShowInTaskbar = false;
            }
            if (_topBar != null)
            {
                _topBar.UpdateMaximizeState(WindowState == FormWindowState.Maximized);
            }
            if (WindowState == FormWindowState.Maximized)
            {
                if (Padding != Padding.Empty)
                    Padding = Padding.Empty;
            }
            else
            {
                int b = Math.Max(5, DpiManager.ScaleInt(6));
                Padding newPad = new Padding(b);
                if (Padding != newPad)
                    Padding = newPad;
            }
            Invalidate();
            if (_sidebar != null)
            {
                bool narrow = ClientSize.Width < DpiManager.ScaleInt(1150);
                if (narrow)
                {
                    if (!_sidebar.IsCompact)
                    {
                        _isAutoCompacting = true;
                        _sidebar.IsCompact = true;
                        _isAutoCompacting = false;
                    }
                }
                else
                {
                    if (_sidebar.IsCompact != _userPreferredCompactSidebar)
                    {
                        _isAutoCompacting = true;
                        _sidebar.IsCompact = _userPreferredCompactSidebar;
                        _isAutoCompacting = false;
                    }
                }
            }
            if (_devicesDrawer != null && _devicesDrawer.Visible)
            {
                _devicesDrawer.UpdateResponsiveWidth(_devicesBodyPanel != null ? _devicesBodyPanel.ClientSize.Width : ClientSize.Width);
            }
            if (_trustedDrawer != null && _trustedDrawer.Visible)
            {
                _trustedDrawer.UpdateResponsiveWidth(_trustedBodyPanel != null ? _trustedBodyPanel.ClientSize.Width : ClientSize.Width);
            }
            if (_activityDrawer != null && _activityDrawer.Visible)
            {
                _activityDrawer.UpdateResponsiveWidth(_activityBodyPanel != null ? _activityBodyPanel.ClientSize.Width : ClientSize.Width);
            }

            // Trigger responsive re-layout for active page during resize
            if (_sidebar != null)
            {
                switch (_sidebar.SelectedIndex)
                {
                    case 0:
                        LayoutDashboard();
                        break;
                    case 1:
                        ResizeDeviceRows();
                        break;
                    case 2:
                        LayoutProtectionCards();
                        break;
                    case 3:
                        ResizeTrustedRows();
                        break;
                    case 4:
                        ResizeActivityRows();
                        break;
                    case 5:
                        LayoutRecoveryTab();
                        break;
                    case 6:
                        LayoutSettingsTab();
                        break;
                }
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Prevent default Windows erasing that paints COLOR_BTNFACE (180, 180, 180)
            IThemePalette pal = ThemeManager.ActivePalette;
            Color bg = (pal != null) ? pal.BackgroundPrimary : BackColor;
            using (SolidBrush b = new SolidBrush(bg))
            {
                e.Graphics.FillRectangle(b, e.ClipRectangle);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (WindowState != FormWindowState.Maximized)
            {
                IThemePalette pal = ThemeManager.ActivePalette;
                if (pal != null)
                {
                    int b = Math.Max(5, DpiManager.ScaleInt(6));
                    // Seamlessly blend top margin with AppTopBar (Surface), and remaining perimeter with BackgroundPrimary
                    using (SolidBrush topBrush = new SolidBrush(pal.Surface))
                    {
                        e.Graphics.FillRectangle(topBrush, 0, 0, ClientSize.Width, b);
                    }
                    using (SolidBrush bgBrush = new SolidBrush(pal.BackgroundPrimary))
                    {
                        e.Graphics.FillRectangle(bgBrush, 0, b, b, ClientSize.Height - b); // Left margin
                        e.Graphics.FillRectangle(bgBrush, ClientSize.Width - b, b, b, ClientSize.Height - b); // Right margin
                        e.Graphics.FillRectangle(bgBrush, 0, ClientSize.Height - b, ClientSize.Width, b); // Bottom margin
                    }
                    using (Pen pen = new Pen(pal.BorderSubtle, 1f))
                    {
                        e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
                    }
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            if (IsCaptureMode && IsHandleCreated && WindowState == FormWindowState.Normal)
            {
                SetWindowPos(Handle, IntPtr.Zero, x, y, width, height, 0x0014); // SWP_NOZORDER | SWP_NOACTIVATE
            }
            else
            {
                base.SetBoundsCore(x, y, width, height, specified);
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        private const int DBT_DEVNODES_CHANGED = 0x0007;
        private long _lastPnpEventTimestamp = 0;

        protected override void WndProc(ref Message m)
        {
            const int WM_SETTINGCHANGE = 0x001A;
            if (m.Msg == WM_SETTINGCHANGE)
            {
                ThemeManager.OnWindowsSettingChanged();
            }

            if (m.Msg == WM_ERASEBKGND)
            {
                // Suppress default WinForms/Windows erasing that fills with COLOR_BTNFACE (180, 180, 180)
                m.Result = (IntPtr)1;
                return;
            }

            if (m.Msg == WM_NCPAINT)
            {
                // Suppress native Windows non-client frame painting
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WM_NCACTIVATE)
            {
                // Prevent DefWindowProc from drawing native titlebar/frame on window activation
                m.Result = (IntPtr)1;
                return;
            }

            if (m.Msg == WM_GETMINMAXINFO)
            {
                base.WndProc(ref m);
                try
                {
                    MINMAXINFO mmi = (MINMAXINFO)System.Runtime.InteropServices.Marshal.PtrToStructure(m.LParam, typeof(MINMAXINFO));
                    Screen scr = Screen.FromHandle(Handle);
                    if (scr == null) scr = Screen.PrimaryScreen;
                    if (scr != null)
                    {
                        mmi.ptMinTrackSize.x = Math.Min(DpiManager.ScaleInt(1024), scr.WorkingArea.Width);
                        mmi.ptMinTrackSize.y = Math.Min(DpiManager.ScaleInt(640), scr.WorkingArea.Height);
                        mmi.ptMaxSize.x = scr.WorkingArea.Width;
                        mmi.ptMaxSize.y = scr.WorkingArea.Height;
                        mmi.ptMaxPosition.x = Math.Abs(scr.WorkingArea.Left - scr.Bounds.Left);
                        mmi.ptMaxPosition.y = Math.Abs(scr.WorkingArea.Top - scr.Bounds.Top);
                        if (IsCaptureMode)
                        {
                            mmi.ptMaxTrackSize.x = 4096;
                            mmi.ptMaxTrackSize.y = 4096;
                        }
                        else
                        {
                            mmi.ptMaxTrackSize.x = scr.WorkingArea.Width;
                            mmi.ptMaxTrackSize.y = scr.WorkingArea.Height;
                        }
                    }
                    else
                    {
                        mmi.ptMinTrackSize.x = DpiManager.ScaleInt(1024);
                        mmi.ptMinTrackSize.y = DpiManager.ScaleInt(640);
                    }
                    System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, m.LParam, true);
                    m.Result = IntPtr.Zero;
                    return;
                }
                catch { }
            }

            if (m.Msg == WM_NCCALCSIZE)
            {
                if (m.WParam != IntPtr.Zero)
                {
                    if (WindowState == FormWindowState.Maximized)
                    {
                        try
                        {
                            NCCALCSIZE_PARAMS ncsp = (NCCALCSIZE_PARAMS)System.Runtime.InteropServices.Marshal.PtrToStructure(m.LParam, typeof(NCCALCSIZE_PARAMS));
                            Screen scr = Screen.FromHandle(Handle);
                            if (scr == null) scr = Screen.PrimaryScreen;
                            if (scr != null)
                            {
                                ncsp.rgrc0.left = scr.WorkingArea.Left;
                                ncsp.rgrc0.top = scr.WorkingArea.Top;
                                ncsp.rgrc0.right = scr.WorkingArea.Right;
                                ncsp.rgrc0.bottom = scr.WorkingArea.Bottom;
                                System.Runtime.InteropServices.Marshal.StructureToPtr(ncsp, m.LParam, true);
                            }
                        }
                        catch { }
                    }
                    m.Result = IntPtr.Zero;
                    return;
                }
                else
                {
                    m.Result = IntPtr.Zero;
                    return;
                }
            }

            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                long lp = m.LParam.ToInt64();
                int screenX = unchecked((short)(lp & 0xFFFF));
                int screenY = unchecked((short)((lp >> 16) & 0xFFFF));
                Point clientPt = PointToClient(new Point(screenX, screenY));

                if (WindowState != FormWindowState.Maximized)
                {
                    int b = Math.Max(5, DpiManager.ScaleInt(6));
                    int corner = Math.Max(12, b * 2);

                    bool left = clientPt.X < b;
                    bool right = clientPt.X >= ClientSize.Width - b;
                    bool top = clientPt.Y < b;
                    bool bottom = clientPt.Y >= ClientSize.Height - b;

                    bool cornerLeft = clientPt.X < corner;
                    bool cornerRight = clientPt.X >= ClientSize.Width - corner;
                    bool cornerTop = clientPt.Y < corner;
                    bool cornerBottom = clientPt.Y >= ClientSize.Height - corner;

                    // 4 Corners:
                    if ((cornerTop && left) || (top && cornerLeft)) { m.Result = (IntPtr)HTTOPLEFT; return; }
                    if ((cornerTop && right) || (top && cornerRight)) { m.Result = (IntPtr)HTTOPRIGHT; return; }
                    if ((cornerBottom && left) || (bottom && cornerLeft)) { m.Result = (IntPtr)HTBOTTOMLEFT; return; }
                    if ((cornerBottom && right) || (bottom && cornerRight)) { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }

                    // 4 Edges:
                    if (top) { m.Result = (IntPtr)HTTOP; return; }
                    if (bottom) { m.Result = (IntPtr)HTBOTTOM; return; }
                    if (left) { m.Result = (IntPtr)HTLEFT; return; }
                    if (right) { m.Result = (IntPtr)HTRIGHT; return; }
                }

                m.Result = (IntPtr)HTCLIENT;
                return;
            }

            if (m.Msg == WM_NCLBUTTONDBLCLK && m.WParam.ToInt32() == HTCAPTION)
            {
                ToggleMaximizeRestore();
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WM_NCLBUTTONDOWN && m.WParam.ToInt32() == HTMAXBUTTON)
            {
                ToggleMaximizeRestore();
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WM_DEVICECHANGE)
            {
                int eventType = m.WParam.ToInt32();
                var parseResult = StorageNotificationManager.ParseDeviceChange(eventType, m.LParam);
                var route = StorageNotificationManager.RouteDeviceChange(eventType, m.LParam);

                if (route == StorageNotificationManager.DeviceChangeDispatchRoute.SettleDebounceWake)
                {
                    // S0 is recorded atomically on the first application-observed arrival event (unexpired S0 preserved)
                    LatencyTracker.RecordPnpArrivalNotification();

                    // Storage interface event (GUID_DEVINTERFACE_DISK or GUID_DEVINTERFACE_VOLUME)
                    // Arm or rearm the 250ms settle timer to coalesce multi-volume arrivals
                    // CRITICAL INVARIANT: Legacy immediate OnHardwareDeviceChanged is NOT called!
                    bool wasRearmed = StorageNotificationManager.ArmOrRearmSettleTimer(() =>
                    {
                        OnHardwareDeviceChanged(DBT_DEVICEARRIVAL, "StorageInterfaceSettleWake");
                    });

                    StorageNotificationManager.LogRawDeviceChange(
                        eventType,
                        parseResult.DeviceType,
                        parseResult.ClassGuid,
                        wasRearmed,
                        wasRearmed ? "Rearmed (250ms settle)" : "Armed (250ms settle)");
                }
                else if (route == StorageNotificationManager.DeviceChangeDispatchRoute.ImmediateGenericArrival)
                {
                    // Generic non-interface arrival (e.g. hub/controller plug-in)
                    LatencyTracker.RecordPnpArrivalNotification();

                    StorageNotificationManager.LogRawDeviceChange(
                        eventType,
                        parseResult.DeviceType,
                        parseResult.ClassGuid,
                        false,
                        "Immediate evaluation");

                    long now = System.Diagnostics.Stopwatch.GetTimestamp();
                    if ((now - _lastPnpEventTimestamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency > 200.0)
                    {
                        _lastPnpEventTimestamp = now;
                        OnHardwareDeviceChanged(eventType, "ImmediateGenericArrival");
                    }
                }
                else if (route == StorageNotificationManager.DeviceChangeDispatchRoute.DeviceRemoval)
                {
                    StorageNotificationManager.CancelSettleTimer();
                    ActiveSentinel.CancelArrivalReconciliation(true);
                    DeviceEnforcementLifecycleManager.ReconcileRemovedDevices(null);

                    StorageNotificationManager.LogRawDeviceChange(
                        eventType,
                        parseResult.DeviceType,
                        parseResult.ClassGuid,
                        false,
                        "Device removal");

                    long now = System.Diagnostics.Stopwatch.GetTimestamp();
                    if ((now - _lastPnpEventTimestamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency > 200.0)
                    {
                        _lastPnpEventTimestamp = now;
                        OnHardwareDeviceChanged(eventType, "DeviceRemoval");
                    }
                }
                else if (route == StorageNotificationManager.DeviceChangeDispatchRoute.VisualRefreshOnly)
                {
                    // Generic DBT_DEVNODES_CHANGED (0x0007): coalesced visual refresh ONLY; NEVER triggers enforcement!
                    long now = System.Diagnostics.Stopwatch.GetTimestamp();
                    if ((now - _lastPnpEventTimestamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency > 200.0)
                    {
                        _lastPnpEventTimestamp = now;
                        StorageNotificationManager.LogRawDeviceChange(
                            eventType,
                            parseResult.DeviceType,
                            parseResult.ClassGuid,
                            false,
                            "Visual refresh only");
                        OnHardwareDeviceChanged(eventType, "VisualRefreshOnly");
                    }
                }
            }

            base.WndProc(ref m);
        }

        private void CleanupPreviousUiControls()
        {
            if (_devicesDrawer != null) { try { _devicesDrawer.Dispose(); } catch { } _devicesDrawer = null; }
            if (_trustedDrawer != null) { try { _trustedDrawer.Dispose(); } catch { } _trustedDrawer = null; }
            if (_activityDrawer != null) { try { _activityDrawer.Dispose(); } catch { } _activityDrawer = null; }

            if (_tabDashboard != null) { try { _tabDashboard.Dispose(); } catch { } _tabDashboard = null; }
            if (_tabDevices != null) { try { _tabDevices.Dispose(); } catch { } _tabDevices = null; }
            if (_tabRules != null) { try { _tabRules.Dispose(); } catch { } _tabRules = null; }
            if (_tabWhitelist != null) { try { _tabWhitelist.Dispose(); } catch { } _tabWhitelist = null; }
            if (_tabActivity != null) { try { _tabActivity.Dispose(); } catch { } _tabActivity = null; }
            if (_tabRecovery != null) { try { _tabRecovery.Dispose(); } catch { } _tabRecovery = null; }
            if (_tabSettings != null) { try { _tabSettings.Dispose(); } catch { } _tabSettings = null; }

            if (_contentPanel != null) { try { _contentPanel.Dispose(); } catch { } _contentPanel = null; }
            if (_sidebar != null) { try { _sidebar.Dispose(); } catch { } _sidebar = null; }
            if (_topBar != null) { try { _topBar.Dispose(); } catch { } _topBar = null; }

            UiDrawingUtils.ClearAndDisposeControls(Controls);
        }

        private void BuildUI()
        {
            CleanupPreviousUiControls();
            RightToLeft = Loc.RtlValue;
            SuspendLayout();

            AppSettingsData settings = SettingsRepository.Load();
            bool isRtl = Loc.IsRtl;

            IThemePalette pal = ThemeManager.ActivePalette;
            BackColor = pal.BackgroundPrimary;
            ForeColor = pal.TextPrimary;

            // ═══════════════════════════════════════════════════════════════
            // LAYER 1: MODERN APP TOP BAR
            // ═══════════════════════════════════════════════════════════════
            _topBar = new AppTopBar();
            _topBar.BrandTitle = "USB SHIELD PRO";
            _topBar.VersionText = "v2.1";
            _topBar.LanguageButton.Text = isRtl ? "AR" : "EN";

            _topBar.CloseClicked += delegate { Close(); };
            _topBar.MinimizeClicked += delegate { WindowState = FormWindowState.Minimized; };
            _topBar.MaximizeClicked += delegate { ToggleMaximizeRestore(); };
            _topBar.LanguageToggleClicked += delegate { ToggleLanguage(); };

            Controls.Add(_topBar);

            // ═══════════════════════════════════════════════════════════════
            // LAYER 2: VERTICAL APP SIDEBAR NAVIGATION
            // ═══════════════════════════════════════════════════════════════
            _sidebar = new AppSidebar();
            _sidebar.Dock = isRtl ? DockStyle.Right : DockStyle.Left;
            _sidebar.RightToLeft = Loc.RtlValue;
            _userPreferredCompactSidebar = settings.SidebarCompact;
            _sidebar.IsCompact = settings.SidebarCompact;

            PopulateSidebarItems(isRtl);

            _sidebar.ItemSelected += delegate(object s, SidebarItemEventArgs ea)
            {
                SwitchTab(ea.Index);
            };
            _sidebar.CompactChanged += delegate
            {
                if (!_isAutoCompacting)
                {
                    _userPreferredCompactSidebar = _sidebar.IsCompact;
                    AppSettingsData st = SettingsRepository.Load();
                    st.SidebarCompact = _sidebar.IsCompact;
                    SettingsRepository.Save(st);
                }
                if (_toggleSidebar != null && _toggleSidebar.Checked != _sidebar.IsCompact)
                {
                    _toggleSidebar.Checked = _sidebar.IsCompact;
                }
                if (_contentPanel != null)
                {
                    _contentPanel.PerformLayout();
                }
                LayoutDashboard(true);
            };

            Controls.Add(_sidebar);

            // ═══════════════════════════════════════════════════════════════
            // LAYER 3: PAGE CONTENT CONTAINER
            // ═══════════════════════════════════════════════════════════════
            _contentPanel = new Panel();
            _contentPanel.Dock = DockStyle.Fill;
            _contentPanel.BackColor = pal.BackgroundPrimary;
            Controls.Add(_contentPanel);

            // Establish strict docking layout order: Edge docks at back dock first, fill at front docks last
            _sidebar.SendToBack();
            _topBar.SendToBack();
            _contentPanel.BringToFront();

            // Build page views
            BuildTabDashboard();
            BuildTabDevices();
            BuildTabRules();
            BuildTabWhitelist();
            BuildTabActivity();
            BuildTabRecovery();
            BuildTabSettings();

            SwitchTab(0);
            ResumeLayout(true);
            LayoutDashboard(true);
        }

        private void PopulateSidebarItems(bool isRtl)
        {
            if (_sidebar == null) return;
            _sidebar.Items.Clear();
            if (isRtl)
            {
                _sidebar.Items.Add(new SidebarNavigationItem(0, "لوحة التحكم", "نظرة عامة"));
                var dev = new SidebarNavigationItem(1, "الأجهزة المتصلة", "نظرة عامة");
                dev.BadgeText = "";
                _sidebar.Items.Add(dev);
                _sidebar.Items.Add(new SidebarNavigationItem(2, "الحماية", "الحماية"));
                _sidebar.Items.Add(new SidebarNavigationItem(3, "الأجهزة الموثوقة", "الحماية"));
                _sidebar.Items.Add(new SidebarNavigationItem(4, "سجل النشاط", "المراقبة"));
                _sidebar.Items.Add(new SidebarNavigationItem(5, "الاسترداد", "النظام"));
                _sidebar.Items.Add(new SidebarNavigationItem(6, "الإعدادات", "النظام"));
            }
            else
            {
                _sidebar.Items.Add(new SidebarNavigationItem(0, "Dashboard", "Overview"));
                var dev = new SidebarNavigationItem(1, "Connected Devices", "Overview");
                dev.BadgeText = "";
                _sidebar.Items.Add(dev);
                _sidebar.Items.Add(new SidebarNavigationItem(2, "Protection", "Protection"));
                _sidebar.Items.Add(new SidebarNavigationItem(3, "Trusted Devices", "Protection"));
                _sidebar.Items.Add(new SidebarNavigationItem(4, "Activity", "Monitoring"));
                _sidebar.Items.Add(new SidebarNavigationItem(5, "Recovery", "System"));
                _sidebar.Items.Add(new SidebarNavigationItem(6, "Settings", "System"));
            }
        }

        private void ToggleLanguage()
        {
            Loc.Toggle();
            AppSettingsData s = SettingsRepository.Load();
            s.LanguagePreference = Loc.IsRtl ? "ar" : "en";
            SettingsRepository.Save(s);
            RebuildUI();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (!IsDisposed)
            {
                IThemePalette pal = ThemeManager.ActivePalette;
                BackColor = pal.BackgroundPrimary;
                ForeColor = pal.TextPrimary;
                if (_contentPanel != null) _contentPanel.BackColor = pal.BackgroundPrimary;
                if (_tabDashboard != null) _tabDashboard.BackColor = pal.BackgroundPrimary;
                if (_tabDashboardScroll != null)
                {
                    _tabDashboardScroll.BackColor = pal.BackgroundPrimary;
                    if (_tabDashboardScroll.ContentPanel != null)
                        _tabDashboardScroll.ContentPanel.BackColor = pal.BackgroundPrimary;
                }
                if (_tabDevices != null) _tabDevices.BackColor = pal.BackgroundPrimary;
                if (_devicesScroll != null)
                {
                    _devicesScroll.BackColor = pal.BackgroundPrimary;
                    if (_devicesScroll.ContentPanel != null)
                        _devicesScroll.ContentPanel.BackColor = pal.BackgroundPrimary;
                }
                if (_devicesHeaderPanel != null) _devicesHeaderPanel.BackColor = pal.BackgroundPrimary;
                if (_devicesBodyPanel != null) _devicesBodyPanel.BackColor = pal.BackgroundPrimary;
                if (_devicesPageTitle != null) _devicesPageTitle.ForeColor = pal.TextPrimary;
                if (_devicesPageSub != null) _devicesPageSub.ForeColor = pal.TextSecondary;
                if (_protectionPageTitle != null) _protectionPageTitle.ForeColor = pal.TextPrimary;
                if (_protectionPageSub != null) _protectionPageSub.ForeColor = pal.TextSecondary;
                if (_tabRules != null) _tabRules.BackColor = pal.BackgroundPrimary;
                if (_tabRulesScroll != null)
                {
                    _tabRulesScroll.BackColor = pal.BackgroundPrimary;
                    if (_tabRulesScroll.ContentPanel != null)
                        _tabRulesScroll.ContentPanel.BackColor = pal.BackgroundPrimary;
                }
                if (_tabWhitelist != null) _tabWhitelist.BackColor = pal.BackgroundPrimary;
                if (_trustedScroll != null)
                {
                    _trustedScroll.BackColor = pal.BackgroundPrimary;
                    if (_trustedScroll.ContentPanel != null)
                        _trustedScroll.ContentPanel.BackColor = pal.BackgroundPrimary;
                }
                if (_trustedHeaderPanel != null) _trustedHeaderPanel.BackColor = pal.BackgroundPrimary;
                if (_trustedSummaryStrip != null) _trustedSummaryStrip.BackColor = pal.BackgroundPrimary;
                if (_trustedBodyPanel != null) _trustedBodyPanel.BackColor = pal.BackgroundPrimary;
                if (_trustedPageTitle != null) _trustedPageTitle.ForeColor = pal.TextPrimary;
                if (_tabActivity != null) _tabActivity.BackColor = pal.BackgroundPrimary;
                if (_activityScroll != null)
                {
                    _activityScroll.BackColor = pal.BackgroundPrimary;
                    if (_activityScroll.ContentPanel != null)
                        _activityScroll.ContentPanel.BackColor = pal.BackgroundPrimary;
                }
                if (_activityHeaderPanel != null) _activityHeaderPanel.BackColor = pal.BackgroundPrimary;
                if (_activityToolbarPanel != null) _activityToolbarPanel.BackColor = pal.BackgroundPrimary;
                if (_activityBodyPanel != null) _activityBodyPanel.BackColor = pal.BackgroundPrimary;
                if (_activityPageTitle != null) _activityPageTitle.ForeColor = pal.TextPrimary;
                if (_activityPageSub != null) _activityPageSub.ForeColor = pal.TextSecondary;
                if (_tabRecovery != null) _tabRecovery.BackColor = pal.BackgroundPrimary;
                if (_tabRecoveryScroll != null)
                {
                    _tabRecoveryScroll.BackColor = pal.BackgroundPrimary;
                    if (_tabRecoveryScroll.ContentPanel != null)
                        _tabRecoveryScroll.ContentPanel.BackColor = pal.BackgroundPrimary;
                }
                if (_recoveryPageTitle != null)
                {
                    _recoveryPageTitle.ForeColor = pal.TextPrimary;
                    _recoveryPageTitle.BackColor = pal.BackgroundPrimary;
                }
                if (_recoveryPageSub != null)
                {
                    _recoveryPageSub.ForeColor = pal.TextSecondary;
                    _recoveryPageSub.BackColor = pal.BackgroundPrimary;
                }
                if (_tabSettings != null) _tabSettings.BackColor = pal.BackgroundPrimary;
                if (_tabSettingsScroll != null)
                {
                    _tabSettingsScroll.BackColor = pal.BackgroundPrimary;
                    if (_tabSettingsScroll.ContentPanel != null)
                        _tabSettingsScroll.ContentPanel.BackColor = pal.BackgroundPrimary;
                }
                if (_settingsPageTitle != null)
                {
                    _settingsPageTitle.ForeColor = pal.TextPrimary;
                    _settingsPageTitle.BackColor = pal.BackgroundPrimary;
                }
                if (_settingsPageSub != null)
                {
                    _settingsPageSub.ForeColor = pal.TextSecondary;
                    _settingsPageSub.BackColor = pal.BackgroundPrimary;
                }
                SyncSettingsThemeDropdown();
                RefreshLiveStatus();
                UpdateDwmImmersiveDarkMode();
                if (_sidebar != null && _sidebar.SelectedIndex == 1) ApplyDeviceFilters();
                if (_sidebar != null && _sidebar.SelectedIndex == 3) ApplyTrustedFilters();
                if (_sidebar != null && _sidebar.SelectedIndex == 4) RefreshActivityView();
                if (_sidebar != null && _sidebar.SelectedIndex == 5) RefreshRecoveryStats();
                if (_sidebar != null && _sidebar.SelectedIndex == 6) RefreshSettingsTab();
                Invalidate(true);
            }
        }

        private void UpdateDwmImmersiveDarkMode()
        {
            if (!IsHandleCreated || IsDisposed) return;
            try
            {
                const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
                int isDark = ThemeManager.ActivePalette.IsDark ? 1 : 0;
                DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref isDark, sizeof(int));
            }
            catch { }
        }

        private bool _switchingTab = false;
        private void SwitchTab(int index)
        {
            if (_switchingTab) return;
            _switchingTab = true;
            try
            {
                if (_sidebar != null && _sidebar.SelectedIndex != index)
                {
                    _sidebar.SelectedIndex = index;
                }

                _contentPanel.SuspendLayout();
                _contentPanel.Controls.Clear();
                switch (index)
                {
                    case 0:
                        _contentPanel.Controls.Add(_tabDashboard);
                        break;
                    case 1:
                        _contentPanel.Controls.Add(_tabDevices);
                        break;
                    case 2:
                        _contentPanel.Controls.Add(_tabRules);
                        break;
                    case 3:
                        _contentPanel.Controls.Add(_tabWhitelist);
                        break;
                    case 4:
                        _contentPanel.Controls.Add(_tabActivity);
                        break;
                    case 5:
                        _contentPanel.Controls.Add(_tabRecovery);
                        break;
                    case 6:
                        _contentPanel.Controls.Add(_tabSettings);
                        break;
                }
                _contentPanel.ResumeLayout(true);
                _contentPanel.PerformLayout();

                // Execute refresh logic after layout computation so container bounds are 100% stable
                switch (index)
                {
                    case 0:
                        LayoutDashboard(true);
                        RefreshLiveStatus();
                        break;
                    case 1:
                        RefreshDevicesList();
                        break;
                    case 2:
                        SyncRuleTogglesWithSettings();
                        break;
                    case 3:
                        RefreshWhitelistView();
                        break;
                    case 4:
                        RefreshActivityView();
                        break;
                    case 5:
                        if (_tabRecoveryScroll != null) _tabRecoveryScroll.ScrollToTop();
                        RefreshRecoveryStats();
                        break;
                    case 6:
                        if (_tabSettingsScroll != null) _tabSettingsScroll.ScrollToTop();
                        RefreshSettingsTab();
                        break;
                }
            }
            finally
            {
                _switchingTab = false;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // PAGE 0: DASHBOARD
        // ═══════════════════════════════════════════════════════════════════
        private void BuildTabDashboard()
        {
            _tabDashboard = new Panel();
            _tabDashboard.Dock = DockStyle.Fill;
            _tabDashboard.BackColor = ThemeManager.ActivePalette.BackgroundPrimary;

            _tabDashboardScroll = new DarkScrollPanel();
            _tabDashboardScroll.Dock = DockStyle.Fill;
            _tabDashboardScroll.RightToLeft = Loc.RtlValue;
            _tabDashboard.Controls.Add(_tabDashboardScroll);

            // Page Header
            _dashboardPageTitle = new Label();
            _dashboardPageTitle.Text = Loc.Get("TabDashboard");
            _dashboardPageTitle.Font = Theme.FontTitle;
            _dashboardPageTitle.ForeColor = ThemeManager.ActivePalette.TextPrimary;
            _dashboardPageTitle.AutoSize = true;
            _tabDashboardScroll.ContentPanel.Controls.Add(_dashboardPageTitle);

            _dashboardPageSub = new Label();
            _dashboardPageSub.Text = Loc.Get("DashboardSubtitle");
            _dashboardPageSub.Font = Theme.FontNormal;
            _dashboardPageSub.ForeColor = ThemeManager.ActivePalette.TextSecondary;
            _dashboardPageSub.AutoSize = true;
            _tabDashboardScroll.ContentPanel.Controls.Add(_dashboardPageSub);

            // 1. Compact Protection Posture Component
            _protectionStatusCard = new ProtectionStatusCard();
            _protectionStatusCard.RightToLeft = Loc.RtlValue;
            _protectionStatusCard.LockClicked += delegate { ExecuteQuickLock(); };
            _protectionStatusCard.UnlockClicked += delegate { ExecuteQuickUnlock(); };
            _tabDashboardScroll.ContentPanel.Controls.Add(_protectionStatusCard);

            // 2. Compact Temporary Access Module
            _tempAccessModule = new CompactTemporaryAccessModule();
            _tempAccessModule.RightToLeft = Loc.RtlValue;
            _tempAccessModule.DurationSelected += delegate(int mins) { StartTemporaryAccess(mins); };
            _tempAccessModule.CancelClicked += delegate { CancelTemporaryAccess(); };
            _tabDashboardScroll.ContentPanel.Controls.Add(_tempAccessModule);

            // 3. Exactly Three Summary Metrics Cards
            _metricConnectedDevices = new SummaryMetricCard();
            _metricConnectedDevices.RightToLeft = Loc.RtlValue;
            _metricConnectedDevices.TitleText = Loc.Get("CardConnectedDevices");
            _metricConnectedDevices.SubtitleText = Loc.IsRtl ? "حالة الأجهزة الحية" : "Real-time PnP status";
            _metricConnectedDevices.Click += delegate { _sidebar.SelectedIndex = 1; };
            _tabDashboardScroll.ContentPanel.Controls.Add(_metricConnectedDevices);

            _metricActiveRules = new SummaryMetricCard();
            _metricActiveRules.RightToLeft = Loc.RtlValue;
            _metricActiveRules.TitleText = Loc.Get("CardActiveRules");
            _metricActiveRules.SubtitleText = Loc.IsRtl ? "السياسات المطبقة" : "Active policy rules";
            _metricActiveRules.Click += delegate { _sidebar.SelectedIndex = 2; };
            _tabDashboardScroll.ContentPanel.Controls.Add(_metricActiveRules);

            _metricTrustedDevices = new SummaryMetricCard();
            _metricTrustedDevices.RightToLeft = Loc.RtlValue;
            _metricTrustedDevices.TitleText = Loc.Get("CardTrustedDevices");
            _metricTrustedDevices.SubtitleText = Loc.IsRtl ? "قائمة الأجهزة المعتمدة" : "Known safe hardware";
            _metricTrustedDevices.Click += delegate { _sidebar.SelectedIndex = 3; };
            _tabDashboardScroll.ContentPanel.Controls.Add(_metricTrustedDevices);

            // 4. Split Area: Recent Activity (Logical Primary) & Protection Overview (Logical Secondary)
            _recentActivityPanel = new RecentActivityPanel();
            _recentActivityPanel.RightToLeft = Loc.RtlValue;
            _recentActivityPanel.ViewAllClicked += delegate { _sidebar.SelectedIndex = 4; };
            _tabDashboardScroll.ContentPanel.Controls.Add(_recentActivityPanel);

            _protectionOverviewPanel = new ProtectionOverviewPanel();
            _protectionOverviewPanel.RightToLeft = Loc.RtlValue;
            _protectionOverviewPanel.ConfigureClicked += delegate { _sidebar.SelectedIndex = 2; };
            _tabDashboardScroll.ContentPanel.Controls.Add(_protectionOverviewPanel);

            // 5. Safety Information Message Bar
            _safetyMessageBar = new SafetyMessageBar();
            _safetyMessageBar.RightToLeft = Loc.RtlValue;
            _tabDashboardScroll.ContentPanel.Controls.Add(_safetyMessageBar);

            _tabDashboardScroll.Resize += delegate { LayoutDashboard(); };
            _tabDashboardScroll.ContentPanel.Resize += delegate { LayoutDashboard(); };
            LayoutDashboard(true);
        }

        private bool _isLayingOutDashboard = false;
        private int _lastDashboardLayoutWidth = -1;
        private bool _lastDashboardLayoutRtl = false;

        private void LayoutDashboard(bool force = false)
        {
            if (_isLayingOutDashboard) return;
            if (_tabDashboardScroll == null || _protectionStatusCard == null) return;

            bool isRtl = Loc.IsRtl;
            int availW = 0;
            if (_tabDashboardScroll.ContentPanel != null && _tabDashboardScroll.ContentPanel.Width > 200)
            {
                availW = _tabDashboardScroll.ContentPanel.Width;
            }
            else if (_tabDashboardScroll.ClientSize.Width > 200)
            {
                availW = _tabDashboardScroll.ClientSize.Width - DpiManager.ScaleInt(18);
            }
            else if (_contentPanel != null && _contentPanel.ClientSize.Width > 200)
            {
                availW = _contentPanel.ClientSize.Width - DpiManager.ScaleInt(18);
            }
            else
            {
                int sideW = (_sidebar != null && _sidebar.Visible) ? _sidebar.Width : 0;
                int formClientW = ClientSize.Width - Padding.Horizontal;
                availW = Math.Max(300, formClientW - sideW - DpiManager.ScaleInt(18));
            }

            if (!force && availW == _lastDashboardLayoutWidth && isRtl == _lastDashboardLayoutRtl)
            {
                return;
            }

            _isLayingOutDashboard = true;
            try
            {
                _lastDashboardLayoutWidth = availW;
                _lastDashboardLayoutRtl = isRtl;

                int clientW = Math.Max(300, availW);
                int pad = DpiManager.ScaleInt(SpacingTokens.Space20);
                int fullW = Math.Max(280, clientW - pad * 2);
                int curY = pad;

                int cardX = isRtl ? (clientW - pad - fullW) : pad;

                // Page Header
                if (isRtl)
                {
                    _dashboardPageTitle.Location = new Point(clientW - pad - _dashboardPageTitle.Width, curY);
                    _dashboardPageSub.Location = new Point(clientW - pad - _dashboardPageSub.Width, curY + DpiManager.ScaleInt(28));
                }
                else
                {
                    _dashboardPageTitle.Location = new Point(pad, curY);
                    _dashboardPageSub.Location = new Point(pad, curY + DpiManager.ScaleInt(28));
                }
                curY += DpiManager.ScaleInt(58);

                // 1. Protection Posture Card
                int cardH = _protectionStatusCard.GetPreferredHeight(fullW);
                _protectionStatusCard.Location = new Point(cardX, curY);
                _protectionStatusCard.Size = new Size(fullW, cardH);
                curY += cardH + DpiManager.ScaleInt(10);

                // 2. Compact Temporary Access Module
                _tempAccessModule.Location = new Point(cardX, curY);
                _tempAccessModule.Size = new Size(fullW, DpiManager.ScaleInt(38));
                curY += _tempAccessModule.Height + DpiManager.ScaleInt(12);

                // 3. Exactly Three Summary Metrics Cards
                int metricGap = DpiManager.ScaleInt(SpacingTokens.Space12);
                int metricW = (fullW - metricGap * 2) / 3;
                int metricH = DpiManager.ScaleInt(70);

                if (isRtl)
                {
                    _metricConnectedDevices.Location = new Point(cardX + (metricW + metricGap) * 2, curY);
                    _metricActiveRules.Location = new Point(cardX + metricW + metricGap, curY);
                    _metricTrustedDevices.Location = new Point(cardX, curY);
                }
                else
                {
                    _metricConnectedDevices.Location = new Point(cardX, curY);
                    _metricActiveRules.Location = new Point(cardX + metricW + metricGap, curY);
                    _metricTrustedDevices.Location = new Point(cardX + (metricW + metricGap) * 2, curY);
                }
                _metricConnectedDevices.Size = new Size(metricW, metricH);
                _metricActiveRules.Size = new Size(metricW, metricH);
                _metricTrustedDevices.Size = new Size(metricW, metricH);
                curY += metricH + DpiManager.ScaleInt(14);

                // 4. Split Area: Recent Activity (Logical Primary) & Protection Overview (Logical Secondary)
                int splitGap = DpiManager.ScaleInt(SpacingTokens.Space12);
                int splitH = DpiManager.ScaleInt(isRtl ? 265 : 240);

                if (fullW < DpiManager.ScaleInt(780))
                {
                    // Adaptive vertical stacking on narrow viewports (e.g. 1024x640)
                    _recentActivityPanel.Location = new Point(cardX, curY);
                    _recentActivityPanel.Size = new Size(fullW, splitH);
                    curY += splitH + DpiManager.ScaleInt(12);

                    _protectionOverviewPanel.Location = new Point(cardX, curY);
                    _protectionOverviewPanel.Size = new Size(fullW, splitH);
                    curY += splitH + DpiManager.ScaleInt(12);
                }
                else
                {
                    int actW = (int)((fullW - splitGap) * 0.58f);
                    int protW = fullW - splitGap - actW;

                    if (isRtl)
                    {
                        // In RTL: Recent Activity on logical primary (Right), Protection Overview on logical secondary (Left)
                        _recentActivityPanel.Location = new Point(cardX + protW + splitGap, curY);
                        _protectionOverviewPanel.Location = new Point(cardX, curY);
                    }
                    else
                    {
                        // In LTR: Recent Activity on Left, Protection Overview on Right
                        _recentActivityPanel.Location = new Point(cardX, curY);
                        _protectionOverviewPanel.Location = new Point(cardX + actW + splitGap, curY);
                    }
                    _recentActivityPanel.Size = new Size(actW, splitH);
                    _protectionOverviewPanel.Size = new Size(protW, splitH);
                    curY += splitH + DpiManager.ScaleInt(12);
                }

                // 5. Safety Message Bar
                _safetyMessageBar.Location = new Point(cardX, curY);
                int bannerH = _safetyMessageBar.GetPreferredHeight(fullW);
                _safetyMessageBar.Size = new Size(fullW, bannerH);
                curY += bannerH + pad;

                _tabDashboardScroll.ContentPanel.Height = curY;
                _tabDashboardScroll.UpdateScroll();
            }
            finally
            {
                _isLayingOutDashboard = false;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // ═══════════════════════════════════════════════════════════════════
        // PAGE 1: CONNECTED DEVICES
        // ═══════════════════════════════════════════════════════════════════
        private void BuildTabDevices()
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = Loc.IsRtl;

            _tabDevices = new Panel();
            _tabDevices.Dock = DockStyle.Fill;
            _tabDevices.BackColor = pal.BackgroundPrimary;
            _tabDevices.RightToLeft = Loc.RtlValue;

            // ── TOP HEADER & TOOLBAR ─────────────────────────────────────────
            _devicesHeaderPanel = new Panel();
            _devicesHeaderPanel.Dock = DockStyle.Top;
            _devicesHeaderPanel.Height = DpiManager.ScaleInt(74);
            _devicesHeaderPanel.BackColor = pal.BackgroundPrimary;

            _devicesPageTitle = new Label();
            _devicesPageTitle.Text = Loc.Get("DevicesTitle");
            _devicesPageTitle.Font = Theme.FontTitle;
            _devicesPageTitle.ForeColor = pal.TextPrimary;
            _devicesPageTitle.AutoSize = true;

            _devicesPageSub = new Label();
            _devicesPageSub.Text = Loc.Get("DevicesSecondary");
            _devicesPageSub.Font = Theme.FontNormal;
            _devicesPageSub.ForeColor = pal.TextSecondary;
            _devicesPageSub.AutoSize = true;

            // Action: Refresh Button
            _btnRefreshDevices = new IconButton();
            _btnRefreshDevices.ButtonType = IconButtonType.Refresh;
            _btnRefreshDevices.SizeVariant = IconButtonSize.Standard;
            _btnRefreshDevices.ToolTipText = Loc.Get("RefreshList");
            _btnRefreshDevices.Click += delegate { RefreshDevicesList(); };

            // Action: Safely Eject All Storage
            _btnEjectAllDevices = new AppButton();
            _btnEjectAllDevices.Text = Loc.Get("EjectAllRemovable");
            _btnEjectAllDevices.Variant = ButtonVariant.Secondary;
            _btnEjectAllDevices.SizeVariant = ButtonSize.Medium;
            _btnEjectAllDevices.Width = AppButton.CalculateWidth(_btnEjectAllDevices.Text, ButtonSize.Medium, isRtl);
            _btnEjectAllDevices.Click += delegate { OnEjectAllStorage(); };

            // Filter: Category & Trust Dropdown
            _devicesFilterDropdown = new AppDropdown();
            _devicesFilterDropdown.Width = DpiManager.ScaleInt(160);
            _devicesFilterDropdown.Height = DpiManager.ScaleInt(34);
            _devicesFilterDropdown.Items.Add(Loc.Get("FilterAll"));
            _devicesFilterDropdown.Items.Add(Loc.Get("FilterStorage"));
            _devicesFilterDropdown.Items.Add(Loc.Get("FilterPhones"));
            _devicesFilterDropdown.Items.Add(Loc.Get("FilterTrusted"));
            _devicesFilterDropdown.Items.Add(Loc.Get("FilterUntrusted"));
            _devicesFilterDropdown.SelectedIndex = 0;
            _devicesFilterDropdown.SelectedIndexChanged += delegate { ApplyDeviceFilters(); };

            // Search Box Component
            _devicesSearchBox = new AppSearchBox();
            _devicesSearchBox.Size = new Size(DpiManager.ScaleInt(180), DpiManager.ScaleInt(34));
            _devicesSearchBox.Placeholder = Loc.Get("SearchDevicesPlaceholder");
            _devicesSearchBox.QueryChanged += delegate { ApplyDeviceFilters(); };

            MethodInvoker layoutDevicesHeader = delegate
            {
                bool rtl = Loc.IsRtl;
                int p = DpiManager.ScaleInt(20);
                int gap = DpiManager.ScaleInt(8);
                int hw = _devicesHeaderPanel.ClientSize.Width;

                // Ensure button is sized for current text
                _btnEjectAllDevices.Width = AppButton.CalculateWidth(_btnEjectAllDevices.Text, ButtonSize.Medium, rtl);

                int titleW = Math.Max(_devicesPageTitle.Width, _devicesPageSub.Width);
                int fixedControlsW = _btnRefreshDevices.Width + gap + _btnEjectAllDevices.Width + gap + _devicesFilterDropdown.Width + gap;
                int availSearchW = hw - (p * 2) - titleW - fixedControlsW - gap;

                // Responsive wrapping: 2-row when header width is under 780px or search cannot comfortably fit
                bool isNarrow = hw < DpiManager.ScaleInt(780) || availSearchW < DpiManager.ScaleInt(140);
                int desiredH = isNarrow ? DpiManager.ScaleInt(108) : DpiManager.ScaleInt(74);
                if (_devicesHeaderPanel.Height != desiredH)
                {
                    _devicesHeaderPanel.Height = desiredH;
                }

                if (isNarrow)
                {
                    // 2-row layout: Row 1 has Title & Sub, Row 2 has Actions & Search
                    if (rtl)
                    {
                        _devicesPageTitle.Location = new Point(hw - p - _devicesPageTitle.Width, DpiManager.ScaleInt(8));
                        _devicesPageSub.Location = new Point(hw - p - _devicesPageSub.Width, DpiManager.ScaleInt(34));

                        int leftX = p;
                        _btnRefreshDevices.Location = new Point(leftX, DpiManager.ScaleInt(66));
                        leftX += _btnRefreshDevices.Width + gap;

                        _btnEjectAllDevices.Location = new Point(leftX, DpiManager.ScaleInt(66));
                        leftX += _btnEjectAllDevices.Width + gap;

                        int rightX = hw - p;
                        rightX -= _devicesFilterDropdown.Width;
                        _devicesFilterDropdown.Location = new Point(rightX, DpiManager.ScaleInt(66));
                        rightX -= gap;

                        int row2AvailW = rightX - leftX - gap;
                        int searchW = Math.Max(DpiManager.ScaleInt(110), Math.Min(DpiManager.ScaleInt(200), row2AvailW));
                        if (searchW > row2AvailW && row2AvailW > DpiManager.ScaleInt(60)) searchW = row2AvailW;
                        _devicesSearchBox.Width = searchW;
                        _devicesSearchBox.Location = new Point(rightX - searchW, DpiManager.ScaleInt(66));
                    }
                    else
                    {
                        _devicesPageTitle.Location = new Point(p, DpiManager.ScaleInt(8));
                        _devicesPageSub.Location = new Point(p, DpiManager.ScaleInt(34));

                        int curX = hw - p;
                        curX -= _btnRefreshDevices.Width;
                        _btnRefreshDevices.Location = new Point(curX, DpiManager.ScaleInt(66));
                        curX -= gap;

                        curX -= _btnEjectAllDevices.Width;
                        _btnEjectAllDevices.Location = new Point(curX, DpiManager.ScaleInt(66));
                        curX -= gap;

                        curX -= _devicesFilterDropdown.Width;
                        _devicesFilterDropdown.Location = new Point(curX, DpiManager.ScaleInt(66));
                        curX -= gap;

                        int row2AvailW = curX - p - gap;
                        int searchW = Math.Max(DpiManager.ScaleInt(110), Math.Min(DpiManager.ScaleInt(200), row2AvailW));
                        if (searchW > row2AvailW && row2AvailW > DpiManager.ScaleInt(60)) searchW = row2AvailW;
                        _devicesSearchBox.Width = searchW;
                        _devicesSearchBox.Location = new Point(curX - searchW, DpiManager.ScaleInt(66));
                    }
                }
                else
                {
                    // Single row layout
                    if (rtl)
                    {
                        _devicesPageTitle.Location = new Point(hw - p - _devicesPageTitle.Width, DpiManager.ScaleInt(12));
                        _devicesPageSub.Location = new Point(hw - p - _devicesPageSub.Width, DpiManager.ScaleInt(40));

                        int leftX = p;
                        _btnRefreshDevices.Location = new Point(leftX, DpiManager.ScaleInt(20));
                        leftX += _btnRefreshDevices.Width + gap;

                        _btnEjectAllDevices.Location = new Point(leftX, DpiManager.ScaleInt(20));
                        leftX += _btnEjectAllDevices.Width + gap;

                        int rightX = hw - p - titleW - gap;
                        rightX -= _devicesFilterDropdown.Width;
                        _devicesFilterDropdown.Location = new Point(rightX, DpiManager.ScaleInt(20));
                        rightX -= gap;

                        int availW = rightX - leftX - gap;
                        int searchW = Math.Max(DpiManager.ScaleInt(110), Math.Min(DpiManager.ScaleInt(240), availW));
                        if (searchW > availW && availW > DpiManager.ScaleInt(60)) searchW = availW;
                        _devicesSearchBox.Width = searchW;
                        _devicesSearchBox.Location = new Point(rightX - searchW, DpiManager.ScaleInt(20));
                    }
                    else
                    {
                        _devicesPageTitle.Location = new Point(p, DpiManager.ScaleInt(12));
                        _devicesPageSub.Location = new Point(p, DpiManager.ScaleInt(40));

                        int curX = hw - p;
                        curX -= _btnRefreshDevices.Width;
                        _btnRefreshDevices.Location = new Point(curX, DpiManager.ScaleInt(20));
                        curX -= gap;

                        curX -= _btnEjectAllDevices.Width;
                        _btnEjectAllDevices.Location = new Point(curX, DpiManager.ScaleInt(20));
                        curX -= gap;

                        curX -= _devicesFilterDropdown.Width;
                        _devicesFilterDropdown.Location = new Point(curX, DpiManager.ScaleInt(20));
                        curX -= gap;

                        int availW = curX - (p + titleW + gap);
                        int searchW = Math.Max(DpiManager.ScaleInt(110), Math.Min(DpiManager.ScaleInt(240), availW));
                        if (searchW > availW && availW > DpiManager.ScaleInt(60)) searchW = availW;
                        _devicesSearchBox.Width = searchW;
                        _devicesSearchBox.Location = new Point(curX - searchW, DpiManager.ScaleInt(20));
                    }
                }
            };

            _devicesHeaderPanel.Resize += delegate { layoutDevicesHeader(); };
            _devicesHeaderPanel.Controls.Add(_devicesPageTitle);
            _devicesHeaderPanel.Controls.Add(_devicesPageSub);
            _devicesHeaderPanel.Controls.Add(_btnRefreshDevices);
            _devicesHeaderPanel.Controls.Add(_btnEjectAllDevices);
            _devicesHeaderPanel.Controls.Add(_devicesFilterDropdown);
            _devicesHeaderPanel.Controls.Add(_devicesSearchBox);
            layoutDevicesHeader();

            _tabDevices.Controls.Add(_devicesHeaderPanel);

            // ── BODY: CONTAINER FOR SCROLL LIST & SLIDE-IN DRAWER ─────────────
            _devicesBodyPanel = new Panel();
            _devicesBodyPanel.Dock = DockStyle.Fill;
            _devicesBodyPanel.BackColor = pal.BackgroundPrimary;
            _devicesBodyPanel.RightToLeft = Loc.RtlValue;
            _devicesBodyPanel.Resize += delegate
            {
                if (_devicesDrawer != null)
                {
                    _devicesDrawer.UpdateResponsiveWidth(_devicesBodyPanel.ClientSize.Width);
                }
            };

            // Non-modal Slide-In Details Drawer (Docks Right in LTR, Left in RTL)
            _devicesDrawer = new DeviceDetailsDrawer();
            _devicesDrawer.Dock = isRtl ? DockStyle.Left : DockStyle.Right;
            _devicesDrawer.Width = DpiManager.ScaleInt(367);
            _devicesDrawer.Visible = false;
            _devicesDrawer.RightToLeft = Loc.RtlValue;
            _devicesDrawer.DrawerClosed += delegate
            {
                _selectedDevice = null;
                foreach (var r in _activeDeviceRows) r.IsSelected = false;
            };
            _devicesDrawer.EjectRequested += delegate
            {
                if (_devicesDrawer.CurrentDevice != null) OnRowEjectRequested(_devicesDrawer.CurrentDevice);
            };
            _devicesDrawer.TrustRequested += delegate
            {
                if (_devicesDrawer.CurrentDevice != null) OnRowTrustRequested(_devicesDrawer.CurrentDevice, _devicesDrawer.IsTrusted);
            };

            // Custom Scroll Panel for Device Rows
            _devicesScroll = new DarkScrollPanel();
            _devicesScroll.Dock = DockStyle.Fill;
            _devicesScroll.RightToLeft = Loc.RtlValue;
            _devicesScroll.ContentPanel.Resize += delegate
            {
                ResizeDeviceRows();
            };

            // Empty State Control
            _devicesEmptyState = new DeviceEmptyState();
            _devicesEmptyState.Visible = false;
            _devicesEmptyState.RefreshClicked += delegate { RefreshDevicesList(); };
            _devicesEmptyState.ClearFilterClicked += delegate
            {
                if (_devicesSearchBox != null) _devicesSearchBox.Text = "";
                if (_devicesFilterDropdown != null) _devicesFilterDropdown.SelectedIndex = 0;
            };

            // Edge dock (_devicesDrawer) added and sent to back first so Fill dock (_devicesScroll) takes remainder
            _devicesBodyPanel.Controls.Add(_devicesScroll);
            _devicesBodyPanel.Controls.Add(_devicesDrawer);
            _devicesDrawer.SendToBack();
            _devicesScroll.BringToFront();

            _tabDevices.Controls.Add(_devicesBodyPanel);
            _devicesHeaderPanel.SendToBack();
            _devicesBodyPanel.BringToFront();
        }

        private void ResizeDeviceRows()
        {
            if (_devicesScroll == null || _devicesScroll.ContentPanel == null) return;
            int pad = DpiManager.ScaleInt(20);
            int newW = Math.Max(DpiManager.ScaleInt(300), _devicesScroll.ContentPanel.ClientSize.Width - (pad * 2));
            foreach (Control c in _devicesScroll.ContentPanel.Controls)
            {
                if (c is DeviceRow || c is DeviceEmptyState)
                {
                    if (c.Width != newW) c.Width = newW;
                }
                else if (c is Panel && c.Tag as string == "sysHeader")
                {
                    if (c.Width != newW)
                    {
                        c.Width = newW;
                        foreach (Control child in c.Controls)
                        {
                            child.Width = newW;
                        }
                    }
                }
            }
        }

        private bool _isRefreshingDevices = false;
        private void RefreshDevicesList()
        {
            if (_devicesScroll == null) return;

            if (TestIsolatedDevices != null || IsCaptureMode)
            {
                try
                {
                    RenderDiscoveredDevices(TestIsolatedDevices ?? new List<DeviceIdentity>());
                }
                catch { }
                return;
            }

            if (_isRefreshingDevices) return;

            _isRefreshingDevices = true;

            System.Threading.Tasks.Task.Run(() =>
            {
                List<DeviceIdentity> allDevices = null;
                try
                {
                    allDevices = DeviceDiscovery.DiscoverAllDevices();
                }
                catch { }

                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            RenderDiscoveredDevices(allDevices);
                        }
                        finally
                        {
                            _isRefreshingDevices = false;
                        }
                    }));
                }
                else
                {
                    _isRefreshingDevices = false;
                }
            });
        }

        private void RenderDiscoveredDevices(List<DeviceIdentity> allDevices)
        {
            _cachedDiscoveredDevices = allDevices ?? new List<DeviceIdentity>();
            ApplyDeviceFilters();
        }

        private void ApplyDeviceFilters()
        {
            if (_devicesScroll == null || _devicesScroll.ContentPanel == null) return;
            _devicesScroll.ContentPanel.SuspendLayout();
            UiDrawingUtils.ClearAndDisposeControls(_devicesScroll.ContentPanel.Controls, _devicesEmptyState);
            _activeDeviceRows.Clear();

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = Loc.IsRtl;
            AppSettingsData currentSettings = SettingsRepository.Load();
            ProtectionPolicy currentPolicy = currentSettings.ActivePolicy ?? new ProtectionPolicy();
            bool isReadOnlyMode = currentPolicy.ReadOnlyRemovable;
            bool isTempAccessActive = TemporaryAccessManager.PeekRemainingSeconds() > 0;

            List<DeviceIdentity> allDevices = _cachedDiscoveredDevices ?? new List<DeviceIdentity>();
            List<DeviceIdentity> externalDevices = new List<DeviceIdentity>();
            List<DeviceIdentity> systemDevices = new List<DeviceIdentity>();

            foreach (var dev in allDevices)
            {
                if (dev.IsSystemDisk || dev.SafetyResult == SafetyValidationResult.RejectedSystemOrBootDisk ||
                    dev.Category == DeviceCategory.InternalFixedDisk || dev.Category == DeviceCategory.CriticalSystemHardware)
                {
                    systemDevices.Add(dev);
                }
                else
                {
                    externalDevices.Add(dev);
                }
            }

            // In-memory filter on external devices (zero WMI calls)
            string query = (_devicesSearchBox != null) ? _devicesSearchBox.Text.Trim().ToLowerInvariant() : "";
            int filterIdx = (_devicesFilterDropdown != null) ? _devicesFilterDropdown.SelectedIndex : 0;

            List<DeviceIdentity> filteredExternal = new List<DeviceIdentity>();
            foreach (var dev in externalDevices)
            {
                bool catMatch = true;
                bool isTrusted = TrustedDeviceRepository.IsDeviceTrusted(dev);
                switch (filterIdx)
                {
                    case 1: // Storage
                        catMatch = (dev.Category == DeviceCategory.UsbFlashDrive ||
                                    dev.Category == DeviceCategory.ExternalUsbHdd ||
                                    dev.Category == DeviceCategory.ExternalUsbSsd ||
                                    dev.WindowsClass == StorageClass.DiskDrive);
                        break;
                    case 2: // Portable Devices
                        catMatch = (dev.Category == DeviceCategory.MobilePhoneOrMediaDevice ||
                                    dev.WindowsClass == StorageClass.WindowsPortableDevice);
                        break;
                    case 3: // Trusted
                        catMatch = isTrusted;
                        break;
                    case 4: // Untrusted
                        catMatch = !isTrusted;
                        break;
                    default: // All
                        catMatch = true;
                        break;
                }

                if (!catMatch) continue;

                if (!string.IsNullOrEmpty(query))
                {
                    bool qMatch = false;
                    if (!string.IsNullOrEmpty(dev.FriendlyName) && dev.FriendlyName.ToLowerInvariant().Contains(query)) qMatch = true;
                    else if (!string.IsNullOrEmpty(dev.Model) && dev.Model.ToLowerInvariant().Contains(query)) qMatch = true;
                    else if (!string.IsNullOrEmpty(dev.Manufacturer) && dev.Manufacturer.ToLowerInvariant().Contains(query)) qMatch = true;
                    else if (!string.IsNullOrEmpty(dev.SerialNumber) && dev.SerialNumber.ToLowerInvariant().Contains(query)) qMatch = true;
                    else if (!string.IsNullOrEmpty(dev.DeviceInstanceId) && dev.DeviceInstanceId.ToLowerInvariant().Contains(query)) qMatch = true;
                    else if (dev.VolumeMountings != null && string.Join(" ", dev.VolumeMountings.ToArray()).ToLowerInvariant().Contains(query)) qMatch = true;
                    else if (dev.HardwareIds != null && string.Join(" ", dev.HardwareIds.ToArray()).ToLowerInvariant().Contains(query)) qMatch = true;

                    if (!qMatch) continue;
                }

                filteredExternal.Add(dev);
            }

            int pad = DpiManager.ScaleInt(20);
            int curY = DpiManager.ScaleInt(14);
            int clientW = _devicesScroll.ContentPanel.ClientSize.Width;
            if (clientW < 300) clientW = _tabDevices.ClientSize.Width;
            int rowW = Math.Max(DpiManager.ScaleInt(300), clientW - (pad * 2));

            if (filteredExternal.Count == 0)
            {
                bool isFilterActive = !string.IsNullOrEmpty(query) || filterIdx > 0;
                _devicesEmptyState.SetSearchMode(isFilterActive, query);
                _devicesEmptyState.Location = new Point(pad, curY);
                _devicesEmptyState.Width = rowW;
                _devicesEmptyState.Visible = true;
                _devicesScroll.ContentPanel.Controls.Add(_devicesEmptyState);
                curY += _devicesEmptyState.Height + DpiManager.ScaleInt(16);
            }
            else
            {
                _devicesEmptyState.Visible = false;
                foreach (var dev in filteredExternal)
                {
                    bool isTrusted = TrustedDeviceRepository.IsDeviceTrusted(dev);
                    var eval = RuleEngine.Evaluate(dev, currentPolicy, isTempAccessActive);
                    bool isActuallyBlocked = (eval.RecommendedAction == RuleAction.DenyBlocked);
                    DeviceRow row = new DeviceRow(dev, isTrusted, isReadOnlyMode);
                    row.IsActuallyBlocked = isActuallyBlocked;
                    row.Location = new Point(pad, curY);
                    row.Width = rowW;
                    row.RightToLeft = Loc.RtlValue;

                    row.RowSelected += delegate(object s, EventArgs ea)
                    {
                        OnDeviceRowSelected(s as DeviceRow);
                    };
                    row.EjectRequested += delegate(object s, EventArgs ea)
                    {
                        DeviceRow r = s as DeviceRow;
                        if (r != null && r.Device != null) OnRowEjectRequested(r.Device);
                    };
                    row.TrustRequested += delegate(object s, EventArgs ea)
                    {
                        DeviceRow r = s as DeviceRow;
                        if (r != null && r.Device != null) OnRowTrustRequested(r.Device, r.IsTrusted);
                    };
                    row.DetailsRequested += delegate(object s, EventArgs ea)
                    {
                        OnDeviceRowSelected(s as DeviceRow);
                    };

                    if (_selectedDevice != null && string.Equals(_selectedDevice.DeviceInstanceId, dev.DeviceInstanceId, StringComparison.OrdinalIgnoreCase))
                    {
                        row.IsSelected = true;
                        if (_devicesDrawer != null)
                        {
                            _devicesDrawer.ShowDevice(dev, isTrusted, isReadOnlyMode);
                        }
                    }

                    _devicesScroll.ContentPanel.Controls.Add(row);
                    _activeDeviceRows.Add(row);
                    curY += row.Height + DpiManager.ScaleInt(6);
                }
            }

            // System Devices Section (Secondary, strictly non-actionable, excluded from USB Shield)
            if (systemDevices.Count > 0)
            {
                curY += DpiManager.ScaleInt(16);

                Panel sysSecHeader = new Panel();
                sysSecHeader.Location = new Point(pad, curY);
                int titleH = DpiManager.ScaleInt(isRtl ? 24 : 20);
                int subH = DpiManager.ScaleInt(isRtl ? 22 : 18);
                sysSecHeader.Size = new Size(rowW, titleH + subH + DpiManager.ScaleInt(4));
                sysSecHeader.Tag = "sysHeader";

                Label sysTitle = new Label();
                sysTitle.Text = Loc.Get("SystemDevicesSectionTitle");
                sysTitle.Font = DpiManager.GetFont(TypographyRole.Subtitle, isRtl);
                sysTitle.ForeColor = pal.TextPrimary;
                sysTitle.AutoSize = false;
                sysTitle.Location = new Point(0, 0);
                sysTitle.Size = new Size(rowW, titleH);
                sysTitle.TextAlign = isRtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;
                sysTitle.RightToLeft = Loc.RtlValue;

                Label sysSub = new Label();
                sysSub.Text = Loc.Get("SystemDevicesSectionSubtitle");
                sysSub.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
                sysSub.ForeColor = pal.TextMuted;
                sysSub.AutoSize = false;
                sysSub.Location = new Point(0, titleH + DpiManager.ScaleInt(2));
                sysSub.Size = new Size(rowW, subH);
                sysSub.TextAlign = isRtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;
                sysSub.RightToLeft = Loc.RtlValue;

                sysSecHeader.Controls.Add(sysTitle);
                sysSecHeader.Controls.Add(sysSub);
                _devicesScroll.ContentPanel.Controls.Add(sysSecHeader);
                curY += sysSecHeader.Height + DpiManager.ScaleInt(8);

                foreach (var dev in systemDevices)
                {
                    DeviceRow row = new DeviceRow(dev, false, false);
                    row.Location = new Point(pad, curY);
                    row.Width = rowW;
                    row.RightToLeft = Loc.RtlValue;

                    row.RowSelected += delegate(object s, EventArgs ea)
                    {
                        OnDeviceRowSelected(s as DeviceRow);
                    };
                    row.DetailsRequested += delegate(object s, EventArgs ea)
                    {
                        OnDeviceRowSelected(s as DeviceRow);
                    };

                    if (_selectedDevice != null && string.Equals(_selectedDevice.DeviceInstanceId, dev.DeviceInstanceId, StringComparison.OrdinalIgnoreCase))
                    {
                        row.IsSelected = true;
                        if (_devicesDrawer != null)
                        {
                            _devicesDrawer.ShowDevice(dev, false, false);
                        }
                    }

                    _devicesScroll.ContentPanel.Controls.Add(row);
                    _activeDeviceRows.Add(row);
                    curY += row.Height + DpiManager.ScaleInt(6);
                }
            }

            // Update metrics and sidebar badge
            if (_metricConnectedDevices != null)
                _metricConnectedDevices.ValueText = externalDevices.Count.ToString();

            if (_sidebar != null && _sidebar.Items.Count > 1)
            {
                _sidebar.Items[1].BadgeText = externalDevices.Count > 0 ? externalDevices.Count.ToString() : "";
                _sidebar.Invalidate();
            }

            // If selected device was removed from the list, close drawer
            if (_selectedDevice != null)
            {
                bool found = false;
                foreach (var d in allDevices)
                {
                    if (string.Equals(d.DeviceInstanceId, _selectedDevice.DeviceInstanceId, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    _selectedDevice = null;
                    if (_devicesDrawer != null) _devicesDrawer.CloseDrawer();
                }
            }

            _devicesScroll.ContentPanel.ResumeLayout(true);
            _devicesScroll.UpdateScroll();
        }

        private void OnDeviceRowSelected(DeviceRow row)
        {
            if (row == null || row.Device == null) return;

            _selectedDevice = row.Device;
            foreach (var r in _activeDeviceRows)
            {
                r.IsSelected = (r == row);
            }

            if (_devicesDrawer != null)
            {
                _devicesDrawer.UpdateResponsiveWidth(_devicesBodyPanel != null ? _devicesBodyPanel.ClientSize.Width : ClientSize.Width);
                bool isReadOnly = (SettingsRepository.Load().ActivePolicy != null && SettingsRepository.Load().ActivePolicy.ReadOnlyRemovable);
                _devicesDrawer.ShowDevice(row.Device, row.IsTrusted, isReadOnly);
            }
        }

        private void OnRowEjectRequested(DeviceIdentity dev)
        {
            if (dev == null) return;
            string prompt = string.Format(Loc.Get("SafeEjectConfirm"), dev.GetCleanDisplayName());
            if (MsgBox.Show(this, prompt, Loc.Get("AppTitle"), MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;

            System.Threading.Tasks.Task.Run(delegate
            {
                DeviceEjectOperationResult opResult;
                if (DeviceEnforcementLifecycleManager.IsTestMode && DeviceEjector.HasActiveSeams())
                {
                    opResult = DeviceEjector.EjectDeviceTransactional(dev);
                }
                else if (PnpSafeEjectBackend.IsSupported(dev))
                {
                    opResult = PnpSafeEjectBackend.EjectDevice(dev);
                }
                else
                {
                    opResult = DeviceEjector.EjectDeviceTransactional(dev);
                }

                bool ok = opResult != null && opResult.Success;
                string err = opResult != null ? (!string.IsNullOrEmpty(opResult.StatusMessage) ? opResult.StatusMessage : opResult.AggregateResult.ToString()) : "Operation failed.";

                try
                {
                    if (this.IsHandleCreated && !this.IsDisposed)
                    {
                        this.BeginInvoke(new MethodInvoker(delegate
                        {
                            if (ok)
                            {
                                LogActivity(string.Format(Loc.Get("SafeEjectSuccess"), dev.GetCleanDisplayName()), ActivityCategory.DeviceEvents, Loc.Get("BadgeSuccess"), Theme.AccentGreen);
                                MsgBox.Show(this, string.Format(Loc.Get("SafeEjectSuccess"), dev.GetCleanDisplayName()), Loc.Get("AppTitle"));
                                RefreshDevicesList();
                            }
                            else
                            {
                                LogActivity(string.Format(Loc.Get("SafeEjectFail"), dev.GetCleanDisplayName()) + " - " + err, ActivityCategory.Errors, Loc.Get("BadgeFailed"), Theme.AccentRed);
                                string userMsg = (opResult != null && (opResult.ConfigRet == PnpSafeEjectBackend.CR_REMOVE_VETOED || (opResult.StatusMessage != null && opResult.StatusMessage.IndexOf("veto", StringComparison.OrdinalIgnoreCase) >= 0)))
                                    ? Loc.Get("SafeEjectVetoed")
                                    : string.Format(Loc.Get("SafeEjectFail"), dev.GetCleanDisplayName());
                                MsgBox.Show(this, userMsg, Loc.Get("ErrorTitle"));
                            }
                        }));
                    }
                }
                catch { }
            });
        }

        private void OnRowTrustRequested(DeviceIdentity dev, bool currentlyTrusted)
        {
            if (dev == null) return;
            if (!RequireAuth(Loc.Get("ActionModifyTrusted"))) return;

            if (currentlyTrusted)
            {
                DeviceRule matchedRule = TrustedDeviceRepository.FindMatchingRule(dev);
                if (matchedRule != null)
                {
                    string err;
                    TrustedDeviceRepository.RemoveTrustedDevice(matchedRule.RuleId, out err);
                    LogActivity(string.Format(Loc.Get("EventDeviceRevoked"), dev.FriendlyName), ActivityCategory.DeviceEvents, Loc.Get("BadgeVerified"), Theme.AccentGreen);
                    RefreshDevicesList();
                    RefreshWhitelistView();
                }
            }
            else
            {
                string err;
                bool ok = TrustedDeviceRepository.AddTrustedDevice(dev, out err);
                if (ok)
                {
                    LogActivity(string.Format(Loc.Get("EventDeviceAllowed"), dev.FriendlyName), ActivityCategory.DeviceEvents, Loc.Get("BadgeVerified"), Theme.AccentGreen);
                    MsgBox.Show(this, string.Format(Loc.Get("TrustAddedSuccess"), dev.FriendlyName, "PnP"), Loc.Get("AppTitle"));
                    RefreshDevicesList();
                    RefreshWhitelistView();
                }
                else
                {
                    MsgBox.Show(this, Loc.Get("ErrorGeneric", err), Loc.Get("ErrorTitle"));
                }
            }
        }

        private void OnEjectAllStorage()
        {
            DialogResult dr = MsgBox.Show(this,
                Loc.Get("EjectAllConfirmMsg"),
                Loc.Get("EjectAllConfirmTitle"),
                MessageBoxButtons.YesNo);
            if (dr != DialogResult.Yes) return;

            System.Threading.Tasks.Task.Run(delegate
            {
                var devices = DeviceDiscovery.DiscoverAllDevices();
                int total = 0;
                int succeeded = 0;

                foreach (var dev in devices)
                {
                    if (dev.IsRemovableMedia || dev.PhysicalTransport == PhysicalBusType.Usb)
                    {
                        string guardReason;
                        if (SystemDeviceSafetyGuard.ValidateDevice(dev, out guardReason) == SafetyValidationResult.Passed)
                        {
                            total++;
                            DeviceEjectOperationResult opResult;
                            if (DeviceEnforcementLifecycleManager.IsTestMode && DeviceEjector.HasActiveSeams())
                            {
                                opResult = DeviceEjector.EjectDeviceTransactional(dev);
                            }
                            else if (PnpSafeEjectBackend.IsSupported(dev))
                            {
                                opResult = PnpSafeEjectBackend.EjectDevice(dev);
                            }
                            else
                            {
                                opResult = DeviceEjector.EjectDeviceTransactional(dev);
                            }

                            if (opResult != null && opResult.Success)
                            {
                                succeeded++;
                            }
                        }
                    }
                }

                string summary;
                if (total == 0)
                {
                    summary = Loc.Get("EjectAllNoneMsg");
                }
                else if (succeeded == total)
                {
                    summary = string.Format(Loc.Get("EjectAllSuccessMsg"), succeeded, total);
                }
                else
                {
                    summary = string.Format(Loc.Get("EjectAllPartialMsg"), succeeded, total);
                }

                try
                {
                    if (this.IsHandleCreated && !this.IsDisposed)
                    {
                        this.BeginInvoke(new MethodInvoker(delegate
                        {
                            MsgBox.Show(this, summary, Loc.Get("AppTitle"));
                            RefreshDevicesList();
                        }));
                    }
                }
                catch { }
            });
        }

        // ═══════════════════════════════════════════════════════════════════
        // PAGE 2: PROTECTION RULES (v2.1 Phase 4.3 Redesign)
        // ═══════════════════════════════════════════════════════════════════
        private void BuildTabRules()
        {
            _tabRules = new Panel();
            _tabRules.Dock = DockStyle.Fill;
            _tabRules.BackColor = ThemeManager.ActivePalette.BackgroundPrimary;

            _tabRulesScroll = new DarkScrollPanel();
            _tabRulesScroll.Dock = DockStyle.Fill;
            _tabRulesScroll.RightToLeft = Loc.RtlValue;
            _tabRules.Controls.Add(_tabRulesScroll);

            bool isRtl = (Loc.RtlValue == RightToLeft.Yes) || Loc.IsRtl;

            // Page Header Labels
            _protectionPageTitle = new Label();
            _protectionPageTitle.Text = Loc.Get("ProtectionTitle");
            _protectionPageTitle.Font = DpiManager.GetFont(TypographyRole.Title, isRtl);
            _protectionPageTitle.ForeColor = ThemeManager.ActivePalette.TextPrimary;
            _protectionPageTitle.AutoSize = true;
            _protectionPageTitle.Padding = new Padding(0, 0, 0, DpiManager.ScaleInt(4));
            _tabRulesScroll.ContentPanel.Controls.Add(_protectionPageTitle);

            _protectionPageSub = new Label();
            _protectionPageSub.Text = Loc.Get("ProtectionSubtitle");
            _protectionPageSub.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            _protectionPageSub.ForeColor = ThemeManager.ActivePalette.TextSecondary;
            _protectionPageSub.AutoSize = true;
            _protectionPageSub.Padding = new Padding(0, 0, 0, DpiManager.ScaleInt(4));
            _tabRulesScroll.ContentPanel.Controls.Add(_protectionPageSub);

            _btnRefreshProtection = new AppButton();
            _btnRefreshProtection.SizeVariant = ButtonSize.Small;
            _btnRefreshProtection.Variant = ButtonVariant.Secondary;
            _btnRefreshProtection.Text = Loc.Get("RefreshStatus");
            _btnRefreshProtection.Click += delegate { RefreshLiveStatus(); };
            _tabRulesScroll.ContentPanel.Controls.Add(_btnRefreshProtection);

            // Posture Card
            _postureCard = new ProtectionPostureCard();
            _tabRulesScroll.ContentPanel.Controls.Add(_postureCard);

            // Mismatch Banner
            _mismatchBar = new SafetyMessageBar();
            _mismatchBar.MessageText = Loc.Get("PostureMismatchDesc");
            _mismatchBar.Visible = false;
            _tabRulesScroll.ContentPanel.Controls.Add(_mismatchBar);

            // Section A: Storage Protection
            _secStorageCard = new ProtectionSectionCard();
            _secStorageCard.SectionTitle = Loc.Get("SecStorageProtection");
            _secStorageCard.SectionDescription = Loc.Get("SecStorageProtectionDesc");
            _secStorageCard.IsCollapsible = true;
            _secStorageCard.IsExpanded = true;

            _rowRemovableDisks = new ProtectionSettingRow();
            _rowRemovableDisks.IconType = ProtectionSettingIcon.StorageDisk;
            _rowRemovableDisks.Title = Loc.Get("RuleBlockRemovableDisks");
            _rowRemovableDisks.Subtitle = Loc.Get("RuleBlockRemovableDisksDesc");
            _rowRemovableDisks.ToolTipText = Loc.Get("TooltipBlockRemovable");
            _rowRemovableDisks.CheckedChanged += delegate { OnRuleToggled(); };
            _secStorageCard.AddRow(_rowRemovableDisks);

            _rowReadOnly = new ProtectionSettingRow();
            _rowReadOnly.IconType = ProtectionSettingIcon.ReadOnly;
            _rowReadOnly.Title = Loc.Get("RuleReadOnly");
            _rowReadOnly.Subtitle = Loc.Get("RuleReadOnlyDesc");
            _rowReadOnly.ToolTipText = Loc.Get("TooltipReadOnly");
            _rowReadOnly.CheckedChanged += delegate { OnRuleToggled(); };
            _secStorageCard.AddRow(_rowReadOnly);

            _secStorageCard.ExpandedChanged += delegate { LayoutProtectionCards(); };
            _tabRulesScroll.ContentPanel.Controls.Add(_secStorageCard);

            // Section B: Device Control
            _secDeviceCard = new ProtectionSectionCard();
            _secDeviceCard.SectionTitle = Loc.Get("SecDeviceControl");
            _secDeviceCard.SectionDescription = Loc.Get("SecDeviceControlDesc");
            _secDeviceCard.IsCollapsible = true;
            _secDeviceCard.IsExpanded = false;

            _rowPhones = new ProtectionSettingRow();
            _rowPhones.IconType = ProtectionSettingIcon.PortableDevice;
            _rowPhones.Title = Loc.Get("RuleBlockPhones");
            _rowPhones.Subtitle = Loc.Get("RuleBlockPhonesDesc");
            _rowPhones.ToolTipText = Loc.Get("TooltipPhones");
            _rowPhones.CheckedChanged += delegate { OnRuleToggled(); };
            _secDeviceCard.AddRow(_rowPhones);

            _rowWhitelist = new ProtectionSettingRow();
            _rowWhitelist.IconType = ProtectionSettingIcon.Whitelist;
            _rowWhitelist.Title = Loc.Get("RuleWhitelistMode");
            _rowWhitelist.Subtitle = Loc.Get("RuleWhitelistModeDesc");
            _rowWhitelist.ToolTipText = Loc.Get("TooltipWhitelist");

            _btnManageTrusted = new AppButton();
            _btnManageTrusted.SizeVariant = ButtonSize.Small;
            _btnManageTrusted.Variant = ButtonVariant.Ghost;
            _btnManageTrusted.Text = Loc.Get("ManageTrustedDevices");
            _btnManageTrusted.Click += delegate { SwitchTab(3); };
            _rowWhitelist.ActionControl = _btnManageTrusted;
            _rowWhitelist.CheckedChanged += delegate { OnRuleToggled(); };
            _secDeviceCard.AddRow(_rowWhitelist);

            _secDeviceCard.ExpandedChanged += delegate
            {
                if (_secDeviceCard.IsExpanded)
                {
                    if (_secAutoCard != null && _secAutoCard.IsExpanded) _secAutoCard.IsExpanded = false;
                    if (_secAdvancedCard != null && _secAdvancedCard.IsExpanded) _secAdvancedCard.IsExpanded = false;
                }
                LayoutProtectionCards();
            };
            _tabRulesScroll.ContentPanel.Controls.Add(_secDeviceCard);

            // Section C: Automatic Protection
            _secAutoCard = new ProtectionSectionCard();
            _secAutoCard.SectionTitle = Loc.Get("SecAutoProtection");
            _secAutoCard.SectionDescription = Loc.Get("SecAutoProtectionDesc");
            _secAutoCard.IsCollapsible = true;
            _secAutoCard.IsExpanded = false;

            _rowFlashSentinel = new ProtectionSettingRow();
            _rowFlashSentinel.IconType = ProtectionSettingIcon.FlashSentinel;
            _rowFlashSentinel.Title = Loc.Get("RuleBlockFlashOnly");
            _rowFlashSentinel.Subtitle = Loc.Get("RuleBlockFlashOnlyDesc");
            _rowFlashSentinel.ToolTipText = Loc.Get("TooltipFlashSentinel");
            _rowFlashSentinel.CheckedChanged += delegate { OnRuleToggled(); };
            _secAutoCard.AddRow(_rowFlashSentinel);

            _rowAutoPlayGuard = new ProtectionSettingRow();
            _rowAutoPlayGuard.IconType = ProtectionSettingIcon.AutoPlay;
            _rowAutoPlayGuard.Title = Loc.Get("RuleAutoPlayGuard");
            _rowAutoPlayGuard.Subtitle = Loc.Get("RuleAutoPlayGuardDesc");
            _rowAutoPlayGuard.ToolTipText = Loc.Get("TooltipAutoPlayGuard");
            _rowAutoPlayGuard.CheckedChanged += delegate { OnRuleToggled(); };
            _secAutoCard.AddRow(_rowAutoPlayGuard);

            _secAutoCard.ExpandedChanged += delegate
            {
                if (_secAutoCard.IsExpanded)
                {
                    if (_secDeviceCard != null && _secDeviceCard.IsExpanded) _secDeviceCard.IsExpanded = false;
                    if (_secAdvancedCard != null && _secAdvancedCard.IsExpanded) _secAdvancedCard.IsExpanded = false;
                }
                LayoutProtectionCards();
            };
            _tabRulesScroll.ContentPanel.Controls.Add(_secAutoCard);

            // Section D: Advanced System Policies (Accordion)
            _secAdvancedCard = new ProtectionSectionCard();
            _secAdvancedCard.SectionTitle = Loc.Get("SecAdvancedPolicies");
            _secAdvancedCard.SectionDescription = Loc.Get("SecAdvancedPoliciesDesc");
            _secAdvancedCard.IsCollapsible = true;
            _secAdvancedCard.IsExpanded = false;

            _rowStrictPnp = new ProtectionSettingRow();
            _rowStrictPnp.IconType = ProtectionSettingIcon.StrictPnp;
            _rowStrictPnp.Title = Loc.Get("RuleStrictPnp");
            _rowStrictPnp.Subtitle = Loc.Get("RuleStrictPnpDesc");
            _rowStrictPnp.ToolTipText = Loc.Get("TooltipStrictPnp");
            _rowStrictPnp.CheckedChanged += delegate { OnRuleToggled(); };
            _secAdvancedCard.AddRow(_rowStrictPnp);

            _rowHardBlock = new ProtectionSettingRow();
            _rowHardBlock.IconType = ProtectionSettingIcon.HardBlock;
            _rowHardBlock.Title = Loc.Get("RuleHardBlock");
            _rowHardBlock.Subtitle = Loc.Get("RuleHardBlockDesc");
            _rowHardBlock.ToolTipText = Loc.Get("TooltipHardBlock");
            _rowHardBlock.CheckedChanged += delegate { OnRuleToggled(); };
            _secAdvancedCard.AddRow(_rowHardBlock);

            _secAdvancedCard.ExpandedChanged += delegate
            {
                if (_secAdvancedCard.IsExpanded)
                {
                    if (_secDeviceCard != null && _secDeviceCard.IsExpanded) _secDeviceCard.IsExpanded = false;
                    if (_secAutoCard != null && _secAutoCard.IsExpanded) _secAutoCard.IsExpanded = false;
                }
                LayoutProtectionCards();
            };
            _tabRulesScroll.ContentPanel.Controls.Add(_secAdvancedCard);

            _tabRulesScroll.ContentPanel.Resize += delegate { LayoutProtectionCards(); };
            LayoutProtectionCards();
            _tabRulesScroll.UpdateScroll();
        }

        private void LayoutProtectionCards()
        {
            if (_tabRulesScroll == null || _tabRulesScroll.ContentPanel == null) return;

            bool isRtl = (Loc.RtlValue == RightToLeft.Yes) || Loc.IsRtl;
            int pad = DpiManager.ScaleInt(SpacingTokens.Space20);
            int clientW = _tabRulesScroll.ContentPanel.ClientSize.Width;
            int maxContentW = DpiManager.ScaleInt(1080);
            int availableW = clientW - (pad * 2);
            int cardW = Math.Min(maxContentW, Math.Max(DpiManager.ScaleInt(300), availableW));
            int startX = pad + Math.Max(0, (availableW - cardW) / 2);

            int curY = pad;

            // Layout Header
            if (_btnRefreshProtection != null && _protectionPageTitle != null)
            {
                int btnW = AppButton.CalculateWidth(_btnRefreshProtection.Text, ButtonSize.Small, isRtl);
                int btnH = DpiManager.ScaleInt(26);
                _btnRefreshProtection.Size = new Size(btnW, btnH);

                if (isRtl)
                {
                    _btnRefreshProtection.Location = new Point(startX, curY);
                    _protectionPageTitle.Location = new Point(startX + cardW - _protectionPageTitle.Width, curY);
                    if (_protectionPageSub != null)
                    {
                        _protectionPageSub.Location = new Point(startX + cardW - _protectionPageSub.Width, curY + _protectionPageTitle.Height + DpiManager.ScaleInt(3));
                    }
                }
                else
                {
                    _protectionPageTitle.Location = new Point(startX, curY);
                    if (_protectionPageSub != null)
                    {
                        _protectionPageSub.Location = new Point(startX, curY + _protectionPageTitle.Height + DpiManager.ScaleInt(3));
                    }
                    _btnRefreshProtection.Location = new Point(startX + cardW - btnW, curY);
                }

                int headerH = Math.Max(_protectionPageTitle.Height + (_protectionPageSub != null ? _protectionPageSub.Height : 0) + DpiManager.ScaleInt(4), btnH);
                curY += headerH + DpiManager.ScaleInt(10);
            }
            else
            {
                curY += DpiManager.ScaleInt(44);
            }

            // Posture Card
            if (_postureCard != null)
            {
                _postureCard.UpdatePostureHeight();
                _postureCard.Location = new Point(startX, curY);
                _postureCard.Width = cardW;
                _postureCard.PerformChildLayout();
                curY += _postureCard.Height + DpiManager.ScaleInt(14);
            }

            // Mismatch Banner
            if (_mismatchBar != null && _mismatchBar.Visible)
            {
                _mismatchBar.Location = new Point(startX, curY);
                _mismatchBar.Width = cardW;
                curY += _mismatchBar.Height + DpiManager.ScaleInt(12);
            }

            // Section Cards
            if (_secStorageCard != null)
            {
                _secStorageCard.Location = new Point(startX, curY);
                _secStorageCard.Width = cardW;
                _secStorageCard.UpdateCardGeometry();
                curY += _secStorageCard.Height + DpiManager.ScaleInt(12);
            }

            if (_secDeviceCard != null)
            {
                _secDeviceCard.Location = new Point(startX, curY);
                _secDeviceCard.Width = cardW;
                _secDeviceCard.UpdateCardGeometry();
                curY += _secDeviceCard.Height + DpiManager.ScaleInt(12);
            }

            if (_secAutoCard != null)
            {
                _secAutoCard.Location = new Point(startX, curY);
                _secAutoCard.Width = cardW;
                _secAutoCard.UpdateCardGeometry();
                curY += _secAutoCard.Height + DpiManager.ScaleInt(12);
            }

            if (_secAdvancedCard != null)
            {
                _secAdvancedCard.Location = new Point(startX, curY);
                _secAdvancedCard.Width = cardW;
                _secAdvancedCard.UpdateCardGeometry();
                curY += _secAdvancedCard.Height + pad;
            }

            _tabRulesScroll.UpdateScroll();
        }

        private static int _currentPolicyMutationGeneration = 0;
        public static int CurrentPolicyMutationGeneration
        {
            get { return _currentPolicyMutationGeneration; }
            set { _currentPolicyMutationGeneration = value; }
        }
        public static int IncrementMutationGeneration()
        {
            return System.Threading.Interlocked.Increment(ref _currentPolicyMutationGeneration);
        }
        public static int ReadMutationGeneration()
        {
            return System.Threading.Thread.VolatileRead(ref _currentPolicyMutationGeneration);
        }

        private bool _isSyncingRules = false;
        private void SyncRuleTogglesWithSettings()
        {
            _isSyncingRules = true;
            try
            {
                AppSettingsData s = SettingsRepository.Load();
                ProtectionPolicy p = s.ActivePolicy ?? new ProtectionPolicy();

                if (_rowRemovableDisks != null) _rowRemovableDisks.Checked = p.BlockRemovableDisks;
                if (_rowFlashSentinel != null) _rowFlashSentinel.Checked = p.BlockFlashDrivesOnlyReactive;
                if (_rowAutoPlayGuard != null) _rowAutoPlayGuard.Checked = p.AutoPlayGuard;
                if (_rowPhones != null) _rowPhones.Checked = p.BlockWpdPhonesAndMedia;
                if (_rowStrictPnp != null) _rowStrictPnp.Checked = p.StrictPnpLockdown;
                if (_rowWhitelist != null) _rowWhitelist.Checked = p.WhitelistMode;
                if (_rowHardBlock != null) _rowHardBlock.Checked = p.HardBlockUsbStor;

                // Storage Protection Dependency: ReadOnly is superseded when BlockRemovableDisks (or BlockAllRemovable) is active
                bool isStorageBlocked = (p.BlockRemovableDisks || p.BlockAllRemovable);
                if (_rowReadOnly != null)
                {
                    _rowReadOnly.Checked = p.ReadOnlyRemovable;
                    if (isStorageBlocked)
                    {
                        _rowReadOnly.IsDependentDisabled = true;
                        _rowReadOnly.HelperText = Loc.Get("RuleSupersededByBlock");
                        _rowReadOnly.BadgeText = Loc.Get("BadgeSuperseded");
                        _rowReadOnly.BadgeVariant = StatusBadgeVariant.Warning;
                    }
                    else
                    {
                        _rowReadOnly.IsDependentDisabled = false;
                        _rowReadOnly.HelperText = "";
                        _rowReadOnly.BadgeText = p.ReadOnlyRemovable ? Loc.Get("BadgeEnforced") : Loc.Get("BadgeAllowed");
                        _rowReadOnly.BadgeVariant = p.ReadOnlyRemovable ? StatusBadgeVariant.Info : StatusBadgeVariant.Neutral;
                    }
                }

                // Row Badges
                if (_rowRemovableDisks != null)
                {
                    _rowRemovableDisks.BadgeText = p.BlockRemovableDisks ? Loc.Get("BadgeLocked") : Loc.Get("BadgeAllowed");
                    _rowRemovableDisks.BadgeVariant = p.BlockRemovableDisks ? StatusBadgeVariant.Success : StatusBadgeVariant.Neutral;
                }
                if (_rowPhones != null)
                {
                    _rowPhones.BadgeText = p.BlockWpdPhonesAndMedia ? Loc.Get("BadgeLocked") : Loc.Get("BadgeAllowed");
                    _rowPhones.BadgeVariant = p.BlockWpdPhonesAndMedia ? StatusBadgeVariant.Success : StatusBadgeVariant.Neutral;
                }
                if (_rowFlashSentinel != null)
                {
                    _rowFlashSentinel.BadgeText = p.BlockFlashDrivesOnlyReactive ? Loc.Get("BadgeMonitoring") : Loc.Get("BadgeAllowed");
                    _rowFlashSentinel.BadgeVariant = p.BlockFlashDrivesOnlyReactive ? StatusBadgeVariant.Info : StatusBadgeVariant.Neutral;
                }
                if (_rowAutoPlayGuard != null)
                {
                    _rowAutoPlayGuard.BadgeText = p.AutoPlayGuard ? Loc.Get("BadgeAutoPlayBlocked") : Loc.Get("BadgeAllowed");
                    _rowAutoPlayGuard.BadgeVariant = p.AutoPlayGuard ? StatusBadgeVariant.Info : StatusBadgeVariant.Neutral;
                }
                if (_rowWhitelist != null)
                {
                    _rowWhitelist.BadgeText = p.WhitelistMode ? Loc.Get("BadgeMonitoring") : Loc.Get("BadgeAllowed");
                    _rowWhitelist.BadgeVariant = p.WhitelistMode ? StatusBadgeVariant.Info : StatusBadgeVariant.Neutral;
                }
                if (_rowStrictPnp != null)
                {
                    _rowStrictPnp.BadgeText = p.StrictPnpLockdown ? Loc.Get("BadgeLocked") : Loc.Get("BadgeAllowed");
                    _rowStrictPnp.BadgeVariant = p.StrictPnpLockdown ? StatusBadgeVariant.Warning : StatusBadgeVariant.Neutral;
                }
                if (_rowHardBlock != null)
                {
                    _rowHardBlock.BadgeText = p.HardBlockUsbStor ? Loc.Get("BadgeRestartRequired") : Loc.Get("BadgeAllowed");
                    _rowHardBlock.BadgeVariant = p.HardBlockUsbStor ? StatusBadgeVariant.Danger : StatusBadgeVariant.Neutral;
                }

                // Update Accordion Section Summaries (Real State, No Invented Metrics)
                if (_secStorageCard != null)
                {
                    if (p.BlockRemovableDisks || p.BlockAllRemovable)
                        _secStorageCard.SummaryText = Loc.Get("ControlsBlockedSummary");
                    else if (p.ReadOnlyRemovable)
                        _secStorageCard.SummaryText = Loc.Get("ControlsReadOnlySummary");
                    else
                        _secStorageCard.SummaryText = Loc.Get("ControlsAllowedSummary");
                }

                if (_secDeviceCard != null)
                {
                    int devActive = (p.BlockWpdPhonesAndMedia ? 1 : 0) + (p.WhitelistMode ? 1 : 0);
                    if (devActive == 0)
                        _secDeviceCard.SummaryText = Loc.Get("ControlsAllowedSummary");
                    else
                        _secDeviceCard.SummaryText = Loc.Get("ControlsActiveSummary", devActive);
                }

                if (_secAutoCard != null)
                {
                    int autoActive = (p.BlockFlashDrivesOnlyReactive ? 1 : 0) + (p.AutoPlayGuard ? 1 : 0);
                    if (autoActive == 0)
                        _secAutoCard.SummaryText = Loc.Get("ControlsDisabledSummary");
                    else
                        _secAutoCard.SummaryText = Loc.Get("ControlsActiveSummary", autoActive);
                }

                if (_secAdvancedCard != null)
                {
                    int advActive = (p.StrictPnpLockdown ? 1 : 0) + (p.HardBlockUsbStor ? 1 : 0);
                    if (advActive == 0)
                        _secAdvancedCard.SummaryText = Loc.Get("ControlsDisabledSummary");
                    else
                        _secAdvancedCard.SummaryText = Loc.Get("ControlsActiveSummary", advActive);
                }

                // Update Authoritative Posture
                bool tempActive = (TemporaryAccessManager.PeekRemainingSeconds() > 0);
                ProtectionStatusReport report = TestIsolatedReport;
                if (report == null)
                {
                    if (IsCaptureMode)
                    {
                        report = new ProtectionStatusReport
                        {
                            OverallState = (p.BlockRemovableDisks || p.BlockAllRemovable) ? ActivationState.ClassPolicyConfigured : (p.IsAnyProtectionActive() ? ActivationState.AppliedAndVerified : ActivationState.Unprotected),
                            LastVerifiedUtc = DateTime.UtcNow
                        };
                    }
                    else
                    {
                        try { report = StateVerifier.Verify(p); } catch { }
                    }
                }

                UpdateProtectionPosture(p, report, tempActive);

                // Update count of active rules for stat card
                int count = 0;
                if (p.BlockRemovableDisks) count++;
                if (p.BlockFlashDrivesOnlyReactive) count++;
                if (p.AutoPlayGuard) count++;
                if (p.BlockWpdPhonesAndMedia) count++;
                if (p.ReadOnlyRemovable) count++;
                if (p.StrictPnpLockdown) count++;
                if (p.WhitelistMode) count++;
                if (p.HardBlockUsbStor) count++;

                if (_metricActiveRules != null)
                    _metricActiveRules.ValueText = count.ToString();
                if (_protectionOverviewPanel != null)
                    _protectionOverviewPanel.UpdatePolicy(p, Loc.IsRtl);
            }
            finally
            {
                _isSyncingRules = false;
            }
        }

        private void UpdateProtectionPosture(ProtectionPolicy p, ProtectionStatusReport report, bool tempActive)
        {
            bool isConflict = (report != null && report.OverallState == ActivationState.ConfigurationConflict);

            if (_mismatchBar != null)
            {
                _mismatchBar.IsActive = isConflict;
                _mismatchBar.Visible = isConflict;
                if (isConflict)
                {
                    _mismatchBar.MessageText = Loc.Get("PostureMismatchDesc");
                }
            }

            string postureTitle;
            string postureDesc;
            StatusBadgeVariant postureVariant;
            string auditText;
            string topBarText;

            if (tempActive)
            {
                int remainingMinutes = Math.Max(1, (TemporaryAccessManager.PeekRemainingSeconds() + 59) / 60);
                postureTitle = Loc.Get("PostureTempActive");
                postureDesc = Loc.Get("TempAccessBannerDesc", remainingMinutes);
                postureVariant = StatusBadgeVariant.Warning;
                topBarText = Loc.IsRtl ? "مؤقت نشط" : "TEMP ACTIVE";
                auditText = "";
            }
            else if (isConflict)
            {
                postureTitle = Loc.Get("PostureMismatch");
                postureDesc = Loc.Get("PostureMismatchDesc");
                postureVariant = StatusBadgeVariant.Danger;
                topBarText = Loc.IsRtl ? "عدم تطابق" : "MISMATCH";
                auditText = "";
            }
            else if (p.BlockRemovableDisks || p.BlockAllRemovable || p.HardBlockUsbStor || p.WhitelistMode)
            {
                postureTitle = Loc.Get("PostureProtected");
                postureDesc = Loc.Get("PostureProtectedDesc");
                postureVariant = StatusBadgeVariant.Success;
                topBarText = Loc.IsRtl ? "محمي" : "PROTECTED";
                auditText = p.HardBlockUsbStor ? Loc.Get("PosturePendingRestart") : Loc.Get("PostureVerified");
            }
            else if (p.ReadOnlyRemovable || p.BlockWpdPhonesAndMedia || p.BlockFlashDrivesOnlyReactive || p.StrictPnpLockdown || p.AutoPlayGuard)
            {
                postureTitle = Loc.Get("PosturePartiallyProtected");
                postureDesc = Loc.Get("PosturePartiallyProtectedDesc");
                postureVariant = StatusBadgeVariant.Info;
                topBarText = Loc.IsRtl ? "حماية جزئية" : "PARTIAL";
                auditText = Loc.Get("PostureVerified");
            }
            else
            {
                postureTitle = Loc.Get("PostureUnprotected");
                postureDesc = Loc.Get("PostureUnprotectedDesc");
                postureVariant = StatusBadgeVariant.Neutral;
                topBarText = Loc.IsRtl ? "غير محمي" : "UNPROTECTED";
                auditText = "";
            }

            if (_postureCard != null)
            {
                _postureCard.SetPosture(postureTitle, postureDesc, postureVariant, postureTitle, auditText);
            }

            if (_topBar != null && _topBar.PostureBadge != null)
            {
                _topBar.PostureBadge.Text = topBarText;
                _topBar.PostureBadge.Variant = postureVariant;
            }

            LayoutProtectionCards();
        }

        private void OnRuleToggled()
        {
            if (_isSyncingRules) return;

            long t0Ticks = System.Diagnostics.Stopwatch.GetTimestamp();
            UserActionLatencyRecord actionRec = new UserActionLatencyRecord("RuleToggle");
            actionRec.T0_UserClickTicks = t0Ticks;
            actionRec.T1_UiHandlerEnteredTicks = System.Diagnostics.Stopwatch.GetTimestamp();

            if (!RequireAuth(Loc.Get("ActionModifyPolicy")))
            {
                SyncRuleTogglesWithSettings();
                return;
            }

            AppSettingsData currentSettings = SettingsRepository.Load();
            ProtectionPolicy currentPolicy = currentSettings.ActivePolicy ?? new ProtectionPolicy();

            bool requestedWhitelist = _rowWhitelist != null && _rowWhitelist.Checked;
            bool requestedRemovable = _rowRemovableDisks != null && _rowRemovableDisks.Checked;

            // Conflict 1: User tries to enable WhitelistMode while Removable Disks block is currently ON
            if (requestedWhitelist && !currentPolicy.WhitelistMode && (currentPolicy.BlockRemovableDisks || currentPolicy.BlockAllRemovable))
            {
                DialogResult dr = MsgBox.Show(this,
                    Loc.Get("ConflictSwitchToTrustedPrompt"),
                    Loc.Get("ConflictSwitchToTrustedTitle"),
                    MessageBoxButtons.YesNo);

                if (dr == DialogResult.Yes)
                {
                    _isSyncingRules = true;
                    if (_rowRemovableDisks != null) _rowRemovableDisks.Checked = false;
                    _isSyncingRules = false;
                    requestedRemovable = false;
                }
                else
                {
                    _isSyncingRules = true;
                    if (_rowWhitelist != null) _rowWhitelist.Checked = false;
                    _isSyncingRules = false;
                    return;
                }
            }

            // Conflict 2: User tries to enable Removable Disks block while WhitelistMode is currently ON
            if (requestedRemovable && !currentPolicy.BlockRemovableDisks && currentPolicy.WhitelistMode)
            {
                DialogResult dr = MsgBox.Show(this,
                    Loc.Get("ConflictSwitchToBlockPrompt"),
                    Loc.Get("ConflictSwitchToBlockTitle"),
                    MessageBoxButtons.YesNo);

                if (dr == DialogResult.Yes)
                {
                    _isSyncingRules = true;
                    if (_rowWhitelist != null) _rowWhitelist.Checked = false;
                    _isSyncingRules = false;
                    requestedWhitelist = false;
                }
                else
                {
                    _isSyncingRules = true;
                    if (_rowRemovableDisks != null) _rowRemovableDisks.Checked = false;
                    _isSyncingRules = false;
                    return;
                }
            }

            ProtectionPolicy newPolicy = new ProtectionPolicy
            {
                BlockAllRemovable = currentPolicy.BlockAllRemovable && requestedRemovable,
                BlockRemovableDisks = requestedRemovable,
                BlockFlashDrivesOnlyReactive = _rowFlashSentinel != null && _rowFlashSentinel.Checked,
                BlockWpdPhonesAndMedia = _rowPhones != null && _rowPhones.Checked,
                ReadOnlyRemovable = _rowReadOnly != null && _rowReadOnly.Checked,
                StrictPnpLockdown = _rowStrictPnp != null && _rowStrictPnp.Checked,
                WhitelistMode = requestedWhitelist,
                HardBlockUsbStor = _rowHardBlock != null && _rowHardBlock.Checked,
                AutoPlayGuard = _rowAutoPlayGuard != null && _rowAutoPlayGuard.Checked,
                PolicyVersion = (currentPolicy != null ? currentPolicy.PolicyVersion : 1) + 1,
                LastModifiedUtc = DateTime.UtcNow
            };

            // Incompatibility Guard: HardBlock + Whitelist
            if (newPolicy.HardBlockUsbStor && newPolicy.WhitelistMode)
            {
                MsgBox.Show(this, Loc.Get("ConflictHardBlockWhitelist"), Loc.Get("ErrorTitle"));
                SyncRuleTogglesWithSettings();
                return;
            }

            // Visual feedback: Show operation in progress
            if (_postureCard != null)
            {
                _postureCard.SetPosture(Loc.Get("StatusApplying"), Loc.Get("StatusApplying"), StatusBadgeVariant.Warning, Loc.Get("StatusApplying"), "");
            }
            if (_topBar != null && _topBar.PostureBadge != null)
            {
                _topBar.PostureBadge.Text = Loc.IsRtl ? "جاري التطبيق..." : "APPLYING...";
                _topBar.PostureBadge.Variant = StatusBadgeVariant.Warning;
            }

            SetRuleTogglesEnabled(false);
            Application.DoEvents();

            actionRec.T2_RegistryTxnStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            int mutationGen = System.Threading.Interlocked.Increment(ref _currentPolicyMutationGeneration);

            if (!this.IsHandleCreated)
            {
                try
                {
                    string applyError;
                    bool ok = ApplyProtectionPolicyUnified(newPolicy, Loc.Get("EventPolicyChanged"), out applyError);
                    actionRec.T3_RegistryWriteDoneTicks = System.Diagnostics.Stopwatch.GetTimestamp();

                    var statusReport = StateVerifier.Verify(newPolicy);
                    actionRec.T4_ReadBackVerifiedTicks = System.Diagnostics.Stopwatch.GetTimestamp();

                    if (ok)
                    {
                        actionRec.T5_RefreshStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        RefreshLiveStatus();
                        actionRec.T6_RefreshDoneTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        actionRec.T7_UiRenderDoneTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        LatencyTracker.StoreActionRecord(actionRec);
                    }
                    else
                    {
                        MsgBox.Show(this, Loc.Get("ErrorGeneric", applyError), Loc.Get("ErrorTitle"));
                        SyncRuleTogglesWithSettings();
                        RefreshLiveStatus();
                    }
                }
                finally
                {
                    SetRuleTogglesEnabled(true);
                }
                return;
            }

            System.Threading.Tasks.Task.Run(() =>
            {
                string applyError;
                bool ok = ApplyProtectionPolicyUnified(newPolicy, Loc.Get("EventPolicyChanged"), out applyError);
                return new { Ok = ok, Error = applyError };
            }).ContinueWith(t =>
            {
                if (this.IsDisposed || !this.IsHandleCreated) return;
                try
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        if (this.IsDisposed || !this.IsHandleCreated) return;
                        if (mutationGen != System.Threading.Thread.VolatileRead(ref _currentPolicyMutationGeneration))
                        {
                            return; // Stale completion callback discarded
                        }

                        try
                        {
                            actionRec.T3_RegistryWriteDoneTicks = System.Diagnostics.Stopwatch.GetTimestamp();

                            var statusReport = StateVerifier.Verify(newPolicy);
                            actionRec.T4_ReadBackVerifiedTicks = System.Diagnostics.Stopwatch.GetTimestamp();

                            bool ok = !t.IsFaulted && !t.IsCanceled && t.Result != null && t.Result.Ok;
                            string applyError = t.IsFaulted
                                ? (t.Exception != null ? t.Exception.GetBaseException().Message : "Task faulted.")
                                : (t.IsCanceled ? "Task canceled." : (t.Result != null ? t.Result.Error : null));

                            if (ok)
                            {
                                actionRec.T5_RefreshStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                                RefreshLiveStatus();
                                actionRec.T6_RefreshDoneTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                                actionRec.T7_UiRenderDoneTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                                LatencyTracker.StoreActionRecord(actionRec);
                            }
                            else
                            {
                                MsgBox.Show(this, Loc.Get("ErrorGeneric", applyError), Loc.Get("ErrorTitle"));
                                SyncRuleTogglesWithSettings();
                                RefreshLiveStatus();
                            }
                        }
                        finally
                        {
                            SetRuleTogglesEnabled(true);
                        }
                    }));
                }
                catch { }
            });
        }

        private void SetRuleTogglesEnabled(bool enabled)
        {
            if (_rowRemovableDisks != null) _rowRemovableDisks.Enabled = enabled;
            if (_rowWhitelist != null) _rowWhitelist.Enabled = enabled;
            if (_rowFlashSentinel != null) _rowFlashSentinel.Enabled = enabled;
            if (_rowAutoPlayGuard != null) _rowAutoPlayGuard.Enabled = enabled;
            if (_rowPhones != null) _rowPhones.Enabled = enabled;
            if (_rowReadOnly != null)
            {
                _rowReadOnly.Enabled = enabled;
                if (_rowReadOnly.IsDependentDisabled)
                {
                    _rowReadOnly.Toggle.Enabled = false;
                }
            }
            if (_rowStrictPnp != null) _rowStrictPnp.Enabled = enabled;
            if (_rowHardBlock != null) _rowHardBlock.Enabled = enabled;
            if (_btnRefreshProtection != null) _btnRefreshProtection.Enabled = enabled;
        }

        // ═══════════════════════════════════════════════════════════════════
        // ═══════════════════════════════════════════════════════════════════
        // PAGE 3: TRUSTED DEVICES (WHITELIST) — REDESIGNED FOR v2.1 Phase 4.4
        // ═══════════════════════════════════════════════════════════════════
        private void BuildTabWhitelist()
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = Loc.IsRtl;

            _tabWhitelist = new Panel();
            _tabWhitelist.Dock = DockStyle.Fill;
            _tabWhitelist.BackColor = pal.BackgroundPrimary;
            _tabWhitelist.RightToLeft = Loc.RtlValue;

            // ── 1. TOP HEADER & TOOLBAR ─────────────────────────────────────
            _trustedHeaderPanel = new Panel();
            _trustedHeaderPanel.Dock = DockStyle.Top;
            _trustedHeaderPanel.Height = DpiManager.ScaleInt(74);
            _trustedHeaderPanel.BackColor = pal.BackgroundPrimary;

            _trustedPageTitle = new Label();
            _trustedPageTitle.Text = Loc.Get("TrustedPageTitle");
            _trustedPageTitle.Font = Theme.FontTitle;
            _trustedPageTitle.ForeColor = pal.TextPrimary;
            _trustedPageTitle.AutoSize = true;

            _trustedPageSub = new Label();
            _trustedPageSub.Text = Loc.Get("TrustedPageSubtitle");
            _trustedPageSub.Font = Theme.FontNormal;
            _trustedPageSub.ForeColor = pal.TextSecondary;
            _trustedPageSub.AutoSize = true;

            // Action: Refresh Button
            _btnRefreshTrusted = new IconButton();
            _btnRefreshTrusted.ButtonType = IconButtonType.Refresh;
            _btnRefreshTrusted.SizeVariant = IconButtonSize.Standard;
            _btnRefreshTrusted.ToolTipText = Loc.Get("RefreshStatus");
            _btnRefreshTrusted.Click += delegate { RefreshWhitelistView(); };

            // Action: Add Connected Device
            _btnAddTrustedDevice = new AppButton();
            _btnAddTrustedDevice.Text = Loc.Get("AddConnectedDevice");
            _btnAddTrustedDevice.Variant = ButtonVariant.Secondary;
            _btnAddTrustedDevice.SizeVariant = ButtonSize.Medium;
            _btnAddTrustedDevice.Width = AppButton.CalculateWidth(_btnAddTrustedDevice.Text, ButtonSize.Medium, isRtl);
            _btnAddTrustedDevice.Click += delegate { OnAddConnectedDeviceClicked(); };

            // Filter: All / Connected / Offline Dropdown
            _trustedFilterDropdown = new AppDropdown();
            _trustedFilterDropdown.Width = DpiManager.ScaleInt(155);
            _trustedFilterDropdown.Height = DpiManager.ScaleInt(34);
            _trustedFilterDropdown.Items.Add(Loc.Get("FilterAll"));
            _trustedFilterDropdown.Items.Add(Loc.Get("FilterConnected"));
            _trustedFilterDropdown.Items.Add(Loc.Get("FilterOffline"));
            _trustedFilterDropdown.SelectedIndex = 0;
            _trustedFilterDropdown.SelectedIndexChanged += delegate { ApplyTrustedFilters(); };

            // Search Box Component
            _trustedSearchBox = new AppSearchBox();
            _trustedSearchBox.Size = new Size(DpiManager.ScaleInt(180), DpiManager.ScaleInt(34));
            _trustedSearchBox.Placeholder = Loc.Get("SearchTrustedPlaceholder");
            _trustedSearchBox.QueryChanged += delegate { ApplyTrustedFilters(); };

            MethodInvoker layoutTrustedHeader = delegate
            {
                bool rtl = Loc.IsRtl;
                int p = DpiManager.ScaleInt(20);
                int gap = DpiManager.ScaleInt(8);
                int hw = _trustedHeaderPanel.ClientSize.Width;

                _btnAddTrustedDevice.Width = AppButton.CalculateWidth(_btnAddTrustedDevice.Text, ButtonSize.Medium, rtl);

                int titleW = Math.Max(_trustedPageTitle.Width, _trustedPageSub.Width);
                int fixedControlsW = _btnRefreshTrusted.Width + gap + _btnAddTrustedDevice.Width + gap + _trustedFilterDropdown.Width + gap;
                int availSearchW = hw - (p * 2) - titleW - fixedControlsW - gap;

                // Responsive wrapping: 2-row when header width is under 780px or search cannot comfortably fit
                bool isNarrow = hw < DpiManager.ScaleInt(780) || availSearchW < DpiManager.ScaleInt(140);
                int desiredH = isNarrow ? DpiManager.ScaleInt(108) : DpiManager.ScaleInt(74);
                if (_trustedHeaderPanel.Height != desiredH)
                {
                    _trustedHeaderPanel.Height = desiredH;
                }

                if (isNarrow)
                {
                    // 2-row layout: Row 1 has Title & Sub, Row 2 has Actions & Search
                    if (rtl)
                    {
                        _trustedPageTitle.Location = new Point(hw - p - _trustedPageTitle.Width, DpiManager.ScaleInt(8));
                        _trustedPageSub.Location = new Point(hw - p - _trustedPageSub.Width, DpiManager.ScaleInt(34));

                        int leftX = p;
                        _btnRefreshTrusted.Location = new Point(leftX, DpiManager.ScaleInt(66));
                        leftX += _btnRefreshTrusted.Width + gap;

                        _btnAddTrustedDevice.Location = new Point(leftX, DpiManager.ScaleInt(66));
                        leftX += _btnAddTrustedDevice.Width + gap;

                        int rightX = hw - p;
                        rightX -= _trustedFilterDropdown.Width;
                        _trustedFilterDropdown.Location = new Point(rightX, DpiManager.ScaleInt(66));
                        rightX -= gap;

                        int row2AvailW = rightX - leftX - gap;
                        int searchW = Math.Max(DpiManager.ScaleInt(110), Math.Min(DpiManager.ScaleInt(200), row2AvailW));
                        if (searchW > row2AvailW && row2AvailW > DpiManager.ScaleInt(60)) searchW = row2AvailW;
                        _trustedSearchBox.Width = searchW;
                        _trustedSearchBox.Location = new Point(rightX - searchW, DpiManager.ScaleInt(66));
                    }
                    else
                    {
                        _trustedPageTitle.Location = new Point(p, DpiManager.ScaleInt(8));
                        _trustedPageSub.Location = new Point(p, DpiManager.ScaleInt(34));

                        int curX = hw - p;
                        curX -= _btnRefreshTrusted.Width;
                        _btnRefreshTrusted.Location = new Point(curX, DpiManager.ScaleInt(66));
                        curX -= gap;

                        curX -= _btnAddTrustedDevice.Width;
                        _btnAddTrustedDevice.Location = new Point(curX, DpiManager.ScaleInt(66));
                        curX -= gap;

                        curX -= _trustedFilterDropdown.Width;
                        _trustedFilterDropdown.Location = new Point(curX, DpiManager.ScaleInt(66));
                        curX -= gap;

                        int row2AvailW = curX - p - gap;
                        int searchW = Math.Max(DpiManager.ScaleInt(110), Math.Min(DpiManager.ScaleInt(200), row2AvailW));
                        if (searchW > row2AvailW && row2AvailW > DpiManager.ScaleInt(60)) searchW = row2AvailW;
                        _trustedSearchBox.Width = searchW;
                        _trustedSearchBox.Location = new Point(curX - searchW, DpiManager.ScaleInt(66));
                    }
                }
                else
                {
                    // Single row layout
                    if (rtl)
                    {
                        _trustedPageTitle.Location = new Point(hw - p - _trustedPageTitle.Width, DpiManager.ScaleInt(12));
                        _trustedPageSub.Location = new Point(hw - p - _trustedPageSub.Width, DpiManager.ScaleInt(40));

                        int leftX = p;
                        _btnRefreshTrusted.Location = new Point(leftX, DpiManager.ScaleInt(20));
                        leftX += _btnRefreshTrusted.Width + gap;

                        _btnAddTrustedDevice.Location = new Point(leftX, DpiManager.ScaleInt(20));
                        leftX += _btnAddTrustedDevice.Width + gap;

                        int rightX = hw - p - titleW - gap;
                        rightX -= _trustedFilterDropdown.Width;
                        _trustedFilterDropdown.Location = new Point(rightX, DpiManager.ScaleInt(20));
                        rightX -= gap;

                        int availW = rightX - leftX - gap;
                        int searchW = Math.Max(DpiManager.ScaleInt(110), Math.Min(DpiManager.ScaleInt(240), availW));
                        if (searchW > availW && availW > DpiManager.ScaleInt(60)) searchW = availW;
                        _trustedSearchBox.Width = searchW;
                        _trustedSearchBox.Location = new Point(rightX - searchW, DpiManager.ScaleInt(20));
                    }
                    else
                    {
                        _trustedPageTitle.Location = new Point(p, DpiManager.ScaleInt(12));
                        _trustedPageSub.Location = new Point(p, DpiManager.ScaleInt(40));

                        int curX = hw - p;
                        curX -= _btnRefreshTrusted.Width;
                        _btnRefreshTrusted.Location = new Point(curX, DpiManager.ScaleInt(20));
                        curX -= gap;

                        curX -= _btnAddTrustedDevice.Width;
                        _btnAddTrustedDevice.Location = new Point(curX, DpiManager.ScaleInt(20));
                        curX -= gap;

                        curX -= _trustedFilterDropdown.Width;
                        _trustedFilterDropdown.Location = new Point(curX, DpiManager.ScaleInt(20));
                        curX -= gap;

                        int availW = curX - (p + titleW + gap);
                        int searchW = Math.Max(DpiManager.ScaleInt(110), Math.Min(DpiManager.ScaleInt(240), availW));
                        if (searchW > availW && availW > DpiManager.ScaleInt(60)) searchW = availW;
                        _trustedSearchBox.Width = searchW;
                        _trustedSearchBox.Location = new Point(curX - searchW, DpiManager.ScaleInt(20));
                    }
                }
            };

            _trustedHeaderPanel.Resize += delegate { layoutTrustedHeader(); };
            _trustedHeaderPanel.Controls.Add(_trustedPageTitle);
            _trustedHeaderPanel.Controls.Add(_trustedPageSub);
            _trustedHeaderPanel.Controls.Add(_btnRefreshTrusted);
            _trustedHeaderPanel.Controls.Add(_btnAddTrustedDevice);
            _trustedHeaderPanel.Controls.Add(_trustedFilterDropdown);
            _trustedHeaderPanel.Controls.Add(_trustedSearchBox);
            layoutTrustedHeader();

            _tabWhitelist.Controls.Add(_trustedHeaderPanel);

            // ── 2. SUMMARY STRIP (COMPACT METRICS) ──────────────────────────
            _trustedSummaryStrip = new Panel();
            _trustedSummaryStrip.Dock = DockStyle.Top;
            _trustedSummaryStrip.Height = DpiManager.ScaleInt(76);
            _trustedSummaryStrip.BackColor = pal.BackgroundPrimary;
            _trustedSummaryStrip.Padding = new Padding(DpiManager.ScaleInt(20), 0, DpiManager.ScaleInt(20), DpiManager.ScaleInt(8));

            _metricTotalTrusted = new SummaryMetricCard();
            _metricTotalTrusted.RightToLeft = Loc.RtlValue;
            _metricTotalTrusted.TitleText = Loc.Get("MetricTrustedDevices");
            _metricTotalTrusted.SubtitleText = Loc.IsRtl ? "هويات عتاد مسجلة" : "Hardware identities enrolled";
            _metricTotalTrusted.BadgeText = Loc.IsRtl ? "مسجلة" : "ENROLLED";
            _metricTotalTrusted.BadgeVariant = StatusBadgeVariant.Success;
            _metricTotalTrusted.ValueText = "0";
            _metricTotalTrusted.Click += delegate
            {
                if (_trustedFilterDropdown != null) _trustedFilterDropdown.SelectedIndex = 0;
            };

            _metricConnectedTrusted = new SummaryMetricCard();
            _metricConnectedTrusted.RightToLeft = Loc.RtlValue;
            _metricConnectedTrusted.TitleText = Loc.Get("MetricCurrentlyConnected");
            _metricConnectedTrusted.SubtitleText = Loc.IsRtl ? "أجهزة نشطة على المنافذ" : "Active on physical ports";
            _metricConnectedTrusted.BadgeText = Loc.IsRtl ? "نشط" : "LIVE";
            _metricConnectedTrusted.BadgeVariant = StatusBadgeVariant.Info;
            _metricConnectedTrusted.ValueText = "0";
            _metricConnectedTrusted.Click += delegate
            {
                if (_trustedFilterDropdown != null) _trustedFilterDropdown.SelectedIndex = 1;
            };

            _trustedSummaryStrip.Controls.Add(_metricTotalTrusted);
            _trustedSummaryStrip.Controls.Add(_metricConnectedTrusted);

            MethodInvoker layoutSummaryStrip = delegate
            {
                bool rtl = Loc.IsRtl;
                int p = DpiManager.ScaleInt(20);
                int gap = DpiManager.ScaleInt(12);
                int stripW = _trustedSummaryStrip.ClientSize.Width;
                int cardH = DpiManager.ScaleInt(66);
                int cardW = Math.Min(DpiManager.ScaleInt(340), (stripW - (p * 2) - gap) / 2);
                if (cardW < DpiManager.ScaleInt(160)) cardW = (stripW - (p * 2) - gap) / 2;

                _metricTotalTrusted.Size = new Size(cardW, cardH);
                _metricConnectedTrusted.Size = new Size(cardW, cardH);

                if (rtl)
                {
                    _metricTotalTrusted.Location = new Point(stripW - p - cardW, 0);
                    _metricConnectedTrusted.Location = new Point(stripW - p - cardW - gap - cardW, 0);
                }
                else
                {
                    _metricTotalTrusted.Location = new Point(p, 0);
                    _metricConnectedTrusted.Location = new Point(p + cardW + gap, 0);
                }
            };
            _trustedSummaryStrip.Resize += delegate { layoutSummaryStrip(); };
            layoutSummaryStrip();

            _tabWhitelist.Controls.Add(_trustedSummaryStrip);

            // ── 3. BODY PANEL: CONTAINER FOR SCROLL LIST & SLIDE-IN DRAWER ───
            _trustedBodyPanel = new Panel();
            _trustedBodyPanel.Dock = DockStyle.Fill;
            _trustedBodyPanel.BackColor = pal.BackgroundPrimary;
            _trustedBodyPanel.RightToLeft = Loc.RtlValue;
            _trustedBodyPanel.Resize += delegate
            {
                if (_trustedDrawer != null)
                {
                    _trustedDrawer.UpdateResponsiveWidth(_trustedBodyPanel.ClientSize.Width);
                }
            };

            // Slide-In Details Drawer (Docks Right in LTR, Left in RTL)
            _trustedDrawer = new TrustedDeviceDetailsDrawer();
            _trustedDrawer.Dock = isRtl ? DockStyle.Left : DockStyle.Right;
            _trustedDrawer.Width = DpiManager.ScaleInt(367);
            _trustedDrawer.Visible = false;
            _trustedDrawer.RightToLeft = Loc.RtlValue;
            _trustedDrawer.DrawerClosed += delegate
            {
                _selectedTrustedRule = null;
                foreach (var r in _activeTrustedRows) r.IsSelected = false;
                if (_trustedBodyPanel != null) _trustedBodyPanel.PerformLayout();
                ResizeTrustedRows();
            };
            _trustedDrawer.RevokeRequested += delegate
            {
                if (_trustedDrawer.CurrentRule != null)
                {
                    OnRevokeRuleRequested(_trustedDrawer.CurrentRule);
                }
            };
            _trustedDrawer.ViewInConnectedRequested += delegate
            {
                if (_trustedDrawer.CorrelatedDevice != null)
                {
                    NavigateToConnectedDevice(_trustedDrawer.CorrelatedDevice);
                }
            };

            // Custom Scroll Panel for Trusted Device Rows
            _trustedScroll = new DarkScrollPanel();
            _trustedScroll.Dock = DockStyle.Fill;
            _trustedScroll.RightToLeft = Loc.RtlValue;
            _trustedScroll.ContentPanel.Resize += delegate
            {
                ResizeTrustedRows();
            };

            // Empty State Control
            _trustedEmptyState = new TrustedEmptyState();
            _trustedEmptyState.Visible = false;
            _trustedEmptyState.GoToDevicesClicked += delegate
            {
                if (_sidebar != null) _sidebar.SelectedIndex = 1;
            };
            _trustedEmptyState.ClearFilterClicked += delegate
            {
                if (_trustedSearchBox != null) _trustedSearchBox.Text = "";
                if (_trustedFilterDropdown != null) _trustedFilterDropdown.SelectedIndex = 0;
            };

            _trustedBodyPanel.Controls.Add(_trustedScroll);
            _trustedBodyPanel.Controls.Add(_trustedDrawer);
            _trustedDrawer.SendToBack();
            _trustedScroll.BringToFront();

            _tabWhitelist.Controls.Add(_trustedBodyPanel);
            _trustedSummaryStrip.SendToBack();
            _trustedHeaderPanel.SendToBack();
            _trustedBodyPanel.BringToFront();
        }

        private void ResizeTrustedRows()
        {
            if (_trustedScroll == null || _trustedScroll.ContentPanel == null) return;
            int pad = DpiManager.ScaleInt(20);
            int newW = Math.Max(DpiManager.ScaleInt(300), _trustedScroll.ContentPanel.ClientSize.Width - (pad * 2));
            foreach (Control c in _trustedScroll.ContentPanel.Controls)
            {
                if (c is TrustedDeviceRow || c is TrustedEmptyState)
                {
                    if (c.Width != newW) c.Width = newW;
                }
            }
        }

        private void RefreshWhitelistView()
        {
            if (_trustedScroll == null) return;

            List<DeviceRule> rules = TestIsolatedWhitelistRules != null
                ? TestIsolatedWhitelistRules
                : TrustedDeviceRepository.LoadRules();

            _cachedRules = rules ?? new List<DeviceRule>();

            // Calculate connected correlation count
            var devList = TestIsolatedDevices ?? _cachedDiscoveredDevices;
            int connectedCount = 0;
            if (_cachedRules != null && devList != null)
            {
                foreach (var r in _cachedRules)
                {
                    bool isConn = false;
                    foreach (var d in devList)
                    {
                        if (r.Matches(d)) { isConn = true; break; }
                    }
                    if (isConn) connectedCount++;
                }
            }

            int totalRules = _cachedRules != null ? _cachedRules.Count : 0;
            if (_metricTotalTrusted != null)
                _metricTotalTrusted.ValueText = totalRules.ToString();
            if (_metricConnectedTrusted != null)
                _metricConnectedTrusted.ValueText = connectedCount.ToString();
            if (_metricTrustedDevices != null)
                _metricTrustedDevices.ValueText = totalRules.ToString();

            ApplyTrustedFilters();
        }

        private void ApplyTrustedFilters()
        {
            if (_trustedScroll == null || _trustedScroll.ContentPanel == null) return;

            _trustedScroll.ContentPanel.SuspendLayout();
            UiDrawingUtils.ClearAndDisposeControls(_trustedScroll.ContentPanel.Controls, _trustedEmptyState);
            _activeTrustedRows.Clear();

            int filterIdx = _trustedFilterDropdown != null ? _trustedFilterDropdown.SelectedIndex : 0;
            string query = _trustedSearchBox != null ? _trustedSearchBox.Text.Trim() : "";

            var devList = TestIsolatedDevices ?? _cachedDiscoveredDevices;

            // Zero enrolled rules overall
            if (_cachedRules == null || _cachedRules.Count == 0)
            {
                _trustedEmptyState.SetSearchMode(false, "");
                int pad = DpiManager.ScaleInt(20);
                int clientW = _trustedScroll.ContentPanel.ClientSize.Width;
                if (clientW < 300) clientW = _tabWhitelist.ClientSize.Width;
                int cardW = Math.Max(DpiManager.ScaleInt(300), clientW - (pad * 2));
                _trustedEmptyState.Size = new Size(cardW, DpiManager.ScaleInt(240));
                _trustedEmptyState.Location = new Point(pad, DpiManager.ScaleInt(40));
                _trustedScroll.ContentPanel.Controls.Add(_trustedEmptyState);
                _trustedEmptyState.Visible = true;
                _trustedEmptyState.BringToFront();

                _trustedScroll.ContentPanel.ResumeLayout(true);
                _trustedScroll.UpdateScroll();
                return;
            }

            // Filter rules
            List<Tuple<DeviceRule, DeviceIdentity>> matched = new List<Tuple<DeviceRule, DeviceIdentity>>();
            foreach (var r in _cachedRules)
            {
                DeviceIdentity correlated = null;
                if (devList != null)
                {
                    foreach (var d in devList)
                    {
                        if (r.Matches(d)) { correlated = d; break; }
                    }
                }
                bool isConn = (correlated != null);

                // Filter dropdown: 0: All, 1: Connected, 2: Offline
                if (filterIdx == 1 && !isConn) continue;
                if (filterIdx == 2 && isConn) continue;

                // Search query match
                if (!string.IsNullOrEmpty(query))
                {
                    bool matchName = !string.IsNullOrEmpty(r.FriendlyName) && r.FriendlyName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    bool matchHw = !string.IsNullOrEmpty(r.TargetHardwareId) && r.TargetHardwareId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    bool matchInst = !string.IsNullOrEmpty(r.TargetInstanceId) && r.TargetInstanceId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    bool matchSer = !string.IsNullOrEmpty(r.TargetSerial) && r.TargetSerial.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!matchName && !matchHw && !matchInst && !matchSer) continue;
                }

                matched.Add(Tuple.Create(r, correlated));
            }

            if (matched.Count == 0)
            {
                // In-memory filter/search returned zero results
                _trustedEmptyState.SetSearchMode(true, query);
                int pad = DpiManager.ScaleInt(20);
                int clientW = _trustedScroll.ContentPanel.ClientSize.Width;
                if (clientW < 300) clientW = _tabWhitelist.ClientSize.Width;
                int cardW = Math.Max(DpiManager.ScaleInt(300), clientW - (pad * 2));
                _trustedEmptyState.Size = new Size(cardW, DpiManager.ScaleInt(240));
                _trustedEmptyState.Location = new Point(pad, DpiManager.ScaleInt(40));
                _trustedScroll.ContentPanel.Controls.Add(_trustedEmptyState);
                _trustedEmptyState.Visible = true;
                _trustedEmptyState.BringToFront();
            }
            else
            {
                _trustedEmptyState.Visible = false;
                int pad = DpiManager.ScaleInt(20);
                int curY = DpiManager.ScaleInt(10);
                int clientW = _trustedScroll.ContentPanel.ClientSize.Width;
                if (clientW < 300) clientW = _tabWhitelist.ClientSize.Width;
                int cardW = Math.Max(DpiManager.ScaleInt(300), clientW - (pad * 2));

                foreach (var pair in matched)
                {
                    DeviceRule rule = pair.Item1;
                    DeviceIdentity dev = pair.Item2;

                    TrustedDeviceRow row = new TrustedDeviceRow(rule, dev);
                    row.Size = new Size(cardW, row.Height);
                    row.Location = new Point(pad, curY);

                    // Re-select if this was the previously selected rule
                    if (_selectedTrustedRule != null && _selectedTrustedRule.RuleId == rule.RuleId)
                    {
                        row.IsSelected = true;
                    }

                    row.RowSelected += delegate(object s, EventArgs e)
                    {
                        OnTrustedRowSelected(s as TrustedDeviceRow);
                    };

                    row.DetailsRequested += delegate(object s, EventArgs e)
                    {
                        TrustedDeviceRow tr = s as TrustedDeviceRow;
                        if (tr != null)
                        {
                            OnTrustedRowSelected(tr);
                        }
                    };

                    row.RevokeRequested += delegate(object s, EventArgs e)
                    {
                        TrustedDeviceRow tr = s as TrustedDeviceRow;
                        if (tr != null && tr.Rule != null)
                        {
                            OnRevokeRuleRequested(tr.Rule);
                        }
                    };

                    row.ViewInConnectedRequested += delegate(object s, EventArgs e)
                    {
                        TrustedDeviceRow tr = s as TrustedDeviceRow;
                        if (tr != null && tr.CorrelatedDevice != null)
                        {
                            NavigateToConnectedDevice(tr.CorrelatedDevice);
                        }
                    };

                    _activeTrustedRows.Add(row);
                    _trustedScroll.ContentPanel.Controls.Add(row);
                    curY += row.Height + DpiManager.ScaleInt(6);
                }
            }

            _trustedScroll.ContentPanel.ResumeLayout(true);
            _trustedScroll.UpdateScroll();
        }

        private void OnTrustedRowSelected(TrustedDeviceRow row)
        {
            if (row == null || row.Rule == null) return;

            foreach (var r in _activeTrustedRows)
            {
                r.IsSelected = (r == row);
            }

            _selectedTrustedRule = row.Rule;

            if (_trustedDrawer != null)
            {
                _trustedDrawer.OpenDrawer(row.Rule, row.CorrelatedDevice);
                if (_trustedBodyPanel != null) _trustedBodyPanel.PerformLayout();
                ResizeTrustedRows();
            }
        }

        private void OnRevokeRuleRequested(DeviceRule rule)
        {
            if (rule == null) return;

            string devName = !string.IsNullOrEmpty(rule.FriendlyName) ? rule.FriendlyName : "USB Device";
            if (MsgBox.Show(this, string.Format(Loc.Get("ConfirmRevoke"), devName), Loc.Get("AppTitle"), MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;

            if (!RequireAuth(Loc.Get("ActionRevokeTrust"))) return;

            string err;
            bool ok = TrustedDeviceRepository.RemoveTrustedDevice(rule.RuleId, out err);
            if (ok)
            {
                LogActivity(string.Format(Loc.Get("EventDeviceRevoked"), devName), ActivityCategory.DeviceEvents, Loc.Get("BadgeVerified"), Theme.AccentGreen);
                if (_trustedDrawer != null) _trustedDrawer.CloseDrawer();
                RefreshWhitelistView();
                RefreshDevicesList();
            }
            else
            {
                MsgBox.Show(this, Loc.Get("ErrorGeneric", err), Loc.Get("ErrorTitle"));
            }
        }

        private void NavigateToConnectedDevice(DeviceIdentity dev)
        {
            if (dev == null) return;
            if (_sidebar != null)
            {
                _sidebar.SelectedIndex = 1; // Connected Devices
            }
            if (_activeDeviceRows != null)
            {
                foreach (var r in _activeDeviceRows)
                {
                    if (r.Device != null && (r.Device == dev || r.Device.DeviceInstanceId == dev.DeviceInstanceId))
                    {
                        OnDeviceRowSelected(r);
                        break;
                    }
                }
            }
        }

        public static bool IsSystemDevice(DeviceIdentity dev)
        {
            if (dev == null) return false;
            return dev.IsSystemDisk ||
                   dev.SafetyResult == SafetyValidationResult.RejectedSystemOrBootDisk ||
                   dev.Category == DeviceCategory.InternalFixedDisk ||
                   dev.Category == DeviceCategory.CriticalSystemHardware;
        }

        private void OnAddConnectedDeviceClicked()
        {
            var devList = TestIsolatedDevices ?? _cachedDiscoveredDevices;
            if (devList == null) devList = new List<DeviceIdentity>();

            List<DeviceRule> rules = TestIsolatedWhitelistRules ?? TrustedDeviceRepository.LoadRules();

            // Eligible untrusted devices: not internal system disk, and not currently trusted
            List<DeviceIdentity> eligible = new List<DeviceIdentity>();
            foreach (var dev in devList)
            {
                if (IsSystemDevice(dev)) continue;
                bool isTrusted = false;
                if (rules != null)
                {
                    foreach (var r in rules)
                    {
                        if (r.Matches(dev)) { isTrusted = true; break; }
                    }
                }
                if (!isTrusted) eligible.Add(dev);
            }

            if (eligible.Count == 0)
            {
                if (_sidebar != null) _sidebar.SelectedIndex = 1;
                return;
            }

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.RightToLeft = Loc.IsRtl ? RightToLeft.Yes : RightToLeft.No;
            menu.ShowImageMargin = false;
            menu.Renderer = new AppMenuRenderer();

            foreach (var dev in eligible)
            {
                DeviceIdentity d = dev;
                string label = d.GetCleanDisplayName();
                var item = new ToolStripMenuItem(label);
                item.Click += delegate
                {
                    OnRowTrustRequested(d, false);
                };
                menu.Items.Add(item);
            }

            menu.Items.Add(new ToolStripSeparator());
            var navItem = new ToolStripMenuItem(Loc.Get("GoToDevicesBtn"));
            navItem.Click += delegate
            {
                if (_sidebar != null) _sidebar.SelectedIndex = 1;
            };
            menu.Items.Add(navItem);

            Point pt = new Point(0, _btnAddTrustedDevice.Height);
            menu.Show(_btnAddTrustedDevice, pt);
        }

        // ═══════════════════════════════════════════════════════════════════
        // PAGE 4: ACTIVITY SENTINEL
        // ═══════════════════════════════════════════════════════════════════
        // ═══════════════════════════════════════════════════════════════════
        // PAGE 4: ACTIVITY & AUDIT WORKSPACE (v2.1 Phase 4.5 Redesign)
        // ═══════════════════════════════════════════════════════════════════
        private void BuildTabActivity()
        {
            _tabActivity = new Panel();
            _tabActivity.Dock = DockStyle.Fill;
            _tabActivity.BackColor = ThemeManager.ActivePalette.BackgroundPrimary;
            _tabActivity.RightToLeft = Loc.RtlValue;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = Loc.IsRtl;

            // ── 1. HEADER PANEL ──────────────────────────────────────────────
            _activityHeaderPanel = new Panel();
            _activityHeaderPanel.Dock = DockStyle.Top;
            _activityHeaderPanel.Height = DpiManager.ScaleInt(70);
            _activityHeaderPanel.BackColor = pal.BackgroundPrimary;

            _activityPageTitle = new Label();
            _activityPageTitle.Text = Loc.Get("ActivityPageTitle");
            _activityPageTitle.Font = DpiManager.GetFont(TypographyRole.Title, isRtl);
            _activityPageTitle.ForeColor = pal.TextPrimary;
            _activityPageTitle.AutoSize = true;

            _activityPageSub = new Label();
            _activityPageSub.Text = Loc.Get("ActivityPageSubtitle");
            _activityPageSub.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            _activityPageSub.ForeColor = pal.TextSecondary;
            _activityPageSub.AutoSize = true;

            _btnClearActivity = new AppButton();
            _btnClearActivity.Text = Loc.Get("ClearActivityBtn");
            _btnClearActivity.Variant = ButtonVariant.DestructiveSecondary;
            _btnClearActivity.SizeVariant = ButtonSize.Small;
            _btnClearActivity.Width = AppButton.CalculateWidth(_btnClearActivity.Text, ButtonSize.Small, isRtl);
            _btnClearActivity.Click += delegate
            {
                if (MsgBox.Show(this, Loc.Get("ClearActivityConfirmMsg"), Loc.Get("ClearActivityConfirmTitle"), MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    _activityRecords.Clear();
                    if (TestIsolatedActivityRecords != null) TestIsolatedActivityRecords.Clear();
                    _selectedActivityRecord = null;
                    if (_activityDrawer != null) _activityDrawer.CloseDrawer();
                    RefreshActivityView();
                }
            };

            MethodInvoker layoutActHeader = delegate
            {
                bool rtl = Loc.IsRtl;
                int p = DpiManager.ScaleInt(20);
                int hw = _activityHeaderPanel.ClientSize.Width;

                _btnClearActivity.Width = AppButton.CalculateWidth(_btnClearActivity.Text, ButtonSize.Small, rtl);
                _btnClearActivity.Height = DpiManager.ScaleInt(28);
                int btnY = (_activityHeaderPanel.Height - _btnClearActivity.Height) / 2;

                if (rtl)
                {
                    _activityPageTitle.Location = new Point(hw - p - _activityPageTitle.Width, DpiManager.ScaleInt(12));
                    _activityPageSub.Location = new Point(hw - p - _activityPageSub.Width, DpiManager.ScaleInt(40));
                    _btnClearActivity.Location = new Point(p, btnY);
                }
                else
                {
                    _activityPageTitle.Location = new Point(p, DpiManager.ScaleInt(12));
                    _activityPageSub.Location = new Point(p, DpiManager.ScaleInt(40));
                    _btnClearActivity.Location = new Point(hw - p - _btnClearActivity.Width, btnY);
                }
            };
            _activityHeaderPanel.Resize += delegate { layoutActHeader(); };
            _activityHeaderPanel.Controls.Add(_activityPageTitle);
            _activityHeaderPanel.Controls.Add(_activityPageSub);
            _activityHeaderPanel.Controls.Add(_btnClearActivity);
            layoutActHeader();

            _tabActivity.Controls.Add(_activityHeaderPanel);

            // ── 2. FILTER TOOLBAR PANEL ───────────────────────────────────────
            _activityToolbarPanel = new Panel();
            _activityToolbarPanel.Dock = DockStyle.Top;
            _activityToolbarPanel.Height = DpiManager.ScaleInt(46);
            _activityToolbarPanel.BackColor = pal.BackgroundPrimary;

            // Search Box Component
            _activitySearchBox = new AppSearchBox();
            _activitySearchBox.Size = new Size(DpiManager.ScaleInt(200), DpiManager.ScaleInt(34));
            _activitySearchBox.Placeholder = Loc.Get("SearchActivityPlaceholder");
            _activitySearchBox.QueryChanged += delegate { RefreshActivityView(); };

            // Category Dropdown
            _activityCatDropdown = new AppDropdown();
            _activityCatDropdown.Width = DpiManager.ScaleInt(160);
            _activityCatDropdown.Height = DpiManager.ScaleInt(34);
            _activityCatDropdown.Items.Add(Loc.Get("ActCatAll"));
            _activityCatDropdown.Items.Add(Loc.Get("ActCatDevices"));
            _activityCatDropdown.Items.Add(Loc.Get("ActCatProtection"));
            _activityCatDropdown.Items.Add(Loc.Get("ActCatSecurity"));
            _activityCatDropdown.Items.Add(Loc.Get("ActCatErrors"));
            _activityCatDropdown.SelectedIndex = 0;
            _activityCatDropdown.SelectedIndexChanged += delegate { RefreshActivityView(); };

            // Date Range Dropdown
            _activityDateDropdown = new AppDropdown();
            _activityDateDropdown.Width = DpiManager.ScaleInt(150);
            _activityDateDropdown.Height = DpiManager.ScaleInt(34);
            _activityDateDropdown.Items.Add(Loc.Get("DateAll"));
            _activityDateDropdown.Items.Add(Loc.Get("DateToday"));
            _activityDateDropdown.Items.Add(Loc.Get("DateLast7Days"));
            _activityDateDropdown.Items.Add(Loc.Get("DateLast30Days"));
            _activityDateDropdown.Items.Add(Loc.Get("DateCustom"));
            _activityDateDropdown.SelectedIndex = 0;
            _activityDateDropdown.SelectedIndexChanged += delegate
            {
                if (_customDatePanel != null)
                {
                    bool isCustom = (_activityDateDropdown.SelectedIndex == 4);
                    _customDatePanel.Visible = isCustom;
                    _activityToolbarPanel.Height = isCustom ? DpiManager.ScaleInt(90) : DpiManager.ScaleInt(46);
                }
                RefreshActivityView();
            };

            // Custom Date Panel
            _customDatePanel = new Panel();
            _customDatePanel.Dock = DockStyle.Bottom;
            _customDatePanel.Height = DpiManager.ScaleInt(40);
            _customDatePanel.BackColor = pal.Surface;
            _customDatePanel.Visible = false;

            Label fromLbl = new Label();
            fromLbl.Text = Loc.Get("DateFrom") + ":";
            fromLbl.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            fromLbl.ForeColor = pal.TextSecondary;
            fromLbl.AutoSize = true;

            _dateFromPicker = new DateTimePicker();
            _dateFromPicker.Format = DateTimePickerFormat.Short;
            _dateFromPicker.Value = DateTime.Today.AddDays(-7);
            _dateFromPicker.Size = new Size(DpiManager.ScaleInt(110), DpiManager.ScaleInt(24));

            Label toLbl = new Label();
            toLbl.Text = Loc.Get("DateTo") + ":";
            toLbl.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            toLbl.ForeColor = pal.TextSecondary;
            toLbl.AutoSize = true;

            _dateToPicker = new DateTimePicker();
            _dateToPicker.Format = DateTimePickerFormat.Short;
            _dateToPicker.Value = DateTime.Today;
            _dateToPicker.Size = new Size(DpiManager.ScaleInt(110), DpiManager.ScaleInt(24));

            _btnApplyDateFilter = new AppButton();
            _btnApplyDateFilter.SizeVariant = ButtonSize.Small;
            _btnApplyDateFilter.Variant = ButtonVariant.Secondary;
            _btnApplyDateFilter.Text = Loc.Get("ApplyFilter");
            _btnApplyDateFilter.Click += delegate { RefreshActivityView(); };

            MethodInvoker layoutCustomDate = delegate
            {
                bool rtl = Loc.IsRtl;
                int p = DpiManager.ScaleInt(20);
                int cy = (_customDatePanel.Height - _dateFromPicker.Height) / 2;

                if (rtl)
                {
                    int x = _customDatePanel.ClientSize.Width - p;
                    fromLbl.Location = new Point(x - fromLbl.Width, cy + 3);
                    x -= (fromLbl.Width + 6);
                    _dateFromPicker.Location = new Point(x - _dateFromPicker.Width, cy);
                    x -= (_dateFromPicker.Width + 16);
                    toLbl.Location = new Point(x - toLbl.Width, cy + 3);
                    x -= (toLbl.Width + 6);
                    _dateToPicker.Location = new Point(x - _dateToPicker.Width, cy);
                    x -= (_dateToPicker.Width + 16);
                    _btnApplyDateFilter.Location = new Point(x - _btnApplyDateFilter.Width, cy - 1);
                }
                else
                {
                    int x = p;
                    fromLbl.Location = new Point(x, cy + 3);
                    x += fromLbl.Width + 6;
                    _dateFromPicker.Location = new Point(x, cy);
                    x += _dateFromPicker.Width + 16;
                    toLbl.Location = new Point(x, cy + 3);
                    x += toLbl.Width + 6;
                    _dateToPicker.Location = new Point(x, cy);
                    x += _dateToPicker.Width + 16;
                    _btnApplyDateFilter.Location = new Point(x, cy - 1);
                }
            };
            _customDatePanel.Resize += delegate { layoutCustomDate(); };
            _customDatePanel.Controls.Add(fromLbl);
            _customDatePanel.Controls.Add(_dateFromPicker);
            _customDatePanel.Controls.Add(toLbl);
            _customDatePanel.Controls.Add(_dateToPicker);
            _customDatePanel.Controls.Add(_btnApplyDateFilter);
            layoutCustomDate();

            MethodInvoker layoutActToolbar = delegate
            {
                bool rtl = Loc.IsRtl;
                int p = DpiManager.ScaleInt(20);
                int gap = DpiManager.ScaleInt(8);
                int cy = DpiManager.ScaleInt(6);

                if (rtl)
                {
                    int curX = _activityToolbarPanel.ClientSize.Width - p;
                    _activitySearchBox.Location = new Point(curX - _activitySearchBox.Width, cy);
                    curX -= (_activitySearchBox.Width + gap);

                    _activityCatDropdown.Location = new Point(curX - _activityCatDropdown.Width, cy);
                    curX -= (_activityCatDropdown.Width + gap);

                    _activityDateDropdown.Location = new Point(curX - _activityDateDropdown.Width, cy);
                }
                else
                {
                    int curX = p;
                    _activitySearchBox.Location = new Point(curX, cy);
                    curX += _activitySearchBox.Width + gap;

                    _activityCatDropdown.Location = new Point(curX, cy);
                    curX += _activityCatDropdown.Width + gap;

                    _activityDateDropdown.Location = new Point(curX, cy);
                }
            };
            _activityToolbarPanel.Resize += delegate { layoutActToolbar(); };
            _activityToolbarPanel.Controls.Add(_activitySearchBox);
            _activityToolbarPanel.Controls.Add(_activityCatDropdown);
            _activityToolbarPanel.Controls.Add(_activityDateDropdown);
            _activityToolbarPanel.Controls.Add(_customDatePanel);
            layoutActToolbar();

            _tabActivity.Controls.Add(_activityToolbarPanel);

            // ── 3. BODY PANEL: CONTAINER FOR TIMELINE SCROLL & SLIDE-IN DRAWER ──
            _activityBodyPanel = new Panel();
            _activityBodyPanel.Dock = DockStyle.Fill;
            _activityBodyPanel.BackColor = pal.BackgroundPrimary;
            _activityBodyPanel.RightToLeft = Loc.RtlValue;
            _activityBodyPanel.Resize += delegate
            {
                if (_activityDrawer != null)
                {
                    _activityDrawer.UpdateResponsiveWidth(_activityBodyPanel.ClientSize.Width);
                }
            };

            // Non-modal Slide-In Details Drawer (Docks Right in LTR, Left in RTL)
            _activityDrawer = new ActivityDetailsDrawer();
            _activityDrawer.Dock = isRtl ? DockStyle.Left : DockStyle.Right;
            _activityDrawer.Width = DpiManager.ScaleInt(367);
            _activityDrawer.Visible = false;
            _activityDrawer.RightToLeft = Loc.RtlValue;
            _activityDrawer.DrawerClosed += delegate
            {
                _selectedActivityRecord = null;
                foreach (var r in _activeActivityRows) r.IsSelected = false;
            };

            // Custom Scroll Panel for Activity Rows
            _activityScroll = new DarkScrollPanel();
            _activityScroll.Dock = DockStyle.Fill;
            _activityScroll.RightToLeft = Loc.RtlValue;
            _activityScroll.ContentPanel.Resize += delegate
            {
                ResizeActivityRows();
            };

            // Dedicated Empty State Control
            _activityEmptyState = new ActivityEmptyState();
            _activityEmptyState.Visible = false;
            _activityEmptyState.ClearFiltersClicked += delegate { ClearActivityFilters(); };

            // Edge dock (_activityDrawer) added and sent to back first so Fill dock (_activityScroll) takes remainder
            _activityBodyPanel.Controls.Add(_activityScroll);
            _activityBodyPanel.Controls.Add(_activityDrawer);
            _activityDrawer.SendToBack();
            _activityScroll.BringToFront();

            _tabActivity.Controls.Add(_activityBodyPanel);
            _activityToolbarPanel.SendToBack();
            _activityHeaderPanel.SendToBack();
            _activityBodyPanel.BringToFront();
        }

        private void ResizeActivityRows()
        {
            if (_activityScroll == null || _activityScroll.ContentPanel == null) return;
            int pad = DpiManager.ScaleInt(20);
            int newW = Math.Max(DpiManager.ScaleInt(300), _activityScroll.ContentPanel.ClientSize.Width - (pad * 2));
            foreach (Control c in _activityScroll.ContentPanel.Controls)
            {
                if (c is ActivityRow || c is ActivityDateHeader || c is ActivityEmptyState)
                {
                    if (c.Width != newW) c.Width = newW;
                }
            }
        }

        private void OnActivityRowSelected(ActivityRow row)
        {
            if (row == null || row.Record == null) return;

            _selectedActivityRecord = row.Record;
            foreach (var r in _activeActivityRows)
            {
                r.IsSelected = (r == row);
            }

            if (_activityDrawer != null)
            {
                _activityDrawer.Dock = Loc.IsRtl ? DockStyle.Left : DockStyle.Right;
                _activityDrawer.RightToLeft = Loc.RtlValue;
                _activityDrawer.OpenDrawer(row.Record);
            }
        }

        private void OnActivityRowDetailsRequested(ActivityRow row)
        {
            OnActivityRowSelected(row);
        }

        private void ClearActivityFilters()
        {
            if (_activitySearchBox != null)
            {
                _activitySearchBox.Text = "";
            }
            if (_activityCatDropdown != null) _activityCatDropdown.SelectedIndex = 0;
            if (_activityDateDropdown != null) _activityDateDropdown.SelectedIndex = 0;
            RefreshActivityView();
        }

        private void RefreshActivityView()
        {
            if (_activityScroll == null || _activityScroll.ContentPanel == null) return;
            _activityScroll.ContentPanel.SuspendLayout();
            UiDrawingUtils.ClearAndDisposeControls(_activityScroll.ContentPanel.Controls, _activityEmptyState);
            _activeActivityRows.Clear();

            string search = (_activitySearchBox != null && !string.IsNullOrEmpty(_activitySearchBox.Text)) ? _activitySearchBox.Text.Trim().ToLowerInvariant() : "";
            int catIdx = (_activityCatDropdown != null) ? _activityCatDropdown.SelectedIndex : 0;
            int dateIdx = (_activityDateDropdown != null) ? _activityDateDropdown.SelectedIndex : 0;

            DateTime? minDate = null;
            DateTime? maxDate = null;

            if (dateIdx == 1) // Today
            {
                minDate = DateTime.Today;
                maxDate = DateTime.Today.AddDays(1).AddTicks(-1);
            }
            else if (dateIdx == 2) // Last 7 Days
            {
                minDate = DateTime.Today.AddDays(-7);
            }
            else if (dateIdx == 3) // Last 30 Days
            {
                minDate = DateTime.Today.AddDays(-30);
            }
            else if (dateIdx == 4 && _dateFromPicker != null && _dateToPicker != null) // Custom Range
            {
                minDate = _dateFromPicker.Value.Date;
                maxDate = _dateToPicker.Value.Date.AddDays(1).AddTicks(-1);
            }

            List<ActivityRecord> sourceRecords = TestIsolatedActivityRecords != null
                ? TestIsolatedActivityRecords
                : _activityRecords;

            if (_btnClearActivity != null)
            {
                _btnClearActivity.Enabled = (sourceRecords != null && sourceRecords.Count > 0);
            }

            List<ActivityRecord> filtered = new List<ActivityRecord>();
            foreach (var rec in sourceRecords)
            {
                // In-memory Search filter
                if (!string.IsNullOrEmpty(search))
                {
                    bool matchTitle = (rec.Title != null && rec.Title.ToLowerInvariant().Contains(search));
                    bool matchContext = (rec.Context != null && rec.Context.ToLowerInvariant().Contains(search));
                    bool matchBadge = (rec.ResultBadge != null && rec.ResultBadge.ToLowerInvariant().Contains(search));
                    if (!matchTitle && !matchContext && !matchBadge) continue;
                }

                // Category filter
                if (catIdx == 1) // Devices
                {
                    if (rec.Category != ActivityCategory.Devices && rec.Category != ActivityCategory.DeviceEvents) continue;
                }
                else if (catIdx == 2) // Protection
                {
                    if (rec.Category != ActivityCategory.Protection && rec.Category != ActivityCategory.PolicyChanges) continue;
                }
                else if (catIdx == 3) // Security
                {
                    if (rec.Category != ActivityCategory.Security && rec.Category != ActivityCategory.TrustedDevices) continue;
                }
                else if (catIdx == 4) // Errors
                {
                    if (rec.Category != ActivityCategory.Errors) continue;
                }

                // Date filter
                if (minDate.HasValue && rec.Timestamp < minDate.Value) continue;
                if (maxDate.HasValue && rec.Timestamp > maxDate.Value) continue;

                filtered.Add(rec);
            }

            int pad = DpiManager.ScaleInt(20);
            int clientW = _activityScroll.ContentPanel.ClientSize.Width;
            if (clientW < 300 && _tabActivity != null) clientW = _tabActivity.ClientSize.Width;
            int rowW = Math.Max(DpiManager.ScaleInt(300), clientW - (pad * 2));

            if (filtered.Count == 0)
            {
                bool isFilterActive = !string.IsNullOrEmpty(search) || catIdx > 0 || dateIdx > 0;
                _activityEmptyState.SetSearchMode(isFilterActive, search);
                _activityEmptyState.Location = new Point(pad, DpiManager.ScaleInt(24));
                _activityEmptyState.Width = rowW;
                _activityEmptyState.Visible = true;
                _activityScroll.ContentPanel.Controls.Add(_activityEmptyState);
            }
            else
            {
                _activityEmptyState.Visible = false;
                int curY = DpiManager.ScaleInt(10);

                // Group by date
                DateTime lastDate = DateTime.MinValue;
                foreach (var rec in filtered)
                {
                    if (rec.Timestamp.Date != lastDate)
                    {
                        lastDate = rec.Timestamp.Date;
                        string dateHeaderStr;
                        if (lastDate == DateTime.Today)
                            dateHeaderStr = (Loc.IsRtl ? "اليوم — " : "Today — ") + lastDate.ToString("yyyy-MM-dd");
                        else if (lastDate == DateTime.Today.AddDays(-1))
                            dateHeaderStr = (Loc.IsRtl ? "أمس — " : "Yesterday — ") + lastDate.ToString("yyyy-MM-dd");
                        else
                            dateHeaderStr = lastDate.ToString("yyyy-MM-dd");

                        ActivityDateHeader header = new ActivityDateHeader(dateHeaderStr);
                        header.Size = new Size(rowW, DpiManager.ScaleInt(32));
                        header.Location = new Point(pad, curY);
                        _activityScroll.ContentPanel.Controls.Add(header);
                        curY += header.Height + DpiManager.ScaleInt(4);
                    }

                    ActivityRow row = new ActivityRow(rec);
                    row.Size = new Size(rowW, DpiManager.ScaleInt(54));
                    row.Location = new Point(pad, curY);
                    if (_selectedActivityRecord != null && rec == _selectedActivityRecord)
                    {
                        row.IsSelected = true;
                    }
                    row.RowSelected += delegate(object s, EventArgs ea)
                    {
                        OnActivityRowSelected((ActivityRow)s);
                    };
                    row.DetailsRequested += delegate(object s, EventArgs ea)
                    {
                        OnActivityRowDetailsRequested((ActivityRow)s);
                    };

                    _activeActivityRows.Add(row);
                    _activityScroll.ContentPanel.Controls.Add(row);
                    curY += row.Height + DpiManager.ScaleInt(4);
                }

                // If drawer is open, keep drawer updated with selected record
                if (_selectedActivityRecord != null && _activityDrawer != null && _activityDrawer.IsOpen)
                {
                    if (filtered.Contains(_selectedActivityRecord))
                    {
                        _activityDrawer.UpdateRecordData();
                    }
                }
            }

            _activityScroll.ContentPanel.ResumeLayout(true);
            _activityScroll.UpdateScroll();
        }

        public void SeedSampleActivityForTesting()
        {
            DateTime now = DateTime.Now;
            List<ActivityRecord> samples = new List<ActivityRecord>();
            if (Loc.IsRtl)
            {
                samples.Add(new ActivityRecord(
                    now.AddMinutes(-5),
                    "تم توصيل وحدة تخزين USB (SanDisk Ultra)",
                    ActivityCategory.DeviceEvents,
                    "مصرح",
                    ThemeManager.ActivePalette.Success,
                    "PnP"));

                samples.Add(new ActivityRecord(
                    now.AddMinutes(-25),
                    "تم تفعيل حماية وسائط التخزين بنجاح",
                    ActivityCategory.Protection,
                    "نشط",
                    ThemeManager.ActivePalette.Success,
                    "ProtectionEngine"));

                samples.Add(new ActivityRecord(
                    now.AddHours(-2),
                    "تم رصد محاولة توصيل قرص خارجي غير موثوق وإخراجه",
                    ActivityCategory.DeviceEvents,
                    "حظر تلقائي",
                    ThemeManager.ActivePalette.Warning,
                    "ActiveSentinel"));

                samples.Add(new ActivityRecord(
                    now.AddHours(-4),
                    "فشل الإخراج التلقائي — استخدم «الإزالة الآمنة» قبل فصل الجهاز",
                    ActivityCategory.Errors,
                    "فشل",
                    ThemeManager.ActivePalette.Danger,
                    "DeviceEjector"));

                samples.Add(new ActivityRecord(
                    now.AddDays(-1),
                    "تم فحص وتأمين أقراص النظام الداخلية",
                    ActivityCategory.Security,
                    "محمي",
                    ThemeManager.ActivePalette.Success,
                    "SafetyGuard"));
            }
            else
            {
                samples.Add(new ActivityRecord(
                    now.AddMinutes(-5),
                    "USB Storage Device Connected (SanDisk Ultra)",
                    ActivityCategory.DeviceEvents,
                    "Permitted",
                    ThemeManager.ActivePalette.Success,
                    "PnP"));

                samples.Add(new ActivityRecord(
                    now.AddMinutes(-25),
                    "Storage Protection Policy Activated",
                    ActivityCategory.Protection,
                    "Active",
                    ThemeManager.ActivePalette.Success,
                    "ProtectionEngine"));

                samples.Add(new ActivityRecord(
                    now.AddHours(-2),
                    "Untrusted Removable Disk Intercepted and Safely Ejected",
                    ActivityCategory.DeviceEvents,
                    "Auto Eject",
                    ThemeManager.ActivePalette.Warning,
                    "ActiveSentinel"));

                samples.Add(new ActivityRecord(
                    now.AddHours(-4),
                    "Automatic eject failed — use Safely Remove before unplugging",
                    ActivityCategory.Errors,
                    "Failed",
                    ThemeManager.ActivePalette.Danger,
                    "DeviceEjector"));

                samples.Add(new ActivityRecord(
                    now.AddDays(-1),
                    "Internal System Boot Disks Protected & Excluded",
                    ActivityCategory.Security,
                    "Protected",
                    ThemeManager.ActivePalette.Success,
                    "SafetyGuard"));
            }
            TestIsolatedActivityRecords = samples;
        }

        public void LogActivity(string text)
        {
            // Smart auto-categorize based on text keywords
            ActivityCategory cat = ActivityCategory.Security;
            string badge = Loc.Get("BadgeEvent");
            Color badgeColor = Theme.AccentBlue;

            if (text.Contains("Blocked") || text.Contains("حظر"))
            {
                cat = ActivityCategory.DeviceEvents;
                badge = Loc.Get("BadgeBlocked");
                badgeColor = Theme.AccentOrange;
            }
            else if (text.Contains("Allowed") || text.Contains("Trust") || text.Contains("توثيق") || text.Contains("إلغاء"))
            {
                cat = ActivityCategory.DeviceEvents;
                badge = Loc.Get("BadgeVerified");
                badgeColor = Theme.AccentGreen;
            }
            else if (text.Contains("Policy") || text.Contains("Unlocked") || text.Contains("حماية"))
            {
                cat = ActivityCategory.PolicyChanges;
                badge = Loc.Get("BadgePolicy");
                badgeColor = Theme.AccentBlue;
            }
            else if (text.Contains("Failed") || text.Contains("فشل") || text.Contains("خطأ"))
            {
                cat = ActivityCategory.Errors;
                badge = Loc.Get("BadgeFailed");
                badgeColor = Theme.AccentRed;
            }

            LogActivity(text, cat, badge, badgeColor, "");
        }

        public void LogActivity(string title, ActivityCategory cat, string badge, Color badgeColor, string context = "")
        {
            ActivityRecord rec = new ActivityRecord(DateTime.Now, title, cat, badge, badgeColor, context);
            _activityRecords.Insert(0, rec);
            if (_activityScroll != null)
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(RefreshActivityView));
                    return;
                }
                RefreshActivityView();
            }
            if (_recentActivityPanel != null)
            {
                List<ActivityRecord> sourceRecords = TestIsolatedActivityRecords != null ? TestIsolatedActivityRecords : _activityRecords;
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(delegate { _recentActivityPanel.SetRecords(sourceRecords, Loc.IsRtl); }));
                }
                else
                {
                    _recentActivityPanel.SetRecords(sourceRecords, Loc.IsRtl);
                }
            }
        }

        internal static string FormatActivityLogMessage(string eventText, string failureDetail, bool ejectSuccess)
        {
            if (!ejectSuccess && !string.IsNullOrEmpty(failureDetail))
            {
                return string.Format("{0} ({1})", eventText, failureDetail);
            }
            return eventText;
        }

        private void OnDeviceEnforcementPending(object sender, DeviceInterceptEventArgs e)
        {
            string name = e.Device != null ? e.Device.FriendlyName : "USB Storage";
            string text = Loc.Get("SafeEjectPreparing");
            string activityText = string.Format("{0}: {1}", name, text);
            LogActivity(activityText, ActivityCategory.DeviceEvents, Loc.Get("BadgeReactiveAutoEject"), Theme.AccentOrange);

            if (_tray != null)
                _tray.ShowBalloon(Loc.Get("AppTitle"), text);
        }

        private void OnDeviceIntercepted(object sender, DeviceInterceptEventArgs e)
        {
            string name = e.Device != null ? e.Device.FriendlyName : "USB Storage";
            string formatKey = (e != null && e.EjectSuccess) ? "SafeEjectSuccessStatus" : "SafeEjectFailedStatus";
            string text = Loc.Get(formatKey);

            string activityText = FormatActivityLogMessage(string.Format("{0}: {1}", name, text), e != null ? e.Message : null, e != null && e.EjectSuccess);
            LogActivity(activityText, (e != null && e.EjectSuccess) ? ActivityCategory.DeviceEvents : ActivityCategory.Errors,
                        (e != null && e.EjectSuccess) ? Loc.Get("BadgeSuccess") : Loc.Get("BadgeFailed"),
                        (e != null && e.EjectSuccess) ? Theme.AccentGreen : Theme.AccentRed);

            if (_tray != null)
                _tray.ShowBalloon(Loc.Get("AppTitle"), text);
        }

        // ═══════════════════════════════════════════════════════════════════
        // PAGE 5: DISASTER RECOVERY (Truthful Pre-Shield State Wording)
        // ═══════════════════════════════════════════════════════════════════
        private void BuildTabRecovery()
        {
            _tabRecovery = new Panel();
            _tabRecovery.Dock = DockStyle.Fill;
            _tabRecovery.BackColor = ThemeManager.ActivePalette.BackgroundPrimary;

            _tabRecoveryScroll = new DarkScrollPanel();
            _tabRecoveryScroll.Dock = DockStyle.Fill;
            _tabRecoveryScroll.RightToLeft = Loc.RtlValue;
            _tabRecovery.Controls.Add(_tabRecoveryScroll);

            bool isRtl = (Loc.RtlValue == RightToLeft.Yes) || Loc.IsRtl;

            // 1. Page Header Labels
            _recoveryPageTitle = new Label();
            _recoveryPageTitle.Text = Loc.Get("RecoveryPageTitle");
            _recoveryPageTitle.Font = DpiManager.GetFont(TypographyRole.Title, isRtl);
            _recoveryPageTitle.ForeColor = ThemeManager.ActivePalette.TextPrimary;
            _recoveryPageTitle.BackColor = ThemeManager.ActivePalette.BackgroundPrimary;
            _recoveryPageTitle.AutoSize = true;
            _tabRecoveryScroll.ContentPanel.Controls.Add(_recoveryPageTitle);

            _recoveryPageSub = new Label();
            _recoveryPageSub.Text = Loc.Get("RecoveryPageSubtitle");
            _recoveryPageSub.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            _recoveryPageSub.ForeColor = ThemeManager.ActivePalette.TextSecondary;
            _recoveryPageSub.BackColor = ThemeManager.ActivePalette.BackgroundPrimary;
            _recoveryPageSub.AutoSize = true;
            _tabRecoveryScroll.ContentPanel.Controls.Add(_recoveryPageSub);

            // 2. Recovery Posture Card
            _recoveryPostureCard = new RecoveryPostureCard();
            _tabRecoveryScroll.ContentPanel.Controls.Add(_recoveryPostureCard);

            // 3. Exact Restore Semantics MessageBar
            _recoverySemanticsBar = new RecoverySemanticsMessageBar();
            _tabRecoveryScroll.ContentPanel.Controls.Add(_recoverySemanticsBar);

            // 4. Action Buttons
            _btnRestoreState = new AppButton();
            _btnRestoreState.Text = Loc.Get("RestoreBaselineBtn");
            _btnRestoreState.Variant = ButtonVariant.Primary; // Uses brand Accent (Crimson in Charcoal Red, Deep Indigo in Porcelain)
            _btnRestoreState.SizeVariant = ButtonSize.Medium;
            _btnRestoreState.Click += delegate { OnRestorePreShieldState(); };

            _btnOpenFolder = new AppButton();
            _btnOpenFolder.Text = Loc.Get("OpenRecoveryFolder");
            _btnOpenFolder.Variant = ButtonVariant.Secondary;
            _btnOpenFolder.SizeVariant = ButtonSize.Medium;
            _btnOpenFolder.Click += delegate { OnOpenRecoveryFolder(); };

            _btnExportPackage = new AppButton();
            _btnExportPackage.Text = Loc.Get("ExportPackageBtn");
            _btnExportPackage.Variant = ButtonVariant.Secondary;
            _btnExportPackage.SizeVariant = ButtonSize.Medium;
            _btnExportPackage.Click += delegate { OnExportRecoveryPackage(); };

            _tabRecoveryScroll.ContentPanel.Controls.Add(_btnRestoreState);
            _tabRecoveryScroll.ContentPanel.Controls.Add(_btnOpenFolder);
            _tabRecoveryScroll.ContentPanel.Controls.Add(_btnExportPackage);

            // 5. Recovery Artifacts Card
            _recoveryArtifactsCard = new RecoveryArtifactsCard();
            _tabRecoveryScroll.ContentPanel.Controls.Add(_recoveryArtifactsCard);

            // 6. Technical Details Accordion
            _recoveryAccordion = new RecoveryTechnicalAccordion();
            _recoveryAccordion.ExpandedChanged += delegate { LayoutRecoveryTab(); };
            _tabRecoveryScroll.ContentPanel.Controls.Add(_recoveryAccordion);

            _tabRecoveryScroll.ContentPanel.Resize += delegate { LayoutRecoveryTab(); };

            RefreshRecoveryStats();
            LayoutRecoveryTab();
            _tabRecoveryScroll.UpdateScroll();
            _tabRecoveryScroll.ScrollToTop();
        }

        private void LayoutRecoveryTab()
        {
            if (_tabRecoveryScroll == null || _tabRecoveryScroll.ContentPanel == null) return;

            bool isRtl = (Loc.RtlValue == RightToLeft.Yes) || Loc.IsRtl;
            int pad = DpiManager.ScaleInt(SpacingTokens.Space20);
            int clientW = _tabRecoveryScroll.ContentPanel.ClientSize.Width;
            int maxContentW = DpiManager.ScaleInt(1080);
            int availableW = clientW - (pad * 2);
            int cardW = Math.Min(maxContentW, Math.Max(DpiManager.ScaleInt(300), availableW));
            int startX = pad + Math.Max(0, (availableW - cardW) / 2);

            int curY = pad;

            // 1. Page Header Labels
            if (_recoveryPageTitle != null)
            {
                _recoveryPageTitle.Font = DpiManager.GetFont(TypographyRole.Title, isRtl);
                _recoveryPageTitle.ForeColor = ThemeManager.ActivePalette.TextPrimary;
                if (isRtl)
                {
                    _recoveryPageTitle.Location = new Point(startX + cardW - _recoveryPageTitle.Width, curY);
                }
                else
                {
                    _recoveryPageTitle.Location = new Point(startX, curY);
                }
                curY += _recoveryPageTitle.Height + DpiManager.ScaleInt(4);
            }

            if (_recoveryPageSub != null)
            {
                _recoveryPageSub.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
                _recoveryPageSub.ForeColor = ThemeManager.ActivePalette.TextSecondary;
                if (isRtl)
                {
                    _recoveryPageSub.Location = new Point(startX + cardW - _recoveryPageSub.Width, curY);
                }
                else
                {
                    _recoveryPageSub.Location = new Point(startX, curY);
                }
                curY += _recoveryPageSub.Height + DpiManager.ScaleInt(14);
            }

            // 2. Recovery Posture Card
            if (_recoveryPostureCard != null)
            {
                _recoveryPostureCard.Location = new Point(startX, curY);
                _recoveryPostureCard.Width = cardW;
                _recoveryPostureCard.UpdateHeight();
                curY += _recoveryPostureCard.Height + DpiManager.ScaleInt(12);
            }

            // 3. Exact Restore Semantics MessageBar
            if (_recoverySemanticsBar != null)
            {
                int barH = _recoverySemanticsBar.CalculateRequiredHeight(cardW);
                _recoverySemanticsBar.Location = new Point(startX, curY);
                _recoverySemanticsBar.Size = new Size(cardW, barH);
                curY += _recoverySemanticsBar.Height + DpiManager.ScaleInt(14);
            }

            // 4. Action Buttons Row
            if (_btnRestoreState != null && _btnOpenFolder != null && _btnExportPackage != null)
            {
                Font btnFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
                int twRestore = TextRenderer.MeasureText(_btnRestoreState.Text ?? "", btnFont).Width;
                int twFolder = TextRenderer.MeasureText(_btnOpenFolder.Text ?? "", btnFont).Width;
                int twExport = TextRenderer.MeasureText(_btnExportPackage.Text ?? "", btnFont).Width;

                int btnH = DpiManager.ScaleInt(34);
                int restoreW = Math.Max(DpiManager.ScaleInt(210), twRestore + DpiManager.ScaleInt(36));
                int folderW = Math.Max(DpiManager.ScaleInt(155), twFolder + DpiManager.ScaleInt(32));
                int exportW = Math.Max(DpiManager.ScaleInt(155), twExport + DpiManager.ScaleInt(32));
                int gap = DpiManager.ScaleInt(10);

                _btnRestoreState.Size = new Size(restoreW, btnH);
                _btnOpenFolder.Size = new Size(folderW, btnH);
                _btnExportPackage.Size = new Size(exportW, btnH);

                if (isRtl)
                {
                    int curX = startX + cardW - restoreW;
                    _btnRestoreState.Location = new Point(curX, curY);

                    curX -= (gap + folderW);
                    _btnOpenFolder.Location = new Point(curX, curY);

                    curX -= (gap + exportW);
                    _btnExportPackage.Location = new Point(curX, curY);
                }
                else
                {
                    int curX = startX;
                    _btnRestoreState.Location = new Point(curX, curY);

                    curX += restoreW + gap;
                    _btnOpenFolder.Location = new Point(curX, curY);

                    curX += folderW + gap;
                    _btnExportPackage.Location = new Point(curX, curY);
                }

                curY += btnH + DpiManager.ScaleInt(16);
            }

            // 5. Recovery Artifacts Card
            if (_recoveryArtifactsCard != null)
            {
                _recoveryArtifactsCard.Location = new Point(startX, curY);
                _recoveryArtifactsCard.Width = cardW;
                _recoveryArtifactsCard.UpdateCardLayout();
                curY += _recoveryArtifactsCard.Height + DpiManager.ScaleInt(16);
            }

            // 6. Technical Details Accordion
            if (_recoveryAccordion != null)
            {
                _recoveryAccordion.Location = new Point(startX, curY);
                _recoveryAccordion.Width = cardW;
                _recoveryAccordion.UpdateAccordionGeometry();
                curY += _recoveryAccordion.Height + DpiManager.ScaleInt(24);
            }

            _tabRecoveryScroll.UpdateScroll();
        }

        private void RefreshRecoveryStats()
        {
            JournalLoadResult loadResult = JournalRepository.LoadWithStatus();
            StateJournal journal = loadResult.Journal ?? new StateJournal();

            bool journalExists = (loadResult.Status == JournalLoadStatus.LoadedPrimary ||
                                  loadResult.Status == JournalLoadStatus.LoadedBackup);
            int baselineCount = (journal.BaselineEntries != null) ? journal.BaselineEntries.Count : 0;

            if (_recoveryPostureCard != null)
            {
                _recoveryPostureCard.SetData(journal, journalExists);
            }

            if (_btnRestoreState != null)
            {
                _btnRestoreState.Enabled = (baselineCount > 0 && loadResult.Status != JournalLoadStatus.Corrupted);
            }

            if (_recoveryArtifactsCard != null)
            {
                _recoveryArtifactsCard.RefreshArtifacts();
            }

            if (_recoveryAccordion != null)
            {
                _recoveryAccordion.BindJournalData(journal);
            }

            LayoutRecoveryTab();
        }

        private void OnOpenRecoveryFolder()
        {
            try
            {
                string dir = SecurityAclHelper.GetProgramDataDirectory();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", dir);
            }
            catch (Exception ex)
            {
                MsgBox.Show(this, Loc.Get("ErrorGeneric", ex.Message), Loc.Get("ErrorTitle"));
            }
        }

        private void OnRestorePreShieldState()
        {
            if (MsgBox.Show(this, Loc.Get("RecoveryWarningPrompt"), Loc.Get("AppTitle"), MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;

            if (!RequireAuth(Loc.Get("RestoreBaselineBtn"))) return;

            List<string> errors;
            bool ok = RecoveryManager.RestorePreShieldState(out errors);
            if (ok)
            {
                // Reset active policy to all false across both GUI and Service
                ProtectionPolicy clearPolicy = new ProtectionPolicy();
                string syncErr;
                ApplyProtectionPolicyUnified(clearPolicy, null, out syncErr);

                LogActivity(Loc.Get("EventPreShieldRestored"));
                MsgBox.Show(this, Loc.Get("RecoveryComplete"), Loc.Get("AppTitle"));
                RebuildUI();
            }
            else
            {
                string errMsg = (errors != null && errors.Count > 0)
                    ? string.Join("\n", errors.ToArray())
                    : Loc.Get("RecoveryStatusNoneDesc");
                MsgBox.Show(this, errMsg, Loc.Get("ErrorTitle"));
            }
        }

        private void OnExportRecoveryPackage()
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = Loc.Get("ExportPackageBtn") + " (.reg)";
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    bool ok = RecoveryManager.GenerateRecoveryPackage(fbd.SelectedPath);
                    if (ok)
                    {
                        MsgBox.Show(this, string.Format(Loc.Get("ExportComplete"), fbd.SelectedPath), Loc.Get("AppTitle"));
                    }
                    else
                    {
                        MsgBox.Show(this, Loc.Get("ExportPackageFailed"), Loc.Get("ErrorTitle"));
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // PAGE 6: SETTINGS
        // ═══════════════════════════════════════════════════════════════════
        // ═══════════════════════════════════════════════════════════════════
        // PAGE 6: SETTINGS (v2.1 Production Redesign)
        // ═══════════════════════════════════════════════════════════════════
        private void BuildTabSettings()
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = Loc.IsRtl;

            _tabSettings = new Panel();
            _tabSettings.Dock = DockStyle.Fill;
            _tabSettings.BackColor = pal.BackgroundPrimary;

            _tabSettingsScroll = new DarkScrollPanel();
            _tabSettingsScroll.Dock = DockStyle.Fill;
            _tabSettingsScroll.RightToLeft = Loc.RtlValue;
            _tabSettingsScroll.BackColor = pal.BackgroundPrimary;
            if (_tabSettingsScroll.ContentPanel != null)
                _tabSettingsScroll.ContentPanel.BackColor = pal.BackgroundPrimary;
            _tabSettings.Controls.Add(_tabSettingsScroll);

            int pad = DpiManager.ScaleInt(24);

            // Page Header
            _settingsPageTitle = new Label();
            _settingsPageTitle.Text = Loc.Get("SettingsTitle");
            _settingsPageTitle.Font = Theme.FontTitle;
            _settingsPageTitle.ForeColor = pal.TextPrimary;
            _settingsPageTitle.BackColor = pal.BackgroundPrimary;
            _settingsPageTitle.AutoSize = true;
            _tabSettingsScroll.ContentPanel.Controls.Add(_settingsPageTitle);

            _settingsPageSub = new Label();
            _settingsPageSub.Text = Loc.Get("SettingsSubtitle");
            _settingsPageSub.Font = Theme.FontNormal;
            _settingsPageSub.ForeColor = pal.TextSecondary;
            _settingsPageSub.BackColor = pal.BackgroundPrimary;
            _settingsPageSub.AutoSize = true;
            _tabSettingsScroll.ContentPanel.Controls.Add(_settingsPageSub);

            AppSettingsData appSettings = SettingsRepository.Load();

            // 1. Appearance Card
            _cardAppearance = new SettingsCard();
            _cardAppearance.CardTitle = Loc.Get("SettingsSecAppearance");

            // Row: Color Theme
            _ddTheme = new AppDropdown();
            _ddTheme.Width = DpiManager.ScaleInt(165);
            _ddTheme.Items.Add("Charcoal Red");
            _ddTheme.Items.Add("Midnight");
            _ddTheme.Items.Add("Graphite");
            _ddTheme.Items.Add("Porcelain");
            _ddTheme.Items.Add("Cloud");
            _ddTheme.Items.Add(isRtl ? "مزامنة مع ويندوز" : "Follow Windows");
            SyncSettingsThemeDropdown();
            _ddTheme.SelectedIndexChanged += delegate
            {
                if (_isSyncingTheme) return;
                AppTheme targetTheme = AppTheme.CharcoalRed;
                switch (_ddTheme.SelectedIndex)
                {
                    case 0: targetTheme = AppTheme.CharcoalRed; break;
                    case 1: targetTheme = AppTheme.Midnight; break;
                    case 2: targetTheme = AppTheme.Graphite; break;
                    case 3: targetTheme = AppTheme.Porcelain; break;
                    case 4: targetTheme = AppTheme.Cloud; break;
                    case 5: targetTheme = AppTheme.FollowWindows; break;
                }
                ThemeManager.SetTheme(targetTheme, true);
            };

            SettingsRow rowTheme = new SettingsRow();
            rowTheme.Title = Loc.Get("SettingsThemeLabel");
            rowTheme.Description = Loc.Get("SettingsThemeDesc");
            rowTheme.ActionControl = _ddTheme;
            _cardAppearance.AddRow(rowTheme);

            // Row: Display Language
            _ddLanguage = new AppDropdown();
            _ddLanguage.Width = DpiManager.ScaleInt(165);
            _ddLanguage.Items.Add("English");
            _ddLanguage.Items.Add("العربية");
            _ddLanguage.SelectedIndex = isRtl ? 1 : 0;
            _ddLanguage.SelectedIndexChanged += delegate
            {
                if (_isSyncingLanguage) return;
                bool chooseAr = (_ddLanguage.SelectedIndex == 1);
                if (chooseAr != Loc.IsRtl)
                {
                    ToggleLanguage();
                }
            };

            SettingsRow rowLang = new SettingsRow();
            rowLang.Title = Loc.Get("SettingsLanguageLabel");
            rowLang.Description = Loc.Get("SettingsLanguageDesc");
            rowLang.ActionControl = _ddLanguage;
            _cardAppearance.AddRow(rowLang);

            // Row: Compact Sidebar
            _toggleSidebar = new AppToggle();
            _toggleSidebar.Checked = _sidebar != null && _sidebar.IsCompact;
            _toggleSidebar.CheckedChanged += delegate
            {
                if (_sidebar != null && _sidebar.IsCompact != _toggleSidebar.Checked)
                {
                    _sidebar.IsCompact = _toggleSidebar.Checked;
                    AppSettingsData st = SettingsRepository.Load();
                    st.SidebarCompact = _toggleSidebar.Checked;
                    SettingsRepository.Save(st);
                }
            };

            SettingsRow rowSidebar = new SettingsRow();
            rowSidebar.Title = Loc.Get("SettingsSidebarCompactLabel");
            rowSidebar.Description = Loc.Get("SettingsSidebarCompactDesc");
            rowSidebar.ActionControl = _toggleSidebar;
            _cardAppearance.AddRow(rowSidebar);

            _tabSettingsScroll.ContentPanel.Controls.Add(_cardAppearance);

            // 2. Application Behavior Card
            _cardBehavior = new SettingsCard();
            _cardBehavior.CardTitle = Loc.Get("SettingsSecBehavior");

            _toggleAutoStart = new AppToggle();
            _toggleAutoStart.Checked = appSettings.AutoStartEnabled;
            _toggleAutoStart.CheckedChanged += delegate
            {
                AppSettingsData s = SettingsRepository.Load();
                s.AutoStartEnabled = _toggleAutoStart.Checked;
                SettingsRepository.Save(s);
                if (!IsCaptureMode)
                {
                    UpdateAutoStartRunKey(_toggleAutoStart.Checked);
                }
            };

            SettingsRow rowAutoStart = new SettingsRow();
            rowAutoStart.Title = Loc.Get("SettingsAutoStartTitle");
            rowAutoStart.Description = Loc.Get("SettingsAutoStartDesc");
            rowAutoStart.ActionControl = _toggleAutoStart;
            _cardBehavior.AddRow(rowAutoStart);

            _toggleAutoLock = new AppToggle();
            _toggleAutoLock.Checked = appSettings.AutoLockOnStartup;
            _toggleAutoLock.CheckedChanged += delegate
            {
                AppSettingsData s = SettingsRepository.Load();
                s.AutoLockOnStartup = _toggleAutoLock.Checked;
                SettingsRepository.Save(s);
            };

            SettingsRow rowAutoLock = new SettingsRow();
            rowAutoLock.Title = Loc.Get("SettingsAutoLockTitle");
            rowAutoLock.Description = Loc.Get("SettingsAutoLockDesc");
            rowAutoLock.ActionControl = _toggleAutoLock;
            _cardBehavior.AddRow(rowAutoLock);

            _toggleTray = new AppToggle();
            _toggleTray.Checked = appSettings.MinimizeToTray;
            _toggleTray.CheckedChanged += delegate
            {
                AppSettingsData s = SettingsRepository.Load();
                s.MinimizeToTray = _toggleTray.Checked;
                SettingsRepository.Save(s);
            };

            SettingsRow rowTray = new SettingsRow();
            rowTray.Title = Loc.Get("SettingsMinimizeToTrayTitle");
            rowTray.Description = Loc.Get("SettingsMinimizeToTrayDesc");
            rowTray.ActionControl = _toggleTray;
            _cardBehavior.AddRow(rowTray);

            _tabSettingsScroll.ContentPanel.Controls.Add(_cardBehavior);

            // 3. Security & Access Control Card
            _cardSecurity = new SettingsSecurityCard();
            _cardSecurity.ConfigurePasswordClicked += delegate { OnConfigurePassword(); };
            _cardSecurity.RemovePasswordClicked += delegate { OnRemovePassword(); };
            _cardSecurity.GenerateRecoveryKeyClicked += delegate
            {
                if (!RequireAuth(Loc.Get("RecoveryGenKey"))) return;
                string newKey = RecoveryKeyManager.GenerateNewMasterRecoveryKey();
                try { Clipboard.SetText(newKey); } catch { }
                MsgBox.Show(this, string.Format(Loc.Get("RecoveryKeyGeneratedMsg"), newKey), Loc.Get("AppTitle"));
                RefreshSettingsTab();
            };
            _tabSettingsScroll.ContentPanel.Controls.Add(_cardSecurity);

            // 4. Background Protection Service Card
            _cardService = new SettingsServiceCard();
            _cardService.InstallClicked += delegate { OnToggleService(); };
            _cardService.UninstallClicked += delegate { OnToggleService(); };
            _cardService.StartStopClicked += delegate
            {
                if (!RequireAuth(Loc.Get("ActionControlService"))) return;
                if (!IsCaptureMode)
                {
                    string err;
                    if (ServiceManager.IsServiceRunning())
                    {
                        if (!ServiceManager.StopService(out err))
                        {
                            MsgBox.Show(this, string.Format(Loc.Get("ServiceStopFailed"), err), Loc.Get("AppTitle"));
                        }
                    }
                    else
                    {
                        if (!ServiceManager.StartService(out err))
                        {
                            MsgBox.Show(this, string.Format(Loc.Get("ServiceStartFailed"), err), Loc.Get("AppTitle"));
                        }
                    }
                }
                RefreshSettingsTab();
            };
            _tabSettingsScroll.ContentPanel.Controls.Add(_cardService);

            // 5. About Card
            _cardAbout = new SettingsAboutCard();
            _tabSettingsScroll.ContentPanel.Controls.Add(_cardAbout);

            _tabSettingsScroll.ContentPanel.Resize += delegate { LayoutSettingsTab(); };

            LayoutSettingsTab();
            RefreshSettingsTab();
            _tabSettingsScroll.UpdateScroll();
        }

        private void LayoutSettingsTab()
        {
            if (_tabSettingsScroll == null || _tabSettingsScroll.ContentPanel == null) return;

            int pad = DpiManager.ScaleInt(SpacingTokens.Space24);
            int clientW = _tabSettingsScroll.ContentPanel.ClientSize.Width;
            int maxContentW = DpiManager.ScaleInt(1080);
            int availableW = clientW - (pad * 2);
            int cardW = Math.Min(maxContentW, Math.Max(DpiManager.ScaleInt(320), availableW));
            int startX = pad + Math.Max(0, (availableW - cardW) / 2);
            int curY = pad;
            int gap = DpiManager.ScaleInt(16);
            bool isRtl = Loc.IsRtl;

            // Page Header
            if (_settingsPageTitle != null)
            {
                int titleX = isRtl ? (startX + cardW - _settingsPageTitle.Width) : startX;
                _settingsPageTitle.Location = new Point(titleX, curY);
            }
            if (_settingsPageSub != null)
            {
                int subX = isRtl ? (startX + cardW - _settingsPageSub.Width) : startX;
                _settingsPageSub.Location = new Point(subX, curY + DpiManager.ScaleInt(28));
            }
            curY += DpiManager.ScaleInt(62);

            // Appearance Card
            if (_cardAppearance != null)
            {
                _cardAppearance.Location = new Point(startX, curY);
                _cardAppearance.Width = cardW;
                _cardAppearance.PerformCardLayout();
                curY += _cardAppearance.Height + gap;
            }

            // Application Behavior Card
            if (_cardBehavior != null)
            {
                _cardBehavior.Location = new Point(startX, curY);
                _cardBehavior.Width = cardW;
                _cardBehavior.PerformCardLayout();
                curY += _cardBehavior.Height + gap;
            }

            // Security Card
            if (_cardSecurity != null)
            {
                _cardSecurity.Location = new Point(startX, curY);
                _cardSecurity.Width = cardW;
                _cardSecurity.PerformCardLayout();
                curY += _cardSecurity.Height + gap;
            }

            // Service Card
            if (_cardService != null)
            {
                _cardService.Location = new Point(startX, curY);
                _cardService.Width = cardW;
                _cardService.PerformCardLayout();
                curY += _cardService.Height + gap;
            }

            // About Card
            if (_cardAbout != null)
            {
                _cardAbout.Location = new Point(startX, curY);
                _cardAbout.Width = cardW;
                _cardAbout.UpdateHeight();
                curY += _cardAbout.Height + pad;
            }

            _tabSettingsScroll.UpdateScroll();
        }

        private void RefreshSettingsTab()
        {
            SyncSettingsThemeDropdown();

            if (_toggleSidebar != null && _sidebar != null)
            {
                _toggleSidebar.Checked = _sidebar.IsCompact;
            }

            if (_cardSecurity != null)
            {
                _cardSecurity.UpdateSecurityState();
            }

            if (_cardService != null)
            {
                _cardService.UpdateServiceState();
            }

            LayoutSettingsTab();
        }

        private void SyncSettingsThemeDropdown()
        {
            if (_ddTheme == null) return;
            _isSyncingTheme = true;
            try
            {
                switch (ThemeManager.CurrentTheme)
                {
                    case AppTheme.CharcoalRed:   _ddTheme.SelectedIndex = 0; break;
                    case AppTheme.Midnight:      _ddTheme.SelectedIndex = 1; break;
                    case AppTheme.Graphite:      _ddTheme.SelectedIndex = 2; break;
                    case AppTheme.Porcelain:     _ddTheme.SelectedIndex = 3; break;
                    case AppTheme.Cloud:         _ddTheme.SelectedIndex = 4; break;
                    case AppTheme.FollowWindows: _ddTheme.SelectedIndex = 5; break;
                }
            }
            finally
            {
                _isSyncingTheme = false;
            }
        }

        private void OnToggleService()
        {
            if (IsCaptureMode) return;
            if (!RequireAuth(Loc.Get("ActionControlService"))) return;
            string err;
            if (ServiceManager.IsServiceInstalled())
            {
                bool ok = ServiceManager.UninstallService(out err);
                MsgBox.Show(this, ok ? Loc.Get("ServiceUninstalled") : Loc.Get("ErrorGeneric", err), Loc.Get("AppTitle"));
            }
            else
            {
                bool ok = ServiceManager.InstallService(out err);
                MsgBox.Show(this, ok ? Loc.Get("ServiceInstalled") : Loc.Get("ErrorGeneric", err), Loc.Get("AppTitle"));
            }
            RefreshSettingsTab();
        }

        private static void UpdateAutoStartRunKey(bool enable)
        {
            if (IsCaptureMode) return;
            const string RUN_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            const string APP_NAME = "UsbShieldPro";
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RUN_KEY, true))
                {
                    if (key == null) return;
                    if (enable)
                        key.SetValue(APP_NAME, "\"" + Application.ExecutablePath + "\" --silent");
                    else
                        key.DeleteValue(APP_NAME, false);
                }
            }
            catch { }
        }

        private void OnRemovePassword()
        {
            if (IsCaptureMode) return;
            if (!RequireAuth(Loc.Get("RemovePassword"))) return;

            DialogResult dr = MessageBox.Show(this,
                Loc.Get("RemovePasswordConfirm"),
                Loc.Get("AppTitle"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (dr != DialogResult.Yes) return;

            PasswordManager.RemovePassword();
            RecoveryKeyManager.DeleteMasterRecoveryKey();
            LogActivity(Loc.Get("PasswordRemoved"));
            MsgBox.Show(this, Loc.Get("RemovePasswordSuccess"), Loc.Get("AppTitle"));
            RefreshSettingsTab();
        }

        private void OnConfigurePassword()
        {
            if (IsCaptureMode) return;
            if (PasswordManager.IsPasswordConfigured())
            {
                if (!RequireAuth(Loc.Get("ChangePassword"))) return;
            }

            using (PasswordDialog dlg = new PasswordDialog(Loc.Get("SetPassword"), Loc.Get("EnterNewPassword"), false))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(dlg.EnteredPassword))
                    return;

                string p1 = dlg.EnteredPassword;
                using (PasswordDialog dlg2 = new PasswordDialog(Loc.Get("SetPassword"), Loc.Get("ConfirmPassword"), false))
                {
                    if (dlg2.ShowDialog(this) != DialogResult.OK || dlg2.EnteredPassword != p1)
                    {
                        MsgBox.Show(this, Loc.Get("PasswordMismatch"), Loc.Get("ErrorTitle"));
                        return;
                    }
                }

                string pwErr;
                if (!PasswordManager.SetPassword(p1, out pwErr))
                {
                    MsgBox.Show(this, Loc.Get("ErrorGeneric", pwErr), Loc.Get("ErrorTitle"));
                    return;
                }
                if (!RecoveryKeyManager.IsMasterRecoveryKeyConfigured())
                {
                    RecoveryKeyManager.GenerateNewMasterRecoveryKey();
                }
                MsgBox.Show(this, Loc.Get("PasswordSet"), Loc.Get("AppTitle"));
                RefreshSettingsTab();
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // AUTHENTICATION GUARD
        // ═══════════════════════════════════════════════════════════════════
        private bool RequireAuth(string actionPrompt)
        {
            if (!PasswordManager.IsPasswordConfigured()) return true;

            using (PasswordDialog dlg = new PasswordDialog(Loc.Get("PasswordRequired"), actionPrompt, true))
            {
                if (!this.Visible || this.WindowState == FormWindowState.Minimized)
                {
                    dlg.StartPosition = FormStartPosition.CenterScreen;
                    dlg.TopMost = true;
                    if (dlg.ShowDialog() != DialogResult.OK) return false;
                }
                else
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                }

                if (dlg.UsedRecoveryKey)
                {
                    LogActivity(Loc.Get("EventRecoveryKeyAuth"));
                }
                return true;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // QUICK ACTIONS & STATUS VERIFICATION
        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// Unified, authoritative protection policy enforcement.
        /// When the Windows Service is running, delegates to the Service via IPC and waits for
        /// explicit acknowledgement. When the Service is stopped or absent (Portable mode),
        /// directly applies policy via ProtectionEngine as GUI user.
        /// Invariant: GUI never declares successful policy enforcement unless the authoritative
        /// engine (Service or local engine) has confirmed success.
        /// </summary>
        private bool ApplyProtectionPolicyUnified(ProtectionPolicy newPolicy, string activityLogMessage, out string errorMessage)
        {
            errorMessage = null;
            if (newPolicy == null)
            {
                errorMessage = "Policy cannot be null.";
                return false;
            }

            // Incompatibility Guard
            if (newPolicy.HardBlockUsbStor && newPolicy.WhitelistMode)
            {
                errorMessage = "Hard driver block (USBSTOR Start=4) completely disables driver loading and is incompatible with Whitelist Mode.";
                return false;
            }

            // Monotonic Policy Version Guard: Enforce strictly increasing generation across all mutations
            AppSettingsData currentSettings = SettingsRepository.Load();
            int currentVer = (currentSettings != null && currentSettings.ActivePolicy != null)
                ? currentSettings.ActivePolicy.PolicyVersion
                : 1;
            if (newPolicy.PolicyVersion <= currentVer)
            {
                newPolicy.PolicyVersion = currentVer + 1;
                newPolicy.LastModifiedUtc = DateTime.UtcNow;
            }

            bool ok = false;
            bool isServiceRunning = ServiceManager.IsServiceRunning();

            if (isServiceRunning)
            {
                var ownerState = EnforcementOwnerResolver.ResolveOwner();
                if (ownerState == EnforcementOwnerState.ServiceDegraded)
                {
                    errorMessage = Loc.IsRtl
                        ? "خدمة الحماية في الخلفية غير مستجيبة. يرجى إعادة تشغيل الخدمة أو استخدام الوضع المحمول."
                        : "Background Service is degraded or unresponsive. Policy cannot be synchronized. Please restart the service or switch to Portable Mode.";
                    return false;
                }

                // Authoritative owner: Service
                bool serviceAck;
                ok = ServiceManager.TrySynchronizePolicy(newPolicy, out errorMessage, out serviceAck);
                if (ok && serviceAck)
                {
                    // Invalidate GUI settings cache to immediately reflect the policy persisted by the service
                    SettingsRepository.ResetCache();
                }
            }
            else
            {
                // Authoritative owner: Local GUI / Portable Mode
                List<string> errors;
                ProtectionPolicy previousPolicy = (currentSettings != null) ? currentSettings.ActivePolicy : null;
                ok = ProtectionEngine.ApplyPolicy(newPolicy, previousPolicy, out errors);
                if (!ok)
                {
                    errorMessage = (errors != null && errors.Count > 0)
                        ? string.Join("\n", errors.ToArray())
                        : "Failed to apply protection policy.";
                }
                else
                {
                    SettingsRepository.SavePolicy(newPolicy);
                }
            }

            if (ok)
            {


                if (!string.IsNullOrEmpty(activityLogMessage))
                {
                    LogActivity(activityLogMessage);
                }

                RefreshLiveStatus();
                SyncRuleTogglesWithSettings();
                return true;
            }

            return false;
        }

        private void ExecuteQuickLock()
        {
            if (!RequireAuth(Loc.Get("QuickLock"))) return;

            AppSettingsData s = SettingsRepository.Load();
            ProtectionPolicy p = s.ActivePolicy != null ? s.ActivePolicy.Clone() : new ProtectionPolicy();
            p.BlockAllRemovable = true;
            p.BlockRemovableDisks = true;
            p.BlockWpdPhonesAndMedia = true;
            p.IncrementVersion();

            string error;
            if (!ApplyProtectionPolicyUnified(p, Loc.Get("RuleBlockAllRemovable"), out error))
            {
                MsgBox.Show(this, Loc.Get("ErrorGeneric", error), Loc.Get("ErrorTitle"));
                SyncRuleTogglesWithSettings();
            }
        }

        private void ExecuteQuickUnlock()
        {
            if (!RequireAuth(Loc.Get("QuickUnlock"))) return;

            AppSettingsData s = SettingsRepository.Load();
            ProtectionPolicy p = new ProtectionPolicy(); // All false
            p.PolicyVersion = (s.ActivePolicy != null ? s.ActivePolicy.PolicyVersion : 1) + 1;
            p.LastModifiedUtc = DateTime.UtcNow;

            string error;
            if (!ApplyProtectionPolicyUnified(p, Loc.Get("StorageUnlockedTitle"), out error))
            {
                MsgBox.Show(this, Loc.Get("ErrorGeneric", error), Loc.Get("ErrorTitle"));
                SyncRuleTogglesWithSettings();
            }
        }

        private void StartTemporaryAccess(int minutes)
        {
            if (!RequireAuth(Loc.Get("GrantTempAccess"))) return;

            AppSettingsData s = SettingsRepository.Load();
            string err;
            bool ok = ServiceManager.StartTemporaryAccess(minutes, s.ActivePolicy, out err);
            if (ok)
            {
                LogActivity(string.Format(Loc.Get("EventTempAccessStarted"), minutes));
                RefreshLiveStatus();
            }
            else
            {
                MsgBox.Show(this, Loc.Get("ErrorGeneric", err), Loc.Get("ErrorTitle"));
            }
        }

        private void CancelTemporaryAccess()
        {
            string err;
            bool ok = ServiceManager.CancelTemporaryAccess(out err);
            if (ok)
            {
                LogActivity(Loc.Get("EventTempAccessExpired"));
            }
            else
            {
                LogActivity(string.Format(Loc.Get("EventCancelTempAccessFailed"), err));
            }
            RefreshLiveStatus();
        }

        private void RefreshLiveStatus()
        {
            AppSettingsData s = SettingsRepository.Load();
            ProtectionPolicy p = s.ActivePolicy ?? new ProtectionPolicy();
            ProtectionStatusReport report = TestIsolatedReport;
            if (report == null)
            {
                if (IsCaptureMode)
                {
                    report = new ProtectionStatusReport
                    {
                        OverallState = (p.BlockRemovableDisks || p.BlockAllRemovable) ? ActivationState.ClassPolicyConfigured : (p.IsAnyProtectionActive() ? ActivationState.AppliedAndVerified : ActivationState.Unprotected),
                        LastVerifiedUtc = DateTime.UtcNow
                    };
                }
                else
                {
                    try { report = StateVerifier.Verify(p); } catch { }
                }
            }

            int remainingSeconds = TemporaryAccessManager.PeekRemainingSeconds();
            bool tempActive = remainingSeconds > 0;

            bool isProtected = false;
            bool isUnprotected = false;
            string statusTitle = "";
            string statusSubtitle = "";
            StatusBadgeVariant statusBadgeVariant = StatusBadgeVariant.Neutral;

            var effectiveOwner = EnforcementOwnerResolver.GetEffectiveOwnerState();
            bool isServiceDegraded = (report != null && report.IsServiceDegraded) || (effectiveOwner == EnforcementOwnerState.ServiceDegraded);

            if (isServiceDegraded)
            {
                statusTitle = Loc.IsRtl ? "تحذير: الخدمة متوقفة" : "SERVICE DEGRADED";
                statusSubtitle = Loc.IsRtl
                    ? "خدمة الحماية في الخلفية غير مستجيبة. يرجى إعادة تشغيل الخدمة."
                    : "Background Protection Service is unresponsive. Enforcement may be inactive.";
                statusBadgeVariant = StatusBadgeVariant.Danger;
                isProtected = false;
                isUnprotected = true;
            }
            else if (tempActive)
            {
                statusTitle = Loc.Get("BadgeTempActive");
                statusSubtitle = string.Format(Loc.Get("TimerActive"), string.Format("{0:D2}:{1:D2}", remainingSeconds / 60, remainingSeconds % 60));
                statusBadgeVariant = StatusBadgeVariant.Warning;
                isProtected = false;
                isUnprotected = false;
            }
            else if (report.OverallState == ActivationState.AppliedAndVerified)
            {
                statusTitle = Loc.Get("StorageProtectedTitle");
                statusSubtitle = Loc.Get("StorageProtectedDesc");
                statusBadgeVariant = StatusBadgeVariant.Success;
                isProtected = true;
                isUnprotected = false;
            }
            else if (report.OverallState == ActivationState.ClassPolicyConfigured ||
                     report.OverallState == ActivationState.ClassPolicyActive ||
                     report.OverallState == ActivationState.StrictInstallationControl ||
                     report.OverallState == ActivationState.PendingReconnect)
            {
                statusTitle = Loc.Get("BadgeAttention");
                statusSubtitle = Loc.Get("ConflictClassStorageOverridePrompt");
                statusBadgeVariant = StatusBadgeVariant.Warning;
                isProtected = true;
                isUnprotected = false;
            }
            else if (report.OverallState == ActivationState.TrustedOnlyReactive ||
                     report.OverallState == ActivationState.ReactiveMonitoringActive)
            {
                statusTitle = Loc.Get("BadgeMonitoring");
                statusSubtitle = Loc.Get("RuleWhitelistModeDesc");
                statusBadgeVariant = StatusBadgeVariant.Info;
                isProtected = true;
                isUnprotected = false;
            }
            else if (report.OverallState == ActivationState.PendingRestart ||
                     report.OverallState == ActivationState.Failed)
            {
                statusTitle = Loc.Get("BadgeFailed");
                statusSubtitle = Loc.Get("BadgeFailed");
                statusBadgeVariant = StatusBadgeVariant.Danger;
                isProtected = false;
                isUnprotected = true;
            }
            else
            {
                // Unprotected / Normal unlocked
                statusTitle = Loc.Get("StorageUnlockedTitle");
                statusSubtitle = Loc.Get("StorageUnlockedDesc");
                statusBadgeVariant = StatusBadgeVariant.Neutral;
                isProtected = false;
                isUnprotected = true;
            }

            // Sync with v2.1 Modern Dashboard Components
            if (_protectionStatusCard != null)
            {
                int oldH = _protectionStatusCard.Height;
                _protectionStatusCard.SetStatus(
                    statusTitle,
                    statusSubtitle,
                    statusBadgeVariant,
                    isProtected,
                    isUnprotected,
                    tempActive);
                int neededH = _protectionStatusCard.GetPreferredHeight(_protectionStatusCard.Width > 0 ? _protectionStatusCard.Width : (_tabDashboard != null ? _tabDashboard.ClientSize.Width : DpiManager.ScaleInt(800)));
                if (oldH != neededH)
                {
                    LayoutDashboard(true);
                }
            }

            if (_tempAccessModule != null)
            {
                _tempAccessModule.SetState(isProtected, tempActive, remainingSeconds);
            }

            if (_protectionOverviewPanel != null)
            {
                _protectionOverviewPanel.UpdatePolicy(s.ActivePolicy, Loc.IsRtl);
            }

            if (_recentActivityPanel != null)
            {
                List<ActivityRecord> sourceRecords = TestIsolatedActivityRecords != null ? TestIsolatedActivityRecords : _activityRecords;
                _recentActivityPanel.SetRecords(sourceRecords, Loc.IsRtl);
            }

            if (_safetyMessageBar != null)
            {
                _safetyMessageBar.UpdateLanguage();
            }

            if (_metricConnectedDevices != null && string.IsNullOrEmpty(_metricConnectedDevices.ValueText))
            {
                _metricConnectedDevices.ValueText = "0";
            }
            if (_metricActiveRules != null)
            {
                if (s.ActivePolicy != null)
                {
                    int activeRules = 0;
                    if (s.ActivePolicy.BlockRemovableDisks || s.ActivePolicy.BlockAllRemovable) activeRules++;
                    if (s.ActivePolicy.BlockFlashDrivesOnlyReactive) activeRules++;
                    if (s.ActivePolicy.AutoPlayGuard) activeRules++;
                    if (s.ActivePolicy.BlockWpdPhonesAndMedia) activeRules++;
                    if (s.ActivePolicy.ReadOnlyRemovable) activeRules++;
                    if (s.ActivePolicy.StrictPnpLockdown) activeRules++;
                    if (s.ActivePolicy.WhitelistMode) activeRules++;
                    if (s.ActivePolicy.HardBlockUsbStor) activeRules++;
                    _metricActiveRules.ValueText = activeRules.ToString();
                }
            }
            if (_metricTrustedDevices != null)
            {
                try
                {
                    var trusted = (TestIsolatedWhitelistRules != null) ? TestIsolatedWhitelistRules : TrustedDeviceRepository.LoadRules();
                    _metricTrustedDevices.ValueText = (trusted != null ? trusted.Count : 0).ToString();
                }
                catch { _metricTrustedDevices.ValueText = "0"; }
            }

            UpdateProtectionPosture(p, report, tempActive);

            if (_tray != null)
            {
                _tray.UpdateTooltip("USB Shield Pro - " + statusTitle);
            }
        }

        private void ClockTimer_Tick(object sender, EventArgs e)
        {
            // 1. Temporary access countdown
            int remaining = TemporaryAccessManager.PeekRemainingSeconds();
            if (remaining > 0)
            {
                string displayTime = string.Format("{0:D2}:{1:D2}", remaining / 60, remaining % 60);
                if (_tempAccessModule != null)
                {
                    _tempAccessModule.SetState(false, true, remaining);
                }
                if (_protectionStatusCard != null)
                {
                    int oldH = _protectionStatusCard.Height;
                    _protectionStatusCard.SetStatus(
                        Loc.Get("BadgeTempActive"),
                        string.Format(Loc.Get("TimerActive"), displayTime),
                        StatusBadgeVariant.Warning,
                        false,
                        false,
                        true);
                    int neededH = _protectionStatusCard.GetPreferredHeight(_protectionStatusCard.Width > 0 ? _protectionStatusCard.Width : (_tabDashboard != null ? _tabDashboard.ClientSize.Width : DpiManager.ScaleInt(800)));
                    if (oldH != neededH)
                    {
                        LayoutDashboard(true);
                    }
                }
            }
            else if ((_tempAccessModule != null && _tempAccessModule.IsTempActive) || TemporaryAccessManager.IsActive)
            {
                if (EnforcementOwnerResolver.IsGuiEnforcementAllowed())
                {
                    if (System.Threading.Interlocked.CompareExchange(ref _isExpiringTempAccess, 1, 0) == 0)
                    {
                        System.Threading.Tasks.Task.Run(delegate
                        {
                            try
                            {
                                EnforcementOwnerResolver.MarkGuiWorkerActive();
                                string expErr;
                                TemporaryAccessManager.CheckAndExpireIfDue(out expErr);
                            }
                            catch { }
                            finally
                            {
                                EnforcementOwnerResolver.MarkGuiWorkerIdle();
                                System.Threading.Interlocked.Exchange(ref _isExpiringTempAccess, 0);
                                try
                                {
                                    if (this.IsHandleCreated && !this.IsDisposed)
                                    {
                                        this.BeginInvoke(new MethodInvoker(delegate { RefreshLiveStatus(); }));
                                    }
                                }
                                catch { }
                            }
                        });
                    }
                }
                else
                {
                    RefreshLiveStatus();
                }
            }

            // 2. Periodic background Sentinel scan (every 4 seconds) - ONLY if GUI is fallback enforcement owner
            _deviceRefreshCounter++;
            if (_deviceRefreshCounter >= 4)
            {
                _deviceRefreshCounter = 0;
                if (EnforcementOwnerResolver.IsGuiEnforcementAllowed())
                {
                    try
                    {
                        EnforcementOwnerResolver.MarkGuiWorkerActive();
                        AppSettingsData s = SettingsRepository.Load();
                        ActiveSentinel.EvaluateAndEnforceConnectedDevices(s.ActivePolicy, remaining > 0, "PeriodicSweep");
                    }
                    catch { }
                    finally
                    {
                        EnforcementOwnerResolver.MarkGuiWorkerIdle();
                    }
                }
            }
        }

        private static int _pnpPendingGeneration = 0;
        private static int _pnpWorkerGate = 0;
        private static string _lastPnpDispatchSource = "Unknown";

        private void OnHardwareDeviceChanged(int eventType, string dispatchSource = "StorageInterfaceSettleWake")
        {
            // Event Separation:
            // DBT_DEVICEARRIVAL (0x8000): triggers arrival discovery + Sentinel evaluation (if GUI owns enforcement)
            // DBT_DEVICEREMOVECOMPLETE (0x8004): triggers visual refresh ONLY; NEVER triggers ejection logic
            // DBT_DEVNODES_CHANGED (0x0007): coalesced visual refresh only
            bool isArrival = (eventType == DBT_DEVICEARRIVAL);
            _lastPnpDispatchSource = dispatchSource;

            // Increment generation counter for every event
            System.Threading.Interlocked.Increment(ref _pnpPendingGeneration);

            // Try to acquire the worker gate
            if (System.Threading.Interlocked.CompareExchange(ref _pnpWorkerGate, 1, 0) == 0)
            {
                System.Threading.Tasks.Task.Run(() => PnpWorkerLoop(isArrival, dispatchSource));
            }
        }

        private void PnpWorkerLoop(bool initialEventIsArrival, string initialDispatchSource = "StorageInterfaceSettleWake")
        {
            bool hasArrival = initialEventIsArrival;
            string dispatchSource = initialDispatchSource;
            while (true)
            {
                int observedGeneration = System.Threading.Volatile.Read(ref _pnpPendingGeneration);

                // Check enforcement ownership:
                EnforcementOwnerState ownerState = EnforcementOwnerResolver.ResolveOwner();
                bool canEnforce = EnforcementOwnerResolver.IsGuiEnforcementAllowed();

                if (hasArrival && canEnforce)
                {
                    try
                    {
                        EnforcementOwnerResolver.MarkGuiWorkerActive();
                        AppSettingsData s = SettingsRepository.Load();
                        if (s != null && s.ActivePolicy != null)
                        {
                            int remaining = TemporaryAccessManager.PeekRemainingSeconds();
                            var outcome = ActiveSentinel.EvaluateAndEnforceConnectedDevices(s.ActivePolicy, remaining > 0, dispatchSource);
                            // Note: Blind 4-pass reconciliation polling removed in production in favor of event-driven interface wake
                        }
                    }
                    catch { }
                    finally
                    {
                        EnforcementOwnerResolver.MarkGuiWorkerIdle();
                    }
                }
                else if (hasArrival && ownerState == EnforcementOwnerState.Service)
                {
                    try
                    {
                        var wakeResp = ServiceManager.SendIpcRequest("WAKESENTINEL", null, 500);
                        if (wakeResp == null || !wakeResp.Success)
                        {
                            EnforcementOwnerResolver.ResetCache();
                        }
                    }
                    catch
                    {
                        EnforcementOwnerResolver.ResetCache();
                    }
                }
                else if (hasArrival && ownerState == EnforcementOwnerState.ServiceDegraded)
                {
                    if (this.IsHandleCreated && !this.IsDisposed)
                    {
                        try
                        {
                            this.BeginInvoke(new Action(() =>
                            {
                                if (this.IsDisposed || !this.IsHandleCreated) return;
                                LogActivity(Loc.IsRtl
                                    ? "تحذير: تم اكتشاف توصيل جهاز ولكن خدمة الحماية في الخلفية غير مستجيبة."
                                    : "Warning: Device connected but background protection service is unresponsive.");
                                RefreshLiveStatus();
                            }));
                        }
                        catch { }
                    }
                }

                // Reset arrival flag for subsequent loops unless new events arrived
                hasArrival = false;
                dispatchSource = _lastPnpDispatchSource;

                // Visual refresh on UI thread (always performed safely for discovery)
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    try
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            if (this.IsDisposed || !this.IsHandleCreated) return;
                            if (_tabDevices != null && _tabDevices.Visible)
                            {
                                RefreshDevicesList();
                            }
                            RefreshLiveStatus();
                        }));
                    }
                    catch { }
                }

                // Deterministic Exit Protocol (Proof against lost wakeups):
                if (observedGeneration == System.Threading.Volatile.Read(ref _pnpPendingGeneration))
                {
                    // Release worker gate
                    System.Threading.Interlocked.Exchange(ref _pnpWorkerGate, 0);

                    // RE-CHECK pending generation AFTER gate release
                    int postReleaseGeneration = System.Threading.Volatile.Read(ref _pnpPendingGeneration);
                    if (postReleaseGeneration == observedGeneration)
                    {
                        // No new events occurred: safely exit worker
                        break;
                    }

                    // Event arrived between check and gate release: attempt to reacquire gate
                    if (System.Threading.Interlocked.CompareExchange(ref _pnpWorkerGate, 1, 0) == 0)
                    {
                        hasArrival = true; // Conservative arrival re-evaluation
                        continue;
                    }
                    else
                    {
                        // Another thread acquired gate and launched worker: safely exit
                        break;
                    }
                }
                else
                {
                    hasArrival = true;
                }
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Program.LogStartup("MAINFORM SHOWN");

            if (IsCaptureMode) return;

            // Check onboarding wizard only after MainForm is physically visible
            try
            {
                AppSettingsData settings = SettingsRepository.Load();
                if (!settings.OnboardingCompleted)
                {
                    ShowOnboardingWizard();
                }
            }
            catch (Exception ex)
            {
                Program.LogStartup("ON_SHOWN WIZARD EXCEPTION: " + ex.Message);
            }
        }

        private void ShowOnboardingWizard()
        {
            try
            {
                using (OnboardingWizardDialog wizard = new OnboardingWizardDialog())
                {
                    wizard.StartPosition = FormStartPosition.CenterParent;
                    if (wizard.ShowDialog(this) == DialogResult.OK)
                    {
                        AppSettingsData s = SettingsRepository.Load();
                        s.OnboardingCompleted = true;
                        SettingsRepository.Save(s);

                        string error;
                        ProtectionPolicy targetPolicy = wizard.SelectedPolicy ?? new ProtectionPolicy();
                        ApplyProtectionPolicyUnified(targetPolicy, Loc.Get("EventPolicyChanged"), out error);
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogStartup("SHOW_ONBOARDING_WIZARD EXCEPTION: " + ex.Message);
            }
        }

        private void RebuildUI()
        {
            int currentTab = (_sidebar != null) ? _sidebar.SelectedIndex : 0;
            BuildUI();
            if (_sidebar != null && currentTab < 7)
            {
                _sidebar.SelectedIndex = currentTab;
                SwitchTab(currentTab);
            }
            RefreshLiveStatus();
            RefreshDevicesList();
            RefreshWhitelistView();
            if (_tray != null) _tray.BuildMenu();

            // Explicitly force full layout and window bounds recalculation
            PerformLayout();
            OnResize(EventArgs.Empty);

            if (_contentPanel != null)
            {
                _contentPanel.Visible = true;
                if (_contentPanel.Width <= 0 || _contentPanel.Height <= 0)
                {
                    int topBarH = (_topBar != null && _topBar.Visible) ? _topBar.Height : 0;
                    int sideBarW = (_sidebar != null && _sidebar.Visible) ? _sidebar.Width : 0;
                    int cpW = Math.Max(100, ClientSize.Width - sideBarW - Padding.Horizontal);
                    int cpH = Math.Max(100, ClientSize.Height - topBarH - Padding.Vertical);
                    int cpX = (Loc.IsRtl) ? Padding.Left : (Padding.Left + sideBarW);
                    int cpY = Padding.Top + topBarH;
                    _contentPanel.SetBounds(cpX, cpY, cpW, cpH);
                }
                _contentPanel.PerformLayout();
                if (_contentPanel.Controls.Count > 0)
                {
                    Control activeTab = _contentPanel.Controls[0];
                    activeTab.Visible = true;
                    if (activeTab.Width <= 0 || activeTab.Height <= 0)
                    {
                        activeTab.SetBounds(0, 0, _contentPanel.ClientSize.Width, _contentPanel.ClientSize.Height);
                    }
                    activeTab.PerformLayout();
                    activeTab.BringToFront();
                }
            }

            Refresh();
            Invalidate(true);
        }

        public void ShowFromTray()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(ShowFromTray));
                return;
            }

            ShowInTaskbar = true;
            Visible = true;
            WindowState = FormWindowState.Normal;
            EnsureWindowWithinWorkingArea();

            if (IsHandleCreated)
            {
                try
                {
                    ShowWindow(Handle, SW_RESTORE);
                    SetForegroundWindow(Handle);
                }
                catch { }
            }

            try
            {
                TopMost = true;
                TopMost = false;
            }
            catch { }

            BringToFront();
            Activate();
            Refresh();
        }

        public void ExitApp()
        {
            if (!RequireAuth(Loc.Get("Exit"))) return;
            if (_tray != null) _tray.Dispose();
            Application.Exit();
        }

        public void PerformLock() { ExecuteQuickLock(); }
        public void PerformUnlock() { ExecuteQuickUnlock(); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!IsCaptureMode && e.CloseReason == CloseReason.UserClosing && SettingsRepository.Load().MinimizeToTray)
            {
                e.Cancel = true;
                WindowState = FormWindowState.Minimized;
                Hide();
                ShowInTaskbar = false;
                if (_tray != null) _tray.ShowBalloon(Loc.Get("AppTitle"), Loc.Get("MinToTray"));
                return;
            }
            try { StorageNotificationManager.Unregister(); } catch { }
            if (_registeredTrayWakeup != null)
            {
                try { _registeredTrayWakeup.Unregister(null); } catch { }
                _registeredTrayWakeup = null;
            }
            if (_trayWakeupEvent != null)
            {
                try { _trayWakeupEvent.Dispose(); } catch { }
                _trayWakeupEvent = null;
            }
            if (_tray != null) { _tray.Dispose(); _tray = null; }
            base.OnFormClosing(e);
        }
    }
}
