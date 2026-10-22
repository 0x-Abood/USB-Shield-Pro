// ═══════════════════════════════════════════════════════════════════════════
// ProtectionStatusCard — Compact Real-Time Posture Component for v2.1
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
    internal class ProtectionStatusCard : Control
    {
        private string _title = "Storage Unlocked";
        private string _subtitle = "External USB storage access is currently allowed.";
        private StatusBadgeVariant _variant = StatusBadgeVariant.Neutral;
        private readonly AppButton _lockButton;
        private readonly AppButton _unlockButton;

        private bool _lastTempActive = false;
        private bool _lastIsProtected = false;
        private bool _lastIsUnprotected = false;
        private StatusBadgeVariant _lastVariant = StatusBadgeVariant.Neutral;
        private string _lastLockText = "";
        private string _lastUnlockText = "";
        private bool _hasEverSetStatus = false;

        public event EventHandler LockClicked;
        public event EventHandler UnlockClicked;

        public AppButton LockButton { get { return _lockButton; } }
        public AppButton UnlockButton { get { return _unlockButton; } }
        public string Title { get { return _title; } }
        public string Subtitle { get { return _subtitle; } }
        public StatusBadgeVariant Variant { get { return _variant; } }
        public int PreferredHeight
        {
            get
            {
                int w = (Width > 0) ? Width : DpiManager.ScaleInt(800);
                return GetPreferredHeight(w);
            }
        }

        public int GetPreferredHeight(int availableWidth)
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            Font subFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            int lockW = (_lockButton != null && _lockButton.Width > 0)
                ? _lockButton.Width
                : Math.Max(DpiManager.ScaleInt(isRtl ? 140 : 135), AppButton.CalculateWidth(_lockButton != null ? _lockButton.Text : "", ButtonSize.Medium, isRtl));
            int unlockW = (_unlockButton != null && _unlockButton.Width > 0)
                ? _unlockButton.Width
                : Math.Max(DpiManager.ScaleInt(isRtl ? 140 : 135), AppButton.CalculateWidth(_unlockButton != null ? _unlockButton.Text : "", ButtonSize.Medium, isRtl));
            int btnGap = DpiManager.ScaleInt(SpacingTokens.Space10);
            int btnPad = DpiManager.ScaleInt(SpacingTokens.Space14);
            int actionGap = DpiManager.ScaleInt(SpacingTokens.Space16);
            int reservedActionW = btnPad + lockW + unlockW + btnGap + actionGap;

            int dotPad = DpiManager.ScaleInt(SpacingTokens.Space16);
            int dotSize = DpiManager.ScaleInt(10);
            int dotGap = DpiManager.ScaleInt(SpacingTokens.Space10);

            int textW = Math.Max(40, availableWidth - reservedActionW - dotPad - dotSize - dotGap);

            TextFormatFlags flags = TextFormatFlags.WordBreak | (isRtl ? (TextFormatFlags.RightToLeft | TextFormatFlags.Right) : TextFormatFlags.Left);
            Size subSize = TextRenderer.MeasureText(_subtitle ?? "", subFont, new Size(textW, int.MaxValue), flags);

            int titleY = DpiManager.ScaleInt(11);
            int subY = titleY + titleFont.Height + DpiManager.ScaleInt(3);
            int bottomPad = DpiManager.ScaleInt(isRtl ? 12 : 8);
            int calculatedH = subY + subSize.Height + bottomPad;

            int baseMinH = DpiManager.ScaleInt(72);
            return Math.Max(baseMinH, calculatedH);
        }

        public ProtectionStatusCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            TabStop = false;
            RightToLeft = Loc.RtlValue;

            _lockButton = new AppButton();
            _lockButton.SizeVariant = ButtonSize.Medium;
            _lockButton.Variant = ButtonVariant.Primary;
            _lockButton.Text = Loc.Get("QuickLock");
            _lockButton.Click += delegate { if (LockClicked != null) LockClicked(this, EventArgs.Empty); };

            _unlockButton = new AppButton();
            _unlockButton.SizeVariant = ButtonSize.Medium;
            _unlockButton.Variant = ButtonVariant.Secondary;
            _unlockButton.Text = Loc.Get("QuickUnlock");
            _unlockButton.Click += delegate { if (UnlockClicked != null) UnlockClicked(this, EventArgs.Empty); };

            Controls.Add(_lockButton);
            Controls.Add(_unlockButton);

            Height = PreferredHeight;

            ThemeManager.ThemeChanged += OnThemeChanged;
            PerformLayout();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        public void SetStatus(string title, string subtitle, StatusBadgeVariant variant, bool isProtected, bool isUnprotected, bool tempActive)
        {
            RightToLeft = Loc.RtlValue;
            _title = title ?? "";
            _subtitle = subtitle ?? "";
            _variant = variant;

            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            string lockText = Loc.Get("QuickLock");
            string unlockText = Loc.Get("QuickUnlock");

            if (tempActive)
            {
                _lockButton.Text = Loc.Get("CancelTempAccess");
                _lockButton.Variant = ButtonVariant.Destructive;
                _lockButton.Enabled = true;

                _unlockButton.Text = unlockText;
                _unlockButton.Variant = ButtonVariant.Secondary;
                _unlockButton.Enabled = false;
            }
            else if (isProtected)
            {
                // When already protected: primary action is to unlock or temporary access
                _unlockButton.Text = unlockText;
                _unlockButton.Variant = ButtonVariant.Primary;
                _unlockButton.Enabled = true;

                _lockButton.Text = lockText;
                _lockButton.Variant = ButtonVariant.Secondary;
                _lockButton.Enabled = false;
            }
            else
            {
                // When unlocked / unprotected: primary action is to lock
                _lockButton.Text = lockText;
                _lockButton.Variant = ButtonVariant.Primary;
                _lockButton.Enabled = true;

                _unlockButton.Text = unlockText;
                _unlockButton.Variant = ButtonVariant.Secondary;
                _unlockButton.Enabled = false;
            }

            bool modeChanged = !_hasEverSetStatus ||
                               (_lastTempActive != tempActive) ||
                               (_lastIsProtected != isProtected) ||
                               (_lastIsUnprotected != isUnprotected) ||
                               (_lastVariant != variant) ||
                               (_lastLockText != _lockButton.Text) ||
                               (_lastUnlockText != _unlockButton.Text);

            _hasEverSetStatus = true;
            _lastTempActive = tempActive;
            _lastIsProtected = isProtected;
            _lastIsUnprotected = isUnprotected;
            _lastVariant = variant;
            _lastLockText = _lockButton.Text;
            _lastUnlockText = _unlockButton.Text;

            if (modeChanged)
            {
                PerformLayout();
            }
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (_lockButton == null || _unlockButton == null) return;

            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int pad = DpiManager.ScaleInt(SpacingTokens.Space14);
            int btnGap = DpiManager.ScaleInt(SpacingTokens.Space10);
            int btnH = DpiManager.ScaleInt(34);
            int lockW = Math.Max(DpiManager.ScaleInt(isRtl ? 140 : 135), AppButton.CalculateWidth(_lockButton.Text, ButtonSize.Medium, isRtl));
            int unlockW = Math.Max(DpiManager.ScaleInt(isRtl ? 140 : 135), AppButton.CalculateWidth(_unlockButton.Text, ButtonSize.Medium, isRtl));
            int btnY = (Height - btnH) / 2;

            if (isRtl)
            {
                // In RTL, buttons go to the left side
                // Lock button first on far left, Unlock button next to it
                _lockButton.Location = new Point(pad, btnY);
                _lockButton.Size = new Size(lockW, btnH);

                _unlockButton.Location = new Point(pad + lockW + btnGap, btnY);
                _unlockButton.Size = new Size(unlockW, btnH);
            }
            else
            {
                // In LTR, buttons go to the right side
                // Unlock button, then Lock button on far right
                int lockX = Width - pad - lockW;
                _lockButton.Location = new Point(lockX, btnY);
                _lockButton.Size = new Size(lockW, btnH);

                int unlockX = lockX - btnGap - unlockW;
                _unlockButton.Location = new Point(unlockX, btnY);
                _unlockButton.Size = new Size(unlockW, btnH);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);

            // Card Background & subtle border
            using (SolidBrush bg = new SolidBrush(pal.Surface))
            {
                UiDrawingUtils.FillRoundedRectangle(g, bg, rect, rad);
            }
            using (Pen borderPen = new Pen(pal.BorderSubtle, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, borderPen, rect, rad);
            }

            int dotSize = DpiManager.ScaleInt(10);

            Color statusColor;
            switch (_variant)
            {
                case StatusBadgeVariant.Success:
                    statusColor = pal.Success;
                    break;
                case StatusBadgeVariant.Warning:
                    statusColor = pal.Warning;
                    break;
                case StatusBadgeVariant.Danger:
                    statusColor = pal.Danger;
                    break;
                case StatusBadgeVariant.Info:
                    statusColor = pal.Info;
                    break;
                default:
                    statusColor = pal.TextMuted;
                    break;
            }

            Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            Font subFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            // Calculate exact reserved width for buttons plus breathing room
            int lockW = (_lockButton != null && _lockButton.Width > 0) ? _lockButton.Width : Math.Max(DpiManager.ScaleInt(isRtl ? 140 : 135), AppButton.CalculateWidth(_lockButton.Text, ButtonSize.Medium, isRtl));
            int unlockW = (_unlockButton != null && _unlockButton.Width > 0) ? _unlockButton.Width : Math.Max(DpiManager.ScaleInt(isRtl ? 140 : 135), AppButton.CalculateWidth(_unlockButton.Text, ButtonSize.Medium, isRtl));
            int btnGap = DpiManager.ScaleInt(SpacingTokens.Space10);
            int btnPad = DpiManager.ScaleInt(SpacingTokens.Space14);
            int actionGap = DpiManager.ScaleInt(SpacingTokens.Space16);
            int reservedActionW = btnPad + lockW + unlockW + btnGap + actionGap;

            int dotPad = DpiManager.ScaleInt(SpacingTokens.Space16);
            int dotGap = DpiManager.ScaleInt(SpacingTokens.Space10);

            int titleY = DpiManager.ScaleInt(11);
            int subY = titleY + titleFont.Height + DpiManager.ScaleInt(3);
            int subH = Math.Max(subFont.Height, Height - subY - DpiManager.ScaleInt(4));

            // Dot vertically centered with title text
            int dotY = titleY + (titleFont.Height - dotSize) / 2;

            if (isRtl)
            {
                // In RTL:
                // RIGHT: status dot + title + subtitle
                // LEFT: action buttons (reservedActionW strictly protects them)
                int dotX = Width - dotPad - dotSize;
                Rectangle dotRect = new Rectangle(dotX, dotY, dotSize, dotSize);
                using (SolidBrush db = new SolidBrush(statusColor))
                {
                    g.FillEllipse(db, dotRect);
                }

                int textRight = dotX - dotGap;
                int textLeft = reservedActionW;
                int textW = Math.Max(20, textRight - textLeft);

                Rectangle titleRect = new Rectangle(textLeft, titleY, textW, titleFont.Height + DpiManager.ScaleInt(2));
                TextRenderer.DrawText(g, _title, titleFont, titleRect, pal.TextPrimary,
                    TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

                Rectangle subRect = new Rectangle(textLeft, subY, textW, subH);
                TextRenderer.DrawText(g, _subtitle, subFont, subRect, pal.TextMuted,
                    TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.Top | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix);
            }
            else
            {
                // In LTR:
                // LEFT: status dot + title + subtitle
                // RIGHT: action buttons (reservedActionW strictly protects them)
                int dotX = dotPad;
                Rectangle dotRect = new Rectangle(dotX, dotY, dotSize, dotSize);
                using (SolidBrush db = new SolidBrush(statusColor))
                {
                    g.FillEllipse(db, dotRect);
                }

                int textLeft = dotX + dotSize + dotGap;
                int textRight = Width - reservedActionW;
                int textW = Math.Max(20, textRight - textLeft);

                Rectangle titleRect = new Rectangle(textLeft, titleY, textW, titleFont.Height + DpiManager.ScaleInt(2));
                TextRenderer.DrawText(g, _title, titleFont, titleRect, pal.TextPrimary,
                    TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

                Rectangle subRect = new Rectangle(textLeft, subY, textW, subH);
                TextRenderer.DrawText(g, _subtitle, subFont, subRect, pal.TextMuted,
                    TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
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
