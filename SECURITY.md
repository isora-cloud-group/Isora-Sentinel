# Security Policy

**Repository:** `Sentinel-IT`  
**Security Operations:** [security@isora.com.co](mailto:security@isora.com.co)  
**General Contact:** [contact@isora.com.co](mailto:contact@isora.com.co)  
**Revision:** 2.0.0  
**Status:** Production  
**Sole Copyright Holder & Architecture:** Sandra Gabriela Puerto Torres  
**Commercial Brand & Distribution:** Isora Cloud Group (ICG)  

---

## 1. Supported Versions

Security maintenance and updates are provided for the following revisions:

| Component / Version | Supported Revision | Status |
|---|---|---|
| Sentinel-IT Suite (Current) | `2.x` | :white_check_mark: Active / Full Maintenance |
| Sentinel-IT Engines & Runtime | `1.x` | :white_check_mark: Critical Security Patches Only |
| Legacy Builds | `< 1.0` | :x: Unsupported / End of Life |

---

## 2. Reporting a Vulnerability

**Do not report security vulnerabilities through public GitHub issues, pull requests, or public forums.**

If you discover a security flaw, privilege escalation vector, or boundary weakness within Sentinel-IT, send an email to:

> **security@isora.com.co**  
> *(CC: contact@isora.com.co)*

Include the following information in your report:

1. **Vulnerability classification:** Short categorization (e.g., Local Privilege Escalation (LPE), Time-of-Check to Time-of-Use (TOCTOU) symlink race, arbitrary file deletion, manifest bypass, command injection).
2. **Affected component:** Specify engine (`Invoke-BitLockerAudit`, `Invoke-CleanupScreenshots`, `Invoke-CleanupTempGlobal`, `Invoke-DefenderScan`, `Invoke-EventAudit`, `Invoke-IntegrityCheck`), core library (`Sentinel-Core`, `Sentinel-Logger`), or installer binary (`SentinelSetup.cs`, `SentinelTray.cs`).
3. **Affected version/commit:** The release tag or commit hash of the inspected build.
4. **Description:** A precise description of the issue and conditions under which it manifests.
5. **Reproduction steps:** A minimal, reproducible sequence of actions or Proof of Concept (PoC).
6. **Impact assessment:** Evaluation of the potential consequences (e.g., unauthorized file access, privilege boundary crossing).
7. **Suggested remediation:** Optional proposed mitigation or patch.

For encrypted communications, request a PGP key exchange via an initial email with no sensitive contents.

---

## 3. Response Timeline

Security disclosures are handled under the following service level objectives:

| Stage | Target |
|---|---|
| Acknowledgment of receipt | Within 48 hours |
| Initial assessment and triage | Within 7 days |
| Critical vulnerability remediation | As soon as feasible (expedited hotfix) |
| High severity remediation | Within 30 days |
| Medium severity remediation | Within 90 days |
| Coordinated public disclosure | Coordinated with reporter post-patch release |

---

## 4. Disclosure Process

1. **Intake:** Receipt confirmed within the 48-hour SLA window.
2. **Triage:** Reproduction and severity scoring under CVSS v3.1 standards.
3. **Remediation:** Engineering of remediation validated across isolated Windows Server and Windows 10/11 environments.
4. **Release:** Publishing of new signed release and updated cryptographic provenance.
5. **Advisory:** Publication of security advisory detailing CVE/vulnerability scope with attribution to the researcher (unless anonymity requested).

---

## 5. Scope

### 5.1 In Scope

- Local Privilege Escalation (LPE) vulnerabilities in installer scripts (`Install-Sentinel.ps1`), compiled wrappers (`Sentinel-IT-Setup.exe`), or Scheduled Task configurations (`schtasks.exe`).
- Symbolic link, hard link, or NTFS junction race conditions (TOCTOU) during temp file or screenshot purging that could allow arbitrary file deletion.
- Flaws or bypasses in cryptographic manifest validation (`Assert-SentinelIntegrity` / `integrity.manifest.json`).
- Argument injection, unquoted path execution, or binary planting in Windows Task Scheduler triggers or registry auto-starts.
- Sensitive credential leakage or improper serialization in structured logging (`Sentinel-Logger.psm1`).
- Overly permissive default NTFS Access Control Lists (ACLs) applied during setup.

### 5.2 Out of Scope

- Vulnerabilities requiring physical root or kernel-mode tampering on the host machine.
- Upstream vulnerabilities in the Microsoft Windows kernel, native Windows Defender engine (`MpCmdRun.exe`), `dism.exe`, `sfc.exe`, or the .NET Framework / PowerShell runtime (`System.Management.Automation`).
- Social engineering, phishing, or physical attacks targeting infrastructure operators or administrators.
- Denial-of-Service resulting from an elevated Administrator intentionally disabling Sentinel tasks or deleting operational binaries.
- Scenarios where an attacker already possesses full `NT AUTHORITY\SYSTEM` or elevated local administrator access on the target host.

---

## 6. Safe Harbor

Security research conducted in good faith against Sentinel-IT and its distributions is considered authorized. The maintainers will not pursue legal action against researchers who:

- Make a good-faith effort to avoid privacy violations, data destruction, and service interruption.
- Test only against non-production environments or systems they own or have explicit permission to test.
- Do not exploit a vulnerability beyond the minimum necessary to demonstrate proof-of-concept.
- Report the vulnerability promptly and privately in accordance with Section 2.
- Respect coordinated disclosure timelines prior to public release.

---

## 7. Threat Model & Security Best Practices for Operators

Because Sentinel-IT executes maintenance routines under high operating system privileges (`NT AUTHORITY\SYSTEM` and elevated Administrator), operators must adhere to these defensive practices:

1. **NTFS ACL Hardening:**
   The installation directory (`C:\Program Files\Sentinel-IT\`) must restrict write and modify privileges exclusively to `BUILTIN\Administrators` and `NT AUTHORITY\SYSTEM`. Standard unprivileged users must have read and execute (`RX`) permissions only. `Install-Sentinel.ps1` enforces this by default.
2. **Fail-Closed Integrity Enforcement:**
   All scheduled routines call `Assert-SentinelIntegrity`. When running under `SYSTEM`, integrity verification operates in fail-closed mode: any modified or missing script causes an immediate abort and writes a tamper alert to the structured audit log.
3. **Structured SIEM Forwarding:**
   Sentinel-IT logs events in UTF-8 without BOM NDJSON format (`C:\Program Files\Sentinel-IT\logs\`). Production deployments should forward these logs in real-time to a SIEM (e.g., Wazuh, Splunk, Microsoft Sentinel) to ensure audit trail immutability.
4. **Automated Log Pruning:**
   Logs older than 90 days are pruned automatically via `Clear-OldSentinelLogs` during maintenance routines to prevent disk exhaustion denial-of-service.

---

## 8. Transparent Auditing & Verification for Security Teams

To assist enterprise SOC teams, red/blue teams, and auditing firms (e.g., Kaspersky, Mandiant, internal compliance auditors), the distribution executable `Sentinel-IT-Setup.exe` provides non-intrusive inspection switches:

### 8.1 In-Memory SHA-256 Digest Audit
Inspect and verify all embedded scripts against `integrity.manifest.json` without launching the GUI or modifying the system:
```powershell
.\Sentinel-IT-Setup.exe --audit
```
*Expected exit code:* `0` (Success: all digests matched).

### 8.2 Sandbox Extraction for Source Code Review
Extract the complete embedded package files into an isolated directory for static analysis:
```powershell
.\Sentinel-IT-Setup.exe --extract C:\Sandbox\SentinelAudit
```

### 8.3 Offline Inspector Script
Auditors can also inspect the binary using the provided PowerShell verification harness:
```powershell
powershell -ExecutionPolicy Bypass -File installer\Verify-PackageIntegrity.ps1 -InstallerPath installer\Sentinel-IT-Setup.exe
```

---

## 9. Document Control

| Field | Value |
|---|---|
| Repository | `Sentinel-IT` |
| Security Contact | security@isora.com.co |
| General Contact | contact@isora.com.co |
| Revision | 2.0.0 |
| Status | Production |
| Classification | Public Security Policy |
