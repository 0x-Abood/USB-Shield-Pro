#if !PRODUCTION
// ═══════════════════════════════════════════════════════════════════════════
// ComponentShowcaseForm — Tier 1 Component Showcase Harness for v2.1 QA
// USB Shield Pro v2.1
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Drawing;
using System.Windows.Forms;

namespace UsbShieldPro.UI
{
    public class ComponentShowcaseForm : Form
    {
        private AppTopBar _topBar;
        private AppSidebar _sidebar;
        private Panel _contentContainer;
        private FlowLayoutPanel _mainFlow;

        public ComponentShowcaseForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            Size = new Size(DpiManager.ScaleInt(1080), DpiManager.ScaleInt(740));
            RightToLeft = Loc.RtlValue;

            BuildShowcase();
            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyPaletteColors();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                ApplyPaletteColors();
                Invalidate(true);
            }
        }

        private void ApplyPaletteColors()
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            BackColor = pal.BackgroundPrimary;
            if (_contentContainer != null) _contentContainer.BackColor = pal.BackgroundPrimary;
            if (_mainFlow != null) _mainFlow.BackColor = pal.BackgroundPrimary;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }

        private void BuildShowcase()
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes);

            // 1. TopBar
            _topBar = new AppTopBar();
            _topBar.BrandTitle = "USB SHIELD PRO";
            _topBar.VersionText = "v2.1 Primitives";
            _topBar.CloseClicked += delegate { Close(); };
            Controls.Add(_topBar);

            // 2. Sidebar
            _sidebar = new AppSidebar();
            _sidebar.Dock = isRtl ? DockStyle.Right : DockStyle.Left;
            Controls.Add(_sidebar);

            // 3. Main Content Container
            _contentContainer = new Panel();
            _contentContainer.Dock = DockStyle.Fill;
            _contentContainer.Padding = new Padding(DpiManager.ScaleInt(SpacingTokens.Space24));
            _contentContainer.AutoScroll = true;
            Controls.Add(_contentContainer);
            _contentContainer.BringToFront();

            _mainFlow = new FlowLayoutPanel();
            _mainFlow.Dock = DockStyle.Fill;
            _mainFlow.FlowDirection = FlowDirection.TopDown;
            _mainFlow.WrapContents = false;
            _mainFlow.AutoScroll = true;
            _contentContainer.Controls.Add(_mainFlow);

            int sectionW = DpiManager.ScaleInt(780);

            // ─── Section 1: AppButton Hierarchy ──────────────────────────────────
            SectionHeader headerButtons = new SectionHeader();
            headerButtons.Width = sectionW;
            headerButtons.Title = isRtl ? "أزرار التحكم والتفاعل (AppButton)" : "Button Hierarchy & Variants (AppButton)";
            headerButtons.Subtitle = isRtl ? "الأزرار الأساسية والثانوية والمحذوفة مع مؤشر التركيز" : "Primary, Secondary, Ghost, and Destructive actions with 2px focus ring";
            headerButtons.ShowDivider = true;
            _mainFlow.Controls.Add(headerButtons);

            FlowLayoutPanel buttonRow1 = CreateItemRow();
            buttonRow1.Width = sectionW;

            AppButton btnPrimary = new AppButton();
            btnPrimary.Variant = ButtonVariant.Primary;
            btnPrimary.SizeVariant = ButtonSize.Medium;
            btnPrimary.Text = isRtl ? "إجراء رئيسي" : "Primary Action";
            btnPrimary.Width = DpiManager.ScaleInt(140);
            buttonRow1.Controls.Add(btnPrimary);

            AppButton btnSecondary = new AppButton();
            btnSecondary.Variant = ButtonVariant.Secondary;
            btnSecondary.SizeVariant = ButtonSize.Medium;
            btnSecondary.Text = isRtl ? "إجراء ثانوي" : "Secondary Action";
            btnSecondary.Width = DpiManager.ScaleInt(140);
            buttonRow1.Controls.Add(btnSecondary);

            AppButton btnGhost = new AppButton();
            btnGhost.Variant = ButtonVariant.Ghost;
            btnGhost.SizeVariant = ButtonSize.Medium;
            btnGhost.Text = isRtl ? "زر شفاف" : "Ghost Action";
            btnGhost.Width = DpiManager.ScaleInt(120);
            buttonRow1.Controls.Add(btnGhost);

            AppButton btnDestructive = new AppButton();
            btnDestructive.Variant = ButtonVariant.Destructive;
            btnDestructive.SizeVariant = ButtonSize.Medium;
            btnDestructive.Text = isRtl ? "حذف نهائي" : "Destructive Action";
            btnDestructive.Width = DpiManager.ScaleInt(140);
            buttonRow1.Controls.Add(btnDestructive);

            AppButton btnDisabled = new AppButton();
            btnDisabled.Variant = ButtonVariant.Primary;
            btnDisabled.SizeVariant = ButtonSize.Medium;
            btnDisabled.Text = isRtl ? "معطل" : "Disabled Action";
            btnDisabled.Width = DpiManager.ScaleInt(130);
            btnDisabled.Enabled = false;
            buttonRow1.Controls.Add(btnDisabled);

            _mainFlow.Controls.Add(buttonRow1);

            // Button Sizes Row
            FlowLayoutPanel buttonRow2 = CreateItemRow();
            buttonRow2.Width = sectionW;

            AppButton btnSm = new AppButton();
            btnSm.Variant = ButtonVariant.Secondary;
            btnSm.SizeVariant = ButtonSize.Small;
            btnSm.Text = isRtl ? "صغير (28px)" : "Small (28px)";
            btnSm.Width = DpiManager.ScaleInt(110);
            buttonRow2.Controls.Add(btnSm);

            AppButton btnMd = new AppButton();
            btnMd.Variant = ButtonVariant.Secondary;
            btnMd.SizeVariant = ButtonSize.Medium;
            btnMd.Text = isRtl ? "قياسي (34px)" : "Standard (34px)";
            btnMd.Width = DpiManager.ScaleInt(130);
            buttonRow2.Controls.Add(btnMd);

            AppButton btnLg = new AppButton();
            btnLg.Variant = ButtonVariant.Primary;
            btnLg.SizeVariant = ButtonSize.Large;
            btnLg.Text = isRtl ? "كبير (42px)" : "Large (42px)";
            btnLg.Width = DpiManager.ScaleInt(140);
            buttonRow2.Controls.Add(btnLg);

            _mainFlow.Controls.Add(buttonRow2);

            // ─── Section 2: Compact Icon Buttons & Binary Toggles ─────────────────
            SectionHeader headerIconsToggles = new SectionHeader();
            headerIconsToggles.Width = sectionW;
            headerIconsToggles.Title = isRtl ? "الأزرار المصغرة ومفاتيح التبديل (IconButton & AppToggle)" : "Icon Buttons & Toggles (IconButton & AppToggle)";
            headerIconsToggles.Subtitle = isRtl ? "أدوات شريط الإجراءات والتبديل الثنائي مع محاذاة RTL" : "Toolbar compact glyphs and binary switches with RTL thumb mirroring";
            headerIconsToggles.ShowDivider = true;
            _mainFlow.Controls.Add(headerIconsToggles);

            FlowLayoutPanel iconToggleRow = CreateItemRow();
            iconToggleRow.Width = sectionW;

            IconButton ibOverflow = new IconButton();
            ibOverflow.ButtonType = IconButtonType.Overflow;
            ibOverflow.ToolTipText = isRtl ? "خيارات إضافية" : "More Options";
            iconToggleRow.Controls.Add(ibOverflow);

            IconButton ibRefresh = new IconButton();
            ibRefresh.ButtonType = IconButtonType.Refresh;
            ibRefresh.ToolTipText = isRtl ? "تحديث الأجهزة" : "Refresh Devices";
            iconToggleRow.Controls.Add(ibRefresh);

            IconButton ibChevronDown = new IconButton();
            ibChevronDown.ButtonType = IconButtonType.ChevronDown;
            ibChevronDown.ToolTipText = isRtl ? "توسيع" : "Expand";
            iconToggleRow.Controls.Add(ibChevronDown);

            IconButton ibChevronRight = new IconButton();
            ibChevronRight.ButtonType = IconButtonType.ChevronRight;
            ibChevronRight.ToolTipText = isRtl ? "التالي" : "Next";
            iconToggleRow.Controls.Add(ibChevronRight);

            IconButton ibClose = new IconButton();
            ibClose.ButtonType = IconButtonType.Close;
            ibClose.ToolTipText = isRtl ? "إغلاق" : "Close";
            iconToggleRow.Controls.Add(ibClose);

            // Spacer
            Panel spacer = new Panel();
            spacer.Width = DpiManager.ScaleInt(SpacingTokens.Space24);
            spacer.Height = DpiManager.ScaleInt(32);
            iconToggleRow.Controls.Add(spacer);

            AppToggle toggleOn = new AppToggle();
            toggleOn.Checked = true;
            toggleOn.Text = isRtl ? "الحماية النشطة" : "Active Sentinel";
            iconToggleRow.Controls.Add(toggleOn);

            AppToggle toggleOff = new AppToggle();
            toggleOff.Checked = false;
            toggleOff.Text = isRtl ? "حظر التشغيل التلقائي" : "AutoPlay Guard";
            iconToggleRow.Controls.Add(toggleOff);

            AppToggle toggleDisabled = new AppToggle();
            toggleDisabled.Checked = true;
            toggleDisabled.Enabled = false;
            toggleDisabled.Text = isRtl ? "معطل بالنظام" : "System Enforced";
            iconToggleRow.Controls.Add(toggleDisabled);

            _mainFlow.Controls.Add(iconToggleRow);

            // ─── Section 3: Status Badges & Dropdowns ─────────────────────────────
            SectionHeader headerBadges = new SectionHeader();
            headerBadges.Width = sectionW;
            headerBadges.Title = isRtl ? "شارات الحالة والقوائم المنسدلة (StatusBadge & AppDropdown)" : "Status Badges & Dropdowns (StatusBadge & AppDropdown)";
            headerBadges.Subtitle = isRtl ? "مؤشرات الحالة الدلالية وقوائم الاختيار المتوافقة مع السمات" : "Semantic status indicators and theme-aware owner-drawn selection menus";
            headerBadges.ShowDivider = true;
            _mainFlow.Controls.Add(headerBadges);

            FlowLayoutPanel badgeRow = CreateItemRow();
            badgeRow.Width = sectionW;

            StatusBadge badgeSuccess = new StatusBadge();
            badgeSuccess.Variant = StatusBadgeVariant.Success;
            badgeSuccess.Text = isRtl ? "محمي بالكامل" : "PROTECTED";
            badgeRow.Controls.Add(badgeSuccess);

            StatusBadge badgeWarning = new StatusBadge();
            badgeWarning.Variant = StatusBadgeVariant.Warning;
            badgeWarning.Text = isRtl ? "تنبيه سياسة" : "ATTENTION";
            badgeRow.Controls.Add(badgeWarning);

            StatusBadge badgeDanger = new StatusBadge();
            badgeDanger.Variant = StatusBadgeVariant.Danger;
            badgeDanger.Text = isRtl ? "محظور" : "BLOCKED";
            badgeRow.Controls.Add(badgeDanger);

            StatusBadge badgeInfo = new StatusBadge();
            badgeInfo.Variant = StatusBadgeVariant.Info;
            badgeInfo.Text = isRtl ? "جاري الفحص" : "AUDITING";
            badgeRow.Controls.Add(badgeInfo);

            StatusBadge badgeNeutral = new StatusBadge();
            badgeNeutral.Variant = StatusBadgeVariant.Neutral;
            badgeNeutral.Text = isRtl ? "غير متصل" : "OFFLINE";
            badgeRow.Controls.Add(badgeNeutral);

            _mainFlow.Controls.Add(badgeRow);

            // Dropdowns Row
            FlowLayoutPanel dropdownRow = CreateItemRow();
            dropdownRow.Width = sectionW;

            AppDropdown dropdown1 = new AppDropdown();
            dropdown1.Items.Add(isRtl ? "سياسة مشددة (حظر الكل)" : "Strict (Block All)");
            dropdown1.Items.Add(isRtl ? "سياسة متوازنة (الموثوق فقط)" : "Balanced (Trusted Only)");
            dropdown1.Items.Add(isRtl ? "وضع التدقيق (مراقبة)" : "Audit Mode (Monitor)");
            dropdown1.SelectedIndex = 1;
            dropdown1.Width = DpiManager.ScaleInt(240);
            dropdownRow.Controls.Add(dropdown1);

            AppDropdown dropdown2 = new AppDropdown();
            dropdown2.Items.Add(isRtl ? "جميع أحداث الأجهزة" : "All Device Events");
            dropdown2.Items.Add(isRtl ? "الأجهزة المحظورة فقط" : "Blocked Devices Only");
            dropdown2.Items.Add(isRtl ? "تغييرات السياسة" : "Policy Audit Trail");
            dropdown2.SelectedIndex = 0;
            dropdown2.Width = DpiManager.ScaleInt(220);
            dropdownRow.Controls.Add(dropdown2);

            _mainFlow.Controls.Add(dropdownRow);
        }

        private FlowLayoutPanel CreateItemRow()
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.AutoSize = true;
            row.FlowDirection = FlowDirection.LeftToRight;
            row.WrapContents = true;
            row.Margin = new Padding(0, DpiManager.ScaleInt(SpacingTokens.Space8), 0, DpiManager.ScaleInt(SpacingTokens.Space12));
            return row;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Outer 1px window border
            using (Pen borderPen = new Pen(ThemeManager.ActivePalette.Border, 1f))
            {
                e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}
#endif
