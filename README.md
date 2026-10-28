<div align="center">

# 🏢 ISORA CLOUD GROUP
## 🛡️ Sentinel-IT Suite
### Enterprise Endpoint Hygiene & Compliance Automation for Windows

[![Commercial Brand & Distributor](https://img.shields.io/badge/Distributor-Isora%20Cloud%20Group-0ea5e9?style=for-the-badge&logo=cloud&logoColor=white)](https://isora.com.co)
[![Copyright & Architecture](https://img.shields.io/badge/Copyright%20%26%20Architect-Sandra%20Gabriela%20Puerto%20Torres%20(SGPT)-f59e0b?style=for-the-badge&logo=shield&logoColor=white)](https://www.linkedin.com/in/sandra-puerto/)
[![GitHub](https://img.shields.io/badge/GitHub-@sandra--puerto-181717?style=for-the-badge&logo=github&logoColor=white)](https://github.com/sandra-puerto)
[![LinkedIn](https://img.shields.io/badge/LinkedIn-Sandra%20Gabriela%20Puerto%20Torres%20(SGPT)-0A66C2?style=for-the-badge&logo=linkedin&logoColor=white)](https://www.linkedin.com/in/sandra-puerto/)
[![Code Implementation](https://img.shields.io/badge/Powered%20By-Google%20Antigravity%20AI-6366f1?style=for-the-badge&logo=google&logoColor=white)](#)
[![Version](https://img.shields.io/badge/Version-2.2.0-blue.svg?style=for-the-badge)](CHANGELOG.md)
[![PowerShell](https://img.shields.io/badge/PowerShell-5.1%20%7C%207%2B-blue.svg?style=for-the-badge&logo=powershell&logoColor=white)](#)
[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011%20%7C%20Server-0078D6?style=for-the-badge&logo=windows&logoColor=white)](#)
[![Mapped Controls](https://img.shields.io/badge/Controls-ISO%2027001%20%7C%20ISO%2020000--1%20%7C%20SOC%202-10b981.svg?style=for-the-badge)](docs/ISO_MAPPING.md)
[![Tests](https://img.shields.io/badge/Tests-25%20Passed%20%7C%20Pester-6366f1.svg?style=for-the-badge&logo=checkmarx&logoColor=white)](tests/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)](LICENSE)

*Plataforma soberana de mantenimiento y mapeo normativo ISO/IEC 27001:2022 e ISO/IEC 20000-1 para endpoints Windows corporativos.*

---
</div>

## 📚 1. Documentation Index

For operators, SIEM engineers, security auditors, and system administrators:

| Document | Purpose & Description |
| :--- | :--- |
| **[docs/MANUAL_DE_USO.md](docs/MANUAL_DE_USO.md)** | **Enterprise Operator Manual:** Interactive execution, GPO deployment, unattended switches (`-AuditOnly`, `-ScanType`, `-AutoRepair`), and task management. |
| **[docs/SIEM_INTEGRATION.md](docs/SIEM_INTEGRATION.md)** | **SIEM Forwarder Guide:** Splunk Universal Forwarder, Elastic Filebeat, Datadog Agent, Azure Sentinel (KQL/SPL alert queries). |
| **[docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md)** | **Diagnostic & Resolution Matrix:** Troubleshooting exit codes, UAC elevation, Task Scheduler errors, and OneDrive cloud files. |
| **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** | **System Architecture Specification:** Hub-and-Spoke topology, trust boundaries, privilege model, and Mermaid diagrams. |
| **[docs/ISO_MAPPING.md](docs/ISO_MAPPING.md)** | **Compliance Matrix & Auditor Evidence:** Complete clause-by-clause mapping to ISO 27001:2022, ISO 20000-1, SOC 2, and CIS Benchmarks. |

---

## 📌 2. Overview

Endpoint hygiene routines (malware scans, Component Store integrity checks, temporary asset reclamation, BitLocker cryptographic audits, security event monitoring, and data-leakage screenshot purges) are frequently executed manually or outsourced to heavy proprietary third-party agents.

**Sentinel-IT** provides a sovereign, lightweight **Hub-and-Spoke** PowerShell automation framework designed to run autonomously under the highest operating system privilege context (`NT AUTHORITY\SYSTEM`). It maps each routine directly to controls from **ISO/IEC 27001:2022** and **ISO/IEC 20000-1**, generating immutable, structured audit trails ready for real-time ingestion by enterprise SIEM platforms.

> **Scope & Compliance Disclaimer:** Sentinel-IT automates operational hygiene routines aligned with the controls documented below. Deploying Sentinel-IT does not inherently certify an organization; full certification requires adherence to an organization-wide Information Security Management System (ISMS).

---

## 🏛️ 3. Hub-and-Spoke Architecture

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
       ┌──────────────────┬───────────────┼───────────────┬──────────────────┐
       ▼                  ▼               ▼               ▼                  ▼
 [Screenshots]      [Temp-Global]     [Defender]     [Integrity]        [BitLocker]
(ISO A.8.1 DLP)    (ISO 20000-1 Cap) (ISO A.8.7)   (ISO A.8.20 WinSxS) (ISO A.8.24)
       │                  │               │               │                  │
       └──────────────────┴───────────────┼───────────────┴──────────────────┘
                                          ▼
                                     [Event-Audit]
                                   (ISO A.8.15 Logs)
                                          │
                     ┌────────────────────┴────────────────────┐
                     ▼                                         ▼
         Human Audit Logs (.log)                    SIEM JSON Events (UTF-8)
      [PID, Timestamp, Identity]                 [Splunk / Datadog / Elastic]
```

### Key Architectural Strengths:
1. **Zero External Dependencies:** Native to Windows PowerShell 5.1+ and PowerShell Core 7+. Does not require third-party packages, Chocolatey, or background daemon processes.
2. **Dual-Format Forensic Logging:** Instant switching between human-readable plaintext (`TEXT`) and RFC-compliant JSON (`JSON`) with host, PID, identity, and rich execution metadata via `$env:SENTINEL_LOG_FORMAT`.
3. **Privilege-Enforced Boundary:** All engines verify execution context via `Assert-IsAdmin`, blocking unauthorized low-privilege executions.
4. **Dry-Run Staging Support:** Sanitization engines implement `-AuditOnly` allowing safe simulation without deleting files.

---

## 📋 4. Engine Specification & Control Mapping

Detailed mapping of each engine script to international standards:

| Engine Script | Control Standard | Objective | Operational Behavior | Forensic Metadata |
| :--- | :--- | :--- | :--- | :--- |
| **`Invoke-CleanupScreenshots.ps1`** | **ISO 27001:2022 A.8.1** *(User Endpoint Devices)* | Reduce sensitive data exposure (DLP) | Purges local and OneDrive screenshot folders (`Pictures\Screenshots`, `Capturas`) across all user profiles. Supports `-AuditOnly`. | `ProfileCount`, `Path`, `TotalIdentified`, `TotalPurged` |
| **`Invoke-CleanupTempGlobal.ps1`** | **ISO 20000-1 §8.2** *(Capacity Management)* | Prevent disk exhaustion and reclaim IO | Systematically clears `C:\Windows\Temp`, Windows Update download caches, WER queues, and local `%TEMP%` across all profiles. Supports `-AuditOnly`. | `Path`, `TotalIdentified`, `TotalCleaned` |
| **`Invoke-DefenderScan.ps1`** | **ISO 27001:2022 A.8.7** *(Protection Against Malware)* | Enforce periodic antimalware scans | Refreshes signature definitions and dispatches `MpCmdRun.exe` (Quick or Full scan) capturing process exit codes. | `EnginePath`, `ScanType`, `ExitCode` |
| **`Invoke-IntegrityCheck.ps1`** | **ISO 27001:2022 A.8.20** *(System & Network Security)* | Detect Component Store drift | Invokes `DISM /Online /Cleanup-Image /ScanHealth` evaluating return codes (0 = Healthy, 20 = Corrupted). Supports `-AutoRepair`. | `ExitCode`, `HealthStatus` |
| **`Invoke-BitLockerAudit.ps1`** | **ISO 27001:2022 A.8.24** *(Use of Cryptography)* | Verify endpoint encryption in transit and rest | Audits volume encryption status (`ProtectionStatus = On`, `100%`) and key protector presence without altering state. | `MountPoint`, `ProtectionStatus`, `KeyProtectors`, `EncryptionPct` |
| **`Invoke-EventAudit.ps1`** | **ISO 27001:2022 A.8.15** *(Logging & Monitoring)* | Detect anti-forensics and intrusion bursts | Queries `Security.evtx` for Event 1102 (audit log cleared), Event 4625 brute force bursts, and account provisioning (4720/4726). | `WindowHours`, `AnomaliesDetected`, `EventID`, `User` |

---

## 📦 5. Enterprise Deployment

### Interactive Installation
Open an elevated **PowerShell** prompt (Administrator) and run:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\installer\Install-Sentinel.ps1
```

### Unattended Enterprise Deployment (Intune / SCCM / GPO / RMM)
Deploy silently across fleet endpoints:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\installer\Install-Sentinel.ps1 `
    -Silent `
    -ScheduleScreenshots "Daily" `
    -ScheduleTemp "Daily" `
    -ScheduleDefender "Weekly" `
    -ScheduleIntegrity "Weekly" `
    -ScheduleBitLocker "Daily" `
    -ScheduleEventAudit "Daily"
```

### Directory Structure (Post-Installation)
```text
C:\Program Files\IT_Maintenance\
├── logs\
│   ├── ISO_BitLocker_Audit\
│   ├── ISO_Cleanup_Screenshots\
│   ├── ISO_Cleanup_Temp_Global\
│   ├── ISO_Defender_Scan\
│   ├── ISO_Event_Audit\
│   └── ISO_Integrity_Check\
└── Sentinel-IT\
    ├── engines\
    │   ├── Invoke-BitLockerAudit.ps1
    │   ├── Invoke-CleanupScreenshots.ps1
    │   ├── Invoke-CleanupTempGlobal.ps1
    │   ├── Invoke-DefenderScan.ps1
    │   ├── Invoke-EventAudit.ps1
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

## 📡 6. SIEM Integration & Audit Log Output

To forward logs directly to an external SIEM (e.g., Datadog Agent, Splunk Universal Forwarder, Elastic Filebeat, or Azure Log Analytics), set the environment variable:

```powershell
[System.Environment]::SetEnvironmentVariable("SENTINEL_LOG_FORMAT", "JSON", "Machine")
```

### Example JSON Log Entry
```json
{
  "Timestamp": "2026-10-27 18:30:15.1234567",
  "Level": "SUCCESS",
  "Host": "SRV-LON-APP01",
  "Identity": "NT AUTHORITY\\SYSTEM",
  "PID": 4812,
  "Message": "VOLUME COMPLIANT: C: is fully encrypted with active protection.",
  "Metadata": {
    "MountPoint": "C:",
    "ProtectionStatus": "On",
    "VolumeStatus": "FullyEncrypted",
    "EncryptionPct": "100",
    "KeyProtectors": "Tpm;RecoveryPassword"
  }
}
```

For configuration files and sample detection queries, see [docs/SIEM_INTEGRATION.md](docs/SIEM_INTEGRATION.md).

---

## 🧪 7. Automated Testing Suite

Sentinel-IT includes a full **Pester** test suite validating privilege assertions, logging formats, environment variable overrides, null-safe metadata handling, engine signatures, and PowerShell AST syntax integrity across all scripts.

To run the automated tests locally:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\tests\Invoke-Tests.ps1
```

```text
------------------------------------------------------------
 Test Summary: Passed=25, Failed=0, Total=25
------------------------------------------------------------
```

---

## 👥 8. Authorship, Governance & Development Team

Sentinel-IT is governed under an enterprise model combining personal intellectual property ownership with commercial brand distribution and modern agentic development:

| Role | Entity / Contributor | Function & Scope | Profiles & Links |
| :--- | :--- | :--- | :--- |
| **Marca Comercial & Distribuidor** | **Isora Cloud Group** | Marca comercial paraguas, distribución y gobernanza del ecosistema | [isora.com.co](https://isora.com.co) |
| **Copyright & Arquitectura** | **Sandra Gabriela Puerto Torres (SGPT)** | Titular única y exclusiva del Copyright, concepción arquitectónica, diseño de controles y dirección técnica | [sandrapuerto.com](https://sandrapuerto.com) · [GitHub: @sandra-puerto](https://github.com/sandra-puerto) · [LinkedIn](https://www.linkedin.com/in/sandra-puerto/) |
| **Implementación de Código** | **Google Antigravity AI Engine** | Generación de código fuente, refactorización y suite de pruebas CI/CD bajo la dirección técnica de la arquitecta | [Google DeepMind](https://deepmind.google) |

---

## 🔒 9. Security Policy

Because Sentinel-IT executes with `NT AUTHORITY\SYSTEM` permissions, strict access controls must be maintained on `C:\Program Files\IT_Maintenance\`. For security advisories, vulnerability disclosure SLAs, and threat models, see [SECURITY.md](SECURITY.md).

* **Security Contact:** [security@isora.com.co](mailto:security@isora.com.co) · [contacto@sandrapuerto.com](mailto:contacto@sandrapuerto.com)
* **Author Profiles:** [GitHub: https://github.com/sandra-puerto](https://github.com/sandra-puerto) · [LinkedIn: https://www.linkedin.com/in/sandra-puerto/](https://www.linkedin.com/in/sandra-puerto/)
* **General Inquiries:** [contact@isora.com.co](mailto:contact@isora.com.co)

---

## 📜 10. License

This project is licensed under the open-source **[MIT License](LICENSE)**.

```text
Copyright (c) 2026 Sandra Gabriela Puerto Torres (SGPT).
Commercial brand & distribution under the auspices of Isora Cloud Group.
```

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files, without restriction.
