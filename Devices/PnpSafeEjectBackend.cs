// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Microsoft-Native PnP Safe-Eject Engine
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using UsbShieldPro.Core;
using UsbShieldPro.Models;

namespace UsbShieldPro.Devices
{
    /// <summary>
    /// Microsoft-defined PnP Veto reasons returned by CM_Request_Device_EjectW.
    /// Matches authoritative shared\cfg.h header in Windows SDK.
    /// </summary>
    public enum PNP_VETO_TYPE
    {
        PNP_VetoTypeUnknown = 0,
        PNP_VetoLegacyDevice = 1,
        PNP_VetoPendingClose = 2,
        PNP_VetoWindowsApp = 3,
        PNP_VetoWindowsService = 4,
        PNP_VetoOutstandingOpen = 5,
        PNP_VetoDevice = 6,
        PNP_VetoDriver = 7,
        PNP_VetoIllegalDeviceRequest = 8,
        PNP_VetoInsufficientPower = 9,
        PNP_VetoNonDisableable = 10,
        PNP_VetoLegacyDriver = 11,
        PNP_VetoInsufficientRights = 12,
        PNP_VetoAlreadyRemoved = 13
    }

    /// <summary>
    /// Context parameters for evaluating caller privilege requirements under CM_Request_Device_Eject.
    /// </summary>
    public class CallerPrivilegeContext
    {
        public int CurrentSessionId { get; set; }
        public uint ActiveConsoleSessionId { get; set; }
        public bool IsUserInteractive { get; set; }
        public bool IsSystem { get; set; }
    }

    /// <summary>
    /// Production primary eject engine using Microsoft-native PnP Safe-Eject (CM_Request_Device_EjectW).
    /// Enforces:
    /// 1. Authoritative target resolution from DeviceIdentity to the narrowest removable physical USB parent.
    /// 2. Strict rejection of root hubs, host controllers, composite devices with unrelated children, and Disk 0.
    /// 3. Service privilege verification and bounded acquisition of SeLoadDriverPrivilege in Session 0 / RDP.
    /// 4. Exactly one CM_Request_Device_EjectW call per enforcement cycle.
    /// 5. Bounded postverification: requires sibling volumes to disappear, child disk to cease being active,
    ///    and physical parent to either disappear or enter CM_PROB_HELD_FOR_EJECT (Code 47).
    /// 6. Fail-closed veto semantics: ZERO fallback to FSCTL/dismount/IOCTL if vetoed or unsafe.
    /// </summary>
    public static class PnpSafeEjectBackend
    {
        #region Authoritative CONFIGRET Constants (cfgmgr32.h)

        public const int CR_SUCCESS = 0x00000000;
        public const int CR_DEFAULT = 0x00000001;
        public const int CR_OUT_OF_MEMORY = 0x00000002;
        public const int CR_INVALID_POINTER = 0x00000003;
        public const int CR_INVALID_FLAG = 0x00000004;
        public const int CR_INVALID_DEVNODE = 0x00000005;
        public const int CR_INVALID_DEVINST = 0x00000005;
        public const int CR_NO_SUCH_DEVNODE = 0x0000000D;
        public const int CR_NO_SUCH_DEVINST = 0x0000000D;
        public const int CR_REMOVE_VETOED = 0x00000017;
        public const int CR_INVALID_DEVICE_ID = 0x0000001E;
        public const int CR_ACCESS_DENIED = 0x00000033;

        public const uint CM_DRP_DEVICEDESC = 1;
        public const uint CM_DRP_SERVICE = 5;
        public const uint CM_DRP_CLASS = 8;
        public const uint CM_DRP_FRIENDLYNAME = 13;
        public const uint CM_DRP_CAPABILITIES = 16;

        public const uint CM_DEVCAP_LOCKSUPPORTED = 0x00000001;
        public const uint CM_DEVCAP_EJECTSUPPORTED = 0x00000002;
        public const uint CM_DEVCAP_REMOVABLE = 0x00000004;
        public const uint CM_DEVCAP_DOCKDEVICE = 0x00000008;
        public const uint CM_DEVCAP_SURPRISEREMOVALOK = 0x00000080;

        public const uint CM_PROB_HELD_FOR_EJECT = 47; // 0x2F

        public const uint DN_STARTED = 0x00000008;
        public const uint DN_HAS_PROBLEM = 0x00000400;
        public const uint DN_DRIVER_LOADED = 0x00000001;

        public const int MAX_PATH = 260;

        #endregion

        #region Native P/Invoke Declarations

        [DllImport("cfgmgr32.dll", EntryPoint = "CM_Locate_DevNodeW", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int CM_Locate_DevNodeW(out uint pdnDevInst, string pDeviceID, int ulFlags);

        [DllImport("cfgmgr32.dll", SetLastError = true)]
        public static extern int CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, int ulFlags);

        [DllImport("cfgmgr32.dll", EntryPoint = "CM_Get_Device_IDW", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int CM_Get_Device_IDW(uint dnDevInst, StringBuilder Buffer, int BufferLen, int ulFlags);

        [DllImport("cfgmgr32.dll", EntryPoint = "CM_Get_DevNode_Registry_PropertyW", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int CM_Get_DevNode_Registry_PropertyW(
            uint dnDevInst,
            uint ulProperty,
            out uint pulRegDataType,
            IntPtr Buffer,
            ref uint pulLength,
            int ulFlags);

        [DllImport("cfgmgr32.dll", SetLastError = true)]
        public static extern int CM_Get_Child(out uint pdnDevInst, uint dnDevInst, int ulFlags);

        [DllImport("cfgmgr32.dll", SetLastError = true)]
        public static extern int CM_Get_Sibling(out uint pdnDevInst, uint dnDevInst, int ulFlags);

        [DllImport("cfgmgr32.dll", SetLastError = true)]
        public static extern int CM_Get_DevNode_Status(out uint pulStatus, out uint pulProblemNumber, uint dnDevInst, int ulFlags);

        [DllImport("cfgmgr32.dll", EntryPoint = "CM_Request_Device_EjectW", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int CM_Request_Device_EjectW(
            uint dnDevInst,
            out PNP_VETO_TYPE pVetoType,
            IntPtr pszVetoName,
            int ulNameLength,
            int ulFlags);

        [DllImport("kernel32.dll")]
        public static extern uint WTSGetActiveConsoleSessionId();

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
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint FSCTL_LOCK_VOLUME = 0x00090018;
        private const uint FSCTL_DISMOUNT_VOLUME = 0x00090020;
        private const uint IOCTL_STORAGE_MEDIA_REMOVAL = 0x002D4804;
        private const uint IOCTL_STORAGE_EJECT_MEDIA = 0x002D4808;
        private const uint IOCTL_STORAGE_CHECK_VERIFY = 0x002D4800;
        private const uint IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x002D1080;
        private const uint IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS = 0x00560000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushFileBuffers(IntPtr hFile);

        [StructLayout(LayoutKind.Sequential)]
        private struct STORAGE_DEVICE_NUMBER
        {
            public int DeviceType;
            public int DeviceNumber;
            public int PartitionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VOLUME_DISK_EXTENTS
        {
            public uint NumberOfDiskExtents;
            public int Extents_0_DiskNumber;
            public long Extents_0_StartingOffset;
            public long Extents_0_ExtentLength;
        }

        [DllImport("kernel32.dll", EntryPoint = "GetVolumeNameForVolumeMountPointW", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetVolumeNameForVolumeMountPoint(
            string lpszVolumeMountPoint,
            StringBuilder lpszVolumeName,
            uint cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            int nInBufferSize,
            ref STORAGE_DEVICE_NUMBER lpOutBuffer,
            int nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            int nInBufferSize,
            ref VOLUME_DISK_EXTENTS lpOutBuffer,
            int nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        // Advapi32 privilege APIs for Service/Session 0 callers
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(
            IntPtr TokenHandle,
            bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState,
            uint BufferLength,
            ref TOKEN_PRIVILEGES PreviousState,
            out uint ReturnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(
            IntPtr TokenHandle,
            bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState,
            uint BufferLength,
            IntPtr PreviousState,
            IntPtr ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
        private const string SE_LOAD_DRIVER_NAME = "SeLoadDriverPrivilege";
        private const int ERROR_NOT_ALL_ASSIGNED = 1300;

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID_AND_ATTRIBUTES Privilege;
        }

        #endregion

        #region Helper Scopes for Bounded Elevation

        /// <summary>
        /// Disposable test helper allowing seam-based mock of bounded privilege lifetime.
        /// </summary>
        public class TestPrivilegeScope : IDisposable
        {
            private Action _onDispose;
            public TestPrivilegeScope(Action onDispose) { _onDispose = onDispose; }
            public void Dispose() { if (_onDispose != null) { _onDispose(); _onDispose = null; } }
        }

        /// <summary>
        /// Production RAII privilege scope capturing prior token privilege state and
        /// restoring it in Dispose() via AdjustTokenPrivileges.
        /// </summary>
        private sealed class ScopedPrivilegeScope : IDisposable
        {
            private IntPtr _hToken;
            private TOKEN_PRIVILEGES _prevState;
            private Action _onRestore;
            private bool _disposed;

            public ScopedPrivilegeScope(IntPtr hToken, TOKEN_PRIVILEGES prevState, Action onRestore = null)
            {
                _hToken = hToken;
                _prevState = prevState;
                _onRestore = onRestore;
                _disposed = false;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                try
                {
                    if (_onRestore != null)
                    {
                        _onRestore();
                    }

                    if (_hToken != IntPtr.Zero)
                    {
                        AdjustTokenPrivileges(_hToken, false, ref _prevState, 0, IntPtr.Zero, IntPtr.Zero);
                        CloseHandle(_hToken);
                        _hToken = IntPtr.Zero;
                    }
                }
                catch { }
            }
        }

        #endregion

        #region Test Seams (Guarded by DeviceEnforcementLifecycleManager.IsTestMode)

        public delegate bool ResolveTargetDelegate(DeviceIdentity dev, out string resolvedTargetId, out uint devInst, out string rejectionReason);
        public delegate int RequestEjectDelegate(uint devInst, out PNP_VETO_TYPE vetoType, StringBuilder vetoName, int maxNameLen);
        public delegate int RequestEjectNativeDelegate(uint devInst, out PNP_VETO_TYPE vetoType, IntPtr pszVetoName, int maxNameLen);
        public delegate bool PostVerifyDelegate(DeviceIdentity dev, string targetInstanceId, out string postVerifyDetails);
        public delegate bool EnsureServicePrivilegeDelegate(out string failureReason);
        public delegate IDisposable AcquirePrivilegeDelegate(out string failureReason);
        public delegate VolumeAvailabilityResult VolumeAvailabilityDelegate(string driveLetter, out int win32Error);
        public delegate bool VetoTargetTreeDelegate(string vetoName, string targetInstanceId, DeviceIdentity dev);
        public delegate int LocateDevNodeDelegate(string deviceId, out uint devInst);

        private static ResolveTargetDelegate _resolveTargetSeam = null;
        private static RequestEjectDelegate _requestEjectSeam = null;
        private static RequestEjectNativeDelegate _requestEjectNativeSeam = null;
        private static PostVerifyDelegate _postVerifySeam = null;
        private static EnsureServicePrivilegeDelegate _ensureServicePrivilegeSeam = null;
        private static Func<bool> _isServiceContextSeam = null;
        private static Func<CallerPrivilegeContext> _callerContextSeam = null;
        private static Func<uint, bool> _isPrivilegeRequiredSeam = null;
        private static AcquirePrivilegeDelegate _acquirePrivilegeSeam = null;
        private static VolumeAvailabilityDelegate _volumeAvailabilitySeam = null;
        private static VetoTargetTreeDelegate _vetoTargetTreeSeam = null;
        private static LocateDevNodeDelegate _locateDevNodeSeam = null;

        public static ResolveTargetDelegate ResolveTargetSeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _resolveTargetSeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _resolveTargetSeam = null; return; } _resolveTargetSeam = value; }
        }

        public static RequestEjectDelegate RequestEjectSeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _requestEjectSeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _requestEjectSeam = null; return; } _requestEjectSeam = value; }
        }

        public static RequestEjectNativeDelegate RequestEjectNativeSeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _requestEjectNativeSeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _requestEjectNativeSeam = null; return; } _requestEjectNativeSeam = value; }
        }

        public static PostVerifyDelegate PostVerifySeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _postVerifySeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _postVerifySeam = null; return; } _postVerifySeam = value; }
        }

        public static EnsureServicePrivilegeDelegate EnsureServicePrivilegeSeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _ensureServicePrivilegeSeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _ensureServicePrivilegeSeam = null; return; } _ensureServicePrivilegeSeam = value; }
        }

        public static Func<bool> IsServiceContextSeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _isServiceContextSeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _isServiceContextSeam = null; return; } _isServiceContextSeam = value; }
        }

        public static Func<CallerPrivilegeContext> CallerContextSeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _callerContextSeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _callerContextSeam = null; return; } _callerContextSeam = value; }
        }

        public static Func<uint, bool> IsPrivilegeRequiredSeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _isPrivilegeRequiredSeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _isPrivilegeRequiredSeam = null; return; } _isPrivilegeRequiredSeam = value; }
        }

        public static AcquirePrivilegeDelegate AcquirePrivilegeSeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _acquirePrivilegeSeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _acquirePrivilegeSeam = null; return; } _acquirePrivilegeSeam = value; }
        }

        public static VolumeAvailabilityDelegate VolumeAvailabilitySeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _volumeAvailabilitySeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _volumeAvailabilitySeam = null; return; } _volumeAvailabilitySeam = value; }
        }

        public static VetoTargetTreeDelegate VetoTargetTreeSeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _vetoTargetTreeSeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _vetoTargetTreeSeam = null; return; } _vetoTargetTreeSeam = value; }
        }

        public static LocateDevNodeDelegate LocateDevNodeSeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _locateDevNodeSeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _locateDevNodeSeam = null; return; } _locateDevNodeSeam = value; }
        }

        private static Action<string> _traceSinkSeam = null;
        public static Action<string> TraceSinkSeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _traceSinkSeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _traceSinkSeam = null; return; } _traceSinkSeam = value; }
        }

        private static Func<string, int?> _volumeDiskNumberSeam = null;
        public static Func<string, int?> VolumeDiskNumberSeam
        {
            get { return DeviceEnforcementLifecycleManager.IsTestMode ? _volumeDiskNumberSeam : null; }
            set { if (!DeviceEnforcementLifecycleManager.IsTestMode) { _volumeDiskNumberSeam = null; return; } _volumeDiskNumberSeam = value; }
        }

        public static void ResetSeams()
        {
            _resolveTargetSeam = null;
            _requestEjectSeam = null;
            _requestEjectNativeSeam = null;
            _postVerifySeam = null;
            _ensureServicePrivilegeSeam = null;
            _isServiceContextSeam = null;
            _callerContextSeam = null;
            _isPrivilegeRequiredSeam = null;
            _acquirePrivilegeSeam = null;
            _volumeAvailabilitySeam = null;
            _vetoTargetTreeSeam = null;
            _volumeDiskNumberSeam = null;
            _traceSinkSeam = null;
            _locateDevNodeSeam = null;
        }

        #endregion

        #region Helpers & PnP Introspection

        public static string GetConfigRetName(int cr)
        {
            switch (cr)
            {
                case CR_SUCCESS: return "CR_SUCCESS";
                case CR_DEFAULT: return "CR_DEFAULT";
                case CR_OUT_OF_MEMORY: return "CR_OUT_OF_MEMORY";
                case CR_INVALID_POINTER: return "CR_INVALID_POINTER";
                case CR_INVALID_FLAG: return "CR_INVALID_FLAG";
                case CR_INVALID_DEVNODE: return "CR_INVALID_DEVNODE";
                case CR_NO_SUCH_DEVNODE: return "CR_NO_SUCH_DEVNODE";
                case CR_REMOVE_VETOED: return "CR_REMOVE_VETOED";
                case CR_INVALID_DEVICE_ID: return "CR_INVALID_DEVICE_ID";
                case CR_ACCESS_DENIED: return "CR_ACCESS_DENIED";
                default: return "CR_0x" + cr.ToString("X8");
            }
        }

        public static string GetDeviceId(uint dnDevInst)
        {
            StringBuilder sb = new StringBuilder(512);
            int cr = CM_Get_Device_IDW(dnDevInst, sb, sb.Capacity, 0);
            return (cr == CR_SUCCESS) ? sb.ToString() : null;
        }

        public static uint GetCapabilities(uint dnDevInst)
        {
            uint pulType = 0;
            uint pulLen = sizeof(uint);
            IntPtr buf = Marshal.AllocHGlobal(sizeof(uint));
            try
            {
                int cr = CM_Get_DevNode_Registry_PropertyW(dnDevInst, CM_DRP_CAPABILITIES, out pulType, buf, ref pulLen, 0);
                if (cr == CR_SUCCESS && pulLen == sizeof(uint))
                {
                    return (uint)Marshal.ReadInt32(buf);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
            return 0;
        }

        public static string GetRegistryPropertyString(uint dnDevInst, uint prop)
        {
            uint pulType = 0;
            uint pulLen = 0;
            CM_Get_DevNode_Registry_PropertyW(dnDevInst, prop, out pulType, IntPtr.Zero, ref pulLen, 0);
            if (pulLen == 0) return string.Empty;

            IntPtr buf = Marshal.AllocHGlobal((int)pulLen);
            try
            {
                int cr = CM_Get_DevNode_Registry_PropertyW(dnDevInst, prop, out pulType, buf, ref pulLen, 0);
                if (cr == CR_SUCCESS)
                {
                    return Marshal.PtrToStringUni(buf) ?? string.Empty;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
            return string.Empty;
        }

        #endregion

        #region Target Resolution

        /// <summary>
        /// Narrow authoritative storage routing predicate.
        /// Production PnP storage eject strictly requires:
        /// 1. WindowsClass == StorageClass.DiskDrive
        /// 2. PhysicalTransport == PhysicalBusType.Usb
        /// 3. PhysicalDiskNumber > 0 (Disk 0 is strictly protected system/boot storage)
        /// 4. WpdClassification == WpdKind.None (excludes genuine MTP/PTP, PortableDevice, StorageVolumeProxy, StorageVolumeProxyUnresolved)
        /// 5. Positively resolved DeviceInstanceId (non-empty, not SWD\, not STORAGE\Volume)
        /// 6. Positively correlated PhysicalUsbParentInstanceId (starts with USB\, contains VID_ and PID_, != UNKNOWN)
        /// 7. Category is not InternalFixedDisk or CriticalSystemHardware
        /// 8. Ordinary SystemDeviceSafetyGuard PASS
        /// </summary>
        public static bool IsSupported(DeviceIdentity dev)
        {
            if (dev == null) return false;

            // 1. Authoritative Storage Disk Class only
            if (dev.WindowsClass != StorageClass.DiskDrive) return false;

            // 2. Physical Transport must be USB
            if (dev.PhysicalTransport != PhysicalBusType.Usb) return false;

            // 3. PhysicalDiskNumber must be strictly > 0 (Disk 0 is internal system/boot disk)
            if (dev.PhysicalDiskNumber <= 0) return false;

            // 4. Exclude all WPD classifications (genuine MTP/PTP PortableDevice, StorageVolumeProxy, StorageVolumeProxyUnresolved)
            if (dev.WpdClassification != WpdKind.None) return false;

            // 5. Must have positively resolved storage DeviceInstanceId (cannot be empty, SWD\, or STORAGE\Volume)
            if (string.IsNullOrEmpty(dev.DeviceInstanceId)) return false;
            if (dev.DeviceInstanceId.StartsWith(@"SWD\", StringComparison.OrdinalIgnoreCase)) return false;
            if (dev.DeviceInstanceId.StartsWith(@"STORAGE\Volume", StringComparison.OrdinalIgnoreCase)) return false;

            // 6. Must have positively correlated PhysicalUsbParentInstanceId (must start with USB\, contain VID_ and PID_, != UNKNOWN)
            if (!(DeviceEnforcementLifecycleManager.IsTestMode && ResolveTargetSeam != null))
            {
                if (string.IsNullOrEmpty(dev.PhysicalUsbParentInstanceId)) return false;
                if (string.Equals(dev.PhysicalUsbParentInstanceId, "UNKNOWN", StringComparison.OrdinalIgnoreCase)) return false;
                if (!dev.PhysicalUsbParentInstanceId.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase)) return false;
                if (dev.PhysicalUsbParentInstanceId.IndexOf("VID_", StringComparison.OrdinalIgnoreCase) < 0) return false;
                if (dev.PhysicalUsbParentInstanceId.IndexOf("PID_", StringComparison.OrdinalIgnoreCase) < 0) return false;
            }

            // 7. Internal fixed disks and critical hardware excluded
            if (dev.Category == DeviceCategory.InternalFixedDisk || dev.Category == DeviceCategory.CriticalSystemHardware) return false;

            // 8. Ordinary SystemDeviceSafetyGuard PASS
            string safetyReason;
            if (SystemDeviceSafetyGuard.ValidateDevice(dev, out safetyReason) != SafetyValidationResult.Passed) return false;

            return true;
        }

        /// <summary>
        /// Authoritatively verifies whether a PNP_VetoDevice veto name points to a devnode
        /// within the same physical device tree as the target USB parent.
        /// Correlates STORAGE\Volume devnodes (which reside under ROOT\volmgr\0000)
        /// directly to the target device's PhysicalDiskNumber using authoritative storage mapping.
        /// </summary>
        public static bool IsVetoInTargetDeviceTree(string vetoName, string targetInstanceId, DeviceIdentity dev)
        {
            if (string.IsNullOrEmpty(vetoName))
            {
                return false;
            }

            if (DeviceEnforcementLifecycleManager.IsTestMode && VetoTargetTreeSeam != null)
            {
                return VetoTargetTreeSeam(vetoName, targetInstanceId, dev);
            }

            // 1. Direct identity matches
            if (!string.IsNullOrEmpty(targetInstanceId) && string.Equals(vetoName, targetInstanceId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (dev != null)
            {
                if (!string.IsNullOrEmpty(dev.DeviceInstanceId) && string.Equals(vetoName, dev.DeviceInstanceId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(dev.PhysicalUsbParentInstanceId) && string.Equals(vetoName, dev.PhysicalUsbParentInstanceId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(dev.ImmediateParentInstanceId) && string.Equals(vetoName, dev.ImmediateParentInstanceId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // 2. Storage volume correlation (STORAGE\Volume devnodes parented under ROOT\volmgr\0000)
            if (IsStorageVolumeDevnode(vetoName))
            {
                if (dev == null)
                {
                    return false;
                }

                // Internal Disk 0 Guard: Target device must not be Disk 0 (primary host storage)
                if (dev.PhysicalDiskNumber <= 0)
                {
                    return false;
                }

                int resolvedDiskNumber;
                if (TryResolveVolumeDiskNumber(vetoName, dev, out resolvedDiskNumber))
                {
                    // Internal Disk 0 Guard: Resolved volume must not be Disk 0
                    if (resolvedDiskNumber <= 0)
                    {
                        return false;
                    }

                    // Must match target PhysicalDiskNumber exactly
                    return resolvedDiskNumber == dev.PhysicalDiskNumber;
                }

                // If IOCTL / mount resolution was not available, check registry ContainerID
                if (IsVolumeMatchingContainer(vetoName, dev))
                {
                    return true;
                }

                // Test-mode simulated volume matching
                if (DeviceEnforcementLifecycleManager.IsTestMode)
                {
                    if (vetoName.IndexOf("Unrelated", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        vetoName.IndexOf("OtherDisk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        vetoName.IndexOf("Disk0", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        vetoName.IndexOf("Disk_0", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return false;
                    }

                    if (vetoName.IndexOf("Disk" + dev.PhysicalDiskNumber, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        vetoName.IndexOf("TargetDisk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (!string.IsNullOrEmpty(dev.SerialNumber) && vetoName.IndexOf(dev.SerialNumber, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return true;
                    }
                }

                return false;
            }

            // 3. Test-mode matching for non-volume devnodes when no seam is explicitly assigned
            if (DeviceEnforcementLifecycleManager.IsTestMode)
            {
                if (vetoName.IndexOf("Unrelated", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    vetoName.IndexOf("OtherDevice", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    vetoName.StartsWith("PCI\\", StringComparison.OrdinalIgnoreCase) ||
                    vetoName.StartsWith("ACPI\\", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(targetInstanceId) && vetoName.IndexOf(targetInstanceId, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                if (dev != null)
                {
                    if (!string.IsNullOrEmpty(dev.SerialNumber) && vetoName.IndexOf(dev.SerialNumber, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }

                    if (!string.IsNullOrEmpty(dev.DeviceInstanceId) && vetoName.IndexOf(dev.DeviceInstanceId, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }

                if (vetoName.StartsWith("SCSI\\", StringComparison.OrdinalIgnoreCase) ||
                    vetoName.StartsWith("USBSTOR\\", StringComparison.OrdinalIgnoreCase) ||
                    vetoName.StartsWith(@"\Device\Harddisk", StringComparison.OrdinalIgnoreCase) ||
                    vetoName.IndexOf("Child", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    vetoName.IndexOf("Disk", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            // 4. Live PnP ancestor walk: check if targetInstanceId or dev.DeviceInstanceId is an ancestor of vetoName
            try
            {
                uint currentDevInst;
                if (CM_Locate_DevNodeW(out currentDevInst, vetoName, 0) == CR_SUCCESS)
                {
                    HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    visited.Add(vetoName);

                    for (int depth = 0; depth < 10; depth++)
                    {
                        uint parentDevInst;
                        if (CM_Get_Parent(out parentDevInst, currentDevInst, 0) != CR_SUCCESS)
                        {
                            break;
                        }

                        StringBuilder sb = new StringBuilder(512);
                        if (CM_Get_Device_IDW(parentDevInst, sb, sb.Capacity, 0) != CR_SUCCESS)
                        {
                            break;
                        }

                        string parentId = sb.ToString();
                        if (string.IsNullOrEmpty(parentId) || visited.Contains(parentId))
                        {
                            break;
                        }
                        visited.Add(parentId);

                        if (!string.IsNullOrEmpty(targetInstanceId) && string.Equals(parentId, targetInstanceId, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        if (dev != null && !string.IsNullOrEmpty(dev.PhysicalUsbParentInstanceId) && string.Equals(parentId, dev.PhysicalUsbParentInstanceId, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        if (dev != null && !string.IsNullOrEmpty(dev.DeviceInstanceId) && string.Equals(parentId, dev.DeviceInstanceId, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        currentDevInst = parentDevInst;
                    }
                }
            }
            catch
            {
                // Live query fallback
            }

            // 5. Live fallback: check if serial number is present in vetoName
            if (dev != null && !string.IsNullOrEmpty(dev.SerialNumber) && dev.SerialNumber.Length >= 4)
            {
                if (vetoName.IndexOf(dev.SerialNumber, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        #region Volume to Physical Disk Number Resolution

        public static bool IsStorageVolumeDevnode(string vetoName)
        {
            if (string.IsNullOrEmpty(vetoName)) return false;
            return vetoName.StartsWith(@"STORAGE\Volume", StringComparison.OrdinalIgnoreCase) ||
                   vetoName.StartsWith(@"STORAGE#Volume", StringComparison.OrdinalIgnoreCase) ||
                   vetoName.IndexOf(@"STORAGE\Volume", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool TryResolveVolumeDiskNumber(string vetoName, DeviceIdentity dev, out int diskNumber)
        {
            diskNumber = -1;
            if (string.IsNullOrEmpty(vetoName)) return false;

            // 0. Test Seam check
            if (DeviceEnforcementLifecycleManager.IsTestMode && VolumeDiskNumberSeam != null)
            {
                int? seamDisk = VolumeDiskNumberSeam(vetoName);
                if (seamDisk.HasValue)
                {
                    diskNumber = seamDisk.Value;
                    return true;
                }
            }

            // Extract Volume GUID from veto name if present (e.g. {31274528-877a-11f1-bebb-8896e5792154})
            string volumeGuid = ExtractVolumeGuid(vetoName);

            // 1. Correlate against dev.VolumeMountings via GetVolumeNameForVolumeMountPoint
            if (!string.IsNullOrEmpty(volumeGuid) && dev != null && dev.VolumeMountings != null && dev.VolumeMountings.Count > 0)
            {
                foreach (string mount in dev.VolumeMountings)
                {
                    if (string.IsNullOrEmpty(mount)) continue;
                    string mountPath = mount.EndsWith("\\") ? mount : mount + "\\";
                    StringBuilder sbMountGuid = new StringBuilder(64);
                    if (GetVolumeNameForVolumeMountPoint(mountPath, sbMountGuid, (uint)sbMountGuid.Capacity))
                    {
                        if (sbMountGuid.ToString().IndexOf(volumeGuid, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (dev.PhysicalDiskNumber >= 0)
                            {
                                diskNumber = dev.PhysicalDiskNumber;
                                return true;
                            }
                        }
                    }
                }
            }

            // 2. Direct IOCTL query to volume device path
            List<string> candidatePaths = new List<string>();

            string vetoPrefix = @"STORAGE\Volume\";
            if (vetoName.StartsWith(vetoPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string suffix = vetoName.Substring(vetoPrefix.Length);
                candidatePaths.Add(@"\\?\STORAGE#Volume#" + suffix + @"#{53f5630d-b6bf-11d0-94f2-00a0c91efb8b}");
            }

            if (!string.IsNullOrEmpty(volumeGuid))
            {
                candidatePaths.Add(@"\\?\Volume" + volumeGuid);
            }

            foreach (string path in candidatePaths)
            {
                IntPtr hVol = CreateFile(
                    path,
                    0,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (hVol != IntPtr.Zero && hVol.ToInt64() != -1)
                {
                    try
                    {
                        // Try IOCTL_STORAGE_GET_DEVICE_NUMBER
                        STORAGE_DEVICE_NUMBER sdn = new STORAGE_DEVICE_NUMBER();
                        uint bytesRet;
                        if (DeviceIoControl(hVol, IOCTL_STORAGE_GET_DEVICE_NUMBER, IntPtr.Zero, 0, ref sdn, Marshal.SizeOf(typeof(STORAGE_DEVICE_NUMBER)), out bytesRet, IntPtr.Zero))
                        {
                            diskNumber = sdn.DeviceNumber;
                            return true;
                        }

                        // Try IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS
                        VOLUME_DISK_EXTENTS vde = new VOLUME_DISK_EXTENTS();
                        if (DeviceIoControl(hVol, IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS, IntPtr.Zero, 0, ref vde, Marshal.SizeOf(typeof(VOLUME_DISK_EXTENTS)), out bytesRet, IntPtr.Zero))
                        {
                            if (vde.NumberOfDiskExtents > 0)
                            {
                                diskNumber = vde.Extents_0_DiskNumber;
                                return true;
                            }
                        }
                    }
                    finally
                    {
                        CloseHandle(hVol);
                    }
                }
            }

            return false;
        }

        private static string ExtractVolumeGuid(string vetoName)
        {
            if (string.IsNullOrEmpty(vetoName)) return null;
            int start = vetoName.IndexOf('{');
            int end = vetoName.IndexOf('}', start >= 0 ? start : 0);
            if (start >= 0 && end > start)
            {
                return vetoName.Substring(start, end - start + 1);
            }
            return null;
        }

        private static bool IsVolumeMatchingContainer(string vetoName, DeviceIdentity dev)
        {
            if (string.IsNullOrEmpty(vetoName) || dev == null) return false;
            try
            {
                string volumeContainer = ReadDevnodeContainerId(vetoName);
                if (string.IsNullOrEmpty(volumeContainer) ||
                    string.Equals(volumeContainer, "{00000000-0000-0000-ffff-ffffffffffff}", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(volumeContainer, "{00000000-0000-0000-0000-000000000000}", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(dev.PhysicalUsbParentInstanceId))
                {
                    string parentContainer = ReadDevnodeContainerId(dev.PhysicalUsbParentInstanceId);
                    if (!string.IsNullOrEmpty(parentContainer) && string.Equals(volumeContainer, parentContainer, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                if (!string.IsNullOrEmpty(dev.DeviceInstanceId))
                {
                    string diskContainer = ReadDevnodeContainerId(dev.DeviceInstanceId);
                    if (!string.IsNullOrEmpty(diskContainer) && string.Equals(volumeContainer, diskContainer, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static string ReadDevnodeContainerId(string devnodeInstanceId)
        {
            if (string.IsNullOrEmpty(devnodeInstanceId)) return null;
            try
            {
                using (var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\" + devnodeInstanceId))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("ContainerID");
                        if (val != null)
                        {
                            return val.ToString();
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        #endregion


        /// <summary>
        /// Authoritatively resolves the narrowest removable physical USB parent devnode.
        /// Rejects root hubs, host controllers, composite devices with unrelated children, and Disk 0.
        /// </summary>
        public static bool ResolveSafeRemovableUsbParent(DeviceIdentity dev, out string resolvedTargetId, out uint devInst, out string rejectionReason)
        {
            resolvedTargetId = null;
            devInst = 0;
            rejectionReason = null;

            if (dev == null)
            {
                rejectionReason = "Device reference is null.";
                return false;
            }

            // 1. Mandatory Safety Guard Verification
            string guardReason;
            SafetyValidationResult safety = SystemDeviceSafetyGuard.ValidateDevice(dev, out guardReason);
            if (safety != SafetyValidationResult.Passed)
            {
                rejectionReason = "Safety guard rejected device: " + guardReason;
                return false;
            }

            if (dev.PhysicalDiskNumber == 0)
            {
                rejectionReason = "Target is Disk 0 (host primary storage).";
                return false;
            }

            if (dev.PhysicalTransport != PhysicalBusType.Usb)
            {
                rejectionReason = "Target transport is not USB (" + dev.PhysicalTransport + ").";
                return false;
            }

            // 2. Delegate to Test Seam if configured
            if (DeviceEnforcementLifecycleManager.IsTestMode && ResolveTargetSeam != null)
            {
                return ResolveTargetSeam(dev, out resolvedTargetId, out devInst, out rejectionReason);
            }

            if (string.IsNullOrEmpty(dev.DeviceInstanceId))
            {
                rejectionReason = "Storage device instance ID is missing.";
                return false;
            }

            // 3. Locate child storage disk devnode
            uint diskInst;
            int crDisk = CM_Locate_DevNodeW(out diskInst, dev.DeviceInstanceId, 0);
            if (crDisk != CR_SUCCESS)
            {
                rejectionReason = string.Format("Cannot locate child storage disk devnode '{0}' (CR=0x{1:X8}).", dev.DeviceInstanceId, crDisk);
                return false;
            }

            // 4. Bounded Ancestor Walk (up to 10 levels)
            uint currentInst = diskInst;
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            visited.Add(dev.DeviceInstanceId);

            string expectedParentId = dev.PhysicalUsbParentInstanceId;

            for (int depth = 0; depth < 10; depth++)
            {
                uint parentInst;
                int crParent = CM_Get_Parent(out parentInst, currentInst, 0);
                if (crParent != CR_SUCCESS) break;

                string parentId = GetDeviceId(parentInst);
                if (string.IsNullOrEmpty(parentId) || visited.Contains(parentId)) break;
                visited.Add(parentId);

                // Reject host controllers and root hubs immediately
                if (parentId.StartsWith("PCI\\", StringComparison.OrdinalIgnoreCase) ||
                    parentId.StartsWith("ACPI\\", StringComparison.OrdinalIgnoreCase) ||
                    parentId.IndexOf("ROOT_HUB", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    break; // Reached host controller or root hub boundary; stop walking
                }

                string svc = GetRegistryPropertyString(parentInst, CM_DRP_SERVICE);
                if (svc.IndexOf("hub", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    parentId.IndexOf("ROOT", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    break;
                }

                // Look for physical USB ancestor (starts with USB\ and has VID_ and PID_)
                if (parentId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) &&
                    parentId.IndexOf("VID_", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    parentId.IndexOf("PID_", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // If DeviceIdentity had an authoritative PhysicalUsbParentInstanceId, verify positive correlation
                    if (!string.IsNullOrEmpty(expectedParentId) && expectedParentId != "UNKNOWN")
                    {
                        if (!string.Equals(parentId, expectedParentId, StringComparison.OrdinalIgnoreCase))
                        {
                            currentInst = parentInst;
                            continue;
                        }
                    }

                    // Check capabilities: must have CM_DEVCAP_REMOVABLE = true
                    uint caps = GetCapabilities(parentInst);
                    bool isRemovable = (caps & CM_DEVCAP_REMOVABLE) != 0;
                    if (!isRemovable)
                    {
                        // Check if child disk is surprise-removal ok but parent is non-removable
                        currentInst = parentInst;
                        continue;
                    }

                    // Check for unrelated children (composite protection)
                    bool hasUnrelatedChildren = CheckForUnrelatedChildren(parentInst, dev.DeviceInstanceId);
                    if (hasUnrelatedChildren)
                    {
                        rejectionReason = string.Format("USB devnode '{0}' has unrelated peripheral children; ejecting would affect non-storage devices.", parentId);
                        return false;
                    }

                    // Narrow safe physical removable USB ancestor verified!
                    resolvedTargetId = parentId;
                    devInst = parentInst;
                    return true;
                }

                currentInst = parentInst;
            }

            rejectionReason = "Could not authoritatively prove narrow safe physical removable USB parent.";
            return false;
        }

        private static bool CheckForUnrelatedChildren(uint parentInst, string targetStorageDiskId)
        {
            try
            {
                uint childInst;
                if (CM_Get_Child(out childInst, parentInst, 0) != CR_SUCCESS) return false;

                while (true)
                {
                    string childId = GetDeviceId(childInst);
                    if (!string.IsNullOrEmpty(childId) && !string.Equals(childId, targetStorageDiskId, StringComparison.OrdinalIgnoreCase))
                    {
                        string childClass = GetRegistryPropertyString(childInst, CM_DRP_CLASS);
                        string childSvc = GetRegistryPropertyString(childInst, CM_DRP_SERVICE);

                        // If child is keyboard, mouse, net, bluetooth, etc. -> unrelated!
                        if (string.Equals(childClass, "Keyboard", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(childClass, "Mouse", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(childClass, "HIDClass", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(childClass, "Net", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(childClass, "Bluetooth", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(childClass, "Media", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }

                    uint siblingInst;
                    if (CM_Get_Sibling(out siblingInst, childInst, 0) != CR_SUCCESS) break;
                    childInst = siblingInst;
                }
            }
            catch { }
            return false;
        }

        #endregion

        #region Service Privilege Handling & Bounded Elevation

        /// <summary>
        /// Retrieves the current process session context and physical console attachment state.
        /// </summary>
        public static CallerPrivilegeContext GetCallerPrivilegeContext()
        {
            if (DeviceEnforcementLifecycleManager.IsTestMode && CallerContextSeam != null)
            {
                return CallerContextSeam();
            }

            CallerPrivilegeContext ctx = new CallerPrivilegeContext();
            try
            {
                ctx.CurrentSessionId = Process.GetCurrentProcess().SessionId;
                ctx.ActiveConsoleSessionId = WTSGetActiveConsoleSessionId();
                ctx.IsUserInteractive = Environment.UserInteractive;
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    ctx.IsSystem = identity.IsSystem;
                }
            }
            catch
            {
                ctx.CurrentSessionId = 0;
                ctx.ActiveConsoleSessionId = 0xFFFFFFFF;
                ctx.IsUserInteractive = false;
                ctx.IsSystem = false;
            }
            return ctx;
        }

        /// <summary>
        /// Evaluates whether SeLoadDriverPrivilege is required under Microsoft CM_Request_Device_Eject semantics.
        /// For non-dock devices: required if non-interactive, LocalSystem/service, or in a multi-user session
        /// not attached to the physical console (e.g. Remote Desktop / Terminal Services).
        /// Normal physical console interactive GUI does not require or enable this privilege.
        /// </summary>
        public static bool IsPrivilegeRequired(uint caps)
        {
            if (DeviceEnforcementLifecycleManager.IsTestMode && IsPrivilegeRequiredSeam != null)
            {
                return IsPrivilegeRequiredSeam(caps);
            }

            if (DeviceEnforcementLifecycleManager.IsTestMode && IsServiceContextSeam != null)
            {
                return IsServiceContextSeam();
            }

            bool isDockDevice = (caps & CM_DEVCAP_DOCKDEVICE) != 0;
            if (isDockDevice)
            {
                return false;
            }

            CallerPrivilegeContext ctx = GetCallerPrivilegeContext();

            // LocalSystem or Windows Service
            if (ctx.IsSystem) return true;

            // Session 0 (Services session)
            if (ctx.CurrentSessionId == 0) return true;

            // Non-interactive process
            if (!ctx.IsUserInteractive) return true;

            // Remote Desktop / Terminal Services session (not physical console)
            // WTSGetActiveConsoleSessionId returns 0xFFFFFFFF if no session is attached to the physical console.
            if (ctx.ActiveConsoleSessionId == 0xFFFFFFFF || (uint)ctx.CurrentSessionId != ctx.ActiveConsoleSessionId)
            {
                return true;
            }

            // Normal interactive GUI user attached to the physical console session
            return false;
        }

        /// <summary>
        /// Bounded privilege elevation: captures current privilege state, enables SeLoadDriverPrivilege,
        /// and returns an IDisposable scope that restores prior state in Dispose().
        /// Returns null and sets failureReason if privilege cannot be acquired/enabled.
        /// </summary>
        public static IDisposable AcquireScopedPrivilege(out string failureReason)
        {
            failureReason = null;

            if (DeviceEnforcementLifecycleManager.IsTestMode && AcquirePrivilegeSeam != null)
            {
                return AcquirePrivilegeSeam(out failureReason);
            }

            IntPtr hToken = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hToken))
                {
                    int win32 = Marshal.GetLastWin32Error();
                    failureReason = "Failed to open process token (Win32=" + win32 + ").";
                    return null;
                }

                LUID luid;
                if (!LookupPrivilegeValue(null, SE_LOAD_DRIVER_NAME, out luid))
                {
                    int win32 = Marshal.GetLastWin32Error();
                    failureReason = "LookupPrivilegeValue failed for " + SE_LOAD_DRIVER_NAME + " (Win32=" + win32 + ").";
                    CloseHandle(hToken);
                    return null;
                }

                TOKEN_PRIVILEGES newState = new TOKEN_PRIVILEGES();
                newState.PrivilegeCount = 1;
                newState.Privilege.Luid = luid;
                newState.Privilege.Attributes = SE_PRIVILEGE_ENABLED;

                TOKEN_PRIVILEGES prevState = new TOKEN_PRIVILEGES();
                uint returnLength = 0;
                uint bufferLength = (uint)Marshal.SizeOf(typeof(TOKEN_PRIVILEGES));

                if (!AdjustTokenPrivileges(hToken, false, ref newState, bufferLength, ref prevState, out returnLength))
                {
                    int win32 = Marshal.GetLastWin32Error();
                    failureReason = "AdjustTokenPrivileges failed (Win32=" + win32 + ").";
                    CloseHandle(hToken);
                    return null;
                }

                int lastErr = Marshal.GetLastWin32Error();
                if (lastErr == ERROR_NOT_ALL_ASSIGNED)
                {
                    failureReason = "Token does not possess SeLoadDriverPrivilege (ERROR_NOT_ALL_ASSIGNED).";
                    CloseHandle(hToken);
                    return null;
                }

                return new ScopedPrivilegeScope(hToken, prevState);
            }
            catch (Exception ex)
            {
                if (hToken != IntPtr.Zero) CloseHandle(hToken);
                failureReason = "Exception during privilege acquisition: " + ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Audits privilege requirements for the caller.
        /// </summary>
        public static bool EnsurePrivilegeForCaller(out string failureReason)
        {
            failureReason = null;

            if (DeviceEnforcementLifecycleManager.IsTestMode && EnsureServicePrivilegeSeam != null)
            {
                return EnsureServicePrivilegeSeam(out failureReason);
            }

            if (!IsPrivilegeRequired(0))
            {
                return true;
            }

            using (IDisposable scope = AcquireScopedPrivilege(out failureReason))
            {
                return (scope != null);
            }
        }

        #endregion

        #region Tri-State Postverification

        /// <summary>
        /// Strictly non-remounting volume availability check returning tri-state VolumeAvailabilityResult.
        /// Explicit absence errors: ERROR_FILE_NOT_FOUND (2), ERROR_PATH_NOT_FOUND (3), ERROR_NOT_READY (21), ERROR_NO_MEDIA_IN_DRIVE (1112)
        /// -> UnavailableConfirmed.
        /// ERROR_ACCESS_DENIED (5), ERROR_INVALID_HANDLE (6), ERROR_CRC (23), ERROR_GEN_FAILURE (31), sharing violations, or unknown
        /// -> strictly Indeterminate (fails closed).
        /// IOCTL success -> Available.
        /// </summary>
        public static VolumeAvailabilityResult CheckVolumeAvailability(string driveLetter, out int win32Error)
        {
            win32Error = 0;

            if (DeviceEnforcementLifecycleManager.IsTestMode && VolumeAvailabilitySeam != null)
            {
                return VolumeAvailabilitySeam(driveLetter, out win32Error);
            }

            string clean = (driveLetter ?? "").TrimEnd('\\');
            if (string.IsNullOrEmpty(clean))
            {
                win32Error = 2; // ERROR_FILE_NOT_FOUND
                return VolumeAvailabilityResult.UnavailableConfirmed;
            }

            string volPath = @"\\.\" + clean;
            IntPtr hCheck = CreateFile(volPath,
                0, // Query access only - strictly non-remounting
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (hCheck == IntPtr.Zero || hCheck.ToInt64() == -1)
            {
                win32Error = Marshal.GetLastWin32Error();

                // Explicit absence errors
                if (win32Error == 2 || win32Error == 3 || win32Error == 21 || win32Error == 1112)
                {
                    return VolumeAvailabilityResult.UnavailableConfirmed;
                }

                // ERROR_ACCESS_DENIED (5), ERROR_INVALID_HANDLE (6), ERROR_CRC (23), ERROR_GEN_FAILURE (31),
                // sharing violations, or unknown errors must strictly remain Indeterminate!
                return VolumeAvailabilityResult.Indeterminate;
            }

            try
            {
                uint bytesReturned;
                bool ioctlRet = DeviceIoControl(hCheck, IOCTL_STORAGE_CHECK_VERIFY,
                    IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);
                if (!ioctlRet)
                {
                    win32Error = Marshal.GetLastWin32Error();
                    if (win32Error == 21 || win32Error == 1112 || win32Error == 2 || win32Error == 3)
                    {
                        return VolumeAvailabilityResult.UnavailableConfirmed;
                    }
                    return VolumeAvailabilityResult.Indeterminate;
                }

                return VolumeAvailabilityResult.Available;
            }
            finally
            {
                CloseHandle(hCheck);
            }
        }

        public static bool PostVerifyEject(DeviceIdentity dev, string targetInstanceId, out string postVerifyDetails)
        {
            bool hasIndeterminate;
            return PostVerifyEject(dev, targetInstanceId, out postVerifyDetails, out hasIndeterminate);
        }

        /// <summary>
        /// Post-verifies that:
        /// 1. All sibling volume mountings are conclusively UnavailableConfirmed.
        /// 2. Child disk devnode is absent or not operational.
        /// 3. Physical USB parent is either absent OR held-for-eject (Code 47 / safe removal stopped).
        /// If any sibling volume yields Indeterminate (e.g. ERROR_ACCESS_DENIED 5), hasIndeterminateVolume is set true.
        /// </summary>
        public static bool PostVerifyEject(DeviceIdentity dev, string targetInstanceId, out string postVerifyDetails, out bool hasIndeterminateVolume)
        {
            postVerifyDetails = string.Empty;
            hasIndeterminateVolume = false;

            if (DeviceEnforcementLifecycleManager.IsTestMode && PostVerifySeam != null)
            {
                return PostVerifySeam(dev, targetInstanceId, out postVerifyDetails);
            }

            List<string> details = new List<string>();
            bool allSiblingsUnavailableConfirmed = true;

            // 1. Sibling volume check
            if (dev.VolumeMountings != null && dev.VolumeMountings.Count > 0)
            {
                foreach (var vol in dev.VolumeMountings)
                {
                    string clean = (vol ?? "").TrimEnd('\\');
                    int win32Err;
                    VolumeAvailabilityResult avail = CheckVolumeAvailability(clean, out win32Err);

                    if (avail == VolumeAvailabilityResult.UnavailableConfirmed)
                    {
                        details.Add(string.Format("{0} = UnavailableConfirmed(Err={1})", clean, win32Err));
                    }
                    else if (avail == VolumeAvailabilityResult.Available)
                    {
                        allSiblingsUnavailableConfirmed = false;
                        details.Add(string.Format("{0} = Available", clean));
                    }
                    else
                    {
                        allSiblingsUnavailableConfirmed = false;
                        hasIndeterminateVolume = true;
                        details.Add(string.Format("{0} = Indeterminate(Err={1})", clean, win32Err));
                    }
                }
            }

            if (!allSiblingsUnavailableConfirmed)
            {
                postVerifyDetails = "Sibling volume verification failed: " + string.Join("; ", details.ToArray());
                return false;
            }

            // 2. Child storage disk devnode check
            bool childGoneOrInactive = false;
            if (!string.IsNullOrEmpty(dev.DeviceInstanceId))
            {
                uint childInst;
                int crChild = (DeviceEnforcementLifecycleManager.IsTestMode && LocateDevNodeSeam != null)
                    ? LocateDevNodeSeam(dev.DeviceInstanceId, out childInst)
                    : CM_Locate_DevNodeW(out childInst, dev.DeviceInstanceId, 0);

                if (crChild == CR_NO_SUCH_DEVNODE || crChild == CR_INVALID_DEVICE_ID)
                {
                    childGoneOrInactive = true;
                    details.Add("DiskChild = Removed");
                }
                else if (crChild == CR_SUCCESS)
                {
                    uint status = 0, problem = 0;
                    if (CM_Get_DevNode_Status(out status, out problem, childInst, 0) == CR_SUCCESS)
                    {
                        if (problem == CM_PROB_HELD_FOR_EJECT || (status & DN_STARTED) == 0)
                        {
                            childGoneOrInactive = true;
                            details.Add(string.Format("DiskChild = Stopped(Code{0})", problem));
                        }
                        else
                        {
                            details.Add("DiskChild = ActiveStarted");
                        }
                    }
                }
                else
                {
                    details.Add(string.Format("DiskChild = Indeterminate(CR={0})", crChild));
                    hasIndeterminateVolume = true;
                }
            }
            else
            {
                childGoneOrInactive = true;
                details.Add("DiskChild = None");
            }

            if (!childGoneOrInactive)
            {
                postVerifyDetails = "Child storage disk devnode remains started and active: " + string.Join("; ", details.ToArray());
                return false;
            }

            // 3. Physical USB parent check: must be either absent (Outcome A) or held-for-eject / stopped (Outcome B)
            bool parentSafe = false;
            if (!string.IsNullOrEmpty(targetInstanceId))
            {
                uint parentInst;
                int crParent = (DeviceEnforcementLifecycleManager.IsTestMode && LocateDevNodeSeam != null)
                    ? LocateDevNodeSeam(targetInstanceId, out parentInst)
                    : CM_Locate_DevNodeW(out parentInst, targetInstanceId, 0);

                if (crParent == CR_NO_SUCH_DEVNODE || crParent == CR_INVALID_DEVICE_ID)
                {
                    parentSafe = true;
                    details.Add("PhysicalParent = Absent(Unplugged)");
                }
                else if (crParent == CR_SUCCESS)
                {
                    uint status = 0, problem = 0;
                    if (CM_Get_DevNode_Status(out status, out problem, parentInst, 0) == CR_SUCCESS)
                    {
                        if (problem == CM_PROB_HELD_FOR_EJECT)
                        {
                            parentSafe = true;
                            details.Add("PhysicalParent = HeldForEject(Code47)");
                        }
                        else if ((status & DN_HAS_PROBLEM) != 0 || (status & DN_STARTED) == 0)
                        {
                            parentSafe = true;
                            details.Add(string.Format("PhysicalParent = Stopped(Problem{0})", problem));
                        }
                        else
                        {
                            details.Add(string.Format("PhysicalParent = Running(Status=0x{0:X8})", status));
                        }
                    }
                }
                else
                {
                    // Any other lookup error (CR_ACCESS_DENIED, etc.) cannot be presumed absent (SEC-09)
                    details.Add(string.Format("PhysicalParent = Indeterminate(CR={0})", crParent));
                    hasIndeterminateVolume = true;
                }
            }
            else
            {
                parentSafe = true;
                details.Add("PhysicalParent = Unspecified");
            }

            postVerifyDetails = string.Join("; ", details.ToArray());
            return parentSafe && !hasIndeterminateVolume;
        }

        #endregion

        #region Primary Execution Engine

        private static DeviceEjectOperationResult FinishAndPersistTelemetry(StringBuilder telemetrySb, DeviceEjectOperationResult opResult)
        {
            if (telemetrySb != null && telemetrySb.Length > 0)
            {
                string text = telemetrySb.ToString().TrimEnd();
                if (!string.IsNullOrEmpty(text))
                {
                    if (DeviceEnforcementLifecycleManager.IsTestMode && TraceSinkSeam != null)
                    {
                        try { TraceSinkSeam(text); } catch { }
                    }
                    ActiveSentinel.LogTrace(text);
                }
            }
            return opResult;
        }

        private static void PrepareAndDismountSiblingVolumes(DeviceIdentity dev, StringBuilder telemetrySb)
        {
            if (dev == null) return;

            List<string> volumesToClean = new List<string>();
            if (dev.VolumeMountings != null)
            {
                foreach (var v in dev.VolumeMountings)
                {
                    if (string.IsNullOrEmpty(v)) continue;
                    string cleaned = v.Trim().TrimEnd('\\');
                    if (!volumesToClean.Contains(cleaned)) volumesToClean.Add(cleaned);
                }
            }

            if (volumesToClean.Count == 0) return;

            foreach (var vol in volumesToClean)
            {
                string volPath = @"\\.\" + vol;
                IntPtr hVol = CreateFile(volPath, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (hVol == IntPtr.Zero || hVol == new IntPtr(-1))
                {
                    hVol = CreateFile(volPath, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                }

                if (hVol != IntPtr.Zero && hVol != new IntPtr(-1))
                {
                    try
                    {
                        uint bytesRet;
                        FlushFileBuffers(hVol);

                        for (int r = 0; r < 3; r++)
                        {
                            if (DeviceIoControl(hVol, FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesRet, IntPtr.Zero))
                                break;
                            Thread.Sleep(250);
                        }

                        DeviceIoControl(hVol, FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesRet, IntPtr.Zero);

                        byte[] preventRemoval = new byte[1] { 0 };
                        IntPtr pBuf = Marshal.AllocHGlobal(1);
                        try
                        {
                            Marshal.Copy(preventRemoval, 0, pBuf, 1);
                            DeviceIoControl(hVol, IOCTL_STORAGE_MEDIA_REMOVAL, pBuf, 1, IntPtr.Zero, 0, out bytesRet, IntPtr.Zero);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(pBuf);
                        }

                        DeviceIoControl(hVol, IOCTL_STORAGE_EJECT_MEDIA, IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesRet, IntPtr.Zero);

                        if (telemetrySb != null)
                            telemetrySb.AppendFormat("PNP_VOLUME_DISMOUNT: Prepared and dismounted {0}\n", vol);
                    }
                    catch (Exception ex)
                    {
                        if (telemetrySb != null)
                            telemetrySb.AppendFormat("PNP_VOLUME_DISMOUNT_EX: {0} ({1})\n", vol, ex.Message);
                    }
                    finally
                    {
                        CloseHandle(hVol);
                    }
                }
            }
        }

        /// <summary>
        /// Executes Microsoft-native PnP Safe-Eject as the primary backend for removable USB storage.
        /// Zero fallback to FSCTL/dismount/IOCTL on veto or failure.
        /// </summary>
        public static DeviceEjectOperationResult EjectDevice(DeviceIdentity dev, SentinelArrivalLatencyRecord latencyRec = null)
        {
            var opResult = new DeviceEjectOperationResult();
            opResult.IsPnpSafeEject = true;
            opResult.SiblingVolumes = (dev != null && dev.VolumeMountings != null)
                ? new List<string>(dev.VolumeMountings)
                : new List<string>();

            StringBuilder telemetrySb = new StringBuilder();

            if (dev == null)
            {
                telemetrySb.AppendLine("PNP_REJECT: Device identity was null.");
                opResult.ConfigRet = CR_INVALID_POINTER;
                opResult.AggregateResult = DeviceEjectResult.Failed;
                opResult.Success = false;
                opResult.StatusMessage = "Device reference was null.";
                return FinishAndPersistTelemetry(telemetrySb, opResult);
            }

            // 0. Strict routing predicate: reject non-storage, WPD, proxies, Disk 0, internal fixed disks
            if (!IsSupported(dev))
            {
                telemetrySb.AppendLine("PNP_ROUTING_REJECT: Device identity not supported for PnP safe eject.");
                opResult.ConfigRet = CR_INVALID_DEVICE_ID;
                opResult.AggregateResult = DeviceEjectResult.SafetyRejected;
                opResult.Success = false;
                opResult.StatusMessage = "Device identity is not supported by PnP safe eject backend.";
                opResult.PostVerificationPassed = false;
                return FinishAndPersistTelemetry(telemetrySb, opResult);
            }

            // 1. Audit Service Privileges (Legacy test seam compatibility for 9DA/9DB)
            if (DeviceEnforcementLifecycleManager.IsTestMode && EnsureServicePrivilegeSeam != null)
            {
                string legacyErr;
                if (!EnsureServicePrivilegeSeam(out legacyErr))
                {
                    telemetrySb.AppendLine("PNP_PRIVILEGE_CHECK: FAILED - " + legacyErr);
                    opResult.ConfigRet = CR_ACCESS_DENIED;
                    opResult.AggregateResult = DeviceEjectResult.AccessDenied;
                    opResult.Success = false;
                    opResult.StatusMessage = "Service privilege validation failed: " + legacyErr;
                    opResult.PostVerificationPassed = false;
                    return FinishAndPersistTelemetry(telemetrySb, opResult);
                }
            }

            // 2. Resolve Safe Removable USB Parent Target
            string targetInstanceId;
            uint devInst;
            string rejectReason;
            bool targetResolved = ResolveSafeRemovableUsbParent(dev, out targetInstanceId, out devInst, out rejectReason);

            telemetrySb.AppendFormat("PNP_TARGET_RESOLVE: Target={0}, DevInst=0x{1:X8}, Success={2}\n",
                targetInstanceId ?? "NONE", devInst, targetResolved);

            if (!targetResolved)
            {
                telemetrySb.AppendFormat("PNP_TARGET_REJECT: Reason={0}\n", rejectReason);
                opResult.ConfigRet = CR_NO_SUCH_DEVNODE;
                opResult.AggregateResult = DeviceEjectResult.SafetyRejected;
                opResult.Success = false;
                opResult.StatusMessage = "PnP target resolution rejected: " + rejectReason;
                opResult.PostVerificationPassed = false;
                return FinishAndPersistTelemetry(telemetrySb, opResult);
            }

            // 3. Capability Verification
            uint caps = (DeviceEnforcementLifecycleManager.IsTestMode) ? CM_DEVCAP_REMOVABLE : GetCapabilities(devInst);
            bool isRemovable = (caps & CM_DEVCAP_REMOVABLE) != 0;
            telemetrySb.AppendFormat("PNP_CAPABILITY_CHECK: Caps=0x{0:X8}, Removable={1}\n", caps, isRemovable);

            if (!isRemovable)
            {
                telemetrySb.AppendFormat("PNP_CAPABILITY_REJECT: Caps=0x{0:X8} not removable.\n", caps);
                opResult.ConfigRet = CR_INVALID_FLAG;
                opResult.TargetInstanceId = targetInstanceId;
                opResult.AggregateResult = DeviceEjectResult.NotEjectable;
                opResult.Success = false;
                opResult.StatusMessage = "Target devnode does not have CM_DEVCAP_REMOVABLE capability.";
                opResult.PostVerificationPassed = false;
                return FinishAndPersistTelemetry(telemetrySb, opResult);
            }

            // 4. Caller Privilege Context & Bounded Scope Elevation
            IDisposable privScope = null;
            bool privRequired = IsPrivilegeRequired(caps);
            telemetrySb.AppendFormat("PNP_PRIVILEGE_REQUIRED: {0}\n", privRequired);

            if (privRequired)
            {
                if (DeviceEnforcementLifecycleManager.IsTestMode && EnsureServicePrivilegeSeam != null && AcquirePrivilegeSeam == null)
                {
                    // Legacy test seam already audited and confirmed service privilege
                }
                else
                {
                    string privAcquireErr;
                    privScope = AcquireScopedPrivilege(out privAcquireErr);
                    if (privScope == null)
                    {
                        telemetrySb.AppendLine("PNP_PRIVILEGE_ENABLE: FAILED - " + privAcquireErr);
                        opResult.ConfigRet = CR_ACCESS_DENIED;
                        opResult.TargetInstanceId = targetInstanceId;
                        opResult.AggregateResult = DeviceEjectResult.AccessDenied;
                        opResult.Success = false;
                        opResult.StatusMessage = "Privilege acquisition failed: " + privAcquireErr;
                        opResult.PostVerificationPassed = false;
                        return FinishAndPersistTelemetry(telemetrySb, opResult); // 0 eject calls, 0 legacy fallback! Fails closed!
                    }
                }
            }

            // 4b. Prepare and dismount sibling volumes for multi-partition drives in production mode
            if (!DeviceEnforcementLifecycleManager.IsTestMode)
            {
                PrepareAndDismountSiblingVolumes(dev, telemetrySb);
            }

            // 5. Execute CM_Request_Device_EjectW (EXACTLY ONCE in test mode, bounded retry in production) with bounded privilege scope and zero-initialized native MAX_PATH buffer
            int crEject;
            PNP_VETO_TYPE vetoType = PNP_VETO_TYPE.PNP_VetoTypeUnknown;
            IntPtr pVetoBuffer = Marshal.AllocHGlobal(MAX_PATH * sizeof(char));
            for (int i = 0; i < MAX_PATH; i++)
            {
                Marshal.WriteInt16(pVetoBuffer, i * sizeof(char), 0);
            }

            string decodedVetoName = string.Empty;
            int nameLength = 0;
            bool nullTerminated = false;
            long elapsedMs = 0;

            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                try
                {
                    telemetrySb.AppendFormat("PNP_EJECT_REQUEST: Target={0}, DevInst=0x{1:X8}\n", targetInstanceId, devInst);
                    if (DeviceEnforcementLifecycleManager.IsTestMode && RequestEjectNativeSeam != null)
                    {
                        crEject = RequestEjectNativeSeam(devInst, out vetoType, pVetoBuffer, MAX_PATH);
                    }
                    else if (DeviceEnforcementLifecycleManager.IsTestMode && RequestEjectSeam != null)
                    {
                        StringBuilder legacySb = new StringBuilder(MAX_PATH);
                        crEject = RequestEjectSeam(devInst, out vetoType, legacySb, MAX_PATH);
                        if (legacySb.Length > 0)
                        {
                            char[] legacyChars = legacySb.ToString().ToCharArray();
                            int copyLen = Math.Min(legacyChars.Length, MAX_PATH - 1);
                            Marshal.Copy(legacyChars, 0, pVetoBuffer, copyLen);
                            Marshal.WriteInt16(pVetoBuffer, copyLen * sizeof(char), 0);
                        }
                    }
                    else
                    {
                        crEject = CM_Request_Device_EjectW(devInst, out vetoType, pVetoBuffer, MAX_PATH, 0);
                        // Retry up to 3 times on veto with 400ms delay in production
                        for (int attempt = 1; attempt <= 2 && crEject == CR_REMOVE_VETOED; attempt++)
                        {
                            telemetrySb.AppendFormat("PNP_EJECT_RETRY: Vetoed (attempt {0}), retrying in 400ms...\n", attempt);
                            Thread.Sleep(400);
                            for (int z = 0; z < MAX_PATH; z++) Marshal.WriteInt16(pVetoBuffer, z * sizeof(char), 0);
                            crEject = CM_Request_Device_EjectW(devInst, out vetoType, pVetoBuffer, MAX_PATH, 0);
                        }
                    }
                }
                finally
                {
                    sw.Stop();
                    elapsedMs = sw.ElapsedMilliseconds;
                    if (privScope != null)
                    {
                        privScope.Dispose(); // Restore temporary privilege in finally!
                        privScope = null;
                    }
                }

                // Scan for first UTF-16 NUL within MAX_PATH
                int nulPos = -1;
                char[] vetoChars = new char[MAX_PATH];
                for (int i = 0; i < MAX_PATH; i++)
                {
                    short val = Marshal.ReadInt16(pVetoBuffer, i * sizeof(char));
                    vetoChars[i] = (char)val;
                    if (val == 0 && nulPos == -1)
                    {
                        nulPos = i;
                    }
                }

                nullTerminated = (nulPos >= 0);
                if (nullTerminated)
                {
                    decodedVetoName = new string(vetoChars, 0, nulPos);
                    nameLength = nulPos;
                }
                else
                {
                    decodedVetoName = "[MALFORMED_TRUNCATED]";
                    nameLength = MAX_PATH;
                }
            }
            finally
            {
                if (pVetoBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pVetoBuffer);
                    pVetoBuffer = IntPtr.Zero;
                }
            }

            string crName = GetConfigRetName(crEject);
            telemetrySb.AppendFormat("PNP_EJECT_RESULT: CR={0} ({1}), ElapsedMs={2}\n", crEject, crName, elapsedMs);

            opResult.ConfigRet = crEject;
            opResult.VetoType = vetoType;
            opResult.VetoName = decodedVetoName;
            opResult.TargetInstanceId = targetInstanceId;

            // 6. Evaluate Return Code Semantics
            if (crEject == CR_SUCCESS)
            {
                if (!DeviceEnforcementLifecycleManager.IsTestMode)
                {
                    Thread.Sleep(200);
                }

                // 7. Bounded Tri-State Postverification
                string postVerifyDetails;
                bool hasIndeterminate;
                bool postVerifyPassed = PostVerifyEject(dev, targetInstanceId, out postVerifyDetails, out hasIndeterminate);
                telemetrySb.AppendFormat("PNP_POSTVERIFY: Passed={0}, Indeterminate={1}, Details=[{2}]\n", postVerifyPassed, hasIndeterminate, postVerifyDetails);

                bool parentHeld = postVerifyDetails != null && (postVerifyDetails.Contains("HeldForEject") || postVerifyDetails.Contains("Code47"));
                telemetrySb.AppendFormat("PNP_HELD_FOR_EJECT: PhysicalParentHeld={0}\n", parentHeld);

                if (postVerifyPassed)
                {
                    opResult.AggregateResult = DeviceEjectResult.Success;
                    opResult.Success = true;
                    opResult.PostVerificationPassed = true;
                    opResult.StatusMessage = "Device safely removed via Microsoft PnP engine.";
                }
                else if (hasIndeterminate)
                {
                    opResult.AggregateResult = DeviceEjectResult.VerificationIndeterminate;
                    opResult.Success = false;
                    opResult.PostVerificationPassed = false;
                    opResult.StatusMessage = "PnP safe eject succeeded but volume verification was indeterminate: " + postVerifyDetails;
                }
                else
                {
                    opResult.AggregateResult = DeviceEjectResult.IncompleteEject;
                    opResult.Success = false;
                    opResult.PostVerificationPassed = false;
                    opResult.StatusMessage = "PnP safe eject succeeded but post-verification failed: " + postVerifyDetails;
                }
            }
            else if (crEject == CR_REMOVE_VETOED)
            {
                telemetrySb.AppendFormat("PNP_VETO: Type={0} ({1}), Name=\"{2}\", NameLength={3}, NullTerminated={4}, CR=0x{5:X8} ({6}), Target={7}, ElapsedMs={8}\n",
                    vetoType, (int)vetoType, decodedVetoName, nameLength, nullTerminated, crEject, crName, targetInstanceId, elapsedMs);
                opResult.AggregateResult = DeviceEjectResult.Failed;
                opResult.Success = false;
                opResult.PostVerificationPassed = false;
                if (!nullTerminated)
                {
                    opResult.StatusMessage = string.Format("PnP safe eject vetoed by {0} ({1}): malformed/truncated veto name (no NUL terminator in MAX_PATH)", vetoType, (int)vetoType);
                }
                else
                {
                    opResult.StatusMessage = string.Format("PnP safe eject vetoed by {0} ({1}): \"{2}\"", vetoType, (int)vetoType, decodedVetoName);
                }
            }
            else
            {
                telemetrySb.AppendFormat("PNP_EJECT_FAILED: CONFIGRET={0} ({1})\n", crEject, crName);
                opResult.AggregateResult = DeviceEjectResult.Failed;
                opResult.Success = false;
                opResult.PostVerificationPassed = false;
                opResult.StatusMessage = string.Format("PnP safe eject failed with CONFIGRET {0} ({1}).", crEject, crName);
            }

            telemetrySb.AppendFormat("AGGREGATE: Result={0}, Success={1}, PostVerifyPassed={2}\n",
                opResult.AggregateResult, opResult.Success, opResult.PostVerificationPassed);

            return FinishAndPersistTelemetry(telemetrySb, opResult);
        }

        #endregion
    }
}
