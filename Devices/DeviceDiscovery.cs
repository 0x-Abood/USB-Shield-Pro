// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Comprehensive Device Discovery Engine
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using UsbShieldPro.Core;
using UsbShieldPro.Models;

namespace UsbShieldPro.Devices
{
    /// <summary>
    /// Explicit exception for intentional WPD SetupAPI/CfgMgr32 enumeration fallback conditions.
    /// Caught strictly to initiate fallback to legacy WMI; unexpected programming bugs bubble unmasked.
    /// </summary>
    [Serializable]
    public class WpdSetupApiFallbackException : Exception
    {
        public WpdSetupApiFallbackException() { }
        public WpdSetupApiFallbackException(string message) : base(message) { }
        public WpdSetupApiFallbackException(string message, Exception innerException) : base(message, innerException) { }
        protected WpdSetupApiFallbackException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    /// <summary>
    /// Discovers and inspects all attached storage and portable devices using WMI and Win32 P/Invoke.
    /// Invariant: Diagnostic inspection must be completely read-only and non-destructive.
    /// </summary>
    public static class DeviceDiscovery
    {
        public static Func<IEnumerable<DeviceIdentity>> RawDiskCandidatesSeam = null;
        public static Func<IEnumerable<DeviceIdentity>> RawWpdCandidatesSeam = null;
        public static Func<bool> SimulateSetupApiWpdFailureSeam = null;
        public static Func<List<DeviceIdentity>> SetupApiWpdCandidateSeam = null;
        private static Func<List<DeviceIdentity>> _wmiFallbackWpdCandidateSeam = null;
        public static Func<List<DeviceIdentity>> WmiFallbackWpdCandidateSeam
        {
            get
            {
                if (!DeviceEnforcementLifecycleManager.IsTestMode) return null;
                return _wmiFallbackWpdCandidateSeam;
            }
            set
            {
                if (!DeviceEnforcementLifecycleManager.IsTestMode)
                {
                    _wmiFallbackWpdCandidateSeam = null;
                    return;
                }
                _wmiFallbackWpdCandidateSeam = value;
            }
        }
        public delegate bool SetupDiEnumDeviceInfoDelegate(IntPtr deviceInfoSet, uint memberIndex, ref SP_DEVINFO_DATA deviceInfoData, out int win32Error);
        public static SetupDiEnumDeviceInfoDelegate SetupDiEnumDeviceInfoSeam = null;

        public static string WpdEnumBackend = "None";
        public static int WpdEnumCount = 0;
        public static double WpdSetupApiTimeMs = 0.0;
        public static double WpdWmiFallbackTimeMs = 0.0;
        public static string WpdFallbackReason = null;
        public static int WpdSetupApiCalls = 0;
        public static int WpdWmiQueryCalls = 0;
        public static int LastVolumeOpensCount = 0;
        public static int LastWmiQueriesCount = 0;

        public static double LastDiskDriveWmiTimeMs = 0.0;
        public static double LastPnpAncestryTimeMs = 0.0;
        public static double LastIoctlStorageTimeMs = 0.0;
        public static double LastVolumeMappingTimeMs = 0.0;
        public static double LastWpdEnumTimeMs = 0.0;
        public static double LastWpdDedupTimeMs = 0.0;
        public static double LastTotalDiscoveryTimeMs = 0.0;

        public static int LastDiskDriveWmiCalls = 0;
        public static int LastPnpAncestryCalls = 0;
        public static int LastIoctlStorageCalls = 0;
        public static int LastVolumeMappingCalls = 0;

        public static string WPD_ENUM_BACKEND { get { return WpdEnumBackend; } }
        public static int WPD_ENUM_COUNT { get { return WpdEnumCount; } }
        public static double WPD_SETUPAPI_TIME_MS { get { return WpdSetupApiTimeMs; } }
        public static double WPD_WMI_FALLBACK_TIME_MS { get { return WpdWmiFallbackTimeMs; } }
        public static string WPD_FALLBACK_REASON { get { return WpdFallbackReason; } }
        public static int WPD_SETUPAPI_CALLS { get { return WpdSetupApiCalls; } }
        public static int WPD_WMI_QUERY_CALLS { get { return WpdWmiQueryCalls; } }

        public static double LAST_DISK_DRIVE_WMI_TIME_MS { get { return LastDiskDriveWmiTimeMs; } }
        public static double LAST_PNP_ANCESTRY_TIME_MS { get { return LastPnpAncestryTimeMs; } }
        public static double LAST_IOCTL_STORAGE_TIME_MS { get { return LastIoctlStorageTimeMs; } }
        public static double LAST_VOLUME_MAPPING_TIME_MS { get { return LastVolumeMappingTimeMs; } }
        public static double LAST_WPD_ENUM_TIME_MS { get { return LastWpdEnumTimeMs; } }
        public static double LAST_WPD_DEDUP_TIME_MS { get { return LastWpdDedupTimeMs; } }
        public static double LAST_TOTAL_DISCOVERY_TIME_MS { get { return LastTotalDiscoveryTimeMs; } }
        private static DiscoveryTimingSnapshot _lastDiscoverySnapshot = new DiscoveryTimingSnapshot();
        public static DiscoveryTimingSnapshot LastDiscoverySnapshot
        {
            get { return _lastDiscoverySnapshot; }
            set { _lastDiscoverySnapshot = value; }
        }

        public static readonly List<DeviceIdentity> LastSuppressedProxies = new List<DeviceIdentity>();
        public static readonly List<DeviceIdentity> LastUnresolvedStorageProxies = new List<DeviceIdentity>();

        public static void ResetSeams()
        {
            RawDiskCandidatesSeam = null;
            RawWpdCandidatesSeam = null;
            SimulateSetupApiWpdFailureSeam = null;
            SetupApiWpdCandidateSeam = null;
            SetupDiEnumDeviceInfoSeam = null;
            _wmiFallbackWpdCandidateSeam = null;
        }

        public static List<DeviceIdentity> DiscoverAllDevices()
        {
            Stopwatch swTotal = Stopwatch.StartNew();
            lock (LastSuppressedProxies)
            {
                LastSuppressedProxies.Clear();
            }
            lock (LastUnresolvedStorageProxies)
            {
                LastUnresolvedStorageProxies.Clear();
            }

            LastVolumeOpensCount = 0;
            LastWmiQueriesCount = 0;
            WpdSetupApiCalls = 0;
            WpdWmiQueryCalls = 0;
            LastDiskDriveWmiCalls = 0;
            LastPnpAncestryCalls = 0;
            LastIoctlStorageCalls = 0;
            LastVolumeMappingCalls = 0;
            LastDiskDriveWmiTimeMs = 0.0;
            LastPnpAncestryTimeMs = 0.0;
            LastIoctlStorageTimeMs = 0.0;
            LastVolumeMappingTimeMs = 0.0;
            LastWpdEnumTimeMs = 0.0;
            LastWpdDedupTimeMs = 0.0;

            List<DeviceIdentity> results = new List<DeviceIdentity>();

            // 1. Discover Physical Disk Drives (Internal & USB)
            DiscoverDiskDrives(results);

            // 2. Discover Windows Portable Devices (MTP/PTP Phones & Cameras)
            Stopwatch swWpd = Stopwatch.StartNew();
            DiscoverPortableDevices(results);
            swWpd.Stop();
            LastWpdEnumTimeMs = swWpd.Elapsed.TotalMilliseconds;

            // 3. Finalize Identity & Safety Verification
            foreach (var device in results)
            {
                DeviceIdentityResolver.Resolve(device);

                string reason;
                device.SafetyResult = SystemDeviceSafetyGuard.ValidateDevice(device, out reason);
                device.SafetyRejectionReason = reason;

                device.Category = DeviceClassifier.Classify(device);
            }

            // 4. Secondary Cross-Layer Deduplication Safety Net
            Stopwatch swDedup = Stopwatch.StartNew();
            results = DeduplicateCrossLayerStorageProxies(results);
            swDedup.Stop();
            LastWpdDedupTimeMs = swDedup.Elapsed.TotalMilliseconds;

            swTotal.Stop();
            LastTotalDiscoveryTimeMs = swTotal.Elapsed.TotalMilliseconds;

            var snapshot = new DiscoveryTimingSnapshot
            {
                TotalDiscoveryMs = LastTotalDiscoveryTimeMs,
                DiskDriveWmiMs = LastDiskDriveWmiTimeMs,
                VolumeMappingMs = LastVolumeMappingTimeMs,
                IoctlStorageMs = LastIoctlStorageTimeMs,
                PnpAncestryMs = LastPnpAncestryTimeMs,
                WpdSetupApiMs = WpdSetupApiTimeMs,
                WpdDedupMs = LastWpdDedupTimeMs,
                WpdWmiCalls = WpdWmiQueryCalls,
                DiskCandidateCount = results.FindAll(d => d.WindowsClass == StorageClass.DiskDrive).Count,
                TotalDeviceCount = results.Count,
                TimestampUtc = DateTime.UtcNow
            };
            LastDiscoverySnapshot = snapshot;

            foreach (var d in results)
            {
                d.DiscoverySnapshot = snapshot;
            }

            return results;
        }

        private class VolumeResolutionEntry
        {
            public int DiskNumber;
            public string VolumeLabel;
            public bool IsRemovable;
            public bool Valid;
        }

        private static void DiscoverDiskDrives(List<DeviceIdentity> list)
        {
            if (DeviceEnforcementLifecycleManager.IsTestMode && RawDiskCandidatesSeam != null)
            {
                var candidates = RawDiskCandidatesSeam();
                if (candidates != null)
                {
                    foreach (var c in candidates)
                    {
                        list.Add(c);
                    }
                }
                return;
            }

            var resolutionVolumeCache = new Dictionary<string, VolumeResolutionEntry>(StringComparer.OrdinalIgnoreCase);

            try
            {
                LastWmiQueriesCount++;
                LastDiskDriveWmiCalls++;
                Stopwatch swWmiQuery = Stopwatch.StartNew();
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
                {
                    var moCollection = searcher.Get();
                    foreach (ManagementObject mo in moCollection)
                    {
                        try
                        {
                            DeviceIdentity dev = new DeviceIdentity();
                            dev.FriendlyName = mo["Model"] as string ?? mo["Caption"] as string ?? "Unknown Disk";
                            dev.Model = mo["Model"] as string;
                            dev.DeviceInstanceId = mo["PNPDeviceID"] as string;

                            Stopwatch swPnp = Stopwatch.StartNew();
                            dev.ImmediateParentInstanceId = PnpHelper.GetImmediateParentDeviceInstanceId(dev.DeviceInstanceId);
                            dev.PhysicalUsbParentInstanceId = PnpHelper.GetPhysicalUsbAncestorInstanceId(dev.DeviceInstanceId);
                            swPnp.Stop();
                            LastPnpAncestryTimeMs += swPnp.Elapsed.TotalMilliseconds;
                            LastPnpAncestryCalls += 2;

                            dev.ParentDeviceInstanceId = (!string.IsNullOrEmpty(dev.PhysicalUsbParentInstanceId) && dev.PhysicalUsbParentInstanceId != "UNKNOWN")
                                ? dev.PhysicalUsbParentInstanceId
                                : dev.ImmediateParentInstanceId;
                            dev.WindowsClass = StorageClass.DiskDrive;
                            dev.SetupClassGuid = "{4d36e967-e325-11ce-bfc1-08002be10318}";

                            // Disk Index (e.g. \\.\PHYSICALDRIVE1 -> 1)
                            if (mo["Index"] != null)
                            {
                                dev.PhysicalDiskNumber = Convert.ToInt32(mo["Index"]);
                            }

                            // Partition Count from physical partition table
                            if (mo["Partitions"] != null)
                            {
                                dev.PartitionCount = Convert.ToInt32(mo["Partitions"]);
                            }

                            // Total size
                            if (mo["Size"] != null)
                            {
                                dev.TotalSizeBytes = Convert.ToInt64(mo["Size"]);
                            }

                            // Interface / Bus type
                            string iface = (mo["InterfaceType"] as string ?? "").ToUpperInvariant();
                            bool hasVerifiedUsbParent = (!string.IsNullOrEmpty(dev.PhysicalUsbParentInstanceId) &&
                                                        dev.PhysicalUsbParentInstanceId != "UNKNOWN" &&
                                                        dev.PhysicalUsbParentInstanceId.StartsWith(@"USB", StringComparison.OrdinalIgnoreCase));
                            bool hasUsbDiskId = (dev.DeviceInstanceId != null && dev.DeviceInstanceId.ToUpperInvariant().Contains("USB"));
                            bool hasUsbParentId = (!string.IsNullOrEmpty(dev.ParentDeviceInstanceId) &&
                                                   dev.ParentDeviceInstanceId.StartsWith(@"USB", StringComparison.OrdinalIgnoreCase));
                            bool hasUsbImmediateParent = (!string.IsNullOrEmpty(dev.ImmediateParentInstanceId) &&
                                                          (dev.ImmediateParentInstanceId.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase) ||
                                                           dev.ImmediateParentInstanceId.StartsWith(@"USBSTOR\", StringComparison.OrdinalIgnoreCase) ||
                                                           dev.ImmediateParentInstanceId.StartsWith(@"UASPSTOR\", StringComparison.OrdinalIgnoreCase)));

                            if (iface.Contains("USB") || hasVerifiedUsbParent || hasUsbDiskId || hasUsbParentId || hasUsbImmediateParent)
                            {
                                dev.PhysicalTransport = PhysicalBusType.Usb;
                            }
                            else if (iface.Contains("SCSI"))
                            {
                                // Could be internal NVMe or SCSI
                                if (dev.DeviceInstanceId != null && dev.DeviceInstanceId.ToUpperInvariant().Contains("NVME"))
                                    dev.PhysicalTransport = PhysicalBusType.Nvme;
                                else
                                    dev.PhysicalTransport = PhysicalBusType.Scsi;
                            }
                            else if (iface.Contains("IDE") || iface.Contains("ATA"))
                            {
                                dev.PhysicalTransport = PhysicalBusType.Sata;
                            }
                            else
                            {
                                dev.PhysicalTransport = PhysicalBusType.Unknown;
                            }

                            // Check seek penalty and removable media flag via Win32 IOCTL
                            if (dev.PhysicalDiskNumber >= 0)
                            {
                                Stopwatch swIoctl = Stopwatch.StartNew();
                                QueryStorageProperties(dev);
                                swIoctl.Stop();
                                LastIoctlStorageTimeMs += swIoctl.Elapsed.TotalMilliseconds;
                                LastIoctlStorageCalls++;
                            }

                            // Find mapped volume letters using resolution-scoped cache
                            Stopwatch swVol = Stopwatch.StartNew();
                            MapDriveLetters(dev, resolutionVolumeCache);
                            swVol.Stop();
                            LastVolumeMappingTimeMs += swVol.Elapsed.TotalMilliseconds;
                            LastVolumeMappingCalls++;

                            list.Add(dev);
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
                swWmiQuery.Stop();
                double pureWmi = swWmiQuery.Elapsed.TotalMilliseconds - (LastPnpAncestryTimeMs + LastIoctlStorageTimeMs + LastVolumeMappingTimeMs);
                LastDiskDriveWmiTimeMs = pureWmi > 0.0 ? pureWmi : 0.0;
            }
            catch
            {
                // Fallback to basic DriveInfo if WMI fails
                DiscoverViaDriveInfo(list);
            }
        }

        private static void DiscoverPortableDevices(List<DeviceIdentity> list)
        {
            if (DeviceEnforcementLifecycleManager.IsTestMode && RawWpdCandidatesSeam != null)
            {
                var candidates = RawWpdCandidatesSeam();
                if (candidates != null)
                {
                    foreach (var c in candidates)
                    {
                        ProcessWpdCandidate(c, list);
                    }
                }
                return;
            }

            // Primary Path: SetupAPI / CfgMgr32 present-device enumeration (GUID_DEVCLASS_WPD)
            Stopwatch swSetup = Stopwatch.StartNew();
            bool setupApiSuccess = false;
            string fallbackReason = null;
            List<DeviceIdentity> candidatesFromSetup = new List<DeviceIdentity>();
            WpdSetupApiCalls++;

            try
            {
                if (DeviceEnforcementLifecycleManager.IsTestMode && SimulateSetupApiWpdFailureSeam != null && SimulateSetupApiWpdFailureSeam())
                {
                    throw new System.ComponentModel.Win32Exception(14, "Simulated SetupAPI failure for testing fallback");
                }

                if (DeviceEnforcementLifecycleManager.IsTestMode && SetupApiWpdCandidateSeam != null)
                {
                    var testCandidates = SetupApiWpdCandidateSeam();
                    if (testCandidates != null)
                    {
                        foreach (var tc in testCandidates)
                        {
                            if (string.IsNullOrEmpty(tc.DeviceInstanceId))
                            {
                                throw new WpdSetupApiFallbackException("SetupAPI candidate missing required DeviceInstanceId");
                            }
                            candidatesFromSetup.Add(tc);
                        }
                    }
                    setupApiSuccess = true;
                }
                else
                {
                    Guid wpdClassGuid = GUID_DEVCLASS_WPD;
                    IntPtr hDevInfo = SetupDiGetClassDevs(ref wpdClassGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT);
                    if (hDevInfo == IntPtr.Zero || hDevInfo.ToInt64() == -1)
                    {
                        int win32Err = Marshal.GetLastWin32Error();
                        throw new System.ComponentModel.Win32Exception(win32Err, "SetupDiGetClassDevs failed with Win32Error=" + win32Err);
                    }

                    try
                    {
                        SP_DEVINFO_DATA devInfoData = new SP_DEVINFO_DATA();
                        devInfoData.cbSize = Marshal.SizeOf(devInfoData);
                        uint idx = 0;
                        while (true)
                        {
                            bool enumSuccess;
                            int lastErr = 0;
                            if (DeviceEnforcementLifecycleManager.IsTestMode && SetupDiEnumDeviceInfoSeam != null)
                            {
                                enumSuccess = SetupDiEnumDeviceInfoSeam(hDevInfo, idx, ref devInfoData, out lastErr);
                            }
                            else
                            {
                                enumSuccess = SetupDiEnumDeviceInfo(hDevInfo, idx, ref devInfoData);
                                if (!enumSuccess)
                                {
                                    lastErr = Marshal.GetLastWin32Error();
                                }
                            }

                            if (!enumSuccess)
                            {
                                // ERROR_NO_MORE_ITEMS (259) or 0 indicates clean/normal enumeration completion!
                                if (lastErr == 259 || lastErr == 0)
                                {
                                    break;
                                }
                                // Unexpected Win32 error during enumeration -> trigger fallback
                                throw new System.ComponentModel.Win32Exception(lastErr, "SetupDiEnumDeviceInfo failed with unexpected Win32Error=" + lastErr);
                            }

                            idx++;
                            StringBuilder sbId = new StringBuilder(1024);
                            int cr = CM_Get_Device_ID(devInfoData.devInst, sbId, sbId.Capacity, 0);
                            string devId = (cr == 0) ? sbId.ToString() : null;
                            if (string.IsNullOrEmpty(devId))
                            {
                                // A required identity property could not be obtained safely from SetupAPI for this present devnode.
                                // Must trigger approved legacy WMI reconciliation/fallback.
                                throw new WpdSetupApiFallbackException("SetupAPI/CfgMgr32 failed to retrieve required DeviceInstanceId for present WPD device (CR=" + cr + ")");
                            }

                            try
                            {
                                string friendlyName = GetDevicePropertyString(hDevInfo, ref devInfoData, SPDRP_FRIENDLYNAME);
                                if (string.IsNullOrEmpty(friendlyName))
                                {
                                    friendlyName = GetDevicePropertyString(hDevInfo, ref devInfoData, SPDRP_DEVICEDESC);
                                }
                                if (string.IsNullOrEmpty(friendlyName))
                                {
                                    friendlyName = "Portable Device"; // Cosmetic fallback only
                                }

                                string mfg = GetDevicePropertyString(hDevInfo, ref devInfoData, SPDRP_MFG);

                                DeviceIdentity dev = new DeviceIdentity();
                                dev.FriendlyName = friendlyName;
                                dev.Manufacturer = mfg;
                                dev.DeviceInstanceId = devId;

                                string[] hids = GetDevicePropertyMultiString(hDevInfo, ref devInfoData, SPDRP_HARDWAREID);
                                if (hids != null && hids.Length > 0) dev.HardwareIds.AddRange(hids);

                                string[] cids = GetDevicePropertyMultiString(hDevInfo, ref devInfoData, SPDRP_COMPATIBLEIDS);
                                if (cids != null && cids.Length > 0) dev.CompatibleIds.AddRange(cids);

                                candidatesFromSetup.Add(dev);
                            }
                            catch { continue; }
                        }

                        // Success even if idx == 0 (0 devices is authoritative empty result)
                        setupApiSuccess = true;
                    }
                    finally
                    {
                        SetupDiDestroyDeviceInfoList(hDevInfo);
                    }
                }
            }
            catch (WpdSetupApiFallbackException ex)
            {
                setupApiSuccess = false;
                fallbackReason = ex.GetType().Name + ": " + ex.Message;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                setupApiSuccess = false;
                fallbackReason = ex.GetType().Name + ": " + ex.Message;
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                setupApiSuccess = false;
                fallbackReason = ex.GetType().Name + ": " + ex.Message;
            }

            swSetup.Stop();
            WpdSetupApiTimeMs = swSetup.Elapsed.TotalMilliseconds;

            if (setupApiSuccess)
            {
                WpdEnumBackend = "SetupAPI";
                WpdEnumCount = candidatesFromSetup.Count;
                WpdWmiFallbackTimeMs = 0.0;
                WpdFallbackReason = null;

                foreach (var c in candidatesFromSetup)
                {
                    ProcessWpdCandidate(c, list);
                }
                return;
            }

            // Fallback to WMI if SetupAPI failed or threw
            Stopwatch swWmi = Stopwatch.StartNew();
            WpdEnumBackend = "WMI_Fallback";
            WpdFallbackReason = fallbackReason;
            int fallbackCount = 0;
            LastWmiQueriesCount++;
            WpdWmiQueryCalls++;
            Trace.WriteLine(string.Format("[WPD_FALLBACK] SetupAPI enumeration fell back to WMI. Reason: {0}", fallbackReason ?? "Unknown"));

            try
            {
                if (DeviceEnforcementLifecycleManager.IsTestMode && WmiFallbackWpdCandidateSeam != null)
                {
                    var fallbackCandidates = WmiFallbackWpdCandidateSeam();
                    if (fallbackCandidates != null)
                    {
                        foreach (var dev in fallbackCandidates)
                        {
                            ProcessWpdCandidate(dev, list);
                            fallbackCount++;
                        }
                    }
                }
                else
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE PNPClass = 'WPD'"))
                    {
                        foreach (ManagementObject mo in searcher.Get())
                        {
                            try
                            {
                                DeviceIdentity dev = new DeviceIdentity();
                                dev.FriendlyName = mo["Name"] as string ?? mo["Caption"] as string ?? "Portable Device";
                                dev.Manufacturer = mo["Manufacturer"] as string;
                                dev.DeviceInstanceId = mo["DeviceID"] as string;
                                string[] hids = mo["HardwareID"] as string[];
                                if (hids != null) dev.HardwareIds.AddRange(hids);
                                string[] cids = mo["CompatibleID"] as string[];
                                if (cids != null) dev.CompatibleIds.AddRange(cids);

                                ProcessWpdCandidate(dev, list);
                                fallbackCount++;
                            }
                            catch { continue; }
                        }
                    }
                }
            }
            catch { }

            swWmi.Stop();
            WpdWmiFallbackTimeMs = swWmi.Elapsed.TotalMilliseconds;
            WpdEnumCount = fallbackCount;
        }

        public static void ProcessWpdCandidate(DeviceIdentity dev, List<DeviceIdentity> list)
        {
            if (dev == null) return;

            if (string.IsNullOrEmpty(dev.ImmediateParentInstanceId))
            {
                dev.ImmediateParentInstanceId = PnpHelper.GetImmediateParentDeviceInstanceId(dev.DeviceInstanceId);
            }
            if (string.IsNullOrEmpty(dev.PhysicalUsbParentInstanceId))
            {
                dev.PhysicalUsbParentInstanceId = PnpHelper.GetPhysicalUsbAncestorInstanceId(dev.DeviceInstanceId);
            }
            if (string.IsNullOrEmpty(dev.ParentDeviceInstanceId))
            {
                dev.ParentDeviceInstanceId = (!string.IsNullOrEmpty(dev.PhysicalUsbParentInstanceId) && dev.PhysicalUsbParentInstanceId != "UNKNOWN")
                    ? dev.PhysicalUsbParentInstanceId
                    : dev.ImmediateParentInstanceId;
            }

            // Primary classification: Check if WPD candidate has structural storage evidence
            if (HasStructuralStorageEvidence(dev))
            {
                DeviceIdentity owner = FindOwningDiskDrive(dev, list);
                if (owner != null)
                {
                    dev.WpdClassification = WpdKind.StorageVolumeProxy;
                    CorrelateSuppressedProxy(dev, list);
                    return; // Positively correlated & suppressed from active enforcement device list
                }
                else
                {
                    // Orphan structural storage proxy: Owner disk not yet resolved in this sweep.
                    // Must not fail open or silently disappear. Mark unresolved, record diagnostic,
                    // and keep visible to enforcement engine (which suppresses virtual SWD direct eject).
                    dev.WpdClassification = WpdKind.StorageVolumeProxyUnresolved;
                    dev.WindowsClass = StorageClass.WindowsPortableDevice;
                    dev.SetupClassGuid = "{eec5ad98-8080-425f-922a-dab33962f594}";
                    dev.PhysicalTransport = PhysicalBusType.Usb;
                    dev.IsRemovableMedia = true;

                    lock (LastUnresolvedStorageProxies)
                    {
                        if (!LastUnresolvedStorageProxies.Contains(dev))
                        {
                            LastUnresolvedStorageProxies.Add(dev);
                        }
                    }

                    list.Add(dev);
                    return;
                }
            }

            // 2. Secondary Classification: Verify positive proof of genuine portable device
            if (IsGenuinePortableDevice(dev))
            {
                dev.WpdClassification = WpdKind.PortableDevice;
                dev.WindowsClass = StorageClass.WindowsPortableDevice;
                dev.SetupClassGuid = "{eec5ad98-8080-425f-922a-dab33962f594}";
                dev.PhysicalTransport = PhysicalBusType.Usb;
                dev.IsRemovableMedia = true;

                list.Add(dev);
                return;
            }

            // 3. Ambiguous Candidate (Lacks positive proof of genuine portable device AND lacks owner disk correlation):
            // Zero Guessing Invariant: Never assume PortableDevice!
            // Must NOT fail open or silently disappear. Retain as StorageVolumeProxyUnresolved,
            // which prevents independent enforcement lease/ejection while remaining observable for reconciliation.
            dev.WpdClassification = WpdKind.StorageVolumeProxyUnresolved;
            dev.WindowsClass = StorageClass.WindowsPortableDevice;
            dev.SetupClassGuid = "{eec5ad98-8080-425f-922a-dab33962f594}";
            dev.PhysicalTransport = PhysicalBusType.Usb;
            dev.IsRemovableMedia = true;

            lock (LastUnresolvedStorageProxies)
            {
                if (!LastUnresolvedStorageProxies.Contains(dev))
                {
                    LastUnresolvedStorageProxies.Add(dev);
                }
            }

            list.Add(dev);
        }

        public static bool IsGenuinePortableDevice(DeviceIdentity dev)
        {
            if (dev == null) return false;
            if (string.IsNullOrEmpty(dev.DeviceInstanceId)) return false;

            // If it has structural storage evidence, it is not a pure genuine portable device
            if (HasStructuralStorageEvidence(dev)) return false;

            // 1. Check Hardware IDs and Compatible IDs for positive MTP / PTP / StillImage markers
            if (dev.HardwareIds != null)
            {
                foreach (var hid in dev.HardwareIds)
                {
                    if (string.IsNullOrEmpty(hid)) continue;
                    if (hid.IndexOf("MS_COMP_MTP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        hid.IndexOf("WPDMTP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        hid.IndexOf("Class_06_SubClass_01", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        hid.IndexOf("STILL_IMAGE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        hid.IndexOf("WPD", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            if (dev.CompatibleIds != null)
            {
                foreach (var cid in dev.CompatibleIds)
                {
                    if (string.IsNullOrEmpty(cid)) continue;
                    if (cid.IndexOf("MS_COMP_MTP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cid.IndexOf("WPDMTP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cid.IndexOf("Class_06_SubClass_01", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cid.IndexOf("STILL_IMAGE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cid.IndexOf("WPD", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            // 2. Pure USB devnode in WPD class (starts with USB\ and has VID_ & PID_)
            string devId = dev.DeviceInstanceId;
            if (devId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) &&
                devId.IndexOf("VID_", StringComparison.OrdinalIgnoreCase) >= 0 &&
                devId.IndexOf("PID_", StringComparison.OrdinalIgnoreCase) >= 0 &&
                devId.IndexOf("USBSTOR", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return true;
            }

            // 3. Physical USB ancestor is verified AND instance ID embeds USB VID/PID (and not storage)
            if (!string.IsNullOrEmpty(dev.PhysicalUsbParentInstanceId) &&
                dev.PhysicalUsbParentInstanceId != "UNKNOWN" &&
                devId.IndexOf("VID_", StringComparison.OrdinalIgnoreCase) >= 0 &&
                devId.IndexOf("PID_", StringComparison.OrdinalIgnoreCase) >= 0 &&
                devId.IndexOf("USBSTOR", StringComparison.OrdinalIgnoreCase) < 0 &&
                devId.IndexOf("STORAGE", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return true;
            }

            // Invariant: Cosmetic strings (e.g. dev.FriendlyName == "Portable Device") are strictly ignored.
            return false;
        }

        public static bool IsDriveRootPattern(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string trimmed = name.Trim();
            if (trimmed.Length == 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':')
                return true;
            if (trimmed.Length == 3 && char.IsLetter(trimmed[0]) && trimmed[1] == ':' && trimmed[2] == '\\')
                return true;
            return false;
        }

        public static bool HasStructuralStorageEvidence(DeviceIdentity dev)
        {
            if (dev == null) return false;

            // 1. Primary Signal: PnP Ancestor walk contains STORAGE\Volume or ROOT\volmgr
            if (PnpHelper.IsStorageBackedWpdProxy(dev.DeviceInstanceId))
            {
                return true;
            }

            // 2. Direct instance ID / immediate parent structural patterns
            string devId = dev.DeviceInstanceId ?? "";
            string parentId = dev.ImmediateParentInstanceId ?? "";
            if (devId.IndexOf("STORAGE#VOLUME", StringComparison.OrdinalIgnoreCase) >= 0 ||
                devId.IndexOf("STORAGE\\VOLUME", StringComparison.OrdinalIgnoreCase) >= 0 ||
                devId.IndexOf("USBSTOR#DISK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                devId.IndexOf("USBSTOR\\DISK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                devId.IndexOf("PARTITION", StringComparison.OrdinalIgnoreCase) >= 0 ||
                parentId.IndexOf("STORAGE\\VOLUME", StringComparison.OrdinalIgnoreCase) >= 0 ||
                parentId.IndexOf("STORAGE#VOLUME", StringComparison.OrdinalIgnoreCase) >= 0 ||
                parentId.IndexOf("ROOT\\VOLMGR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                parentId.IndexOf("VOLMGR", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        public static DeviceIdentity FindOwningDiskDrive(DeviceIdentity proxyDev, List<DeviceIdentity> existingDisks)
        {
            if (proxyDev == null || existingDisks == null || existingDisks.Count == 0) return null;

            string candidateName = (proxyDev.FriendlyName ?? "").Trim();
            string candidateTrimmed = candidateName.TrimEnd('\\');
            string proxyInstId = proxyDev.DeviceInstanceId ?? "";
            string parentInstId = proxyDev.ImmediateParentInstanceId ?? "";

            // 1. Exact volume root match against DiskDrive.VolumeMountings (contextual correlation)
            if (IsDriveRootPattern(candidateName))
            {
                foreach (var disk in existingDisks)
                {
                    if (disk.WindowsClass != StorageClass.DiskDrive) continue;

                    if (disk.VolumeMountings != null)
                    {
                        foreach (var vm in disk.VolumeMountings)
                        {
                            if (string.IsNullOrEmpty(vm)) continue;
                            string vmTrimmed = vm.Trim().TrimEnd('\\');
                            if (string.Equals(vmTrimmed, candidateTrimmed, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(vm.Trim(), candidateName, StringComparison.OrdinalIgnoreCase))
                            {
                                return disk;
                            }
                        }
                    }
                }
            }

            // 2. Direct devnode / serial / physical USB parent correlation
            foreach (var disk in existingDisks)
            {
                if (disk.WindowsClass != StorageClass.DiskDrive) continue;

                if (!string.IsNullOrEmpty(disk.DeviceInstanceId) &&
                    (proxyInstId.IndexOf(disk.DeviceInstanceId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     parentInstId.IndexOf(disk.DeviceInstanceId, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return disk;
                }

                if (!string.IsNullOrEmpty(disk.SerialNumber) && disk.SerialNumber.Length >= 6 &&
                    (proxyInstId.IndexOf(disk.SerialNumber, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     parentInstId.IndexOf(disk.SerialNumber, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return disk;
                }

                if (!string.IsNullOrEmpty(disk.PhysicalUsbParentInstanceId) &&
                    (proxyInstId.IndexOf(disk.PhysicalUsbParentInstanceId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     parentInstId.IndexOf(disk.PhysicalUsbParentInstanceId, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return disk;
                }

                if (!string.IsNullOrEmpty(disk.PhysicalUsbParentInstanceId) &&
                    !string.IsNullOrEmpty(proxyDev.PhysicalUsbParentInstanceId) &&
                    disk.PhysicalUsbParentInstanceId != "UNKNOWN" &&
                    string.Equals(disk.PhysicalUsbParentInstanceId, proxyDev.PhysicalUsbParentInstanceId, StringComparison.OrdinalIgnoreCase))
                {
                    return disk;
                }
            }

            // Positive owner correlation NOT established. Never guess or rely on arbitrary fallback.
            return null;
        }

        public static bool IsStorageBackedWpdCandidate(DeviceIdentity dev, List<DeviceIdentity> existingDisks)
        {
            if (dev == null) return false;
            if (!HasStructuralStorageEvidence(dev)) return false;
            if (existingDisks == null || existingDisks.Count == 0) return true;
            return FindOwningDiskDrive(dev, existingDisks) != null;
        }

        private static void CorrelateSuppressedProxy(DeviceIdentity proxyDev, List<DeviceIdentity> existingDisks)
        {
            if (proxyDev == null) return;

            lock (LastSuppressedProxies)
            {
                if (!LastSuppressedProxies.Contains(proxyDev))
                {
                    LastSuppressedProxies.Add(proxyDev);
                }
            }

            DeviceIdentity targetDisk = FindOwningDiskDrive(proxyDev, existingDisks);
            if (targetDisk != null && !string.IsNullOrEmpty(proxyDev.DeviceInstanceId))
            {
                if (targetDisk.SuppressedProxyInstanceIds == null)
                {
                    targetDisk.SuppressedProxyInstanceIds = new List<string>();
                }
                if (!targetDisk.SuppressedProxyInstanceIds.Contains(proxyDev.DeviceInstanceId))
                {
                    targetDisk.SuppressedProxyInstanceIds.Add(proxyDev.DeviceInstanceId);
                }
            }
        }

        public static List<DeviceIdentity> DeduplicateCrossLayerStorageProxies(List<DeviceIdentity> devices)
        {
            if (devices == null) return new List<DeviceIdentity>();

            List<DeviceIdentity> disks = new List<DeviceIdentity>();
            foreach (var d in devices)
            {
                if (d.WindowsClass == StorageClass.DiskDrive)
                    disks.Add(d);
            }

            List<DeviceIdentity> filtered = new List<DeviceIdentity>();
            foreach (var d in devices)
            {
                // Never return confirmed storage volume proxies for enforcement
                if (d.WpdClassification == WpdKind.StorageVolumeProxy)
                {
                    continue;
                }

                // If proxy was previously unresolved, re-attempt positive owner resolution against final disk list
                if (d.WpdClassification == WpdKind.StorageVolumeProxyUnresolved)
                {
                    DeviceIdentity owner = FindOwningDiskDrive(d, disks);
                    if (owner != null)
                    {
                        d.WpdClassification = WpdKind.StorageVolumeProxy;
                        CorrelateSuppressedProxy(d, disks);
                        lock (LastUnresolvedStorageProxies)
                        {
                            LastUnresolvedStorageProxies.Remove(d);
                        }
                        continue; // Now positively correlated & suppressed
                    }

                    // Still unresolved: Retain in filtered list so it is not silently lost
                    filtered.Add(d);
                    continue;
                }

                // Check portable devices against discovered disks
                if (d.WindowsClass == StorageClass.WindowsPortableDevice)
                {
                    if (HasStructuralStorageEvidence(d))
                    {
                        DeviceIdentity owner = FindOwningDiskDrive(d, disks);
                        if (owner != null)
                        {
                            d.WpdClassification = WpdKind.StorageVolumeProxy;
                            CorrelateSuppressedProxy(d, disks);
                            continue;
                        }
                        else
                        {
                            d.WpdClassification = WpdKind.StorageVolumeProxyUnresolved;
                            lock (LastUnresolvedStorageProxies)
                            {
                                if (!LastUnresolvedStorageProxies.Contains(d))
                                {
                                    LastUnresolvedStorageProxies.Add(d);
                                }
                            }
                            filtered.Add(d);
                            continue;
                        }
                    }
                }

                filtered.Add(d);
            }

            return filtered;
        }

        private static void QueryStorageProperties(DeviceIdentity dev)
        {
            string diskPath = @"\\.\PhysicalDrive" + dev.PhysicalDiskNumber;
            IntPtr hDevice = CreateFile(diskPath,
                0, // Query only
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (hDevice == IntPtr.Zero || hDevice.ToInt64() == -1) return;

            try
            {
                // Query Seek Penalty (SSD vs HDD)
                STORAGE_PROPERTY_QUERY query = new STORAGE_PROPERTY_QUERY();
                query.PropertyId = 7; // StorageDeviceSeekPenaltyProperty
                query.QueryType = 0;  // PropertyStandardQuery

                DEVICE_SEEK_PENALTY_DESCRIPTOR desc = new DEVICE_SEEK_PENALTY_DESCRIPTOR();
                int outSize = Marshal.SizeOf(desc);
                int bytesReturned;

                bool ok = DeviceIoControl(hDevice,
                    IOCTL_STORAGE_QUERY_PROPERTY,
                    ref query, Marshal.SizeOf(query),
                    ref desc, outSize,
                    out bytesReturned, IntPtr.Zero);

                if (ok)
                {
                    dev.HasSeekPenalty = desc.IncursSeekPenalty;
                }
            }
            catch { }
            finally
            {
                CloseHandle(hDevice);
            }
        }

        private static void MapDriveLetters(DeviceIdentity dev, Dictionary<string, VolumeResolutionEntry> cache = null)
        {
            if (dev.PhysicalDiskNumber < 0) return;

            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        string driveLetter = drive.Name.TrimEnd('\\');
                        VolumeResolutionEntry entry = null;

                        if (cache != null && cache.TryGetValue(driveLetter, out entry))
                        {
                            // Entry already resolved in this single discovery pass
                        }
                        else
                        {
                            entry = new VolumeResolutionEntry { Valid = false, DiskNumber = -1 };
                            string volPath = @"\\.\" + driveLetter;
                            LastVolumeOpensCount++;
                            IntPtr hVol = CreateFile(volPath,
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
                                    STORAGE_DEVICE_NUMBER sdn = new STORAGE_DEVICE_NUMBER();
                                    int bytesReturned;
                                    bool ok = DeviceIoControl(hVol,
                                        IOCTL_STORAGE_GET_DEVICE_NUMBER,
                                        IntPtr.Zero, 0,
                                        ref sdn, Marshal.SizeOf(sdn),
                                        out bytesReturned, IntPtr.Zero);

                                    if (ok)
                                    {
                                        entry.Valid = true;
                                        entry.DiskNumber = sdn.DeviceNumber;
                                        try
                                        {
                                            entry.VolumeLabel = drive.VolumeLabel;
                                        }
                                        catch { }
                                        entry.IsRemovable = (drive.DriveType == DriveType.Removable);
                                    }
                                }
                                finally
                                {
                                    CloseHandle(hVol);
                                }
                            }

                            if (cache != null)
                            {
                                cache[driveLetter] = entry;
                            }
                        }

                        if (entry != null && entry.Valid && entry.DiskNumber == dev.PhysicalDiskNumber)
                        {
                            dev.VolumeMountings.Add(driveLetter);
                            if (!string.IsNullOrEmpty(entry.VolumeLabel))
                            {
                                dev.VolumeLabels[driveLetter] = entry.VolumeLabel;
                            }
                            if (entry.IsRemovable)
                            {
                                dev.IsRemovableMedia = true;
                            }
                        }
                    }
                    catch { continue; }
                }
            }
            catch { }
        }

        private static void DiscoverViaDriveInfo(List<DeviceIdentity> list)
        {
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType == DriveType.Removable)
                    {
                        DeviceIdentity dev = new DeviceIdentity();
                        dev.FriendlyName = string.IsNullOrEmpty(drive.VolumeLabel) ? "Removable USB Drive" : drive.VolumeLabel;
                        dev.PhysicalTransport = PhysicalBusType.Usb;
                        dev.WindowsClass = StorageClass.DiskDrive;
                        dev.IsRemovableMedia = true;
                        dev.VolumeMountings.Add(drive.Name.TrimEnd('\\'));
                        list.Add(dev);
                    }
                }
            }
            catch { }
        }

        private static string GetDevicePropertyString(IntPtr hDevInfo, ref SP_DEVINFO_DATA devInfo, uint property)
        {
            uint propType;
            uint requiredSize = 0;
            SetupDiGetDeviceRegistryProperty(hDevInfo, ref devInfo, property, out propType, null, 0, out requiredSize);
            if (requiredSize == 0) return null;
            byte[] buffer = new byte[requiredSize];
            if (SetupDiGetDeviceRegistryProperty(hDevInfo, ref devInfo, property, out propType, buffer, requiredSize, out requiredSize))
            {
                return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
            }
            return null;
        }

        private static string[] GetDevicePropertyMultiString(IntPtr hDevInfo, ref SP_DEVINFO_DATA devInfo, uint property)
        {
            uint propType;
            uint requiredSize = 0;
            SetupDiGetDeviceRegistryProperty(hDevInfo, ref devInfo, property, out propType, null, 0, out requiredSize);
            if (requiredSize == 0) return new string[0];
            byte[] buffer = new byte[requiredSize];
            if (SetupDiGetDeviceRegistryProperty(hDevInfo, ref devInfo, property, out propType, buffer, requiredSize, out requiredSize))
            {
                string raw = Encoding.Unicode.GetString(buffer).TrimEnd('\0');
                return raw.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
            }
            return new string[0];
        }

        // Win32 Definitions
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;
        private const uint IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x002D1080;

        // SetupAPI & CfgMgr32 Definitions
        private static readonly Guid GUID_DEVCLASS_WPD = new Guid("eec5ad98-8080-425f-922a-dab33962f594");
        private const int DIGCF_PRESENT = 0x00000002;
        private const uint SPDRP_DEVICEDESC = 0x00000000;
        private const uint SPDRP_HARDWAREID = 0x00000001;
        private const uint SPDRP_COMPATIBLEIDS = 0x00000002;
        private const uint SPDRP_MFG = 0x0000000B;
        private const uint SPDRP_FRIENDLYNAME = 0x0000000C;

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVINFO_DATA
        {
            public int cbSize;
            public Guid classGuid;
            public uint devInst;
            public IntPtr reserved;
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, int flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr deviceInfoSet,
            ref SP_DEVINFO_DATA deviceInfoData,
            uint property,
            out uint propertyRegDataType,
            byte[] propertyBuffer,
            uint propertyBufferSize,
            out uint requiredSize);

        [DllImport("cfgmgr32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int CM_Get_Device_ID(uint dnDevInst, StringBuilder Buffer, int BufferLen, int ulFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct STORAGE_PROPERTY_QUERY
        {
            public int PropertyId;
            public int QueryType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public byte[] AdditionalParameters;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVICE_SEEK_PENALTY_DESCRIPTOR
        {
            public uint Version;
            public uint Size;
            [MarshalAs(UnmanagedType.I1)]
            public bool IncursSeekPenalty;
        }

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
            ref STORAGE_PROPERTY_QUERY lpInBuffer,
            int nInBufferSize,
            ref DEVICE_SEEK_PENALTY_DESCRIPTOR lpOutBuffer,
            int nOutBufferSize,
            out int lpBytesReturned,
            IntPtr lpOverlapped);

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
    }
}
