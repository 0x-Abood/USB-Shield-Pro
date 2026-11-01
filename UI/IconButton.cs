// ═══════════════════════════════════════════════════════════════════════════
// IconButton — Tier 1 Compact Icon Button Primitive for v2.1
// USB Shield Pro v2.1
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace UsbShieldPro.UI
{
    public enum IconButtonSize
    {
        Small,    // 26px
        Standard  // 32px
    }

    public enum IconButtonType
    {
        Custom,
        Overflow,
        Close,
        Minimize,
        Maximize,
        Restore,
        Refresh,
        ChevronDown,
        ChevronUp,
        ChevronLeft,
        ChevronRight,
        Copy
    }

    public class IconButton : Control, IButtonControl
    {
        private IconButtonSize _sizeVariant = IconButtonSize.Standard;
        private IconButtonType _buttonType = IconButtonType.Custom;
        private Image _customIcon = null;
        private ToolTip _toolTip = null;
        private string _toolTipText = "";
        private DialogResult _dialogResult = DialogResult.None;

        private bool _isHovered = false;
        private bool _isPressed = false;

        public IconButton()
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
            BackColor = Color.Transparent;

            _toolTip = new ToolTip();
            _toolTip.InitialDelay = 400;
            _toolTip.ReshowDelay = 200;
            _toolTip.AutoPopDelay = 5000;

            UpdateDimensions();
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public IconButtonSize SizeVariant
        {
            get { return _sizeVariant; }
            set
            {
                if (_sizeVariant != value)
                {
                    _sizeVariant = value;
                    UpdateDimensions();
                    Invalidate();
                }
            }
        }

        public IconButtonType ButtonType
        {
            get { return _buttonType; }
            set { if (_buttonType != value) { _buttonType = value; Invalidate(); } }
        }

        public Image CustomIcon
        {
            get { return _customIcon; }
            set { if (_customIcon != value) { _customIcon = value; Invalidate(); } }
        }

        public string ToolTipText
        {
            get { return _toolTipText; }
            set
            {
                _toolTipText = value;
                AccessibleName = value;
                if (_toolTip != null)
                {
                    _toolTip.SetToolTip(this, _toolTipText);
                }
            }
        }

        public DialogResult DialogResult
        {
            get { return _dialogResult; }
            set { _dialogResult = value; }
        }

        public void NotifyDefault(bool value) { }

        public void PerformClick()
        {
            if (CanSelect)
            {
                OnClick(EventArgs.Empty);
            }
        }

        private void UpdateDimensions()
        {
            int basePx = (_sizeVariant == IconButtonSize.Small) ? 26 : 32;
            int dim = DpiManager.ScaleInt(basePx);
            Size = new Size(dim, dim);
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
                if (_toolTip != null)
                {
                    _toolTip.Dispose();
                    _toolTip = null;
                }
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

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            IThemePalette pal = ThemeManager.ActivePalette;

            // Clean background fill matching effective parent surface (eliminates corner artifacts)
            Color surfaceColor = UiDrawingUtils.GetEffectiveParentSurface(this);
            using (SolidBrush surfaceBrush = new SolidBrush(surfaceColor))
            {
                g.FillRectangle(surfaceBrush, ClientRectangle);
            }

            Rectangle rect = new Rectangle(1, 1, Width - 2, Height - 2);
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusSmall);

            // Draw Background
            if (Enabled)
            {
                if (_isPressed)
                {
                    using (SolidBrush b = new SolidBrush(pal.SurfaceSelected))
                    {
                        UiDrawingUtils.FillRoundedRectangle(g, b, rect, rad);
                    }
                }
                else if (_isHovered)
                {
                    using (SolidBrush b = new SolidBrush(pal.SurfaceHover))
                    {
                        UiDrawingUtils.FillRoundedRectangle(g, b, rect, rad);
                    }
                }
            }

            // Draw Glyph or Custom Icon
            Color glyphColor = (!Enabled) ? pal.TextDisabled : (_isHovered ? pal.TextPrimary : pal.TextSecondary);
            Rectangle contentBounds = new Rectangle(0, 0, Width, Height);

            if (_buttonType == IconButtonType.Custom && _customIcon != null)
            {
                int iconDim = DpiManager.ScaleInt((_sizeVariant == IconButtonSize.Small) ? 14 : 16);
                int ix = (Width - iconDim) / 2;
                int iy = (Height - iconDim) / 2;
                if (Enabled)
                {
                    g.DrawImage(_customIcon, ix, iy, iconDim, iconDim);
                }
                else
                {
                    ControlPaint.DrawImageDisabled(g, _customIcon, ix, iy, Color.Transparent);
                }
            }
            else
            {
                RenderBuiltInGlyph(g, glyphColor, contentBounds);
            }

            // Draw Focus Ring
            if (Focused && ShowFocusCues)
            {
                UiDrawingUtils.DrawFocusRing(g, rect, rad, pal.FocusRing);
            }
        }

        private void RenderBuiltInGlyph(Graphics g, Color color, Rectangle bounds)
        {
            int glyphSize = DpiManager.ScaleInt((_sizeVariant == IconButtonSize.Small) ? 12 : 16);
            Rectangle centerBox = new Rectangle((Width - glyphSize) / 2, (Height - glyphSize) / 2, glyphSize, glyphSize);

            switch (_buttonType)
            {
                case IconButtonType.Close:
                    UiDrawingUtils.DrawCloseGlyph(g, color, centerBox, DpiManager.ScaleInt(8));
                    break;

                case IconButtonType.Minimize:
                    int mcx = centerBox.X + centerBox.Width / 2;
                    int mcy = centerBox.Y + centerBox.Height / 2 + DpiManager.ScaleInt(2);
                    int mHalf = DpiManager.ScaleInt(5);
                    using (Pen pen = new Pen(color, 1.8f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, mcx - mHalf, mcy, mcx + mHalf, mcy);
                    }
                    break;

                case IconButtonType.Maximize:
                    int maxBoxSize = DpiManager.ScaleInt(9);
                    int maxBoxX = (Width - maxBoxSize) / 2;
                    int maxBoxY = (Height - maxBoxSize) / 2;
                    using (Pen pen = new Pen(color, 1.4f))
                    {
                        g.DrawRectangle(pen, maxBoxX, maxBoxY, maxBoxSize, maxBoxSize);
                    }
                    break;

                case IconButtonType.Restore:
                    int resBoxSize = DpiManager.ScaleInt(8);
                    int resOffset = DpiManager.ScaleInt(2);
                    int resBoxX = (Width - resBoxSize) / 2 - resOffset / 2;
                    int resBoxY = (Height - resBoxSize) / 2 + resOffset / 2;
                    using (Pen pen = new Pen(color, 1.3f))
                    {
                        // Back square (upper right)
                        g.DrawRectangle(pen, resBoxX + resOffset, resBoxY - resOffset, resBoxSize, resBoxSize);
                        // Clear front area so lines don't intersect
                        Color resSurf = UiDrawingUtils.GetEffectiveParentSurface(this);
                        using (SolidBrush sb = new SolidBrush(resSurf))
                        {
                            g.FillRectangle(sb, resBoxX, resBoxY, resBoxSize, resBoxSize);
                        }
                        // Front square (lower left)
                        g.DrawRectangle(pen, resBoxX, resBoxY, resBoxSize, resBoxSize);
                    }
                    break;

                case IconButtonType.Overflow:
                    UiDrawingUtils.DrawOverflowGlyph(g, color, centerBox, DpiManager.ScaleInt(3));
                    break;

                case IconButtonType.Refresh:
                    UiDrawingUtils.DrawRefreshGlyph(g, color, centerBox, DpiManager.ScaleInt(5));
                    break;

                case IconButtonType.ChevronDown:
                    UiDrawingUtils.DrawChevronDown(g, color, centerBox, DpiManager.ScaleInt(6));
                    break;

                case IconButtonType.ChevronUp:
                    int cx = centerBox.X + centerBox.Width / 2;
                    int cy = centerBox.Y + centerBox.Height / 2;
                    int half = DpiManager.ScaleInt(3);
                    using (Pen pen = new Pen(color, 1.8f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, cx - half, cy + 2, cx, cy - 2);
                        g.DrawLine(pen, cx, cy - 2, cx + half, cy + 2);
                    }
                    break;

                case IconButtonType.ChevronLeft:
                case IconButtonType.ChevronRight:
                    bool isRtl = (RightToLeft == RightToLeft.Yes);
                    bool pointsRight = (_buttonType == IconButtonType.ChevronRight);
                    if (isRtl) pointsRight = !pointsRight; // Mirror for RTL

                    int chx = centerBox.X + centerBox.Width / 2;
                    int chy = centerBox.Y + centerBox.Height / 2;
                    int chHalf = DpiManager.ScaleInt(3);
                    using (Pen pen = new Pen(color, 1.8f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        if (pointsRight)
                        {
                            g.DrawLine(pen, chx - 2, chy - chHalf, chx + 2, chy);
                            g.DrawLine(pen, chx + 2, chy, chx - 2, chy + chHalf);
                        }
                        else
                        {
                            g.DrawLine(pen, chx + 2, chy - chHalf, chx - 2, chy);
                            g.DrawLine(pen, chx - 2, chy, chx + 2, chy + chHalf);
                        }
                    }
                    break;

                case IconButtonType.Copy:
                    Color surf = UiDrawingUtils.GetEffectiveParentSurface(this);
                    UiDrawingUtils.DrawCopyIcon(g, color, centerBox, surf);
                    break;
            }
        }
    }
}
