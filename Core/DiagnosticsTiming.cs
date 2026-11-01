// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — High-Resolution Latency Diagnostics & Instrumentation
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using UsbShieldPro.Core;
using UsbShieldPro.Devices;
using UsbShieldPro.Models;
using UsbShieldPro.Persistence;

namespace UsbShieldPro.Core
{
    /// <summary>
    /// Records high-resolution timestamps for user action execution (T0 -> T7)
    /// </summary>
    public class UserActionLatencyRecord
    {
        public string ActionName { get; set; }
        public long T0_UserClickTicks { get; set; }
        public long T1_UiHandlerEnteredTicks { get; set; }
        public long T2_RegistryTxnStartTicks { get; set; }
        public long T3_RegistryWriteDoneTicks { get; set; }
        public long T4_ReadBackVerifiedTicks { get; set; }
        public long T5_RefreshStartTicks { get; set; }
        public long T6_RefreshDoneTicks { get; set; }
        public long T7_UiRenderDoneTicks { get; set; }
        public DateTime TimestampUtc { get; set; }

        public UserActionLatencyRecord(string actionName)
        {
            ActionName = actionName;
            TimestampUtc = DateTime.UtcNow;
            T0_UserClickTicks = Stopwatch.GetTimestamp();
        }

        private static double TicksToMs(long start, long end)
        {
            if (start <= 0 || end <= 0 || end < start) return 0.0;
            return ((end - start) * 1000.0) / Stopwatch.Frequency;
        }

        public double Duration_T0_to_T1_Ms { get { return TicksToMs(T0_UserClickTicks, T1_UiHandlerEnteredTicks); } }
        public double Duration_T1_to_T2_Ms { get { return TicksToMs(T1_UiHandlerEnteredTicks, T2_RegistryTxnStartTicks); } }
        public double Duration_T2_to_T3_Ms { get { return TicksToMs(T2_RegistryTxnStartTicks, T3_RegistryWriteDoneTicks); } }
        public double Duration_T3_to_T4_Ms { get { return TicksToMs(T3_RegistryWriteDoneTicks, T4_ReadBackVerifiedTicks); } }
        public double Duration_T4_to_T5_Ms { get { return TicksToMs(T4_ReadBackVerifiedTicks, T5_RefreshStartTicks); } }
        public double Duration_T5_to_T6_Ms { get { return TicksToMs(T5_RefreshStartTicks, T6_RefreshDoneTicks); } }
        public double Duration_T6_to_T7_Ms { get { return TicksToMs(T6_RefreshDoneTicks, T7_UiRenderDoneTicks); } }
        public double TotalDurationMs { get { return TicksToMs(T0_UserClickTicks, T7_UiRenderDoneTicks); } }

        public string FormatReport()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Format("User Action Latency Trace: [{0}] at {1:yyyy-MM-dd HH:mm:ss.fff} UTC", ActionName, TimestampUtc));
            sb.AppendLine(string.Format("  T0 -> T1 (UI Event Dispatch):       {0,8:F3} ms", Duration_T0_to_T1_Ms));
            sb.AppendLine(string.Format("  T1 -> T2 (Pre-Flight & Journal Prep):{0,8:F3} ms", Duration_T1_to_T2_Ms));
            sb.AppendLine(string.Format("  T2 -> T3 (Registry Write & Flush):  {0,8:F3} ms", Duration_T2_to_T3_Ms));
            sb.AppendLine(string.Format("  T3 -> T4 (Read-back Verification):  {0,8:F3} ms", Duration_T3_to_T4_Ms));
            sb.AppendLine(string.Format("  T4 -> T5 (Transition to Refresh):   {0,8:F3} ms", Duration_T4_to_T5_Ms));
            sb.AppendLine(string.Format("  T5 -> T6 (Device / State Rescan):   {0,8:F3} ms", Duration_T5_to_T6_Ms));
            sb.AppendLine(string.Format("  T6 -> T7 (UI Invalidation & Render):{0,8:F3} ms", Duration_T6_to_T7_Ms));
            sb.AppendLine("  " + new string('-', 50));
            sb.AppendLine(string.Format("  Total Action Turnaround (T0 -> T7): {0,8:F3} ms", TotalDurationMs));
            return sb.ToString();
        }
    }

    /// <summary>
    /// Records high-resolution timestamps for Sentinel device arrival & reactive ejection (S0 -> S7)
    /// </summary>
    public class SentinelArrivalLatencyRecord
    {
        public string DeviceName { get; set; }
        public long S0_NotificationReceivedTicks { get; set; }
        public long S1_IdentityResolutionStartTicks { get; set; }
        public long S2_IdentityResolvedTicks { get; set; }
        public long S3_RuleEngineDecisionTicks { get; set; }
        public long S4_EjectAttemptStartTicks { get; set; }
        /// <summary>
        /// Exact timestamp when the final successful (or terminal) DeviceEjector attempt began.
        /// </summary>
        public long S4b_FinalAttemptStartTicks { get; set; }
        /// <summary>
        /// Number of retry attempts executed before terminal outcome.
        /// </summary>
        public int RetryCount { get; set; }
        /// <summary>
        /// Total backoff delay in milliseconds incurred across retries.
        /// </summary>
        public double RetryBackoffMs { get; set; }
        public long S5_VolumeLockAcquiredTicks { get; set; }
        public long S6_DismountCompletedTicks { get; set; }
        public long S7_EjectRequestCompletedTicks { get; set; }
        public DateTime TimestampUtc { get; set; }
        public bool EjectSuccess { get; set; }
        public bool IsTrusted { get; set; }
        public DiscoveryTimingSnapshot DiscoverySnapshot { get; set; }

        public SentinelArrivalLatencyRecord(string deviceName)
        {
            DeviceName = deviceName;
            TimestampUtc = DateTime.UtcNow;
            S0_NotificationReceivedTicks = Stopwatch.GetTimestamp();
        }

        private static double TicksToMs(long start, long end)
        {
            if (start <= 0 || end <= 0 || end < start) return 0.0;
            return ((end - start) * 1000.0) / Stopwatch.Frequency;
        }

        private static string FormatStage(long start, long end, string unreachedText = "NOT REACHED")
        {
            if (start <= 0 || end <= 0) return unreachedText;
            if (end < start) return "0.000 ms";
            double ms = ((end - start) * 1000.0) / Stopwatch.Frequency;
            return string.Format("{0,8:F3} ms", ms);
        }

        public double Duration_S0_to_S1_Ms { get { return TicksToMs(S0_NotificationReceivedTicks, S1_IdentityResolutionStartTicks); } }
        public double Duration_S1_to_S2_Ms { get { return TicksToMs(S1_IdentityResolutionStartTicks, S2_IdentityResolvedTicks); } }
        public double Duration_S2_to_S3_Ms { get { return TicksToMs(S2_IdentityResolvedTicks, S3_RuleEngineDecisionTicks); } }
        public double Duration_S3_to_S4_Ms { get { return TicksToMs(S3_RuleEngineDecisionTicks, S4_EjectAttemptStartTicks); } }
        public double Duration_S4_to_S5_Ms { get { return TicksToMs(S4_EjectAttemptStartTicks, S5_VolumeLockAcquiredTicks); } }
        public double Duration_S5_to_S6_Ms { get { return TicksToMs(S5_VolumeLockAcquiredTicks, S6_DismountCompletedTicks); } }
        public double Duration_S6_to_S7_Ms { get { return TicksToMs(S6_DismountCompletedTicks, S7_EjectRequestCompletedTicks); } }
        public double TotalSentinelResponseMs { get { return TicksToMs(S0_NotificationReceivedTicks, S7_EjectRequestCompletedTicks); } }

        public string FormatReport()
        {
            StringBuilder sb = new StringBuilder();
            if (IsTrusted)
            {
                sb.AppendLine(string.Format("Sentinel Arrival Latency Trace (TRUSTED DEVICE): [{0}] at {1:yyyy-MM-dd HH:mm:ss.fff} UTC", DeviceName ?? "Unknown", TimestampUtc));
                sb.AppendLine(string.Format("  S0 -> S1 (PnP Notification to Dispatch):  {0}", FormatStage(S0_NotificationReceivedTicks, S1_IdentityResolutionStartTicks)));
                sb.AppendLine(string.Format("  S1 -> S2 (DeviceIdentity Resolution):     {0}", FormatStage(S1_IdentityResolutionStartTicks, S2_IdentityResolvedTicks)));
                if (DiscoverySnapshot != null)
                {
                    sb.AppendLine(string.Format("    Discovery Snapshot:                      {0}", DiscoverySnapshot.FormatBreakdown()));
                }
                sb.AppendLine(string.Format("  S2 -> S3 (RuleEngine Decision):           {0}", FormatStage(S2_IdentityResolvedTicks, S3_RuleEngineDecisionTicks)));
                sb.AppendLine("  S4 (Enforcement Decision):                ALLOW / NO EJECT");
                sb.AppendLine("  " + new string('-', 50));
                sb.AppendLine(string.Format("  Total Arrival Evaluation (S0 -> S3):     {0} [Result: ALLOWED]", FormatStage(S0_NotificationReceivedTicks, S3_RuleEngineDecisionTicks)));
                return sb.ToString();
            }

            sb.AppendLine(string.Format("Sentinel Arrival Latency Trace (UNAUTHORIZED DEVICE): [{0}] at {1:yyyy-MM-dd HH:mm:ss.fff} UTC", DeviceName ?? "Unknown", TimestampUtc));
            sb.AppendLine(string.Format("  S0 -> S1 (PnP Notification to Dispatch):  {0}", FormatStage(S0_NotificationReceivedTicks, S1_IdentityResolutionStartTicks)));
            sb.AppendLine(string.Format("  S1 -> S2 (DeviceIdentity Resolution):     {0}", FormatStage(S1_IdentityResolutionStartTicks, S2_IdentityResolvedTicks)));
            if (DiscoverySnapshot != null)
            {
                sb.AppendLine(string.Format("    Discovery Snapshot:                      {0}", DiscoverySnapshot.FormatBreakdown()));
            }
            sb.AppendLine(string.Format("  S2 -> S3 (RuleEngine Decision):           {0}", FormatStage(S2_IdentityResolvedTicks, S3_RuleEngineDecisionTicks)));
            sb.AppendLine(string.Format("  S3 -> S4 (Dispatch to Ejector Start):     {0}", FormatStage(S3_RuleEngineDecisionTicks, S4_EjectAttemptStartTicks)));

            if (RetryCount > 0)
            {
                sb.AppendLine(string.Format("  Retry Backoff ({0} {1}):                  {2,8:F3} ms",
                    RetryCount, RetryCount == 1 ? "retry" : "retries", RetryBackoffMs));
            }

            long lockStartTicks = (S4b_FinalAttemptStartTicks > 0) ? S4b_FinalAttemptStartTicks : S4_EjectAttemptStartTicks;
            string s4_to_s5 = (lockStartTicks > 0 && S5_VolumeLockAcquiredTicks <= 0)
                ? "FAILED / NOT COMPLETED"
                : FormatStage(lockStartTicks, S5_VolumeLockAcquiredTicks);
            sb.AppendLine(string.Format("  S4 -> S5 (FSCTL_LOCK_VOLUME):             {0}", s4_to_s5));
            sb.AppendLine(string.Format("  S5 -> S6 (FSCTL_DISMOUNT_VOLUME):         {0}", FormatStage(S5_VolumeLockAcquiredTicks, S6_DismountCompletedTicks)));
            sb.AppendLine(string.Format("  S6 -> S7 (IOCTL_STORAGE_EJECT_MEDIA):     {0}", FormatStage(S6_DismountCompletedTicks, S7_EjectRequestCompletedTicks)));
            sb.AppendLine("  " + new string('-', 50));
            sb.AppendLine(string.Format("  Total Reactive Eject Duration (S0 -> S7): {0} [Result: {1}]", 
                FormatStage(S0_NotificationReceivedTicks, S7_EjectRequestCompletedTicks), 
                EjectSuccess ? "SUCCESS" : "FAIL/SKIPPED"));
            return sb.ToString();
        }
    }

    /// <summary>
    /// Central thread-safe collector for latency timing measurements.
    /// </summary>
    public static class LatencyTracker
    {
        private static readonly object _sync = new object();
        private static UserActionLatencyRecord _lastActionRecord;
        private static SentinelArrivalLatencyRecord _lastArrivalRecord;
        private static long _lastPnpArrivalTicks;

        /// <summary>
        /// Maximum age in seconds before a pending PnP arrival timestamp expires as stale.
        /// Default is 15 seconds.
        /// </summary>
        public static double MaxPnpArrivalAgeSeconds = 15.0;

        public static void RecordPnpArrivalNotification()
        {
            lock (_sync)
            {
                if (_lastPnpArrivalTicks > 0)
                {
                    long now = Stopwatch.GetTimestamp();
                    double ageSec = ((double)(now - _lastPnpArrivalTicks)) / Stopwatch.Frequency;
                    if (ageSec <= MaxPnpArrivalAgeSeconds)
                    {
                        return; // Preserve the original application-observed S0 for this arrival episode!
                    }
                }
                _lastPnpArrivalTicks = Stopwatch.GetTimestamp();
            }
        }

        public static long GetLastPnpArrivalTicks(bool consume = false)
        {
            lock (_sync)
            {
                if (_lastPnpArrivalTicks <= 0) return 0;

                long now = Stopwatch.GetTimestamp();
                double ageSec = ((double)(now - _lastPnpArrivalTicks)) / Stopwatch.Frequency;
                if (ageSec > MaxPnpArrivalAgeSeconds)
                {
                    _lastPnpArrivalTicks = 0; // Stale timestamp expired
                    return 0;
                }

                long ticks = _lastPnpArrivalTicks;
                if (consume) _lastPnpArrivalTicks = 0;
                return ticks;
            }
        }

        public static void ResetPnpArrivalNotification()
        {
            lock (_sync)
            {
                _lastPnpArrivalTicks = 0;
                MaxPnpArrivalAgeSeconds = 15.0;
            }
        }

        public static void StoreActionRecord(UserActionLatencyRecord rec)
        {
            lock (_sync)
            {
                _lastActionRecord = rec;
            }
        }

        public static void StoreArrivalRecord(SentinelArrivalLatencyRecord rec)
        {
            lock (_sync)
            {
                _lastArrivalRecord = rec;
            }
        }

        public static UserActionLatencyRecord GetLastActionRecord()
        {
            lock (_sync) { return _lastActionRecord; }
        }

        public static SentinelArrivalLatencyRecord GetLastArrivalRecord()
        {
            lock (_sync) { return _lastArrivalRecord; }
        }

        /// <summary>
        /// Executes an isolated, safe, non-destructive diagnostic benchmark of all latency subsystems.
        /// </summary>
        public static void RunDiagnosticBenchmark(Action<string> log)
        {
            log("");
            log("================================================================================");
            log("  USB SHIELD PRO v2.0 — HIGH-RESOLUTION LATENCY & PERFORMANCE BENCHMARK");
            log("  (Measures subsystem execution overhead; zero modifications to host policies)");
            log("================================================================================");
            log("");

            Stopwatch sw = new Stopwatch();

            // ── 1. Stopwatch Frequency & Precision ─────────────────────────────
            log(string.Format("High-Resolution Timer Frequency: {0:N0} ticks/sec (Resolution: {1:F3} ns)",
                Stopwatch.Frequency, 1000000000.0 / Stopwatch.Frequency));
            log("IsHighResolution: " + Stopwatch.IsHighResolution);
            log("");

            // ── 2. Registry Read/Write Micro-benchmark (Isolated HKCU Test Key) ─
            log("[Benchmark 1/5] Registry Transaction & Read-Back Overhead (Isolated HKCU Test Key)");
            string testSubkey = @"SOFTWARE\USBShieldPro\LatencyBenchmark";
            double writeMs = 0;
            double readMs = 0;
            double deleteMs = 0;
            try
            {
                // Write test
                sw.Restart();
                using (var k = Registry.CurrentUser.CreateSubKey(testSubkey))
                {
                    k.SetValue("LatencyTestDword", 1, RegistryValueKind.DWord);
                    k.Flush();
                }
                sw.Stop();
                writeMs = sw.Elapsed.TotalMilliseconds;

                // Read-back test
                sw.Restart();
                int readVal = -1;
                using (var k = Registry.CurrentUser.OpenSubKey(testSubkey))
                {
                    if (k != null) readVal = Convert.ToInt32(k.GetValue("LatencyTestDword"));
                }
                sw.Stop();
                readMs = sw.Elapsed.TotalMilliseconds;

                // Cleanup test
                sw.Restart();
                Registry.CurrentUser.DeleteSubKeyTree(testSubkey, false);
                sw.Stop();
                deleteMs = sw.Elapsed.TotalMilliseconds;

                log(string.Format("  Registry Flush & Commit:   {0,7:F3} ms", writeMs));
                log(string.Format("  Registry Read-Back:        {0,7:F3} ms", readMs));
                log(string.Format("  Registry Subkey Deletion:  {0,7:F3} ms", deleteMs));
                log(string.Format("  Total Transaction Cycle:   {0,7:F3} ms", writeMs + readMs + deleteMs));
            }
            catch (Exception ex)
            {
                log("  Registry Benchmark Failed: " + ex.Message);
            }
            log("");

            // ── 3. RuleEngine In-Memory Decision Latency ────────────────────────
            log("[Benchmark 2/5] RuleEngine In-Memory Decision Latency");
            DeviceIdentity sampleDev = new DeviceIdentity
            {
                FriendlyName = "Benchmark Flash Disk",
                SerialNumber = "119627AD",
                DeviceInstanceId = @"USBSTOR\DISK&VEN_GENERIC&PROD_FLASH_DISK\119627AD&0",
                IsRemovableMedia = true,
                PhysicalTransport = PhysicalBusType.Usb,
                Category = DeviceCategory.UsbFlashDrive,
                Confidence = IdentityConfidence.Strong
            };

            ProtectionPolicy samplePolicy = new ProtectionPolicy
            {
                WhitelistMode = true
            };

            // Warmup
            RuleEngine.Evaluate(sampleDev, samplePolicy, false);

            int iterations = 1000;
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                RuleEngine.Evaluate(sampleDev, samplePolicy, false);
            }
            sw.Stop();
            double perEvalMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000.0) / iterations;
            log(string.Format("  Evaluated {0:N0} iterations in {1:F3} ms", iterations, sw.Elapsed.TotalMilliseconds));
            log(string.Format("  Average RuleEngine Decision: {0:F3} microseconds ({1:F4} ms)", perEvalMicroseconds, perEvalMicroseconds / 1000.0));
            log("");

            // ── 4. Full Device Discovery & WMI Query Latency ────────────────────
            log("[Benchmark 3/5] Device Discovery (WMI Scan + Partition Mapping Latency)");
            sw.Restart();
            List<DeviceIdentity> devices = DeviceDiscovery.DiscoverAllDevices();
            sw.Stop();
            double wmiDiscoveryMs = sw.Elapsed.TotalMilliseconds;
            log(string.Format("  Synchronous WMI Scan:      {0,7:F2} ms (Discovered {1} device(s))", wmiDiscoveryMs, devices.Count));
            if (wmiDiscoveryMs > 50.0)
            {
                log("  * Note: WMI query execution is the dominant source of synchronous UI latency!");
                log("  * Optimization: Must NEVER be executed directly on the UI thread without Task.Run.");
            }
            log("");

            // ── 5. SetupAPI / DriveInfo Native Enumeration Latency ──────────────
            log("[Benchmark 4/5] Native DriveInfo / Volume Enumeration Latency");
            sw.Restart();
            DriveInfo[] drives = DriveInfo.GetDrives();
            int readyCount = 0;
            foreach (var d in drives)
            {
                if (d.IsReady) readyCount++;
            }
            sw.Stop();
            double driveInfoMs = sw.Elapsed.TotalMilliseconds;
            log(string.Format("  DriveInfo.GetDrives():     {0,7:F3} ms (Enumerated {1} drives, {2} ready)", driveInfoMs, drives.Length, readyCount));
            log("");

            // ── 5. Architectural Stage Classification & Benchmarks ───────────────────
            double totalRegMs = writeMs + readMs + deleteMs;
            log("[Benchmark 5/5] Subsystem Stage Classification & Measurements");
            log("  [MEASURED BENCHMARK] Registry Commit Cycle (T2->T4):          " + string.Format("{0:F3} ms", totalRegMs));
            log("  [MEASURED BENCHMARK] RuleEngine Decision Latency (S2->S3):     " + string.Format("{0:F3} \u00b5s ({1:F6} ms)", perEvalMicroseconds, perEvalMicroseconds / 1000.0));
            log("  [MEASURED BENCHMARK] WMI Device Discovery Duration (S1->S2):   " + string.Format("{0:F3} ms", wmiDiscoveryMs));
            log("  [MEASURED BENCHMARK] DriveInfo Volume Enumeration:             " + string.Format("{0:F3} ms", driveInfoMs));
            log("");
            log("  Stage T (User Protection Toggle Stages):");
            log("    T0 -> T1 (UI Event Dispatch):             ~0.05 - 0.20 ms  [THEORETICAL ESTIMATE]");
            log("    T1 -> T2 (Pre-Flight & Journal Prep):     ~0.10 - 0.50 ms  [THEORETICAL ESTIMATE]");
            log("    T2 -> T3 (Registry Transaction Commit):   " + string.Format("{0:F3} ms", writeMs) + "         [MEASURED BENCHMARK]");
            log("    T3 -> T4 (Read-Back Verification):        " + string.Format("{0:F3} ms", readMs) + "         [MEASURED BENCHMARK]");
            log("    T4 -> T5 (Refresh Task Dispatch):         ~0.05 - 0.15 ms  [THEORETICAL ESTIMATE]");
            log("    T5 -> T6 (Background Device Scan):        " + string.Format("{0:F3} ms", wmiDiscoveryMs) + "       [MEASURED BENCHMARK]");
            log("    T6 -> T7 (UI Invalidation & Paint):       ~2.00 - 8.00 ms  [THEORETICAL ESTIMATE]");
            log("");
            log("  Stage S (Sentinel Reactive Arrival & Ejection Stages):");
            log("    S0 -> S1 (PnP Arrival Event Dispatch):    ~0.50 - 2.00 ms  [THEORETICAL ESTIMATE]");
            log("    S1 -> S2 (DeviceIdentity WMI Resolution): " + string.Format("{0:F3} ms", wmiDiscoveryMs) + "       [MEASURED BENCHMARK]");
            log("    S2 -> S3 (RuleEngine Decision):           " + string.Format("{0:F3} \u00b5s", perEvalMicroseconds) + "          [MEASURED BENCHMARK]");
            log("    S3 -> S4 (Dispatch to DeviceEjector):     ~0.01 - 0.05 ms  [THEORETICAL ESTIMATE]");
            log("    S4 -> S5 (FSCTL_LOCK_VOLUME):             NOT YET MEASURED (Awaiting authorized physical test)");
            log("    S5 -> S6 (FSCTL_DISMOUNT_VOLUME):         NOT YET MEASURED (Awaiting authorized physical test)");
            log("    S6 -> S7 (IOCTL_STORAGE_EJECT_MEDIA):     NOT YET MEASURED (Awaiting authorized physical test)");
            log("  Total Reactive Latency Window:              NOT YET FULLY MEASURED (S4->S7 awaiting live physical event)");
            log("");

            var lastAction = GetLastActionRecord();
            if (lastAction != null)
            {
                log("Last Live Recorded User Action Trace (LIVE MEASURED EVENT):");
                log(lastAction.FormatReport());
            }

            var lastArrival = GetLastArrivalRecord();
            if (lastArrival != null)
            {
                log("Last Live Recorded Sentinel Arrival Trace (LIVE MEASURED EVENT):");
                log(lastArrival.FormatReport());
            }
            else
            {
                log("No live Sentinel arrival/ejection trace recorded yet.");
            }

            log("================================================================================");
            log("Latency benchmark completed successfully.");
            log("================================================================================");
        }
    }
}
