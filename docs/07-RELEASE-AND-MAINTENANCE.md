# USB Shield Pro v2.0 — Release & Maintenance Specification

This document records the exact procedure for packaging, verifying, maintaining, and versioning USB Shield Pro.

---

## 1. Release Inventory & Artifact Structure

The official production release distribution resides in [`release\USB-Shield-Pro-v2.0\`](../release/USB-Shield-Pro-v2.0/):

```
release\
├── USB-Shield-Pro-v2.0\
│   ├── UsbShieldPro.exe                  (812,544 bytes, standalone 64-bit production executable)
│   └── README.txt                        (3,794 bytes, complete end-user documentation)
└── USB-Shield-Pro-v2.0-SHA256.txt        (Authoritative SHA-256 cryptographic hashes)
```

### Authoritative SHA-256 Checksums:
```
72f43477e564a9f718cc5f7870f132b26c70168ae111b445d6b927079c1c2b17  README.txt
0492e602f0e471b7efeb661690584c92f062a525118aff53af55ff72372ec622  UsbShieldPro.exe
```

---

## 2. End-to-End Release Packaging Procedure

To reproduce or prepare a future release package, follow this 11-step checklist:

1. **Freeze Code & Architecture**: Verify that no uncommitted or experimental changes remain in source code.
2. **Clean Stale Artifacts**: Delete previous intermediate build files (`TestRunner.exe`, temporary logs, scratch files).
3. **Execute Production Build**: Run [`build.bat`](../build.bat) using 64-bit .NET 4.8 `csc.exe` with `/optimize+` and `app.manifest`.
4. **Execute Full Test Regression**: Run `TestRunner.exe --test-harness --out test_results.txt`. Confirm **147/147 PASS** with **0 failures**.
5. **Verify Test Seams Disabled**: Confirm that `DeviceEnforcementLifecycleManager.IsTestMode == false` and all simulation seams (`DelaySeam`, `PresenceSeam`, `NotificationSinkSeam`) are null.
6. **Execute Diagnostic Smoke Test**: Run `TestRunner.exe --diag-detect` to verify hardware enumeration, physical disk numbering, and Disk 0 safety protection.
7. **Stage Release Directory**: Create `release\USB-Shield-Pro-v2.0\`. Copy `UsbShieldPro.exe` into the staging folder.
8. **Generate Release README**: Ensure `README.txt` contains system requirements, quick start, feature summary, safe removal notice, emergency recovery instructions, and copyright.
9. **Compute SHA-256 Checksums**: Run `Get-FileHash -Algorithm SHA256` on all files in the release folder and output to `release\USB-Shield-Pro-v2.0-SHA256.txt`.
10. **Execute Standalone Package Launch**: Launch `release\USB-Shield-Pro-v2.0\UsbShieldPro.exe` directly from its folder to verify it has zero dependencies on repository source files, opens the GUI cleanly, initializes the tray, and exits normally without startup registry mutations.
11. **Emit Final Release Verdict**: If all 10 steps pass, mark the build as **READY FOR DISTRIBUTION**.

---

## 3. Versioning Policy & Guidelines

Future development should follow this strict Semantic Versioning policy:

- **v2.0 (Current Release)**: **Frozen Baseline**. No further behavioral changes, retry redesigns, or PnP modifications may be applied to this version.
- **v2.0.x (Maintenance & Security Patches)**: Strictly for critical bug fixes, security patch updates, or typo corrections that do not alter core enforcement mechanics, retry timing, or Windows policy keys.
- **v2.1 (Backward-Compatible Enhancements)**: For non-breaking new features (e.g., additional localized languages, new audit log export formats, enhanced reporting) that preserve all v2.0 invariants.
- **v3.0 (Major Architectural Upgrades)**: For future major shifts (e.g., dedicated kernel-mode filter driver for pre-mount interception, multi-tenant cloud telemetry, or enterprise SIEM integration).

---

## 4. Maintenance Rule: Preserving the v2.0 Baseline

> [!IMPORTANT]
> **BEFORE MODIFYING THIS REPOSITORY**:
> 1. Create a dedicated development branch or directory clone.
> 2. **DO NOT overwrite** the known-good `release\USB-Shield-Pro-v2.0\` distribution binaries or `USB-Shield-Pro-v2.0-SHA256.txt`.
> 3. Read [`09-V2.0-BASELINE.md`](./09-V2.0-BASELINE.md) to understand the frozen contracts before touching code.
