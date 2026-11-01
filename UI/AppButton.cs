// ═══════════════════════════════════════════════════════════════════════════
// AppButton — Tier 1 Button Primitive for v2.1
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
    public enum ButtonVariant
    {
        Primary,
        Secondary,
        Ghost,
        Destructive,
        DestructiveSecondary
    }

    public enum ButtonSize
    {
        Small,   // 28px base
        Medium,  // 34px base
        Large    // 42px base
    }

    public class AppButton : Control, IButtonControl
    {
        private ButtonVariant _variant = ButtonVariant.Primary;
        private ButtonSize _sizeVariant = ButtonSize.Medium;
        private Image _leadingIcon = null;
        private int _cornerRadius = RadiusTokens.RadiusMedium;
        private DialogResult _dialogResult = DialogResult.None;
        private bool _isDefault = false;

        private bool _isHovered = false;
        private bool _isPressed = false;

        public AppButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PushButton;

            UpdateTargetHeight();
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public ButtonVariant Variant
        {
            get { return _variant; }
            set { if (_variant != value) { _variant = value; Invalidate(); } }
        }

        public ButtonSize SizeVariant
        {
            get { return _sizeVariant; }
            set
            {
                if (_sizeVariant != value)
                {
                    _sizeVariant = value;
                    UpdateTargetHeight();
                    Invalidate();
                }
            }
        }

        public Image LeadingIcon
        {
            get { return _leadingIcon; }
            set { if (_leadingIcon != value) { _leadingIcon = value; Invalidate(); } }
        }

        public int CornerRadius
        {
            get { return _cornerRadius; }
            set { if (_cornerRadius != value) { _cornerRadius = value; Invalidate(); } }
        }

        // IButtonControl implementation
        public DialogResult DialogResult
        {
            get { return _dialogResult; }
            set { _dialogResult = value; }
        }

        public void NotifyDefault(bool value)
        {
            if (_isDefault != value)
            {
                _isDefault = value;
                Invalidate();
            }
        }

        public void PerformClick()
        {
            OnClick(EventArgs.Empty);
        }

        public static int CalculateWidth(string text, ButtonSize size, bool isRtl, Image leadingIcon = null)
        {
            Font font = (size == ButtonSize.Small)
                ? DpiManager.GetFont(TypographyRole.Caption, isRtl)
                : DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            int textW = TextRenderer.MeasureText(text ?? "", font).Width;
            int padH = DpiManager.ScaleInt(SpacingTokens.Space12) * 2;
            int iconW = 0;
            if (leadingIcon != null)
            {
                int iconSize = (size == ButtonSize.Small) ? DpiManager.ScaleInt(IconTokens.IconSm) : DpiManager.ScaleInt(IconTokens.IconMd);
                iconW = iconSize + DpiManager.ScaleInt(SpacingTokens.Space8);
            }
            int minW = (size == ButtonSize.Small) ? DpiManager.ScaleInt(80) : DpiManager.ScaleInt(100);
            return Math.Max(minW, textW + iconW + padH + DpiManager.ScaleInt(8));
        }

        private void UpdateTargetHeight()
        {
            int baseH = HeightTokens.HeightMd;
            switch (_sizeVariant)
            {
                case ButtonSize.Small: baseH = HeightTokens.HeightSm; break;
                case ButtonSize.Large: baseH = HeightTokens.HeightLg; break;
            }
            Height = DpiManager.ScaleInt(baseH);
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

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHovered = false;
            _isPressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Focus();
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _isPressed = false;
            Invalidate();
            base.OnMouseUp(e);
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

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                _isPressed = true;
                Invalidate();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (_isPressed && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
            {
                _isPressed = false;
                Invalidate();
                PerformClick();
                e.Handled = true;
            }
            base.OnKeyUp(e);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            AccessibleName = Text;
            Invalidate();
            base.OnTextChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes);
            int rad = DpiManager.ScaleInt(_cornerRadius);

            // Compute bounds
            Rectangle bounds = new Rectangle(0, 0, Width, Height);
            Rectangle fillRect = new Rectangle(1, 1, Width - 2, Height - 2);

            Color bgColor;
            Color borderColor;
            Color textColor;

            ResolveColors(pal, out bgColor, out borderColor, out textColor);

            // Clean background fill matching effective parent surface (eliminates corner artifacts)
            Color surfaceColor = UiDrawingUtils.GetEffectiveParentSurface(this);
            using (SolidBrush surfaceBrush = new SolidBrush(surfaceColor))
            {
                g.FillRectangle(surfaceBrush, ClientRectangle);
            }

            // Draw Background
            if (bgColor != Color.Transparent)
            {
                using (SolidBrush brush = new SolidBrush(bgColor))
                {
                    UiDrawingUtils.FillRoundedRectangle(g, brush, fillRect, rad);
                }
            }

            // Draw Border
            if (borderColor != Color.Transparent)
            {
                using (Pen pen = new Pen(borderColor, 1f))
                {
                    UiDrawingUtils.DrawRoundedRectangle(g, pen, fillRect, rad);
                }
            }

            // Draw Content (Leading Icon + Text)
            Font font = (_sizeVariant == ButtonSize.Small)
                ? DpiManager.GetFont(TypographyRole.Caption, isRtl)
                : DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            int iconSize = (_sizeVariant == ButtonSize.Small) ? DpiManager.ScaleInt(IconTokens.IconSm) : DpiManager.ScaleInt(IconTokens.IconMd);
            int paddingHorz = DpiManager.ScaleInt(SpacingTokens.Space12);
            int spacing = DpiManager.ScaleInt(SpacingTokens.Space8);

            int totalContentWidth = 0;
            Size textSize = TextRenderer.MeasureText(g, Text ?? "", font, Size.Empty, TextFormatFlags.SingleLine);
            totalContentWidth += textSize.Width;

            if (_leadingIcon != null)
            {
                totalContentWidth += iconSize + spacing;
            }

            int startX = (Width - totalContentWidth) / 2;
            if (startX < paddingHorz) startX = paddingHorz;

            if (isRtl)
            {
                if (_leadingIcon != null)
                {
                    int iconX = startX + totalContentWidth - iconSize;
                    int iconY = (Height - iconSize) / 2;

                    if (Enabled)
                    {
                        g.DrawImage(_leadingIcon, iconX, iconY, iconSize, iconSize);
                    }
                    else
                    {
                        ControlPaint.DrawImageDisabled(g, _leadingIcon, iconX, iconY, bgColor);
                    }

                    int textX = startX;
                    int textW = Math.Max(10, iconX - textX - spacing);
                    Rectangle textRect = new Rectangle(textX, 0, textW, Height);

                    TextRenderer.DrawText(g, Text ?? "", font, textRect, textColor,
                        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);
                }
                else
                {
                    Rectangle textRect = new Rectangle(0, 0, Width, Height);
                    TextRenderer.DrawText(g, Text ?? "", font, textRect, textColor,
                        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);
                }
            }
            else
            {
                if (_leadingIcon != null)
                {
                    int iconY = (Height - iconSize) / 2;
                    if (Enabled)
                    {
                        g.DrawImage(_leadingIcon, startX, iconY, iconSize, iconSize);
                    }
                    else
                    {
                        ControlPaint.DrawImageDisabled(g, _leadingIcon, startX, iconY, bgColor);
                    }

                    int curX = startX + iconSize + spacing;
                    int textW = Width - curX - paddingHorz;
                    Rectangle textRect = new Rectangle(curX, 0, Math.Max(textSize.Width + 4, textW), Height);
                    TextRenderer.DrawText(g, Text ?? "", font, textRect, textColor,
                        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);
                }
                else
                {
                    Rectangle textRect = new Rectangle(0, 0, Width, Height);
                    TextRenderer.DrawText(g, Text ?? "", font, textRect, textColor,
                        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);
                }
            }

            // Draw Focus Ring
            if (Focused && ShowFocusCues)
            {
                UiDrawingUtils.DrawFocusRing(g, fillRect, rad, pal.FocusRing);
            }
        }

        private void ResolveColors(IThemePalette pal, out Color bg, out Color border, out Color text)
        {
            if (!Enabled)
            {
                bg = (_variant == ButtonVariant.Ghost || _variant == ButtonVariant.DestructiveSecondary) ? Color.Transparent : pal.Surface;
                border = (_variant == ButtonVariant.Secondary || _variant == ButtonVariant.DestructiveSecondary) ? pal.BorderSubtle : Color.Transparent;
                text = pal.TextDisabled;
                return;
            }

            switch (_variant)
            {
                case ButtonVariant.Primary:
                    border = Color.Transparent;
                    text = Color.White;
                    if (_isPressed) bg = pal.AccentPressed;
                    else if (_isHovered) bg = pal.AccentHover;
                    else bg = pal.Accent;
                    break;

                case ButtonVariant.Secondary:
                    text = pal.TextPrimary;
                    border = pal.Border;
                    if (_isPressed) bg = pal.SurfaceElevated;
                    else if (_isHovered) bg = pal.SurfaceHover;
                    else bg = pal.Surface;
                    break;

                case ButtonVariant.Ghost:
                    border = Color.Transparent;
                    if (_isPressed) { bg = pal.SurfaceSelected; text = pal.TextPrimary; }
                    else if (_isHovered) { bg = pal.SurfaceHover; text = pal.TextPrimary; }
                    else { bg = Color.Transparent; text = pal.TextSecondary; }
                    break;

                case ButtonVariant.Destructive:
                    border = Color.Transparent;
                    text = Color.White;
                    if (_isPressed)
                    {
                        bg = Color.FromArgb(Math.Max(0, pal.Danger.R - 30), Math.Max(0, pal.Danger.G - 10), Math.Max(0, pal.Danger.B - 10));
                    }
                    else if (_isHovered)
                    {
                        bg = Color.FromArgb(Math.Min(255, pal.Danger.R + 25), Math.Min(255, pal.Danger.G + 15), Math.Min(255, pal.Danger.B + 15));
                    }
                    else
                    {
                        bg = pal.Danger;
                    }
                    break;

                case ButtonVariant.DestructiveSecondary:
                    border = pal.Danger;
                    text = pal.Danger;
                    if (_isPressed) bg = Color.FromArgb(40, pal.Danger);
                    else if (_isHovered) bg = Color.FromArgb(20, pal.Danger);
                    else bg = Color.Transparent;
                    break;

                default:
                    bg = pal.Surface;
                    border = pal.Border;
                    text = pal.TextPrimary;
                    break;
            }
        }
    }
}
