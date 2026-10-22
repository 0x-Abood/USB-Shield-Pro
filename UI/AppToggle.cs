// ═══════════════════════════════════════════════════════════════════════════
// AppToggle — Tier 1 Binary Toggle Switch Primitive for v2.1
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
    public class AppToggle : Control
    {
        private bool _checked = false;
        private bool _isHovered = false;
        private bool _useSemanticSuccessTrack = false;

        public event EventHandler CheckedChanged;

        public bool UseSemanticSuccessTrack
        {
            get { return _useSemanticSuccessTrack; }
            set
            {
                if (_useSemanticSuccessTrack != value)
                {
                    _useSemanticSuccessTrack = value;
                    Invalidate();
                }
            }
        }

        public AppToggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.CheckButton;

            UpdateDimensions();
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    Invalidate();
                    OnCheckedChanged();
                }
            }
        }

        private void OnCheckedChanged()
        {
            if (CheckedChanged != null)
            {
                CheckedChanged(this, EventArgs.Empty);
            }
        }

        private void UpdateDimensions()
        {
            int baseW = 44;
            int baseH = 26;
            if (!string.IsNullOrEmpty(Text))
            {
                // Has label
                Font font = DpiManager.GetFont(TypographyRole.Body, RightToLeft == RightToLeft.Yes);
                Size textSize = TextRenderer.MeasureText(Text, font);
                int totalW = DpiManager.ScaleInt(baseW + SpacingTokens.Space8) + textSize.Width + DpiManager.ScaleInt(SpacingTokens.Space8);
                int totalH = Math.Max(DpiManager.ScaleInt(baseH), textSize.Height);
                Size = new Size(totalW, totalH);
            }
            else
            {
                Size = new Size(DpiManager.ScaleInt(baseW), DpiManager.ScaleInt(baseH));
            }
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
            AccessibleName = Text;
            UpdateDimensions();
            Invalidate();
            base.OnTextChanged(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            Focus();
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                Checked = !Checked;
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes);

            // Clean background fill matching effective parent surface (eliminates corner artifacts)
            Color surfaceColor = UiDrawingUtils.GetEffectiveParentSurface(this);
            using (SolidBrush surfaceBrush = new SolidBrush(surfaceColor))
            {
                g.FillRectangle(surfaceBrush, ClientRectangle);
            }

            int trackW = DpiManager.ScaleInt(44);
            int trackH = DpiManager.ScaleInt(24);

            // Track location with 1px stroke inset for zero edge clipping
            int trackX = isRtl ? (Width - trackW) : 0;
            int trackY = (Height - trackH) / 2;
            Rectangle trackRect = new Rectangle(trackX + 1, trackY + 1, trackW - 2, trackH - 2);
            int trackRad = trackRect.Height / 2;

            int thumbSize = DpiManager.ScaleInt(18);
            int thumbMargin = (trackRect.Height - thumbSize) / 2;

            // Track Colors
            Color trackBg;
            Color trackBorder;
            Color thumbColor;

            if (!Enabled)
            {
                trackBg = pal.Surface;
                trackBorder = pal.BorderSubtle;
                thumbColor = pal.TextDisabled;
            }
            else if (_checked)
            {
                if (_useSemanticSuccessTrack)
                {
                    trackBg = _isHovered ? (pal.IsDark ? Color.FromArgb(60, 215, 140) : Color.FromArgb(25, 130, 75)) : pal.Success;
                }
                else
                {
                    trackBg = _isHovered ? pal.AccentHover : pal.Accent;
                }
                trackBorder = trackBg;
                thumbColor = Color.White;
            }
            else
            {
                trackBg = _isHovered ? pal.SurfaceHover : pal.SurfaceElevated;
                trackBorder = pal.Border;
                thumbColor = pal.TextSecondary;
            }

            // Fill Track
            using (SolidBrush b = new SolidBrush(trackBg))
            {
                UiDrawingUtils.FillRoundedRectangle(g, b, trackRect, trackRad);
            }

            // Draw Track Border (when unchecked or subtle)
            if (!_checked || !Enabled)
            {
                using (Pen pen = new Pen(trackBorder, 1.2f))
                {
                    UiDrawingUtils.DrawRoundedRectangle(g, pen, trackRect, trackRad);
                }
            }

            // Compute Thumb Location:
            // Standard OS Convention (Windows 11 Fluent UI / iOS / Android):
            // Unchecked (OFF) = Left, Checked (ON) = Right across both LTR and RTL.
            int thumbX = _checked
                ? (trackRect.Right - thumbMargin - thumbSize)
                : (trackRect.Left + thumbMargin);
            int thumbY = trackRect.Top + thumbMargin;
            Rectangle thumbRect = new Rectangle(thumbX, thumbY, thumbSize, thumbSize);

            // Draw Thumb
            using (SolidBrush tb = new SolidBrush(thumbColor))
            {
                g.FillEllipse(tb, thumbRect);
            }

            // Draw Focus Ring (independent of track color)
            if (Focused && ShowFocusCues)
            {
                Rectangle focusBounds = new Rectangle(trackRect.X - 2, trackRect.Y - 2, trackRect.Width + 4, trackRect.Height + 4);
                using (Pen fpen = new Pen(pal.FocusRing, 2f))
                {
                    fpen.Alignment = PenAlignment.Outset;
                    UiDrawingUtils.DrawRoundedRectangle(g, fpen, trackRect, trackRad + 2);
                }
            }

            // Draw Optional Text Label
            if (!string.IsNullOrEmpty(Text))
            {
                Font font = DpiManager.GetFont(TypographyRole.Body, isRtl);
                Color textColor = Enabled ? pal.TextPrimary : pal.TextDisabled;
                int textSpacing = DpiManager.ScaleInt(SpacingTokens.Space8);

                Rectangle labelRect;
                TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;

                if (isRtl)
                {
                    labelRect = new Rectangle(0, 0, Width - trackW - textSpacing, Height);
                    flags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
                }
                else
                {
                    labelRect = new Rectangle(trackW + textSpacing, 0, Width - trackW - textSpacing, Height);
                    flags |= TextFormatFlags.Left;
                }

                TextRenderer.DrawText(g, Text, font, labelRect, textColor, flags);
            }
        }
    }
}
