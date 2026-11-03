<#
.SYNOPSIS
    System Integrity Audit Engine - ISO/IEC 27001:2022 Control A.8.20
.DESCRIPTION
    Audits the Windows Component Store (WinSxS) integrity via DISM.exe /ScanHealth.
    Captures DISM return codes (0 = Healthy, 20 = Repairable Corruption, 3010 = Reboot Needed)
    and supports optional -AutoRepair parameter to execute /RestoreHealth.
#>
[CmdletBinding()]
param(
    [switch]$AutoRepair,
    [switch]$QuickCheck
)

$ErrorActionPreference = "Continue" 
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Logger.psm1")
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Core.psm1")

Initialize-SentinelLog -EngineName "ISO_Integrity_Check"
Set-SentinelProgress -Activity "Comprobación de Windows (DISM)" -Percent 5 -Status "Iniciando motor de comprobación e integridad..."
try { 
    $null = Assert-SentinelIntegrity 
} catch { 
    Write-AuditEntry "INTEGRITY CRITICAL (HALTED): $($_.Exception.Message)" -Level "ERROR" -Metadata @{ SecurityViolation = "TamperDetected"; Aborted = "True" }
    throw 
}

Write-AuditEntry "=========================================================="
Write-AuditEntry "   [ISO_Integrity_Check] - SYSTEM INTEGRITY AUDIT START"
if ($AutoRepair) {
    Write-AuditEntry "   CONFIGURATION: AutoRepair enabled (will dispatch /RestoreHealth if needed)" -Level "WARN"
}
Write-AuditEntry "=========================================================="

try {
    $IsAdmin = Test-IsAdmin
    if (-not $IsAdmin) {
        Write-AuditEntry "AUDIT NOTICE: Component Store integrity scan via DISM requires Administrator or SYSTEM privileges. Execution skipped in standard user context." -Level "WARN" -Metadata @{ User = $env:USERNAME }
        return
    }

    $sysRoot = if ($env:SystemRoot) { $env:SystemRoot } else { "C:\WINDOWS" }
    $dismPath = Join-Path $sysRoot "System32\dism.exe"
    if (-not (Test-Path -LiteralPath $dismPath)) {
        $sysnativeDism = Join-Path $sysRoot "Sysnative\dism.exe"
        if (Test-Path -LiteralPath $sysnativeDism) {
            $dismPath = $sysnativeDism
        } else {
            $dismPath = "dism.exe"
        }
    }

    Write-AuditEntry "AUDIT: Dispatching DISM online scan health event..." -Metadata @{ DismPath = $dismPath }
    $dismArg = if ($QuickCheck) { "/CheckHealth" } elseif ($AutoRepair) { "/RestoreHealth" } else { "/ScanHealth" }
    Write-AuditEntry "AUDIT: Dispatching DISM online scan mode: $dismArg..."
    $actDesc = if ($AutoRepair) { "Reparación de Windows (DISM)" } else { "Comprobación de Windows (DISM)" }
    Set-SentinelProgress -Activity $actDesc -Percent 15 -Status "Ejecutando DISM $dismArg en segundo plano..."
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $dismPath
    $psi.Arguments = "/Online /Cleanup-Image $dismArg"
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true

    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo = $psi
    $dismOutputList = New-Object System.Collections.Generic.List[string]

    [void]$proc.Start()
    $outTask = $proc.StandardOutput.ReadLineAsync()
    $errTask = $proc.StandardError.ReadLineAsync()

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $reportedPct = -1.0

    while (-not $proc.WaitForExit(1000)) {
        while ($null -ne $outTask -and $outTask.IsCompleted) {
            if (-not $outTask.IsFaulted -and -not $outTask.IsCanceled) {
                $line = $outTask.Result
                if ($null -ne $line) {
                    if (-not [string]::IsNullOrWhiteSpace($line)) {
                        $trimmed = $line.Trim()
                        $dismOutputList.Add($trimmed)
                        Write-AuditEntry $trimmed -Level "INFO"
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
                        $dismOutputList.Add($trimmedErr)
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

        $timeStr = $sw.Elapsed.ToString("mm\:ss")
        if ($reportedPct -ge 0) {
            $clamped = [int][math]::Min(98, [math]::Max(15, $reportedPct))
            Set-SentinelProgress -Activity $actDesc -Percent $clamped -Status "DISM $($dismArg): $([math]::Round($reportedPct, 1))% ($timeStr)"
        } else {
            $elapsed = $sw.Elapsed.TotalSeconds
            $dynPct = [int][math]::Min(90, 15 + ($elapsed / 8))
            Set-SentinelProgress -Activity $actDesc -Percent $dynPct -Status "Ejecutando DISM $dismArg ($timeStr)..."
        }
    }

    try {
        if ($null -ne $outTask -and $outTask.Wait(500) -and $null -ne $outTask.Result) {
            $remLine = $outTask.Result.Trim()
            if ($remLine) {
                $dismOutputList.Add($remLine)
                Write-AuditEntry $remLine -Level "INFO"
            }
        }
        while (-not $proc.StandardOutput.EndOfStream) {
            $rem = $proc.StandardOutput.ReadLine()
            if (-not [string]::IsNullOrWhiteSpace($rem)) {
                $trimmedRem = $rem.Trim()
                $dismOutputList.Add($trimmedRem)
                Write-AuditEntry $trimmedRem -Level "INFO"
            }
        }
        while (-not $proc.StandardError.EndOfStream) {
            $remErr = $proc.StandardError.ReadLine()
            if (-not [string]::IsNullOrWhiteSpace($remErr)) {
                $trimmedRemErr = $remErr.Trim()
                $dismOutputList.Add($trimmedRemErr)
                Write-AuditEntry $trimmedRemErr -Level "WARN"
            }
        }
    } catch { }

    $dismCode = $proc.ExitCode
    $dismResult = ($dismOutputList -join "`r`n")

    # Evaluate DISM standard exit codes:
    # 0 = No corruption, 20 = Corruption detected and repairable, 740 = Elevation required, 3010 = Reboot required
    switch ($dismCode) {
        0 {
            Write-AuditEntry "RESULT: Component Store is verified healthy. No corruption detected." -Level "SUCCESS" -Metadata @{ 
                ExitCode = 0; 
                HealthStatus = "Healthy" 
            }
        }
        20 {
            Write-AuditEntry "RESULT: Component Store corruption detected (repairable)." -Level "WARN" -Metadata @{ 
                ExitCode = 20; 
                HealthStatus = "CorruptedRepairable" 
            }

            if ($AutoRepair) {
                Write-AuditEntry "REMEDIATION: AutoRepair invoked. Dispatching DISM /RestoreHealth..." -Level "WARN"
                $restoreResult = & $dismPath /Online /Cleanup-Image /RestoreHealth 2>&1 | Out-String
                $restoreCode = $LASTEXITCODE
                $restoreLevel = if ($restoreCode -eq 0) { "SUCCESS" } else { "ERROR" }
                Write-AuditEntry "REMEDIATION: RestoreHealth concluded." -Level $restoreLevel -Metadata @{ 
                    ExitCode = $restoreCode 
                }
            }
        }
        740 {
            Write-AuditEntry "RESULT: DISM operation failed because administrative elevation is required (Exit code 740)." -Level "WARN" -Metadata @{ 
                ExitCode = 740; 
                HealthStatus = "ElevationRequired" 
            }
        }
        3010 {
            Write-AuditEntry "RESULT: Component Store scan requires system restart to finalize operations." -Level "WARN" -Metadata @{ 
                ExitCode = 3010; 
                HealthStatus = "RebootRequired" 
            }
        }
        default {
            Write-AuditEntry "RESULT: DISM operation ended with non-zero exit code." -Level "ERROR" -Metadata @{ 
                ExitCode = $dismCode; 
                HealthStatus = "UnknownFailure" 
            }
        }
    }
} catch {
    Write-AuditEntry "AUDIT FAILURE: DISM engine exception: $($_.Exception.Message)" -Level "ERROR"
}

Write-AuditEntry "=========================================================="

$finalAct = if ($AutoRepair) { "Reparación de Windows (DISM)" } else { "Comprobación de Windows (DISM)" }
$finalMsg = if ($dismCode -eq 0) { "Comprobación/reparación finalizada con éxito." } else { "Proceso de componentes finalizado (Código $dismCode)." }
Set-SentinelProgress -Activity $finalAct -Percent 100 -Status $finalMsg -Completed
