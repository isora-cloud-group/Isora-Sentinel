<#
.SYNOPSIS
    System-wide and User Cache Reclamation Engine - ISO/IEC 20000-1:2018 §8.2
.DESCRIPTION
    Systematically reclaims disk capacity by safely purging system temp folders,
    Windows Update download caches, and per-profile AppData Temp caches.
    Enforces real-time TOCTOU symlink/junction defenses and path boundary constraints.
    Supports -AuditOnly switch for dry-run simulation in enterprise staging.
#>
[CmdletBinding()]
param(
    [switch]$Apply,
    [switch]$AuditOnly,
    [int]$DaysOld = 0
)

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Logger.psm1")
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Core.psm1")

Initialize-SentinelLog -EngineName "ISO_Cleanup_Temp_Global"
Set-SentinelProgress -Activity "Limpieza de archivos temporales" -Percent 5 -Status "Iniciando motor de optimización..."
try { 
    $null = Assert-SentinelIntegrity 
} catch { 
    Write-AuditEntry "INTEGRITY CRITICAL (HALTED): $($_.Exception.Message)" -Level "ERROR" -Metadata @{ SecurityViolation = "TamperDetected"; Aborted = "True" }
    throw 
}

# Safe by default: Audit-only mode unless -Apply is explicitly specified.
$script:AuditMode = (-not $Apply) -or $AuditOnly

Write-AuditEntry "=========================================================="
Write-AuditEntry "   [ISO_Cleanup_Temp_Global] - CAPACITY RECLAMATION START"
if ($script:AuditMode) {
    Write-AuditEntry "   MODE: Audit-Only (Dry-Run / Safe default / No files will be deleted)" -Level "WARN"
} else {
    Write-AuditEntry "   MODE: Apply (Active deletion enabled via -Apply)" -Level "INFO"
}
Write-AuditEntry "=========================================================="

$TotalCleaned = 0
$TotalIdentified = 0

function Clean-Directory {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return }

    $baseItem = Get-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
    if (-not $baseItem) { return }
    if ($baseItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
        Write-AuditEntry "SECURITY: Target directory is a reparse point. Skipping for safety." -Level "WARN" -Metadata @{ Path = $Path }
        return
    }

    try {
        $normalizedBase = [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/') + '\'
        $Items = Get-ChildItem -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue
        if (-not $Items) { return }
        
        $Count = 0
        $script:TotalIdentified += $Items.Count

        if ($script:AuditMode) {
            Write-AuditEntry "AUDIT: Identified items in directory (Dry-Run)." -Level "INFO" -Metadata @{ Path = $Path; Count = $Items.Count }
            return
        }

        # Sort descending by length so child items and subfiles are deleted before parent folders
        $SortedItems = $Items | Sort-Object { $_.FullName.Length } -Descending
        $cutoffDate = if ($DaysOld -gt 0) { (Get-Date).AddDays(-$DaysOld) } else { $null }

        foreach ($Item in $SortedItems) {
            $itemFullPath = [System.IO.Path]::GetFullPath($Item.FullName)
            if ($itemFullPath -match '\\Isora-Sentinel(\\|$)') { continue }
            
            # Boundary check: enforce containment within normalized target directory
            if (-not $itemFullPath.StartsWith($normalizedBase, [System.StringComparison]::OrdinalIgnoreCase)) {
                Write-AuditEntry "SECURITY: Path traversal boundary breach detected. Skipping item." -Level "WARN" -Metadata @{ Path = $Item.FullName }
                continue
            }

            if ($cutoffDate -and $Item.LastWriteTime -ge $cutoffDate) {
                continue
            }

            # Real-time fresh attribute query (defeats TOCTOU symlink/junction races)
            try {
                $freshAttr = [System.IO.File]::GetAttributes($Item.FullName)
                if (($freshAttr -band [System.IO.FileAttributes]::ReparsePoint) -or (-not (Test-SafePathHierarchy -TargetPath $Item.FullName -BaseRoot $Path))) {
                    Write-AuditEntry "SECURITY: Reparse point detected during sanitization. Skipping item." -Level "WARN" -Metadata @{ Path = $Item.FullName }
                    continue
                }
                if ($freshAttr -band [System.IO.FileAttributes]::ReadOnly) {
                    [System.IO.File]::SetAttributes($Item.FullName, [System.IO.FileAttributes]::Normal)
                }
            } catch {
                continue
            }

            try {
                # Never recurse on individual item deletion - prevents cascading into newly created junctions
                Remove-Item -LiteralPath $Item.FullName -Force -Recurse:$false -ErrorAction Stop
                $Count++
            } catch {
                # Files locked by running system processes or non-empty directories are skipped gracefully
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

try {
    $IsAdmin = Test-IsAdmin
    if ($IsAdmin) {
        Write-AuditEntry "DISCOVERY: Administrator/SYSTEM session - Purging system-wide and all profile temp caches."
        
        $sysRoot = if ($env:SystemRoot) { $env:SystemRoot } else { "C:\WINDOWS" }
        $progData = if ($env:ProgramData) { $env:ProgramData } else { "C:\ProgramData" }

        Set-SentinelProgress -Activity "Limpieza de archivos temporales" -Percent 20 -Status "Limpiando archivos temporales del sistema..."
        Clean-Directory (Join-Path $sysRoot "Temp")
        Set-SentinelProgress -Activity "Limpieza de archivos temporales" -Percent 35 -Status "Limpiando caché de Windows Update y reportes WER..."
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

        $pTotal = [math]::Max(1, $profilePaths.Count)
        $pIdx = 0
        foreach ($pPath in $profilePaths) {
            $pIdx++
            $pctProf = 50 + [int](($pIdx / $pTotal) * 45)
            $leafName = Split-Path $pPath -Leaf
            Set-SentinelProgress -Activity "Limpieza de archivos temporales" -Percent $pctProf -Status "Limpiando perfiles de usuario ($leafName)..."
            Clean-Directory (Join-Path $pPath "AppData\Local\Temp")
            Clean-Directory (Join-Path $pPath "AppData\Local\CrashDumps")
        }
    } else {
        Write-AuditEntry "SCOPE: Standard user session - Sanitization scoped to current user temp and cache." -Metadata @{ User = $env:USERNAME }
        Set-SentinelProgress -Activity "Limpieza de archivos temporales" -Percent 30 -Status "Limpiando temporales del usuario..."
        $userDirs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        $userTemp = [System.IO.Path]::GetTempPath()
        if ($userTemp) { [void]$userDirs.Add(([System.IO.Path]::GetFullPath($userTemp).TrimEnd('\', '/'))) }
        if ($env:LOCALAPPDATA) {
            [void]$userDirs.Add(([System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA "Temp")).TrimEnd('\', '/')))
            [void]$userDirs.Add(([System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA "CrashDumps")).TrimEnd('\', '/')))
        }
        $uTotal = [math]::Max(1, $userDirs.Count)
        $uIdx = 0
        foreach ($uDir in $userDirs) {
            $uIdx++
            $pctU = 40 + [int](($uIdx / $uTotal) * 50)
            Set-SentinelProgress -Activity "Limpieza de archivos temporales" -Percent $pctU -Status "Limpiando: $uDir..."
            Clean-Directory $uDir
        }
    }
    
    $SummaryLevel = if ($script:AuditMode -and $TotalIdentified -gt 0) { "WARN" } else { "SUCCESS" }
    Write-AuditEntry "RESULT: Global Temp Reclamation completed." -Level $SummaryLevel -Metadata @{ 
        AuditOnly       = [string]$script:AuditMode; 
        Apply           = [string](-not $script:AuditMode);
        TotalIdentified = $TotalIdentified; 
        TotalCleaned    = $TotalCleaned 
    }
} catch {
    Write-AuditEntry "CRITICAL FAILURE: $($_.Exception.Message)" -Level "ERROR"
}

Write-AuditEntry "=========================================================="

Set-SentinelProgress -Activity "Limpieza de archivos temporales" -Percent 100 -Status "Limpieza completada ($TotalCleaned archivos liberados)." -Completed
