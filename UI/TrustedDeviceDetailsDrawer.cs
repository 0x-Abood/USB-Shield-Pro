// ═══════════════════════════════════════════════════════════════════════════
// TrustedDeviceDetailsDrawer — Non-Modal Slide-In Inspector for Trusted Devices
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
    public class TrustedDeviceDetailsDrawer : Panel
    {
        private DeviceRule _currentRule;
        private DeviceIdentity _correlatedDevice;
        private bool _isOpen;

        private Panel _headerPanel;
        private Label _titleLabel;
        private Label _deviceNameLabel;
        private IconButton _closeBtn;

        private DarkScrollPanel _scrollContent;
        private Panel _footerPanel;
        private AppButton _footerCloseBtn;
        private AppButton _footerRevokeBtn;
        private AppButton _footerViewInConnBtn;
        private MethodInvoker _layoutFooter;

        // Section Badges
        private Panel _badgeRow;
        private MethodInvoker _badgeRowLayout;
        private StatusBadge _drawerTrustBadge;
        private StatusBadge _drawerConfidenceBadge;
        private StatusBadge _drawerConnectionBadge;

        // Themed Monospace Identifier Fields
        private IdentifierField _fieldSerial;
        private IdentifierField _fieldHardwareId;
        private IdentifierField _fieldInstanceId;

        // Labels for dynamic values
        private Label _lblFriendlyNameVal;
        private Label _lblDateVal;
        private Label _lblMatchTypeVal;
        private Label _lblRuleIdVal;

        // Dynamic Connection labels
        private Label _lblConnStateVal;
        private Label _lblCategoryVal;
        private Label _lblModelVal;
        private Label _lblVolumesVal;
        private Label _lblBusVal;
        private Label _lblOfflineNotice;

        public event EventHandler DrawerClosed;
        public event EventHandler RevokeRequested;
        public event EventHandler ViewInConnectedRequested;

        public DeviceRule CurrentRule { get { return _currentRule; } }
        public DeviceIdentity CorrelatedDevice { get { return _correlatedDevice; } }
        public bool IsConnected { get { return _correlatedDevice != null; } }
        public bool IsOpen { get { return _isOpen; } }

        public AppButton RevokeButton { get { return _footerRevokeBtn; } }
        public AppButton CloseButton { get { return _footerCloseBtn; } }
        public AppButton ViewInConnectedButton { get { return _footerViewInConnBtn; } }

        public IdentifierField SerialField     { get { return _fieldSerial; } }
        public IdentifierField HardwareIdField { get { return _fieldHardwareId; } }
        public IdentifierField InstanceIdField { get { return _fieldInstanceId; } }

        public string DeviceTitle { get { return _deviceNameLabel != null ? _deviceNameLabel.Text : ""; } }
        public StatusBadge TrustBadge { get { return _drawerTrustBadge; } }
        public StatusBadge ConfidenceBadge { get { return _drawerConfidenceBadge; } }
        public StatusBadge ConnectionBadge { get { return _drawerConnectionBadge; } }
        public Label OfflineNotice { get { return _lblOfflineNotice; } }
        public bool IsOfflineNoticeVisible { get { return _currentRule != null && _correlatedDevice == null; } }
        public Label RuleIdLabel { get { return _lblRuleIdVal; } }
        public Label MatchTypeLabel { get { return _lblMatchTypeVal; } }
        public Label FriendlyNameLabel { get { return _lblFriendlyNameVal; } }
        public Label DateLabel { get { return _lblDateVal; } }
        public Label ConnStateLabel { get { return _lblConnStateVal; } }
        public Label CategoryLabel { get { return _lblCategoryVal; } }
        public Label ModelLabel { get { return _lblModelVal; } }
        public Label VolumesLabel { get { return _lblVolumesVal; } }
        public Label BusLabel { get { return _lblBusVal; } }

        private ToolTip _drawerToolTip;

        public TrustedDeviceDetailsDrawer()
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
                if (Parent != null) Parent.PerformLayout();
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
            _titleLabel.Text = Loc.Get("DrawerTrustedTitle");
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

            _footerRevokeBtn = new AppButton();
            _footerRevokeBtn.Text = Loc.Get("RevokeTrust");
            _footerRevokeBtn.Variant = ButtonVariant.Destructive;
            _footerRevokeBtn.SizeVariant = ButtonSize.Small;
            _footerRevokeBtn.Click += delegate
            {
                if (RevokeRequested != null) RevokeRequested(this, EventArgs.Empty);
            };

            _footerViewInConnBtn = new AppButton();
            _footerViewInConnBtn.Text = Loc.Get("ViewInConnectedDevices");
            _footerViewInConnBtn.Variant = ButtonVariant.Secondary;
            _footerViewInConnBtn.SizeVariant = ButtonSize.Small;
            _footerViewInConnBtn.Visible = false;
            _footerViewInConnBtn.Click += delegate
            {
                if (ViewInConnectedRequested != null) ViewInConnectedRequested(this, EventArgs.Empty);
            };

            _footerPanel.Controls.Add(_footerCloseBtn);
            _footerPanel.Controls.Add(_footerRevokeBtn);
            _footerPanel.Controls.Add(_footerViewInConnBtn);

            _layoutFooter = delegate
            {
                int p = DpiManager.ScaleInt(16);
                int gap = DpiManager.ScaleInt(8);
                int fw = _footerPanel.ClientSize.Width;
                int btnY = (_footerPanel.Height - _footerCloseBtn.Height) / 2;
                bool rtl = RightToLeft == RightToLeft.Yes;

                _footerRevokeBtn.Width = AppButton.CalculateWidth(_footerRevokeBtn.Text, _footerRevokeBtn.SizeVariant, rtl);
                _footerCloseBtn.Width = AppButton.CalculateWidth(_footerCloseBtn.Text, _footerCloseBtn.SizeVariant, rtl);
                if (_footerViewInConnBtn.Visible)
                {
                    _footerViewInConnBtn.Width = AppButton.CalculateWidth(_footerViewInConnBtn.Text, _footerViewInConnBtn.SizeVariant, rtl);
                }

                if (rtl)
                {
                    int curX = p;
                    _footerCloseBtn.Location = new Point(curX, btnY);
                    curX += _footerCloseBtn.Width + gap;

                    _footerRevokeBtn.Location = new Point(curX, btnY);
                    curX += _footerRevokeBtn.Width + gap;

                    if (_footerViewInConnBtn.Visible)
                    {
                        _footerViewInConnBtn.Location = new Point(curX, btnY);
                    }
                }
                else
                {
                    int curX = fw - p;

                    curX -= _footerRevokeBtn.Width;
                    _footerRevokeBtn.Location = new Point(curX, btnY);
                    curX -= gap;

                    if (_footerViewInConnBtn.Visible)
                    {
                        curX -= _footerViewInConnBtn.Width;
                        _footerViewInConnBtn.Location = new Point(curX, btnY);
                        curX -= gap;
                    }

                    curX -= _footerCloseBtn.Width;
                    _footerCloseBtn.Location = new Point(curX, btnY);
                }
            };
            _footerPanel.Resize += delegate { _layoutFooter(); };
            _layoutFooter();

            // ── MIDDLE SCROLL CONTENT ───────────────────────────────────────
            _scrollContent = new DarkScrollPanel();
            _scrollContent.Dock = DockStyle.Fill;
            _scrollContent.BackColor = pal.BackgroundSecondary;
            _scrollContent.ContentPanel.BackColor = pal.BackgroundSecondary;
            _scrollContent.RightToLeft = RightToLeft;

            PopulateScrollSections();

            Controls.Add(_scrollContent);
            Controls.Add(_headerPanel);
            Controls.Add(_footerPanel);

            _headerPanel.SendToBack();
            _footerPanel.SendToBack();
            _scrollContent.BringToFront();

            layoutHeader();
            _layoutFooter();
        }

        private void PopulateScrollSections()
        {
            UiDrawingUtils.ClearAndDisposeControls(_scrollContent.ContentPanel.Controls);
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = RightToLeft == RightToLeft.Yes;

            int p = DpiManager.ScaleInt(16);
            int curY = DpiManager.ScaleInt(12);

            // ═══════════════════════════════════════════════════════════════
            // SECTION 1: TRUST INFORMATION
            // ═══════════════════════════════════════════════════════════════
            Label secTrustTitle = CreateSectionHeader(Loc.Get("SectionTrustInfo"), ref curY);
            _scrollContent.ContentPanel.Controls.Add(secTrustTitle);

            // Badges Row Container: Primary (Trusted) + Secondary (Connection)
            _badgeRow = new Panel();
            _badgeRow.Height = DpiManager.ScaleInt(26);
            _badgeRow.Location = new Point(p, curY);
            _badgeRow.Width = _scrollContent.ContentPanel.Width - (p * 2);
            _badgeRow.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _drawerTrustBadge = new StatusBadge();
            _drawerTrustBadge.Variant = StatusBadgeVariant.Success;
            _drawerTrustBadge.Text = Loc.Get("BadgeTrusted");

            _drawerConnectionBadge = new StatusBadge();
            _drawerConnectionBadge.Variant = StatusBadgeVariant.Neutral;
            _drawerConnectionBadge.Text = Loc.Get("BadgeOffline");

            _badgeRow.Controls.Add(_drawerTrustBadge);
            _badgeRow.Controls.Add(_drawerConnectionBadge);

            _badgeRowLayout = delegate
            {
                int bGap = DpiManager.ScaleInt(8);
                bool rtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
                int rowH = _badgeRow.Height;
                if (rtl)
                {
                    int bx = _badgeRow.Width - _drawerTrustBadge.Width;
                    _drawerTrustBadge.Location = new Point(bx, (rowH - _drawerTrustBadge.Height) / 2);
                    bx -= (_drawerConnectionBadge.Width + bGap);
                    _drawerConnectionBadge.Location = new Point(bx, (rowH - _drawerConnectionBadge.Height) / 2);
                }
                else
                {
                    int bx = 0;
                    _drawerTrustBadge.Location = new Point(bx, (rowH - _drawerTrustBadge.Height) / 2);
                    bx += _drawerTrustBadge.Width + bGap;
                    _drawerConnectionBadge.Location = new Point(bx, (rowH - _drawerConnectionBadge.Height) / 2);
                }
            };
            _badgeRow.Resize += delegate { _badgeRowLayout(); };
            _badgeRowLayout();

            _scrollContent.ContentPanel.Controls.Add(_badgeRow);
            curY += _badgeRow.Height + DpiManager.ScaleInt(12);

            // Metadata key-value rows
            AddDetailRow(Loc.Get("LabelFriendlyName"), out _lblFriendlyNameVal, ref curY);
            AddDetailRow(Loc.Get("LabelEnrollmentDate"), out _lblDateVal, ref curY);
            AddDetailRow(Loc.Get("LabelMatchType"), out _lblMatchTypeVal, ref curY);

            // Identity Quality (Confidence) moved into Identity / Trust Information content section
            _drawerConfidenceBadge = new StatusBadge();
            _drawerConfidenceBadge.Variant = StatusBadgeVariant.Success;
            _drawerConfidenceBadge.Text = Loc.Get("ConfidenceStrong");
            AddBadgeDetailRow(Loc.Get("PropConfidence"), _drawerConfidenceBadge, ref curY);

            AddDetailRow(Loc.Get("LabelRuleId"), out _lblRuleIdVal, ref curY);

            curY += DpiManager.ScaleInt(14);

            // ═══════════════════════════════════════════════════════════════
            // SECTION 2: HARDWARE IDENTIFIERS
            // ═══════════════════════════════════════════════════════════════
            Label secHwTitle = CreateSectionHeader(Loc.Get("SectionHardwareIds"), ref curY);
            _scrollContent.ContentPanel.Controls.Add(secHwTitle);

            // Serial Number
            Label lblSerTitle = CreateFieldLabel(Loc.Get("LabelSerial"), ref curY);
            _scrollContent.ContentPanel.Controls.Add(lblSerTitle);
            _fieldSerial = new IdentifierField(Loc.Get("CopySerial"));
            _fieldSerial.Location = new Point(p, curY);
            _fieldSerial.Width = _scrollContent.ContentPanel.Width - (p * 2);
            _fieldSerial.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _scrollContent.ContentPanel.Controls.Add(_fieldSerial);
            curY += _fieldSerial.Height + DpiManager.ScaleInt(8);

            // Hardware ID
            Label lblHwTitle = CreateFieldLabel(Loc.Get("LabelHardwareId"), ref curY);
            _scrollContent.ContentPanel.Controls.Add(lblHwTitle);
            _fieldHardwareId = new IdentifierField(Loc.Get("CopyHardwareId"));
            _fieldHardwareId.Location = new Point(p, curY);
            _fieldHardwareId.Width = _scrollContent.ContentPanel.Width - (p * 2);
            _fieldHardwareId.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _scrollContent.ContentPanel.Controls.Add(_fieldHardwareId);
            curY += _fieldHardwareId.Height + DpiManager.ScaleInt(8);

            // Instance ID
            Label lblInstTitle = CreateFieldLabel(Loc.Get("LabelDeviceInstanceId"), ref curY);
            _scrollContent.ContentPanel.Controls.Add(lblInstTitle);
            _fieldInstanceId = new IdentifierField(Loc.Get("CopyInstanceId"));
            _fieldInstanceId.Location = new Point(p, curY);
            _fieldInstanceId.Width = _scrollContent.ContentPanel.Width - (p * 2);
            _fieldInstanceId.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _scrollContent.ContentPanel.Controls.Add(_fieldInstanceId);
            curY += _fieldInstanceId.Height + DpiManager.ScaleInt(14);

            // ═══════════════════════════════════════════════════════════════
            // SECTION 3: CURRENT CONNECTION
            // ═══════════════════════════════════════════════════════════════
            Label secConnTitle = CreateSectionHeader(Loc.Get("SectionConnectionStatus"), ref curY);
            _scrollContent.ContentPanel.Controls.Add(secConnTitle);

            AddDetailRow(Loc.Get("SectionConnectionStatus"), out _lblConnStateVal, ref curY);
            AddDetailRow(Loc.Get("LabelDeviceCategory"), out _lblCategoryVal, ref curY);
            AddDetailRow(Loc.Get("PropModel"), out _lblModelVal, ref curY);
            AddDetailRow(Loc.Get("LabelMountPoints"), out _lblVolumesVal, ref curY);
            AddDetailRow(Loc.Get("LabelPhysicalBus"), out _lblBusVal, ref curY);

            _lblOfflineNotice = new Label();
            _lblOfflineNotice.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            _lblOfflineNotice.ForeColor = pal.TextMuted;
            _lblOfflineNotice.Location = new Point(p, curY);
            _lblOfflineNotice.Size = new Size(_scrollContent.ContentPanel.Width - (p * 2), DpiManager.ScaleInt(34));
            _lblOfflineNotice.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _lblOfflineNotice.Text = Loc.Get("DeviceOfflineNotice");
            _lblOfflineNotice.TextAlign = isRtl ? ContentAlignment.TopRight : ContentAlignment.TopLeft;
            _lblOfflineNotice.Visible = false;
            _scrollContent.ContentPanel.Controls.Add(_lblOfflineNotice);

            curY += _lblOfflineNotice.Height + DpiManager.ScaleInt(20);

            _scrollContent.UpdateScroll();
        }

        private Label CreateSectionHeader(string text, ref int curY)
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int p = DpiManager.ScaleInt(16);

            Label lbl = new Label();
            lbl.Text = text;
            lbl.Font = new Font("Segoe UI", DpiManager.ScaleFloat(10.5f), FontStyle.Bold);
            lbl.ForeColor = pal.TextPrimary;
            lbl.Location = new Point(p, curY);
            lbl.Size = new Size(_scrollContent.ContentPanel.Width - (p * 2), DpiManager.ScaleInt(isRtl ? 24 : 20));
            lbl.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lbl.TextAlign = isRtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;
            lbl.RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;

            curY += lbl.Height + DpiManager.ScaleInt(6);
            return lbl;
        }

        private void AddBadgeDetailRow(string key, StatusBadge badge, ref int curY)
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int p = DpiManager.ScaleInt(16);
            int rowH = DpiManager.ScaleInt(26);

            Panel row = new Panel();
            row.Location = new Point(p, curY);
            row.Size = new Size(_scrollContent.ContentPanel.Width - (p * 2), rowH);
            row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.RightToLeft = RightToLeft.No;

            Label lblKey = new Label();
            lblKey.Text = (key ?? "").TrimEnd(':', ' ', '：');
            lblKey.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            lblKey.ForeColor = pal.TextSecondary;
            lblKey.AutoSize = true;
            lblKey.RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;

            row.Controls.Add(lblKey);
            row.Controls.Add(badge);

            MethodInvoker layoutRow = delegate
            {
                int rw = row.Width;
                bool rtl = (this.RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
                int keyW = lblKey.Width;

                if (rtl)
                {
                    lblKey.Location = new Point(rw - keyW, (rowH - lblKey.Height) / 2);
                    badge.Location = new Point(0, (rowH - badge.Height) / 2);
                }
                else
                {
                    lblKey.Location = new Point(0, (rowH - lblKey.Height) / 2);
                    badge.Location = new Point(keyW + DpiManager.ScaleInt(12), (rowH - badge.Height) / 2);
                }
            };

            row.Resize += delegate { layoutRow(); };
            layoutRow();

            _scrollContent.ContentPanel.Controls.Add(row);
            curY += rowH + DpiManager.ScaleInt(4);
        }

        private Label CreateFieldLabel(string text, ref int curY)
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int p = DpiManager.ScaleInt(16);

            Label lbl = new Label();
            lbl.Text = (text ?? "").TrimEnd(':', ' ', '：');
            lbl.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            lbl.ForeColor = pal.TextSecondary;
            lbl.Location = new Point(p, curY);
            lbl.Size = new Size(_scrollContent.ContentPanel.Width - (p * 2), DpiManager.ScaleInt(18));
            lbl.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lbl.TextAlign = isRtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;
            lbl.RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;

            curY += lbl.Height + DpiManager.ScaleInt(3);
            return lbl;
        }

        private void AddDetailRow(string key, out Label valLabel, ref int curY)
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int p = DpiManager.ScaleInt(16);
            int rowH = DpiManager.ScaleInt(22);

            Panel row = new Panel();
            row.Location = new Point(p, curY);
            row.Size = new Size(_scrollContent.ContentPanel.Width - (p * 2), rowH);
            row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.RightToLeft = RightToLeft.No; // Ensure absolute coordinates across RTL containers

            Label lblKey = new Label();
            lblKey.Text = (key ?? "").TrimEnd(':', ' ', '：');
            lblKey.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            lblKey.ForeColor = pal.TextSecondary;
            lblKey.AutoSize = true;
            lblKey.RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;

            valLabel = new Label();
            valLabel.Text = "-";
            valLabel.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            valLabel.ForeColor = pal.TextPrimary;
            valLabel.AutoEllipsis = true;
            valLabel.RightToLeft = RightToLeft.No; // Strict LTR

            row.Controls.Add(lblKey);
            row.Controls.Add(valLabel);

            Label capturedVal = valLabel;
            Panel capturedRow = row;
            Action updateTips = delegate
            {
                if (_drawerToolTip == null) return;
                string raw = (capturedVal.Text ?? "").Trim('\u200E', '\u200F', ' ');
                if (!string.IsNullOrEmpty(raw) && raw != "-")
                {
                    _drawerToolTip.SetToolTip(capturedVal, raw);
                    _drawerToolTip.SetToolTip(capturedRow, raw);
                }
                else
                {
                    _drawerToolTip.SetToolTip(capturedVal, null);
                    _drawerToolTip.SetToolTip(capturedRow, null);
                }
            };
            valLabel.TextChanged += delegate { updateTips(); };
            updateTips();

            MethodInvoker layoutRow = delegate
            {
                int rw = row.Width;
                bool rtl = (this.RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
                int keyW = lblKey.Width;
                int valW = Math.Max(50, rw - keyW - DpiManager.ScaleInt(12));
                capturedVal.Size = new Size(valW, rowH);

                if (rtl)
                {
                    // RIGHT COLUMN: Arabic field label, RTL / right aligned
                    lblKey.Location = new Point(rw - keyW, (rowH - lblKey.Height) / 2);
                    lblKey.RightToLeft = RightToLeft.Yes;

                    // LEFT COLUMN: Technical value, strict LTR / left aligned
                    capturedVal.Location = new Point(0, (rowH - capturedVal.Height) / 2);
                    capturedVal.TextAlign = ContentAlignment.MiddleLeft;
                    capturedVal.RightToLeft = RightToLeft.No;
                }
                else
                {
                    // LEFT COLUMN: English field label, left aligned
                    lblKey.Location = new Point(0, (rowH - lblKey.Height) / 2);
                    lblKey.RightToLeft = RightToLeft.No;

                    // RIGHT COLUMN: Value, left aligned
                    capturedVal.Location = new Point(keyW + DpiManager.ScaleInt(12), (rowH - capturedVal.Height) / 2);
                    capturedVal.TextAlign = ContentAlignment.MiddleLeft;
                    capturedVal.RightToLeft = RightToLeft.No;
                }
            };

            row.Resize += delegate { layoutRow(); };
            layoutRow();

            _scrollContent.ContentPanel.Controls.Add(row);
            curY += rowH + DpiManager.ScaleInt(4);
        }

        public void SetRule(DeviceRule rule, DeviceIdentity correlatedDevice)
        {
            _currentRule = rule;
            _correlatedDevice = correlatedDevice;

            if (rule == null)
            {
                CloseDrawer();
                return;
            }

            bool isRtl = RightToLeft == RightToLeft.Yes;
            bool isConnected = (correlatedDevice != null);

            // Header
            _deviceNameLabel.Text = !string.IsNullOrEmpty(rule.FriendlyName) ? rule.FriendlyName : "USB Device";

            // Badges
            _drawerTrustBadge.Variant = StatusBadgeVariant.Success;
            _drawerTrustBadge.Text = Loc.Get("BadgeTrusted");

            switch (rule.Confidence)
            {
                case IdentityConfidence.Strong:
                    _drawerConfidenceBadge.Variant = StatusBadgeVariant.Success;
                    _drawerConfidenceBadge.Text = Loc.Get("ConfidenceStrong");
                    break;
                case IdentityConfidence.Medium:
                    _drawerConfidenceBadge.Variant = StatusBadgeVariant.Info;
                    _drawerConfidenceBadge.Text = Loc.Get("ConfidenceMedium");
                    break;
                default:
                    _drawerConfidenceBadge.Variant = StatusBadgeVariant.Warning;
                    _drawerConfidenceBadge.Text = Loc.Get("ConfidenceWeak");
                    break;
            }

            _drawerConnectionBadge.Variant = isConnected ? StatusBadgeVariant.Success : StatusBadgeVariant.Neutral;
            _drawerConnectionBadge.Text = isConnected ? Loc.Get("BadgeConnected") : Loc.Get("BadgeOffline");
            if (_badgeRowLayout != null) _badgeRowLayout();

            // Section 1 Values
            _lblFriendlyNameVal.Text = !string.IsNullOrEmpty(rule.FriendlyName) ? rule.FriendlyName : "-";
            _lblDateVal.Text = rule.CreatedUtc != DateTime.MinValue ? rule.CreatedUtc.ToLocalTime().ToString("g") : "-";
            switch (rule.MatchType)
            {
                case RuleMatchType.DeviceInstanceId: _lblMatchTypeVal.Text = Loc.Get("DeviceMatchInstanceId"); break;
                case RuleMatchType.HardwareId: _lblMatchTypeVal.Text = Loc.Get("DeviceMatchHardwareId"); break;
                case RuleMatchType.SerialNumber: _lblMatchTypeVal.Text = Loc.Get("DeviceMatchSerial"); break;
                case RuleMatchType.ModelFallback: _lblMatchTypeVal.Text = Loc.Get("DeviceMatchModel"); break;
                default: _lblMatchTypeVal.Text = rule.MatchType.ToString(); break;
            }
            _lblRuleIdVal.Text = !string.IsNullOrEmpty(rule.RuleId) ? rule.RuleId : "-";

            // Section 2 Values
            _fieldSerial.Value = !string.IsNullOrEmpty(rule.TargetSerial) ? rule.TargetSerial : "-";
            _fieldHardwareId.Value = !string.IsNullOrEmpty(rule.TargetHardwareId) ? rule.TargetHardwareId : "-";
            _fieldInstanceId.Value = !string.IsNullOrEmpty(rule.TargetInstanceId) ? rule.TargetInstanceId : "-";

            // Section 3 Values
            if (isConnected)
            {
                _lblConnStateVal.Text = Loc.Get("BadgeConnected");
                _lblCategoryVal.Text = DeviceDetailsDrawer.GetCategoryText(correlatedDevice.Category);
                _lblModelVal.Text = !string.IsNullOrEmpty(correlatedDevice.Model) ? correlatedDevice.Model : (!string.IsNullOrEmpty(correlatedDevice.FriendlyName) ? correlatedDevice.FriendlyName : "-");
                _lblVolumesVal.Text = (correlatedDevice.VolumeMountings != null && correlatedDevice.VolumeMountings.Count > 0)
                    ? string.Join(", ", correlatedDevice.VolumeMountings.ToArray())
                    : "-";
                _lblBusVal.Text = correlatedDevice.PhysicalTransport.ToString();

                _lblOfflineNotice.Visible = false;
                _footerViewInConnBtn.Visible = true;
            }
            else
            {
                _lblConnStateVal.Text = Loc.Get("BadgeOffline");
                _lblCategoryVal.Text = "-";
                _lblModelVal.Text = "-";
                _lblVolumesVal.Text = "-";
                _lblBusVal.Text = "-";

                _lblOfflineNotice.Visible = true;
                _footerViewInConnBtn.Visible = false;
            }

            if (_layoutFooter != null) _layoutFooter();
            _footerPanel.PerformLayout();
            _scrollContent.UpdateScroll();
        }

        public void OpenDrawer(DeviceRule rule, DeviceIdentity correlatedDevice)
        {
            SetRule(rule, correlatedDevice);
            _isOpen = true;
            Visible = true;
            SendToBack();
            Focus();
        }

        public void CloseDrawer()
        {
            _isOpen = false;
            Visible = false;
            if (DrawerClosed != null)
            {
                DrawerClosed(this, EventArgs.Empty);
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
            if (!IsDisposed)
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
                PopulateScrollSections();
                if (_currentRule != null)
                {
                    SetRule(_currentRule, _correlatedDevice);
                }
                if (IsHandleCreated) Invalidate(true);
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
                PopulateScrollSections();
                if (_currentRule != null)
                {
                    SetRule(_currentRule, _correlatedDevice);
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
}
