// ═══════════════════════════════════════════════════════════════════════════
// TrustedEmptyState — Dedicated Restrained Empty State for Trusted Devices
// USB Shield Pro v2.1 (Phase 4.4 Redesign)
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace UsbShieldPro.UI
{
    public class TrustedEmptyState : Control
    {
        private bool _isFiltered = false;
        private string _activeQuery = "";
        private bool _explicitVisible = false;

        private readonly AppButton _actionBtn;

        public event EventHandler GoToDevicesClicked;
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

        public TrustedEmptyState()
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
            _actionBtn.Variant = ButtonVariant.Primary;
            _actionBtn.Text = Loc.Get("GoToDevicesBtn");
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
            bool isRtl = Loc.IsRtl;
            if (_isFiltered)
            {
                _actionBtn.Text = Loc.Get("ClearFilter");
                _actionBtn.Variant = ButtonVariant.Ghost;
            }
            else
            {
                _actionBtn.Text = Loc.Get("GoToDevicesBtn");
                _actionBtn.Variant = ButtonVariant.Primary;
            }

            Font btnFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            int textW = TextRenderer.MeasureText(_actionBtn.Text ?? "", btnFont).Width;
            _actionBtn.Width = Math.Max(DpiManager.ScaleInt(140), textW + DpiManager.ScaleInt(28));

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
                if (GoToDevicesClicked != null) GoToDevicesClicked(this, EventArgs.Empty);
            }
        }

        private void LayoutControls()
        {
            if (_actionBtn == null) return;
            int cx = Width / 2;
            int btnY = DpiManager.ScaleInt(165);
            _actionBtn.Location = new Point(cx - _actionBtn.Width / 2, btnY);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutControls();
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
            bool isRtl = Loc.IsRtl;

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
                UiDrawingUtils.DrawNavTrusted(g, iconStroke, iconRect);
            }

            // 2. Title
            string titleText = _isFiltered ? Loc.Get("NoFilterResultsTitle") : Loc.Get("NoTrustedDevicesTitle");
            Font titleFont = DpiManager.GetFont(TypographyRole.Subtitle, isRtl);
            int titleY = iconBoxY + iconBoxSize + DpiManager.ScaleInt(14);
            int titleW = Math.Min(Width - 40, DpiManager.ScaleInt(460));
            int titleH = DpiManager.ScaleInt(isRtl ? 26 : 24);
            Rectangle titleRect = new Rectangle(cx - titleW / 2, titleY, titleW, titleH);

            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping;
            if (isRtl)
            {
                flags |= TextFormatFlags.RightToLeft;
            }
            TextRenderer.DrawText(g, titleText, titleFont, titleRect, pal.TextPrimary, flags);

            // 3. Subtitle
            string subText = _isFiltered ? Loc.Get("NoFilterResultsSubtitle") : Loc.Get("NoTrustedDevicesDesc");
            Font subFont = DpiManager.GetFont(TypographyRole.Body, isRtl);
            int subY = titleY + titleH + DpiManager.ScaleInt(4);
            int subW = Math.Min(Width - 40, DpiManager.ScaleInt(460));
            Size measuredSub = TextRenderer.MeasureText(g, subText, subFont, new Size(subW, int.MaxValue), flags | TextFormatFlags.WordBreak);
            int subH = Math.Max(measuredSub.Height + DpiManager.ScaleInt(isRtl ? 10 : 6), DpiManager.ScaleInt(isRtl ? 48 : 38));
            Rectangle subRect = new Rectangle(cx - subW / 2, subY, subW, subH);

            TextRenderer.DrawText(g, subText, subFont, subRect, pal.TextSecondary, flags | TextFormatFlags.WordBreak);
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
