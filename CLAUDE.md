# Isora Sentinel — AI Agent Assistant Guide (CLAUDE.md)

## Project Overview
Isora Sentinel is a free, open-source, zero-dependency PowerShell automation suite for Windows endpoints. It executes as `NT AUTHORITY\SYSTEM` or elevated administrator to perform endpoint auditing, cleanup, and ISO control mapping (ISO/IEC 27001:2022 and ISO/IEC 20000-1:2018), producing structured logs for SIEM platforms (Splunk, Elastic, Datadog, Microsoft Sentinel).

**Author & Architecture:** Sandra Gabriela Puerto Torres  
**Commercial Brand:** Isora Cloud Group  
**Portals:** https://sandrapuerto.com | https://isora.com.co  
**License:** Apache License, Version 2.0 (v2.4.0+; v2.3.1 and earlier remain under MIT)

---

## Directory Layout
* `src/lib/`
  * `Sentinel-Core.psm1`: `Assert-IsAdmin`, `Assert-SentinelIntegrity`, `Get-DefenderPlatformPath`
  * `Sentinel-Logger.psm1`: `Initialize-SentinelLog`, `Write-AuditEntry` (Text & NDJSON)
* `src/engines/`
  * `Invoke-CleanupScreenshots.ps1` (ISO 27001 Control A.8.1 - DLP)
  * `Invoke-CleanupTempGlobal.ps1` (ISO 20000-1 §8.2 - Capacity Management)
  * `Invoke-DefenderScan.ps1` (ISO 27001 Control A.8.7 - Antimalware)
  * `Invoke-IntegrityCheck.ps1` (ISO 27001 Control A.8.20 - Component Store)
  * `Invoke-BitLockerAudit.ps1` (ISO 27001 Control A.8.24 - BitLocker Encryption)
  * `Invoke-EventAudit.ps1` (ISO 27001 Control A.8.15 - Event Logs)
  * `Invoke-ArchiveDownloads.ps1` (ISO 27001 Control A.8.10 - Downloads Archival)
* `installer/`
  * `Isora-Sentinel-Setup.exe`: Standalone GUI WinForms installer.
  * `Build-InstallerExe.ps1`: Build script compiling installer via native .NET `csc.exe`.
  * `Build-IntegrityManifest.ps1`: Generates SHA-256 hashes in `integrity.manifest.json`.
  * `Install-Sentinel.ps1`: CLI deployment to `C:\Program Files\Isora-Sentinel\` and task registration.
  * `Uninstall-Sentinel.ps1`: Deregisters scheduled tasks and unregisters from Windows Programs & Features.
* `docs/`
  * `OPERATOR_GUIDE.md`: Operator and administration manual.
  * `MANUAL_DE_USO.md`: Legacy operator guide redirect.
  * `SIEM_INTEGRATION.md`: Forwarder configs and SIEM query guides.
  * `TROUBLESHOOTING.md`: Common issue resolution and diagnostic matrix.
  * `ARCHITECTURE.md`: Technical specification and Mermaid diagrams.
  * `ISO_MAPPING.md`: Control mapping matrix for ISO 27001/20000-1/SOC 2/CIS.
* `tests/`
  * `Invoke-Tests.ps1`: Pester test runner and CI/CD gate.
  * `Sentinel-Core.Tests.ps1`: Core utility tests.
  * `Sentinel-Logger.Tests.ps1`: Forensics and SIEM format tests.
  * `Sentinel-Engines.Tests.ps1`: Engine parameter binding tests.
  * `Sentinel-Syntax.Tests.ps1`: Static AST syntax integrity validator.

---

## Essential Commands

### Run Test Suite
```powershell
powershell.exe -ExecutionPolicy Bypass -File .\tests\Invoke-Tests.ps1
```

### Run Engines Locally (Manual Elevated Run)
```powershell
# Safe audit mode (default, no deletion/move):
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-CleanupScreenshots.ps1
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-CleanupTempGlobal.ps1
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-ArchiveDownloads.ps1

# Active mode (-Apply):
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-CleanupScreenshots.ps1 -Apply
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-CleanupTempGlobal.ps1 -Apply
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-ArchiveDownloads.ps1 -Apply

# Auditing engines:
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-DefenderScan.ps1 -ScanType 1
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-IntegrityCheck.ps1
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-BitLockerAudit.ps1
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-EventAudit.ps1 -Hours 24
```

### Install / Register Tasks
```powershell
powershell.exe -ExecutionPolicy Bypass -File .\installer\Install-Sentinel.ps1 -Silent
```

---

## Rules for AI Agents
1. **Zero External Dependencies:** Never add npm packages, python requirements, or external PowerShell gallery modules.
2. **Safe by Default:** Ensure destructive operations require an explicit `-Apply` switch.
3. **Deterministic Syntax:** Always test with AST parser and Pester before submitting code.
4. **Pipeline Parameter Binding Safety:** Never pipe into `ForEach-Object` with `-join` inside string expressions. Use native `foreach ($k in $dict)`.
5. **Privilege Boundary:** Maintain `Assert-IsAdmin` at the entry of every engine script.
