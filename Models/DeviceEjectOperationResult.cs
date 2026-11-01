// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Device Ejection Operation Result Model
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using UsbShieldPro.Devices;

namespace UsbShieldPro.Models
{
    /// <summary>
    /// Detailed execution state of an individual sibling volume during a multi-volume eject transaction.
    /// </summary>
    public class VolumeEjectDetail
    {
        public string DriveLetter { get; set; }
        public bool OpenAttempted { get; set; }
        public bool OpenSucceeded { get; set; }
        public int OpenWin32Error { get; set; }
        public bool LockAttempted { get; set; }
        public bool LockSucceeded { get; set; }
        public int LockWin32Error { get; set; }
        public bool LockAcquired { get; set; }
        public bool DismountSucceeded { get; set; }
        public int DismountWin32Error { get; set; }
        public bool PostVerifyUnavailable { get; set; }
        public bool IOCTLReturned { get; set; }
        public VolumeAvailabilityResult Availability { get; set; }
        public int Win32Error { get; set; }
        public string Status { get; set; }

        public VolumeEjectDetail(string driveLetter)
        {
            DriveLetter = driveLetter;
            OpenAttempted = false;
            OpenSucceeded = false;
            OpenWin32Error = 0;
            LockAttempted = false;
            LockSucceeded = false;
            LockWin32Error = 0;
            LockAcquired = false;
            DismountSucceeded = false;
            DismountWin32Error = 0;
            PostVerifyUnavailable = false;
            IOCTLReturned = false;
            Availability = VolumeAvailabilityResult.Indeterminate;
            Win32Error = 0;
            Status = "Pending";
        }
    }

    /// <summary>
    /// Authoritative aggregate outcome of a transactional multi-volume physical device ejection attempt.
    /// Invariant: Success == true ONLY IF every sibling volume was confirmed safely absent/ejected
    /// and post-verification confirmed ALL siblings UnavailableConfirmed.
    /// Invariant: Success == true iff (AggregateResult == Success || AggregateResult == DeviceGone) && PostVerificationPassed == true.
    /// Invariant: Success == false whenever AggregateResult is Busy, AccessDenied, Failed, IncompleteEject, VerificationIndeterminate, or RetryableAccessDenied.
    /// </summary>
    public class DeviceEjectOperationResult
    {
        public DeviceEjectResult AggregateResult { get; set; }
        public bool Success { get; set; }
        public bool MediaEjectIssued { get; set; }
        public bool PostVerificationPassed { get; set; }
        public List<VolumeEjectDetail> VolumeResults { get; set; }
        public List<string> SiblingVolumes { get; set; }
        public int Win32Error { get; set; }
        public string StatusMessage { get; set; }
        public int HandlesReleasedOnRollback { get; set; }
        public int OpenedHandlesClosedOnLockFailure { get; set; }
        public int DismountsIssuedOnRollback { get; set; }
        public int MediaEjectsIssuedOnRollback { get; set; }
        public string RetryClassification { get; set; }
        public int RetryDelayMs { get; set; }

        public bool IsPnpSafeEject { get; set; }
        public int ConfigRet { get; set; }
        public PNP_VETO_TYPE VetoType { get; set; }
        public string VetoName { get; set; }
        public string TargetInstanceId { get; set; }

        /// <summary>
        /// Explicit property defining successful terminal outcome:
        /// Success or DeviceGone with PostVerificationPassed == true and Success == true.
        /// </summary>
        public bool IsSuccessfulTerminalOutcome
        {
            get
            {
                return (AggregateResult == DeviceEjectResult.Success || AggregateResult == DeviceEjectResult.DeviceGone)
                       && PostVerificationPassed
                       && Success;
            }
        }

        public DeviceEjectOperationResult()
        {
            AggregateResult = DeviceEjectResult.Failed;
            Success = false;
            MediaEjectIssued = false;
            PostVerificationPassed = false;
            VolumeResults = new List<VolumeEjectDetail>();
            SiblingVolumes = new List<string>();
            Win32Error = 0;
            StatusMessage = string.Empty;
            HandlesReleasedOnRollback = 0;
            OpenedHandlesClosedOnLockFailure = 0;
            DismountsIssuedOnRollback = 0;
            MediaEjectsIssuedOnRollback = 0;
            RetryClassification = "None";
            RetryDelayMs = 0;
            IsPnpSafeEject = false;
            ConfigRet = 0;
            VetoType = PNP_VETO_TYPE.PNP_VetoTypeUnknown;
            VetoName = string.Empty;
            TargetInstanceId = string.Empty;
        }

        public string FormatTelemetry(int attemptNumber, string physicalKey, int diskNumber, int generationId = 0, int cycleId = 0)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendFormat("Attempt {0}\n", attemptNumber);
            if (generationId > 0 || cycleId > 0)
            {
                sb.AppendFormat("GenerationId: {0}\n", generationId);
                sb.AppendFormat("EnforcementCycleId: {0}\n", cycleId);
            }
            sb.AppendFormat("PhysicalKey: {0}\n", physicalKey ?? "UNKNOWN");
            sb.AppendFormat("DiskNumber: {0}\n", diskNumber);
            sb.AppendFormat("SiblingVolumes: [{0}]\n", string.Join(", ", SiblingVolumes.ToArray()));

            foreach (var v in VolumeResults)
            {
                if (v.OpenAttempted)
                {
                    sb.AppendFormat("  OPEN {0}: {1} Win32={2}\n",
                        v.DriveLetter, v.OpenSucceeded ? "SUCCESS" : "FAILED", v.OpenWin32Error);
                }
                if (v.LockAttempted)
                {
                    sb.AppendFormat("  LOCK {0}: {1} Win32={2}\n",
                        v.DriveLetter, v.LockSucceeded ? "SUCCESS" : "FAILED", v.LockWin32Error);
                }
                else if (!v.OpenAttempted)
                {
                    sb.AppendFormat("  LOCK {0}: NOT ATTEMPTED\n", v.DriveLetter);
                }
            }

            if (!Success && (AggregateResult == DeviceEjectResult.Busy || AggregateResult == DeviceEjectResult.RetryableAccessDenied || AggregateResult == DeviceEjectResult.AccessDenied || AggregateResult == DeviceEjectResult.Failed))
            {
                sb.AppendFormat("  ROLLBACK:\n");
                sb.AppendFormat("    opened handle closed after failed lock = {0}\n", OpenedHandlesClosedOnLockFailure);
                sb.AppendFormat("    locked handles released on rollback = {0}\n", HandlesReleasedOnRollback);
                sb.AppendFormat("    handles released = {0}\n", HandlesReleasedOnRollback);
                sb.AppendFormat("    dismounts issued = {0}\n", DismountsIssuedOnRollback);
                sb.AppendFormat("    media ejects issued = {0}\n", MediaEjectsIssuedOnRollback);
                sb.AppendFormat("  RETRY_CLASSIFICATION: {0}\n", RetryClassification ?? "None");
                sb.AppendFormat("  RETRY_DELAY_MS: {0}\n", RetryDelayMs);
            }

            foreach (var v in VolumeResults)
            {
                if (v.LockAcquired)
                {
                    if (v.DismountSucceeded)
                    {
                        sb.AppendFormat("  DISMOUNT {0}: SUCCESS\n", v.DriveLetter);
                    }
                    else
                    {
                        sb.AppendFormat("  DISMOUNT {0}: FAILED (Win32: {1})\n",
                            v.DriveLetter, v.DismountWin32Error);
                    }
                }
            }

            sb.AppendFormat("  MEDIA_EJECT: issued={0}, result={1}, Win32Error={2}\n",
                MediaEjectIssued, AggregateResult, Win32Error);

            if (MediaEjectIssued || AggregateResult == DeviceEjectResult.DeviceGone || AggregateResult == DeviceEjectResult.VerificationIndeterminate)
            {
                foreach (var v in VolumeResults)
                {
                    sb.AppendFormat("  POST_VERIFY {0}:\n", v.DriveLetter);
                    sb.AppendFormat("    IOCTLReturned = {0}\n", v.IOCTLReturned);
                    sb.AppendFormat("    Win32Error = {0}\n", v.Win32Error);
                    sb.AppendFormat("    Availability = {0}\n", v.Availability);
                }
                sb.AppendFormat("  POST_VERIFY_ALL_CONFIRMED = {0}\n", PostVerificationPassed);
            }

            if (IsPnpSafeEject)
            {
                sb.AppendFormat("  PNP_EJECT: ConfigRet=0x{0:X8}, VetoType={1} ({2}), VetoName=\"{3}\", Target=\"{4}\"\n",
                    ConfigRet, VetoType, (int)VetoType, VetoName ?? "", TargetInstanceId ?? "");
            }

            sb.AppendFormat("  AGGREGATE: Result={0}, Success={1}, PostVerifyPassed={2}\n",
                AggregateResult, Success, PostVerificationPassed);

            return sb.ToString();
        }
    }
}
