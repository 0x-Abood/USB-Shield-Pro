// ═══════════════════════════════════════════════════════════════════════════
// SettingsControls — Production UI Surfaces for v2.1 Settings Workspace
// USB Shield Pro v2.1 (Phase 4.7 Settings Page Production Redesign)
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using UsbShieldPro.Models;
using UsbShieldPro.Persistence;
using UsbShieldPro.Security;
using UsbShieldPro.Service;

namespace UsbShieldPro.UI
{
    /// <summary>
    /// Probe hooks for safe synthetic state injection in capture mode and unit testing.
    /// Invariant: When probes are null, authoritative managers are queried.
    /// </summary>
    public static class SettingsProbes
    {
        public static Func<bool> ServiceInstalledProbe = null;
        public static Func<bool> ServiceRunningProbe = null;
        public static Func<bool> PasswordConfiguredProbe = null;
        public static Func<bool> RecoveryKeyConfiguredProbe = null;

        public static bool CheckServiceInstalled()
        {
            if (ServiceInstalledProbe != null) return ServiceInstalledProbe();
            return ServiceManager.IsServiceInstalled();
        }

        public static bool CheckServiceRunning()
        {
            if (ServiceRunningProbe != null) return ServiceRunningProbe();
            return ServiceManager.IsServiceRunning();
        }

        public static bool CheckPasswordConfigured()
        {
            if (PasswordConfiguredProbe != null) return PasswordConfiguredProbe();
            return PasswordManager.IsPasswordConfigured();
        }

        public static bool CheckRecoveryKeyConfigured()
        {
            if (RecoveryKeyConfiguredProbe != null) return RecoveryKeyConfiguredProbe();
            return RecoveryKeyManager.IsMasterRecoveryKeyConfigured();
        }

        public static void Reset()
        {
            ServiceInstalledProbe = null;
            ServiceRunningProbe = null;
            PasswordConfiguredProbe = null;
            RecoveryKeyConfiguredProbe = null;
        }
    }

    /// <summary>
    /// Professional bounded section card for grouping settings with unified styling.
    /// Invariant: Inner labels match surface background to prevent rectangular artifacts.
    /// </summary>
    public class SettingsCard : Control
    {
        private string _cardTitle = "";
        private string _cardSubtitle = "";
        private readonly List<SettingsRow> _rows = new List<SettingsRow>();

        public string CardTitle
        {
            get { return _cardTitle; }
            set { _cardTitle = value ?? ""; Invalidate(); }
        }

        public string CardSubtitle
        {
            get { return _cardSubtitle; }
            set { _cardSubtitle = value ?? ""; Invalidate(); }
        }

        public IReadOnlyList<SettingsRow> Rows
        {
            get { return _rows.AsReadOnly(); }
        }

        public SettingsCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = false;
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public SettingsCard(string title, string subtitle = null) : this()
        {
            _cardTitle = title ?? "";
            _cardSubtitle = subtitle ?? "";
        }

        public void AddRow(SettingsRow row)
        {
            if (row == null) return;
            _rows.Add(row);
            Controls.Add(row);
            row.RightToLeft = RightToLeft;
            PerformCardLayout();
        }

        public void UpdateLocalization()
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;
            foreach (var row in _rows)
            {
                row.RightToLeft = RightToLeft;
                row.UpdateLocalization();
            }
            PerformCardLayout();
            Invalidate();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                IThemePalette pal = ThemeManager.ActivePalette;
                foreach (Control c in Controls)
                {
                    c.BackColor = pal.Surface;
                    c.Invalidate();
                }
                Invalidate();
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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PerformCardLayout();
        }

        protected override void OnRightToLeftChanged(EventArgs e)
        {
            base.OnRightToLeftChanged(e);
            foreach (var row in _rows)
            {
                row.RightToLeft = RightToLeft;
            }
            PerformCardLayout();
        }

        public void PerformCardLayout()
        {
            int pad = DpiManager.ScaleInt(16);
            int curY = DpiManager.ScaleInt(14);
            int headerH = 0;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;

            if (!string.IsNullOrEmpty(_cardTitle))
            {
                headerH = DpiManager.ScaleInt(string.IsNullOrEmpty(_cardSubtitle) ? 28 : (isRtl ? 46 : 42));
                curY += headerH;
            }

            int clientW = Width - (pad * 2);
            if (clientW < 200) clientW = 200;

            for (int i = 0; i < _rows.Count; i++)
            {
                SettingsRow r = _rows[i];
                r.Location = new Point(pad, curY);
                r.Width = clientW;
                r.ShowBottomSeparator = (i < _rows.Count - 1);
                r.PerformLayoutRow();
                curY += r.Height;
            }

            curY += DpiManager.ScaleInt(12);
            Height = curY;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Background surface
            using (SolidBrush b = new SolidBrush(pal.Surface))
            {
                UiDrawingUtils.FillRoundedRectangle(g, b, rect, rad);
            }

            // Outer subtle border
            using (Pen p = new Pen(pal.Border, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, p, rect, rad);
            }

            // Header Title & Subtitle
            if (!string.IsNullOrEmpty(_cardTitle))
            {
                int pad = DpiManager.ScaleInt(16);
                int textW = Width - (pad * 2);
                int titleY = DpiManager.ScaleInt(14);

                Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
                Rectangle titleRect = new Rectangle(pad, titleY, textW, DpiManager.ScaleInt(22));
                TextFormatFlags titleFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.WordEllipsis | TextFormatFlags.NoPrefix;
                if (isRtl) titleFlags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
                else titleFlags |= TextFormatFlags.Left;

                TextRenderer.DrawText(g, _cardTitle, titleFont, titleRect, pal.TextPrimary, titleFlags);

                if (!string.IsNullOrEmpty(_cardSubtitle))
                {
                    int subY = titleY + DpiManager.ScaleInt(22);
                    Font subFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
                    Rectangle subRect = new Rectangle(pad, subY, textW, DpiManager.ScaleInt(isRtl ? 22 : 18));
                    TextFormatFlags subFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.WordEllipsis | TextFormatFlags.NoPrefix;
                    if (isRtl) subFlags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
                    else subFlags |= TextFormatFlags.Left;

                    TextRenderer.DrawText(g, _cardSubtitle, subFont, subRect, pal.TextSecondary, subFlags);
                }
            }
        }
    }

    /// <summary>
    /// Discrete settings row with title, auto-wrapped explanation, and action control.
    /// Handles RTL mirroring, dynamic height, multi-DPI zero clipping, and bottom separator.
    /// </summary>
    public class SettingsRow : Control
    {
        private string _title = "";
        private string _description = "";
        private Control _actionControl;
        private bool _showBottomSeparator = true;

        public string Title
        {
            get { return _title; }
            set { _title = value ?? ""; PerformLayoutRow(); Invalidate(); }
        }

        public string Description
        {
            get { return _description; }
            set { _description = value ?? ""; PerformLayoutRow(); Invalidate(); }
        }

        public Control ActionControl
        {
            get { return _actionControl; }
            set
            {
                if (_actionControl != value)
                {
                    if (_actionControl != null && Controls.Contains(_actionControl))
                    {
                        Controls.Remove(_actionControl);
                    }
                    _actionControl = value;
                    if (_actionControl != null)
                    {
                        Controls.Add(_actionControl);
                        _actionControl.RightToLeft = RightToLeft;
                    }
                    PerformLayoutRow();
                    Invalidate();
                }
            }
        }

        public bool ShowBottomSeparator
        {
            get { return _showBottomSeparator; }
            set { _showBottomSeparator = value; Invalidate(); }
        }

        public SettingsRow()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = false;
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public void UpdateLocalization()
        {
            PerformLayoutRow();
            Invalidate();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                IThemePalette pal = ThemeManager.ActivePalette;
                BackColor = pal.Surface;
                if (_actionControl != null)
                {
                    _actionControl.BackColor = pal.Surface;
                    _actionControl.Invalidate();
                }
                Invalidate();
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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PerformLayoutRow();
        }

        protected override void OnRightToLeftChanged(EventArgs e)
        {
            base.OnRightToLeftChanged(e);
            if (_actionControl != null)
            {
                _actionControl.RightToLeft = RightToLeft;
            }
            PerformLayoutRow();
        }

        public void PerformLayoutRow()
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int pad = DpiManager.ScaleInt(4);
            int gap = DpiManager.ScaleInt(14);
            int actionW = _actionControl != null ? _actionControl.Width : 0;
            int actionH = _actionControl != null ? _actionControl.Height : DpiManager.ScaleInt(24);

            int textW = Width - actionW - gap - (pad * 2);
            if (textW < 120) textW = 120;

            // Measure typography to guarantee zero bottom clipping
            Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            Font descFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            Size titleSize = TextRenderer.MeasureText(_title, titleFont);
            Size descSize = Size.Empty;
            if (!string.IsNullOrEmpty(_description))
            {
                TextFormatFlags mFlags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
                descSize = TextRenderer.MeasureText(_description, descFont, new Size(textW, 2000), mFlags);
            }

            int contentH = titleSize.Height + (descSize.Height > 0 ? (descSize.Height + DpiManager.ScaleInt(isRtl ? 6 : 4)) : 0);
            int minH = Math.Max(actionH + DpiManager.ScaleInt(18), DpiManager.ScaleInt(isRtl ? 54 : 48));
            int totalH = Math.Max(minH, contentH + DpiManager.ScaleInt(isRtl ? 20 : 16));
            Height = totalH;

            // Position action control
            if (_actionControl != null)
            {
                int actionY = (Height - _actionControl.Height) / 2;
                if (actionY < DpiManager.ScaleInt(6)) actionY = DpiManager.ScaleInt(6);

                if (isRtl)
                {
                    _actionControl.Location = new Point(pad, actionY);
                }
                else
                {
                    _actionControl.Location = new Point(Width - pad - _actionControl.Width, actionY);
                }
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;

            int pad = DpiManager.ScaleInt(4);
            int gap = DpiManager.ScaleInt(14);
            int actionW = _actionControl != null ? _actionControl.Width : 0;
            int textW = Width - actionW - gap - (pad * 2);
            if (textW < 120) textW = 120;

            int textX = isRtl ? (Width - pad - textW) : pad;
            int curY = DpiManager.ScaleInt(8);

            // Title
            Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            Size titleSize = TextRenderer.MeasureText(g, _title, titleFont);
            Rectangle titleRect = new Rectangle(textX, curY, textW, titleSize.Height);
            TextFormatFlags titleFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.WordEllipsis | TextFormatFlags.NoPrefix;
            if (isRtl) titleFlags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
            else titleFlags |= TextFormatFlags.Left;

            TextRenderer.DrawText(g, _title, titleFont, titleRect, pal.TextPrimary, titleFlags);
            curY += titleSize.Height + DpiManager.ScaleInt(isRtl ? 4 : 2);

            // Description
            if (!string.IsNullOrEmpty(_description))
            {
                Font descFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
                Rectangle descRect = new Rectangle(textX, curY, textW, Height - curY - DpiManager.ScaleInt(6));
                TextFormatFlags descFlags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix;
                if (isRtl) descFlags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
                else descFlags |= TextFormatFlags.Left;

                TextRenderer.DrawText(g, _description, descFont, descRect, pal.TextSecondary, descFlags);
            }

            // Bottom subtle separator
            if (_showBottomSeparator)
            {
                using (Pen sp = new Pen(pal.BorderSubtle, 1f))
                {
                    g.DrawLine(sp, pad, Height - 1, Width - pad, Height - 1);
                }
            }
        }
    }

    /// <summary>
    /// Specialized Background Protection Service card with authoritative status badging,
    /// truthful explanation, and safe action handlers.
    /// Invariant: Truthfully states reactive monitoring vs independent registry policies.
    /// </summary>
    public class SettingsServiceCard : Control
    {
        private string _cardTitle = "";
        private string _explanation = "";
        private readonly StatusBadge _badge;
        private readonly AppButton _btnInstall;
        private readonly AppButton _btnStartStop;
        private readonly AppButton _btnUninstall;

        public event EventHandler InstallClicked;
        public event EventHandler StartStopClicked;
        public event EventHandler UninstallClicked;

        public StatusBadge Badge { get { return _badge; } }
        public AppButton ButtonInstall { get { return _btnInstall; } }
        public AppButton ButtonStartStop { get { return _btnStartStop; } }
        public AppButton ButtonUninstall { get { return _btnUninstall; } }

        public SettingsServiceCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = false;

            _badge = new StatusBadge();
            _badge.TabStop = false;
            _badge.Variant = StatusBadgeVariant.Neutral;
            _badge.Text = Loc.Get("SettingsServiceStatusNotInstalled");

            _btnInstall = new AppButton();
            _btnInstall.SizeVariant = ButtonSize.Small;
            _btnInstall.Variant = ButtonVariant.Primary;
            _btnInstall.Text = Loc.Get("InstallServiceBtn");
            _btnInstall.Click += delegate { if (InstallClicked != null) InstallClicked(this, EventArgs.Empty); };

            _btnStartStop = new AppButton();
            _btnStartStop.SizeVariant = ButtonSize.Small;
            _btnStartStop.Variant = ButtonVariant.Secondary;
            _btnStartStop.Text = Loc.Get("SettingsStartServiceBtn");
            _btnStartStop.Click += delegate { if (StartStopClicked != null) StartStopClicked(this, EventArgs.Empty); };

            _btnUninstall = new AppButton();
            _btnUninstall.SizeVariant = ButtonSize.Small;
            _btnUninstall.Variant = ButtonVariant.DestructiveSecondary;
            _btnUninstall.Text = Loc.Get("UninstallServiceBtn");
            _btnUninstall.Click += delegate { if (UninstallClicked != null) UninstallClicked(this, EventArgs.Empty); };

            Controls.Add(_badge);
            Controls.Add(_btnInstall);
            Controls.Add(_btnStartStop);
            Controls.Add(_btnUninstall);

            ThemeManager.ThemeChanged += OnThemeChanged;
            UpdateServiceState();
        }

        public void UpdateLocalization()
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;
            _cardTitle = Loc.Get("SettingsSecService");
            _explanation = Loc.Get("SettingsServiceDesc");
            UpdateServiceState();
            PerformCardLayout();
            Invalidate();
        }

        public void UpdateServiceState()
        {
            bool installed = SettingsProbes.CheckServiceInstalled();
            bool running = SettingsProbes.CheckServiceRunning();

            _cardTitle = Loc.Get("SettingsSecService");
            _explanation = Loc.Get("SettingsServiceDesc");

            if (!installed)
            {
                _badge.Variant = StatusBadgeVariant.Neutral;
                _badge.Text = Loc.Get("SettingsServiceStatusNotInstalled");
                _btnInstall.Visible = true;
                _btnInstall.Text = Loc.Get("InstallServiceBtn");
                _btnStartStop.Visible = false;
                _btnUninstall.Visible = false;
            }
            else if (running)
            {
                _badge.Variant = StatusBadgeVariant.Success;
                _badge.Text = Loc.Get("SettingsServiceStatusActive");
                _btnInstall.Visible = false;
                _btnStartStop.Visible = true;
                _btnStartStop.Text = Loc.Get("SettingsStopServiceBtn");
                _btnStartStop.Variant = ButtonVariant.Secondary;
                _btnUninstall.Visible = true;
                _btnUninstall.Text = Loc.Get("UninstallServiceBtn");
            }
            else
            {
                _badge.Variant = StatusBadgeVariant.Warning;
                _badge.Text = Loc.Get("SettingsServiceStatusStopped");
                _btnInstall.Visible = false;
                _btnStartStop.Visible = true;
                _btnStartStop.Text = Loc.Get("SettingsStartServiceBtn");
                _btnStartStop.Variant = ButtonVariant.Primary;
                _btnUninstall.Visible = true;
                _btnUninstall.Text = Loc.Get("UninstallServiceBtn");
            }

            PerformCardLayout();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                IThemePalette pal = ThemeManager.ActivePalette;
                BackColor = pal.Surface;
                Invalidate();
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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PerformCardLayout();
        }

        protected override void OnRightToLeftChanged(EventArgs e)
        {
            base.OnRightToLeftChanged(e);
            PerformCardLayout();
        }

        public void PerformCardLayout()
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int pad = DpiManager.ScaleInt(16);
            int btnGap = DpiManager.ScaleInt(8);
            int clientW = Width - (pad * 2);
            if (clientW < 200) clientW = 200;

            Font fBtn = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            if (_btnInstall.Visible)
            {
                int tw = TextRenderer.MeasureText(_btnInstall.Text ?? "", fBtn).Width;
                _btnInstall.Width = Math.Max(DpiManager.ScaleInt(90), tw + DpiManager.ScaleInt(24));
            }
            if (_btnStartStop.Visible)
            {
                int tw = TextRenderer.MeasureText(_btnStartStop.Text ?? "", fBtn).Width;
                _btnStartStop.Width = Math.Max(DpiManager.ScaleInt(90), tw + DpiManager.ScaleInt(24));
            }
            if (_btnUninstall.Visible)
            {
                int tw = TextRenderer.MeasureText(_btnUninstall.Text ?? "", fBtn).Width;
                _btnUninstall.Width = Math.Max(DpiManager.ScaleInt(90), tw + DpiManager.ScaleInt(24));
            }

            int badgeW = _badge.Width;
            int titleY = DpiManager.ScaleInt(14);
            int titleH = DpiManager.ScaleInt(24);

            // Badge position: opposite side of title in header line
            if (isRtl)
            {
                _badge.Location = new Point(pad, titleY + (titleH - _badge.Height) / 2);
            }
            else
            {
                _badge.Location = new Point(Width - pad - badgeW, titleY + (titleH - _badge.Height) / 2);
            }

            // Buttons position in bottom action strip
            int btnY = Height - DpiManager.ScaleInt(46);
            if (isRtl)
            {
                int curBtnX = pad;
                if (_btnInstall.Visible)
                {
                    _btnInstall.Location = new Point(curBtnX, btnY);
                    curBtnX += _btnInstall.Width + btnGap;
                }
                if (_btnStartStop.Visible)
                {
                    _btnStartStop.Location = new Point(curBtnX, btnY);
                    curBtnX += _btnStartStop.Width + btnGap;
                }
                if (_btnUninstall.Visible)
                {
                    _btnUninstall.Location = new Point(curBtnX, btnY);
                }
            }
            else
            {
                int curBtnX = Width - pad;
                if (_btnUninstall.Visible)
                {
                    curBtnX -= _btnUninstall.Width;
                    _btnUninstall.Location = new Point(curBtnX, btnY);
                    curBtnX -= btnGap;
                }
                if (_btnStartStop.Visible)
                {
                    curBtnX -= _btnStartStop.Width;
                    _btnStartStop.Location = new Point(curBtnX, btnY);
                    curBtnX -= btnGap;
                }
                if (_btnInstall.Visible)
                {
                    curBtnX -= _btnInstall.Width;
                    _btnInstall.Location = new Point(curBtnX, btnY);
                }
            }

            // Compute dynamic card height
            Font descFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            TextFormatFlags mFlags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
            Size descSize = TextRenderer.MeasureText(_explanation, descFont, new Size(clientW, 2000), mFlags);

            int minH = DpiManager.ScaleInt(isRtl ? 134 : 126);
            int totalH = Math.Max(minH, titleY + titleH + descSize.Height + DpiManager.ScaleInt(isRtl ? 62 : 56));
            Height = totalH;

            // Recalculate button Y after final height
            btnY = Height - DpiManager.ScaleInt(44);
            _btnInstall.Top = btnY;
            _btnStartStop.Top = btnY;
            _btnUninstall.Top = btnY;

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Background
            using (SolidBrush b = new SolidBrush(pal.Surface))
            {
                UiDrawingUtils.FillRoundedRectangle(g, b, rect, rad);
            }

            // Border
            using (Pen p = new Pen(pal.Border, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, p, rect, rad);
            }

            int pad = DpiManager.ScaleInt(16);
            int badgeW = _badge.Width;
            int titleY = DpiManager.ScaleInt(14);
            int textW = Width - (pad * 2) - badgeW - DpiManager.ScaleInt(12);

            // Title
            Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            int titleX = isRtl ? (Width - pad - textW) : pad;
            Rectangle titleRect = new Rectangle(titleX, titleY, textW, DpiManager.ScaleInt(24));
            TextFormatFlags titleFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.WordEllipsis | TextFormatFlags.NoPrefix;
            if (isRtl) titleFlags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
            else titleFlags |= TextFormatFlags.Left;

            TextRenderer.DrawText(g, _cardTitle, titleFont, titleRect, pal.TextPrimary, titleFlags);

            // Truthful Explanation text
            int descY = titleY + DpiManager.ScaleInt(26);
            int fullTextW = Width - (pad * 2);
            int descX = isRtl ? (Width - pad - fullTextW) : pad;
            Font descFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            Rectangle descRect = new Rectangle(descX, descY, fullTextW, Height - descY - DpiManager.ScaleInt(48));
            TextFormatFlags descFlags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix;
            if (isRtl) descFlags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
            else descFlags |= TextFormatFlags.Left;

            TextRenderer.DrawText(g, _explanation, descFont, descRect, pal.TextSecondary, descFlags);
        }
    }

    /// <summary>
    /// Specialized Security card with password state, recovery key distinction, and safe action handlers.
    /// Invariant: Explicitly clarifies that Password Recovery Key does NOT touch Windows registry configuration.
    /// </summary>
    public class SettingsSecurityCard : Control
    {
        private string _cardTitle = "";
        private readonly StatusBadge _pwBadge;
        private readonly StatusBadge _recBadge;
        private readonly AppButton _btnSetPassword;
        private readonly AppButton _btnRemovePassword;
        private readonly AppButton _btnGenRecoveryKey;

        public event EventHandler ConfigurePasswordClicked;
        public event EventHandler RemovePasswordClicked;
        public event EventHandler GenerateRecoveryKeyClicked;

        public StatusBadge PasswordBadge { get { return _pwBadge; } }
        public StatusBadge RecoveryKeyBadge { get { return _recBadge; } }
        public AppButton ButtonConfigurePassword { get { return _btnSetPassword; } }
        public AppButton ButtonRemovePassword { get { return _btnRemovePassword; } }
        public AppButton ButtonGenerateRecoveryKey { get { return _btnGenRecoveryKey; } }

        public SettingsSecurityCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = false;

            _pwBadge = new StatusBadge();
            _pwBadge.TabStop = false;

            _recBadge = new StatusBadge();
            _recBadge.TabStop = false;

            _btnSetPassword = new AppButton();
            _btnSetPassword.SizeVariant = ButtonSize.Small;
            _btnSetPassword.Variant = ButtonVariant.Primary;
            _btnSetPassword.Click += delegate { if (ConfigurePasswordClicked != null) ConfigurePasswordClicked(this, EventArgs.Empty); };

            _btnRemovePassword = new AppButton();
            _btnRemovePassword.SizeVariant = ButtonSize.Small;
            _btnRemovePassword.Variant = ButtonVariant.DestructiveSecondary;
            _btnRemovePassword.Text = Loc.Get("RemovePassword");
            _btnRemovePassword.Click += delegate { if (RemovePasswordClicked != null) RemovePasswordClicked(this, EventArgs.Empty); };

            _btnGenRecoveryKey = new AppButton();
            _btnGenRecoveryKey.SizeVariant = ButtonSize.Small;
            _btnGenRecoveryKey.Variant = ButtonVariant.Secondary;
            _btnGenRecoveryKey.Text = Loc.Get("GenNewRecoveryKey");
            _btnGenRecoveryKey.Click += delegate { if (GenerateRecoveryKeyClicked != null) GenerateRecoveryKeyClicked(this, EventArgs.Empty); };

            Controls.Add(_pwBadge);
            Controls.Add(_recBadge);
            Controls.Add(_btnSetPassword);
            Controls.Add(_btnRemovePassword);
            Controls.Add(_btnGenRecoveryKey);

            ThemeManager.ThemeChanged += OnThemeChanged;
            UpdateSecurityState();
        }

        public void UpdateLocalization()
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;
            _cardTitle = Loc.Get("SettingsSecSecurity");
            _btnRemovePassword.Text = Loc.Get("RemovePassword");
            _btnGenRecoveryKey.Text = Loc.Get("GenNewRecoveryKey");
            UpdateSecurityState();
            PerformCardLayout();
            Invalidate();
        }

        public void UpdateSecurityState()
        {
            bool hasPw = SettingsProbes.CheckPasswordConfigured();
            bool hasRec = SettingsProbes.CheckRecoveryKeyConfigured();

            _cardTitle = Loc.Get("SettingsSecSecurity");

            if (hasPw)
            {
                _pwBadge.Variant = StatusBadgeVariant.Success;
                _pwBadge.Text = Loc.Get("SettingsStatusConfigured");
                _btnSetPassword.Text = Loc.Get("ChangePassword");
                _btnSetPassword.Variant = ButtonVariant.Secondary;
                _btnRemovePassword.Visible = true;
            }
            else
            {
                _pwBadge.Variant = StatusBadgeVariant.Neutral;
                _pwBadge.Text = Loc.Get("SettingsStatusNotConfigured");
                _btnSetPassword.Text = Loc.Get("SetPassword");
                _btnSetPassword.Variant = ButtonVariant.Primary;
                _btnRemovePassword.Visible = false;
            }

            if (hasRec)
            {
                _recBadge.Variant = StatusBadgeVariant.Success;
                _recBadge.Text = Loc.Get("SettingsStatusAvailable");
            }
            else
            {
                _recBadge.Variant = StatusBadgeVariant.Neutral;
                _recBadge.Text = Loc.Get("SettingsStatusNotConfigured");
            }

            _btnGenRecoveryKey.Visible = hasPw;

            PerformCardLayout();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                IThemePalette pal = ThemeManager.ActivePalette;
                BackColor = pal.Surface;
                Invalidate();
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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PerformCardLayout();
        }

        protected override void OnRightToLeftChanged(EventArgs e)
        {
            base.OnRightToLeftChanged(e);
            PerformCardLayout();
        }

        public void PerformCardLayout()
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int pad = DpiManager.ScaleInt(16);
            int btnGap = DpiManager.ScaleInt(8);
            int row1Y = DpiManager.ScaleInt(46);
            int sepY = DpiManager.ScaleInt(isRtl ? 114 : 106);
            int row2Y = sepY + DpiManager.ScaleInt(12);

            Font fBtn = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            int twSet = TextRenderer.MeasureText(_btnSetPassword.Text ?? "", fBtn).Width;
            _btnSetPassword.Width = Math.Max(DpiManager.ScaleInt(100), twSet + DpiManager.ScaleInt(24));

            if (_btnRemovePassword.Visible)
            {
                int twRem = TextRenderer.MeasureText(_btnRemovePassword.Text ?? "", fBtn).Width;
                _btnRemovePassword.Width = Math.Max(DpiManager.ScaleInt(110), twRem + DpiManager.ScaleInt(24));
            }

            if (_btnGenRecoveryKey.Visible)
            {
                int twGen = TextRenderer.MeasureText(_btnGenRecoveryKey.Text ?? "", fBtn).Width;
                _btnGenRecoveryKey.Width = Math.Max(DpiManager.ScaleInt(130), twGen + DpiManager.ScaleInt(24));
            }

            // Row 1 Action Controls (Password)
            if (isRtl)
            {
                int curX = pad;
                _pwBadge.Location = new Point(curX, row1Y + DpiManager.ScaleInt(4));
                curX += _pwBadge.Width + btnGap;

                if (_btnRemovePassword.Visible)
                {
                    _btnRemovePassword.Location = new Point(curX, row1Y);
                    curX += _btnRemovePassword.Width + btnGap;
                }
                _btnSetPassword.Location = new Point(curX, row1Y);
            }
            else
            {
                int curX = Width - pad;
                if (_btnRemovePassword.Visible)
                {
                    curX -= _btnRemovePassword.Width;
                    _btnRemovePassword.Location = new Point(curX, row1Y);
                    curX -= btnGap;
                }
                curX -= _btnSetPassword.Width;
                _btnSetPassword.Location = new Point(curX, row1Y);
                curX -= btnGap;
                _pwBadge.Location = new Point(curX - _pwBadge.Width, row1Y + DpiManager.ScaleInt(4));
            }

            // Row 2 Action Controls (Recovery Key)
            if (isRtl)
            {
                int curX = pad;
                _recBadge.Location = new Point(curX, row2Y + DpiManager.ScaleInt(4));
                curX += _recBadge.Width + btnGap;
                if (_btnGenRecoveryKey.Visible)
                {
                    _btnGenRecoveryKey.Location = new Point(curX, row2Y);
                }
            }
            else
            {
                int curX = Width - pad;
                if (_btnGenRecoveryKey.Visible)
                {
                    curX -= _btnGenRecoveryKey.Width;
                    _btnGenRecoveryKey.Location = new Point(curX, row2Y);
                    curX -= btnGap;
                }
                _recBadge.Location = new Point(curX - _recBadge.Width, row2Y + DpiManager.ScaleInt(4));
            }

            int minH = row2Y + DpiManager.ScaleInt(isRtl ? 78 : 70);
            Height = minH;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Card surface
            using (SolidBrush b = new SolidBrush(pal.Surface))
            {
                UiDrawingUtils.FillRoundedRectangle(g, b, rect, rad);
            }

            // Card border
            using (Pen p = new Pen(pal.Border, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, p, rect, rad);
            }

            int pad = DpiManager.ScaleInt(16);
            int fullTextW = Width - (pad * 2);

            // Section Header
            Font headerFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            Rectangle headerRect = new Rectangle(pad, DpiManager.ScaleInt(14), fullTextW, DpiManager.ScaleInt(22));
            TextFormatFlags headerFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            if (isRtl) headerFlags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
            else headerFlags |= TextFormatFlags.Left;

            TextRenderer.DrawText(g, _cardTitle, headerFont, headerRect, pal.TextPrimary, headerFlags);

            // Row 1: Password Protection
            int row1Y = DpiManager.ScaleInt(46);
            int row1ActionW = _btnSetPassword.Width + (_btnRemovePassword.Visible ? (_btnRemovePassword.Width + DpiManager.ScaleInt(8)) : 0) + _pwBadge.Width + DpiManager.ScaleInt(16);
            int row1TextW = Width - (pad * 2) - row1ActionW - DpiManager.ScaleInt(14);
            if (row1TextW < 120) row1TextW = 120;
            int row1TextX = isRtl ? (Width - pad - row1TextW) : pad;

            Font rowTitleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            Rectangle r1TitleRect = new Rectangle(row1TextX, row1Y, row1TextW, DpiManager.ScaleInt(20));
            TextFormatFlags rFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.WordEllipsis | TextFormatFlags.NoPrefix;
            if (isRtl) rFlags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
            else rFlags |= TextFormatFlags.Left;

            TextRenderer.DrawText(g, Loc.Get("SettingsPasswordTitle"), rowTitleFont, r1TitleRect, pal.TextPrimary, rFlags);

            Font captionFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            Rectangle r1DescRect = new Rectangle(row1TextX, row1Y + DpiManager.ScaleInt(22), row1TextW, DpiManager.ScaleInt(isRtl ? 38 : 34));
            TextFormatFlags descFlags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix;
            if (isRtl) descFlags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
            else descFlags |= TextFormatFlags.Left;

            TextRenderer.DrawText(g, Loc.Get("SettingsPasswordDesc"), captionFont, r1DescRect, pal.TextSecondary, descFlags);

            // Separator between rows
            int sepY = DpiManager.ScaleInt(isRtl ? 114 : 106);
            using (Pen sp = new Pen(pal.BorderSubtle, 1f))
            {
                g.DrawLine(sp, pad, sepY, Width - pad, sepY);
            }

            // Row 2: Password Recovery Key
            int row2Y = sepY + DpiManager.ScaleInt(12);
            int row2ActionW = (_btnGenRecoveryKey.Visible ? _btnGenRecoveryKey.Width + DpiManager.ScaleInt(8) : 0) + _recBadge.Width + DpiManager.ScaleInt(16);
            int row2TextW = Width - (pad * 2) - row2ActionW - DpiManager.ScaleInt(14);
            if (row2TextW < 120) row2TextW = 120;
            int row2TextX = isRtl ? (Width - pad - row2TextW) : pad;

            Rectangle r2TitleRect = new Rectangle(row2TextX, row2Y, row2TextW, DpiManager.ScaleInt(20));
            TextRenderer.DrawText(g, Loc.Get("SettingsRecoveryKeyTitle"), rowTitleFont, r2TitleRect, pal.TextPrimary, rFlags);

            Rectangle r2DescRect = new Rectangle(row2TextX, row2Y + DpiManager.ScaleInt(22), row2TextW, DpiManager.ScaleInt(isRtl ? 44 : 38));
            TextRenderer.DrawText(g, Loc.Get("SettingsRecoveryKeyTruthDesc"), captionFont, r2DescRect, pal.TextSecondary, descFlags);
        }
    }

    /// <summary>
    /// Restrained About card displaying product version, OS architecture, developer attribution, and GitHub repository.
    /// Invariant: Omits dead URLs, telemetry claims, and fake update checks.
    /// </summary>
    public class SettingsAboutCard : Control
    {
        private readonly AppButton _btnGitHub;
        private Rectangle _linkRect = Rectangle.Empty;

        public SettingsAboutCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = false;

            _btnGitHub = new AppButton();
            _btnGitHub.SizeVariant = ButtonSize.Small;
            _btnGitHub.Variant = ButtonVariant.Secondary;
            _btnGitHub.Text = Loc.Get("SettingsGitHubRepoBtn");
            _btnGitHub.Click += delegate { OpenGitHubRepository(); };
            Controls.Add(_btnGitHub);

            ThemeManager.ThemeChanged += OnThemeChanged;
            UpdateHeight();
        }

        public AppButton ButtonGitHub { get { return _btnGitHub; } }

        public void UpdateHeight()
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            Height = DpiManager.ScaleInt(isRtl ? 132 : 124);
            PositionControls();
            Invalidate();
        }

        public void UpdateLocalization()
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;
            if (_btnGitHub != null)
            {
                _btnGitHub.Text = Loc.Get("SettingsGitHubRepoBtn");
            }
            UpdateHeight();
        }

        public string AppTitleText { get { return Loc.Get("SettingsAboutTitle"); } }
        public string AppDescText { get { return Loc.Get("SettingsAboutDesc"); } }
        public string AppSysInfoText { get { return Loc.Get("SettingsAboutSysInfo"); } }
        public string AppCopyrightText { get { return Loc.Get("SettingsAboutCopyright"); } }

        public static void OpenGitHubRepository()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/0x-Abood/USB-Shield-Pro",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void PositionControls()
        {
            if (_btnGitHub == null) return;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int pad = DpiManager.ScaleInt(16);
            int btnW = DpiManager.ScaleInt(isRtl ? 175 : 150);
            int btnH = DpiManager.ScaleInt(30);
            int btnY = (Height - btnH) / 2;

            int btnX = isRtl ? pad : (Width - pad - btnW);
            _btnGitHub.Bounds = new Rectangle(btnX, btnY, btnW, btnH);
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                IThemePalette pal = ThemeManager.ActivePalette;
                BackColor = pal.Surface;
                Invalidate();
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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PositionControls();
            Invalidate();
        }

        protected override void OnRightToLeftChanged(EventArgs e)
        {
            base.OnRightToLeftChanged(e);
            UpdateHeight();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_linkRect.IsEmpty && _linkRect.Contains(e.Location))
            {
                Cursor = Cursors.Hand;
            }
            else
            {
                Cursor = Cursors.Default;
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Cursor = Cursors.Default;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && !_linkRect.IsEmpty && _linkRect.Contains(e.Location))
            {
                OpenGitHubRepository();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Card background
            using (SolidBrush b = new SolidBrush(pal.Surface))
            {
                UiDrawingUtils.FillRoundedRectangle(g, b, rect, rad);
            }

            // Card border
            using (Pen p = new Pen(pal.Border, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, p, rect, rad);
            }

            int pad = DpiManager.ScaleInt(16);
            int iconSize = DpiManager.ScaleInt(64);

            // Draw Brand Shield Icon
            int iconX = isRtl ? (Width - pad - iconSize) : pad;
            int iconY = (Height - iconSize) / 2;
            Rectangle iconRect = new Rectangle(iconX, iconY, iconSize, iconSize);

            Bitmap brandBmp = BrandAssets.GetBrandBitmap(iconSize);
            if (brandBmp != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(brandBmp, iconRect);
            }

            int btnW = (_btnGitHub != null && _btnGitHub.Visible) ? (_btnGitHub.Width + DpiManager.ScaleInt(16)) : 0;
            int textX = isRtl ? (pad + btnW) : (pad + iconSize + DpiManager.ScaleInt(16));
            int textW = Math.Max(DpiManager.ScaleInt(200), Width - (pad * 2) - iconSize - DpiManager.ScaleInt(16) - btnW);

            int titleH = DpiManager.ScaleInt(24);
            int descH = DpiManager.ScaleInt(20);
            int infoH = DpiManager.ScaleInt(18);
            int copyH = DpiManager.ScaleInt(18);
            int totalTextH = titleH + descH + infoH + copyH;
            int curY = Math.Max(DpiManager.ScaleInt(8), (Height - totalTextH) / 2);

            // Title: USB Shield Pro v2.1
            Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            Rectangle titleRect = new Rectangle(textX, curY, textW, titleH);
            TextFormatFlags titleFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping;
            if (isRtl) titleFlags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
            else titleFlags |= TextFormatFlags.Left;

            TextRenderer.DrawText(g, Loc.Get("SettingsAboutTitle"), titleFont, titleRect, pal.TextPrimary, pal.Surface, titleFlags);
            curY += titleH;

            // Subtitle Description
            Font captionFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            Rectangle descRect = new Rectangle(textX, curY, textW, descH);
            TextRenderer.DrawText(g, Loc.Get("SettingsAboutDesc"), captionFont, descRect, pal.TextSecondary, pal.Surface, titleFlags);
            curY += descH;

            // System info
            Rectangle infoRect = new Rectangle(textX, curY, textW, infoH);
            string sysInfo = "\u202A" + Loc.Get("SettingsAboutSysInfo") + "\u202C";
            TextRenderer.DrawText(g, sysInfo, captionFont, infoRect, pal.TextMuted, pal.Surface, titleFlags);
            curY += infoH;

            // Developer attribution & GitHub link
            _linkRect = new Rectangle(textX, curY, textW, copyH);
            string copyText = Loc.Get("SettingsAboutCopyright");
            TextFormatFlags copyFlags = titleFlags;
            TextRenderer.DrawText(g, copyText, captionFont, _linkRect, pal.Accent, pal.Surface, copyFlags);
        }
    }
}
