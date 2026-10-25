<#
.SYNOPSIS
    System Integrity Audit Engine - ISO 27001:2022 A.8.20
#>
$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Logger.psm1")
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Core.psm1")

Initialize-SentinelLog -EngineName "ISO_Integrity_Check"

Write-AuditEntry "=========================================================="
Write-AuditEntry "   [ISO_Integrity_Check] - SYSTEM INTEGRITY AUDIT START"
Write-AuditEntry "=========================================================="

try {
    Assert-IsAdmin
    Write-AuditEntry "AUDIT: Dispatching DISM online scan health event..."
    $dismResult = & dism.exe /Online /Cleanup-Image /ScanHealth | Out-String
    $dismResult | ForEach-Object { Write-AuditEntry $_ -Level "INFO" }
    Write-AuditEntry "STATUS: Integrity report generated successfully." -Level "SUCCESS"
} catch {
    Write-AuditEntry "AUDIT FAILURE: DISM engine exception: $($_.Exception.Message)" -Level "ERROR"
}

Write-AuditEntry "=========================================================="
