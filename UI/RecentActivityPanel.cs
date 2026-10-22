// ═══════════════════════════════════════════════════════════════════════════
// RecentActivityPanel — Real Production Activity List for v2.1 Dashboard
// USB Shield Pro v2.1
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using UsbShieldPro.Models;

namespace UsbShieldPro.UI
{
    internal class RecentActivityPanel : Control
    {
        private readonly List<ActivityRecord> _records = new List<ActivityRecord>();
        private readonly AppButton _viewAllBtn;
        private int _hoveredRow = -1;

        public event EventHandler ViewAllClicked;

        public int RecordCount { get { return _records.Count; } }

        public RecentActivityPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            TabStop = false;

            _viewAllBtn = new AppButton();
            _viewAllBtn.SizeVariant = ButtonSize.Small;
            _viewAllBtn.Variant = ButtonVariant.Ghost;
            _viewAllBtn.Text = "View All →";
            _viewAllBtn.Click += delegate { if (ViewAllClicked != null) ViewAllClicked(this, EventArgs.Empty); };
            Controls.Add(_viewAllBtn);

            Height = DpiManager.ScaleInt(250);
            RightToLeft = Loc.RtlValue;

            ThemeManager.ThemeChanged += OnThemeChanged;
            PerformLayout();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                if (_viewAllBtn != null)
                {
                    _viewAllBtn.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        public void SetRecords(IEnumerable<ActivityRecord> records, bool isRtl)
        {
            _records.Clear();
            if (records != null)
            {
                int count = 0;
                foreach (var r in records)
                {
                    _records.Add(r);
                    count++;
                    if (count >= 5) break; // Display top 4-5 events
                }
            }

            RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;
            _viewAllBtn.Text = isRtl ? "عرض الكل" : "View All";
            PerformLayout();
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (_viewAllBtn == null) return;

            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int pad = DpiManager.ScaleInt(SpacingTokens.Space14);
            int btnW = Math.Max(DpiManager.ScaleInt(80), AppButton.CalculateWidth(_viewAllBtn.Text, ButtonSize.Small, isRtl));
            int btnH = DpiManager.ScaleInt(26);
            int headerH = DpiManager.ScaleInt(38);
            int btnY = (headerH - btnH) / 2;

            if (isRtl)
            {
                _viewAllBtn.Location = new Point(pad, btnY);
            }
            else
            {
                _viewAllBtn.Location = new Point(Width - pad - btnW, btnY);
            }
            _viewAllBtn.Size = new Size(btnW, btnH);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int headerH = DpiManager.ScaleInt(38);
            if (e.Y < headerH || _records.Count == 0)
            {
                if (_hoveredRow != -1) { _hoveredRow = -1; Invalidate(); }
                return;
            }

            int contentH = Height - headerH;
            int rowH = contentH / _records.Count;
            int row = (e.Y - headerH) / Math.Max(1, rowH);
            if (row >= _records.Count) row = -1;

            if (_hoveredRow != row)
            {
                _hoveredRow = row;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredRow != -1)
            {
                _hoveredRow = -1;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);

            // Container Surface & Border
            using (SolidBrush bg = new SolidBrush(pal.Surface))
            {
                UiDrawingUtils.FillRoundedRectangle(g, bg, rect, rad);
            }
            using (Pen borderPen = new Pen(pal.BorderSubtle, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, borderPen, rect, rad);
            }

            int pad = DpiManager.ScaleInt(SpacingTokens.Space14);
            int headerH = DpiManager.ScaleInt(38);

            // Header Section
            Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            string headerText = Loc.Get("RecentActivityTitle");
            if (string.IsNullOrEmpty(headerText) || headerText == "RecentActivityTitle")
            {
                headerText = isRtl ? "النشاط الأخير" : "Recent Activity";
            }

            int titleW = Width - DpiManager.ScaleInt(120);
            Rectangle headerTitleRect = isRtl
                ? new Rectangle(Width - pad - titleW, 0, titleW, headerH)
                : new Rectangle(pad, 0, titleW, headerH);

            TextFormatFlags headerFlags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
            headerFlags |= isRtl ? (TextFormatFlags.Right | TextFormatFlags.RightToLeft) : TextFormatFlags.Left;
            TextRenderer.DrawText(g, headerText, titleFont, headerTitleRect, pal.TextPrimary, headerFlags);

            // Header Separator Line
            using (Pen sep = new Pen(pal.BorderSubtle, 1f))
            {
                g.DrawLine(sep, 0, headerH, Width, headerH);
            }

            // Body
            if (_records.Count == 0)
            {
                // Empty State
                int emptyY = headerH + (Height - headerH) / 2 - DpiManager.ScaleInt(24);
                Font emptyTitleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
                Font emptySubFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

                string emptyTitle = Loc.Get("NoActivityTitle");
                if (string.IsNullOrEmpty(emptyTitle) || emptyTitle == "NoActivityTitle")
                    emptyTitle = isRtl ? "لا توجد أنشطة مسجلة بعد" : "No recent activity";

                string emptySub = Loc.Get("NoActivityDesc");
                if (string.IsNullOrEmpty(emptySub) || emptySub == "NoActivityDesc")
                    emptySub = isRtl ? "ستظهر هنا أحداث النظام وسجلات اتصال الأجهزة." : "System events and device connection logs will appear here.";

                int etH = emptyTitleFont.Height + DpiManager.ScaleInt(isRtl ? 6 : 4);
                int esH = emptySubFont.Height + DpiManager.ScaleInt(isRtl ? 6 : 4);
                Rectangle etRect = new Rectangle(pad, emptyY, Width - pad * 2, etH);
                Rectangle esRect = new Rectangle(pad, emptyY + etH + DpiManager.ScaleInt(4), Width - pad * 2, esH);

                TextRenderer.DrawText(g, emptyTitle, emptyTitleFont, etRect, pal.TextSecondary,
                    TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);
                TextRenderer.DrawText(g, emptySub, emptySubFont, esRect, pal.TextMuted,
                    TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);
                return;
            }

            // Render existing activity rows
            int contentH = Height - headerH;
            int count = _records.Count;
            int rowH = contentH / count;

            Font rowTitleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            Font rowSubFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            Font timeFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            for (int i = 0; i < count; i++)
            {
                var r = _records[i];
                int rowY = headerH + i * rowH;
                Rectangle rowRect = new Rectangle(1, rowY, Width - 2, rowH);

                if (i == _hoveredRow)
                {
                    using (SolidBrush hb = new SolidBrush(pal.SurfaceHover))
                    {
                        g.FillRectangle(hb, rowRect);
                    }
                }

                // Row separator
                if (i > 0)
                {
                    using (Pen sp = new Pen(pal.BorderSubtle, 1f))
                    {
                        g.DrawLine(sp, pad, rowY, Width - pad, rowY);
                    }
                }

                // Status indicator color
                Color badgeColor = r.BadgeColor;
                if (badgeColor.IsEmpty || badgeColor == Color.Transparent)
                {
                    badgeColor = pal.Info;
                }

                int dotSize = DpiManager.ScaleInt(8);
                int dotY = rowY + (rowH - dotSize) / 2;

                string timeText = r.Timestamp.ToString("HH:mm:ss");
                Size timeSize = TextRenderer.MeasureText(g, timeText, timeFont);
                int timeW = Math.Max(timeSize.Width, DpiManager.ScaleInt(54));

                string desc;
                if (!string.IsNullOrEmpty(r.Context) && !string.IsNullOrEmpty(r.ResultBadge))
                {
                    desc = r.Context + " • " + r.ResultBadge;
                }
                else
                {
                    desc = !string.IsNullOrEmpty(r.Context) ? r.Context : (r.ResultBadge ?? "");
                }

                if (isRtl)
                {
                    // In RTL: Time on far left (logical trailing edge), dot on far right (logical leading edge), text in between
                    int dotX = Width - pad - dotSize;
                    using (SolidBrush db = new SolidBrush(badgeColor))
                    {
                        g.FillEllipse(db, dotX, dotY, dotSize, dotSize);
                    }

                    Rectangle timeRect = new Rectangle(pad, rowY, timeW, rowH);
                    TextRenderer.DrawText(g, timeText, timeFont, timeRect, pal.TextMuted,
                        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);

                    int textRight = dotX - DpiManager.ScaleInt(SpacingTokens.Space10);
                    int textLeft = pad + timeW + DpiManager.ScaleInt(SpacingTokens.Space12);
                    int textW = Math.Max(20, textRight - textLeft);

                    int blockH = rowTitleFont.Height + rowSubFont.Height + DpiManager.ScaleInt(2);
                    int tY = rowY + Math.Max(DpiManager.ScaleInt(2), (rowH - blockH) / 2);
                    Rectangle tr = new Rectangle(textLeft, tY, textW, rowTitleFont.Height + DpiManager.ScaleInt(4));
                    TextRenderer.DrawText(g, r.Title ?? "", rowTitleFont, tr, pal.TextPrimary,
                        TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                    int sY = tY + rowTitleFont.Height + DpiManager.ScaleInt(2);
                    Rectangle sr = new Rectangle(textLeft, sY, textW, rowSubFont.Height + DpiManager.ScaleInt(4));
                    TextRenderer.DrawText(g, desc ?? "", rowSubFont, sr, pal.TextMuted,
                        TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
                else
                {
                    // In LTR: Dot on far left (logical leading edge), time on far right (logical trailing edge), text in between
                    int dotX = pad;
                    using (SolidBrush db = new SolidBrush(badgeColor))
                    {
                        g.FillEllipse(db, dotX, dotY, dotSize, dotSize);
                    }

                    int timeX = Width - pad - timeW;
                    Rectangle timeRect = new Rectangle(timeX, rowY, timeW, rowH);
                    TextRenderer.DrawText(g, timeText, timeFont, timeRect, pal.TextMuted,
                        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPrefix);

                    int textLeft = dotX + dotSize + DpiManager.ScaleInt(SpacingTokens.Space10);
                    int textRight = timeX - DpiManager.ScaleInt(SpacingTokens.Space12);
                    int textW = Math.Max(20, textRight - textLeft);

                    int blockH = rowTitleFont.Height + rowSubFont.Height + DpiManager.ScaleInt(2);
                    int tY = rowY + Math.Max(DpiManager.ScaleInt(2), (rowH - blockH) / 2);
                    Rectangle tr = new Rectangle(textLeft, tY, textW, rowTitleFont.Height + DpiManager.ScaleInt(4));
                    TextRenderer.DrawText(g, r.Title ?? "", rowTitleFont, tr, pal.TextPrimary,
                        TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                    int sY = tY + rowTitleFont.Height + DpiManager.ScaleInt(2);
                    Rectangle sr = new Rectangle(textLeft, sY, textW, rowSubFont.Height + DpiManager.ScaleInt(4));
                    TextRenderer.DrawText(g, desc ?? "", rowSubFont, sr, pal.TextMuted,
                        TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
            }
        }
    }
}
