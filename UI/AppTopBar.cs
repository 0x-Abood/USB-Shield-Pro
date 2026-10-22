// ═══════════════════════════════════════════════════════════════════════════
// AppTopBar — Tier 1 Modern Header / Titlebar Primitive for v2.1
// USB Shield Pro v2.1
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace UsbShieldPro.UI
{
    public class AppTopBar : Control
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private string _brandTitle = "USB SHIELD PRO";
        private string _versionText = "v2.1";
        private Image _brandIcon = null;

        private readonly StatusBadge _postureBadge;
        private readonly IconButton _btnClose;
        private readonly IconButton _btnMaximize;
        private readonly IconButton _btnMinimize;
        private readonly AppButton _btnLanguage;

        public event EventHandler CloseClicked;
        public event EventHandler MaximizeClicked;
        public event EventHandler MinimizeClicked;
        public event EventHandler LanguageToggleClicked;

        public AppTopBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            _postureBadge = new StatusBadge();
            _postureBadge.Text = "PROTECTED";
            _postureBadge.Variant = StatusBadgeVariant.Success;

            _btnClose = new IconButton();
            _btnClose.ButtonType = IconButtonType.Close;
            _btnClose.SizeVariant = IconButtonSize.Small;
            _btnClose.ToolTipText = Loc.Get("Close");
            _btnClose.Click += delegate { if (CloseClicked != null) CloseClicked(this, EventArgs.Empty); };

            _btnMaximize = new IconButton();
            _btnMaximize.ButtonType = IconButtonType.Maximize;
            _btnMaximize.SizeVariant = IconButtonSize.Small;
            _btnMaximize.ToolTipText = Loc.Get("Maximize");
            _btnMaximize.Click += delegate { if (MaximizeClicked != null) MaximizeClicked(this, EventArgs.Empty); };

            _btnMinimize = new IconButton();
            _btnMinimize.ButtonType = IconButtonType.Minimize;
            _btnMinimize.SizeVariant = IconButtonSize.Small;
            _btnMinimize.ToolTipText = Loc.Get("Minimize");
            _btnMinimize.Click += delegate { if (MinimizeClicked != null) MinimizeClicked(this, EventArgs.Empty); };

            _btnLanguage = new AppButton();
            _btnLanguage.Variant = ButtonVariant.Ghost;
            _btnLanguage.SizeVariant = ButtonSize.Small;
            _btnLanguage.Text = "EN / AR";
            _btnLanguage.Width = DpiManager.ScaleInt(64);
            _btnLanguage.Click += delegate { if (LanguageToggleClicked != null) LanguageToggleClicked(this, EventArgs.Empty); };

            Controls.Add(_postureBadge);
            Controls.Add(_btnLanguage);
            Controls.Add(_btnMinimize);
            Controls.Add(_btnMaximize);
            Controls.Add(_btnClose);

            Height = DpiManager.ScaleInt(48);
            Dock = DockStyle.Top;

            LayoutTopBar();
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public string BrandTitle
        {
            get { return _brandTitle; }
            set { if (_brandTitle != value) { _brandTitle = value; Invalidate(); } }
        }

        public string VersionText
        {
            get { return _versionText; }
            set { if (_versionText != value) { _versionText = value; Invalidate(); } }
        }

        public Image BrandIcon
        {
            get { return _brandIcon; }
            set { if (_brandIcon != value) { _brandIcon = value; Invalidate(); } }
        }

        public StatusBadge PostureBadge
        {
            get { return _postureBadge; }
        }

        public AppButton LanguageButton
        {
            get { return _btnLanguage; }
        }

        public IconButton CloseButton
        {
            get { return _btnClose; }
        }

        public IconButton MaximizeButton
        {
            get { return _btnMaximize; }
        }

        public IconButton MinimizeButton
        {
            get { return _btnMinimize; }
        }

        public void UpdateMaximizeState(bool isMaximized)
        {
            if (_btnMaximize != null)
            {
                _btnMaximize.ButtonType = isMaximized ? IconButtonType.Restore : IconButtonType.Maximize;
                _btnMaximize.ToolTipText = Loc.Get(isMaximized ? "Restore" : "Maximize");
                _btnMaximize.Invalidate();
            }
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
            LayoutTopBar();
        }

        protected override void OnRightToLeftChanged(EventArgs e)
        {
            base.OnRightToLeftChanged(e);
            LayoutTopBar();
            Invalidate();
        }

        private void LayoutTopBar()
        {
            if (_btnClose == null || _btnMaximize == null || _btnMinimize == null || _btnLanguage == null || _postureBadge == null) return;

            bool isRtl = (RightToLeft == RightToLeft.Yes);
            int pad = DpiManager.ScaleInt(SpacingTokens.Space8);
            int btnSize = _btnClose.Width;
            int cy = (Height - btnSize) / 2;

            Font brandFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            Font versionFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            int iconW = (_brandIcon != null) ? (DpiManager.ScaleInt(IconTokens.IconMd) + DpiManager.ScaleInt(SpacingTokens.Space8)) : 0;
            Size titleSz = TextRenderer.MeasureText(_brandTitle, brandFont);
            Size verSz = TextRenderer.MeasureText(_versionText, versionFont);
            int brandTotalW = iconW + titleSz.Width + DpiManager.ScaleInt(SpacingTokens.Space4) + verSz.Width;
            int brandPad = DpiManager.ScaleInt(SpacingTokens.Space16);

            if (isRtl)
            {
                // In RTL, caption buttons are on far left: [Close] [Maximize] [Minimize]
                int leftX = pad;
                _btnClose.Location = new Point(leftX, cy);
                leftX += (btnSize + DpiManager.ScaleInt(SpacingTokens.Space4));
                _btnMaximize.Location = new Point(leftX, cy);
                leftX += (btnSize + DpiManager.ScaleInt(SpacingTokens.Space4));
                _btnMinimize.Location = new Point(leftX, cy);
                leftX += (btnSize + DpiManager.ScaleInt(SpacingTokens.Space12));

                _btnLanguage.Location = new Point(leftX, (Height - _btnLanguage.Height) / 2);

                // Brand text is on the far right, Posture badge is adjacent to its left
                int badgeX = Width - brandPad - brandTotalW - DpiManager.ScaleInt(SpacingTokens.Space12) - _postureBadge.Width;
                _postureBadge.Location = new Point(badgeX, (Height - _postureBadge.Height) / 2);
            }
            else
            {
                // In LTR, caption buttons are on far right: [Minimize] [Maximize] [Close]
                int rightX = Width - pad - btnSize;
                _btnClose.Location = new Point(rightX, cy);
                rightX -= (btnSize + DpiManager.ScaleInt(SpacingTokens.Space4));
                _btnMaximize.Location = new Point(rightX, cy);
                rightX -= (btnSize + DpiManager.ScaleInt(SpacingTokens.Space4));
                _btnMinimize.Location = new Point(rightX, cy);
                rightX -= (btnSize + DpiManager.ScaleInt(SpacingTokens.Space12));

                _btnLanguage.Location = new Point(rightX - _btnLanguage.Width, (Height - _btnLanguage.Height) / 2);

                // Posture badge placed adjacent to the right of brand text
                int badgeX = brandPad + brandTotalW + DpiManager.ScaleInt(SpacingTokens.Space12);
                _postureBadge.Location = new Point(badgeX, (Height - _postureBadge.Height) / 2);
            }
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            if (MaximizeClicked != null) MaximizeClicked(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                Form form = TopLevelControl as Form ?? FindForm();
                if (form != null)
                {
                    ReleaseCapture();
                    SendMessage(form.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes);

            // Background
            using (SolidBrush bg = new SolidBrush(pal.BackgroundSecondary))
            {
                g.FillRectangle(bg, ClientRectangle);
            }

            // Bottom subtle divider
            using (Pen pen = new Pen(pal.BorderSubtle, 1f))
            {
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }

            // Draw Brand Icon + Title + Version
            Font brandFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            Font versionFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            int pad = DpiManager.ScaleInt(SpacingTokens.Space16);
            int iconDim = DpiManager.ScaleInt(IconTokens.IconMd);
            int spacing = DpiManager.ScaleInt(SpacingTokens.Space8);

            if (isRtl)
            {
                int curX = Width - pad;
                if (_brandIcon != null)
                {
                    int iconY = (Height - iconDim) / 2;
                    g.DrawImage(_brandIcon, curX - iconDim, iconY, iconDim, iconDim);
                    curX -= (iconDim + spacing);
                }

                // Title + Version (Latin text rendered right-aligned without BiDi distortion)
                string combined = string.Format("{0} {1}", _brandTitle, _versionText);
                Size combinedSize = TextRenderer.MeasureText(g, combined, brandFont);
                Rectangle brandRect = new Rectangle(curX - combinedSize.Width, 0, combinedSize.Width, Height);
                TextRenderer.DrawText(g, combined, brandFont, brandRect, pal.TextPrimary,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPrefix);
            }
            else
            {
                int curX = pad;
                if (_brandIcon != null)
                {
                    int iconY = (Height - iconDim) / 2;
                    g.DrawImage(_brandIcon, curX, iconY, iconDim, iconDim);
                    curX += iconDim + spacing;
                }

                Size titleSize = TextRenderer.MeasureText(g, _brandTitle, brandFont);
                Rectangle titleRect = new Rectangle(curX, 0, titleSize.Width, Height);
                TextRenderer.DrawText(g, _brandTitle, brandFont, titleRect, pal.TextPrimary,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

                curX += titleSize.Width + DpiManager.ScaleInt(SpacingTokens.Space4);
                Rectangle versionRect = new Rectangle(curX, 0, DpiManager.ScaleInt(40), Height);
                TextRenderer.DrawText(g, _versionText, versionFont, versionRect, pal.TextMuted,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
        }
    }
}
