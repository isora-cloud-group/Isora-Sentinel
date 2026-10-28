<p align="center">
  <img src="installer/src/app.svg" width="80" height="80" alt="Sentinel-IT Institutional Logo" />
</p>

<h1 align="center">Sentinel-IT Suite</h1>

<p align="center">
  <strong>Endpoint Hardening, Cryptographic Audit, and Compliance Telemetry for Microsoft Windows</strong><br>
  Aligned with ISO/IEC 27001:2022, ISO/IEC 20000-1:2018, SOC 2 Type II, and CIS Benchmarks
</p>

<p align="center">
  <a href="CHANGELOG.md"><img src="https://img.shields.io/badge/Version-2.2.0-0284c7.svg?style=flat-square" alt="Version 2.2.0" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-059669.svg?style=flat-square" alt="License MIT" /></a>
  <a href="docs/ISO_MAPPING.md"><img src="https://img.shields.io/badge/Compliance-ISO%2027001%20%7C%20ISO%2020000--1-0f766e.svg?style=flat-square" alt="Compliance Controls" /></a>
  <a href="tests/"><img src="https://img.shields.io/badge/Tests-31%20Passed%20%7C%20Pester-4338ca.svg?style=flat-square" alt="Pester Tests" /></a>
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20%7C%20Server-334155.svg?style=flat-square" alt="Supported OS" />
  <img src="https://img.shields.io/badge/Runtime-PowerShell%205.1%20%7C%207%2B-1e293b.svg?style=flat-square" alt="PowerShell Runtime" />
</p>

---

## 1. Executive Summary & Operational Scope

Sentinel-IT is a host-based automation framework engineered for corporate and public-sector Microsoft Windows environments. It provides continuous compliance verification, disk capacity reclamation, sensitive visual data sanitization, and forensic security logging without relying on heavy background services or unverified third-party runtime dependencies.

Designed specifically for organizations subject to regulatory frameworks such as **ISO/IEC 27001**, **ISO/IEC 20000-1**, **SOC 2 Type II**, and **CIS Microsoft Windows Benchmarks**, Sentinel-IT executes maintenance and audit routines using native Windows APIs, PowerShell, and .NET Framework. All telemetry is emitted as standardized, structured logs (RFC 8259 NDJSON or plain text) compatible with central Security Information and Event Management (SIEM) collectors.

### Core Engineering Requirements
* **Zero External Dependencies:** Built solely on native Windows components (`powershell.exe`, `schtasks.exe`, `dism.exe`, `manage-bde.exe`, `MpCmdRun.exe`). Requires no package managers (NuGet, Chocolatey) or third-party interpreters (Python, Node.js).
* **Deterministic Hub-and-Spoke Architecture:** Isolates common utilities (cryptographic integrity checking, standardized logging, privilege verification) in `src/lib/`, while operational engines execute as independent, decoupled routines in `src/engines/`.
* **Privilege Separation:** Distinguishes between local user profile operations and machine-wide administrative routines executed under `NT AUTHORITY\SYSTEM`.
* **Tamper-Evident Manifest:** Core modules and execution scripts are bound to SHA-256 cryptographic fingerprints recorded in `integrity.manifest.json`.

---

## 2. Regulatory Compliance & Control Mapping

Sentinel-IT operationalizes technical controls defined in international standards:

| Control Standard | Clause / ID | Domain | Technical Implementation in Sentinel-IT |
| :--- | :--- | :--- | :--- |
| **ISO/IEC 27001:2022** | **A.8.1** | User Endpoint Devices | Discovers standard, redirected, and cloud-synced screenshot folders across all user profiles; purges local visual artifacts to prevent Data Loss (DLP). |
| **ISO/IEC 27001:2022** | **A.8.7** | Protection Against Malware | Resolves active Microsoft Defender platform binaries dynamically, updates signatures, and dispatches scans with exit code capture. |
| **ISO/IEC 27001:2022** | **A.8.10** | Information Deletion | Enforces systematic data sanitization on temporary storage, diagnostic crash dumps, and staged installation caches. |
| **ISO/IEC 27001:2022** | **A.8.15** | Logging | Produces microsecond-precision NDJSON forensic records; audits Windows Event Logs for clearing events (1102, 104) and brute-force bursts (4625). |
| **ISO/IEC 27001:2022** | **A.8.20** | Systems Security | Dispatches DISM online component store health checks to detect binary drift or corruption in operating system libraries. |
| **ISO/IEC 27001:2022** | **A.8.24** | Use of Cryptography | Audits BitLocker volume encryption state, protection status, and key protectors across OS and fixed data volumes without altering state. |
| **ISO/IEC 20000-1:2018** | **§8.2** | Capacity Management | Purges Windows Update caches, WER queues, and multi-user temporary directories to reclaim storage capacity and prevent I/O bottlenecks. |
| **SOC 2 Type II** | **CC6.1 / CC6.7 / CC7.1** | Security & Availability | Enforces administrative access gates, data sanitization at rest, and continuous event log auditing. |
| **CIS Windows Benchmark** | **Controls 3.11, 8.2, 10.1** | Endpoint Hardening | Validates disk encryption, event log retention monitoring, and Defender scanning schedules. |

Detailed audit evidence commands and control rationales are provided in **[docs/ISO_MAPPING.md](docs/ISO_MAPPING.md)**.

---

## 3. Architecture & Security Model

```
+-----------------------------------------------------------------------------------+
|                        Execution & Orchestration Layer                            |
|  - Windows Task Scheduler (NT AUTHORITY\SYSTEM, RunLevel: Highest)                |
|  - System Tray Observability Utility (Sentinel-IT-Tray.exe, asInvoker)            |
|  - Unattended CLI Deployment (Install-Sentinel.ps1, Intune/GPO)                   |
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
|                     Security Boundary & Privilege Gate                            |
|  - Assert-IsAdmin: Verifies Administrator / SYSTEM security identifier            |
|  - Assert-SentinelIntegrity: SHA-256 fingerprint validation against manifest      |
|  - NTFS ACL Hardening: BUILTIN\Administrators & SYSTEM (Full), Users (Read-Only)  |
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
|                         Central Hub Modules (src/lib/)                            |
|  - Sentinel-Core.psm1   : Elevation gates, Defender platform resolution, manifest |
|  - Sentinel-Logger.psm1 : Dual-format logging (Human Text / NDJSON), log pruning  |
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
|                        Execution Engines (src/engines/)                           |
|  - Invoke-CleanupScreenshots.ps1 : Multi-profile DLP screenshot sanitization       |
|  - Invoke-CleanupTempGlobal.ps1  : System-wide and profile temporary cache purge  |
|  - Invoke-DefenderScan.ps1       : Antimalware definition refresh and scan trigger|
|  - Invoke-IntegrityCheck.ps1     : Windows Component Store (DISM) health audit    |
|  - Invoke-BitLockerAudit.ps1     : Volume encryption compliance audit             |
|  - Invoke-EventAudit.ps1         : Anti-forensics and security event log monitor  |
|  - Invoke-ArchiveDownloads.ps1   : Aged downloads lifecycle & tiering (A.8.10)    |
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
|                         Forensic Telemetry Output                                 |
|  - Local Store : C:\Program Files\Sentinel-IT\logs\<EngineName>\*.log          |
|  - Ingestion   : Splunk UF, Elastic Filebeat, Datadog Agent, Azure Monitor Agent  |
+-----------------------------------------------------------------------------------+
```

### Security Controls & Privilege Enforcement
1. **Local Privilege Escalation (LPE) Defense:**  
   The installation directory (`C:\Program Files\Sentinel-IT\`) is secured with explicit Windows Access Control Lists (ACLs). Inherited user write permissions are stripped; `BUILTIN\Users` retains read and execute permissions (`RX`) only.
2. **Cryptographic Anti-Tampering Gate:**  
   The function `Assert-SentinelIntegrity` checks all operational scripts against `integrity.manifest.json`. Under `NT AUTHORITY\SYSTEM` or production paths, missing or mismatched manifests result in immediate execution termination (Fail-Closed).
3. **Automated Log Retention:**  
   The logging subsystem implements configurable file pruning (`Clear-OldSentinelLogs`, default 90-day retention) to prevent local drive exhaustion while preserving forensic history.

---

## 4. Execution Engines Reference

All engines reside in `src/engines/` and accept structured command-line parameters:

| Engine | Primary Parameters | Default Behavior | Forensic Artifacts Emitted |
| :--- | :--- | :--- | :--- |
| `Invoke-CleanupScreenshots.ps1` | `-AuditOnly`<br>`[switch]` | Scans all user profiles via `ProfileList` and user registry hives; purges local screenshots. Skips cloud placeholders (`ReparsePoint` / `Offline`). | `ProfileCount`, `Path`, `TotalIdentified`, `TotalPurged` |
| `Invoke-CleanupTempGlobal.ps1` | `-AuditOnly`<br>`-DaysOld [int]` | Recursively purges `C:\Windows\Temp`, `SoftwareDistribution\Download`, WER crash queues, and user `%TEMP%` caches. | `Path`, `TotalIdentified`, `TotalCleaned`, `ReclaimedBytes` |
| `Invoke-DefenderScan.ps1` | `-ScanType [1\|2]`<br>`-SkipSignatureUpdate` | Refreshes malware definitions, then triggers Quick (`1`) or Full (`2`) scan via `MpCmdRun.exe`. Exit code 2 triggers active threat alerts. | `EnginePath`, `ScanType`, `SignatureUpdateCode`, `ExitCode` |
| `Invoke-IntegrityCheck.ps1` | `-AutoRepair`<br>`-QuickCheck` | Audits the Windows Component Store via `dism.exe /ScanHealth` (or `/CheckHealth` with `-QuickCheck`). Dispatches `/RestoreHealth` when repairable corruption is found and `-AutoRepair` is passed. | `DismPath`, `DismCode`, `HealthStatus`, `AutoRepair` |
| `Invoke-BitLockerAudit.ps1` | `-TargetMountPoint`<br>`-IncludeRemovable` | Evaluates BitLocker encryption percentage, protection state, and protector types across OS and fixed volumes. Removable media is excluded by default. | `MountPoint`, `VolumeType`, `ProtectionStatus`, `VolumeStatus`, `KeyProtectors` |
| `Invoke-EventAudit.ps1` | `-Hours [int]`<br>`-FailedLogonThreshold [int]` | Audits `Security.evtx` for Event 1102 (Log cleared) and Event 4625 (Failed logon bursts), and `System.evtx` for Event 104 (Log cleared). Resolves SIDs to account names. | `WindowHours`, `AnomaliesDetected`, `EventID`, `User`, `Timestamp` |
| `Invoke-ArchiveDownloads.ps1` | `-DaysOld [int]`<br>`-CustomArchiveDir [string]`<br>`-AuditOnly [switch]` | Identifies unmanaged downloads older than threshold (default 30 days) and relocates them to a protected archive (`Documents\Sentinel_Archived\Downloads`) with a user policy notice file. | `User`, `MovedCount`, `ReclaimedBytes`, `ArchiveDir` |

---

## 5. Deployment & Configuration

### Option A: Standalone GUI Installer (`Sentinel-IT-Setup.exe`)
For manual deployments, staging evaluation, or workstation rollouts:
1. Run `installer/Sentinel-IT-Setup.exe` with Administrator privileges.
2. The four-step wizard guides configuration of target paths, scheduled task frequencies, cryptographic manifest verification, and optional desktop tray monitor installation.
3. Modules are unpacked from an internal package resource and verified against SHA-256 signatures prior to task scheduling.

### Option B: Unattended Enterprise Deployment (`Install-Sentinel.ps1`)
For mass rollouts via Microsoft Intune, Microsoft Endpoint Configuration Manager (SCCM), Active Directory Group Policy (GPO), or RMM agents:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\installer\Install-Sentinel.ps1 `
    -Silent `
    -InstallDir "C:\Program Files\Sentinel-IT" `
    -ScheduleScreenshots "Daily" `
    -ScheduleTemp "Daily" `
    -ScheduleDefender "Weekly" `
    -ScheduleIntegrity "Weekly" `
    -ScheduleBitLocker "Daily" `
    -ScheduleEventAudit "Daily" `
    -ScheduleDownloads "Monthly"
```

#### Supported Deployment Parameters:
* `-InstallDir` `[string]`: Target installation folder (default: `C:\Program Files\Sentinel-IT`).
* `-LogsDir` `[string]`: Custom log storage path (default: `C:\Program Files\Sentinel-IT\logs`).
* `-Silent` `[switch]`: Suppresses all user prompts for non-interactive execution.
* `-SkipIntegrityCheck` `[switch]`: Bypasses pre-installation manifest check (recovery only).
* `-Schedule*` `[string]`: Sets trigger frequency (`Daily`, `Weekly`, `Monthly`) per engine.

### Option C: System Tray Monitor (`Sentinel-IT-Tray.exe`)
The system tray monitor runs within user desktop sessions:
* Real-time SHA-256 integrity badge (Green = Verified; Amber = Discrepancy detected).
* On-demand manual execution with standard Windows UAC elevation.
* Direct configuration panel (`TaskConfigForm.cs`) to adjust Task Scheduler schedules.

---

## 6. Forensic Telemetry & SIEM Forwarding

### Machine-Wide NDJSON Activation
To instruct all engines to emit JSON formatted for SIEM ingestion, configure the system-level environment variable:

```powershell
[System.Environment]::SetEnvironmentVariable("SENTINEL_LOG_FORMAT", "JSON", "Machine")
```

### Standardized Event Schema (RFC 8259)
Every log line is emitted in UTF-8 without Byte Order Mark (BOM):

```json
{
  "Timestamp": "2026-10-28 02:15:30.1284591",
  "Level": "SUCCESS",
  "Host": "SRV-LON-APP01",
  "Identity": "NT AUTHORITY\SYSTEM",
  "PID": 14208,
  "Message": "VOLUME COMPLIANT: C: is fully encrypted with active protection.",
  "Metadata": {
    "MountPoint": "C:",
    "VolumeType": "OperatingSystem",
    "ProtectionStatus": "On",
    "VolumeStatus": "FullyEncrypted",
    "EncryptionPct": "100",
    "KeyProtectors": "Tpm;RecoveryPassword"
  }
}
```

Pre-configured shipper definitions for **Splunk Universal Forwarder**, **Elastic Filebeat**, **Datadog Agent**, and **Microsoft Sentinel** are documented in **[docs/SIEM_INTEGRATION.md](docs/SIEM_INTEGRATION.md)**.

---

## 7. Quality Assurance & Forensic Verification

The codebase includes an automated test harness built on Pester and AST static analysis:

```powershell
# Run the test suite:
powershell.exe -ExecutionPolicy Bypass -File .\tests\Invoke-Tests.ps1
```

```
Test Summary: Passed=31, Failed=0, Total=31
- Sentinel-Core.Tests.ps1    : Privilege assertion, Defender discovery, integrity gate
- Sentinel-Engines.Tests.ps1 : Parameter binding, switch verification
- Sentinel-Logger.Tests.ps1  : Text/NDJSON serialization, BOM-free UTF8, log retention
- Sentinel-Syntax.Tests.ps1  : PowerShell Abstract Syntax Tree (AST) validation
```

### Forensic Package Inspection & Transparent Auditing
Enterprise security teams, blue teams, and independent auditors (such as Kaspersky, Mandiant, or internal SOC teams) can verify the integrity of the compiled distribution without executing the installer wizard:

```powershell
# 1. Non-interactive in-memory SHA-256 integrity check against manifest (exits 0 if valid):
.\installer\Sentinel-IT-Setup.exe --audit

# 2. Extract embedded package files to a sandbox directory for static code review:
.\installer\Sentinel-IT-Setup.exe --extract C:\Sandbox\SentinelAudit

# 3. Automated offline inspector harness:
powershell.exe -ExecutionPolicy Bypass -File .\installer\Verify-PackageIntegrity.ps1
```

This utility extracts PE metadata, unpacks embedded resources in memory, and re-hashes every script against `integrity.manifest.json`.

---

## 8. Repository Structure

```
Sentinel-IT/
├── .github/
│   └── workflows/
│       └── test.yml                  # GitHub Actions CI validation workflow
├── docs/
│   ├── ARCHITECTURE.md               # System design specification & security boundaries
│   ├── ISO_MAPPING.md                # ISO/IEC 27001 & 20000-1 control traceability
│   ├── OPERATOR_GUIDE.md             # Enterprise operations and parameter manual
│   ├── SIEM_INTEGRATION.md           # Forwarder configs (Splunk, Elastic, Datadog)
│   └── TROUBLESHOOTING.md            # Error codes, return values, and remediations
├── installer/
│   ├── src/
│   │   ├── AssemblyInfo.cs           # Assembly metadata and versioning
│   │   ├── app.ico                   # Application icon asset
│   │   ├── app.manifest              # Windows UAC application manifest (asInvoker)
│   │   ├── app.svg                   # Vector icon asset
│   │   ├── SentinelSetup.cs          # Standalone WinForms installer wizard
│   │   ├── SentinelTray.cs           # System tray observability process
│   │   └── TaskConfigForm.cs         # Task Scheduler GUI configurator
│   ├── Build-InstallerExe.ps1        # MSIL csc.exe native builder
│   ├── Build-IntegrityManifest.ps1   # SHA-256 integrity manifest generator
│   ├── Install-Sentinel.ps1          # Unattended CLI installation script
│   ├── Uninstall-Sentinel.ps1        # Removal and task unregistration script
│   └── Verify-PackageIntegrity.ps1   # Forensic binary inspection script
├── src/
│   ├── engines/
│   │   ├── Invoke-ArchiveDownloads.ps1   # Aged downloads lifecycle & tiering engine
│   │   ├── Invoke-BitLockerAudit.ps1     # BitLocker encryption compliance audit
│   │   ├── Invoke-CleanupScreenshots.ps1 # DLP screenshot sanitization engine
│   │   ├── Invoke-CleanupTempGlobal.ps1  # System & user cache capacity reclamation
│   │   ├── Invoke-DefenderScan.ps1       # Microsoft Defender scan & update engine
│   │   ├── Invoke-EventAudit.ps1         # Windows Security & System event log audit
│   │   └── Invoke-IntegrityCheck.ps1     # DISM component store integrity audit
│   └── lib/
│       ├── Sentinel-Core.psm1        # Elevation check, Defender path, integrity gate
│       └── Sentinel-Logger.psm1      # Standardized logging & retention module
├── tests/
│   ├── Invoke-Tests.ps1              # Unified Pester test runner
│   ├── Sentinel-Core.Tests.ps1       # Core module unit tests
│   ├── Sentinel-Engines.Tests.ps1    # Engine parameter & signature tests
│   ├── Sentinel-Logger.Tests.ps1     # Logging & serialization tests
│   └── Sentinel-Syntax.Tests.ps1     # AST static syntax parser tests
├── CHANGELOG.md                      # Version release notes
├── CODE_OF_CONDUCT.md                # Community and contributor guidelines
├── CONTRIBUTING.md                   # Development workflow and PR instructions
├── integrity.manifest.json           # SHA-256 cryptographic file table
├── LICENSE                           # MIT License
├── README.md                         # Main repository documentation
└── SECURITY.md                       # Vulnerability disclosure policy
```

---

## 9. Security Policy & Responsible Disclosure

Security vulnerabilities, privilege escalation concerns, or cryptographic discrepancies should be reported privately in accordance with **[SECURITY.md](SECURITY.md)**:

* **Security Operations:** [security@isora.com.co](mailto:security@isora.com.co)
* **General Contact:** [contact@isora.com.co](mailto:contact@isora.com.co)
* **Response Commitment:** Initial receipt acknowledgment within 48 business hours; patch coordination in isolated staging prior to public release. Safe harbor terms protect good-faith security research.

---

## 10. Intellectual Property & License

* **Commercial Brand & Distribution:** Isora Cloud Group
* **Architect & Sole Copyright Holder:** Sandra Gabriela Puerto Torres ([sandrapuerto.com](https://sandrapuerto.com) · [LinkedIn](https://www.linkedin.com/in/sandra-puerto/))
* **License:** Distributed under the terms of the **[MIT License](LICENSE)**.
