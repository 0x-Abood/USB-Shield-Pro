// ═══════════════════════════════════════════════════════════════════════════
// ActivityDetailsDrawer — Non-Modal Slide-In Inspector for Activity Events
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
    public class ActivityDetailsDrawer : Panel
    {
        private ActivityRecord _currentRecord;
        private bool _isOpen;

        private Panel _headerPanel;
        private Label _titleLabel;
        private Label _eventTitleHeaderLabel;
        private IconButton _closeBtn;

        private DarkScrollPanel _scrollContent;
        private Panel _footerPanel;
        private AppButton _footerCloseBtn;

        // Section Badges
        private Panel _badgeRow;
        private StatusBadge _drawerCategoryBadge;
        private StatusBadge _drawerResultBadge;

        // Themed Monospace Identifier Fields
        private IdentifierField _fieldTimestampIso;
        private IdentifierField _fieldCategoryCode;
        private IdentifierField _fieldContextToken;
        private IdentifierField _fieldResultCode;

        // Labels for dynamic values
        private Label _lblEventTitleVal;
        private Label _lblTimestampVal;
        private Label _lblCategoryVal;
        private Label _lblResultVal;
        private Label _lblContextVal;
        private Label _lblActionDescVal;

        public event EventHandler DrawerClosed;

        public ActivityRecord CurrentRecord { get { return _currentRecord; } }
        public bool IsOpen { get { return _isOpen; } }

        public AppButton CloseButton { get { return _footerCloseBtn; } }
        public IconButton HeaderCloseButton { get { return _closeBtn; } }

        public IdentifierField TimestampField { get { return _fieldTimestampIso; } }
        public IdentifierField CategoryField  { get { return _fieldCategoryCode; } }
        public IdentifierField ContextField   { get { return _fieldContextToken; } }
        public IdentifierField ResultField    { get { return _fieldResultCode; } }

        public StatusBadge CategoryBadge { get { return _drawerCategoryBadge; } }
        public StatusBadge ResultBadge   { get { return _drawerResultBadge; } }

        public Label EventTitleLabel       { get { return _lblEventTitleVal; } }
        public Label TimestampValueLabel   { get { return _lblTimestampVal; } }
        public Label ContextValueLabel     { get { return _lblContextVal; } }
        public Label ActionDescValueLabel  { get { return _lblActionDescVal; } }

        public ActivityDetailsDrawer()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            Visible = false;
            Width = DpiManager.ScaleInt(367);
            BackColor = ThemeManager.ActivePalette.Surface;

            BuildDrawerUI();
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public void UpdateResponsiveWidth(int viewportWidth)
        {
            int targetW = (int)Math.Round(viewportWidth * 0.32);
            int minW = DpiManager.ScaleInt(320);
            int maxW = DpiManager.ScaleInt(400);
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
            bool isRtl = Loc.IsRtl;

            // ── 1. HEADER PANEL ──────────────────────────────────────────────
            _headerPanel = new Panel();
            _headerPanel.Dock = DockStyle.Top;
            _headerPanel.Height = DpiManager.ScaleInt(68);
            _headerPanel.BackColor = pal.SurfaceElevated;
            _headerPanel.Padding = new Padding(DpiManager.ScaleInt(16));

            _titleLabel = new Label();
            _titleLabel.Text = Loc.Get("DrawerEventTitle");
            _titleLabel.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            _titleLabel.ForeColor = pal.TextSecondary;
            _titleLabel.AutoSize = false;
            _titleLabel.Height = DpiManager.ScaleInt(isRtl ? 20 : 16);

            _eventTitleHeaderLabel = new Label();
            _eventTitleHeaderLabel.Text = "-";
            _eventTitleHeaderLabel.Font = DpiManager.GetFont(TypographyRole.Subtitle, isRtl);
            _eventTitleHeaderLabel.ForeColor = pal.TextPrimary;
            _eventTitleHeaderLabel.AutoSize = false;
            _eventTitleHeaderLabel.Height = DpiManager.ScaleInt(isRtl ? 28 : 24);

            _closeBtn = new IconButton();
            _closeBtn.ButtonType = IconButtonType.Close;
            _closeBtn.SizeVariant = IconButtonSize.Small;
            _closeBtn.ToolTipText = Loc.Get("Close");
            _closeBtn.Click += delegate { CloseDrawer(); };

            _headerPanel.Controls.Add(_titleLabel);
            _headerPanel.Controls.Add(_eventTitleHeaderLabel);
            _headerPanel.Controls.Add(_closeBtn);

            MethodInvoker layoutHeader = delegate
            {
                bool rtl = Loc.IsRtl;
                int p = DpiManager.ScaleInt(16);
                int btnSize = DpiManager.ScaleInt(26);
                _closeBtn.Size = new Size(btnSize, btnSize);
                int btnY = (_headerPanel.Height - btnSize) / 2;

                if (rtl)
                {
                    _closeBtn.Location = new Point(p, btnY);
                    int textLeft = p + btnSize + DpiManager.ScaleInt(8);
                    int textRight = _headerPanel.Width - p;
                    int textW = Math.Max(50, textRight - textLeft);

                    _titleLabel.Location = new Point(textLeft, DpiManager.ScaleInt(10));
                    _titleLabel.Width = textW;
                    _titleLabel.TextAlign = ContentAlignment.MiddleRight;

                    _eventTitleHeaderLabel.Location = new Point(textLeft, DpiManager.ScaleInt(32));
                    _eventTitleHeaderLabel.Width = textW;
                    _eventTitleHeaderLabel.TextAlign = ContentAlignment.MiddleRight;
                }
                else
                {
                    _closeBtn.Location = new Point(_headerPanel.Width - p - btnSize, btnY);
                    int textLeft = p;
                    int textW = Math.Max(50, _headerPanel.Width - p - btnSize - DpiManager.ScaleInt(8) - textLeft);

                    _titleLabel.Location = new Point(textLeft, DpiManager.ScaleInt(10));
                    _titleLabel.Width = textW;
                    _titleLabel.TextAlign = ContentAlignment.MiddleLeft;

                    _eventTitleHeaderLabel.Location = new Point(textLeft, DpiManager.ScaleInt(32));
                    _eventTitleHeaderLabel.Width = textW;
                    _eventTitleHeaderLabel.TextAlign = ContentAlignment.MiddleLeft;
                }
            };
            _headerPanel.Resize += delegate { layoutHeader(); };
            layoutHeader();

            // ── 2. FOOTER PANEL ──────────────────────────────────────────────
            _footerPanel = new Panel();
            _footerPanel.Dock = DockStyle.Bottom;
            _footerPanel.Height = DpiManager.ScaleInt(54);
            _footerPanel.BackColor = pal.SurfaceElevated;
            _footerPanel.Padding = new Padding(DpiManager.ScaleInt(16), DpiManager.ScaleInt(10), DpiManager.ScaleInt(16), DpiManager.ScaleInt(10));

            _footerCloseBtn = new AppButton();
            _footerCloseBtn.SizeVariant = ButtonSize.Small;
            _footerCloseBtn.Variant = ButtonVariant.Secondary;
            _footerCloseBtn.Text = Loc.Get("Close");
            _footerCloseBtn.Click += delegate { CloseDrawer(); };

            _footerPanel.Controls.Add(_footerCloseBtn);

            MethodInvoker layoutFooter = delegate
            {
                bool rtl = Loc.IsRtl;
                int p = DpiManager.ScaleInt(16);
                int btnH = DpiManager.ScaleInt(28);
                int btnY = (_footerPanel.Height - btnH) / 2;

                _footerCloseBtn.Width = AppButton.CalculateWidth(_footerCloseBtn.Text, _footerCloseBtn.SizeVariant, rtl);
                _footerCloseBtn.Height = btnH;

                if (rtl)
                {
                    _footerCloseBtn.Location = new Point(p, btnY);
                }
                else
                {
                    _footerCloseBtn.Location = new Point(_footerPanel.Width - p - _footerCloseBtn.Width, btnY);
                }
            };
            _footerPanel.Resize += delegate { layoutFooter(); };
            layoutFooter();

            // ── 3. SCROLL CONTENT ────────────────────────────────────────────
            _scrollContent = new DarkScrollPanel();
            _scrollContent.Dock = DockStyle.Fill;
            _scrollContent.RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;

            BuildScrollBody();

            // Assemble Drawer controls
            Controls.Add(_scrollContent);
            Controls.Add(_footerPanel);
            Controls.Add(_headerPanel);

            _headerPanel.BringToFront();
            _footerPanel.BringToFront();
            _scrollContent.BringToFront();
        }

        private void BuildScrollBody()
        {
            Panel container = _scrollContent.ContentPanel;
            container.SuspendLayout();
            container.Controls.Clear();

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = Loc.IsRtl;

            int pad = DpiManager.ScaleInt(16);
            int curY = pad;

            // ── BADGE ROW (Category & Result) ────────────────────────────────
            _badgeRow = new Panel();
            _badgeRow.Height = DpiManager.ScaleInt(26);
            _badgeRow.BackColor = Color.Transparent;

            _drawerCategoryBadge = new StatusBadge();
            _drawerCategoryBadge.TabStop = false;
            _drawerCategoryBadge.Variant = StatusBadgeVariant.Info;
            _drawerCategoryBadge.Text = "-";

            _drawerResultBadge = new StatusBadge();
            _drawerResultBadge.TabStop = false;
            _drawerResultBadge.Variant = StatusBadgeVariant.Neutral;
            _drawerResultBadge.Text = "-";

            _badgeRow.Controls.Add(_drawerCategoryBadge);
            _badgeRow.Controls.Add(_drawerResultBadge);

            MethodInvoker layoutBadgeRow = delegate
            {
                bool rtl = Loc.IsRtl;
                int gap = DpiManager.ScaleInt(6);
                int bH = _badgeRow.Height;
                if (rtl)
                {
                    int x = _badgeRow.Width;
                    x -= _drawerCategoryBadge.Width;
                    _drawerCategoryBadge.Location = new Point(x, (_badgeRow.Height - _drawerCategoryBadge.Height) / 2);
                    x -= gap;
                    x -= _drawerResultBadge.Width;
                    _drawerResultBadge.Location = new Point(x, (_badgeRow.Height - _drawerResultBadge.Height) / 2);
                }
                else
                {
                    int x = 0;
                    _drawerCategoryBadge.Location = new Point(x, (_badgeRow.Height - _drawerCategoryBadge.Height) / 2);
                    x += _drawerCategoryBadge.Width + gap;
                    _drawerResultBadge.Location = new Point(x, (_badgeRow.Height - _drawerResultBadge.Height) / 2);
                }
            };
            _badgeRow.Resize += delegate { layoutBadgeRow(); };
            container.Controls.Add(_badgeRow);
            curY += _badgeRow.Height + DpiManager.ScaleInt(14);

            // ── SECTION 1: EVENT SUMMARY ─────────────────────────────────────
            CreateSectionHeader(container, Loc.Get("SectionEventSummary"), ref curY);

            curY = AddDrawerField(container, curY, Loc.Get("LabelEventTitle"), out _lblEventTitleVal);
            curY = AddDrawerField(container, curY, Loc.Get("LabelTimestamp"), out _lblTimestampVal);
            curY = AddDrawerField(container, curY, Loc.Get("LabelCategory"), out _lblCategoryVal);
            curY = AddDrawerField(container, curY, Loc.Get("LabelResult"), out _lblResultVal);

            curY += DpiManager.ScaleInt(10);

            // ── SECTION 2: CONTEXT & ACTION DETAILS ──────────────────────────
            CreateSectionHeader(container, Loc.Get("SectionEventContext"), ref curY);

            curY = AddDrawerField(container, curY, Loc.Get("LabelContext"), out _lblContextVal);
            curY = AddDrawerField(container, curY, Loc.Get("Details"), out _lblActionDescVal);

            curY += DpiManager.ScaleInt(10);

            // ── SECTION 3: TECHNICAL CONTEXT (LTR Monospace) ──────────────────
            CreateSectionHeader(container, Loc.Get("SectionTechnicalDetails"), ref curY);

            curY = AddIdentifierFieldRow(container, curY, "TIMESTAMP (ISO):", out _fieldTimestampIso);
            curY = AddIdentifierFieldRow(container, curY, "CATEGORY CODE:", out _fieldCategoryCode);
            curY = AddIdentifierFieldRow(container, curY, "CONTEXT TOKEN:", out _fieldContextToken);
            curY = AddIdentifierFieldRow(container, curY, "RESULT / STATUS:", out _fieldResultCode);

            curY += DpiManager.ScaleInt(20);

            // Responsive width adjustment for all children
            MethodInvoker resizeBodyControls = delegate
            {
                int cW = Math.Max(200, container.ClientSize.Width - (pad * 2));
                _badgeRow.Location = new Point(pad, DpiManager.ScaleInt(16));
                _badgeRow.Width = cW;
                layoutBadgeRow();

                foreach (Control c in container.Controls)
                {
                    if (c == _badgeRow) continue;
                    c.Location = new Point(pad, c.Top);
                    c.Width = cW;
                }
            };
            container.Resize += delegate { resizeBodyControls(); };
            resizeBodyControls();

            container.ResumeLayout(true);
            _scrollContent.UpdateScroll();
        }

        private Label CreateSectionHeader(Panel container, string text, ref int curY)
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = Loc.IsRtl;
            int p = DpiManager.ScaleInt(16);
            int rowW = Math.Max(200, container.ClientSize.Width - (p * 2));

            Label lbl = new Label();
            lbl.Text = text;
            lbl.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            lbl.ForeColor = pal.TextSecondary;
            lbl.Location = new Point(p, curY);
            lbl.Size = new Size(rowW, DpiManager.ScaleInt(20));
            lbl.TextAlign = isRtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;
            container.Controls.Add(lbl);

            curY += lbl.Height + DpiManager.ScaleInt(6);
            return lbl;
        }

        private int AddDrawerField(Panel container, int startY, string labelText, out Label valLabel)
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = Loc.IsRtl;
            int pad = DpiManager.ScaleInt(16);
            int rowW = Math.Max(200, container.ClientSize.Width - (pad * 2));
            int lblH = DpiManager.ScaleInt(isRtl ? 20 : 16);
            int valH = DpiManager.ScaleInt(isRtl ? 24 : 20);

            Label lbl = new Label();
            lbl.Text = labelText;
            lbl.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            lbl.ForeColor = pal.TextSecondary;
            lbl.AutoSize = false;
            lbl.Size = new Size(rowW, lblH);
            lbl.Location = new Point(pad, startY);
            lbl.TextAlign = isRtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;
            container.Controls.Add(lbl);

            valLabel = new Label();
            valLabel.Text = "-";
            valLabel.Font = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            valLabel.ForeColor = pal.TextPrimary;
            valLabel.AutoSize = false;
            valLabel.Size = new Size(rowW, valH);
            valLabel.Location = new Point(pad, startY + lblH);
            valLabel.TextAlign = isRtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;
            container.Controls.Add(valLabel);

            return startY + lblH + valH + DpiManager.ScaleInt(6);
        }

        private int AddIdentifierFieldRow(Panel container, int startY, string labelText, out IdentifierField field)
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = Loc.IsRtl;
            int pad = DpiManager.ScaleInt(16);
            int rowW = Math.Max(200, container.ClientSize.Width - (pad * 2));
            int lblH = DpiManager.ScaleInt(isRtl ? 20 : 16);

            Label lbl = new Label();
            lbl.Text = labelText;
            lbl.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            lbl.ForeColor = pal.TextSecondary;
            lbl.AutoSize = false;
            lbl.Size = new Size(rowW, lblH);
            lbl.Location = new Point(pad, startY);
            lbl.TextAlign = isRtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;
            container.Controls.Add(lbl);

            field = new IdentifierField(Loc.Get("CopyHardwareId"));
            field.Size = new Size(rowW, DpiManager.ScaleInt(30));
            field.Location = new Point(pad, startY + lblH + DpiManager.ScaleInt(2));
            container.Controls.Add(field);

            return startY + lblH + field.Height + DpiManager.ScaleInt(8);
        }

        public void OpenDrawer(ActivityRecord record)
        {
            if (record == null) return;
            _currentRecord = record;
            _isOpen = true;

            UpdateRecordData();

            Visible = true;
            SendToBack(); // Important: SendToBack enables Dock=Fill scroll panel to take remaining space without overlay
            Invalidate();
        }

        public void CloseDrawer()
        {
            if (!_isOpen && !Visible) return;
            _isOpen = false;
            Visible = false;
            if (DrawerClosed != null) DrawerClosed(this, EventArgs.Empty);
        }

        public static string GetLocalizedCategoryName(ActivityCategory cat)
        {
            switch (cat)
            {
                case ActivityCategory.Devices:
                case ActivityCategory.DeviceEvents:
                    return Loc.Get("ActCatDeviceEvents");
                case ActivityCategory.Protection:
                case ActivityCategory.PolicyChanges:
                    return Loc.Get("ActCatProtection");
                case ActivityCategory.Security:
                case ActivityCategory.TrustedDevices:
                    return Loc.Get("ActCatSecurity");
                case ActivityCategory.Errors:
                    return Loc.Get("ActCatErrors");
                default:
                    return cat.ToString();
            }
        }

        public void UpdateRecordData()
        {
            if (_currentRecord == null) return;

            bool isRtl = Loc.IsRtl;

            _eventTitleHeaderLabel.Text = _currentRecord.Title ?? "-";
            _lblEventTitleVal.Text = _currentRecord.Title ?? "-";
            _lblTimestampVal.Text = _currentRecord.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

            string catLocalized = GetLocalizedCategoryName(_currentRecord.Category);
            _lblCategoryVal.Text = catLocalized;
            _lblResultVal.Text = !string.IsNullOrEmpty(_currentRecord.ResultBadge) ? _currentRecord.ResultBadge : "-";

            _lblContextVal.Text = !string.IsNullOrEmpty(_currentRecord.Context) ? _currentRecord.Context : "-";
            _lblActionDescVal.Text = _currentRecord.Title ?? "-";

            // Update Badges
            _drawerCategoryBadge.Variant = StatusBadgeVariant.Info;
            _drawerCategoryBadge.Text = catLocalized;

            _drawerResultBadge.Variant = ActivityRow.DetermineBadgeVariant(_currentRecord);
            _drawerResultBadge.Text = !string.IsNullOrEmpty(_currentRecord.ResultBadge) ? _currentRecord.ResultBadge : "-";

            // Update Technical Identifiers (strictly LTR Consolas)
            _fieldTimestampIso.Value = _currentRecord.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            _fieldCategoryCode.Value = ((int)_currentRecord.Category).ToString() + " (" + _currentRecord.Category.ToString() + ")";
            _fieldContextToken.Value = !string.IsNullOrEmpty(_currentRecord.Context) ? _currentRecord.Context : "N/A";
            _fieldResultCode.Value = !string.IsNullOrEmpty(_currentRecord.ResultBadge) ? _currentRecord.ResultBadge : "N/A";

            if (_badgeRow != null)
            {
                _badgeRow.PerformLayout();
            }
            if (_scrollContent != null)
            {
                _scrollContent.UpdateScroll();
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
                UpdateThemeColors();
                if (_currentRecord != null) UpdateRecordData();
                Invalidate(true);
            }
        }

        private void UpdateThemeColors()
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            BackColor = pal.Surface;

            if (_headerPanel != null)
            {
                _headerPanel.BackColor = pal.SurfaceElevated;
            }
            if (_titleLabel != null)
            {
                _titleLabel.ForeColor = pal.TextSecondary;
            }
            if (_eventTitleHeaderLabel != null)
            {
                _eventTitleHeaderLabel.ForeColor = pal.TextPrimary;
            }
            if (_footerPanel != null)
            {
                _footerPanel.BackColor = pal.SurfaceElevated;
            }
            if (_scrollContent != null)
            {
                _scrollContent.BackColor = pal.Surface;
                if (_scrollContent.ContentPanel != null)
                {
                    _scrollContent.ContentPanel.BackColor = pal.Surface;
                    foreach (Control c in _scrollContent.ContentPanel.Controls)
                    {
                        Label lbl = c as Label;
                        if (lbl != null)
                        {
                            if (lbl == _lblEventTitleVal || lbl == _lblTimestampVal ||
                                lbl == _lblCategoryVal || lbl == _lblResultVal ||
                                lbl == _lblContextVal || lbl == _lblActionDescVal)
                            {
                                lbl.ForeColor = pal.TextPrimary;
                            }
                            else
                            {
                                lbl.ForeColor = pal.TextSecondary;
                            }
                        }
                        else
                        {
                            IdentifierField idField = c as IdentifierField;
                            if (idField != null)
                            {
                                idField.Invalidate(true);
                            }
                        }
                    }
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = Loc.IsRtl;

            // Background
            using (SolidBrush bg = new SolidBrush(pal.Surface))
            {
                g.FillRectangle(bg, ClientRectangle);
            }

            // Boundary separator line (Left border in LTR, Right border in RTL)
            using (Pen borderPen = new Pen(pal.BorderSubtle, 1f))
            {
                if (isRtl)
                {
                    g.DrawLine(borderPen, Width - 1, 0, Width - 1, Height);
                }
                else
                {
                    g.DrawLine(borderPen, 0, 0, 0, Height);
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
