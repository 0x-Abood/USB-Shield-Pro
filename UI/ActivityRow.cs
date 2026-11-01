// ═══════════════════════════════════════════════════════════════════════════
// ActivityRow — High-Density Timeline Row for Activity & Audit Workspace
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
    public class ActivityRow : Control
    {
        private readonly ActivityRecord _record;
        private bool _isSelected;
        private bool _isHovered;

        private readonly StatusBadge _badge;
        private readonly AppButton _detailsBtn;

        public event EventHandler RowSelected;
        public event EventHandler DetailsRequested;

        public ActivityRecord Record { get { return _record; } }

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

        public StatusBadge Badge { get { return _badge; } }
        public AppButton DetailsButton { get { return _detailsBtn; } }

        public ActivityRow(ActivityRecord record)
        {
            _record = record ?? new ActivityRecord(DateTime.Now, "", "", ActivityCategory.Security);

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = true;
            Cursor = Cursors.Hand;
            Height = DpiManager.ScaleInt(54);

            // Sub-control: Semantic Status Badge
            _badge = new StatusBadge();
            _badge.TabStop = false;
            _badge.Variant = DetermineBadgeVariant(_record);
            _badge.Text = !string.IsNullOrEmpty(_record.ResultBadge) ? _record.ResultBadge : GetCategoryFallbackBadge(_record.Category);
            _badge.Click += OnSubControlClick;

            // Sub-control: View Details Button
            _detailsBtn = new AppButton();
            _detailsBtn.SizeVariant = ButtonSize.Small;
            _detailsBtn.Variant = ButtonVariant.Ghost;
            _detailsBtn.Text = Loc.Get("ViewDetails");
            _detailsBtn.Click += delegate
            {
                TriggerRowSelect();
                if (DetailsRequested != null) DetailsRequested(this, EventArgs.Empty);
            };

            Controls.Add(_badge);
            Controls.Add(_detailsBtn);

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

        public static StatusBadgeVariant DetermineBadgeVariant(ActivityRecord rec)
        {
            if (rec == null) return StatusBadgeVariant.Neutral;

            if (rec.Category == ActivityCategory.Errors)
                return StatusBadgeVariant.Danger;

            string b = (rec.ResultBadge ?? "").Trim().ToLowerInvariant();
            string t = (rec.Title ?? "").Trim().ToLowerInvariant();

            // Error / Failure
            if (b.Contains("fail") || b.Contains("error") || b.Contains("فشل") || b.Contains("خطأ") ||
                t.Contains("fail") || t.Contains("فشل"))
            {
                return StatusBadgeVariant.Danger;
            }

            // Warning / Auto-Eject / Blocked
            if (b.Contains("auto eject") || b.Contains("blocked") || b.Contains("حظر") || b.Contains("intercept") ||
                t.Contains("auto eject") || t.Contains("blocked") || t.Contains("حظر"))
            {
                return StatusBadgeVariant.Warning;
            }

            // Success / Allowed / Active / Protected
            if (b.Contains("permit") || b.Contains("allow") || b.Contains("verif") || b.Contains("trust") ||
                b.Contains("active") || b.Contains("protect") || b.Contains("مصرح") || b.Contains("نشط") ||
                b.Contains("محمي") || b.Contains("موثوق") || b.Contains("نجاح") ||
                t.Contains("permit") || t.Contains("allowed") || t.Contains("trusted") || t.Contains("activated") ||
                t.Contains("مصرح") || t.Contains("محمي"))
            {
                return StatusBadgeVariant.Success;
            }

            // Info / Policy / System
            if (rec.Category == ActivityCategory.Protection || rec.Category == ActivityCategory.PolicyChanges ||
                b.Contains("policy") || b.Contains("سياسة") || b.Contains("pnp") || b.Contains("event"))
            {
                return StatusBadgeVariant.Info;
            }

            return StatusBadgeVariant.Neutral;
        }

        public static Color GetSemanticColor(ActivityRecord rec, IThemePalette pal)
        {
            StatusBadgeVariant variant = DetermineBadgeVariant(rec);
            switch (variant)
            {
                case StatusBadgeVariant.Success:
                    return pal.Success;
                case StatusBadgeVariant.Warning:
                    return pal.Warning;
                case StatusBadgeVariant.Danger:
                    return pal.Danger;
                case StatusBadgeVariant.Info:
                    return pal.Info;
                default:
                    return pal.TextMuted;
            }
        }

        private static string GetCategoryFallbackBadge(ActivityCategory cat)
        {
            switch (cat)
            {
                case ActivityCategory.Devices:
                case ActivityCategory.DeviceEvents:
                    return Loc.Get("ActCatDevices");
                case ActivityCategory.Protection:
                case ActivityCategory.PolicyChanges:
                    return Loc.Get("ActCatProtection");
                case ActivityCategory.Security:
                case ActivityCategory.TrustedDevices:
                    return Loc.Get("ActCatSecurity");
                case ActivityCategory.Errors:
                    return Loc.Get("ActCatErrors");
                default:
                    return Loc.Get("BadgeEvent");
            }
        }

        public void UpdateBadgesAndText()
        {
            bool isRtl = Loc.IsRtl;

            _badge.RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;
            _badge.Variant = DetermineBadgeVariant(_record);
            _badge.Text = !string.IsNullOrEmpty(_record.ResultBadge) ? _record.ResultBadge : GetCategoryFallbackBadge(_record.Category);

            _detailsBtn.Text = Loc.Get("ViewDetails");
            Font btnFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            int tw = TextRenderer.MeasureText(_detailsBtn.Text ?? "", btnFont).Width;
            _detailsBtn.Width = Math.Max(DpiManager.ScaleInt(70), tw + DpiManager.ScaleInt(20));

            PerformChildLayout();
            Invalidate();
        }

        private void PerformChildLayout()
        {
            if (_detailsBtn == null || _badge == null) return;

            bool isRtl = Loc.IsRtl;
            int pad = DpiManager.ScaleInt(16);
            int gap = DpiManager.ScaleInt(8);
            int btnH = DpiManager.ScaleInt(26);
            int btnY = (Height - btnH) / 2;

            _detailsBtn.Height = btnH;

            if (isRtl)
            {
                int curX = pad;
                _detailsBtn.Location = new Point(curX, btnY);
                curX += _detailsBtn.Width + gap;

                int bY = (Height - _badge.Height) / 2;
                _badge.Location = new Point(curX, bY);
                curX += _badge.Width + gap;
            }
            else
            {
                int curX = Width - pad;
                curX -= _detailsBtn.Width;
                _detailsBtn.Location = new Point(curX, btnY);
                curX -= gap;

                curX -= _badge.Width;
                int bY = (Height - _badge.Height) / 2;
                _badge.Location = new Point(curX, bY);
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

            // 1. Surface Background
            Color bgColor = _isSelected ? pal.SurfaceSelected : (_isHovered ? pal.SurfaceHover : pal.Surface);
            using (SolidBrush bgBrush = new SolidBrush(bgColor))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            // 2. Selection Indicator Bar (3px on logical leading edge)
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

            int pad = DpiManager.ScaleInt(16);
            int dotSize = DpiManager.ScaleInt(8);
            Color semanticColor = GetSemanticColor(_record, pal);

            // 5. Time String & Semantic Status Indicator
            string timeStr = _record.Timestamp.ToString("HH:mm:ss");
            Font timeFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            int timeW = Math.Max(DpiManager.ScaleInt(54), TextRenderer.MeasureText(timeStr, timeFont).Width);
            int timeH = DpiManager.ScaleInt(isRtl ? 20 : 18);
            int timeY = (Height - timeH) / 2;
            int dotY = (Height - dotSize) / 2;

            int textLeft, textRight;
            int gap = DpiManager.ScaleInt(10);

            if (isRtl)
            {
                // Leading edge is RIGHT
                int dotX = Width - pad - dotSize;
                using (SolidBrush dotBrush = new SolidBrush(semanticColor))
                {
                    g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
                }

                int timeX = dotX - gap - timeW;
                Rectangle timeRect = new Rectangle(timeX, timeY, timeW, timeH);
                TextRenderer.DrawText(g, timeStr, timeFont, timeRect, pal.TextMuted,
                    TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                textRight = timeX - gap;

                int trailingLimit = pad;
                if (_detailsBtn != null) trailingLimit = Math.Max(trailingLimit, _detailsBtn.Right + gap);
                if (_badge != null) trailingLimit = Math.Max(trailingLimit, _badge.Right + gap);
                textLeft = trailingLimit;
            }
            else
            {
                // Leading edge is LEFT
                int dotX = pad;
                using (SolidBrush dotBrush = new SolidBrush(semanticColor))
                {
                    g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
                }

                int timeX = dotX + dotSize + gap;
                Rectangle timeRect = new Rectangle(timeX, timeY, timeW, timeH);
                TextRenderer.DrawText(g, timeStr, timeFont, timeRect, pal.TextMuted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                textLeft = timeX + timeW + gap;

                int trailingLimit = Width - pad;
                if (_detailsBtn != null) trailingLimit = Math.Min(trailingLimit, _detailsBtn.Left - gap);
                if (_badge != null) trailingLimit = Math.Min(trailingLimit, _badge.Left - gap);
                textRight = trailingLimit;
            }

            int textW = Math.Max(50, textRight - textLeft);

            // 6. Title and Context/Subtitle
            Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            Font subFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            int titleH = DpiManager.ScaleInt(isRtl ? 22 : 20);
            int subH = DpiManager.ScaleInt(isRtl ? 20 : 16);
            int textBlockH = titleH + subH;
            int textTop = (Height - textBlockH) / 2;

            Rectangle titleRect = new Rectangle(textLeft, textTop, textW, titleH);
            Rectangle subRect = new Rectangle(textLeft, textTop + titleH, textW, subH);

            TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;
            if (isRtl)
            {
                flags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
            }
            else
            {
                flags |= TextFormatFlags.Left;
            }

            TextRenderer.DrawText(g, _record.Title ?? "", titleFont, titleRect, pal.TextPrimary, flags);

            string subtitle = !string.IsNullOrEmpty(_record.Context) ? _record.Context : GetCategoryFallbackBadge(_record.Category);
            TextRenderer.DrawText(g, subtitle, subFont, subRect, pal.TextSecondary, flags);
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
