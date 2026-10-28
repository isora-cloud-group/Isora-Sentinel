# Diagnostic & Troubleshooting Guide — Isora Sentinel Suite

**Commercial Brand & Distribution:** Isora Cloud Group  
**Sole Copyright Holder & Architect:** Sandra Gabriela Puerto Torres ([sandrapuerto.com](https://sandrapuerto.com) · [GitHub: @sandra-puerto](https://github.com/sandra-puerto) · [LinkedIn](https://www.linkedin.com/in/sandra-puerto/))  
**Software Version:** 2.3.0  

This guide provides technical root-cause analysis and operational remediation steps for common error scenarios, return codes, and system integration points across **Isora Sentinel**.

---

## Quick Symptom Index

1. [Error: Access Denied / Privilege Verification Failed](#1-error-access-denied--privilege-verification-failed)
2. [Task Scheduler Exit Codes Matrix](#2-task-scheduler-exit-codes-matrix)
3. [Windows UAC Elevation Nuances (Windows 10/11)](#3-windows-uac-elevation-nuances-windows-1011)
4. [OneDrive Files-on-Demand & Cloud Reparse Points](#4-onedrive-files-on-demand--cloud-reparse-points)
5. [DISM Component Store Exit Codes (Invoke-IntegrityCheck)](#5-dism-component-store-exit-codes-invoke-integritycheck)
6. [Microsoft Defender Exit Codes (Invoke-DefenderScan)](#6-microsoft-defender-exit-codes-invoke-defenderscan)
7. [PowerShell Execution Policy Restrictions](#7-powershell-execution-policy-restrictions)
8. [Historical: RemainingScripts Parameter Binding Error](#8-historical-remainingscripts-parameter-binding-error)

---

## 1. Error: Access Denied / Privilege Verification Failed

### Symptom
An engine script halts execution immediately, logging:
```text
[ERROR] SECURITY EXCEPTION: Administrative privileges required. Execution halted.
```

### Technical Root Cause
Administrative routines (DISM scans, Defender platform automation, multi-user temp purges, BitLocker queries) call `Assert-IsAdmin` upon entry to prevent unprivileged execution. If invoked within a standard user shell (Medium Integrity level) rather than an elevated token (High Integrity or SYSTEM), the security gate throws a terminating exception.

### Remediation
1. **Interactive Run:** Launch PowerShell with elevated privileges: Right-click **Windows PowerShell** > **Run as administrator**.
2. **Automated Run:** Ensure the scheduled task is configured with `/RL HIGHEST` and `/RU "NT AUTHORITY\SYSTEM"`.

---

## 2. Task Scheduler Exit Codes Matrix

When inspecting task history via `Get-ScheduledTaskInfo`:

| Return Code | Hex Representation | Meaning & Status | Resolution |
| :--- | :--- | :--- | :--- |
| `0` | `0x0` | **Success (Operation completed cleanly)** | No action required. |
| `1` | `0x1` | **PowerShell Script Encountered Errors** | Inspect the corresponding log file in `C:\Program Files\Isora-Sentinel\logs\<Engine>\`. |
| `267009` | `0x41301` | **Task is Currently Running** | Wait for completion; for long scans (DISM/Defender), this is expected. |
| `267011` | `0x41303` | **Task Did Not Run at Scheduled Time** | Check if machine was asleep, powered down, or if the user missed the maintenance window. |
| `2147942402` | `0x80070002` | **File Not Found** | Verify that `powershell.exe` or the engine `.ps1` path exists in `C:\Program Files\Isora-Sentinel\engines\`. |
| `2147942405` | `0x80070005` | **Access Denied** | Task lacks permission to execute as configured user; re-register using `NT AUTHORITY\SYSTEM`. |

---

## 3. Windows UAC Elevation Nuances (Windows 10/11)

### Symptom
When triggering an administrative module from `Isora-Sentinel-Tray.exe`, the UAC elevation prompt does not display, or the task fails silently.

### Technical Root Cause
1. **The `SW_HIDE` with `runas` Issue:** In Windows 10 (22H2) and Windows 11 (23H2/24H2), passing `ProcessWindowStyle.Hidden` (`SW_HIDE`) to `ShellExecuteEx` alongside `Verb = "runas"` suppresses the secure desktop credential prompt (`consent.exe`), causing Windows to block execution.
2. **Handle Duplication Restriction:** Setting `p.EnableRaisingEvents = true` on a standard-user process before calling `Process.Start()` with `Verb = "runas"` causes .NET to attempt to duplicate the handle with `SYNCHRONIZE` rights across integrity levels. Windows security policy blocks this, throwing `Win32Exception: 0x32 (The request is not supported)`.

### Remediation (Implemented in v2.2.0)
* In `SentinelTray.cs`, elevated commands set `psi.WindowStyle = ProcessWindowStyle.Normal` alongside `Verb = "runas"` to ensure the UAC consent desktop prompt appears reliably without suppression. Non-elevated execution relies on `CreateNoWindow = true`.
* Event monitoring is delegated to `ThreadPool.QueueUserWorkItem(delegate { p.WaitForExit(); })` without calling `EnableRaisingEvents`.

---

## 4. OneDrive Files-on-Demand & Cloud Reparse Points

### Symptom
Screenshot purging causes unwanted network downloads of archived files, or fails to delete files located in OneDrive folders.

### Technical Root Cause
OneDrive Files-on-Demand uses NTFS Reparse Points (`IO_REPARSE_TAG_FILE_PLACEHOLDER` / attribute `0x00000400`) and the `RecallOnDataAccess` attribute (`0x00400000`). Attempting to read or hash a cloud-only file forces Windows to download the entire file over the network.

### Remediation (Implemented in v2.2.0)
`Invoke-CleanupScreenshots.ps1` inspects file attributes before acting:
```powershell
$isReparse = [bool]($file.Attributes -band [System.IO.FileAttributes]::ReparsePoint)
$isOffline = [bool]($file.Attributes -band [System.IO.FileAttributes]::Offline)
if ($isReparse -or $isOffline) {
    # Skip cloud-only file to prevent unnecessary bandwidth consumption
    continue
}
```

---

## 5. DISM Component Store Exit Codes (`Invoke-IntegrityCheck`)

`Invoke-IntegrityCheck.ps1` invokes `dism.exe /Online /Cleanup-Image /ScanHealth`.

| Return Code | Meaning | Operational Diagnosis & Action |
| :--- | :--- | :--- |
| `0` | **Healthy** | No component store corruption detected. System image is healthy. |
| `20` | **Corrupted (Repairable)** | Corruption detected. Run `Invoke-IntegrityCheck.ps1 -AutoRepair` or `dism /Online /Cleanup-Image /RestoreHealth`. |
| `740` | **Elevation Required** | Process was invoked without Administrator privileges. Re-run from an elevated console. |
| `3010` | **Reboot Required** | Component store cleanup or repair requires a system restart to finalize pending file replacements. |

---

## 6. Microsoft Defender Exit Codes (`Invoke-DefenderScan`)

`Invoke-DefenderScan.ps1` invokes `MpCmdRun.exe`.

| Return Code | Meaning | Operational Action |
| :--- | :--- | :--- |
| `0` | **Scan Completed Cleanly** | No active threats detected on endpoint. |
| `2` | **Threat Detected / Quarantined** | Malware found and handled. Review Defender Quarantine via Windows Security app. |
| `-2147024894` | **Platform Path Not Found** | Microsoft Defender platform folder was updated. `Get-DefenderPlatformPath` dynamically handles versioned paths in `C:\ProgramData\Microsoft\Windows Defender\Platform\<Version>\`. |

---

## 7. PowerShell Execution Policy Restrictions

### Symptom
```text
File C:\Program Files\Isora-Sentinel\engines\Invoke-DefenderScan.ps1 cannot be loaded because running scripts is disabled on this system.
```

### Remediation
Always invoke Isora Sentinel routines using the `-ExecutionPolicy Bypass` switch:
```cmd
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Program Files\Isora-Sentinel\engines\Invoke-DefenderScan.ps1"
```
This bypasses per-user execution policy restrictions without permanently altering system-wide Group Policy.

---

## 8. Historical: `RemainingScripts` Parameter Binding Error

### Symptom (Pre-v2.2.0)
```text
[ERROR] CRITICAL FAILURE: Cannot bind parameter 'RemainingScripts'. 
Cannot convert the "-join" value of type "System.String" to type "System.Management.Automation.ScriptBlock".
```

### Technical Root Cause
In legacy versions, metadata formatting used a pipeline to `ForEach-Object` immediately followed by `-join`:
```powershell
# PROBLEMATIC LEGACY CODE
$MetaStr = ($SafeMetadata.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" } -join ", ")
```
In PowerShell 5.1, `ForEach-Object` has a secondary positional parameter named `-RemainingScripts`. The parser bound `-join` to `-RemainingScripts`, failing when trying to convert `"-join"` into a `ScriptBlock`.

### Remediation
Refactored to native, zero-overhead `foreach` iteration in `Sentinel-Logger.psm1`:
```powershell
# ROBUST NATIVE CODE (v2.2.0)
if ($SafeMetadata.Count -gt 0) {
    $pairs = foreach ($entry in $SafeMetadata.GetEnumerator()) {
        "$($entry.Key)=$($entry.Value)"
    }
    $MetaStr = " | " + ($pairs -join ", ")
}
```
All current deployments running v2.2.0+ are completely immune to this issue.
