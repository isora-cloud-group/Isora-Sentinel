# System Architecture Specification — Sentinel-IT Suite

**Commercial Brand & Distribution:** Isora Cloud Group  
**Sole Copyright Holder & Architect:** Sandra Gabriela Puerto Torres ([sandrapuerto.com](https://sandrapuerto.com) · [GitHub: @sandra-puerto](https://github.com/sandra-puerto) · [LinkedIn](https://www.linkedin.com/in/sandra-puerto/))  
**Agentic AI Implementation:** Google Antigravity AI Engine  
**Document Version:** 2.2.0  
**Architectural Taxonomy:** Modular Hub-and-Spoke, Zero External Dependencies, Sovereign Local Execution  

---

## 1. Architectural Vision & Core Tenets

**Sentinel-IT** was engineered from inception to solve a major challenge in corporate Windows infrastructure management: the reliance on bloated, proprietary third-party endpoint agents that introduce unmonitored attack surfaces, consume significant CPU/RAM memory, and operate with opaque, vendor-locked telemetry.

The platform is designed upon four uncompromising architectural tenets:

1. **Zero External Dependencies:** Native to Windows PowerShell 5.1 and PowerShell Core 7+. Requires zero external package managers (NuGet, Chocolatey, Scoop), zero non-standard runtimes (Python, Node.js, Ruby), and zero unverified compiled DLLs.
2. **Deterministic Hub-and-Spoke Topology:** Decouples shared governance, cryptographic validation, and telemetry formatting into a central core library (`src/lib/`), while isolating operational maintenance routines into independent atomic spokes (`src/engines/`).
3. **Defense-in-Depth Privilege Separation:** High-privilege operating system tasks run under `NT AUTHORITY\SYSTEM` without interactive UAC prompts, while user-centric data operations (such as personal screenshot purges) can execute within the active user session token.
4. **Forensic Immutability & SIEM Interoperability:** Every routine generates deterministic, RFC 8259-compliant NDJSON telemetry with microsecond timestamps, operating system identity, PID, and structured metadata suitable for direct ingestion into Splunk, Elastic, Datadog, or Microsoft Sentinel.

---

## 2. Global Architecture & Data Flow

```mermaid
flowchart TD
    subgraph Schedulers["Execution & Orchestration Layer"]
        direction TB
        TaskSched["Windows Task Scheduler (Kernel Engine)\n• RunLevel: Highest (NT AUTHORITY\\SYSTEM)\n• Triggers: Daily 03:00 / Weekly Sun 02:00"]
        Tray["System Tray Monitor (Sentinel-IT-Tray.exe)\n• Single-Instance Process (Local\\SentinelIT.TrayMonitor)\n• Real-Time Integrity Status (SHA-256)\n• On-Demand UAC Elevation Dispatcher"]
        ConfigForm["TaskConfigForm (Task Configurator GUI)\n• Direct Task Scheduler XML Query / Update\n• Interactive ToggleSwitches & Schedule Controls"]
    end

    subgraph SecurityBoundary["Privilege & Integrity Security Boundary"]
        direction TB
        AssertAdmin["Assert-IsAdmin\nVerifies Administrator / SYSTEM Token"]
        AssertIntegrity["Assert-SentinelIntegrity\nValidates SHA-256 Manifest Hashes"]
        Manifest["integrity.manifest.json\nCryptographic Fingerprints"]
    end

    subgraph CentralHub["Shared Hub Modules (src/lib/)"]
        direction TB
        Core["Sentinel-Core.psm1\n• Platform Path Discovery (Defender, DISM)\n• Privilege & Integrity Enforcement"]
        Logger["Sentinel-Logger.psm1\n• Dual Format Engine (Human Text & NDJSON)\n• Precise Microsecond Timestamps\n• Safe Metadata Serialization"]
    end

    subgraph ExecutionSpokes["Atomic Execution Engines (src/engines/)"]
        direction TB
        E1["Invoke-CleanupScreenshots.ps1\nISO 27001 A.8.1 (DLP Screenshot Purge)"]
        E2["Invoke-CleanupTempGlobal.ps1\nISO 20000-1 §8.2 (Temp & Cache Reclamation)"]
        E3["Invoke-DefenderScan.ps1\nISO 27001 A.8.7 (Antimalware Scan & Updates)"]
        E4["Invoke-IntegrityCheck.ps1\nISO 27001 A.8.20 (DISM Component Store Audit)"]
        E5["Invoke-BitLockerAudit.ps1\nISO 27001 A.8.24 (Cryptographic Encryption Audit)"]
        E6["Invoke-EventAudit.ps1\nISO 27001 A.8.15 (Security Event Log Anti-Forensics)"]
    end

    subgraph LocalStorage["Forensic Telemetry Store"]
        direction TB
        LogsDir["C:\\Program Files\\Sentinel-IT\\logs\\\n• Restricted ACLs (Administrators / SYSTEM)\n• Engine-Dedicated Subdirectories"]
        NDJSONStream["NDJSON Real-Time Telemetry Stream\n(RFC 8259 Standardized Fields)"]
    end

    subgraph SIEMLayer["Enterprise Telemetry & SIEM Ingestion"]
        Splunk["Splunk Universal Forwarder"]
        Elastic["Elastic Filebeat Agent"]
        Datadog["Datadog Log Agent"]
        Sentinel["Microsoft Sentinel (Azure Monitor Agent)"]
    end

    Schedulers --> SecurityBoundary
    SecurityBoundary --> CentralHub
    CentralHub --> ExecutionSpokes
    ExecutionSpokes --> LocalStorage
    LocalStorage --> SIEMLayer
```

---

## 3. Privilege Model & Security Boundaries

### 3.1. `NT AUTHORITY\SYSTEM` Execution Context
System-wide hygiene operations (purging multi-user temporary files, auditing the Windows Component Store via DISM, querying BitLocker volume encryption metadata across locked volumes, and reading the Windows Security Event log) require elevated operating system privileges.

1. **Scheduled Task Identity:** Automated routines are registered in Windows Task Scheduler under `NT AUTHORITY\SYSTEM` with `RunLevel = Highest`. This enables fully unattended, background execution without interactive UAC dialogs.
2. **Explicit Privilege Assertion:** Every engine executes `Assert-IsAdmin` upon entry. If the caller does not hold the Local Administrator or SYSTEM security identifier, the script terminates immediately with a non-zero exit code, preventing unauthorized standard users from invoking administrative logic.
3. **Cryptographic Manifest Verification:** Prior to performing actions, `Assert-SentinelIntegrity` recalculates the SHA-256 hash of all libraries and engines and compares them against `integrity.manifest.json`.

### 3.2. Local Privilege Escalation (LPE) Defense
Because scripts run with `SYSTEM` privileges, the installation directory (`C:\Program Files\Sentinel-IT\`) is strictly protected against binary hijacking and script poisoning:

```mermaid
flowchart LR
    A["C:\\Program Files\\Sentinel-IT\\"] --> B["BUILTIN\\Administrators\n(Full Control: Read, Write, Execute, Delete)"]
    A --> C["NT AUTHORITY\\SYSTEM\n(Full Control: Read, Write, Execute, Delete)"]
    A --> D["BUILTIN\\Users\n(Read & Execute Only — Write Denied)"]
```

Standard users cannot alter script content, overwrite manifest files, or plant rogue modules to gain elevated privileges.

---

## 4. System Tray Observability & Elevation Architecture

Sentinel-IT includes a lightweight, unmanaged C# Windows Forms utility (`Sentinel-IT-Tray.exe`) that executes in the user's interactive desktop session:

```mermaid
sequenceDiagram
    autonumber
    actor User as Operator / Administrator
    participant Tray as Sentinel-IT-Tray.exe (asInvoker)
    participant Config as TaskConfigForm
    participant UAC as Windows UAC (consent.exe)
    participant Sched as Task Scheduler (SYSTEM)

    User->>Tray: Left-Click / Open Context Menu
    Tray->>Tray: Run Background SHA-256 Integrity Check
    alt Integrity Verified
        Tray-->>User: Green Status Badge (Authorized)
    else File Tampered
        Tray-->>User: Amber Status Badge (Tampering Detected)
    end

    User->>Tray: Select "DISM Component Store Audit" (Requires Admin)
    Tray->>User: Display Confirmation Dialog ("Elevation required. Proceed?")
    User->>Tray: Confirm (Yes)
    Tray->>UAC: ProcessStartInfo (Verb='runas', WindowStyle=Normal)
    UAC-->>User: Display Secure Desktop Credential Prompt
    User->>UAC: Consent / Provide Credentials
    UAC->>Sched: Dispatch schtasks.exe /Run /TN 'Sentinel-IT\Integrity_Check'
    Sched-->>Tray: Return Process Exit Code
    Tray-->>User: Display Notification Balloon ("Task completed successfully")
```

### Technical Highlights of `Sentinel-IT-Tray.exe`:
* **High-DPI Awareness:** Uses `SetProcessDPIAware()` and dynamic layout scaling based on screen PPI (96 to 288 DPI), rendering crisp vector glyphs and typography on 4K displays.
* **Non-Blocking Elevation:** Starts elevated tasks using `ProcessStartInfo` with `Verb = "runas"` and `WindowStyle = ProcessWindowStyle.Normal`. PowerShell hides itself internally with `-WindowStyle Hidden`, avoiding the Windows 10/11 UAC suppression bug caused by `SW_HIDE`.
* **Asynchronous Thread Pool Waiting:** Eliminates cross-integrity `EnableRaisingEvents` handle duplication crashes (`Win32Exception: 0x32`) by awaiting process completion via `ThreadPool.QueueUserWorkItem(delegate { p.WaitForExit(); })`.
* **TaskConfigForm GUI (`TaskConfigForm.cs`):** Directly queries Windows Task Scheduler XML definitions via `schtasks.exe /Query /XML`, parsing frequencies (Daily, Weekly, Monthly), day-of-week selections, and start boundaries without external management dependencies.

---

## 5. Multi-User Discovery & Cloud Storage Protection

The screenshot purging engine (`Invoke-CleanupScreenshots.ps1`) implements intelligent multi-profile discovery:

```mermaid
flowchart TD
    Start["Invoke-CleanupScreenshots.ps1"] --> Discover["Query HKLM ProfileList & C:\\Users\\*"]
    Discover --> LoopProfiles["Iterate Through Each User Profile"]
    LoopProfiles --> CheckRegistry["Query HKCU & User Hive for KnownFolder Screenshot Path"]
    CheckRegistry --> CheckOneDrive["Scan OneDrive & OneDrive Commercial Paths"]
    CheckOneDrive --> FilterReparse["Evaluate File Attributes\n• Check for ReparsePoint (0x00000400)\n• Check for Offline/RecallOnDataAccess (0x00400000)"]
    FilterReparse -->|Cloud-Only Stub| Skip["Skip Cloud-Only File\n(Prevents Forced Bandwidth Download)"]
    FilterReparse -->|Locally Pinned / On-Disk| Purge["Secure Deletion via Remove-Item"]
    Purge --> LogEntry["Write Forensic Metadata\n(ProfileCount, PurgedCount, BytesReclaimed)"]
```

---

## 6. Dual-Format Forensic Telemetry Engine

The logging engine (`Sentinel-Logger.psm1`) guarantees deterministic audit logging across heterogeneous environments:

1. **Human-Readable Text Format (`TEXT`):**
   ```text
   [2026-10-28 01:05:42.1284591] [SUCCESS] [PID:15024] [NT AUTHORITY\SYSTEM] VOLUME COMPLIANT: C: is fully encrypted | MountPoint=C:, ProtectionStatus=On, EncryptionPct=100
   ```
2. **RFC 8259 Standardized JSON Format (`JSON`):**
   ```json
   {
     "Timestamp": "2026-10-28 01:05:42.1284591",
     "Level": "SUCCESS",
     "Host": "DESKTOP-FIN01",
     "Identity": "NT AUTHORITY\\SYSTEM",
     "PID": 15024,
     "Message": "VOLUME COMPLIANT: C: is fully encrypted with active hardware protection.",
     "Metadata": {
       "MountPoint": "C:",
       "ProtectionStatus": "On",
       "VolumeStatus": "FullyEncrypted",
       "EncryptionPct": "100",
       "KeyProtectors": "Tpm;RecoveryPassword",
       "AuditStandard": "ISO/IEC 27001:2022 Control A.8.24"
     }
   }
   ```

Toggled transparently across all fleet endpoints via the machine-level environment variable:
```powershell
[System.Environment]::SetEnvironmentVariable("SENTINEL_LOG_FORMAT", "JSON", "Machine")
```

---

## 7. Failure Handling & Self-Healing Matrix

| Component | Failure Condition | Architectural Defense & Recovery Behavior |
| :--- | :--- | :--- |
| **`Assert-SentinelIntegrity`** | SHA-256 hash mismatch in any file | Immediate terminal exception; halts before running high-privilege code; writes forensic security event. |
| **`Invoke-DefenderScan`** | Signature update server unreachable (offline endpoint) | Network timeout caught gracefully; logs `WARN` and proceeds with cached antimalware definitions without failing. |
| **`Invoke-IntegrityCheck`** | Component Store corruption detected (DISM exit code 20) | Emits `WARN` alert to SIEM; if `-AutoRepair` is enabled, triggers `/RestoreHealth` automatically. |
| **`Invoke-BitLockerAudit`** | Unencrypted volume or protection suspended | Flags volume with `WARN` severity; records unencrypted volume status for immediate SOC alerting. |
| **`Invoke-EventAudit`** | Security Event Log cleared (Event 1102) | Emits immediate `ERROR` alert with actor identity and timestamp indicating potential anti-forensics evasion. |
| **`Sentinel-IT-Tray`** | Scheduled task not yet registered in Task Scheduler | Detects missing task and falls back seamlessly to direct elevated PowerShell script execution. |
