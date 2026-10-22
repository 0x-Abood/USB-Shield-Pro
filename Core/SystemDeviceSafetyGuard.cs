// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — System Device Safety Guard
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UsbShieldPro.Models;

namespace UsbShieldPro.Core
{
    /// <summary>
    /// Absolute barrier preventing any USB Shield operation from touching, disabling,
    /// or ejecting internal system disks, boot volumes, or critical computer peripherals.
    /// Invariant: Fail Open on any ambiguity.
    /// </summary>
    public static class SystemDeviceSafetyGuard
    {
        private static int _systemPhysicalDiskNumber = -1;
        private static bool _systemDiskQueried = false;
        private static readonly object _lock = new object();

        private static int? _systemPhysicalDiskNumberSeam = null;
        public static int? SystemPhysicalDiskNumberSeam
        {
            get
            {
                if (!DeviceEnforcementLifecycleManager.IsTestMode) return null;
                return _systemPhysicalDiskNumberSeam;
            }
            set
            {
                if (!DeviceEnforcementLifecycleManager.IsTestMode)
                {
                    _systemPhysicalDiskNumberSeam = null;
                    return;
                }
                _systemPhysicalDiskNumberSeam = value;
            }
        }

        private static HashSet<string> _activePagingFileVolumesSeam = null;
        public static HashSet<string> ActivePagingFileVolumesSeam
        {
            get
            {
                if (!DeviceEnforcementLifecycleManager.IsTestMode) return null;
                return _activePagingFileVolumesSeam;
            }
            set
            {
                if (!DeviceEnforcementLifecycleManager.IsTestMode)
                {
                    _activePagingFileVolumesSeam = null;
                    return;
                }
                _activePagingFileVolumesSeam = value;
            }
        }

        public static void ResetForTest()
        {
            _systemPhysicalDiskNumberSeam = null;
            _activePagingFileVolumesSeam = null;
        }

        // Critical Peripheral Setup Class GUIDs that MUST NEVER be targeted
        public static readonly HashSet<string> ProhibitedSetupClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "{4d36e967-e325-11ce-bfc1-08002be10318}", // DiskDrive (Generic - internal & external disks)
            "{4d36e96b-e325-11ce-bfc1-08002be10318}", // Keyboard
            "{4d36e96f-e325-11ce-bfc1-08002be10318}", // Mouse
            "{4d36e972-e325-11ce-bfc1-08002be10318}", // Net (Network cards/Wi-Fi)
            "{4d36e97d-e325-11ce-bfc1-08002be10318}", // System
            "{36fc9e60-c465-11cf-8056-444553540000}", // USB Host Controllers and Root Hubs
            "{4d36e96a-e325-11ce-bfc1-08002be10318}", // HDC (Hard Disk Controllers)
            "{4d36e97b-e325-11ce-bfc1-08002be10318}", // SCSIAdapter
            "{745a17a0-74d3-11d0-b6fe-00a0c90f57da}"  // HIDClass
        };

        /// <summary>
        /// Validates whether a device is safe to target with storage/ejection rules.
        /// Throws nothing; returns structured safety result.
        /// </summary>
        public static SafetyValidationResult ValidateDevice(DeviceIdentity device, out string reason)
        {
            reason = null;
            if (device == null)
            {
                reason = "Null device reference";
                return SafetyValidationResult.RejectedAmbiguousTransport;
            }

            int sysDisk = GetSystemPhysicalDiskNumber();

            // 1. Critical Peripheral Check (Mouse, Keyboard, Network, USB Host Controllers)
            if (device.WindowsClass == StorageClass.InputDevice ||
                device.WindowsClass == StorageClass.NetworkAdapter ||
                device.WindowsClass == StorageClass.UsbHubOrController)
            {
                reason = "Device is a critical input, network, or controller peripheral.";
                return SafetyValidationResult.RejectedCriticalPeripheral;
            }

            if (!string.IsNullOrEmpty(device.SetupClassGuid) &&
                ProhibitedSetupClasses.Contains(device.SetupClassGuid))
            {
                // Note: DiskDrive setup class is allowed ONLY if proven USB transport AND not the system disk
                if (!string.Equals(device.SetupClassGuid, "{4d36e967-e325-11ce-bfc1-08002be10318}", StringComparison.OrdinalIgnoreCase))
                {
                    reason = "Setup class " + device.SetupClassGuid + " is a prohibited system class.";
                    return SafetyValidationResult.RejectedCriticalPeripheral;
                }
            }

            // 2. Physical Disk System/Boot Check (All system disks: Disk 0 floor + detected OS disk)
            if (device.PhysicalDiskNumber == 0 || (sysDisk >= 0 && device.PhysicalDiskNumber == sysDisk))
            {
                reason = "Target physical disk #" + device.PhysicalDiskNumber + " hosts the Windows system or boot volume.";
                return SafetyValidationResult.RejectedSystemOrBootDisk;
            }

            // Check if any mapped volume letter is the Windows SystemDrive
            string systemDrive = Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\');
            if (device.VolumeMountings != null)
            {
                foreach (string vol in device.VolumeMountings)
                {
                    if (string.Equals(vol.TrimEnd('\\'), systemDrive, StringComparison.OrdinalIgnoreCase))
                    {
                        reason = "Device volume " + vol + " contains the active Windows system directory.";
                        return SafetyValidationResult.RejectedSystemOrBootDisk;
                    }
                }
            }

            // Check if any mapped volume hosts active Windows paging files (SEC-08 hardening)
            if (device.VolumeMountings != null && device.VolumeMountings.Count > 0)
            {
                var pageVolumes = GetActivePagingFileVolumes();
                if (pageVolumes != null && pageVolumes.Count > 0)
                {
                    foreach (string vol in device.VolumeMountings)
                    {
                        string root = (vol ?? "").TrimEnd('\\');
                        if (pageVolumes.Contains(root))
                        {
                            reason = "Device volume " + vol + " hosts an active Windows paging file.";
                            return SafetyValidationResult.RejectedSystemOrBootDisk;
                        }
                    }
                }
            }

            // 3. Internal Bus Check
            if (device.PhysicalTransport == PhysicalBusType.Nvme ||
                device.PhysicalTransport == PhysicalBusType.Sata ||
                device.PhysicalTransport == PhysicalBusType.Ata ||
                device.PhysicalTransport == PhysicalBusType.Atapi ||
                device.PhysicalTransport == PhysicalBusType.Raid ||
                device.PhysicalTransport == PhysicalBusType.IScsi)
            {
                reason = "Internal system bus (" + device.PhysicalTransport + ") is strictly protected.";
                return SafetyValidationResult.RejectedInternalBus;
            }

            // 4. Must be confirmed USB or WPD
            if (device.PhysicalTransport != PhysicalBusType.Usb &&
                device.WindowsClass != StorageClass.WindowsPortableDevice)
            {
                reason = "Non-USB transport (" + device.PhysicalTransport + ") rejected by fail-open policy.";
                return SafetyValidationResult.RejectedAmbiguousTransport;
            }

            // 5. PnP ID Sanity Check (must not belong to PCI root / NVMe controller)
            if (!string.IsNullOrEmpty(device.DeviceInstanceId))
            {
                string idUpper = device.DeviceInstanceId.ToUpperInvariant();
                if (idUpper.StartsWith("PCI\\") ||
                    idUpper.StartsWith("ACPI\\") ||
                    idUpper.Contains("VEN_NVME") ||
                    idUpper.Contains("VEN_SATA"))
                {
                    reason = "PnP device instance indicates internal controller ancestry.";
                    return SafetyValidationResult.RejectedInternalBus;
                }
            }

            return SafetyValidationResult.Passed;
        }

        /// <summary>
        /// Retrieves the PhysicalDisk number backing the Windows OS installation drive (e.g. C:).
        /// </summary>
        public static int GetSystemPhysicalDiskNumber()
        {
            if (DeviceEnforcementLifecycleManager.IsTestMode && SystemPhysicalDiskNumberSeam.HasValue)
            {
                return SystemPhysicalDiskNumberSeam.Value;
            }

            lock (_lock)
            {
                if (_systemDiskQueried) return _systemPhysicalDiskNumber;
                _systemDiskQueried = true;
                try
                {
                    string sysRoot = Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\');
                    string volumePath = @"\\.\" + sysRoot;

                    IntPtr hDevice = CreateFile(volumePath,
                        GENERIC_READ,
                        FILE_SHARE_READ | FILE_SHARE_WRITE,
                        IntPtr.Zero,
                        OPEN_EXISTING,
                        0,
                        IntPtr.Zero);

                    if (hDevice != IntPtr.Zero && hDevice.ToInt64() != -1)
                    {
                        try
                        {
                            STORAGE_DEVICE_NUMBER sdn = new STORAGE_DEVICE_NUMBER();
                            int size = Marshal.SizeOf(sdn);
                            int bytesReturned;
                            bool ok = DeviceIoControl(hDevice,
                                IOCTL_STORAGE_GET_DEVICE_NUMBER,
                                IntPtr.Zero, 0,
                                ref sdn, size,
                                out bytesReturned, IntPtr.Zero);

                            if (ok)
                            {
                                _systemPhysicalDiskNumber = sdn.DeviceNumber;
                            }
                        }
                        finally
                        {
                            CloseHandle(hDevice);
                        }
                    }
                }
                catch
                {
                    _systemPhysicalDiskNumber = -1;
                }
                return _systemPhysicalDiskNumber;
            }
        }

        // P/Invoke definitions for Win32 Storage queries
        private const uint GENERIC_READ = 0x80000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x002D1080;

        [StructLayout(LayoutKind.Sequential)]
        private struct STORAGE_DEVICE_NUMBER
        {
            public int DeviceType;
            public int DeviceNumber;
            public int PartitionNumber;
        }

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
            int nInBufferSize,
            ref STORAGE_DEVICE_NUMBER lpOutBuffer,
            int nOutBufferSize,
            out int lpBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        /// <summary>
        /// Retrieves drive roots (e.g. "C:", "D:") hosting active Windows paging files.
        /// </summary>
        public static HashSet<string> GetActivePagingFileVolumes()
        {
            if (DeviceEnforcementLifecycleManager.IsTestMode && ActivePagingFileVolumesSeam != null)
            {
                return ActivePagingFileVolumesSeam;
            }

            HashSet<string> roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64))
                using (var mmKey = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", false))
                {
                    if (mmKey != null)
                    {
                        object pfVal = mmKey.GetValue("PagingFiles");
                        string[] pfList = pfVal as string[];
                        if (pfList == null && pfVal is string) pfList = new string[] { (string)pfVal };
                        if (pfList != null)
                        {
                            foreach (string pf in pfList)
                            {
                                if (!string.IsNullOrEmpty(pf) && pf.Length >= 2 && pf[1] == ':')
                                {
                                    roots.Add(pf.Substring(0, 2).ToUpperInvariant());
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return roots;
        }
    }
}
