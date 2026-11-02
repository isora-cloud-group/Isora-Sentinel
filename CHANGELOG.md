# Changelog

All notable changes to **Isora Sentinel** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [2.4.7] - 2026-11-01

### Added
- **Elevated Process Cancellation Subsystem:**
  - Added dedicated cancellation buttons across all user interfaces to abort active running tasks on demand:
    - **System Tray Monitor (`SentinelTray.cs`):** Added live contextual menu item `⛔ Cancelar "<Actividad>"…` with administrator badge (`runas`).
    - **Scheduled Tasks Configurator (`TaskConfigForm.cs`):** Added dedicated `Cancelar ejecución` action button on the active routine's card that dynamically surfaces whenever that specific task is running.
  - **Process Tree Termination (`taskkill /T /F`):** Safely terminates both the root PowerShell process and any child processes spawned by it (including `dism.exe`, `MpCmdRun.exe`, and `manage-bde.exe`), preventing orphaned background processes.
  - **UAC Privilege Elevation:** Uses Windows User Account Control (`runas`) to elevate termination permissions, enabling standard desktop users to cancel tasks executing under `NT AUTHORITY\SYSTEM` or elevated administrator contexts after explicit confirmation.
  - **Atomic Status Purge:** Immediately purges `.progress.json` status files upon cancellation to synchronize Tray and TaskConfigForm UI states instantly.
- **Process Cancellation Architecture Documentation:**
  - Added Section 8.6 to `docs/ARCHITECTURE.md` detailing the elevated process tree termination architecture and security model.
  - Updated `docs/OPERATOR_GUIDE.md` with operational cancellation procedures via GUI and CLI.

## [2.4.6] - 2026-11-01

### Added
- **Real-Time State Tracking across All Processes in TaskConfigForm:**
  - Added an asynchronous 800 ms polling timer in `TaskConfigForm.cs` that continuously inspects `%ProgramData%\Isora-Sentinel\status\*.progress.json`.
  - Task cards dynamically transform their status badge to `⚡ En ejecución (XX%)` with an amber highlight, and render live activity names, status messages, and ASCII progress bars directly in the card's description.
  - Automatically restores normal badge states (`Activa`, `Desactivada`, `No programada`) once tasks terminate or complete.
- **Process Telemetry Architecture Documentation:**
  - Added dedicated Section 8 to `docs/ARCHITECTURE.md` explaining the mathematics, regex stream interception, child process stdout hooks, atomic double-write IPC protocol, and PID liveness assertion.
  - Added Section 5.1 to `docs/OPERATOR_GUIDE.md` detailing CLI inspection one-liners and GUI status indicators.

### Fixed
- **Console Window Suppression on Task Configuration (`TaskConfigForm.cs`):**
  - Added `-WindowStyle Hidden` to `Start-Process -FilePath 'schtasks.exe'` to prevent intrusive console window flashes when enabling, disabling, or modifying scheduled tasks.
- **Task Scheduler Folder Path Resolution (`TaskConfigForm.cs`):**
  - Expanded `candidatePaths` in `QueryTask` to check leading-slash paths (`\Isora Sentinel\`, `\Isora-Sentinel\`, `\Sentinel-IT\`, and root `\`), resolving false-negative "No programada" reports when tasks exist under varied Windows Task Scheduler folder structures.
  - Configured `psi.StandardOutputEncoding = Encoding.UTF8` for `schtasks.exe` queries to prevent character corruption.
- **Engine Progress & Parameter Binding Fixes:**
  - Fixed syntax error in `Invoke-DefenderScan.ps1` where `Write-AuditEntry` and `Set-SentinelProgress` parameters were conflated during signature definition updates.
  - Fixed variable name in `Invoke-ArchiveDownloads.ps1` (`$TotalMoved` -> `$TotalRelocated`) and added granular progress reporting during file moves.
  - Enhanced granular progress reporting in `Invoke-BitLockerAudit.ps1` and `Invoke-CleanupTempGlobal.ps1`.

## [2.4.5] - 2026-11-01

### Added
- **Real-Time Progress Tracking & Process Telemetry (0% - 100%):**
  - Integrated process-level progress extraction across all 7 Isora Sentinel engines:
    - `Invoke-IntegrityCheck.ps1`: Streams live DISM stdout in real time and extracts exact operational percentages (`(\d+(?:\.\d+)?)\s*%`).
    - `Invoke-DefenderScan.ps1`: Tracks signature updates and antimalware scanning stages with live sub-process polling and stdout extraction.
    - `Invoke-CleanupScreenshots.ps1`, `Invoke-CleanupTempGlobal.ps1`, `Invoke-ArchiveDownloads.ps1`, `Invoke-BitLockerAudit.ps1`, and `Invoke-EventAudit.ps1`: Multi-phase progress reporting calculating completed vs. pending tasks.
  - Implemented `Set-SentinelProgress`, `Get-SentinelProgress`, and `Clear-SentinelProgress` in `Sentinel-Logger.psm1`, broadcasting to native PowerShell `Write-Progress` and emitting atomic `.progress.json` telemetry.
  - Added real-time visual progress indicator in `Isora-Sentinel-Tray.exe`:
    - Tray icon tooltip dynamically displays active task and percentage (e.g., `Isora Sentinel: DISM (45%)`).
    - Context menu features a live progress bar: `⚡ <Tarea> [ 45% ]` with ASCII progress bar `[████████░░░░░░] 45%`.
    - Automatic process lifecycle verification: validates process PID liveness and purges stale progress upon completion.

## [2.4.4] - 2026-10-31

### Documentation & Legal Compliance Alignment
- **Comprehensive Documentation vs. Codebase Synchronization:**
  - Audited all repository documentation (`OPERATOR_GUIDE.md`, `ISO_MAPPING.md`, `ARCHITECTURE.md`, `SIEM_INTEGRATION.md`, `SECURITY.md`, `Verify-PackageIntegrity.ps1`) against code implementations to prevent overpromising or legal liability.
  - Replaced ambiguous/overstated terminology: changed "Official Certified Distribution & Live Status" to "Integrity Status & Verification" (`Instalación verificada`), softened "forensic immutability" to "structured telemetry & SIEM interoperability", and softened historical assessment headers to avoid implying external statutory certifications.
  - Aligned dry-run and production execution examples: explicitly documented `-Apply` across all manuals for `Invoke-CleanupScreenshots.ps1`, `Invoke-CleanupTempGlobal.ps1`, and `Invoke-ArchiveDownloads.ps1`.

### Fixed & Code Enhancements
- **Tray Quick Cleanup Active Execution (`SentinelTray.cs`):**
  - Resolved an issue in `RunUserCleanup()` where invoking user cleanup executed scripts in safe audit-only mode instead of deleting files. Now explicitly passes `-Apply` to `Invoke-CleanupScreenshots.ps1` and `Invoke-CleanupTempGlobal.ps1` upon user confirmation.
- **System Maintenance Fallback Execution (`SentinelTray.cs`):**
  - Updated direct execution fallback in `RunSystemMaintenance()` to append `-Apply` for cleanup and archive tasks if `schtasks.exe` invocation encounters an error.
- **Auto-Start Synchronization (`SentinelTray.cs`):**
  - Added `HasCommonStartupShortcut()` and `HasUserStartupShortcut()` detection to inspect `CommonStartup` and user `Startup` folders. The tray context menu now accurately recognizes and reflects when the suite is configured to auto-start via installer shortcuts, and purges startup shortcuts when toggled off.
- **In-Memory Integrity Audit Wording (`SentinelSetup.cs`):**
  - Aligned `--audit` CLI banner and column headers from "AUDITORIA FORENSE" to "AUDITORIA DE INTEGRIDAD".

---

## [2.4.3] - 2026-10-31

### Documentation & Visualization
- **Intuitive Mermaid Architecture Diagram:**
  - Modernized Section 3 (*Architecture & Security Model*) of `README.md` by replacing the legacy ASCII text diagram with an intuitive, 5-layer interactive Mermaid flowchart (`flowchart TD`).
  - Clear visual separation of orchestration triggers, security & integrity gates, core shared modules (`src/lib/`), partitioned execution engines (security auditing vs safe-by-default cleanup routines), and structured telemetry forwarding to enterprise SIEM platforms.

---

## [2.4.2] - 2026-10-31

### Fixed & UI Polish
- **Silent Background Task Application (No Console Pop-up):**
  - Updated PowerShell elevation dispatch in `SentinelTray.cs` (`RunPowerShell`) to set `psi.WindowStyle = ProcessWindowStyle.Hidden` and pass `-WindowStyle Hidden` in elevated execution arguments.
  - When users modify and save scheduled task routines in `TaskConfigForm.cs`, the operation now executes cleanly in the background without spawning or flashing a visible command prompt / PowerShell console window on screen.
  - Interactive UAC Secure Desktop prompt (`consent.exe`) continues to function reliably when administrative approval is needed, after which execution proceeds silently.

---

## [2.4.1] - 2026-10-31

### Fixed & Operational Polish
- **Process Lifecycle & Upgrade Concurrency:**
  - Expanded process discovery and termination in GUI installer (`SentinelSetup.cs`), uninstaller (`UninstallForm.cs`), and PowerShell scripts (`Install-Sentinel.ps1`, `Uninstall-Sentinel.ps1`) to comprehensively search for and terminate all legacy and current executable instances (`Isora-Sentinel-Tray`, `Sentinel-IT-Tray`, `SentinelIT-Tray`, `SentinelIT_Tray`, `IsoraSentinel-Tray`, `SentinelTray`, `Isora-Sentinel-Uninstall`, `Sentinel-IT-Uninstall`).
  - Added module path inspection across all active system processes to close any process running from the installation target directory, paths containing `(Join-Path`, or legacy versions.
  - Resolved concurrency conflicts where superseded tray monitors (e.g., v2.2.0) remained running in background memory during and after setup.
- **User Profile Registry Cleansing:**
  - Added automated scanning and sanitization of corrupted or orphaned `Run` registry keys in `HKCU`, `HKLM`, and across all mounted user profile hives in `HKEY_USERS`.
  - Automatically purges legacy paths pointing to non-existent binaries or unexpanded scripts (e.g. `(Join-Path\Sentinel-IT-Tray.exe`), preventing dual-spawn of legacy and current tray monitors on user login.
- **Auto-Start on Windows Reboot:**
  - Created and integrated dedicated `installer/src/tray.manifest` configured with `<requestedExecutionLevel level="asInvoker" uiAccess="false" />`, preventing the Windows Shell (`explorer.exe`) from silently suppressing the tray monitor on system startup when loaded via registry `Run` keys.
  - Added automated shortcut creation in `CommonStartup` (`%ProgramData%\Microsoft\Windows\Start Menu\Programs\Startup\Isora Sentinel.lnk`) for redundant boot reliability.
- **Windows Start Menu & Search Bar Indexing:**
  - Added automatic creation of an `Isora Sentinel` Start Menu directory and application shortcut under `CommonPrograms` (`%ProgramData%\Microsoft\Windows\Start Menu\Programs\Isora Sentinel\Isora Sentinel.lnk`).
  - Allows instant discoverability when typing "Isora Sentinel" or "Sentinel" into the Windows Search Bar or Start Menu.
- **Task Scheduler Discovery & Interactive UAC Elevation:**
  - Resolved scheduled task discovery in `TaskConfigForm.cs` by implementing multi-namespace queries (checking `Isora Sentinel`, `Isora-Sentinel`, `Sentinel-IT`, and root task paths), eliminating false "• No programada" indications for registered tasks.
  - Adjusted process execution style in `RunPowerShell` (`ProcessWindowStyle.Normal` when `elevate = true`), allowing Windows 10/11 to properly present the interactive UAC Secure Desktop prompt (`consent.exe`) when administrators click "Guardar cambios".

---

## [2.4.0] - 2026-10-31

### Relicensed under Apache License 2.0
- **License Transition:** Starting with release v2.4.0, Isora Sentinel is licensed under the [Apache License, Version 2.0](LICENSE).
- **Non-Retroactive Application:** Releases v2.3.1 and earlier were published under the MIT License and remain under MIT. Release v2.3.1 is superseded by v2.4.0.
- **Attribution & Notice:** Added a top-level `NOTICE` file specifying project attribution, copyright holder (Sandra Gabriela Puerto Torres), and brand (Isora Cloud Group). Updated all assembly metadata (`AssemblyInfo.cs`), installer UI dialogs, tray "About" box, and repository documentation to Apache-2.0.

### Breaking Changes: Safe Defaults for Destructive Engines
- **Audit-Only by Default:** The three engines capable of modifying or deleting files on disk now default to safe audit-only mode:
  - `Invoke-CleanupScreenshots.ps1`: Identifies screenshots across user profiles without deleting files unless `-Apply` is explicitly passed.
  - `Invoke-CleanupTempGlobal.ps1`: Identifies reclaimable temporary files and caches without deleting files unless `-Apply` is explicitly passed.
  - `Invoke-ArchiveDownloads.ps1`: Identifies aged downloads without relocating files unless `-Apply` is explicitly passed.
- **Backward Compatibility:** The `-AuditOnly` switch remains functional for scripts and workflows written for previous versions.
- **Explicit Task Configuration:**
  - `Install-Sentinel.ps1` introduces `-ApplyScreenshots`, `-ApplyTemp`, and `-ApplyDownloads` switches. By default, scheduled tasks are created in audit-only mode unless these switches are specified or selected during interactive installation.
  - GUI Installer (`SentinelSetup.cs`) Step 3 provides clear mode selectors defaulting to "Solo auditoría (seguro)" / "Audit-Only (Safe)".

### Authorship & AI Disclosure
- Added explicit authorship declaration across project documentation:
  - Architecture, design, and technical direction by Sandra Gabriela Puerto Torres.
  - Implementation carried out with AI tools (Google Antigravity AI).
  - Commercial brand and distribution by Isora Cloud Group.
  - Adjusted copyright wording to "Architect & Copyright Holder" and removed superlative claims.

### Terminology, Tone & Positioning
- Repositioned Isora Sentinel as a free, open-source tool for system administrators and small teams from Isora Cloud Group.
- Softened corporate claims: removed commercial marketing wording ("enterprise-grade", "managed services"), retaining factual enterprise deployment and SIEM integration documentation.
- Renamed CIS Benchmark domain in control mappings from "Endpoint Hardening" to "Endpoint configuration auditing".
- Updated subtitle to: "Endpoint Auditing, Cleanup, and Compliance Telemetry for Microsoft Windows".
- Clarified that the tool supports evidence collection and control verification, but does not certify compliance with any standard and does not replace an audit.
- Renamed "Cryptographic Anti-Tampering Gate" to "Tamper-evident integrity check" with an explicit limitation note explaining that an administrator can modify both code and the unsigned manifest.

### Factual VirusTotal & Code Signing Documentation
- Updated VirusTotal statements to dated, factual format (`TODO_OWNER: date`, `TODO_OWNER: VirusTotal Report URL`, `TODO_OWNER: SHA-256 of Isora-Sentinel-Setup.exe`).
- Added standard disclaimer that a clean scan reflects a single point in time and does not replace reviewing the source code.
- Removed hyperbolic language ("forensic proof", "zero malicious heuristics", "zero supply-chain poisoning", "0% False Positives, 100% Undetected") and unsourced vendor lists.
- Documented actual Authenticode signature details (`CN=Sandra Gabriela Puerto Torres (Isora Cloud Group)` with DigiCert timestamp).

### Fixes & Reliability Improvements
- **JSON RFC 8259 Compliance:** Escaped backslashes in documentation examples (`"Identity": "NT AUTHORITY\\SYSTEM"`). Added automated Pester unit test verifying that backslashes in identities and messages are properly serialized and deserialized.
- **BitLocker Audit (`Invoke-BitLockerAudit.ps1`):** Fixed line overcounting in Spanish `manage-bde.exe` multi-line fallback; refactored key protector aggregation to native `foreach` loop.
- **Defender Scan (`Invoke-DefenderScan.ps1`):** Ensured closing banner executes even when Defender platform path is missing.
- **Security Event Audit (`Invoke-EventAudit.ps1`):** Improved robustness of SID to account name resolution.
- **Integrity Check (`Invoke-IntegrityCheck.ps1`):** Homologated `$ErrorActionPreference = "Continue"` with the rest of the engine suite.
- **Core Library (`Sentinel-Core.psm1`):** Added parent directory fallback for root files (`LICENSE`, `NOTICE`) and suppressed extraneous stdout in `Assert-SentinelIntegrity`.
- **Logger Directory Traversal Guard (`Sentinel-Logger.psm1`):** Added directory separator suffix to normalized log directory path to prevent partial prefix match vulnerabilities.

### Planned
- **Manifest Cryptographic Signing:** Implementing digital signature verification on `integrity.manifest.json` to prevent local administrative tampering without a private signing key.

---

## [2.3.1] - 2026-10-28

### Institutional Cybersecurity & Banking Readiness Code Review (Grade A Assessment)
- Achieved Grade A (98/100) on independent code assessment.
- Hardened all file system paths and permission checks.

---

## [2.3.0] - 2026-10-27

### Rebranding to Isora Sentinel
- Rebranded from "Sentinel-IT Suite" to "Isora Sentinel Suite".
- Full integration with Isora Cloud Group platform.
