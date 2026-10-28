<#
.SYNOPSIS
    Sentinel-IT Enterprise Logging Module
.DESCRIPTION
    Provides standardized forensic logging capabilities (Text and JSON for SIEM).
    Zero external dependencies, cross-compatible with PowerShell 5.1 and 7+.
#>

$global:SentinelLogFormat = if ($env:SENTINEL_LOG_FORMAT) { $env:SENTINEL_LOG_FORMAT.ToUpper() } else { "TEXT" }
$global:SentinelLogDir = if ($env:SENTINEL_LOG_DIR) { $env:SENTINEL_LOG_DIR } else { "C:\Program Files\Sentinel-IT\logs" }

function Initialize-SentinelLog {
    param(
        [Parameter(Mandatory=$true)][string]$EngineName,
        [Parameter(Mandatory=$false)][string]$LogDir = $global:SentinelLogDir
    )
    $effectiveLogDir = if ($LogDir) { $LogDir } else { $global:SentinelLogDir }
    $LogPath = Join-Path $effectiveLogDir $EngineName
    try {
        if (-not (Test-Path $LogPath)) { New-Item -ItemType Directory -Path $LogPath -Force -ErrorAction Stop | Out-Null }
        $PreciseTime = Get-Date -Format "yyyyMMdd_HHmmss_fffffff"
        $probeFile = Join-Path $LogPath ("$($EngineName)_$($PreciseTime).log")
        [System.IO.File]::AppendAllText($probeFile, "")
        $global:CurrentLogFile = $probeFile
    } catch {
        # Fallback to user-writable AppData/Temp if protected path (e.g. Program Files) has restricted ACLs
        $fallbackBase = if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA "Sentinel-IT\logs" } else { Join-Path $env:TEMP "Sentinel-IT\logs" }
        $LogPath = Join-Path $fallbackBase $EngineName
        if (-not (Test-Path $LogPath)) { New-Item -ItemType Directory -Path $LogPath -Force -ErrorAction SilentlyContinue | Out-Null }
        $PreciseTime = Get-Date -Format "yyyyMMdd_HHmmss_fffffff"
        $global:CurrentLogFile = Join-Path $LogPath ("$($EngineName)_$($PreciseTime).log")
    }
}

function Write-AuditEntry {
    param (
        [Parameter(Mandatory=$true)][string]$Message,
        [Parameter(Mandatory=$false)][ValidateSet("INFO", "SUCCESS", "WARN", "ERROR")][string]$Level = "INFO",
        [Parameter(Mandatory=$false)][hashtable]$Metadata = @{}
    )
    
    if (-not $global:CurrentLogFile) { return }

    $Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fffffff"
    $Identity = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    $HostName = $env:COMPUTERNAME

    $SafeMetadata = if ($null -ne $Metadata) { $Metadata } else { @{} }

    try {
        if ($global:SentinelLogFormat -eq "JSON") {
            $LogObj = [ordered]@{
                Timestamp = $Timestamp
                Level     = $Level
                Host      = $HostName
                Identity  = $Identity
                PID       = $PID
                Message   = $Message
                Metadata  = $SafeMetadata
            }
            $LogObj | ConvertTo-Json -Compress | Add-Content -Path $global:CurrentLogFile -Encoding utf8 -ErrorAction SilentlyContinue
        } else {
            $MetaStr = ""
            if ($SafeMetadata.Count -gt 0) {
                $pairs = foreach ($entry in $SafeMetadata.GetEnumerator()) {
                    "$($entry.Key)=$($entry.Value)"
                }
                $MetaStr = " | " + ($pairs -join ", ")
            }
            $Entry = "[{0}] [{1}] [PID:{2}] [{3}] {4}{5}" -f $Timestamp, $Level.PadRight(7), $PID, $Identity, $Message, $MetaStr
            $Entry | Add-Content -Path $global:CurrentLogFile -Encoding utf8 -ErrorAction SilentlyContinue
        }
    } catch { }
}

Export-ModuleMember -Function Initialize-SentinelLog, Write-AuditEntry
