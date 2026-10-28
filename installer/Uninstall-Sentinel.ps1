<#
.SYNOPSIS
    Uninstaller for Sentinel-IT Suite
#>
Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan
Write-Host " ISORA CLOUD GROUP | Sentinel-IT Suite Uninstaller" -ForegroundColor Cyan
Write-Host " Copyright & Arquitectura: Sandra Gabriela Puerto Torres (SGPT)" -ForegroundColor Gray
Write-Host " Operaciones de Seguridad: security@isora.com.co" -ForegroundColor DarkGray
Write-Host " GitHub: https://github.com/sandra-puerto | LinkedIn: https://www.linkedin.com/in/sandra-puerto/" -ForegroundColor DarkGray
Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan

if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "[ERROR] This uninstaller must be run as Administrator." -ForegroundColor Red
    Exit
}

Write-Host "[*] Deteniendo proceso del monitor de bandeja si está activo..."
Stop-Process -Name "Sentinel-IT-Tray" -Force -ErrorAction SilentlyContinue

Write-Host "[*] Removing Scheduled Tasks..."
$Tasks = @("Cleanup_Screenshots", "Cleanup_Temp_Global", "Defender_Scan", "Integrity_Check", "BitLocker_Audit", "Event_Audit", "Archive_Downloads", "SystemTray_Monitor")
foreach ($Task in $Tasks) {
    if (Get-ScheduledTask -TaskName "Sentinel-IT\$Task" -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName "Sentinel-IT\$Task" -Confirm:$false
        Write-Host "  -> Removed task: $Task" -ForegroundColor Green
    }
}

Write-Host "[*] Removing Files..."
$InstallDir = if ($PSScriptRoot -and (Test-Path (Join-Path $PSScriptRoot "Sentinel-IT-Tray.exe"))) { $PSScriptRoot } else { "C:\Program Files\Sentinel-IT" }
if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force
    Write-Host "  -> Removed directory: $InstallDir" -ForegroundColor Green
}

# Clean up Windows Registry entries
try {
    $regPath = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Sentinel-IT"
    if (Test-Path $regPath) {
        Remove-Item -Path $regPath -Recurse -Force
        Write-Host "  -> Removed Windows Uninstall registry entry." -ForegroundColor Green
    }
    $runHklm = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run"
    if (Get-ItemProperty -Path $runHklm -Name "SentinelIT_Tray" -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $runHklm -Name "SentinelIT_Tray" -Force -ErrorAction SilentlyContinue
    }
    $runHkcu = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    if (Get-ItemProperty -Path $runHkcu -Name "SentinelIT_Tray" -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $runHkcu -Name "SentinelIT_Tray" -Force -ErrorAction SilentlyContinue
    }
} catch {
    # Non-fatal if key could not be opened
}

Write-Host "`n[SUCCESS] Sentinel-IT has been uninstalled." -ForegroundColor Cyan
Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan
Write-Host " ISORA CLOUD GROUP | Sentinel-IT Suite" -ForegroundColor Cyan
Write-Host " Copyright & Arquitectura: Sandra Gabriela Puerto Torres (SGPT)" -ForegroundColor White
Write-Host " Operaciones de Seguridad: security@isora.com.co" -ForegroundColor Gray
Write-Host " https://github.com/sandra-puerto | https://www.linkedin.com/in/sandra-puerto/" -ForegroundColor DarkGray
Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan
