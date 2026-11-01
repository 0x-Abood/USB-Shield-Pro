// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Per-Discovery Immutable Timing Snapshot
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Text;

namespace UsbShieldPro.Models
{
    /// <summary>
    /// Thread-safe immutable timing snapshot captured per DiscoverAllDevices() invocation.
    /// Eliminates static metric overwriting and isolates S1->S2 sub-stage observability.
    /// </summary>
    public class DiscoveryTimingSnapshot
    {
        public double TotalDiscoveryMs { get; set; }
        public double DiskDriveWmiMs { get; set; }
        public double VolumeMappingMs { get; set; }
        public double IoctlStorageMs { get; set; }
        public double PnpAncestryMs { get; set; }
        public double WpdSetupApiMs { get; set; }
        public double WpdDedupMs { get; set; }
        public int WpdWmiCalls { get; set; }
        public int DiskCandidateCount { get; set; }
        public int TotalDeviceCount { get; set; }
        public DateTime TimestampUtc { get; set; }

        public double UninstrumentedMs
        {
            get
            {
                double accounted = DiskDriveWmiMs + VolumeMappingMs + IoctlStorageMs + PnpAncestryMs + WpdSetupApiMs + WpdDedupMs;
                double remainder = TotalDiscoveryMs - accounted;
                return remainder > 0.0 ? remainder : 0.0;
            }
        }

        public DiscoveryTimingSnapshot()
        {
            TimestampUtc = DateTime.UtcNow;
        }

        public DiscoveryTimingSnapshot Clone()
        {
            return new DiscoveryTimingSnapshot
            {
                TotalDiscoveryMs = this.TotalDiscoveryMs,
                DiskDriveWmiMs = this.DiskDriveWmiMs,
                VolumeMappingMs = this.VolumeMappingMs,
                IoctlStorageMs = this.IoctlStorageMs,
                PnpAncestryMs = this.PnpAncestryMs,
                WpdSetupApiMs = this.WpdSetupApiMs,
                WpdDedupMs = this.WpdDedupMs,
                WpdWmiCalls = this.WpdWmiCalls,
                DiskCandidateCount = this.DiskCandidateCount,
                TotalDeviceCount = this.TotalDeviceCount,
                TimestampUtc = this.TimestampUtc
            };
        }

        public string FormatBreakdown()
        {
            return string.Format(
                "DiskWmi={0,6:F1} ms, VolMap={1,5:F1} ms, Ioctl={2,5:F1} ms, PnpAncestry={3,5:F1} ms, Wpd={4,5:F1} ms, Dedup={5,5:F1} ms, Uninstrumented={6,5:F1} ms [Candidates: {7}, Total: {8}]",
                DiskDriveWmiMs, VolumeMappingMs, IoctlStorageMs, PnpAncestryMs, WpdSetupApiMs, WpdDedupMs, UninstrumentedMs, DiskCandidateCount, TotalDeviceCount);
        }
    }
}
