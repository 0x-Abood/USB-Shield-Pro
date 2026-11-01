// ═══════════════════════════════════════════════════════════════════════════
// DeviceDetailsDrawer — Non-Modal Slide-In Inspector for Connected Devices
// USB Shield Pro v2.1 (Phase 4.2.1 Visual Polish)
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
using UsbShieldPro.Persistence;
using UsbShieldPro.Security;

namespace UsbShieldPro.UI
{
    public class DeviceDetailsDrawer : Panel
    {
        private DeviceIdentity _currentDevice;
        private bool _isTrusted;
        private bool _isReadOnly;
        private bool _isOpen;

        private Panel _headerPanel;
        private Label _titleLabel;
        private Label _deviceNameLabel;
        private IconButton _closeBtn;

        private DarkScrollPanel _scrollContent;
        private Panel _footerPanel;
        private AppButton _footerCloseBtn;
        private AppButton _footerPrimaryBtn;
        private MethodInvoker _layoutFooter;

        // Section Badges
        private StatusBadge _drawerTrustBadge;
        private StatusBadge _drawerAccessBadge;

        // Themed Monospace Identifier Fields
        private IdentifierField _fieldHardwareId;
        private IdentifierField _fieldInstanceId;
        private IdentifierField _fieldSerial;

        // Labels for dynamic values
        private Label _lblFriendlyNameVal;
        private Label _lblModelVal;
        private Label _lblManufacturerVal;
        private Label _lblCategoryVal;
        private Label _lblBusVal;
        private Label _lblVolumesVal;
        private Label _lblCapacityVal;
        private Label _lblDiskIndexVal;
        private Label _lblSafetyVal;

        public event EventHandler DrawerClosed;
        public event EventHandler EjectRequested;
        public event EventHandler TrustRequested;

        public DeviceIdentity CurrentDevice { get { return _currentDevice; } }
        public bool IsTrusted { get { return _isTrusted; } }
        public bool IsOpen { get { return _isOpen; } }
        public AppButton PrimaryActionButton { get { return _footerPrimaryBtn; } }
        public AppButton CloseButton { get { return _footerCloseBtn; } }

        public IdentifierField HardwareIdField { get { return _fieldHardwareId; } }
        public IdentifierField InstanceIdField { get { return _fieldInstanceId; } }
        public IdentifierField SerialField     { get { return _fieldSerial; } }

        public TextBox HardwareIdTextBox { get { return _fieldHardwareId != null ? _fieldHardwareId.InnerTextBox : null; } }
        public TextBox InstanceIdTextBox { get { return _fieldInstanceId != null ? _fieldInstanceId.InnerTextBox : null; } }
        public TextBox SerialTextBox     { get { return _fieldSerial != null ? _fieldSerial.InnerTextBox : null; } }

        private ToolTip _drawerToolTip;

        public DeviceDetailsDrawer()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            Visible = false;
            Width = DpiManager.ScaleInt(367);
            BackColor = ThemeManager.ActivePalette.Surface;

            _drawerToolTip = new ToolTip();
            _drawerToolTip.ShowAlways = true;
            _drawerToolTip.InitialDelay = 300;
            _drawerToolTip.AutoPopDelay = 10000;
            _drawerToolTip.ReshowDelay = 100;

            BuildDrawerUI();
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public void UpdateResponsiveWidth(int viewportWidth)
        {
            // Adaptive drawer width: ~30-33% of viewport, scaling up to 520px on 1080p/1440p
            int targetW = (int)Math.Round(viewportWidth * 0.32);
            int minW = DpiManager.ScaleInt(340);
            int maxW = DpiManager.ScaleInt(520);
            int clampedW = Math.Max(minW, Math.Min(maxW, targetW));
            if (Width != clampedW)
            {
                Width = clampedW;
            }
        }

        private void BuildDrawerUI()
        {
            Controls.Clear();
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = RightToLeft == RightToLeft.Yes;

            // ── TOP HEADER ──────────────────────────────────────────────────
            _headerPanel = new Panel();
            _headerPanel.Dock = DockStyle.Top;
            _headerPanel.Height = DpiManager.ScaleInt(62);
            _headerPanel.BackColor = pal.Surface;
            _headerPanel.Padding = new Padding(DpiManager.ScaleInt(16), DpiManager.ScaleInt(12), DpiManager.ScaleInt(16), DpiManager.ScaleInt(10));

            _closeBtn = new IconButton();
            _closeBtn.ButtonType = IconButtonType.Close;
            _closeBtn.SizeVariant = IconButtonSize.Standard;
            _closeBtn.ToolTipText = Loc.Get("DrawerCloseTooltip");
            _closeBtn.Click += delegate { CloseDrawer(); };

            _titleLabel = new Label();
            _titleLabel.Text = Loc.Get("DrawerTitle");
            _titleLabel.Font = DpiManager.GetFont(TypographyRole.Subtitle, isRtl);
            _titleLabel.ForeColor = pal.TextPrimary;
            _titleLabel.AutoSize = true;

            _deviceNameLabel = new Label();
            _deviceNameLabel.Text = "";
            _deviceNameLabel.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            _deviceNameLabel.ForeColor = pal.TextSecondary;
            _deviceNameLabel.AutoEllipsis = true;

            _deviceNameLabel.TextChanged += delegate
            {
                if (_drawerToolTip != null && _deviceNameLabel != null)
                {
                    string devName = (_deviceNameLabel.Text ?? "").Trim();
                    _drawerToolTip.SetToolTip(_deviceNameLabel, !string.IsNullOrEmpty(devName) ? devName : null);
                }
            };

            _headerPanel.Controls.Add(_closeBtn);
            _headerPanel.Controls.Add(_titleLabel);
            _headerPanel.Controls.Add(_deviceNameLabel);

            MethodInvoker layoutHeader = delegate
            {
                int p = DpiManager.ScaleInt(16);
                int btnSize = _closeBtn.Height;
                int hw = _headerPanel.ClientSize.Width;
                bool rtl = RightToLeft == RightToLeft.Yes;

                if (rtl)
                {
                    _closeBtn.Location = new Point(p, (_headerPanel.Height - btnSize) / 2);
                    _titleLabel.Location = new Point(hw - p - _titleLabel.Width, DpiManager.ScaleInt(10));
                    int maxNameW = Math.Max(50, _titleLabel.Right - (_closeBtn.Right + DpiManager.ScaleInt(10)));
                    _deviceNameLabel.Size = new Size(maxNameW, DpiManager.ScaleInt(18));
                    _deviceNameLabel.Location = new Point(hw - p - _deviceNameLabel.Width, DpiManager.ScaleInt(34));
                }
                else
                {
                    _closeBtn.Location = new Point(hw - p - btnSize, (_headerPanel.Height - btnSize) / 2);
                    _titleLabel.Location = new Point(p, DpiManager.ScaleInt(10));
                    int maxNameW = Math.Max(50, _closeBtn.Left - p - DpiManager.ScaleInt(10));
                    _deviceNameLabel.Size = new Size(maxNameW, DpiManager.ScaleInt(18));
                    _deviceNameLabel.Location = new Point(p, DpiManager.ScaleInt(34));
                }
            };
            _headerPanel.Resize += delegate { layoutHeader(); };

            // ── BOTTOM FOOTER BAR ───────────────────────────────────────────
            _footerPanel = new Panel();
            _footerPanel.Dock = DockStyle.Bottom;
            _footerPanel.Height = DpiManager.ScaleInt(58);
            _footerPanel.BackColor = pal.Surface;
            _footerPanel.Padding = new Padding(DpiManager.ScaleInt(16), DpiManager.ScaleInt(12), DpiManager.ScaleInt(16), DpiManager.ScaleInt(12));

            _footerCloseBtn = new AppButton();
            _footerCloseBtn.Text = Loc.Get("Close");
            _footerCloseBtn.Variant = ButtonVariant.Ghost;
            _footerCloseBtn.SizeVariant = ButtonSize.Small;
            _footerCloseBtn.Click += delegate { CloseDrawer(); };

            _footerPrimaryBtn = new AppButton();
            _footerPrimaryBtn.Text = Loc.Get("SafelyEject");
            _footerPrimaryBtn.Variant = ButtonVariant.Secondary;
            _footerPrimaryBtn.SizeVariant = ButtonSize.Small;
            _footerPrimaryBtn.Click += OnPrimaryActionClick;

            _footerPanel.Controls.Add(_footerCloseBtn);
            _footerPanel.Controls.Add(_footerPrimaryBtn);

            _layoutFooter = delegate
            {
                int p = DpiManager.ScaleInt(16);
                int gap = DpiManager.ScaleInt(8);
                int fw = _footerPanel.ClientSize.Width;
                bool rtl = RightToLeft == RightToLeft.Yes;

                _footerCloseBtn.Width = AppButton.CalculateWidth(_footerCloseBtn.Text, _footerCloseBtn.SizeVariant, rtl);
                if (_footerPrimaryBtn.Visible)
                {
                    _footerPrimaryBtn.Width = AppButton.CalculateWidth(_footerPrimaryBtn.Text, _footerPrimaryBtn.SizeVariant, rtl);
                }
                int btnY = (_footerPanel.Height - _footerCloseBtn.Height) / 2;

                if (rtl)
                {
                    _footerCloseBtn.Location = new Point(p, btnY);
                    if (_footerPrimaryBtn.Visible)
                    {
                        _footerPrimaryBtn.Location = new Point(p + _footerCloseBtn.Width + gap, btnY);
                    }
                }
                else
                {
                    if (_footerPrimaryBtn.Visible)
                    {
                        _footerPrimaryBtn.Location = new Point(fw - p - _footerPrimaryBtn.Width, btnY);
                        _footerCloseBtn.Location = new Point(_footerPrimaryBtn.Left - gap - _footerCloseBtn.Width, btnY);
                    }
                    else
                    {
                        _footerCloseBtn.Location = new Point(fw - p - _footerCloseBtn.Width, btnY);
                    }
                }
            };
            _footerPanel.Resize += delegate { _layoutFooter(); };
            _layoutFooter();

            // ── SCROLLABLE BODY (DarkScrollPanel) ───────────────────────────
            _scrollContent = new DarkScrollPanel();
            _scrollContent.Dock = DockStyle.Fill;
            _scrollContent.BackColor = pal.BackgroundSecondary;
            _scrollContent.ContentPanel.BackColor = pal.BackgroundSecondary;
            _scrollContent.RightToLeft = RightToLeft;
            _scrollContent.ContentPanel.RightToLeft = RightToLeft;

            PopulateSections();

            Controls.Add(_scrollContent);
            Controls.Add(_headerPanel);
            Controls.Add(_footerPanel);

            // Docking order: header and footer edge-dock first, scrollContent fills middle
            _headerPanel.SendToBack();
            _footerPanel.SendToBack();
            _scrollContent.BringToFront();

            layoutHeader();
            _layoutFooter();
        }

        private void PopulateSections()
        {
            UiDrawingUtils.ClearAndDisposeControls(_scrollContent.ContentPanel.Controls);
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int pad = DpiManager.ScaleInt(16);
            int curY = pad;

            // SECTION 1: IDENTITY & CLASSIFICATION
            AddSectionHeader(Loc.Get("SectionIdentity"), ref curY);
            _lblFriendlyNameVal = AddMetadataField(Loc.Get("PropFriendlyName"), "-", ref curY, isTechnical: true);
            _lblModelVal = AddMetadataField(Loc.Get("PropModel"), "-", ref curY, isTechnical: true);
            _lblManufacturerVal = AddMetadataField(Loc.Get("PropManufacturer"), "-", ref curY, isTechnical: true);
            _lblCategoryVal = AddMetadataField(Loc.Get("PropCategory"), "-", ref curY, isTechnical: true);
            _lblBusVal = AddMetadataField(Loc.Get("PropBusType"), "-", ref curY, isTechnical: true);

            curY += DpiManager.ScaleInt(10);

            // SECTION 2: HARDWARE IDENTIFIERS
            AddSectionHeader(Loc.Get("SectionHardwareIds"), ref curY);
            _fieldHardwareId = AddIdentifierField(Loc.Get("PropHardwareIds"), Loc.Get("CopyHardwareId"), ref curY);
            _fieldInstanceId = AddIdentifierField(Loc.Get("PropInstanceId"), Loc.Get("CopyInstanceId"), ref curY);
            _fieldSerial = AddIdentifierField(Loc.Get("PropSerial"), Loc.Get("CopySerial"), ref curY);

            curY += DpiManager.ScaleInt(10);

            // SECTION 3: STORAGE & MOUNTINGS
            AddSectionHeader(Loc.Get("SectionStorage"), ref curY);
            _lblVolumesVal = AddMetadataField(Loc.Get("PropVolumes"), "-", ref curY, isTechnical: true);
            _lblCapacityVal = AddMetadataField(Loc.Get("PropTotalSize"), "-", ref curY, isTechnical: true);
            _lblDiskIndexVal = AddMetadataField(Loc.Get("PropDiskIndex"), "-", ref curY, isTechnical: true);

            curY += DpiManager.ScaleInt(10);

            // SECTION 4: PROTECTION STATUS
            AddSectionHeader(Loc.Get("SectionProtection"), ref curY);

            Panel badgesRow = new Panel();
            badgesRow.Height = DpiManager.ScaleInt(28);
            badgesRow.Location = new Point(pad, curY);
            badgesRow.Width = Math.Max(100, _scrollContent.ContentPanel.ClientSize.Width - pad * 2);
            badgesRow.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            badgesRow.RightToLeft = RightToLeft.No;

            _drawerTrustBadge = new StatusBadge();
            _drawerAccessBadge = new StatusBadge();

            badgesRow.Controls.Add(_drawerTrustBadge);
            badgesRow.Controls.Add(_drawerAccessBadge);

            MethodInvoker layoutBadges = delegate
            {
                int bGap = DpiManager.ScaleInt(8);
                bool rtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
                if (rtl)
                {
                    int bx = badgesRow.Width - _drawerTrustBadge.Width;
                    _drawerTrustBadge.Location = new Point(bx, 0);
                    bx -= (_drawerAccessBadge.Width + bGap);
                    _drawerAccessBadge.Location = new Point(bx, 0);
                }
                else
                {
                    int bx = 0;
                    _drawerTrustBadge.Location = new Point(bx, 0);
                    bx += _drawerTrustBadge.Width + bGap;
                    _drawerAccessBadge.Location = new Point(bx, 0);
                }
            };
            badgesRow.Resize += delegate { layoutBadges(); };
            layoutBadges();

            _scrollContent.ContentPanel.Controls.Add(badgesRow);
            curY += badgesRow.Height + DpiManager.ScaleInt(8);

            _lblSafetyVal = AddMetadataField(Loc.Get("PropSecurityPolicy"), "-", ref curY, isTechnical: true);
            curY += pad;

            _scrollContent.UpdateScroll();
        }

        private void AddSectionHeader(string title, ref int curY)
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int pad = DpiManager.ScaleInt(16);

            Label header = new Label();
            header.Text = title;
            header.Font = new Font("Segoe UI", DpiManager.ScaleFloat(11f), FontStyle.Bold);
            header.ForeColor = pal.TextPrimary;
            header.Location = new Point(pad, curY);
            header.Size = new Size(Math.Max(100, _scrollContent.ContentPanel.ClientSize.Width - pad * 2), DpiManager.ScaleInt(isRtl ? 24 : 20));
            header.TextAlign = isRtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;
            header.RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;
            header.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            header.AutoSize = false;

            _scrollContent.ContentPanel.Controls.Add(header);
            curY += header.Height + DpiManager.ScaleInt(8);
        }

        private Label AddMetadataField(string label, string defaultVal, ref int curY, bool isTechnical = false)
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int pad = DpiManager.ScaleInt(16);

            Panel row = new Panel();
            row.Height = DpiManager.ScaleInt(isRtl ? 24 : 22);
            row.Location = new Point(pad, curY);
            row.Width = Math.Max(100, _scrollContent.ContentPanel.ClientSize.Width - pad * 2);
            row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.RightToLeft = RightToLeft.No; // Ensure absolute coordinates across RTL containers

            Label lblKey = new Label();
            lblKey.Text = (label ?? "").TrimEnd(':', ' ', '：');
            lblKey.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            lblKey.ForeColor = pal.TextSecondary;
            lblKey.AutoEllipsis = true;
            lblKey.RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;

            Label lblVal = new Label();
            lblVal.Text = defaultVal;
            lblVal.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            lblVal.ForeColor = pal.TextPrimary;
            lblVal.AutoEllipsis = true;
            lblVal.RightToLeft = RightToLeft.No; // Strict LTR

            Action updateTips = delegate
            {
                if (_drawerToolTip == null) return;
                string raw = (lblVal.Text ?? "").Trim('\u200E', '\u200F', ' ');
                if (!string.IsNullOrEmpty(raw) && raw != "-")
                {
                    _drawerToolTip.SetToolTip(lblVal, raw);
                    _drawerToolTip.SetToolTip(row, raw);
                }
                else
                {
                    _drawerToolTip.SetToolTip(lblVal, null);
                    _drawerToolTip.SetToolTip(row, null);
                }
            };

            lblVal.TextChanged += delegate { updateTips(); };
            updateTips();

            MethodInvoker layoutRow = delegate
            {
                int rw = row.Width;
                bool rtl = (this.RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
                Size keySz = TextRenderer.MeasureText(lblKey.Text, lblKey.Font);
                int keyW = Math.Max(DpiManager.ScaleInt(rtl ? 115 : 95), keySz.Width + DpiManager.ScaleInt(6));
                if (keyW > rw - DpiManager.ScaleInt(70)) keyW = Math.Max(DpiManager.ScaleInt(70), rw - DpiManager.ScaleInt(70));
                int valW = Math.Max(20, rw - keyW - DpiManager.ScaleInt(8));

                if (rtl)
                {
                    // RIGHT COLUMN: Arabic field label, RTL / right aligned
                    lblKey.Location = new Point(rw - keyW, 0);
                    lblKey.Size = new Size(keyW, row.Height);
                    lblKey.TextAlign = ContentAlignment.MiddleRight;
                    lblKey.RightToLeft = RightToLeft.Yes;

                    // LEFT COLUMN: Technical value, strict LTR / left aligned
                    lblVal.Location = new Point(0, 0);
                    lblVal.Size = new Size(valW, row.Height);
                    lblVal.TextAlign = ContentAlignment.MiddleLeft;
                    lblVal.RightToLeft = RightToLeft.No;
                }
                else
                {
                    // LEFT COLUMN: English field label, left aligned
                    lblKey.Location = new Point(0, 0);
                    lblKey.Size = new Size(keyW, row.Height);
                    lblKey.TextAlign = ContentAlignment.MiddleLeft;
                    lblKey.RightToLeft = RightToLeft.No;

                    // RIGHT COLUMN: Value, left aligned
                    lblVal.Location = new Point(keyW + DpiManager.ScaleInt(8), 0);
                    lblVal.Size = new Size(valW, row.Height);
                    lblVal.TextAlign = ContentAlignment.MiddleLeft;
                    lblVal.RightToLeft = RightToLeft.No;
                }
            };

            row.Resize += delegate { layoutRow(); };
            layoutRow();

            row.Controls.Add(lblKey);
            row.Controls.Add(lblVal);
            _scrollContent.ContentPanel.Controls.Add(row);

            curY += row.Height + DpiManager.ScaleInt(4);
            return lblVal;
        }

        private IdentifierField AddIdentifierField(string label, string copyTooltip, ref int curY)
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int pad = DpiManager.ScaleInt(16);

            Label lblKey = new Label();
            lblKey.Text = (label ?? "").TrimEnd(':', ' ', '：');
            lblKey.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            lblKey.ForeColor = pal.TextSecondary;
            lblKey.Location = new Point(pad, curY);
            lblKey.Size = new Size(Math.Max(100, _scrollContent.ContentPanel.ClientSize.Width - pad * 2), DpiManager.ScaleInt(isRtl ? 20 : 16));
            lblKey.TextAlign = isRtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;
            lblKey.RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;
            lblKey.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblKey.AutoSize = false;
            _scrollContent.ContentPanel.Controls.Add(lblKey);
            curY += lblKey.Height + DpiManager.ScaleInt(4);

            IdentifierField field = new IdentifierField(copyTooltip);
            field.Location = new Point(pad, curY);
            field.Width = Math.Max(100, _scrollContent.ContentPanel.ClientSize.Width - pad * 2);
            field.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            field.RightToLeft = RightToLeft.No; // Strict LTR

            _scrollContent.ContentPanel.Controls.Add(field);
            curY += field.Height + DpiManager.ScaleInt(8);
            return field;
        }

        public void ShowDevice(DeviceIdentity dev, bool isTrusted, bool isReadOnly = false)
        {
            if (dev == null) return;
            _currentDevice = dev;
            _isTrusted = isTrusted;
            _isReadOnly = isReadOnly;

            bool isSys = dev.IsSystemDisk ||
                         dev.SafetyResult == SafetyValidationResult.RejectedSystemOrBootDisk ||
                         dev.Category == DeviceCategory.InternalFixedDisk ||
                         dev.Category == DeviceCategory.CriticalSystemHardware;

            _deviceNameLabel.Text = dev.GetCleanDisplayName();

            // Populate Section 1
            if (_lblFriendlyNameVal != null) _lblFriendlyNameVal.Text = dev.FriendlyName != null ? ("\u200E" + dev.FriendlyName + "\u200E") : "-";
            if (_lblModelVal != null) _lblModelVal.Text = dev.Model != null ? ("\u200E" + dev.Model + "\u200E") : "-";
            if (_lblManufacturerVal != null) _lblManufacturerVal.Text = dev.Manufacturer != null ? ("\u200E" + dev.Manufacturer + "\u200E") : "-";
            if (_lblCategoryVal != null) _lblCategoryVal.Text = GetCategoryText(dev.Category);
            if (_lblBusVal != null) _lblBusVal.Text = "\u200E" + dev.PhysicalTransport.ToString() + "\u200E";

            // Populate Section 2
            if (_fieldHardwareId != null)
            {
                _fieldHardwareId.Value = (dev.HardwareIds != null && dev.HardwareIds.Count > 0) ? string.Join("; ", dev.HardwareIds.ToArray()) : "-";
            }
            if (_fieldInstanceId != null)
            {
                _fieldInstanceId.Value = dev.DeviceInstanceId ?? "-";
            }
            if (_fieldSerial != null)
            {
                _fieldSerial.Value = dev.SerialNumber ?? "-";
            }

            // Populate Section 3
            if (_lblVolumesVal != null)
            {
                string vols = dev.GetVolumesDisplayString();
                _lblVolumesVal.Text = !string.IsNullOrEmpty(vols) ? ("\u200E" + vols + "\u200E") : "-";
            }
            if (_lblCapacityVal != null)
            {
                if (dev.TotalSizeBytes > 0)
                {
                    double gb = dev.TotalSizeBytes / (1024.0 * 1024.0 * 1024.0);
                    _lblCapacityVal.Text = "\u200E" + string.Format("{0:F2} GB ({1:N0} bytes)", gb, dev.TotalSizeBytes) + "\u200E";
                }
                else
                {
                    _lblCapacityVal.Text = "-";
                }
            }
            if (_lblDiskIndexVal != null)
            {
                _lblDiskIndexVal.Text = dev.DiskIndex >= 0 ? ("\u200EPhysicalDrive" + dev.DiskIndex + "\u200E") : "-";
            }

            // Populate Section 4 Badges
            if (_drawerTrustBadge != null)
            {
                if (isSys)
                {
                    _drawerTrustBadge.Variant = StatusBadgeVariant.Neutral;
                    _drawerTrustBadge.Text = Loc.Get("BadgeSystemExcluded");
                }
                else
                {
                    _drawerTrustBadge.Variant = isTrusted ? StatusBadgeVariant.Success : StatusBadgeVariant.Neutral;
                    _drawerTrustBadge.Text = isTrusted ? Loc.Get("BadgeTrusted") : Loc.Get("BadgeUntrusted");
                }
            }

            if (_drawerAccessBadge != null)
            {
                if (isSys)
                {
                    _drawerAccessBadge.Visible = false;
                }
                else
                {
                    _drawerAccessBadge.Visible = true;
                    string physKey = DeviceEnforcementLifecycleManager.BuildPhysicalKey(dev);
                    DeviceLifecycleRecord rec = DeviceEnforcementLifecycleManager.GetLifecycleSnapshot(physKey);

                    if (rec != null && rec.State == EnforcementState.EjectedUntilRemoval)
                    {
                        _drawerAccessBadge.Variant = StatusBadgeVariant.Neutral;
                        _drawerAccessBadge.Text = Loc.Get("BadgeEjectedStatus");
                    }
                    AppSettingsData curSet = SettingsRepository.Load();
                    ProtectionPolicy curPol = (curSet != null && curSet.ActivePolicy != null) ? curSet.ActivePolicy : new ProtectionPolicy();
                    bool isTemp = TemporaryAccessManager.PeekRemainingSeconds() > 0;
                    var eval = RuleEngine.Evaluate(dev, curPol, isTemp);
                    bool actuallyBlocked = (eval.RecommendedAction == RuleAction.DenyBlocked);

                    if (actuallyBlocked)
                    {
                        _drawerAccessBadge.Variant = StatusBadgeVariant.Danger;
                        _drawerAccessBadge.Text = Loc.Get("BadgeBlocked");
                    }
                    else if (_isReadOnly)
                    {
                        _drawerAccessBadge.Variant = StatusBadgeVariant.Warning;
                        _drawerAccessBadge.Text = Loc.Get("BadgeReadOnlyStatus");
                    }
                    else
                    {
                        _drawerAccessBadge.Variant = StatusBadgeVariant.Success;
                        _drawerAccessBadge.Text = Loc.Get("BadgeConnectedAllowed");
                    }
                }
            }

            if (_lblSafetyVal != null)
            {
                if (isSys)
                {
                    _lblSafetyVal.Text = Loc.Get("PolicyInternalProtected");
                }
                else if (isTrusted)
                {
                    _lblSafetyVal.Text = Loc.Get("PolicyTrustedActive");
                }
                else
                {
                    AppSettingsData curSet = SettingsRepository.Load();
                    ProtectionPolicy curPol = (curSet != null && curSet.ActivePolicy != null) ? curSet.ActivePolicy : new ProtectionPolicy();
                    bool isTemp = TemporaryAccessManager.PeekRemainingSeconds() > 0;
                    var eval = RuleEngine.Evaluate(dev, curPol, isTemp);
                    bool actuallyBlocked = (eval.RecommendedAction == RuleAction.DenyBlocked);
                    _lblSafetyVal.Text = actuallyBlocked ? Loc.Get("PolicyUntrustedRestricted") : Loc.Get("BadgeConnectedAllowed");
                }
            }

            // Footer Primary Button
            if (isSys)
            {
                _footerPrimaryBtn.Visible = false;
            }
            else
            {
                if (dev.IsRemovableMedia || dev.Category == DeviceCategory.UsbFlashDrive || dev.Category == DeviceCategory.ExternalUsbHdd || dev.Category == DeviceCategory.ExternalUsbSsd)
                {
                    _footerPrimaryBtn.Text = Loc.Get("SafelyEject");
                    _footerPrimaryBtn.Variant = ButtonVariant.Secondary;
                    _footerPrimaryBtn.Visible = true;
                }
                else
                {
                    _footerPrimaryBtn.Text = isTrusted ? Loc.Get("RevokeTrust") : Loc.Get("TrustDevice");
                    _footerPrimaryBtn.Variant = isTrusted ? ButtonVariant.Destructive : ButtonVariant.Secondary;
                    _footerPrimaryBtn.Visible = true;
                }
            }

            if (_layoutFooter != null) _layoutFooter();
            _isOpen = true;
            Visible = true;
            SendToBack();
            _scrollContent.UpdateScroll();
            Invalidate();
        }

        public static string GetCategoryText(DeviceCategory cat)
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

        public void CloseDrawer()
        {
            if (_isOpen || Visible)
            {
                _isOpen = false;
                Visible = false;
                if (DrawerClosed != null)
                {
                    DrawerClosed(this, EventArgs.Empty);
                }
            }
        }

        private void OnPrimaryActionClick(object sender, EventArgs e)
        {
            if (_currentDevice == null) return;

            bool isSys = _currentDevice.IsSystemDisk ||
                         _currentDevice.SafetyResult == SafetyValidationResult.RejectedSystemOrBootDisk ||
                         _currentDevice.Category == DeviceCategory.InternalFixedDisk ||
                         _currentDevice.Category == DeviceCategory.CriticalSystemHardware;

            if (isSys) return;

            if (_currentDevice.IsRemovableMedia || _currentDevice.Category == DeviceCategory.UsbFlashDrive || _currentDevice.Category == DeviceCategory.ExternalUsbHdd || _currentDevice.Category == DeviceCategory.ExternalUsbSsd)
            {
                if (EjectRequested != null) EjectRequested(this, EventArgs.Empty);
            }
            else
            {
                if (TrustRequested != null) TrustRequested(this, EventArgs.Empty);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                CloseDrawer();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                IThemePalette pal = ThemeManager.ActivePalette;
                BackColor = pal.Surface;
                if (_headerPanel != null) _headerPanel.BackColor = pal.Surface;
                if (_footerPanel != null) _footerPanel.BackColor = pal.Surface;
                if (_scrollContent != null)
                {
                    _scrollContent.BackColor = pal.BackgroundSecondary;
                    _scrollContent.ContentPanel.BackColor = pal.BackgroundSecondary;
                }
                if (_titleLabel != null) _titleLabel.ForeColor = pal.TextPrimary;
                if (_deviceNameLabel != null) _deviceNameLabel.ForeColor = pal.TextSecondary;
                Invalidate(true);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Draw leading separator line between list and drawer
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = RightToLeft == RightToLeft.Yes;
            using (Pen pen = new Pen(pal.Border, 1f))
            {
                if (isRtl)
                {
                    // Drawer is on Left, so its inner separator is on its Right edge
                    e.Graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height);
                }
                else
                {
                    // Drawer is on Right, so its inner separator is on its Left edge
                    e.Graphics.DrawLine(pen, 0, 0, 0, Height);
                }
            }
        }

        protected override void OnRightToLeftChanged(EventArgs e)
        {
            base.OnRightToLeftChanged(e);
            if (_scrollContent != null)
            {
                _scrollContent.RightToLeft = RightToLeft;
                _scrollContent.ContentPanel.RightToLeft = RightToLeft;
                PopulateSections();
                if (_currentDevice != null)
                {
                    ShowDevice(_currentDevice, _isTrusted, _isReadOnly);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                if (_drawerToolTip != null)
                {
                    _drawerToolTip.Dispose();
                    _drawerToolTip = null;
                }
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Custom themed value surface for hardware identifiers, replacing legacy WinForms textboxes.
    /// Features: SurfaceElevated background, subtle 1px BorderSubtle border, Consolas 8.5pt font,
    /// integrated ghost copy IconButton, full-value tooltip, and strict LTR rendering.
    /// </summary>
    public class IdentifierField : Panel
    {
        private readonly TextBox _textBox;
        private readonly IconButton _copyBtn;
        private readonly ToolTip _fullToolTip;
        private string _value = "";

        public string Value
        {
            get { return _value; }
            set
            {
                _value = value ?? "";
                _textBox.Text = _value;
                if (!string.IsNullOrEmpty(_value) && _value != "-")
                {
                    _fullToolTip.SetToolTip(this, _value);
                    _fullToolTip.SetToolTip(_textBox, _value);
                }
                else
                {
                    _fullToolTip.SetToolTip(this, null);
                    _fullToolTip.SetToolTip(_textBox, null);
                }
            }
        }

        public TextBox InnerTextBox { get { return _textBox; } }

        public IdentifierField(string copyTooltip)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            Height = DpiManager.ScaleInt(30);
            IThemePalette pal = ThemeManager.ActivePalette;
            BackColor = pal.SurfaceElevated;
            RightToLeft = RightToLeft.No;

            _fullToolTip = new ToolTip();
            _fullToolTip.ShowAlways = true;
            _fullToolTip.InitialDelay = 300;
            _fullToolTip.AutoPopDelay = 12000;
            _fullToolTip.ReshowDelay = 100;

            _textBox = new TextBox();
            _textBox.BorderStyle = BorderStyle.None;
            _textBox.BackColor = pal.SurfaceElevated;
            _textBox.ForeColor = pal.TextPrimary;
            _textBox.Font = new Font("Consolas", 8.5f, FontStyle.Regular);
            _textBox.ReadOnly = true;
            _textBox.RightToLeft = RightToLeft.No; // CRITICAL: strictly LTR

            _copyBtn = new IconButton();
            _copyBtn.ButtonType = IconButtonType.Copy;
            _copyBtn.SizeVariant = IconButtonSize.Small;
            _copyBtn.BackColor = Color.Transparent;
            _copyBtn.ToolTipText = copyTooltip;
            string defaultTip = copyTooltip;
            _copyBtn.MouseLeave += delegate
            {
                _copyBtn.ToolTipText = defaultTip;
            };
            _copyBtn.Click += delegate
            {
                if (!string.IsNullOrEmpty(_value) && _value != "-")
                {
                    try
                    {
                        Clipboard.SetText(_value);
                        _copyBtn.ToolTipText = Loc.Get("CopiedToClipboard");
                    }
                    catch { }
                }
            };

            Controls.Add(_textBox);
            Controls.Add(_copyBtn);

            PerformFieldLayout();
            Resize += delegate { PerformFieldLayout(); };
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void PerformFieldLayout()
        {
            int btnDim = DpiManager.ScaleInt(24);
            int padH = DpiManager.ScaleInt(6);
            int cy = (Height - btnDim) / 2;

            _copyBtn.Size = new Size(btnDim, btnDim);
            _copyBtn.Location = new Point(Width - padH - btnDim, cy);

            int txtX = padH;
            int txtW = Math.Max(20, _copyBtn.Left - padH - txtX);
            int txtY = (Height - _textBox.Height) / 2;
            _textBox.Location = new Point(txtX, txtY);
            _textBox.Width = txtW;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            IThemePalette pal = ThemeManager.ActivePalette;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusSmall);

            using (SolidBrush b = new SolidBrush(pal.SurfaceElevated))
            {
                UiDrawingUtils.FillRoundedRectangle(e.Graphics, b, rect, rad);
            }
            using (Pen p = new Pen(pal.BorderSubtle, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(e.Graphics, p, rect, rad);
            }
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                IThemePalette pal = ThemeManager.ActivePalette;
                BackColor = pal.SurfaceElevated;
                _textBox.BackColor = pal.SurfaceElevated;
                _textBox.ForeColor = pal.TextPrimary;
                Invalidate();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                if (_fullToolTip != null) _fullToolTip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
