# Security Policy — USB Shield Pro

## Supported Versions

| Version | Supported | Status |
| :--- | :--- | :--- |
| **2.1.0** | :white_check_mark: | Official Gold Master (Current) |
| < 2.1.0 | :x: | Deprecated / Unsupported |

---

## Reporting a Vulnerability

If you discover a security vulnerability or unexpected privilege escalation vector within USB Shield Pro, please report it responsibly:

- **Private Security Advisory:** [GitHub Security Advisories](https://github.com/0x-Abood/USB-Shield-Pro/security/advisories)
- **Direct Maintainer Contact:** `122826239+0x-Abood@users.noreply.github.com`
- **Response Commitment:** Initial assessment and response within 48 hours.

Please include:
1. Detailed reproduction steps or Proof of Concept (PoC).
2. Affected Windows OS build and patch level.
3. Diagnostic logs or journal state snapshots if applicable.

---

## Security Boundaries & Safety Invariants

USB Shield Pro is built with a defense-in-depth, fail-closed architecture designed to protect enterprise workstations while preventing accidental denial of service:

1. **System Disk Protection Floor:**
   - **Physical Disk 0**, the active Windows boot partition, system root drive (`%SystemDrive%`), and volumes holding the active Windows paging file (`pagefile.sys`) are hard-coded as non-removable system boundaries. The reactive ejection sentinel and PnP controller will unconditionally refuse to lock, dismount, or eject these volumes.

2. **Fail-Closed Verification (SEC-09):**
   - Any ambiguity, lookup timeout, or native Win32/PnP failure when querying a device or volume automatically fails closed: unauthorized devices are blocked/ejected, and unauthenticated administrative actions are denied.

3. **Multi-Partition Ejection Integrity:**
   - Multi-partition external drives (e.g., Ventoy, Windows To Go, dual-partition bootable flash drives) undergo synchronized multi-volume dismounting (`FSCTL_LOCK_VOLUME` -> `FSCTL_DISMOUNT_VOLUME` -> `IOCTL_STORAGE_MEDIA_REMOVAL`) prior to hardware devnode teardown, eliminating volume dangling and data corruption risks.

4. **Zero-Dropper Clean-Room Execution (AV-Clean Architecture):**
   - The application does not drop, generate, or execute any `.bat`, `.cmd`, `.vbs`, or `.ps1` scripts on the file system.
   - All Windows service lifecycle operations (`CreateService`, `StartService`, `DeleteService`) are executed natively via Win32 `advapi32.dll` APIs without spawning hidden shell subprocesses (`sc.exe` / `cmd.exe`).

5. **Cryptographic Integrity & Key Protection:**
   - Administrative master password verification utilizes PBKDF2 with HMAC-SHA256 across high iteration counts.
   - Secrets and configurations stored at rest are guarded using DPAPI (Data Protection API) with machine-bound and user-scoped protection.
   - 128-bit emergency recovery keys provide offline administrative recovery without requiring plain-text storage.

6. **Clean-Room Compilation & OPSEC:**
   - Production binaries are clean-room compiled (`/optimize+ /debug-`) and strictly sanitized: zero local machine paths, zero user identities, and zero debug symbol paths are present in the distributed binaries.
