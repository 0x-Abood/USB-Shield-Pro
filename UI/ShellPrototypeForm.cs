#if !PRODUCTION
// ═══════════════════════════════════════════════════════════════════════════
// ShellPrototypeForm — v2.1 Charcoal Red Shell Visual Prototype
// USB Shield Pro v2.1
// QA & Design Prototype Harness (PRESENTATION ONLY — NO BACKEND ENFORCEMENT)
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace UsbShieldPro.UI
{
    public class ShellPrototypeForm : Form
    {
        private AppTopBar _topBar;
        private AppSidebar _sidebar;
        private Panel _contentPanel;

        // Header controls
        private Label _lblPageTitle;
        private Label _lblPageSubtitle;
        private AppButton _btnLock;
        private AppButton _btnUnlock;
        private AppButton _btnTempAccess;

        // Status Strip
        private ShellStatusStrip _statusStrip;

        // Metric Cards
        private ShellMetricCard _cardDevices;
        private ShellMetricCard _cardRules;
        private ShellMetricCard _cardTrusted;

        // Split Columns
        private SectionHeader _headerActivity;
        private SectionHeader _headerOverview;
        private ShellActivityList _panelActivity;
        private ShellProtectionOverview _panelOverview;
        private AppButton _btnViewAll;

        private bool _isArabic = false;

        public ShellPrototypeForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            Size = new Size(DpiManager.ScaleInt(1366), DpiManager.ScaleInt(768));
            MinimumSize = new Size(DpiManager.ScaleInt(1280), DpiManager.ScaleInt(720));

            _isArabic = (Loc.Current == Language.Arabic);
            RightToLeft = _isArabic ? RightToLeft.Yes : RightToLeft.No;

            BuildShell();
            ApplyPaletteColors();

            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public bool IsArabicMode
        {
            get { return _isArabic; }
            set
            {
                if (_isArabic != value)
                {
                    _isArabic = value;
                    Loc.Current = _isArabic ? Language.Arabic : Language.English;
                    RightToLeft = _isArabic ? RightToLeft.Yes : RightToLeft.No;
                    UpdateLanguageState();
                }
            }
        }

        public bool IsSidebarCompact
        {
            get { return _sidebar != null && _sidebar.IsCompact; }
            set
            {
                if (_sidebar != null && _sidebar.IsCompact != value)
                {
                    _sidebar.IsCompact = value;
                }
            }
        }

        public AppSidebar Sidebar
        {
            get { return _sidebar; }
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                ApplyPaletteColors();
                Invalidate(true);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }

        private void ApplyPaletteColors()
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            BackColor = pal.BackgroundPrimary;
            if (_contentPanel != null) _contentPanel.BackColor = pal.BackgroundPrimary;
            if (_lblPageTitle != null) _lblPageTitle.ForeColor = pal.TextPrimary;
            if (_lblPageSubtitle != null) _lblPageSubtitle.ForeColor = pal.TextMuted;

        }

        private void BuildShell()
        {
            SuspendLayout();
            Controls.Clear();

            IThemePalette pal = ThemeManager.ActivePalette;
            _isArabic = (RightToLeft == RightToLeft.Yes);

            // ─── 1. Top Bar ──────────────────────────────────────────────────
            _topBar = new AppTopBar();
            _topBar.BrandTitle = "USB SHIELD PRO";
            _topBar.VersionText = "v2.1";
            _topBar.PostureBadge.Text = _isArabic ? "محمي" : "PROTECTED";
            _topBar.PostureBadge.Variant = StatusBadgeVariant.Success;
            _topBar.LanguageButton.Text = _isArabic ? "AR" : "EN";

            _topBar.CloseClicked += delegate { Close(); };
            _topBar.MinimizeClicked += delegate { WindowState = FormWindowState.Minimized; };
            _topBar.LanguageToggleClicked += delegate { ToggleLanguage(); };

            Controls.Add(_topBar);

            // ─── 2. Navigation Sidebar ───────────────────────────────────────
            _sidebar = new AppSidebar();
            _sidebar.Dock = _isArabic ? DockStyle.Right : DockStyle.Left;
            PopulateSidebarItems();
            _sidebar.SelectedIndex = 0; // Dashboard selected
            _sidebar.CompactChanged += delegate { LayoutDashboard(); };
            Controls.Add(_sidebar);

            // ─── 3. Main Content Container ───────────────────────────────────
            _contentPanel = new Panel();
            _contentPanel.Dock = DockStyle.Fill;
            _contentPanel.BackColor = pal.BackgroundPrimary;
            Controls.Add(_contentPanel);
            _contentPanel.BringToFront();

            // ─── 4. Header Labels & Quick Actions ────────────────────────────
            _lblPageTitle = new Label();
            _lblPageTitle.AutoSize = true;
            _lblPageTitle.Font = DpiManager.GetFont(TypographyRole.Display, _isArabic);
            _lblPageTitle.ForeColor = pal.TextPrimary;
            _lblPageTitle.Text = _isArabic ? "لوحة التحكم" : "Dashboard";
            _contentPanel.Controls.Add(_lblPageTitle);

            _lblPageSubtitle = new Label();
            _lblPageSubtitle.AutoSize = true;
            _lblPageSubtitle.Font = DpiManager.GetFont(TypographyRole.Caption, _isArabic);
            _lblPageSubtitle.ForeColor = pal.TextMuted;
            _lblPageSubtitle.Text = _isArabic
                ? "حالة الحماية الحالية ونظرة عامة على الأجهزة المتصلة."
                : "Current protection status and connected device overview.";
            _contentPanel.Controls.Add(_lblPageSubtitle);

            // Quick Actions: Primary "Lock Storage", Secondary "Quick Unlock", Secondary "Temporary Access"
            _btnLock = new AppButton();
            _btnLock.Variant = ButtonVariant.Primary;
            _btnLock.SizeVariant = ButtonSize.Medium;
            _btnLock.Text = _isArabic ? "قفل التخزين" : "Lock Storage";
            _btnLock.Width = DpiManager.ScaleInt(125);
            _contentPanel.Controls.Add(_btnLock);

            _btnUnlock = new AppButton();
            _btnUnlock.Variant = ButtonVariant.Secondary;
            _btnUnlock.SizeVariant = ButtonSize.Medium;
            _btnUnlock.Text = _isArabic ? "إلغاء قفل سريع" : "Quick Unlock";
            _btnUnlock.Width = DpiManager.ScaleInt(120);
            _contentPanel.Controls.Add(_btnUnlock);

            _btnTempAccess = new AppButton();
            _btnTempAccess.Variant = ButtonVariant.Secondary;
            _btnTempAccess.SizeVariant = ButtonSize.Medium;
            _btnTempAccess.Text = _isArabic ? "وصول مؤقت (15 د)" : "Temporary Access (15m)";
            _btnTempAccess.Width = _isArabic ? DpiManager.ScaleInt(165) : DpiManager.ScaleInt(205);
            _contentPanel.Controls.Add(_btnTempAccess);

            // ─── 5. Protection Status Strip ──────────────────────────────────
            _statusStrip = new ShellStatusStrip();
            _statusStrip.UpdateLanguage(_isArabic);
            _contentPanel.Controls.Add(_statusStrip);

            // ─── 6. Summary Metric Cards (3 Cards) ───────────────────────────
            _cardDevices = new ShellMetricCard();
            _cardDevices.ValueText = "2";
            _cardDevices.TitleText = _isArabic ? "الأجهزة المتصلة" : "Connected Devices";
            _cardDevices.SubtitleText = _isArabic ? "1 مصرح به، 1 للقراءة فقط" : "1 authorized, 1 read-only";
            _cardDevices.BadgeText = _isArabic ? "تم اكتشاف 2" : "2 DETECTED";
            _cardDevices.BadgeVariant = StatusBadgeVariant.Info;
            _contentPanel.Controls.Add(_cardDevices);

            _cardRules = new ShellMetricCard();
            _cardRules.ValueText = "5";
            _cardRules.TitleText = _isArabic ? "القواعد النشطة" : "Active Rules";
            _cardRules.SubtitleText = _isArabic ? "قفل التخزين، حظر التشغيل التلقائي" : "Storage lock, AutoPlay blocked";
            _cardRules.BadgeText = _isArabic ? "قيد الفرض" : "ENFORCING";
            _cardRules.BadgeVariant = StatusBadgeVariant.Success;
            _contentPanel.Controls.Add(_cardRules);

            _cardTrusted = new ShellMetricCard();
            _cardTrusted.ValueText = "1";
            _cardTrusted.TitleText = _isArabic ? "الأجهزة الموثوقة" : "Trusted Devices";
            _cardTrusted.SubtitleText = _isArabic ? "تم تسجيل معرّف العتاد" : "Hardware ID enrolled";
            _cardTrusted.BadgeText = _isArabic ? "تم التحقق" : "VERIFIED";
            _cardTrusted.BadgeVariant = StatusBadgeVariant.Success;
            _contentPanel.Controls.Add(_cardTrusted);

            // ─── 7. Two Split Content Columns ────────────────────────────────
            _headerActivity = new SectionHeader();
            _headerActivity.Title = _isArabic ? "سجل النشاط الحديث" : "Recent Activity";
            _btnViewAll = new AppButton();
            _btnViewAll.Variant = ButtonVariant.Ghost;
            _btnViewAll.SizeVariant = ButtonSize.Small;
            _btnViewAll.Text = _isArabic ? "عرض الكل" : "View All";
            _btnViewAll.Width = DpiManager.ScaleInt(80);
            _headerActivity.TrailingAction = _btnViewAll;
            _contentPanel.Controls.Add(_headerActivity);

            _panelActivity = new ShellActivityList();
            _panelActivity.UpdateLanguage(_isArabic);
            _contentPanel.Controls.Add(_panelActivity);

            _headerOverview = new SectionHeader();
            _headerOverview.Title = _isArabic ? "نظرة عامة على الحماية" : "Protection Overview";
            _contentPanel.Controls.Add(_headerOverview);

            _panelOverview = new ShellProtectionOverview();
            _panelOverview.UpdateLanguage(_isArabic);
            _contentPanel.Controls.Add(_panelOverview);

            _contentPanel.Resize += delegate { LayoutDashboard(); };
            LayoutDashboard();

            ResumeLayout(true);
        }

        private void PopulateSidebarItems()
        {
            int prevSel = _sidebar.SelectedIndex;
            _sidebar.Items.Clear();
            if (_isArabic)
            {
                _sidebar.Items.Add(new SidebarNavigationItem(0, "لوحة التحكم", "نظرة عامة"));
                var dev = new SidebarNavigationItem(1, "الأجهزة المتصلة", "نظرة عامة");
                dev.BadgeText = "2";
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
                dev.BadgeText = "2";
                _sidebar.Items.Add(dev);
                _sidebar.Items.Add(new SidebarNavigationItem(2, "Protection", "Protection"));
                _sidebar.Items.Add(new SidebarNavigationItem(3, "Trusted Devices", "Protection"));
                _sidebar.Items.Add(new SidebarNavigationItem(4, "Activity", "Monitoring"));
                _sidebar.Items.Add(new SidebarNavigationItem(5, "Recovery", "System"));
                _sidebar.Items.Add(new SidebarNavigationItem(6, "Settings", "System"));
            }
            if (prevSel >= 0 && prevSel < _sidebar.Items.Count)
            {
                _sidebar.SelectedIndex = prevSel;
            }
            _sidebar.Invalidate();
        }

        private void ToggleLanguage()
        {
            IsArabicMode = !_isArabic;
        }

        private void UpdateLanguageState()
        {
            _isArabic = (RightToLeft == RightToLeft.Yes);

            // Re-dock sidebar
            _sidebar.Dock = _isArabic ? DockStyle.Right : DockStyle.Left;
            PopulateSidebarItems();

            _topBar.PostureBadge.Text = _isArabic ? "محمي" : "PROTECTED";
            _topBar.LanguageButton.Text = _isArabic ? "AR" : "EN";

            _lblPageTitle.Font = DpiManager.GetFont(TypographyRole.Display, _isArabic);
            _lblPageTitle.Text = _isArabic ? "لوحة التحكم" : "Dashboard";

            _lblPageSubtitle.Font = DpiManager.GetFont(TypographyRole.Caption, _isArabic);
            _lblPageSubtitle.Text = _isArabic
                ? "حالة الحماية الحالية ونظرة عامة على الأجهزة المتصلة."
                : "Current protection status and connected device overview.";

            _btnLock.Text = _isArabic ? "قفل التخزين" : "Lock Storage";
            _btnUnlock.Text = _isArabic ? "إلغاء قفل سريع" : "Quick Unlock";
            _btnTempAccess.Text = _isArabic ? "وصول مؤقت (15 د)" : "Temporary Access (15m)";
            _btnTempAccess.Width = _isArabic ? DpiManager.ScaleInt(165) : DpiManager.ScaleInt(205);

            _statusStrip.UpdateLanguage(_isArabic);

            _cardDevices.TitleText = _isArabic ? "الأجهزة المتصلة" : "Connected Devices";
            _cardDevices.SubtitleText = _isArabic ? "1 مصرح به، 1 للقراءة فقط" : "1 authorized, 1 read-only";
            _cardDevices.BadgeText = _isArabic ? "تم اكتشاف 2" : "2 DETECTED";

            _cardRules.TitleText = _isArabic ? "القواعد النشطة" : "Active Rules";
            _cardRules.SubtitleText = _isArabic ? "قفل التخزين، حظر التشغيل التلقائي" : "Storage lock, AutoPlay blocked";
            _cardRules.BadgeText = _isArabic ? "قيد الفرض" : "ENFORCING";

            _cardTrusted.TitleText = _isArabic ? "الأجهزة الموثوقة" : "Trusted Devices";
            _cardTrusted.SubtitleText = _isArabic ? "تم تسجيل معرّف العتاد" : "Hardware ID enrolled";
            _cardTrusted.BadgeText = _isArabic ? "تم التحقق" : "VERIFIED";

            _headerActivity.Title = _isArabic ? "سجل النشاط الحديث" : "Recent Activity";
            _btnViewAll.Text = _isArabic ? "عرض الكل" : "View All";
            _panelActivity.UpdateLanguage(_isArabic);

            _headerOverview.Title = _isArabic ? "نظرة عامة على الحماية" : "Protection Overview";
            _panelOverview.UpdateLanguage(_isArabic);

            LayoutDashboard();
            Invalidate(true);
        }

        private void LayoutDashboard()
        {
            if (_contentPanel == null || _lblPageTitle == null) return;

            int padX = DpiManager.ScaleInt(SpacingTokens.Space24);
            int padY = DpiManager.ScaleInt(20);
            int availW = _contentPanel.ClientSize.Width - (padX * 2);
            if (availW <= 0) return;

            int curY = padY;
            bool isRtl = (_contentPanel.RightToLeft == RightToLeft.Yes);

            // ─── Row 1: Header + Quick Action Buttons ────────────────────────
            int titleH = _lblPageTitle.Height;
            int subH = _lblPageSubtitle.Height;
            int btnH = _btnLock.Height;
            int headerH = Math.Max(titleH + subH + DpiManager.ScaleInt(SpacingTokens.Space4), btnH);

            if (isRtl)
            {
                // In RTL, Title & Subtitle are on the RIGHT
                int titleX = _contentPanel.ClientSize.Width - padX - _lblPageTitle.Width;
                _lblPageTitle.Location = new Point(titleX, curY);

                int subX = _contentPanel.ClientSize.Width - padX - _lblPageSubtitle.Width;
                _lblPageSubtitle.Location = new Point(subX, curY + titleH + DpiManager.ScaleInt(SpacingTokens.Space4));

                // Quick Action Buttons on the LEFT
                int btnY = curY + (headerH - btnH) / 2;
                int curBtnX = padX;

                _btnLock.Location = new Point(curBtnX, btnY);
                curBtnX += _btnLock.Width + DpiManager.ScaleInt(SpacingTokens.Space8);

                _btnUnlock.Location = new Point(curBtnX, btnY);
                curBtnX += _btnUnlock.Width + DpiManager.ScaleInt(SpacingTokens.Space8);

                _btnTempAccess.Location = new Point(curBtnX, btnY);
            }
            else
            {
                // In LTR, Title & Subtitle are on the LEFT
                _lblPageTitle.Location = new Point(padX, curY);
                _lblPageSubtitle.Location = new Point(padX, curY + titleH + DpiManager.ScaleInt(SpacingTokens.Space4));

                // Quick Action Buttons on the RIGHT
                int btnY = curY + (headerH - btnH) / 2;
                int rightEdge = padX + availW;

                _btnTempAccess.Location = new Point(rightEdge - _btnTempAccess.Width, btnY);
                rightEdge -= (_btnTempAccess.Width + DpiManager.ScaleInt(SpacingTokens.Space8));

                _btnUnlock.Location = new Point(rightEdge - _btnUnlock.Width, btnY);
                rightEdge -= (_btnUnlock.Width + DpiManager.ScaleInt(SpacingTokens.Space8));

                _btnLock.Location = new Point(rightEdge - _btnLock.Width, btnY);
            }

            curY += headerH + DpiManager.ScaleInt(SpacingTokens.Space16);

            // ─── Row 2: Protection Status Strip ──────────────────────────────
            int stripH = DpiManager.ScaleInt(46);
            _statusStrip.Location = new Point(padX, curY);
            _statusStrip.Size = new Size(availW, stripH);

            curY += stripH + DpiManager.ScaleInt(SpacingTokens.Space16);

            // ─── Row 3: Summary Metric Cards (3 Cards) ───────────────────────
            int cardGap = DpiManager.ScaleInt(SpacingTokens.Space16);
            int cardH = DpiManager.ScaleInt(74);
            int cardW = (availW - (cardGap * 2)) / 3;

            if (isRtl)
            {
                // In RTL, Card 1 (Devices) is on the right, Card 3 (Trusted) is on the left
                _cardDevices.Location = new Point(padX + (cardW + cardGap) * 2, curY);
                _cardDevices.Size = new Size(cardW, cardH);

                _cardRules.Location = new Point(padX + cardW + cardGap, curY);
                _cardRules.Size = new Size(cardW, cardH);

                _cardTrusted.Location = new Point(padX, curY);
                _cardTrusted.Size = new Size(cardW, cardH);
            }
            else
            {
                // In LTR, Card 1 (Devices) is on the left, Card 3 (Trusted) is on the right
                _cardDevices.Location = new Point(padX, curY);
                _cardDevices.Size = new Size(cardW, cardH);

                _cardRules.Location = new Point(padX + cardW + cardGap, curY);
                _cardRules.Size = new Size(cardW, cardH);

                _cardTrusted.Location = new Point(padX + (cardW + cardGap) * 2, curY);
                _cardTrusted.Size = new Size(cardW, cardH);
            }

            curY += cardH + DpiManager.ScaleInt(SpacingTokens.Space16);

            // ─── Row 4: Two Split Content Columns ────────────────────────────
            int sectionH = DpiManager.ScaleInt(34);
            int colGap = DpiManager.ScaleInt(SpacingTokens.Space16);
            int col1W = (int)Math.Round(availW * 0.585f);
            int col2W = availW - col1W - colGap;
            int bottomAvailH = _contentPanel.ClientSize.Height - curY - padY - sectionH;
            int listH = Math.Max(bottomAvailH, DpiManager.ScaleInt(280));

            if (isRtl)
            {
                // In RTL: Recent Activity (~58.5%) on the RIGHT, Protection Overview (~41.5%) on the LEFT
                int rightColX = padX + col2W + colGap;

                _headerActivity.Location = new Point(rightColX, curY);
                _headerActivity.Size = new Size(col1W, sectionH);
                _panelActivity.Location = new Point(rightColX, curY + sectionH);
                _panelActivity.Size = new Size(col1W, listH);

                _headerOverview.Location = new Point(padX, curY);
                _headerOverview.Size = new Size(col2W, sectionH);
                _panelOverview.Location = new Point(padX, curY + sectionH);
                _panelOverview.Size = new Size(col2W, listH);
            }
            else
            {
                // In LTR: Recent Activity (~58.5%) on the LEFT, Protection Overview (~41.5%) on the RIGHT
                _headerActivity.Location = new Point(padX, curY);
                _headerActivity.Size = new Size(col1W, sectionH);
                _panelActivity.Location = new Point(padX, curY + sectionH);
                _panelActivity.Size = new Size(col1W, listH);

                _headerOverview.Location = new Point(padX + col1W + colGap, curY);
                _headerOverview.Size = new Size(col2W, sectionH);
                _panelOverview.Location = new Point(padX + col1W + colGap, curY + sectionH);
                _panelOverview.Size = new Size(col2W, listH);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ShellStatusStrip — Compact Horizontal Policy Posture Banner
    // ═══════════════════════════════════════════════════════════════════════════
    internal class ShellStatusStrip : Control
    {
        private bool _isArabic = false;

        public ShellStatusStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            TabStop = false;
            ThemeManager.ThemeChanged += delegate { Invalidate(); };
        }

        public void UpdateLanguage(bool isArabic)
        {
            _isArabic = isArabic;
            RightToLeft = _isArabic ? RightToLeft.Yes : RightToLeft.No;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);

            // Background & Border
            using (SolidBrush bg = new SolidBrush(pal.Surface))
            {
                UiDrawingUtils.FillRoundedRectangle(g, bg, rect, rad);
            }
            using (Pen borderPen = new Pen(pal.BorderSubtle, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, borderPen, rect, rad);
            }

            int pad = DpiManager.ScaleInt(SpacingTokens.Space16);
            Font badgeFont = DpiManager.GetFont(TypographyRole.Caption, _isArabic);
            Font descFont = DpiManager.GetFont(TypographyRole.Body, _isArabic);
            Font metaFont = DpiManager.GetFont(TypographyRole.Caption, _isArabic);

            string badgeText = _isArabic ? "الحماية نشطة" : "Protection Active";
            string descText = _isArabic
                ? "فرض السياسة الحالية — سيتم حظر أجهزة تخزين USB غير المصرح بها بأمان."
                : "Enforcing current policy — unauthorized USB storage devices will be safely blocked.";
            string metaText = _isArabic
                ? "السياسة v2.1 • جميع قواعد الحراسة نشطة"
                : "Policy v2.1 • All Sentinel rules active";

            // Draw Badge
            int badgeH = DpiManager.ScaleInt(22);
            Size badgeSize = TextRenderer.MeasureText(g, badgeText, badgeFont);
            int dotPad = DpiManager.ScaleInt(16);
            int badgeW = badgeSize.Width + dotPad + DpiManager.ScaleInt(14);
            int badgeY = (Height - badgeH) / 2;

            if (_isArabic)
            {
                // Leading edge is on the RIGHT
                int badgeX = Width - pad - badgeW;
                Rectangle badgeRect = new Rectangle(badgeX, badgeY, badgeW, badgeH);

                // Fill badge soft bg
                using (SolidBrush bb = new SolidBrush(pal.SuccessSoft))
                {
                    UiDrawingUtils.FillRoundedRectangle(g, bb, badgeRect, badgeH / 2);
                }
                using (Pen bp = new Pen(pal.Success, 1f))
                {
                    UiDrawingUtils.DrawRoundedRectangle(g, bp, badgeRect, badgeH / 2);
                }

                // Dot on right of badge
                int dotSize = DpiManager.ScaleInt(6);
                int dotX = badgeRect.Right - DpiManager.ScaleInt(10);
                int dotY = badgeRect.Y + (badgeH - dotSize) / 2;
                using (SolidBrush dotBrush = new SolidBrush(pal.Success))
                {
                    g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
                }

                // Badge text
                Rectangle badgeTextRect = new Rectangle(badgeRect.X + DpiManager.ScaleInt(4), badgeRect.Y, badgeRect.Width - DpiManager.ScaleInt(16), badgeH);
                TextRenderer.DrawText(g, badgeText, badgeFont, badgeTextRect, pal.Success,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.RightToLeft);

                // Meta text on far left
                Size metaSz = TextRenderer.MeasureText(g, metaText, metaFont);
                int metaW = metaSz.Width + DpiManager.ScaleInt(16);
                Rectangle metaRect = new Rectangle(pad, 0, metaW, Height);
                TextRenderer.DrawText(g, metaText, metaFont, metaRect, pal.TextMuted,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);

                // Description text between meta and badge
                int descX = pad + metaW + DpiManager.ScaleInt(SpacingTokens.Space12);
                int descW = badgeX - descX - DpiManager.ScaleInt(SpacingTokens.Space12);
                Rectangle descRect = new Rectangle(descX, 0, descW, Height);
                TextRenderer.DrawText(g, descText, descFont, descRect, pal.TextSecondary,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix);
            }
            else
            {
                // Leading edge is on the LEFT
                int badgeX = pad;
                Rectangle badgeRect = new Rectangle(badgeX, badgeY, badgeW, badgeH);

                using (SolidBrush bb = new SolidBrush(pal.SuccessSoft))
                {
                    UiDrawingUtils.FillRoundedRectangle(g, bb, badgeRect, badgeH / 2);
                }
                using (Pen bp = new Pen(pal.Success, 1f))
                {
                    UiDrawingUtils.DrawRoundedRectangle(g, bp, badgeRect, badgeH / 2);
                }

                int dotSize = DpiManager.ScaleInt(6);
                int dotX = badgeRect.X + DpiManager.ScaleInt(8);
                int dotY = badgeRect.Y + (badgeH - dotSize) / 2;
                using (SolidBrush dotBrush = new SolidBrush(pal.Success))
                {
                    g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
                }

                Rectangle badgeTextRect = new Rectangle(dotX + dotSize + DpiManager.ScaleInt(4), badgeRect.Y, badgeW - dotSize - DpiManager.ScaleInt(14), badgeH);
                TextRenderer.DrawText(g, badgeText, badgeFont, badgeTextRect, pal.Success,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);

                // Meta text on far right
                Size metaSz = TextRenderer.MeasureText(g, metaText, metaFont);
                int metaW = metaSz.Width + DpiManager.ScaleInt(16);
                Rectangle metaRect = new Rectangle(Width - pad - metaW, 0, metaW, Height);
                TextRenderer.DrawText(g, metaText, metaFont, metaRect, pal.TextMuted,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPrefix);

                // Description text to the right of the badge
                int descX = badgeX + badgeW + DpiManager.ScaleInt(SpacingTokens.Space12);
                int descW = (Width - pad - metaW - DpiManager.ScaleInt(SpacingTokens.Space12)) - descX;
                Rectangle descRect = new Rectangle(descX, 0, descW, Height);
                TextRenderer.DrawText(g, descText, descFont, descRect, pal.TextSecondary,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ShellMetricCard — Surface Stat Card with Value, Title, Subtitle, and Badge
    // ═══════════════════════════════════════════════════════════════════════════
    internal class ShellMetricCard : Control
    {
        private string _valueText = "0";
        private string _titleText = "";
        private string _subtitleText = "";
        private string _badgeText = "";
        private StatusBadgeVariant _badgeVariant = StatusBadgeVariant.Neutral;
        private bool _isHovered = false;

        public ShellMetricCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            TabStop = false;
            ThemeManager.ThemeChanged += delegate { Invalidate(); };
        }

        public string ValueText
        {
            get { return _valueText; }
            set { if (_valueText != value) { _valueText = value; Invalidate(); } }
        }

        public string TitleText
        {
            get { return _titleText; }
            set { if (_titleText != value) { _titleText = value; Invalidate(); } }
        }

        public string SubtitleText
        {
            get { return _subtitleText; }
            set { if (_subtitleText != value) { _subtitleText = value; Invalidate(); } }
        }

        public string BadgeText
        {
            get { return _badgeText; }
            set { if (_badgeText != value) { _badgeText = value; Invalidate(); } }
        }

        public StatusBadgeVariant BadgeVariant
        {
            get { return _badgeVariant; }
            set { if (_badgeVariant != value) { _badgeVariant = value; Invalidate(); } }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);

            // Background & Border
            Color bgColor = _isHovered ? pal.SurfaceElevated : pal.Surface;
            using (SolidBrush bg = new SolidBrush(bgColor))
            {
                UiDrawingUtils.FillRoundedRectangle(g, bg, rect, rad);
            }
            using (Pen borderPen = new Pen(_isHovered ? pal.Border : pal.BorderSubtle, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, borderPen, rect, rad);
            }

            int pad = DpiManager.ScaleInt(SpacingTokens.Space16);
            Font valFont = DpiManager.GetFont(TypographyRole.Display, isRtl);
            Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            Font subFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            // Measure value
            Size valSize = TextRenderer.MeasureText(g, _valueText, valFont);
            int valW = Math.Max(valSize.Width, DpiManager.ScaleInt(28));

            Font badgeFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            int badgeH = DpiManager.ScaleInt(20);
            Size sz = TextRenderer.MeasureText(g, _badgeText ?? "", badgeFont);
            int badgeW = sz.Width + DpiManager.ScaleInt(14);
            int badgeY = DpiManager.ScaleInt(14);

            if (isRtl)
            {
                // In RTL, Value on far right
                int valX = Width - pad - valW;
                Rectangle valRect = new Rectangle(valX, 0, valW, Height);
                TextRenderer.DrawText(g, _valueText, valFont, valRect, pal.TextPrimary,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix);

                // Title on top row next to value
                int textRight = valX - DpiManager.ScaleInt(SpacingTokens.Space12);
                int badgeX = pad;
                int titleLeft = badgeX + badgeW + DpiManager.ScaleInt(SpacingTokens.Space8);
                int titleW = Math.Max(20, textRight - titleLeft);
                int titleY = DpiManager.ScaleInt(14);

                Rectangle titleRect = new Rectangle(titleLeft, titleY, titleW, titleFont.Height);
                TextRenderer.DrawText(g, _titleText, titleFont, titleRect, pal.TextPrimary,
                    TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix);

                // Subtitle on second row spanning full width under title/badge
                int subY = titleY + titleFont.Height + DpiManager.ScaleInt(4);
                int subW = Math.Max(20, textRight - pad);
                Rectangle subRect = new Rectangle(pad, subY, subW, subFont.Height);
                TextRenderer.DrawText(g, _subtitleText, subFont, subRect, pal.TextMuted,
                    TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix);

                // Badge on far left top row
                if (!string.IsNullOrEmpty(_badgeText))
                {
                    DrawBadgePill(g, pal, badgeX, badgeY, badgeW, badgeH, isRtl);
                }
            }
            else
            {
                // In LTR, Value on far left
                Rectangle valRect = new Rectangle(pad, 0, valW, Height);
                TextRenderer.DrawText(g, _valueText, valFont, valRect, pal.TextPrimary,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);

                // Title on top row next to value
                int textLeft = pad + valW + DpiManager.ScaleInt(SpacingTokens.Space12);
                int badgeX = Width - pad - badgeW;
                int titleW = Math.Max(20, badgeX - DpiManager.ScaleInt(SpacingTokens.Space8) - textLeft);
                int titleY = DpiManager.ScaleInt(14);

                Rectangle titleRect = new Rectangle(textLeft, titleY, titleW, titleFont.Height);
                TextRenderer.DrawText(g, _titleText, titleFont, titleRect, pal.TextPrimary,
                    TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.NoPrefix);

                // Subtitle on second row spanning full width
                int subY = titleY + titleFont.Height + DpiManager.ScaleInt(4);
                int subW = Math.Max(20, Width - pad - textLeft);
                Rectangle subRect = new Rectangle(textLeft, subY, subW, subFont.Height);
                TextRenderer.DrawText(g, _subtitleText, subFont, subRect, pal.TextMuted,
                    TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.NoPrefix);

                // Badge on far right top row
                if (!string.IsNullOrEmpty(_badgeText))
                {
                    DrawBadgePill(g, pal, badgeX, badgeY, badgeW, badgeH, isRtl);
                }
            }
        }

        private void DrawBadgePill(Graphics g, IThemePalette pal, int x, int y, int badgeW, int badgeH, bool isRtl)
        {
            Font badgeFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            Rectangle bRect = new Rectangle(x, y, badgeW, badgeH);

            Color fg = pal.TextPrimary;
            Color bg = pal.SurfaceElevated;

            switch (_badgeVariant)
            {
                case StatusBadgeVariant.Success:
                    fg = pal.Success;
                    bg = pal.SuccessSoft;
                    break;
                case StatusBadgeVariant.Info:
                    fg = pal.Info;
                    bg = pal.InfoSoft;
                    break;
                case StatusBadgeVariant.Warning:
                    fg = pal.Warning;
                    bg = pal.WarningSoft;
                    break;
                case StatusBadgeVariant.Danger:
                    fg = pal.Danger;
                    bg = pal.DangerSoft;
                    break;
            }

            using (SolidBrush b = new SolidBrush(bg))
            {
                UiDrawingUtils.FillRoundedRectangle(g, b, bRect, badgeH / 2);
            }
            using (Pen p = new Pen(fg, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, p, bRect, badgeH / 2);
            }

            bool isArabic = UiDrawingUtils.IsArabicText(_badgeText, isRtl);
            int yOffset = UiDrawingUtils.GetBadgeTextVerticalOffset(isArabic);
            Rectangle textRect = new Rectangle(x, y + yOffset, badgeW, badgeH);
            TextRenderer.DrawText(g, _badgeText, badgeFont, textRect, fg,
                TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ShellActivityList — Flat Table Panel for 4 Recent Security Events
    // ═══════════════════════════════════════════════════════════════════════════
    internal class ShellActivityList : Control
    {
        private class ActivityItem
        {
            public string TimeEn { get; set; }
            public string TimeAr { get; set; }
            public StatusBadgeVariant Variant { get; set; }
            public string BadgeEn { get; set; }
            public string BadgeAr { get; set; }
            public string TitleEn { get; set; }
            public string TitleAr { get; set; }
            public string DescEn { get; set; }
            public string DescAr { get; set; }
        }

        private readonly List<ActivityItem> _items = new List<ActivityItem>();
        private int _hoveredIndex = -1;
        private bool _isArabic = false;

        public ShellActivityList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            TabStop = false;
            InitializeEvents();
            ThemeManager.ThemeChanged += delegate { Invalidate(); };
        }

        private void InitializeEvents()
        {
            _items.Add(new ActivityItem
            {
                TimeEn = "10:42 AM",
                TimeAr = "10:42 ص",
                Variant = StatusBadgeVariant.Danger,
                BadgeEn = "BLOCKED",
                BadgeAr = "محظور",
                TitleEn = "Kingston DataTraveler 3.0",
                TitleAr = "Kingston DataTraveler 3.0",
                DescEn = "Unauthorized storage device blocked by policy",
                DescAr = "تم حظر جهاز تخزين غير مصرح به بموجب السياسة"
            });

            _items.Add(new ActivityItem
            {
                TimeEn = "10:15 AM",
                TimeAr = "10:15 ص",
                Variant = StatusBadgeVariant.Success,
                BadgeEn = "TRUSTED",
                BadgeAr = "موثوق",
                TitleEn = "SanDisk Ultra USB 3.0",
                TitleAr = "SanDisk Ultra USB 3.0",
                DescEn = "Hardware ID matched trusted device list",
                DescAr = "تطابق معرّف العتاد مع قائمة الأجهزة الموثوقة"
            });

            _items.Add(new ActivityItem
            {
                TimeEn = "09:30 AM",
                TimeAr = "09:30 ص",
                Variant = StatusBadgeVariant.Info,
                BadgeEn = "READ-ONLY",
                BadgeAr = "قراءة فقط",
                TitleEn = "Samsung BAR Plus",
                TitleAr = "Samsung BAR Plus",
                DescEn = "Enforced write-protection mode",
                DescAr = "تم فرض وضع الحماية من الكتابة"
            });

            _items.Add(new ActivityItem
            {
                TimeEn = "08:00 AM",
                TimeAr = "08:00 ص",
                Variant = StatusBadgeVariant.Success,
                BadgeEn = "ENGINE",
                BadgeAr = "المحرك",
                TitleEn = "Sentinel Engine Started",
                TitleAr = "بدء تشغيل محرك الحراسة",
                DescEn = "Protection rules initialized and active",
                DescAr = "تمت تهيئة قواعد الحماية وتنشيطها"
            });
        }

        public void UpdateLanguage(bool isArabic)
        {
            _isArabic = isArabic;
            RightToLeft = _isArabic ? RightToLeft.Yes : RightToLeft.No;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int rowH = Height / Math.Max(1, _items.Count);
            int idx = e.Y / Math.Max(1, rowH);
            if (idx >= _items.Count) idx = -1;

            if (_hoveredIndex != idx)
            {
                _hoveredIndex = idx;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredIndex != -1)
            {
                _hoveredIndex = -1;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);

            // Container Panel Background & Border
            using (SolidBrush bg = new SolidBrush(pal.Surface))
            {
                UiDrawingUtils.FillRoundedRectangle(g, bg, rect, rad);
            }
            using (Pen borderPen = new Pen(pal.BorderSubtle, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, borderPen, rect, rad);
            }

            int count = _items.Count;
            if (count == 0) return;

            int rowH = Height / count;
            int pad = DpiManager.ScaleInt(SpacingTokens.Space16);

            Font timeFont = DpiManager.GetFont(TypographyRole.Caption, _isArabic);
            Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, _isArabic);
            Font descFont = DpiManager.GetFont(TypographyRole.Caption, _isArabic);
            Font badgeFont = DpiManager.GetFont(TypographyRole.Caption, _isArabic);

            for (int i = 0; i < count; i++)
            {
                var item = _items[i];
                Rectangle rowRect = new Rectangle(1, i * rowH + 1, Width - 2, rowH - 1);

                // Row hover tint
                if (i == _hoveredIndex)
                {
                    using (SolidBrush hb = new SolidBrush(pal.SurfaceHover))
                    {
                        g.FillRectangle(hb, rowRect);
                    }
                }

                // 1px divider between rows
                if (i < count - 1)
                {
                    using (Pen p = new Pen(pal.BorderSubtle, 1f))
                    {
                        g.DrawLine(p, pad, (i + 1) * rowH, Width - pad, (i + 1) * rowH);
                    }
                }

                string timeText = _isArabic ? item.TimeAr : item.TimeEn;
                string badgeText = _isArabic ? item.BadgeAr : item.BadgeEn;
                string titleText = _isArabic ? item.TitleAr : item.TitleEn;
                string descText = _isArabic ? item.DescAr : item.DescEn;

                // Badge metrics
                int badgeH = DpiManager.ScaleInt(20);
                Size bSize = TextRenderer.MeasureText(g, badgeText, badgeFont);
                int badgeW = bSize.Width + DpiManager.ScaleInt(14);
                int badgeY = rowRect.Y + (rowH - badgeH) / 2;

                if (_isArabic)
                {
                    // RTL: Timestamp on far right
                    int timeW = DpiManager.ScaleInt(65);
                    int timeX = rowRect.Right - pad - timeW;
                    Rectangle timeRect = new Rectangle(timeX, rowRect.Y, timeW, rowH);
                    TextRenderer.DrawText(g, timeText, timeFont, timeRect, pal.TextMuted,
                        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.RightToLeft);

                    // Badge to left of timestamp
                    int badgeX = timeX - DpiManager.ScaleInt(SpacingTokens.Space12) - badgeW;
                    DrawRowBadge(g, pal, item.Variant, badgeText, badgeFont, new Rectangle(badgeX, badgeY, badgeW, badgeH));

                    // Title & Description stacked to left of badge
                    int textRight = badgeX - DpiManager.ScaleInt(SpacingTokens.Space12);
                    int textLeft = rowRect.X + pad;
                    int textW = Math.Max(20, textRight - textLeft);

                    int textY = rowRect.Y + (rowH - (titleFont.Height + descFont.Height + DpiManager.ScaleInt(2))) / 2;
                    Rectangle titleRect = new Rectangle(textLeft, textY, textW, titleFont.Height);
                    TextRenderer.DrawText(g, titleText, titleFont, titleRect, pal.TextPrimary,
                        TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix);

                    Rectangle descRect = new Rectangle(textLeft, textY + titleFont.Height + DpiManager.ScaleInt(2), textW, descFont.Height);
                    TextRenderer.DrawText(g, descText, descFont, descRect, pal.TextSecondary,
                        TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix);
                }
                else
                {
                    // LTR: Timestamp on far left
                    int timeW = DpiManager.ScaleInt(65);
                    Rectangle timeRect = new Rectangle(rowRect.X + pad, rowRect.Y, timeW, rowH);
                    TextRenderer.DrawText(g, timeText, timeFont, timeRect, pal.TextMuted,
                        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);

                    // Badge to right of timestamp
                    int badgeX = rowRect.X + pad + timeW + DpiManager.ScaleInt(SpacingTokens.Space8);
                    DrawRowBadge(g, pal, item.Variant, badgeText, badgeFont, new Rectangle(badgeX, badgeY, badgeW, badgeH));

                    // Title & Description stacked to right of badge
                    int textLeft = badgeX + badgeW + DpiManager.ScaleInt(SpacingTokens.Space12);
                    int textRight = rowRect.Right - pad;
                    int textW = Math.Max(20, textRight - textLeft);

                    int textY = rowRect.Y + (rowH - (titleFont.Height + descFont.Height + DpiManager.ScaleInt(2))) / 2;
                    Rectangle titleRect = new Rectangle(textLeft, textY, textW, titleFont.Height);
                    TextRenderer.DrawText(g, titleText, titleFont, titleRect, pal.TextPrimary,
                        TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.NoPrefix);

                    Rectangle descRect = new Rectangle(textLeft, textY + titleFont.Height + DpiManager.ScaleInt(2), textW, descFont.Height);
                    TextRenderer.DrawText(g, descText, descFont, descRect, pal.TextSecondary,
                        TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                }
            }
        }

        private void DrawRowBadge(Graphics g, IThemePalette pal, StatusBadgeVariant variant, string text, Font font, Rectangle rect)
        {
            Color fg = pal.TextPrimary;
            Color bg = pal.SurfaceElevated;

            switch (variant)
            {
                case StatusBadgeVariant.Success:
                    fg = pal.Success;
                    bg = pal.SuccessSoft;
                    break;
                case StatusBadgeVariant.Warning:
                    fg = pal.Warning;
                    bg = pal.WarningSoft;
                    break;
                case StatusBadgeVariant.Danger:
                    fg = pal.Danger;
                    bg = pal.DangerSoft;
                    break;
                case StatusBadgeVariant.Info:
                    fg = pal.Info;
                    bg = pal.InfoSoft;
                    break;
            }

            using (SolidBrush b = new SolidBrush(bg))
            {
                UiDrawingUtils.FillRoundedRectangle(g, b, rect, rect.Height / 2);
            }
            using (Pen p = new Pen(fg, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, p, rect, rect.Height / 2);
            }

            bool isArabic = UiDrawingUtils.IsArabicText(text, false);
            int yOffset = UiDrawingUtils.GetBadgeTextVerticalOffset(isArabic);
            Rectangle textRect = new Rectangle(rect.X, rect.Y + yOffset, rect.Width, rect.Height);
            TextRenderer.DrawText(g, text, font, textRect, fg,
                TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ShellProtectionOverview — Flat Table Panel for 6 Existing v2.0 Controls
    // ═══════════════════════════════════════════════════════════════════════════
    internal class ShellProtectionOverview : Control
    {
        private class OverviewRow
        {
            public string TitleEn { get; set; }
            public string TitleAr { get; set; }
            public string SubtitleEn { get; set; }
            public string SubtitleAr { get; set; }
            public StatusBadgeVariant Variant { get; set; }
            public string BadgeEn { get; set; }
            public string BadgeAr { get; set; }
        }

        private readonly List<OverviewRow> _rows = new List<OverviewRow>();
        private int _hoveredIndex = -1;
        private bool _isArabic = false;

        public ShellProtectionOverview()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            TabStop = false;
            InitializeRows();
            ThemeManager.ThemeChanged += delegate { Invalidate(); };
        }

        private void InitializeRows()
        {
            _rows.Add(new OverviewRow
            {
                TitleEn = "Removable Storage",
                TitleAr = "وحدات التخزين القابلة للإزالة",
                SubtitleEn = "USB flash drives, external HDDs/SSDs",
                SubtitleAr = "محركات أقراص فلاش وأقراص خارجية",
                Variant = StatusBadgeVariant.Success,
                BadgeEn = "LOCKED",
                BadgeAr = "مقفل"
            });

            _rows.Add(new OverviewRow
            {
                TitleEn = "Read-Only Mode",
                TitleAr = "وضع القراءة فقط",
                SubtitleEn = "Write-protect deny enforcement",
                SubtitleAr = "فرض حظر الكتابة على الوسائط",
                Variant = StatusBadgeVariant.Info,
                BadgeEn = "ENFORCED",
                BadgeAr = "مفروض"
            });

            _rows.Add(new OverviewRow
            {
                TitleEn = "Flash Storage Sentinel",
                TitleAr = "حارس ذاكرة الفلاش",
                SubtitleEn = "Real-time PnP & volume monitoring",
                SubtitleAr = "مراقبة فورية للأجهزة ووحدات التخزين",
                Variant = StatusBadgeVariant.Success,
                BadgeEn = "ACTIVE",
                BadgeAr = "نشط"
            });

            _rows.Add(new OverviewRow
            {
                TitleEn = "AutoPlay Guard",
                TitleAr = "حارس التشغيل التلقائي",
                SubtitleEn = "DisableAutoplay=1 registry lockdown",
                SubtitleAr = "قفل سجل النظام DisableAutoplay=1",
                Variant = StatusBadgeVariant.Success,
                BadgeEn = "BLOCKED",
                BadgeAr = "محظور"
            });

            _rows.Add(new OverviewRow
            {
                TitleEn = "Portable Devices (WPD)",
                TitleAr = "الأجهزة المحمولة (WPD)",
                SubtitleEn = "Media transfer & mobile devices",
                SubtitleAr = "نقل الوسائط والأجهزة المحمولة",
                Variant = StatusBadgeVariant.Neutral,
                BadgeEn = "MONITORED",
                BadgeAr = "مراقب"
            });

            _rows.Add(new OverviewRow
            {
                TitleEn = "Whitelist Enforcement",
                TitleAr = "فرض القائمة البيضاء",
                SubtitleEn = "Hardware ID signature verification",
                SubtitleAr = "التحقق من بصمة معرّف العتاد",
                Variant = StatusBadgeVariant.Success,
                BadgeEn = "ENABLED",
                BadgeAr = "مفعل"
            });
        }

        public void UpdateLanguage(bool isArabic)
        {
            _isArabic = isArabic;
            RightToLeft = _isArabic ? RightToLeft.Yes : RightToLeft.No;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int rowH = Height / Math.Max(1, _rows.Count);
            int idx = e.Y / Math.Max(1, rowH);
            if (idx >= _rows.Count) idx = -1;

            if (_hoveredIndex != idx)
            {
                _hoveredIndex = idx;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredIndex != -1)
            {
                _hoveredIndex = -1;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);

            // Background & Border
            using (SolidBrush bg = new SolidBrush(pal.Surface))
            {
                UiDrawingUtils.FillRoundedRectangle(g, bg, rect, rad);
            }
            using (Pen borderPen = new Pen(pal.BorderSubtle, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, borderPen, rect, rad);
            }

            int count = _rows.Count;
            if (count == 0) return;

            int rowH = Height / count;
            int pad = DpiManager.ScaleInt(SpacingTokens.Space16);

            Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, _isArabic);
            Font subFont = DpiManager.GetFont(TypographyRole.Caption, _isArabic);
            Font badgeFont = DpiManager.GetFont(TypographyRole.Caption, _isArabic);

            for (int i = 0; i < count; i++)
            {
                var row = _rows[i];
                Rectangle rowRect = new Rectangle(1, i * rowH + 1, Width - 2, rowH - 1);

                // Row hover tint
                if (i == _hoveredIndex)
                {
                    using (SolidBrush hb = new SolidBrush(pal.SurfaceHover))
                    {
                        g.FillRectangle(hb, rowRect);
                    }
                }

                // 1px divider between rows
                if (i < count - 1)
                {
                    using (Pen p = new Pen(pal.BorderSubtle, 1f))
                    {
                        g.DrawLine(p, pad, (i + 1) * rowH, Width - pad, (i + 1) * rowH);
                    }
                }

                string titleText = _isArabic ? row.TitleAr : row.TitleEn;
                string subText = _isArabic ? row.SubtitleAr : row.SubtitleEn;
                string badgeText = _isArabic ? row.BadgeAr : row.BadgeEn;

                // Badge metrics
                int badgeH = DpiManager.ScaleInt(20);
                Size bSize = TextRenderer.MeasureText(g, badgeText, badgeFont);
                int badgeW = bSize.Width + DpiManager.ScaleInt(14);
                int badgeY = rowRect.Y + (rowH - badgeH) / 2;

                if (_isArabic)
                {
                    // RTL: Badge on far left (trailing edge)
                    int badgeX = rowRect.X + pad;
                    DrawRowBadge(g, pal, row.Variant, badgeText, badgeFont, new Rectangle(badgeX, badgeY, badgeW, badgeH));

                    // Title & Subtitle on right (leading edge)
                    int textRight = rowRect.Right - pad;
                    int textLeft = badgeX + badgeW + DpiManager.ScaleInt(SpacingTokens.Space12);
                    int textW = Math.Max(20, textRight - textLeft);

                    int textY = rowRect.Y + (rowH - (titleFont.Height + subFont.Height + DpiManager.ScaleInt(2))) / 2;
                    Rectangle titleRect = new Rectangle(textRight - textW, textY, textW, titleFont.Height);
                    TextRenderer.DrawText(g, titleText, titleFont, titleRect, pal.TextPrimary,
                        TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix);

                    Rectangle subRect = new Rectangle(textRight - textW, textY + titleFont.Height + DpiManager.ScaleInt(2), textW, subFont.Height);
                    TextRenderer.DrawText(g, subText, subFont, subRect, pal.TextMuted,
                        TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix);
                }
                else
                {
                    // LTR: Badge on far right (trailing edge)
                    int badgeX = rowRect.Right - pad - badgeW;
                    DrawRowBadge(g, pal, row.Variant, badgeText, badgeFont, new Rectangle(badgeX, badgeY, badgeW, badgeH));

                    // Title & Subtitle on left (leading edge)
                    int textLeft = rowRect.X + pad;
                    int textRight = badgeX - DpiManager.ScaleInt(SpacingTokens.Space12);
                    int textW = Math.Max(20, textRight - textLeft);

                    int textY = rowRect.Y + (rowH - (titleFont.Height + subFont.Height + DpiManager.ScaleInt(2))) / 2;
                    Rectangle titleRect = new Rectangle(textLeft, textY, textW, titleFont.Height);
                    TextRenderer.DrawText(g, titleText, titleFont, titleRect, pal.TextPrimary,
                        TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.NoPrefix);

                    Rectangle subRect = new Rectangle(textLeft, textY + titleFont.Height + DpiManager.ScaleInt(2), textW, subFont.Height);
                    TextRenderer.DrawText(g, subText, subFont, subRect, pal.TextMuted,
                        TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                }
            }
        }

        private void DrawRowBadge(Graphics g, IThemePalette pal, StatusBadgeVariant variant, string text, Font font, Rectangle rect)
        {
            Color fg = pal.TextPrimary;
            Color bg = pal.SurfaceElevated;

            switch (variant)
            {
                case StatusBadgeVariant.Success:
                    fg = pal.Success;
                    bg = pal.SuccessSoft;
                    break;
                case StatusBadgeVariant.Warning:
                    fg = pal.Warning;
                    bg = pal.WarningSoft;
                    break;
                case StatusBadgeVariant.Danger:
                    fg = pal.Danger;
                    bg = pal.DangerSoft;
                    break;
                case StatusBadgeVariant.Info:
                    fg = pal.Info;
                    bg = pal.InfoSoft;
                    break;
            }

            using (SolidBrush b = new SolidBrush(bg))
            {
                UiDrawingUtils.FillRoundedRectangle(g, b, rect, rect.Height / 2);
            }
            using (Pen p = new Pen(fg, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, p, rect, rect.Height / 2);
            }

            bool isArabic = UiDrawingUtils.IsArabicText(text, false);
            int yOffset = UiDrawingUtils.GetBadgeTextVerticalOffset(isArabic);
            Rectangle textRect = new Rectangle(rect.X, rect.Y + yOffset, rect.Width, rect.Height);
            TextRenderer.DrawText(g, text, font, textRect, fg,
                TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);
        }
    }
}
#endif
