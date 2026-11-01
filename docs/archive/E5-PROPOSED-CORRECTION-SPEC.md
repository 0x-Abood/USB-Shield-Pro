# USB SHIELD PRO — PROPOSED CORRECTION SPECIFICATION

**Document Reference:** `E5-PROPOSED-CORRECTION-SPEC.md`  
**Security Classification:** Engineering Specification & Security Implementation Plan  
**Target Candidate Binary:** `UsbShieldPro_Candidate.exe` (SHA-256: `B5F93964176C7FE6F8E17EF7F5744213C1F6E69755E86A6449E2E7A313B8F3B0`)  
**Baseline Preserved:** `UsbShieldPro.exe` (SHA-256: `636ACCCE8E9689CDCD109042129480805616238C68FAB881C22DD33B9AC43A33`)  
**Date of Audit:** October 12, 2026  

---

## 1. Architectural Strategy & Scope

The remediation for incident `E5-USB-04` bridges the gap between passive Windows Group Policy settings and active user-mode containment. 

Rather than relying solely on passive registry keys that fail on fixed disks or multi-partition layouts, the engine now employs an **Active Dual-Layer Defense Architecture**:
1. **Layer 1 (Passive Defense):** Windows Group Policy registry settings (`RemovableStorageDevices\Deny_All`, etc.) remain active to prevent standard OS mounting whenever applicable.
2. **Layer 2 (Active User-Mode Containment):** `RuleEngine` deterministically mandates `ShouldAutoEject = true` for all blocked USB storage. `ActiveSentinel` immediately captures the arriving device, unmounts all sibling partitions (`E:`, `F:`), and executes a PnP devnode ejection at the USB hub child level.
3. **Layer 3 (Real-Time Service Synchronization):** Zero-latency event-driven wakeups via `AutoResetEvent` and IPC synchronization eliminate the 3000ms polling latency and prevent stale policy configurations in the background service.

---

## 2. Detailed Code Modifications

### 2.1 `Core\RuleEngine.cs`

#### A. Gate 2: Class Policy Override on Trusted Whitelist Devices
```csharp
// Before (RC5):
result.RecommendedAction = RuleAction.DenyBlocked;
result.DisplayBadge = "CLASS BLOCKED (TRUST OVERRIDDEN)";
result.Confidence = IdentityConfidence.Definite;
result.ShouldAutoEject = false; // Bug: no ejection

// After (Candidate):
result.RecommendedAction = RuleAction.DenyBlocked;
result.DisplayBadge = "CLASS BLOCKED (TRUST OVERRIDDEN)";
result.Confidence = IdentityConfidence.Definite;
result.ShouldAutoEject = true;  // Fixed: active auto-ejection mandated
```

#### B. Gate 4: General Policy Blocks (`BlockAllRemovable` & `BlockRemovableDisks`)
```csharp
// Before (RC5):
result.RecommendedAction = RuleAction.DenyBlocked;
result.DisplayBadge = "POLICY BLOCK";
result.Confidence = IdentityConfidence.Definite;
result.AllocatedPolicyIndex = 0;
result.ShouldAutoEject = false; // Bug: no ejection

// After (Candidate):
result.RecommendedAction = RuleAction.DenyBlocked;
result.DisplayBadge = "POLICY BLOCK";
result.Confidence = IdentityConfidence.Definite;
result.AllocatedPolicyIndex = 0;

// Mandate active auto-ejection for all USB storage devices (both Removable Media
// and Fixed Disk presentations like Ventoy or USB SSDs)
bool isStorageDevice = (dev.WindowsClass == StorageClass.DiskDrive ||
                        dev.Category == DeviceCategory.UsbFlashDrive ||
                        dev.Category == DeviceCategory.ExternalDrive ||
                        dev.Category == DeviceCategory.MassStorage ||
                        dev.IsRemovableMedia ||
                        dev.PhysicalTransport == PhysicalBusType.Usb);

result.ShouldAutoEject = isStorageDevice; // Fixed: active auto-ejection mandated
```

---

### 2.2 `Devices\ActiveSentinel.cs`

#### A. Added `IsAutoEjectRequired` Dynamic Policy Evaluator
```csharp
public static bool IsAutoEjectRequired(DeviceIdentity dev, ProtectionPolicy policy)
{
    if (policy == null || dev == null) return false;

    // 1. Whitelist mode enforces auto-eject on untrusted devices
    if (policy.WhitelistMode) return true;

    // 2. Master Lock (BlockAllRemovable) enforces auto-eject on all storage
    if (policy.BlockAllRemovable) return true;

    // 3. Removable Disks policy enforces auto-eject on disk storage
    if (policy.BlockRemovableDisks && (dev.WindowsClass == StorageClass.DiskDrive ||
                                       dev.Category == DeviceCategory.UsbFlashDrive ||
                                       dev.Category == DeviceCategory.ExternalDrive ||
                                       dev.Category == DeviceCategory.MassStorage ||
                                       dev.PhysicalTransport == PhysicalBusType.Usb))
    {
        return true;
    }

    // 4. Reactive Flash Drive Only Mode
    if (policy.BlockFlashDrivesOnlyReactive && (dev.Category == DeviceCategory.UsbFlashDrive ||
                                               dev.IsRemovableMedia))
    {
        return true;
    }

    return false;
}
```

#### B. Replaced Hardcoded `!effectivePolicy.WhitelistMode` Gates
In lines 625, 838, and 1000:
```csharp
// Before:
if (effectivePolicy == null || !effectivePolicy.WhitelistMode) return;

// After:
if (!IsAutoEjectRequired(dev, effectivePolicy)) return;
```

---

### 2.3 `Service\UsbShieldService.cs`

#### A. Event-Driven Wakeups & Reduced Latency
- Added `private readonly AutoResetEvent _wakeEvent = new AutoResetEvent(false);`
- Implemented `public void WakeSentinelSweep()`:
  ```csharp
  public void WakeSentinelSweep()
  {
      try { _wakeEvent.Set(); } catch { }
  }
  ```
- Wired `StorageNotificationManager.TopologyWakeAction = WakeSentinelSweep;`
- Replaced `Thread.Sleep(3000)` with `_wakeEvent.WaitOne(1000)` in `SentinelWorkerLoop`.

#### B. Dynamic Policy Reload on Each Sentinel Sweep
```csharp
// Reload active policy on sweep to ensure immediate synchronization with settings store
try
{
    ProtectionPolicy refreshedPolicy = SettingsRepository.LoadPolicy();
    if (refreshedPolicy != null)
    {
        _currentPolicy = refreshedPolicy;
    }
}
catch { }
```

#### C. IPC Synchronization & Commands
- Updated `APPLYPOLICY` command to save policy and immediately pulse `WakeSentinelSweep()`.
- Added `WAKESENTINEL` and `EVALUATENOW` IPC commands for instant GUI-triggered sweeps.

---

### 2.4 `MainForm.cs`

#### A. GUI-to-Service Policy Synchronization
Added `SynchronizePolicyWithService(ProtectionPolicy policy)` helper:
```csharp
private void SynchronizePolicyWithService(ProtectionPolicy policy)
{
    if (policy == null) return;
    try
    {
        if (ServiceManager.IsServiceRunning())
        {
            ServiceManager.SendIpcRequest("APPLYPOLICY",
                new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(policy));
            ServiceManager.SendIpcRequest("WAKESENTINEL");
        }
    }
    catch { }
}
```
Wired to:
- `ExecuteQuickLock()`
- `ExecuteQuickUnlock()`
- `ApplyPolicyFromUi()`

#### B. PnP Arrival Trigger Pulse
In `PnpWorkerLoop`, when a device arrival notification is processed while the service is authoritative:
```csharp
try { ServiceManager.SendIpcRequest("WAKESENTINEL"); } catch { }
```

---

## 3. Verification & Safety Safeguards

1. **Safety Rejection Invariant:** If a device is determined to be the Windows system drive (`C:`) or a local boot drive, `VolumeEjectBackend` and `ActiveSentinel` immediately abort ejection with `DeviceEjectResult.SafetyRejected`.
2. **Cancellation on Protection Disable:** When the user turns off protection or selects `Unrestricted`, `IsAutoEjectRequired` returns `false`, immediately canceling any pending ejection queues.
3. **Immutable Baseline Intact:** All changes are isolated in `UsbShieldPro_Candidate.exe`. The baseline `UsbShieldPro.exe` is unchanged.
