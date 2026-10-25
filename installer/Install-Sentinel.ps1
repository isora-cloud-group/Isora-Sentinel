<#
.SYNOPSIS
    Enterprise Installer for Sentinel-IT Suite
.DESCRIPTION
    Deploys the modularized scripts to C:\Program Files\IT_Maintenance\
    Registers Scheduled Tasks based on user interaction or silent arguments.
#>
param(
    [switch]$Silent,
    [string]$ScheduleScreenshots = "Daily",
    [string]$ScheduleTemp = "Daily",
    [string]$ScheduleDefender = "Weekly",
    [string]$ScheduleIntegrity = "Weekly"
)

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host " Sentinel-IT Enterprise Installer & Orchestrator" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan

if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "[ERROR] This installer must be run as Administrator." -ForegroundColor Red
    Exit
}

$InstallDir = "C:\Program Files\IT_Maintenance\Sentinel-IT"
$LogsDir = "C:\Program Files\IT_Maintenance\logs"
if (!(Test-Path $InstallDir)) { New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null }
if (!(Test-Path $LogsDir)) { New-Item -ItemType Directory -Path $LogsDir -Force | Out-Null }

Write-Host "[*] Deploying Enterprise Modules..."
Copy-Item -Path "$PSScriptRoot\..\src\*" -Destination $InstallDir -Force -Recurse
Write-Host "[SUCCESS] Modules deployed to $InstallDir" -ForegroundColor Green

Write-Host "`n[*] Scheduled Task Registration" -ForegroundColor Cyan
$RegisterTasks = "Y"
if (!$Silent) {
    $RegisterTasks = Read-Host "Register scheduled tasks? (Y/N) [Default: Y]"
    if ([string]::IsNullOrWhiteSpace($RegisterTasks)) { $RegisterTasks = "Y" }
}

if ($RegisterTasks -match "^y") {
    $Principal = New-ScheduledTaskPrincipal -UserId "NT AUTHORITY\SYSTEM" -LogonType ServiceAccount -RunLevel Highest

    function Register-SentinelTask {
        param([string]$TaskName, [string]$ScriptFile, [string]$DefaultTrigger)
        
        Write-Host "`nConfiguring: $TaskName" -ForegroundColor Yellow
        $TriggerInput = $DefaultTrigger
        if (!$Silent) {
            $InputStr = Read-Host "Enter schedule (Daily, Weekly) [Default: $DefaultTrigger]"
            if (![string]::IsNullOrWhiteSpace($InputStr)) { $TriggerInput = $InputStr }
        }

        # Handle existing task edge-case
        if (Get-ScheduledTask -TaskName "Sentinel-IT\$TaskName" -ErrorAction SilentlyContinue) {
            Unregister-ScheduledTask -TaskName "Sentinel-IT\$TaskName" -Confirm:$false
        }

        $Trigger = if ($TriggerInput -eq "Weekly") { New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At 2am } else { New-ScheduledTaskTrigger -Daily -At 3am }
        $Action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$InstallDir\engines\$ScriptFile`""
        
        Register-ScheduledTask -TaskName "Sentinel-IT\$TaskName" -Action $Action -Trigger $Trigger -Principal $Principal -Force | Out-Null
        Write-Host "[SUCCESS] Task '$TaskName' registered ($TriggerInput)." -ForegroundColor Green
    }

    Register-SentinelTask -TaskName "Cleanup_Screenshots" -ScriptFile "Invoke-CleanupScreenshots.ps1" -DefaultTrigger $ScheduleScreenshots
    Register-SentinelTask -TaskName "Cleanup_Temp_Global" -ScriptFile "Invoke-CleanupTempGlobal.ps1" -DefaultTrigger $ScheduleTemp
    Register-SentinelTask -TaskName "Defender_Scan" -ScriptFile "Invoke-DefenderScan.ps1" -DefaultTrigger $ScheduleDefender
    Register-SentinelTask -TaskName "Integrity_Check" -ScriptFile "Invoke-IntegrityCheck.ps1" -DefaultTrigger $ScheduleIntegrity
    
    Write-Host "`n[SUCCESS] Orchestration Complete!" -ForegroundColor Cyan
} else {
    Write-Host "`n[INFO] Installation finished. Tasks were not registered." -ForegroundColor Cyan
}
