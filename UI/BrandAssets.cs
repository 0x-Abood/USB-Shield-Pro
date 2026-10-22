// ═══════════════════════════════════════════════════════════════════════════
// BrandAssets — Production Brand Identity Assets & Icon Management
// © 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace UsbShieldPro
{
    /// <summary>
    /// Central manager for official USB Shield Pro brand identity assets.
    /// Provides multi-resolution window icons, system tray icons, and
    /// high-fidelity scaled brand marks with zero external runtime dependencies.
    /// </summary>
    public static class BrandAssets
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        internal static Icon CreateIconFromBitmap(Bitmap bmp)
        {
            if (bmp == null) return null;
            IntPtr hIcon = bmp.GetHicon();
            try
            {
                using (Icon tempIcon = Icon.FromHandle(hIcon))
                {
                    return (Icon)tempIcon.Clone();
                }
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }

        private static readonly object _syncLock = new object();
        private static Icon _appIcon;
        private static Icon _trayIcon;
        private static readonly Dictionary<int, Bitmap> _cachedBitmaps = new Dictionary<int, Bitmap>();

        /// <summary>
        /// Retrieves the primary application icon (multi-resolution: 16 to 256px).
        /// </summary>
        public static Icon AppIcon
        {
            get
            {
                if (_appIcon != null) return _appIcon;
                lock (_syncLock)
                {
                    if (_appIcon != null) return _appIcon;
                    _appIcon = LoadAppIcon();
                    return _appIcon;
                }
            }
        }

        /// <summary>
        /// Retrieves the 16x16 system tray icon with crisp alpha blending.
        /// </summary>
        public static Icon TrayIcon
        {
            get
            {
                if (_trayIcon != null) return _trayIcon;
                lock (_syncLock)
                {
                    if (_trayIcon != null) return _trayIcon;
                    _trayIcon = LoadTrayIcon();
                    return _trayIcon;
                }
            }
        }

        /// <summary>
        /// Retrieves a high-fidelity brand mark bitmap scaled to target size.
        /// </summary>
        public static Bitmap GetBrandBitmap(int targetSize)
        {
            if (targetSize <= 0) targetSize = 48;

            lock (_syncLock)
            {
                Bitmap cached;
                if (_cachedBitmaps.TryGetValue(targetSize, out cached))
                {
                    return cached;
                }

                Bitmap bmp = LoadBrandBitmapInternal(targetSize);
                _cachedBitmaps[targetSize] = bmp;
                return bmp;
            }
        }

        private static Icon LoadAppIcon()
        {
            try
            {
                // 1. Try file on disk: Assets\Brand\UsbShieldPro.ico
                string baseDir = AppDomain.CurrentDomain.BaseDirectory ?? "";
                string path1 = Path.Combine(baseDir, @"Assets\Brand\UsbShieldPro.ico");
                if (File.Exists(path1))
                {
                    return new Icon(path1);
                }

                string path2 = Path.Combine(baseDir, "shield.ico");
                if (File.Exists(path2))
                {
                    return new Icon(path2);
                }

                string relPath1 = @"Assets\Brand\UsbShieldPro.ico";
                if (File.Exists(relPath1))
                {
                    return new Icon(relPath1);
                }

                // 2. Try embedded resource
                Assembly asm = Assembly.GetExecutingAssembly();
                foreach (string name in asm.GetManifestResourceNames())
                {
                    if (name.EndsWith("UsbShieldPro.ico", StringComparison.OrdinalIgnoreCase))
                    {
                        using (Stream s = asm.GetManifestResourceStream(name))
                        {
                            if (s != null) return new Icon(s);
                        }
                    }
                }

                // 3. Try executable's own Win32 icon
                string exePath = Application.ExecutablePath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    Icon extracted = Icon.ExtractAssociatedIcon(exePath);
                    if (extracted != null) return extracted;
                }
            }
            catch
            {
                // Fall through to fallback
            }

            // Fallback: create from 32x32 bitmap
            Bitmap bmp = GetBrandBitmap(32);
            return CreateIconFromBitmap(bmp);
        }

        private static Icon LoadTrayIcon()
        {
            try
            {
                // Extract clean 16x16 frame from AppIcon
                Icon appIco = AppIcon;
                if (appIco != null)
                {
                    return new Icon(appIco, new Size(16, 16));
                }
            }
            catch
            {
                // Fall through
            }

            // Fallback from 16px bitmap
            Bitmap bmp = GetBrandBitmap(16);
            return CreateIconFromBitmap(bmp);
        }

        private static Bitmap LoadBrandBitmapInternal(int targetSize)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory ?? "";

            // 1. Try exact size derivative on disk
            string exactPath = Path.Combine(baseDir, string.Format(@"Assets\Brand\usbshield-icon-{0}.png", targetSize));
            if (!File.Exists(exactPath))
            {
                exactPath = string.Format(@"Assets\Brand\usbshield-icon-{0}.png", targetSize);
            }

            if (File.Exists(exactPath))
            {
                try
                {
                    using (FileStream fs = new FileStream(exactPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (Image src = Image.FromStream(fs))
                    {
                        return new Bitmap(src);
                    }
                }
                catch { }
            }

            // 2. Try closest master/derivative on disk (64, 128, 256)
            int[] trySizes = new int[] { 64, 128, 256, 48, 32 };
            foreach (int sz in trySizes)
            {
                string path = Path.Combine(baseDir, string.Format(@"Assets\Brand\usbshield-icon-{0}.png", sz));
                if (!File.Exists(path))
                {
                    path = string.Format(@"Assets\Brand\usbshield-icon-{0}.png", sz);
                }
                if (File.Exists(path))
                {
                    try
                    {
                        using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (Image src = Image.FromStream(fs))
                        {
                            return ScaleBitmap(src, targetSize, targetSize);
                        }
                    }
                    catch { }
                }
            }

            // 3. Try embedded resource
            Assembly asm = Assembly.GetExecutingAssembly();
            foreach (string name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith(string.Format("usbshield-icon-{0}.png", targetSize), StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("usbshield-icon-64.png", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("usbshield-icon-128.png", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using (Stream s = asm.GetManifestResourceStream(name))
                        {
                            if (s != null)
                            {
                                using (Image src = Image.FromStream(s))
                                {
                                    return ScaleBitmap(src, targetSize, targetSize);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            // 4. Try extract from AppIcon
            try
            {
                Icon appIco = AppIcon;
                if (appIco != null)
                {
                    using (Icon sizedIco = new Icon(appIco, new Size(targetSize, targetSize)))
                    {
                        return sizedIco.ToBitmap();
                    }
                }
            }
            catch { }

            // 5. Ultimate fallback: return empty transparent bitmap
            return new Bitmap(targetSize, targetSize, PixelFormat.Format32bppArgb);
        }

        private static Bitmap ScaleBitmap(Image source, int width, int height)
        {
            if (source.Width == width && source.Height == height && source is Bitmap)
            {
                return new Bitmap(source);
            }

            Bitmap dest = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(dest))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.Clear(Color.Transparent);
                g.DrawImage(source, 0, 0, width, height);
            }
            return dest;
        }
    }
}
