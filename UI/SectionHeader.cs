// ═══════════════════════════════════════════════════════════════════════════
// SectionHeader — Tier 1 Section Header Primitive for v2.1
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
    public class SectionHeader : Control
    {
        private string _title = "";
        private string _subtitle = "";
        private Image _icon = null;
        private Control _trailingAction = null;
        private StatusBadge _statusBadge = null;
        private bool _showDivider = false;

        public SectionHeader()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = false;
            Height = DpiManager.ScaleInt(40);

            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public string Title
        {
            get { return _title; }
            set { if (_title != value) { _title = value; Invalidate(); } }
        }

        public string Subtitle
        {
            get { return _subtitle; }
            set
            {
                if (_subtitle != value)
                {
                    _subtitle = value;
                    if (!string.IsNullOrEmpty(_subtitle) && Height < DpiManager.ScaleInt(54))
                    {
                        Height = DpiManager.ScaleInt(54);
                    }
                    Invalidate();
                }
            }
        }

        public Image Icon
        {
            get { return _icon; }
            set { if (_icon != value) { _icon = value; Invalidate(); } }
        }

        public bool ShowDivider
        {
            get { return _showDivider; }
            set { if (_showDivider != value) { _showDivider = value; Invalidate(); } }
        }

        public Control TrailingAction
        {
            get { return _trailingAction; }
            set
            {
                if (_trailingAction != value)
                {
                    if (_trailingAction != null && Controls.Contains(_trailingAction))
                    {
                        Controls.Remove(_trailingAction);
                    }
                    _trailingAction = value;
                    if (_trailingAction != null)
                    {
                        Controls.Add(_trailingAction);
                        LayoutControls();
                    }
                    Invalidate();
                }
            }
        }

        public StatusBadge StatusBadgeControl
        {
            get { return _statusBadge; }
            set
            {
                if (_statusBadge != value)
                {
                    if (_statusBadge != null && Controls.Contains(_statusBadge))
                    {
                        Controls.Remove(_statusBadge);
                    }
                    _statusBadge = value;
                    if (_statusBadge != null)
                    {
                        Controls.Add(_statusBadge);
                        LayoutControls();
                    }
                    Invalidate();
                }
            }
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed)
            {
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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutControls();
        }

        protected override void OnRightToLeftChanged(EventArgs e)
        {
            base.OnRightToLeftChanged(e);
            LayoutControls();
            Invalidate();
        }

        private void LayoutControls()
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes);
            int pad = DpiManager.ScaleInt(SpacingTokens.Space8);

            if (isRtl)
            {
                int curLeft = pad;
                if (_trailingAction != null)
                {
                    _trailingAction.Location = new Point(curLeft, (Height - _trailingAction.Height) / 2);
                    curLeft += _trailingAction.Width + pad;
                }

                if (_statusBadge != null)
                {
                    _statusBadge.Location = new Point(curLeft, (Height - _statusBadge.Height) / 2);
                }
            }
            else
            {
                int curRight = Width - pad;
                if (_trailingAction != null)
                {
                    curRight -= _trailingAction.Width;
                    _trailingAction.Location = new Point(curRight, (Height - _trailingAction.Height) / 2);
                    curRight -= pad;
                }

                if (_statusBadge != null)
                {
                    curRight -= _statusBadge.Width;
                    _statusBadge.Location = new Point(curRight, (Height - _statusBadge.Height) / 2);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes);

            // Compute right/left bounds occupied by badge/action
            int reservedLeft = 0;
            int reservedRight = 0;
            int pad = DpiManager.ScaleInt(SpacingTokens.Space8);

            if (_trailingAction != null)
            {
                if (isRtl) reservedLeft += _trailingAction.Width + pad;
                else reservedRight += _trailingAction.Width + pad;
            }
            if (_statusBadge != null)
            {
                if (isRtl) reservedLeft += _statusBadge.Width + pad;
                else reservedRight += _statusBadge.Width + pad;
            }

            int iconSize = DpiManager.ScaleInt(IconTokens.IconMd);
            int iconSpacing = DpiManager.ScaleInt(SpacingTokens.Space8);

            Font titleFont = DpiManager.GetFont(TypographyRole.Subtitle, isRtl);
            Font subFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            if (isRtl)
            {
                int rightEdge = Width - pad;
                if (_icon != null)
                {
                    int iconX = rightEdge - iconSize;
                    int iconY = string.IsNullOrEmpty(_subtitle) ? (Height - iconSize) / 2 : DpiManager.ScaleInt(SpacingTokens.Space4);
                    g.DrawImage(_icon, iconX, iconY, iconSize, iconSize);
                    rightEdge -= (iconSize + iconSpacing);
                }

                int textW = rightEdge - (reservedLeft + pad);
                if (textW > 0)
                {
                    if (string.IsNullOrEmpty(_subtitle))
                    {
                        Rectangle textRect = new Rectangle(reservedLeft + pad, 0, textW, Height);
                        TextRenderer.DrawText(g, _title ?? "", titleFont, textRect, pal.TextPrimary,
                            TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                    }
                    else
                    {
                        int tH = DpiManager.ScaleInt(22);
                        Rectangle tRect = new Rectangle(reservedLeft + pad, DpiManager.ScaleInt(SpacingTokens.Space4), textW, tH);
                        TextRenderer.DrawText(g, _title ?? "", titleFont, tRect, pal.TextPrimary,
                            TextFormatFlags.SingleLine | TextFormatFlags.Bottom | TextFormatFlags.Right | TextFormatFlags.RightToLeft);

                        Rectangle sRect = new Rectangle(reservedLeft + pad, tH + DpiManager.ScaleInt(SpacingTokens.Space2), textW, Height - tH - DpiManager.ScaleInt(SpacingTokens.Space4));
                        TextRenderer.DrawText(g, _subtitle ?? "", subFont, sRect, pal.TextSecondary,
                            TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                    }
                }
            }
            else
            {
                int leftEdge = pad;
                if (_icon != null)
                {
                    int iconY = string.IsNullOrEmpty(_subtitle) ? (Height - iconSize) / 2 : DpiManager.ScaleInt(SpacingTokens.Space4);
                    g.DrawImage(_icon, leftEdge, iconY, iconSize, iconSize);
                    leftEdge += (iconSize + iconSpacing);
                }

                int textW = Width - reservedRight - pad - leftEdge;
                if (textW > 0)
                {
                    if (string.IsNullOrEmpty(_subtitle))
                    {
                        Rectangle textRect = new Rectangle(leftEdge, 0, textW, Height);
                        TextRenderer.DrawText(g, _title ?? "", titleFont, textRect, pal.TextPrimary,
                            TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                    }
                    else
                    {
                        int tH = DpiManager.ScaleInt(22);
                        Rectangle tRect = new Rectangle(leftEdge, DpiManager.ScaleInt(SpacingTokens.Space4), textW, tH);
                        TextRenderer.DrawText(g, _title ?? "", titleFont, tRect, pal.TextPrimary,
                            TextFormatFlags.SingleLine | TextFormatFlags.Bottom | TextFormatFlags.Left);

                        Rectangle sRect = new Rectangle(leftEdge, tH + DpiManager.ScaleInt(SpacingTokens.Space2), textW, Height - tH - DpiManager.ScaleInt(SpacingTokens.Space4));
                        TextRenderer.DrawText(g, _subtitle ?? "", subFont, sRect, pal.TextSecondary,
                            TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Left);
                    }
                }
            }

            // Draw Bottom Divider
            if (_showDivider)
            {
                using (Pen divPen = new Pen(pal.BorderSubtle, 1f))
                {
                    g.DrawLine(divPen, pad, Height - 1, Width - pad, Height - 1);
                }
            }
        }
    }
}
