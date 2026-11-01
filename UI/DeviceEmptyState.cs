// ═══════════════════════════════════════════════════════════════════════════
// DeviceEmptyState — Restrained Empty State Vector Component for Devices
// USB Shield Pro v2.1 (Phase 4.2)
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace UsbShieldPro.UI
{
    public class DeviceEmptyState : Control
    {
        private bool _isFiltered = false;
        private string _activeQuery = "";
        private bool _explicitVisible = false;

        private readonly AppButton _actionBtn;

        public event EventHandler RefreshClicked;
        public event EventHandler ClearFilterClicked;

        public bool IsFiltered { get { return _isFiltered; } }
        public AppButton ActionButton { get { return _actionBtn; } }
        public bool IsActive { get { return _explicitVisible; } }

        public new bool Visible
        {
            get { return _explicitVisible; }
            set
            {
                _explicitVisible = value;
                base.Visible = value;
            }
        }

        public DeviceEmptyState()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = false;
            BackColor = Color.Transparent;
            Height = DpiManager.ScaleInt(240);

            _actionBtn = new AppButton();
            _actionBtn.SizeVariant = ButtonSize.Small;
            _actionBtn.Variant = ButtonVariant.Secondary;
            _actionBtn.Text = Loc.Get("RefreshList");
            _actionBtn.Click += OnActionBtnClick;

            Controls.Add(_actionBtn);

            UpdateState();
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public void SetSearchMode(bool isFilterActive, string query)
        {
            _isFiltered = isFilterActive;
            _activeQuery = query ?? "";
            UpdateState();
            Invalidate();
        }

        private void UpdateState()
        {
            if (_isFiltered)
            {
                _actionBtn.Text = Loc.Get("ClearFilter");
                _actionBtn.Variant = ButtonVariant.Ghost;
            }
            else
            {
                _actionBtn.Text = Loc.Get("RefreshList");
                _actionBtn.Variant = ButtonVariant.Secondary;
            }
            LayoutControls();
        }

        private void OnActionBtnClick(object sender, EventArgs e)
        {
            if (_isFiltered)
            {
                if (ClearFilterClicked != null) ClearFilterClicked(this, EventArgs.Empty);
            }
            else
            {
                if (RefreshClicked != null) RefreshClicked(this, EventArgs.Empty);
            }
        }

        private void LayoutControls()
        {
            if (_actionBtn == null) return;
            int cx = Width / 2;
            bool isRtl = RightToLeft == RightToLeft.Yes;

            int iconBoxSize = DpiManager.ScaleInt(48);
            int iconBoxY = DpiManager.ScaleInt(24);
            Font titleFont = DpiManager.GetFont(TypographyRole.Subtitle, isRtl);
            int titleY = iconBoxY + iconBoxSize + DpiManager.ScaleInt(14);
            int titleH = DpiManager.ScaleInt(isRtl ? 26 : 24);

            string subText = _isFiltered ? Loc.Get("NoFilterResultsSubtitle") : Loc.Get("NoExternalDevicesConnectedSubtitle");
            Font subFont = DpiManager.GetFont(TypographyRole.Body, isRtl);
            int subY = titleY + titleH + DpiManager.ScaleInt(6);
            int subW = Math.Min(Math.Max(200, Width - 40), DpiManager.ScaleInt(480));

            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak;
            if (isRtl) flags |= TextFormatFlags.RightToLeft;
            Size measuredSub = TextRenderer.MeasureText(subText, subFont, new Size(subW, int.MaxValue), flags);
            int subH = Math.Max(measuredSub.Height + DpiManager.ScaleInt(isRtl ? 10 : 6), DpiManager.ScaleInt(isRtl ? 54 : 40));

            int btnY = subY + subH + DpiManager.ScaleInt(16);
            _actionBtn.Location = new Point(cx - _actionBtn.Width / 2, btnY);

            int neededH = btnY + _actionBtn.Height + DpiManager.ScaleInt(24);
            if (Height < neededH)
            {
                Height = neededH;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutControls();
        }

        protected override void OnRightToLeftChanged(EventArgs e)
        {
            base.OnRightToLeftChanged(e);
            LayoutControls();
            Invalidate();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = RightToLeft == RightToLeft.Yes;

            int cx = Width / 2;

            // 1. Vector Graphic Illustration
            int iconBoxSize = DpiManager.ScaleInt(48);
            int iconBoxY = DpiManager.ScaleInt(24);
            Rectangle iconRect = new Rectangle(cx - iconBoxSize / 2, iconBoxY, iconBoxSize, iconBoxSize);

            Color iconStroke = pal.TextMuted;
            if (_isFiltered)
            {
                UiDrawingUtils.DrawSearchIcon(g, iconStroke, iconRect);
            }
            else
            {
                UiDrawingUtils.DrawDeviceFlashDrive(g, iconStroke, iconRect);
            }

            // 2. Title
            string titleText = _isFiltered ? Loc.Get("NoFilterResultsTitle") : Loc.Get("NoExternalDevicesConnectedTitle");
            Font titleFont = DpiManager.GetFont(TypographyRole.Subtitle, isRtl);
            int titleY = iconBoxY + iconBoxSize + DpiManager.ScaleInt(14);
            int titleW = Math.Min(Width - 40, DpiManager.ScaleInt(480));
            int titleH = DpiManager.ScaleInt(isRtl ? 26 : 24);
            Rectangle titleRect = new Rectangle(cx - titleW / 2, titleY, titleW, titleH);

            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping;
            if (isRtl) flags |= TextFormatFlags.RightToLeft;
            TextRenderer.DrawText(g, titleText, titleFont, titleRect, pal.TextPrimary, flags);

            // 3. Subtitle
            string subText = _isFiltered ? Loc.Get("NoFilterResultsSubtitle") : Loc.Get("NoExternalDevicesConnectedSubtitle");
            Font subFont = DpiManager.GetFont(TypographyRole.Body, isRtl);
            int subY = titleY + titleH + DpiManager.ScaleInt(6);
            int subW = Math.Min(Math.Max(200, Width - 40), DpiManager.ScaleInt(480));

            TextFormatFlags subFlags = TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoClipping;
            if (isRtl) subFlags |= TextFormatFlags.RightToLeft;

            Size measuredSub = TextRenderer.MeasureText(g, subText, subFont, new Size(subW, int.MaxValue), subFlags);
            int subH = Math.Max(measuredSub.Height + DpiManager.ScaleInt(isRtl ? 10 : 6), DpiManager.ScaleInt(isRtl ? 54 : 40));
            Rectangle subRect = new Rectangle(cx - subW / 2, subY, subW, subH);

            TextRenderer.DrawText(g, subText, subFont, subRect, pal.TextSecondary, subFlags);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }
    }
}
