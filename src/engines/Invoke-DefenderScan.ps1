<#
.SYNOPSIS
    Endpoint Security Engine - ISO/IEC 27001:2022 Control A.8.7
.DESCRIPTION
    Triggers signature updates and automated antimalware scans via Microsoft Defender MpCmdRun.exe.
    Supports -ScanType 1 (Quick Scan) and 2 (Full Scan), captures process exit codes for SIEM alerts,
    and gracefully handles third-party EDR environments.
#>
[CmdletBinding()]
param(
    [ValidateSet("1", "2")]
    [string]$ScanType = "2",
    [switch]$SkipSignatureUpdate
)

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Logger.psm1")
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Core.psm1")

Initialize-SentinelLog -EngineName "ISO_Defender_Scan"
Set-SentinelProgress -Activity "Análisis antimalware (Defender)" -Percent 5 -Status "Iniciando motor antimalware..."
try { 
    $null = Assert-SentinelIntegrity 
} catch { 
    Write-AuditEntry "INTEGRITY CRITICAL (HALTED): $($_.Exception.Message)" -Level "ERROR" -Metadata @{ SecurityViolation = "TamperDetected"; Aborted = "True" }
    throw 
}

$ScanTypeName = if ($ScanType -eq "1") { "Quick Scan" } else { "Full Scan" }

Write-AuditEntry "=========================================================="
Write-AuditEntry "   [ISO_Defender_Scan] - SECURITY SCAN START"
Write-AuditEntry "   CONFIGURATION: ScanType=$ScanType ($ScanTypeName)"
Write-AuditEntry "=========================================================="

try {
    $IsAdmin = Test-IsAdmin
    if (-not $IsAdmin) {
        Write-AuditEntry "NOTICE: Standard user session - Antimalware scan running under user context." -Level "INFO" -Metadata @{ User = $env:USERNAME }
    }

    $DefenderPath = Get-DefenderPlatformPath
    Write-AuditEntry "ENGINE: Resolved Defender Platform Path." -Metadata @{ EnginePath = $DefenderPath }

    if (-not (Test-Path -LiteralPath $DefenderPath)) {
        Write-AuditEntry "ENGINE NOTICE: Microsoft Defender platform executable (MpCmdRun.exe) was not found. Endpoint may be protected by third-party EDR/AV solution." -Level "WARN" -Metadata @{ ResolvedPath = $DefenderPath }
        Write-AuditEntry "=========================================================="
        return
    }

    # 1. Signature Definitions Refresh (with offline network tolerance and active progress heartbeats)
    if (-not $SkipSignatureUpdate) {
        Write-AuditEntry "UPDATE: Checking and updating antimalware signature definitions..."
        Set-SentinelProgress -Activity "Análisis antimalware (Defender)" -Percent 15 -Status "Actualizando definiciones de seguridad..."
        try {
            $sigPsi = New-Object System.Diagnostics.ProcessStartInfo
            $sigPsi.FileName = $DefenderPath
            $sigPsi.Arguments = "-SignatureUpdate"
            $sigPsi.UseShellExecute = $false
            $sigPsi.CreateNoWindow = $true
            $sigPsi.RedirectStandardOutput = $true
            $sigPsi.RedirectStandardError = $true

            $sigProc = New-Object System.Diagnostics.Process
            $sigProc.StartInfo = $sigPsi
            [void]$sigProc.Start()

            $sigSw = [System.Diagnostics.Stopwatch]::StartNew()
            while (-not $sigProc.WaitForExit(1500)) {
                $sElapsed = [int]$sigSw.Elapsed.TotalSeconds
                $sPct = [int][math]::Min(30, 15 + ($sElapsed / 3))
                Set-SentinelProgress -Activity "Análisis antimalware (Defender)" -Percent $sPct -Status "Actualizando definiciones ($sElapsed s)..."
                if ($sElapsed -gt 90) { 
                    try { $sigProc.Kill() } catch { }; break 
                }
            }
            $updateExitCode = $sigProc.ExitCode
            $updateLevel = if ($updateExitCode -eq 0) { "SUCCESS" } else { "WARN" }
            Write-AuditEntry "UPDATE: Signature definition update finished." -Level $updateLevel -Metadata @{ 
                ExitCode = $updateExitCode 
            }
            Set-SentinelProgress -Activity "Análisis antimalware (Defender)" -Percent 30 -Status "Definiciones de seguridad actualizadas."
        } catch {
            Write-AuditEntry "UPDATE: Online signature update unavailable ($($_.Exception.Message)). Proceeding with cached definitions." -Level "WARN"
        }
    }

    # 2. Antimalware Scan Execution
    # Ensure no orphaned or stuck MpCmdRun instance is locking the engine
    $hadStuckScan = $false
    try {
        Get-Process -Name "MpCmdRun" -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $PID } | ForEach-Object {
            $hadStuckScan = $true
            Write-AuditEntry "ENGINE: Found existing MpCmdRun instance (PID: $($_.Id)). Clearing scan lock..." -Level "WARN"
            try { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue } catch { }
        }
    } catch { }

    # Only request service-level scan cancellation if a stuck MpCmdRun instance was actually present
    if ($hadStuckScan) {
        try {
            & $DefenderPath -Scan -Cancel 2>&1 | Out-Null
            Start-Sleep -Seconds 2
        } catch { }
    }

    Write-AuditEntry "SCAN: Invoking Defender $ScanTypeName..." -Metadata @{ ScanType = $ScanType }
    Set-SentinelProgress -Activity "Análisis Defender ($ScanTypeName)" -Percent 35 -Status "Iniciando $ScanTypeName..."
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $DefenderPath
    $psi.Arguments = "-Scan -ScanType $ScanType"
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true

    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo = $psi
    $scanOutputList = New-Object System.Collections.Generic.List[string]

    [void]$proc.Start()
    $outTask = $proc.StandardOutput.ReadLineAsync()
    $errTask = $proc.StandardError.ReadLineAsync()

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $estSec = if ($ScanType -eq "1") { 300 } else { 3600 }
    $reportedPct = -1.0

    while (-not $proc.WaitForExit(1000)) {
        while ($null -ne $outTask -and $outTask.IsCompleted) {
            if (-not $outTask.IsFaulted -and -not $outTask.IsCanceled) {
                $line = $outTask.Result
                if ($null -ne $line) {
                    if (-not [string]::IsNullOrWhiteSpace($line)) {
                        $trimmed = $line.Trim()
                        $scanOutputList.Add($trimmed)
                        $lvl = if ($trimmed -match "Error|Fail|threat|Threat") { "WARN" } else { "INFO" }
                        Write-AuditEntry $trimmed -Level $lvl
                        if ($trimmed -match '(\d+(?:\.\d+)?)\s*%') {
                            $reportedPct = [double]$matches[1]
                        }
                    }
                    $outTask = $proc.StandardOutput.ReadLineAsync()
                } else {
                    $outTask = $null
                }
            } else {
                $outTask = $null
            }
        }

        while ($null -ne $errTask -and $errTask.IsCompleted) {
            if (-not $errTask.IsFaulted -and -not $errTask.IsCanceled) {
                $errLine = $errTask.Result
                if ($null -ne $errLine) {
                    if (-not [string]::IsNullOrWhiteSpace($errLine)) {
                        $trimmedErr = $errLine.Trim()
                        $scanOutputList.Add($trimmedErr)
                        Write-AuditEntry $trimmedErr -Level "WARN"
                    }
                    $errTask = $proc.StandardError.ReadLineAsync()
                } else {
                    $errTask = $null
                }
            } else {
                $errTask = $null
            }
        }

        $elapsed = $sw.Elapsed.TotalSeconds
        $timeStr = $sw.Elapsed.ToString("mm\:ss")
        if ($reportedPct -ge 0) {
            $dynPct = [int][math]::Min(95, [math]::Max(35, (35 + ($reportedPct * 0.6))))
            Set-SentinelProgress -Activity "Análisis Defender ($ScanTypeName)" -Percent $dynPct -Status "Escaneando archivos ($([math]::Round($reportedPct, 1))% · $timeStr)..."
        } else {
            $ratio = [math]::Min(0.95, 0.35 + (0.60 * (1.0 - [math]::Exp(-$elapsed / ([math]::Max(10, $estSec * 0.5))))))
            $dynPct = [int]($ratio * 100)
            Set-SentinelProgress -Activity "Análisis Defender ($ScanTypeName)" -Percent $dynPct -Status "Escaneando archivos ($dynPct% · $timeStr)..."
        }
    }

    # Drain any remaining buffered lines after process exit
    try {
        if ($null -ne $outTask -and $outTask.Wait(500) -and $null -ne $outTask.Result) {
            $remLine = $outTask.Result.Trim()
            if ($remLine) {
                $scanOutputList.Add($remLine)
                $lvl = if ($remLine -match "Error|Fail|threat|Threat") { "WARN" } else { "INFO" }
                Write-AuditEntry $remLine -Level $lvl
            }
        }
        while (-not $proc.StandardOutput.EndOfStream) {
            $rem = $proc.StandardOutput.ReadLine()
            if (-not [string]::IsNullOrWhiteSpace($rem)) {
                $trimmedRem = $rem.Trim()
                $scanOutputList.Add($trimmedRem)
                $lvl = if ($trimmedRem -match "Error|Fail|threat|Threat") { "WARN" } else { "INFO" }
                Write-AuditEntry $trimmedRem -Level $lvl
            }
        }
        while (-not $proc.StandardError.EndOfStream) {
            $remErr = $proc.StandardError.ReadLine()
            if (-not [string]::IsNullOrWhiteSpace($remErr)) {
                $trimmedRemErr = $remErr.Trim()
                $scanOutputList.Add($trimmedRemErr)
                Write-AuditEntry $trimmedRemErr -Level "WARN"
            }
        }
    } catch { }

    $scanExitCode = $proc.ExitCode
    $ScanProcess = ($scanOutputList -join "`r`n")

    # Evaluate MpCmdRun exit code:
    # 0 = No threats detected, 2 = Threats detected OR command execution failure / cancellation
    $hasScanError = ($ScanProcess -match '0x[0-9a-fA-F]+' -or $ScanProcess -match '\[Failed\]' -or $ScanProcess -match 'CmdTool: Failed')
    $isAnotherScanBusy = ($ScanProcess -match '0x8050111c' -or $ScanProcess -match 'Another scan already in progress')
    $hasThreatEvidence = ($ScanProcess -match 'threat|amenaza' -and $ScanProcess -notmatch 'found no threats|0 threats')

    if ($scanExitCode -eq 0) {
        Write-AuditEntry "RESULT: Antimalware scan concluded. No threats detected." -Level "SUCCESS" -Metadata @{ 
            ExitCode = $scanExitCode; 
            ScanType = $ScanTypeName 
        }
    } elseif ($isAnotherScanBusy) {
        Write-AuditEntry "RESULT: Could not start scan because another Defender scan was active (Error 0x8050111c)." -Level "WARN" -Metadata @{ 
            ExitCode = $scanExitCode; 
            ScanType = $ScanTypeName;
            ErrorCode = "0x8050111c";
            Note = "Windows Defender reported another scan in progress. Stale locks have been reset."
        }
    } elseif ($hasScanError) {
        Write-AuditEntry "RESULT: Defender engine returned an error during scan execution." -Level "ERROR" -Metadata @{ 
            ExitCode = $scanExitCode; 
            ScanType = $ScanTypeName 
        }
    } elseif ($scanExitCode -eq 2 -and $hasThreatEvidence) {
        Write-AuditEntry "RESULT: Threat detected and remediation invoked by Defender engine." -Level "ERROR" -Metadata @{ 
            ExitCode = $scanExitCode; 
            ScanType = $ScanTypeName;
            Alert    = "ActiveThreatDetected"
        }
    } elseif ($scanExitCode -eq 2) {
        Write-AuditEntry "RESULT: Scan was aborted, cancelled, or rejected by Defender service without threat findings." -Level "WARN" -Metadata @{ 
            ExitCode = $scanExitCode; 
            ScanType = $ScanTypeName;
            Status   = "ScanAbortedOrBusy"
        }
    } else {
        Write-AuditEntry "RESULT: Scan ended with unexpected status code." -Level "WARN" -Metadata @{ 
            ExitCode = $scanExitCode; 
            ScanType = $ScanTypeName 
        }
    }
} catch {
    Write-AuditEntry "CRITICAL EXCEPTION: $($_.Exception.Message)" -Level "ERROR"
}

Write-AuditEntry "=========================================================="

Set-SentinelProgress -Activity "Análisis antimalware (Defender)" -Percent 100 -Status "Análisis $ScanTypeName finalizado exitosamente." -Completed
