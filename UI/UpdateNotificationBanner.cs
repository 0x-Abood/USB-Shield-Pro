// ═══════════════════════════════════════════════════════════════════════════
// UpdateNotificationBanner — Non-Intrusive GitHub Release Notification Banner
// USB Shield Pro v2.1
// (c) 2026 0xAbood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using UsbShieldPro.Core;

namespace UsbShieldPro.UI
{
    /// <summary>
    /// Restrained, non-blocking notification banner displayed directly below the top bar
    /// when a newer verified release is detected on GitHub.
    /// </summary>
    public class UpdateNotificationBanner : Control
    {
        private string _latestVersion = "";
        private string _releaseUrl = UpdateChecker.DefaultReleasesUrl;
        private Rectangle _textRect = Rectangle.Empty;
        private Rectangle _closeRect = Rectangle.Empty;
        private bool _isHoveringText = false;
        private bool _isHoveringClose = false;

        public string LatestVersion
        {
            get { return _latestVersion; }
            set
            {
                _latestVersion = value ?? "";
                Invalidate();
            }
        }

        public string ReleaseUrl
        {
            get { return _releaseUrl; }
            set
            {
                _releaseUrl = value ?? UpdateChecker.DefaultReleasesUrl;
            }
        }

        public UpdateNotificationBanner()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            TabStop = false;
            Visible = false;
            Dock = DockStyle.Top;
            Height = DpiManager.ScaleInt(38);
            RightToLeft = Loc.RtlValue;

            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public void ShowUpdate(string latestVersion, string releaseUrl)
        {
            _latestVersion = latestVersion;
            _releaseUrl = releaseUrl;
            Visible = true;
            BringToFront();
            Invalidate();
        }

        public void Dismiss()
        {
            Visible = false;
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

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hoverText = !_textRect.IsEmpty && _textRect.Contains(e.Location);
            bool hoverClose = !_closeRect.IsEmpty && _closeRect.Contains(e.Location);

            if (hoverText != _isHoveringText || hoverClose != _isHoveringClose)
            {
                _isHoveringText = hoverText;
                _isHoveringClose = hoverClose;
                Cursor = (hoverText || hoverClose) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHoveringText = false;
            _isHoveringClose = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (!_closeRect.IsEmpty && _closeRect.Contains(e.Location))
                {
                    Dismiss();
                }
                else if (!_textRect.IsEmpty && _textRect.Contains(e.Location))
                {
                    UpdateChecker.OpenReleaseUrl(_releaseUrl);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;

            // Draw subtle accent/surface background
            using (SolidBrush bgBrush = new SolidBrush(pal.Surface))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            // Draw subtle bottom divider border
            using (Pen borderPen = new Pen(pal.BorderSubtle, 1f))
            {
                g.DrawLine(borderPen, 0, Height - 1, Width, Height - 1);
            }

            int pad = DpiManager.ScaleInt(16);
            int iconSize = DpiManager.ScaleInt(16);
            int iconY = (Height - iconSize) / 2;
            int closeBtnSize = DpiManager.ScaleInt(24);
            int closeBtnY = (Height - closeBtnSize) / 2;

            int closeBtnX = isRtl ? pad : (Width - pad - closeBtnSize);
            _closeRect = new Rectangle(closeBtnX, closeBtnY, closeBtnSize, closeBtnSize);

            // Draw Close Button (×)
            if (_isHoveringClose)
            {
                using (SolidBrush hoverBg = new SolidBrush(Color.FromArgb(20, pal.TextPrimary)))
                {
                    UiDrawingUtils.FillRoundedRectangle(g, hoverBg, _closeRect, DpiManager.ScaleInt(4));
                }
            }
            Font closeFont = DpiManager.GetFont(TypographyRole.Body, false);
            TextFormatFlags closeFlags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
            TextRenderer.DrawText(g, "\u2715", closeFont, _closeRect, _isHoveringClose ? pal.TextPrimary : pal.TextMuted, closeFlags);

            // Position and draw icon
            int iconX = isRtl ? (Width - pad - iconSize) : pad;
            Rectangle iconRect = new Rectangle(iconX, iconY, iconSize, iconSize);
            DrawUpdateBadgeIcon(g, pal.Accent, iconRect);

            // Position text
            int textLeft, textRight;
            if (isRtl)
            {
                textRight = iconX - DpiManager.ScaleInt(10);
                textLeft = closeBtnX + closeBtnSize + DpiManager.ScaleInt(10);
            }
            else
            {
                textLeft = iconX + iconSize + DpiManager.ScaleInt(10);
                textRight = closeBtnX - DpiManager.ScaleInt(10);
            }

            int textW = Math.Max(20, textRight - textLeft);
            _textRect = new Rectangle(textLeft, 0, textW, Height);

            string bannerTemplate = Loc.Get("UpdateBannerText");
            string bannerText = string.Format(bannerTemplate, _latestVersion);

            Font font = DpiManager.GetFont(TypographyRole.Body, isRtl);
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            if (isRtl) flags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
            else flags |= TextFormatFlags.Left;

            Color textColor = _isHoveringText ? pal.Accent : pal.TextPrimary;
            TextRenderer.DrawText(g, bannerText, font, _textRect, textColor, pal.Surface, flags);
        }

        private void DrawUpdateBadgeIcon(Graphics g, Color accentColor, Rectangle rect)
        {
            // Draw a crisp upward arrow inside a circle
            using (SolidBrush circleBrush = new SolidBrush(Color.FromArgb(32, accentColor)))
            {
                g.FillEllipse(circleBrush, rect);
            }

            using (Pen pen = new Pen(accentColor, 1.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                int cx = rect.X + rect.Width / 2;
                int cy = rect.Y + rect.Height / 2;
                int arrowH = rect.Height / 4;

                // Vertical shaft
                g.DrawLine(pen, cx, cy + arrowH, cx, cy - arrowH);

                // Arrowhead
                g.DrawLine(pen, cx - arrowH / 2, cy - arrowH / 2, cx, cy - arrowH);
                g.DrawLine(pen, cx + arrowH / 2, cy - arrowH / 2, cx, cy - arrowH);
            }
        }
    }
}
