// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Service Controller & IPC Client
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using UsbShieldPro.Core;

namespace UsbShieldPro.Service
{
    /// <summary>
    /// Manages service installation, execution lifecycle, and client-side Named Pipe IPC.
    /// Invariant: Service uninstallation never silently alters policy unless explicitly requested.
    /// Invariant: Zero subprocess spawning — utilizes direct Win32 advapi32 APIs.
    /// Invariant: Zero stealth self-copying to Program Files or ProgramData.
    /// </summary>
    public static class ServiceManager
    {
        #region Native Win32 Service Management APIs
        private const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
        private const uint SERVICE_ALL_ACCESS = 0xF01FF;
        private const uint DELETE = 0x00010000;
        private const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;
        private const uint SERVICE_AUTO_START = 0x00000002;
        private const uint SERVICE_ERROR_NORMAL = 0x00000001;
        private const uint SERVICE_CONFIG_DESCRIPTION = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SERVICE_DESCRIPTION
        {
            public string lpDescription;
        }

        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenSCManager(string lpMachineName, string lpDatabaseName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "CreateServiceW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateService(
            IntPtr hSCManager,
            string lpServiceName,
            string lpDisplayName,
            uint dwDesiredAccess,
            uint dwServiceType,
            uint dwStartType,
            uint dwErrorControl,
            string lpBinaryPathName,
            string lpLoadOrderGroup,
            IntPtr lpdwTagId,
            string lpDependencies,
            string lpServiceStartName,
            string lpPassword);

        [DllImport("advapi32.dll", EntryPoint = "OpenServiceW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "DeleteService", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteService(IntPtr hService);

        [DllImport("advapi32.dll", EntryPoint = "CloseServiceHandle", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseServiceHandle(IntPtr hSCObject);

        [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeServiceConfig2(IntPtr hService, uint dwInfoLevel, ref SERVICE_DESCRIPTION lpInfo);
        #endregion
        public delegate bool ServiceActionWithOutError(out string error);

        private static ServiceActionWithOutError _startServiceOverride = null;
        private static ServiceActionWithOutError _stopServiceOverride = null;
        private static ServiceActionWithOutError _restartServiceOverride = null;
        private static Func<bool> _isServiceInstalledOverride = null;
        private static Func<bool> _isServiceRunningOverride = null;
        private static Func<string, string, int, IpcResponse> _sendIpcRequestOverride = null;

        public static ServiceActionWithOutError StartServiceOverride
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _startServiceOverride : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _startServiceOverride = null; return; } _startServiceOverride = value; }
        }

        public static ServiceActionWithOutError StopServiceOverride
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _stopServiceOverride : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _stopServiceOverride = null; return; } _stopServiceOverride = value; }
        }

        public static ServiceActionWithOutError RestartServiceOverride
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _restartServiceOverride : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _restartServiceOverride = null; return; } _restartServiceOverride = value; }
        }

        public static Func<bool> IsServiceInstalledOverride
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _isServiceInstalledOverride : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _isServiceInstalledOverride = null; return; } _isServiceInstalledOverride = value; }
        }

        public static Func<bool> IsServiceRunningOverride
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _isServiceRunningOverride : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _isServiceRunningOverride = null; return; } _isServiceRunningOverride = value; }
        }

        public static Func<string, string, int, IpcResponse> SendIpcRequestOverride
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _sendIpcRequestOverride : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _sendIpcRequestOverride = null; return; } _sendIpcRequestOverride = value; }
        }

        public static void ResetSeams()
        {
            _startServiceOverride = null;
            _stopServiceOverride = null;
            _restartServiceOverride = null;
            _isServiceInstalledOverride = null;
            _isServiceRunningOverride = null;
            _sendIpcRequestOverride = null;
        }

        public static bool IsServiceInstalled()
        {
            if (IsServiceInstalledOverride != null) return IsServiceInstalledOverride();
            try
            {
                using (var sc = new ServiceController(UsbShieldService.ServiceNameId))
                {
                    var status = sc.Status;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool IsServiceRunning()
        {
            if (IsServiceRunningOverride != null) return IsServiceRunningOverride();
            try
            {
                using (var sc = new ServiceController(UsbShieldService.ServiceNameId))
                {
                    return sc.Status == ServiceControllerStatus.Running;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool InstallService(out string error)
        {
            error = null;
            try
            {
                // Invariant: Do NOT copy executable to Program Files or ProgramData.
                // Invariant: Register service pointing directly to current running executable.
                // Invariant: Zero subprocesses — native advapi32.dll Win32 Service APIs.
                string currentExePath = Application.ExecutablePath.Trim('"');
                string binPath = string.Format("\"{0}\" --service", currentExePath);
                string displayName = "USB Shield Pro Background Protection Agent";
                string description = UsbShieldPro.Loc.Get("ServiceDescription");

                IntPtr hSCM = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
                if (hSCM == IntPtr.Zero)
                {
                    int win32Err = Marshal.GetLastWin32Error();
                    error = string.Format("Failed to open Service Control Manager (Win32 Error: {0})", win32Err);
                    return false;
                }

                try
                {
                    // Check if already installed; if so, delete existing first
                    IntPtr hExisting = OpenService(hSCM, UsbShieldService.ServiceNameId, SERVICE_ALL_ACCESS | DELETE);
                    if (hExisting != IntPtr.Zero)
                    {
                        DeleteService(hExisting);
                        CloseServiceHandle(hExisting);
                    }

                    IntPtr hService = CreateService(
                        hSCM,
                        UsbShieldService.ServiceNameId,
                        displayName,
                        SERVICE_ALL_ACCESS,
                        SERVICE_WIN32_OWN_PROCESS,
                        SERVICE_AUTO_START,
                        SERVICE_ERROR_NORMAL,
                        binPath,
                        null,
                        IntPtr.Zero,
                        null,
                        null,
                        null);

                    if (hService == IntPtr.Zero)
                    {
                        int win32Err = Marshal.GetLastWin32Error();
                        error = string.Format("Failed to create Windows service (Win32 Error: {0})", win32Err);
                        return false;
                    }

                    try
                    {
                        SERVICE_DESCRIPTION desc = new SERVICE_DESCRIPTION { lpDescription = description };
                        ChangeServiceConfig2(hService, SERVICE_CONFIG_DESCRIPTION, ref desc);
                    }
                    catch { }
                    finally
                    {
                        CloseServiceHandle(hService);
                    }

                    // Start service through coordinated StartService
                    return StartService(out error);
                }
                finally
                {
                    CloseServiceHandle(hSCM);
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool UninstallService(out string error)
        {
            error = null;
            try
            {
                // Stop service through coordinated StopService
                string stopErr;
                StopService(out stopErr);

                IntPtr hSCM = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
                if (hSCM == IntPtr.Zero)
                {
                    int win32Err = Marshal.GetLastWin32Error();
                    error = string.Format("Failed to open Service Control Manager (Win32 Error: {0})", win32Err);
                    return false;
                }

                try
                {
                    IntPtr hService = OpenService(hSCM, UsbShieldService.ServiceNameId, SERVICE_ALL_ACCESS | DELETE);
                    if (hService == IntPtr.Zero)
                    {
                        int win32Err = Marshal.GetLastWin32Error();
                        // 1060 = ERROR_SERVICE_DOES_NOT_EXIST
                        if (win32Err == 1060) return true;
                        error = string.Format("Failed to open Windows service for deletion (Win32 Error: {0})", win32Err);
                        return false;
                    }

                    try
                    {
                        if (!DeleteService(hService))
                        {
                            int win32Err = Marshal.GetLastWin32Error();
                            // 1072 = ERROR_SERVICE_MARKED_FOR_DELETE
                            if (win32Err != 1072)
                            {
                                error = string.Format("Failed to delete Windows service (Win32 Error: {0})", win32Err);
                                return false;
                            }
                        }
                        return true;
                    }
                    finally
                    {
                        CloseServiceHandle(hService);
                    }
                }
                finally
                {
                    CloseServiceHandle(hSCM);
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool StartService(out string error)
        {
            if (StartServiceOverride != null) return StartServiceOverride(out error);
            error = null;
            try
            {
                if (!EnforcementOwnerResolver.PrepareForServiceStart(3000))
                {
                    error = "Failed to drain active GUI workers before service start.";
                    return false;
                }

                using (var sc = new ServiceController(UsbShieldService.ServiceNameId))
                {
                    if (sc.Status != ServiceControllerStatus.Running)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    }
                }
                EnforcementOwnerResolver.ResetCache();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool StopService(out string error)
        {
            if (StopServiceOverride != null) return StopServiceOverride(out error);
            error = null;
            try
            {
                using (var sc = new ServiceController(UsbShieldService.ServiceNameId))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                    }
                }
                EnforcementOwnerResolver.HandleServiceStopped();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool RestartService(out string error)
        {
            if (RestartServiceOverride != null) return RestartServiceOverride(out error);
            error = null;
            try
            {
                using (var sc = new ServiceController(UsbShieldService.ServiceNameId))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                    }
                }
                EnforcementOwnerResolver.HandleServiceStopped();

                if (!EnforcementOwnerResolver.PrepareForServiceStart(3000))
                {
                    error = "Failed to drain active GUI workers before service restart.";
                    return false;
                }

                using (var sc = new ServiceController(UsbShieldService.ServiceNameId))
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                }
                EnforcementOwnerResolver.ResetCache();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static IpcResponse SendIpcRequest(string command, string payload = null, int timeoutMs = 3000)
        {
            if (SendIpcRequestOverride != null)
            {
                return SendIpcRequestOverride(command, payload, timeoutMs);
            }

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            try
            {
                using (var pipe = new NamedPipeClientStream(".", UsbShieldService.PipeName, PipeDirection.InOut))
                {
                    pipe.Connect(timeoutMs);

                    using (StreamReader reader = new StreamReader(pipe, Encoding.UTF8))
                    using (StreamWriter writer = new StreamWriter(pipe, Encoding.UTF8))
                    {
                        IpcRequest req = new IpcRequest { Command = command, Payload = payload };
                        writer.WriteLine(serializer.Serialize(req));
                        writer.Flush();

                        string responseLine = reader.ReadLine();
                        if (!string.IsNullOrEmpty(responseLine))
                        {
                            return serializer.Deserialize<IpcResponse>(responseLine);
                        }
                    }
                }
            }
            catch (System.TimeoutException)
            {
                return new IpcResponse { Success = false, ErrorMessage = "IPC Connection timed out after " + timeoutMs + "ms." };
            }
            catch (Exception ex)
            {
                return new IpcResponse { Success = false, ErrorMessage = "IPC Connection failed: " + ex.Message };
            }

            return new IpcResponse { Success = false, ErrorMessage = "No response from service." };
        }

        public static System.Threading.Tasks.Task<IpcResponse> SendIpcRequestAsync(string command, string payload = null, int timeoutMs = 3000)
        {
            return System.Threading.Tasks.Task.Run(() => SendIpcRequest(command, payload, timeoutMs));
        }

        public static System.Threading.Tasks.Task<PolicySyncResult> TrySynchronizePolicyAsync(UsbShieldPro.Models.ProtectionPolicy policy, int timeoutMs = 3500)
        {
            return System.Threading.Tasks.Task.Run(() =>
            {
                string err;
                bool ack;
                bool ok = TrySynchronizePolicy(policy, out err, out ack);
                return new PolicySyncResult { Success = ok, ErrorMessage = err, ServiceAcknowledged = ack };
            });
        }

        public static ServiceHealthReport CheckServiceHealth(int timeoutMs = 1500)
        {
            try
            {
                if (!IsServiceRunning())
                {
                    return new ServiceHealthReport
                    {
                        IsHealthy = false,
                        IsSentinelAlive = false,
                        StatusMessage = "Service is not running."
                    };
                }

                IpcResponse resp = SendIpcRequest("GET_HEALTH", null, timeoutMs);
                if (resp != null && resp.Success && !string.IsNullOrEmpty(resp.Data))
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    ServiceHealthReport report = serializer.Deserialize<ServiceHealthReport>(resp.Data);
                    if (report != null) return report;
                }

                return new ServiceHealthReport
                {
                    IsHealthy = false,
                    IsSentinelAlive = false,
                    StatusMessage = (resp != null && !string.IsNullOrEmpty(resp.ErrorMessage)) ? resp.ErrorMessage : "Service IPC unresponsive."
                };
            }
            catch (Exception ex)
            {
                return new ServiceHealthReport
                {
                    IsHealthy = false,
                    IsSentinelAlive = false,
                    StatusMessage = "Service health check failed: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Synchronizes a protection policy with the Windows Service if running.
        /// If the service is running, waits for explicit service acknowledgement.
        /// If the service is not running, succeeds with serviceAcknowledged = false.
        /// </summary>
        public static bool TrySynchronizePolicy(UsbShieldPro.Models.ProtectionPolicy policy, out string error, out bool serviceAcknowledged)
        {
            error = null;
            serviceAcknowledged = false;

            if (policy == null)
            {
                error = "Policy cannot be null.";
                return false;
            }

            if (!IsServiceRunning())
            {
                // Portable mode or service stopped: GUI fallback is authoritative
                return true;
            }

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string payload = serializer.Serialize(policy);
            IpcResponse resp = SendIpcRequest("APPLYPOLICY", payload, 3500);

            if (resp == null)
            {
                error = "Service IPC returned null response.";
                return false;
            }

            if (!resp.Success)
            {
                error = !string.IsNullOrEmpty(resp.ErrorMessage) ? resp.ErrorMessage : "Service rejected policy update.";
                return false;
            }

            serviceAcknowledged = true;
            return true;
        }

        public static bool TrySynchronizePolicy(UsbShieldPro.Models.ProtectionPolicy policy, out string error)
        {
            bool ack;
            return TrySynchronizePolicy(policy, out error, out ack);
        }

        public static bool StartTemporaryAccess(int minutes, UsbShieldPro.Models.ProtectionPolicy currentPolicy, out string error)
        {
            error = null;
            if (EnforcementOwnerResolver.IsServiceAuthoritative())
            {
                IpcResponse resp = SendIpcRequest("SETTEMPORARYACCESS", minutes.ToString(), 5000);
                if (resp != null && resp.Success) return true;
                error = resp != null ? resp.ErrorMessage : "Service unresponsive for temporary access.";
                return false;
            }

            return UsbShieldPro.Core.TemporaryAccessManager.StartTemporaryAccess(minutes, currentPolicy, out error);
        }

        public static bool CancelTemporaryAccess(out string error)
        {
            error = null;
            if (EnforcementOwnerResolver.IsServiceAuthoritative())
            {
                IpcResponse resp = SendIpcRequest("CANCELTEMPORARYACCESS", null, 5000);
                if (resp != null && resp.Success) return true;
                error = resp != null ? resp.ErrorMessage : "Service unresponsive for cancel temporary access.";
                return false;
            }

            return UsbShieldPro.Core.TemporaryAccessManager.CancelTemporaryAccess(out error);
        }
    }

    public class PolicySyncResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public bool ServiceAcknowledged { get; set; }
    }
}
