// ═══════════════════════════════════════════════════════════════════════════
// DeviceRow — Compact Architectural List Row for Connected Devices
// USB Shield Pro v2.1 (Phase 4.2)
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using UsbShieldPro.Core;
using UsbShieldPro.Devices;
using UsbShieldPro.Models;
using UsbShieldPro.Security;

namespace UsbShieldPro.UI
{
    public class DeviceRow : Control
    {
        private readonly DeviceIdentity _device;
        private bool _isTrusted;
        private bool _isSelected;
        private bool _isHovered;
        private bool _isReadOnlyMode;
        private bool _isActuallyBlocked;
        private bool _showTrustBadge = true;
        private bool _showAccessBadge = true;
        private bool _showPrimaryAction = true;

        private readonly StatusBadge _trustBadge;
        private readonly StatusBadge _accessBadge;
        private readonly AppButton _primaryActionBtn;
        private readonly IconButton _overflowBtn;
        private ContextMenuStrip _overflowMenu;

        public event EventHandler RowSelected;
        public event EventHandler EjectRequested;
        public event EventHandler TrustRequested;
        public event EventHandler DetailsRequested;

        public DeviceIdentity Device { get { return _device; } }
        public bool IsSystemDevice
        {
            get
            {
                if (_device == null) return false;
                return _device.IsSystemDisk ||
                       _device.SafetyResult == SafetyValidationResult.RejectedSystemOrBootDisk ||
                       _device.Category == DeviceCategory.InternalFixedDisk ||
                       _device.Category == DeviceCategory.CriticalSystemHardware;
            }
        }

        public bool IsTrusted
        {
            get { return _isTrusted; }
            set
            {
                if (_isTrusted != value)
                {
                    _isTrusted = value;
                    UpdateStatusAndActions();
                    Invalidate();
                }
            }
        }

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

        public bool IsReadOnlyMode
        {
            get { return _isReadOnlyMode; }
            set
            {
                if (_isReadOnlyMode != value)
                {
                    _isReadOnlyMode = value;
                    UpdateStatusAndActions();
                    Invalidate();
                }
            }
        }

        public bool IsActuallyBlocked
        {
            get { return _isActuallyBlocked; }
            set
            {
                if (_isActuallyBlocked != value)
                {
                    _isActuallyBlocked = value;
                    UpdateStatusAndActions();
                    Invalidate();
                }
            }
        }

        public StatusBadge TrustBadge { get { return _trustBadge; } }
        public StatusBadge AccessBadge { get { return _accessBadge; } }
        public AppButton PrimaryActionButton { get { return _primaryActionBtn; } }
        public IconButton OverflowButton { get { return _overflowBtn; } }

        public DeviceRow(DeviceIdentity device, bool isTrusted, bool isReadOnlyMode = false)
        {
            _device = device ?? new DeviceIdentity();
            _isTrusted = isTrusted;
            _isReadOnlyMode = isReadOnlyMode;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = true;
            Cursor = Cursors.Hand;
            Height = DpiManager.ScaleInt(68);

            // Sub-control: Trust Badge
            _trustBadge = new StatusBadge();
            _trustBadge.TabStop = false;
            _trustBadge.Click += OnSubControlClick;

            // Sub-control: Access / Policy Badge
            _accessBadge = new StatusBadge();
            _accessBadge.TabStop = false;
            _accessBadge.Click += OnSubControlClick;

            // Sub-control: Primary Action CTA
            _primaryActionBtn = new AppButton();
            _primaryActionBtn.SizeVariant = ButtonSize.Small;
            _primaryActionBtn.Variant = ButtonVariant.Secondary;
            _primaryActionBtn.Click += OnPrimaryActionClick;

            // Sub-control: Overflow Menu Button
            _overflowBtn = new IconButton();
            _overflowBtn.ButtonType = IconButtonType.Overflow;
            _overflowBtn.SizeVariant = IconButtonSize.Small;
            _overflowBtn.BackColor = Color.Transparent;
            _overflowBtn.ToolTipText = Loc.Get("MoreActions");
            _overflowBtn.Click += OnOverflowBtnClick;

            Controls.Add(_trustBadge);
            Controls.Add(_accessBadge);
            Controls.Add(_primaryActionBtn);
            Controls.Add(_overflowBtn);

            UpdateStatusAndActions();
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

        private void UpdateStatusAndActions()
        {
            bool isRtl = Loc.IsRtl;
            bool isSys = IsSystemDevice;

            _trustBadge.RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;
            _accessBadge.RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;

            if (isSys)
            {
                _trustBadge.Variant = StatusBadgeVariant.Neutral;
                _trustBadge.Text = Loc.Get("BadgeSystemExcluded");
                _showTrustBadge = true;
                _showAccessBadge = false;
                _showPrimaryAction = false;
            }
            else
            {
                // Trust Badge
                _trustBadge.Variant = _isTrusted ? StatusBadgeVariant.Success : StatusBadgeVariant.Neutral;
                _trustBadge.Text = _isTrusted ? Loc.Get("BadgeTrusted") : Loc.Get("BadgeUntrusted");
                _showTrustBadge = true;
                _showAccessBadge = true;
                _showPrimaryAction = true;

                // Lifecycle / Access State
                string physKey = DeviceEnforcementLifecycleManager.BuildPhysicalKey(_device);
                DeviceLifecycleRecord rec = DeviceEnforcementLifecycleManager.GetLifecycleSnapshot(physKey);

                if (rec != null && rec.State == EnforcementState.EjectedUntilRemoval)
                {
                    _accessBadge.Variant = StatusBadgeVariant.Neutral;
                    _accessBadge.Text = Loc.Get("BadgeEjectedStatus");

                    _primaryActionBtn.Text = Loc.Get("SafelyEject");
                    _primaryActionBtn.Enabled = false;
                }
                else if (rec != null && rec.State == EnforcementState.RetryWaiting)
                {
                    _accessBadge.Variant = StatusBadgeVariant.Warning;
                    _accessBadge.Text = Loc.Get("SafeEjectPreparing");

                    _primaryActionBtn.Text = Loc.Get("SafelyEject");
                    _primaryActionBtn.Enabled = false;
                }
                else if (rec != null && rec.State == EnforcementState.FailedTerminal)
                {
                    _accessBadge.Variant = StatusBadgeVariant.Danger;
                    _accessBadge.Text = Loc.Get("BadgeBlocked");

                    _primaryActionBtn.Text = Loc.Get("SafelyEject");
                    _primaryActionBtn.Enabled = true;
                }
                else
                {
                    if (_isReadOnlyMode)
                    {
                        _accessBadge.Variant = StatusBadgeVariant.Warning;
                        _accessBadge.Text = Loc.Get("BadgeReadOnlyStatus");
                    }
                    else if (_isActuallyBlocked || ((DeviceEnforcementLifecycleManager.IsTestMode || MainForm.IsCaptureMode) && !_isTrusted))
                    {
                        _accessBadge.Variant = StatusBadgeVariant.Danger;
                        _accessBadge.Text = Loc.Get("BadgeBlocked");
                    }
                    else
                    {
                        _accessBadge.Variant = StatusBadgeVariant.Success;
                        _accessBadge.Text = _isTrusted ? Loc.Get("BadgeAllowed") : Loc.Get("BadgeConnectedAllowed");
                    }

                    // Context-sensitive primary CTA
                    if (_device.IsRemovableMedia || _device.Category == DeviceCategory.UsbFlashDrive || _device.Category == DeviceCategory.ExternalUsbHdd || _device.Category == DeviceCategory.ExternalUsbSsd)
                    {
                        _primaryActionBtn.Text = Loc.Get("SafelyEject");
                        _primaryActionBtn.Variant = ButtonVariant.Secondary;
                        _primaryActionBtn.Enabled = true;
                    }
                    else
                    {
                        // MTP / Smartphone / Non-removable
                        _primaryActionBtn.Text = _isTrusted ? Loc.Get("RevokeTrust") : Loc.Get("TrustDevice");
                        _primaryActionBtn.Variant = _isTrusted ? ButtonVariant.Destructive : ButtonVariant.Secondary;
                        _primaryActionBtn.Enabled = true;
                    }
                }
            }

            _trustBadge.Visible = _showTrustBadge;
            _accessBadge.Visible = _showAccessBadge;
            _primaryActionBtn.Visible = _showPrimaryAction;

            PerformChildLayout();
        }

        private void OnPrimaryActionClick(object sender, EventArgs e)
        {
            if (IsSystemDevice) return;

            if (_device.IsRemovableMedia || _device.Category == DeviceCategory.UsbFlashDrive || _device.Category == DeviceCategory.ExternalUsbHdd || _device.Category == DeviceCategory.ExternalUsbSsd)
            {
                if (EjectRequested != null)
                {
                    EjectRequested(this, EventArgs.Empty);
                }
            }
            else
            {
                if (TrustRequested != null)
                {
                    TrustRequested(this, EventArgs.Empty);
                }
            }
        }

        private void OnOverflowBtnClick(object sender, EventArgs e)
        {
            ShowOverflowMenu();
        }

        private void ShowOverflowMenu()
        {
            if (_overflowMenu != null)
            {
                _overflowMenu.Dispose();
                _overflowMenu = null;
            }

            _overflowMenu = new ContextMenuStrip();
            _overflowMenu.RightToLeft = Loc.RtlValue;
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

            if (!IsSystemDevice)
            {
                // 2. Trust / Revoke Trust
                string trustText = _isTrusted ? Loc.Get("RevokeTrust") : Loc.Get("TrustDevice");
                var itemTrust = new ToolStripMenuItem(trustText);
                itemTrust.Click += delegate
                {
                    if (TrustRequested != null) TrustRequested(this, EventArgs.Empty);
                };
                _overflowMenu.Items.Add(itemTrust);

                // 3. Safely Eject (if removable or storage)
                if (_device.IsRemovableMedia || _device.Category == DeviceCategory.UsbFlashDrive || _device.Category == DeviceCategory.ExternalUsbHdd || _device.Category == DeviceCategory.ExternalUsbSsd)
                {
                    var itemEject = new ToolStripMenuItem(Loc.Get("SafelyEject"));
                    itemEject.Click += delegate
                    {
                        if (EjectRequested != null) EjectRequested(this, EventArgs.Empty);
                    };
                    _overflowMenu.Items.Add(itemEject);
                }
            }

            _overflowMenu.Items.Add(new ToolStripSeparator());

            // 4. Copy Hardware ID
            var itemCopyHw = new ToolStripMenuItem(Loc.Get("CopyHardwareId"));
            itemCopyHw.Click += delegate
            {
                string hw = (_device.HardwareIds != null && _device.HardwareIds.Count > 0) ? _device.HardwareIds[0] : _device.DeviceInstanceId;
                if (!string.IsNullOrEmpty(hw))
                {
                    try { Clipboard.SetText(hw); } catch { }
                }
            };
            _overflowMenu.Items.Add(itemCopyHw);

            // 5. Copy Instance ID
            var itemCopyInst = new ToolStripMenuItem(Loc.Get("CopyInstanceId"));
            itemCopyInst.Click += delegate
            {
                if (!string.IsNullOrEmpty(_device.DeviceInstanceId))
                {
                    try { Clipboard.SetText(_device.DeviceInstanceId); } catch { }
                }
            };
            _overflowMenu.Items.Add(itemCopyInst);

            Point pt = new Point(0, _overflowBtn.Height);
            _overflowMenu.Show(_overflowBtn, pt);
        }

        private void PerformChildLayout()
        {
            if (_overflowBtn == null || _primaryActionBtn == null || _accessBadge == null || _trustBadge == null)
                return;

            bool isRtl = Loc.IsRtl;
            int pad = DpiManager.ScaleInt(16);
            int gap = DpiManager.ScaleInt(6);
            int btnH = DpiManager.ScaleInt(26);
            int btnY = (Height - btnH) / 2;

            if (_showPrimaryAction)
            {
                int btnW = AppButton.CalculateWidth(_primaryActionBtn.Text, ButtonSize.Small, isRtl);
                _primaryActionBtn.Size = new Size(btnW, btnH);
            }

            _overflowBtn.Size = new Size(btnH, btnH);

            if (isRtl)
            {
                // In RTL: trailing controls start on LEFT edge and stack towards center
                int curX = pad;

                // Overflow button
                _overflowBtn.Location = new Point(curX, btnY);
                curX += _overflowBtn.Width + gap;

                // Primary Action Button
                if (_showPrimaryAction)
                {
                    _primaryActionBtn.Location = new Point(curX, btnY);
                    curX += _primaryActionBtn.Width + gap;
                }

                // Access Badge
                if (_showAccessBadge)
                {
                    int bY = (Height - _accessBadge.Height) / 2;
                    _accessBadge.Location = new Point(curX, bY);
                    curX += _accessBadge.Width + gap;
                }

                // Trust Badge
                if (_showTrustBadge)
                {
                    int bY = (Height - _trustBadge.Height) / 2;
                    _trustBadge.Location = new Point(curX, bY);
                    curX += _trustBadge.Width + gap;
                }
            }
            else
            {
                // In LTR: trailing controls start on RIGHT edge and stack towards center
                int curX = Width - pad;

                // Overflow button
                curX -= _overflowBtn.Width;
                _overflowBtn.Location = new Point(curX, btnY);
                curX -= gap;

                // Primary Action Button
                if (_showPrimaryAction)
                {
                    curX -= _primaryActionBtn.Width;
                    _primaryActionBtn.Location = new Point(curX, btnY);
                    curX -= gap;
                }

                // Access Badge
                if (_showAccessBadge)
                {
                    curX -= _accessBadge.Width;
                    int bY = (Height - _accessBadge.Height) / 2;
                    _accessBadge.Location = new Point(curX, bY);
                    curX -= gap;
                }

                // Trust Badge
                if (_showTrustBadge)
                {
                    curX -= _trustBadge.Width;
                    int bY = (Height - _trustBadge.Height) / 2;
                    _trustBadge.Location = new Point(curX, bY);
                    curX -= gap;
                }
            }
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

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                TriggerRowSelect();
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
                UpdateStatusAndActions();
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

            // 5. Vector Device Icon
            int pad = DpiManager.ScaleInt(16);
            int iconBoxSize = DpiManager.ScaleInt(34);
            int iconBoxY = (Height - iconBoxSize) / 2;
            int iconBoxX = isRtl ? (Width - pad - iconBoxSize) : pad;
            Rectangle iconBoxRect = new Rectangle(iconBoxX, iconBoxY, iconBoxSize, iconBoxSize);

            Color iconColor = _isSelected ? pal.Accent : (IsSystemDevice ? pal.TextMuted : pal.TextSecondary);
            DrawCategoryIcon(g, _device.Category, iconColor, iconBoxRect);

            // 6. Text Labels Region (Primary Title + Secondary Subtitle)
            int textGap = DpiManager.ScaleInt(14);
            Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            Font subFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            int titleH = DpiManager.ScaleInt(isRtl ? 22 : 20);
            int subH = DpiManager.ScaleInt(isRtl ? 22 : 18);
            int textBlockH = titleH + subH + DpiManager.ScaleInt(2);
            int textTop = (Height - textBlockH) / 2;

            int textLeft, textRight;
            if (isRtl)
            {
                textRight = iconBoxX - textGap;
                // Left boundary is the rightmost badge or button
                int trailingLeftLimit = pad;
                if (_showTrustBadge) trailingLeftLimit = Math.Max(trailingLeftLimit, _trustBadge.Right + gap());
                if (_showAccessBadge) trailingLeftLimit = Math.Max(trailingLeftLimit, _accessBadge.Right + gap());
                if (_showPrimaryAction) trailingLeftLimit = Math.Max(trailingLeftLimit, _primaryActionBtn.Right + gap());
                trailingLeftLimit = Math.Max(trailingLeftLimit, _overflowBtn.Right + gap());

                textLeft = trailingLeftLimit;
            }
            else
            {
                textLeft = iconBoxRect.Right + textGap;
                // Right boundary is the leftmost badge or button
                int trailingRightLimit = Width - pad;
                if (_showTrustBadge) trailingRightLimit = Math.Min(trailingRightLimit, _trustBadge.Left - gap());
                if (_showAccessBadge) trailingRightLimit = Math.Min(trailingRightLimit, _accessBadge.Left - gap());
                if (_showPrimaryAction) trailingRightLimit = Math.Min(trailingRightLimit, _primaryActionBtn.Left - gap());
                trailingRightLimit = Math.Min(trailingRightLimit, _overflowBtn.Left - gap());

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

            string primaryText = _device.GetCleanDisplayName();
            TextRenderer.DrawText(g, primaryText, titleFont, titleRect, pal.TextPrimary, flags);

            string secondaryText = BuildSecondaryMetadataString();
            Color subColor = IsSystemDevice ? pal.TextMuted : pal.TextSecondary;
            TextRenderer.DrawText(g, secondaryText, subFont, subRect, subColor, flags);
        }

        private int gap() { return DpiManager.ScaleInt(6); }

        private void DrawCategoryIcon(Graphics g, DeviceCategory cat, Color color, Rectangle bounds)
        {
            switch (cat)
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
                case DeviceCategory.InternalFixedDisk:
                case DeviceCategory.CriticalSystemHardware:
                    UiDrawingUtils.DrawDeviceInternal(g, color, bounds);
                    break;
                default:
                    UiDrawingUtils.DrawDeviceGeneric(g, color, bounds);
                    break;
            }
        }

        private string BuildSecondaryMetadataString()
        {
            List<string> parts = new List<string>();

            // 1. Category and Transport Bus: e.g. "فلاش ميموري (USB)" or "قرص نظام داخلي (NVMe)"
            string catText = GetCategoryText(_device.Category);
            string busStr = (_device.PhysicalTransport != PhysicalBusType.Unknown) ? _device.PhysicalTransport.ToString() : null;
            if (!string.IsNullOrEmpty(catText))
            {
                if (!string.IsNullOrEmpty(busStr) && !catText.EndsWith("(" + busStr + ")", StringComparison.OrdinalIgnoreCase))
                {
                    parts.Add(string.Format("{0} ({1})", catText, busStr));
                }
                else
                {
                    parts.Add(catText);
                }
            }
            else if (!string.IsNullOrEmpty(busStr))
            {
                parts.Add(busStr);
            }

            // 2. Capacity: e.g. "14.6 GB"
            if (_device.TotalSizeBytes > 0)
            {
                double gb = _device.TotalSizeBytes / (1024.0 * 1024.0 * 1024.0);
                string capStr = string.Format("{0:F1} GB", gb);
                parts.Add(Loc.IsRtl ? ("\u200E" + capStr + "\u200E") : capStr);
            }

            // 3. Partitions / Volumes: e.g. "الأقسام: D: (Ventoy), F: (VTOYEFI)" or "القسم: C:"
            if (_device.VolumeMountings != null && _device.VolumeMountings.Count > 0)
            {
                List<string> volItems = new List<string>();
                foreach (var v in _device.VolumeMountings)
                {
                    if (string.IsNullOrEmpty(v)) continue;
                    string cleaned = v.Trim().TrimEnd('\\');
                    string label = null;
                    if (_device.VolumeLabels != null)
                    {
                        if (!_device.VolumeLabels.TryGetValue(cleaned, out label))
                        {
                            _device.VolumeLabels.TryGetValue(v, out label);
                        }
                    }

                    if (!string.IsNullOrEmpty(label))
                    {
                        volItems.Add(cleaned + " (" + label + ")");
                    }
                    else
                    {
                        volItems.Add(cleaned);
                    }
                }

                if (volItems.Count > 0)
                {
                    string joinedVols = string.Join(", ", volItems.ToArray());
                    string key = (volItems.Count > 1) ? "VolumePlural" : "VolumeSingle";
                    string formattedVols = string.Format(Loc.Get(key), joinedVols);
                    parts.Add(formattedVols);
                }
            }

            return string.Join("  •  ", parts.ToArray());
        }

        private string GetCategoryText(DeviceCategory cat)
        {
            switch (cat)
            {
                case DeviceCategory.UsbFlashDrive: return Loc.Get("DeviceTypeFlash");
                case DeviceCategory.MobilePhoneOrMediaDevice: return Loc.Get("DeviceTypePhone");
                case DeviceCategory.ExternalUsbSsd:
                case DeviceCategory.ExternalUsbHdd: return Loc.Get("DeviceTypeDisk");
                case DeviceCategory.InternalFixedDisk:
                case DeviceCategory.CriticalSystemHardware: return Loc.Get("DeviceTypeSystem");
                default: return Loc.Get("DeviceTypePeripheral");
            }
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

    public class OverflowMenuColorTable : ProfessionalColorTable
    {
        private readonly IThemePalette _pal;
        public OverflowMenuColorTable(IThemePalette pal) { _pal = pal; }
        public override Color ToolStripDropDownBackground { get { return _pal.Surface; } }
        public override Color ImageMarginGradientBegin { get { return _pal.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return _pal.Surface; } }
        public override Color ImageMarginGradientEnd { get { return _pal.Surface; } }
        public override Color MenuBorder { get { return _pal.Border; } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color MenuItemSelected { get { return _pal.SurfaceHover; } }
        public override Color SeparatorDark { get { return _pal.BorderSubtle; } }
        public override Color SeparatorLight { get { return Color.Transparent; } }
    }
}
