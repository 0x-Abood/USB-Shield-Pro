// ═══════════════════════════════════════════════════════════════════════════
// UiDrawingUtils — Shared Vector & GDI+ Drawing Helpers for v2.1 Primitives
// USB Shield Pro v2.1
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace UsbShieldPro.UI
{
    public static class UiDrawingUtils
    {
        /// <summary>
        /// Creates a rounded rectangle GraphicsPath with boundary validation.
        /// Caller is responsible for disposing the returned GraphicsPath.
        /// </summary>
        public static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

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

        public static void FillRoundedRectangle(Graphics g, Brush brush, Rectangle rect, int radius)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return;
            using (GraphicsPath path = CreateRoundedRectangle(rect, radius))
            {
                g.FillPath(brush, path);
            }
        }

        public static void DrawRoundedRectangle(Graphics g, Pen pen, Rectangle rect, int radius)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return;
            using (GraphicsPath path = CreateRoundedRectangle(rect, radius))
            {
                g.DrawPath(pen, path);
            }
        }

        /// <summary>
        /// Checks if the given text contains any Arabic Unicode characters or is explicitly rendered RTL.
        /// </summary>
        public static bool IsArabicText(string text, bool isRtl = false)
        {
            if (isRtl) return true;
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] >= 0x0600 && text[i] <= 0x06FF) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the calibrated optical vertical baseline offset for status badge and pill text.
        /// Due to Arabic typography font metrics in Windows (Segoe UI), Arabic text sits
        /// lower in its em-box than Latin characters. This method provides the calibrated
        /// optical offset across DPI scales to ensure uniform vertical centering and
        /// alignment with indicator dots (-1px at 100%, -2px at 125%/150%, -3px at 200%).
        /// </summary>
        public static int GetBadgeTextVerticalOffset(bool isArabic = true)
        {
            if (!isArabic) return 0;
            return -(int)Math.Max(1, Math.Round(1.3f * DpiManager.CurrentDpiScale));
        }

        /// <summary>
        /// Draws a crisp 2px focus ring conforming to the control's corner radius.
        /// </summary>
        public static void DrawFocusRing(Graphics g, Rectangle bounds, int radius, Color ringColor)
        {
            Rectangle ringRect = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 3, bounds.Height - 3);
            if (ringRect.Width <= 0 || ringRect.Height <= 0) return;

            using (Pen pen = new Pen(ringColor, 2f))
            {
                pen.Alignment = PenAlignment.Inset;
                int ringRadius = Math.Max(0, radius - 1);
                DrawRoundedRectangle(g, pen, ringRect, ringRadius);
            }
        }

        /// <summary>
        /// Draws a vector dropdown chevron/arrow centered in the target bounds.
        /// </summary>
        public static void DrawChevronDown(Graphics g, Color color, Rectangle bounds, int size = 6)
        {
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            int half = size / 2;

            using (Pen pen = new Pen(color, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx - half, cy - 2, cx, cy + 2);
                g.DrawLine(pen, cx, cy + 2, cx + half, cy - 2);
            }
        }

        /// <summary>
        /// Draws a vector close 'X' glyph centered in the target bounds.
        /// </summary>
        public static void DrawCloseGlyph(Graphics g, Color color, Rectangle bounds, int size = 8)
        {
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            int half = size / 2;

            using (Pen pen = new Pen(color, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx - half, cy - half, cx + half, cy + half);
                g.DrawLine(pen, cx - half, cy + half, cx + half, cy - half);
            }
        }

        /// <summary>
        /// Draws a vector checkmark glyph centered in target bounds.
        /// </summary>
        public static void DrawCheckmark(Graphics g, Color color, Rectangle bounds, float strokeWidth = 1.8f)
        {
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            using (Pen pen = new Pen(color, strokeWidth))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                int chkX = cx - 4;
                int chkY = cy;
                g.DrawLine(pen, chkX, chkY, chkX + 3, chkY + 3);
                g.DrawLine(pen, chkX + 3, chkY + 3, chkX + 8, chkY - 4);
            }
        }

        /// <summary>
        /// Draws a vector overflow icon (3 vertical dots) centered in bounds.
        /// </summary>
        public static void DrawOverflowGlyph(Graphics g, Color color, Rectangle bounds, int dotSize = 3)
        {
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            int spacing = dotSize + 3;

            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, cx - dotSize / 2, cy - spacing - dotSize / 2, dotSize, dotSize);
                g.FillEllipse(brush, cx - dotSize / 2, cy - dotSize / 2, dotSize, dotSize);
                g.FillEllipse(brush, cx - dotSize / 2, cy + spacing - dotSize / 2, dotSize, dotSize);
            }
        }

        /// <summary>
        /// Draws a vector refresh glyph (circular arrow) centered in bounds.
        /// </summary>
        public static void DrawRefreshGlyph(Graphics g, Color color, Rectangle bounds, int radius = 5)
        {
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;

            using (Pen pen = new Pen(color, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawArc(pen, cx - radius, cy - radius, radius * 2, radius * 2, 45, 270);
                // Arrow head
                int ax = cx + (int)(radius * 0.7f);
                int ay = cy + (int)(radius * 0.7f);
                g.DrawLine(pen, ax, ay - 3, ax, ay);
                g.DrawLine(pen, ax - 3, ay, ax, ay);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Vector Navigation Glyphs for AppSidebar (v2.1 Phase 2.6)
        // ═══════════════════════════════════════════════════════════════════════

        public static void DrawNavDashboard(Graphics g, Color color, Rectangle bounds)
        {
            int size = Math.Min(bounds.Width, bounds.Height);
            int pad = Math.Max(2, size / 8);
            int drawSize = size - pad * 2;
            int gap = Math.Max(2, drawSize / 5);
            int tileW = (drawSize - gap) / 2;
            int tileH = tileW;
            int startX = bounds.X + (bounds.Width - (tileW * 2 + gap)) / 2;
            int startY = bounds.Y + (bounds.Height - (tileH * 2 + gap)) / 2;
            int rad = Math.Max(1, tileW / 4);

            using (SolidBrush b = new SolidBrush(color))
            {
                FillRoundedRectangle(g, b, new Rectangle(startX, startY, tileW, tileH), rad);
                FillRoundedRectangle(g, b, new Rectangle(startX + tileW + gap, startY, tileW, tileH), rad);
                FillRoundedRectangle(g, b, new Rectangle(startX, startY + tileH + gap, tileW, tileH), rad);
                FillRoundedRectangle(g, b, new Rectangle(startX + tileW + gap, startY + tileH + gap, tileW, tileH), rad);
            }
        }

        public static void DrawNavUsb(Graphics g, Color color, Rectangle bounds)
        {
            int size = Math.Min(bounds.Width, bounds.Height);
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;

            int bodyW = Math.Max(8, (int)(size * 0.58f));
            int bodyH = Math.Max(8, (int)(size * 0.46f));
            int plugW = Math.Max(6, (int)(size * 0.38f));
            int plugH = Math.Max(4, (int)(size * 0.28f));

            int topY = cy - (bodyH + plugH) / 2;
            Rectangle plugRect = new Rectangle(cx - plugW / 2, topY, plugW, plugH);
            Rectangle bodyRect = new Rectangle(cx - bodyW / 2, topY + plugH, bodyW, bodyH);

            using (Pen pen = new Pen(color, 1.4f))
            {
                pen.LineJoin = LineJoin.Round;
                g.DrawRectangle(pen, plugRect);

                int pinY = plugRect.Y + plugRect.Height / 3;
                int pinH = Math.Max(1, plugRect.Height / 3);
                int pinW = Math.Max(1, plugW / 5);
                using (SolidBrush pinBrush = new SolidBrush(color))
                {
                    g.FillRectangle(pinBrush, plugRect.X + plugW / 4, pinY, pinW, pinH);
                    g.FillRectangle(pinBrush, plugRect.Right - plugW / 4 - pinW, pinY, pinW, pinH);
                }

                DrawRoundedRectangle(g, pen, bodyRect, Math.Max(1, bodyW / 6));

                int cableH = Math.Max(2, (int)(size * 0.12f));
                g.DrawLine(pen, cx, bodyRect.Bottom, cx, bodyRect.Bottom + cableH);
            }
        }

        public static void DrawNavShield(Graphics g, Color color, Rectangle bounds)
        {
            int size = Math.Min(bounds.Width, bounds.Height);
            int cx = bounds.X + bounds.Width / 2;
            int w = Math.Max(10, (int)(size * 0.70f));
            int h = Math.Max(12, (int)(size * 0.82f));
            int left = cx - w / 2;
            int top = bounds.Y + (bounds.Height - h) / 2;
            int right = left + w;
            int bottom = top + h;

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddLine(left, top + 2, cx, top);
                path.AddLine(cx, top, right, top + 2);
                path.AddBezier(right, top + 2, right, top + (int)(h * 0.55f), cx + w / 4, bottom - 2, cx, bottom);
                path.AddBezier(cx, bottom, cx - w / 4, bottom - 2, left, top + (int)(h * 0.55f), left, top + 2);
                path.CloseFigure();

                using (Pen pen = new Pen(color, 1.4f))
                {
                    pen.LineJoin = LineJoin.Round;
                    g.DrawPath(pen, path);
                    g.DrawLine(pen, cx, top + 3, cx, bottom - 3);
                }
            }
        }

        public static void DrawNavTrusted(Graphics g, Color color, Rectangle bounds)
        {
            int size = Math.Min(bounds.Width, bounds.Height);
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            int w = Math.Max(10, (int)(size * 0.72f));
            int h = Math.Max(12, (int)(size * 0.78f));
            int left = cx - w / 2;
            int top = bounds.Y + (bounds.Height - h) / 2;

            using (Pen pen = new Pen(color, 1.4f))
            {
                pen.LineJoin = LineJoin.Round;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddLine(left, top + 2, cx, top);
                    path.AddLine(cx, top, left + w, top + 2);
                    path.AddBezier(left + w, top + 2, left + w, top + (int)(h * 0.55f), cx + w / 4, top + h - 1, cx, top + h);
                    path.AddBezier(cx, top + h, cx - w / 4, top + h - 1, left, top + (int)(h * 0.55f), left, top + 2);
                    path.CloseFigure();
                    g.DrawPath(pen, path);
                }

                using (Pen checkPen = new Pen(color, 1.8f))
                {
                    checkPen.StartCap = LineCap.Round;
                    checkPen.EndCap = LineCap.Round;
                    int chkX = cx - 3;
                    int chkY = cy;
                    g.DrawLine(checkPen, chkX, chkY, chkX + 2, chkY + 3);
                    g.DrawLine(checkPen, chkX + 2, chkY + 3, chkX + 6, chkY - 3);
                }
            }
        }

        public static void DrawNavActivity(Graphics g, Color color, Rectangle bounds)
        {
            int size = Math.Min(bounds.Width, bounds.Height);
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            int w = Math.Max(12, (int)(size * 0.85f));
            int left = cx - w / 2;
            int right = left + w;

            using (Pen pen = new Pen(color, 1.5f))
            {
                pen.LineJoin = LineJoin.Round;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                Point[] pts = new Point[]
                {
                    new Point(left, cy),
                    new Point(cx - 5, cy),
                    new Point(cx - 2, cy - 5),
                    new Point(cx + 1, cy + 5),
                    new Point(cx + 4, cy - 2),
                    new Point(cx + 6, cy),
                    new Point(right, cy)
                };
                g.DrawLines(pen, pts);
            }
        }

        public static void DrawNavRecovery(Graphics g, Color color, Rectangle bounds)
        {
            int size = Math.Min(bounds.Width, bounds.Height);
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            int r = Math.Max(4, (int)(size * 0.34f));

            using (Pen pen = new Pen(color, 1.5f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                g.DrawArc(pen, cx - r, cy - r, r * 2, r * 2, 40, 280);

                double rad = 40.0 * Math.PI / 180.0;
                int tipX = cx + (int)(r * Math.Cos(rad));
                int tipY = cy + (int)(r * Math.Sin(rad));
                g.DrawLine(pen, tipX, tipY, tipX + 4, tipY);
                g.DrawLine(pen, tipX, tipY, tipX, tipY - 4);
            }
        }

        public static void DrawNavSettings(Graphics g, Color color, Rectangle bounds)
        {
            int size = Math.Min(bounds.Width, bounds.Height);
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            int outerR = Math.Max(5, (int)(size * 0.38f));
            int innerR = Math.Max(3, (int)(size * 0.26f));
            int holeR = Math.Max(1, (int)(size * 0.12f));

            using (Pen pen = new Pen(color, 1.4f))
            {
                pen.LineJoin = LineJoin.Round;
                g.DrawEllipse(pen, cx - innerR, cy - innerR, innerR * 2, innerR * 2);
                g.DrawEllipse(pen, cx - holeR, cy - holeR, holeR * 2, holeR * 2);

                for (int i = 0; i < 6; i++)
                {
                    double angle = i * (Math.PI / 3.0);
                    int x1 = cx + (int)(innerR * Math.Cos(angle));
                    int y1 = cy + (int)(innerR * Math.Sin(angle));
                    int x2 = cx + (int)(outerR * Math.Cos(angle));
                    int y2 = cy + (int)(outerR * Math.Sin(angle));
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
            }
        }

        public static void DrawNavCollapse(Graphics g, Color color, Rectangle bounds, bool isCompact, bool isRtl)
        {
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            int arm = Math.Max(3, bounds.Height / 5);
            int sep = Math.Max(2, arm - 1);

            bool pointLeft = isRtl ? isCompact : !isCompact;

            using (Pen pen = new Pen(color, 1.5f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                if (pointLeft)
                {
                    // Pointing Left: <<
                    g.DrawLine(pen, cx - sep / 2, cy - arm, cx - sep / 2 - arm, cy);
                    g.DrawLine(pen, cx - sep / 2 - arm, cy, cx - sep / 2, cy + arm);
                    g.DrawLine(pen, cx + sep / 2 + arm / 2, cy - arm, cx + sep / 2 - arm / 2, cy);
                    g.DrawLine(pen, cx + sep / 2 - arm / 2, cy, cx + sep / 2 + arm / 2, cy + arm);
                }
                else
                {
                    // Pointing Right: >>
                    g.DrawLine(pen, cx - sep / 2 - arm / 2, cy - arm, cx - sep / 2 + arm / 2, cy);
                    g.DrawLine(pen, cx - sep / 2 + arm / 2, cy, cx - sep / 2 - arm / 2, cy + arm);
                    g.DrawLine(pen, cx + sep / 2, cy - arm, cx + sep / 2 + arm, cy);
                    g.DrawLine(pen, cx + sep / 2 + arm, cy, cx + sep / 2, cy + arm);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Device Category Vector Icons (v2.1 Phase 4.2 Connected Devices)
        // ═══════════════════════════════════════════════════════════════════════

        public static void DrawDeviceFlashDrive(Graphics g, Color color, Rectangle bounds)
        {
            int size = Math.Min(bounds.Width, bounds.Height);
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;

            int bodyW = Math.Max(10, (int)(size * 0.52f));
            int bodyH = Math.Max(12, (int)(size * 0.54f));
            int plugW = Math.Max(6, (int)(size * 0.34f));
            int plugH = Math.Max(4, (int)(size * 0.22f));

            int totalH = bodyH + plugH;
            int topY = cy - totalH / 2;

            Rectangle plugRect = new Rectangle(cx - plugW / 2, topY, plugW, plugH);
            Rectangle bodyRect = new Rectangle(cx - bodyW / 2, topY + plugH, bodyW, bodyH);

            using (Pen pen = new Pen(color, 1.4f))
            {
                pen.LineJoin = LineJoin.Round;
                g.DrawRectangle(pen, plugRect);

                int pinY = plugRect.Y + plugRect.Height / 3;
                int pinH = Math.Max(1, plugRect.Height / 3);
                int pinW = Math.Max(1, plugW / 5);
                using (SolidBrush pinBrush = new SolidBrush(color))
                {
                    g.FillRectangle(pinBrush, plugRect.X + plugW / 4, pinY, pinW, pinH);
                    g.FillRectangle(pinBrush, plugRect.Right - plugW / 4 - pinW, pinY, pinW, pinH);
                }

                DrawRoundedRectangle(g, pen, bodyRect, Math.Max(2, bodyW / 6));

                // Lanyard hole / notch at bottom
                int loopW = Math.Max(3, bodyW / 3);
                int loopH = Math.Max(2, bodyH / 5);
                Rectangle loopRect = new Rectangle(cx - loopW / 2, bodyRect.Bottom - loopH - 2, loopW, loopH);
                DrawRoundedRectangle(g, pen, loopRect, 1);
            }
        }

        public static void DrawDeviceExternalDisk(Graphics g, Color color, Rectangle bounds)
        {
            int size = Math.Min(bounds.Width, bounds.Height);
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;

            int w = Math.Max(14, (int)(size * 0.72f));
            int h = Math.Max(16, (int)(size * 0.82f));
            int left = cx - w / 2;
            int top = cy - h / 2;

            Rectangle bodyRect = new Rectangle(left, top, w, h);

            using (Pen pen = new Pen(color, 1.4f))
            {
                pen.LineJoin = LineJoin.Round;
                DrawRoundedRectangle(g, pen, bodyRect, Math.Max(2, w / 8));

                // Subtle drive enclosure accent line
                int lineY = top + (int)(h * 0.72f);
                g.DrawLine(pen, left + 3, lineY, left + w - 3, lineY);

                // LED dot
                int dotSize = Math.Max(2, w / 7);
                using (SolidBrush dotBrush = new SolidBrush(color))
                {
                    g.FillEllipse(dotBrush, left + w - dotSize - 3, top + 3, dotSize, dotSize);
                }

                // Port notch at bottom
                int portW = Math.Max(4, w / 3);
                g.DrawLine(pen, cx - portW / 2, top + h, cx + portW / 2, top + h);
            }
        }

        public static void DrawDevicePhoneOrMedia(Graphics g, Color color, Rectangle bounds)
        {
            int size = Math.Min(bounds.Width, bounds.Height);
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;

            int w = Math.Max(10, (int)(size * 0.48f));
            int h = Math.Max(16, (int)(size * 0.84f));
            int left = cx - w / 2;
            int top = cy - h / 2;

            Rectangle phoneRect = new Rectangle(left, top, w, h);

            using (Pen pen = new Pen(color, 1.4f))
            {
                pen.LineJoin = LineJoin.Round;
                DrawRoundedRectangle(g, pen, phoneRect, Math.Max(3, w / 5));

                // Speaker slot at top
                int speakerW = Math.Max(3, w / 3);
                int speakerY = top + Math.Max(2, h / 10);
                g.DrawLine(pen, cx - speakerW / 2, speakerY, cx + speakerW / 2, speakerY);

                // Screen outline
                int scrPadX = Math.Max(2, w / 8);
                int scrTop = top + Math.Max(4, (int)(h * 0.16f));
                int scrBottom = top + h - Math.Max(4, (int)(h * 0.18f));
                Rectangle scrRect = new Rectangle(left + scrPadX, scrTop, w - scrPadX * 2, scrBottom - scrTop);
                DrawRoundedRectangle(g, pen, scrRect, 1);

                // Home indicator / button
                int pillW = Math.Max(3, w / 4);
                int pillY = top + h - Math.Max(2, (int)(h * 0.09f));
                g.DrawLine(pen, cx - pillW / 2, pillY, cx + pillW / 2, pillY);
            }
        }

        public static void DrawDeviceGeneric(Graphics g, Color color, Rectangle bounds)
        {
            DrawNavUsb(g, color, bounds);
        }

        public static void DrawDeviceInternal(Graphics g, Color color, Rectangle bounds)
        {
            int size = Math.Min(bounds.Width, bounds.Height);
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;

            int w = Math.Max(14, (int)(size * 0.72f));
            int h = Math.Max(14, (int)(size * 0.76f));
            int left = cx - w / 2;
            int top = cy - h / 2;

            using (Pen pen = new Pen(color, 1.4f))
            {
                pen.LineJoin = LineJoin.Round;
                DrawRoundedRectangle(g, pen, new Rectangle(left, top, w, h), 2);

                // Drive platter circle
                int platterR = Math.Max(3, (int)(w * 0.28f));
                g.DrawEllipse(pen, cx - platterR, cy - platterR - 1, platterR * 2, platterR * 2);

                // Head arm
                g.DrawLine(pen, cx + platterR / 2, cy + platterR / 2, left + w - 3, top + h - 3);

                // Small lock / shield overlay indicator
                int lockW = Math.Max(4, w / 4);
                int lockH = Math.Max(4, h / 4);
                Rectangle lockRect = new Rectangle(left + 2, top + h - lockH - 2, lockW, lockH);
                using (SolidBrush bgB = new SolidBrush(color))
                {
                    g.FillRectangle(bgB, lockRect);
                }
            }
        }

        public static void DrawSearchIcon(Graphics g, Color color, Rectangle bounds)
        {
            int size = Math.Min(bounds.Width, bounds.Height);
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;

            int r = Math.Max(3, (int)(size * 0.26f));
            int lensCx = cx - (int)(r * 0.35f);
            int lensCy = cy - (int)(r * 0.35f);

            using (Pen pen = new Pen(color, 1.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawEllipse(pen, lensCx - r, lensCy - r, r * 2, r * 2);

                // Handle at 45 degrees
                int hStartX = lensCx + (int)(r * 0.707f);
                int hStartY = lensCy + (int)(r * 0.707f);
                int hLen = Math.Max(3, (int)(size * 0.32f));
                g.DrawLine(pen, hStartX, hStartY, hStartX + hLen, hStartY + hLen);
            }
        }

        public static void DrawCopyIcon(Graphics g, Color color, Rectangle bounds, Color? fillBg = null)
        {
            int size = Math.Min(bounds.Width, bounds.Height);
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;

            int docW = Math.Max(6, (int)(size * 0.44f));
            int docH = Math.Max(8, (int)(size * 0.54f));

            int backLeft = cx - docW / 2 + 2;
            int backTop = cy - docH / 2 - 2;
            int frontLeft = cx - docW / 2 - 2;
            int frontTop = cy - docH / 2 + 2;

            Color bg = fillBg.HasValue ? fillBg.Value : (ThemeManager.ActivePalette != null ? ThemeManager.ActivePalette.SurfaceElevated : Color.FromArgb(30, 34, 40));

            using (Pen pen = new Pen(color, 1.4f))
            {
                pen.LineJoin = LineJoin.Round;
                // Back doc
                g.DrawRectangle(pen, backLeft, backTop, docW, docH);
                // Fill front doc so back doc lines don't bleed through
                using (SolidBrush fb = new SolidBrush(bg))
                {
                    g.FillRectangle(fb, frontLeft, frontTop, docW, docH);
                }
                // Front doc
                g.DrawRectangle(pen, frontLeft, frontTop, docW, docH);
            }
        }

        /// <summary>
        /// Resolves the actual background surface color behind a control by traversing up its parent hierarchy.
        /// Accurately detects row surfaces, card surfaces, and control back colors, eliminating rectangular corner artifacts.
        /// </summary>
        public static Color GetEffectiveParentSurface(System.Windows.Forms.Control control)
        {
            IThemePalette pal = ThemeManager.ActivePalette;
            Color defaultSurface = (pal != null) ? pal.BackgroundPrimary : Color.FromArgb(13, 15, 17);

            if (control == null)
            {
                return defaultSurface;
            }

            System.Windows.Forms.Control p = control.Parent;
            while (p != null)
            {
                if (p is ProtectionSettingRow)
                {
                    return ((ProtectionSettingRow)p).CurrentSurfaceColor;
                }
                if (p is DeviceRow)
                {
                    return ((DeviceRow)p).CurrentSurfaceColor;
                }
                if (p is TrustedDeviceRow)
                {
                    return ((TrustedDeviceRow)p).CurrentSurfaceColor;
                }
                if (p is ActivityRow)
                {
                    return ((ActivityRow)p).CurrentSurfaceColor;
                }

                // Recognize standard card containers whose primary surface is pal.Surface
                string typeName = p.GetType().Name;
                if (typeName == "ProtectionStatusCard" ||
                    typeName == "SettingsCard" ||
                    typeName == "SettingsSecurityCard" ||
                    typeName == "SettingsServiceCard" ||
                    typeName == "SettingsAboutCard" ||
                    typeName == "SummaryMetricCard" ||
                    typeName == "ProtectionSectionCard" ||
                    typeName == "ProtectionOverviewPanel" ||
                    typeName == "RecentActivityPanel" ||
                    typeName == "SafetyMessageBar" ||
                    typeName == "RecoveryPostureCard" ||
                    typeName == "RecoveryArtifactsCard" ||
                    typeName == "RecoveryTechnicalAccordion" ||
                    typeName == "DeviceDetailsDrawer" ||
                    typeName == "TrustedDeviceDetailsDrawer" ||
                    typeName == "CardPanel")
                {
                    return (pal != null) ? pal.Surface : defaultSurface;
                }

                if (typeName == "AppTopBar")
                {
                    return (pal != null) ? pal.BackgroundSecondary : defaultSurface;
                }

                if (p.BackColor != Color.Empty && p.BackColor != Color.Transparent && p.BackColor.A == 255)
                {
                    // Filter out uninitialized WinForms default SystemColors.Control (240, 240, 240) in dark themes
                    if (pal != null && pal.IsDark && (p.BackColor == SystemColors.Control || p.BackColor == System.Windows.Forms.Control.DefaultBackColor))
                    {
                        // Skip uninitialized default gray in dark themes
                    }
                    else
                    {
                        return p.BackColor;
                    }
                }
                p = p.Parent;
            }

            return defaultSurface;
        }

        public static void DrawTextSafe(Graphics g, string text, Font font, Rectangle bounds, Color color, TextFormatFlags flags)
        {
            if (string.IsNullOrEmpty(text)) return;
            TextRenderer.DrawText(g, text, font, bounds, color, flags | TextFormatFlags.NoPrefix);
        }

        /// <summary>
        /// Clears a control collection and disposes all controls that are not included in keepAlive.
        /// Prevents GDI/User32 handle and managed memory leaks when repopulating containers.
        /// </summary>
        public static void ClearAndDisposeControls(Control.ControlCollection controls, params Control[] keepAlive)
        {
            if (controls == null) return;
            var keepSet = new System.Collections.Generic.HashSet<Control>();
            if (keepAlive != null)
            {
                for (int i = 0; i < keepAlive.Length; i++)
                {
                    if (keepAlive[i] != null) keepSet.Add(keepAlive[i]);
                }
            }
            var toDispose = new System.Collections.Generic.List<Control>();
            for (int i = controls.Count - 1; i >= 0; i--)
            {
                Control c = controls[i];
                if (c != null && !keepSet.Contains(c))
                {
                    toDispose.Add(c);
                }
            }
            controls.Clear();
            for (int i = 0; i < toDispose.Count; i++)
            {
                try { toDispose[i].Dispose(); } catch { }
            }
        }
    }

    public class AppMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground
        {
            get { return ThemeManager.IsDarkTheme ? ColorTranslator.FromHtml("#18181C") : ColorTranslator.FromHtml("#FFFFFF"); }
        }

        public override Color ImageMarginGradientBegin { get { return ToolStripDropDownBackground; } }
        public override Color ImageMarginGradientMiddle { get { return ToolStripDropDownBackground; } }
        public override Color ImageMarginGradientEnd { get { return ToolStripDropDownBackground; } }

        public override Color MenuBorder
        {
            get { return ThemeManager.IsDarkTheme ? ColorTranslator.FromHtml("#3F3F46") : ColorTranslator.FromHtml("#E5E7EB"); }
        }

        public override Color MenuItemBorder { get { return Color.Transparent; } }

        public override Color MenuItemSelected
        {
            get { return ThemeManager.IsDarkTheme ? ColorTranslator.FromHtml("#2A2A32") : ColorTranslator.FromHtml("#F3F4F6"); }
        }

        public override Color MenuItemSelectedGradientBegin { get { return MenuItemSelected; } }
        public override Color MenuItemSelectedGradientEnd { get { return MenuItemSelected; } }

        public override Color SeparatorDark
        {
            get { return ThemeManager.IsDarkTheme ? ColorTranslator.FromHtml("#2E2E38") : ColorTranslator.FromHtml("#E5E7EB"); }
        }

        public override Color SeparatorLight { get { return Color.Transparent; } }
    }

    public class AppMenuRenderer : ToolStripProfessionalRenderer
    {
        public AppMenuRenderer() : base(new AppMenuColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            Color borderColor = ThemeManager.IsDarkTheme ? ColorTranslator.FromHtml("#3F3F46") : ColorTranslator.FromHtml("#E5E7EB");
            using (Pen p = new Pen(borderColor, 1f))
            {
                e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            bool isDark = ThemeManager.IsDarkTheme;
            Color bg = isDark ? ColorTranslator.FromHtml("#18181C") : ColorTranslator.FromHtml("#FFFFFF");
            Color hoverBg = isDark ? ColorTranslator.FromHtml("#2A2A32") : ColorTranslator.FromHtml("#F3F4F6");
            Color hoverBorder = isDark ? ColorTranslator.FromHtml("#3F3F46") : ColorTranslator.FromHtml("#E5E7EB");

            Rectangle rect = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);

            if (e.Item.Selected && e.Item.Enabled)
            {
                using (SolidBrush b = new SolidBrush(hoverBg))
                    e.Graphics.FillRectangle(b, rect);
                using (Pen p = new Pen(hoverBorder, 1f))
                    e.Graphics.DrawRectangle(p, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            }
            else
            {
                using (SolidBrush b = new SolidBrush(bg))
                    e.Graphics.FillRectangle(b, e.Item.ContentRectangle);
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            bool isDark = ThemeManager.IsDarkTheme;
            Color bg = isDark ? ColorTranslator.FromHtml("#18181C") : ColorTranslator.FromHtml("#FFFFFF");
            Color lineColor = isDark ? ColorTranslator.FromHtml("#2E2E38") : ColorTranslator.FromHtml("#E5E7EB");

            // Fill entire separator rectangle with menu background to eliminate dark blocks
            using (SolidBrush b = new SolidBrush(bg))
                e.Graphics.FillRectangle(b, new Rectangle(0, 0, e.Item.Width, e.Item.Height));

            // Draw clean 1px centered line
            int y = e.Item.Height / 2;
            int margin = 8;
            using (Pen p = new Pen(lineColor, 1f))
                e.Graphics.DrawLine(p, margin, y, e.Item.Width - margin, y);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            bool isDark = ThemeManager.IsDarkTheme;
            if (!e.Item.Enabled)
            {
                e.TextColor = isDark ? ColorTranslator.FromHtml("#71717A") : ColorTranslator.FromHtml("#9CA3AF");
            }
            else
            {
                if (e.Item.Tag is string && (string)e.Item.Tag == "LockAction")
                {
                    e.TextColor = isDark ? ColorTranslator.FromHtml("#F87171") : ColorTranslator.FromHtml("#DC2626");
                }
                else if (e.Item.Tag is string && (string)e.Item.Tag == "UnlockAction")
                {
                    e.TextColor = isDark ? ColorTranslator.FromHtml("#4ADE80") : ColorTranslator.FromHtml("#16A34A");
                }
                else
                {
                    e.TextColor = isDark ? ColorTranslator.FromHtml("#F4F4F6") : ColorTranslator.FromHtml("#111827");
                }
            }
            e.Item.ForeColor = e.TextColor;

            base.OnRenderItemText(e);
        }
    }
}

