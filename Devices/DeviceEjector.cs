// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Safe Device Dismount & Ejection Engine (Transactional)
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UsbShieldPro.Core;
using UsbShieldPro.Models;

namespace UsbShieldPro.Devices
{
    /// <summary>
    /// Executes transactional whole-device volume dismounting and physical media ejection.
    /// Invariant: Passes SystemDeviceSafetyGuard before any I/O control is issued.
    /// Invariant: Resolves all sibling volumes dynamically using IOCTL_STORAGE_GET_DEVICE_NUMBER.
    /// Invariant: Locks ALL sibling volumes before dismounting ANY volume.
    /// Invariant: If any sibling lock fails with transient error, rolls back all acquired locks,
    ///            issues 0 dismounts, 0 media ejects, and returns aggregate Busy.
    /// Invariant: Issues ONE physical media eject operation per device, not N per partition.
    /// Invariant: Post-verifies ALL sibling volumes before committing aggregate Success.
    /// Invariant: If media eject succeeds but any sibling remains readable, returns IncompleteEject.
    /// </summary>
    public static class DeviceEjector
    {
        // ── TEST SEAMS (Strictly gated by DeviceEnforcementLifecycleManager.IsTestMode) ──
        public delegate bool LockVolumeDelegate(string driveLetter, out int win32Error);
        public delegate bool DismountVolumeDelegate(string driveLetter, out int win32Error);
        public delegate bool MediaEjectDelegate(DeviceIdentity dev, out int win32Error);

        public static LockVolumeDelegate LockVolumeSeam = null;
        public static DismountVolumeDelegate DismountVolumeSeam = null;
        public static MediaEjectDelegate MediaEjectSeam = null;
        public static Func<string, Tuple<bool, int>> OpenVolumeSeam = null;
        public static Func<string, bool> PostVerifyVolumeSeam = null;
        public static Func<string, Tuple<bool, int>> PostVerifyIoctlSeam = null;
        public static Func<string, VolumeAvailabilityResult> PostVerifyTriStateSeam = null;
        public static Func<DeviceIdentity, List<string>> SiblingVolumeResolutionSeam = null;
        public static Func<string, int?> StorageDeviceNumberSeam = null;
        public static Action<string> FilesystemAccessDetectorSeam = null;

        public static void ResetSeams()
        {
            LockVolumeSeam = null;
            DismountVolumeSeam = null;
            MediaEjectSeam = null;
            OpenVolumeSeam = null;
            PostVerifyVolumeSeam = null;
            PostVerifyIoctlSeam = null;
            PostVerifyTriStateSeam = null;
            SiblingVolumeResolutionSeam = null;
            StorageDeviceNumberSeam = null;
            FilesystemAccessDetectorSeam = null;
        }

        public static bool HasActiveSeams()
        {
            return DeviceEnforcementLifecycleManager.IsTestMode && (
                LockVolumeSeam != null ||
                DismountVolumeSeam != null ||
                MediaEjectSeam != null ||
                OpenVolumeSeam != null ||
                PostVerifyVolumeSeam != null ||
                PostVerifyIoctlSeam != null ||
                PostVerifyTriStateSeam != null ||
                SiblingVolumeResolutionSeam != null ||
                StorageDeviceNumberSeam != null ||
                FilesystemAccessDetectorSeam != null);
        }

        public static bool EjectDevice(DeviceIdentity device, out string statusMessage, SentinelArrivalLatencyRecord latencyRecord = null)
        {
            DeviceEjectResult res;
            return EjectDevice(device, out res, out statusMessage, latencyRecord);
        }

        public static bool EjectDevice(DeviceIdentity device, out DeviceEjectResult result, out string statusMessage, SentinelArrivalLatencyRecord latencyRecord = null)
        {
            var opResult = EjectDeviceTransactional(device, latencyRecord);
            result = opResult.AggregateResult;
            statusMessage = opResult.StatusMessage;
            return opResult.Success;
        }

        /// <summary>
        /// Authoritative whole-device multi-volume transactional eject engine.
        /// </summary>
        public static DeviceEjectOperationResult EjectDeviceTransactional(DeviceIdentity device, SentinelArrivalLatencyRecord latencyRecord = null)
        {
            var opResult = new DeviceEjectOperationResult();

            if (device == null)
            {
                opResult.AggregateResult = DeviceEjectResult.Failed;
                opResult.Success = false;
                opResult.StatusMessage = "Device reference was null.";
                return opResult;
            }

            // 1. Mandatory Safety Guard Verification
            string guardReason;
            SafetyValidationResult safety = SystemDeviceSafetyGuard.ValidateDevice(device, out guardReason);
            if (safety != SafetyValidationResult.Passed)
            {
                opResult.AggregateResult = DeviceEjectResult.SafetyRejected;
                opResult.Success = false;
                opResult.StatusMessage = "SAFETY GUARD BLOCKED EJECT: " + guardReason;
                return opResult;
            }

            // 2. WPD Devices (Phones/Cameras) have no drive letters; handled via PnP/Interface
            if (device.WindowsClass == StorageClass.WindowsPortableDevice)
            {
                opResult.AggregateResult = DeviceEjectResult.NotEjectable;
                opResult.Success = false;
                opResult.StatusMessage = "Portable device access is governed by WPD policy.";
                return opResult;
            }

            // Record historical volume count before dynamic resolution
            int historicalVolumeCount = (device.VolumeMountings != null) ? device.VolumeMountings.Count : 0;

            // ── PHASE A: RESOLVE CURRENT SIBLING VOLUMES ──
            List<string> siblingVolumes = ResolveCurrentSiblingVolumes(device);
            opResult.SiblingVolumes = new List<string>(siblingVolumes);

            if (siblingVolumes.Count == 0)
            {
                // Invariant: If device originally had mounted sibling volumes,
                // transient enumeration failure must NOT auto-succeed or bypass transaction!
                if (historicalVolumeCount > 0)
                {
                    opResult.AggregateResult = DeviceEjectResult.Failed;
                    opResult.Success = false;
                    opResult.StatusMessage = "Cannot positively verify current ownership of any sibling volumes.";
                    return opResult;
                }

                opResult.AggregateResult = DeviceEjectResult.NotEjectable;
                opResult.Success = false;
                opResult.StatusMessage = "No mapped volume drive letters found for device.";
                return opResult;
            }

            // Update device.VolumeMountings to reflect positively verified current sibling volumes
            device.VolumeMountings = new List<string>(siblingVolumes);

            Dictionary<string, VolumeEjectDetail> detailsMap = new Dictionary<string, VolumeEjectDetail>(StringComparer.OrdinalIgnoreCase);
            foreach (var vol in siblingVolumes)
            {
                var detail = new VolumeEjectDetail(vol);
                detailsMap[vol] = detail;
                opResult.VolumeResults.Add(detail);
            }

            // ── PHASE B: LOCK ALL SIBLING VOLUMES BEFORE DISMOUNTING ANY ──
            List<Tuple<string, IntPtr>> acquiredLocks = new List<Tuple<string, IntPtr>>();
            int openedHandlesClosedOnLockFailure = 0;
            try
            {
                bool allLocksAcquired = true;
                bool anyTransientBusy = false;
                bool anyRetryableAccessDenied = false;
                bool allAlreadyGone = true;
                int firstLockError = 0;

                foreach (string driveLetter in siblingVolumes)
                {
                    var detail = detailsMap[driveLetter];
                    string cleanLetter = driveLetter.TrimEnd('\\');
                    string volPath = @"\\.\" + cleanLetter;

                    detail.OpenAttempted = true;
                    IntPtr hVol = IntPtr.Zero;
                    int openErr = 0;

                    if (DeviceEnforcementLifecycleManager.IsTestMode && OpenVolumeSeam != null)
                    {
                        var seamRes = OpenVolumeSeam(cleanLetter);
                        if (seamRes.Item1)
                        {
                            detail.OpenSucceeded = true;
                            hVol = (IntPtr)1234;
                        }
                        else
                        {
                            detail.OpenSucceeded = false;
                            openErr = seamRes.Item2;
                            detail.OpenWin32Error = openErr;
                            detail.Win32Error = openErr;
                        }
                    }
                    else if (DeviceEnforcementLifecycleManager.IsTestMode && LockVolumeSeam != null)
                    {
                        // Open mock success in test mode when LockVolumeSeam is provided
                        detail.OpenSucceeded = true;
                        hVol = (IntPtr)1234;
                    }
                    else
                    {
                        hVol = CreateFile(volPath,
                            GENERIC_READ | GENERIC_WRITE,
                            FILE_SHARE_READ | FILE_SHARE_WRITE,
                            IntPtr.Zero,
                            OPEN_EXISTING,
                            0,
                            IntPtr.Zero);

                        if (hVol == IntPtr.Zero || hVol.ToInt64() == -1)
                        {
                            openErr = Marshal.GetLastWin32Error();
                            detail.OpenSucceeded = false;
                            detail.OpenWin32Error = openErr;
                            detail.Win32Error = openErr;
                        }
                        else
                        {
                            detail.OpenSucceeded = true;
                        }
                    }

                    if (!detail.OpenSucceeded)
                    {
                        if (firstLockError == 0) firstLockError = openErr;
                        detail.LockAcquired = false;

                        if (openErr == 32 || openErr == 33 || openErr == 170)
                        {
                            anyTransientBusy = true;
                            allAlreadyGone = false;
                            detail.Status = "OpenBusy";
                        }
                        else if (openErr == 5)
                        {
                            // Contextual Error 5 during pre-eject open: transient/retryable
                            anyRetryableAccessDenied = true;
                            allAlreadyGone = false;
                            detail.Status = "OpenAccessDenied";
                            detail.Availability = VolumeAvailabilityResult.Available;
                        }
                        else if (openErr == 2 || openErr == 3 || openErr == 21 || openErr == 1112)
                        {
                            detail.Status = "AlreadyGone";
                            detail.Availability = VolumeAvailabilityResult.UnavailableConfirmed;
                            detail.PostVerifyUnavailable = true;
                        }
                        else
                        {
                            allAlreadyGone = false;
                            detail.Status = "OpenFailed";
                            detail.Availability = VolumeAvailabilityResult.Indeterminate;
                        }

                        allLocksAcquired = false;
                        break;
                    }

                    // Open succeeded, now attempt FSCTL_LOCK_VOLUME
                    detail.LockAttempted = true;
                    bool locked = false;
                    int lockErr = 0;

                    if (DeviceEnforcementLifecycleManager.IsTestMode && LockVolumeSeam != null)
                    {
                        locked = LockVolumeSeam(cleanLetter, out lockErr);
                    }
                    else
                    {
                        uint bytesReturned;
                        locked = DeviceIoControl(hVol, FSCTL_LOCK_VOLUME,
                            IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);
                        lockErr = locked ? 0 : Marshal.GetLastWin32Error();
                    }

                    detail.LockSucceeded = locked;
                    detail.LockWin32Error = lockErr;

                    if (!locked)
                    {
                        if (hVol != IntPtr.Zero && hVol != (IntPtr)1234)
                        {
                            CloseHandle(hVol);
                        }
                        if (hVol != IntPtr.Zero)
                        {
                            openedHandlesClosedOnLockFailure++;
                        }
                        detail.Win32Error = lockErr;
                        if (firstLockError == 0) firstLockError = lockErr;
                        detail.LockAcquired = false;

                        if (lockErr == 32 || lockErr == 33 || lockErr == 170)
                        {
                            anyTransientBusy = true;
                            allAlreadyGone = false;
                            detail.Status = "LockBusy";
                        }
                        else if (lockErr == 5)
                        {
                            // Contextual Error 5 during pre-eject lock: transient/retryable
                            anyRetryableAccessDenied = true;
                            allAlreadyGone = false;
                            detail.Status = "LockAccessDenied";
                            detail.Availability = VolumeAvailabilityResult.Available;
                        }
                        else if (lockErr == 21 || lockErr == 1112 || lockErr == 2 || lockErr == 3)
                        {
                            detail.Status = "AlreadyGone";
                            detail.Availability = VolumeAvailabilityResult.UnavailableConfirmed;
                            detail.PostVerifyUnavailable = true;
                        }
                        else
                        {
                            allAlreadyGone = false;
                            detail.Status = "LockFailed";
                            detail.Availability = VolumeAvailabilityResult.Indeterminate;
                        }

                        allLocksAcquired = false;
                        break;
                    }

                    detail.LockAcquired = true;
                    detail.Status = "Locked";
                    allAlreadyGone = false;
                    acquiredLocks.Add(new Tuple<string, IntPtr>(cleanLetter, hVol));

                    if (latencyRecord != null && latencyRecord.S5_VolumeLockAcquiredTicks == 0)
                    {
                        latencyRecord.S5_VolumeLockAcquiredTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                    }
                }

                if (!allLocksAcquired)
                {
                    // Rollback every acquired lock in guaranteed cleanup
                    int handlesReleased = 0;
                    foreach (var tuple in acquiredLocks)
                    {
                        if (tuple.Item2 != IntPtr.Zero)
                        {
                            if (tuple.Item2 != (IntPtr)1234)
                            {
                                CloseHandle(tuple.Item2);
                            }
                            handlesReleased++;
                        }
                    }
                    acquiredLocks.Clear();

                    opResult.HandlesReleasedOnRollback = handlesReleased;
                    opResult.OpenedHandlesClosedOnLockFailure = openedHandlesClosedOnLockFailure;
                    opResult.DismountsIssuedOnRollback = 0;
                    opResult.MediaEjectsIssuedOnRollback = 0;
                    opResult.Win32Error = firstLockError;

                    if (allAlreadyGone && !anyTransientBusy && !anyRetryableAccessDenied)
                    {
                        bool allConfirmedGone = true;
                        foreach (var vol in siblingVolumes)
                        {
                            bool ioctlRet;
                            int err;
                            if (CheckVolumeAvailability(vol, out ioctlRet, out err) != VolumeAvailabilityResult.UnavailableConfirmed)
                            {
                                allConfirmedGone = false;
                                break;
                            }
                        }

                        if (allConfirmedGone)
                        {
                            opResult.AggregateResult = DeviceEjectResult.DeviceGone;
                            opResult.PostVerificationPassed = true;
                            opResult.Success = true;
                            opResult.StatusMessage = "Device on all volumes is no longer present or has no media.";
                            return opResult;
                        }
                    }

                    if (anyTransientBusy)
                    {
                        opResult.AggregateResult = DeviceEjectResult.Busy;
                        opResult.RetryClassification = "Busy";
                        opResult.Success = false;
                        opResult.StatusMessage = "Safe eject failed because volume is in active use (Win32 " + firstLockError + ").";
                        return opResult;
                    }

                    if (anyRetryableAccessDenied)
                    {
                        opResult.AggregateResult = DeviceEjectResult.RetryableAccessDenied;
                        opResult.RetryClassification = "RetryableAccessDenied";
                        opResult.Success = false;
                        opResult.StatusMessage = "Safe eject encountered access denied during pre-eject volume open/lock (Win32 5).";
                        return opResult;
                    }

                    opResult.AggregateResult = (firstLockError == 5) ? DeviceEjectResult.AccessDenied : DeviceEjectResult.Failed;
                    opResult.RetryClassification = (firstLockError == 5) ? "PermanentAccessDenied" : "Other";
                    opResult.Success = false;
                    opResult.StatusMessage = "Failed to lock all sibling volumes (Win32 " + firstLockError + ").";
                    return opResult;
                }

                // ── PHASE C: DISMOUNT ALL LOCKED SIBLING VOLUMES ──
                bool allDismountsSucceeded = true;
                int dismountError = 0;

                foreach (var tuple in acquiredLocks)
                {
                    var detail = detailsMap[tuple.Item1];

                    if (DeviceEnforcementLifecycleManager.IsTestMode && DismountVolumeSeam != null)
                    {
                        int seamErr;
                        bool seamDismounted = DismountVolumeSeam(tuple.Item1, out seamErr);
                        if (seamDismounted)
                        {
                            detail.DismountSucceeded = true;
                            detail.DismountWin32Error = 0;
                            continue;
                        }
                        else
                        {
                            detail.DismountSucceeded = false;
                            detail.DismountWin32Error = seamErr;
                            detail.Win32Error = seamErr;
                            dismountError = seamErr;
                            allDismountsSucceeded = false;
                            break;
                        }
                    }

                    uint bytesReturned;
                    bool dismounted = DeviceIoControl(tuple.Item2, FSCTL_DISMOUNT_VOLUME,
                        IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);
                    int dErr = dismounted ? 0 : Marshal.GetLastWin32Error();

                    if (dismounted)
                    {
                        detail.DismountSucceeded = true;
                        detail.DismountWin32Error = 0;
                    }
                    else
                    {
                        detail.DismountSucceeded = false;
                        detail.DismountWin32Error = dErr;
                        detail.Win32Error = dErr;
                        dismountError = dErr;
                        allDismountsSucceeded = false;
                        break;
                    }
                }

                if (latencyRecord != null && latencyRecord.S6_DismountCompletedTicks == 0)
                {
                    latencyRecord.S6_DismountCompletedTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                }

                if (!allDismountsSucceeded)
                {
                    foreach (var tuple in acquiredLocks)
                    {
                        if (tuple.Item2 != IntPtr.Zero && tuple.Item2 != (IntPtr)1234) CloseHandle(tuple.Item2);
                    }
                    acquiredLocks.Clear();

                    opResult.AggregateResult = (dismountError == 32 || dismountError == 33 || dismountError == 170) ? DeviceEjectResult.Busy : DeviceEjectResult.Failed;
                    opResult.Success = false;
                    opResult.Win32Error = dismountError;
                    opResult.StatusMessage = "Dismount failed on sibling volume (Win32 " + dismountError + ").";
                    return opResult;
                }

                // ── PHASE D: ONE MEDIA EJECT ACTION PER PHYSICAL DEVICE ──
                // Invariant: IOCTL_STORAGE_EJECT_MEDIA is submitted once through the selected prepared volume handle: acquiredLocks[0].Item2
                bool mediaEjected = false;
                int ejectError = 0;

                if (DeviceEnforcementLifecycleManager.IsTestMode && MediaEjectSeam != null)
                {
                    mediaEjected = MediaEjectSeam(device, out ejectError);
                }
                else
                {
                    if (acquiredLocks.Count > 0 && acquiredLocks[0].Item2 != IntPtr.Zero && acquiredLocks[0].Item2 != (IntPtr)1234)
                    {
                        uint bytesReturned;
                        mediaEjected = DeviceIoControl(acquiredLocks[0].Item2, IOCTL_STORAGE_EJECT_MEDIA,
                            IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);
                        ejectError = mediaEjected ? 0 : Marshal.GetLastWin32Error();
                    }
                }

                opResult.MediaEjectIssued = true;
                opResult.Win32Error = ejectError;

                if (latencyRecord != null && latencyRecord.S7_EjectRequestCompletedTicks == 0)
                {
                    latencyRecord.S7_EjectRequestCompletedTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                }

                // ── PHASE E: POST-EJECT VERIFICATION (NON-REMOUNTING, FAIL-CLOSED) ──
                // Invariant: Verify using already-open prepared volume handles BEFORE closing them!
                // Invariant: Zero filesystem root traversal, zero Directory.*, zero DriveInfo.
                // Invariant: Only explicitly recognized terminal absence errors (21, 1112, 2, 3) yield UnavailableConfirmed.
                // Invariant: ANY unknown or unexpected error strictly yields Indeterminate (fails closed).
                Dictionary<string, VolumeAvailabilityResult> handleAvailResults = new Dictionary<string, VolumeAvailabilityResult>(StringComparer.OrdinalIgnoreCase);

                foreach (var tuple in acquiredLocks)
                {
                    string driveLetter = tuple.Item1;
                    var detail = detailsMap[driveLetter];
                    bool ioctlRet = false;
                    int verifyErr = 0;
                    VolumeAvailabilityResult avail = VolumeAvailabilityResult.Indeterminate;

                    if (DeviceEnforcementLifecycleManager.IsTestMode && PostVerifyTriStateSeam != null)
                    {
                        avail = PostVerifyTriStateSeam(driveLetter);
                        ioctlRet = (avail == VolumeAvailabilityResult.Available);
                        verifyErr = (avail == VolumeAvailabilityResult.UnavailableConfirmed) ? 21 : 0;
                    }
                    else if (DeviceEnforcementLifecycleManager.IsTestMode && PostVerifyIoctlSeam != null)
                    {
                        var seamTuple = PostVerifyIoctlSeam(driveLetter);
                        ioctlRet = seamTuple.Item1;
                        verifyErr = seamTuple.Item2;
                        if (!ioctlRet)
                        {
                            if (verifyErr == 21 || verifyErr == 1112 || verifyErr == 2 || verifyErr == 3)
                            {
                                avail = VolumeAvailabilityResult.UnavailableConfirmed;
                            }
                            else
                            {
                                avail = VolumeAvailabilityResult.Indeterminate;
                            }
                        }
                        else
                        {
                            avail = VolumeAvailabilityResult.Available;
                        }
                    }
                    else if (DeviceEnforcementLifecycleManager.IsTestMode && PostVerifyVolumeSeam != null)
                    {
                        bool unavail = PostVerifyVolumeSeam(driveLetter);
                        ioctlRet = !unavail;
                        verifyErr = unavail ? 21 : 0;
                        avail = unavail ? VolumeAvailabilityResult.UnavailableConfirmed : VolumeAvailabilityResult.Available;
                    }
                    else if (tuple.Item2 != IntPtr.Zero && tuple.Item2 != (IntPtr)1234)
                    {
                        uint bytesReturned;
                        ioctlRet = DeviceIoControl(tuple.Item2, IOCTL_STORAGE_CHECK_VERIFY,
                            IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);
                        if (!ioctlRet)
                        {
                            verifyErr = Marshal.GetLastWin32Error();
                            if (verifyErr == 21 || verifyErr == 1112 || verifyErr == 2 || verifyErr == 3)
                            {
                                avail = VolumeAvailabilityResult.UnavailableConfirmed;
                            }
                            else
                            {
                                // Unknown Win32 error fails closed as Indeterminate!
                                avail = VolumeAvailabilityResult.Indeterminate;
                            }
                        }
                        else
                        {
                            verifyErr = 0;
                            avail = VolumeAvailabilityResult.Available;
                        }
                    }

                    detail.IOCTLReturned = ioctlRet;
                    detail.Win32Error = verifyErr;
                    detail.Availability = avail;
                    detail.PostVerifyUnavailable = (avail == VolumeAvailabilityResult.UnavailableConfirmed);
                    handleAvailResults[driveLetter] = avail;
                }

                // Close all prepared lock handles now in guaranteed cleanup
                foreach (var tuple in acquiredLocks)
                {
                    if (tuple.Item2 != IntPtr.Zero && tuple.Item2 != (IntPtr)1234)
                    {
                        CloseHandle(tuple.Item2);
                    }
                }
                acquiredLocks.Clear();

                // Check all sibling volumes: ensure every sibling is confirmed unavailable
                bool allPostVerifiedConfirmed = true;
                bool anyStillAvailable = false;
                bool anyIndeterminate = false;

                foreach (string driveLetter in siblingVolumes)
                {
                    var detail = detailsMap[driveLetter];
                    VolumeAvailabilityResult avail = VolumeAvailabilityResult.Indeterminate;

                    if (handleAvailResults.ContainsKey(driveLetter))
                    {
                        avail = handleAvailResults[driveLetter];
                        // If handle check indicated Available or Indeterminate, re-verify via CheckVolumeAvailability
                        if (avail != VolumeAvailabilityResult.UnavailableConfirmed)
                        {
                            bool checkIoctlRet;
                            int checkErr;
                            var postAvail = CheckVolumeAvailability(driveLetter, out checkIoctlRet, out checkErr);
                            if (postAvail == VolumeAvailabilityResult.UnavailableConfirmed)
                            {
                                avail = postAvail;
                                detail.IOCTLReturned = checkIoctlRet;
                                detail.Win32Error = checkErr;
                                detail.Availability = avail;
                                detail.PostVerifyUnavailable = true;
                            }
                        }
                    }
                    else
                    {
                        bool checkIoctlRet;
                        int checkErr;
                        avail = CheckVolumeAvailability(driveLetter, out checkIoctlRet, out checkErr);
                        detail.IOCTLReturned = checkIoctlRet;
                        detail.Win32Error = checkErr;
                        detail.Availability = avail;
                        detail.PostVerifyUnavailable = (avail == VolumeAvailabilityResult.UnavailableConfirmed);
                    }

                    if (avail != VolumeAvailabilityResult.UnavailableConfirmed)
                    {
                        allPostVerifiedConfirmed = false;
                    }
                    if (avail == VolumeAvailabilityResult.Available)
                    {
                        anyStillAvailable = true;
                    }
                    if (avail == VolumeAvailabilityResult.Indeterminate)
                    {
                        anyIndeterminate = true;
                    }
                }

                opResult.PostVerificationPassed = allPostVerifiedConfirmed;

                // ── AGGREGATE OUTCOME DETERMINATION ──
                if (mediaEjected)
                {
                    if (allPostVerifiedConfirmed)
                    {
                        opResult.AggregateResult = DeviceEjectResult.Success;
                        opResult.Success = true;
                        opResult.StatusMessage = "Successfully unmounted and ejected " + device.FriendlyName;
                    }
                    else if (anyStillAvailable)
                    {
                        opResult.AggregateResult = DeviceEjectResult.IncompleteEject;
                        opResult.Success = false;
                        opResult.StatusMessage = "Untrusted device was detected, but automatic ejection did not complete.";
                    }
                    else // anyIndeterminate
                    {
                        opResult.AggregateResult = DeviceEjectResult.VerificationIndeterminate;
                        opResult.Success = false;
                        opResult.StatusMessage = "Untrusted device was detected, but automatic ejection verification was indeterminate.";
                    }
                }
                else if (ejectError == 21 || ejectError == 1112)
                {
                    // Media was already ejected or not present when media eject was issued
                    if (allPostVerifiedConfirmed)
                    {
                        opResult.AggregateResult = DeviceEjectResult.DeviceGone;
                        opResult.Success = true;
                        opResult.StatusMessage = "Media already ejected or no media present.";
                    }
                    else if (anyStillAvailable)
                    {
                        opResult.AggregateResult = DeviceEjectResult.IncompleteEject;
                        opResult.Success = false;
                        opResult.StatusMessage = "Untrusted device was detected, but automatic ejection did not complete.";
                    }
                    else
                    {
                        opResult.AggregateResult = DeviceEjectResult.VerificationIndeterminate;
                        opResult.Success = false;
                        opResult.StatusMessage = "Device media check failed and verification is indeterminate.";
                    }
                }
                else
                {
                    // Media eject IOCTL failed with an error
                    if (allPostVerifiedConfirmed)
                    {
                        // Media eject failed, but all siblings are confirmed unavailable (DeviceGone)
                        opResult.AggregateResult = (ejectError == 0) ? DeviceEjectResult.DeviceGone :
                                                  (ejectError == 5) ? DeviceEjectResult.AccessDenied : DeviceEjectResult.Failed;
                        opResult.Success = (opResult.AggregateResult == DeviceEjectResult.DeviceGone);
                        opResult.StatusMessage = opResult.Success ? "Media already ejected or no media present." :
                            ("Media eject IOCTL failed for device (Win32 " + ejectError + ").");
                    }
                    else if (anyIndeterminate)
                    {
                        opResult.AggregateResult = DeviceEjectResult.VerificationIndeterminate;
                        opResult.Success = false;
                        opResult.StatusMessage = "Media eject IOCTL failed and verification is indeterminate.";
                    }
                    else
                    {
                        opResult.AggregateResult = (ejectError == 5) ? DeviceEjectResult.AccessDenied :
                                                  (ejectError == 32 || ejectError == 33 || ejectError == 170) ? DeviceEjectResult.Busy :
                                                  DeviceEjectResult.Failed;
                        opResult.Success = false;
                        opResult.StatusMessage = "Media eject IOCTL failed for device (Win32 " + ejectError + ").";
                    }
                }

                return opResult;
            }
            finally
            {
                // Guaranteed cleanup: ensures 0 handle leaks under any path
                foreach (var tuple in acquiredLocks)
                {
                    if (tuple.Item2 != IntPtr.Zero && tuple.Item2 != (IntPtr)1234)
                    {
                        CloseHandle(tuple.Item2);
                    }
                }
                acquiredLocks.Clear();
            }
        }

        /// <summary>
        /// Positively queries the physical disk number for a volume using IOCTL_STORAGE_GET_DEVICE_NUMBER.
        /// Non-remounting: opens the volume device node (\\.\X:), NOT the filesystem root (X:\).
        /// Returns null if query fails or handle cannot be opened.
        /// </summary>
        public static int? GetDiskNumberForVolume(string driveLetter)
        {
            if (string.IsNullOrEmpty(driveLetter)) return null;
            string clean = driveLetter.TrimEnd('\\');

            if (DeviceEnforcementLifecycleManager.IsTestMode && StorageDeviceNumberSeam != null)
            {
                return StorageDeviceNumberSeam(clean);
            }

            string volPath = @"\\.\" + clean;
            IntPtr hVol = CreateFile(volPath,
                0, // Query access only - strictly non-remounting
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (hVol == IntPtr.Zero || hVol.ToInt64() == -1)
            {
                return null;
            }

            try
            {
                IntPtr pSdn = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(STORAGE_DEVICE_NUMBER)));
                try
                {
                    uint bytesReturned;
                    bool ok = DeviceIoControl(hVol, IOCTL_STORAGE_GET_DEVICE_NUMBER,
                        IntPtr.Zero, 0, pSdn, Marshal.SizeOf(typeof(STORAGE_DEVICE_NUMBER)),
                        out bytesReturned, IntPtr.Zero);
                    if (ok)
                    {
                        STORAGE_DEVICE_NUMBER sdn = (STORAGE_DEVICE_NUMBER)Marshal.PtrToStructure(pSdn, typeof(STORAGE_DEVICE_NUMBER));
                        return sdn.DeviceNumber;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(pSdn);
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                CloseHandle(hVol);
            }

            return null;
        }

        /// <summary>
        /// Resolves all current mounted sibling volumes belonging to the specified physical disk.
        /// Positive current disk ownership via IOCTL_STORAGE_GET_DEVICE_NUMBER is mandatory.
        /// Invariant: Stale or reassigned drive letters are NEVER added to VerifiedCurrentSiblingVolumes.
        /// </summary>
        public static List<string> ResolveCurrentSiblingVolumes(DeviceIdentity device)
        {
            if (DeviceEnforcementLifecycleManager.IsTestMode && SiblingVolumeResolutionSeam != null)
            {
                return SiblingVolumeResolutionSeam(device);
            }

            var verified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int targetDiskNumber = device != null ? device.PhysicalDiskNumber : -1;

            if (targetDiskNumber < 0)
            {
                // Positive disk ownership is mandatory before destructive I/O.
                return new List<string>();
            }

            // 1. Scan currently mounted system drives
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    string letter = drive.Name.TrimEnd('\\');
                    int? diskNum = GetDiskNumberForVolume(letter);
                    if (diskNum.HasValue && diskNum.Value == targetDiskNumber)
                    {
                        verified.Add(letter);
                    }
                }
            }
            catch { }

            // 2. Cross-reference historical VolumeMountings on device
            // INVARIANT: ONLY add if POSITIVELY verified to currently belong to targetDiskNumber!
            // Stale drive letters reassigned to foreign disks (or where IOCTL fails) MUST NOT be added!
            if (device != null && device.VolumeMountings != null)
            {
                foreach (string origLetter in device.VolumeMountings)
                {
                    if (string.IsNullOrEmpty(origLetter)) continue;
                    string clean = origLetter.TrimEnd('\\');
                    int? diskNum = GetDiskNumberForVolume(clean);
                    if (diskNum.HasValue && diskNum.Value == targetDiskNumber)
                    {
                        verified.Add(clean);
                    }
                    // Note: NO fallback to adding unverified origLetter!
                }
            }

            List<string> result = new List<string>(verified);
            result.Sort(StringComparer.OrdinalIgnoreCase);

            return result;
        }

        /// <summary>
        /// Strictly non-remounting volume availability check returning tri-state VolumeAvailabilityResult.
        /// Invariant: NEVER touches filesystem root (e.g. F:\).
        /// Invariant: NEVER calls Directory.*, File.*, or DriveInfo.
        /// Invariant: Queries storage device object (\\.\F:) via IOCTL_STORAGE_CHECK_VERIFY.
        /// Invariant: Only explicitly recognized terminal absence/no-media errors (21, 1112, 2, 3) yield UnavailableConfirmed.
        /// Invariant: ANY unknown or unexpected error strictly yields Indeterminate (fails closed).
        /// </summary>
        public static VolumeAvailabilityResult CheckVolumeAvailability(string driveLetter, out bool ioctlReturned, out int win32Error)
        {
            ioctlReturned = false;
            win32Error = 0;

            if (DeviceEnforcementLifecycleManager.IsTestMode && PostVerifyTriStateSeam != null)
            {
                var seamRes = PostVerifyTriStateSeam(driveLetter);
                ioctlReturned = (seamRes == VolumeAvailabilityResult.Available);
                win32Error = (seamRes == VolumeAvailabilityResult.UnavailableConfirmed) ? 21 : 0;
                return seamRes;
            }

            if (DeviceEnforcementLifecycleManager.IsTestMode && PostVerifyIoctlSeam != null)
            {
                var tuple = PostVerifyIoctlSeam(driveLetter);
                ioctlReturned = tuple.Item1;
                win32Error = tuple.Item2;

                if (!ioctlReturned)
                {
                    if (win32Error == 21 || win32Error == 1112 || win32Error == 2 || win32Error == 3)
                    {
                        return VolumeAvailabilityResult.UnavailableConfirmed;
                    }
                    return VolumeAvailabilityResult.Indeterminate;
                }
                return VolumeAvailabilityResult.Available;
            }

            if (DeviceEnforcementLifecycleManager.IsTestMode && PostVerifyVolumeSeam != null)
            {
                bool unavail = PostVerifyVolumeSeam(driveLetter);
                ioctlReturned = !unavail;
                win32Error = unavail ? 21 : 0;
                return unavail ? VolumeAvailabilityResult.UnavailableConfirmed : VolumeAvailabilityResult.Available;
            }

            // Test seam hook: detect any illegal filesystem access
            if (DeviceEnforcementLifecycleManager.IsTestMode && FilesystemAccessDetectorSeam != null)
            {
                // Strictly not called because this method only uses storage IOCTLs
            }

            string clean = (driveLetter ?? "").TrimEnd('\\');
            if (string.IsNullOrEmpty(clean)) return VolumeAvailabilityResult.UnavailableConfirmed;

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
                // Recognized terminal absence errors
                if (win32Error == 2 || win32Error == 3 || win32Error == 21 || win32Error == 1112)
                {
                    return VolumeAvailabilityResult.UnavailableConfirmed;
                }
                else if (win32Error == 5 || win32Error == 32 || win32Error == 33 || win32Error == 170)
                {
                    // Access denied or sharing violation means volume exists and is occupied/mounted
                    return VolumeAvailabilityResult.Available;
                }
                // Unknown open error strictly fails closed as Indeterminate
                return VolumeAvailabilityResult.Indeterminate;
            }

            try
            {
                uint bytesReturned;
                ioctlReturned = DeviceIoControl(hCheck, IOCTL_STORAGE_CHECK_VERIFY,
                    IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);
                if (!ioctlReturned)
                {
                    win32Error = Marshal.GetLastWin32Error();
                    if (win32Error == 21 || win32Error == 1112 || win32Error == 2 || win32Error == 3)
                    {
                        return VolumeAvailabilityResult.UnavailableConfirmed; // Confirmed safely unavailable!
                    }
                    // Unknown or unexpected failure (e.g. 5, 1, 6, 23, 31, 0) strictly fails closed!
                    return VolumeAvailabilityResult.Indeterminate;
                }
                return VolumeAvailabilityResult.Available; // Still ready / mounted
            }
            finally
            {
                CloseHandle(hCheck);
            }
        }

        /// <summary>
        /// Strictly non-remounting boolean helper.
        /// Returns true ONLY IF volume is conclusively confirmed unavailable.
        /// Returns false if volume is still available OR if verification is indeterminate.
        /// </summary>
        public static bool IsVolumeUnavailable(string driveLetter)
        {
            bool ioctlRet;
            int err;
            return CheckVolumeAvailability(driveLetter, out ioctlRet, out err) == VolumeAvailabilityResult.UnavailableConfirmed;
        }

        /// <summary>
        /// Safely ejects all connected removable storage devices that pass SafetyGuard.
        /// </summary>
        public static bool EjectAllRemovable(out string summary)
        {
            var devices = DeviceDiscovery.DiscoverAllDevices();
            int total = 0;
            int succeeded = 0;

            foreach (var dev in devices)
            {
                if (dev.IsRemovableMedia || dev.PhysicalTransport == PhysicalBusType.Usb)
                {
                    string guardReason;
                    if (SystemDeviceSafetyGuard.ValidateDevice(dev, out guardReason) == SafetyValidationResult.Passed)
                    {
                        total++;
                        string msg;
                        DeviceEjectResult res;
                        if (EjectDevice(dev, out res, out msg))
                        {
                            succeeded++;
                        }
                    }
                }
            }

            if (total == 0)
                summary = Loc.Get("EjectAllNoneMsg");
            else if (succeeded == total)
                summary = string.Format(Loc.Get("EjectAllSuccessMsg"), succeeded, total);
            else
                summary = string.Format(Loc.Get("EjectAllPartialMsg"), succeeded, total);
            return total == 0 || succeeded == total;
        }
        // Win32 Definitions
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;

        private const uint FSCTL_LOCK_VOLUME = 0x00090018;
        private const uint FSCTL_DISMOUNT_VOLUME = 0x00090020;
        private const uint IOCTL_STORAGE_EJECT_MEDIA = 0x002D4808;
        private const uint IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x002D1080;
        private const uint IOCTL_STORAGE_CHECK_VERIFY = 0x002D4800;

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
            IntPtr lpOutBuffer,
            int nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
    }
}
