<#
.SYNOPSIS
    Asset Sanitization Engine - ISO/IEC 27001:2022 Control A.8.1
.DESCRIPTION
    Purges local and OneDrive screenshot vectors across all user profiles to prevent
    unauthorized data retention and endpoint Data Loss (DLP).
    Supports -AuditOnly switch for dry-run simulation in enterprise staging.
#>
[CmdletBinding()]
param(
    [switch]$AuditOnly
)

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Logger.psm1")
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Core.psm1")

Initialize-SentinelLog -EngineName "ISO_Cleanup_Screenshots"

Write-AuditEntry "=========================================================="
Write-AuditEntry "   [ISO_Cleanup_Screenshots] - SECURITY AUDIT START"
if ($AuditOnly) {
    Write-AuditEntry "   MODE: Audit-Only (Dry-Run / No files will be deleted)" -Level "WARN"
}
Write-AuditEntry "=========================================================="

$TotalPurged = 0
$TotalIdentified = 0

try {
    Assert-IsAdmin
    $Profiles = Get-ChildItem -Path "C:\Users" -Directory -Force -ErrorAction SilentlyContinue
    Write-AuditEntry "DISCOVERY: Total profile objects identified." -Metadata @{ ProfileCount = $Profiles.Count }

    foreach ($Profile in $Profiles) {
        $UserName = $Profile.Name
        if ($UserName -match "^(All Users|Default|Default User|LocalService|NetworkService|Public)$") { continue }
        
        $OneDriveDirs = Get-ChildItem -Path $Profile.FullName -Directory -Filter "OneDrive*" -ErrorAction SilentlyContinue
        
        # Standard local vectors (multi-language support: English, Spanish, German, French, Italian)
        $Vectors = @(
            (Join-Path $Profile.FullName "Pictures\Screenshots"),
            (Join-Path $Profile.FullName "Imágenes\Capturas"),
            (Join-Path $Profile.FullName "Imágenes\Capturas de pantalla"),
            (Join-Path $Profile.FullName "Bilder\Screenshots"),
            (Join-Path $Profile.FullName "Images\Captures d'écran"),
            (Join-Path $Profile.FullName "Immagini\Schermate")
        )

        foreach ($ODir in $OneDriveDirs) {
            $Vectors += (Join-Path $ODir.FullName "Pictures\Screenshots")
            $Vectors += (Join-Path $ODir.FullName "Imágenes\Capturas")
            $Vectors += (Join-Path $ODir.FullName "Imágenes\Capturas de pantalla")
        }

        foreach ($PathItem in $Vectors) {
            if (Test-Path -Path $PathItem) {
                try {
                    $Assets = Get-ChildItem -Path $PathItem -File -Force -ErrorAction SilentlyContinue
                    if ($Assets -and $Assets.Count -gt 0) {
                        $TotalIdentified += $Assets.Count
                        if ($AuditOnly) {
                            Write-AuditEntry "AUDIT: Found screenshot assets (Dry-Run)." -Level "INFO" -Metadata @{ Path = $PathItem; Count = $Assets.Count }
                        } else {
                            Remove-Item -Path "$PathItem\*" -Force -Recurse -ErrorAction Stop
                            $TotalPurged += $Assets.Count
                            Write-AuditEntry "PURGE: Cleared assets from vector." -Level "SUCCESS" -Metadata @{ Path = $PathItem; Count = $Assets.Count }
                        }
                    }
                } catch {
                    Write-AuditEntry "I/O FAILURE: Access denied or file lock." -Level "WARN" -Metadata @{ Path = $PathItem; Error = $_.Exception.Message }
                }
            }
        }
    }

    $SummaryLevel = if ($AuditOnly -and $TotalIdentified -gt 0) { "WARN" } else { "SUCCESS" }
    Write-AuditEntry "RESULT: Asset sanitization cycle completed." -Level $SummaryLevel -Metadata @{ 
        AuditOnly       = [string]$AuditOnly; 
        TotalIdentified = $TotalIdentified; 
        TotalPurged     = $TotalPurged 
    }
} catch {
    Write-AuditEntry "CRITICAL FAILURE: $($_.Exception.Message)" -Level "ERROR"
}

Write-AuditEntry "=========================================================="
