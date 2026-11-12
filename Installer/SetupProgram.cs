// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.1.0 — Native Self-Contained Installer & Uninstaller
// (c) 2026 0xAbood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("USB Shield Pro Setup")]
[assembly: AssemblyDescription("USB Shield Pro v2.1.0 Installer & Uninstaller")]
[assembly: AssemblyCompany("0xAbood")]
[assembly: AssemblyProduct("USB Shield Pro")]
[assembly: AssemblyCopyright("Copyright © 2026 0xAbood (https://github.com/0x-Abood/USB-Shield-Pro)")]
[assembly: AssemblyVersion("2.1.0.0")]
[assembly: AssemblyFileVersion("2.1.0.0")]

namespace UsbShieldPro.Installer
{
    internal static class Program
    {
        public const string EXPECTED_EXE_SHA256 = "4F9A904E9AB5236C18D298DD78443D5021DB5781BFCF16C0C037100DFEC5AFA9";
        public const long EXPECTED_EXE_LENGTH = 1060864L;

        public const string APP_NAME = "USB Shield Pro";
        public const string APP_VERSION = "2.1.0";
        public const string SERVICE_NAME = "UsbShieldService";

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenSCManager(string lpMachineName, string lpDatabaseName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "OpenServiceW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "DeleteService", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteService(IntPtr hService);

        [DllImport("advapi32.dll", EntryPoint = "CloseServiceHandle", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseServiceHandle(IntPtr hSCObject);

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 1. Verify Mode CLI Flag
            if (args != null && args.Length > 0 && args[0].Equals("--verify", StringComparison.OrdinalIgnoreCase))
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                string verifyMsg;
                bool verified = VerifyEmbeddedResources(out verifyMsg);
                Console.WriteLine("\n[USB Shield Pro Installer Verification]");
                Console.WriteLine(verifyMsg);
                Environment.Exit(verified ? 0 : 1);
                return;
            }

            // 2. Ensure Administrator Elevation
            if (!IsAdministrator())
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        Arguments = string.Join(" ", args ?? new string[0]),
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Administrator privileges are required to install or configure USB Shield Pro.\n\n" + ex.Message,
                        APP_NAME,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return;
            }

            // 3. Uninstall Mode CLI Flag
            if (args != null && args.Length > 0 &&
                (args[0].Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("/uninstall", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("-u", StringComparison.OrdinalIgnoreCase)))
            {
                bool silent = (args.Length > 1 && (args[1].Equals("--silent", StringComparison.OrdinalIgnoreCase) || args[1].Equals("/S", StringComparison.OrdinalIgnoreCase)));
                PerformUninstall(silent);
                return;
            }

            // 4. Silent Install Mode CLI Flag
            if (args != null && args.Length > 0 &&
                (args[0].Equals("--silent", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("/S", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("-s", StringComparison.OrdinalIgnoreCase)))
            {
                string err;
                bool ok = PerformInstall(true, false, (p, s) => { }, out err);
                Environment.Exit(ok ? 0 : 1);
                return;
            }

            // 5. GUI Setup Mode
            Application.Run(new SetupForm());
        }

        public static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(id);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static string ComputeSha256(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(data);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("X2"));
                return sb.ToString();
            }
        }

        public static bool VerifyEmbeddedResources(out string message)
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UsbShieldPro.exe"))
                {
                    if (stream == null)
                    {
                        message = "FAIL: Embedded resource 'UsbShieldPro.exe' not found.";
                        return false;
                    }

                    byte[] data = new byte[stream.Length];
                    int read = 0;
                    while (read < data.Length)
                    {
                        int r = stream.Read(data, read, data.Length - read);
                        if (r <= 0) break;
                        read += r;
                    }

                    if (data.Length != EXPECTED_EXE_LENGTH)
                    {
                        message = string.Format("FAIL: UsbShieldPro.exe byte length mismatch (Expected: {0}, Actual: {1})", EXPECTED_EXE_LENGTH, data.Length);
                        return false;
                    }

                    string actualSha = ComputeSha256(data);
                    if (!string.Equals(actualSha, EXPECTED_EXE_SHA256, StringComparison.OrdinalIgnoreCase))
                    {
                        message = string.Format("FAIL: UsbShieldPro.exe SHA-256 mismatch!\nExpected: {0}\nActual:   {1}", EXPECTED_EXE_SHA256, actualSha);
                        return false;
                    }

                    message = string.Format("PASS: UsbShieldPro.exe verified successfully.\n  Size:    {0:N0} bytes\n  SHA-256: {1}", data.Length, actualSha);
                    return true;
                }
            }
            catch (Exception ex)
            {
                message = "FAIL: Resource verification threw exception: " + ex.Message;
                return false;
            }
        }

        public static void StopRunningProcesses()
        {
            try
            {
                Process[] procs = Process.GetProcessesByName("UsbShieldPro");
                foreach (Process p in procs)
                {
                    try
                    {
                        p.CloseMainWindow();
                        if (!p.WaitForExit(2500))
                        {
                            p.Kill();
                            p.WaitForExit(1500);
                        }
                    }
                    catch { }
                }
            }
            catch { }

            try
            {
                using (var sc = new ServiceController(SERVICE_NAME))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(3));
                    }
                }
            }
            catch { }
        }

        public class InstallDiagnosticReport
        {
            public bool Success { get; set; }
            public string FailedPhase { get; set; }
            public int PhaseNumber { get; set; }
            public string ExceptionType { get; set; }
            public int HResult { get; set; }
            public int Win32ErrorCode { get; set; }
            public string Message { get; set; }
            public string StackTrace { get; set; }
            public string TargetDirectory { get; set; }

            public string FormatReport(bool isArabic)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine("USB SHIELD PRO INSTALLATION DIAGNOSTIC REPORT");
                sb.AppendLine("================================================================================");
                sb.AppendFormat("Timestamp:     {0:yyyy-MM-dd HH:mm:ss} UTC\n", DateTime.UtcNow);
                sb.AppendFormat("OS Version:    {0} ({1})\n", Environment.OSVersion, Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit");
                sb.AppendFormat("Target Path:   {0}\n", TargetDirectory ?? "Unknown");
                sb.AppendFormat("Failed Phase:  {0} (Phase {1}/6)\n", FailedPhase ?? "Unknown", PhaseNumber);
                sb.AppendFormat("Exception:     {0}\n", ExceptionType ?? "Unknown");
                sb.AppendFormat("HRESULT:       0x{0:X8}\n", HResult);
                if (Win32ErrorCode != 0)
                {
                    sb.AppendFormat("Win32 Error:   {0} (0x{0:X8})\n", Win32ErrorCode);
                }
                sb.AppendFormat("Error Message: {0}\n", Message ?? "None");
                if (!string.IsNullOrEmpty(StackTrace))
                {
                    sb.AppendLine("--------------------------------------------------------------------------------");
                    sb.AppendLine("Stack Trace:");
                    sb.AppendLine(StackTrace);
                }
                sb.AppendLine("================================================================================");
                return sb.ToString();
            }
        }

        public static void PersistDiagnosticReport(InstallDiagnosticReport report)
        {
            if (report == null) return;
            try
            {
                string logPath = Path.Combine(Path.GetTempPath(), "UsbShieldPro_Setup.log");
                File.WriteAllText(logPath, report.FormatReport(false), Encoding.UTF8);
            }
            catch { }
        }

        public static bool PerformInstall(bool createDesktopShortcut, bool launchAfter, Action<int, string> progress, out string error)
        {
            InstallDiagnosticReport rep;
            bool ok = PerformInstall(createDesktopShortcut, launchAfter, progress, out rep);
            error = ok ? null : (rep != null ? rep.Message : "Installation failed.");
            return ok;
        }

        public static bool PerformInstall(bool createDesktopShortcut, bool launchAfter, Action<int, string> progress, out InstallDiagnosticReport report)
        {
            report = new InstallDiagnosticReport { Success = false };
            string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), APP_NAME);
            report.TargetDirectory = targetDir;

            string currentPhase = "Initializing";
            int phaseNumber = 0;

            try
            {
                // Step 1: Stopping existing processes
                phaseNumber = 1;
                currentPhase = "Stopping running USB Shield Pro instances";
                progress(15, currentPhase + "...");
                StopRunningProcesses();
                Thread.Sleep(500);

                // Step 2: Ensure Target Directory Exists
                phaseNumber = 2;
                currentPhase = "Creating installation directory";
                progress(30, currentPhase + "...");
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                // Step 3: Extract & Verify UsbShieldPro.exe
                phaseNumber = 3;
                currentPhase = "Extracting and verifying core executable";
                progress(50, currentPhase + "...");
                string destExe = Path.Combine(targetDir, "UsbShieldPro.exe");
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UsbShieldPro.exe"))
                {
                    if (stream == null)
                    {
                        report.FailedPhase = currentPhase;
                        report.PhaseNumber = phaseNumber;
                        report.Message = "Embedded resource UsbShieldPro.exe not found in installer manifest.";
                        PersistDiagnosticReport(report);
                        return false;
                    }
                    byte[] data = new byte[stream.Length];
                    int read = 0;
                    while (read < data.Length)
                    {
                        int r = stream.Read(data, read, data.Length - read);
                        if (r <= 0) break;
                        read += r;
                    }

                    if (data.Length != EXPECTED_EXE_LENGTH)
                    {
                        report.FailedPhase = currentPhase;
                        report.PhaseNumber = phaseNumber;
                        report.Message = string.Format("Embedded binary size mismatch (Expected: {0}, Found: {1})", EXPECTED_EXE_LENGTH, data.Length);
                        PersistDiagnosticReport(report);
                        return false;
                    }

                    string hash = ComputeSha256(data);
                    if (!string.Equals(hash, EXPECTED_EXE_SHA256, StringComparison.OrdinalIgnoreCase))
                    {
                        report.FailedPhase = currentPhase;
                        report.PhaseNumber = phaseNumber;
                        report.Message = string.Format("Embedded binary checksum failed!\nExpected: {0}\nFound: {1}", EXPECTED_EXE_SHA256, hash);
                        PersistDiagnosticReport(report);
                        return false;
                    }

                    File.WriteAllBytes(destExe, data);
                }

                // Step 4: Extract Config & Readme
                phaseNumber = 4;
                currentPhase = "Extracting application configuration and documentation";
                progress(65, currentPhase + "...");
                string destConfig = Path.Combine(targetDir, "UsbShieldPro.exe.config");
                ExtractResourceToFile("UsbShieldPro.exe.config", destConfig);

                string destReadme = Path.Combine(targetDir, "README.txt");
                ExtractResourceToFile("README.txt", destReadme);

                // Copy Uninstaller
                string destUninstaller = Path.Combine(targetDir, "Uninstall.exe");
                try
                {
                    File.Copy(Application.ExecutablePath, destUninstaller, true);
                }
                catch { }

                // Step 5: Create Shortcuts
                phaseNumber = 5;
                currentPhase = "Creating application shortcuts";
                progress(80, currentPhase + "...");
                string startMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                if (Directory.Exists(startMenuDir))
                {
                    string startMenuLnk = Path.Combine(startMenuDir, "USB Shield Pro.lnk");
                    CreateShortcut(startMenuLnk, destExe, destExe + ",0", "USB Shield Pro - Advanced Endpoint Protection");
                }

                if (createDesktopShortcut)
                {
                    string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                    if (Directory.Exists(desktopDir))
                    {
                        string desktopLnk = Path.Combine(desktopDir, "USB Shield Pro.lnk");
                        CreateShortcut(desktopLnk, destExe, destExe + ",0", "USB Shield Pro - Advanced Endpoint Protection");
                    }
                }

                // Step 6: Register in Windows Add/Remove Programs
                phaseNumber = 6;
                currentPhase = "Registering application with Windows";
                progress(90, currentPhase + "...");
                RegisterUninstallEntry(targetDir, destExe, destUninstaller);

                progress(100, "Installation complete!");

                if (launchAfter)
                {
                    try
                    {
                        Process.Start(destExe);
                    }
                    catch { }
                }

                report.Success = true;
                return true;
            }
            catch (Exception ex)
            {
                report.Success = false;
                report.FailedPhase = currentPhase;
                report.PhaseNumber = phaseNumber;
                report.ExceptionType = ex.GetType().FullName;
                report.HResult = ex.HResult;
                Win32Exception w32 = ex as Win32Exception;
                if (w32 != null)
                {
                    report.Win32ErrorCode = w32.NativeErrorCode;
                }
                report.Message = ex.Message;
                report.StackTrace = ex.StackTrace;
                PersistDiagnosticReport(report);
                return false;
            }
        }

        private static void ExtractResourceToFile(string resourceName, string targetPath)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null) return;
                using (FileStream fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.CopyTo(fs);
                }
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string iconLocation, string description)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { shortcutPath });

                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(targetPath) });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
                if (!string.IsNullOrEmpty(iconLocation))
                {
                    shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconLocation });
                }
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            catch { }
        }

        private static void RegisterUninstallEntry(string targetDir, string exePath, string uninstallerPath)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey parent = baseKey.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                using (RegistryKey appKey = parent.CreateSubKey("USBShieldPro"))
                {
                    if (appKey != null)
                    {
                        appKey.SetValue("DisplayName", "USB Shield Pro", RegistryValueKind.String);
                        appKey.SetValue("DisplayVersion", "2.1.0.0", RegistryValueKind.String);
                        appKey.SetValue("Publisher", "0xAbood", RegistryValueKind.String);
                        appKey.SetValue("URLInfoAbout", "https://github.com/0x-Abood/USB-Shield-Pro", RegistryValueKind.String);
                        appKey.SetValue("HelpLink", "https://github.com/0x-Abood/USB-Shield-Pro", RegistryValueKind.String);
                        appKey.SetValue("DisplayIcon", exePath + ",0", RegistryValueKind.String);
                        appKey.SetValue("InstallLocation", targetDir, RegistryValueKind.String);
                        appKey.SetValue("UninstallString", "\"" + uninstallerPath + "\" --uninstall", RegistryValueKind.String);
                        appKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        appKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                        appKey.SetValue("EstimatedSize", 1052, RegistryValueKind.DWord);
                        appKey.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"), RegistryValueKind.String);
                    }
                }
            }
            catch { }
        }

        public static void PerformUninstall(bool silent)
        {
            bool isAr = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

            if (!silent)
            {
                string title = isAr ? "إلغاء تثبيت USB Shield Pro" : "Uninstall USB Shield Pro";
                string prompt = isAr
                    ? "هل أنت متأكد من رغبتك في إلغاء تثبيت USB Shield Pro وجميع مكوناته من هذا الكمبيوتر؟"
                    : "Are you sure you want to completely uninstall USB Shield Pro and all its components from this computer?";

                DialogResult dr = MessageBox.Show(prompt, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr != DialogResult.Yes) return;
            }

            // 1. Stop processes
            StopRunningProcesses();

            // 2. Stop & Delete Background Service via native Win32 advapi32 APIs
            try
            {
                using (var sc = new ServiceController(SERVICE_NAME))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(3));
                    }
                }
            }
            catch { }

            try
            {
                IntPtr hSCM = OpenSCManager(null, null, 0xF003F);
                if (hSCM != IntPtr.Zero)
                {
                    try
                    {
                        IntPtr hService = OpenService(hSCM, SERVICE_NAME, 0xF01FF | 0x00010000);
                        if (hService != IntPtr.Zero)
                        {
                            try
                            {
                                DeleteService(hService);
                            }
                            finally
                            {
                                CloseServiceHandle(hService);
                            }
                        }
                    }
                    finally
                    {
                        CloseServiceHandle(hSCM);
                    }
                }
            }
            catch { }

            // 3. Remove Shortcuts
            try
            {
                string startMenuLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "USB Shield Pro.lnk");
                if (File.Exists(startMenuLnk)) File.Delete(startMenuLnk);

                string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "USB Shield Pro.lnk");
                if (File.Exists(desktopLnk)) File.Delete(desktopLnk);

                string userDesktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "USB Shield Pro.lnk");
                if (File.Exists(userDesktopLnk)) File.Delete(userDesktopLnk);
            }
            catch { }

            // 4. Remove Registry Key
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey parent = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", true))
                {
                    if (parent != null)
                    {
                        parent.DeleteSubKeyTree("USBShieldPro", false);
                    }
                }
            }
            catch { }

            // 5. Spawn Delayed Self-Deleting Cleanup for C:\Program Files\USB Shield Pro
            try
            {
                string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), APP_NAME);
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c ping 127.0.0.1 -n 2 > nul & rmdir /s /q \"" + targetDir + "\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch { }

            if (!silent)
            {
                string doneTitle = isAr ? "إلغاء التثبيت مكتمل" : "Uninstall Complete";
                string doneMsg = isAr
                    ? "تم إلغاء تثبيت USB Shield Pro بنجاح من جهازك."
                    : "USB Shield Pro has been successfully uninstalled from your computer.";
                MessageBox.Show(doneMsg, doneTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SETUP GUI FORM
    // ═══════════════════════════════════════════════════════════════════════
    internal sealed class SetupForm : Form
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private bool _isArabic;
        private bool _isInstalling;
        private bool _isFinished;

        // Visual controls
        private Panel _pnlHeader;
        private Label _lblAppTitle;
        private Label _lblBadge;
        private Button _btnLangToggle;

        private Panel _pnlContent;
        private Panel _cardBanner;
        private Label _lblBannerTitle;
        private Label _lblBannerSub;

        private Panel _cardDest;
        private Label _lblDestTitle;
        private Label _lblDestPath;
        private Label _lblSpace;

        private CheckBox _chkDesktop;
        private CheckBox _chkLaunch;

        private Label _lblStatus;
        private ProgressBar _progressBar;

        private Panel _pnlErrorDetails;
        private TextBox _txtErrorDetails;
        private Button _btnCopyError;
        private Label _lblErrorLogPath;
        private Program.InstallDiagnosticReport _lastReport;

        private Panel _pnlFooter;
        private Button _btnInstall;
        private Button _btnCancel;
        private Button _btnFinish;

        // Theme palette
        private static readonly Color ColorBg = Color.FromArgb(15, 20, 28);
        private static readonly Color ColorHeader = Color.FromArgb(18, 24, 38);
        private static readonly Color ColorCard = Color.FromArgb(22, 31, 46);
        private static readonly Color ColorBorder = Color.FromArgb(34, 48, 68);
        private static readonly Color ColorCyan = Color.FromArgb(0, 229, 255);
        private static readonly Color ColorBtnCyan = Color.FromArgb(0, 180, 216);
        private static readonly Color ColorGreen = Color.FromArgb(16, 185, 129);
        private static readonly Color ColorTextWhite = Color.FromArgb(248, 250, 252);
        private static readonly Color ColorTextSlate = Color.FromArgb(148, 163, 184);
        private static readonly Color ColorTextMuted = Color.FromArgb(100, 116, 139);

        public SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ClientSize = new Size(560, 450);
            this.BackColor = ColorBg;
            this.ForeColor = ColorTextWhite;
            this.Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            this.DoubleBuffered = true;

            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            _isArabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

            InitializeComponents();
            SetLanguage(_isArabic);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int darkMode = 1;
                DwmSetWindowAttribute(this.Handle, 20, ref darkMode, sizeof(int));
                DwmSetWindowAttribute(this.Handle, 19, ref darkMode, sizeof(int));
            }
            catch { }
        }

        private void InitializeComponents()
        {
            // 1. Header Panel
            _pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = ColorHeader
            };
            _pnlHeader.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(ColorBorder, 1))
                {
                    e.Graphics.DrawLine(pen, 0, _pnlHeader.Height - 1, _pnlHeader.Width, _pnlHeader.Height - 1);
                }
                DrawHeaderShield(e.Graphics);
            };

            _lblAppTitle = new Label
            {
                Text = "USB Shield Pro",
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                ForeColor = ColorTextWhite,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _lblBadge = new Label
            {
                Text = "v2.1.0",
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                ForeColor = ColorCyan,
                BackColor = Color.FromArgb(10, 40, 55),
                AutoSize = false,
                Size = new Size(50, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _lblBadge.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(0, 150, 180), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, _lblBadge.Width - 1, _lblBadge.Height - 1);
                }
            };

            _btnLangToggle = new Button
            {
                Size = new Size(88, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(26, 36, 52),
                ForeColor = ColorCyan,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnLangToggle.FlatAppearance.BorderColor = Color.FromArgb(44, 62, 88);
            _btnLangToggle.Click += (s, e) => SetLanguage(!_isArabic);

            _pnlHeader.Controls.Add(_lblAppTitle);
            _pnlHeader.Controls.Add(_lblBadge);
            _pnlHeader.Controls.Add(_btnLangToggle);
            _pnlHeader.Resize += (s, e) => LayoutHeader();

            // 2. Footer Panel
            _pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = ColorHeader
            };
            _pnlFooter.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(ColorBorder, 1))
                {
                    e.Graphics.DrawLine(pen, 0, 0, _pnlFooter.Width, 0);
                }
            };

            _btnInstall = new Button
            {
                Size = new Size(125, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorBtnCyan,
                ForeColor = Color.FromArgb(15, 20, 28),
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnInstall.FlatAppearance.BorderSize = 0;
            _btnInstall.Click += OnInstallClicked;

            _btnCancel = new Button
            {
                Size = new Size(95, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(203, 213, 225),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            _btnCancel.FlatAppearance.BorderColor = ColorBorder;
            _btnCancel.Click += (s, e) => this.Close();

            _btnFinish = new Button
            {
                Size = new Size(125, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorGreen,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Visible = false
            };
            _btnFinish.FlatAppearance.BorderSize = 0;
            _btnFinish.Click += (s, e) => this.Close();

            _pnlFooter.Controls.Add(_btnInstall);
            _pnlFooter.Controls.Add(_btnCancel);
            _pnlFooter.Controls.Add(_btnFinish);
            _pnlFooter.Resize += (s, e) => LayoutFooter();

            // 3. Content Panel
            _pnlContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBg
            };

            // Banner Card
            _cardBanner = new Panel
            {
                Height = 62,
                BackColor = ColorCard
            };
            _cardBanner.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(ColorBorder, 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, _cardBanner.Width - 1, _cardBanner.Height - 1);
            };

            _lblBannerTitle = new Label
            {
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                ForeColor = ColorTextWhite,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _lblBannerSub = new Label
            {
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
                ForeColor = ColorTextSlate,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _cardBanner.Controls.Add(_lblBannerTitle);
            _cardBanner.Controls.Add(_lblBannerSub);

            // Dest Card
            _cardDest = new Panel
            {
                Height = 68,
                BackColor = ColorCard
            };
            _cardDest.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(ColorBorder, 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, _cardDest.Width - 1, _cardDest.Height - 1);
            };

            _lblDestTitle = new Label
            {
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
                ForeColor = ColorTextSlate,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _lblDestPath = new Label
            {
                Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Program.APP_NAME),
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                ForeColor = ColorCyan,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _lblSpace = new Label
            {
                Font = new Font("Segoe UI", 8f, FontStyle.Regular),
                ForeColor = ColorTextMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _cardDest.Controls.Add(_lblDestTitle);
            _cardDest.Controls.Add(_lblDestPath);
            _cardDest.Controls.Add(_lblSpace);

            // Options Checkboxes
            _chkDesktop = new CheckBox
            {
                Checked = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(226, 232, 240),
                BackColor = Color.Transparent,
                AutoSize = true,
                Cursor = Cursors.Hand
            };

            _chkLaunch = new CheckBox
            {
                Checked = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(226, 232, 240),
                BackColor = Color.Transparent,
                AutoSize = true,
                Cursor = Cursors.Hand
            };

            // Status & Progress
            _lblStatus = new Label
            {
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = ColorTextSlate,
                AutoSize = false,
                BackColor = Color.Transparent
            };

            _progressBar = new ProgressBar
            {
                Height = 12,
                Style = ProgressBarStyle.Continuous,
                Value = 0
            };

            // Error Details Panel (Collapsible / Expandable on failure)
            _pnlErrorDetails = new Panel
            {
                Visible = false,
                BackColor = Color.Transparent
            };

            _txtErrorDetails = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(12, 17, 26),
                ForeColor = Color.FromArgb(248, 113, 113),
                Font = new Font("Consolas", 8.25f, FontStyle.Regular),
                BorderStyle = BorderStyle.FixedSingle,
                WordWrap = false
            };

            _btnCopyError = new Button
            {
                Size = new Size(140, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(203, 213, 225),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            _btnCopyError.FlatAppearance.BorderColor = ColorBorder;
            _btnCopyError.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_txtErrorDetails.Text))
                {
                    try
                    {
                        Clipboard.SetText(_txtErrorDetails.Text);
                        _btnCopyError.Text = _isArabic ? "تم النسخ!" : "Copied!";
                    }
                    catch { }
                }
            };

            _lblErrorLogPath = new Label
            {
                Font = new Font("Segoe UI", 7.75f, FontStyle.Regular),
                ForeColor = ColorTextMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _pnlErrorDetails.Controls.Add(_txtErrorDetails);
            _pnlErrorDetails.Controls.Add(_btnCopyError);
            _pnlErrorDetails.Controls.Add(_lblErrorLogPath);

            _pnlContent.Controls.Add(_cardBanner);
            _pnlContent.Controls.Add(_cardDest);
            _pnlContent.Controls.Add(_chkDesktop);
            _pnlContent.Controls.Add(_chkLaunch);
            _pnlContent.Controls.Add(_lblStatus);
            _pnlContent.Controls.Add(_progressBar);
            _pnlContent.Controls.Add(_pnlErrorDetails);
            _pnlContent.Resize += (s, e) => LayoutContent();

            this.Controls.Add(_pnlContent);
            this.Controls.Add(_pnlFooter);
            this.Controls.Add(_pnlHeader);
        }

        private void DrawHeaderShield(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int shieldX = _isArabic ? (_pnlHeader.Width - 20 - 28) : 20;
            int shieldY = (_pnlHeader.Height - 32) / 2;

            // Simple vector shield shape
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddLine(shieldX + 14, shieldY, shieldX + 28, shieldY + 6);
                path.AddLine(shieldX + 28, shieldY + 6, shieldX + 28, shieldY + 18);
                path.AddBezier(shieldX + 28, shieldY + 18, shieldX + 24, shieldY + 28, shieldX + 14, shieldY + 32, shieldX + 14, shieldY + 32);
                path.AddBezier(shieldX + 14, shieldY + 32, shieldX + 4, shieldY + 28, shieldX, shieldY + 18, shieldX, shieldY + 18);
                path.AddLine(shieldX, shieldY + 18, shieldX, shieldY + 6);
                path.CloseFigure();

                using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(16, 36, 56)))
                {
                    g.FillPath(fillBrush, path);
                }
                using (Pen pen = new Pen(ColorCyan, 1.8f))
                {
                    g.DrawPath(pen, path);
                }
            }
        }

        private void SetLanguage(bool isArabic)
        {
            _isArabic = isArabic;
            this.SuspendLayout();

            this.RightToLeft = _isArabic ? RightToLeft.Yes : RightToLeft.No;

            this.Text = _isArabic ? "تثبيت USB Shield Pro v2.1.0" : "USB Shield Pro v2.1.0 Setup";
            _btnLangToggle.Text = _isArabic ? "English" : "العربية";

            _lblBannerTitle.Text = _isArabic
                ? "حماية متقدمة لمنافذ التوصيل والأمن السيبراني للأجهزة"
                : "Next-Generation Endpoint Protection & Hardware Security";
            _lblBannerSub.Text = _isArabic
                ? "تثبيت آمن لبرنامج USB Shield Pro v2.1.0 على جهازك."
                : "Safely deploy USB Shield Pro v2.1.0 to your system.";

            _lblDestTitle.Text = _isArabic ? "مجلد التثبيت:" : "Destination Directory:";
            _lblSpace.Text = _isArabic ? "المساحة المطلوبة: ~3.5 ميجابايت" : "Space Required: ~3.5 MB";

            _chkDesktop.Text = _isArabic ? "إنشاء اختصار على سطح المكتب" : "Create Desktop Shortcut";
            _chkLaunch.Text = _isArabic ? "تشغيل البرنامج بعد انتهاء التثبيت" : "Launch USB Shield Pro after installation";

            _btnInstall.Text = _isArabic ? "تثبيت الآن" : "Install Now";
            _btnCancel.Text = _isArabic ? "إلغاء" : "Cancel";
            _btnFinish.Text = _isArabic ? "إنهاء" : "Finish";

            if (_isFinished)
            {
                _lblStatus.Text = _isArabic ? "تم التثبيت بنجاح!" : "Installation completed successfully!";
            }
            else if (!_isInstalling)
            {
                _lblStatus.Text = _isArabic ? "جاهز لبدء التثبيت." : "Ready to install.";
            }

            if (_btnCopyError != null)
            {
                _btnCopyError.Text = _isArabic ? "نسخ تفاصيل الخطأ" : "Copy Error Details";
            }
            if (_lblErrorLogPath != null)
            {
                _lblErrorLogPath.Text = _isArabic ? "سجل التشخيص: %TEMP%\\UsbShieldPro_Setup.log" : "Log: %TEMP%\\UsbShieldPro_Setup.log";
            }

            LayoutHeader();
            LayoutContent();
            LayoutFooter();
            _pnlHeader.Invalidate();

            this.ResumeLayout(true);
        }

        private void LayoutHeader()
        {
            int h = _pnlHeader.Height;
            int w = _pnlHeader.Width;

            if (_isArabic)
            {
                _btnLangToggle.Location = new Point(20, (h - _btnLangToggle.Height) / 2);

                int textRight = w - 20 - 28 - 14;
                _lblAppTitle.Location = new Point(textRight - _lblAppTitle.Width, (h - _lblAppTitle.Height) / 2);
                _lblBadge.Location = new Point(_lblAppTitle.Left - 8 - _lblBadge.Width, (h - _lblBadge.Height) / 2 + 1);
            }
            else
            {
                int textLeft = 20 + 28 + 14;
                _lblAppTitle.Location = new Point(textLeft, (h - _lblAppTitle.Height) / 2);
                _lblBadge.Location = new Point(_lblAppTitle.Right + 8, (h - _lblBadge.Height) / 2 + 1);

                _btnLangToggle.Location = new Point(w - 20 - _btnLangToggle.Width, (h - _btnLangToggle.Height) / 2);
            }
        }

        private void LayoutFooter()
        {
            int h = _pnlFooter.Height;
            int w = _pnlFooter.Width;

            if (_isArabic)
            {
                _btnInstall.Location = new Point(20, (h - _btnInstall.Height) / 2);
                _btnFinish.Location = _btnInstall.Location;
                _btnCancel.Location = new Point(_btnInstall.Right + 10, (h - _btnCancel.Height) / 2);
            }
            else
            {
                _btnCancel.Location = new Point(w - 20 - _btnCancel.Width, (h - _btnCancel.Height) / 2);
                _btnInstall.Location = new Point(_btnCancel.Left - 10 - _btnInstall.Width, (h - _btnInstall.Height) / 2);
                _btnFinish.Location = _btnInstall.Location;
            }
        }

        private void LayoutContent()
        {
            int pad = 20;
            int cardW = _pnlContent.ClientSize.Width - (pad * 2);

            _cardBanner.Location = new Point(pad, 14);
            _cardBanner.Width = cardW;
            _cardBanner.Height = 72;

            _lblBannerTitle.Location = new Point(14, 10);
            _lblBannerTitle.Size = new Size(_cardBanner.Width - 28, 24);
            _lblBannerTitle.TextAlign = _isArabic ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;

            _lblBannerSub.Location = new Point(14, 38);
            _lblBannerSub.Size = new Size(_cardBanner.Width - 28, 24);
            _lblBannerSub.TextAlign = _isArabic ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;

            _cardDest.Location = new Point(pad, _cardBanner.Bottom + 12);
            _cardDest.Width = cardW;
            _cardDest.Height = 76;

            _lblDestTitle.Location = new Point(14, 10);
            _lblDestTitle.Size = new Size(_cardDest.Width - 28, 18);
            _lblDestTitle.TextAlign = _isArabic ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;

            _lblDestPath.Location = new Point(14, 30);
            _lblDestPath.Size = new Size(_cardDest.Width - 28, 20);
            _lblDestPath.TextAlign = _isArabic ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;

            _lblSpace.Location = new Point(14, 52);
            _lblSpace.Size = new Size(_cardDest.Width - 28, 18);
            _lblSpace.TextAlign = _isArabic ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;

            int chkY = _cardDest.Bottom + 14;
            _chkDesktop.RightToLeft = _isArabic ? RightToLeft.Yes : RightToLeft.No;
            _chkLaunch.RightToLeft = _isArabic ? RightToLeft.Yes : RightToLeft.No;

            if (_isArabic)
            {
                _chkDesktop.Location = new Point(pad + cardW - _chkDesktop.Width, chkY);
                _chkLaunch.Location = new Point(pad + cardW - _chkLaunch.Width, chkY + 26);
            }
            else
            {
                _chkDesktop.Location = new Point(pad + 4, chkY);
                _chkLaunch.Location = new Point(pad + 4, chkY + 26);
            }

            int statY = _chkLaunch.Bottom + 14;
            _lblStatus.Location = new Point(pad, statY);
            _lblStatus.Size = new Size(cardW, 22);
            _lblStatus.TextAlign = _isArabic ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;

            _progressBar.Location = new Point(pad, _lblStatus.Bottom + 6);
            _progressBar.Width = cardW;
            _progressBar.Height = 12;

            if (_pnlErrorDetails != null && _pnlErrorDetails.Visible)
            {
                _pnlErrorDetails.Location = new Point(pad, _progressBar.Bottom + 10);
                _pnlErrorDetails.Size = new Size(cardW, 145);

                _txtErrorDetails.Location = new Point(0, 0);
                _txtErrorDetails.Size = new Size(cardW, 110);

                if (_isArabic)
                {
                    _btnCopyError.Location = new Point(cardW - _btnCopyError.Width, 116);
                    _lblErrorLogPath.Location = new Point(0, 121);
                }
                else
                {
                    _btnCopyError.Location = new Point(0, 116);
                    _lblErrorLogPath.Location = new Point(_btnCopyError.Right + 12, 121);
                }
            }
        }

        private void OnInstallClicked(object sender, EventArgs e)
        {
            if (_isInstalling) return;
            _isInstalling = true;

            _btnInstall.Enabled = false;
            _btnCancel.Enabled = false;
            _btnLangToggle.Enabled = false;
            _chkDesktop.Enabled = false;
            _chkLaunch.Enabled = false;

            bool desktop = _chkDesktop.Checked;
            bool launch = _chkLaunch.Checked;

            Thread worker = new Thread(() =>
            {
                Program.InstallDiagnosticReport report;
                bool ok = Program.PerformInstall(
                    desktop,
                    launch,
                    (pct, msg) =>
                    {
                        try
                        {
                            this.BeginInvoke(new Action(() =>
                            {
                                _progressBar.Value = Math.Max(0, Math.Min(100, pct));
                                _lblStatus.Text = _isArabic ? TranslateStatusToAr(msg) : msg;
                                LayoutContent();
                            }));
                        }
                        catch { }
                    },
                    out report);

                this.BeginInvoke(new Action(() =>
                {
                    _isInstalling = false;
                    _isFinished = ok;

                    if (ok)
                    {
                        if (_pnlErrorDetails != null) _pnlErrorDetails.Visible = false;
                        this.ClientSize = new Size(560, 450);
                        _progressBar.Value = 100;
                        _lblStatus.ForeColor = ColorGreen;
                        _lblStatus.Text = _isArabic ? "تم التثبيت بنجاح!" : "Installation completed successfully!";
                        _btnInstall.Visible = false;
                        _btnCancel.Visible = false;
                        _btnFinish.Visible = true;
                        _btnFinish.Focus();
                    }
                    else
                    {
                        _lastReport = report;
                        if (_pnlErrorDetails != null)
                        {
                            _pnlErrorDetails.Visible = true;
                            _txtErrorDetails.Text = report != null ? report.FormatReport(_isArabic) : "Unknown error during installation.";
                            _lblErrorLogPath.Text = _isArabic ? "سجل التشخيص: %TEMP%\\UsbShieldPro_Setup.log" : "Log: %TEMP%\\UsbShieldPro_Setup.log";
                        }
                        this.ClientSize = new Size(560, 605);
                        _lblStatus.ForeColor = Color.FromArgb(239, 68, 68);
                        string phaseDesc = report != null && !string.IsNullOrEmpty(report.FailedPhase) ? report.FailedPhase : "Setup";
                        _lblStatus.Text = _isArabic
                            ? string.Format("فشل التثبيت (المرحلة {0}: {1})", report != null ? report.PhaseNumber : 0, TranslateStatusToAr(phaseDesc))
                            : string.Format("Installation failed at Phase {0} ({1})", report != null ? report.PhaseNumber : 0, phaseDesc);

                        _btnInstall.Enabled = true;
                        _btnCancel.Enabled = true;
                    }
                    LayoutContent();
                    LayoutFooter();
                }));
            });
            worker.IsBackground = true;
            worker.Start();
        }

        private static string TranslateStatusToAr(string enMsg)
        {
            if (string.IsNullOrEmpty(enMsg)) return "";
            if (enMsg.Contains("Stopping")) return "إيقاف تشغيل البرامج النشطة حالياً...";
            if (enMsg.Contains("Creating installation directory")) return "إنشاء مجلد التثبيت...";
            if (enMsg.Contains("Extracting and verifying")) return "استخراج ملفات البرنامج والتحقق من صحتها...";
            if (enMsg.Contains("Extracting application configuration")) return "استخراج ملفات الإعدادات والتوثيق...";
            if (enMsg.Contains("Creating application shortcuts")) return "إنشاء اختصارات البرنامج...";
            if (enMsg.Contains("Registering application")) return "تسجيل البرنامج في لوحة تحكم ويندوز...";
            if (enMsg.Contains("complete")) return "تم التثبيت بنجاح!";
            return enMsg;
        }
    }
}
