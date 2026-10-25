<#
.SYNOPSIS
    System-wide and User Cache Reclamation Engine - ISO 20000
#>
$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Logger.psm1")
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Core.psm1")

Initialize-SentinelLog -EngineName "ISO_Cleanup_Temp_Global"

Write-AuditEntry "=========================================================="
Write-AuditEntry "   [ISO_Cleanup_Temp_Global] - CAPACITY RECLAMATION START"
Write-AuditEntry "=========================================================="

$TotalCleaned = 0

function Clean-Directory {
    param([string]$Path)
    if (Test-Path $Path) {
        try {
            $Items = Get-ChildItem -Path $Path -Recurse -Force -ErrorAction SilentlyContinue
            $Count = 0
            foreach ($Item in $Items) {
                try {
                    Remove-Item $Item.FullName -Force -Recurse -ErrorAction Stop
                    $Count++
                } catch { }
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

    $Profiles = Get-ChildItem -Path "C:\Users" -Directory -Force -ErrorAction SilentlyContinue
    foreach ($Profile in $Profiles) {
        $UserName = $Profile.Name
        if ($UserName -match "All Users|Default User|LocalService|NetworkService") { continue }
        $UserTemp = Join-Path $Profile.FullName "AppData\Local\Temp"
        Clean-Directory $UserTemp
    }
    
    Write-AuditEntry "RESULT: Global Temp Cleanup completed." -Level "SUCCESS" -Metadata @{ TotalCleaned = $TotalCleaned }
} catch {
    Write-AuditEntry "CRITICAL FAILURE: $($_.Exception.Message)" -Level "ERROR"
}

Write-AuditEntry "=========================================================="
