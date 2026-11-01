// ═══════════════════════════════════════════════════════════════════════════
// ProtectionSettingRow — Production Policy Row Primitive for v2.1
// USB Shield Pro v2.1 (Phase 4.3 Protection Page Redesign)
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace UsbShieldPro.UI
{
    public enum ProtectionSettingIcon
    {
        StorageDisk,
        ReadOnly,
        FlashSentinel,
        AutoPlay,
        PortableDevice,
        Whitelist,
        StrictPnp,
        HardBlock
    }

    public class ProtectionSettingRow : Control
    {
        private ProtectionSettingIcon _iconType = ProtectionSettingIcon.StorageDisk;
        private string _title = "";
        private string _subtitle = "";
        private string _helperText = "";
        private string _tooltipText = "";
        private bool _isDependentDisabled = false;
        private bool _showBottomSeparator = true;
        private bool _isHovered = false;

        private readonly AppToggle _toggle;
        private readonly StatusBadge _badge;
        private Control _actionControl;
        private readonly ToolTip _toolTip;

        public event EventHandler CheckedChanged;

        public ProtectionSettingIcon IconType
        {
            get { return _iconType; }
            set { _iconType = value; Invalidate(); }
        }

        public string Title
        {
            get { return _title; }
            set { _title = value ?? ""; UpdateRowHeight(); Invalidate(); }
        }

        public string Subtitle
        {
            get { return _subtitle; }
            set { _subtitle = value ?? ""; UpdateRowHeight(); Invalidate(); }
        }

        public string HelperText
        {
            get { return _helperText; }
            set { _helperText = value ?? ""; UpdateRowHeight(); Invalidate(); }
        }

        public string ToolTipText
        {
            get { return _tooltipText; }
            set
            {
                _tooltipText = value ?? "";
                if (_toolTip != null)
                {
                    _toolTip.SetToolTip(this, _tooltipText);
                    if (_toggle != null) _toolTip.SetToolTip(_toggle, _tooltipText);
                    if (_badge != null) _toolTip.SetToolTip(_badge, _tooltipText);
                }
            }
        }

        public bool Checked
        {
            get { return _toggle != null && _toggle.Checked; }
            set
            {
                if (_toggle != null && _toggle.Checked != value)
                {
                    _toggle.Checked = value;
                    Invalidate();
                }
            }
        }

        public bool IsDependentDisabled
        {
            get { return _isDependentDisabled; }
            set
            {
                if (_isDependentDisabled != value)
                {
                    _isDependentDisabled = value;
                    if (_toggle != null)
                    {
                        _toggle.Enabled = !_isDependentDisabled;
                    }
                    Invalidate();
                }
            }
        }

        public Color CurrentSurfaceColor
        {
            get
            {
                IThemePalette pal = ThemeManager.ActivePalette;
                if (_isHovered && Enabled && !_isDependentDisabled)
                {
                    return pal.SurfaceHover;
                }
                return pal.Surface;
            }
        }

        public bool ShowBottomSeparator
        {
            get { return _showBottomSeparator; }
            set { _showBottomSeparator = value; Invalidate(); }
        }

        public string BadgeText
        {
            get { return _badge != null ? _badge.Text : ""; }
            set
            {
                if (_badge != null)
                {
                    _badge.Text = value ?? "";
                    _badge.Visible = !string.IsNullOrEmpty(value);
                    PerformChildLayout();
                    Invalidate();
                }
            }
        }

        public StatusBadgeVariant BadgeVariant
        {
            get { return _badge != null ? _badge.Variant : StatusBadgeVariant.Neutral; }
            set
            {
                if (_badge != null)
                {
                    _badge.Variant = value;
                    Invalidate();
                }
            }
        }

        public bool ShowBadge
        {
            get { return _badge != null && _badge.Visible; }
            set
            {
                if (_badge != null)
                {
                    _badge.Visible = value;
                    PerformChildLayout();
                    Invalidate();
                }
            }
        }

        public AppToggle Toggle
        {
            get { return _toggle; }
        }

        public StatusBadge Badge
        {
            get { return _badge; }
        }

        public Control ActionControl
        {
            get { return _actionControl; }
            set
            {
                if (_actionControl != value)
                {
                    if (_actionControl != null && Controls.Contains(_actionControl))
                    {
                        Controls.Remove(_actionControl);
                    }
                    _actionControl = value;
                    if (_actionControl != null)
                    {
                        Controls.Add(_actionControl);
                    }
                    PerformChildLayout();
                    Invalidate();
                }
            }
        }

        public ProtectionSettingRow()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = false;
            UpdateRowHeight();

            _toggle = new AppToggle();
            _toggle.TabStop = true;
            _toggle.UseSemanticSuccessTrack = true;
            _toggle.CheckedChanged += delegate
            {
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            };

            _badge = new StatusBadge();
            _badge.TabStop = false;
            _badge.Visible = false;

            Controls.Add(_badge);
            Controls.Add(_toggle);

            _toolTip = new ToolTip();
            _toolTip.InitialDelay = 400;
            _toolTip.ReshowDelay = 200;
            _toolTip.AutoPopDelay = 8000;

            ThemeManager.ThemeChanged += OnThemeChanged;
            PerformLayout();
        }

        public void UpdateRowHeight()
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int padX = DpiManager.ScaleInt(SpacingTokens.Space16);
            int iconSize = DpiManager.ScaleInt(22);
            int textGap = DpiManager.ScaleInt(SpacingTokens.Space12);

            int toggleW = _toggle != null ? _toggle.Width : DpiManager.ScaleInt(44);
            int badgeW = (_badge != null && !string.IsNullOrEmpty(_badge.Text)) ? (_badge.Width + DpiManager.ScaleInt(SpacingTokens.Space8)) : 0;
            int actW = (_actionControl != null && _actionControl.Visible) ? (_actionControl.Width + DpiManager.ScaleInt(SpacingTokens.Space8)) : 0;
            int trailingSpace = toggleW + badgeW + actW + DpiManager.ScaleInt(SpacingTokens.Space16);

            int effectiveW = Width > 0 ? Width : DpiManager.ScaleInt(600);
            int textW = effectiveW - (padX * 2) - iconSize - textGap - trailingSpace;
            if (textW < DpiManager.ScaleInt(160)) textW = DpiManager.ScaleInt(160);

            Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            Font subFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            Size titleSize = TextRenderer.MeasureText(_title ?? "", titleFont);
            int titleH = Math.Max(titleSize.Height, DpiManager.ScaleInt(isRtl ? 22 : 20));

            int subH = 0;
            if (!string.IsNullOrEmpty(_subtitle))
            {
                TextFormatFlags mFlags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix;
                Size subSize = TextRenderer.MeasureText(_subtitle, subFont, new Size(textW, 4000), mFlags);
                subH = Math.Max(subSize.Height, DpiManager.ScaleInt(isRtl ? 20 : 18));
            }

            bool hasHelper = !string.IsNullOrEmpty(_helperText);
            int helperH = 0;
            if (hasHelper)
            {
                TextFormatFlags mFlags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix;
                Size hSize = TextRenderer.MeasureText(_helperText, subFont, new Size(textW, 4000), mFlags);
                helperH = Math.Max(hSize.Height, DpiManager.ScaleInt(isRtl ? 20 : 18));
            }

            int gapY = DpiManager.ScaleInt(isRtl ? 4 : 3);
            int topPad = DpiManager.ScaleInt(10);
            int bottomPad = DpiManager.ScaleInt(10);
            int totalTextContent = titleH + (subH > 0 ? (gapY + subH) : 0) + (hasHelper ? (gapY + helperH) : 0);

            int toggleH = _toggle != null ? _toggle.Height : DpiManager.ScaleInt(24);
            int minH = Math.Max(DpiManager.ScaleInt(isRtl ? 62 : 56), toggleH + DpiManager.ScaleInt(24));
            int finalH = Math.Max(minH, topPad + totalTextContent + bottomPad);

            Height = finalH;
            BackColor = CurrentSurfaceColor;
            PerformChildLayout();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            BackColor = CurrentSurfaceColor;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                if (_toolTip != null) _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHovered = true;
            BackColor = CurrentSurfaceColor;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHovered = false;
            BackColor = CurrentSurfaceColor;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRowHeight();
            PerformChildLayout();
        }

        protected override void OnRightToLeftChanged(EventArgs e)
        {
            base.OnRightToLeftChanged(e);
            UpdateRowHeight();
            PerformChildLayout();
            Invalidate();
        }

        public void PerformChildLayout()
        {
            if (_toggle == null || _badge == null) return;

            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int padX = DpiManager.ScaleInt(SpacingTokens.Space16);
            int gap = DpiManager.ScaleInt(SpacingTokens.Space8);

            // Toggle geometry
            int toggleW = _toggle.Width;
            int toggleH = _toggle.Height;
            int toggleY = (Height - toggleH) / 2;

            int toggleX;
            if (isRtl)
            {
                toggleX = padX;
            }
            else
            {
                toggleX = Width - padX - toggleW;
            }
            _toggle.Location = new Point(toggleX, toggleY);

            // Status Badge geometry (adjacent to toggle)
            int badgeW = _badge.Width;
            int badgeH = _badge.Height;
            int badgeY = (Height - badgeH) / 2;

            int badgeX;
            if (isRtl)
            {
                badgeX = toggleX + toggleW + gap;
            }
            else
            {
                badgeX = toggleX - gap - badgeW;
            }
            _badge.Location = new Point(badgeX, badgeY);

            // Action control geometry (e.g. Manage Trusted Devices link)
            if (_actionControl != null)
            {
                if (_actionControl is AppButton)
                {
                    Font btnFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
                    int tw = TextRenderer.MeasureText(_actionControl.Text ?? "", btnFont).Width;
                    _actionControl.Width = tw + DpiManager.ScaleInt(20);
                    _actionControl.Height = DpiManager.ScaleInt(26);
                }
                int actW = _actionControl.Width;
                int actH = _actionControl.Height;
                int actY = (Height - actH) / 2;

                int actX;
                if (isRtl)
                {
                    int startRef = (_badge != null && !string.IsNullOrEmpty(_badge.Text)) ? (_badge.Right + gap) : (toggleX + toggleW + gap);
                    actX = startRef;
                }
                else
                {
                    int startRef = (_badge != null && !string.IsNullOrEmpty(_badge.Text)) ? (_badge.Left - gap) : (toggleX - gap);
                    actX = startRef - actW;
                }
                _actionControl.Location = new Point(actX, actY);
                _actionControl.BringToFront();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;

            // Background
            using (SolidBrush bgBrush = new SolidBrush(CurrentSurfaceColor))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            int padX = DpiManager.ScaleInt(SpacingTokens.Space16);
            int iconSize = DpiManager.ScaleInt(22);
            int iconY = (Height - iconSize) / 2;

            // Icon Position
            int iconX = isRtl ? (Width - padX - iconSize) : padX;
            Rectangle iconBounds = new Rectangle(iconX, iconY, iconSize, iconSize);

            // Determine Icon Color
            Color iconColor;
            if (!Enabled || _isDependentDisabled)
            {
                iconColor = pal.TextDisabled;
            }
            else if (Checked)
            {
                iconColor = pal.Success;
            }
            else
            {
                iconColor = pal.TextSecondary;
            }

            DrawIcon(g, iconBounds, iconColor);

            // Text Positioning
            int textGap = DpiManager.ScaleInt(SpacingTokens.Space12);
            int textStartX;
            int textAvailableWidth;

            // Calculate trailing bounds to avoid text overlap
            int trailingBoundary;
            if (isRtl)
            {
                int rightEdge = _toggle.Right;
                if (_badge != null && !string.IsNullOrEmpty(_badge.Text) && _badge.Right > rightEdge) rightEdge = _badge.Right;
                if (_actionControl != null && _actionControl.Right > rightEdge) rightEdge = _actionControl.Right;
                trailingBoundary = rightEdge + DpiManager.ScaleInt(SpacingTokens.Space12);

                textStartX = trailingBoundary;
                textAvailableWidth = (iconX - textGap) - trailingBoundary;
            }
            else
            {
                int leftEdge = _toggle.Left;
                if (_badge != null && !string.IsNullOrEmpty(_badge.Text) && _badge.Left < leftEdge) leftEdge = _badge.Left;
                if (_actionControl != null && _actionControl.Left < leftEdge) leftEdge = _actionControl.Left;
                trailingBoundary = leftEdge - DpiManager.ScaleInt(SpacingTokens.Space12);

                textStartX = iconX + iconSize + textGap;
                textAvailableWidth = trailingBoundary - textStartX;
            }

            if (textAvailableWidth > 20)
            {
                Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
                Font subFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

                Color titleColor = (!Enabled || _isDependentDisabled) ? pal.TextDisabled : pal.TextPrimary;
                Color subColor = (!Enabled || _isDependentDisabled) ? pal.TextDisabled : pal.TextSecondary;

                TextFormatFlags titleFlags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping;
                TextFormatFlags wrapFlags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix;
                if (isRtl)
                {
                    titleFlags |= TextFormatFlags.RightToLeft | TextFormatFlags.Right;
                    wrapFlags |= TextFormatFlags.RightToLeft | TextFormatFlags.Right;
                }
                else
                {
                    titleFlags |= TextFormatFlags.Left;
                    wrapFlags |= TextFormatFlags.Left;
                }

                bool hasHelper = !string.IsNullOrEmpty(_helperText);
                Size titleSize = TextRenderer.MeasureText(_title ?? "", titleFont);
                int titleH = Math.Max(titleSize.Height, DpiManager.ScaleInt(isRtl ? 22 : 20));

                int subH = 0;
                if (!string.IsNullOrEmpty(_subtitle))
                {
                    Size subMeasured = TextRenderer.MeasureText(_subtitle, subFont, new Size(textAvailableWidth, 4000), wrapFlags);
                    subH = Math.Max(subMeasured.Height, DpiManager.ScaleInt(isRtl ? 20 : 18));
                }

                int helperH = 0;
                if (hasHelper)
                {
                    Size hMeasured = TextRenderer.MeasureText(_helperText, subFont, new Size(textAvailableWidth, 4000), wrapFlags);
                    helperH = Math.Max(hMeasured.Height, DpiManager.ScaleInt(isRtl ? 20 : 18));
                }

                int gapY = DpiManager.ScaleInt(isRtl ? 4 : 3);
                int totalTextH = titleH + (subH > 0 ? (gapY + subH) : 0) + (hasHelper ? (gapY + helperH) : 0);
                int titleY = Math.Max(DpiManager.ScaleInt(10), (Height - totalTextH) / 2);

                Rectangle titleRect = new Rectangle(textStartX, titleY, textAvailableWidth, titleH);
                TextRenderer.DrawText(g, _title, titleFont, titleRect, titleColor, titleFlags);

                int subY = titleY + titleH + gapY;
                if (subH > 0)
                {
                    Rectangle subRect = new Rectangle(textStartX, subY, textAvailableWidth, subH);
                    TextRenderer.DrawText(g, _subtitle, subFont, subRect, subColor, wrapFlags);
                }

                if (hasHelper && helperH > 0)
                {
                    int helperY = subY + subH + gapY;
                    Rectangle helperRect = new Rectangle(textStartX, helperY, textAvailableWidth, helperH);
                    Color helperColor = _isDependentDisabled ? pal.Warning : pal.TextMuted;
                    TextRenderer.DrawText(g, _helperText, subFont, helperRect, helperColor, wrapFlags);
                }
            }

            // Bottom Separator line
            if (_showBottomSeparator)
            {
                using (Pen sepPen = new Pen(pal.BorderSubtle, 1f))
                {
                    int sepY = Height - 1;
                    int sepLeft = padX;
                    int sepRight = Width - padX;
                    g.DrawLine(sepPen, sepLeft, sepY, sepRight, sepY);
                }
            }
        }

        private void DrawIcon(Graphics g, Rectangle bounds, Color color)
        {
            switch (_iconType)
            {
                case ProtectionSettingIcon.StorageDisk:
                    UiDrawingUtils.DrawDeviceExternalDisk(g, color, bounds);
                    break;
                case ProtectionSettingIcon.ReadOnly:
                    DrawReadOnlyIcon(g, color, bounds);
                    break;
                case ProtectionSettingIcon.FlashSentinel:
                    UiDrawingUtils.DrawDeviceFlashDrive(g, color, bounds);
                    break;
                case ProtectionSettingIcon.AutoPlay:
                    DrawAutoPlayIcon(g, color, bounds);
                    break;
                case ProtectionSettingIcon.PortableDevice:
                    UiDrawingUtils.DrawDevicePhoneOrMedia(g, color, bounds);
                    break;
                case ProtectionSettingIcon.Whitelist:
                    UiDrawingUtils.DrawNavTrusted(g, color, bounds);
                    break;
                case ProtectionSettingIcon.StrictPnp:
                    DrawStrictPnpIcon(g, color, bounds);
                    break;
                case ProtectionSettingIcon.HardBlock:
                    DrawHardBlockIcon(g, color, bounds);
                    break;
                default:
                    UiDrawingUtils.DrawNavShield(g, color, bounds);
                    break;
            }
        }

        private void DrawReadOnlyIcon(Graphics g, Color color, Rectangle bounds)
        {
            // Padlock / document lock icon
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;

            int bodyW = Math.Max(10, (int)(bounds.Width * 0.60f));
            int bodyH = Math.Max(8, (int)(bounds.Height * 0.44f));
            int shackleW = Math.Max(6, (int)(bodyW * 0.58f));
            int shackleH = Math.Max(5, (int)(bounds.Height * 0.30f));

            int totalH = bodyH + shackleH;
            int topY = cy - totalH / 2;

            Rectangle shackleRect = new Rectangle(cx - shackleW / 2, topY, shackleW, shackleH * 2);
            Rectangle bodyRect = new Rectangle(cx - bodyW / 2, topY + shackleH, bodyW, bodyH);

            using (Pen pen = new Pen(color, 1.4f))
            {
                pen.LineJoin = LineJoin.Round;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                // Shackle arc
                g.DrawArc(pen, shackleRect, 180, 180);

                // Body
                UiDrawingUtils.DrawRoundedRectangle(g, pen, bodyRect, 2);

                // Keyhole
                int khY = bodyRect.Y + bodyRect.Height / 3;
                using (SolidBrush b = new SolidBrush(color))
                {
                    g.FillEllipse(b, cx - 1, khY, 3, 3);
                    g.FillRectangle(b, cx, khY + 2, 1, 3);
                }
            }
        }

        private void DrawAutoPlayIcon(Graphics g, Color color, Rectangle bounds)
        {
            // Play triangle inside circle
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            int r = Math.Max(7, (int)(bounds.Width * 0.42f));

            using (Pen pen = new Pen(color, 1.4f))
            {
                g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);

                // Play triangle
                int triH = Math.Max(5, (int)(r * 0.9f));
                int triW = Math.Max(4, (int)(r * 0.8f));
                Point[] pts = new Point[]
                {
                    new Point(cx - triW / 3, cy - triH / 2),
                    new Point(cx + triW * 2 / 3, cy),
                    new Point(cx - triW / 3, cy + triH / 2)
                };
                using (SolidBrush b = new SolidBrush(color))
                {
                    g.FillPolygon(b, pts);
                }
            }
        }

        private void DrawStrictPnpIcon(Graphics g, Color color, Rectangle bounds)
        {
            // Hardware chip / shield hybrid
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            int size = Math.Max(12, (int)(bounds.Width * 0.65f));
            Rectangle chipRect = new Rectangle(cx - size / 2, cy - size / 2, size, size);

            using (Pen pen = new Pen(color, 1.4f))
            {
                pen.LineJoin = LineJoin.Round;
                UiDrawingUtils.DrawRoundedRectangle(g, pen, chipRect, 2);

                // Pins
                int pinLen = Math.Max(2, (int)(bounds.Width * 0.12f));
                // Top & Bottom pins
                g.DrawLine(pen, cx - 2, chipRect.Top - pinLen, cx - 2, chipRect.Top);
                g.DrawLine(pen, cx + 2, chipRect.Top - pinLen, cx + 2, chipRect.Top);
                g.DrawLine(pen, cx - 2, chipRect.Bottom, cx - 2, chipRect.Bottom + pinLen);
                g.DrawLine(pen, cx + 2, chipRect.Bottom, cx + 2, chipRect.Bottom + pinLen);

                // Left & Right pins
                g.DrawLine(pen, chipRect.Left - pinLen, cy - 2, chipRect.Left, cy - 2);
                g.DrawLine(pen, chipRect.Left - pinLen, cy + 2, chipRect.Left, cy + 2);
                g.DrawLine(pen, chipRect.Right, cy - 2, chipRect.Right + pinLen, cy - 2);
                g.DrawLine(pen, chipRect.Right, cy + 2, chipRect.Right + pinLen, cy + 2);

                // Inner lock dot
                using (SolidBrush b = new SolidBrush(color))
                {
                    g.FillEllipse(b, cx - 2, cy - 2, 4, 4);
                }
            }
        }

        private void DrawHardBlockIcon(Graphics g, Color color, Rectangle bounds)
        {
            // Shield with diagonal block slash
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            int r = Math.Max(7, (int)(bounds.Width * 0.42f));

            using (Pen pen = new Pen(color, 1.5f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);

                int slashOffset = (int)(r * 0.707f);
                g.DrawLine(pen, cx - slashOffset, cy - slashOffset, cx + slashOffset, cy + slashOffset);
            }
        }
    }
}
