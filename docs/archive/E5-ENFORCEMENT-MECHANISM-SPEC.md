# USB SHIELD PRO — WINDOWS STORAGE ENFORCEMENT MECHANISM SPECIFICATION

**Document Reference:** `E5-ENFORCEMENT-MECHANISM-SPEC.md`  
**Security Classification:** Engineering Specification & Security Architecture  
**Target Operating Systems:** Windows 10 (21H2–22H2), Windows 11 (22H2, 23H2, 24H2, 25H2)  
**Target Architecture:** x64 / ARM64  
**Date of Audit:** October 12, 2026  

---

## 1. Overview of Windows USB Storage Stack

To design reliable security enforcement without introducing system instability, we must analyze the exact Windows storage architecture from hardware bus arrival to user-mode file system access.

```
+-------------------------------------------------------------------------+
|                         User Applications / Explorer                    |
+-------------------------------------------------------------------------+
                                     |
                                     v
+-------------------------------------------------------------------------+
|        File System Drivers (FLTMGR, NTFS.sys, FAT32.sys, exFAT.sys)     |
+-------------------------------------------------------------------------+
                                     |
                                     v
+-------------------------------------------------------------------------+
|        Volume Manager (VOLMGR.SYS) & Mount Manager (MOUNTMGR.SYS)       |
|                 -> Assigns drive letters (E:, F:, etc.)                 |
+-------------------------------------------------------------------------+
                                     |
                                     v
+-------------------------------------------------------------------------+
|                  Partition Manager (PARTMGR.SYS)                        |
|            -> Interprets MBR/GPT, creates partition devnodes            |
+-------------------------------------------------------------------------+
                                     |
                                     v
+-------------------------------------------------------------------------+
|                     Disk Class Driver (DISK.SYS)                        |
|                 -> Issues SCSI INQUIRY (Checks RMB bit)                 |
+-------------------------------------------------------------------------+
                                     |
                                     v
+-------------------------------------------------------------------------+
|                   USB Storage Driver (USBSTOR.SYS)                      |
|                  -> Translates SCSI commands to USB BOT/UASP            |
+-------------------------------------------------------------------------+
                                     |
                                     v
+-------------------------------------------------------------------------+
|                   USB Host Controller / Hub Drivers                     |
|           (USBXHCI.SYS, USBHUB3.SYS) -> Enumerates physical ports       |
+-------------------------------------------------------------------------+
```

---

## 2. Evaluation of Windows Enforcement Mechanisms

### 2.1 Mechanism A: Passive Group Policy Registry Keys (Current Primary in RC5)

Windows Group Policy provides storage restriction policies under:
`HKLM\SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices`
- `Deny_All` (DWORD = 1): Blocks all removable storage devices.
- `{53f5630d-b6bf-11d0-94f2-00a0c91efb8b}` (Removable Storage Class GUID):
  - `Deny_Read` (DWORD = 1)
  - `Deny_Write` (DWORD = 1)
  - `Deny_Execute` (DWORD = 1)

#### Capabilities & Limitations:
1. **Passive vs Active Enforcement:** Writing directly to the registry via `Microsoft.Win32.Registry` modifies data in hive files, but DOES NOT trigger an immediate Group Policy broadcast. The Windows Group Policy engine (`gpsvc`) typically applies policies during user logon, background refresh (every 90–120 minutes), or explicit `gpupdate /force`.
2. **The Removable Media Bit (RMB) Boundary:**
   - Group Policy applies strictly to storage devices reporting `FILE_REMOVABLE_MEDIA` in response to `IOCTL_STORAGE_GET_DEVICE_NUMBER` and SCSI INQUIRY bit 7 byte 1 (`RMB = 1`).
   - If a USB flash drive or SSD presents as a **Fixed Disk** (`RMB = 0`), Windows treats it as a non-removable local hard disk. The `RemovableStorageDevices` policy keys are **completely ignored** by `volmgr.sys` and `partmgr.sys`!
3. **Multi-Partition Tools (Ventoy, Rufus, WinToUSB):**
   - Ventoy formats physical drives with an MBR table creating:
     - Partition 1: Large exFAT/NTFS volume for ISO storage (`E:`).
     - Partition 2: Small FAT16/FAT32 volume with VTOYEFI bootloaders (`F:`).
   - On many modern USB 3.0/3.1 controllers, the controller firmware exposes fixed-disk descriptors (`RMB = 0`), which renders Group Policy registry keys 100% ineffective against read operations.

---

### 2.2 Mechanism B: Driver Service Disable (`USBSTOR Start = 4`)

Setting `HKLM\SYSTEM\CurrentControlSet\Services\USBSTOR\Start = 4` prevents Windows from loading `usbstor.sys`.

#### Capabilities & Limitations:
1. **Absolute Prevention:** When `USBSTOR` is set to 4, any newly inserted USB storage device fails to start with error code 28 / 31 (Driver Not Found / Service Disabled).
2. **No Per-Device Granularity:** Disabling the service disables ALL USB mass storage devices globally.
3. **Incompatible with Whitelist:** If `USBSTOR` is stopped, trusted USB devices CANNOT be authorized, because Windows refuses to create the storage devnode for any USB drive.
4. **Already Plugged Devices:** Does not affect USB drives already connected and running before the key was set.

---

### 2.3 Mechanism C: DevNode Disablement via SetupAPI / CfgMgr32 (`CM_Disable_DevNode`)

Calling `CM_Disable_DevNode` on the PnP device instance node (`USBSTOR\DISK&VEN_...` or `USB\VID_...`).

#### Capabilities & Limitations:
1. **Strong OS-Level Halt:** Disabling the devnode stops the device driver stack and marks the device with Code 22 in Device Manager.
2. **Handle Interruption:** When disabled, Windows forcibly invalidates volume device objects.
3. **Elevated Privilege Requirement:** Requires `SE_LOAD_DRIVER_NAME` or LocalSystem administrator privileges.
4. **Recovery Complexity:** If the application crashes while devices are disabled, the user cannot re-enable them without Device Manager or system restart.

---

### 2.4 Mechanism D: Multi-Layered Reactive Safe Ejection & Dismount (Enhanced RC5 Solution)

User-mode active containment using Win32 Device Management and Storage IOCTLs:
1. **Volume Level (VolumeEjectBackend):**
   - Open volume handle: `CreateFileW(@"\\.\E:", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, ...)`
   - Lock Volume: `DeviceIoControl(hVol, FSCTL_LOCK_VOLUME, ...)`
   - Dismount Volume: `DeviceIoControl(hVol, FSCTL_DISMOUNT_VOLUME, ...)`
   - Prevent Removal Off: `DeviceIoControl(hVol, IOCTL_STORAGE_MEDIA_REMOVAL, PreventMediaRemoval = false, ...)`
   - Eject Volume: `DeviceIoControl(hVol, IOCTL_STORAGE_EJECT_MEDIA, ...)`
2. **PnP Parent DevNode Level (PnpSafeEjectBackend):**
   - Query PnP DevNode for the physical USB hub child: `USB\VID_058F&PID_6387\12345678`.
   - Request Device Ejection: `CM_Request_Device_EjectW(devInst, out vetoType, vetoName, 256, 0)`.
   - If vetoed by open handles, invoke `CM_Query_And_Remove_SubTreeW(devInst, out vetoType, vetoName, 256, CM_REMOVE_NO_RESTART)`.

#### Why This Completely Handles Ventoy & Fixed Disks:
- Even if Windows treats Ventoy as a fixed disk and ignores Group Policy, invoking `PnpSafeEjectBackend` tears down the entire physical USB devnode.
- Tearing down the parent devnode immediately invalidates ALL child disk and volume objects (`E:` and `F:` simultaneously), terminating all active file read handles and removing the drive letters from Windows Explorer within milliseconds.

---

## 3. Storage Stack Comparison Matrix

| Enforcement Mechanism | Scope | Whitelist Compatible? | Fixed Disks (RMB=0)? | Ventoy Multi-Partition? | Latency |
|---|---|---|---|---|---|
| **Group Policy Registry** | Passive / System | Yes (with gaps) | **NO** (Bypassed) | **NO** (Bypassed) | Infinite (Requires GPSVC refresh) |
| **USBSTOR Start = 4** | Global Service | **NO** (Blocks all) | Yes | Yes | Immediate on next insert |
| **DevNode Disablement** | Target DevNode | Yes | Yes | Yes | ~50–100ms |
| **Reactive PnP Ejection** | Target DevNode | Yes | Yes | Yes | ~50–200ms |
| **Volume Dismount IOCTL** | Individual Volume | Yes | Yes | Yes (Must dismount all siblings) | ~20–80ms per volume |

---

## 4. Multi-Partition Topology Dismount Requirements

When a multi-partition drive like Ventoy arrives:
1. **Container Tracking:** The engine must identify all volume drive letters sharing the same `PhysicalDiskNumber` or `PhysicalUsbParentInstanceId`.
2. **Atomic Sibling Dismount:** Ejecting volume `E:` alone leaves `F:` accessible. All sibling volumes (`E:`, `F:`) must be dismounted in parallel.
3. **PnP Parent Ejection Fallback:** If volume handles on `E:` are locked by user processes, `ActiveSentinel` must escalate to `PnpSafeEjectBackend` on the parent container node `USB\VID_...`, forcibly severing the USB connection at the hub level.
