// ═══════════════════════════════════════════════════════════════════════════
// TrustedDeviceRow — Compact Architectural List Row for Trusted Devices
// USB Shield Pro v2.1 (Phase 4.4 Redesign)
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using UsbShieldPro.Core;
using UsbShieldPro.Devices;
using UsbShieldPro.Models;

namespace UsbShieldPro.UI
{
    public class TrustedDeviceRow : Control
    {
        private readonly DeviceRule _rule;
        private readonly DeviceIdentity _correlatedDevice;
        private bool _isSelected;
        private bool _isHovered;

        private readonly StatusBadge _trustBadge;
        private readonly StatusBadge _connectionBadge;
        private readonly AppButton _detailsBtn;
        private readonly IconButton _overflowBtn;
        private ContextMenuStrip _overflowMenu;

        public event EventHandler RowSelected;
        public event EventHandler DetailsRequested;
        public event EventHandler RevokeRequested;
        public event EventHandler ViewInConnectedRequested;

        public DeviceRule Rule { get { return _rule; } }
        public DeviceIdentity CorrelatedDevice { get { return _correlatedDevice; } }
        public bool IsConnected { get { return _correlatedDevice != null; } }

        public Color CurrentSurfaceColor
        {
            get
            {
                IThemePalette pal = ThemeManager.ActivePalette;
                return _isSelected ? pal.SurfaceSelected : (_isHovered ? pal.SurfaceHover : pal.Surface);
            }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    BackColor = CurrentSurfaceColor;
                    Invalidate();
                }
            }
        }

        public StatusBadge TrustBadge { get { return _trustBadge; } }
        public StatusBadge ConnectionBadge { get { return _connectionBadge; } }
        public AppButton DetailsButton { get { return _detailsBtn; } }
        public IconButton OverflowButton { get { return _overflowBtn; } }

        public TrustedDeviceRow(DeviceRule rule, DeviceIdentity correlatedDevice = null)
        {
            _rule = rule ?? new DeviceRule();
            _correlatedDevice = correlatedDevice;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = true;
            Cursor = Cursors.Hand;
            Height = DpiManager.ScaleInt(64);

            // Sub-control: Trust Badge (Restrained, semantic success)
            _trustBadge = new StatusBadge();
            _trustBadge.TabStop = false;
            _trustBadge.Variant = StatusBadgeVariant.Success;
            _trustBadge.Text = Loc.Get("BadgeTrusted");
            _trustBadge.Click += OnSubControlClick;

            // Sub-control: Connection Badge (Connected vs Offline)
            _connectionBadge = new StatusBadge();
            _connectionBadge.TabStop = false;
            _connectionBadge.Variant = IsConnected ? StatusBadgeVariant.Success : StatusBadgeVariant.Neutral;
            _connectionBadge.Text = IsConnected ? Loc.Get("BadgeConnected") : Loc.Get("BadgeOffline");
            _connectionBadge.Click += OnSubControlClick;

            // Sub-control: View Details Button
            _detailsBtn = new AppButton();
            _detailsBtn.SizeVariant = ButtonSize.Small;
            _detailsBtn.Variant = ButtonVariant.Secondary;
            _detailsBtn.Text = Loc.Get("ViewDetails");
            _detailsBtn.Click += delegate
            {
                TriggerRowSelect();
                if (DetailsRequested != null) DetailsRequested(this, EventArgs.Empty);
            };

            // Sub-control: Overflow Menu Button
            _overflowBtn = new IconButton();
            _overflowBtn.ButtonType = IconButtonType.Overflow;
            _overflowBtn.SizeVariant = IconButtonSize.Small;
            _overflowBtn.BackColor = Color.Transparent;
            _overflowBtn.ToolTipText = Loc.Get("MoreActions");
            _overflowBtn.Click += delegate { ShowOverflowMenu(); };

            Controls.Add(_trustBadge);
            Controls.Add(_connectionBadge);
            Controls.Add(_detailsBtn);
            Controls.Add(_overflowBtn);

            UpdateBadgesAndText();
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void OnSubControlClick(object sender, EventArgs e)
        {
            TriggerRowSelect();
        }

        private void TriggerRowSelect()
        {
            if (!_isSelected)
            {
                _isSelected = true;
                Invalidate();
            }
            if (RowSelected != null)
            {
                RowSelected(this, EventArgs.Empty);
            }
        }

        public void UpdateBadgesAndText()
        {
            bool isRtl = Loc.IsRtl;

            _trustBadge.RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;
            _connectionBadge.RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;

            _trustBadge.Variant = StatusBadgeVariant.Success;
            _trustBadge.Text = Loc.Get("BadgeTrusted");

            _connectionBadge.Variant = IsConnected ? StatusBadgeVariant.Success : StatusBadgeVariant.Neutral;
            _connectionBadge.Text = IsConnected ? Loc.Get("BadgeConnected") : Loc.Get("BadgeOffline");

            _detailsBtn.Text = Loc.Get("ViewDetails");
            _detailsBtn.Width = AppButton.CalculateWidth(_detailsBtn.Text, _detailsBtn.SizeVariant, isRtl);

            PerformChildLayout();
            Invalidate();
        }

        private void PerformChildLayout()
        {
            if (_overflowBtn == null || _detailsBtn == null || _connectionBadge == null || _trustBadge == null)
                return;

            bool isRtl = Loc.IsRtl;
            int pad = DpiManager.ScaleInt(16);
            int gap = DpiManager.ScaleInt(6);
            int btnH = DpiManager.ScaleInt(26);
            int btnY = (Height - btnH) / 2;

            _detailsBtn.Height = btnH;
            _overflowBtn.Size = new Size(btnH, btnH);

            if (isRtl)
            {
                // In RTL: trailing controls start on LEFT edge and stack towards center
                int curX = pad;

                _overflowBtn.Location = new Point(curX, btnY);
                curX += _overflowBtn.Width + gap;

                _detailsBtn.Location = new Point(curX, btnY);
                curX += _detailsBtn.Width + gap;

                int bYConn = (Height - _connectionBadge.Height) / 2;
                _connectionBadge.Location = new Point(curX, bYConn);
                curX += _connectionBadge.Width + gap;

                int bYTrust = (Height - _trustBadge.Height) / 2;
                _trustBadge.Location = new Point(curX, bYTrust);
                curX += _trustBadge.Width + gap;
            }
            else
            {
                // In LTR: trailing controls start on RIGHT edge and stack towards center
                int curX = Width - pad;

                curX -= _overflowBtn.Width;
                _overflowBtn.Location = new Point(curX, btnY);
                curX -= gap;

                curX -= _detailsBtn.Width;
                _detailsBtn.Location = new Point(curX, btnY);
                curX -= gap;

                curX -= _connectionBadge.Width;
                int bYConn = (Height - _connectionBadge.Height) / 2;
                _connectionBadge.Location = new Point(curX, bYConn);
                curX -= gap;

                curX -= _trustBadge.Width;
                int bYTrust = (Height - _trustBadge.Height) / 2;
                _trustBadge.Location = new Point(curX, bYTrust);
                curX -= gap;
            }
        }

        private void ShowOverflowMenu()
        {
            if (_overflowMenu != null)
            {
                _overflowMenu.Dispose();
                _overflowMenu = null;
            }

            IThemePalette pal = ThemeManager.ActivePalette;
            _overflowMenu = new ContextMenuStrip();
            _overflowMenu.RightToLeft = Loc.IsRtl ? RightToLeft.Yes : RightToLeft.No;
            _overflowMenu.ShowImageMargin = false;
            _overflowMenu.Renderer = new AppMenuRenderer();

            // 1. View Details
            var itemDetails = new ToolStripMenuItem(Loc.Get("ViewDetails"));
            itemDetails.Click += delegate
            {
                TriggerRowSelect();
                if (DetailsRequested != null) DetailsRequested(this, EventArgs.Empty);
            };
            _overflowMenu.Items.Add(itemDetails);

            // 2. Revoke Trust
            var itemRevoke = new ToolStripMenuItem(Loc.Get("RevokeTrust"));
            itemRevoke.Tag = "LockAction";
            itemRevoke.Click += delegate
            {
                if (RevokeRequested != null) RevokeRequested(this, EventArgs.Empty);
            };
            _overflowMenu.Items.Add(itemRevoke);

            _overflowMenu.Items.Add(new ToolStripSeparator());

            // 3. Copy Identifier (Serial if available, else Hardware ID, else Instance ID)
            string copyTarget = !string.IsNullOrEmpty(_rule.TargetSerial) ? _rule.TargetSerial :
                                (!string.IsNullOrEmpty(_rule.TargetHardwareId) ? _rule.TargetHardwareId : _rule.TargetInstanceId);
            var itemCopyId = new ToolStripMenuItem(Loc.Get("CopyHardwareId"));
            itemCopyId.Click += delegate
            {
                if (!string.IsNullOrEmpty(copyTarget))
                {
                    try { Clipboard.SetText(copyTarget); } catch { }
                }
            };
            _overflowMenu.Items.Add(itemCopyId);

            // 4. Copy Instance ID
            if (!string.IsNullOrEmpty(_rule.TargetInstanceId))
            {
                var itemCopyInst = new ToolStripMenuItem(Loc.Get("CopyInstanceId"));
                itemCopyInst.Click += delegate
                {
                    try { Clipboard.SetText(_rule.TargetInstanceId); } catch { }
                };
                _overflowMenu.Items.Add(itemCopyInst);
            }

            // 5. If connected: View in Connected Devices
            if (IsConnected)
            {
                _overflowMenu.Items.Add(new ToolStripSeparator());
                var itemViewInConn = new ToolStripMenuItem(Loc.Get("ViewInConnectedDevices"));
                itemViewInConn.Click += delegate
                {
                    if (ViewInConnectedRequested != null) ViewInConnectedRequested(this, EventArgs.Empty);
                };
                _overflowMenu.Items.Add(itemViewInConn);
            }

            Point pt = new Point(0, _overflowBtn.Height);
            _overflowMenu.Show(_overflowBtn, pt);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PerformChildLayout();
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Focus();
                TriggerRowSelect();
            }
            base.OnMouseDown(e);
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            TriggerRowSelect();
            if (DetailsRequested != null) DetailsRequested(this, EventArgs.Empty);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                TriggerRowSelect();
                if (DetailsRequested != null) DetailsRequested(this, EventArgs.Empty);
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

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                BackColor = CurrentSurfaceColor;
                UpdateBadgesAndText();
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

            // 1. Row Surface Background
            Color bgColor = _isSelected ? pal.SurfaceSelected : (_isHovered ? pal.SurfaceHover : pal.Surface);
            using (SolidBrush bgBrush = new SolidBrush(bgColor))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            // 2. Selection Indicator Bar (3px on leading edge: left in LTR, right in RTL)
            if (_isSelected)
            {
                int barW = DpiManager.ScaleInt(3);
                int barX = isRtl ? (Width - barW) : 0;
                using (SolidBrush barBrush = new SolidBrush(pal.Accent))
                {
                    g.FillRectangle(barBrush, barX, 0, barW, Height);
                }
            }

            // 3. Subtle 1px Bottom Separator
            using (Pen sepPen = new Pen(pal.BorderSubtle, 1f))
            {
                g.DrawLine(sepPen, 0, Height - 1, Width, Height - 1);
            }

            // 4. Focus Ring
            if (Focused)
            {
                UiDrawingUtils.DrawFocusRing(g, ClientRectangle, 0, pal.FocusRing);
            }

            // 5. Vector Leading Icon Box
            int pad = DpiManager.ScaleInt(16);
            int iconBoxSize = DpiManager.ScaleInt(34);
            int iconBoxY = (Height - iconBoxSize) / 2;
            int iconBoxX = isRtl ? (Width - pad - iconBoxSize) : pad;
            Rectangle iconBoxRect = new Rectangle(iconBoxX, iconBoxY, iconBoxSize, iconBoxSize);

            Color iconColor = _isSelected ? pal.Accent : pal.TextSecondary;
            DrawRowIcon(g, iconColor, iconBoxRect);

            // 6. Text Labels Region (Primary Title + Secondary Subtitle)
            int textGap = DpiManager.ScaleInt(14);
            Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            Font subFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            int titleH = DpiManager.ScaleInt(isRtl ? 22 : 20);
            int subH = DpiManager.ScaleInt(isRtl ? 22 : 18);
            int textBlockH = titleH + subH + DpiManager.ScaleInt(2);
            int textTop = (Height - textBlockH) / 2;

            int textLeft, textRight;
            int gVal = DpiManager.ScaleInt(6);

            if (isRtl)
            {
                textRight = iconBoxX - textGap;
                int trailingLeftLimit = pad;
                if (_overflowBtn != null) trailingLeftLimit = Math.Max(trailingLeftLimit, _overflowBtn.Right + gVal);
                if (_detailsBtn != null) trailingLeftLimit = Math.Max(trailingLeftLimit, _detailsBtn.Right + gVal);
                if (_connectionBadge != null) trailingLeftLimit = Math.Max(trailingLeftLimit, _connectionBadge.Right + gVal);
                if (_trustBadge != null) trailingLeftLimit = Math.Max(trailingLeftLimit, _trustBadge.Right + gVal);
                textLeft = trailingLeftLimit;
            }
            else
            {
                textLeft = iconBoxRect.Right + textGap;
                int trailingRightLimit = Width - pad;
                if (_overflowBtn != null) trailingRightLimit = Math.Min(trailingRightLimit, _overflowBtn.Left - gVal);
                if (_detailsBtn != null) trailingRightLimit = Math.Min(trailingRightLimit, _detailsBtn.Left - gVal);
                if (_connectionBadge != null) trailingRightLimit = Math.Min(trailingRightLimit, _connectionBadge.Left - gVal);
                if (_trustBadge != null) trailingRightLimit = Math.Min(trailingRightLimit, _trustBadge.Left - gVal);
                textRight = trailingRightLimit;
            }

            int textW = Math.Max(50, textRight - textLeft);

            Rectangle titleRect = new Rectangle(textLeft, textTop, textW, titleH);
            Rectangle subRect = new Rectangle(textLeft, textTop + titleH + DpiManager.ScaleInt(2), textW, subH);

            TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping;
            if (isRtl)
            {
                flags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
            }
            else
            {
                flags |= TextFormatFlags.VerticalCenter | TextFormatFlags.Left;
            }

            string primaryText = !string.IsNullOrEmpty(_rule.FriendlyName) ? _rule.FriendlyName : "USB Device";
            TextRenderer.DrawText(g, primaryText, titleFont, titleRect, pal.TextPrimary, flags);

            string secondaryText = BuildSecondaryMetadataString();
            TextRenderer.DrawText(g, secondaryText, subFont, subRect, pal.TextSecondary, flags);
        }

        private void DrawRowIcon(Graphics g, Color color, Rectangle bounds)
        {
            if (_correlatedDevice != null)
            {
                switch (_correlatedDevice.Category)
                {
                    case DeviceCategory.UsbFlashDrive:
                        UiDrawingUtils.DrawDeviceFlashDrive(g, color, bounds);
                        break;
                    case DeviceCategory.ExternalUsbHdd:
                    case DeviceCategory.ExternalUsbSsd:
                        UiDrawingUtils.DrawDeviceExternalDisk(g, color, bounds);
                        break;
                    case DeviceCategory.MobilePhoneOrMediaDevice:
                        UiDrawingUtils.DrawDevicePhoneOrMedia(g, color, bounds);
                        break;
                    default:
                        UiDrawingUtils.DrawNavTrusted(g, color, bounds);
                        break;
                }
            }
            else
            {
                UiDrawingUtils.DrawNavTrusted(g, color, bounds);
            }
        }

        private string BuildSecondaryMetadataString()
        {
            string idPart;
            if (!string.IsNullOrEmpty(_rule.TargetSerial))
            {
                idPart = Loc.IsRtl ? ("الرقم التسلسلي: " + _rule.TargetSerial) : ("Serial: " + _rule.TargetSerial);
            }
            else if (!string.IsNullOrEmpty(_rule.TargetHardwareId))
            {
                idPart = Loc.IsRtl ? ("معرّف العتاد: " + _rule.TargetHardwareId) : ("ID: " + _rule.TargetHardwareId);
            }
            else if (!string.IsNullOrEmpty(_rule.TargetInstanceId))
            {
                idPart = Loc.IsRtl ? ("المثيل: " + _rule.TargetInstanceId) : ("Instance: " + _rule.TargetInstanceId);
            }
            else
            {
                idPart = Loc.IsRtl ? "جهاز مصرح به" : "Enrolled Device";
            }

            string datePart = _rule.CreatedUtc != DateTime.MinValue
                ? _rule.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd")
                : "";

            if (!string.IsNullOrEmpty(datePart))
            {
                return idPart + " • " + (Loc.IsRtl ? ("تاريخ الإضافة: " + datePart) : ("Added: " + datePart));
            }
            return idPart;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                if (_overflowMenu != null)
                {
                    _overflowMenu.Dispose();
                    _overflowMenu = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
