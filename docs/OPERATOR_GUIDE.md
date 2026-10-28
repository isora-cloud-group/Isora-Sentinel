# Enterprise Operator & Deployment Manual — Isora Sentinel Suite

**Commercial Brand & Distribution:** Isora Cloud Group  
**Sole Copyright Holder & Architect:** Sandra Gabriela Puerto Torres ([sandrapuerto.com](https://sandrapuerto.com) · [GitHub: @sandra-puerto](https://github.com/sandra-puerto) · [LinkedIn](https://www.linkedin.com/in/sandra-puerto/))  
**Software Version:** 2.3.0  
**Target Operating Systems:** Windows 10 (1809+), Windows 11, Windows Server 2016 / 2019 / 2022 / 2025  
**Execution Runtimes:** Windows PowerShell 5.1+ or PowerShell Core 7+ (`pwsh`)  
**Privilege Context:** `NT AUTHORITY\SYSTEM` / Elevated Local Administrator  

---

## 1. Introduction & Operational Scope

**Isora Sentinel** is an enterprise endpoint hardening and regulatory governance suite for corporate Microsoft Windows environments. Designed under a modular **Hub-and-Spoke** architecture, it separates shared core libraries and telemetry modules (`src/lib/`) from isolated, purpose-built execution engines (`src/engines/`).

Each routine maps directly to international standards (**ISO/IEC 27001:2022** and **ISO/IEC 20000-1:2018**), outputting immutable, RFC 8259-compliant NDJSON forensic logs ready for immediate ingestion by enterprise SIEM platforms (Splunk, Elastic, Datadog, Microsoft Sentinel).

---

## 2. System Requirements & Privilege Baseline

### Hardware & Operating System
* **Operating System:** Windows 10 (Build 17763 / 1809 or higher), Windows 11 (all versions), Windows Server 2016 / 2019 / 2022 / 2025.
* **PowerShell:** Windows PowerShell 5.1 (native built-in) or PowerShell Core 7.x (`pwsh`).
* **Privileges:** Administrator privileges for installation and configuration; automated routines run under `NT AUTHORITY\SYSTEM`.
* **External Dependencies:** **Zero external dependencies**. Does not require third-party package managers (Chocolatey, Scoop), external runtimes (Node.js, Python), or additional .NET SDK installations.

---

## 3. Enterprise Deployment Methods

Isora Sentinel offers three deployment workflows tailored to different operational requirements:

### 3.1. Standalone Enterprise GUI Installer (`Isora-Sentinel-Setup.exe`)
Ideal for systems administrators, desktop support engineers, and standalone workstations:

1. Launch `Isora-Sentinel-Setup.exe` with Administrator privileges (prompts for standard Windows UAC elevation).
2. The installation wizard features an enterprise setup wizard with four structured steps:
   * **Step 1 (Welcome & Scope):** Displays compliance overview (ISO 27001 / ISO 20000-1 / SOC 2) and validates local administrator privileges.
   * **Step 2 (Paths & Integrity):**
     * Installation Directory: Defaults to `C:\Program Files\Isora-Sentinel` (hardened with strict NTFS ACLs).
     * Forensic Log Directory: Defaults to `C:\Program Files\Isora-Sentinel\logs`.
     * Downloads Archival Directory: Configurable target path for archiving aged downloads (defaults to `$HOME\Documents\Isora_Sentinel_Archived\Downloads`).
     * Pre-Flight SHA-256 Validation: Verifies package hashes against `integrity.manifest.json` before writing files to disk.
     * Control Panel Registration: Enables Windows *Installed Apps* / *Programs and Features* registration.
   * **Step 3 (SYSTEM Scheduled Tasks & Workload Modes):** Select which of the 7 automated routines to schedule, with configurable execution frequencies (Daily / Weekly / Monthly) and workload intensity modes (Quick vs Full scan for Defender; Quick check vs Full repair for Component Store) with safeguard resource warnings.
   * **Step 4 (Installation & Telemetry):**
     * Click **"Instalar Suite"**.
     * The installer detects any pre-existing installation, prompts for confirmation, gracefully closes old running processes, creates an audit backup archive (`backup/archive_YYYYMMDD_HHMMSS_vX_Y_Z/`), preserves historical logs, extracts fresh modules, validates SHA-256 hashes, registers the tasks under `NT AUTHORITY\SYSTEM`, and launches the System Tray Monitor.

#### Non-Interactive & Forensic CLI Flags for `Isora-Sentinel-Setup.exe`:
Enterprise SOC teams, auditors, and blue teams can inspect the installer without launching the GUI:

| Parameter | Function | Example |
| :--- | :--- | :--- |
| `--audit` | Performs an in-memory SHA-256 integrity check of all embedded package files against the internal manifest. Returns exit code 0 if all digests match. | `.\Isora-Sentinel-Setup.exe --audit` |
| `--extract <dir>` | Decompresses and extracts the full source package into the designated directory for offline static code analysis. | `.\Isora-Sentinel-Setup.exe --extract C:\Sandbox\Review` |
| `--version` | Prints the assembly product, version, and copyright details. | `.\Isora-Sentinel-Setup.exe --version` |
| `--help` | Displays command line usage and available parameters. | `.\Isora-Sentinel-Setup.exe --help` |

---

### 3.2. Unattended Enterprise CLI Deployment (`Install-Sentinel.ps1`)
Designed for automated mass deployment via Microsoft Intune, SCCM/MECM, Active Directory GPO, Datadog RMM, or NinjaOne:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\installer\Install-Sentinel.ps1 `
    -Silent `
    -InstallDir "C:\Program Files\Isora-Sentinel" `
    -ScheduleScreenshots "Daily" `
    -ScheduleTemp "Daily" `
    -ScheduleDefender "Weekly" `
    -ScheduleIntegrity "Weekly" `
    -ScheduleBitLocker "Daily" `
    -ScheduleEventAudit "Daily"
```

#### CLI Deployment Parameters:
| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `-InstallDir` | String | `C:\Program Files\Isora-Sentinel` | Destination directory. Enforces administrative permissions. |
| `-Silent` | Switch | `False` | Suppresses console prompts for non-interactive RMM execution. |
| `-SkipIntegrityCheck` | Switch | `False` | Bypasses pre-flight SHA-256 validation (for emergency recovery only). |
| `-ScheduleScreenshots`| String | `Daily` | Frequency for screenshot cleanup (`Daily`, `Weekly`, `Monthly`). |
| `-ScheduleTemp` | String | `Daily` | Frequency for temporary file reclamation (`Daily`, `Weekly`, `Monthly`). |
| `-ScheduleDefender` | String | `Weekly` | Frequency for Defender scan (`Daily`, `Weekly`, `Monthly`). |
| `-ScheduleIntegrity` | String | `Weekly` | Frequency for DISM integrity scan (`Daily`, `Weekly`, `Monthly`). |
| `-ScheduleBitLocker` | String | `Daily` | Frequency for BitLocker audit (`Daily`, `Weekly`, `Monthly`). |
| `-ScheduleEventAudit`| String | `Daily` | Frequency for security event audit (`Daily`, `Weekly`, `Monthly`). |

---

### 3.3. System Tray Monitor & Task Configurator (`Isora-Sentinel-Tray.exe`)
The System Tray application provides continuous endpoint observability and interactive task management:

* **Official Certified Distribution & Live Status:** Displays **"Versión Oficial"** with live verification timestamp and certification details, confirming all endpoint protection components are unaltered and authorized.
* **Workload Intensity Controls:** Allows selecting between Quick and Full Exhaustive scans for Defender and Component Store directly from the menu, with built-in modal confirmation warnings against accidental resource exhaustion.
* **Ubuntu Philosophy Easter Egg:** Clicking the suite subtitle in the tray menu displays an institutional note on the humanist philosophy of Ubuntu («Soy porque somos»).
* **On-Demand Routine Execution:** Trigger individual modules directly from the tray. Modules requiring administrative privileges (Defender, DISM, BitLocker, EventAudit) display a confirmation dialog, request UAC elevation, and execute through Task Scheduler under `NT AUTHORITY\SYSTEM`.
* **Installed Services Configurator (`TaskConfigForm.cs`):** An intuitive Windows Forms management window that allows administrators to toggle individual routines, alter schedule frequencies (Daily, Weekly, Monthly, Yearly), modify execution times, and commit changes with UAC elevation.
* **Auto-Start with Windows:** Easily toggled via the tray context menu, writing to the user's `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` registry hive.

---

## 4. Manual Engine Execution & Dry-Run Testing

Administrators can invoke any engine manually from an elevated PowerShell terminal for troubleshooting or ad-hoc compliance checks:

### 4.1. Sensitive Screenshot Purge (DLP)
```powershell
# Safe simulation (identifies screenshots without deleting files):
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-CleanupScreenshots.ps1 -AuditOnly

# Production execution (purges unmanaged screenshots across all profiles):
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-CleanupScreenshots.ps1
```

### 4.2. Temporary File & Cache Reclamation
```powershell
# Safe simulation (reports reclaimable bytes without deletion):
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-CleanupTempGlobal.ps1 -AuditOnly

# Production execution:
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-CleanupTempGlobal.ps1
```

### 4.3. Microsoft Defender Scan & Updates
```powershell
# Quick scan with automatic signature definition update:
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-DefenderScan.ps1 -ScanType 1

# Full system scan skipping signature update:
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-DefenderScan.ps1 -ScanType 2 -SkipSignatureUpdate
```

### 4.4. Windows Component Store Integrity Check (DISM)
```powershell
# Scan health check only:
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-IntegrityCheck.ps1

# Scan health with automated remediation if corruption is detected:
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-IntegrityCheck.ps1 -AutoRepair
```

### 4.5. BitLocker Cryptographic Compliance Audit
```powershell
# Audit all OS and fixed data volumes:
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-BitLockerAudit.ps1

# Audit a specific mount point:
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-BitLockerAudit.ps1 -TargetMountPoint "C:"
```

### 4.6. Security Event Log & Anti-Forensics Audit
```powershell
# Audit past 24 hours with default failed logon threshold (10):
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-EventAudit.ps1 -Hours 24

# Audit past 48 hours with custom failed logon threshold (5):
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-EventAudit.ps1 -Hours 48 -FailedLogonThreshold 5
```

### 4.7. Aged Downloads Relocation & Archival (ISO 27001 A.8.10)
```powershell
# Safe simulation (identifies aged downloads without moving files):
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-ArchiveDownloads.ps1 -AuditOnly

# Production execution (relocates files older than 30 days to Documents\Isora_Sentinel_Archived\Downloads):
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-ArchiveDownloads.ps1

# Custom aging threshold (60 days) with explicit destination path:
powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-ArchiveDownloads.ps1 -DaysOld 60 -CustomArchiveDir "D:\CorporateArchive\Downloads"
```

---

## 5. Windows Task Scheduler Management

Automated routines are registered under the root task folder `\Isora Sentinel\`.

### Inspecting Task Status via PowerShell:
```powershell
Get-ScheduledTask -TaskPath "\Isora Sentinel\" | 
    Select-Object TaskName, State, @{N='LastRun';E={($_ | Get-ScheduledTaskInfo).LastRunTime}}, @{N='LastResult';E={($_ | Get-ScheduledTaskInfo).LastTaskResult}} |
    Format-Table -AutoSize
```

### Triggering a Task Immediately via Command Line:
```powershell
# Must be executed from an elevated terminal:
schtasks.exe /Run /TN "Isora Sentinel\Defender_Scan"
schtasks.exe /Run /TN "Isora Sentinel\Integrity_Check"
```

---

## 6. Log Maintenance & SIEM Forwarding

Forensic logs are written to `C:\Program Files\Isora-Sentinel\logs\<EngineName>\*.log`.

* **Immutability:** Access Control Lists (ACLs) restrict write permissions to `BUILTIN\Administrators` and `NT AUTHORITY\SYSTEM`.
* **Telemetry Mode:** To stream events as RFC 8259 NDJSON for SIEM collectors (Splunk, Elastic, Datadog), execute:
  ```powershell
  [System.Environment]::SetEnvironmentVariable("SENTINEL_LOG_FORMAT", "JSON", "Machine")
  ```
* **Log Rotation:** Each run produces a uniquely timestamped log file (`<Engine>_YYYYMMDD_HHMMSS_fffffff.log`). Enterprise log forwarders should be configured to ship and purge rotated logs after ingestion.

---

## 7. Clean Uninstallation

### Via GUI (Windows Settings & Interactive Wizard):
Navigate to **Windows Settings > Apps > Installed Apps** (or *Control Panel > Programs and Features*), locate **Isora Sentinel Suite**, and click **Uninstall**.

This launches the native **Isora Sentinel Suite Uninstallation Wizard** (`Isora-Sentinel-Uninstall.exe`), featuring:
* **Interactive 3-Step Assistant:** *Revisar*, *Desinstalar*, *Resultado*.
* **Selective Data Retention:** Checkboxes to preserve compliance audit logs (`logs/`) and user-organized downloads (`Documents\Isora_Sentinel_Archived\`).
* **Technical Telemetry Log:** Real-time operation progress, collapsible technical details viewer, and one-click clipboard telemetry copy for enterprise IT tickets.
* **Safe Deferred Self-Deletion:** Cleans program executables and registry keys cleanly without locking running files or deleting retained audit data.

Alternatively, operators can launch the graphical uninstaller directly from the command line:
```powershell
.\Isora-Sentinel-Setup.exe --uninstall
```

### Via PowerShell CLI (Automated / RMM):
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Program Files\Isora-Sentinel\Uninstall-Sentinel.ps1"
```

This procedure cleanly removes all registered scheduled tasks, deletes registry keys, and uninstalls all application files while preserving historical logs for audit retention.
