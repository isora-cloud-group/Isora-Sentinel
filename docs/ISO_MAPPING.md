# Regulatory Compliance & Audit Traceability Matrix
### ISO/IEC 27001:2022 · ISO/IEC 20000-1:2018 · SOC 2 Type II · CIS Benchmark

**Commercial Brand & Distribution:** Isora Cloud Group  
**Sole Copyright Holder & Architect:** Sandra Gabriela Puerto Torres ([sandrapuerto.com](https://sandrapuerto.com) · [GitHub: @sandra-puerto](https://github.com/sandra-puerto) · [LinkedIn](https://www.linkedin.com/in/sandra-puerto/))  
**Document Version:** 2.2.0  
**Purpose:** Provides technical traceability, control justification, and auditor evidence verification for accreditation assessors, ISMS compliance officers, and external security auditors.

---

## 1. Scope & Statement of Applicability

**Sentinel-IT** automates technical endpoint hardening, sensitive data leakage prevention (DLP), cryptographic validation, and continuous integrity monitoring on Microsoft Windows endpoints. Every execution produces **deterministic, immutable forensic audit trails** designed to substantiate compliance during internal and third-party accreditation audits.

> **Regulatory Disclaimer:** Deploying Sentinel-IT fulfills the technical implementation and auditability requirements for the specific controls detailed below. Full organizational ISO/IEC 27001 or ISO/IEC 20000-1 certification requires an overarching organizational governance framework, risk assessment methodology, and formal Information Security Management System (ISMS) policies.

---

## 2. ISO/IEC 27001:2022 (Annex A) Control Mapping Matrix

| Execution Engine | ISO 27001:2022 Control | Control Title & Domain | Risk Mitigated | Technical Implementation in Sentinel-IT | Forensic Evidence Emitted |
| :--- | :---: | :--- | :--- | :--- | :--- |
| **`Invoke-CleanupScreenshots.ps1`** | **A.8.1** | **User endpoint devices**<br>*(Technological Controls)* | Inadvertent leakage of sensitive data (credentials, personally identifiable information [PII], payment card data, intellectual property) stored in screenshots left on disk or synchronized to unmanaged cloud drives. | Discovers all user profiles via `ProfileList` and user hives. Discovers standard and OneDrive screenshot folders (`Pictures\Screenshots`, `Capturas`). Evaluates reparse points (0x00000400) to prevent forced cloud re-hydration. Purges local artifacts and supports `-AuditOnly` for non-destructive simulation. | `ProfileCount`, `Path`, `TotalIdentified`, `TotalPurged`, `ExecutionMode`. Severity level `SUCCESS` or `WARN`. |
| **`Invoke-DefenderScan.ps1`** | **A.8.7** | **Protection against malware**<br>*(Technological Controls)* | Infection by ransomware, trojans, or zero-day malware due to stale signature definitions or disabled antimalware scans on remote endpoints. | Discovers active Microsoft Defender platform path dynamically, refreshes signature definitions with network fault tolerance (`-SignatureUpdate`), and triggers automated Quick (`-ScanType 1`) or Full (`-ScanType 2`) scans via `MpCmdRun.exe`. | `EnginePath`, `ScanType`, `SignatureUpdateCode`, `ExitCode`, live threat detection strings. |
| **`Invoke-EventAudit.ps1`** | **A.8.15** | **Logging**<br>*(Technological Controls)* | Undetected anti-forensic log tampering (clearing the security event log), credential brute-forcing bursts, or rogue account provisioning. | Queries Windows `Security.evtx` for Event 1102 (Audit log cleared), Event 4625 (Failed logon bursts exceeding configurable threshold), and Events 4720/4726 (Account creation/deletion anomalies) across a rolling time window (`-Hours`). | Immediate `ERROR` alert if Event 1102 is found; structured `WARN` on brute-force spikes with `AnomaliesDetected`, `EventID`, `User`, `Timestamp`. |
| **`Invoke-ArchiveDownloads.ps1`** | **A.8.10** | **Information deletion**<br>*(Technological Controls)* | Retention of obsolete, unmanaged sensitive files and installer assets in unbacked Downloads folders. | Discovers user Downloads folders across all profiles, identifies assets exceeding configured aging threshold (`-DaysOld`, default 30), and relocates them to a protected archive location (`Documents\Sentinel_Archived\Downloads`) with an informative policy notice file. | `User`, `MovedCount`, `BytesReclaimed`, `ArchiveDir`. |
| **`Invoke-IntegrityCheck.ps1`** | **A.8.20** | **Networks and systems security**<br>*(Technological Controls)* | Unauthorized binary modification, rootkits, driver poisoning, or silent component corruption in the Windows Component Store (`WinSxS`). | Executes `DISM.exe /Online /Cleanup-Image /ScanHealth` to verify operating system digital signatures and component store health. Evaluates return codes (0 = Healthy, 20 = Repairable Corruption, 3010 = Reboot Needed). Supports automated healing via `-AutoRepair`. | `DismPath`, `DismCode`, `HealthStatus`, `AutoRepair`. |
| **`Invoke-BitLockerAudit.ps1`** | **A.8.24** | **Use of cryptography**<br>*(Technological Controls)* | Unauthorized data-at-rest exposure due to lost, stolen, or decommissioned laptops and workstations without active encryption. | Queries Windows BitLocker management APIs (or `manage-bde.exe`) across all OS and fixed data volumes. Verifies `ProtectionStatus = On`, `EncryptionPercentage = 100%`, and validates key protectors (TPM, Recovery Password, PIN) without altering state. | `MountPoint`, `ProtectionStatus`, `VolumeStatus`, `EncryptionPct`, `KeyProtectors`. |

---

## 3. ISO/IEC 20000-1:2018 (IT Service Management) Control Mapping

| Execution Engine | ISO 20000-1 Clause | Service Process Domain | Operational Risk Mitigated | Technical Implementation in Sentinel-IT | Forensic Evidence Emitted |
| :--- | :---: | :--- | :--- | :--- | :--- |
| **`Invoke-CleanupTempGlobal.ps1`** | **§8.2** | **Capacity Management**<br>*(Service Delivery Operations)* | System drive (`C:\`) disk space exhaustion, I/O performance bottlenecks, failed Windows Updates, and service crashes resulting from accumulated temporary assets. | Systematically and safely purges `C:\Windows\Temp`, Windows Update download caches (`SoftwareDistribution\Download`), Windows Error Reporting (`WER`) crash dumps, and per-profile user `%TEMP%` directories. Supports `-AuditOnly`. | `Path`, `TotalIdentified`, `TotalCleaned`, `ReclaimedBytes`. |

---

## 4. Cross-Framework Traceability Matrix

### 4.1. SOC 2 Type II (Trust Services Criteria)
* **CC6.1 (Logical Access Security):** Enforced via `Assert-IsAdmin` privilege verification and execution isolated to `NT AUTHORITY\SYSTEM`.
* **CC6.6 (Logical Threat Mitigation):** Addressed by automated antimalware scans and definition refreshes in `Invoke-DefenderScan.ps1`.
* **CC6.7 (Data Protection at Rest):** Fulfilled by cryptographic verification in `Invoke-BitLockerAudit.ps1` and data purge in `Invoke-CleanupScreenshots.ps1`.
* **CC7.1 & CC7.2 (Infrastructure Monitoring & Vulnerability Management):** Implemented via `Invoke-EventAudit.ps1` and Component Store auditing in `Invoke-IntegrityCheck.ps1`.

### 4.2. CIS Microsoft Windows Benchmark (v2.0)
* **CIS 3.11 (Ensure BitLocker is Configured):** Validated by `Invoke-BitLockerAudit.ps1`.
* **CIS 8.2 (Ensure Audit Logs are Retained):** Monitored by `Invoke-EventAudit.ps1` (detection of Event 1102).
* **CIS 10.1 (Ensure Microsoft Defender Antivirus is Enabled and Scanning):** Enforced by `Invoke-DefenderScan.ps1`.

### 4.3. NIST SP 800-53 Rev. 5
* **SI-2 (Flaw Remediation):** Component Store drift verification (`Invoke-IntegrityCheck.ps1`).
* **SI-3 (Malicious Code Protection):** Antimalware updates and automated scans (`Invoke-DefenderScan.ps1`).
* **SI-4 (Information System Monitoring):** Security event monitoring and anti-forensics alerting (`Invoke-EventAudit.ps1`).
* **SC-13 (Cryptographic Protection):** Volume encryption state enforcement (`Invoke-BitLockerAudit.ps1`).
* **AU-9 (Protection of Audit Information):** Centralized, read-only forensic log store (`Sentinel-Logger.psm1`).

---

## 5. Auditor Evidence Collection Guide

When conducting a compliance audit, use the following PowerShell commands to collect objective operational evidence:

### 5.1. Verify Scheduled Task Operational Status
```powershell
Get-ScheduledTask -TaskPath "\Sentinel-IT\" | 
    Select-Object TaskName, State, @{N='LastRun';E={($_ | Get-ScheduledTaskInfo).LastRunTime}}, @{N='LastResult';E={($_ | Get-ScheduledTaskInfo).LastTaskResult}} |
    Format-Table -AutoSize
```
*Expected Result:* Tasks show `Ready` state and `LastTaskResult` of `0` (Success).

### 5.2. Inspect Forensic Telemetry Logs
```powershell
Get-ChildItem -Path "C:\Program Files\Sentinel-IT\logs" -Recurse -Filter "*.log" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 10 FullName, Length, LastWriteTime
```
*Expected Result:* Dedicated directories exist for each control with up-to-date `.log` files containing structured execution evidence.

### 5.3. Verify Cryptographic Anti-Tampering Manifest
```powershell
powershell.exe -ExecutionPolicy Bypass -Command {
    Import-Module "C:\Program Files\Sentinel-IT\lib\Sentinel-Core.psm1"
    Assert-SentinelIntegrity -ManifestPath "C:\Program Files\Sentinel-IT\integrity.manifest.json" -TargetDir "C:\Program Files\Sentinel-IT"
    Write-Host "Integrity Gate: PASSED (All files conform to SHA-256 manifest)" -ForegroundColor Green
}
```
*Expected Result:* Zero discrepancies reported; returns exit code `0`.

### 5.4. Query Live SIEM Forensic Records
```powershell
# Read latest BitLocker audit forensic record:
Get-Content (Get-ChildItem "C:\Program Files\Sentinel-IT\logs\ISO_BitLocker_Audit\*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName | Select-Object -First 5
```
