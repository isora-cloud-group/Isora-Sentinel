---
name: Bug Report
about: Report a defect, exception, or unexpected behavior in Sentinel-IT
title: '[BUG] <Short description>'
labels: ['bug', 'triage']
assignees: ''
---

## Description
A clear and concise description of the bug or unexpected behavior.

## Environment Information
- **OS Version:** Windows 10 / Windows 11 / Windows Server (specify build, e.g. 22H2 / 23H2)
- **PowerShell Version:** `$PSVersionTable.PSVersion` (e.g. 5.1.22621 or 7.4.x)
- **Sentinel-IT Version:** (e.g. v2.2.0)
- **Execution Mode:** Manual Elevated / Scheduled Task (`NT AUTHORITY\SYSTEM`)
- **Log Format:** TEXT / JSON

## Script / Engine Affected
- [ ] `src/lib/Sentinel-Core.psm1`
- [ ] `src/lib/Sentinel-Logger.psm1`
- [ ] `src/engines/Invoke-CleanupScreenshots.ps1`
- [ ] `src/engines/Invoke-CleanupTempGlobal.ps1`
- [ ] `src/engines/Invoke-DefenderScan.ps1`
- [ ] `src/engines/Invoke-IntegrityCheck.ps1`
- [ ] `src/engines/Invoke-BitLockerAudit.ps1`
- [ ] `src/engines/Invoke-EventAudit.ps1`
- [ ] `installer/Install-Sentinel.ps1`
- [ ] `installer/Uninstall-Sentinel.ps1`

## Steps to Reproduce
1. Execute command: `powershell.exe -ExecutionPolicy Bypass -File ...`
2. Observe error or log output.

## Expected Behavior
A clear and concise description of what you expected to happen.

## Actual Behavior / Error Logs
```text
Paste log output or stack trace here from C:\Program Files\IT_Maintenance\logs\
```

## Additional Context
Add any other context, GPO settings, or third-party EDR software in place (e.g., CrowdStrike, Defender, SentinelOne).
