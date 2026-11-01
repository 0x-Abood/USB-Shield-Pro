// ═══════════════════════════════════════════════════════════════════════════
// DpiManager — Centralized Per-Monitor DPI Scaling & GDI Font Cache for v2.1
// USB Shield Pro v2.1
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace UsbShieldPro.UI
{
    public static class DpiManager
    {
        private static readonly object _syncLock = new object();
        private static float _currentDpiScale = 1.0f;
        private static bool _hasSegoeUiVariable = false;
        private static bool _fontFamilyChecked = false;

        // Font Cache: key = "Role_Arabic_Scale" => Font
        private static readonly Dictionary<string, Font> _fontCache = new Dictionary<string, Font>();

        static DpiManager()
        {
            CheckSystemFontFamilies();
        }

        public static float CurrentDpiScale
        {
            get
            {
                lock (_syncLock)
                {
                    return _currentDpiScale;
                }
            }
            set
            {
                lock (_syncLock)
                {
                    if (Math.Abs(_currentDpiScale - value) > 0.01f)
                    {
                        _currentDpiScale = Math.Max(1.0f, value);
                        ClearFontCache();
                    }
                }
            }
        }

        public static int ScaleInt(int baseValue, float scale = -1f)
        {
            float s = (scale > 0f) ? scale : CurrentDpiScale;
            return (int)Math.Round(baseValue * s);
        }

        public static float ScaleFloat(float baseValue, float scale = -1f)
        {
            float s = (scale > 0f) ? scale : CurrentDpiScale;
            return baseValue * s;
        }

        public static Padding ScalePadding(Padding basePadding, float scale = -1f)
        {
            float s = (scale > 0f) ? scale : CurrentDpiScale;
            return new Padding(
                (int)Math.Round(basePadding.Left * s),
                (int)Math.Round(basePadding.Top * s),
                (int)Math.Round(basePadding.Right * s),
                (int)Math.Round(basePadding.Bottom * s)
            );
        }

        /// <summary>
        /// Mirrors horizontal padding for RTL layouts without mutating vertical padding.
        /// Left becomes Right, Right becomes Left.
        /// </summary>
        public static Padding MirrorPadding(Padding p, bool isRtl)
        {
            if (!isRtl) return p;
            return new Padding(p.Right, p.Top, p.Left, p.Bottom);
        }

        private static void CheckSystemFontFamilies()
        {
            if (_fontFamilyChecked) return;
            try
            {
                using (InstalledFontCollection fonts = new InstalledFontCollection())
                {
                    foreach (FontFamily ff in fonts.Families)
                    {
                        if (string.Equals(ff.Name, "Segoe UI Variable Text", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ff.Name, "Segoe UI Variable Display", StringComparison.OrdinalIgnoreCase))
                        {
                            _hasSegoeUiVariable = true;
                            break;
                        }
                    }
                }
            }
            catch
            {
                _hasSegoeUiVariable = false;
            }
            _fontFamilyChecked = true;
        }

        /// <summary>
        /// Retrieves a cached, DPI-scaled GDI Font instance for the specified typography role.
        /// Automatically selects Segoe UI Variable when available, falling back to Segoe UI.
        /// For Arabic, applies appropriate font family and size adjustment.
        /// </summary>
        public static Font GetFont(TypographyRole role, bool isArabic = false, float scale = -1f)
        {
            float s = (scale > 0f) ? scale : CurrentDpiScale;
            string key = string.Format("{0}_{1}_{2:F2}", role, isArabic ? "AR" : "EN", s);

            lock (_syncLock)
            {
                Font cached;
                if (_fontCache.TryGetValue(key, out cached))
                {
                    return cached;
                }

                Font created = CreateFont(role, isArabic, s);
                _fontCache[key] = created;
                return created;
            }
        }

        private static Font CreateFont(TypographyRole role, bool isArabic, float scale)
        {
            string family = "Segoe UI";
            float basePt = 9.5f;
            FontStyle style = FontStyle.Regular;

            switch (role)
            {
                case TypographyRole.Display:
                    family = _hasSegoeUiVariable && !isArabic ? "Segoe UI Variable Display" : "Segoe UI";
                    basePt = 18f;
                    style = FontStyle.Bold;
                    break;

                case TypographyRole.Title:
                    family = _hasSegoeUiVariable && !isArabic ? "Segoe UI Variable Display" : "Segoe UI";
                    basePt = 14f;
                    style = FontStyle.Bold;
                    break;

                case TypographyRole.Subtitle:
                    family = _hasSegoeUiVariable && !isArabic ? "Segoe UI Variable Text" : "Segoe UI";
                    basePt = 11.5f;
                    style = FontStyle.Bold;
                    break;

                case TypographyRole.Body:
                    family = _hasSegoeUiVariable && !isArabic ? "Segoe UI Variable Text" : "Segoe UI";
                    basePt = 9.5f;
                    style = FontStyle.Regular;
                    break;

                case TypographyRole.BodyStrong:
                    family = _hasSegoeUiVariable && !isArabic ? "Segoe UI Variable Text" : "Segoe UI";
                    basePt = 9.5f;
                    style = FontStyle.Bold;
                    break;

                case TypographyRole.Caption:
                    family = _hasSegoeUiVariable && !isArabic ? "Segoe UI Variable Small" : "Segoe UI";
                    basePt = 8.5f;
                    style = FontStyle.Regular;
                    break;

                case TypographyRole.Code:
                    family = "Consolas";
                    basePt = 9.0f;
                    style = FontStyle.Regular;
                    break;
            }

            // Arabic diacritic clarity adjustment
            if (isArabic && role == TypographyRole.Caption)
            {
                basePt = 9.0f; // slightly larger for Arabic diacritic legibility
            }

            float finalSize = basePt * scale;

            try
            {
                return new Font(family, finalSize, style, GraphicsUnit.Point);
            }
            catch
            {
                // Fallback to standard Segoe UI
                try
                {
                    return new Font("Segoe UI", finalSize, style, GraphicsUnit.Point);
                }
                catch
                {
                    // Fallback to default sans serif
                    return new Font(FontFamily.GenericSansSerif, finalSize, style, GraphicsUnit.Point);
                }
            }
        }

        /// <summary>
        /// Disposes all cached GDI fonts to eliminate font handle leaks.
        /// </summary>
        public static void ClearFontCache()
        {
            lock (_syncLock)
            {
                foreach (Font f in _fontCache.Values)
                {
                    try { f.Dispose(); } catch { }
                }
                _fontCache.Clear();
            }
        }

        /// <summary>
        /// Calculates the safe line height for text rendering to avoid bottom-clipping Arabic descenders.
        /// </summary>
        public static int GetSafeTextHeight(Font font, bool isArabic = false)
        {
            if (font == null) return ScaleInt(isArabic ? 22 : 18);
            int baseH = TextRenderer.MeasureText("Ag", font).Height;
            if (isArabic)
            {
                // Arabic glyph descenders (ي, ر, ز, و, ق, ل, م, ج, ح, خ, ع, غ) extend lower than Latin baseline.
                return Math.Max(baseH + ScaleInt(4), ScaleInt(22));
            }
            return Math.Max(baseH, ScaleInt(18));
        }

        /// <summary>
        /// Returns safe TextFormatFlags including NoClipping to guarantee Arabic descenders are never eaten.
        /// </summary>
        public static TextFormatFlags GetSafeTextFlags(bool isRtl, bool singleLine = true, bool centerVertically = false)
        {
            TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping;
            if (singleLine) flags |= TextFormatFlags.SingleLine;
            if (centerVertically) flags |= TextFormatFlags.VerticalCenter;
            if (isRtl) flags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
            else flags |= TextFormatFlags.Left;
            return flags;
        }

        /// <summary>
        /// Measures required text bounding height with generous headroom for Arabic ascenders and descenders.
        /// </summary>
        public static int GetRequiredTextHeight(string text, Font font, int proposedWidth, bool isArabic = false)
        {
            if (string.IsNullOrEmpty(text) || font == null) return ScaleInt(isArabic ? 22 : 18);
            Size sz = TextRenderer.MeasureText(text, font, new Size(proposedWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);
            int headroom = isArabic ? ScaleInt(6) : ScaleInt(2);
            return sz.Height + headroom;
        }
    }
}
