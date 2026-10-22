// ═══════════════════════════════════════════════════════════════════════════
// RecoveryControls — Production UI Surfaces for v2.1 Recovery Workspace
// USB Shield Pro v2.1 (Phase 4.6 Recovery Page Redesign)
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;
using UsbShieldPro.Core;
using UsbShieldPro.Models;
using UsbShieldPro.Persistence;

namespace UsbShieldPro.UI
{
    /// <summary>
    /// Prominent, restrained recovery posture card displaying authoritative baseline readiness,
    /// timestamp, recorded registry settings count, and journal status.
    /// </summary>
    public class RecoveryPostureCard : Control
    {
        private string _postureTitle = "";
        private string _postureSubtitle = "";
        private string _baselineTimestamp = "—";
        private int _entriesCount = 0;
        private bool _journalExists = false;
        private StatusBadgeVariant _variant = StatusBadgeVariant.Neutral;
        private readonly StatusBadge _badge;

        public StatusBadge Badge { get { return _badge; } }
        public string PostureTitle { get { return _postureTitle; } set { _postureTitle = value ?? ""; Invalidate(); } }
        public string PostureSubtitle { get { return _postureSubtitle; } set { _postureSubtitle = value ?? ""; Invalidate(); } }
        public string BaselineTimestamp { get { return _baselineTimestamp; } set { _baselineTimestamp = value ?? "—"; Invalidate(); } }
        public int EntriesCount { get { return _entriesCount; } set { _entriesCount = value; Invalidate(); } }
        public bool JournalExists { get { return _journalExists; } set { _journalExists = value; Invalidate(); } }
        public StatusBadgeVariant Variant { get { return _variant; } set { _variant = value; if (_badge != null) _badge.Variant = value; Invalidate(); } }

        public RecoveryPostureCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = false;
            _badge = new StatusBadge();
            _badge.TabStop = false;
            _badge.Text = Loc.Get("RecoveryStatusMissing");
            _badge.Variant = StatusBadgeVariant.Neutral;
            Controls.Add(_badge);

            UpdateHeight();
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public void UpdateHeight()
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            Height = DpiManager.ScaleInt(isRtl ? 118 : 104);
            PerformChildLayout();
            Invalidate();
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
            UpdateHeight();
        }

        public void SetData(StateJournal journal, bool journalFileExists)
        {
            _journalExists = journalFileExists;
            if (journal != null && journal.BaselineEntries != null && journal.BaselineEntries.Count > 0)
            {
                _variant = StatusBadgeVariant.Success;
                _postureTitle = Loc.Get("RecoveryStatusReady");
                _postureSubtitle = Loc.Get("RecoveryStatusReadyDesc");
                _baselineTimestamp = journal.BaselineCapturedUtc != DateTime.MinValue
                    ? journal.BaselineCapturedUtc.ToString("yyyy-MM-dd HH:mm 'UTC'")
                    : "—";
                _entriesCount = journal.BaselineEntries.Count;
                _badge.Text = Loc.Get("RecoveryStatusReady");
                _badge.Variant = StatusBadgeVariant.Success;
            }
            else
            {
                _variant = StatusBadgeVariant.Neutral;
                _postureTitle = Loc.Get("RecoveryStatusNone");
                _postureSubtitle = Loc.Get("RecoveryStatusNoneDesc");
                _baselineTimestamp = "—";
                _entriesCount = 0;
                _badge.Text = Loc.Get("RecoveryStatusMissing");
                _badge.Variant = StatusBadgeVariant.Neutral;
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
            int badgeY = DpiManager.ScaleInt(14);

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
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // 1. Background & Border
            using (SolidBrush bgBrush = new SolidBrush(pal.Surface))
            {
                UiDrawingUtils.FillRoundedRectangle(g, bgBrush, rect, rad);
            }
            using (Pen borderPen = new Pen(pal.BorderSubtle, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, borderPen, rect, rad);
            }

            // 2. Icon Tile
            int padX = DpiManager.ScaleInt(SpacingTokens.Space16);
            int iconSize = DpiManager.ScaleInt(32);
            int iconY = DpiManager.ScaleInt(12);
            int iconX = isRtl ? (Width - padX - iconSize) : padX;
            Rectangle iconTile = new Rectangle(iconX, iconY, iconSize, iconSize);

            Color stateColor = (_variant == StatusBadgeVariant.Success) ? pal.Success : pal.TextSecondary;
            Color stateSoft = (_variant == StatusBadgeVariant.Success) ? pal.SuccessSoft : pal.SurfaceElevated;

            using (SolidBrush tileBrush = new SolidBrush(stateSoft))
            {
                UiDrawingUtils.FillRoundedRectangle(g, tileBrush, iconTile, DpiManager.ScaleInt(6));
            }
            Rectangle navRect = new Rectangle(iconTile.X + DpiManager.ScaleInt(6),
                                              iconTile.Y + DpiManager.ScaleInt(6),
                                              iconTile.Width - DpiManager.ScaleInt(12),
                                              iconTile.Height - DpiManager.ScaleInt(12));
            UiDrawingUtils.DrawNavRecovery(g, stateColor, navRect);

            // 3. Title & Subtitle
            int textGap = DpiManager.ScaleInt(SpacingTokens.Space12);
            int textStartX;
            int textAvailW;

            if (isRtl)
            {
                int textRight = iconX - textGap;
                int textLeft = _badge.Right + DpiManager.ScaleInt(SpacingTokens.Space12);
                textStartX = Math.Max(padX, textLeft);
                textAvailW = Math.Max(20, textRight - textStartX);
            }
            else
            {
                textStartX = iconX + iconSize + textGap;
                int textRight = _badge.Left - DpiManager.ScaleInt(SpacingTokens.Space12);
                textAvailW = Math.Max(20, textRight - textStartX);
            }

            Font titleFont = DpiManager.GetFont(TypographyRole.Subtitle, isRtl);
            Font subFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            int titleH = DpiManager.ScaleInt(isRtl ? 24 : 20);
            int subH = DpiManager.ScaleInt(isRtl ? 22 : 18);
            Rectangle titleRect = new Rectangle(textStartX, DpiManager.ScaleInt(10), textAvailW, titleH);
            Rectangle subRect = new Rectangle(textStartX, DpiManager.ScaleInt(10) + titleH + DpiManager.ScaleInt(2), textAvailW, subH);

            TextFormatFlags textFlags = TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping;
            if (isRtl) textFlags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
            else textFlags |= TextFormatFlags.Left;

            TextRenderer.DrawText(g, _postureTitle, titleFont, titleRect, pal.TextPrimary, textFlags);
            TextRenderer.DrawText(g, _postureSubtitle, subFont, subRect, pal.TextSecondary, textFlags);

            // 4. Horizontal Divider
            int splitY = DpiManager.ScaleInt(isRtl ? 62 : 56);
            using (Pen divPen = new Pen(pal.BorderSubtle, 1f))
            {
                g.DrawLine(divPen, padX, splitY, Width - padX, splitY);
            }

            // 5. Metric Summary Strip (3 Columns)
            int metricsY = splitY + DpiManager.ScaleInt(8);
            int metricsW = Width - (padX * 2);
            int colW = metricsW / 3;

            Font metricLabelFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            Font metricValFont = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            string[] labels = new string[]
            {
                Loc.Get("RecoveryMetricCaptured"),
                Loc.Get("RecoveryMetricTracked"),
                Loc.Get("RecoveryMetricJournal")
            };

            string[] values = new string[]
            {
                _baselineTimestamp,
                string.Format(Loc.Get("RecoveryValuesCount"), _entriesCount),
                _journalExists ? Loc.Get("RecoveryStatusAvailable") : Loc.Get("RecoveryStatusMissing")
            };

            for (int i = 0; i < 3; i++)
            {
                int colX = isRtl ? (Width - padX - ((i + 1) * colW)) : (padX + (i * colW));
                Rectangle colRect = new Rectangle(colX, metricsY, colW - DpiManager.ScaleInt(8), Height - metricsY - DpiManager.ScaleInt(6));

                int lblH = DpiManager.ScaleInt(isRtl ? 20 : 16);
                int valH = DpiManager.ScaleInt(isRtl ? 24 : 20);
                Rectangle lblRect = new Rectangle(colRect.X, colRect.Y, colRect.Width, lblH);
                Rectangle valRect = new Rectangle(colRect.X, colRect.Y + lblH + DpiManager.ScaleInt(2), colRect.Width, valH);

                TextFormatFlags metricFlags = TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping;
                if (isRtl) metricFlags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
                else metricFlags |= TextFormatFlags.Left;

                TextRenderer.DrawText(g, labels[i], metricLabelFont, lblRect, pal.TextMuted, metricFlags);
                TextRenderer.DrawText(g, values[i], metricValFont, valRect, pal.TextPrimary, metricFlags);
            }
        }
    }

    /// <summary>
    /// Restrained informational MessageBar communicating the truthful pre-Shield restoration contract:
    /// restores exact pre-Shield values, and removes values that were originally absent.
    /// </summary>
    public class RecoverySemanticsMessageBar : Control
    {
        private string _text = "";

        public string MessageText
        {
            get { return _text; }
            set { _text = value ?? ""; Invalidate(); }
        }

        public RecoverySemanticsMessageBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            TabStop = false;
            _text = Loc.Get("RecoverySemanticsExplanation");
            ThemeManager.ThemeChanged += OnThemeChanged;
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
            }
            base.Dispose(disposing);
        }

        public void UpdateLanguage()
        {
            _text = Loc.Get("RecoverySemanticsExplanation");
            Invalidate();
        }

        public int CalculateRequiredHeight(int targetWidth)
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int pad = DpiManager.ScaleInt(SpacingTokens.Space14);
            int iconSize = DpiManager.ScaleInt(16);
            int availTextW = Math.Max(50, targetWidth - (pad * 2) - iconSize - DpiManager.ScaleInt(12));

            Font font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            Size textSize = TextRenderer.MeasureText(_text ?? "", font, new Size(availTextW, 0),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

            int minH = DpiManager.ScaleInt(44);
            int computedH = textSize.Height + DpiManager.ScaleInt(18);
            return Math.Max(minH, computedH);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            IThemePalette pal = ThemeManager.ActivePalette;
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Container Surface & Border
            using (SolidBrush bg = new SolidBrush(pal.SurfaceElevated))
            {
                UiDrawingUtils.FillRoundedRectangle(g, bg, rect, rad);
            }
            using (Pen borderPen = new Pen(pal.BorderSubtle, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, borderPen, rect, rad);
            }

            int pad = DpiManager.ScaleInt(SpacingTokens.Space14);
            int iconSize = DpiManager.ScaleInt(16);
            int iconY = (Height - iconSize) / 2;

            Font font = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            if (isRtl)
            {
                int iconX = Width - pad - iconSize;
                DrawInfoIcon(g, pal.TextSecondary, iconX, iconY, iconSize);

                int textRight = iconX - DpiManager.ScaleInt(10);
                int textLeft = pad;
                int textW = Math.Max(20, textRight - textLeft);

                Rectangle textRect = new Rectangle(textLeft, (Height - font.Height * 2) / 2, textW, font.Height * 2 + DpiManager.ScaleInt(4));
                TextRenderer.DrawText(g, _text, font, textRect, pal.TextPrimary,
                    TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix);
            }
            else
            {
                int iconX = pad;
                DrawInfoIcon(g, pal.TextSecondary, iconX, iconY, iconSize);

                int textLeft = iconX + iconSize + DpiManager.ScaleInt(10);
                int textRight = Width - pad;
                int textW = Math.Max(20, textRight - textLeft);

                Rectangle textRect = new Rectangle(textLeft, (Height - font.Height * 2) / 2, textW, font.Height * 2 + DpiManager.ScaleInt(4));
                TextRenderer.DrawText(g, _text, font, textRect, pal.TextPrimary,
                    TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            }
        }

        private void DrawInfoIcon(Graphics g, Color color, int x, int y, int size)
        {
            using (Pen pen = new Pen(color, 1.4f))
            {
                g.DrawEllipse(pen, x, y, size - 1, size - 1);

                int cx = x + size / 2;
                int dotY = y + (int)(size * 0.28f);
                using (SolidBrush b = new SolidBrush(color))
                {
                    g.FillEllipse(b, cx - 1, dotY, 2, 2);
                }
                g.DrawLine(pen, cx, y + (int)(size * 0.46f), cx, y + (int)(size * 0.74f));
            }
        }
    }

    /// <summary>
    /// Displays a single recovery file or journal artifact with its name, status badge, and copyable LTR path.
    /// </summary>
    public class RecoveryArtifactRow : Panel
    {
        private readonly Label _titleLabel;
        private readonly StatusBadge _badge;
        private readonly IdentifierField _identifierField;

        public string ArtifactTitle
        {
            get { return _titleLabel.Text; }
            set { _titleLabel.Text = value ?? ""; Invalidate(); }
        }

        public string ArtifactPath
        {
            get { return _identifierField.Value; }
            set { _identifierField.Value = value ?? ""; }
        }

        private string _description = "";
        private ToolTip _descToolTip;
        public string Description
        {
            get { return _description; }
            set
            {
                _description = value ?? "";
                if (_titleLabel != null && !string.IsNullOrEmpty(_description))
                {
                    if (_descToolTip == null)
                    {
                        _descToolTip = new ToolTip();
                    }
                    _descToolTip.SetToolTip(_titleLabel, _description);
                }
                else if (_descToolTip != null && _titleLabel != null)
                {
                    _descToolTip.SetToolTip(_titleLabel, null);
                }
            }
        }

        public StatusBadge StatusBadge { get { return _badge; } }
        public IdentifierField Field { get { return _identifierField; } }

        public RecoveryArtifactRow(string title, string copyTooltip)
        {
            Height = DpiManager.ScaleInt(62);
            IThemePalette pal = ThemeManager.ActivePalette;
            BackColor = pal.Surface;

            _titleLabel = new Label();
            _titleLabel.Text = title;
            _titleLabel.AutoSize = true;
            _titleLabel.Font = DpiManager.GetFont(TypographyRole.Caption, Loc.IsRtl);
            _titleLabel.ForeColor = pal.TextPrimary;
            _titleLabel.BackColor = pal.Surface;

            _badge = new StatusBadge();
            _badge.TabStop = false;
            _badge.Variant = StatusBadgeVariant.Neutral;
            _badge.Text = Loc.Get("RecoveryStatusMissing");

            _identifierField = new IdentifierField(copyTooltip);

            Controls.Add(_titleLabel);
            Controls.Add(_badge);
            Controls.Add(_identifierField);

            PerformRowLayout();
            Resize += delegate { PerformRowLayout(); };
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            UpdateTheme();
        }

        public void UpdateTheme()
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            BackColor = pal.Surface;
            if (_titleLabel != null)
            {
                _titleLabel.ForeColor = pal.TextPrimary;
                _titleLabel.BackColor = pal.Surface;
            }
            Invalidate();
        }

        public void PerformRowLayout()
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int padX = 0;
            int topH = DpiManager.ScaleInt(20);

            _titleLabel.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            if (isRtl)
            {
                _titleLabel.Location = new Point(Width - padX - _titleLabel.Width, DpiManager.ScaleInt(2));
                _badge.Location = new Point(padX, DpiManager.ScaleInt(2));
            }
            else
            {
                _titleLabel.Location = new Point(padX, DpiManager.ScaleInt(2));
                _badge.Location = new Point(Width - padX - _badge.Width, DpiManager.ScaleInt(2));
            }

            int fieldY = topH + DpiManager.ScaleInt(4);
            _identifierField.Location = new Point(0, fieldY);
            _identifierField.Size = new Size(Width, DpiManager.ScaleInt(30));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                if (_descToolTip != null)
                {
                    _descToolTip.Dispose();
                    _descToolTip = null;
                }
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Section card presenting authoritative state journal and offline recovery artifacts (.bat and .reg).
    /// </summary>
    public class RecoveryArtifactsCard : Panel
    {
        private readonly Label _headerTitle;
        private readonly Label _headerDesc;
        private readonly RecoveryArtifactRow _rowJournal;
        private readonly RecoveryArtifactRow _rowScript;
        private readonly RecoveryArtifactRow _rowReg;

        public RecoveryArtifactRow RowJournal { get { return _rowJournal; } }
        public RecoveryArtifactRow RowScript { get { return _rowScript; } }
        public RecoveryArtifactRow RowReg { get { return _rowReg; } }

        public RecoveryArtifactsCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            TabStop = false;
            Padding = new Padding(DpiManager.ScaleInt(16));
            IThemePalette pal = ThemeManager.ActivePalette;

            bool isRtl = Loc.IsRtl;
            _headerTitle = new Label();
            _headerTitle.Text = Loc.Get("RecoverySecArtifacts");
            _headerTitle.Font = DpiManager.GetFont(TypographyRole.Subtitle, isRtl);
            _headerTitle.ForeColor = pal.TextPrimary;
            _headerTitle.BackColor = pal.Surface;
            _headerTitle.AutoSize = true;

            _headerDesc = new Label();
            _headerDesc.Text = Loc.Get("RecoveryArtifactsPurpose");
            _headerDesc.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            _headerDesc.ForeColor = pal.TextSecondary;
            _headerDesc.BackColor = pal.Surface;
            _headerDesc.AutoSize = true;

            _rowJournal = new RecoveryArtifactRow(Loc.Get("RecoveryJournalLabel"), Loc.Get("RecoveryCopyPath"));
            _rowJournal.Description = Loc.Get("RecoveryJournalLabel");
            _rowScript = new RecoveryArtifactRow(Loc.Get("RecoveryScriptLabel"), Loc.Get("RecoveryCopyPath"));
            _rowScript.Description = Loc.Get("RecoveryScriptLabel");
            _rowReg = new RecoveryArtifactRow(Loc.Get("RecoveryRegLabel"), Loc.Get("RecoveryCopyPath"));
            _rowReg.Description = Loc.Get("RecoveryRegDesc");

            Controls.Add(_headerTitle);
            Controls.Add(_headerDesc);
            Controls.Add(_rowJournal);
            Controls.Add(_rowScript);
            Controls.Add(_rowReg);

            RefreshArtifacts();
            UpdateCardLayout();
            Resize += delegate { UpdateCardLayout(); };
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            if (_headerTitle != null)
            {
                _headerTitle.ForeColor = pal.TextPrimary;
                _headerTitle.BackColor = pal.Surface;
            }
            if (_headerDesc != null)
            {
                _headerDesc.ForeColor = pal.TextSecondary;
                _headerDesc.BackColor = pal.Surface;
            }
            if (_rowJournal != null) _rowJournal.UpdateTheme();
            if (_rowScript != null) _rowScript.UpdateTheme();
            if (_rowReg != null) _rowReg.UpdateTheme();
            Invalidate();
        }

        public void RefreshArtifacts()
        {
            string journalPath = JournalRepository.GetJournalFilePath();
            string cliCmd = RecoveryManager.GetRestoreCliCommand();
            string regPath = RecoveryManager.GetRecoveryRegPath();

            bool journalOk = File.Exists(journalPath);
            bool regOk = File.Exists(regPath);

            _rowJournal.ArtifactPath = journalPath;
            _rowJournal.StatusBadge.Variant = journalOk ? StatusBadgeVariant.Success : StatusBadgeVariant.Neutral;
            _rowJournal.StatusBadge.Text = journalOk ? Loc.Get("RecoveryStatusAvailable") : Loc.Get("RecoveryStatusMissing");

            _rowScript.ArtifactPath = cliCmd;
            _rowScript.StatusBadge.Variant = StatusBadgeVariant.Success;
            _rowScript.StatusBadge.Text = Loc.Get("RecoveryStatusReadyBadge");

            _rowReg.ArtifactPath = regPath;
            _rowReg.StatusBadge.Variant = regOk ? StatusBadgeVariant.Success : StatusBadgeVariant.Neutral;
            _rowReg.StatusBadge.Text = regOk ? Loc.Get("RecoveryStatusReadyBadge") : Loc.Get("RecoveryStatusMissing");
        }

        public void UpdateCardLayout()
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int pad = DpiManager.ScaleInt(16);
            int innerW = Width - (pad * 2);

            _headerTitle.Font = DpiManager.GetFont(TypographyRole.Subtitle, isRtl);
            _headerDesc.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            int curY = pad;

            if (isRtl)
            {
                _headerTitle.Location = new Point(Width - pad - _headerTitle.Width, curY);
                curY += _headerTitle.Height + DpiManager.ScaleInt(4);
                _headerDesc.Location = new Point(Width - pad - _headerDesc.Width, curY);
            }
            else
            {
                _headerTitle.Location = new Point(pad, curY);
                curY += _headerTitle.Height + DpiManager.ScaleInt(4);
                _headerDesc.Location = new Point(pad, curY);
            }
            curY += _headerDesc.Height + DpiManager.ScaleInt(16);

            RecoveryArtifactRow[] rows = new RecoveryArtifactRow[] { _rowJournal, _rowScript, _rowReg };
            foreach (var r in rows)
            {
                r.Location = new Point(pad, curY);
                r.Width = innerW;
                r.PerformRowLayout();
                curY += r.Height + DpiManager.ScaleInt(12);
            }

            Height = curY + DpiManager.ScaleInt(6);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            IThemePalette pal = ThemeManager.ActivePalette;

            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            using (SolidBrush bg = new SolidBrush(pal.Surface))
            {
                UiDrawingUtils.FillRoundedRectangle(g, bg, rect, rad);
            }
            using (Pen borderPen = new Pen(pal.BorderSubtle, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, borderPen, rect, rad);
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
    }

    /// <summary>
    /// Item row in the Technical Details accordion displaying one recorded registry setting.
    /// Strictly renders technical registry keys and data LTR monospace.
    /// </summary>
    public class RecoveryScopeItemRow : Panel
    {
        private readonly Label _keyPathLabel;
        private readonly Label _dataLabel;
        private readonly StatusBadge _badge;

        public RecoveryScopeItemRow(RegistryOriginalValue entry)
        {
            Height = DpiManager.ScaleInt(52);
            bool isRtl = Loc.IsRtl;
            IThemePalette pal = ThemeManager.ActivePalette;
            BackColor = pal.Surface;

            string fullIdentifier = (entry != null)
                ? string.Format("{0}\\{1} -> {2}", entry.Hive ?? "HKLM", entry.KeyPath ?? "", entry.ValueName ?? "@")
                : "—";

            _keyPathLabel = new Label();
            _keyPathLabel.Text = fullIdentifier;
            _keyPathLabel.Font = new Font("Consolas", 8.5f, FontStyle.Regular);
            _keyPathLabel.ForeColor = pal.TextPrimary;
            _keyPathLabel.BackColor = pal.Surface;
            _keyPathLabel.AutoSize = true;
            _keyPathLabel.RightToLeft = RightToLeft.No; // CRITICAL: strictly LTR

            _dataLabel = new Label();
            if (entry != null && entry.ExistedBefore)
            {
                string valStr = entry.OriginalData != null ? entry.OriginalData.ToString() : "(null)";
                _dataLabel.Text = string.Format("{0} | Original Data: {1}", entry.ValueKind ?? "REG_SZ", valStr);
            }
            else
            {
                _dataLabel.Text = Loc.Get("RecoveryValueAbsent");
            }
            _dataLabel.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            _dataLabel.ForeColor = pal.TextSecondary;
            _dataLabel.BackColor = pal.Surface;
            _dataLabel.AutoSize = true;

            _badge = new StatusBadge();
            _badge.TabStop = false;
            if (entry != null && entry.ExistedBefore)
            {
                _badge.Variant = StatusBadgeVariant.Success;
                _badge.Text = Loc.Get("RecoveryValuePresent");
            }
            else
            {
                _badge.Variant = StatusBadgeVariant.Info;
                _badge.Text = Loc.Get("RecoveryValueAbsent");
            }

            Controls.Add(_keyPathLabel);
            Controls.Add(_dataLabel);
            Controls.Add(_badge);

            PerformItemLayout();
            Resize += delegate { PerformItemLayout(); };
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            UpdateTheme();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }

        public void UpdateTheme()
        {
            IThemePalette cp = ThemeManager.ActivePalette;
            BackColor = cp.Surface;
            if (_keyPathLabel != null)
            {
                _keyPathLabel.ForeColor = cp.TextPrimary;
                _keyPathLabel.BackColor = cp.Surface;
            }
            if (_dataLabel != null)
            {
                _dataLabel.ForeColor = cp.TextSecondary;
                _dataLabel.BackColor = cp.Surface;
            }
            Invalidate();
        }

        public void PerformItemLayout()
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int padX = DpiManager.ScaleInt(4);

            if (isRtl)
            {
                // In RTL: Badge on left, text on right (with KeyPath LTR aligned)
                _badge.Location = new Point(padX, DpiManager.ScaleInt(6));
                int availW = Width - _badge.Width - (padX * 3);
                _keyPathLabel.Location = new Point(Width - padX - _keyPathLabel.Width, DpiManager.ScaleInt(6));
                _dataLabel.Location = new Point(Width - padX - _dataLabel.Width, DpiManager.ScaleInt(28));
            }
            else
            {
                _badge.Location = new Point(Width - padX - _badge.Width, DpiManager.ScaleInt(6));
                _keyPathLabel.Location = new Point(padX, DpiManager.ScaleInt(6));
                _dataLabel.Location = new Point(padX, DpiManager.ScaleInt(28));
            }
        }
    }

    /// <summary>
    /// Collapsible accordion exposing authoritative technical recovery details:
    /// transaction ID, baseline timestamp, tracked settings count, and registry scopes.
    /// Starts collapsed by default to keep the primary recovery workspace focused and uncluttered.
    /// </summary>
    public class RecoveryTechnicalAccordion : Control
    {
        private bool _isExpanded = false;
        private readonly AppButton _toggleBtn;
        private readonly Label _titleLabel;
        private readonly Label _subtitleLabel;
        private readonly Panel _contentContainer;
        private readonly IdentifierField _fieldTxId;
        private readonly IdentifierField _fieldTimestamp;
        private readonly List<RecoveryScopeItemRow> _scopeRows = new List<RecoveryScopeItemRow>();

        public event EventHandler ExpandedChanged;

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    UpdateToggleText();
                    UpdateAccordionGeometry();
                    if (ExpandedChanged != null) ExpandedChanged(this, EventArgs.Empty);
                }
            }
        }

        public AppButton ToggleButton { get { return _toggleBtn; } }

        public RecoveryTechnicalAccordion()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            TabStop = false;
            bool isRtl = Loc.IsRtl;
            IThemePalette pal = ThemeManager.ActivePalette;

            _titleLabel = new Label();
            _titleLabel.Text = Loc.Get("RecoveryTechDetailsTitle");
            _titleLabel.Font = DpiManager.GetFont(TypographyRole.Subtitle, isRtl);
            _titleLabel.ForeColor = pal.TextPrimary;
            _titleLabel.BackColor = pal.Surface;
            _titleLabel.AutoSize = true;

            _subtitleLabel = new Label();
            _subtitleLabel.Text = Loc.Get("RecoveryScopesHeader");
            _subtitleLabel.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            _subtitleLabel.ForeColor = pal.TextSecondary;
            _subtitleLabel.BackColor = pal.Surface;
            _subtitleLabel.AutoSize = true;
            _subtitleLabel.UseMnemonic = false;

            _toggleBtn = new AppButton();
            _toggleBtn.Variant = ButtonVariant.Ghost;
            _toggleBtn.SizeVariant = ButtonSize.Small;
            _toggleBtn.Click += delegate { IsExpanded = !IsExpanded; };

            _contentContainer = new Panel();
            _contentContainer.BackColor = pal.Surface;
            _contentContainer.Visible = false;

            _fieldTxId = new IdentifierField(Loc.Get("RecoveryCopyPath"));
            _fieldTimestamp = new IdentifierField(Loc.Get("RecoveryCopyPath"));

            Controls.Add(_titleLabel);
            Controls.Add(_subtitleLabel);
            Controls.Add(_toggleBtn);
            Controls.Add(_contentContainer);

            UpdateToggleText();
            UpdateAccordionGeometry();
            Resize += delegate { UpdateAccordionGeometry(); };
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            if (_titleLabel != null)
            {
                _titleLabel.ForeColor = pal.TextPrimary;
                _titleLabel.BackColor = pal.Surface;
            }
            if (_subtitleLabel != null)
            {
                _subtitleLabel.ForeColor = pal.TextSecondary;
                _subtitleLabel.BackColor = pal.Surface;
            }
            if (_contentContainer != null)
            {
                _contentContainer.BackColor = pal.Surface;
                foreach (Control c in _contentContainer.Controls)
                {
                    Label lbl = c as Label;
                    if (lbl != null)
                    {
                        lbl.BackColor = pal.Surface;
                    }
                    else
                    {
                        RecoveryScopeItemRow row = c as RecoveryScopeItemRow;
                        if (row != null)
                        {
                            row.UpdateTheme();
                        }
                    }
                }
            }
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

        public void BindJournalData(StateJournal journal)
        {
            UiDrawingUtils.ClearAndDisposeControls(_contentContainer.Controls);
            _scopeRows.Clear();

            string txId = (journal != null && !string.IsNullOrEmpty(journal.TransactionId)) ? journal.TransactionId : "—";
            string ts = (journal != null && journal.BaselineCapturedUtc != DateTime.MinValue)
                ? journal.BaselineCapturedUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
                : "—";

            _fieldTxId.Value = txId;
            _fieldTimestamp.Value = ts;

            bool isRtl = Loc.IsRtl;
            IThemePalette pal = ThemeManager.ActivePalette;

            Label lblTx = new Label();
            lblTx.Text = Loc.Get("RecoveryTxIdLabel");
            lblTx.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            lblTx.ForeColor = pal.TextMuted;
            lblTx.BackColor = pal.Surface;
            lblTx.AutoSize = true;

            Label lblTs = new Label();
            lblTs.Text = Loc.Get("RecoveryCapturedLabel");
            lblTs.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            lblTs.ForeColor = pal.TextMuted;
            lblTs.BackColor = pal.Surface;
            lblTs.AutoSize = true;

            Label lblScopes = new Label();
            lblScopes.Text = Loc.Get("RecoveryScopesHeader");
            lblScopes.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
            lblScopes.ForeColor = pal.TextMuted;
            lblScopes.BackColor = pal.Surface;
            lblScopes.AutoSize = true;
            lblScopes.UseMnemonic = false;

            _contentContainer.Controls.Add(lblTx);
            _contentContainer.Controls.Add(_fieldTxId);
            _contentContainer.Controls.Add(lblTs);
            _contentContainer.Controls.Add(_fieldTimestamp);
            _contentContainer.Controls.Add(lblScopes);

            if (journal != null && journal.BaselineEntries != null && journal.BaselineEntries.Count > 0)
            {
                foreach (var entry in journal.BaselineEntries)
                {
                    RecoveryScopeItemRow row = new RecoveryScopeItemRow(entry);
                    _scopeRows.Add(row);
                    _contentContainer.Controls.Add(row);
                }
            }
            else
            {
                Label emptyLbl = new Label();
                emptyLbl.Text = Loc.Get("RecoveryNoEntries");
                emptyLbl.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);
                emptyLbl.ForeColor = pal.TextSecondary;
                emptyLbl.BackColor = pal.Surface;
                emptyLbl.AutoSize = true;
                _contentContainer.Controls.Add(emptyLbl);
            }

            UpdateAccordionGeometry();
        }

        private void UpdateToggleText()
        {
            _toggleBtn.Text = _isExpanded ? Loc.Get("RecoveryTechHide") : Loc.Get("RecoveryTechShow");
            _toggleBtn.Width = AppButton.CalculateWidth(_toggleBtn.Text, _toggleBtn.SizeVariant, Loc.IsRtl);
            _toggleBtn.Height = DpiManager.ScaleInt(28);
        }

        public void UpdateAccordionGeometry()
        {
            bool isRtl = (RightToLeft == RightToLeft.Yes) || Loc.IsRtl;
            int pad = DpiManager.ScaleInt(16);
            int innerW = Width - (pad * 2);

            _titleLabel.Font = DpiManager.GetFont(TypographyRole.Subtitle, isRtl);
            _subtitleLabel.Font = DpiManager.GetFont(TypographyRole.Caption, isRtl);

            int titleY = DpiManager.ScaleInt(10);
            int subY = _titleLabel.Bottom + DpiManager.ScaleInt(3);
            if (subY < DpiManager.ScaleInt(34)) subY = DpiManager.ScaleInt(34);

            int btnY = DpiManager.ScaleInt(14);
            int btnX = isRtl ? pad : (Width - pad - _toggleBtn.Width);
            _toggleBtn.Location = new Point(btnX, btnY);

            if (isRtl)
            {
                _titleLabel.Location = new Point(Width - pad - _titleLabel.Width, titleY);
                _subtitleLabel.Location = new Point(Width - pad - _subtitleLabel.Width, subY);
            }
            else
            {
                _titleLabel.Location = new Point(pad, titleY);
                _subtitleLabel.Location = new Point(pad, subY);
            }

            int headerH = _subtitleLabel.Bottom + DpiManager.ScaleInt(12);
            if (headerH < DpiManager.ScaleInt(62)) headerH = DpiManager.ScaleInt(62);

            _contentContainer.Visible = _isExpanded;
            if (_isExpanded)
            {
                _contentContainer.Location = new Point(pad, headerH);
                _contentContainer.Width = innerW;

                int curY = DpiManager.ScaleInt(8);
                int labelH = DpiManager.ScaleInt(18);
                int fieldH = DpiManager.ScaleInt(30);

                // Transaction ID
                if (_contentContainer.Controls.Count >= 5)
                {
                    Control lblTx = _contentContainer.Controls[0];
                    Control fldTx = _contentContainer.Controls[1];
                    Control lblTs = _contentContainer.Controls[2];
                    Control fldTs = _contentContainer.Controls[3];
                    Control lblScopes = _contentContainer.Controls[4];

                    lblTx.Location = isRtl ? new Point(innerW - lblTx.Width, curY) : new Point(0, curY);
                    curY += labelH;
                    fldTx.Location = new Point(0, curY);
                    fldTx.Size = new Size(innerW, fieldH);
                    curY += fieldH + DpiManager.ScaleInt(10);

                    lblTs.Location = isRtl ? new Point(innerW - lblTs.Width, curY) : new Point(0, curY);
                    curY += labelH;
                    fldTs.Location = new Point(0, curY);
                    fldTs.Size = new Size(innerW, fieldH);
                    curY += fieldH + DpiManager.ScaleInt(14);

                    lblScopes.Location = isRtl ? new Point(innerW - lblScopes.Width, curY) : new Point(0, curY);
                    curY += labelH + DpiManager.ScaleInt(6);
                }

                // Scope rows
                foreach (var row in _scopeRows)
                {
                    row.Location = new Point(0, curY);
                    row.Width = innerW;
                    row.PerformItemLayout();
                    curY += row.Height + DpiManager.ScaleInt(6);
                }

                if (_scopeRows.Count == 0 && _contentContainer.Controls.Count > 5)
                {
                    Control emptyLbl = _contentContainer.Controls[5];
                    emptyLbl.Location = isRtl ? new Point(innerW - emptyLbl.Width, curY) : new Point(0, curY);
                    curY += emptyLbl.Height + DpiManager.ScaleInt(12);
                }

                _contentContainer.Height = curY;
                Height = headerH + curY + DpiManager.ScaleInt(12);
            }
            else
            {
                Height = headerH;
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            IThemePalette pal = ThemeManager.ActivePalette;

            int rad = DpiManager.ScaleInt(RadiusTokens.RadiusMedium);
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            using (SolidBrush bg = new SolidBrush(pal.Surface))
            {
                UiDrawingUtils.FillRoundedRectangle(g, bg, rect, rad);
            }
            using (Pen borderPen = new Pen(pal.BorderSubtle, 1f))
            {
                UiDrawingUtils.DrawRoundedRectangle(g, borderPen, rect, rad);
            }
        }
    }
}
