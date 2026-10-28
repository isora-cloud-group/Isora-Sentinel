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
    if (Test-Path -LiteralPath $Path) {
        $baseItem = Get-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
        if ($baseItem -and ($baseItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
            Write-AuditEntry "SECURITY: Target directory is a reparse point. Skipping for safety." -Level "WARN" -Metadata @{ Path = $Path }
            return
        }
        try {
            $Items = Get-ChildItem -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue
            if (-not $Items) { return }
            
            $Count = 0
            $script:TotalIdentified += $Items.Count

            if ($AuditOnly) {
                Write-AuditEntry "AUDIT: Identified items in directory (Dry-Run)." -Level "INFO" -Metadata @{ Path = $Path; Count = $Items.Count }
                return
            }

            # Sort descending by length so child items and subfiles are deleted before parent folders
            $SortedItems = $Items | Sort-Object { $_.FullName.Length } -Descending
            foreach ($Item in $SortedItems) {
                if ($Item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                    continue
                }
                if ($Item.Attributes -band [System.IO.FileAttributes]::ReadOnly) {
                    try { [System.IO.File]::SetAttributes($Item.FullName, [System.IO.FileAttributes]::Normal) } catch { }
                }
                try {
                    Remove-Item -LiteralPath $Item.FullName -Force -Recurse -ErrorAction Stop
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
    $IsAdmin = Test-IsAdmin
    if ($IsAdmin) {
        Write-AuditEntry "DISCOVERY: Administrator/SYSTEM session - Purging system-wide and all profile temp caches."
        
        $sysRoot = if ($env:SystemRoot) { $env:SystemRoot } else { "C:\WINDOWS" }
        $progData = if ($env:ProgramData) { $env:ProgramData } else { "C:\ProgramData" }

        Clean-Directory (Join-Path $sysRoot "Temp")
        Clean-Directory (Join-Path $sysRoot "SoftwareDistribution\Download")
        Clean-Directory (Join-Path $progData "Microsoft\Windows\WER\ReportQueue")
        Clean-Directory (Join-Path $progData "Microsoft\Windows\WER\Temp")

        # Discover profiles via Windows Registry ProfileList (handles custom/relocated user disks)
        $ProfileList = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\*" -ErrorAction SilentlyContinue
        $profilePaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

        if ($ProfileList) {
            foreach ($entry in $ProfileList) {
                if ($entry.ProfileImagePath -and (Test-Path -LiteralPath $entry.ProfileImagePath)) {
                    $uName = Split-Path $entry.ProfileImagePath -Leaf
                    if ($uName -notmatch "^(All Users|Default|Default User|LocalService|NetworkService|Public|systemprofile)$") {
                        [void]$profilePaths.Add($entry.ProfileImagePath)
                    }
                }
            }
        }

        # Fallback to scanning C:\Users
        $userRoots = @("C:\Users")
        foreach ($ur in $userRoots) {
            if (Test-Path -LiteralPath $ur) {
                $dirs = Get-ChildItem -LiteralPath $ur -Directory -Force -ErrorAction SilentlyContinue
                foreach ($d in $dirs) {
                    if ($d.Name -notmatch "^(All Users|Default|Default User|LocalService|NetworkService|Public)$") {
                        [void]$profilePaths.Add($d.FullName)
                    }
                }
            }
        }

        foreach ($pPath in $profilePaths) {
            Clean-Directory (Join-Path $pPath "AppData\Local\Temp")
            Clean-Directory (Join-Path $pPath "AppData\Local\CrashDumps")
        }
    } else {
        Write-AuditEntry "SCOPE: Standard user session - Sanitization scoped to current user temp and cache." -Metadata @{ User = $env:USERNAME }
        $userTemp = [System.IO.Path]::GetTempPath()
        Clean-Directory $userTemp
        if ($env:LOCALAPPDATA) {
            Clean-Directory (Join-Path $env:LOCALAPPDATA "Temp")
            Clean-Directory (Join-Path $env:LOCALAPPDATA "CrashDumps")
        }
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
