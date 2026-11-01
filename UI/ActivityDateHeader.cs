// ═══════════════════════════════════════════════════════════════════════════
// ActivityDateHeader — Architectural Date Separator Header for Activity Page
// USB Shield Pro v2.1 (Phase 4.5 Redesign)
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using UsbShieldPro.Core;

namespace UsbShieldPro.UI
{
    public class ActivityDateHeader : Control
    {
        private string _dateText;

        public string DateText
        {
            get { return _dateText; }
            set
            {
                if (_dateText != value)
                {
                    _dateText = value ?? "";
                    Invalidate();
                }
            }
        }

        public ActivityDateHeader(string dateText)
        {
            _dateText = dateText ?? "";

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = false;
            BackColor = Color.Transparent;
            Height = DpiManager.ScaleInt(32);

            ThemeManager.ThemeChanged += OnThemeChanged;
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

            int pad = DpiManager.ScaleInt(16);
            int gap = DpiManager.ScaleInt(12);
            int lineY = Height / 2;

            Font font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            using (Font boldFont = new Font(font, FontStyle.Bold))
            {
                Size textSize = TextRenderer.MeasureText(g, _dateText, boldFont);
                int textW = textSize.Width;
                int textH = DpiManager.ScaleInt(isRtl ? 22 : 18);
                int textY = (Height - textH) / 2;

                if (isRtl)
                {
                    int textX = Width - pad - textW;
                    Rectangle textRect = new Rectangle(textX, textY, textW, textH);
                    TextRenderer.DrawText(g, _dateText, boldFont, textRect, pal.TextSecondary,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix);

                    int lineRight = textX - gap;
                    int lineLeft = pad;
                    if (lineRight > lineLeft)
                    {
                        using (Pen pen = new Pen(pal.BorderSubtle, 1f))
                        {
                            g.DrawLine(pen, lineLeft, lineY, lineRight, lineY);
                        }
                    }
                }
                else
                {
                    int textX = pad;
                    Rectangle textRect = new Rectangle(textX, textY, textW, textH);
                    TextRenderer.DrawText(g, _dateText, boldFont, textRect, pal.TextSecondary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                    int lineLeft = textX + textW + gap;
                    int lineRight = Width - pad;
                    if (lineRight > lineLeft)
                    {
                        using (Pen pen = new Pen(pal.BorderSubtle, 1f))
                        {
                            g.DrawLine(pen, lineLeft, lineY, lineRight, lineY);
                        }
                    }
                }
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
