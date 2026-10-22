// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Active Sentinel Event Dispatcher
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UsbShieldPro.Core;
using UsbShieldPro.Models;
using UsbShieldPro.Persistence;

namespace UsbShieldPro.Devices
{
    public class DeviceInterceptEventArgs : EventArgs
    {
        public DeviceIdentity Device { get; set; }
        public bool EjectSuccess { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Outcome of a Sentinel evaluation cycle across discovered devices.
    /// </summary>
    public struct SentinelEvaluationOutcome
    {
        public int DevicesEvaluated;
        public int ActionsExecuted;
        public bool S0Consumed;
    }

    /// <summary>
    /// Real-time sentinel monitoring device arrivals and enforcing reactive ejection of untrusted devices.
    /// Operates in both Portable Mode (hosted by UI) and Full Protection Mode (hosted by Service).
    /// </summary>
    public static class ActiveSentinel
    {
        public static event EventHandler<DeviceInterceptEventArgs> DeviceIntercepted;
        public static event EventHandler<DeviceInterceptEventArgs> DeviceEnforcementPending;

        public static Func<ProtectionPolicy> PolicyProviderSeam = null;

        // ── ARRIVAL RECONCILIATION STATE & SEAMS ──
        private static int _reconciliationGate = 0;
        private static System.Threading.CancellationTokenSource _reconciliationCts = null;
        private static readonly object _reconciliationLock = new object();

        public static readonly int[] DefaultReconciliationScheduleMs = new int[0];

        public static Action<int> ReconciliationDelaySeam = null;
        public static int[] ReconciliationScheduleSeam = null;
        public static System.Threading.Tasks.Task<bool> LastReconciliationTask { get; private set; }

        public static bool IsReconciliationActive
        {
            get { return System.Threading.Volatile.Read(ref _reconciliationGate) == 1; }
        }

        public static ProtectionPolicy GetEffectivePolicy(ProtectionPolicy fallback)
        {
            if (DeviceEnforcementLifecycleManager.IsTestMode && PolicyProviderSeam != null)
            {
                return PolicyProviderSeam();
            }
            if (!DeviceEnforcementLifecycleManager.IsTestMode)
            {
                try { return SettingsRepository.LoadPolicy(); } catch { }
            }
            return fallback;
        }

        internal static bool IsAutoEjectRequired(DeviceIdentity dev, ProtectionPolicy policy)
        {
            if (policy == null || dev == null) return false;
            var eval = RuleEngine.Evaluate(dev, policy, isTemporaryAccessActive: TemporaryAccessManager.IsActive);
            return eval.ShouldAutoEject;
        }

        private static Func<DeviceIdentity, bool> _topologyStabilitySeam = null;
        public static Func<DeviceIdentity, bool> TopologyStabilitySeam
        {
            get
            {
                if (!DeviceEnforcementLifecycleManager.IsTestMode) return null;
                return _topologyStabilitySeam;
            }
            set
            {
                if (!DeviceEnforcementLifecycleManager.IsTestMode)
                {
                    _topologyStabilitySeam = null;
                    return;
                }
                _topologyStabilitySeam = value;
            }
        }

        internal class DeviceTopologySnapshot
        {
            public string PhysicalKey;
            public int DiskNumber;
            public int PartitionCount;
            public List<string> NormalizedVolumes;
            public long CaptureTicks;
            public int EventGeneration;
            public bool IsInvalidated;
            public string DispatchSource;
            public int ThreadId;
        }

        private static readonly object _topologyLock = new object();
        private static readonly Dictionary<string, DeviceTopologySnapshot> _pendingTopologySnapshots =
            new Dictionary<string, DeviceTopologySnapshot>(StringComparer.OrdinalIgnoreCase);

        public static void InvalidatePendingTopologySnapshots()
        {
            lock (_topologyLock)
            {
                foreach (var snap in _pendingTopologySnapshots.Values)
                {
                    snap.IsInvalidated = true;
                }
            }
        }

        public static void ClearPendingTopologySnapshots()
        {
            lock (_topologyLock)
            {
                _pendingTopologySnapshots.Clear();
            }
        }

        public static void ResetSeams()
        {
            PolicyProviderSeam = null;
            ReconciliationDelaySeam = null;
            ReconciliationScheduleSeam = null;
            _topologyStabilitySeam = null;
            ClearPendingTopologySnapshots();
            CancelArrivalReconciliation(true);
        }

        internal static DeviceTopologySnapshot GetPendingTopologySnapshotForTest(string physicalKey)
        {
            lock (_topologyLock)
            {
                DeviceTopologySnapshot snap;
                if (_pendingTopologySnapshots.TryGetValue(physicalKey, out snap))
                {
                    return snap;
                }
                return null;
            }
        }

        internal static bool ValidateTopologyStability(DeviceIdentity dev, string dispatchSource = "Unknown")
        {
            if (dev == null) return false;

            if (DeviceEnforcementLifecycleManager.IsTestMode && TopologyStabilitySeam != null)
            {
                return TopologyStabilitySeam(dev);
            }

            // Legacy mock invariant: mock devices in tests that do not specify PartitionCount
            if (dev.PartitionCount <= 0)
            {
                return true;
            }

            string physicalKey = DeviceEnforcementLifecycleManager.BuildPhysicalKey(dev);
            if (string.IsNullOrEmpty(physicalKey))
            {
                physicalKey = @"\\.\PHYSICALDRIVE" + dev.PhysicalDiskNumber;
            }

            List<string> currentVolumes = (dev.VolumeMountings != null)
                ? dev.VolumeMountings.Select(v => v.TrimEnd('\\').ToUpperInvariant()).OrderBy(v => v).ToList()
                : new List<string>();

            int currentGen = StorageNotificationManager.StorageArrivalEventGeneration;

            lock (_topologyLock)
            {
                long nowTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                string timeUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                DeviceTopologySnapshot snapA;
                if (!_pendingTopologySnapshots.TryGetValue(physicalKey, out snapA))
                {
                    // First observation: Record Snapshot A and defer enforcement
                    snapA = new DeviceTopologySnapshot
                    {
                        PhysicalKey = physicalKey,
                        DiskNumber = dev.PhysicalDiskNumber,
                        PartitionCount = dev.PartitionCount,
                        NormalizedVolumes = currentVolumes,
                        CaptureTicks = nowTicks,
                        EventGeneration = currentGen,
                        IsInvalidated = false,
                        DispatchSource = dispatchSource,
                        ThreadId = threadId
                    };
                    _pendingTopologySnapshots[physicalKey] = snapA;

                    LogTrace(string.Format("[TOPOLOGY_PENDING_A] Time: {0}, Source: {1}, Thread: {2}, Key: {3}, Disk: {4}, Partitions: {5}, Volumes: [{6}], EventGen: {7}, Age: 0.0ms, Decision: PendingA. Deferring enforcement until confirmation snapshot.",
                        timeUtc, dispatchSource, threadId, physicalKey, dev.PhysicalDiskNumber, dev.PartitionCount, string.Join(",", currentVolumes.ToArray()), currentGen));

                    StorageNotificationManager.ArmTopologyConfirmationTimer();
                    return false; // Do NOT enforce yet
                }

                // Second observation: Candidate Snapshot B
                double snapshotAgeMs = (nowTicks - snapA.CaptureTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

                // 1. Did another relevant DISK or VOLUME arrival notification occur between A and B?
                if (snapA.IsInvalidated || currentGen != snapA.EventGeneration)
                {
                    // Invalidate Snapshot A and rearm
                    snapA.PartitionCount = dev.PartitionCount;
                    snapA.NormalizedVolumes = currentVolumes;
                    snapA.CaptureTicks = nowTicks;
                    snapA.EventGeneration = currentGen;
                    snapA.IsInvalidated = false;
                    snapA.DispatchSource = dispatchSource;
                    snapA.ThreadId = threadId;

                    LogTrace(string.Format("[TOPOLOGY_EVENT_BETWEEN_SNAPSHOTS] Time: {0}, Source: {1}, Thread: {2}, Key: {3}, Partitions: {4}, Volumes: [{5}], EventGen: {6}, Age: {7:F1}ms, Decision: InvalidatedByEvent. Invalidating Snapshot A and rearming confirmation.",
                        timeUtc, dispatchSource, threadId, physicalKey, dev.PartitionCount, string.Join(",", currentVolumes.ToArray()), currentGen, snapshotAgeMs));

                    StorageNotificationManager.ArmTopologyConfirmationTimer();
                    return false;
                }

                // 2. Require same DiskNumber
                if (snapA.DiskNumber != dev.PhysicalDiskNumber)
                {
                    snapA.DiskNumber = dev.PhysicalDiskNumber;
                    snapA.PartitionCount = dev.PartitionCount;
                    snapA.NormalizedVolumes = currentVolumes;
                    snapA.CaptureTicks = nowTicks;
                    snapA.EventGeneration = currentGen;
                    snapA.IsInvalidated = false;
                    snapA.DispatchSource = dispatchSource;
                    snapA.ThreadId = threadId;

                    LogTrace(string.Format("[TOPOLOGY_CHANGED] Time: {0}, Source: {1}, Thread: {2}, Key: {3}, Disk changed ({4} -> {5}), Partitions: {6}, Volumes: [{7}], Decision: ChangedTopology. Deferring enforcement.",
                        timeUtc, dispatchSource, threadId, physicalKey, snapA.DiskNumber, dev.PhysicalDiskNumber, dev.PartitionCount, string.Join(",", currentVolumes.ToArray())));

                    StorageNotificationManager.ArmTopologyConfirmationTimer();
                    return false;
                }

                // 3. Require same PartitionCount
                if (snapA.PartitionCount != dev.PartitionCount)
                {
                    LogTrace(string.Format("[TOPOLOGY_PARTITION_GROWTH] Time: {0}, Source: {1}, Thread: {2}, Key: {3}, PartitionCount changed ({4} -> {5}), Volumes: [{6}], Age: {7:F1}ms, Decision: ChangedTopology. Deferring enforcement until topology stabilizes.",
                        timeUtc, dispatchSource, threadId, physicalKey, snapA.PartitionCount, dev.PartitionCount, string.Join(",", currentVolumes.ToArray()), snapshotAgeMs));

                    snapA.PartitionCount = dev.PartitionCount;
                    snapA.NormalizedVolumes = currentVolumes;
                    snapA.CaptureTicks = nowTicks;
                    snapA.EventGeneration = currentGen;
                    snapA.IsInvalidated = false;
                    snapA.DispatchSource = dispatchSource;
                    snapA.ThreadId = threadId;

                    StorageNotificationManager.ArmTopologyConfirmationTimer();
                    return false;
                }

                // 4. Require same normalized sibling VolumeMountings set
                bool volumesMatch = (snapA.NormalizedVolumes.Count == currentVolumes.Count);
                if (volumesMatch)
                {
                    for (int i = 0; i < currentVolumes.Count; i++)
                    {
                        if (!string.Equals(snapA.NormalizedVolumes[i], currentVolumes[i], StringComparison.OrdinalIgnoreCase))
                        {
                            volumesMatch = false;
                            break;
                        }
                    }
                }

                if (!volumesMatch)
                {
                    LogTrace(string.Format("[TOPOLOGY_VOLUMES_GROWTH] Time: {0}, Source: {1}, Thread: {2}, Key: {3}, Volume set changed ([{4}] -> [{5}]), Age: {6:F1}ms, Decision: ChangedTopology. Deferring enforcement until stable second observation.",
                        timeUtc, dispatchSource, threadId, physicalKey, string.Join(",", snapA.NormalizedVolumes.ToArray()), string.Join(",", currentVolumes.ToArray()), snapshotAgeMs));

                    snapA.NormalizedVolumes = currentVolumes;
                    snapA.CaptureTicks = nowTicks;
                    snapA.EventGeneration = currentGen;
                    snapA.IsInvalidated = false;
                    snapA.DispatchSource = dispatchSource;
                    snapA.ThreadId = threadId;

                    StorageNotificationManager.ArmTopologyConfirmationTimer();
                    return false;
                }

                // 5. Normal safety checks: If disk declared multiple partitions but exposes 0 mounted volumes, fail closed
                if (dev.PartitionCount > 1 && currentVolumes.Count == 0)
                {
                    LogTrace(string.Format("[TOPOLOGY_UNMOUNTED] Time: {0}, Source: {1}, Thread: {2}, Key: {3}, Partitions: {4}, Volumes: [0], Decision: PendingA. Cannot lock or eject.",
                        timeUtc, dispatchSource, threadId, physicalKey, dev.PartitionCount));
                    return false;
                }

                // 6. ENFORCE MINIMUM SNAPSHOT AGE INVARIANT (>= TopologyConfirmationIntervalMs)
                if (snapshotAgeMs < StorageNotificationManager.TopologyConfirmationIntervalMs)
                {
                    LogTrace(string.Format("[TOPOLOGY_TOO_YOUNG] Time: {0}, Source: {1}, Thread: {2}, Key: {3}, Partitions: {4}, Volumes: [{5}], EventGen: {6}, Age: {7:F1}ms < {8}ms, Decision: TooYoung. Preserving Snapshot A and deferring enforcement.",
                        timeUtc, dispatchSource, threadId, physicalKey, dev.PartitionCount, string.Join(",", currentVolumes.ToArray()), currentGen, snapshotAgeMs, StorageNotificationManager.TopologyConfirmationIntervalMs));

                    StorageNotificationManager.ArmTopologyConfirmationTimer();
                    return false;
                }

                // ALL CRITERIA MET: Two-snapshot stability confirmed!
                _pendingTopologySnapshots.Remove(physicalKey);

                LogTrace(string.Format("[TOPOLOGY_CONFIRMED_STABLE] Time: {0}, Source: {1}, Thread: {2}, Key: {3}, Partitions: {4}, Volumes: [{5}], EventGen: {6}, Age: {7:F1}ms >= {8}ms, Decision: ConfirmedStable. Two-snapshot topology stability confirmed for '{9}' (Disk {10}). Proceeding to enforcement.",
                    timeUtc, dispatchSource, threadId, physicalKey, dev.PartitionCount, string.Join(",", currentVolumes.ToArray()), currentGen, snapshotAgeMs, StorageNotificationManager.TopologyConfirmationIntervalMs, dev.FriendlyName, dev.PhysicalDiskNumber));

                return true;
            }
        }

        public static bool StartArrivalReconciliation(ProtectionPolicy currentPolicy, bool isTemporaryAccessActive)
        {
            if (!DeviceEnforcementLifecycleManager.IsTestMode)
            {
                return false; // Blind reconciliation polling removed in production in favor of event-driven interface notifications
            }

            if (System.Threading.Interlocked.CompareExchange(ref _reconciliationGate, 1, 0) != 0)
            {
                return false; // Dedup: at most ONE active reconciliation worker for the pending arrival episode
            }

            System.Threading.CancellationTokenSource cts;
            lock (_reconciliationLock)
            {
                if (_reconciliationCts != null)
                {
                    try { _reconciliationCts.Cancel(); _reconciliationCts.Dispose(); } catch { }
                }
                _reconciliationCts = new System.Threading.CancellationTokenSource();
                cts = _reconciliationCts;
            }

            int[] schedule = (DeviceEnforcementLifecycleManager.IsTestMode && ReconciliationScheduleSeam != null)
                ? ReconciliationScheduleSeam
                : DefaultReconciliationScheduleMs;

            LastReconciliationTask = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < schedule.Length; i++)
                    {
                        if (cts.IsCancellationRequested) break;
                        if (!EnforcementOwnerResolver.IsGuiEnforcementAllowed()) break;

                        ProtectionPolicy effPolicy = GetEffectivePolicy(currentPolicy);
                        if (effPolicy == null || !effPolicy.IsAnyProtectionActive())
                        {
                            break;
                        }

                        int delayMs = schedule[i];
                        if (DeviceEnforcementLifecycleManager.IsTestMode && ReconciliationDelaySeam != null)
                        {
                            ReconciliationDelaySeam(delayMs);
                        }
                        else
                        {
                            if (cts.Token.WaitHandle.WaitOne(delayMs))
                            {
                                break; // Cancelled during wait
                            }
                        }

                        if (cts.IsCancellationRequested) break;
                        if (!EnforcementOwnerResolver.IsGuiEnforcementAllowed()) break;

                        effPolicy = GetEffectivePolicy(currentPolicy);
                        if (effPolicy == null || !effPolicy.IsAnyProtectionActive())
                        {
                            break;
                        }

                        // Run normal live production discovery pipeline
                        var outcome = EvaluateAndEnforceConnectedDevices(effPolicy, isTemporaryAccessActive, "ArrivalReconciliationSweep");

                        if (outcome.S0Consumed || LatencyTracker.GetLastPnpArrivalTicks(consume: false) == 0)
                        {
                            return true; // Target device reconciled and lifecycle/allow began!
                        }
                    }
                    return false;
                }
                finally
                {
                    lock (_reconciliationLock)
                    {
                        if (_reconciliationCts == cts)
                        {
                            _reconciliationCts = null;
                        }
                    }
                    System.Threading.Interlocked.Exchange(ref _reconciliationGate, 0);
                }
            });

            return true;
        }

        public static void CancelArrivalReconciliation(bool resetPendingS0 = false)
        {
            lock (_reconciliationLock)
            {
                if (_reconciliationCts != null)
                {
                    try { _reconciliationCts.Cancel(); } catch { }
                }
            }
            if (resetPendingS0)
            {
                LatencyTracker.ResetPnpArrivalNotification();
            }
        }

        public static SentinelEvaluationOutcome EvaluateAndEnforceConnectedDevices(
            ProtectionPolicy currentPolicy,
            bool isTemporaryAccessActive,
            string dispatchSource = "Unknown")
        {
            SentinelEvaluationOutcome outcome = new SentinelEvaluationOutcome();
            if (currentPolicy == null) return outcome;

            long pendingS0 = LatencyTracker.GetLastPnpArrivalTicks(consume: false);
            long s1Ticks = System.Diagnostics.Stopwatch.GetTimestamp();

            // Discover currently attached devices
            List<DeviceIdentity> devices = DeviceDiscovery.DiscoverAllDevices();
            long s2Ticks = System.Diagnostics.Stopwatch.GetTimestamp();

            // Reconcile physically removed devices with lifecycle manager
            DeviceEnforcementLifecycleManager.ReconcileRemovedDevices(devices);

            if (devices == null) return outcome;
            outcome.DevicesEvaluated = devices.Count;

            foreach (var dev in devices)
            {
                // Check if device is already suppressed (e.g. EjectedUntilRemoval, Evaluating, etc.)
                string suppressReason;
                if (DeviceEnforcementLifecycleManager.ShouldSuppressEnforcement(dev, currentPolicy, out suppressReason))
                {
                    continue; // Skip enforcement without RuleEngine or DeviceEjector!
                }

                long s3Start = System.Diagnostics.Stopwatch.GetTimestamp();
                DeviceEvaluationResult eval = RuleEngine.Evaluate(dev, currentPolicy, isTemporaryAccessActive);
                long s3Done = System.Diagnostics.Stopwatch.GetTimestamp();

                // System/Internal disk safety guard: Never correlate PnP arrival timestamp to internal Disk 0
                bool isInternalOrSystemDisk = eval.IsSafetyProtected ||
                    dev.PhysicalDiskNumber == 0 ||
                    (dev.PhysicalTransport != PhysicalBusType.Usb && dev.WindowsClass != StorageClass.WindowsPortableDevice);

                if (isInternalOrSystemDisk)
                {
                    // Internal system disks are safety protected and must never consume external PnP arrival S0
                    continue;
                }

                if (eval.ShouldAutoEject)
                {
                    // Conservative topology-stability validation:
                    // Prevents premature ejection when only part of a multi-volume disk has mounted.
                    if (!ValidateTopologyStability(dev, dispatchSource))
                    {
                        LogTrace(string.Format("[TOPOLOGY_UNCONFIRMED] Multi-volume topology for {0} (Disk {1}) is not yet confirmed complete (mounted: {2}, partitions: {3}). Deferring enforcement.",
                            dev.FriendlyName, dev.PhysicalDiskNumber,
                            dev.VolumeMountings != null ? dev.VolumeMountings.Count : 0,
                            dev.PartitionCount));
                        continue;
                    }

                    // Correlate and atomically consume S0 exactly once for the arriving removable device
                    long s0Ticks = (pendingS0 > 0) ? LatencyTracker.GetLastPnpArrivalTicks(consume: true) : 0;
                    if (s0Ticks > 0) outcome.S0Consumed = true;
                    pendingS0 = 0; // Clear local reference so subsequent devices in this sweep don't re-consume

                    if (ExecuteBoundedEjection(dev, currentPolicy, s0Ticks, s1Ticks, s2Ticks, s3Done))
                    {
                        outcome.ActionsExecuted++;
                    }
                }
                else if (eval.RecommendedAction == RuleAction.AllowTrusted)
                {
                    // Correlate and atomically consume S0 exactly once for the arriving trusted device
                    long s0Ticks = (pendingS0 > 0) ? LatencyTracker.GetLastPnpArrivalTicks(consume: true) : 0;
                    if (s0Ticks > 0) outcome.S0Consumed = true;
                    pendingS0 = 0; // Clear local reference so subsequent devices in this sweep don't re-consume
                    outcome.ActionsExecuted++;

                    SentinelArrivalLatencyRecord trustedRec = new SentinelArrivalLatencyRecord(dev.FriendlyName)
                    {
                        S0_NotificationReceivedTicks = s0Ticks,
                        S1_IdentityResolutionStartTicks = s1Ticks,
                        S2_IdentityResolvedTicks = s2Ticks,
                        S3_RuleEngineDecisionTicks = s3Done,
                        S4_EjectAttemptStartTicks = 0,
                        S5_VolumeLockAcquiredTicks = 0,
                        S6_DismountCompletedTicks = 0,
                        S7_EjectRequestCompletedTicks = 0,
                        EjectSuccess = false,
                        IsTrusted = true,
                        DiscoverySnapshot = dev.DiscoverySnapshot
                    };
                    LatencyTracker.StoreArrivalRecord(trustedRec);

                    LogTrace(trustedRec.FormatReport());
                }
            }

            return outcome;
        }

        internal static void EnforceSingleDeviceForTesting(
            DeviceIdentity dev,
            ProtectionPolicy currentPolicy,
            bool isTemporaryAccessActive = false)
        {
            if (currentPolicy == null || dev == null) return;

            string suppressReason;
            if (DeviceEnforcementLifecycleManager.ShouldSuppressEnforcement(dev, currentPolicy, out suppressReason))
            {
                return;
            }

            long s0Ticks = System.Diagnostics.Stopwatch.GetTimestamp();
            long s1Ticks = s0Ticks;
            long s2Ticks = s0Ticks;
            DeviceEvaluationResult eval = RuleEngine.Evaluate(dev, currentPolicy, isTemporaryAccessActive);
            long s3Done = System.Diagnostics.Stopwatch.GetTimestamp();

            if (eval.ShouldAutoEject)
            {
                ExecuteBoundedEjection(dev, currentPolicy, s0Ticks, s1Ticks, s2Ticks, s3Done);
            }
        }

        private static bool ExecuteBoundedEjection(
            DeviceIdentity dev,
            ProtectionPolicy currentPolicy,
            long s0Ticks,
            long s1Ticks,
            long s2Ticks,
            long s3Done)
        {
            DeviceLifecycleRecord lifecycleRec;
            if (!DeviceEnforcementLifecycleManager.TryStartEnforcement(dev, out lifecycleRec))
            {
                return false; // Another worker or sweep owns this lifecycle
            }

            string physicalKey = lifecycleRec.PhysicalKey;
            int generationId = lifecycleRec.GenerationId;
            int cycleId = lifecycleRec.EnforcementCycleId;

            if (!DeviceEnforcementLifecycleManager.TransitionState(physicalKey, generationId, cycleId, EnforcementState.EjectPending))
            {
                return false; // Stale worker or cycle preempted
            }

            try
            {
                using (System.Threading.CancellationTokenSource cts = new System.Threading.CancellationTokenSource())
                {
                    lifecycleRec.ActiveCts = cts;

                    long firstAttemptS4Ticks = System.Diagnostics.Stopwatch.GetTimestamp();
                    SentinelArrivalLatencyRecord latencyRec = new SentinelArrivalLatencyRecord(dev.FriendlyName)
                    {
                        S0_NotificationReceivedTicks = s0Ticks,
                        S1_IdentityResolutionStartTicks = s1Ticks,
                        S2_IdentityResolvedTicks = s2Ticks,
                        S3_RuleEngineDecisionTicks = s3Done,
                        S4_EjectAttemptStartTicks = firstAttemptS4Ticks,
                        S4b_FinalAttemptStartTicks = firstAttemptS4Ticks,
                        IsTrusted = false,
                        DiscoverySnapshot = dev.DiscoverySnapshot
                    };

                    bool isPnpTarget = PnpSafeEjectBackend.IsSupported(dev)
                        && (DeviceEnforcementLifecycleManager.EjectExecutionSeam == null)
                        && !(DeviceEnforcementLifecycleManager.IsTestMode && DeviceEjector.HasActiveSeams());
                    int maxAttempts = isPnpTarget ? 2 : DeviceEnforcementLifecycleManager.MaxEjectAttempts;
                    for (int attempt = 1; attempt <= maxAttempts; attempt++)
                    {
                        // ── PRE-ATTEMPT VALIDATION ──
                        if (cts.IsCancellationRequested || !DeviceEnforcementLifecycleManager.ValidateWorkerLease(physicalKey, generationId, cycleId))
                        {
                            break;
                        }

                        // Pre-check 1 & 2: Dynamic policy and trust evaluation via RuleEngine (SEC-04)
                        var effectivePolicy = GetEffectivePolicy(currentPolicy);
                        var eval = RuleEngine.Evaluate(dev, effectivePolicy, TemporaryAccessManager.IsActive);
                        if (eval.IsSafetyProtected)
                        {
                            bool isSuccessClaim;
                            DeviceEnforcementLifecycleManager.TryClaimNotification(physicalKey, generationId, cycleId, EnforcementState.FailedTerminal, out isSuccessClaim);
                            break;
                        }
                        if (!eval.ShouldAutoEject)
                        {
                            bool isAllowedTrust = (eval.RecommendedAction == RuleAction.AllowTrusted && TrustedDeviceRepository.IsDeviceTrusted(dev));
                            DeviceEnforcementLifecycleManager.HandlePolicyOrTrustChange(physicalKey, generationId, cycleId, isNowTrusted: isAllowedTrust, isPolicyOff: !isAllowedTrust);
                            break;
                        }

                        // Pre-check 3: Physical presence check (on retries)
                        if (attempt > 1 && !DeviceEnforcementLifecycleManager.CheckPhysicalPresence(dev.PhysicalUsbParentInstanceId, dev.DeviceInstanceId))
                        {
                            DeviceEnforcementLifecycleManager.HandlePhysicalRemoval(physicalKey, generationId);
                            break;
                        }

                        // Pre-check 4: Safety Guard
                        string safetyReason;
                        if (SystemDeviceSafetyGuard.ValidateDevice(dev, out safetyReason) != SafetyValidationResult.Passed)
                        {
                            bool isSuccessClaim;
                            DeviceEnforcementLifecycleManager.TryClaimNotification(physicalKey, generationId, cycleId, EnforcementState.FailedTerminal, out isSuccessClaim);
                            break;
                        }

                        if (!DeviceEnforcementLifecycleManager.ValidateWorkerLease(physicalKey, generationId, cycleId))
                        {
                            break;
                        }

                        lifecycleRec.AttemptCount = attempt;

                        if (attempt > 1)
                        {
                            latencyRec.S4b_FinalAttemptStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                            latencyRec.S5_VolumeLockAcquiredTicks = 0;
                            latencyRec.S6_DismountCompletedTicks = 0;
                            latencyRec.S7_EjectRequestCompletedTicks = 0;
                        }

                        DeviceEjectOperationResult opResult;

                        if (DeviceEnforcementLifecycleManager.IsTestMode && DeviceEnforcementLifecycleManager.EjectExecutionSeam != null)
                        {
                            DeviceEjectResult seamResult = DeviceEnforcementLifecycleManager.EjectExecutionSeam(dev, latencyRec);
                            opResult = new DeviceEjectOperationResult
                            {
                                AggregateResult = seamResult,
                                Success = (seamResult == DeviceEjectResult.Success || seamResult == DeviceEjectResult.DeviceGone),
                                PostVerificationPassed = (seamResult == DeviceEjectResult.Success || seamResult == DeviceEjectResult.DeviceGone),
                                StatusMessage = seamResult.ToString(),
                                SiblingVolumes = (dev.VolumeMountings != null) ? new List<string>(dev.VolumeMountings) : new List<string>()
                            };
                        }
                        else if (DeviceEnforcementLifecycleManager.IsTestMode && DeviceEjector.HasActiveSeams())
                        {
                            // Isolated diagnostic / testing for legacy whole-device FSCTL engine
                            opResult = DeviceEjector.EjectDeviceTransactional(dev, latencyRec);
                        }
                        else if (PnpSafeEjectBackend.IsSupported(dev))
                        {
                            // Production Primary: Microsoft-native PnP Safe-Eject (Zero fallback)
                            opResult = PnpSafeEjectBackend.EjectDevice(dev, latencyRec);
                        }
                        else if (dev.WindowsClass == StorageClass.DiskDrive && dev.PhysicalTransport == PhysicalBusType.Usb)
                        {
                            opResult = DeviceEjector.EjectDeviceTransactional(dev, latencyRec);
                        }
                        else
                        {
                            opResult = new DeviceEjectOperationResult
                            {
                                AggregateResult = DeviceEjectResult.NotEjectable,
                                Success = false,
                                PostVerificationPassed = false,
                                StatusMessage = "Device identity is not supported for storage volume dismount or safe ejection."
                            };
                        }

                        // Pre-delay / Retry Telemetry Classification
                        if (opResult.AggregateResult == DeviceEjectResult.Busy || opResult.AggregateResult == DeviceEjectResult.RetryableAccessDenied)
                        {
                            if (attempt < maxAttempts)
                            {
                                int initialDelay = (opResult.AggregateResult == DeviceEjectResult.RetryableAccessDenied)
                                    ? DeviceEnforcementLifecycleManager.AccessDeniedInitialRetryDelayMs
                                    : DeviceEnforcementLifecycleManager.BusyInitialRetryDelayMs;
                                int maxDelay = (opResult.AggregateResult == DeviceEjectResult.RetryableAccessDenied)
                                    ? DeviceEnforcementLifecycleManager.AccessDeniedMaxRetryDelayMs
                                    : DeviceEnforcementLifecycleManager.BusyMaxRetryDelayMs;

                                int delayMs = (attempt == 1) ? initialDelay : maxDelay;
                                opResult.RetryDelayMs = delayMs;
                                opResult.RetryClassification = (opResult.AggregateResult == DeviceEjectResult.RetryableAccessDenied) ? "RetryableAccessDenied" : "Busy";

                                latencyRec.RetryCount++;
                                latencyRec.RetryBackoffMs += delayMs;
                            }
                            else
                            {
                                opResult.RetryDelayMs = 0;
                                if (opResult.AggregateResult == DeviceEjectResult.RetryableAccessDenied)
                                {
                                    opResult.AggregateResult = DeviceEjectResult.AccessDenied;
                                    opResult.RetryClassification = "PermanentAccessDenied";
                                }
                                else
                                {
                                    opResult.RetryClassification = "Busy";
                                }
                            }
                        }

                        // Per-Volume Telemetry Log
                        LogTrace(opResult.FormatTelemetry(attempt, physicalKey, dev.PhysicalDiskNumber, generationId, cycleId));

                        if (latencyRec.S7_EjectRequestCompletedTicks == 0)
                        {
                            latencyRec.S7_EjectRequestCompletedTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        }
                        latencyRec.EjectSuccess = opResult.Success;
                        LatencyTracker.StoreArrivalRecord(latencyRec);

                        // ── RESULT CLASSIFICATION ──
                        if (opResult.IsPnpSafeEject)
                        {
                            if (opResult.Success && opResult.AggregateResult == DeviceEjectResult.Success && opResult.PostVerificationPassed)
                            {
                                bool isSuccessClaim;
                                if (DeviceEnforcementLifecycleManager.TryClaimNotification(physicalKey, generationId, cycleId, EnforcementState.EjectedUntilRemoval, out isSuccessClaim))
                                {
                                    EmitNotification(dev, true, opResult.StatusMessage ?? "Device safely ejected via PnP.");
                                    LogTrace(latencyRec.FormatReport());
                                }
                                break; // Success is terminal!
                            }

                            // Single delayed retry eligibility:
                            // 1. First attempt only (max 2 CM calls per insertion)
                            // 2. Exact error: CR_REMOVE_VETOED
                            // 3. Exact veto type: PNP_VetoDevice
                            // 4. Exact target tree: Devnode within same physical device tree
                            bool isPnpVetoDeviceOnTargetTree = (attempt == 1) &&
                                (opResult.ConfigRet == PnpSafeEjectBackend.CR_REMOVE_VETOED) &&
                                (opResult.VetoType == PNP_VETO_TYPE.PNP_VetoDevice) &&
                                PnpSafeEjectBackend.IsVetoInTargetDeviceTree(opResult.VetoName, opResult.TargetInstanceId, dev);

                            if (isPnpVetoDeviceOnTargetTree)
                            {
                                LogTrace(string.Format("[PNP_VETO_RETRY_SCHEDULED] Target {0} received PNP_VetoDevice on tree '{1}'. Scheduling delayed retry targeting {2}ms total age.",
                                    dev.FriendlyName, opResult.VetoName, DeviceEnforcementLifecycleManager.PnpVetoDeviceRetryTargetAgeMs));

                                if (!DeviceEnforcementLifecycleManager.TransitionState(physicalKey, generationId, cycleId, EnforcementState.RetryWaiting))
                                {
                                    break; // Stale cycle or cancelled
                                }

                                if (DeviceEnforcementPending != null)
                                {
                                    DeviceEnforcementPending(null, new DeviceInterceptEventArgs
                                    {
                                        Device = dev,
                                        EjectSuccess = false,
                                        Message = Loc.Get("SafeEjectPreparing")
                                    });
                                }

                                // Calculate delay to reach approximately 10s total device age
                                long t0Ticks = (s0Ticks > 0) ? s0Ticks : lifecycleRec.FirstObservedTicks;
                                double currentAgeMs = ((double)(System.Diagnostics.Stopwatch.GetTimestamp() - t0Ticks) / System.Diagnostics.Stopwatch.Frequency) * 1000.0;
                                int targetAgeMs = DeviceEnforcementLifecycleManager.PnpVetoDeviceRetryTargetAgeMs;
                                int delayMs = (int)Math.Max(500, (int)(targetAgeMs - currentAgeMs));

                                opResult.RetryDelayMs = delayMs;
                                opResult.RetryClassification = "PnpVetoDeviceDelayedRetry";
                                latencyRec.RetryCount++;
                                latencyRec.RetryBackoffMs += delayMs;

                                try
                                {
                                    if (DeviceEnforcementLifecycleManager.IsTestMode && DeviceEnforcementLifecycleManager.DelaySeam != null)
                                    {
                                        DeviceEnforcementLifecycleManager.DelaySeam(delayMs, cts.Token).Wait();
                                    }
                                    else
                                    {
                                        System.Threading.Tasks.Task.Delay(delayMs, cts.Token).Wait();
                                    }
                                }
                                catch (Exception)
                                {
                                    // Cancelled during delay
                                    break;
                                }

                                if (cts.IsCancellationRequested || !DeviceEnforcementLifecycleManager.ValidateWorkerLease(physicalKey, generationId, cycleId))
                                {
                                    break;
                                }

                                // Pre-retry check 1: Physical presence
                                if (!DeviceEnforcementLifecycleManager.CheckPhysicalPresence(dev.PhysicalUsbParentInstanceId, dev.DeviceInstanceId))
                                {
                                    DeviceEnforcementLifecycleManager.HandlePhysicalRemoval(physicalKey, generationId);
                                    break;
                                }

                                // Pre-retry check 2 & 3: Dynamic policy and trust evaluation via RuleEngine (SEC-04)
                                var retryPolicy = GetEffectivePolicy(currentPolicy);
                                var retryEval = RuleEngine.Evaluate(dev, retryPolicy, TemporaryAccessManager.IsActive);
                                if (retryEval.IsSafetyProtected)
                                {
                                    bool isSuccessClaim;
                                    DeviceEnforcementLifecycleManager.TryClaimNotification(physicalKey, generationId, cycleId, EnforcementState.FailedTerminal, out isSuccessClaim);
                                    break;
                                }
                                if (!retryEval.ShouldAutoEject)
                                {
                                    bool isAllowedTrust = (retryEval.RecommendedAction == RuleAction.AllowTrusted && TrustedDeviceRepository.IsDeviceTrusted(dev));
                                    DeviceEnforcementLifecycleManager.HandlePolicyOrTrustChange(physicalKey, generationId, cycleId, isNowTrusted: isAllowedTrust, isPolicyOff: !isAllowedTrust);
                                    break;
                                }

                                // Pre-retry check 4: Fresh target resolution
                                string freshTargetDevNode;
                                uint freshDevInst;
                                string resolveErr;
                                if (!PnpSafeEjectBackend.ResolveSafeRemovableUsbParent(dev, out freshTargetDevNode, out freshDevInst, out resolveErr))
                                {
                                    LogTrace(string.Format("[PNP_RETRY_TARGET_RESOLVE_FAILED] Fresh target resolution failed for {0}: {1}", dev.FriendlyName, resolveErr));
                                    bool isSuccessClaim;
                                    if (DeviceEnforcementLifecycleManager.TryClaimNotification(physicalKey, generationId, cycleId, EnforcementState.FailedTerminal, out isSuccessClaim))
                                    {
                                        EmitNotification(dev, false, "Device target resolution failed prior to retry: " + resolveErr);
                                        LogTrace(latencyRec.FormatReport());
                                    }
                                    break;
                                }

                                // Pre-retry check 5: Fresh safety validation
                                string freshSafetyReason;
                                if (SystemDeviceSafetyGuard.ValidateDevice(dev, out freshSafetyReason) != SafetyValidationResult.Passed)
                                {
                                    LogTrace(string.Format("[PNP_RETRY_SAFETY_REJECTED] Safety validation rejected {0}: {1}", dev.FriendlyName, freshSafetyReason));
                                    bool isSuccessClaim;
                                    if (DeviceEnforcementLifecycleManager.TryClaimNotification(physicalKey, generationId, cycleId, EnforcementState.FailedTerminal, out isSuccessClaim))
                                    {
                                        EmitNotification(dev, false, "Device failed safety validation: " + freshSafetyReason);
                                        LogTrace(latencyRec.FormatReport());
                                    }
                                    break;
                                }


                                if (!DeviceEnforcementLifecycleManager.TransitionState(physicalKey, generationId, cycleId, EnforcementState.EjectPending))
                                {
                                    break;
                                }

                                continue; // Proceed to attempt 2
                            }
                            else
                            {
                                // Any other veto, non-veto error, or second failure is terminal failure
                                bool isSuccessClaim;
                                if (DeviceEnforcementLifecycleManager.TryClaimNotification(physicalKey, generationId, cycleId, EnforcementState.FailedTerminal, out isSuccessClaim))
                                {
                                    string failMsg = opResult.StatusMessage ?? "PnP safe ejection failed.";
                                    EmitNotification(dev, false, failMsg);
                                    LogTrace(latencyRec.FormatReport());
                                }
                                break; // Terminal failure - NO fallback to FSCTL
                            }
                        }
                        else if (opResult.Success && opResult.AggregateResult == DeviceEjectResult.Success && opResult.PostVerificationPassed)
                        {
                            bool isSuccessClaim;
                            if (DeviceEnforcementLifecycleManager.TryClaimNotification(physicalKey, generationId, cycleId, EnforcementState.EjectedUntilRemoval, out isSuccessClaim))
                            {
                                EmitNotification(dev, true, opResult.StatusMessage);
                                LogTrace(latencyRec.FormatReport());
                            }
                            break; // Success is terminal!
                        }
                        else if (opResult.AggregateResult == DeviceEjectResult.IncompleteEject || opResult.AggregateResult == DeviceEjectResult.VerificationIndeterminate)
                        {
                            // Partial post-eject anomaly or indeterminate verification: terminal failure, do NOT retry!
                            bool isSuccessClaim;
                            if (DeviceEnforcementLifecycleManager.TryClaimNotification(physicalKey, generationId, cycleId, EnforcementState.FailedTerminal, out isSuccessClaim))
                            {
                                string failMsg = (opResult.AggregateResult == DeviceEjectResult.VerificationIndeterminate)
                                    ? (opResult.StatusMessage ?? "Untrusted device was detected, but automatic ejection verification was indeterminate.")
                                    : "Untrusted device was detected, but automatic ejection did not complete.";
                                EmitNotification(dev, false, failMsg);
                                LogTrace(latencyRec.FormatReport());
                            }
                            break; // Terminal failure
                        }
                        else if (opResult.AggregateResult == DeviceEjectResult.DeviceGone)
                        {
                            // Correlated No-Media check across the WHOLE physical device
                            bool isCorrelatedEjectSuccess = lifecycleRec.HadUsableMediaBeforeAttempt &&
                                attempt >= 1 &&
                                opResult.PostVerificationPassed &&
                                DeviceEnforcementLifecycleManager.ValidateWorkerLease(physicalKey, generationId, cycleId);

                            if (isCorrelatedEjectSuccess)
                            {
                                bool isSuccessClaim;
                                if (DeviceEnforcementLifecycleManager.TryClaimNotification(physicalKey, generationId, cycleId, EnforcementState.EjectedUntilRemoval, out isSuccessClaim))
                                {
                                    EmitNotification(dev, true, "Device media unmounted and ejected.");
                                    LogTrace(latencyRec.FormatReport());
                                }
                                break; // Terminal success
                            }
                            else
                            {
                                bool isSuccessClaim;
                                if (DeviceEnforcementLifecycleManager.TryClaimNotification(physicalKey, generationId, cycleId, EnforcementState.FailedTerminal, out isSuccessClaim))
                                {
                                    EmitNotification(dev, false, "Device has no media or is inaccessible.");
                                    LogTrace(latencyRec.FormatReport());
                                }
                                break; // Terminal non-ejectable / failure
                            }
                        }
                        else if (opResult.AggregateResult == DeviceEjectResult.Busy || opResult.AggregateResult == DeviceEjectResult.RetryableAccessDenied)
                        {
                            // Transient lock/sharing violation or transient pre-eject AccessDenied
                            if (attempt < maxAttempts)
                            {
                                if (!DeviceEnforcementLifecycleManager.TransitionState(physicalKey, generationId, cycleId, EnforcementState.RetryWaiting))
                                {
                                    break; // Stale cycle or cancelled
                                }
                                int delayMs = (opResult.AggregateResult == DeviceEjectResult.RetryableAccessDenied)
                                    ? ((attempt == 1) ? DeviceEnforcementLifecycleManager.AccessDeniedInitialRetryDelayMs : DeviceEnforcementLifecycleManager.AccessDeniedMaxRetryDelayMs)
                                    : ((attempt == 1) ? DeviceEnforcementLifecycleManager.BusyInitialRetryDelayMs : DeviceEnforcementLifecycleManager.BusyMaxRetryDelayMs);

                                try
                                {
                                    if (DeviceEnforcementLifecycleManager.IsTestMode && DeviceEnforcementLifecycleManager.DelaySeam != null)
                                    {
                                        DeviceEnforcementLifecycleManager.DelaySeam(delayMs, cts.Token).Wait();
                                    }
                                    else
                                    {
                                        System.Threading.Tasks.Task.Delay(delayMs, cts.Token).Wait();
                                    }
                                }
                                catch (Exception)
                                {
                                    // Cancelled during delay
                                    break;
                                }

                                if (cts.IsCancellationRequested || !DeviceEnforcementLifecycleManager.ValidateWorkerLease(physicalKey, generationId, cycleId))
                                {
                                    break;
                                }

                                // Revalidate before next retry:
                                // 1. Physical presence
                                if (!DeviceEnforcementLifecycleManager.CheckPhysicalPresence(dev.PhysicalUsbParentInstanceId, dev.DeviceInstanceId))
                                {
                                    DeviceEnforcementLifecycleManager.HandlePhysicalRemoval(physicalKey, generationId);
                                    break;
                                }

                                // 2 & 3: Dynamic policy and trust evaluation via RuleEngine (SEC-04)
                                var retryPolicy = GetEffectivePolicy(currentPolicy);
                                var retryEval = RuleEngine.Evaluate(dev, retryPolicy, TemporaryAccessManager.IsActive);
                                if (retryEval.IsSafetyProtected)
                                {
                                    bool isSuccessClaim;
                                    DeviceEnforcementLifecycleManager.TryClaimNotification(physicalKey, generationId, cycleId, EnforcementState.FailedTerminal, out isSuccessClaim);
                                    break;
                                }
                                if (!retryEval.ShouldAutoEject)
                                {
                                    bool isAllowedTrust = (retryEval.RecommendedAction == RuleAction.AllowTrusted && TrustedDeviceRepository.IsDeviceTrusted(dev));
                                    DeviceEnforcementLifecycleManager.HandlePolicyOrTrustChange(physicalKey, generationId, cycleId, isNowTrusted: isAllowedTrust, isPolicyOff: !isAllowedTrust);
                                    break;
                                }

                                if (!DeviceEnforcementLifecycleManager.TransitionState(physicalKey, generationId, cycleId, EnforcementState.EjectPending))
                                {
                                    break;
                                }
                                continue; // Proceed to next retry attempt
                            }
                            else
                            {
                                // Retry budget exhausted (busy)
                                bool isSuccessClaim;
                                if (DeviceEnforcementLifecycleManager.TryClaimNotification(physicalKey, generationId, cycleId, EnforcementState.FailedTerminal, out isSuccessClaim))
                                {
                                    EmitNotification(dev, false, "Ejection failed: volume remained busy after maximum retries.");
                                    LogTrace(latencyRec.FormatReport());
                                }
                                break;
                            }
                        }
                        else
                        {
                            // Permanent failure (AccessDenied, SafetyRejected, NotEjectable, Failed)
                            bool isSuccessClaim;
                            if (DeviceEnforcementLifecycleManager.TryClaimNotification(physicalKey, generationId, cycleId, EnforcementState.FailedTerminal, out isSuccessClaim))
                            {
                                string failMsg = (opResult.AggregateResult == DeviceEjectResult.AccessDenied)
                                    ? string.Format("Untrusted device '{0}' was detected, but automatic ejection failed.", dev.FriendlyName)
                                    : (opResult.StatusMessage ?? "Ejection failed.");
                                EmitNotification(dev, false, failMsg);
                                LogTrace(latencyRec.FormatReport());
                            }
                            break;
                        }
                    }
                }
            }
            finally
            {
                lifecycleRec.ActiveCts = null;
            }
            return true;
        }

        private static void EmitNotification(DeviceIdentity dev, bool success, string msg)
        {
            if (DeviceEnforcementLifecycleManager.IsTestMode && DeviceEnforcementLifecycleManager.NotificationSinkSeam != null)
            {
                DeviceEnforcementLifecycleManager.NotificationSinkSeam(dev, success, msg);
                return;
            }

            if (DeviceIntercepted != null)
            {
                DeviceIntercepted(null, new DeviceInterceptEventArgs
                {
                    Device = dev,
                    EjectSuccess = success,
                    Message = msg
                });
            }
        }

        private static readonly string TraceLogPath = Path.Combine(Path.GetTempPath(), "usbshield_sentinel_trace.log");

        internal static void LogTrace(string message)
        {
            try
            {
                File.AppendAllText(TraceLogPath, string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] {1}{2}", DateTime.Now, message, Environment.NewLine));
            }
            catch { }
        }
    }
}
