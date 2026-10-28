<#
.SYNOPSIS
    BitLocker Cryptographic Compliance Audit Engine - ISO/IEC 27001:2022 Control A.8.24
.DESCRIPTION
    Audits disk encryption status across Operating System and fixed data volumes.
    Evaluates ProtectionStatus, EncryptionPercentage, and KeyProtector types without altering state.
    Supports bilingual English/Spanish manage-bde output parsing for international deployments.
    Emits structured SIEM forensic events for cryptographic compliance monitoring.
#>
[CmdletBinding()]
param(
    [string]$TargetMountPoint,
    [switch]$IncludeRemovable
)

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Logger.psm1")
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Core.psm1")

Initialize-SentinelLog -EngineName "ISO_BitLocker_Audit"
try { 
    Assert-SentinelIntegrity 
} catch { 
    Write-AuditEntry "INTEGRITY CRITICAL (HALTED): $($_.Exception.Message)" -Level "ERROR" -Metadata @{ SecurityViolation = "TamperDetected"; Aborted = "True" }
    throw 
}

Write-AuditEntry "=========================================================="
Write-AuditEntry "   [ISO_BitLocker_Audit] - CRYPTOGRAPHIC COMPLIANCE START"
Write-AuditEntry "=========================================================="

try {
    $IsAdmin = Test-IsAdmin
    if (-not $IsAdmin) {
        Write-AuditEntry "NOTICE: BitLocker volume encryption queries require Administrator or SYSTEM privileges. Attempting read-only check..." -Level "WARN"
    }

    # Check for Get-BitLockerVolume cmdlet; fallback to manage-bde.exe if unavailable
    $hasBitLockerCmdlet = [bool](Get-Command -Name Get-BitLockerVolume -ErrorAction SilentlyContinue)
    $volumesAudited = 0
    $unprotectedCount = 0
    $cmdletSucceeded = $false

    if ($hasBitLockerCmdlet) {
        try {
            $volumes = Get-BitLockerVolume -ErrorAction Stop
            if (-not $IncludeRemovable) {
                $volumes = $volumes | Where-Object { [string]$_.VolumeType -notmatch "Removable" }
            }
            if ($TargetMountPoint) {
                $volumes = $volumes | Where-Object { $_.MountPoint -eq $TargetMountPoint }
            }

            foreach ($vol in $volumes) {
                $volumesAudited++
                $isProtected = ($vol.ProtectionStatus -eq "On")
                $isFullyEncrypted = ($vol.VolumeStatus -eq "FullyEncrypted")
                $protectorTypes = ($vol.KeyProtector | ForEach-Object { $_.KeyProtectorType }) -join ";"

                $meta = @{
                    MountPoint       = $vol.MountPoint
                    VolumeType       = [string]$vol.VolumeType
                    ProtectionStatus = [string]$vol.ProtectionStatus
                    VolumeStatus     = [string]$vol.VolumeStatus
                    EncryptionPct    = [string]$vol.EncryptionPercentage
                    KeyProtectors    = if ($protectorTypes) { $protectorTypes } else { "None" }
                }

                if ($isProtected -and $isFullyEncrypted) {
                    Write-AuditEntry "VOLUME COMPLIANT: $($vol.MountPoint) is fully encrypted with active protection." -Level "SUCCESS" -Metadata $meta
                } else {
                    $unprotectedCount++
                    Write-AuditEntry "NON-COMPLIANCE DETECTED: $($vol.MountPoint) does not meet cryptographic encryption policy." -Level "WARN" -Metadata $meta
                }
            }
            $cmdletSucceeded = $true
        } catch {
            Write-AuditEntry "AUDIT: Get-BitLockerVolume threw exception ($($_.Exception.Message)). Attempting manage-bde.exe fallback..." -Level "WARN"
        }
    }

    if (-not $cmdletSucceeded) {
        # Fallback to manage-bde.exe parsing (bilingual regex: English and Spanish)
        Write-AuditEntry "AUDIT: Invoking manage-bde.exe fallback..."
        try {
            $bdeExe = Join-Path $env:SystemRoot "System32\manage-bde.exe"
            if (-not (Test-Path $bdeExe)) { $bdeExe = "manage-bde.exe" }
            $bdeOutput = & $bdeExe -status 2>&1 | Out-String
            $lines = $bdeOutput -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
            foreach ($line in $lines) {
                Write-AuditEntry $line.Trim() -Level "INFO"
                if ($line -match "(?:Volume|Volumen)\s+([A-Z]:)") { 
                    $volumesAudited++ 
                }
                if ($line -match "Protection\s*Off|Protecci[oó]n\s*(?:desactivada|deshabilitada)|Conversion Status:\s*Fully Decrypted|Estado de conversi[oó]n:\s*Totalmente descifrado|Percentage Encrypted:\s*0|Porcentaje cifrado:\s*0") {
                    $unprotectedCount++
                }
            }
        } catch {
            Write-AuditEntry "AUDIT NOTICE: BitLocker management utilities are not available or not supported on this Windows edition." -Level "WARN" -Metadata @{ Error = $_.Exception.Message }
        }
    }

    $summaryLevel = if ($unprotectedCount -eq 0 -and $volumesAudited -gt 0) { "SUCCESS" } elseif ($volumesAudited -eq 0) { "WARN" } else { "WARN" }
    Write-AuditEntry "RESULT: BitLocker cryptographic compliance audit concluded." -Level $summaryLevel -Metadata @{
        VolumesAudited   = $volumesAudited
        UnprotectedCount = $unprotectedCount
        AuditStandard    = "ISO/IEC 27001:2022 A.8.24"
    }
} catch {
    Write-AuditEntry "CRITICAL FAILURE: $($_.Exception.Message)" -Level "ERROR"
}

Write-AuditEntry "=========================================================="
