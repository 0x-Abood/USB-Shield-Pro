// ═══════════════════════════════════════════════════════════════════════════
// ProtectionSectionCard — Bounded Section Container for v2.1 Policy Groups
// USB Shield Pro v2.1 (Phase 4.3 Protection Page Redesign)
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace UsbShieldPro.UI
{
    public class ProtectionSectionCard : Control
    {
        private string _sectionTitle = "";
        private string _sectionDescription = "";
        private bool _isCollapsible = false;
        private bool _isExpanded = true;
        private string _summaryText = "";
        private string _customExpandText = null;
        private string _customCollapseText = null;
        private readonly List<ProtectionSettingRow> _rows = new List<ProtectionSettingRow>();
        private readonly AppButton _accordionBtn;

        public event EventHandler ExpandedChanged;

        public string SectionTitle
        {
            get { return _sectionTitle; }
            set { _sectionTitle = value ?? ""; Invalidate(); }
        }

        public string SectionDescription
        {
            get { return _sectionDescription; }
            set { _sectionDescription = value ?? ""; Invalidate(); }
        }

        public string SummaryText
        {
            get { return _summaryText; }
            set { _summaryText = value ?? ""; UpdateCardGeometry(); Invalidate(); }
        }

        public string CustomExpandText
        {
            get { return _customExpandText; }
            set { _customExpandText = value; UpdateAccordionButtonText(); }
        }

        public string CustomCollapseText
        {
            get { return _customCollapseText; }
            set { _customCollapseText = value; UpdateAccordionButtonText(); }
        }

        public bool IsCollapsible
        {
            get { return _isCollapsible; }
            set
            {
                _isCollapsible = value;
                if (_accordionBtn != null)
                {
                    _accordionBtn.Visible = _isCollapsible;
                }
                UpdateAccordionButtonText();
                UpdateCardGeometry();
            }
        }

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    UpdateAccordionButtonText();
                    UpdateCardGeometry();
                    if (ExpandedChanged != null) ExpandedChanged(this, EventArgs.Empty);
                }
            }
        }

        public AppButton AccordionButton
        {
            get { return _accordionBtn; }
        }

        public int HeaderHeight
        {
            get
            {
                bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
                bool hasSub = _isExpanded ? !string.IsNullOrEmpty(_sectionDescription) : (!string.IsNullOrEmpty(_summaryText) || !string.IsNullOrEmpty(_sectionDescription));
                return DpiManager.ScaleInt(!hasSub ? 38 : (isRtl ? 54 : 48));
            }
        }

        public IReadOnlyList<ProtectionSettingRow> Rows
        {
            get { return _rows.AsReadOnly(); }
        }

        public ProtectionSectionCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = false;

            _accordionBtn = new AppButton();
            _accordionBtn.SizeVariant = ButtonSize.Small;
            _accordionBtn.Variant = ButtonVariant.Ghost;
            _accordionBtn.Visible = false;
            _accordionBtn.Click += delegate { IsExpanded = !IsExpanded; };
            Controls.Add(_accordionBtn);

            IThemePalette pal = ThemeManager.ActivePalette;
            if (pal != null) BackColor = pal.Surface;

            ThemeManager.ThemeChanged += OnThemeChanged;
            UpdateCardGeometry();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            if (pal != null) BackColor = pal.Surface;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }

        public void AddRow(ProtectionSettingRow row)
        {
            if (row == null) return;
            _rows.Add(row);
            Controls.Add(row);
            UpdateAccordionButtonText();
            UpdateCardGeometry();
        }

        public void UpdateAccordionButtonText()
        {
            if (!_isCollapsible || _accordionBtn == null) return;

            if (_isExpanded)
            {
                _accordionBtn.Text = !string.IsNullOrEmpty(_customCollapseText) ? _customCollapseText : Loc.Get("AccordionCollapse");
            }
            else
            {
                _accordionBtn.Text = !string.IsNullOrEmpty(_customExpandText) ? _customExpandText : Loc.Get("AccordionExpand");
            }
            _accordionBtn.PerformLayout();
            PerformHeaderLayout();
        }

        public void UpdateCardGeometry()
        {
            int hHeight = HeaderHeight;
            int currentY = hHeight;

            bool rowsVisible = !_isCollapsible || _isExpanded;

            for (int i = 0; i < _rows.Count; i++)
            {
                ProtectionSettingRow row = _rows[i];
                row.Visible = rowsVisible;
                if (rowsVisible)
                {
                    row.Width = Width;
                    row.UpdateRowHeight();
                    row.Location = new Point(0, currentY);
                    // Don't draw bottom divider on the last row
                    row.ShowBottomSeparator = (i < _rows.Count - 1);
                    currentY += row.Height;
                }
            }

            Height = rowsVisible ? currentY : hHeight;
            PerformHeaderLayout();
            Invalidate();
        }

        private void PerformHeaderLayout()
        {
            if (_accordionBtn == null || !_isCollapsible) return;

            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int padX = DpiManager.ScaleInt(SpacingTokens.Space16);
            int btnH = DpiManager.ScaleInt(26);
            int btnY = (HeaderHeight - btnH) / 2;

            int minAccordionW = DpiManager.ScaleInt(92);
            int calcW = AppButton.CalculateWidth(_accordionBtn.Text, ButtonSize.Small, isRtl);
            int btnW = Math.Max(minAccordionW, calcW);
            _accordionBtn.Size = new Size(btnW, btnH);

            int btnX = isRtl ? padX : (Width - padX - btnW);
            _accordionBtn.Location = new Point(btnX, btnY);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isCollapsible && e.Y < HeaderHeight)
            {
                Cursor = Cursors.Hand;
            }
            else
            {
                Cursor = Cursors.Default;
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (_isCollapsible && e.Y < HeaderHeight && e.Button == MouseButtons.Left)
            {
                IsExpanded = !IsExpanded;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateCardGeometry();
        }

        protected override void OnRightToLeftChanged(EventArgs e)
        {
            base.OnRightToLeftChanged(e);
            UpdateAccordionButtonText();
            UpdateCardGeometry();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;

            int radius = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Container Background
            using (SolidBrush bgBrush = new SolidBrush(pal.Surface))
            {
                UiDrawingUtils.FillRoundedRectangle(g, bgBrush, rect, radius);
            }

            // Container Border
            using (Pen borderPen = new Pen(pal.BorderSubtle, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, borderPen, rect, radius);
            }

            // Header separator line (if expanded and has rows)
            int hHeight = HeaderHeight;
            bool rowsVisible = (!_isCollapsible || _isExpanded) && _rows.Count > 0;
            if (rowsVisible)
            {
                using (Pen headerSepPen = new Pen(pal.BorderSubtle, 1f))
                {
                    int sepY = hHeight - 1;
                    g.DrawLine(headerSepPen, 0, sepY, Width, sepY);
                }
            }

            // Header Texts
            int padX = DpiManager.ScaleInt(SpacingTokens.Space16);
            int textStartX;
            int textAvailableWidth;

            if (isRtl)
            {
                int btnRight = (_accordionBtn != null && _isCollapsible) ? _accordionBtn.Right : padX;
                int trailingBoundary = btnRight + DpiManager.ScaleInt(SpacingTokens.Space12);
                textStartX = trailingBoundary;
                textAvailableWidth = (Width - padX) - trailingBoundary;
            }
            else
            {
                int btnLeft = (_accordionBtn != null && _isCollapsible) ? _accordionBtn.Left : (Width - padX);
                int trailingBoundary = btnLeft - DpiManager.ScaleInt(SpacingTokens.Space12);
                textStartX = padX;
                textAvailableWidth = trailingBoundary - textStartX;
            }

            if (textAvailableWidth > 20)
            {
                Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
                Font subFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

                // Eliminate VerticalCenter to prevent clipping Arabic descenders and bottom dots
                TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping;
                if (isRtl)
                {
                    flags |= TextFormatFlags.RightToLeft | TextFormatFlags.Right;
                }
                else
                {
                    flags |= TextFormatFlags.Left;
                }

                string subText = _isExpanded ? _sectionDescription : (!string.IsNullOrEmpty(_summaryText) ? _summaryText : _sectionDescription);
                bool hasDesc = !string.IsNullOrEmpty(subText);
                int titleH = DpiManager.ScaleInt(isRtl ? 22 : 20);
                int subH = DpiManager.ScaleInt(isRtl ? 22 : 18);
                int gapY = DpiManager.ScaleInt(1);

                int totalHeaderH = hasDesc ? (titleH + gapY + subH) : titleH;
                int titleY = Math.Max(DpiManager.ScaleInt(4), (hHeight - totalHeaderH) / 2);

                Rectangle titleRect = new Rectangle(textStartX, titleY, textAvailableWidth, titleH);
                TextRenderer.DrawText(g, _sectionTitle, titleFont, titleRect, pal.TextPrimary, flags);

                if (hasDesc)
                {
                    int subY = titleY + titleH + gapY;
                    Rectangle subRect = new Rectangle(textStartX, subY, textAvailableWidth, subH);
                    TextRenderer.DrawText(g, subText, subFont, subRect, pal.TextSecondary, flags);
                }
            }
        }
    }
}
