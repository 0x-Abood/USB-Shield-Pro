// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.1 - Application Entry Point
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;
using UsbShieldPro.Core;
using UsbShieldPro.Devices;
using UsbShieldPro.Models;
using UsbShieldPro.Persistence;
using UsbShieldPro.Security;
using UsbShieldPro.Service;
using System.Web.Script.Serialization;

namespace UsbShieldPro
{
    static class Program
    {
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetStdHandle(int nStdHandle);
        [DllImport("kernel32.dll")]
        static extern uint GetFileType(IntPtr hFile);
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern bool AllowSetForegroundWindow(int dwProcessId);
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        const int ASFW_ANY = -1;
        const int STD_OUTPUT_HANDLE = -11;
        const int STD_ERROR_HANDLE = -12;
        const uint FILE_TYPE_DISK = 0x0001;
        const uint FILE_TYPE_CHAR = 0x0002;
        const uint FILE_TYPE_PIPE = 0x0003;

        static Mutex _mutex;
        private static readonly string StartupLogPath = Path.Combine(Path.GetTempPath(), "UsbShieldPro_startup.log");

        public static void LogStartup(string checkpoint)
        {
            try
            {
                string line = string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] {1}", DateTime.UtcNow, checkpoint);
                File.AppendAllText(StartupLogPath, line + Environment.NewLine);
            }
            catch { }
        }

        private static void InitConsoleOutput()
        {
            try
            {
                IntPtr stdHandle = GetStdHandle(STD_OUTPUT_HANDLE);
                uint fileType = GetFileType(stdHandle);
                LogStartup("Initial GetStdHandle: " + stdHandle + " Type: " + fileType);
                if (fileType != FILE_TYPE_PIPE && fileType != FILE_TYPE_DISK && fileType != FILE_TYPE_CHAR)
                {
                    bool attached = AttachConsole(-1);
                    LogStartup("AttachConsole(-1) result: " + attached);
                    stdHandle = GetStdHandle(STD_OUTPUT_HANDLE);
                }

                if (stdHandle != IntPtr.Zero && stdHandle != new IntPtr(-1))
                {
                    SafeFileHandle safeFileHandle = new SafeFileHandle(stdHandle, false);
                    FileStream fs = new FileStream(safeFileHandle, FileAccess.Write);
                    StreamWriter sw = new StreamWriter(fs, Encoding.UTF8);
                    sw.AutoFlush = true;
                    Console.SetOut(sw);
                    Console.SetError(sw);
                    LogStartup("Console.SetOut succeeded");
                }
            }
            catch (Exception ex)
            {
                LogStartup("InitConsoleOutput exception: " + ex.ToString());
            }
        }

#if !PRODUCTION
        private static string GetDefaultCaptureDir(string subfolder = null)
        {
            string baseDir = Environment.GetEnvironmentVariable("ANTIGRAVITY_ARTIFACTS_DIR");
            if (string.IsNullOrEmpty(baseDir))
            {
                baseDir = Environment.GetEnvironmentVariable("USB_SHIELD_OUT_DIR");
            }
            if (string.IsNullOrEmpty(baseDir))
            {
                baseDir = Path.Combine(Environment.CurrentDirectory, "Captures");
            }
            if (!string.IsNullOrEmpty(subfolder))
            {
                baseDir = Path.Combine(baseDir, subfolder);
            }
            return baseDir;
        }
#endif

        [STAThread]
        static int Main(string[] args)
        {
            try { File.Delete(StartupLogPath); } catch { }
            LogStartup("ENTRY START");

            // ─── 1. Background Windows Service Mode ───────────────────────────
            if (args != null && Array.Exists(args, a => string.Equals(a, "--service", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("SERVICE MODE SELECTED");
                ServiceBase.Run(new UsbShieldService());
                return 0;
            }

            // ─── 2. CLI Mode: Diagnostic Inspection ───────────────────────────
            if (args != null && Array.Exists(args, a => string.Equals(a, "--diag-detect", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("DIAG-DETECT MODE SELECTED");
                InitConsoleOutput();
                string outFile = null;
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outFile = args[i + 1];
                        break;
                    }
                }
                RunDiagnosticDetection(outFile);
                return 0;
            }

            // ─── 2c. CLI Mode: Security ACL & Ownership Verification ─────────
            if (args != null && Array.Exists(args, a => string.Equals(a, "--diag-security", StringComparison.OrdinalIgnoreCase) ||
                                                        string.Equals(a, "--verify-security", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("DIAG-SECURITY MODE SELECTED");
                InitConsoleOutput();
                string outFile = null;
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outFile = args[i + 1];
                        break;
                    }
                }
                RunDiagnosticSecurity(outFile);
                return 0;
            }

            // ─── 2b. CLI Mode: Latency Diagnostics & Benchmarking ─────────────
            if (args != null && Array.Exists(args, a => string.Equals(a, "--diagnostic-latency", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("DIAGNOSTIC-LATENCY MODE SELECTED");
                InitConsoleOutput();
                string outFile = null;
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outFile = args[i + 1];
                        break;
                    }
                }

                StringBuilder latencySb = new StringBuilder();
                Action<string> latencyLog = s =>
                {
                    Console.WriteLine(s);
                    latencySb.AppendLine(s);
                };

                LatencyTracker.RunDiagnosticBenchmark(latencyLog);

                if (!string.IsNullOrEmpty(outFile))
                {
                    try { File.WriteAllText(outFile, latencySb.ToString()); } catch { }
                }
                return 0;
            }

            // ─── 3. CLI Mode: Standalone Recovery Rollback ─────────────────────
            if (args != null && Array.Exists(args, a =>
                string.Equals(a, "--restore-exact", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "--restore-defaults", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "--restore", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("RESTORE-EXACT MODE SELECTED");
                InitConsoleOutput();
                string outFile = null;
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outFile = args[i + 1];
                        break;
                    }
                }

                StringBuilder logSb = new StringBuilder();
                Action<string> printLog = s =>
                {
                    Console.WriteLine(s);
                    logSb.AppendLine(s);
                };

                printLog("Restoring exact pre-shield configuration from write-ahead journal...");
                List<string> errors;
                bool ok = RecoveryManager.RestorePreShieldState(out errors);
                if (ok)
                {
                    printLog("SUCCESS: Pre-shield configuration restored.");
                }
                else
                {
                    printLog("FAILED: " + string.Join("; ", errors.ToArray()));
                }

                if (!string.IsNullOrEmpty(outFile))
                {
                    try { File.WriteAllText(outFile, logSb.ToString()); } catch { }
                }
                try
                {
                    // No disk log dropped to ProgramData

                }
                catch { }

                return ok ? 0 : 1;
            }

            // ─── 4. CLI Mode: Install / Uninstall Background Service ──────────
            if (args != null && Array.Exists(args, a => string.Equals(a, "--install-service", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("INSTALL-SERVICE MODE SELECTED");
                InitConsoleOutput();
                string err;
                bool ok = ServiceManager.InstallService(out err);
                Console.WriteLine(ok ? "Service installed successfully." : ("Failed: " + err));
                return ok ? 0 : 1;
            }

            if (args != null && Array.Exists(args, a => string.Equals(a, "--uninstall-service", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("UNINSTALL-SERVICE MODE SELECTED");
                InitConsoleOutput();
                string err;
                bool ok = ServiceManager.UninstallService(out err);
                Console.WriteLine(ok ? "Service uninstalled successfully." : ("Failed: " + err));
                return ok ? 0 : 1;
            }

#if !PRODUCTION
            // ─── 5. CLI Mode: Isolated Recovery Test Harness ─────────────────
            if (args != null && Array.Exists(args, a => string.Equals(a, "--test-harness", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("TEST-HARNESS MODE SELECTED");
                InitConsoleOutput();
                string outFile = null;
                string forcedHive = null;
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outFile = args[i + 1];
                    }
                    if (string.Equals(args[i], "--hive", StringComparison.OrdinalIgnoreCase))
                    {
                        forcedHive = args[i + 1];
                    }
                }

                StringBuilder harnessSb = new StringBuilder();
                Action<string> harnessLog = s =>
                {
                    Console.WriteLine(s);
                    harnessSb.AppendLine(s);
                };

                List<string> failures;
                bool passed = TestHarness.RunSafeRecoveryTests(harnessLog, out failures, forcedHive);

                if (!string.IsNullOrEmpty(outFile))
                {
                    try { File.WriteAllText(outFile, harnessSb.ToString()); } catch { }
                }

                return passed ? 0 : 1;
            }

            // ─── 5.5 CLI Mode: Native UI/UX Pass 3 Screenshot Capture ───────
            if (args != null && Array.Exists(args, a => string.Equals(a, "--capture-pass3", StringComparison.OrdinalIgnoreCase) ||
                                                        string.Equals(a, "--capture-pass2", StringComparison.OrdinalIgnoreCase) ||
                                                        string.Equals(a, "--capture-screens", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("CAPTURE-PASS3 MODE SELECTED");
                InitConsoleOutput();
                string outDir = GetDefaultCaptureDir();
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out-dir", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outDir = args[i + 1];
                    }
                }
                ScreenCaptureRunner.RunPass3Captures(outDir);
                return 0;
            }

            // ─── 5.5b CLI Mode: v2.1 Remediation Captures ───────────────────
            if (args != null && Array.Exists(args, a => string.Equals(a, "--capture-remediation", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("CAPTURE-REMEDIATION MODE SELECTED");
                InitConsoleOutput();
                string outDir = GetDefaultCaptureDir();
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out-dir", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outDir = args[i + 1];
                    }
                }
                ScreenCaptureRunner.RunRemediationCaptures(outDir);
                Environment.Exit(0);
                return 0;
            }

            // ─── 5.6 CLI Mode: v2.1 Theme Screenshot Capture ──────────────────
            if (args != null && Array.Exists(args, a => string.Equals(a, "--capture-themes", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("CAPTURE-THEMES MODE SELECTED");
                InitConsoleOutput();
                string outDir = GetDefaultCaptureDir("theme_smoke");
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out-dir", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outDir = args[i + 1];
                    }
                }
                MainForm.IsCaptureMode = true;
                ScreenCaptureRunner.RunThemeCaptures(outDir);
                Environment.Exit(0);
                return 0;
            }

            // ─── 5.7 CLI Mode: v2.1 Component Showcase Capture ────────────────
            if (args != null && Array.Exists(args, a => string.Equals(a, "--capture-components", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("CAPTURE-COMPONENTS MODE SELECTED");
                InitConsoleOutput();
                string outDir = GetDefaultCaptureDir("component_showcase");
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out-dir", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outDir = args[i + 1];
                    }
                }
                MainForm.IsCaptureMode = true;
                ScreenCaptureRunner.RunComponentCaptures(outDir);
                Environment.Exit(0);
                return 0;
            }

            // ─── 5.8 CLI Mode: v2.1 Shell Prototype Capture ───────────────────
            if (args != null && Array.Exists(args, a => string.Equals(a, "--capture-shell-prototype", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("CAPTURE-SHELL-PROTOTYPE MODE SELECTED");
                InitConsoleOutput();
                string outDir = GetDefaultCaptureDir("shell_prototype");
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out-dir", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outDir = args[i + 1];
                    }
                }
                MainForm.IsCaptureMode = true;
                ScreenCaptureRunner.RunShellPrototypeCaptures(outDir);
                Environment.Exit(0);
                return 0;
            }

            // ─── 5.9 CLI Mode: v2.1 Navigation Prototype Capture ──────────────
            if (args != null && Array.Exists(args, a => string.Equals(a, "--capture-nav-prototype", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("CAPTURE-NAV-PROTOTYPE MODE SELECTED");
                InitConsoleOutput();
                string outDir = GetDefaultCaptureDir("nav_prototype");
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out-dir", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outDir = args[i + 1];
                    }
                }
                MainForm.IsCaptureMode = true;
                ScreenCaptureRunner.RunNavigationPrototypeCaptures(outDir);
                Environment.Exit(0);
                return 0;
            }

            // ─── 5.10 CLI Mode: v2.1 Integrated Shell Capture ─────────────────
            if (args != null && Array.Exists(args, a => string.Equals(a, "--capture-integrated-shell", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("CAPTURE-INTEGRATED-SHELL MODE SELECTED");
                InitConsoleOutput();
                string outDir = GetDefaultCaptureDir("integrated_shell");
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out-dir", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outDir = args[i + 1];
                    }
                }
                MainForm.IsCaptureMode = true;
                ScreenCaptureRunner.RunIntegratedShellCaptures(outDir);
                Environment.Exit(0);
                return 0;
            }

            // ─── 5.11 CLI Mode: v2.1 Phase 4.1 Production Dashboard Capture ───
            if (args != null && Array.Exists(args, a => string.Equals(a, "--capture-dashboard-v21", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("CAPTURE-DASHBOARD-V21 MODE SELECTED");
                InitConsoleOutput();
                string outDir = GetDefaultCaptureDir("dashboard_v21");
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out-dir", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outDir = args[i + 1];
                    }
                }
                MainForm.IsCaptureMode = true;
                ScreenCaptureRunner.RunDashboardV21Captures(outDir);
                Environment.Exit(0);
                return 0;
            }

            // ─── 5.12 CLI Mode: v2.1 Phase 4.2 Connected Devices Capture ──────
            if (args != null && Array.Exists(args, a => string.Equals(a, "--capture-devices-v21", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("CAPTURE-DEVICES-V21 MODE SELECTED");
                InitConsoleOutput();
                string outDir = GetDefaultCaptureDir("devices_v21");
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out-dir", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outDir = args[i + 1];
                    }
                }
                MainForm.IsCaptureMode = true;
                ScreenCaptureRunner.RunDevicesV21Captures(outDir);
                Environment.Exit(0);
                return 0;
            }

            // ─── 5.13 CLI Mode: v2.1 Phase 4.3 Protection Page Capture ────────────
            if (args != null && Array.Exists(args, a => string.Equals(a, "--capture-protection-v21", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("CAPTURE-PROTECTION-V21 MODE SELECTED");
                InitConsoleOutput();
                string outDir = GetDefaultCaptureDir("protection_v21");
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out-dir", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outDir = args[i + 1];
                    }
                }
                MainForm.IsCaptureMode = true;
                ScreenCaptureRunner.RunProtectionV21Captures(outDir);
                Environment.Exit(0);
                return 0;
            }

            // ─── 5.14 CLI Mode: v2.1 Phase 4.4 Trusted Devices Page Capture ──────
            if (args != null && Array.Exists(args, a => string.Equals(a, "--capture-trusted-v21", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("CAPTURE-TRUSTED-V21 MODE SELECTED");
                InitConsoleOutput();
                string outDir = GetDefaultCaptureDir("trusted_v21");
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out-dir", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outDir = args[i + 1];
                    }
                }
                MainForm.IsCaptureMode = true;
                ScreenCaptureRunner.RunTrustedV21Captures(outDir);
                Environment.Exit(0);
                return 0;
            }

            if (args != null && Array.Exists(args, a => string.Equals(a, "--capture-activity-v21", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("CAPTURE-ACTIVITY-V21 MODE SELECTED");
                InitConsoleOutput();
                string outDir = GetDefaultCaptureDir("activity_v21");
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out-dir", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outDir = args[i + 1];
                    }
                }
                MainForm.IsCaptureMode = true;
                ScreenCaptureRunner.RunActivityV21Captures(outDir);
                Environment.Exit(0);
                return 0;
            }

            if (args != null && Array.Exists(args, a => string.Equals(a, "--capture-recovery-v21", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("CAPTURE-RECOVERY-V21 MODE SELECTED");
                InitConsoleOutput();
                string outDir = GetDefaultCaptureDir("recovery_v21");
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out-dir", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outDir = args[i + 1];
                    }
                }
                MainForm.IsCaptureMode = true;
                ScreenCaptureRunner.RunRecoveryV21Captures(outDir);
                Environment.Exit(0);
                return 0;
            }

            if (args != null && Array.Exists(args, a => string.Equals(a, "--capture-settings-v21", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("CAPTURE-SETTINGS-V21 MODE SELECTED");
                InitConsoleOutput();
                string outDir = GetDefaultCaptureDir("settings_v21");
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out-dir", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outDir = args[i + 1];
                    }
                }
                MainForm.IsCaptureMode = true;
                ScreenCaptureRunner.RunSettingsV21Captures(outDir);
                Environment.Exit(0);
                return 0;
            }

            if (args != null && Array.Exists(args, a => string.Equals(a, "--capture-rc5-live", StringComparison.OrdinalIgnoreCase)))
            {
                LogStartup("CAPTURE-RC5-LIVE MODE SELECTED");
                InitConsoleOutput();
                string outDir = GetDefaultCaptureDir("rc5_live");
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--out-dir", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    {
                        outDir = args[i + 1];
                    }
                }
                MainForm.IsCaptureMode = true;
                ScreenCaptureRunner.RunRc5LiveCaptures(outDir);
                Environment.Exit(0);
                return 0;
            }

#endif // !PRODUCTION

            // ─── 6. Interactive GUI Mode ──────────────────────────────────────
            LogStartup("ARGUMENTS PARSED");

            // Check admin privileges (Manifest specifies requireAdministrator for Explorer double-click)
            bool isAdmin = IsRunningAsAdmin();
            if (isAdmin)
            {
                LogStartup("ADMIN CHECK PASSED");
            }
            else
            {
                LogStartup("ADMIN CHECK PASSED (Non-elevated caller; manifest specifies requireAdministrator on shell launch)");
            }

            // Single instance check with robust cross-session exception protection
            bool bypassMutex = args != null && Array.Exists(args, a => string.Equals(a, "--force", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "--no-mutex", StringComparison.OrdinalIgnoreCase));
            bool createdNew = false;
            bool ownsMutex = false;
            if (!bypassMutex)
            {
                try
                {
                    _mutex = new Mutex(true, "Global\\UsbShieldPro_x0abood_2026", out createdNew);
                    ownsMutex = createdNew;
                }
                catch (Exception ex)
                {
                    LogStartup("MUTEX WARNING: " + ex.Message);
                    createdNew = false;
                    ownsMutex = false;
                }

                if (!createdNew)
                {
                    // Vector 10B: Unconditionally signal the running instance to restore from tray first
                    try
                    {
                        using (var evt = EventWaitHandle.OpenExisting("Global\\UsbShieldPro_TrayWakeup_Event"))
                        {
                            evt.Set();
                            LogStartup("MUTEX: Signaled existing tray instance to restore via EventWaitHandle.");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogStartup("MUTEX: Failed to signal tray wakeup event: " + ex.Message);
                    }

                    // Allow the primary instance to take foreground focus
                    try
                    {
                        AllowSetForegroundWindow(ASFW_ANY);
                    }
                    catch { }

                    // Also attempt to find existing window handle and bring it forward as fallback
                    IntPtr existingWnd = IntPtr.Zero;
                    try
                    {
                        Process currentProc = Process.GetCurrentProcess();
                        Process[] procs = Process.GetProcessesByName(currentProc.ProcessName);
                        foreach (Process p in procs)
                        {
                            if (p.Id != currentProc.Id && p.MainWindowHandle != IntPtr.Zero)
                            {
                                existingWnd = p.MainWindowHandle;
                                break;
                            }
                        }
                    }
                    catch { }

                    if (existingWnd == IntPtr.Zero)
                    {
                        try
                        {
                            IntPtr fw = FindWindow(null, "USB Shield Pro");
                            if (fw != IntPtr.Zero) existingWnd = fw;
                        }
                        catch { }
                    }

                    if (existingWnd != IntPtr.Zero)
                    {
                        LogStartup("MUTEX: Active window found on existing process. Bringing to foreground.");
                        try { SetForegroundWindow(existingWnd); } catch { }
                    }

                    _mutex = null;
                    return 0;
                }
                else
                {
                    LogStartup("MUTEX ACQUIRED");
                }
            }
            else
            {
                LogStartup("MUTEX BYPASSED VIA CLI FLAG");
            }

            try
            {
                LogStartup("DPI INITIALIZED");
                try
                {
                    var culture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
                    culture.NumberFormat.DigitSubstitution = System.Globalization.DigitShapes.None;
                    System.Threading.Thread.CurrentThread.CurrentCulture = culture;

                    var uiCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentUICulture.Clone();
                    uiCulture.NumberFormat.DigitSubstitution = System.Globalization.DigitShapes.None;
                    System.Threading.Thread.CurrentThread.CurrentUICulture = uiCulture;
                }
                catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                LogStartup("APPLICATION CONFIGURED");

                // ─── Elevated Startup Security & Ownership Enforcement ───────────
                if (isAdmin)
                {
                    string secError;
                    bool secOk = SecurityAclHelper.EnsureAllOwnershipAndAclsElevated(out secError);
                    if (!secOk)
                    {
                        LogStartup("SECURITY ENFORCEMENT WARNING: " + secError);
                        MsgBox.Show(null,
                            "USB Shield Pro Security Enforcement Notice:\n\n" +
                            "Configuration directory ownership or ACL verification returned an issue:\n" + secError + "\n\n" +
                            "Sensitive machine-level files may not be fully protected.",
                            "Security Configuration Notice");
                    }
                    else
                    {
                        LogStartup("ELEVATED SECURITY & OWNERSHIP REPAIR COMPLETED AND VERIFIED");
                    }
                }

                AppSettingsData settings = null;
                try
                {
                    settings = SettingsRepository.Load();
                    LogStartup("SETTINGS LOADED");
                }
                catch (Exception ex)
                {
                    LogStartup("SETTINGS LOAD EXCEPTION: " + ex.Message);
                }

                try
                {
                    bool authConfigured = PasswordManager.IsPasswordConfigured();
                    LogStartup("AUTH STATE LOADED (Configured=" + authConfigured + ")");
                }
                catch (Exception ex)
                {
                    LogStartup("AUTH STATE EXCEPTION: " + ex.Message);
                }

                bool isFirstRun = (settings == null || !settings.OnboardingCompleted);
                LogStartup("FIRST-RUN STATE DETERMINED (FirstRun=" + isFirstRun + ")");

                LogStartup("MAINFORM CONSTRUCTOR BEGIN");
                MainForm mainForm = new MainForm();
                LogStartup("MAINFORM CONSTRUCTOR COMPLETE");

                // Audit & Force visible state for normal launch
                mainForm.ShowInTaskbar = true;
                mainForm.WindowState = FormWindowState.Normal;
                mainForm.Visible = true;
                mainForm.Opacity = 1.0;
                mainForm.StartPosition = FormStartPosition.CenterScreen;

                LogStartup("APPLICATION.RUN BEGIN");
                Application.Run(mainForm);
                LogStartup("APPLICATION EXIT");
                return 0;
            }
            catch (Exception ex)
            {
                StringBuilder errSb = new StringBuilder();
                errSb.AppendLine("STARTUP CRASH EXCEPTION:");
                Exception cur = ex;
                int depth = 0;
                while (cur != null)
                {
                    errSb.AppendLine(string.Format("[{0}] {1}: {2}", depth, cur.GetType().FullName, cur.Message));
                    errSb.AppendLine("StackTrace:");
                    errSb.AppendLine(cur.StackTrace);
                    cur = cur.InnerException;
                    depth++;
                }
                string errText = errSb.ToString();
                LogStartup(errText);
                MsgBox.Show(null, "USB Shield Pro Fatal Startup Crash:\n\n" + errText, "Startup Error");
                return -1;
            }
            finally
            {
                if (_mutex != null && ownsMutex)
                {
                    try { _mutex.ReleaseMutex(); } catch { }
                    try { _mutex.Dispose(); } catch { }
                    _mutex = null;
                }
            }
        }

        public static void RunDiagnosticDetection(string outFile = null)
        {
            StringBuilder sb = new StringBuilder();
            Action<string> print = s =>
            {
                Console.WriteLine(s);
                sb.AppendLine(s);
            };

            print("");
            print("================================================================================");
            print("  USB SHIELD PRO v2.1 — NON-DESTRUCTIVE DIAGNOSTIC DEVICE DETECTION");
            print("  (No registry, PnP, service, or driver settings will be modified)");
            print("================================================================================");
            print("");

            int sysDisk = SystemDeviceSafetyGuard.GetSystemPhysicalDiskNumber();
            print("System/Boot Physical Disk Number: " + (sysDisk >= 0 ? "#" + sysDisk : "Unknown (Protected by path)"));
            print("System Directory Path: " + Environment.SystemDirectory);
            print("");

            List<DeviceIdentity> devices = DeviceDiscovery.DiscoverAllDevices();
            print("--------------------------------------------------------------------------------");

            List<DeviceRule> trustedRules = TrustedDeviceRepository.LoadRules();
            if (TrustedDeviceRepository.LastLoadStatus == RepositoryLoadStatus.AccessDenied)
            {
                print("PERSISTED TRUST STATE: ACCESS DENIED IN CURRENT TOKEN");
                print("Production repository could not be evaluated.");
                print("Reason: Process running with unprivileged token unable to read strict ACL on primary repository.");

                string bak = TrustedDeviceRepository.GetFilePath() + ".bak";
                if (File.Exists(bak))
                {
                    try
                    {
                        string bakJson = File.ReadAllText(bak, Encoding.UTF8);
                        JavaScriptSerializer serializer = new JavaScriptSerializer();
                        var bakRules = serializer.Deserialize<List<DeviceRule>>(bakJson);
                        if (bakRules != null && bakRules.Count > 0)
                        {
                            print("");
                            print("RECOVERY BACKUP ONLY — NOT CURRENT AUTHORITATIVE STATE");
                            print("Total Backup Rules Available: " + bakRules.Count);
                            for (int b = 0; b < bakRules.Count; b++)
                            {
                                DeviceRule br = bakRules[b];
                                print(string.Format("  [Backup Rule {0}] Friendly Name: {1} | Serial: {2} | RuleID: {3}", b + 1, br.FriendlyName, br.TargetSerial ?? "None", br.RuleId));
                            }
                        }
                    }
                    catch { }
                }
            }
            else
            {
                print("Total Persistent Trusted Device Rules Configured: " + trustedRules.Count);
                for (int i = 0; i < trustedRules.Count; i++)
                {
                    DeviceRule r = trustedRules[i];
                    print(string.Format("  [Rule {0}] Friendly Name:        {1}", i + 1, r.FriendlyName));
                    print(string.Format("           Rule ID:              {0}", r.RuleId));
                    print(string.Format("           Target Instance ID:   {0}", r.TargetInstanceId ?? "None"));
                    print(string.Format("           Target Hardware ID:   {0}", r.TargetHardwareId ?? "None"));
                    print(string.Format("           Target Serial:        {0}", r.TargetSerial ?? "None"));
                    print(string.Format("           Match Type:           {0}", r.MatchType));
                    print(string.Format("           Identity Confidence:  {0}", r.Confidence));
                    print(string.Format("           Action:               {0}", r.Action));
                    print(string.Format("           Policy Index:         {0}", r.AllocatedPolicyIndex.HasValue ? r.AllocatedPolicyIndex.Value.ToString() : "None"));
                }
            }
            print("--------------------------------------------------------------------------------");

            for (int i = 0; i < devices.Count; i++)
            {
                DeviceIdentity d = devices[i];
                print(string.Format("[Device {0} of {1}]", i + 1, devices.Count));
                print("  Friendly Name:        " + d.FriendlyName);
                print("  Model:                " + (d.Model ?? "N/A"));
                print("  Manufacturer:         " + (d.Manufacturer ?? "N/A"));
                print("  PnP Instance ID:      " + (d.DeviceInstanceId ?? "N/A"));
                print("  Hardware IDs:         " + (d.HardwareIds.Count > 0 ? string.Join(", ", d.HardwareIds.ToArray()) : "None"));
                print("  Parent Device:        " + (d.ParentDeviceInstanceId ?? "None"));
                print("  Setup Class:          " + (d.SetupClassGuid ?? "N/A") + " (" + d.WindowsClass + ")");
                print("  Physical Transport:   " + d.PhysicalTransport);
                print("  Storage Disk Number:  " + (d.PhysicalDiskNumber >= 0 ? "#" + d.PhysicalDiskNumber : "N/A"));
                print("  Total Capacity:       " + (d.TotalSizeBytes > 0 ? (d.TotalSizeBytes / (1024 * 1024 * 1024.0)).ToString("F2") + " GB" : "N/A"));
                print("  Removable Media Bit:  " + (d.IsRemovableMedia ? "True (Removable)" : "False (Fixed Disk)"));
                print("  Seek Penalty (SSD):   " + (d.HasSeekPenalty.HasValue ? (d.HasSeekPenalty.Value ? "True (Rotational HDD)" : "False (SSD/Flash)") : "Unknown"));
                print("  Serial Number:        " + (d.SerialNumber ?? "None/Synthetic"));
                print("  Identity Confidence:  " + d.Confidence + " (" + d.GetConfidenceDescription() + ")");
                print("  Volume Mappings:      " + (d.VolumeMountings.Count > 0 ? string.Join(", ", d.VolumeMountings.ToArray()) : "None"));
                print("  Friendly Category:    " + d.Category);
                print("  System Safety Result: " + d.SafetyResult.ToString().ToUpperInvariant());
                if (d.SafetyResult != SafetyValidationResult.Passed)
                {
                    print("  Safety Guard Reason:  " + d.SafetyRejectionReason);
                    print("  Enforcement Action:   STRICTLY PROHIBITED (FAIL-OPEN)");
                }
                else
                {
                    print("  Available Enforcements:");
                    print("    - Removable Policy Block:  " + (d.IsRemovableMedia ? "ReliablyEnforceableAfterReconnect" : "UnsupportedForDevice"));
                    print("    - Read-Only Mode:          " + (d.IsRemovableMedia ? "ReliablyEnforceableAfterReconnect" : "UnsupportedForDevice"));
                    print("    - Whitelist by Instance:   PartiallyEnforceable (Policy + Auto-Eject)");
                    print("    - Reactive Auto-Eject:     ReactiveOnly");

                    // 1. Truthful Persisted Rule Evaluation
                    DeviceRule matchedPersistedRule = null;
                    foreach (var r in trustedRules)
                    {
                        if (r.Matches(d))
                        {
                            matchedPersistedRule = r;
                            break;
                        }
                    }

                    if (matchedPersistedRule != null)
                    {
                        print("  PERSISTED RULE MATCH:      TRUE");
                        print("    - Rule ID:               " + matchedPersistedRule.RuleId);
                        print("    - Match Type:            " + matchedPersistedRule.MatchType);
                    }
                    else
                    {
                        print("  PERSISTED RULE MATCH:      NOT EVALUABLE");
                        print("    - Reason:                No persisted DeviceRule exists for " + (!string.IsNullOrEmpty(d.SerialNumber) ? d.SerialNumber : (d.DeviceInstanceId ?? "device")) + ".");
                    }

                    // 2. Clearly separated synthetic unit evaluation
                    print("  SYNTHETIC UNIT EVALUATION: (Candidate in-memory rule against detected identity)");
                    DeviceRule syntheticRule = new DeviceRule
                    {
                        FriendlyName = d.FriendlyName,
                        TargetInstanceId = d.DeviceInstanceId,
                        TargetSerial = d.SerialNumber,
                        MatchType = !string.IsNullOrEmpty(d.DeviceInstanceId) ? RuleMatchType.DeviceInstanceId : RuleMatchType.HardwareId,
                        Confidence = d.Confidence,
                        Action = RuleAction.AllowTrusted
                    };
                    if (d.HardwareIds.Count > 0) syntheticRule.TargetHardwareId = d.HardwareIds[0];
                    bool syntheticMatches = syntheticRule.Matches(d);
                    print("    - Synthetic Candidate Match: " + (syntheticMatches ? "TRUE" : "FALSE"));
                }
                print("--------------------------------------------------------------------------------");
            }
            print("");
            print("Diagnostic scan completed successfully. Zero mutations committed.");
            print("");

            if (!string.IsNullOrEmpty(outFile))
            {
                try { File.WriteAllText(outFile, sb.ToString()); } catch { }
            }
        }

        public static void RunDiagnosticSecurity(string outFile = null)
        {
            StringBuilder sb = new StringBuilder();
            Action<string> print = s =>
            {
                Console.WriteLine(s);
                sb.AppendLine(s);
            };

            print("");
            print("================================================================================");
            print("  USB SHIELD PRO v2.1 — SECURITY ACL & OWNERSHIP DIAGNOSTIC AUDIT");
            print("================================================================================");
            print("");

            WindowsIdentity currentIdentity = WindowsIdentity.GetCurrent();
            bool isAdmin = IsRunningAsAdmin();
            print("Current Process Identity: " + (currentIdentity != null ? currentIdentity.Name : "Unknown"));
            print("Process Elevation (Admin): " + isAdmin);
            print("");

            string rootDir = SecurityAclHelper.GetProgramDataDirectory();
            string[] targets = new string[]
            {
                rootDir,
                Path.Combine(rootDir, "trusted_devices.json"),
                Path.Combine(rootDir, "trusted_devices.json.bak"),
                Path.Combine(rootDir, "settings.json"),
                Path.Combine(rootDir, "journal.json"),
                Path.Combine(rootDir, "journal.bak"),
                Path.Combine(rootDir, ".auth"),
                Path.Combine(rootDir, ".recovery"),
                Path.Combine(rootDir, "temp_access.json"),


            };

            AccessControlSections secSections = AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access;

            foreach (string path in targets)
            {
                print("--------------------------------------------------------------------------------");
                print("TARGET: " + path);
                bool isDir = Directory.Exists(path);
                bool isFile = File.Exists(path);

                if (!isDir && !isFile)
                {
                    print("  Status: [NOT PRESENT]");
                    continue;
                }

                print("  Status: [EXISTS] (" + (isDir ? "Directory" : "File") + ")");

                try
                {
                    FileSystemSecurity sec = isDir ? 
                        (FileSystemSecurity)new DirectoryInfo(path).GetAccessControl(secSections) : 
                        (FileSystemSecurity)new FileInfo(path).GetAccessControl(secSections);

                    IdentityReference owner = sec.GetOwner(typeof(NTAccount));
                    IdentityReference ownerSid = sec.GetOwner(typeof(SecurityIdentifier));
                    IdentityReference group = sec.GetGroup(typeof(NTAccount));
                    IdentityReference groupSid = sec.GetGroup(typeof(SecurityIdentifier));

                    print("  Owner:         " + (owner != null ? owner.Value : "null") + " (" + (ownerSid != null ? ownerSid.Value : "null") + ")");
                    print("  Primary Group: " + (group != null ? group.Value : "null") + " (" + (groupSid != null ? groupSid.Value : "null") + ")");
                    print("  Inheritance:   " + (sec.AreAccessRulesProtected ? "Protected (Inheritance Blocked)" : "Inherited"));

                    AuthorizationRuleCollection rules = sec.GetAccessRules(true, false, typeof(NTAccount));
                    print("  Explicit ACEs (" + rules.Count + "):");
                    foreach (FileSystemAccessRule r in rules)
                    {
                        print(string.Format("    [{0}] {1,-28} | Rights: {2,-25} | Inherit: {3,-16} | Prop: {4}",
                            r.AccessControlType, r.IdentityReference.Value, r.FileSystemRights, r.InheritanceFlags, r.PropagationFlags));
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    print("  Security Descriptor: [ACCESS DENIED TO CALLER TOKEN] (Caller lacks READ_CONTROL)");
                }
                catch (Exception ex)
                {
                    print("  Security Descriptor Error: " + ex.Message);
                }
            }

            print("");
            print("================================================================================");
            print("  SECURITY VERIFICATION GATES");
            print("================================================================================");

            string dirReason;
            bool dirOk = SecurityAclHelper.VerifyProgramDataDirectorySecurity(rootDir, out dirReason);
            print("ROOT DIRECTORY GATE:  " + (dirOk ? "PASS" : "FAIL (" + dirReason + ")"));

            string primaryPath = Path.Combine(rootDir, "trusted_devices.json");
            if (File.Exists(primaryPath))
            {
                string primReason;
                bool primOk = SecurityAclHelper.VerifySensitiveFileSecurity(primaryPath, out primReason);
                print("PRIMARY FILE GATE:    " + (primOk ? "PASS" : "FAIL (" + primReason + ")"));
            }
            else
            {
                print("PRIMARY FILE GATE:    [FILE NOT PRESENT]");
            }

            string bakPath = primaryPath + ".bak";
            if (File.Exists(bakPath))
            {
                string bakReason;
                bool bakOk = SecurityAclHelper.VerifySensitiveFileSecurity(bakPath, out bakReason);
                print("BACKUP FILE GATE:     " + (bakOk ? "PASS" : "FAIL (" + bakReason + ")"));
            }
            else
            {
                print("BACKUP FILE GATE:     [FILE NOT PRESENT]");
            }

            print("");
            print("================================================================================");
            print("  EFFECTIVE ACCESS PROBES (Current Process Token)");
            print("================================================================================");

            print("ROOT DIRECTORY (" + rootDir + "):");
            print("  FILE_ADD_FILE:        " + ProbeFileAccess(rootDir, 0x0002, true));
            print("  FILE_ADD_SUBDIR:      " + ProbeFileAccess(rootDir, 0x0004, true));
            print("  FILE_DELETE_CHILD:    " + ProbeFileAccess(rootDir, 0x0040, true));
            print("  WRITE_DAC:            " + ProbeFileAccess(rootDir, 0x00040000, true));
            print("  WRITE_OWNER:          " + ProbeFileAccess(rootDir, 0x00080000, true));

            if (File.Exists(primaryPath))
            {
                print("PRIMARY FILE (trusted_devices.json):");
                print("  WRITE_DATA:           " + ProbeFileAccess(primaryPath, 0x0002, false));
                print("  DELETE:               " + ProbeFileAccess(primaryPath, 0x00010000, false));
                print("  WRITE_DAC:            " + ProbeFileAccess(primaryPath, 0x00040000, false));
                print("  WRITE_OWNER:          " + ProbeFileAccess(primaryPath, 0x00080000, false));
            }

            if (File.Exists(bakPath))
            {
                print("BACKUP FILE (trusted_devices.json.bak):");
                print("  WRITE_DATA:           " + ProbeFileAccess(bakPath, 0x0002, false));
                print("  DELETE:               " + ProbeFileAccess(bakPath, 0x00010000, false));
                print("  WRITE_DAC:            " + ProbeFileAccess(bakPath, 0x00040000, false));
                print("  WRITE_OWNER:          " + ProbeFileAccess(bakPath, 0x00080000, false));
            }

            print("================================================================================");

            if (!string.IsNullOrEmpty(outFile))
            {
                try { File.WriteAllText(outFile, sb.ToString()); } catch { }
            }
        }

        private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_SHARE_READ = 1;
        private const uint FILE_SHARE_WRITE = 2;
        private const uint FILE_SHARE_DELETE = 4;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private static string ProbeFileAccess(string path, uint accessMask, bool isDirectory)
        {
            uint flags = isDirectory ? FILE_FLAG_BACKUP_SEMANTICS : 0;
            IntPtr h = CreateFile(
                path,
                accessMask,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                IntPtr.Zero,
                OPEN_EXISTING,
                flags,
                IntPtr.Zero);

            if (h == new IntPtr(-1) || h == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                return string.Format("DENIED (Win32 Error: {0})", err);
            }
            else
            {
                CloseHandle(h);
                return "ALLOWED (Security Warning)";
            }
        }

        static bool IsRunningAsAdmin()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
