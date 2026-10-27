<#
.SYNOPSIS
    Security Event & Anti-Forensics Audit Engine - ISO/IEC 27001:2022 Control A.8.15
.DESCRIPTION
    Audits the Windows Security Event Log for critical indicators of compromise:
    - Event 1102: Audit log clearing (anti-forensics technique)
    - Event 4625: Failed authentication spikes (credential brute-forcing)
    - Event 4720 / 4726: Account creation or deletion anomalies
    Emits structured SIEM forensic events for continuous compliance monitoring.
#>
[CmdletBinding()]
param(
    [int]$Hours = 24,
    [int]$FailedLogonThreshold = 10
)

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Logger.psm1")
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Core.psm1")

Initialize-SentinelLog -EngineName "ISO_Event_Audit"

Write-AuditEntry "=========================================================="
Write-AuditEntry "   [ISO_Event_Audit] - SECURITY EVENT LOG AUDIT START"
Write-AuditEntry "   WINDOW: Past $Hours hour(s) | FailedLogonThreshold: $FailedLogonThreshold"
Write-AuditEntry "=========================================================="

try {
    Assert-IsAdmin

    $StartTime = (Get-Date).AddHours(-$Hours)
    $AnomaliesDetected = 0

    # 1. Event 1102: Audit Log Cleared (High Severity Anti-Forensics Flag)
    try {
        $ClearLogEvents = Get-WinEvent -FilterHashtable @{
            LogName   = "Security"
            Id        = 1102
            StartTime = $StartTime
        } -ErrorAction SilentlyContinue

        if ($ClearLogEvents) {
            $AnomaliesDetected += $ClearLogEvents.Count
            foreach ($ev in $ClearLogEvents) {
                Write-AuditEntry "CRITICAL ANTI-FORENSICS ALERT: Security Audit Log was cleared." -Level "ERROR" -Metadata @{
                    EventID   = 1102
                    Timestamp = $ev.TimeCreated.ToString("yyyy-MM-dd HH:mm:ss")
                    User      = $ev.UserId
                }
            }
        } else {
            Write-AuditEntry "AUDIT: No audit log clearance events detected in window." -Level "SUCCESS"
        }
    } catch {
        Write-AuditEntry "AUDIT: Could not query Event 1102: $($_.Exception.Message)" -Level "WARN"
    }

    # 2. Event 4625: Failed Logon Attempts (Brute Force Monitoring)
    try {
        $FailedLogons = Get-WinEvent -FilterHashtable @{
            LogName   = "Security"
            Id        = 4625
            StartTime = $StartTime
        } -ErrorAction SilentlyContinue

        $FailCount = if ($FailedLogons) { $FailedLogons.Count } else { 0 }

        if ($FailCount -ge $FailedLogonThreshold) {
            $AnomaliesDetected += $FailCount
            Write-AuditEntry "BRUTE-FORCE ALERT: Elevated failed logon count exceeds threshold." -Level "WARN" -Metadata @{
                EventID   = 4625
                Count     = $FailCount
                Threshold = $FailedLogonThreshold
            }
        } else {
            Write-AuditEntry "AUDIT: Failed logon count within baseline threshold." -Level "INFO" -Metadata @{
                FailedLogonCount = $FailCount
            }
        }
    } catch {
        Write-AuditEntry "AUDIT: Could not query Event 4625: $($_.Exception.Message)" -Level "WARN"
    }

    # 3. Event 4720 & 4726: Account Provisioning & Deletion Auditing
    try {
        $AccountChanges = Get-WinEvent -FilterHashtable @{
            LogName   = "Security"
            Id        = @(4720, 4726)
            StartTime = $StartTime
        } -ErrorAction SilentlyContinue

        if ($AccountChanges) {
            foreach ($ev in $AccountChanges) {
                $eventType = if ($ev.Id -eq 4720) { "AccountCreated" } else { "AccountDeleted" }
                Write-AuditEntry "ACCOUNT ANOMALY AUDIT: $eventType detected." -Level "INFO" -Metadata @{
                    EventID   = $ev.Id
                    EventType = $eventType
                    Timestamp = $ev.TimeCreated.ToString("yyyy-MM-dd HH:mm:ss")
                }
            }
        }
    } catch {
        # Expected if no events exist in filter window
    }

    $summaryLevel = if ($AnomaliesDetected -eq 0) { "SUCCESS" } else { "WARN" }
    Write-AuditEntry "RESULT: Security Event Log audit concluded." -Level $summaryLevel -Metadata @{
        WindowHours       = $Hours
        AnomaliesDetected = $AnomaliesDetected
        AuditStandard     = "ISO/IEC 27001:2022 A.8.15"
    }
} catch {
    Write-AuditEntry "CRITICAL FAILURE: $($_.Exception.Message)" -Level "ERROR"
}

Write-AuditEntry "=========================================================="
