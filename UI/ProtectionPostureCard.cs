// ═══════════════════════════════════════════════════════════════════════════
// ProtectionPostureCard — Compact Authoritative Posture Surface for v2.1
// USB Shield Pro v2.1 (Phase 4.3 Protection Page Redesign)
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using UsbShieldPro.Models;

namespace UsbShieldPro.UI
{
    public class ProtectionPostureCard : Control
    {
        private string _postureTitle = "";
        private string _postureSubtitle = "";
        private string _auditText = "";
        private StatusBadgeVariant _variant = StatusBadgeVariant.Neutral;
        private readonly StatusBadge _badge;

        public string PostureTitle
        {
            get { return _postureTitle; }
            set { _postureTitle = value ?? ""; Invalidate(); }
        }

        public string PostureSubtitle
        {
            get { return _postureSubtitle; }
            set { _postureSubtitle = value ?? ""; Invalidate(); }
        }

        public string AuditText
        {
            get { return _auditText; }
            set { _auditText = value ?? ""; Invalidate(); }
        }

        public StatusBadgeVariant Variant
        {
            get { return _variant; }
            set
            {
                _variant = value;
                if (_badge != null)
                {
                    _badge.Variant = value;
                }
                Invalidate();
            }
        }

        public string BadgeText
        {
            get { return _badge != null ? _badge.Text : ""; }
            set
            {
                if (_badge != null)
                {
                    _badge.Text = value ?? "";
                    _badge.Visible = !string.IsNullOrEmpty(value);
                    PerformChildLayout();
                    Invalidate();
                }
            }
        }

        public StatusBadge Badge
        {
            get { return _badge; }
        }

        public ProtectionPostureCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = false;
            UpdatePostureHeight();

            _badge = new StatusBadge();
            _badge.TabStop = false;
            _badge.Text = Loc.Get("PostureUnprotected");
            _badge.Variant = StatusBadgeVariant.Neutral;
            Controls.Add(_badge);

            IThemePalette pal = ThemeManager.ActivePalette;
            if (pal != null) BackColor = pal.Surface;

            ThemeManager.ThemeChanged += OnThemeChanged;
            PerformLayout();
        }

        public void UpdatePostureHeight()
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            Height = DpiManager.ScaleInt(isRtl ? 60 : 54);
            PerformChildLayout();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            if (pal != null) BackColor = pal.Surface;
            Invalidate();
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
            PerformChildLayout();
        }

        protected override void OnRightToLeftChanged(EventArgs e)
        {
            base.OnRightToLeftChanged(e);
            UpdatePostureHeight();
            PerformChildLayout();
            Invalidate();
        }

        public void SetPosture(string title, string subtitle, StatusBadgeVariant variant, string badgeText, string auditText)
        {
            _postureTitle = title ?? "";
            _postureSubtitle = subtitle ?? "";
            _variant = variant;
            _auditText = auditText ?? "";

            if (_badge != null)
            {
                _badge.Variant = variant;
                _badge.Text = badgeText ?? "";
                _badge.Visible = !string.IsNullOrEmpty(badgeText);
            }

            PerformChildLayout();
            Invalidate();
        }

        public void PerformChildLayout()
        {
            if (_badge == null) return;

            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int padX = DpiManager.ScaleInt(SpacingTokens.Space16);

            int badgeW = _badge.Width;
            int badgeH = _badge.Height;
            int badgeY = (Height - badgeH) / 2;

            int badgeX = isRtl ? padX : (Width - padX - badgeW);
            _badge.Location = new Point(badgeX, badgeY);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;

            int radius = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Background
            using (SolidBrush bgBrush = new SolidBrush(pal.Surface))
            {
                UiDrawingUtils.FillRoundedRectangle(g, bgBrush, rect, radius);
            }

            // Border
            using (Pen borderPen = new Pen(pal.BorderSubtle, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, borderPen, rect, radius);
            }

            // Variant Color selection
            Color stateColor;
            Color stateSoft;
            switch (_variant)
            {
                case StatusBadgeVariant.Success:
                    stateColor = pal.Success;
                    stateSoft = pal.SuccessSoft;
                    break;
                case StatusBadgeVariant.Warning:
                    stateColor = pal.Warning;
                    stateSoft = pal.WarningSoft;
                    break;
                case StatusBadgeVariant.Danger:
                    stateColor = pal.Danger;
                    stateSoft = pal.DangerSoft;
                    break;
                case StatusBadgeVariant.Info:
                    stateColor = pal.Info;
                    stateSoft = pal.InfoSoft;
                    break;
                default:
                    stateColor = pal.TextSecondary;
                    stateSoft = pal.SurfaceHover;
                    break;
            }

            int padX = DpiManager.ScaleInt(SpacingTokens.Space16);
            int shieldSize = DpiManager.ScaleInt(30);
            int shieldY = (Height - shieldSize) / 2;
            int shieldX = isRtl ? (Width - padX - shieldSize) : padX;
            Rectangle shieldTile = new Rectangle(shieldX, shieldY, shieldSize, shieldSize);

            // Shield icon background circle / rounded tile
            using (SolidBrush tileBrush = new SolidBrush(stateSoft))
            {
                UiDrawingUtils.FillRoundedRectangle(g, tileBrush, shieldTile, radius);
            }

            // Vector Shield Icon inside tile
            Rectangle shieldIconRect = new Rectangle(shieldTile.X + DpiManager.ScaleInt(6),
                                                     shieldTile.Y + DpiManager.ScaleInt(6),
                                                     shieldTile.Width - DpiManager.ScaleInt(12),
                                                     shieldTile.Height - DpiManager.ScaleInt(12));
            UiDrawingUtils.DrawNavShield(g, stateColor, shieldIconRect);

            // Text calculations
            int textGap = DpiManager.ScaleInt(SpacingTokens.Space12);
            int textStartX;
            int textAvailableWidth;

            if (isRtl)
            {
                int badgeRight = _badge.Visible ? _badge.Right : padX;
                int trailingBoundary = badgeRight + DpiManager.ScaleInt(SpacingTokens.Space16);
                textStartX = trailingBoundary;
                textAvailableWidth = (shieldX - textGap) - trailingBoundary;
            }
            else
            {
                int badgeLeft = _badge.Visible ? _badge.Left : (Width - padX);
                int trailingBoundary = badgeLeft - DpiManager.ScaleInt(SpacingTokens.Space16);
                textStartX = shieldX + shieldSize + textGap;
                textAvailableWidth = trailingBoundary - textStartX;
            }

            if (textAvailableWidth > 20)
            {
                Font titleFont = DpiManager.GetFont(TypographyRole.BodyStrong, isRtl);
                Font subFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

                // Eliminate VerticalCenter to prevent clipping Arabic descenders and bottom dots
                TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping;
                if (isRtl)
                {
                    flags |= TextFormatFlags.RightToLeft | TextFormatFlags.Right;
                }
                else
                {
                    flags |= TextFormatFlags.Left;
                }

                int titleH = DpiManager.ScaleInt(isRtl ? 22 : 20);
                int subH = DpiManager.ScaleInt(isRtl ? 22 : 18);
                int gapY = DpiManager.ScaleInt(1);
                int totalTextH = titleH + gapY + subH;
                int titleY = Math.Max(DpiManager.ScaleInt(4), (Height - totalTextH) / 2);

                Rectangle titleRect = new Rectangle(textStartX, titleY, textAvailableWidth, titleH);
                TextRenderer.DrawText(g, _postureTitle, titleFont, titleRect, pal.TextPrimary, flags);

                int subY = titleY + titleH + gapY;
                Rectangle subRect = new Rectangle(textStartX, subY, textAvailableWidth, subH);
                TextRenderer.DrawText(g, _postureSubtitle, subFont, subRect, pal.TextSecondary, flags);
            }
        }
    }
}
