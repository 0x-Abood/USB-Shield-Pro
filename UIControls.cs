// ═══════════════════════════════════════════════════════════════════════════
// UIControls — Clean Dark-Mode Controls, Design System & Dialogs
// USB Shield Pro v2.0 (C# 5 compatible)
// © 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using UsbShieldPro.Core;
using UsbShieldPro.Models;
using UsbShieldPro.Persistence;
using UsbShieldPro.Security;
using UsbShieldPro.Service;
using UsbShieldPro.UI;

namespace UsbShieldPro
{
    // ─── Design System & Theme ──────────────────────────────────────────────
    static class Theme
    {
        // Consistent Corner Radii
        public const int RadiusCard = 7;
        public const int RadiusBtn = 6;
        public const int RadiusModal = 8;

        // Dark layers (backed dynamically by ThemeManager.ActivePalette)
        public static Color BgDark         { get { return ThemeManager.ActivePalette.BackgroundPrimary; } }
        public static Color BgPanel        { get { return ThemeManager.ActivePalette.BackgroundSecondary; } }
        public static Color BgCard         { get { return ThemeManager.ActivePalette.Surface; } }
        public static Color BgElevated     { get { return ThemeManager.ActivePalette.SurfaceElevated; } }
        public static Color BgHover        { get { return ThemeManager.ActivePalette.SurfaceHover; } }

        // Semantic Accents
        public static Color AccentBlue       { get { return ThemeManager.ActivePalette.Accent; } }
        public static Color AccentBlueHover  { get { return ThemeManager.ActivePalette.AccentHover; } }
        public static Color AccentBlueDim    { get { return ThemeManager.ActivePalette.AccentSoft; } }

        public static Color AccentGreen      { get { return ThemeManager.ActivePalette.Success; } }
        public static Color AccentGreenHover { get { return ThemeManager.ActivePalette.Success; } }
        public static Color AccentGreenDim   { get { return ThemeManager.ActivePalette.SuccessSoft; } }

        public static Color AccentOrange     { get { return ThemeManager.ActivePalette.Warning; } }
        public static Color AccentOrangeHover{ get { return ThemeManager.ActivePalette.Warning; } }
        public static Color AccentOrangeDim  { get { return ThemeManager.ActivePalette.WarningSoft; } }

        public static Color AccentRed        { get { return ThemeManager.ActivePalette.Danger; } }
        public static Color AccentRedHover   { get { return ThemeManager.ActivePalette.Danger; } }
        public static Color AccentRedDim     { get { return ThemeManager.ActivePalette.DangerSoft; } }

        // Text Hierarchy
        public static Color TextPrimary      { get { return ThemeManager.ActivePalette.TextPrimary; } }
        public static Color TextSecondary    { get { return ThemeManager.ActivePalette.TextSecondary; } }
        public static Color TextMuted        { get { return ThemeManager.ActivePalette.TextMuted; } }

        // Borders & Dividers (subtle 1px desktop styling)
        public static Color Border           { get { return ThemeManager.ActivePalette.Border; } }
        public static Color BorderLight      { get { return ThemeManager.ActivePalette.BorderSubtle; } }
        public static Color Divider          { get { return ThemeManager.ActivePalette.BorderSubtle; } }

        // Typography Hierarchy (backed by DpiManager)
        public static Font FontTitle         { get { return DpiManager.GetFont(TypographyRole.Title, Loc.IsRtl); } }
        public static Font FontLarge         { get { return DpiManager.GetFont(TypographyRole.Subtitle, Loc.IsRtl); } }
        public static Font FontNormal        { get { return DpiManager.GetFont(TypographyRole.Body, Loc.IsRtl); } }
        public static Font FontBold          { get { return DpiManager.GetFont(TypographyRole.BodyStrong, Loc.IsRtl); } }
        public static Font FontSmall         { get { return DpiManager.GetFont(TypographyRole.Caption, Loc.IsRtl); } }
        public static Font FontBtn           { get { return DpiManager.GetFont(TypographyRole.BodyStrong, Loc.IsRtl); } }
        public static Font FontMono          { get { return DpiManager.GetFont(TypographyRole.Code, false); } }

        public static GraphicsPath RoundedRectPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;
            if (d <= 0) d = 1;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRoundedRect(Graphics g, Brush brush, Rectangle rect, int radius)
        {
            using (GraphicsPath path = RoundedRectPath(rect, radius))
                g.FillPath(brush, path);
        }

        public static void DrawRoundedRect(Graphics g, Pen pen, Rectangle rect, int radius)
        {
            using (GraphicsPath path = RoundedRectPath(rect, radius))
                g.DrawPath(pen, path);
        }

        public static Color BlendColor(Color a, Color b, float t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }

    public enum ModernButtonStyle { Primary, Secondary, Danger }

    // ─── Standardized Modern Button ──────────────────────────────────────────
    class ModernButton : Control
    {
        private ModernButtonStyle _style = ModernButtonStyle.Secondary;
        private bool _hovering;
        private bool _pressing;
        private int _cornerRadius = 6;

        public ModernButtonStyle ButtonStyle
        {
            get { return _style; }
            set { _style = value; Invalidate(); }
        }

        public int CornerRadius
        {
            get { return _cornerRadius; }
            set { _cornerRadius = value; Invalidate(); }
        }

        public ModernButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);

            Font = Theme.FontBtn;
            Cursor = Cursors.Hand;
            Size = new Size(140, 38);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovering = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovering = false; _pressing = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressing = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressing = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(1, 1, Width - 3, Height - 3);

            Color bg, border, text;

            if (!Enabled)
            {
                bg = Theme.BgCard;
                border = Theme.Border;
                text = Theme.TextMuted;
            }
            else
            {
                switch (_style)
                {
                    case ModernButtonStyle.Primary:
                        bg = _pressing ? Color.FromArgb(31, 111, 235) : (_hovering ? Theme.AccentBlueHover : Theme.AccentBlue);
                        border = bg;
                        text = Color.White;
                        break;
                    case ModernButtonStyle.Danger:
                        bg = _pressing ? Color.FromArgb(180, 40, 40) : (_hovering ? Theme.AccentRed : Theme.AccentRedDim);
                        border = _hovering ? Theme.AccentRedHover : Theme.AccentRed;
                        text = _hovering ? Color.White : Theme.AccentRed;
                        break;
                    case ModernButtonStyle.Secondary:
                    default:
                        bg = _pressing ? Theme.BgDark : (_hovering ? Theme.BgHover : Theme.BgCard);
                        border = _hovering ? Theme.BorderLight : Theme.Border;
                        text = _hovering ? Theme.TextPrimary : Theme.TextSecondary;
                        break;
                }
            }

            using (SolidBrush fill = new SolidBrush(bg))
                Theme.FillRoundedRect(g, fill, rect, _cornerRadius);

            using (Pen pen = new Pen(border, 1f))
                Theme.DrawRoundedRect(g, pen, rect, _cornerRadius);

            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            if (RightToLeft == RightToLeft.Yes)
                sf.FormatFlags = StringFormatFlags.DirectionRightToLeft;

            using (SolidBrush tb = new SolidBrush(text))
                g.DrawString(Text, Font, tb, new RectangleF(0, 0, Width, Height), sf);
        }
    }

    // ─── Vertical Sidebar Navigation Control ─────────────────────────────────
    class SidebarNavControl : Control
    {
        public class NavItem
        {
            public string Key { get; set; }
            public string Title { get; set; }
            public string IconGlyph { get; set; }

            public NavItem(string key, string title, string iconGlyph)
            {
                Key = key;
                Title = title;
                IconGlyph = iconGlyph;
            }
        }

        private List<NavItem> _items = new List<NavItem>();
        private int _selectedIndex = 0;
        private int _hoveredIndex = -1;
        private int _itemHeight = 46;

        public event EventHandler SelectedIndexChanged;

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (value >= 0 && value < _items.Count && _selectedIndex != value)
                {
                    _selectedIndex = value;
                    Invalidate();
                    if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
                }
            }
        }

        public SidebarNavControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Theme.BgPanel;
            Font = Theme.FontNormal;
            Cursor = Cursors.Hand;
            Width = 210;
        }

        public void SetItems(List<NavItem> items)
        {
            _items = items ?? new List<NavItem>();
            if (_selectedIndex >= _items.Count) _selectedIndex = 0;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = GetIndexAt(e.Y);
            if (idx != _hoveredIndex)
            {
                _hoveredIndex = idx;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoveredIndex = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int idx = GetIndexAt(e.Y);
            if (idx >= 0 && idx < _items.Count)
            {
                SelectedIndex = idx;
            }
            base.OnMouseDown(e);
        }

        private int GetIndexAt(int y)
        {
            int topPad = 12;
            int relY = y - topPad;
            if (relY < 0) return -1;
            int idx = relY / _itemHeight;
            return (idx >= 0 && idx < _items.Count) ? idx : -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Background
            using (SolidBrush bg = new SolidBrush(BackColor))
                g.FillRectangle(bg, 0, 0, Width, Height);

            bool isRtl = RightToLeft == RightToLeft.Yes;

            // Inner divider line between sidebar and page content
            using (Pen divPen = new Pen(Theme.Border))
            {
                if (isRtl)
                    g.DrawLine(divPen, 0, 0, 0, Height);
                else
                    g.DrawLine(divPen, Width - 1, 0, Width - 1, Height);
            }

            int curY = 12;
            int hMargin = 8;
            int itemW = Width - (hMargin * 2);

            for (int i = 0; i < _items.Count; i++)
            {
                NavItem item = _items[i];
                Rectangle itemRect = new Rectangle(hMargin, curY, itemW, _itemHeight - 4);

                bool selected = (i == _selectedIndex);
                bool hovered = (i == _hoveredIndex);

                if (selected)
                {
                    using (SolidBrush selBg = new SolidBrush(Theme.AccentBlueDim))
                        Theme.FillRoundedRect(g, selBg, itemRect, 6);

                    // Active indicator bar
                    using (SolidBrush bar = new SolidBrush(Theme.AccentBlue))
                    {
                        if (isRtl)
                            g.FillRectangle(bar, itemRect.Right - 3, itemRect.Y + 6, 3, itemRect.Height - 12);
                        else
                            g.FillRectangle(bar, itemRect.X, itemRect.Y + 6, 3, itemRect.Height - 12);
                    }
                }
                else if (hovered)
                {
                    using (SolidBrush hovBg = new SolidBrush(Theme.BgHover))
                        Theme.FillRoundedRect(g, hovBg, itemRect, 6);
                }

                Color textCol = selected ? Theme.AccentBlue : (hovered ? Theme.TextPrimary : Theme.TextSecondary);
                Font itemFont = selected ? Theme.FontBold : Theme.FontNormal;

                // Draw icon & text
                StringFormat sf = new StringFormat();
                sf.LineAlignment = StringAlignment.Center;
                sf.Alignment = StringAlignment.Near;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                if (isRtl)
                    sf.FormatFlags |= StringFormatFlags.DirectionRightToLeft;

                Rectangle textRect = new Rectangle(
                    itemRect.X + 14,
                    itemRect.Y,
                    itemRect.Width - 28,
                    itemRect.Height);

                string displayString = string.IsNullOrEmpty(item.IconGlyph) ?
                    item.Title :
                    (isRtl ? (item.Title + "  " + item.IconGlyph) : (item.IconGlyph + "  " + item.Title));

                using (SolidBrush tb = new SolidBrush(textCol))
                    g.DrawString(displayString, itemFont, tb, textRect, sf);

                curY += _itemHeight;
            }
        }
    }

    // ─── Card Panel Container ────────────────────────────────────────────────
    class CardPanel : Panel
    {
        private int _radius = 6;
        private Color? _topAccent = null;

        private int _topAccentHeight = 3;

        public int CornerRadius
        {
            get { return _radius; }
            set { _radius = value; Invalidate(); }
        }

        public Color? TopAccentColor
        {
            get { return _topAccent; }
            set { _topAccent = value; Invalidate(); }
        }

        public int TopAccentHeight
        {
            get { return _topAccentHeight; }
            set { _topAccentHeight = value; Invalidate(); }
        }

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Theme.BgCard;
            Padding = new Padding(16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(1, 1, Width - 3, Height - 3);

            using (SolidBrush bg = new SolidBrush(BackColor))
                Theme.FillRoundedRect(g, bg, rect, _radius);

            using (Pen border = new Pen(Theme.Border, 1f))
                Theme.DrawRoundedRect(g, border, rect, _radius);

            if (_topAccent.HasValue)
            {
                using (SolidBrush topBrush = new SolidBrush(_topAccent.Value))
                    g.FillRectangle(topBrush, 2, 2, Width - 4, _topAccentHeight);
            }
        }
    }

    // ─── Custom Dark ScrollBar (Restrained, Modern Dark Scrollbar) ───────────
    class DarkScrollBar : Control
    {
        private int _min = 0;
        private int _max = 100;
        private int _value = 0;
        private int _pageSize = 20;
        private bool _isDragging = false;
        private int _dragStartMouseY = 0;
        private int _dragStartValue = 0;
        private bool _isHovered = false;

        public event EventHandler ValueChanged;

        public int Minimum { get { return _min; } set { _min = value; Invalidate(); } }
        public int Maximum { get { return _max; } set { _max = Math.Max(_min, value); Invalidate(); } }
        public int PageSize { get { return _pageSize; } set { _pageSize = Math.Max(1, value); Invalidate(); } }
        public int Value
        {
            get { return _value; }
            set
            {
                int maxScroll = Math.Max(_min, _max - _pageSize);
                int clamped = Math.Max(_min, Math.Min(maxScroll, value));
                if (_value != clamped)
                {
                    _value = clamped;
                    Invalidate();
                    if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
                }
            }
        }

        public DarkScrollBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);
            Width = 8;
            BackColor = Color.Transparent;
        }

        private Rectangle GetThumbRect()
        {
            int range = _max - _min;
            if (range <= 0 || Height <= 0) return Rectangle.Empty;

            float viewRatio = (float)_pageSize / (float)range;
            if (viewRatio >= 1.0f) return Rectangle.Empty;

            int thumbH = Math.Max(24, (int)(Height * viewRatio));
            int availTrack = Height - thumbH;
            int maxScroll = range - _pageSize;
            if (maxScroll <= 0 || availTrack <= 0) return Rectangle.Empty;

            float posRatio = (float)(_value - _min) / (float)maxScroll;
            int thumbY = (int)(availTrack * posRatio);

            return new Rectangle(1, thumbY, Width - 2, thumbH);
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { if (!_isDragging) _isHovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Rectangle thumb = GetThumbRect();
                if (thumb != Rectangle.Empty && thumb.Contains(e.Location))
                {
                    _isDragging = true;
                    _dragStartMouseY = e.Y;
                    _dragStartValue = _value;
                }
                else if (thumb != Rectangle.Empty)
                {
                    if (e.Y < thumb.Y)
                        Value -= _pageSize;
                    else if (e.Y > thumb.Bottom)
                        Value += _pageSize;
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_isDragging)
            {
                int deltaY = e.Y - _dragStartMouseY;
                Rectangle thumb = GetThumbRect();
                int availTrack = Height - thumb.Height;
                int maxScroll = (_max - _min) - _pageSize;
                if (availTrack > 0 && maxScroll > 0)
                {
                    float deltaVal = ((float)deltaY / availTrack) * maxScroll;
                    Value = (int)(_dragStartValue + deltaVal);
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _isDragging = false;
            if (!ClientRectangle.Contains(e.Location)) _isHovered = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle thumb = GetThumbRect();
            if (thumb != Rectangle.Empty && thumb.Height > 0)
            {
                Color thumbColor;
                try
                {
                    var pal = UsbShieldPro.UI.ThemeManager.ActivePalette;
                    if (pal != null && !pal.IsDark)
                    {
                        thumbColor = _isDragging ? pal.Accent : (_isHovered ? pal.Border : Color.FromArgb(180, 188, 198));
                    }
                    else
                    {
                        thumbColor = _isDragging ? Theme.AccentBlue : (_isHovered ? Theme.BorderLight : Color.FromArgb(70, 80, 95));
                    }
                }
                catch
                {
                    thumbColor = _isDragging ? Theme.AccentBlue : (_isHovered ? Theme.BorderLight : Color.FromArgb(70, 80, 95));
                }
                using (SolidBrush brush = new SolidBrush(thumbColor))
                    Theme.FillRoundedRect(g, brush, thumb, 3);
            }
        }
    }

    // ─── Custom Dark Scroll Panel (Replaces white native Win32 scrollbar) ─────
    class DarkScrollPanel : Panel
    {
        private Panel _contentPanel;
        private DarkScrollBar _scrollBar;

        public Panel ContentPanel { get { return _contentPanel; } }
        public DarkScrollBar ScrollBar { get { return _scrollBar; } }

        public void ScrollTo(int value)
        {
            if (_scrollBar != null)
            {
                _scrollBar.Value = Math.Max(0, Math.Min(_scrollBar.Maximum, value));
                if (_contentPanel != null)
                {
                    _contentPanel.Top = -_scrollBar.Value;
                }
            }
        }

        public DarkScrollPanel()
        {
            AutoScroll = false;
            Color bg = (UsbShieldPro.UI.ThemeManager.ActivePalette != null) ? UsbShieldPro.UI.ThemeManager.ActivePalette.BackgroundPrimary : Theme.BgDark;
            BackColor = bg;

            _contentPanel = new Panel();
            _contentPanel.BackColor = bg;
            _contentPanel.Location = new Point(0, 0);
            _contentPanel.Size = new Size(Width, Height);

            _scrollBar = new DarkScrollBar();
            _scrollBar.Visible = false;
            _scrollBar.ValueChanged += delegate
            {
                _contentPanel.Top = -_scrollBar.Value;
            };

            Controls.Add(_contentPanel);
            Controls.Add(_scrollBar);

            _contentPanel.ControlAdded += delegate(object s, ControlEventArgs e)
            {
                HookWheel(e.Control);
                UpdateScroll();
            };

            _contentPanel.ControlRemoved += delegate { UpdateScroll(); };
            _contentPanel.Resize += delegate { UpdateScroll(); };

            UsbShieldPro.UI.ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (!IsDisposed)
            {
                Color bg = (UsbShieldPro.UI.ThemeManager.ActivePalette != null) ? UsbShieldPro.UI.ThemeManager.ActivePalette.BackgroundPrimary : Theme.BgDark;
                BackColor = bg;
                if (_contentPanel != null)
                {
                    _contentPanel.BackColor = bg;
                }
                if (IsHandleCreated)
                {
                    Invalidate();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UsbShieldPro.UI.ThemeManager.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }

        private void HookWheel(Control c)
        {
            if (c == null) return;
            c.MouseWheel += delegate(object s, MouseEventArgs e) { HandleWheel(e); };
            foreach (Control child in c.Controls)
            {
                HookWheel(child);
            }
            c.ControlAdded += delegate(object s, ControlEventArgs e) { HookWheel(e.Control); };
        }

        private void HandleWheel(MouseEventArgs e)
        {
            if (_scrollBar != null && _scrollBar.Visible)
            {
                int delta = (e.Delta / 120) * 48;
                _scrollBar.Value -= delta;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            HandleWheel(e);
        }

        private bool _isUpdatingScroll = false;

        public void UpdateScroll()
        {
            if (_isUpdatingScroll || _contentPanel == null || _scrollBar == null) return;
            try
            {
                _isUpdatingScroll = true;
                int maxBottom = 0;
                foreach (Control c in _contentPanel.Controls)
                {
                    if (c.Visible && c.Bottom > maxBottom)
                        maxBottom = c.Bottom;
                }

                int contentHeight = maxBottom + 28;
                bool isRtl = RightToLeft == RightToLeft.Yes;

                _scrollBar.Height = Height;
                if (isRtl)
                {
                    _scrollBar.Location = new Point(2, 0);
                    _contentPanel.Left = 14;
                    _contentPanel.Width = Math.Max(100, Width - 18);
                }
                else
                {
                    _scrollBar.Location = new Point(Width - 10, 0);
                    _contentPanel.Left = 0;
                    _contentPanel.Width = Math.Max(100, Width - 18);
                }

                if (contentHeight > Height)
                {
                    _scrollBar.Visible = true;
                    _scrollBar.Minimum = 0;
                    _scrollBar.Maximum = contentHeight;
                    _scrollBar.PageSize = Height;
                    _contentPanel.Height = contentHeight;
                }
                else
                {
                    _scrollBar.Visible = false;
                    _scrollBar.Value = 0;
                    _contentPanel.Top = 0;
                    _contentPanel.Height = Height;
                }
                _scrollBar.BringToFront();
            }
            finally
            {
                _isUpdatingScroll = false;
            }
        }

        public void ScrollToBottom()
        {
            if (_scrollBar != null && _scrollBar.Visible)
            {
                _scrollBar.Value = Math.Max(0, _scrollBar.Maximum - _scrollBar.PageSize);
                _contentPanel.Top = -_scrollBar.Value;
            }
        }

        public void ScrollToTop()
        {
            if (_scrollBar != null)
            {
                _scrollBar.Value = 0;
                _contentPanel.Top = 0;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            UpdateScroll();
            base.OnResize(e);
        }
    }

    // ─── Truthful Semantic Badge ─────────────────────────────────────────────
    class TruthfulBadge : Control
    {
        private string _text = "";
        private Color _badgeColor = Theme.AccentBlue;

        public string BadgeText
        {
            get { return _text; }
            set { _text = value ?? ""; Invalidate(); AdjustSize(); }
        }

        public Color BadgeColor
        {
            get { return _badgeColor; }
            set { _badgeColor = value; Invalidate(); }
        }

        public TruthfulBadge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);

            Font = Theme.FontSmall;
            Height = 24;
            Width = 100;
        }

        private void AdjustSize()
        {
            if (string.IsNullOrEmpty(_text)) return;
            using (Graphics g = CreateGraphics())
            {
                SizeF sz = g.MeasureString(_text, Font);
                Width = (int)sz.Width + 24;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (string.IsNullOrEmpty(_text)) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 1, Width - 1, Height - 2);
            int radius = Math.Min(11, (Height - 2) / 2);

            Color dimBg = Color.FromArgb(24, _badgeColor.R, _badgeColor.G, _badgeColor.B);
            using (SolidBrush bg = new SolidBrush(dimBg))
                Theme.FillRoundedRect(g, bg, rect, radius);

            using (Pen p = new Pen(Color.FromArgb(65, _badgeColor.R, _badgeColor.G, _badgeColor.B), 1f))
                Theme.DrawRoundedRect(g, p, rect, radius);

            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            if (RightToLeft == RightToLeft.Yes)
                sf.FormatFlags = StringFormatFlags.DirectionRightToLeft;

            using (SolidBrush tb = new SolidBrush(_badgeColor))
                g.DrawString(_text, Font, tb, rect, sf);
        }
    }

    // ─── Status Dot Indicator ────────────────────────────────────────────────
    class StatusIndicator : Control
    {
        private Color _color = Theme.AccentGreen;
        public Color StatusColor
        {
            get { return _color; }
            set { _color = value; Invalidate(); }
        }

        public StatusIndicator()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(18, 18);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int sz = Math.Min(Width, Height) - 4;
            int x = (Width - sz) / 2;
            int y = (Height - sz) / 2;

            using (SolidBrush glow = new SolidBrush(Color.FromArgb(50, _color)))
                g.FillEllipse(glow, x - 2, y - 2, sz + 4, sz + 4);

            using (SolidBrush dot = new SolidBrush(_color))
                g.FillEllipse(dot, x, y, sz, sz);
        }
    }

    // ─── Toggle Switch ───────────────────────────────────────────────────────
    class ToggleSwitch : Control
    {
        private bool _checked;
        private bool _hovering;
        private Timer _animTimer;
        private float _animPos;

        private string _labelText = "";

        public event EventHandler CheckedChanged;

        public string LabelText
        {
            get { return _labelText; }
            set { _labelText = value ?? ""; Invalidate(); }
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    _animTimer.Start();
                    if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
                }
            }
        }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(46, 24);
            Cursor = Cursors.Hand;
            _animPos = _checked ? 1f : 0f;

            _animTimer = new Timer();
            _animTimer.Interval = 16;
            _animTimer.Tick += delegate
            {
                float target = _checked ? 1f : 0f;
                _animPos += (target - _animPos) * 0.35f;
                if (Math.Abs(_animPos - target) < 0.05f)
                {
                    _animPos = target;
                    _animTimer.Stop();
                }
                Invalidate();
            };
        }

        protected override void OnMouseEnter(EventArgs e) { _hovering = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovering = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool hasLabel = !string.IsNullOrEmpty(_labelText);
            bool isRtl = RightToLeft == RightToLeft.Yes;

            int switchW = 46;
            int switchH = 24;
            int switchX = 0;
            int switchY = (Height - switchH) / 2;

            if (hasLabel)
            {
                if (isRtl)
                {
                    switchX = 4;
                    int textX = switchW + 14;
                    int textW = Width - textX - 4;
                    StringFormat sf = new StringFormat();
                    sf.LineAlignment = StringAlignment.Center;
                    sf.Alignment = StringAlignment.Near;
                    sf.FormatFlags = StringFormatFlags.DirectionRightToLeft;
                    using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
                        g.DrawString(_labelText, Theme.FontNormal, tb, new Rectangle(textX, 0, textW, Height), sf);
                }
                else
                {
                    switchX = Width - switchW - 4;
                    int textW = switchX - 10;
                    StringFormat sf = new StringFormat();
                    sf.LineAlignment = StringAlignment.Center;
                    sf.Alignment = StringAlignment.Near;
                    using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
                        g.DrawString(_labelText, Theme.FontNormal, tb, new Rectangle(4, 0, textW, Height), sf);
                }
            }

            Rectangle track = new Rectangle(switchX, switchY + 2, switchW, switchH - 4);
            int radius = track.Height / 2;

            Color activeTrack = Theme.AccentBlue;
            Color inactiveTrack = Theme.BgHover;
            Color currentTrack = Theme.BlendColor(inactiveTrack, activeTrack, _animPos);

            using (SolidBrush tb = new SolidBrush(currentTrack))
                Theme.FillRoundedRect(g, tb, track, radius);

            using (Pen borderPen = new Pen(_checked ? activeTrack : (_hovering ? Theme.BorderLight : Theme.Border), 1f))
                Theme.DrawRoundedRect(g, borderPen, track, radius);

            // Thumb
            int thumbSize = track.Height - 4;
            int travel = track.Width - thumbSize - 4;
            int thumbX = track.X + 2 + (int)(travel * _animPos);
            Rectangle thumbRect = new Rectangle(thumbX, track.Y + 2, thumbSize, thumbSize);

            using (SolidBrush thumbBrush = new SolidBrush(Color.White))
                g.FillEllipse(thumbBrush, thumbRect);
        }
    }

    // ─── Protection Rule Toggle Card ─────────────────────────────────────────
    class RuleToggleCard : CardPanel
    {
        private ToggleSwitch _toggle;
        private Label _titleLabel;
        private Label _descLabel;
        private TruthfulBadge _badge;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _toggle != null && _toggle.Checked; }
            set { if (_toggle != null) _toggle.Checked = value; }
        }

        public string RuleTitle
        {
            get { return _titleLabel != null ? _titleLabel.Text : ""; }
            set { if (_titleLabel != null) _titleLabel.Text = value; }
        }

        public string RuleDescription
        {
            get { return _descLabel != null ? _descLabel.Text : ""; }
            set { if (_descLabel != null) _descLabel.Text = value; }
        }

        public string BadgeText
        {
            get { return _badge != null ? _badge.BadgeText : ""; }
            set { if (_badge != null) _badge.BadgeText = value; }
        }

        public Color BadgeColor
        {
            get { return _badge != null ? _badge.BadgeColor : Theme.AccentBlue; }
            set { if (_badge != null) _badge.BadgeColor = value; }
        }

        public RuleToggleCard(string title, string description, string badgeText, Color badgeColor)
        {
            Padding = new Padding(16, 12, 16, 12);
            BackColor = Theme.BgCard;

            _toggle = new ToggleSwitch();
            _toggle.CheckedChanged += delegate { if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); };

            _titleLabel = new Label();
            _titleLabel.Text = title;
            _titleLabel.Font = Theme.FontBold;
            _titleLabel.ForeColor = Theme.TextPrimary;
            _titleLabel.AutoSize = false;

            _descLabel = new Label();
            _descLabel.Text = description;
            _descLabel.Font = Theme.FontSmall;
            _descLabel.ForeColor = Theme.TextSecondary;
            _descLabel.AutoSize = false;

            _badge = new TruthfulBadge();
            _badge.BadgeText = badgeText;
            _badge.BadgeColor = badgeColor;

            Controls.Add(_toggle);
            Controls.Add(_titleLabel);
            Controls.Add(_descLabel);
            Controls.Add(_badge);

            Height = 78;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutElements();
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            LayoutElements();
        }

        private void LayoutElements()
        {
            if (_toggle == null || _badge == null || _titleLabel == null || _descLabel == null) return;

            bool isRtl = Loc.IsRtl;
            int pad = 14;

            if (isRtl)
            {
                // Toggle on the left side
                _toggle.Location = new Point(pad, (Height - _toggle.Height) / 2);

                // Badge next to toggle
                _badge.Location = new Point(_toggle.Right + 12, (Height - _badge.Height) / 2);

                // Title & Description aligned right
                int textLeft = _badge.Right + 16;
                int textWidth = Width - pad - textLeft;
                if (textWidth < 120) textWidth = 120;

                _titleLabel.TextAlign = ContentAlignment.MiddleRight;
                _titleLabel.Location = new Point(textLeft, 14);
                _titleLabel.Size = new Size(textWidth, 22);

                _descLabel.TextAlign = ContentAlignment.TopRight;
                _descLabel.Location = new Point(textLeft, 38);
                _descLabel.Size = new Size(textWidth, Height - 44);
            }
            else
            {
                // Toggle on the right side
                _toggle.Location = new Point(Width - pad - _toggle.Width, (Height - _toggle.Height) / 2);

                // Badge next to toggle
                _badge.Location = new Point(_toggle.Left - 12 - _badge.Width, (Height - _badge.Height) / 2);

                // Title & Description aligned left
                int textWidth = _badge.Left - 16 - pad;
                if (textWidth < 120) textWidth = 120;

                _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
                _titleLabel.Location = new Point(pad, 14);
                _titleLabel.Size = new Size(textWidth, 22);

                _descLabel.TextAlign = ContentAlignment.TopLeft;
                _descLabel.Location = new Point(pad, 38);
                _descLabel.Size = new Size(textWidth, Height - 44);
            }
        }
    }

    // ─── Large Clickable Selection Card ───────────────────────────────────────
    class SelectableOptionCard : Control
    {
        private bool _selected;
        private bool _hovering;
        private string _title = "";
        private string _desc = "";
        private string _badge = "";
        private int _radius = 6;

        public event EventHandler SelectionChanged;

        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    Invalidate();
                    if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
                }
            }
        }

        public string CardTitle
        {
            get { return _title; }
            set { _title = value ?? ""; Invalidate(); }
        }

        public string CardDescription
        {
            get { return _desc; }
            set { _desc = value ?? ""; Invalidate(); }
        }

        public string BadgeText
        {
            get { return _badge; }
            set { _badge = value ?? ""; Invalidate(); }
        }

        public SelectableOptionCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);

            Font = Theme.FontNormal;
            Cursor = Cursors.Hand;
            Size = new Size(580, 80);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovering = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovering = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e)
        {
            Selected = true;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(1, 1, Width - 3, Height - 3);

            Color bg = _selected ? Theme.BgElevated : (_hovering ? Theme.BgHover : Theme.BgCard);
            Color border = _selected ? Theme.AccentBlue : (_hovering ? Theme.BorderLight : Theme.Border);

            using (SolidBrush b = new SolidBrush(bg))
                Theme.FillRoundedRect(g, b, rect, _radius);

            using (Pen p = new Pen(border, _selected ? 1.5f : 1f))
                Theme.DrawRoundedRect(g, p, rect, _radius);

            bool isRtl = RightToLeft == RightToLeft.Yes;

            // Radio indicator ring
            int radioSize = 18;
            int radioY = (Height - radioSize) / 2;
            int radioX = isRtl ? (Width - 18 - radioSize) : 18;
            Rectangle radioRect = new Rectangle(radioX, radioY, radioSize, radioSize);

            using (Pen rp = new Pen(_selected ? Theme.AccentBlue : Theme.BorderLight, 1.5f))
                g.DrawEllipse(rp, radioRect);

            if (_selected)
            {
                Rectangle innerDot = new Rectangle(radioX + 4, radioY + 4, radioSize - 8, radioSize - 8);
                using (SolidBrush dot = new SolidBrush(Theme.AccentBlue))
                    g.FillEllipse(dot, innerDot);
            }

            // Text layout
            int textX = isRtl ? 16 : (radioX + radioSize + 16);
            int textW = Width - radioSize - 50;

            StringFormat sfTitle = new StringFormat();
            sfTitle.LineAlignment = StringAlignment.Center;
            sfTitle.Alignment = StringAlignment.Near;
            if (isRtl)
                sfTitle.FormatFlags = StringFormatFlags.DirectionRightToLeft;

            Rectangle titleRect = new Rectangle(textX, 12, textW, 24);
            using (SolidBrush tb = new SolidBrush(_selected ? Theme.AccentBlue : Theme.TextPrimary))
                g.DrawString(_title, Theme.FontBold, tb, titleRect, sfTitle);

            // Optional Badge
            if (!string.IsNullOrEmpty(_badge))
            {
                int badgeW = 76;
                int badgeX = isRtl ? 16 : (Width - 16 - badgeW);
                Rectangle bRect = new Rectangle(badgeX, 14, badgeW, 20);
                using (SolidBrush bBg = new SolidBrush(Theme.AccentGreenDim))
                    Theme.FillRoundedRect(g, bBg, bRect, 4);
                using (Pen bPen = new Pen(Theme.AccentGreen, 1f))
                    Theme.DrawRoundedRect(g, bPen, bRect, 4);

                StringFormat bSf = new StringFormat();
                bSf.Alignment = StringAlignment.Center;
                bSf.LineAlignment = StringAlignment.Center;
                using (SolidBrush bTb = new SolidBrush(Theme.AccentGreen))
                    g.DrawString(_badge, Theme.FontSmall, bTb, bRect, bSf);
            }

            // Description
            StringFormat sfDesc = new StringFormat();
            sfDesc.Alignment = StringAlignment.Near;
            if (isRtl)
                sfDesc.FormatFlags = StringFormatFlags.DirectionRightToLeft;
            Rectangle descRect = new Rectangle(textX, 38, textW, Height - 44);
            using (SolidBrush db = new SolidBrush(Theme.TextSecondary))
                g.DrawString(_desc, Theme.FontSmall, db, descRect, sfDesc);
        }
    }

    // ─── Connected Device Card Control ───────────────────────────────────────
    class DeviceCardControl : CardPanel
    {
        private DeviceIdentity _device;
        private Label _nameLabel;
        private Label _metaLabel;
        private Label _volumesLabel;
        private Label _systemNoticeLabel;
        private TruthfulBadge _statusBadge;
        private TruthfulBadge _trustBadge;
        private ModernButton _ejectBtn;
        private ModernButton _trustBtn;
        private ModernButton _detailsBtn;

        public event EventHandler EjectRequested;
        public event EventHandler TrustRequested;
        public event EventHandler DetailsRequested;

        public DeviceIdentity Device { get { return _device; } }

        public DeviceCardControl(DeviceIdentity device, bool isTrusted)
        {
            _device = device;
            Padding = new Padding(16, 12, 16, 12);
            BackColor = Theme.BgCard;

            bool isSystem = (device.IsSystemDisk ||
                             device.SafetyResult == SafetyValidationResult.RejectedSystemOrBootDisk ||
                             device.Category == DeviceCategory.InternalFixedDisk ||
                             device.Category == DeviceCategory.CriticalSystemHardware);

            // Row 1: Hardware Model Name
            _nameLabel = new Label();
            string cleanName = device.GetCleanDisplayName();
            _nameLabel.Text = cleanName;
            _nameLabel.Font = Theme.FontLarge;
            _nameLabel.ForeColor = Theme.TextPrimary;
            _nameLabel.AutoSize = true;

            // Row 2: Metadata (Type • Connection Bus • Capacity)
            _metaLabel = new Label();
            string catText = GetDeviceCategoryText(device.Category);
            string busText = string.Format(Loc.Get("ConnectionBus"), device.PhysicalTransport);
            string capText = device.TotalSizeBytes > 0 ? string.Format(Loc.Get("CapacityGB"), device.TotalSizeBytes / (1024 * 1024 * 1024.0)) : "";
            
            List<string> metaParts = new List<string>();
            if (!string.IsNullOrEmpty(catText)) metaParts.Add(catText);
            if (!string.IsNullOrEmpty(busText)) metaParts.Add(busText);
            if (!string.IsNullOrEmpty(capText)) metaParts.Add(capText);
            _metaLabel.Text = string.Join("  •  ", metaParts.ToArray());
            _metaLabel.Font = Theme.FontSmall;
            _metaLabel.ForeColor = isSystem ? Theme.TextMuted : Theme.TextSecondary;
            _metaLabel.AutoSize = true;

            // Row 3: Partitions / Volumes or System Excluded Notice
            _volumesLabel = new Label();
            string vols = device.GetVolumesDisplayString();
            if (!string.IsNullOrEmpty(vols))
            {
                _volumesLabel.Text = Loc.Get("PartitionsLabel") + ":  " + vols;
                _volumesLabel.ForeColor = Theme.AccentBlue;
                _volumesLabel.Visible = true;
            }
            else
            {
                _volumesLabel.Text = "";
                _volumesLabel.Visible = false;
            }
            _volumesLabel.Font = Theme.FontSmall;
            _volumesLabel.AutoSize = true;

            _systemNoticeLabel = new Label();
            _systemNoticeLabel.Text = Loc.Get("SystemDeviceNotice");
            _systemNoticeLabel.Font = Theme.FontSmall;
            _systemNoticeLabel.ForeColor = Theme.TextMuted;
            _systemNoticeLabel.Visible = false;
            _systemNoticeLabel.AutoSize = false;
            _systemNoticeLabel.Location = new Point(-2000, -2000);

            // Badges
            _statusBadge = new TruthfulBadge();
            _trustBadge = new TruthfulBadge();

            if (isSystem)
            {
                _statusBadge.BadgeText = Loc.Get("BadgeSystemExcluded");
                _statusBadge.BadgeColor = Theme.TextMuted;
                _trustBadge.Visible = false;
                _systemNoticeLabel.Visible = !_volumesLabel.Visible;
            }
            else
            {
                _trustBadge.BadgeText = isTrusted ? Loc.Get("BadgeTrusted") : Loc.Get("BadgeUntrusted");
                _trustBadge.BadgeColor = isTrusted ? Theme.AccentGreen : Theme.TextSecondary;
                _trustBadge.Visible = true;

                string physKey = DeviceEnforcementLifecycleManager.BuildPhysicalKey(device);
                DeviceLifecycleRecord rec = DeviceEnforcementLifecycleManager.GetLifecycleSnapshot(physKey);
                if (rec != null)
                {
                    if (rec.State == EnforcementState.RetryWaiting)
                    {
                        _statusBadge.BadgeText = Loc.Get("SafeEjectPreparing");
                        _statusBadge.BadgeColor = Theme.AccentOrange;
                    }
                    else if (rec.State == EnforcementState.EjectedUntilRemoval)
                    {
                        _statusBadge.BadgeText = Loc.Get("SafeEjectSuccessStatus");
                        _statusBadge.BadgeColor = Theme.AccentGreen;
                    }
                    else if (rec.State == EnforcementState.FailedTerminal)
                    {
                        _statusBadge.BadgeText = Loc.Get("SafeEjectFailedStatus");
                        _statusBadge.BadgeColor = Theme.AccentRed;
                    }
                    else
                    {
                        _statusBadge.BadgeText = isTrusted ? Loc.Get("BadgeVerified") : Loc.Get("BadgeReactiveAutoEject");
                        _statusBadge.BadgeColor = isTrusted ? Theme.AccentGreen : Theme.AccentOrange;
                    }
                }
                else
                {
                    _statusBadge.BadgeText = isTrusted ? Loc.Get("BadgeVerified") : Loc.Get("BadgeReactiveAutoEject");
                    _statusBadge.BadgeColor = isTrusted ? Theme.AccentGreen : Theme.AccentOrange;
                }
                _statusBadge.Visible = true;
            }

            // Row 4: Action Toolbar
            _detailsBtn = new ModernButton();
            _detailsBtn.Text = Loc.Get("ViewDetails");
            _detailsBtn.ButtonStyle = ModernButtonStyle.Secondary;
            _detailsBtn.Size = new Size(84, 30);
            _detailsBtn.Click += delegate { if (DetailsRequested != null) DetailsRequested(this, EventArgs.Empty); };

            _trustBtn = new ModernButton();
            _trustBtn.Text = isTrusted ? Loc.Get("RevokeTrust") : Loc.Get("TrustDevice");
            _trustBtn.ButtonStyle = isTrusted ? ModernButtonStyle.Danger : ModernButtonStyle.Secondary;
            _trustBtn.Size = new Size(125, 30);
            _trustBtn.Visible = !isSystem;
            _trustBtn.Click += delegate { if (TrustRequested != null) TrustRequested(this, EventArgs.Empty); };

            _ejectBtn = new ModernButton();
            _ejectBtn.Text = Loc.Get("SafelyEject");
            _ejectBtn.ButtonStyle = ModernButtonStyle.Secondary;
            _ejectBtn.Size = new Size(110, 30);
            _ejectBtn.Visible = !isSystem;
            _ejectBtn.Click += delegate { if (EjectRequested != null) EjectRequested(this, EventArgs.Empty); };

            Controls.Add(_nameLabel);
            Controls.Add(_metaLabel);
            Controls.Add(_volumesLabel);
            Controls.Add(_systemNoticeLabel);
            Controls.Add(_statusBadge);
            Controls.Add(_trustBadge);
            Controls.Add(_detailsBtn);
            Controls.Add(_trustBtn);
            Controls.Add(_ejectBtn);

            Height = 132;
        }

        private string GetDeviceCategoryText(DeviceCategory cat)
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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutElements();
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            LayoutElements();
        }

        private void LayoutElements()
        {
            if (_nameLabel == null || _metaLabel == null || _detailsBtn == null) return;

            bool isRtl = Loc.IsRtl;
            int pad = 16;
            int availW = Math.Max(240, ClientSize.Width);

            bool isSystem = (_device != null && (_device.IsSystemDisk ||
                             _device.SafetyResult == SafetyValidationResult.RejectedSystemOrBootDisk ||
                             _device.Category == DeviceCategory.InternalFixedDisk ||
                             _device.Category == DeviceCategory.CriticalSystemHardware));

            if (isRtl)
            {
                // ROW 1: Badges on left, Name on right
                int badgeX = pad;
                if (_trustBadge.Visible)
                {
                    _trustBadge.Location = new Point(badgeX, 12);
                    badgeX += _trustBadge.Width + 8;
                }
                if (_statusBadge.Visible)
                {
                    _statusBadge.Location = new Point(badgeX, 12);
                    badgeX += _statusBadge.Width + 8;
                }

                int maxNameW = Math.Max(100, availW - badgeX - pad - 12);
                _nameLabel.MaximumSize = new Size(maxNameW, 24);
                _nameLabel.Location = new Point(availW - pad - _nameLabel.Width, 12);

                // ROW 2: Metadata (Type • Connection • Capacity)
                int metaY = 38;
                _metaLabel.Location = new Point(availW - pad - _metaLabel.Width, metaY);

                // ROW 3: Volumes or System Notice
                int curY = 62;
                if (_volumesLabel.Visible)
                {
                    _volumesLabel.Location = new Point(availW - pad - _volumesLabel.Width, curY);
                    curY += 24;
                }
                else if (isSystem && _systemNoticeLabel.Visible)
                {
                    _systemNoticeLabel.Location = new Point(availW - pad - Math.Min(availW - 120, 500), curY);
                    _systemNoticeLabel.Size = new Size(Math.Min(availW - 120, 500), 20);
                    curY += 24;
                }

                // ROW 4: Action Toolbar (aligned on left)
                int btnY = Math.Max(curY, Height - 40);
                int btnX = pad;
                _detailsBtn.Location = new Point(btnX, btnY);
                btnX += _detailsBtn.Width + 8;

                if (_trustBtn.Visible)
                {
                    _trustBtn.Location = new Point(btnX, btnY);
                    btnX += _trustBtn.Width + 8;
                }

                if (_ejectBtn.Visible)
                {
                    _ejectBtn.Location = new Point(btnX, btnY);
                }
            }
            else
            {
                // ROW 1: Name on left, Badges on right
                int badgeX = availW - pad;
                if (_statusBadge.Visible)
                {
                    badgeX -= _statusBadge.Width;
                    _statusBadge.Location = new Point(badgeX, 12);
                    badgeX -= 8;
                }
                if (_trustBadge.Visible)
                {
                    badgeX -= _trustBadge.Width;
                    _trustBadge.Location = new Point(badgeX, 12);
                    badgeX -= 8;
                }

                int maxNameW = Math.Max(100, badgeX - pad - 12);
                _nameLabel.MaximumSize = new Size(maxNameW, 24);
                _nameLabel.Location = new Point(pad, 12);

                // ROW 2: Metadata (Type • Connection • Capacity)
                int metaY = 38;
                _metaLabel.Location = new Point(pad, metaY);

                // ROW 3: Volumes or System Notice
                int curY = 62;
                if (_volumesLabel.Visible)
                {
                    _volumesLabel.Location = new Point(pad, curY);
                    curY += 24;
                }
                else if (isSystem && _systemNoticeLabel.Visible)
                {
                    _systemNoticeLabel.Location = new Point(pad, curY);
                    _systemNoticeLabel.Size = new Size(Math.Min(availW - 120, 500), 20);
                    curY += 24;
                }

                // ROW 4: Action Toolbar (aligned on right)
                int btnY = Math.Max(curY, Height - 40);
                int btnX = availW - pad;
                if (_ejectBtn.Visible)
                {
                    btnX -= _ejectBtn.Width;
                    _ejectBtn.Location = new Point(btnX, btnY);
                    btnX -= 8;
                }
                if (_trustBtn.Visible)
                {
                    btnX -= _trustBtn.Width;
                    _trustBtn.Location = new Point(btnX, btnY);
                    btnX -= 8;
                }
                btnX -= _detailsBtn.Width;
                _detailsBtn.Location = new Point(btnX, btnY);
            }
        }
    }

    // ─── Countdown Progress Bar ──────────────────────────────────────────────
    class CountdownProgressBar : Control
    {
        private int _max = 100;
        private int _val = 0;
        private string _displayTime = "";

        public int Maximum { get { return _max; } set { _max = Math.Max(1, value); Invalidate(); } }
        public int Value { get { return _val; } set { _val = Math.Max(0, Math.Min(_max, value)); Invalidate(); } }
        public string DisplayTime { get { return _displayTime; } set { _displayTime = value ?? ""; Invalidate(); } }

        public CountdownProgressBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);

            Font = Theme.FontBold;
            Height = 26;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle track = new Rectangle(1, 1, Width - 3, Height - 3);
            using (SolidBrush bg = new SolidBrush(Theme.BgCard))
                Theme.FillRoundedRect(g, bg, track, 4);

            float pct = _max > 0 ? ((float)_val / _max) : 0f;
            int fillW = (int)(track.Width * pct);
            if (fillW > 0)
            {
                Rectangle fillRect = new Rectangle(track.X, track.Y, fillW, track.Height);
                using (SolidBrush b = new SolidBrush(Theme.AccentOrangeDim))
                    Theme.FillRoundedRect(g, b, fillRect, 4);
            }

            using (Pen p = new Pen(Theme.Border, 1f))
                Theme.DrawRoundedRect(g, p, track, 4);

            if (!string.IsNullOrEmpty(_displayTime))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                using (SolidBrush tb = new SolidBrush(Theme.AccentOrange))
                    g.DrawString(_displayTime, Font, tb, track, sf);
            }
        }
    }

    // ─── Section Header Label ────────────────────────────────────────────────
    class SectionLabel : Label
    {
        public SectionLabel()
        {
            Font = Theme.FontLarge;
            ForeColor = Theme.TextPrimary;
            AutoSize = false;
            Height = 28;
        }
    }

    // ─── Native Drag Support ─────────────────────────────────────────────────
    static class NativeDragHelper
    {
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        public static void DragWindow(IntPtr handle, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(handle, 0xA1, 0x2, 0);
            }
        }
    }

    // ─── Dark Modal Message Box ──────────────────────────────────────────────
    class MsgBox : Form
    {
        public static DialogResult Show(IWin32Window owner, string message, string title, MessageBoxButtons buttons = MessageBoxButtons.OK)
        {
            using (MsgBox dlg = new MsgBox(message, title, buttons))
            {
                return dlg.ShowDialog(owner);
            }
        }

        private MsgBox(string message, string title, MessageBoxButtons buttons)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            TopMost = false;
            ShowInTaskbar = false;
            BackColor = Theme.BgDark;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FontNormal;
            RightToLeft = Loc.RtlValue;
            Size = new Size(460, 200);

            // Title bar
            Panel titleBar = new Panel();
            titleBar.Dock = DockStyle.Top;
            titleBar.Height = 40;
            titleBar.BackColor = Theme.BgPanel;
            titleBar.MouseDown += delegate(object s, MouseEventArgs e) { NativeDragHelper.DragWindow(Handle, e); };

            Label titleLbl = new Label();
            titleLbl.Text = title;
            titleLbl.Font = Theme.FontBold;
            titleLbl.ForeColor = Theme.TextPrimary;
            titleLbl.Location = new Point(14, 10);
            titleLbl.AutoSize = true;
            titleBar.Controls.Add(titleLbl);
            Controls.Add(titleBar);

            // Content
            Label msgLbl = new Label();
            msgLbl.Text = message;
            msgLbl.Font = Theme.FontNormal;
            msgLbl.ForeColor = Theme.TextSecondary;
            msgLbl.Location = new Point(18, 54);
            msgLbl.Size = new Size(424, 80);
            Controls.Add(msgLbl);

            // Buttons
            Panel btnPanel = new Panel();
            btnPanel.Dock = DockStyle.Bottom;
            btnPanel.Height = 52;
            btnPanel.BackColor = Theme.BgDark;

            if (buttons == MessageBoxButtons.YesNo)
            {
                ModernButton yesBtn = new ModernButton();
                yesBtn.Text = (RightToLeft == RightToLeft.Yes) ? "نعم" : "Yes";
                yesBtn.ButtonStyle = ModernButtonStyle.Primary;
                yesBtn.Size = new Size(90, 34);
                yesBtn.Location = new Point(Width - 110, 8);
                yesBtn.Click += delegate { DialogResult = DialogResult.Yes; Close(); };

                ModernButton noBtn = new ModernButton();
                noBtn.Text = (RightToLeft == RightToLeft.Yes) ? "لا" : "No";
                noBtn.ButtonStyle = ModernButtonStyle.Secondary;
                noBtn.Size = new Size(90, 34);
                noBtn.Location = new Point(Width - 210, 8);
                noBtn.Click += delegate { DialogResult = DialogResult.No; Close(); };

                btnPanel.Controls.Add(yesBtn);
                btnPanel.Controls.Add(noBtn);
            }
            else
            {
                ModernButton okBtn = new ModernButton();
                okBtn.Text = (RightToLeft == RightToLeft.Yes) ? "موافق" : "OK";
                okBtn.ButtonStyle = ModernButtonStyle.Primary;
                okBtn.Size = new Size(90, 34);
                okBtn.Location = new Point(Width - 110, 8);
                okBtn.Click += delegate { DialogResult = DialogResult.OK; Close(); };
                btnPanel.Controls.Add(okBtn);
            }

            Controls.Add(btnPanel);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Theme.Border, 1f))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    // ─── Dark Password Authentication Dialog ─────────────────────────────────
    class PasswordDialog : Form
    {
        private TextBox _pwBox;
        private Label _errorLabel;
        private string _actionPrompt;
        private bool _verifyCurrent;
        public string EnteredPassword { get; private set; }
        public bool UsedRecoveryKey { get; private set; }

        public static bool Prompt(IWin32Window owner, string actionName)
        {
            if (!PasswordManager.IsPasswordConfigured()) return true;
            using (PasswordDialog dlg = new PasswordDialog(Loc.Get("PasswordRequired"), actionName, true))
            {
                return dlg.ShowDialog(owner) == DialogResult.OK;
            }
        }

        public PasswordDialog(string actionPrompt) : this(Loc.Get("PasswordRequired"), actionPrompt, true)
        {
        }

        public PasswordDialog(string title, string actionPrompt, bool verifyCurrent = true)
        {
            _actionPrompt = actionPrompt;
            _verifyCurrent = verifyCurrent;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            TopMost = false;
            ShowInTaskbar = false;
            BackColor = Theme.BgDark;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FontNormal;
            RightToLeft = Loc.RtlValue;
            Size = new Size(460, 240);

            // Title bar
            Panel titleBar = new Panel();
            titleBar.Dock = DockStyle.Top;
            titleBar.Height = 40;
            titleBar.BackColor = Theme.BgPanel;
            titleBar.MouseDown += delegate(object s, MouseEventArgs e) { NativeDragHelper.DragWindow(Handle, e); };

            Label titleLbl = new Label();
            titleLbl.Text = title ?? Loc.Get("PasswordRequired");
            titleLbl.Font = Theme.FontBold;
            titleLbl.ForeColor = Theme.TextPrimary;
            titleLbl.Location = new Point(16, 10);
            titleLbl.AutoSize = true;
            titleBar.Controls.Add(titleLbl);
            Controls.Add(titleBar);

            // Prompt
            Label descLbl = new Label();
            descLbl.Text = _verifyCurrent ? (Loc.Get("EnterPassword") + "\n(" + _actionPrompt + ")") : _actionPrompt;
            descLbl.ForeColor = Theme.TextSecondary;
            descLbl.Location = new Point(20, 52);
            descLbl.Size = new Size(420, 36);
            Controls.Add(descLbl);

            // TextBox
            _pwBox = new TextBox();
            _pwBox.PasswordChar = '\u2022';
            _pwBox.BackColor = Theme.BgCard;
            _pwBox.ForeColor = Theme.TextPrimary;
            _pwBox.BorderStyle = BorderStyle.FixedSingle;
            _pwBox.Font = new Font("Segoe UI", 11f);
            _pwBox.Location = new Point(20, 96);
            _pwBox.Size = new Size(420, 30);
            _pwBox.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) Submit();
            };
            Controls.Add(_pwBox);

            // Error message
            _errorLabel = new Label();
            _errorLabel.ForeColor = Theme.AccentRed;
            _errorLabel.Font = Theme.FontSmall;
            _errorLabel.Location = new Point(20, 134);
            _errorLabel.Size = new Size(420, 20);
            Controls.Add(_errorLabel);

            // Buttons
            ModernButton submitBtn = new ModernButton();
            submitBtn.Text = (RightToLeft == RightToLeft.Yes) ? "تأكيد" : "Confirm";
            submitBtn.ButtonStyle = ModernButtonStyle.Primary;
            submitBtn.Size = new Size(100, 34);
            submitBtn.Location = new Point(Width - 120, 180);
            submitBtn.Click += delegate { Submit(); };

            ModernButton cancelBtn = new ModernButton();
            cancelBtn.Text = (RightToLeft == RightToLeft.Yes) ? "إلغاء" : "Cancel";
            cancelBtn.ButtonStyle = ModernButtonStyle.Secondary;
            cancelBtn.Size = new Size(90, 34);
            cancelBtn.Location = new Point(Width - 220, 180);
            cancelBtn.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(submitBtn);
            Controls.Add(cancelBtn);
        }

        private void Submit()
        {
            string input = _pwBox.Text;
            if (string.IsNullOrEmpty(input))
            {
                _errorLabel.Text = Loc.Get("WrongPassword");
                return;
            }

            EnteredPassword = input;

            if (!_verifyCurrent)
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            if (PasswordManager.VerifyPassword(input))
            {
                UsedRecoveryKey = false;
                DialogResult = DialogResult.OK;
                Close();
            }
            else if (RecoveryKeyManager.VerifyMasterRecoveryKey(input))
            {
                UsedRecoveryKey = true;
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                _errorLabel.Text = Loc.Get("WrongPassword");
                _pwBox.SelectAll();
                _pwBox.Focus();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Theme.Border, 1f))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    // ─── Technical Device Details Dialog ─────────────────────────────────────
    class DeviceDetailsDialog : Form
    {
        public DeviceDetailsDialog(DeviceIdentity d)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            TopMost = false;
            ShowInTaskbar = false;
            BackColor = Theme.BgDark;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FontNormal;
            RightToLeft = Loc.RtlValue;
            Size = new Size(560, 480);

            // Title bar
            Panel titleBar = new Panel();
            titleBar.Dock = DockStyle.Top;
            titleBar.Height = 42;
            titleBar.BackColor = Theme.BgPanel;
            titleBar.MouseDown += delegate(object s, MouseEventArgs e) { NativeDragHelper.DragWindow(Handle, e); };

            Label titleLbl = new Label();
            titleLbl.Text = Loc.Get("DeviceDetailsTitle") + " — " + (d.FriendlyName ?? "Device");
            titleLbl.Font = Theme.FontBold;
            titleLbl.ForeColor = Theme.TextPrimary;
            titleLbl.Location = new Point(16, 11);
            titleLbl.AutoSize = true;
            titleBar.Controls.Add(titleLbl);

            Button closeX = new Button();
            closeX.Text = "\u2715";
            closeX.FlatStyle = FlatStyle.Flat;
            closeX.FlatAppearance.BorderSize = 0;
            closeX.ForeColor = Theme.TextSecondary;
            closeX.Size = new Size(36, 36);
            closeX.Location = new Point(Width - 40, 3);
            closeX.Click += delegate { Close(); };
            titleBar.Controls.Add(closeX);
            Controls.Add(titleBar);

            // Table / Details Container
            Panel content = new Panel();
            content.Dock = DockStyle.Fill;
            content.Padding = new Padding(20);
            content.AutoScroll = true;

            int curY = 16;
            AddPropRow(content, Loc.Get("PropFriendlyName"), d.FriendlyName, ref curY);
            AddPropRow(content, Loc.Get("PropVolumes"), d.GetVolumesDisplayString(), ref curY);
            AddPropRow(content, Loc.Get("PropTotalSize"), d.TotalSizeBytes > 0 ? string.Format("{0:F2} GB ({1:N0} bytes)", d.TotalSizeBytes / (1024.0 * 1024.0 * 1024.0), d.TotalSizeBytes) : "N/A", ref curY);
            AddPropRow(content, Loc.Get("PropInstanceId"), d.DeviceInstanceId, ref curY);
            AddPropRow(content, Loc.Get("PropDiskIndex"), d.DiskIndex >= 0 ? ("PhysicalDrive" + d.DiskIndex) : "N/A", ref curY);
            AddPropRow(content, Loc.Get("PropSerial"), d.SerialNumber ?? "N/A", ref curY);
            AddPropRow(content, Loc.Get("PropHardwareIds"), string.Join(", ", d.HardwareIds.ToArray()), ref curY);
            AddPropRow(content, Loc.Get("PropSetupClass"), d.SetupClassGuid + " (" + d.WindowsClass + ")", ref curY);
            AddPropRow(content, Loc.Get("PropBusType"), d.PhysicalTransport.ToString(), ref curY);
            AddPropRow(content, Loc.Get("PropRemovable"), d.IsRemovableMedia ? "True" : "False", ref curY);
            AddPropRow(content, Loc.Get("PropSystemDisk"), d.SafetyResult == SafetyValidationResult.RejectedSystemOrBootDisk ? "Yes (Protected)" : "No", ref curY);
            AddPropRow(content, Loc.Get("PropConfidence"), d.Confidence + " (" + d.GetConfidenceDescription() + ")", ref curY);

            Controls.Add(content);

            // Bottom bar
            Panel bottomBar = new Panel();
            bottomBar.Dock = DockStyle.Bottom;
            bottomBar.Height = 50;
            bottomBar.BackColor = Theme.BgDark;

            ModernButton okBtn = new ModernButton();
            okBtn.Text = (RightToLeft == RightToLeft.Yes) ? "إغلاق" : "Close";
            okBtn.ButtonStyle = ModernButtonStyle.Secondary;
            okBtn.Size = new Size(90, 34);
            okBtn.Location = new Point(Width - 110, 8);
            okBtn.Click += delegate { Close(); };
            bottomBar.Controls.Add(okBtn);
            Controls.Add(bottomBar);
        }

        private void AddPropRow(Panel parent, string name, string val, ref int curY)
        {
            Label lblName = new Label();
            lblName.Text = name;
            lblName.Font = Theme.FontBold;
            lblName.ForeColor = Theme.TextSecondary;
            lblName.Location = new Point(20, curY);
            lblName.Size = new Size(180, 22);

            TextBox txtVal = new TextBox();
            txtVal.Text = val ?? "N/A";
            txtVal.ReadOnly = true;
            txtVal.BackColor = Theme.BgCard;
            txtVal.ForeColor = Theme.TextPrimary;
            txtVal.BorderStyle = BorderStyle.FixedSingle;
            txtVal.Font = Theme.FontSmall;
            txtVal.Location = new Point(200, curY);
            txtVal.Size = new Size(300, 22);

            parent.Controls.Add(lblName);
            parent.Controls.Add(txtVal);
            curY += 32;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Theme.Border, 1f))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    // ─── Completely Redesigned Onboarding Setup Wizard (Password Optional) ──
    class OnboardingWizardDialog : Form
    {
        private int _step = 1;
        private bool _enablePassword = false; // Default: No password required!

        private Label _wizardHeaderLbl;
        private Label _stepIndicatorLbl;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private Panel _stepContent;
        private ModernButton _backBtn;
        private ModernButton _nextBtn;

        // Step 2 controls
        private SelectableOptionCard _optNoPassword;
        private SelectableOptionCard _optSetPassword;
        private Panel _pwInputPanel;
        private TextBox _pwBox1;
        private TextBox _pwBox2;
        private CheckBox _showPwCheck;
        private Label _pwValidationLbl;

        // Step 3 controls
        private string _generatedRecoveryKey;
        private TextBox _keyBox;
        private ModernButton _copyKeyBtn;
        private CheckBox _confirmSavedCheck;
        private CardPanel _inlineErrorPanel;
        private Label _inlineErrorLabel;
        private ModernButton _retrySaveBtn;

        // Step 4 controls
        private SelectableOptionCard _cardPortable;
        private SelectableOptionCard _cardService;
        private bool _selectedServiceMode = false;

        // Step 5 controls
        private List<SelectableOptionCard> _protectionCards = new List<SelectableOptionCard>();
        private int _selectedProtectionIndex = 0; // 0 = No blocking (safest default!)

        public ProtectionPolicy SelectedPolicy { get; private set; }
        public bool FullProtectionSelected { get { return _selectedServiceMode; } }

        public OnboardingWizardDialog()
        {
            Text = Loc.Get("WizardWindowTitle");
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            TopMost = false;
            ShowInTaskbar = false;
            BackColor = Theme.BgDark;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FontNormal;
            RightToLeft = Loc.RtlValue;
            Size = new Size(680, 540);

            SelectedPolicy = new ProtectionPolicy(); // Safest default: all false!
            _generatedRecoveryKey = RecoveryKeyManager.GenerateKeyString();

            BuildFrame();
            RenderCurrentStep();
        }

        private void BuildFrame()
        {
            // Integrated Dark Header
            Panel titleBar = new Panel();
            titleBar.Dock = DockStyle.Top;
            titleBar.Height = 44;
            titleBar.BackColor = Theme.BgPanel;
            titleBar.MouseDown += delegate(object s, MouseEventArgs e) { NativeDragHelper.DragWindow(Handle, e); };

            _wizardHeaderLbl = new Label();
            _wizardHeaderLbl.Text = Loc.Get("WizardDialogHeading");
            _wizardHeaderLbl.Font = Theme.FontBold;
            _wizardHeaderLbl.ForeColor = Theme.TextPrimary;
            _wizardHeaderLbl.Location = new Point(18, 12);
            _wizardHeaderLbl.AutoSize = true;
            titleBar.Controls.Add(_wizardHeaderLbl);

            _stepIndicatorLbl = new Label();
            _stepIndicatorLbl.Font = Theme.FontSmall;
            _stepIndicatorLbl.ForeColor = Theme.AccentBlue;
            _stepIndicatorLbl.Location = new Point(Width - 170, 14);
            _stepIndicatorLbl.Size = new Size(120, 20);
            _stepIndicatorLbl.TextAlign = ContentAlignment.MiddleRight;
            titleBar.Controls.Add(_stepIndicatorLbl);

            Button closeBtn = new Button();
            closeBtn.Text = "\u2715";
            closeBtn.FlatStyle = FlatStyle.Flat;
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.ForeColor = Theme.TextSecondary;
            closeBtn.Size = new Size(34, 34);
            closeBtn.Location = new Point(Width - 40, 5);
            closeBtn.Click += delegate { Close(); };
            titleBar.Controls.Add(closeBtn);
            Controls.Add(titleBar);

            // Step Titles
            _titleLabel = new Label();
            _titleLabel.Font = Theme.FontTitle;
            _titleLabel.ForeColor = Theme.TextPrimary;
            _titleLabel.Location = new Point(28, 54);
            _titleLabel.Size = new Size(624, 32);
            Controls.Add(_titleLabel);

            _subtitleLabel = new Label();
            _subtitleLabel.Font = Theme.FontNormal;
            _subtitleLabel.ForeColor = Theme.TextSecondary;
            _subtitleLabel.Location = new Point(28, 88);
            _subtitleLabel.Size = new Size(624, 38);
            Controls.Add(_subtitleLabel);

            // Step Content Host
            _stepContent = new Panel();
            _stepContent.Location = new Point(28, 134);
            _stepContent.Size = new Size(624, 336);
            _stepContent.BackColor = Theme.BgDark;
            Controls.Add(_stepContent);

            // Bottom Navigation Actions
            Panel bottomBar = new Panel();
            bottomBar.Dock = DockStyle.Bottom;
            bottomBar.Height = 60;
            bottomBar.BackColor = Theme.BgDark;

            _backBtn = new ModernButton();
            _backBtn.Text = Loc.Get("BackBtn");
            _backBtn.ButtonStyle = ModernButtonStyle.Secondary;
            _backBtn.Size = new Size(110, 38);
            _backBtn.Location = new Point(Width - 270, 10);
            _backBtn.Click += delegate { Navigate(-1); };
            bottomBar.Controls.Add(_backBtn);

            _nextBtn = new ModernButton();
            _nextBtn.Text = Loc.Get("NextBtn");
            _nextBtn.ButtonStyle = ModernButtonStyle.Primary;
            _nextBtn.Size = new Size(140, 38);
            _nextBtn.Location = new Point(Width - 150, 10);
            _nextBtn.Click += delegate { Navigate(1); };
            bottomBar.Controls.Add(_nextBtn);

            Controls.Add(bottomBar);
        }

        private void UpdateStepIndicator()
        {
            int totalSteps = _enablePassword ? 6 : 5;
            int displayStep = _step;
            if (!_enablePassword && _step >= 4)
                displayStep = _step - 1;

            _stepIndicatorLbl.Text = string.Format(Loc.Get("StepXOfY"), displayStep, totalSteps);
        }

        private void Navigate(int delta)
        {
            if (delta > 0)
            {
                // Validation before moving forward
                if (_step == 2)
                {
                    if (_enablePassword)
                    {
                        string pw1 = _pwBox1 != null ? _pwBox1.Text : "";
                        string pw2 = _pwBox2 != null ? _pwBox2.Text : "";
                        if (string.IsNullOrEmpty(pw1) || pw1.Length < 4 || pw1 != pw2)
                        {
                            return; // Keep on step 2 until valid
                        }
                        _step = 3; // Move to recovery key
                    }
                    else
                    {
                        _step = 4; // Skip recovery key, jump to mode
                    }
                    RenderCurrentStep();
                    return;
                }
                else if (_step == 3)
                {
                    if (_confirmSavedCheck == null || !_confirmSavedCheck.Checked)
                    {
                        return;
                    }
                    // Persist recovery key with inline error reporting
                    string keyErr;
                    if (!RecoveryKeyManager.SaveMasterRecoveryKey(_generatedRecoveryKey, out keyErr))
                    {
                        ShowInlineKeyError(keyErr);
                        return;
                    }
                    if (_inlineErrorPanel != null) _inlineErrorPanel.Visible = false;
                }
                else if (_step == 6)
                {
                    // Finish wizard
                    CompleteOnboarding();
                    return;
                }
            }
            else if (delta < 0)
            {
                // Going back
                if (_step == 4 && !_enablePassword)
                {
                    _step = 2; // Skip step 3 going backward
                    RenderCurrentStep();
                    return;
                }
            }

            _step += delta;
            if (_step < 1) _step = 1;
            if (_step > 6) _step = 6;

            RenderCurrentStep();
        }

        private void ShowInlineKeyError(string err)
        {
            if (_inlineErrorPanel != null && _inlineErrorLabel != null)
            {
                _inlineErrorLabel.Text = string.Format(Loc.Get("InlineKeySaveError"), err);
                _inlineErrorPanel.Visible = true;
            }
        }

        private void RenderCurrentStep()
        {
            _stepContent.Controls.Clear();
            UpdateStepIndicator();

            _backBtn.Visible = (_step > 1);
            _nextBtn.Text = (_step == 6) ? Loc.Get("FinishBtn") : Loc.Get("NextBtn");

            switch (_step)
            {
                case 1: RenderStep1Welcome(); break;
                case 2: RenderStep2Password(); break;
                case 3: RenderStep3Recovery(); break;
                case 4: RenderStep4Mode(); break;
                case 5: RenderStep5InitialProtection(); break;
                case 6: RenderStep6Finish(); break;
            }
        }

        // ─── Step 1: Welcome ──────────────────────────────────────────────────
        private void RenderStep1Welcome()
        {
            _titleLabel.Text = Loc.Get("Step1WelcomeTitle");
            _subtitleLabel.Text = Loc.Get("Step1WelcomeSubtitle");
            _nextBtn.Enabled = true;

            int curY = 16;
            AddFeatureBullet(Loc.Get("Step1Bullet1"), ref curY);
            AddFeatureBullet(Loc.Get("Step1Bullet2"), ref curY);
            AddFeatureBullet(Loc.Get("Step1Bullet3"), ref curY);
        }

        private void AddFeatureBullet(string text, ref int curY)
        {
            CardPanel bulletCard = new CardPanel();
            bulletCard.Size = new Size(_stepContent.Width - 8, 64);
            bulletCard.Location = new Point(4, curY);
            bulletCard.BackColor = Theme.BgCard;

            Label lbl = new Label();
            lbl.Text = text;
            lbl.Font = Theme.FontNormal;
            lbl.ForeColor = Theme.TextPrimary;
            lbl.Location = new Point(16, 18);
            lbl.Size = new Size(bulletCard.Width - 32, 28);

            bulletCard.Controls.Add(lbl);
            _stepContent.Controls.Add(bulletCard);
            curY += 76;
        }

        // ─── Step 2: Password Choice (Optional) ───────────────────────────────
        private void RenderStep2Password()
        {
            _titleLabel.Text = Loc.Get("Step2SecurityTitle");
            _subtitleLabel.Text = Loc.Get("Step2SecuritySubtitle");

            int cardW = _stepContent.Width - 8;

            // Option A: Use without password (Default)
            _optNoPassword = new SelectableOptionCard();
            _optNoPassword.CardTitle = Loc.Get("SecPasswordOptionNone");
            _optNoPassword.CardDescription = Loc.Get("SecPasswordOptionNoneDesc");
            _optNoPassword.Size = new Size(cardW, 64);
            _optNoPassword.Location = new Point(4, 8);
            _optNoPassword.Selected = !_enablePassword;

            // Option B: Create password
            _optSetPassword = new SelectableOptionCard();
            _optSetPassword.CardTitle = Loc.Get("SecPasswordOptionSet");
            _optSetPassword.CardDescription = Loc.Get("SecPasswordOptionSetDesc");
            _optSetPassword.Size = new Size(cardW, 64);
            _optSetPassword.Location = new Point(4, 80);
            _optSetPassword.Selected = _enablePassword;

            // Password Inputs Panel
            _pwInputPanel = new Panel();
            _pwInputPanel.Size = new Size(cardW, 168);
            _pwInputPanel.Location = new Point(4, 152);
            _pwInputPanel.BackColor = Theme.BgCard;
            _pwInputPanel.Visible = _enablePassword;

            Label lbl1 = new Label();
            lbl1.Text = Loc.Get("FieldPassword");
            lbl1.Font = Theme.FontBold;
            lbl1.ForeColor = Theme.TextPrimary;
            lbl1.Location = new Point(20, 10);
            lbl1.AutoSize = true;

            _pwBox1 = new TextBox();
            _pwBox1.PasswordChar = '\u2022';
            _pwBox1.BackColor = Theme.BgDark;
            _pwBox1.ForeColor = Theme.TextPrimary;
            _pwBox1.BorderStyle = BorderStyle.FixedSingle;
            _pwBox1.Font = new Font("Segoe UI", 10.5f);
            _pwBox1.Location = new Point(20, 32);
            _pwBox1.Size = new Size(cardW - 40, 26);
            _pwBox1.TextChanged += delegate { UpdatePasswordValidation(); };

            Label lbl2 = new Label();
            lbl2.Text = Loc.Get("FieldConfirmPassword");
            lbl2.Font = Theme.FontBold;
            lbl2.ForeColor = Theme.TextPrimary;
            lbl2.Location = new Point(20, 64);
            lbl2.AutoSize = true;

            _pwBox2 = new TextBox();
            _pwBox2.PasswordChar = '\u2022';
            _pwBox2.BackColor = Theme.BgDark;
            _pwBox2.ForeColor = Theme.TextPrimary;
            _pwBox2.BorderStyle = BorderStyle.FixedSingle;
            _pwBox2.Font = new Font("Segoe UI", 10.5f);
            _pwBox2.Location = new Point(20, 86);
            _pwBox2.Size = new Size(cardW - 40, 26);
            _pwBox2.TextChanged += delegate { UpdatePasswordValidation(); };

            _showPwCheck = new CheckBox();
            _showPwCheck.Text = Loc.Get("ShowPassword");
            _showPwCheck.ForeColor = Theme.TextSecondary;
            _showPwCheck.Font = Theme.FontSmall;
            _showPwCheck.Location = new Point(20, 118);
            _showPwCheck.AutoSize = true;
            _showPwCheck.CheckedChanged += delegate
            {
                char c = _showPwCheck.Checked ? '\0' : '\u2022';
                _pwBox1.PasswordChar = c;
                _pwBox2.PasswordChar = c;
            };

            _pwValidationLbl = new Label();
            _pwValidationLbl.Font = Theme.FontSmall;
            _pwValidationLbl.ForeColor = Theme.TextMuted;
            _pwValidationLbl.Text = Loc.Get("PasswordMinLength");
            _pwValidationLbl.Location = new Point(20, 142);
            _pwValidationLbl.Size = new Size(cardW - 40, 20);

            _pwInputPanel.Controls.Add(lbl1);
            _pwInputPanel.Controls.Add(_pwBox1);
            _pwInputPanel.Controls.Add(lbl2);
            _pwInputPanel.Controls.Add(_pwBox2);
            _pwInputPanel.Controls.Add(_showPwCheck);
            _pwInputPanel.Controls.Add(_pwValidationLbl);

            _optNoPassword.SelectionChanged += delegate
            {
                if (_optNoPassword.Selected)
                {
                    _enablePassword = false;
                    _optSetPassword.Selected = false;
                    _pwInputPanel.Visible = false;
                    _nextBtn.Enabled = true;
                    UpdateStepIndicator();
                }
            };

            _optSetPassword.SelectionChanged += delegate
            {
                if (_optSetPassword.Selected)
                {
                    _enablePassword = true;
                    _optNoPassword.Selected = false;
                    _pwInputPanel.Visible = true;
                    UpdateStepIndicator();
                    UpdatePasswordValidation();
                    _pwBox1.Focus();
                }
            };

            _stepContent.Controls.Add(_optNoPassword);
            _stepContent.Controls.Add(_optSetPassword);
            _stepContent.Controls.Add(_pwInputPanel);

            if (_enablePassword)
                UpdatePasswordValidation();
            else
                _nextBtn.Enabled = true;
        }

        private void UpdatePasswordValidation()
        {
            if (!_enablePassword)
            {
                _nextBtn.Enabled = true;
                return;
            }

            string p1 = _pwBox1 != null ? _pwBox1.Text : "";
            string p2 = _pwBox2 != null ? _pwBox2.Text : "";

            if (p1.Length == 0)
            {
                _pwValidationLbl.Text = Loc.Get("PasswordMinLength");
                _pwValidationLbl.ForeColor = Theme.TextMuted;
                _nextBtn.Enabled = false;
            }
            else if (p1.Length < 4)
            {
                _pwValidationLbl.Text = Loc.Get("PasswordMinLength");
                _pwValidationLbl.ForeColor = Theme.AccentOrange;
                _nextBtn.Enabled = false;
            }
            else if (p1 == p2)
            {
                _pwValidationLbl.Text = Loc.Get("PasswordMatch");
                _pwValidationLbl.ForeColor = Theme.AccentGreen;
                _nextBtn.Enabled = true;
            }
            else
            {
                _pwValidationLbl.Text = Loc.Get("PasswordMismatch");
                _pwValidationLbl.ForeColor = Theme.AccentRed;
                _nextBtn.Enabled = false;
            }
        }

        // ─── Step 3: Emergency Recovery Key (Only if password enabled) ────────
        private void RenderStep3Recovery()
        {
            _titleLabel.Text = Loc.Get("Step3RecoveryTitle");
            _subtitleLabel.Text = Loc.Get("Step3RecoverySubtitleNew");

            int pad = 4;
            int cardW = _stepContent.Width - 8;

            CardPanel keyCard = new CardPanel();
            keyCard.Size = new Size(cardW, 310);
            keyCard.Location = new Point(pad, 8);
            keyCard.BackColor = Theme.BgCard;

            _keyBox = new TextBox();
            _keyBox.Text = _generatedRecoveryKey;
            _keyBox.ReadOnly = true;
            _keyBox.Font = Theme.FontMono;
            _keyBox.BackColor = Theme.BgDark;
            _keyBox.ForeColor = Theme.AccentBlue;
            _keyBox.BorderStyle = BorderStyle.FixedSingle;
            _keyBox.TextAlign = HorizontalAlignment.Center;
            _keyBox.Location = new Point(24, 24);
            _keyBox.Size = new Size(cardW - 48, 36);

            _copyKeyBtn = new ModernButton();
            _copyKeyBtn.Text = Loc.Get("CopyKeyBtn");
            _copyKeyBtn.ButtonStyle = ModernButtonStyle.Secondary;
            _copyKeyBtn.Size = new Size(160, 36);
            _copyKeyBtn.Location = new Point((cardW - 160) / 2, 74);
            _copyKeyBtn.Click += delegate
            {
                try
                {
                    Clipboard.SetText(_generatedRecoveryKey);
                    _copyKeyBtn.Text = Loc.Get("KeyCopied");
                }
                catch { }
            };

            _confirmSavedCheck = new CheckBox();
            _confirmSavedCheck.Text = Loc.Get("RecoveryKeyConfirm");
            _confirmSavedCheck.Font = Theme.FontNormal;
            _confirmSavedCheck.ForeColor = Theme.TextPrimary;
            _confirmSavedCheck.Location = new Point(24, 130);
            _confirmSavedCheck.Size = new Size(cardW - 48, 36);
            _confirmSavedCheck.CheckedChanged += delegate
            {
                _nextBtn.Enabled = _confirmSavedCheck.Checked;
            };

            // Inline Error Card (Non-blocking, inline)
            _inlineErrorPanel = new CardPanel();
            _inlineErrorPanel.Size = new Size(cardW - 48, 70);
            _inlineErrorPanel.Location = new Point(24, 180);
            _inlineErrorPanel.BackColor = Theme.AccentRedDim;
            _inlineErrorPanel.Visible = false;

            _inlineErrorLabel = new Label();
            _inlineErrorLabel.Font = Theme.FontSmall;
            _inlineErrorLabel.ForeColor = Theme.AccentRed;
            _inlineErrorLabel.Location = new Point(14, 10);
            _inlineErrorLabel.Size = new Size(cardW - 190, 48);

            _retrySaveBtn = new ModernButton();
            _retrySaveBtn.Text = Loc.Get("KeySaveRetryBtn");
            _retrySaveBtn.ButtonStyle = ModernButtonStyle.Secondary;
            _retrySaveBtn.Size = new Size(110, 32);
            _retrySaveBtn.Location = new Point(cardW - 180, 18);
            _retrySaveBtn.Click += delegate
            {
                string kErr;
                if (RecoveryKeyManager.SaveMasterRecoveryKey(_generatedRecoveryKey, out kErr))
                {
                    _inlineErrorPanel.Visible = false;
                }
                else
                {
                    ShowInlineKeyError(kErr);
                }
            };

            _inlineErrorPanel.Controls.Add(_inlineErrorLabel);
            _inlineErrorPanel.Controls.Add(_retrySaveBtn);

            keyCard.Controls.Add(_keyBox);
            keyCard.Controls.Add(_copyKeyBtn);
            keyCard.Controls.Add(_confirmSavedCheck);
            keyCard.Controls.Add(_inlineErrorPanel);

            _stepContent.Controls.Add(keyCard);
            _nextBtn.Enabled = (_confirmSavedCheck.Checked);
        }

        // ─── Step 4: Protection Mode ──────────────────────────────────────────
        private void RenderStep4Mode()
        {
            _titleLabel.Text = Loc.Get("Step4ModeTitle");
            _subtitleLabel.Text = "";
            _nextBtn.Enabled = true;

            int cardW = _stepContent.Width - 8;

            _cardPortable = new SelectableOptionCard();
            _cardPortable.CardTitle = Loc.Get("ModePortableTitle");
            _cardPortable.CardDescription = Loc.Get("ModePortableDesc");
            _cardPortable.BadgeText = Loc.Get("ModePortableBadge");
            _cardPortable.Size = new Size(cardW, 90);
            _cardPortable.Location = new Point(4, 20);
            _cardPortable.Selected = !_selectedServiceMode;
            _cardPortable.SelectionChanged += delegate
            {
                if (_cardPortable.Selected)
                {
                    _selectedServiceMode = false;
                    _cardService.Selected = false;
                }
            };

            _cardService = new SelectableOptionCard();
            _cardService.CardTitle = Loc.Get("ModeServiceTitle");
            _cardService.CardDescription = Loc.Get("ModeServiceDesc");
            _cardService.Size = new Size(cardW, 90);
            _cardService.Location = new Point(4, 130);
            _cardService.Selected = _selectedServiceMode;
            _cardService.SelectionChanged += delegate
            {
                if (_cardService.Selected)
                {
                    _selectedServiceMode = true;
                    _cardPortable.Selected = false;
                }
            };

            _stepContent.Controls.Add(_cardPortable);
            _stepContent.Controls.Add(_cardService);
        }

        // ─── Step 5: Initial Protection Level ─────────────────────────────────
        private void RenderStep5InitialProtection()
        {
            _titleLabel.Text = Loc.Get("Step5ProtectionTitle");
            _subtitleLabel.Text = "";
            _nextBtn.Enabled = true;

            int cardW = _stepContent.Width - 8;
            _protectionCards.Clear();

            string[] titles = new string[]
            {
                Loc.Get("OptNoBlockTitle"),
                Loc.Get("OptReadOnlyTitle"),
                Loc.Get("OptBlockDisksTitle"),
                Loc.Get("OptBlockPhonesTitle")
            };

            string[] descs = new string[]
            {
                Loc.Get("OptNoBlockDesc"),
                Loc.Get("OptReadOnlyDesc"),
                Loc.Get("OptBlockDisksDesc"),
                Loc.Get("OptBlockPhonesDesc")
            };

            int curY = 6;
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                SelectableOptionCard opt = new SelectableOptionCard();
                opt.CardTitle = titles[i];
                opt.CardDescription = descs[i];
                if (i == 0) opt.BadgeText = Loc.Get("ModePortableBadge"); // Recommended badge on safest option
                opt.Size = new Size(cardW, 64);
                opt.Location = new Point(4, curY);
                opt.Selected = (i == _selectedProtectionIndex);
                opt.SelectionChanged += delegate
                {
                    if (opt.Selected)
                    {
                        _selectedProtectionIndex = index;
                        for (int j = 0; j < _protectionCards.Count; j++)
                        {
                            if (j != index) _protectionCards[j].Selected = false;
                        }
                    }
                };
                _protectionCards.Add(opt);
                _stepContent.Controls.Add(opt);
                curY += 72;
            }
        }

        // ─── Step 6: Finish Setup ─────────────────────────────────────────────
        private void RenderStep6Finish()
        {
            _titleLabel.Text = Loc.Get("Step6FinishTitle");
            _subtitleLabel.Text = "";
            _nextBtn.Enabled = true;

            int curY = 16;
            if (_enablePassword)
            {
                AddFinishCheck(Loc.Get("CheckPasswordSet"), ref curY);
                AddFinishCheck(Loc.Get("CheckKeySaved"), ref curY);
            }
            else
            {
                AddFinishCheck(Loc.Get("CheckNoPassword"), ref curY);
            }

            AddFinishCheck(_selectedServiceMode ? Loc.Get("ModeServiceTitle") : Loc.Get("ModePortableTitle"), ref curY);
            AddFinishCheck(Loc.Get("CheckProtectionSet"), ref curY);
            AddFinishCheck(Loc.Get("CheckRecoveryReady"), ref curY);
        }

        private void AddFinishCheck(string text, ref int curY)
        {
            CardPanel checkCard = new CardPanel();
            checkCard.Size = new Size(_stepContent.Width - 8, 48);
            checkCard.Location = new Point(4, curY);
            checkCard.BackColor = Theme.BgCard;

            Label lbl = new Label();
            lbl.Text = text;
            lbl.Font = Theme.FontNormal;
            lbl.ForeColor = Theme.AccentGreen;
            lbl.Location = new Point(16, 14);
            lbl.Size = new Size(checkCard.Width - 32, 22);

            checkCard.Controls.Add(lbl);
            _stepContent.Controls.Add(checkCard);
            curY += 56;
        }

        private void CompleteOnboarding()
        {
            // 1. Password persistence
            if (_enablePassword && _pwBox1 != null && !string.IsNullOrEmpty(_pwBox1.Text))
            {
                PasswordManager.SetPassword(_pwBox1.Text);
            }
            else
            {
                PasswordManager.RemovePassword();
                RecoveryKeyManager.DeleteMasterRecoveryKey();
            }

            // 2. Build chosen policy
            SelectedPolicy = new ProtectionPolicy();
            switch (_selectedProtectionIndex)
            {
                case 0: // No block
                    break;
                case 1: // Read-only
                    SelectedPolicy.ReadOnlyRemovable = true;
                    break;
                case 2: // Block disks
                    SelectedPolicy.BlockRemovableDisks = true;
                    break;
                case 3: // Block phones
                    SelectedPolicy.BlockWpdPhonesAndMedia = true;
                    break;
            }

            // 3. Save settings
            AppSettingsData s = SettingsRepository.Load();
            s.OnboardingCompleted = true;
            s.ActivePolicy = SelectedPolicy;
            s.FullProtectionModeEnabled = _selectedServiceMode;
            SettingsRepository.Save(s);

            // 4. Install service only if chosen
            if (_selectedServiceMode)
            {
                string sErr;
                ServiceManager.InstallService(out sErr);
            }



            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Theme.Border, 1f))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    // ─── Trusted Device Whitelist Card Control ───────────────────────────────
    class TrustedDeviceCardControl : CardPanel
    {
        private DeviceRule _rule;
        private Label _nameLabel;
        private Label _metaLabel;
        private Label _warningLabel;
        private TruthfulBadge _strengthBadge;
        private ModernButton _revokeBtn;
        private ModernButton _detailsBtn;

        public event EventHandler RevokeRequested;
        public event EventHandler DetailsRequested;

        public DeviceRule Rule { get { return _rule; } }

        public TrustedDeviceCardControl(DeviceRule rule)
        {
            _rule = rule;
            Padding = new Padding(20, 14, 20, 14);
            BackColor = Theme.BgCard;

            bool isWeak = (rule.Confidence != IdentityConfidence.Strong);
            TopAccentColor = !isWeak ? Theme.AccentGreen : Theme.AccentOrange;

            _nameLabel = new Label();
            _nameLabel.Text = string.IsNullOrEmpty(rule.FriendlyName) ? "USB Device" : rule.FriendlyName;
            _nameLabel.Font = Theme.FontLarge;
            _nameLabel.ForeColor = Theme.TextPrimary;
            _nameLabel.AutoSize = true;

            _strengthBadge = new TruthfulBadge();
            string confStr = rule.Confidence == IdentityConfidence.Strong ? Loc.Get("ConfidenceStrong") :
                             rule.Confidence == IdentityConfidence.Medium ? Loc.Get("ConfidenceMedium") : Loc.Get("ConfidenceWeak");
            _strengthBadge.BadgeText = Loc.Get("ColConfidence") + ": " + confStr;
            _strengthBadge.BadgeColor = (rule.Confidence == IdentityConfidence.Strong) ? Theme.AccentGreen :
                                        (rule.Confidence == IdentityConfidence.Medium) ? Theme.AccentBlue : Theme.AccentOrange;

            _metaLabel = new Label();
            string scopeText = string.IsNullOrEmpty(rule.TargetSerial) ? rule.TargetInstanceId : ("Serial: " + rule.TargetSerial);
            string dateText = rule.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            _metaLabel.Text = scopeText + "  •  " + dateText;
            _metaLabel.Font = Theme.FontSmall;
            _metaLabel.ForeColor = Theme.TextSecondary;
            _metaLabel.AutoSize = true;

            _warningLabel = new Label();
            _warningLabel.Text = isWeak ? Loc.Get("IdentityWeakWarning") : Loc.Get("IdentityStrongNotice");
            _warningLabel.Font = Theme.FontSmall;
            _warningLabel.ForeColor = isWeak ? Theme.AccentOrange : Color.FromArgb(145, Theme.TextSecondary);
            _warningLabel.AutoSize = true;

            _detailsBtn = new ModernButton();
            _detailsBtn.Text = Loc.Get("ViewDetails");
            _detailsBtn.ButtonStyle = ModernButtonStyle.Secondary;
            _detailsBtn.Size = new Size(95, 28);
            _detailsBtn.Click += delegate { if (DetailsRequested != null) DetailsRequested(this, EventArgs.Empty); };

            _revokeBtn = new ModernButton();
            _revokeBtn.Text = Loc.Get("RevokeTrust");
            _revokeBtn.ButtonStyle = ModernButtonStyle.Danger;
            _revokeBtn.Size = new Size(125, 28);
            _revokeBtn.Click += delegate { if (RevokeRequested != null) RevokeRequested(this, EventArgs.Empty); };

            Controls.Add(_nameLabel);
            Controls.Add(_strengthBadge);
            Controls.Add(_metaLabel);
            Controls.Add(_warningLabel);
            Controls.Add(_detailsBtn);
            Controls.Add(_revokeBtn);

            Height = 124;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutElements();
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            LayoutElements();
        }

        private void LayoutElements()
        {
            if (_nameLabel == null || _detailsBtn == null || _strengthBadge == null) return;

            bool isRtl = Loc.IsRtl;
            int pad = 20;
            int availW = Math.Max(240, ClientSize.Width);
            int btnY = Height - 14 - _revokeBtn.Height;

            if (isRtl)
            {
                // Row 1: Badge pill on left, Name on right
                _strengthBadge.Location = new Point(pad, 14);

                int maxNameW = Math.Max(100, availW - _strengthBadge.Right - pad - 16);
                _nameLabel.MaximumSize = new Size(maxNameW, 26);
                _nameLabel.Location = new Point(availW - pad - _nameLabel.Width, 13);

                // Row 2: Metadata (Serial • Date)
                _metaLabel.Location = new Point(availW - pad - _metaLabel.Width, 42);

                // Row 3: Security verification notice
                _warningLabel.Location = new Point(availW - pad - _warningLabel.Width, 64);

                // Row 4: Action Toolbar (aligned on left, safely clear of badge)
                int btnX = pad;
                _revokeBtn.Location = new Point(btnX, btnY);
                btnX += _revokeBtn.Width + 10;
                _detailsBtn.Location = new Point(btnX, btnY);
            }
            else
            {
                // Row 1: Name on left, Badge pill on right
                _strengthBadge.Location = new Point(availW - pad - _strengthBadge.Width, 14);

                int maxNameW = Math.Max(100, _strengthBadge.Left - pad - 16);
                _nameLabel.MaximumSize = new Size(maxNameW, 26);
                _nameLabel.Location = new Point(pad, 13);

                // Row 2: Metadata (Serial • Date)
                _metaLabel.Location = new Point(pad, 42);

                // Row 3: Security verification notice
                _warningLabel.Location = new Point(pad, 64);

                // Row 4: Action Toolbar (aligned on right)
                int btnX = availW - pad - _revokeBtn.Width;
                _revokeBtn.Location = new Point(btnX, btnY);
                btnX -= (_detailsBtn.Width + 10);
                _detailsBtn.Location = new Point(btnX, btnY);
            }
        }
    }

    // ─── Custom Dark Dropdown Control ────────────────────────────────────────
    class DarkDropdown : Control
    {
        private List<string> _items = new List<string>();
        private int _selectedIndex = 0;
        private bool _hovering = false;
        private bool _pressing = false;

        public event EventHandler SelectedIndexChanged;

        public List<string> Items { get { return _items; } }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (value >= 0 && value < _items.Count)
                {
                    bool changed = (_selectedIndex != value);
                    _selectedIndex = value;
                    Invalidate();
                    if (changed && SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
                }
            }
        }

        public string SelectedItem
        {
            get { return (_selectedIndex >= 0 && _selectedIndex < _items.Count) ? _items[_selectedIndex] : ""; }
        }

        public DarkDropdown()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);

            Font = Theme.FontNormal;
            Cursor = Cursors.Hand;
            Size = new Size(150, 32);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovering = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovering = false; _pressing = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressing = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressing = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            ShowDropdownMenu();
        }

        public ContextMenuStrip ActiveMenu { get; private set; }

        public void ShowDropdownMenu()
        {
            if (_items.Count == 0) return;

            if (ActiveMenu != null)
            {
                try { ActiveMenu.Close(); } catch { }
                ActiveMenu = null;
            }

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new DarkDropdownMenuRenderer();
            menu.ShowImageMargin = false;
            menu.BackColor = Theme.BgElevated;
            menu.ForeColor = Theme.TextPrimary;
            menu.Font = Theme.FontNormal;
            menu.RightToLeft = RightToLeft;
            ActiveMenu = menu;

            menu.Closed += delegate
            {
                if (ActiveMenu == menu) ActiveMenu = null;
            };

            for (int i = 0; i < _items.Count; i++)
            {
                int idx = i;
                ToolStripMenuItem item = new ToolStripMenuItem(_items[i]);
                item.BackColor = Theme.BgElevated;
                item.ForeColor = Theme.TextPrimary;
                if (idx == _selectedIndex)
                {
                    item.Font = Theme.FontBold;
                    item.ForeColor = Theme.AccentBlue;
                }
                item.Click += delegate
                {
                    SelectedIndex = idx;
                };
                menu.Items.Add(item);
            }

            Point pt = new Point(0, Height + 2);
            menu.Show(this, pt);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(1, 1, Width - 3, Height - 3);
            Color bg = _pressing ? Theme.BgDark : (_hovering ? Theme.BgHover : Theme.BgCard);
            Color border = _hovering ? Theme.BorderLight : Theme.Border;

            using (SolidBrush fill = new SolidBrush(bg))
                Theme.FillRoundedRect(g, fill, rect, Theme.RadiusBtn);

            using (Pen pen = new Pen(border, 1f))
                Theme.DrawRoundedRect(g, pen, rect, Theme.RadiusBtn);

            bool isRtl = RightToLeft == RightToLeft.Yes;

            // Draw downward arrow
            int arrowX = isRtl ? 14 : Width - 22;
            int arrowY = (Height - 5) / 2;
            Point[] arrowPts = new Point[]
            {
                new Point(arrowX, arrowY),
                new Point(arrowX + 8, arrowY),
                new Point(arrowX + 4, arrowY + 5)
            };
            using (SolidBrush arrowBrush = new SolidBrush(Theme.TextSecondary))
                g.FillPolygon(arrowBrush, arrowPts);

            // Draw text
            string text = SelectedItem;
            if (!string.IsNullOrEmpty(text))
            {
                int textX = isRtl ? 28 : 12;
                int textW = Width - 42;
                Rectangle textRect = new Rectangle(textX, 0, textW, Height);
                StringFormat sf = new StringFormat();
                sf.LineAlignment = StringAlignment.Center;
                sf.Alignment = isRtl ? StringAlignment.Far : StringAlignment.Near;
                if (isRtl) sf.FormatFlags = StringFormatFlags.DirectionRightToLeft;

                using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
                    g.DrawString(text, Font, tb, textRect, sf);
            }
        }
    }

    class DarkDropdownMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkDropdownMenuRenderer() : base(new DarkDropdownColorTable()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                using (SolidBrush b = new SolidBrush(Theme.BgHover))
                    e.Graphics.FillRectangle(b, e.Item.ContentRectangle);
            }
            else
            {
                using (SolidBrush b = new SolidBrush(Theme.BgElevated))
                    e.Graphics.FillRectangle(b, e.Item.ContentRectangle);
            }
        }
    }

    class DarkDropdownColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color ToolStripDropDownBackground { get { return Theme.BgElevated; } }
        public override Color MenuItemSelected { get { return Theme.BgHover; } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
    }

    // ─── Activity Log Data Model & Controls ──────────────────────────────────
    public enum ActivityCategory
    {
        All,
        Devices,
        DeviceEvents,
        Protection,
        Security,
        PolicyChanges,
        TrustedDevices,
        Recovery,
        Settings,
        Errors
    }

    public class ActivityRecord
    {
        public DateTime Timestamp { get; set; }
        public string Title { get; set; }
        public string Context { get; set; }
        public ActivityCategory Category { get; set; }
        public string ResultBadge { get; set; }
        public Color BadgeColor { get; set; }

        public ActivityRecord(DateTime time, string title, string context, ActivityCategory category, string badge = "", Color? badgeColor = null)
        {
            Timestamp = time;
            Title = title ?? "";
            Context = context ?? "";
            Category = category;
            ResultBadge = badge ?? "";
            BadgeColor = badgeColor.HasValue ? badgeColor.Value : Theme.AccentBlue;
        }

        public ActivityRecord(DateTime time, string title, ActivityCategory category, string badge, Color? badgeColor, string context = "")
        {
            Timestamp = time;
            Title = title ?? "";
            Context = context ?? "";
            Category = category;
            ResultBadge = badge ?? "";
            BadgeColor = badgeColor.HasValue ? badgeColor.Value : Theme.AccentBlue;
        }
    }

    class ActivityRowControl : Control
    {
        private ActivityRecord _record;
        private bool _hovering;

        public ActivityRecord Record { get { return _record; } }

        public ActivityRowControl(ActivityRecord record)
        {
            _record = record;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);

            Font = Theme.FontNormal;
            Size = new Size(400, 38);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovering = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovering = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (_hovering)
            {
                using (SolidBrush hb = new SolidBrush(Theme.BgHover))
                    g.FillRectangle(hb, 0, 0, Width, Height);
            }

            // Divider line
            using (Pen divPen = new Pen(Theme.Divider, 1f))
                g.DrawLine(divPen, 16, Height - 1, Width - 16, Height - 1);

            bool isRtl = RightToLeft == RightToLeft.Yes;
            string timeStr = _record.Timestamp.ToString("HH:mm:ss");

            if (isRtl)
            {
                // Right side: Time
                int curX = Width - 16;
                StringFormat sfRight = new StringFormat();
                sfRight.LineAlignment = StringAlignment.Center;
                sfRight.Alignment = StringAlignment.Far;
                sfRight.FormatFlags = StringFormatFlags.DirectionRightToLeft;

                using (SolidBrush tb = new SolidBrush(Theme.TextMuted))
                    g.DrawString(timeStr, Theme.FontSmall, tb, new RectangleF(curX - 60, 0, 60, Height), sfRight);
                curX -= 72;

                // Title & Context
                string mainText = _record.Title;
                if (!string.IsNullOrEmpty(_record.Context))
                    mainText += "  (" + _record.Context + ")";

                int titleW = curX - 120;
                if (titleW < 100) titleW = 100;
                using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
                    g.DrawString(mainText, Theme.FontNormal, tb, new RectangleF(curX - titleW, 0, titleW, Height), sfRight);

                // Badge on Left side
                if (!string.IsNullOrEmpty(_record.ResultBadge))
                {
                    int badgeW = 90;
                    Rectangle badgeRect = new Rectangle(16, (Height - 20) / 2, badgeW, 20);
                    Color bColor = _record.BadgeColor;
                    using (SolidBrush bb = new SolidBrush(Color.FromArgb(24, bColor.R, bColor.G, bColor.B)))
                        Theme.FillRoundedRect(g, bb, badgeRect, 4);
                    using (Pen bp = new Pen(Color.FromArgb(70, bColor.R, bColor.G, bColor.B), 1f))
                        Theme.DrawRoundedRect(g, bp, badgeRect, 4);

                    StringFormat sfCenter = new StringFormat();
                    sfCenter.LineAlignment = StringAlignment.Center;
                    sfCenter.Alignment = StringAlignment.Center;
                    using (SolidBrush btb = new SolidBrush(bColor))
                        g.DrawString(_record.ResultBadge, Theme.FontSmall, btb, badgeRect, sfCenter);
                }
            }
            else
            {
                // Left side: Time
                int curX = 16;
                StringFormat sfLeft = new StringFormat();
                sfLeft.LineAlignment = StringAlignment.Center;
                sfLeft.Alignment = StringAlignment.Near;

                using (SolidBrush tb = new SolidBrush(Theme.TextMuted))
                    g.DrawString(timeStr, Theme.FontSmall, tb, new RectangleF(curX, 0, 60, Height), sfLeft);
                curX += 68;

                // Title & Context
                string mainText = _record.Title;
                if (!string.IsNullOrEmpty(_record.Context))
                    mainText += "  (" + _record.Context + ")";

                int badgeW = !string.IsNullOrEmpty(_record.ResultBadge) ? 100 : 0;
                int titleW = Width - curX - badgeW - 24;
                if (titleW < 100) titleW = 100;
                using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
                    g.DrawString(mainText, Theme.FontNormal, tb, new RectangleF(curX, 0, titleW, Height), sfLeft);

                // Badge on Right side
                if (!string.IsNullOrEmpty(_record.ResultBadge))
                {
                    Rectangle badgeRect = new Rectangle(Width - 16 - 90, (Height - 20) / 2, 90, 20);
                    Color bColor = _record.BadgeColor;
                    using (SolidBrush bb = new SolidBrush(Color.FromArgb(24, bColor.R, bColor.G, bColor.B)))
                        Theme.FillRoundedRect(g, bb, badgeRect, 4);
                    using (Pen bp = new Pen(Color.FromArgb(70, bColor.R, bColor.G, bColor.B), 1f))
                        Theme.DrawRoundedRect(g, bp, badgeRect, 4);

                    StringFormat sfCenter = new StringFormat();
                    sfCenter.LineAlignment = StringAlignment.Center;
                    sfCenter.Alignment = StringAlignment.Center;
                    using (SolidBrush btb = new SolidBrush(bColor))
                        g.DrawString(_record.ResultBadge, Theme.FontSmall, btb, badgeRect, sfCenter);
                }
            }
        }
    }

    class ActivityDateHeaderControl : Control
    {
        private string _dateText;

        public ActivityDateHeaderControl(string dateText)
        {
            _dateText = dateText ?? "";
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);

            Font = Theme.FontBold;
            Size = new Size(400, 32);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            bool isRtl = RightToLeft == RightToLeft.Yes;
            StringFormat sf = new StringFormat();
            sf.LineAlignment = StringAlignment.Center;
            sf.Alignment = isRtl ? StringAlignment.Far : StringAlignment.Near;
            if (isRtl) sf.FormatFlags = StringFormatFlags.DirectionRightToLeft;

            Rectangle textRect = new Rectangle(16, 0, Width - 32, Height);
            using (SolidBrush tb = new SolidBrush(Theme.TextSecondary))
                g.DrawString(_dateText, Font, tb, textRect, sf);

            using (Pen p = new Pen(Theme.Border, 1f))
                g.DrawLine(p, 16, Height - 4, Width - 16, Height - 4);
        }
    }

    // ─── Compact Titlebar Language Selector ──────────────────────────────────
    class LanguageSelectorPill : Control
    {
        private bool _hovering = false;
        private bool _pressing = false;

        public LanguageSelectorPill()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);

            Font = Theme.FontSmall;
            Cursor = Cursors.Hand;
            Size = new Size(76, 26);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovering = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovering = false; _pressing = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressing = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressing = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int pillH = 26;
            int pillY = Math.Max(0, (Height - pillH) / 2);
            int pillW = Width - 8;
            int pillX = 4;
            Rectangle rect = new Rectangle(pillX, pillY, pillW, pillH);
            Color bg = _pressing ? Theme.BgDark : (_hovering ? Theme.BgHover : Theme.BgCard);
            Color border = _hovering ? Theme.BorderLight : Theme.Border;

            using (SolidBrush fill = new SolidBrush(bg))
                Theme.FillRoundedRect(g, fill, rect, 5);

            using (Pen pen = new Pen(border, 1f))
                Theme.DrawRoundedRect(g, pen, rect, 5);

            string text = Loc.Get("LangSwitch");
            StringFormat sf = new StringFormat();
            sf.LineAlignment = StringAlignment.Center;
            sf.Alignment = StringAlignment.Center;

            using (SolidBrush tb = new SolidBrush(_hovering ? Theme.AccentBlueHover : Theme.AccentBlue))
                g.DrawString(text, Font, tb, rect, sf);
        }
    }
}

