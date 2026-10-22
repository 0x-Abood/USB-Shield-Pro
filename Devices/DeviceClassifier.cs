// ═══════════════════════════════════════════════════════════════════════════
// USB Shield Pro v2.0 — Device Classification Engine
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using UsbShieldPro.Models;

namespace UsbShieldPro.Devices
{
    /// <summary>
    /// Accurately categorizes devices by decoupling physical transport,
    /// Windows storage class, and user-friendly categories.
    /// </summary>
    public static class DeviceClassifier
    {
        public static DeviceCategory Classify(DeviceIdentity device)
        {
            if (device == null) return DeviceCategory.Unknown;

            // 1. Safety Guard Check
            if (device.IsSystemOrBootDrive() ||
                device.PhysicalTransport == PhysicalBusType.Nvme ||
                device.PhysicalTransport == PhysicalBusType.Sata ||
                device.PhysicalTransport == PhysicalBusType.Ata)
            {
                return DeviceCategory.InternalFixedDisk;
            }

            if (device.IsCriticalPeripheral())
            {
                return DeviceCategory.CriticalSystemHardware;
            }

            // 2. Windows Portable Devices (Phones, Cameras, Media Players via MTP/PTP)
            if (device.WindowsClass == StorageClass.WindowsPortableDevice)
            {
                return DeviceCategory.MobilePhoneOrMediaDevice;
            }

            // 3. Optical Drives
            if (device.WindowsClass == StorageClass.CdRom)
            {
                return DeviceCategory.OpticalDrive;
            }

            // 4. USB Storage Classification
            if (device.PhysicalTransport == PhysicalBusType.Usb)
            {
                // If it exposes the Removable Media bit, it's typically a Flash Thumb Drive
                if (device.IsRemovableMedia)
                {
                    return DeviceCategory.UsbFlashDrive;
                }

                // If non-removable (Fixed USB disk), inspect seek penalty (SSD vs HDD)
                if (device.HasSeekPenalty.HasValue)
                {
                    if (device.HasSeekPenalty.Value == false)
                    {
                        return DeviceCategory.ExternalUsbSsd; // Non-rotational = SSD / NVMe enclosure
                    }
                    else
                    {
                        return DeviceCategory.ExternalUsbHdd; // Rotational = Spindle HDD
                    }
                }

                // Check FriendlyName and Model for common flash/thumb drive indicators (e.g. fixed-bit flash drives)
                string nameAndModel = ((device.FriendlyName ?? "") + " " + (device.Model ?? "")).ToUpperInvariant();
                string[] flashKeywords = new string[] { "FLASH", "THUMB", "CRUZER", "ULTRA FIT", "DATATRAVELER", "BAR PLUS", "JUMPDRIVE" };
                for (int i = 0; i < flashKeywords.Length; i++)
                {
                    if (nameAndModel.Contains(flashKeywords[i]))
                    {
                        return DeviceCategory.UsbFlashDrive;
                    }
                }

                // Fallback: If total size is small (<= 128GB), likely flash drive; otherwise external drive
                if (device.TotalSizeBytes > 0 && device.TotalSizeBytes <= (137438953472L)) // 128 GB
                {
                    return DeviceCategory.UsbFlashDrive;
                }

                return DeviceCategory.ExternalUsbSsd;
            }

            return DeviceCategory.Unknown;
        }
    }
}
