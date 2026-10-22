# USB SHIELD PRO — E5 LABORATORY RETEST MATRIX

**Document Reference:** `E5-RETEST-MATRIX.md`  
**Security Classification:** Laboratory Verification Runbook & Retest Protocol  
**Environment:** Isolated Windows 11 25H2 Guest VM (VMware Workstation 17.5.2)  
**Target Candidate Binary:** `UsbShieldPro_Candidate.exe`  
**Target Candidate SHA-256:** `B5F93964176C7FE6F8E17EF7F5744213C1F6E69755E86A6449E2E7A313B8F3B0`  
**Baseline Hash (Comparison):** `636ACCCE8E9689CDCD109042129480805616238C68FAB881C22DD33B9AC43A33`  
**Date of Audit:** October 12, 2026  

---

## 1. Laboratory Environment Pre-Requisites

Before conducting tests in the guest VM:
1. Revert the Windows 11 25H2 VM to its clean baseline snapshot: `Clean-Win11-25H2-Baseline`.
2. Verify host workstation isolation (USB devices connected exclusively to VM via VMware Removable Devices passthrough).
3. Copy `UsbShieldPro_Candidate.exe` into the guest VM directory `C:\Testing\USBShield\`.
4. Ensure no residual test logs, previous configuration files, or temporary keys exist in `C:\ProgramData\USBShieldPro\`.

---

## 2. Test Execution Matrix

| Test ID | Test Category | Target Device Presentation | Configured Policy | Expected System & OS Behavior | Pass / Fail Criteria |
|---|---|---|---|---|---|
| **E5-RET-01** | **Multi-Partition Flash Drive** | Generic Flash Disk (Ventoy: `E:` exFAT + `F:` VTOYEFI FAT) | **Master Lock** (`BlockAllRemovable = true`) | Arriving device detected; sibling volumes `E:` and `F:` dismounted; PnP devnode ejected. | **PASS:** File reads fail immediately; drive letters vanish from Explorer; log states `Blocked by policy (Auto-Ejected)`. |
| **E5-RET-02** | **Standard Flash Drive** | Sandisk Cruzer / Kingston DT (`E:` single partition, `RMB = 1`) | **Master Lock** (`BlockAllRemovable = true`) | Arriving device detected; volume `E:` dismounted; PnP devnode ejected. | **PASS:** Copy attempts to desktop abort with `The device is not ready` or `Access Denied`; drive ejected. |
| **E5-RET-03** | **Fixed-Disk USB SSD** | Portable SSD (`E:` single NTFS partition, `RMB = 0`, Fixed Disk) | **Block Removable Disks** (`BlockRemovableDisks = true`) | Arriving fixed-disk USB storage detected; volume dismounted; devnode ejected. | **PASS:** Device access denied; drive ejected despite `RMB = 0` presentation. |
| **E5-RET-04** | **Trusted Whitelist Device** | Authorized USB Flash Drive with matching Serial & VID/PID | **Whitelist Mode** (`WhitelistMode = true`, Master Lock = false) | Arriving device evaluated against rules; rule matches with high confidence; device permitted. | **PASS:** Volumes mount normally; operator can read and write files; NO ejection occurs; badge displays `TRUSTED`. |
| **E5-RET-05** | **Class Override on Trusted Device** | Authorized USB Flash Drive with matching Serial & VID/PID | **Whitelist Mode** + **Master Lock** | Rule matches, but Master Lock class policy overrides trust; device ejected. | **PASS:** Device ejected despite trust rule; badge displays `CLASS BLOCKED`; files inaccessible. |
| **E5-RET-06** | **Policy Unlock & Cancellation** | Any Blocked Device | **Unrestricted** (Quick Unlock executed) | Policy removed; registry keys cleaned; pending ejection queues immediately cancelled. | **PASS:** Connecting USB device mounts successfully; full read/write access restored. |
| **E5-RET-07** | **Headless Background Service** | Generic Flash Disk (Ventoy: `E:` + `F:`) | **Master Lock** (GUI closed, Service active) | Service receives arrival notification; wakes sentinel sweep via `AutoResetEvent`; dismounts volumes. | **PASS:** Device ejected in < 500ms without GUI running; Event Log confirms service-driven ejection. |
| **E5-RET-08** | **Emergency Rollback Script** | System in Locked State | Invoke `USBShield_Restore.bat` | Script executes; cleans `HKLM` policy keys; restores baseline Windows policies. | **PASS:** USB functionality 100% restored without reboot; verification tool reports `CLEAN`. |

---

## 3. Step-by-Step Operator Procedure for E5-RET-01 (Ventoy Failure Verification)

1. **Step 1:** Launch `UsbShieldPro_Candidate.exe` as Administrator inside the Windows 11 VM.
2. **Step 2:** Ensure the background service is installed and running (`Status: Running`).
3. **Step 3:** Click **Lock All USB Storage** (Master Lock). Confirm UI displays `PROTECTED`.
4. **Step 4:** In VMware menu, connect the Ventoy USB drive to the VM:  
   `VM > Removable Devices > Generic Flash Disk > Connect (Disconnect from host)`.
5. **Step 5:** Monitor Windows Explorer and USB Shield Pro Activity Tab.
6. **Step 6:** Attempt to open a text or image file on drive `E:` or `F:`.
7. **Step 7:** Observe the response:
   - *Expected Candidate Behavior:* Within 200–500ms, drive letters `E:` and `F:` disappear from Explorer. Any in-flight file read is immediately aborted with `The device does not exist`. Activity log records active auto-ejection.
   - *Failure Indicator:* If `E:` or `F:` remains mounted and files can be copied to the desktop, mark **FAIL**.

---

## 4. Retest Sign-Off Checklist

- [ ] `E5-RET-01` (Ventoy Multi-Partition) Passed
- [ ] `E5-RET-02` (Standard Flash Drive) Passed
- [ ] `E5-RET-03` (Fixed-Disk USB Storage) Passed
- [ ] `E5-RET-04` (Trusted Device Whitelist) Passed
- [ ] `E5-RET-05` (Class Override on Whitelist) Passed
- [ ] `E5-RET-06` (Policy Unlock Restoration) Passed
- [ ] `E5-RET-07` (Headless Service Enforcement) Passed
- [ ] `E5-RET-08` (Emergency Rollback Script) Passed
- [ ] Final Determination: **READY FOR CANDIDATE ACCEPTANCE** / **REJECTED**
