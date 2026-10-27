<#
.SYNOPSIS
    BitLocker Cryptographic Compliance Audit Engine - ISO/IEC 27001:2022 Control A.8.24
.DESCRIPTION
    Audits disk encryption status across Operating System and fixed data volumes.
    Evaluates ProtectionStatus, EncryptionPercentage, and KeyProtector types without altering state.
    Emits structured SIEM forensic events for cryptographic compliance monitoring.
#>
[CmdletBinding()]
param(
    [string]$TargetMountPoint
)

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Logger.psm1")
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Core.psm1")

Initialize-SentinelLog -EngineName "ISO_BitLocker_Audit"

Write-AuditEntry "=========================================================="
Write-AuditEntry "   [ISO_BitLocker_Audit] - CRYPTOGRAPHIC COMPLIANCE START"
Write-AuditEntry "=========================================================="

try {
    Assert-IsAdmin

    # Check for Get-BitLockerVolume cmdlet; fallback to manage-bde.exe if unavailable
    $hasBitLockerCmdlet = [bool](Get-Command -Name Get-BitLockerVolume -ErrorAction SilentlyContinue)
    $volumesAudited = 0
    $unprotectedCount = 0

    if ($hasBitLockerCmdlet) {
        $volumes = Get-BitLockerVolume -ErrorAction Stop
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
    } else {
        # Fallback to manage-bde.exe parsing
        Write-AuditEntry "AUDIT: Get-BitLockerVolume unavailable. Invoking manage-bde.exe fallback..."
        $bdeOutput = & manage-bde.exe -status 2>&1 | Out-String
        $lines = $bdeOutput -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        foreach ($line in $lines) {
            Write-AuditEntry $line.Trim() -Level "INFO"
        }
    }

    $summaryLevel = if ($unprotectedCount -eq 0) { "SUCCESS" } else { "WARN" }
    Write-AuditEntry "RESULT: BitLocker cryptographic compliance audit concluded." -Level $summaryLevel -Metadata @{
        VolumesAudited   = $volumesAudited
        UnprotectedCount = $unprotectedCount
        AuditStandard    = "ISO/IEC 27001:2022 A.8.24"
    }
} catch {
    Write-AuditEntry "CRITICAL FAILURE: $($_.Exception.Message)" -Level "ERROR"
}

Write-AuditEntry "=========================================================="
