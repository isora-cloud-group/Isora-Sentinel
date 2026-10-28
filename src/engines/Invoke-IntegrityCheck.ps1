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
    [switch]$AutoRepair,
    [switch]$QuickCheck
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Logger.psm1")
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Core.psm1")

Initialize-SentinelLog -EngineName "ISO_Integrity_Check"
try { Assert-SentinelIntegrity } catch { Write-AuditEntry "INTEGRITY WARNING: $($_.Exception.Message)" -Level "WARN" }

Write-AuditEntry "=========================================================="
Write-AuditEntry "   [ISO_Integrity_Check] - SYSTEM INTEGRITY AUDIT START"
if ($AutoRepair) {
    Write-AuditEntry "   CONFIGURATION: AutoRepair enabled (will dispatch /RestoreHealth if needed)" -Level "WARN"
}
Write-AuditEntry "=========================================================="

try {
    $IsAdmin = Test-IsAdmin
    if (-not $IsAdmin) {
        Write-AuditEntry "AUDIT NOTICE: Component Store integrity scan via DISM requires Administrator or SYSTEM privileges. Execution skipped in standard user context." -Level "WARN" -Metadata @{ User = $env:USERNAME }
        return
    }

    $sysRoot = if ($env:SystemRoot) { $env:SystemRoot } else { "C:\WINDOWS" }
    $dismPath = Join-Path $sysRoot "System32\dism.exe"
    if (-not (Test-Path -LiteralPath $dismPath)) {
        $sysnativeDism = Join-Path $sysRoot "Sysnative\dism.exe"
        if (Test-Path -LiteralPath $sysnativeDism) {
            $dismPath = $sysnativeDism
        } else {
            $dismPath = "dism.exe"
        }
    }

    Write-AuditEntry "AUDIT: Dispatching DISM online scan health event..." -Metadata @{ DismPath = $dismPath }
    $dismArg = if ($QuickCheck) { "/CheckHealth" } else { "/ScanHealth" }
    Write-AuditEntry "AUDIT: Dispatching DISM online scan mode: $dismArg..."
    $dismResult = & $dismPath /Online /Cleanup-Image $dismArg 2>&1 | Out-String
    $dismCode = $LASTEXITCODE

    if ($dismResult) {
        $lines = $dismResult -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        foreach ($line in $lines) {
            Write-AuditEntry $line.Trim() -Level "INFO"
        }
    }

    # Evaluate DISM standard exit codes:
    # 0 = No corruption, 20 = Corruption detected and repairable, 740 = Elevation required, 3010 = Reboot required
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
                $restoreResult = & $dismPath /Online /Cleanup-Image /RestoreHealth 2>&1 | Out-String
                $restoreCode = $LASTEXITCODE
                $restoreLevel = if ($restoreCode -eq 0) { "SUCCESS" } else { "ERROR" }
                Write-AuditEntry "REMEDIATION: RestoreHealth concluded." -Level $restoreLevel -Metadata @{ 
                    ExitCode = $restoreCode 
                }
            }
        }
        740 {
            Write-AuditEntry "RESULT: DISM operation failed because administrative elevation is required (Exit code 740)." -Level "WARN" -Metadata @{ 
                ExitCode = 740; 
                HealthStatus = "ElevationRequired" 
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
