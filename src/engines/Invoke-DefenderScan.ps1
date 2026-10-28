<#
.SYNOPSIS
    Endpoint Security Engine - ISO/IEC 27001:2022 Control A.8.7
.DESCRIPTION
    Triggers signature updates and automated antimalware scans via Microsoft Defender MpCmdRun.exe.
    Supports -ScanType 1 (Quick Scan) and 2 (Full Scan), and captures process exit codes for SIEM alerts.
#>
[CmdletBinding()]
param(
    [ValidateSet("1", "2")]
    [string]$ScanType = "2",
    [switch]$SkipSignatureUpdate
)

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Logger.psm1")
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Core.psm1")

Initialize-SentinelLog -EngineName "ISO_Defender_Scan"
try { Assert-SentinelIntegrity } catch { Write-AuditEntry "INTEGRITY WARNING: $($_.Exception.Message)" -Level "WARN" }

$ScanTypeName = if ($ScanType -eq "1") { "Quick Scan" } else { "Full Scan" }

Write-AuditEntry "=========================================================="
Write-AuditEntry "   [ISO_Defender_Scan] - SECURITY SCAN START"
Write-AuditEntry "   CONFIGURATION: ScanType=$ScanType ($ScanTypeName)"
Write-AuditEntry "=========================================================="

try {
    $IsAdmin = Test-IsAdmin
    if (-not $IsAdmin) {
        Write-AuditEntry "NOTICE: Standard user session - Antimalware scan running under user context." -Level "INFO" -Metadata @{ User = $env:USERNAME }
    }

    $DefenderPath = Get-DefenderPlatformPath
    Write-AuditEntry "ENGINE: Resolved Defender Platform Path." -Metadata @{ EnginePath = $DefenderPath }

    # 1. Signature Definitions Refresh (with offline network tolerance)
    if (-not $SkipSignatureUpdate) {
        Write-AuditEntry "UPDATE: Checking and updating antimalware signature definitions..."
        try {
            $updateOutput = & $DefenderPath -SignatureUpdate 2>&1 | Out-String
            $updateExitCode = $LASTEXITCODE
            $updateLevel = if ($updateExitCode -eq 0) { "SUCCESS" } else { "WARN" }
            Write-AuditEntry "UPDATE: Signature definition update finished." -Level $updateLevel -Metadata @{ 
                ExitCode = $updateExitCode 
            }
        } catch {
            Write-AuditEntry "UPDATE: Online signature update unavailable ($($_.Exception.Message)). Proceeding with cached definitions." -Level "WARN"
        }
    }

    # 2. Antimalware Scan Execution
    Write-AuditEntry "SCAN: Invoking Defender $ScanTypeName..." -Metadata @{ ScanType = $ScanType }
    $ScanProcess = & $DefenderPath -Scan -ScanType ([int]$ScanType) 2>&1 | Out-String
    $scanExitCode = $LASTEXITCODE

    if ($ScanProcess) {
        $lines = $ScanProcess -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        foreach ($line in $lines) {
            if ($line -match "Error|Fail|threat|Threat") {
                Write-AuditEntry $line.Trim() -Level "WARN"
            } else {
                Write-AuditEntry $line.Trim() -Level "INFO"
            }
        }
    }

    # Evaluate MpCmdRun exit code:
    # 0 = No threats detected, 2 = Threats detected and remediated
    if ($scanExitCode -eq 0) {
        Write-AuditEntry "RESULT: Antimalware scan concluded. No threats detected." -Level "SUCCESS" -Metadata @{ 
            ExitCode = $scanExitCode; 
            ScanType = $ScanTypeName 
        }
    } elseif ($scanExitCode -eq 2) {
        Write-AuditEntry "RESULT: Threat detected and remediation invoked by Defender engine." -Level "ERROR" -Metadata @{ 
            ExitCode = $scanExitCode; 
            ScanType = $ScanTypeName;
            Alert    = "ActiveThreatDetected"
        }
    } else {
        Write-AuditEntry "RESULT: Scan ended with unexpected status code." -Level "WARN" -Metadata @{ 
            ExitCode = $scanExitCode; 
            ScanType = $ScanTypeName 
        }
    }
} catch {
    Write-AuditEntry "CRITICAL EXCEPTION: $($_.Exception.Message)" -Level "ERROR"
}

Write-AuditEntry "=========================================================="
