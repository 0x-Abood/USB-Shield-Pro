---
name: Device Compatibility Report
about: Report compatibility or testing results for specific USB storage hardware
title: "[HARDWARE] "
labels: ["hardware-compatibility"]
assignees: "0x-Abood"
---

### Hardware Identity
- **Manufacturer & Model:** [e.g., Samsung Bar Plus 128GB / Ventoy Dual-Boot 32GB]
- **Hardware ID (VID/PID):** [e.g., `USB\VID_090C&PID_1000`]
- **Hardware Serial Number:** [e.g., `0373821092003847`]
- **Bus Type:** [e.g., USB 3.2 Gen 1 / Type-C External SSD / SD Card Reader]

### Tested Scenarios
- [ ] Hardware Allowlisting (Adding to Trusted Devices)
- [ ] Storage Block (Deny Read / Write while locked)
- [ ] Read-Only Inspection Mode
- [ ] Multi-Partition Safe Ejection (Zero `PNP_VetoDevice` error)
- [ ] Physical Removal & Re-insertion Detection
- [ ] Windows Background Service Enforcement

### Operating System
- **Windows Version:** [e.g., Windows 11 Pro 64-bit / Windows 10 Enterprise]

### Additional Notes / Quirks
Describe any unusual firmware or controller behaviors observed during testing.
