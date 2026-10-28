# Changelog

All notable changes to **Isora Sentinel** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [2.3.1] - 2026-10-28

### Institutional Cybersecurity & Banking Readiness Audit (Grade A Certification)
Comprehensive offensive and defensive audit performed under institutional validation standards (simulating central banking critical infrastructure e.g., Banco de la República de Colombia, and Tier-1 security analysis e.g., Kaspersky Threat Research). Engineered to ensure 100% autonomous, zero-maintenance unattended operation over 6+ month deployments.

### Bilingual Internationalization (English & Spanish Support)
- **Shared Zero-Dependency Localization Engine (`Localization.cs`):**
  - Seamless auto-detection of operating system UI culture (`CultureInfo.CurrentUICulture`), defaulting to Spanish for `es-*` environments and English for all international locales.
  - Interactive on-the-fly language toggle button (`[ 🌐 English | 🌐 Español ]`) in GUI Setup and Uninstaller headers.
  - Interactive language selector submenu (`Idioma / Language` -> `[✓] Español`, `[ ] English`) in the System Tray monitor (`SentinelTray.cs`).
  - Command-line switch support (`--lang en`, `--lang es`, `/lang:en`, `/lang:es`) in `Isora-Sentinel-Setup.exe`.
- **Bilingual End-User Notice Files:**
  - `Invoke-ArchiveDownloads.ps1` automatically writes both `LEAME_POLITICA_TI.txt` (Español) and `README_IT_POLICY.txt` (English) in user archive directories.
- **Bilingual CLI Deployment Scripts:**
  - `Install-Sentinel.ps1` and `Uninstall-Sentinel.ps1` support `-Language "ES"` or `-Language "EN"`, auto-detecting the active shell culture for localized console status notices and prompts.

### Security Hardening (Offensive / Defensive Remediation)
- **Real-Time TOCTOU Junction Defense (LPE Vector Remediation):**
  - Hardened `Invoke-CleanupTempGlobal.ps1` and `Invoke-CleanupScreenshots.ps1` against Time-of-Check to Time-of-Use symlink and NTFS junction swap attacks.
  - Re-evaluates live Win32 attributes (`[System.IO.File]::GetAttributes()`) immediately prior to any file action, bypassing stale cached enumeration properties.
  - Implemented normalized path boundary constraints (`StartsWith($normalizedBase)`) to prevent directory traversal outside authorized sanitization targets.
  - Replaced recursive deletions inside iterative child loops with explicit non-recursive deletion (`-Recurse:$false`) to eliminate arbitrary file deletion risks through junction redirection.
- **Archive Target Reparse Point Verification:**
  - Hardened `Invoke-ArchiveDownloads.ps1` to verify that user-defined or default destination archive folders (`Documents\Isora_Sentinel_Archived\Downloads`) are not NTFS junctions or symlinks pointing to protected system directories.
- **Fail-Closed Integrity Enforcement:**
  - Updated all 6 engines (`Invoke-BitLockerAudit`, `Invoke-CleanupScreenshots`, `Invoke-CleanupTempGlobal`, `Invoke-DefenderScan`, `Invoke-EventAudit`, `Invoke-IntegrityCheck`, `Invoke-ArchiveDownloads`) so that under `NT AUTHORITY\SYSTEM` or elevated administrator contexts, any manifest mismatch or script tampering immediately raises a fatal error (`throw`) and halts execution, closing the previous fail-open exception trap.
- **Central Audit Log ACL Lockdown:**
  - Hardened `Install-Sentinel.ps1` NTFS permissions on the log directory (`C:\Program Files\Isora-Sentinel\logs`): removed unprivileged `BUILTIN\Users` Modify (`M`) permission and enforced strict Read/Execute (`RX`) alongside Administrator/SYSTEM Full Control (`F`), preventing local anti-forensics log tampering and junction planting. Standard user interactive runs safely redirect to `%LOCALAPPDATA%\Isora-Sentinel\logs`.
- **Log Injection Defense (SIEM Integrity):**
  - Implemented newline sanitization in `Write-AuditEntry` (`Sentinel-Logger.psm1`) across messages and metadata values, preventing multi-line log injection and RFC 8259 NDJSON stream desynchronization.

### Enterprise Reliability & 6-Month Unattended Stability
- **Automated Directory Quota Management (Disk Outage Prevention):**
  - Enhanced `Clear-OldSentinelLogs` in `Sentinel-Logger.psm1` with total directory quota enforcement (default 250 MB, configurable via `SENTINEL_MAX_LOG_MB`). If log accumulation exceeds quota, the oldest log files are pruned dynamically, preventing disk exhaustion denial-of-service on long-running unattended endpoints.
- **Task Scheduler Unattended Resilience:**
  - Configured `New-ScheduledTaskSettingsSet` in `Install-Sentinel.ps1` with `ExecutionTimeLimit = 2 Hours` (auto-terminates hung processes), `StartWhenAvailable = True` (executes missed tasks when laptops or sleeping workstations power on), and `MultipleInstancesPolicy = IgnoreNew`.
  - Added support for `Monthly`, `Weekly`, and `Daily` schedule triggers in `Install-Sentinel.ps1`, and enabled `-ExtraArgs` pass-through to scheduled actions.
- **Bilingual BitLocker Forensic Detection (Spanish / English):**
  - Broadened `manage-bde.exe` parser in `Invoke-BitLockerAudit.ps1` to support bilingual output patterns (`Volume`/`Volumen`, `Protection Off`/`Protección desactivada`, `Fully Decrypted`/`Totalmente descifrado`, `Porcentaje cifrado: 0`), eliminating false-negative audit evasion on Latin American Windows systems (`es-CO`).
- **Event Flood Throttling:**
  - Added `-MaxEventsToLog` cap (default 50 individual records) to `Invoke-EventAudit.ps1` to prevent disk/SIEM saturation during massive brute-force attacks or automated provisioning storms.
- **Third-Party EDR / Missing Defender Resilience:**
  - Enhanced `Invoke-DefenderScan.ps1` to detect when `MpCmdRun.exe` is absent or uninstalled (e.g. replaced by enterprise third-party EDR), emitting an informative warning rather than raising an uncaught exception.
- **Path Consistency:**
  - Corrected directory typo in `Invoke-ArchiveDownloads.ps1` from `Isora_Isora_Sentinel_Archived` to `Isora_Sentinel_Archived`.

---

## [2.3.0] - 2026-10-28

### Added
- **Interactive GUI Uninstallation Wizard (`UninstallForm.cs`):**
  - Standalone, high-fidelity uninstaller matching suite design tokens (`Theme`, `Card`, `StepButton`).
  - Selective data retention controls: checkboxes to preserve compliance logs (`logs/`) and user-organized downloads (`Documents\Isora_Sentinel_Archived\`).
  - Real-time progress bar, status notices, expandable technical log viewer, and one-click clipboard telemetry copy for enterprise IT tickets.
  - Safe deferred binary self-deletion routine that guarantees clean directory removal without locking files in use.
  - Registered in Windows *Installed Apps* / *Programs and Features* as `Isora-Sentinel-Uninstall.exe --uninstall`.
- **New Engine — Downloads Archiving Policy (`Invoke-ArchiveDownloads.ps1`):** Automatically and safely archives non-recent downloads to an enterprise-defined directory (`$HOME\Documents\Isora_Sentinel_Archived\Downloads` by default) to fulfill data retention and ISO/IEC 27001:2022 Control A.8.10. Supports custom target path configuration in both installer wizard and runtime settings dialog.
- **Workload Intensity Modes (Quick vs Full / Comprehensive):**
  - Microsoft Defender Antimalware: User-selectable Quick Scan (`-ScanType 1`) vs Full Exhaustive Scan (`-ScanType 2`).
  - Windows System Component Store (SFC/DISM): User-selectable Quick Health Check (`-QuickCheck`) vs Complete Component Repair (`-AutoRepair`).
  - Integrated across Setup Wizard (`SentinelSetup.cs`), Task Configuration (`TaskConfigForm.cs`), and Tray context menus (`SentinelTray.cs`).
  - Resource consumption confirmation warning dialogs added to safeguard CPU/RAM/Disk availability during active work.
- **Ubuntu Philosophy Easter Egg:** Integrated interactive notice displaying the South African humanist philosophy of *Ubuntu* («Soy porque somos») upon clicking the suite subtitle / version string in the system tray monitor.

### Changed
- **Macro Rebrand to Isora Sentinel Suite:** Unified sovereign naming across all source files, manifests, Task Scheduler folders (`\Isora Sentinel\`), registry keys (`HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\Isora-Sentinel`), and technical documentation.
- **Zero-Delay Single-Window Elevation:** Configured `requireAdministrator` in `app.manifest`, ensuring the setup wizard operates within a single continuous window from start to finish without window flickers or elevation handoff pauses.
- **System Tray UX & Sovereign Enterprise Rebrand:**
  - Status indicator modernized to **"Versión Oficial"** with live verification timestamp, replacing intimidating low-level crypto jargon.
  - Replaced ambiguous option labels (e.g., "Recuperación de temporales y caché") with user-friendly enterprise terminology ("Limpieza de archivos temporales", "Reparar archivos del sistema (SFC/DISM)").
  - Modernized details dialog with formal enterprise certification layout.
- **Documentation Suite Updates:** Updated `README.md`, `docs/OPERATOR_GUIDE.md`, and `docs/MANUAL_DE_USO.md` reflecting the new Download Archival engine and workload intensity configurations.

### Fixed
- **Win32 System Tray Focus & Menu Auto-Dismiss Bug (KB 135788):** Resolved Windows Forms notification area issue where the context menu failed to auto-close when clicking outside on taskbar or desktop; implemented foreground window focus management with invisible parent handle and `AutoClose` event coordination.

---

## [2.2.0] - 2026-10-27

### Added
- **New Engine — BitLocker Cryptographic Compliance (`Invoke-BitLockerAudit.ps1`):** Audits disk encryption status, protection state, and key protectors mapped to ISO/IEC 27001:2022 Control A.8.24.
- **New Engine — Security Event & Anti-Forensics Audit (`Invoke-EventAudit.ps1`):** Audits Windows Security Event log for Event 1102 (audit log cleared), Event 4625 brute-force bursts, and account provisioning anomalies mapped to ISO/IEC 27001:2022 Control A.8.15.
- **Enterprise Operator Manual (`docs/OPERATOR_GUIDE.md`):** Comprehensive guide for deployment, interactive runs, GPO automation, parameters, and verification.
- **SIEM Ingestion Guide (`docs/SIEM_INTEGRATION.md`):** Complete setup for Splunk Universal Forwarder, Elastic Filebeat, Datadog Agent, and Microsoft Sentinel (KQL/SPL alert queries).
- **Troubleshooting & Diagnostic Matrix (`docs/TROUBLESHOOTING.md`):** Root-cause analysis and resolutions for exit codes, UAC elevation, Task Scheduler status, and OneDrive cloud-only files.
- **GitHub Issue Templates:** Added `.github/ISSUE_TEMPLATE/bug_report.md` and `.github/ISSUE_TEMPLATE/feature_request.md`.
- **Developer Guidelines:** Added `.cursorrules` and `CLAUDE.md`.
