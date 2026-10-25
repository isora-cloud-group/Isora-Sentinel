<#
.SYNOPSIS
    Endpoint Security Engine - ISO 27001:2022 Control A.8.7
#>
$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Logger.psm1")
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Core.psm1")

Initialize-SentinelLog -EngineName "ISO_Defender_Scan"

Write-AuditEntry "=========================================================="
Write-AuditEntry "   [ISO_Defender_Scan] - SECURITY SCAN START"
Write-AuditEntry "=========================================================="

try {
    Assert-IsAdmin
    $DefenderPath = Get-DefenderPlatformPath
    Write-AuditEntry "ENGINE: Invoking Defender for Full Malware Analysis." -Metadata @{ EnginePath = $DefenderPath }
    
    $ScanProcess = & $DefenderPath -Scan -ScanType 2 | Out-String
    $ScanProcess | ForEach-Object {
        if ($_ -match "Error|Fail|threat") { Write-AuditEntry $_ -Level "WARN" }
        else { Write-AuditEntry $_ -Level "INFO" }
    }
    Write-AuditEntry "RESULT: Antimalware scan operation concluded." -Level "SUCCESS"
} catch {
    Write-AuditEntry "CRITICAL EXCEPTION: $($_.Exception.Message)" -Level "ERROR"
}

Write-AuditEntry "=========================================================="
