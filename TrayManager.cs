// ═══════════════════════════════════════════════════════════════════════════
// TrayManager — System tray icon and context menu (C# 5 compatible)
// © 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using UsbShieldPro.UI;

namespace UsbShieldPro
{
    class TrayManager : IDisposable
    {
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _menu;
        private MainForm _owner;
        private ToolStripMenuItem _lockItem;
        private ToolStripMenuItem _unlockItem;
        private ToolStripMenuItem _openItem;
        private ToolStripMenuItem _exitItem;

        public TrayManager(MainForm owner)
        {
            _owner = owner;
            _trayIcon = new NotifyIcon();
            _trayIcon.Icon = CreateTrayIcon();
            _trayIcon.Visible = true;
            _trayIcon.Text = "USB Shield Pro";
            _trayIcon.DoubleClick += delegate { _owner.ShowFromTray(); };
            BuildMenu();
        }

        public void BuildMenu()
        {
            if (_menu != null) _menu.Dispose();
            _menu = new ContextMenuStrip();
            _menu.Renderer = new AppMenuRenderer();
            _menu.RightToLeft = Loc.RtlValue;

            _lockItem = new ToolStripMenuItem(Loc.Get("LockBtn"));
            _lockItem.Tag = "LockAction";
            _lockItem.Click += delegate { _owner.PerformLock(); };

            _unlockItem = new ToolStripMenuItem(Loc.Get("UnlockBtn"));
            _unlockItem.Tag = "UnlockAction";
            _unlockItem.Click += delegate { _owner.PerformUnlock(); };

            _openItem = new ToolStripMenuItem(Loc.Get("Open"));
            _openItem.Click += delegate { _owner.ShowFromTray(); };
            _openItem.Font = DpiManager.GetFont(TypographyRole.BodyStrong, Loc.IsRtl);

            _exitItem = new ToolStripMenuItem(Loc.Get("Exit"));
            _exitItem.Click += delegate { _owner.ExitApp(); };

            _menu.Items.Add(_openItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_lockItem);
            _menu.Items.Add(_unlockItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_exitItem);

            _menu.Opening += delegate
            {
                UpdateMenuColors();
            };

            UpdateMenuColors();
            _trayIcon.ContextMenuStrip = _menu;
        }

        private void UpdateMenuColors()
        {
            if (_menu == null) return;
            bool isDark = ThemeManager.IsDarkTheme;
            _menu.BackColor = isDark ? ColorTranslator.FromHtml("#18181C") : ColorTranslator.FromHtml("#FFFFFF");
            _menu.RightToLeft = Loc.RtlValue;

            if (_openItem != null)
                _openItem.ForeColor = isDark ? ColorTranslator.FromHtml("#F4F4F6") : ColorTranslator.FromHtml("#111827");
            if (_lockItem != null)
                _lockItem.ForeColor = isDark ? ColorTranslator.FromHtml("#F87171") : ColorTranslator.FromHtml("#DC2626");
            if (_unlockItem != null)
                _unlockItem.ForeColor = isDark ? ColorTranslator.FromHtml("#4ADE80") : ColorTranslator.FromHtml("#16A34A");
            if (_exitItem != null)
                _exitItem.ForeColor = isDark ? ColorTranslator.FromHtml("#F4F4F6") : ColorTranslator.FromHtml("#111827");
        }

        public void ShowBalloon(string title, string text, ToolTipIcon icon)
        {
            _trayIcon.ShowBalloonTip(3000, title, text, icon);
        }

        public void ShowBalloon(string title, string text)
        {
            ShowBalloon(title, text, ToolTipIcon.Info);
        }

        public void UpdateTooltip(string text)
        {
            if (text.Length > 127) text = text.Substring(0, 127);
            _trayIcon.Text = text;
        }

        static Icon CreateTrayIcon()
        {
            return BrandAssets.TrayIcon;
        }

        public void Dispose()
        {
            if (_trayIcon != null) { _trayIcon.Visible = false; _trayIcon.Dispose(); }
            if (_menu != null) _menu.Dispose();
        }
    }
}
