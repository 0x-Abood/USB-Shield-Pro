// ═══════════════════════════════════════════════════════════════════════════
// ProtectionOverviewPanel — Real Protection Feature Summary for v2.1 Dashboard
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
    internal class ProtectionOverviewPanel : Control
    {
        private class FeatureItem
        {
            public string TitleEn { get; set; }
            public string TitleAr { get; set; }
            public string SubtitleEn { get; set; }
            public string SubtitleAr { get; set; }
            public string BadgeEn { get; set; }
            public string BadgeAr { get; set; }
            public StatusBadgeVariant Variant { get; set; }
        }

        private readonly List<FeatureItem> _items = new List<FeatureItem>();
        private readonly AppButton _configureBtn;
        private int _hoveredRow = -1;

        public event EventHandler ConfigureClicked;

        public int FeatureCount { get { return _items.Count; } }
        public string GetFeatureBadgeEn(int index) { return (index >= 0 && index < _items.Count) ? _items[index].BadgeEn : ""; }

        public ProtectionOverviewPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            TabStop = false;
            Cursor = Cursors.Hand;

            _configureBtn = new AppButton();
            _configureBtn.SizeVariant = ButtonSize.Small;
            _configureBtn.Variant = ButtonVariant.Ghost;
            _configureBtn.Text = "Configure →";
            _configureBtn.Click += delegate { if (ConfigureClicked != null) ConfigureClicked(this, EventArgs.Empty); };
            Controls.Add(_configureBtn);

            Height = DpiManager.ScaleInt(250);

            ThemeManager.ThemeChanged += OnThemeChanged;
            InitializeEmptyFeatures();
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
                if (_configureBtn != null)
                {
                    _configureBtn.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        private void InitializeEmptyFeatures()
        {
            _items.Clear();
            _items.Add(new FeatureItem { TitleEn = "Removable Storage", TitleAr = "وحدات التخزين", SubtitleEn = "Flash drives & external disks", SubtitleAr = "أقراص الفلاش والوسائط المتنقلة", BadgeEn = "ALLOWED", BadgeAr = "مسموح", Variant = StatusBadgeVariant.Neutral });
            _items.Add(new FeatureItem { TitleEn = "Read-Only Mode", TitleAr = "وضع القراءة فقط", SubtitleEn = "Write-protection enforcement", SubtitleAr = "منع الكتابة على الأجهزة", BadgeEn = "OFF", BadgeAr = "معطل", Variant = StatusBadgeVariant.Neutral });
            _items.Add(new FeatureItem { TitleEn = "Flash Storage Sentinel", TitleAr = "حارس ذاكرة الفلاش", SubtitleEn = "Reactive auto-eject monitor", SubtitleAr = "المراقبة الفورية للأجهزة", BadgeEn = "OFF", BadgeAr = "معطل", Variant = StatusBadgeVariant.Neutral });
            _items.Add(new FeatureItem { TitleEn = "AutoPlay Guard", TitleAr = "حارس التشغيل التلقائي", SubtitleEn = "Registry lockdown", SubtitleAr = "قفل التشغيل التلقائي", BadgeEn = "OFF", BadgeAr = "معطل", Variant = StatusBadgeVariant.Neutral });
            _items.Add(new FeatureItem { TitleEn = "Portable Devices (WPD)", TitleAr = "الهواتف والأجهزة المحمولة", SubtitleEn = "MTP & smart device lockdown", SubtitleAr = "حظر نقل الوسائط والهواتف", BadgeEn = "ALLOWED", BadgeAr = "مسموح", Variant = StatusBadgeVariant.Neutral });
            _items.Add(new FeatureItem { TitleEn = "Whitelist Mode", TitleAr = "نمط القائمة البيضاء", SubtitleEn = "Hardware ID signature check", SubtitleAr = "التحقق من قائمة الأجهزة الموثوقة", BadgeEn = "OFF", BadgeAr = "معطل", Variant = StatusBadgeVariant.Neutral });
        }

        public void UpdatePolicy(ProtectionPolicy policy, bool isRtl)
        {
            _items.Clear();
            ProtectionPolicy p = policy ?? new ProtectionPolicy();

            bool blockRemovable = (p.BlockRemovableDisks || p.BlockAllRemovable);
            _items.Add(new FeatureItem
            {
                TitleEn = "Removable Storage",
                TitleAr = "وحدات التخزين القابلة للإزالة",
                SubtitleEn = blockRemovable ? "Unauthorized drives blocked" : "USB storage access allowed",
                SubtitleAr = blockRemovable ? "حظر الأجهزة غير المصرح بها" : "الوصول لوحدات التخزين مسموح",
                BadgeEn = blockRemovable ? "LOCKED" : "ALLOWED",
                BadgeAr = blockRemovable ? "مقفل" : "مسموح",
                Variant = blockRemovable ? StatusBadgeVariant.Success : StatusBadgeVariant.Neutral
            });

            _items.Add(new FeatureItem
            {
                TitleEn = "Read-Only Mode",
                TitleAr = "وضع القراءة فقط",
                SubtitleEn = p.ReadOnlyRemovable ? "Write-protect deny enforced" : "Write access permitted",
                SubtitleAr = p.ReadOnlyRemovable ? "فرض حظر الكتابة على الوسائط" : "الكتابة مسموحة",
                BadgeEn = p.ReadOnlyRemovable ? "ENFORCED" : "OFF",
                BadgeAr = p.ReadOnlyRemovable ? "مفروض" : "معطل",
                Variant = p.ReadOnlyRemovable ? StatusBadgeVariant.Info : StatusBadgeVariant.Neutral
            });

            _items.Add(new FeatureItem
            {
                TitleEn = "Flash Storage Sentinel",
                TitleAr = "حارس ذاكرة الفلاش",
                SubtitleEn = p.BlockFlashDrivesOnlyReactive ? "Active background monitoring" : "Background sentinel idle",
                SubtitleAr = p.BlockFlashDrivesOnlyReactive ? "المراقبة الفورية نشطة" : "الحارس معطل",
                BadgeEn = p.BlockFlashDrivesOnlyReactive ? "ACTIVE" : "OFF",
                BadgeAr = p.BlockFlashDrivesOnlyReactive ? "نشط" : "معطل",
                Variant = p.BlockFlashDrivesOnlyReactive ? StatusBadgeVariant.Success : StatusBadgeVariant.Neutral
            });

            _items.Add(new FeatureItem
            {
                TitleEn = "AutoPlay Guard",
                TitleAr = "حارس التشغيل التلقائي",
                SubtitleEn = p.AutoPlayGuard ? "AutoPlay execution blocked" : "System default behavior",
                SubtitleAr = p.AutoPlayGuard ? "التشغيل التلقائي محظور" : "الافتراضي للنظام",
                BadgeEn = p.AutoPlayGuard ? "BLOCKED" : "OFF",
                BadgeAr = p.AutoPlayGuard ? "محظور" : "معطل",
                Variant = p.AutoPlayGuard ? StatusBadgeVariant.Success : StatusBadgeVariant.Neutral
            });

            _items.Add(new FeatureItem
            {
                TitleEn = "Portable Devices (WPD)",
                TitleAr = "الأجهزة المحمولة (WPD)",
                SubtitleEn = p.BlockWpdPhonesAndMedia ? "Smartphone transfer blocked" : "Smartphones permitted",
                SubtitleAr = p.BlockWpdPhonesAndMedia ? "نقل وسائط الهواتف محظور" : "الهواتف مسموحة",
                BadgeEn = p.BlockWpdPhonesAndMedia ? "BLOCKED" : "ALLOWED",
                BadgeAr = p.BlockWpdPhonesAndMedia ? "محظور" : "مسموح",
                Variant = p.BlockWpdPhonesAndMedia ? StatusBadgeVariant.Success : StatusBadgeVariant.Neutral
            });

            _items.Add(new FeatureItem
            {
                TitleEn = "Whitelist Mode",
                TitleAr = "نمط القائمة البيضاء",
                SubtitleEn = p.WhitelistMode ? "Strict trusted hardware ID check" : "Whitelist not enforced",
                SubtitleAr = p.WhitelistMode ? "فرض الأجهزة الموثوقة بدقة" : "النمط غير مفعل",
                BadgeEn = p.WhitelistMode ? "STRICT" : "OFF",
                BadgeAr = p.WhitelistMode ? "صارم" : "معطل",
                Variant = p.WhitelistMode ? StatusBadgeVariant.Success : StatusBadgeVariant.Neutral
            });

            _configureBtn.Text = isRtl ? "تهيئة" : "Configure";
            PerformLayout();
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (_configureBtn == null) return;

            bool isRtl = (RightToLeft == RightToLeft.Yes);
            int pad = DpiManager.ScaleInt(SpacingTokens.Space14);
            int btnW = DpiManager.ScaleInt(100);
            int btnH = DpiManager.ScaleInt(26);
            int headerH = DpiManager.ScaleInt(38);
            int btnY = (headerH - btnH) / 2;

            if (isRtl)
            {
                _configureBtn.Location = new Point(pad, btnY);
            }
            else
            {
                _configureBtn.Location = new Point(Width - pad - btnW, btnY);
            }
            _configureBtn.Size = new Size(btnW, btnH);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int headerH = DpiManager.ScaleInt(38);
            if (e.Y < headerH || _items.Count == 0)
            {
                if (_hoveredRow != -1) { _hoveredRow = -1; Invalidate(); }
                return;
            }

            int contentH = Height - headerH;
            int rowH = contentH / _items.Count;
            int row = (e.Y - headerH) / Math.Max(1, rowH);
            if (row >= _items.Count) row = -1;

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

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int headerH = DpiManager.ScaleInt(38);
            if (e.Y >= headerH && ConfigureClicked != null)
            {
                ConfigureClicked(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes);

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

            // Header
            Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            string headerText = isRtl ? "نظرة عامة على الحماية" : "Protection Overview";

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

            int contentH = Height - headerH;
            int count = _items.Count;
            if (count == 0) return;

            int rowH = contentH / count;
            Font rowTitleFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            Font badgeFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            for (int i = 0; i < count; i++)
            {
                var item = _items[i];
                int rowY = headerH + i * rowH;
                Rectangle rowRect = new Rectangle(1, rowY, Width - 2, rowH);

                if (i == _hoveredRow)
                {
                    using (SolidBrush hb = new SolidBrush(pal.SurfaceHover))
                    {
                        g.FillRectangle(hb, rowRect);
                    }
                }

                if (i > 0)
                {
                    using (Pen sp = new Pen(pal.BorderSubtle, 1f))
                    {
                        g.DrawLine(sp, pad, rowY, Width - pad, rowY);
                    }
                }

                string title = isRtl ? item.TitleAr : item.TitleEn;
                string badge = isRtl ? item.BadgeAr : item.BadgeEn;

                // Measure badge pill
                int badgeH = DpiManager.ScaleInt(isRtl ? 22 : 18);
                Size bSize = TextRenderer.MeasureText(g, badge, badgeFont);
                int badgeW = bSize.Width + DpiManager.ScaleInt(14);
                int badgeY = rowY + (rowH - badgeH) / 2;

                if (isRtl)
                {
                    // In RTL, Badge on far left, Title on right
                    int badgeX = pad;
                    DrawBadgePill(g, pal, item.Variant, badge, badgeFont, badgeX, badgeY, badgeW, badgeH, isRtl);

                    int textRight = Width - pad;
                    int textLeft = badgeX + badgeW + DpiManager.ScaleInt(SpacingTokens.Space8);
                    int textW = Math.Max(20, textRight - textLeft);

                    Rectangle tRect = new Rectangle(textLeft, rowY, textW, rowH);
                    TextRenderer.DrawText(g, title, rowTitleFont, tRect, pal.TextPrimary,
                        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);
                }
                else
                {
                    // In LTR, Title on left, Badge on far right
                    int badgeX = Width - pad - badgeW;
                    DrawBadgePill(g, pal, item.Variant, badge, badgeFont, badgeX, badgeY, badgeW, badgeH, isRtl);

                    int textLeft = pad;
                    int textRight = badgeX - DpiManager.ScaleInt(SpacingTokens.Space8);
                    int textW = Math.Max(20, textRight - textLeft);

                    Rectangle tRect = new Rectangle(textLeft, rowY, textW, rowH);
                    TextRenderer.DrawText(g, title, rowTitleFont, tRect, pal.TextPrimary,
                        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);
                }
            }
        }

        private void DrawBadgePill(Graphics g, IThemePalette pal, StatusBadgeVariant variant, string text, Font font, int x, int y, int w, int h, bool isRtl)
        {
            Color fg, bg, bd;
            switch (variant)
            {
                case StatusBadgeVariant.Success:
                    fg = pal.Success;
                    bg = pal.SuccessSoft;
                    bd = pal.Success;
                    break;
                case StatusBadgeVariant.Warning:
                    fg = pal.Warning;
                    bg = pal.WarningSoft;
                    bd = pal.Warning;
                    break;
                case StatusBadgeVariant.Danger:
                    fg = pal.Danger;
                    bg = pal.DangerSoft;
                    bd = pal.Danger;
                    break;
                case StatusBadgeVariant.Info:
                    fg = pal.Info;
                    bg = pal.InfoSoft;
                    bd = pal.Info;
                    break;
                default:
                    fg = pal.TextSecondary;
                    bg = pal.SurfaceElevated;
                    bd = pal.BorderSubtle;
                    break;
            }

            Rectangle pillRect = new Rectangle(x, y, w, h);
            using (SolidBrush b = new SolidBrush(bg))
            {
                UiDrawingUtils.FillRoundedRectangle(g, b, pillRect, h / 2);
            }
            using (Pen p = new Pen(bd, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, p, pillRect, h / 2);
            }

            TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping;
            if (isRtl) flags |= TextFormatFlags.RightToLeft;
            bool isArabic = UiDrawingUtils.IsArabicText(text, isRtl);
            int yOffset = UiDrawingUtils.GetBadgeTextVerticalOffset(isArabic);
            Rectangle textRect = new Rectangle(x, y + yOffset, w, h);
            TextRenderer.DrawText(g, text, font, textRect, fg, flags);
        }
    }
}
