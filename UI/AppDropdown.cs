// ═══════════════════════════════════════════════════════════════════════════
// AppDropdown — Tier 1 Owner-Drawn Dropdown Primitive for v2.1
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
    public class AppDropdown : Control
    {
        private readonly List<string> _items = new List<string>();
        private int _selectedIndex = -1;
        private bool _isHovered = false;
        private bool _isPressed = false;
        private ToolStripDropDownMenu _dropdownMenu = null;

        public event EventHandler SelectedIndexChanged;

        public AppDropdown()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.ComboBox;

            Height = DpiManager.ScaleInt(HeightTokens.HeightMd);
            Width = DpiManager.ScaleInt(160);

            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public List<string> Items
        {
            get { return _items; }
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (value >= -1 && value < _items.Count)
                {
                    if (_selectedIndex != value)
                    {
                        _selectedIndex = value;
                        Invalidate();
                        OnSelectedIndexChanged();
                    }
                }
            }
        }

        public string SelectedItem
        {
            get
            {
                if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
                {
                    return _items[_selectedIndex];
                }
                return "";
            }
            set
            {
                int idx = _items.IndexOf(value);
                if (idx >= 0)
                {
                    SelectedIndex = idx;
                }
            }
        }

        public bool IsDroppedDown
        {
            get { return _dropdownMenu != null && _dropdownMenu.Visible; }
        }

        public ToolStripDropDownMenu ActiveMenu
        {
            get { return _dropdownMenu; }
        }

        public void ShowDropdownMenu()
        {
            ShowDropdown();
        }

        private void OnSelectedIndexChanged()
        {
            if (SelectedIndexChanged != null)
            {
                SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                Invalidate();
                if (_dropdownMenu != null && _dropdownMenu.Visible)
                {
                    _dropdownMenu.Close();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                if (_dropdownMenu != null)
                {
                    _dropdownMenu.Dispose();
                    _dropdownMenu = null;
                }
            }
            base.Dispose(disposing);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHovered = false;
            _isPressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Focus();
                Invalidate();
                ToggleDropdown();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _isPressed = false;
            Invalidate();
            base.OnMouseUp(e);
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

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Alt && e.KeyCode == Keys.Down)
            {
                ToggleDropdown();
                e.Handled = true;
                return;
            }

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
                ToggleDropdown();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Home)
            {
                if (_items.Count > 0) SelectedIndex = 0;
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.End)
            {
                if (_items.Count > 0) SelectedIndex = _items.Count - 1;
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        public void ToggleDropdown()
        {
            if (_dropdownMenu != null && _dropdownMenu.Visible)
            {
                _dropdownMenu.Close();
                return;
            }

            ShowDropdown();
        }

        public void ShowDropdown()
        {
            if (_items.Count == 0) return;

            if (_dropdownMenu != null)
            {
                try { _dropdownMenu.Close(); } catch { }
                _dropdownMenu.Dispose();
                _dropdownMenu = null;
            }

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes);

            // Measure items to dynamically size the popup so no text is ever clipped or wrapped
            int maxItemTextW = 0;
            Font itemFont = DpiManager.GetFont(TypographyRole.Body, isRtl);
            Font selectedFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
            using (Graphics g = CreateGraphics())
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    Font f = (i == _selectedIndex) ? selectedFont : itemFont;
                    Size sz = TextRenderer.MeasureText(g, _items[i], f, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                    if (sz.Width > maxItemTextW) maxItemTextW = sz.Width;
                }
            }

            int checkArea = DpiManager.ScaleInt(26);
            int padH = DpiManager.ScaleInt(16);
            int targetMenuW = Math.Max(Width, maxItemTextW + checkArea + padH * 2);
            int itemHeight = DpiManager.ScaleInt(34); // 34px ensures ample vertical clearance for Arabic descenders

            _dropdownMenu = new ToolStripDropDownMenu();
            _dropdownMenu.AutoClose = true;
            _dropdownMenu.DropShadowEnabled = true;
            _dropdownMenu.ShowImageMargin = false;
            _dropdownMenu.ShowCheckMargin = false;
            _dropdownMenu.BackColor = pal.SurfaceElevated;
            _dropdownMenu.ForeColor = pal.TextPrimary;
            _dropdownMenu.Font = itemFont;
            _dropdownMenu.RightToLeft = RightToLeft;
            _dropdownMenu.Renderer = new AppDropdownMenuRenderer(pal, this);
            _dropdownMenu.MinimumSize = new Size(targetMenuW, 0);

            for (int i = 0; i < _items.Count; i++)
            {
                int idx = i;
                ToolStripMenuItem item = new ToolStripMenuItem(_items[i]);
                item.AutoSize = false;
                item.Size = new Size(targetMenuW, itemHeight);
                item.Height = itemHeight;
                item.Width = targetMenuW;
                item.BackColor = pal.SurfaceElevated;
                item.ForeColor = (idx == _selectedIndex) ? pal.Accent : pal.TextPrimary;
                item.Font = (idx == _selectedIndex) ? selectedFont : itemFont;
                item.Tag = idx;
                item.RightToLeft = RightToLeft;

                item.Click += delegate
                {
                    SelectedIndex = idx;
                    Focus();
                };

                _dropdownMenu.Items.Add(item);
            }

            _dropdownMenu.Closed += delegate
            {
                Invalidate();
            };

            // Positioning & Multi-Monitor Screen Clamping
            int totalMenuH = itemHeight * _items.Count + DpiManager.ScaleInt(8);
            Point screenPt;
            if (isRtl)
            {
                // In RTL mode, align the menu's right edge with the control's right edge
                screenPt = PointToScreen(new Point(Width - targetMenuW, Height + 2));
            }
            else
            {
                // In LTR mode, align the menu's left edge with the control's left edge
                screenPt = PointToScreen(new Point(0, Height + 2));
            }

            Screen scr = Screen.FromControl(this) ?? Screen.PrimaryScreen;
            if (scr != null)
            {
                Rectangle work = scr.WorkingArea;
                // If the dropdown would overflow below the screen working area, open upwards
                if (screenPt.Y + totalMenuH > work.Bottom - DpiManager.ScaleInt(6))
                {
                    screenPt.Y = PointToScreen(new Point(0, -totalMenuH - 2)).Y;
                }
                // Clamp X within screen working area
                screenPt.X = Math.Max(work.Left + DpiManager.ScaleInt(4), Math.Min(screenPt.X, work.Right - targetMenuW - DpiManager.ScaleInt(4)));
                // Clamp Y within screen working area
                screenPt.Y = Math.Max(work.Top + DpiManager.ScaleInt(4), screenPt.Y);
            }

            _dropdownMenu.Show(screenPt);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes);
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);

            Rectangle rect = new Rectangle(1, 1, Width - 2, Height - 2);

            Color bg;
            Color border;
            Color text;

            if (!Enabled)
            {
                bg = pal.Surface;
                border = pal.BorderSubtle;
                text = pal.TextDisabled;
            }
            else if (_isPressed || IsDroppedDown)
            {
                bg = pal.SurfaceElevated;
                border = pal.Accent;
                text = pal.TextPrimary;
            }
            else if (_isHovered)
            {
                bg = pal.SurfaceHover;
                border = pal.Border;
                text = pal.TextPrimary;
            }
            else
            {
                bg = pal.Surface;
                border = pal.Border;
                text = pal.TextPrimary;
            }

            // Clean background fill matching effective parent surface (eliminates corner artifacts)
            Color surfaceColor = UiDrawingUtils.GetEffectiveParentSurface(this);
            using (SolidBrush surfaceBrush = new SolidBrush(surfaceColor))
            {
                g.FillRectangle(surfaceBrush, ClientRectangle);
            }

            // Fill
            using (SolidBrush b = new SolidBrush(bg))
            {
                UiDrawingUtils.FillRoundedRectangle(g, b, rect, rad);
            }

            // Border
            using (Pen pen = new Pen(border, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, pen, rect, rad);
            }

            // Arrow
            int arrowBoxSize = DpiManager.ScaleInt(16);
            int arrowX = isRtl ? DpiManager.ScaleInt(SpacingTokens.Space8) : (Width - arrowBoxSize - DpiManager.ScaleInt(SpacingTokens.Space8));
            int arrowY = (Height - arrowBoxSize) / 2;
            Rectangle arrowBox = new Rectangle(arrowX, arrowY, arrowBoxSize, arrowBoxSize);

            Color arrowColor = Enabled ? pal.TextSecondary : pal.TextDisabled;
            UiDrawingUtils.DrawChevronDown(g, arrowColor, arrowBox, DpiManager.ScaleInt(6));

            // Selected Text
            string currentText = SelectedItem;
            if (!string.IsNullOrEmpty(currentText))
            {
                Font font = DpiManager.GetFont(TypographyRole.Body, isRtl);
                int pad = DpiManager.ScaleInt(SpacingTokens.Space12);
                int textW = Width - arrowBoxSize - pad * 2;

                Rectangle textRect;
                TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoClipping | TextFormatFlags.NoPrefix;

                if (isRtl)
                {
                    textRect = new Rectangle(arrowBoxSize + pad, 0, textW, Height);
                    flags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
                }
                else
                {
                    textRect = new Rectangle(pad, 0, textW, Height);
                    flags |= TextFormatFlags.Left;
                }

                TextRenderer.DrawText(g, currentText, font, textRect, text, flags);
            }

            // Focus Ring
            if (Focused && ShowFocusCues)
            {
                UiDrawingUtils.DrawFocusRing(g, rect, rad, pal.FocusRing);
            }
        }
    }

    internal class AppDropdownMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly IThemePalette _palette;
        private readonly AppDropdown _owner;

        public AppDropdownMenuRenderer(IThemePalette palette, AppDropdown owner = null) : base(new AppDropdownColorTable(palette))
        {
            _palette = palette;
            _owner = owner;
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            int idx = (e.Item.Tag is int) ? (int)e.Item.Tag : -1;
            bool isCurrent = (_owner != null && idx == _owner.SelectedIndex);

            if (e.Item.Selected) // Hovered state in ToolStrip terminology
            {
                using (SolidBrush b = new SolidBrush(_palette.SurfaceHover))
                {
                    e.Graphics.FillRectangle(b, e.Item.ContentRectangle);
                }
            }
            else if (isCurrent)
            {
                using (SolidBrush b = new SolidBrush(_palette.SurfaceSelected))
                {
                    e.Graphics.FillRectangle(b, e.Item.ContentRectangle);
                }
            }
            else
            {
                using (SolidBrush b = new SolidBrush(_palette.SurfaceElevated))
                {
                    e.Graphics.FillRectangle(b, e.Item.ContentRectangle);
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            int idx = (e.Item.Tag is int) ? (int)e.Item.Tag : -1;
            bool isSelected = (_owner != null && idx == _owner.SelectedIndex);
            bool isRtl = (e.Item.RightToLeft == RightToLeft.Yes);

            Rectangle itemRect = e.Item.ContentRectangle;
            int padH = DpiManager.ScaleInt(14);
            int checkSize = DpiManager.ScaleInt(14);

            Color textColor = isSelected ? _palette.Accent : (e.Item.Selected ? _palette.TextPrimary : _palette.TextSecondary);
            Font font = isSelected ? DpiManager.GetFont(TypographyRole.BodyStrong, isRtl) : DpiManager.GetFont(TypographyRole.Body, isRtl);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Rectangle textRect;
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoClipping | TextFormatFlags.NoPrefix;

            if (isRtl)
            {
                int checkX = itemRect.Right - padH - checkSize;
                int checkY = (itemRect.Height - checkSize) / 2;
                Rectangle checkRect = new Rectangle(checkX, checkY, checkSize, checkSize);

                if (isSelected)
                {
                    UiDrawingUtils.DrawCheckmark(e.Graphics, _palette.Accent, checkRect, 1.8f);
                }

                textRect = new Rectangle(itemRect.Left + padH, 0, itemRect.Width - padH * 2 - (checkSize + DpiManager.ScaleInt(8)), itemRect.Height);
                flags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
            }
            else
            {
                int checkX = itemRect.Left + padH;
                int checkY = (itemRect.Height - checkSize) / 2;
                Rectangle checkRect = new Rectangle(checkX, checkY, checkSize, checkSize);

                if (isSelected)
                {
                    UiDrawingUtils.DrawCheckmark(e.Graphics, _palette.Accent, checkRect, 1.8f);
                }

                textRect = new Rectangle(itemRect.Left + padH + checkSize + DpiManager.ScaleInt(8), 0, itemRect.Width - padH * 2 - (checkSize + DpiManager.ScaleInt(8)), itemRect.Height);
                flags |= TextFormatFlags.Left;
            }

            TextRenderer.DrawText(e.Graphics, e.Text, font, textRect, textColor, flags);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (Pen pen = new Pen(_palette.Border, 1f))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            }
        }
    }

    internal class AppDropdownColorTable : ProfessionalColorTable
    {
        private readonly IThemePalette _palette;

        public AppDropdownColorTable(IThemePalette palette)
        {
            _palette = palette;
        }

        public override Color MenuBorder { get { return _palette.Border; } }
        public override Color ToolStripDropDownBackground { get { return _palette.SurfaceElevated; } }
        public override Color MenuItemSelected { get { return _palette.SurfaceHover; } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
    }
}
