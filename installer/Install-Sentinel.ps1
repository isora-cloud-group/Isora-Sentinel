<#
.SYNOPSIS
    Enterprise Installer for Sentinel-IT Suite
.DESCRIPTION
    Deploys the modularized scripts to C:\Program Files\Sentinel-IT\
    Registers Scheduled Tasks based on user interaction or silent arguments.
#>
param(
    [switch]$Silent,
    [string]$ScheduleScreenshots = "Daily",
    [string]$ScheduleTemp = "Daily",
    [string]$ScheduleDefender = "Weekly",
    [string]$ScheduleIntegrity = "Weekly",
    [string]$ScheduleBitLocker = "Daily",
    [string]$ScheduleEventAudit = "Daily"
)

Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan
Write-Host " ISORA CLOUD GROUP | Sentinel-IT Suite" -ForegroundColor Cyan
Write-Host " Copyright & Arquitectura: Sandra Gabriela Puerto Torres (SGPT)" -ForegroundColor Gray
Write-Host " Implementación: Google Antigravity AI (Pair Programming)" -ForegroundColor DarkGray
Write-Host " GitHub: https://github.com/sandra-puerto | LinkedIn: https://www.linkedin.com/in/sandra-puerto/" -ForegroundColor DarkGray
Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan

if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "[ERROR] This installer must be run as Administrator." -ForegroundColor Red
    Exit
}

$InstallDir = "C:\Program Files\Sentinel-IT"
$LogsDir = "C:\Program Files\Sentinel-IT\logs"
if (!(Test-Path $InstallDir)) { New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null }
if (!(Test-Path $LogsDir)) { New-Item -ItemType Directory -Path $LogsDir -Force | Out-Null }

Write-Host "[*] Deploying Enterprise Modules..."
Copy-Item -Path "$PSScriptRoot\..\src\*" -Destination $InstallDir -Force -Recurse
if (Test-Path "$PSScriptRoot\..\LICENSE") {
    Copy-Item -Path "$PSScriptRoot\..\LICENSE" -Destination (Join-Path $InstallDir "LICENSE") -Force
}
if (Test-Path "$PSScriptRoot\..\integrity.manifest.json") {
    Copy-Item -Path "$PSScriptRoot\..\integrity.manifest.json" -Destination (Join-Path $InstallDir "integrity.manifest.json") -Force
}
Copy-Item -Path "$PSScriptRoot\Uninstall-Sentinel.ps1" -Destination (Join-Path $InstallDir "Uninstall-Sentinel.ps1") -Force
Write-Host "[SUCCESS] Modules deployed to $InstallDir" -ForegroundColor Green

# Register in Windows Add/Remove Programs (Registry)
try {
    $regPath = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Sentinel-IT"
    if (-not (Test-Path $regPath)) { New-Item -Path $regPath -Force | Out-Null }
    Set-ItemProperty -Path $regPath -Name "DisplayName" -Value "Sentinel-IT Suite (Enterprise Endpoint Hygiene)" -Force
    Set-ItemProperty -Path $regPath -Name "DisplayVersion" -Value "2.2.0" -Force
    Set-ItemProperty -Path $regPath -Name "Publisher" -Value "Sandra Gabriela Puerto Torres" -Force
    Set-ItemProperty -Path $regPath -Name "InstallLocation" -Value $InstallDir -Force
    Set-ItemProperty -Path $regPath -Name "UninstallString" -Value "PowerShell.exe -NoProfile -ExecutionPolicy Bypass -File `"$InstallDir\Uninstall-Sentinel.ps1`"" -Force
    Set-ItemProperty -Path $regPath -Name "NoModify" -Value 1 -Type DWord -Force
    Set-ItemProperty -Path $regPath -Name "NoRepair" -Value 1 -Type DWord -Force
} catch {
    Write-Host "[WARN] Could not register in Windows Uninstall registry: $($_.Exception.Message)" -ForegroundColor Yellow
}

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
    Register-SentinelTask -TaskName "BitLocker_Audit" -ScriptFile "Invoke-BitLockerAudit.ps1" -DefaultTrigger $ScheduleBitLocker
    Register-SentinelTask -TaskName "Event_Audit" -ScriptFile "Invoke-EventAudit.ps1" -DefaultTrigger $ScheduleEventAudit
    
    Write-Host "`n[SUCCESS] Orchestration Complete!" -ForegroundColor Cyan
} else {
    Write-Host "`n[INFO] Installation finished. Tasks were not registered." -ForegroundColor Cyan
}

Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan
Write-Host " ISORA CLOUD GROUP | Sentinel-IT Suite" -ForegroundColor Cyan
Write-Host " Copyright & Arquitectura: Sandra Gabriela Puerto Torres (SGPT)" -ForegroundColor White
Write-Host " Powered by: Google Antigravity AI" -ForegroundColor Gray
Write-Host " https://github.com/sandra-puerto | https://www.linkedin.com/in/sandra-puerto/" -ForegroundColor DarkGray
Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan
