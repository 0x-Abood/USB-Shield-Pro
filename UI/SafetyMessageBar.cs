// ═══════════════════════════════════════════════════════════════════════════
// SafetyMessageBar — Quiet Informational Safety Banner for v2.1 Dashboard
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
    internal class SafetyMessageBar : Control
    {
        private string _text = "";
        private bool _isActive = false;

        public bool IsActive
        {
            get { return _isActive; }
            set { _isActive = value; Visible = value; }
        }

        public string MessageText
        {
            get { return _text; }
            set { if (_text != value) { _text = value; Invalidate(); } }
        }

        public SafetyMessageBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            TabStop = false;
            Height = DpiManager.ScaleInt(46);
            RightToLeft = Loc.RtlValue;
            _text = Loc.Get("SafetyBannerText");

            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public int GetPreferredHeight(int availableWidth)
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int pad = DpiManager.ScaleInt(SpacingTokens.Space14);
            int iconSize = DpiManager.ScaleInt(14);
            int gap = DpiManager.ScaleInt(SpacingTokens.Space10);
            int textW = Math.Max(20, availableWidth - (pad * 2) - iconSize - gap);
            Font font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            TextFormatFlags flags = TextFormatFlags.WordBreak | (isRtl ? (TextFormatFlags.RightToLeft | TextFormatFlags.Right) : TextFormatFlags.Left);
            Size measured = TextRenderer.MeasureText(_text ?? "", font, new Size(textW, int.MaxValue), flags);
            int vertPad = DpiManager.ScaleInt(SpacingTokens.Space6) * 2;
            int minH = DpiManager.ScaleInt(46);
            return Math.Max(minH, measured.Height + vertPad);
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

        public void UpdateLanguage()
        {
            RightToLeft = Loc.RtlValue;
            _text = Loc.Get("SafetyBannerText");
            Invalidate();
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

            // Container Surface & Border (restrained, no danger red)
            using (SolidBrush bg = new SolidBrush(pal.Surface))
            {
                UiDrawingUtils.FillRoundedRectangle(g, bg, rect, rad);
            }
            using (Pen borderPen = new Pen(pal.BorderSubtle, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, borderPen, rect, rad);
            }

            int pad = DpiManager.ScaleInt(SpacingTokens.Space14);
            int iconSize = DpiManager.ScaleInt(14);
            int iconY = (Height - iconSize) / 2;

            Font textFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            int padV = DpiManager.ScaleInt(SpacingTokens.Space6);
            int textH = Math.Max(10, Height - (padV * 2));

            if (isRtl)
            {
                // In RTL, Shield icon on the right, text on left
                int iconX = Width - pad - iconSize;
                DrawShieldIcon(g, pal.TextMuted, iconX, iconY, iconSize);

                int textRight = iconX - DpiManager.ScaleInt(SpacingTokens.Space10);
                int textLeft = pad;
                int textW = Math.Max(20, textRight - textLeft);

                Rectangle textRect = new Rectangle(textLeft, padV, textW, textH);
                TextRenderer.DrawText(g, _text, textFont, textRect, pal.TextSecondary,
                    TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix);
            }
            else
            {
                // In LTR, Shield icon on the left, text on right
                int iconX = pad;
                DrawShieldIcon(g, pal.TextMuted, iconX, iconY, iconSize);

                int textLeft = iconX + iconSize + DpiManager.ScaleInt(SpacingTokens.Space10);
                int textRight = Width - pad;
                int textW = Math.Max(20, textRight - textLeft);

                Rectangle textRect = new Rectangle(textLeft, padV, textW, textH);
                TextRenderer.DrawText(g, _text, textFont, textRect, pal.TextSecondary,
                    TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            }
        }

        private void DrawShieldIcon(Graphics g, Color color, int x, int y, int size)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                float left = x;
                float top = y;
                float right = x + size;
                float bottom = y + size;
                float midX = x + size / 2.0f;
                float waistY = y + size * 0.55f;

                p.AddLine(midX, top, right, top + size * 0.15f);
                p.AddLine(right, top + size * 0.15f, right, waistY);
                p.AddBezier(right, waistY, right, bottom * 0.85f, midX, bottom, midX, bottom);
                p.AddBezier(midX, bottom, left, bottom * 0.85f, left, waistY, left, waistY);
                p.AddLine(left, waistY, left, top + size * 0.15f);
                p.CloseFigure();

                using (SolidBrush b = new SolidBrush(color))
                {
                    g.FillPath(b, p);
                }
            }
        }
    }
}
