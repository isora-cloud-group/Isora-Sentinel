<#
.SYNOPSIS
    System Integrity Audit Engine - ISO/IEC 27001:2022 Control A.8.20
.DESCRIPTION
    Audits the Windows Component Store (WinSxS) integrity via DISM.exe /ScanHealth.
    Captures DISM return codes (0 = Healthy, 20 = Repairable Corruption, 3010 = Reboot Needed)
    and supports optional -AutoRepair parameter to execute /RestoreHealth.
#>
[CmdletBinding()]
param(
    [switch]$AutoRepair
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Logger.psm1")
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Core.psm1")

Initialize-SentinelLog -EngineName "ISO_Integrity_Check"

Write-AuditEntry "=========================================================="
Write-AuditEntry "   [ISO_Integrity_Check] - SYSTEM INTEGRITY AUDIT START"
if ($AutoRepair) {
    Write-AuditEntry "   CONFIGURATION: AutoRepair enabled (will dispatch /RestoreHealth if needed)" -Level "WARN"
}
Write-AuditEntry "=========================================================="

try {
    Assert-IsAdmin
    Write-AuditEntry "AUDIT: Dispatching DISM online scan health event..."
    $dismResult = & dism.exe /Online /Cleanup-Image /ScanHealth 2>&1 | Out-String
    $dismCode = $LASTEXITCODE

    if ($dismResult) {
        $lines = $dismResult -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        foreach ($line in $lines) {
            Write-AuditEntry $line.Trim() -Level "INFO"
        }
    }

    # Evaluate DISM standard exit codes:
    # 0 = No corruption, 20 = Corruption detected and repairable, 3010 = Reboot required
    switch ($dismCode) {
        0 {
            Write-AuditEntry "RESULT: Component Store is verified healthy. No corruption detected." -Level "SUCCESS" -Metadata @{ 
                ExitCode = 0; 
                HealthStatus = "Healthy" 
            }
        }
        20 {
            Write-AuditEntry "RESULT: Component Store corruption detected (repairable)." -Level "WARN" -Metadata @{ 
                ExitCode = 20; 
                HealthStatus = "CorruptedRepairable" 
            }

            if ($AutoRepair) {
                Write-AuditEntry "REMEDIATION: AutoRepair invoked. Dispatching DISM /RestoreHealth..." -Level "WARN"
                $restoreResult = & dism.exe /Online /Cleanup-Image /RestoreHealth 2>&1 | Out-String
                $restoreCode = $LASTEXITCODE
                $restoreLevel = if ($restoreCode -eq 0) { "SUCCESS" } else { "ERROR" }
                Write-AuditEntry "REMEDIATION: RestoreHealth concluded." -Level $restoreLevel -Metadata @{ 
                    ExitCode = $restoreCode 
                }
            }
        }
        3010 {
            Write-AuditEntry "RESULT: Component Store scan requires system restart to finalize operations." -Level "WARN" -Metadata @{ 
                ExitCode = 3010; 
                HealthStatus = "RebootRequired" 
            }
        }
        default {
            Write-AuditEntry "RESULT: DISM operation ended with non-zero exit code." -Level "ERROR" -Metadata @{ 
                ExitCode = $dismCode; 
                HealthStatus = "UnknownFailure" 
            }
        }
    }
} catch {
    Write-AuditEntry "AUDIT FAILURE: DISM engine exception: $($_.Exception.Message)" -Level "ERROR"
}

Write-AuditEntry "=========================================================="
