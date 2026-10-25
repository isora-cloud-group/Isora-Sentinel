<div align="center">

# 🛡️ Sentinel-IT
### Enterprise Maintenance & Control-Mapping Suite for Windows Endpoints

[![PowerShell](https://img.shields.io/badge/PowerShell-5.1%20%7C%207%2B-blue.svg?style=for-the-badge&logo=powershell&logoColor=white)](#)
[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011%20%7C%20Server-0078D6?style=for-the-badge&logo=windows&logoColor=white)](#)
[![Mapped Controls](https://img.shields.io/badge/Controls-ISO%2027001%20%7C%20ISO%2020000--1-10b981.svg?style=for-the-badge)](#)
[![Tests](https://img.shields.io/badge/Tests-15%20Passed%20%7C%20Pester-6366f1.svg?style=for-the-badge&logo=checkmarx&logoColor=white)](#)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)](LICENSE)
[![Author](https://img.shields.io/badge/Author-Sandra%20Puerto%20%7C%20Isora-f59e0b?style=for-the-badge&logo=linkedin&logoColor=white)](https://sandrapuerto.com)

*Zero-dependency native PowerShell automation with structured forensic audit logging for SIEM ingestion and ISO compliance mapping.*

---
</div>

## 📌 1. Overview

Endpoint hygiene routines (malware scans, Component Store integrity checks, temporary asset reclamation, and data-leakage screenshot purges) are frequently executed manually or outsourced to heavy proprietary third-party agents.

**Sentinel-IT** provides a sovereign, lightweight **Hub-and-Spoke** PowerShell automation framework designed to run autonomously under the highest operating system privilege context (`NT AUTHORITY\SYSTEM`). It maps each routine directly to controls from **ISO/IEC 27001:2022** and **ISO/IEC 20000-1**, generating immutable, structured audit trails ready for real-time ingestion by enterprise SIEM platforms.

> **Scope & Compliance Disclaimer:** Sentinel-IT automates operational hygiene routines aligned with the controls documented below. Deploying Sentinel-IT does not inherently certify an organization; full certification requires adherence to an organization-wide Information Security Management System (ISMS).

---

## 🏛️ 2. Hub-and-Spoke Architecture

Sentinel-IT is decoupled into a centralized core library (`src/lib/`) and independent execution engines (`src/engines/`):

```
                       ┌─────────────────────────────────────┐
                       │   Windows Task Scheduler (SYSTEM)   │
                       └──────────────────┬──────────────────┘
                                          │
                   ┌──────────────────────┴──────────────────────┐
                   │                                             │
                   ▼                                             ▼
       ┌────────────────────────┐                   ┌────────────────────────┐
       │   Sentinel-Core.psm1   │                   │  Sentinel-Logger.psm1  │
       │ (Privilege & Platform) │                   │  (Forensics & SIEM)    │
       └───────────┬────────────┘                   └────────────┬───────────┘
                   │                                             │
                   └──────────────────────┬──────────────────────┘
                                          │
              ┌───────────────────────────┼───────────────────────────┐
              ▼                           ▼                           ▼
    [Cleanup-Screenshots]       [Cleanup-Temp-Global]         [Defender-Scan]
      (ISO 27001 A.8.1)           (ISO 20000-1 Cap.)         (ISO 27001 A.8.7)
              │                           │                           │
              └───────────────────────────┼───────────────────────────┘
                                          ▼
                             [Component Integrity]
                              (ISO 27001 A.8.20)
                                          │
                     ┌────────────────────┴────────────────────┐
                     ▼                                         ▼
         Human Audit Logs (.log)                    SIEM JSON Events (UTF-8)
      [PID, Timestamp, Identity]                 [Splunk / Datadog / Elastic]
```

### Key Architectural Strengths:
1. **Zero External Dependencies:** Native to Windows PowerShell 5.1+ and PowerShell Core 7+. Does not require third-party packages, Chocolatey, or background daemon processes.
2. **Dual-Format Forensic Logging:** Instant switching between human-readable plaintext (`TEXT`) and RFC-compliant JSON (`JSON`) with host, PID, identity, and rich execution metadata.
3. **Privilege-Enforced Boundary:** All engines verify execution context via `Assert-IsAdmin`, blocking unauthorized low-privilege executions.

---

## 📋 3. Engine Specification & Control Mapping

Detailed mapping of each engine script to international standards:

| Engine Script | Control Standard | Objective | Operational Behavior | Forensic Metadata |
| :--- | :--- | :--- | :--- | :--- |
| **`Invoke-CleanupScreenshots.ps1`** | **ISO 27001:2022 A.8.1** *(User Endpoint Devices)* | Reduce sensitive data exposure (DLP) | Purges local and OneDrive screenshot folders (`Pictures\Screenshots`, `Capturas`) across all user profiles. | `ProfileCount`, `Path`, `PurgeCount` |
| **`Invoke-DefenderScan.ps1`** | **ISO 27001:2022 A.8.7** *(Protection Against Malware)* | Enforce periodic antimalware scans | Dispatches `MpCmdRun.exe` full signature and heuristic scan bypassing interactive UI. | `EnginePath`, `ThreatDetections` |
| **`Invoke-IntegrityCheck.ps1`** | **ISO 27001:2022 A.8.20** *(System & Network Security)* | Detect Component Store drift | Invokes `DISM /Online /Cleanup-Image /ScanHealth` to audit system health without altering state. | `DismOutput`, `HealthStatus` |
| **`Invoke-CleanupTempGlobal.ps1`** | **ISO 20000-1 §8.2** *(Capacity Management)* | Prevent disk exhaustion and reclaim IO | Systematically clears `C:\Windows\Temp`, Windows Update download caches, and local `%TEMP%` across all profiles. | `Path`, `PurgedFiles`, `TotalCleaned` |

> ⚠️ **Destructive Action Notice:** `Invoke-CleanupScreenshots` and `Invoke-CleanupTempGlobal` permanently delete unindexed cache and screenshot files. Test on staging endpoints before deploying to production fleets.

---

## 📦 4. Enterprise Deployment

### Interactive Installation
Open an elevated **PowerShell** prompt (Administrator) and run:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\installer\Install-Sentinel.ps1
```

### Unattended Enterprise Deployment (Intune / SCCM / GPO / RMM)
Deploy silently without interactive prompts:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\installer\Install-Sentinel.ps1 -Silent -ScheduleScreenshots "Daily" -ScheduleTemp "Daily" -ScheduleDefender "Weekly" -ScheduleIntegrity "Weekly"
```

### Directory Structure (Post-Installation)
```text
C:\Program Files\IT_Maintenance\
├── logs\
│   ├── ISO_Cleanup_Screenshots\
│   ├── ISO_Cleanup_Temp_Global\
│   ├── ISO_Defender_Scan\
│   └── ISO_Integrity_Check\
└── Sentinel-IT\
    ├── engines\
    │   ├── Invoke-CleanupScreenshots.ps1
    │   ├── Invoke-CleanupTempGlobal.ps1
    │   ├── Invoke-DefenderScan.ps1
    │   └── Invoke-IntegrityCheck.ps1
    └── lib\
        ├── Sentinel-Core.psm1
        └── Sentinel-Logger.psm1
```

### Uninstallation
To cleanly deregister all scheduled tasks and remove program files:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\installer\Uninstall-Sentinel.ps1
```

---

## 📡 5. SIEM Integration & Audit Log Output

To forward logs directly to an external SIEM (e.g., Datadog Agent, Splunk Universal Forwarder, Elastic Filebeat, or Azure Log Analytics), set `$global:SentinelLogFormat = "JSON"` in `Sentinel-Logger.psm1`.

### Example JSON Log Entry
```json
{
  "Timestamp": "2026-10-25 00:26:45.1234567",
  "Level": "SUCCESS",
  "Host": "SRV-LON-APP01",
  "Identity": "NT AUTHORITY\\SYSTEM",
  "PID": 4812,
  "Message": "PURGE: Cleared assets from vector.",
  "Metadata": {
    "Path": "C:\\Users\\JohnDoe\\Pictures\\Screenshots",
    "Count": 14
  }
}
```

---

## 🧪 6. Automated Testing Suite

Sentinel-IT includes a full **Pester** test suite validating privilege assertions, logging formats, null-safe metadata handling, and PowerShell AST syntax integrity across all scripts.

To run the automated tests locally:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\tests\Invoke-Tests.ps1
```

---

## 🔒 7. Security Policy

Because Sentinel-IT executes with `NT AUTHORITY\SYSTEM` permissions, strict access controls must be maintained on `C:\Program Files\IT_Maintenance\`. For security advisories, vulnerability disclosure SLAs, and threat models, see [SECURITY.md](SECURITY.md).

* **Security Contact:** [security@isora.com.co](mailto:security@isora.com.co) · [contacto@sandrapuerto.com](mailto:contacto@sandrapuerto.com)
* **General Inquiries:** [contact@isora.com.co](mailto:contact@isora.com.co)

---

## 📜 8. License

This project is licensed under the open-source **[MIT License](LICENSE)**.

```text
Copyright (c) 2026 Sandra Gabriela Puerto Torres / Isora Cloud Group
```

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files, without restriction.
