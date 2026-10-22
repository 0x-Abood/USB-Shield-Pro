// ═══════════════════════════════════════════════════════════════════════════
// StatusBadge — Tier 1 Status Badge Primitive for v2.1
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
    public enum StatusBadgeVariant
    {
        Success,
        Warning,
        Danger,
        Info,
        Neutral
    }

    public class StatusBadge : Control
    {
        private StatusBadgeVariant _variant = StatusBadgeVariant.Neutral;
        private bool _showIndicatorDot = true;
        private Image _customIcon = null;

        public StatusBadge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = false;
            Text = "Active";
            UpdateDimensions();

            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public StatusBadgeVariant Variant
        {
            get { return _variant; }
            set { if (_variant != value) { _variant = value; Invalidate(); } }
        }

        public bool ShowIndicatorDot
        {
            get { return _showIndicatorDot; }
            set { if (_showIndicatorDot != value) { _showIndicatorDot = value; UpdateDimensions(); Invalidate(); } }
        }

        public Image CustomIcon
        {
            get { return _customIcon; }
            set { if (_customIcon != value) { _customIcon = value; UpdateDimensions(); Invalidate(); } }
        }

        private void UpdateDimensions()
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            Font font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            TextFormatFlags flags = TextFormatFlags.SingleLine | (isRtl ? TextFormatFlags.RightToLeft : 0);
            Size textSize = TextRenderer.MeasureText(Text ?? "", font, new Size(int.MaxValue, int.MaxValue), flags);

            int baseH = 22;
            int h = DpiManager.ScaleInt(baseH);
            int padH = DpiManager.ScaleInt(SpacingTokens.Space8);
            int dotW = (_showIndicatorDot || _customIcon != null) ? (DpiManager.ScaleInt(8) + DpiManager.ScaleInt(SpacingTokens.Space4)) : 0;
            int buffer = isRtl ? DpiManager.ScaleInt(8) : DpiManager.ScaleInt(2);

            int w = padH * 2 + dotW + textSize.Width + buffer;
            Size = new Size(w, h);
        }

        protected override void OnRightToLeftChanged(EventArgs e)
        {
            UpdateDimensions();
            Invalidate();
            base.OnRightToLeftChanged(e);
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
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

        protected override void OnTextChanged(EventArgs e)
        {
            UpdateDimensions();
            Invalidate();
            base.OnTextChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;

            Color bg;
            Color fg;
            Color border;

            switch (_variant)
            {
                case StatusBadgeVariant.Success:
                    bg = pal.SuccessSoft;
                    fg = pal.Success;
                    border = pal.Success;
                    break;
                case StatusBadgeVariant.Warning:
                    bg = pal.WarningSoft;
                    fg = pal.Warning;
                    border = pal.Warning;
                    break;
                case StatusBadgeVariant.Danger:
                    bg = pal.DangerSoft;
                    fg = pal.Danger;
                    border = pal.Danger;
                    break;
                case StatusBadgeVariant.Info:
                    bg = pal.InfoSoft;
                    fg = pal.Info;
                    border = pal.Info;
                    break;
                case StatusBadgeVariant.Neutral:
                default:
                    bg = pal.SurfaceElevated;
                    fg = pal.TextSecondary;
                    border = pal.Border;
                    break;
            }

            // Clean background fill matching effective parent surface (eliminates corner artifacts)
            Color surfaceColor = UiDrawingUtils.GetEffectiveParentSurface(this);
            using (SolidBrush surfaceBrush = new SolidBrush(surfaceColor))
            {
                g.FillRectangle(surfaceBrush, ClientRectangle);
            }

            Rectangle rect = new Rectangle(1, 1, Width - 2, Height - 2);
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusSmall);

            // Fill background
            using (SolidBrush b = new SolidBrush(bg))
            {
                UiDrawingUtils.FillRoundedRectangle(g, b, rect, rad);
            }

            // Draw border
            using (Pen pen = new Pen(border, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, pen, rect, rad);
            }

            // Draw Dot or Icon + Text
            Font font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            int padH = DpiManager.ScaleInt(SpacingTokens.Space8);
            int dotSize = DpiManager.ScaleInt(6);

            if (isRtl)
            {
                int curX = Width - padH;
                if (_customIcon != null)
                {
                    int iconDim = DpiManager.ScaleInt(12);
                    curX -= iconDim;
                    int iconY = (Height - iconDim) / 2;
                    g.DrawImage(_customIcon, curX, iconY, iconDim, iconDim);
                    curX -= DpiManager.ScaleInt(SpacingTokens.Space4);
                }
                else if (_showIndicatorDot)
                {
                    curX -= dotSize;
                    int dotY = (Height - dotSize) / 2;
                    using (SolidBrush db = new SolidBrush(fg))
                    {
                        g.FillEllipse(db, curX, dotY, dotSize, dotSize);
                    }
                    curX -= DpiManager.ScaleInt(SpacingTokens.Space4);
                }

                bool isArabic = UiDrawingUtils.IsArabicText(Text, isRtl);
                int yOffset = UiDrawingUtils.GetBadgeTextVerticalOffset(isArabic);
                Rectangle textRect = new Rectangle(padH, yOffset, curX - padH, Height);
                TextRenderer.DrawText(g, Text ?? "", font, textRect, fg,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoClipping);
            }
            else
            {
                int curX = padH;
                if (_customIcon != null)
                {
                    int iconDim = DpiManager.ScaleInt(12);
                    int iconY = (Height - iconDim) / 2;
                    g.DrawImage(_customIcon, curX, iconY, iconDim, iconDim);
                    curX += iconDim + DpiManager.ScaleInt(SpacingTokens.Space4);
                }
                else if (_showIndicatorDot)
                {
                    int dotY = (Height - dotSize) / 2;
                    using (SolidBrush db = new SolidBrush(fg))
                    {
                        g.FillEllipse(db, curX, dotY, dotSize, dotSize);
                    }
                    curX += dotSize + DpiManager.ScaleInt(SpacingTokens.Space4);
                }

                bool isArabic = UiDrawingUtils.IsArabicText(Text, false);
                int yOffset = UiDrawingUtils.GetBadgeTextVerticalOffset(isArabic);
                Rectangle textRect = new Rectangle(curX, yOffset, Width - padH - curX, Height);
                TextRenderer.DrawText(g, Text ?? "", font, textRect, fg,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoClipping);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && Parent is AppTopBar)
            {
                Form form = FindForm();
                if (form != null)
                {
                    ReleaseCapture();
                    SendMessage(form.Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
                }
            }
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            if (Parent is AppTopBar)
            {
                Form form = FindForm();
                if (form is MainForm)
                {
                    ((MainForm)form).ToggleMaximizeRestore();
                }
            }
        }
    }
}
