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
    $IsAdmin = Test-IsAdmin
    if (-not $IsAdmin) {
        Write-AuditEntry "NOTICE: Security Event Log analysis typically requires Administrator or SYSTEM privileges. Attempting read-only query..." -Level "WARN" -Metadata @{ User = $env:USERNAME }
    }

    $StartTime = (Get-Date).AddHours(-$Hours)
    $AnomaliesDetected = 0
    $AccessDeniedDetected = $false

    # 1. Event 1102: Audit Log Cleared (High Severity Anti-Forensics Flag)
    $ClearLogEvents = $null
    try {
        $ClearLogEvents = Get-WinEvent -FilterHashtable @{
            LogName   = "Security"
            Id        = 1102
            StartTime = $StartTime
        } -ErrorAction Stop
    } catch {
        if ($_.FullyQualifiedErrorId -match "NoMatchingEventsFound" -or $_.Exception.Message -match "No events were found|No se encontraron eventos") {
            # 0 events is the healthy expected baseline
            $ClearLogEvents = @()
        } elseif ($_.Exception -is [System.UnauthorizedAccessException] -or $_.Exception.Message -match "Access is denied|Acceso denegado|authorized") {
            $AccessDeniedDetected = $true
            Write-AuditEntry "AUDIT PERMISSION NOTICE: Access denied querying Event 1102 in Security Event Log (requires Administrator/SYSTEM)." -Level "WARN"
        } else {
            Write-AuditEntry "AUDIT: Could not query Event 1102: $($_.Exception.Message)" -Level "WARN"
        }
    }

    if ($ClearLogEvents -and $ClearLogEvents.Count -gt 0) {
        $AnomaliesDetected += $ClearLogEvents.Count
        foreach ($ev in $ClearLogEvents) {
            Write-AuditEntry "CRITICAL ANTI-FORENSICS ALERT: Security Audit Log was cleared." -Level "ERROR" -Metadata @{
                EventID   = 1102
                Timestamp = $ev.TimeCreated.ToString("yyyy-MM-dd HH:mm:ss")
                User      = $ev.UserId
            }
        }
    } elseif (-not $AccessDeniedDetected) {
        Write-AuditEntry "AUDIT: No audit log clearance events detected in window." -Level "SUCCESS"
    }

    # 2. Event 4625: Failed Logon Attempts (Brute Force Monitoring)
    $FailedLogons = $null
    try {
        $FailedLogons = Get-WinEvent -FilterHashtable @{
            LogName   = "Security"
            Id        = 4625
            StartTime = $StartTime
        } -ErrorAction Stop
    } catch {
        if ($_.FullyQualifiedErrorId -match "NoMatchingEventsFound" -or $_.Exception.Message -match "No events were found|No se encontraron eventos") {
            $FailedLogons = @()
        } elseif ($_.Exception -is [System.UnauthorizedAccessException] -or $_.Exception.Message -match "Access is denied|Acceso denegado|authorized") {
            $AccessDeniedDetected = $true
            Write-AuditEntry "AUDIT PERMISSION NOTICE: Access denied querying Event 4625 in Security Event Log (requires Administrator/SYSTEM)." -Level "WARN"
        } else {
            Write-AuditEntry "AUDIT: Could not query Event 4625: $($_.Exception.Message)" -Level "WARN"
        }
    }

    $FailCount = if ($FailedLogons) { $FailedLogons.Count } else { 0 }

    if ($FailCount -ge $FailedLogonThreshold) {
        $AnomaliesDetected += $FailCount
        Write-AuditEntry "BRUTE-FORCE ALERT: Elevated failed logon count exceeds threshold." -Level "WARN" -Metadata @{
            EventID   = 4625
            Count     = $FailCount
            Threshold = $FailedLogonThreshold
        }
    } elseif (-not $AccessDeniedDetected) {
        Write-AuditEntry "AUDIT: Failed logon count within baseline threshold." -Level "INFO" -Metadata @{
            FailedLogonCount = $FailCount
        }
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
        # Expected if no events exist in filter window or permission restricted
    }

    $summaryLevel = if ($AccessDeniedDetected) { "WARN" } elseif ($AnomaliesDetected -eq 0) { "SUCCESS" } else { "WARN" }
    Write-AuditEntry "RESULT: Security Event Log audit concluded." -Level $summaryLevel -Metadata @{
        WindowHours          = $Hours
        AnomaliesDetected    = $AnomaliesDetected
        AccessDeniedDetected = $AccessDeniedDetected
        AuditStandard        = "ISO/IEC 27001:2022 A.8.15"
    }
} catch {
    Write-AuditEntry "CRITICAL FAILURE: $($_.Exception.Message)" -Level "ERROR"
}

Write-AuditEntry "=========================================================="
