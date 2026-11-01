// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Storage Device Interface Notification & Wake Manager
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Runtime.InteropServices;
using System.Threading;
using UsbShieldPro.Core;
using UsbShieldPro.Models;

namespace UsbShieldPro.Devices
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEV_BROADCAST_HDR
    {
        public int dbch_size;
        public int dbch_devicetype;
        public int dbch_reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct DEV_BROADCAST_DEVICEINTERFACE
    {
        public int dbcc_size;
        public int dbcc_devicetype;
        public int dbcc_reserved;
        public Guid dbcc_classguid;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public char[] dbcc_name;
    }

    public class StorageDeviceChangeInfo
    {
        public int EventType { get; set; }
        public int DeviceType { get; set; }
        public Guid ClassGuid { get; set; }
        public bool IsRelevantStorageArrival { get; set; }
        public DateTime TimestampUtc { get; set; }

        public StorageDeviceChangeInfo()
        {
            DeviceType = -1;
            ClassGuid = Guid.Empty;
            TimestampUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Manages window-based RegisterDeviceNotification subscriptions for disk and volume interfaces.
    /// Provides event coalescing with a 250ms settle window to prevent partial multi-volume ejections.
    /// Storage notifications serve strictly as asynchronous wake signals; no device identity or trust
    /// is derived from notification paths.
    /// </summary>
    public static class StorageNotificationManager
    {
        // ── WIN32 CONSTANTS ──
        public const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

        public const int DBT_DEVICEARRIVAL = 0x8000;
        public const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        public const int DBT_DEVNODES_CHANGED = 0x0007;

        public const int DBT_DEVTYP_OEM = 0x00000000;
        public const int DBT_DEVTYP_DEVNODE = 0x00000001;
        public const int DBT_DEVTYP_VOLUME = 0x00000002;
        public const int DBT_DEVTYP_PORT = 0x00000003;
        public const int DBT_DEVTYP_NET = 0x00000004;
        public const int DBT_DEVTYP_DEVICEINTERFACE = 0x00000005;
        public const int DBT_DEVTYP_HANDLE = 0x00000006;

        public static readonly Guid GUID_DEVINTERFACE_DISK = new Guid("53f56307-b6bf-11d0-94f2-00a0c91efb8b");
        public static readonly Guid GUID_DEVINTERFACE_VOLUME = new Guid("53f5630d-b6bf-11d0-94f2-00a0c91efb8b");

        // ── P/INVOKE SIGNATURES ──
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient, IntPtr NotificationFilter, uint Flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterDeviceNotification(IntPtr Handle);

        // ── REGISTRATION STATE ──
        private static IntPtr _hDevNotifyDisk = IntPtr.Zero;
        private static IntPtr _hDevNotifyVolume = IntPtr.Zero;
        private static readonly object _regLock = new object();

        // ── DEBOUNCE / SETTLE WINDOW STATE ──
        private static Timer _settleTimer = null;
        private static readonly object _timerLock = new object();
        private static int _settleIntervalMs = 250;
        private static Action _wakeAction = null;

        // ── OBSERVABILITY & DIAGNOSTICS COUNTERS ──
        public static int SettleTimerArmedCount = 0;
        public static int SettleTimerRearmedCount = 0;
        public static int CoalescedEventCount = 0;
        public static int SettleFiredCount = 0;

        // ── TEST SEAMS (Guarded: strictly isolated by IsTestMode) ──
        private static Func<IntPtr, Guid, IntPtr> _registerDeviceNotificationSeam = null;
        public static Func<IntPtr, Guid, IntPtr> RegisterDeviceNotificationSeam
        {
            get
            {
                if (!DeviceEnforcementLifecycleManager.IsTestMode) return null;
                return _registerDeviceNotificationSeam;
            }
            set
            {
                if (!DeviceEnforcementLifecycleManager.IsTestMode)
                {
                    _registerDeviceNotificationSeam = null;
                    return;
                }
                _registerDeviceNotificationSeam = value;
            }
        }

        private static Func<IntPtr, bool> _unregisterDeviceNotificationSeam = null;
        public static Func<IntPtr, bool> UnregisterDeviceNotificationSeam
        {
            get
            {
                if (!DeviceEnforcementLifecycleManager.IsTestMode) return null;
                return _unregisterDeviceNotificationSeam;
            }
            set
            {
                if (!DeviceEnforcementLifecycleManager.IsTestMode)
                {
                    _unregisterDeviceNotificationSeam = null;
                    return;
                }
                _unregisterDeviceNotificationSeam = value;
            }
        }
        public static Action<string> TraceLoggerSeam = null;
        public static int StorageArrivalEventGeneration = 0;

        public static int SettleIntervalMs
        {
            get { return _settleIntervalMs; }
            set { _settleIntervalMs = value; }
        }

        public static bool IsRegistered
        {
            get
            {
                lock (_regLock)
                {
                    return _hDevNotifyDisk != IntPtr.Zero && _hDevNotifyVolume != IntPtr.Zero;
                }
            }
        }

        public static int ActiveRegistrationCount
        {
            get
            {
                lock (_regLock)
                {
                    int c = 0;
                    if (_hDevNotifyDisk != IntPtr.Zero) c++;
                    if (_hDevNotifyVolume != IntPtr.Zero) c++;
                    return c;
                }
            }
        }

        public static IntPtr DiskRegistrationHandle
        {
            get { lock (_regLock) return _hDevNotifyDisk; }
        }

        public static IntPtr VolumeRegistrationHandle
        {
            get { lock (_regLock) return _hDevNotifyVolume; }
        }

        public static bool IsSettleTimerActive
        {
            get { lock (_timerLock) return _settleTimer != null; }
        }

        /// <summary>
        /// Registers for GUID_DEVINTERFACE_DISK and GUID_DEVINTERFACE_VOLUME notifications.
        /// Prevents duplicate registrations idempotently.
        /// </summary>
        public static bool Register(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;

            lock (_regLock)
            {
                if (_hDevNotifyDisk != IntPtr.Zero && _hDevNotifyVolume != IntPtr.Zero)
                {
                    return false; // Already registered; duplicate registration prevented
                }

                _hDevNotifyDisk = DoRegisterInterface(hWnd, GUID_DEVINTERFACE_DISK);
                _hDevNotifyVolume = DoRegisterInterface(hWnd, GUID_DEVINTERFACE_VOLUME);

                LogTrace(string.Format("[NOTIFICATION_REG] Registered GUID_DEVINTERFACE_DISK (h={0}) and GUID_DEVINTERFACE_VOLUME (h={1})",
                    _hDevNotifyDisk, _hDevNotifyVolume));

                return (_hDevNotifyDisk != IntPtr.Zero && _hDevNotifyVolume != IntPtr.Zero);
            }
        }

        /// <summary>
        /// Deterministically unregisters notification handles and disposes pending timers.
        /// </summary>
        public static void Unregister()
        {
            lock (_regLock)
            {
                if (_hDevNotifyDisk != IntPtr.Zero)
                {
                    DoUnregister(_hDevNotifyDisk);
                    _hDevNotifyDisk = IntPtr.Zero;
                }
                if (_hDevNotifyVolume != IntPtr.Zero)
                {
                    DoUnregister(_hDevNotifyVolume);
                    _hDevNotifyVolume = IntPtr.Zero;
                }
                LogTrace("[NOTIFICATION_REG] Unregistered storage device interface notifications cleanly.");
            }

            CancelSettleTimer();
        }

        private static IntPtr DoRegisterInterface(IntPtr hWnd, Guid classGuid)
        {
            if (DeviceEnforcementLifecycleManager.IsTestMode && RegisterDeviceNotificationSeam != null)
            {
                return RegisterDeviceNotificationSeam(hWnd, classGuid);
            }

            DEV_BROADCAST_DEVICEINTERFACE dbi = new DEV_BROADCAST_DEVICEINTERFACE();
            dbi.dbcc_size = Marshal.SizeOf(typeof(DEV_BROADCAST_DEVICEINTERFACE));
            dbi.dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE;
            dbi.dbcc_reserved = 0;
            dbi.dbcc_classguid = classGuid;
            dbi.dbcc_name = new char[1];

            IntPtr buffer = Marshal.AllocHGlobal(dbi.dbcc_size);
            try
            {
                Marshal.StructureToPtr(dbi, buffer, false);
                return RegisterDeviceNotification(hWnd, buffer, DEVICE_NOTIFY_WINDOW_HANDLE);
            }
            catch (Exception ex)
            {
                LogTrace("[NOTIFICATION_REG_ERROR] Failed to register " + classGuid + ": " + ex.Message);
                return IntPtr.Zero;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static bool DoUnregister(IntPtr hNotify)
        {
            if (DeviceEnforcementLifecycleManager.IsTestMode && UnregisterDeviceNotificationSeam != null)
            {
                return UnregisterDeviceNotificationSeam(hNotify);
            }

            try
            {
                return UnregisterDeviceNotification(hNotify);
            }
            catch (Exception ex)
            {
                LogTrace("[NOTIFICATION_UNREG_ERROR] Failed to unregister " + hNotify + ": " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Arms or rearms the settle timer (250ms debounce window).
        /// If an event arrives while a settle timer is pending, the timer is rearmed to ensure
        /// all sibling volumes (such as F: and G:) finish mounting before discovery begins.
        /// </summary>
        public static bool ArmOrRearmSettleTimer(Action wakeAction)
        {
            bool wasRearmed = false;
            lock (_timerLock)
            {
                _wakeAction = wakeAction;
                if (_settleTimer != null)
                {
                    try
                    {
                        _settleTimer.Change(_settleIntervalMs, Timeout.Infinite);
                        SettleTimerRearmedCount++;
                        CoalescedEventCount++;
                        wasRearmed = true;
                    }
                    catch
                    {
                        _settleTimer = new Timer(OnSettleTimerFired, null, _settleIntervalMs, Timeout.Infinite);
                        SettleTimerArmedCount++;
                    }
                }
                else
                {
                    _settleTimer = new Timer(OnSettleTimerFired, null, _settleIntervalMs, Timeout.Infinite);
                    SettleTimerArmedCount++;
                }
            }
            return wasRearmed;
        }

        public static void CancelSettleTimer()
        {
            lock (_timerLock)
            {
                if (_settleTimer != null)
                {
                    try { _settleTimer.Dispose(); } catch { }
                    _settleTimer = null;
                }
                _wakeAction = null;
            }
        }

        private static void OnSettleTimerFired(object state)
        {
            Action action = null;
            lock (_timerLock)
            {
                if (_settleTimer != null)
                {
                    try { _settleTimer.Dispose(); } catch { }
                    _settleTimer = null;
                }
                action = _wakeAction;
                _wakeAction = null;
            }

            if (action != null)
            {
                Interlocked.Increment(ref SettleFiredCount);
                LogTrace(string.Format("[SETTLE_GATE_FIRED] Settle window of {0} ms elapsed. Dispatching authoritative wake.", _settleIntervalMs));

                // Dispatch to thread pool to avoid blocking the timer thread
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        LogTrace("[SETTLE_GATE_ERROR] Wake action threw: " + ex.Message);
                    }
                });
            }
        }

        private static Timer _topologyTimer = null;
        private static readonly object _topologyTimerLock = new object();
        public static int TopologyConfirmationIntervalMs = 250;
        public static Action TopologyWakeAction = null;

        public static void ArmTopologyConfirmationTimer(Action wakeAction = null)
        {
            lock (_topologyTimerLock)
            {
                if (_topologyTimer != null)
                {
                    try { _topologyTimer.Dispose(); } catch { }
                    _topologyTimer = null;
                }
                Action targetAction = wakeAction ?? TopologyWakeAction ?? _wakeAction;
                _topologyTimer = new Timer(OnTopologyTimerFired, targetAction, TopologyConfirmationIntervalMs, Timeout.Infinite);
            }
        }

        public static void CancelTopologyConfirmationTimer()
        {
            lock (_topologyTimerLock)
            {
                if (_topologyTimer != null)
                {
                    try { _topologyTimer.Dispose(); } catch { }
                    _topologyTimer = null;
                }
            }
        }

        private static void OnTopologyTimerFired(object state)
        {
            CancelTopologyConfirmationTimer();
            Action action = state as Action ?? TopologyWakeAction ?? _wakeAction;
            if (action != null)
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        LogTrace("[TOPOLOGY_CONFIRMATION_FIRED] Confirmation interval elapsed. Initiating fresh authoritative discovery.");
                        action();
                    }
                    catch (Exception ex)
                    {
                        LogTrace("[TOPOLOGY_CONFIRMATION_ERROR] Action threw: " + ex.Message);
                    }
                });
            }
        }

        public static void IncrementArrivalEventGeneration()
        {
            Interlocked.Increment(ref StorageArrivalEventGeneration);
            ActiveSentinel.InvalidatePendingTopologySnapshots();
            CancelTopologyConfirmationTimer();
        }

        /// <summary>
        /// Parses incoming WM_DEVICECHANGE message and LParam safely to identify device types and interface GUIDs.
        /// Extracts wake signals ONLY without attempting to infer identity, serial, or trust from notification paths.
        /// </summary>
        public static StorageDeviceChangeInfo ParseDeviceChange(int eventType, IntPtr lParam)
        {
            var info = new StorageDeviceChangeInfo
            {
                EventType = eventType,
                DeviceType = -1,
                ClassGuid = Guid.Empty,
                IsRelevantStorageArrival = false,
                TimestampUtc = DateTime.UtcNow
            };

            if (lParam == IntPtr.Zero)
            {
                return info;
            }

            try
            {
                int size = Marshal.ReadInt32(lParam, 0);
                if (size >= 12)
                {
                    info.DeviceType = Marshal.ReadInt32(lParam, 4);

                    if (info.DeviceType == DBT_DEVTYP_DEVICEINTERFACE && size >= 28)
                    {
                        byte[] guidBytes = new byte[16];
                        Marshal.Copy(new IntPtr(lParam.ToInt64() + 12), guidBytes, 0, 16);
                        info.ClassGuid = new Guid(guidBytes);

                        if (info.ClassGuid == GUID_DEVINTERFACE_DISK || info.ClassGuid == GUID_DEVINTERFACE_VOLUME)
                        {
                            info.IsRelevantStorageArrival = (eventType == DBT_DEVICEARRIVAL);
                        }
                    }
                    else if (info.DeviceType == DBT_DEVTYP_VOLUME)
                    {
                        info.ClassGuid = GUID_DEVINTERFACE_VOLUME;
                        info.IsRelevantStorageArrival = (eventType == DBT_DEVICEARRIVAL);
                    }
                }
            }
            catch { }

            return info;
        }

        public enum DeviceChangeDispatchRoute
        {
            SettleDebounceWake,
            ImmediateGenericArrival,
            DeviceRemoval,
            VisualRefreshOnly,
            Ignored
        }

        public static DeviceChangeDispatchRoute RouteDeviceChange(int eventType, IntPtr lParam)
        {
            var parseResult = ParseDeviceChange(eventType, lParam);
            if (eventType == DBT_DEVICEARRIVAL)
            {
                if (parseResult.IsRelevantStorageArrival)
                {
                    Interlocked.Increment(ref StorageArrivalEventGeneration);
                    ActiveSentinel.InvalidatePendingTopologySnapshots();
                    CancelTopologyConfirmationTimer();
                    return DeviceChangeDispatchRoute.SettleDebounceWake;
                }
                return DeviceChangeDispatchRoute.ImmediateGenericArrival;
            }
            if (eventType == DBT_DEVICEREMOVECOMPLETE)
            {
                CancelTopologyConfirmationTimer();
                return DeviceChangeDispatchRoute.DeviceRemoval;
            }
            if (eventType == DBT_DEVNODES_CHANGED)
            {
                return DeviceChangeDispatchRoute.VisualRefreshOnly;
            }
            return DeviceChangeDispatchRoute.Ignored;
        }

        /// <summary>
        /// Logs production-safe diagnostic telemetry for incoming WM_DEVICECHANGE events.
        /// </summary>
        public static void LogRawDeviceChange(int eventType, int devType, Guid classGuid, bool coalesced, string actionTaken)
        {
            string eventName;
            switch (eventType)
            {
                case DBT_DEVICEARRIVAL: eventName = "DBT_DEVICEARRIVAL"; break;
                case DBT_DEVICEREMOVECOMPLETE: eventName = "DBT_DEVICEREMOVECOMPLETE"; break;
                case DBT_DEVNODES_CHANGED: eventName = "DBT_DEVNODES_CHANGED"; break;
                default: eventName = "0x" + eventType.ToString("X4"); break;
            }

            string devTypeName;
            switch (devType)
            {
                case DBT_DEVTYP_DEVICEINTERFACE: devTypeName = "DEVICEINTERFACE"; break;
                case DBT_DEVTYP_VOLUME: devTypeName = "VOLUME"; break;
                case DBT_DEVTYP_OEM: devTypeName = "OEM"; break;
                case DBT_DEVTYP_DEVNODE: devTypeName = "DEVNODE"; break;
                case DBT_DEVTYP_PORT: devTypeName = "PORT"; break;
                case DBT_DEVTYP_HANDLE: devTypeName = "HANDLE"; break;
                default: devTypeName = devType >= 0 ? devType.ToString() : "None"; break;
            }

            string guidStr = (classGuid != Guid.Empty) ? "{" + classGuid.ToString() + "}" : "None";
            string timeUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            string line = string.Format("[RAW_WM_DEVICECHANGE] Time: {0}, Event: {1} (0x{2:X4}), DevType: {3} ({4}), ClassGuid: {5}, Coalesced: {6}, Action: {7}",
                timeUtc, eventName, eventType, devTypeName, devType, guidStr, coalesced ? "True" : "False", actionTaken ?? "None");

            LogTrace(line);
        }

        private static void LogTrace(string message)
        {
            if (DeviceEnforcementLifecycleManager.IsTestMode && TraceLoggerSeam != null)
            {
                TraceLoggerSeam(message);
                return;
            }
            ActiveSentinel.LogTrace(message);
        }

        public static void ResetForTest()
        {
            CancelSettleTimer();
            CancelTopologyConfirmationTimer();
            lock (_regLock)
            {
                _hDevNotifyDisk = IntPtr.Zero;
                _hDevNotifyVolume = IntPtr.Zero;
            }
            SettleTimerArmedCount = 0;
            SettleTimerRearmedCount = 0;
            CoalescedEventCount = 0;
            SettleFiredCount = 0;
            StorageArrivalEventGeneration = 0;
            _registerDeviceNotificationSeam = null;
            _unregisterDeviceNotificationSeam = null;
            TraceLoggerSeam = null;
            _settleIntervalMs = 250;
            TopologyConfirmationIntervalMs = 250;
            TopologyWakeAction = null;
        }
    }
}
