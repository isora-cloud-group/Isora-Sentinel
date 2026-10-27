# Sentinel-IT — AI Agent Assistant Guide (CLAUDE.md)

## Project Overview
Sentinel-IT is a sovereign, zero-dependency PowerShell automation suite for Windows endpoints. It executes as `NT AUTHORITY\SYSTEM` to enforce endpoint hygiene and ISO compliance mapping (ISO/IEC 27001:2022 and ISO/IEC 20000-1:2018), producing structured forensic logs for SIEM platforms (Splunk, Elastic, Datadog, Microsoft Sentinel).

**Author:** Sandra Gabriela Puerto Torres / Isora Cloud Group  
**Portals:** https://sandrapuerto.com | https://isora.com.co  
**License:** MIT  

---

## Directory Layout
* `src/lib/`
  * `Sentinel-Core.psm1`: `Assert-IsAdmin`, `Get-DefenderPlatformPath`
  * `Sentinel-Logger.psm1`: `Initialize-SentinelLog`, `Write-AuditEntry` (Text & NDJSON)
* `src/engines/`
  * `Invoke-CleanupScreenshots.ps1` (ISO 27001 Control A.8.1)
  * `Invoke-CleanupTempGlobal.ps1` (ISO 20000-1 §8.2)
  * `Invoke-DefenderScan.ps1` (ISO 27001 Control A.8.7)
  * `Invoke-IntegrityCheck.ps1` (ISO 27001 Control A.8.20)
  * `Invoke-BitLockerAudit.ps1` (ISO 27001 Control A.8.24)
  * `Invoke-EventAudit.ps1` (ISO 27001 Control A.8.15)
* `installer/`
  * `Install-Sentinel.ps1`: Deploys to `C:\Program Files\IT_Maintenance\Sentinel-IT\` and registers scheduled tasks.
  * `Uninstall-Sentinel.ps1`: Deregisters scheduled tasks and removes program binaries.
* `docs/`
  * `MANUAL_DE_USO.md`: Operator and administration manual.
  * `SIEM_INTEGRATION.md`: Forwarder configs and SIEM query guides.
  * `TROUBLESHOOTING.md`: Common issue resolution and diagnostic matrix.
  * `ARCHITECTURE.md`: Technical specification and Mermaid diagrams.
  * `ISO_MAPPING.md`: Compliance matrix for ISO 27001/20000-1/SOC 2/CIS.
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
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-CleanupScreenshots.ps1 -AuditOnly
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-CleanupTempGlobal.ps1 -AuditOnly
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
2. **Deterministic Syntax:** Always test with AST parser and Pester before submitting code.
3. **Pipeline Parameter Binding Safety:** Never pipe into `ForEach-Object` with `-join` inside string expressions. Use native `foreach ($k in $dict)`.
4. **Privilege Boundary:** Maintain `Assert-IsAdmin` at the entry of every engine script.
