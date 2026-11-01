// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Protection Policy Configuration Model
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;

namespace UsbShieldPro.Models
{
    /// <summary>
    /// Represents the desired user-configured protection policy matrix.
    /// </summary>
    public class ProtectionPolicy
    {
        public bool BlockAllRemovable { get; set; }
        public bool BlockRemovableDisks { get; set; }
        public bool BlockFlashDrivesOnlyReactive { get; set; }
        public bool BlockWpdPhonesAndMedia { get; set; }
        public bool ReadOnlyRemovable { get; set; }
        public bool HardBlockUsbStor { get; set; }
        public bool WhitelistMode { get; set; }       // Normal Trusted-Only Mode (Active Sentinel Auto-Eject)
        public bool StrictPnpLockdown { get; set; }   // Strict Trusted Device Mode (Device Installation PnP Restrictions)
        public bool AutoPlayGuard { get; set; }       // AutoPlay Guard (Disable Windows AutoPlay for media and devices)

        // Compatibility alias: maps old BlockFlashDrives to BlockRemovableDisks
        public bool BlockFlashDrives
        {
            get { return BlockRemovableDisks; }
            set { BlockRemovableDisks = value; }
        }

        public int PolicyVersion { get; set; }
        public DateTime LastModifiedUtc { get; set; }

        public ProtectionPolicy()
        {
            PolicyVersion = 2;
            LastModifiedUtc = DateTime.UtcNow;
        }

        public void IncrementVersion()
        {
            PolicyVersion++;
            LastModifiedUtc = DateTime.UtcNow;
        }

        public ProtectionPolicy Clone()
        {
            return new ProtectionPolicy
            {
                BlockAllRemovable = this.BlockAllRemovable,
                BlockRemovableDisks = this.BlockRemovableDisks,
                BlockFlashDrivesOnlyReactive = this.BlockFlashDrivesOnlyReactive,
                BlockWpdPhonesAndMedia = this.BlockWpdPhonesAndMedia,
                ReadOnlyRemovable = this.ReadOnlyRemovable,
                HardBlockUsbStor = this.HardBlockUsbStor,
                WhitelistMode = this.WhitelistMode,
                StrictPnpLockdown = this.StrictPnpLockdown,
                AutoPlayGuard = this.AutoPlayGuard,
                PolicyVersion = this.PolicyVersion,
                LastModifiedUtc = this.LastModifiedUtc
            };
        }

        public bool IsAnyProtectionActive()
        {
            return BlockAllRemovable ||
                   BlockRemovableDisks ||
                   BlockFlashDrivesOnlyReactive ||
                   BlockWpdPhonesAndMedia ||
                   ReadOnlyRemovable ||
                   HardBlockUsbStor ||
                   WhitelistMode ||
                   StrictPnpLockdown ||
                   AutoPlayGuard;
        }

        public static bool AreEquivalent(ProtectionPolicy a, ProtectionPolicy b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            return a.BlockAllRemovable == b.BlockAllRemovable &&
                   a.BlockRemovableDisks == b.BlockRemovableDisks &&
                   a.BlockFlashDrivesOnlyReactive == b.BlockFlashDrivesOnlyReactive &&
                   a.BlockWpdPhonesAndMedia == b.BlockWpdPhonesAndMedia &&
                   a.ReadOnlyRemovable == b.ReadOnlyRemovable &&
                   a.HardBlockUsbStor == b.HardBlockUsbStor &&
                   a.WhitelistMode == b.WhitelistMode &&
                   a.StrictPnpLockdown == b.StrictPnpLockdown &&
                   a.AutoPlayGuard == b.AutoPlayGuard;
        }
    }
}
