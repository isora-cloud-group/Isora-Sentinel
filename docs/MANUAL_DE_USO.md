# Enterprise Operator Manual — Isora Sentinel Suite

> **Notice:** The canonical, fully expanded Enterprise Operator & Deployment Manual is now maintained in English at **[docs/OPERATOR_GUIDE.md](OPERATOR_GUIDE.md)**.

**Commercial Brand & Distribution:** Isora Cloud Group  
**Sole Copyright Holder & Architect:** Sandra Gabriela Puerto Torres ([sandrapuerto.com](https://sandrapuerto.com) • [GitHub: @sandra-puerto](https://github.com/sandra-puerto) • [LinkedIn](https://www.linkedin.com/in/sandra-puerto/))  
**Software Version:** 2.3.1  
**VirusTotal Multi-Vendor Audit:** [0/71 Clean Report (d868caca4e02ec83001320023fee4dbb31f08c44d5123d2757d8e34becfa4c90)](https://www.virustotal.com/gui/file/d868caca4e02ec83001320023fee4dbb31f08c44d5123d2757d8e34becfa4c90?nocache=1)

---

## Quick Reference Summary

### 1. Graphical Deployment (GUI)
Run `installer/Isora-Sentinel-Setup.exe` with Administrator privileges. The 4-step wizard validates local administrator rights, performs pre-flight SHA-256 integrity validation, configures scheduled tasks under `NT AUTHORITY\SYSTEM`, and registers the installation in Windows Settings / Control Panel.

### 2. Unattended CLI Deployment (RMM / Intune / GPO)
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\installer\Install-Sentinel.ps1 `
    -Silent `
    -InstallDir "C:\Program Files\Isora-Sentinel" `
    -ScheduleScreenshots "Daily" `
    -ScheduleTemp "Daily" `
    -ScheduleDefender "Weekly" `
    -ScheduleIntegrity "Weekly" `
    -ScheduleBitLocker "Daily" `
    -ScheduleEventAudit "Daily"
```

### 3. System Tray Monitor (`Isora-Sentinel-Tray.exe`)
* Live SHA-256 integrity indicator (Green = Authorized, Amber = Tampering / Missing Manifest).
* On-demand routine triggers with native UAC elevation dialogs.
* Service configuration GUI (`TaskConfigForm.cs`) to toggle tasks and adjust scheduling frequencies.
* Bilingual language switch (`ES / EN`).

### 4. SIEM Telemetry Activation
```powershell
[System.Environment]::SetEnvironmentVariable("SENTINEL_LOG_FORMAT", "JSON", "Machine")
```

### 5. Multi-Vendor Security Verification (VirusTotal 0/71)
The compiled executable has passed multi-vendor static and behavioral analysis with **0 detections out of 71 security engines**:
* **VirusTotal URL:** [https://www.virustotal.com/gui/file/d868caca4e02ec83001320023fee4dbb31f08c44d5123d2757d8e34becfa4c90?nocache=1](https://www.virustotal.com/gui/file/d868caca4e02ec83001320023fee4dbb31f08c44d5123d2757d8e34becfa4c90?nocache=1)
* **SHA-256:** `d868caca4e02ec83001320023fee4dbb31f08c44d5123d2757d8e34becfa4c90`

### 6. Full Documentation
Please refer to **[OPERATOR_GUIDE.md](OPERATOR_GUIDE.md)** for detailed technical parameters, Task Scheduler XML integration, dry-run simulation instructions, and disaster recovery procedures.
