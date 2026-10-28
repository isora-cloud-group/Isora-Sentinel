<#
.SYNOPSIS
    Sentinel-IT Enterprise Logging Module - ISO/IEC 27001:2022 Control A.8.15
.DESCRIPTION
    Provides standardized forensic logging capabilities (Human Text and RFC 8259 NDJSON for SIEM).
    Zero external dependencies, cross-compatible with PowerShell 5.1 and 7+.
    Enforces clean UTF-8 encoding without Byte Order Mark (BOM) and automated log retention.
#>

$global:SentinelLogFormat = if ($env:SENTINEL_LOG_FORMAT) { $env:SENTINEL_LOG_FORMAT.ToUpper() } else { "TEXT" }
$global:SentinelLogDir = if ($env:SENTINEL_LOG_DIR) { $env:SENTINEL_LOG_DIR } else { "C:\Program Files\Sentinel-IT\logs" }
$global:SentinelUtf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Clear-OldSentinelLogs {
    param(
        [Parameter(Mandatory=$false)][string]$LogDir = $global:SentinelLogDir,
        [Parameter(Mandatory=$false)][int]$RetentionDays = 90
    )
    try {
        if (Test-Path -LiteralPath $LogDir) {
            $cutoff = (Get-Date).AddDays(-$RetentionDays)
            $oldLogs = Get-ChildItem -LiteralPath $LogDir -Recurse -Filter "*.log" -File -ErrorAction SilentlyContinue |
                Where-Object { $_.LastWriteTime -lt $cutoff }
            foreach ($old in $oldLogs) {
                Remove-Item -LiteralPath $old.FullName -Force -ErrorAction SilentlyContinue
            }
        }
    } catch { }
}

function Initialize-SentinelLog {
    param(
        [Parameter(Mandatory=$true)][string]$EngineName,
        [Parameter(Mandatory=$false)][string]$LogDir = $global:SentinelLogDir,
        [Parameter(Mandatory=$false)][int]$RetentionDays = 90
    )
    $effectiveLogDir = if ($LogDir) { $LogDir } else { $global:SentinelLogDir }
    $LogPath = Join-Path $effectiveLogDir $EngineName

    $retDays = if ($env:SENTINEL_LOG_RETENTION_DAYS) {
        $parsed = 0
        if ([int]::TryParse($env:SENTINEL_LOG_RETENTION_DAYS, [ref]$parsed) -and $parsed -gt 0) { $parsed } else { $RetentionDays }
    } else {
        $RetentionDays
    }

    try {
        if (-not (Test-Path $LogPath)) { New-Item -ItemType Directory -Path $LogPath -Force -ErrorAction Stop | Out-Null }
        Clear-OldSentinelLogs -LogDir $LogPath -RetentionDays $retDays
        $PreciseTime = Get-Date -Format "yyyyMMdd_HHmmss_fffffff"
        $probeFile = Join-Path $LogPath ("$($EngineName)_$($PreciseTime).log")
        [System.IO.File]::AppendAllText($probeFile, "", $global:SentinelUtf8NoBom)
        $global:CurrentLogFile = $probeFile
    } catch {
        # Fallback to user-writable AppData/Temp if protected path has restricted ACLs
        $fallbackBase = if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA "Sentinel-IT\logs" } else { Join-Path $env:TEMP "Sentinel-IT\logs" }
        $LogPath = Join-Path $fallbackBase $EngineName
        if (-not (Test-Path $LogPath)) { New-Item -ItemType Directory -Path $LogPath -Force -ErrorAction SilentlyContinue | Out-Null }
        Clear-OldSentinelLogs -LogDir $LogPath -RetentionDays $retDays
        $PreciseTime = Get-Date -Format "yyyyMMdd_HHmmss_fffffff"
        $probeFile = Join-Path $LogPath ("$($EngineName)_$($PreciseTime).log")
        try {
            [System.IO.File]::AppendAllText($probeFile, "", $global:SentinelUtf8NoBom)
        } catch { }
        $global:CurrentLogFile = $probeFile
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
            $JsonLine = ($LogObj | ConvertTo-Json -Compress) + [Environment]::NewLine
            [System.IO.File]::AppendAllText($global:CurrentLogFile, $JsonLine, $global:SentinelUtf8NoBom)
        } else {
            $MetaStr = ""
            if ($SafeMetadata.Count -gt 0) {
                $pairs = foreach ($entry in $SafeMetadata.GetEnumerator()) {
                    "$($entry.Key)=$($entry.Value)"
                }
                $MetaStr = " | " + ($pairs -join ", ")
            }
            $Entry = "[{0}] [{1}] [PID:{2}] [{3}] {4}{5}{6}" -f $Timestamp, $Level.PadRight(7), $PID, $Identity, $Message, $MetaStr, [Environment]::NewLine
            [System.IO.File]::AppendAllText($global:CurrentLogFile, $Entry, $global:SentinelUtf8NoBom)
        }
    } catch { }
}

Export-ModuleMember -Function Initialize-SentinelLog, Write-AuditEntry, Clear-OldSentinelLogs
