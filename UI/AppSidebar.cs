// ═══════════════════════════════════════════════════════════════════════════
// AppSidebar — Tier 1 Modern Navigation Sidebar Primitive for v2.1
// USB Shield Pro v2.1
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
    public class SidebarItemEventArgs : EventArgs
    {
        public int Index { get; private set; }
        public string Title { get; private set; }

        public SidebarItemEventArgs(int index, string title)
        {
            Index = index;
            Title = title;
        }
    }

    public class SidebarNavigationItem
    {
        public int Index { get; set; }
        public string Title { get; set; }
        public string Group { get; set; }
        public Image Icon { get; set; }
        public string BadgeText { get; set; }

        public SidebarNavigationItem(int index, string title, string group = "", Image icon = null)
        {
            Index = index;
            Title = title;
            Group = group;
            Icon = icon;
            BadgeText = "";
        }
    }

    public class AppSidebar : Control
    {
        private readonly List<SidebarNavigationItem> _items = new List<SidebarNavigationItem>();
        private int _selectedIndex = 0;
        private int _hoveredIndex = -1;
        private bool _isCompact = false;
        private bool _footerHovered = false;

        private Rectangle _footerRect;
        private Rectangle _toggleButtonRect;
        private readonly Dictionary<int, Rectangle> _itemBounds = new Dictionary<int, Rectangle>();
        private CompactNavFlyout _flyout;

        public event EventHandler<SidebarItemEventArgs> ItemSelected;
        public event EventHandler SelectedIndexChanged;
        public event EventHandler CompactChanged;

        public AppSidebar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.Outline;

            Width = DpiManager.ScaleInt(220);
            InitializeStandardItems();

            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public List<SidebarNavigationItem> Items
        {
            get { return _items; }
        }

        public bool IsCompact
        {
            get { return _isCompact; }
            set
            {
                if (_isCompact != value)
                {
                    _isCompact = value;
                    Width = DpiManager.ScaleInt(_isCompact ? 68 : 220);
                    HideFlyout();
                    Invalidate();
                    OnCompactChanged();
                }
            }
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (value >= 0 && value < _items.Count)
                {
                    if (_selectedIndex != value)
                    {
                        _selectedIndex = value;
                        Invalidate();
                        OnItemSelected(_selectedIndex);
                    }
                }
            }
        }

        public SidebarNavigationItem SelectedItem
        {
            get
            {
                if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
                {
                    return _items[_selectedIndex];
                }
                return null;
            }
        }

        public void InitializeStandardItems()
        {
            _items.Clear();
            _items.Add(new SidebarNavigationItem(0, "Dashboard", "Overview"));
            var devItem = new SidebarNavigationItem(1, "Connected Devices", "Overview");
            devItem.BadgeText = "2";
            _items.Add(devItem);
            _items.Add(new SidebarNavigationItem(2, "Protection", "Protection"));
            _items.Add(new SidebarNavigationItem(3, "Trusted Devices", "Protection"));
            _items.Add(new SidebarNavigationItem(4, "Activity", "Monitoring"));
            _items.Add(new SidebarNavigationItem(5, "Recovery", "System"));
            _items.Add(new SidebarNavigationItem(6, "Settings", "System"));
        }

        private void OnItemSelected(int index)
        {
            if (ItemSelected != null && index >= 0 && index < _items.Count)
            {
                ItemSelected(this, new SidebarItemEventArgs(index, _items[index].Title));
            }
            if (SelectedIndexChanged != null)
            {
                SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        private void OnCompactChanged()
        {
            if (CompactChanged != null)
            {
                CompactChanged(this, EventArgs.Empty);
            }
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                Invalidate();
            }
        }

        private void HideFlyout()
        {
            if (_flyout != null && _flyout.Visible)
            {
                _flyout.Hide();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                if (_flyout != null)
                {
                    _flyout.Dispose();
                    _flyout = null;
                }
            }
            base.Dispose(disposing);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // 1. Check footer collapse toggle button
            bool isToggleHover = _toggleButtonRect.Contains(e.Location);
            if (_footerHovered != isToggleHover)
            {
                _footerHovered = isToggleHover;
                Invalidate();
            }

            // 2. Check items
            int newHover = -1;
            foreach (var kvp in _itemBounds)
            {
                if (kvp.Value.Contains(e.Location))
                {
                    newHover = kvp.Key;
                    break;
                }
            }

            if (_hoveredIndex != newHover)
            {
                _hoveredIndex = newHover;
                Invalidate();

                if (_isCompact && _hoveredIndex >= 0 && _hoveredIndex < _items.Count)
                {
                    ShowFlyoutForItem(_hoveredIndex);
                }
                else
                {
                    HideFlyout();
                }
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredIndex != -1 || _footerHovered)
            {
                _hoveredIndex = -1;
                _footerHovered = false;
                HideFlyout();
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (_toggleButtonRect.Contains(e.Location))
            {
                IsCompact = !_isCompact;
                return;
            }

            foreach (var kvp in _itemBounds)
            {
                if (kvp.Value.Contains(e.Location))
                {
                    SelectedIndex = kvp.Key;
                    break;
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                if (_selectedIndex < _items.Count - 1)
                {
                    SelectedIndex++;
                }
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Up)
            {
                if (_selectedIndex > 0)
                {
                    SelectedIndex--;
                }
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                OnItemSelected(_selectedIndex);
                e.Handled = true;
                return;
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

        private void ShowFlyoutForItem(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            if (!IsHandleCreated || IsDisposed) return;

            Rectangle itemRect;
            if (!_itemBounds.TryGetValue(index, out itemRect)) return;

            if (_flyout == null || _flyout.IsDisposed)
            {
                _flyout = new CompactNavFlyout();
            }

            var item = _items[index];
            bool isRtl = (RightToLeft == RightToLeft.Yes);
            _flyout.SetContent(item.Title, item.Group, item.BadgeText, isRtl);

            int flyoutW = _flyout.Width;
            int flyoutH = _flyout.Height;
            int flyoutY = itemRect.Y + (itemRect.Height - flyoutH) / 2;

            Point screenPt;
            if (isRtl)
            {
                // In RTL mode, sidebar is docked to the right. Place flyout adjacent to the left edge of the sidebar.
                screenPt = PointToScreen(new Point(-flyoutW - DpiManager.ScaleInt(8), flyoutY));
            }
            else
            {
                screenPt = PointToScreen(new Point(Width + DpiManager.ScaleInt(8), flyoutY));
            }

            // Screen boundary clamping & topbar overlap prevention:
            Screen scr = Screen.FromControl(this);
            if (scr != null)
            {
                Rectangle work = scr.WorkingArea;
                // Never overlap topbar: compute topbar bottom in screen coordinates
                Form parentForm = FindForm();
                int topbarBottomScreen = (parentForm != null) 
                    ? parentForm.PointToScreen(new Point(0, DpiManager.ScaleInt(48) + parentForm.Padding.Top)).Y 
                    : work.Top;
                int minY = Math.Max(work.Top + DpiManager.ScaleInt(6), topbarBottomScreen + DpiManager.ScaleInt(4));
                int maxY = work.Bottom - flyoutH - DpiManager.ScaleInt(8);

                screenPt.Y = Math.Max(minY, Math.Min(screenPt.Y, maxY));
                screenPt.X = Math.Max(work.Left + DpiManager.ScaleInt(8), Math.Min(screenPt.X, work.Right - flyoutW - DpiManager.ScaleInt(8)));
            }

            _flyout.Location = screenPt;
            if (!_flyout.Visible)
            {
                _flyout.Show();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes);

            // Fill Sidebar Background
            using (SolidBrush bgBrush = new SolidBrush(pal.BackgroundSecondary))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            // Draw Border on Trailing Edge
            using (Pen borderPen = new Pen(pal.BorderSubtle, 1f))
            {
                if (isRtl)
                {
                    g.DrawLine(borderPen, 0, 0, 0, Height);
                }
                else
                {
                    g.DrawLine(borderPen, Width - 1, 0, Width - 1, Height);
                }
            }

            // Calculate Footer Area
            int footerH = DpiManager.ScaleInt(42);
            _footerRect = new Rectangle(0, Height - footerH, Width, footerH);

            _itemBounds.Clear();

            int curY = DpiManager.ScaleInt(SpacingTokens.Space12);
            int itemHeight = DpiManager.ScaleInt(HeightTokens.HeightMd);
            int itemMarginHorz = DpiManager.ScaleInt(SpacingTokens.Space6);
            int groupHeight = DpiManager.ScaleInt(24);
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusSmall);

            Font itemFont = DpiManager.GetFont(TypographyRole.Body, isRtl);
            Font selectedFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            Font groupFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            string currentGroup = null;

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];

                // Group Header / Divider
                if (!string.IsNullOrEmpty(item.Group) && item.Group != currentGroup)
                {
                    currentGroup = item.Group;
                    if (_isCompact)
                    {
                        // In compact mode, draw a quiet divider line instead of text
                        if (i > 0)
                        {
                            int divY = curY + DpiManager.ScaleInt(2);
                            using (Pen divPen = new Pen(pal.BorderSubtle, 1f))
                            {
                                g.DrawLine(divPen, itemMarginHorz, divY, Width - itemMarginHorz, divY);
                            }
                            curY += DpiManager.ScaleInt(8);
                        }
                    }
                    else
                    {
                        // In expanded mode, draw quiet uppercase group header
                        Rectangle groupRect = new Rectangle(itemMarginHorz, curY, Width - itemMarginHorz * 2, groupHeight);
                        TextRenderer.DrawText(g, currentGroup.ToUpperInvariant(), groupFont, groupRect, pal.TextMuted,
                            TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | (isRtl ? (TextFormatFlags.Right | TextFormatFlags.RightToLeft) : TextFormatFlags.Left) | TextFormatFlags.NoPrefix);
                        curY += groupHeight;
                    }
                }

                Rectangle itemRect = new Rectangle(itemMarginHorz, curY, Width - itemMarginHorz * 2, itemHeight);
                _itemBounds[i] = itemRect;

                bool isSelected = (i == _selectedIndex);
                bool isHovered = (i == _hoveredIndex && !isSelected);

                // Draw Item Background
                if (isSelected)
                {
                    using (SolidBrush b = new SolidBrush(pal.SurfaceSelected))
                    {
                        UiDrawingUtils.FillRoundedRectangle(g, b, itemRect, rad);
                    }

                    // Restrained Accent Indicator Stripe (3px on logical leading edge)
                    int stripeW = DpiManager.ScaleInt(3);
                    int stripeX = isRtl ? (itemRect.Right - stripeW) : itemRect.X;
                    Rectangle stripeRect = new Rectangle(stripeX, itemRect.Y + DpiManager.ScaleInt(5), stripeW, itemRect.Height - DpiManager.ScaleInt(10));
                    using (SolidBrush sb = new SolidBrush(pal.Accent))
                    {
                        UiDrawingUtils.FillRoundedRectangle(g, sb, stripeRect, stripeW / 2);
                    }
                }
                else if (isHovered)
                {
                    using (SolidBrush b = new SolidBrush(pal.SurfaceHover))
                    {
                        UiDrawingUtils.FillRoundedRectangle(g, b, itemRect, rad);
                    }
                }

                // Determine Colors
                Color textColor = isSelected ? pal.TextPrimary : (isHovered ? pal.TextPrimary : pal.TextSecondary);
                Color iconColor = isSelected ? pal.Accent : (isHovered ? pal.TextPrimary : pal.TextSecondary);
                Font activeFont = isSelected ? selectedFont : itemFont;

                int iconDim = DpiManager.ScaleInt(IconTokens.IconMd);

                if (_isCompact)
                {
                    // ─── Compact Mode: Icon Centered ─────────────────────────
                    int iconX = itemRect.X + (itemRect.Width - iconDim) / 2;
                    int iconY = itemRect.Y + (itemRect.Height - iconDim) / 2;
                    Rectangle iconBounds = new Rectangle(iconX, iconY, iconDim, iconDim);

                    DrawItemVectorIcon(g, item, iconBounds, iconColor);

                    // If item has a badge, show a subtle accent dot in compact mode
                    if (!string.IsNullOrEmpty(item.BadgeText))
                    {
                        int dotSize = DpiManager.ScaleInt(6);
                        int dotX = isRtl ? (iconBounds.Left - DpiManager.ScaleInt(1)) : (iconBounds.Right - dotSize + DpiManager.ScaleInt(1));
                        int dotY = iconBounds.Top - DpiManager.ScaleInt(1);
                        using (SolidBrush dotBrush = new SolidBrush(pal.Accent))
                        {
                            g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
                        }
                    }
                }
                else
                {
                    // ─── Expanded Mode: Leading Icon + Label + Trailing Badge ─
                    int padEdge = DpiManager.ScaleInt(SpacingTokens.Space6);
                    int spacing = DpiManager.ScaleInt(SpacingTokens.Space6);

                    if (isRtl)
                    {
                        int rightPos = itemRect.Right - padEdge;

                        // Icon on the right
                        int iconX = rightPos - iconDim;
                        int iconY = itemRect.Y + (itemRect.Height - iconDim) / 2;
                        Rectangle iconBounds = new Rectangle(iconX, iconY, iconDim, iconDim);
                        DrawItemVectorIcon(g, item, iconBounds, iconColor);
                        rightPos -= (iconDim + spacing);

                        // Optional Badge on far left (trailing edge in RTL)
                        int textLeftPos = itemRect.X + padEdge;
                        if (!string.IsNullOrEmpty(item.BadgeText))
                        {
                            Size badgeSz = TextRenderer.MeasureText(g, item.BadgeText, groupFont);
                            int bH = DpiManager.ScaleInt(18);
                            int bW = Math.Max(bH, badgeSz.Width + DpiManager.ScaleInt(6));
                            int bY = itemRect.Y + (itemRect.Height - bH) / 2;
                            Rectangle badgeRect = new Rectangle(textLeftPos, bY, bW, bH);

                            using (SolidBrush bb = new SolidBrush(isSelected ? pal.AccentSoft : pal.SurfaceElevated))
                            {
                                UiDrawingUtils.FillRoundedRectangle(g, bb, badgeRect, bH / 2);
                            }
                            using (Pen bp = new Pen(isSelected ? pal.Accent : pal.BorderSubtle, 1f))
                            {
                                UiDrawingUtils.DrawRoundedRectangle(g, bp, badgeRect, bH / 2);
                            }
                            TextRenderer.DrawText(g, item.BadgeText, groupFont, badgeRect, isSelected ? pal.Accent : pal.TextMuted,
                                TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);

                            textLeftPos += bW + DpiManager.ScaleInt(SpacingTokens.Space4);
                        }

                        // Text Label
                        Rectangle textRect = new Rectangle(textLeftPos, itemRect.Y, rightPos - textLeftPos, itemRect.Height);
                        TextRenderer.DrawText(g, item.Title, activeFont, textRect, textColor,
                            TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix);
                    }
                    else
                    {
                        int leftPos = itemRect.X + padEdge;

                        // Icon on the left
                        int iconY = itemRect.Y + (itemRect.Height - iconDim) / 2;
                        Rectangle iconBounds = new Rectangle(leftPos, iconY, iconDim, iconDim);
                        DrawItemVectorIcon(g, item, iconBounds, iconColor);
                        leftPos += (iconDim + spacing);

                        // Optional Badge on far right (trailing edge in LTR)
                        int textRightPos = itemRect.Right - padEdge;
                        if (!string.IsNullOrEmpty(item.BadgeText))
                        {
                            Size badgeSz = TextRenderer.MeasureText(g, item.BadgeText, groupFont);
                            int bH = DpiManager.ScaleInt(18);
                            int bW = Math.Max(bH, badgeSz.Width + DpiManager.ScaleInt(6));
                            int bY = itemRect.Y + (itemRect.Height - bH) / 2;
                            int bX = textRightPos - bW;
                            Rectangle badgeRect = new Rectangle(bX, bY, bW, bH);

                            using (SolidBrush bb = new SolidBrush(isSelected ? pal.AccentSoft : pal.SurfaceElevated))
                            {
                                UiDrawingUtils.FillRoundedRectangle(g, bb, badgeRect, bH / 2);
                            }
                            using (Pen bp = new Pen(isSelected ? pal.Accent : pal.BorderSubtle, 1f))
                            {
                                UiDrawingUtils.DrawRoundedRectangle(g, bp, badgeRect, bH / 2);
                            }
                            TextRenderer.DrawText(g, item.BadgeText, groupFont, badgeRect, isSelected ? pal.Accent : pal.TextMuted,
                                TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);

                            textRightPos -= (bW + DpiManager.ScaleInt(SpacingTokens.Space4));
                        }

                        // Text Label
                        Rectangle textRect = new Rectangle(leftPos, itemRect.Y, textRightPos - leftPos, itemRect.Height);
                        TextRenderer.DrawText(g, item.Title, activeFont, textRect, textColor,
                            TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                    }
                }

                // Focus ring on selected item when focused
                if (isSelected && Focused && ShowFocusCues)
                {
                    UiDrawingUtils.DrawFocusRing(g, itemRect, rad, pal.FocusRing);
                }

                curY += itemHeight + DpiManager.ScaleInt(SpacingTokens.Space4);
            }

            // ─── Draw Sidebar Footer ─────────────────────────────────────────
            using (Pen borderPen = new Pen(pal.BorderSubtle, 1f))
            {
                g.DrawLine(borderPen, 0, _footerRect.Y, Width, _footerRect.Y);
            }

            int btnDim = DpiManager.ScaleInt(30);
            int btnY = _footerRect.Y + (_footerRect.Height - btnDim) / 2;

            if (_isCompact)
            {
                _toggleButtonRect = new Rectangle((Width - btnDim) / 2, btnY, btnDim, btnDim);
            }
            else
            {
                int padEdge = DpiManager.ScaleInt(SpacingTokens.Space12);
                if (isRtl)
                {
                    _toggleButtonRect = new Rectangle(padEdge, btnY, btnDim, btnDim);

                    // Version text on the right
                    Rectangle verRect = new Rectangle(Width - padEdge - DpiManager.ScaleInt(60), _footerRect.Y, DpiManager.ScaleInt(60), _footerRect.Height);
                    TextRenderer.DrawText(g, "v2.1", groupFont, verRect, pal.TextMuted,
                        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix);
                }
                else
                {
                    _toggleButtonRect = new Rectangle(Width - padEdge - btnDim, btnY, btnDim, btnDim);

                    // Version text on the left
                    Rectangle verRect = new Rectangle(padEdge, _footerRect.Y, DpiManager.ScaleInt(60), _footerRect.Height);
                    TextRenderer.DrawText(g, "v2.1", groupFont, verRect, pal.TextMuted,
                        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                }
            }

            // Draw toggle button background on hover
            if (_footerHovered)
            {
                using (SolidBrush hb = new SolidBrush(pal.SurfaceHover))
                {
                    UiDrawingUtils.FillRoundedRectangle(g, hb, _toggleButtonRect, DpiManager.ScaleInt(RadiusTokens.RadiusSmall));
                }
            }

            // Draw vector collapse/expand chevron
            Color toggleColor = _footerHovered ? pal.TextPrimary : pal.TextMuted;
            UiDrawingUtils.DrawNavCollapse(g, toggleColor, _toggleButtonRect, _isCompact, isRtl);
        }

        private void DrawItemVectorIcon(Graphics g, SidebarNavigationItem item, Rectangle bounds, Color color)
        {
            if (item.Icon != null)
            {
                g.DrawImage(item.Icon, bounds);
                return;
            }

            switch (item.Index)
            {
                case 0:
                    UiDrawingUtils.DrawNavDashboard(g, color, bounds);
                    break;
                case 1:
                    UiDrawingUtils.DrawNavUsb(g, color, bounds);
                    break;
                case 2:
                    UiDrawingUtils.DrawNavShield(g, color, bounds);
                    break;
                case 3:
                    UiDrawingUtils.DrawNavTrusted(g, color, bounds);
                    break;
                case 4:
                    UiDrawingUtils.DrawNavActivity(g, color, bounds);
                    break;
                case 5:
                    UiDrawingUtils.DrawNavRecovery(g, color, bounds);
                    break;
                case 6:
                    UiDrawingUtils.DrawNavSettings(g, color, bounds);
                    break;
                default:
                    UiDrawingUtils.DrawNavDashboard(g, color, bounds);
                    break;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CompactNavFlyout — Lightweight Floating Destination Flyout for Compact Mode
    // ═══════════════════════════════════════════════════════════════════════════
    internal class CompactNavFlyout : Form
    {
        private string _title = "";
        private string _group = "";
        private string _badge = "";
        private bool _isRtl = false;

        public CompactNavFlyout()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.Style |= unchecked((int)0x80000000); // WS_POPUP
                return cp;
            }
        }

        public void SetContent(string title, string group, string badge, bool isRtl)
        {
            _title = title ?? "";
            _group = group ?? "";
            _badge = badge ?? "";
            _isRtl = isRtl;
            RightToLeft = _isRtl ? RightToLeft.Yes : RightToLeft.No;

            using (Graphics g = CreateGraphics())
            {
                Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, _isRtl);
                Font groupFont = DpiManager.GetFont(TypographyRole.Caption, _isRtl);

                Size titleSz = TextRenderer.MeasureText(g, _title, titleFont, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                Size groupSz = TextRenderer.MeasureText(g, _group.ToUpperInvariant(), groupFont, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

                int badgeW = 0;
                if (!string.IsNullOrEmpty(_badge))
                {
                    badgeW = TextRenderer.MeasureText(g, _badge, groupFont, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width + DpiManager.ScaleInt(14);
                }

                int contentW = Math.Max(titleSz.Width + badgeW + (badgeW > 0 ? DpiManager.ScaleInt(10) : 0), groupSz.Width);
                int padH = DpiManager.ScaleInt(16);
                int padV = DpiManager.ScaleInt(9);

                int extraW = _isRtl ? DpiManager.ScaleInt(18) : DpiManager.ScaleInt(8);
                Width = Math.Max(DpiManager.ScaleInt(140), contentW + padH * 2 + extraW);
                Height = titleFont.Height + groupFont.Height + padV * 2 + DpiManager.ScaleInt(6);
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);

            // Background & Border
            using (SolidBrush bg = new SolidBrush(pal.SurfaceElevated))
            {
                UiDrawingUtils.FillRoundedRectangle(g, bg, rect, rad);
            }
            using (Pen borderPen = new Pen(pal.Border, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, borderPen, rect, rad);
            }

            int padH = DpiManager.ScaleInt(16);
            int padV = DpiManager.ScaleInt(9);
            Font groupFont = DpiManager.GetFont(TypographyRole.Caption, _isRtl);
            Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, _isRtl);

            int curY = padV;

            // Group Label
            if (!string.IsNullOrEmpty(_group))
            {
                Rectangle groupRect = new Rectangle(padH, curY, Width - padH * 2, groupFont.Height + DpiManager.ScaleInt(2));
                TextRenderer.DrawText(g, _group.ToUpperInvariant(), groupFont, groupRect, pal.TextMuted,
                    TextFormatFlags.SingleLine | TextFormatFlags.Top | (_isRtl ? (TextFormatFlags.Right | TextFormatFlags.RightToLeft) : TextFormatFlags.Left) | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);
                curY += groupFont.Height + DpiManager.ScaleInt(3);
            }

            // Title Label + Badge
            int titleW = Width - padH * 2;
            if (!string.IsNullOrEmpty(_badge))
            {
                Size badgeSz = TextRenderer.MeasureText(g, _badge, groupFont, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                int bW = badgeSz.Width + DpiManager.ScaleInt(12);
                int bH = groupFont.Height + DpiManager.ScaleInt(2);
                titleW -= (bW + DpiManager.ScaleInt(6));

                int bY = curY + (titleFont.Height - bH) / 2;
                Rectangle bRect = _isRtl ? new Rectangle(padH, bY, bW, bH) : new Rectangle(Width - padH - bW, bY, bW, bH);
                using (SolidBrush bb = new SolidBrush(pal.AccentSoft))
                {
                    UiDrawingUtils.FillRoundedRectangle(g, bb, bRect, bH / 2);
                }
                using (Pen bp = new Pen(pal.Accent, 1f))
                {
                    UiDrawingUtils.DrawRoundedRectangle(g, bp, bRect, bH / 2);
                }
                TextRenderer.DrawText(g, _badge, groupFont, bRect, pal.Accent,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);
            }

            int titleX = _isRtl ? (Width - padH - titleW) : padH;
            Rectangle titleRect = new Rectangle(titleX, curY, titleW, titleFont.Height + DpiManager.ScaleInt(4));
            TextRenderer.DrawText(g, _title, titleFont, titleRect, pal.TextPrimary,
                TextFormatFlags.SingleLine | TextFormatFlags.Top | (_isRtl ? (TextFormatFlags.Right | TextFormatFlags.RightToLeft) : TextFormatFlags.Left) | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);
        }
    }
}
