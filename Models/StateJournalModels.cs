// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — State Journal & Baseline Preservation Models
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace UsbShieldPro.Models
{
    /// <summary>
    /// Captures the exact state of a Windows registry value BEFORE USB Shield modified it.
    /// Invariant: The original state is captured ONLY ONCE. Intermediate Shield values
    /// never overwrite this baseline.
    /// </summary>
    public class RegistryOriginalValue
    {
        public string Hive { get; set; }
        public string KeyPath { get; set; }
        public string ValueName { get; set; }
        public string ValueKind { get; set; }
        public bool ExistedBefore { get; set; }
        public object OriginalData { get; set; }
        public object ShieldAppliedData { get; set; }
        public int? AllocatedNumberedIndex { get; set; }
        public DateTime FirstCapturedUtc { get; set; }

        public RegistryOriginalValue()
        {
            FirstCapturedUtc = DateTime.UtcNow;
        }

        public string GetTargetKeyIdentifier()
        {
            return (Hive ?? "") + "\\" + (KeyPath ?? "") + "\\" + (ValueName ?? "");
        }
    }

    /// <summary>
    /// Represents an individual mutation operation in the write-ahead journal.
    /// </summary>
    public class JournalOperation
    {
        public int OperationId { get; set; }
        public string TargetDescription { get; set; }
        public JournalOperationStatus Status { get; set; }
        public DateTime PreparedUtc { get; set; }
        public DateTime? AppliedUtc { get; set; }
        public DateTime? VerifiedUtc { get; set; }
        public RegistryOriginalValue TargetRegistryValue { get; set; }
        public string ErrorMessage { get; set; }

        public JournalOperation()
        {
            Status = JournalOperationStatus.Prepared;
            PreparedUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// The canonical state journal persisted on disk in %ProgramData%\USBShieldPro\journal.json.
    /// </summary>
    public class StateJournal
    {
        public string TransactionId { get; set; }
        public string AppVersion { get; set; }
        public DateTime BaselineCapturedUtc { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
        public bool BaselineRestored { get; set; }
        public DateTime? BaselineRestoredUtc { get; set; }

        public List<RegistryOriginalValue> BaselineEntries { get; set; }
        public List<JournalOperation> Operations { get; set; }

        public StateJournal()
        {
            TransactionId = Guid.NewGuid().ToString("D");
            AppVersion = "2.0.0";
            BaselineCapturedUtc = DateTime.UtcNow;
            LastUpdatedUtc = DateTime.UtcNow;
            BaselineRestored = false;
            BaselineEntries = new List<RegistryOriginalValue>();
            Operations = new List<JournalOperation>();
        }
    }
}
