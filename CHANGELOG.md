# Changelog

All notable changes to **Sentinel-IT** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [2.2.0] - 2026-10-27

### Added
- **New Engine — BitLocker Cryptographic Compliance (`Invoke-BitLockerAudit.ps1`):** Audits disk encryption status, protection state, and key protectors mapped to ISO/IEC 27001:2022 Control A.8.24.
- **New Engine — Security Event & Anti-Forensics Audit (`Invoke-EventAudit.ps1`):** Audits Windows Security Event log for Event 1102 (audit log cleared), Event 4625 brute-force bursts, and account provisioning anomalies mapped to ISO/IEC 27001:2022 Control A.8.15.
- **Enterprise Operator Manual (`docs/MANUAL_DE_USO.md`):** Comprehensive guide for deployment, interactive runs, GPO automation, parameters, and verification.
- **SIEM Ingestion Guide (`docs/SIEM_INTEGRATION.md`):** Complete setup for Splunk Universal Forwarder, Elastic Filebeat, Datadog Agent, and Microsoft Sentinel (KQL/SPL alert queries).
- **Troubleshooting & Diagnostic Matrix (`docs/TROUBLESHOOTING.md`):** Root-cause analysis and resolutions for exit codes, UAC elevation, Task Scheduler status, and OneDrive cloud-only files.
- **GitHub Issue Templates:** Added `.github/ISSUE_TEMPLATE/bug_report.md` and `.github/ISSUE_TEMPLATE/feature_request.md`.
- **Developer Guidelines:** Added `.cursorrules` and `CLAUDE.md`.
- **Expanded Test Suite (`tests/Sentinel-Engines.Tests.ps1`):** Pester test suite expanded to 25/25 tests passing (100% automated coverage).

### Fixed
- **`RemainingScripts` Binding Bug in `Sentinel-Logger.psm1`:** Resolved critical parameter binding exception caused by pipeline evaluation of `ForEach-Object` with `-join` in Windows PowerShell 5.1; refactored to native, zero-overhead `foreach` iteration.

### Changed
- **GPO Environment Variable Support:** `Sentinel-Logger.psm1` now reads `$env:SENTINEL_LOG_FORMAT` and `$env:SENTINEL_LOG_DIR` before falling back to defaults.
- **Dry-Run Switch (`-AuditOnly`):** Added to `Invoke-CleanupScreenshots.ps1` and `Invoke-CleanupTempGlobal.ps1` for safe enterprise simulation.
- **DISM Exit Code Auditing:** `Invoke-IntegrityCheck.ps1` now evaluates standard DISM exit codes (0 = Healthy, 20 = Corrupted repairable, 3010 = Reboot required) and supports `-AutoRepair`.
- **Defender Parameterization:** `Invoke-DefenderScan.ps1` supports `-ScanType 1|2`, pre-scan signature definition updates, and exit code capture.
- **Expanded Specifications:** Completely rewrote `docs/ARCHITECTURE.md` (Mermaid diagrams, threat model) and `docs/ISO_MAPPING.md` (auditor evidence matrix).
- **Installer Harmonization:** Updated `installer/Install-Sentinel.ps1` and `installer/Uninstall-Sentinel.ps1` to orchestrate all 6 engines.

---

## [2.1.0] - 2026-10-25

### Added
- Standardized open-source community files: `CODE_OF_CONDUCT.md`, `CONTRIBUTING.md`, and enterprise `SECURITY.md`.
- Automated test suite with Pester unit tests and cross-platform AST syntax validator (`tests/`).
- GitHub Actions CI workflow for script validation and automated testing (`.github/workflows/test.yml`).
- Pull request template (`.github/PULL_REQUEST_TEMPLATE.md`).
- Explicit `.gitignore` for Windows endpoint artifacts, runtime logs, and test results.

### Changed
- Standardized project license to official **MIT License** for unrestricted adoption.
- Enhanced `Sentinel-Logger.psm1` with robust null-metadata handling and explicit UTF-8 encoding.
- Modernized documentation, control mappings (ISO 27001 / ISO 20000-1), and architecture references.

---

## [2.0.0] - 2026-04-10

### Added
- Hub-and-Spoke modular architecture with `src/lib/` and `src/engines/`.
- Dual logging formats: Human-readable text and SIEM-ingestible JSON with timestamp, PID, and identity.
- ISO 27001:2022 and ISO 20000-1 control mapping.
- Silent enterprise installer supporting automated Intune/SCCM deployment.

---

## [1.0.0] - 2025-11-15

### Added
- Initial standalone PowerShell automation scripts for endpoint hygiene and malware scanning.
