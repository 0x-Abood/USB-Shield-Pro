// ═══════════════════════════════════════════════════════════════════════════
// ActivityEmptyState — Dedicated Restrained Empty State for Activity Page
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
    public class ActivityEmptyState : Control
    {
        private bool _isFiltered = false;
        private string _activeQuery = "";
        private bool _explicitVisible = false;

        private readonly AppButton _clearFiltersBtn;

        public event EventHandler ClearFiltersClicked;

        public bool IsFiltered { get { return _isFiltered; } }
        public AppButton ClearFiltersButton { get { return _clearFiltersBtn; } }
        public bool IsClearFiltersButtonVisible { get { return _isFiltered; } }
        public bool IsActive { get { return _explicitVisible; } }

        public new bool Visible
        {
            get { return _explicitVisible; }
            set
            {
                _explicitVisible = value;
                base.Visible = value;
            }
        }

        public ActivityEmptyState()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = false;
            BackColor = Color.Transparent;
            Height = DpiManager.ScaleInt(240);

            _clearFiltersBtn = new AppButton();
            _clearFiltersBtn.SizeVariant = ButtonSize.Small;
            _clearFiltersBtn.Variant = ButtonVariant.Secondary;
            _clearFiltersBtn.Text = Loc.Get("ClearFiltersBtn");
            _clearFiltersBtn.Visible = false;
            _clearFiltersBtn.Click += delegate
            {
                if (ClearFiltersClicked != null) ClearFiltersClicked(this, EventArgs.Empty);
            };

            Controls.Add(_clearFiltersBtn);

            UpdateState();
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public void SetSearchMode(bool isFilterActive, string query)
        {
            _isFiltered = isFilterActive;
            _activeQuery = query ?? "";
            UpdateState();
            Invalidate();
        }

        private void UpdateState()
        {
            bool isRtl = Loc.IsRtl;
            _clearFiltersBtn.Visible = _isFiltered;
            _clearFiltersBtn.Text = Loc.Get("ClearFiltersBtn");

            Font btnFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            int textW = TextRenderer.MeasureText(_clearFiltersBtn.Text ?? "", btnFont).Width;
            _clearFiltersBtn.Width = Math.Max(DpiManager.ScaleInt(130), textW + DpiManager.ScaleInt(28));

            LayoutControls();
        }

        private void LayoutControls()
        {
            if (_clearFiltersBtn == null) return;
            int cx = Width / 2;
            int btnY = DpiManager.ScaleInt(165);
            _clearFiltersBtn.Location = new Point(cx - _clearFiltersBtn.Width / 2, btnY);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutControls();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                UpdateState();
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

            int cx = Width / 2;

            // 1. Vector Graphic Illustration
            int iconBoxSize = DpiManager.ScaleInt(48);
            int iconBoxY = DpiManager.ScaleInt(24);
            Rectangle iconRect = new Rectangle(cx - iconBoxSize / 2, iconBoxY, iconBoxSize, iconBoxSize);

            Color iconStroke = pal.TextMuted;
            if (_isFiltered)
            {
                UiDrawingUtils.DrawSearchIcon(g, iconStroke, iconRect);
            }
            else
            {
                DrawActivityAuditIcon(g, iconStroke, iconRect);
            }

            // 2. Title
            string titleText = _isFiltered ? Loc.Get("NoMatchingActivityTitle") : Loc.Get("NoActivityRecordedTitle");
            Font titleFont = DpiManager.GetFont(TypographyRole.Subtitle, isRtl);
            int titleY = iconBoxY + iconBoxSize + DpiManager.ScaleInt(14);
            int titleW = Math.Min(Width - 40, DpiManager.ScaleInt(460));
            int titleH = DpiManager.ScaleInt(isRtl ? 26 : 24);
            Rectangle titleRect = new Rectangle(cx - titleW / 2, titleY, titleW, titleH);

            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping;
            if (isRtl)
            {
                flags |= TextFormatFlags.RightToLeft;
            }
            TextRenderer.DrawText(g, titleText, titleFont, titleRect, pal.TextPrimary, flags);

            // 3. Subtitle
            string subText = _isFiltered ? Loc.Get("NoMatchingActivityDesc") : Loc.Get("NoActivityRecordedDesc");
            Font subFont = DpiManager.GetFont(TypographyRole.Body, isRtl);
            int subY = titleY + titleH + DpiManager.ScaleInt(4);
            int subW = Math.Min(Width - 40, DpiManager.ScaleInt(460));
            Size measuredSub = TextRenderer.MeasureText(g, subText, subFont, new Size(subW, int.MaxValue), flags | TextFormatFlags.WordBreak);
            int subH = Math.Max(measuredSub.Height + DpiManager.ScaleInt(isRtl ? 10 : 6), DpiManager.ScaleInt(isRtl ? 48 : 38));
            Rectangle subRect = new Rectangle(cx - subW / 2, subY, subW, subH);

            TextRenderer.DrawText(g, subText, subFont, subRect, pal.TextSecondary, flags | TextFormatFlags.WordBreak);
        }

        private void DrawActivityAuditIcon(Graphics g, Color color, Rectangle bounds)
        {
            int size = Math.Min(bounds.Width, bounds.Height);
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;

            int w = Math.Max(12, (int)(size * 0.65f));
            int h = Math.Max(14, (int)(size * 0.78f));
            int left = cx - w / 2;
            int top = cy - h / 2;

            using (Pen pen = new Pen(color, 1.4f))
            {
                pen.LineJoin = LineJoin.Round;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                // Clipboard / board outline
                UiDrawingUtils.DrawRoundedRectangle(g, pen, new Rectangle(left, top, w, h), 2);

                // Top clip
                int clipW = Math.Max(6, w / 2);
                int clipH = Math.Max(3, h / 7);
                UiDrawingUtils.DrawRoundedRectangle(g, pen, new Rectangle(cx - clipW / 2, top - clipH / 2, clipW, clipH), 1);

                // Document lines / checklist
                int lineLeft = left + 4;
                int lineRight = left + w - 4;
                int startY = top + clipH + 4;
                int gapY = Math.Max(3, (h - clipH - 8) / 4);

                for (int i = 0; i < 3; i++)
                {
                    int ly = startY + i * gapY;
                    if (ly < top + h - 3)
                    {
                        g.DrawLine(pen, lineLeft, ly, lineRight, ly);
                    }
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
