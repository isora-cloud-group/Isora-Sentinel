<#
.SYNOPSIS
    Isora Sentinel Enterprise Logging Module - ISO/IEC 27001:2022 Control A.8.15
.DESCRIPTION
    Provides standardized forensic logging capabilities (Human Text and RFC 8259 NDJSON for SIEM).
    Zero external dependencies, cross-compatible with PowerShell 5.1 and 7+.
    Enforces clean UTF-8 encoding without Byte Order Mark (BOM), automated log retention,
    log injection sanitization, and total directory disk quota protection for unattended deployments.
#>

$global:SentinelLogFormat = if ($env:SENTINEL_LOG_FORMAT) { $env:SENTINEL_LOG_FORMAT.ToUpper() } else { "TEXT" }
$global:SentinelLogDir = if ($env:ISORA_SENTINEL_LOG_DIR) { $env:ISORA_SENTINEL_LOG_DIR } elseif ($env:SENTINEL_LOG_DIR) { $env:SENTINEL_LOG_DIR } else { "C:\Program Files\Isora-Sentinel\logs" }
$global:SentinelUtf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Clear-OldSentinelLogs {
    param(
        [Parameter(Mandatory=$false)][string]$LogDir = $global:SentinelLogDir,
        [Parameter(Mandatory=$false)][int]$RetentionDays = 90,
        [Parameter(Mandatory=$false)][int]$MaxTotalSizeMB = 250
    )
    try {
        if (Test-Path -LiteralPath $LogDir) {
            $effectiveMaxMB = if ($env:SENTINEL_MAX_LOG_MB) {
                $parsedMB = 0
                if ([int]::TryParse($env:SENTINEL_MAX_LOG_MB, [ref]$parsedMB) -and $parsedMB -gt 0) { $parsedMB } else { $MaxTotalSizeMB }
            } else {
                $MaxTotalSizeMB
            }

            # Security: Ensure LogDir itself is not a junction or reparse point
            $logDirItem = Get-Item -LiteralPath $LogDir -Force -ErrorAction SilentlyContinue
            if (-not $logDirItem -or ($logDirItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
                return
            }
            $normalizedLogDir = [System.IO.Path]::GetFullPath($LogDir).TrimEnd('\', '/')

            # 1. Prune files older than retention days
            $cutoff = (Get-Date).AddDays(-$RetentionDays)
            $oldLogs = Get-ChildItem -LiteralPath $LogDir -Recurse -Filter "*.log" -File -ErrorAction SilentlyContinue |
                Where-Object { $_.LastWriteTime -lt $cutoff }
            foreach ($old in $oldLogs) {
                $freshAttr = [System.IO.File]::GetAttributes($old.FullName)
                if (($freshAttr -band [System.IO.FileAttributes]::ReparsePoint) -eq 0 -and
                    $old.FullName.StartsWith($normalizedLogDir, [System.StringComparison]::OrdinalIgnoreCase)) {
                    Remove-Item -LiteralPath $old.FullName -Force -ErrorAction SilentlyContinue
                }
            }

            # 2. Enforce total log size quota (prevent disk exhaustion in unattended 6-month operation)
            $allLogs = Get-ChildItem -LiteralPath $LogDir -Recurse -Filter "*.log" -File -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime
            if ($allLogs) {
                $totalBytes = ($allLogs | Measure-Object -Property Length -Sum).Sum
                $maxBytes = [int64]$effectiveMaxMB * 1024 * 1024
                if ($totalBytes -gt $maxBytes) {
                    foreach ($logFile in $allLogs) {
                        if ($totalBytes -le $maxBytes) { break }
                        $fileSize = $logFile.Length
                        $freshAttr = [System.IO.File]::GetAttributes($logFile.FullName)
                        if (($freshAttr -band [System.IO.FileAttributes]::ReparsePoint) -eq 0 -and
                            $logFile.FullName.StartsWith($normalizedLogDir, [System.StringComparison]::OrdinalIgnoreCase)) {
                            Remove-Item -LiteralPath $logFile.FullName -Force -ErrorAction SilentlyContinue
                        }
                        $totalBytes -= $fileSize
                    }
                }
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
        Clear-OldSentinelLogs -LogDir $effectiveLogDir -RetentionDays $retDays
        $PreciseTime = Get-Date -Format "yyyyMMdd_HHmmss_fffffff"
        $probeFile = Join-Path $LogPath ("$($EngineName)_$($PreciseTime).log")
        [System.IO.File]::AppendAllText($probeFile, "", $global:SentinelUtf8NoBom)
        $global:CurrentLogFile = $probeFile
    } catch {
        # Fallback to user-writable AppData/Temp if protected path has restricted ACLs
        $fallbackBase = if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA "Isora-Sentinel\logs" } else { Join-Path $env:TEMP "Isora-Sentinel\logs" }
        $LogPath = Join-Path $fallbackBase $EngineName
        if (-not (Test-Path $LogPath)) { New-Item -ItemType Directory -Path $LogPath -Force -ErrorAction SilentlyContinue | Out-Null }
        Clear-OldSentinelLogs -LogDir $fallbackBase -RetentionDays $retDays
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

    # Log injection defense: sanitize newlines from user/engine messages
    $SafeMessage = if ($Message) { $Message -replace "[\r\n]+", " " } else { "" }
    $SafeMetadata = if ($null -ne $Metadata) { $Metadata } else { @{} }

    try {
        if ($global:SentinelLogFormat -eq "JSON") {
            $LogObj = [ordered]@{
                Timestamp = $Timestamp
                Level     = $Level
                Host      = $HostName
                Identity  = $Identity
                PID       = $PID
                Message   = $SafeMessage
                Metadata  = $SafeMetadata
            }
            $JsonLine = ($LogObj | ConvertTo-Json -Compress) + [Environment]::NewLine
            [System.IO.File]::AppendAllText($global:CurrentLogFile, $JsonLine, $global:SentinelUtf8NoBom)
        } else {
            $MetaStr = ""
            if ($SafeMetadata.Count -gt 0) {
                $pairs = foreach ($entry in $SafeMetadata.GetEnumerator()) {
                    $cleanVal = [string]$entry.Value -replace "[\r\n]+", " "
                    "$($entry.Key)=$cleanVal"
                }
                $MetaStr = " | " + ($pairs -join ", ")
            }
            $Entry = "[{0}] [{1}] [PID:{2}] [{3}] {4}{5}{6}" -f $Timestamp, $Level.PadRight(7), $PID, $Identity, $SafeMessage, $MetaStr, [Environment]::NewLine
            [System.IO.File]::AppendAllText($global:CurrentLogFile, $Entry, $global:SentinelUtf8NoBom)
        }
    } catch { }
}

Export-ModuleMember -Function Initialize-SentinelLog, Write-AuditEntry, Clear-OldSentinelLogs
