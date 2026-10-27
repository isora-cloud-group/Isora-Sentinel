<#
.SYNOPSIS
    System-wide and User Cache Reclamation Engine - ISO/IEC 20000-1:2018 §8.2
.DESCRIPTION
    Systematically reclaims disk capacity by safely purging system temp folders,
    Windows Update download caches, and per-profile AppData Temp caches.
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

Initialize-SentinelLog -EngineName "ISO_Cleanup_Temp_Global"

Write-AuditEntry "=========================================================="
Write-AuditEntry "   [ISO_Cleanup_Temp_Global] - CAPACITY RECLAMATION START"
if ($AuditOnly) {
    Write-AuditEntry "   MODE: Audit-Only (Dry-Run / No files will be deleted)" -Level "WARN"
}
Write-AuditEntry "=========================================================="

$TotalCleaned = 0
$TotalIdentified = 0

function Clean-Directory {
    param([string]$Path)
    if (Test-Path $Path) {
        try {
            $Items = Get-ChildItem -Path $Path -Recurse -Force -ErrorAction SilentlyContinue
            if (-not $Items) { return }
            
            $Count = 0
            $script:TotalIdentified += $Items.Count

            if ($AuditOnly) {
                Write-AuditEntry "AUDIT: Identified items in directory (Dry-Run)." -Level "INFO" -Metadata @{ Path = $Path; Count = $Items.Count }
                return
            }

            foreach ($Item in $Items) {
                try {
                    Remove-Item $Item.FullName -Force -Recurse -ErrorAction Stop
                    $Count++
                } catch {
                    # Files locked by running system processes are skipped gracefully
                }
            }
            if ($Count -gt 0) {
                Write-AuditEntry "PURGE: Removed items from directory." -Level "SUCCESS" -Metadata @{ Path = $Path; Count = $Count }
                $script:TotalCleaned += $Count
            }
        } catch {
            Write-AuditEntry "I/O FAILURE: Could not process directory." -Level "WARN" -Metadata @{ Path = $Path; Error = $_.Exception.Message }
        }
    }
}

try {
    Assert-IsAdmin
    Clean-Directory "C:\Windows\Temp"
    Clean-Directory "C:\Windows\SoftwareDistribution\Download"
    Clean-Directory "C:\ProgramData\Microsoft\Windows\WER\ReportQueue"

    $Profiles = Get-ChildItem -Path "C:\Users" -Directory -Force -ErrorAction SilentlyContinue
    foreach ($Profile in $Profiles) {
        $UserName = $Profile.Name
        if ($UserName -match "^(All Users|Default|Default User|LocalService|NetworkService|Public)$") { continue }
        $UserTemp = Join-Path $Profile.FullName "AppData\Local\Temp"
        Clean-Directory $UserTemp
    }
    
    $SummaryLevel = if ($AuditOnly -and $TotalIdentified -gt 0) { "WARN" } else { "SUCCESS" }
    Write-AuditEntry "RESULT: Global Temp Reclamation completed." -Level $SummaryLevel -Metadata @{ 
        AuditOnly       = [string]$AuditOnly; 
        TotalIdentified = $TotalIdentified; 
        TotalCleaned    = $TotalCleaned 
    }
} catch {
    Write-AuditEntry "CRITICAL FAILURE: $($_.Exception.Message)" -Level "ERROR"
}

Write-AuditEntry "=========================================================="
