# Operator Manual — Isora Sentinel Suite

> **Notice:** The canonical, fully expanded Enterprise Operator & Deployment Manual is now maintained in English at **[docs/OPERATOR_GUIDE.md](OPERATOR_GUIDE.md)**.

**Commercial Brand & Distribution:** Isora Cloud Group  
**Architect & Copyright Holder:** Sandra Gabriela Puerto Torres ([sandrapuerto.com](https://sandrapuerto.com) • [GitHub: @sandra-puerto](https://github.com/sandra-puerto) • [LinkedIn](https://www.linkedin.com/in/sandra-puerto/))  
**Software Version:** 2.4.7  
**VirusTotal Multi-Vendor Scan:** [TODO_OWNER: VirusTotal Report URL](TODO_OWNER: VirusTotal Report URL)

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

### 5. Multi-Vendor Security Verification (VirusTotal)
The compiled executable is submitted to VirusTotal for analysis across security engines:
* **VirusTotal URL:** [TODO_OWNER: VirusTotal Report URL](TODO_OWNER: VirusTotal Report URL)
* **SHA-256:** `BD282F5184D654F8BD3CC7CCA34BD1AAB6DA359CD366A2D21CE70430E7E9EC41`
* **Important Note:** A clean antivirus scan reflects a single point in time and does not prove the software is safe; it is not a substitute for reviewing the source code.

### 6. Full Documentation
Please refer to **[OPERATOR_GUIDE.md](OPERATOR_GUIDE.md)** for detailed technical parameters, Task Scheduler XML integration, dry-run simulation instructions, and disaster recovery procedures.
