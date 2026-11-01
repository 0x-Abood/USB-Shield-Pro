# USB Shield Pro v2.0 — Recovery & Troubleshooting Guide

This guide provides step-by-step diagnostic and recovery runbooks for administrators and developers.

---

## 1. Disaster Recovery & Emergency Rollback

USB Shield Pro is built with a **fail-safe rollback guarantee**: before modifying any Windows registry key or Group Policy setting, the original state is written to an immutable write-ahead journal (`journal.json`).

### 1.1 Offline Recovery (No Executable Required)
If the application crashes, files are deleted, or the system cannot boot into USB Shield Pro, you can execute a complete offline rollback using native Windows tools.

#### Method A: Command-Line Rollback (`USBShield_Restore.bat`)
1. Open an elevated Windows Command Prompt (`cmd.exe` as Administrator).
2. Run:
   ```cmd
   "C:\ProgramData\USBShieldPro\USBShield_Restore.bat"
   ```
3. The script executes concrete pre-computed `reg.exe add` and `reg.exe delete` commands that restore each modified key to its exact pre-shield state.

#### Method B: Windows Registry Editor (`USBShield_Restore.reg`)
1. Open Windows File Explorer and navigate to `C:\ProgramData\USBShieldPro\`.
2. Double-click **`USBShield_Restore.reg`**.
3. Click **Yes** when prompted by Windows Registry Editor.
4. All modified keys will be overwritten with original baseline values.

### 1.2 Programmatic Rollback via Executable
If `UsbShieldPro.exe` is available:
```powershell
.\UsbShieldPro.exe --restore-exact
```
This reads `journal.json`, applies exact baseline values, regenerates recovery packages, and logs results to `C:\ProgramData\USBShieldPro\restore_exact.txt`.

---

## 2. Diagnostics: Read-Only Triage Commands

Always perform read-only triage before attempting configuration resets:

```powershell
# 1. Inspect live hardware and safety status
.\UsbShieldPro.exe --diag-detect

# 2. Audit file permissions and NTFS ACLs
.\UsbShieldPro.exe --diag-security

# 3. Check live registry policy values directly
reg query "HKLM\SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices"
reg query "HKLM\SYSTEM\CurrentControlSet\Services\USBSTOR" /v Start
reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers" /v DisableAutoplay
reg query "HKLM\SOFTWARE\Policies\Microsoft\Windows\Explorer" /v NoAutoplayfornonVolume
```

---

## 3. Step-by-Step Troubleshooting Runbooks

### 3.1 "Access is Denied" or Application Fails to Launch
- **Likely Cause**: The user attempted to run `UsbShieldPro.exe` without administrative elevation. The embedded application manifest specifies `<requestedExecutionLevel level="requireAdministrator" />`.
- **Diagnostic**: Run in PowerShell: `([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)`
- **Solution**: Right-click `UsbShieldPro.exe` and select **Run as administrator**.

### 3.2 Windows SmartScreen Block on First Launch
- **Likely Cause**: `UsbShieldPro.exe` is distributed legitimately unsigned without an enterprise Authenticode certificate. Windows SmartScreen displays "Windows protected your PC".
- **Solution**:
  1. Click **More info**.
  2. Click **Run anyway**.
  3. For enterprise distribution, sign the binary using an organizational Code Signing Certificate as documented in [`03-BUILD-DEVELOPMENT-GUIDE.md`](./03-BUILD-DEVELOPMENT-GUIDE.md).

### 3.3 Auto-Eject Reports "Automatic eject failed"
- **Likely Cause**: Windows kernel or a running process maintained an open handle to the volume that was not released during the 10-second retry window. Common culprits: active antivirus scans, Windows Search Indexer, or an open Explorer window browsing the drive.
- **Diagnostic**: Check the **Activity** tab in the GUI or review the trace log for the veto reason.
- **Solution**:
  1. Close any open File Explorer windows or applications accessing the USB drive.
  2. Use the standard Windows **Safely Remove Hardware and Eject Media** taskbar icon before physically disconnecting the drive.
  3. **DO NOT** abruptly yank the drive out while unmounted handles remain.

### 3.4 AutoPlay Policies Not Reverting When Toggled OFF
- **Likely Cause**: Missing write access to `HKCU` or `HKLM`, or a corrupted baseline entry in `journal.json`.
- **Diagnostic**:
  ```powershell
  reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers" /v DisableAutoplay
  reg query "HKLM\SOFTWARE\Policies\Microsoft\Windows\Explorer" /v NoAutoplayfornonVolume
  ```
- **Solution**: Run `UsbShieldPro.exe --restore-exact` to force baseline re-synchronization.

### 3.5 Forgotten Application Password
- **Likely Cause**: Administrator password lost or forgotten.
- **Solution**:
  1. Click **Forgot Password?** on the lock dialog.
  2. Enter the **128-bit Master Recovery Key** generated during setup (`SHLD-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX-X`).
  3. If the recovery key is also lost:
     > [!CAUTION]
     > **Destructive Reset Action**: Delete `C:\ProgramData\USBShieldPro\.auth` and `C:\ProgramData\USBShieldPro\.recovery` using an elevated Command Prompt. This removes password protection while leaving policies and whitelists intact.

### 3.6 Single Enforcement Owner Conflicts (Service vs GUI)
- **Likely Cause**: Attempting to run enforcement in both the background Windows Service and the interactive GUI simultaneously.
- **Diagnostic**: Check `sc.exe query UsbShieldService`.
- **Architectural Resolution**: Handled automatically by [`EnforcementOwnerResolver`](../Core/EnforcementOwnerResolver.cs). When the service is running, the GUI automatically operates in viewer/client mode and suspends its internal Sentinel worker.
- **Solution**: If the service is stuck in a non-responsive state:
  ```cmd
  sc stop UsbShieldService
  ```
  The GUI will automatically assume fallback enforcement ownership after a 300 ms settling interval.

### 3.7 Corrupted `trusted_devices.json`
- **Likely Cause**: Unexpected system power loss while writing whitelist changes.
- **Automated Protection**: `TrustedDeviceRepository` automatically detects JSON parse errors, copies the corrupted file to `trusted_devices_corrupt_<timestamp>.json` for forensic analysis, and attempts to load `trusted_devices.json.bak`.
- **Manual Repair**: Inspect `C:\ProgramData\USBShieldPro\` and rename the `.bak` file to `trusted_devices.json`.
