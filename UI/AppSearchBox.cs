// ═══════════════════════════════════════════════════════════════════════════
// AppSearchBox — Tier 1 Unified Search Input Primitive for v2.1
// USB Shield Pro v2.1
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace UsbShieldPro.UI
{
    /// <summary>
    /// Unified, accessible search input control supporting RTL/LTR bidirectional typing,
    /// animated focus ring, interactive vector clear button, and dynamic responsiveness.
    /// Inherits Panel for seamless Drop-in container compatibility.
    /// </summary>
    public class AppSearchBox : Panel
    {
        private readonly TextBox _textBox;
        private string _placeholder = "";
        private bool _isHovered = false;
        private bool _isClearHovered = false;
        private Rectangle _clearRect = Rectangle.Empty;
        private Rectangle _iconRect = Rectangle.Empty;

        public event EventHandler QueryChanged;
        public event EventHandler SearchCleared;

        public TextBox InnerTextBox
        {
            get { return _textBox; }
        }

        public override string Text
        {
            get { return (_textBox != null) ? _textBox.Text : ""; }
            set
            {
                if (_textBox != null)
                {
                    _textBox.Text = value ?? "";
                    UpdateTextBoxVisibility();
                    Invalidate();
                }
            }
        }

        public string Query
        {
            get { return (_textBox != null) ? _textBox.Text.Trim() : ""; }
            set { Text = value; }
        }

        public string Placeholder
        {
            get { return _placeholder; }
            set
            {
                _placeholder = value ?? "";
                Invalidate();
            }
        }

        public AppSearchBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            Cursor = Cursors.IBeam;
            TabStop = true;

            Height = DpiManager.ScaleInt(HeightTokens.HeightMd);
            Width = DpiManager.ScaleInt(180);

            IThemePalette pal = ThemeManager.ActivePalette;
            BackColor = (pal != null) ? pal.Surface : Color.FromArgb(23, 26, 31);

            bool isRtl = (Loc.RtlValue == RightToLeft.Yes) || Loc.IsRtl;

            _textBox = new TextBox();
            _textBox.BorderStyle = BorderStyle.None;
            _textBox.Font = DpiManager.GetFont(TypographyRole.Body, isRtl);
            _textBox.BackColor = BackColor;
            _textBox.ForeColor = (pal != null) ? pal.TextPrimary : Color.White;
            _textBox.RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;
            _textBox.TextAlign = isRtl ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            _textBox.Visible = false;

            _textBox.Enter += delegate { UpdateTextBoxVisibility(); Invalidate(); };
            _textBox.Leave += delegate { UpdateTextBoxVisibility(); Invalidate(); };
            _textBox.TextChanged += delegate
            {
                UpdateInnerLayout();
                UpdateTextBoxVisibility();
                Invalidate();
                OnQueryChanged();
            };
            _textBox.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape && !string.IsNullOrEmpty(_textBox.Text))
                {
                    _textBox.Text = "";
                    e.Handled = true;
                    OnSearchCleared();
                }
            };

            Controls.Add(_textBox);
            UpdateInnerLayout();

            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void UpdateTextBoxVisibility()
        {
            if (_textBox == null) return;
            bool shouldBeVisible = _textBox.Focused || !string.IsNullOrEmpty(_textBox.Text);
            if (_textBox.Visible != shouldBeVisible)
            {
                _textBox.Visible = shouldBeVisible;
            }
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            if (_textBox != null)
            {
                _textBox.Visible = true;
                _textBox.Focus();
            }
        }

        private void OnQueryChanged()
        {
            if (QueryChanged != null)
            {
                QueryChanged(this, EventArgs.Empty);
            }
        }

        private void OnSearchCleared()
        {
            if (SearchCleared != null)
            {
                SearchCleared(this, EventArgs.Empty);
            }
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                IThemePalette pal = ThemeManager.ActivePalette;
                if (pal != null)
                {
                    BackColor = pal.Surface;
                    _textBox.BackColor = pal.Surface;
                    _textBox.ForeColor = pal.TextPrimary;
                }
                Invalidate();
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

        public void UpdateInnerLayout()
        {
            if (_textBox == null) return;

            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            _textBox.Font = DpiManager.GetFont(TypographyRole.Body, isRtl);
            _textBox.RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;
            _textBox.TextAlign = isRtl ? HorizontalAlignment.Right : HorizontalAlignment.Left;

            int iconSize = DpiManager.ScaleInt(16);
            int iconPad = DpiManager.ScaleInt(10);
            int clearSize = DpiManager.ScaleInt(14);
            int clearPad = DpiManager.ScaleInt(10);

            bool hasText = !string.IsNullOrEmpty(_textBox.Text);

            int tbY = (Height - _textBox.Height) / 2;

            if (isRtl)
            {
                _iconRect = new Rectangle(Width - iconPad - iconSize, (Height - iconSize) / 2, iconSize, iconSize);
                _clearRect = hasText ? new Rectangle(clearPad, (Height - clearSize) / 2, clearSize, clearSize) : Rectangle.Empty;

                int tbLeft = clearPad + (hasText ? clearSize + DpiManager.ScaleInt(6) : 0);
                int tbRight = Width - iconPad - iconSize - DpiManager.ScaleInt(8);
                int tbWidth = Math.Max(20, tbRight - tbLeft);

                _textBox.Location = new Point(tbLeft, tbY);
                _textBox.Width = tbWidth;
            }
            else
            {
                _iconRect = new Rectangle(iconPad, (Height - iconSize) / 2, iconSize, iconSize);
                _clearRect = hasText ? new Rectangle(Width - clearPad - clearSize, (Height - clearSize) / 2, clearSize, clearSize) : Rectangle.Empty;

                int tbLeft = iconPad + iconSize + DpiManager.ScaleInt(8);
                int tbRight = Width - clearPad - (hasText ? clearSize + DpiManager.ScaleInt(6) : 0);
                int tbWidth = Math.Max(20, tbRight - tbLeft);

                _textBox.Location = new Point(tbLeft, tbY);
                _textBox.Width = tbWidth;
            }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            UpdateInnerLayout();
        }

        protected override void OnRightToLeftChanged(EventArgs e)
        {
            base.OnRightToLeftChanged(e);
            UpdateInnerLayout();
            Invalidate();
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
            _isClearHovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_clearRect.IsEmpty && _clearRect.Contains(e.Location))
            {
                Cursor = Cursors.Hand;
                if (!_isClearHovered)
                {
                    _isClearHovered = true;
                    Invalidate(_clearRect);
                }
            }
            else
            {
                Cursor = Cursors.IBeam;
                if (_isClearHovered)
                {
                    _isClearHovered = false;
                    Invalidate();
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!_clearRect.IsEmpty && _clearRect.Contains(e.Location) && !string.IsNullOrEmpty(_textBox.Text))
            {
                _textBox.Text = "";
                _textBox.Visible = true;
                _textBox.Focus();
                OnSearchCleared();
                return;
            }

            _textBox.Visible = true;
            _textBox.Focus();
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            if (pal == null) return;

            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);

            // Clean background fill matching effective parent surface
            Color surfaceColor = UiDrawingUtils.GetEffectiveParentSurface(this);
            using (SolidBrush surfaceBrush = new SolidBrush(surfaceColor))
            {
                g.FillRectangle(surfaceBrush, ClientRectangle);
            }

            Rectangle rect = new Rectangle(1, 1, Width - 2, Height - 2);

            // Inner fill
            using (SolidBrush b = new SolidBrush(pal.Surface))
            {
                UiDrawingUtils.FillRoundedRectangle(g, b, rect, rad);
            }

            // Border & Focus Ring
            bool isFocused = _textBox.Focused;
            if (isFocused)
            {
                using (Pen pen = new Pen(pal.Accent, 1.5f))
                {
                    UiDrawingUtils.DrawRoundedRectangle(g, pen, rect, rad);
                }
            }
            else if (_isHovered)
            {
                using (Pen pen = new Pen(pal.Border, 1f))
                {
                    UiDrawingUtils.DrawRoundedRectangle(g, pen, rect, rad);
                }
            }
            else
            {
                using (Pen pen = new Pen(pal.BorderSubtle, 1f))
                {
                    UiDrawingUtils.DrawRoundedRectangle(g, pen, rect, rad);
                }
            }

            // Search Icon
            Color iconColor = isFocused ? pal.Accent : pal.TextSecondary;
            UiDrawingUtils.DrawSearchIcon(g, iconColor, _iconRect);

            // Placeholder Text
            if (string.IsNullOrEmpty(_textBox.Text) && !isFocused && !string.IsNullOrEmpty(_placeholder))
            {
                Font phFont = DpiManager.GetFont(TypographyRole.Body, isRtl);
                Rectangle phRect = new Rectangle(_textBox.Location.X, 0, _textBox.Width, Height);

                TextFormatFlags flags = TextFormatFlags.VerticalCenter |
                                        TextFormatFlags.EndEllipsis |
                                        TextFormatFlags.NoPrefix |
                                        TextFormatFlags.SingleLine;

                if (isRtl)
                {
                    flags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
                }
                else
                {
                    flags |= TextFormatFlags.Left;
                }

                TextRenderer.DrawText(g, _placeholder, phFont, phRect, pal.TextMuted, flags);
            }

            // Interactive Clear Button ("X")
            if (!_clearRect.IsEmpty && !string.IsNullOrEmpty(_textBox.Text))
            {
                Color clearColor = _isClearHovered ? pal.TextPrimary : pal.TextMuted;
                UiDrawingUtils.DrawCloseGlyph(g, clearColor, _clearRect, DpiManager.ScaleInt(8));
            }
        }
    }
}
