// ═══════════════════════════════════════════════════════════════════════════
// SummaryMetricCard — Compact Architectural Metric Card for v2.1
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
    internal class SummaryMetricCard : Control
    {
        private string _valueText = "0";
        private string _titleText = "";
        private string _subtitleText = "";
        private string _badgeText = "";
        private StatusBadgeVariant _badgeVariant = StatusBadgeVariant.Neutral;
        private bool _isHovered = false;

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

        public SummaryMetricCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.StaticText;
            Height = DpiManager.ScaleInt(70);

            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
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

            // Architectural Surface & Subtle Border (no decorative colored top bar)
            Color bgColor = _isHovered ? pal.SurfaceElevated : pal.Surface;
            using (SolidBrush bg = new SolidBrush(bgColor))
            {
                UiDrawingUtils.FillRoundedRectangle(g, bg, rect, rad);
            }
            using (Pen borderPen = new Pen(_isHovered ? pal.Border : pal.BorderSubtle, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, borderPen, rect, rad);
            }

            int pad = DpiManager.ScaleInt(SpacingTokens.Space14);
            Font valFont = DpiManager.GetFont(TypographyRole.Display, isRtl);
            Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            Font subFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            // Measure value text
            Size valSize = TextRenderer.MeasureText(g, _valueText, valFont);
            int valW = Math.Max(valSize.Width, DpiManager.ScaleInt(28));

            Font badgeFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            int badgeH = DpiManager.ScaleInt(isRtl ? 22 : 20);
            Size sz = TextRenderer.MeasureText(g, _badgeText ?? "", badgeFont);
            int badgeW = sz.Width + DpiManager.ScaleInt(14);
            int badgeY = DpiManager.ScaleInt(12);

            if (isRtl)
            {
                // In RTL, Value is on the far right
                int valX = Width - pad - valW;
                Rectangle valRect = new Rectangle(valX, 0, valW, Height);
                TextRenderer.DrawText(g, _valueText, valFont, valRect, pal.TextPrimary,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix);

                int textRight = valX - DpiManager.ScaleInt(SpacingTokens.Space12);
                int badgeX = pad;
                int titleLeft = !string.IsNullOrEmpty(_badgeText) ? (badgeX + badgeW + DpiManager.ScaleInt(SpacingTokens.Space8)) : pad;
                int titleW = Math.Max(20, textRight - titleLeft);
                int titleY = DpiManager.ScaleInt(14);

                Rectangle titleRect = new Rectangle(titleLeft, titleY, titleW, titleFont.Height + DpiManager.ScaleInt(4));
                TextRenderer.DrawText(g, _titleText, titleFont, titleRect, pal.TextPrimary,
                    TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);

                int subY = titleY + titleFont.Height + DpiManager.ScaleInt(4);
                int subW = Math.Max(20, textRight - pad);
                Rectangle subRect = new Rectangle(pad, subY, subW, subFont.Height + DpiManager.ScaleInt(4));
                TextRenderer.DrawText(g, _subtitleText, subFont, subRect, pal.TextMuted,
                    TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);

                if (!string.IsNullOrEmpty(_badgeText))
                {
                    DrawBadgePill(g, pal, badgeX, badgeY, badgeW, badgeH, isRtl);
                }
            }
            else
            {
                // In LTR, Value is on the far left
                Rectangle valRect = new Rectangle(pad, 0, valW, Height);
                TextRenderer.DrawText(g, _valueText, valFont, valRect, pal.TextPrimary,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);

                int textLeft = pad + valW + DpiManager.ScaleInt(SpacingTokens.Space12);
                int badgeX = Width - pad - badgeW;
                int titleW = !string.IsNullOrEmpty(_badgeText) ? Math.Max(20, badgeX - DpiManager.ScaleInt(SpacingTokens.Space8) - textLeft) : (Width - pad - textLeft);
                int titleY = DpiManager.ScaleInt(14);

                Rectangle titleRect = new Rectangle(textLeft, titleY, titleW, titleFont.Height + DpiManager.ScaleInt(4));
                TextRenderer.DrawText(g, _titleText, titleFont, titleRect, pal.TextPrimary,
                    TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);

                int subY = titleY + titleFont.Height + DpiManager.ScaleInt(4);
                int subW = Math.Max(20, Width - pad - textLeft);
                Rectangle subRect = new Rectangle(textLeft, subY, subW, subFont.Height + DpiManager.ScaleInt(4));
                TextRenderer.DrawText(g, _subtitleText, subFont, subRect, pal.TextMuted,
                    TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);

                if (!string.IsNullOrEmpty(_badgeText))
                {
                    DrawBadgePill(g, pal, badgeX, badgeY, badgeW, badgeH, isRtl);
                }
            }
        }

        private void DrawBadgePill(Graphics g, IThemePalette pal, int x, int y, int w, int h, bool isRtl)
        {
            Color fg, bg, bd;
            switch (_badgeVariant)
            {
                case StatusBadgeVariant.Success:
                    fg = pal.Success;
                    bg = pal.SuccessSoft;
                    bd = pal.Success;
                    break;
                case StatusBadgeVariant.Warning:
                    fg = pal.Warning;
                    bg = pal.WarningSoft;
                    bd = pal.Warning;
                    break;
                case StatusBadgeVariant.Danger:
                    fg = pal.Danger;
                    bg = pal.DangerSoft;
                    bd = pal.Danger;
                    break;
                case StatusBadgeVariant.Info:
                    fg = pal.Info;
                    bg = pal.InfoSoft;
                    bd = pal.Info;
                    break;
                default:
                    fg = pal.TextSecondary;
                    bg = pal.SurfaceElevated;
                    bd = pal.BorderSubtle;
                    break;
            }

            Rectangle pillRect = new Rectangle(x, y, w, h);
            using (SolidBrush b = new SolidBrush(bg))
            {
                UiDrawingUtils.FillRoundedRectangle(g, b, pillRect, h / 2);
            }
            using (Pen p = new Pen(bd, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, p, pillRect, h / 2);
            }

            Font badgeFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping;
            if (isRtl) flags |= TextFormatFlags.RightToLeft;
            bool isArabic = UiDrawingUtils.IsArabicText(_badgeText, isRtl);
            int yOffset = UiDrawingUtils.GetBadgeTextVerticalOffset(isArabic);
            Rectangle textRect = new Rectangle(x, y + yOffset, w, h);
            TextRenderer.DrawText(g, _badgeText, badgeFont, textRect, fg, flags);
        }
    }
}
