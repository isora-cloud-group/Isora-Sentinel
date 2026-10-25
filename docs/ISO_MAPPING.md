# ISO Compliance Mapping

Sentinel-IT maps its automated routines directly to ISO/IEC 27001:2022 and ISO/IEC 20000-1 requirements.

## ISO 27001:2022 Mapping

| Engine Script                 | ISO Control | Control Objective                                   | Sentinel-IT Implementation                                    |
|-------------------------------|-------------|-----------------------------------------------------|---------------------------------------------------------------|
| `Invoke-CleanupScreenshots`   | **A.8.1**   | User endpoint devices (Data Leakage)                | Purges all local and OneDrive screenshot vectors to prevent DLP. |
| `Invoke-DefenderScan`         | **A.8.7**   | Protection against malware                          | Triggers mandatory Full Scans via `MpCmdRun.exe` bypassing UI. |
| `Invoke-IntegrityCheck`       | **A.8.20**  | Networks and system security (Integrity)            | Runs `DISM /ScanHealth` to detect Component Store drift/corruption. |

## ISO 20000-1 Mapping

| Engine Script                 | Process Area        | Control Objective                                   | Sentinel-IT Implementation                                    |
|-------------------------------|---------------------|-----------------------------------------------------|---------------------------------------------------------------|
| `Invoke-CleanupTempGlobal`    | Capacity Management | Ensure adequate capacity is available.              | Systematically clears `%TEMP%`, `WinSxS\Temp`, and User Caches. |
