// ═══════════════════════════════════════════════════════════════════════════
// CompactTemporaryAccessModule — Compact Temporary Access Strip for v2.1
// USB Shield Pro v2.1
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace UsbShieldPro.UI
{
    internal class CompactTemporaryAccessModule : Control
    {
        private readonly AppButton _btn5Min;
        private readonly AppButton _btn15Min;
        private readonly AppButton _btn30Min;
        private readonly AppButton _btnCancel;
        private readonly StatusBadge _countdownBadge;
        private bool _isTempActive = false;
        private bool _isProtectionActive = false;
        private int _remainingSeconds = 0;

        public event Action<int> DurationSelected;
        public event EventHandler CancelClicked;

        public AppButton Btn5Min { get { return _btn5Min; } }
        public AppButton Btn15Min { get { return _btn15Min; } }
        public AppButton Btn30Min { get { return _btn30Min; } }
        public AppButton BtnCancel { get { return _btnCancel; } }
        public StatusBadge CountdownBadge { get { return _countdownBadge; } }
        public bool IsTempActive { get { return _isTempActive; } }
        public bool IsProtectionActive { get { return _isProtectionActive; } }
        public int RemainingSeconds { get { return _remainingSeconds; } }

        public CompactTemporaryAccessModule()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            TabStop = false;

            _btn5Min = new AppButton();
            _btn5Min.SizeVariant = ButtonSize.Small;
            _btn5Min.Variant = ButtonVariant.Secondary;
            _btn5Min.Text = "5 min";
            _btn5Min.Click += delegate { if (DurationSelected != null) DurationSelected(5); };

            _btn15Min = new AppButton();
            _btn15Min.SizeVariant = ButtonSize.Small;
            _btn15Min.Variant = ButtonVariant.Secondary;
            _btn15Min.Text = "15 min";
            _btn15Min.Click += delegate { if (DurationSelected != null) DurationSelected(15); };

            _btn30Min = new AppButton();
            _btn30Min.SizeVariant = ButtonSize.Small;
            _btn30Min.Variant = ButtonVariant.Secondary;
            _btn30Min.Text = "30 min";
            _btn30Min.Click += delegate { if (DurationSelected != null) DurationSelected(30); };

            _btnCancel = new AppButton();
            _btnCancel.SizeVariant = ButtonSize.Small;
            _btnCancel.Variant = ButtonVariant.Destructive;
            _btnCancel.Text = Loc.Get("CancelTempAccess");
            _btnCancel.Visible = false;
            _btnCancel.Click += delegate { if (CancelClicked != null) CancelClicked(this, EventArgs.Empty); };

            _countdownBadge = new StatusBadge();
            _countdownBadge.Variant = StatusBadgeVariant.Warning;
            _countdownBadge.Visible = false;

            Controls.Add(_btn5Min);
            Controls.Add(_btn15Min);
            Controls.Add(_btn30Min);
            Controls.Add(_btnCancel);
            Controls.Add(_countdownBadge);

            Height = DpiManager.ScaleInt(38);

            ThemeManager.ThemeChanged += OnThemeChanged;
            PerformLayout();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        public void SetState(bool isProtectionActive, bool isTempActive, int remainingSeconds)
        {
            bool modeChanged = (_isProtectionActive != isProtectionActive) ||
                               (_isTempActive != isTempActive) ||
                               ((_remainingSeconds > 0) != (remainingSeconds > 0));

            _isProtectionActive = isProtectionActive;
            _isTempActive = isTempActive;
            _remainingSeconds = remainingSeconds;

            bool isRtl = (RightToLeft == RightToLeft.Yes);
            _btn5Min.Text = isRtl ? "5 دقائق" : "5 min";
            _btn15Min.Text = isRtl ? "15 دقيقة" : "15 min";
            _btn30Min.Text = isRtl ? "30 دقيقة" : "30 min";
            _btnCancel.Text = Loc.Get("CancelTempAccess");

            if (_isTempActive && _remainingSeconds > 0)
            {
                _btn5Min.Visible = false;
                _btn5Min.Enabled = false;
                _btn15Min.Visible = false;
                _btn15Min.Enabled = false;
                _btn30Min.Visible = false;
                _btn30Min.Enabled = false;

                _btnCancel.Visible = true;
                _btnCancel.Enabled = true;

                string timeStr = string.Format("{0:D2}:{1:D2}", _remainingSeconds / 60, _remainingSeconds % 60);
                string newBadgeText = isRtl ? string.Format("متبقي {0}", timeStr) : string.Format("{0} remaining", timeStr);
                if (_countdownBadge.Text != newBadgeText)
                {
                    _countdownBadge.Text = newBadgeText;
                }
                _countdownBadge.Visible = true;
            }
            else
            {
                // When storage is protected, temporary access is available to grant
                // When storage is unlocked/disabled, temporary access is not relevant
                _btn5Min.Visible = true;
                _btn15Min.Visible = true;
                _btn30Min.Visible = true;

                _btn5Min.Enabled = _isProtectionActive;
                _btn15Min.Enabled = _isProtectionActive;
                _btn30Min.Enabled = _isProtectionActive;

                _btnCancel.Visible = false;
                _countdownBadge.Visible = false;
            }

            if (modeChanged)
            {
                PerformLayout();
            }
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (_btn5Min == null || _btn15Min == null || _btn30Min == null || _btnCancel == null || _countdownBadge == null) return;

            bool isRtl = (RightToLeft == RightToLeft.Yes);
            int pad = DpiManager.ScaleInt(SpacingTokens.Space14);
            int btnGap = DpiManager.ScaleInt(SpacingTokens.Space8);
            int btnH = DpiManager.ScaleInt(28);
            int btnW = isRtl ? DpiManager.ScaleInt(82) : DpiManager.ScaleInt(70);
            int cancelW = Math.Max(DpiManager.ScaleInt(130), AppButton.CalculateWidth(_btnCancel.Text, ButtonSize.Small, isRtl));
            int badgeW = Math.Max(DpiManager.ScaleInt(110), _countdownBadge.Width);
            int btnY = (Height - btnH) / 2;

            if (isRtl)
            {
                int curX = pad;
                if (_btnCancel.Visible)
                {
                    _btnCancel.Location = new Point(curX, btnY);
                    _btnCancel.Size = new Size(cancelW, btnH);
                    curX += cancelW + btnGap;

                    _countdownBadge.Location = new Point(curX, btnY + (btnH - DpiManager.ScaleInt(22)) / 2);
                    _countdownBadge.Size = new Size(badgeW, DpiManager.ScaleInt(22));
                    curX += badgeW + btnGap;
                }
                else
                {
                    _btn5Min.Location = new Point(curX, btnY);
                    _btn5Min.Size = new Size(btnW, btnH);
                    curX += btnW + btnGap;

                    _btn15Min.Location = new Point(curX, btnY);
                    _btn15Min.Size = new Size(btnW, btnH);
                    curX += btnW + btnGap;

                    _btn30Min.Location = new Point(curX, btnY);
                    _btn30Min.Size = new Size(btnW, btnH);
                }
            }
            else
            {
                int curX = Width - pad;
                if (_btnCancel.Visible)
                {
                    curX -= cancelW;
                    _btnCancel.Location = new Point(curX, btnY);
                    _btnCancel.Size = new Size(cancelW, btnH);
                    curX -= btnGap;

                    curX -= badgeW;
                    _countdownBadge.Location = new Point(curX, btnY + (btnH - DpiManager.ScaleInt(22)) / 2);
                    _countdownBadge.Size = new Size(badgeW, DpiManager.ScaleInt(22));
                }
                else
                {
                    curX -= btnW;
                    _btn30Min.Location = new Point(curX, btnY);
                    _btn30Min.Size = new Size(btnW, btnH);
                    curX -= (btnW + btnGap);

                    _btn15Min.Location = new Point(curX, btnY);
                    _btn15Min.Size = new Size(btnW, btnH);
                    curX -= (btnW + btnGap);

                    _btn5Min.Location = new Point(curX, btnY);
                    _btn5Min.Size = new Size(btnW, btnH);
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

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);

            // Container Background & subtle border
            using (SolidBrush bg = new SolidBrush(pal.Surface))
            {
                UiDrawingUtils.FillRoundedRectangle(g, bg, rect, rad);
            }
            using (Pen borderPen = new Pen(pal.BorderSubtle, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, borderPen, rect, rad);
            }

            int pad = DpiManager.ScaleInt(SpacingTokens.Space14);
            int btnGap = DpiManager.ScaleInt(SpacingTokens.Space8);
            Font labelFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            string title = Loc.Get("TempAccessTitle");
            if (string.IsNullOrEmpty(title) || title == "TempAccessTitle")
            {
                title = isRtl ? "الفتح المؤقت لوسائط التخزين" : "Temporary Access";
            }

            // Draw clock icon or bullet
            int iconSize = DpiManager.ScaleInt(6);
            int iconY = (Height - iconSize) / 2;

            int reservedActions;
            if (_btnCancel.Visible)
            {
                int cancelW = _btnCancel.Width > 0 ? _btnCancel.Width : Math.Max(DpiManager.ScaleInt(130), AppButton.CalculateWidth(_btnCancel.Text, ButtonSize.Small, isRtl));
                int badgeW = _countdownBadge.Width > 0 ? _countdownBadge.Width : DpiManager.ScaleInt(110);
                reservedActions = pad + cancelW + btnGap + badgeW + DpiManager.ScaleInt(SpacingTokens.Space12);
            }
            else
            {
                int btnW = isRtl ? DpiManager.ScaleInt(82) : DpiManager.ScaleInt(70);
                reservedActions = pad + (btnW * 3) + (btnGap * 2) + DpiManager.ScaleInt(SpacingTokens.Space12);
            }

            if (isRtl)
            {
                int iconX = Width - pad - iconSize;
                using (SolidBrush b = new SolidBrush(_isProtectionActive ? pal.Accent : pal.TextMuted))
                {
                    g.FillEllipse(b, iconX, iconY, iconSize, iconSize);
                }

                int textRight = iconX - DpiManager.ScaleInt(SpacingTokens.Space8);
                int textLeft = reservedActions;
                Rectangle textRect = new Rectangle(textLeft, 0, Math.Max(20, textRight - textLeft), Height);
                TextRenderer.DrawText(g, title, labelFont, textRect, _isProtectionActive ? pal.TextPrimary : pal.TextMuted,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
            else
            {
                int iconX = pad;
                using (SolidBrush b = new SolidBrush(_isProtectionActive ? pal.Accent : pal.TextMuted))
                {
                    g.FillEllipse(b, iconX, iconY, iconSize, iconSize);
                }

                int textLeft = iconX + iconSize + DpiManager.ScaleInt(SpacingTokens.Space8);
                int textRight = Width - reservedActions;
                Rectangle textRect = new Rectangle(textLeft, 0, Math.Max(20, textRight - textLeft), Height);
                TextRenderer.DrawText(g, title, labelFont, textRect, _isProtectionActive ? pal.TextPrimary : pal.TextMuted,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
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
    }
}
