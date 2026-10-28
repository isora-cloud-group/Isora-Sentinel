<#
.SYNOPSIS
    Uninstaller for Sentinel-IT Suite
#>
Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan
Write-Host " ISORA CLOUD GROUP | Sentinel-IT Suite Uninstaller" -ForegroundColor Cyan
Write-Host " Copyright & Arquitectura: Sandra Gabriela Puerto Torres (SGPT)" -ForegroundColor Gray
Write-Host " Implementación: Google Antigravity AI (Pair Programming)" -ForegroundColor DarkGray
Write-Host " GitHub: https://github.com/sandra-puerto | LinkedIn: https://www.linkedin.com/in/sandra-puerto/" -ForegroundColor DarkGray
Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan

if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "[ERROR] This uninstaller must be run as Administrator." -ForegroundColor Red
    Exit
}

Write-Host "[*] Removing Scheduled Tasks..."
$Tasks = @("Cleanup_Screenshots", "Cleanup_Temp_Global", "Defender_Scan", "Integrity_Check", "BitLocker_Audit", "Event_Audit")
foreach ($Task in $Tasks) {
    if (Get-ScheduledTask -TaskName "Sentinel-IT\$Task" -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName "Sentinel-IT\$Task" -Confirm:$false
        Write-Host "  -> Removed task: $Task" -ForegroundColor Green
    }
}

Write-Host "[*] Removing Files..."
$InstallDir = "C:\Program Files\IT_Maintenance\Sentinel-IT"
if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force
    Write-Host "  -> Removed directory: $InstallDir" -ForegroundColor Green
}

Write-Host "`n[SUCCESS] Sentinel-IT has been uninstalled." -ForegroundColor Cyan
Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan
Write-Host " ISORA CLOUD GROUP | Sentinel-IT Suite" -ForegroundColor Cyan
Write-Host " Copyright & Arquitectura: Sandra Gabriela Puerto Torres (SGPT)" -ForegroundColor White
Write-Host " Powered by: Google Antigravity AI" -ForegroundColor Gray
Write-Host " https://github.com/sandra-puerto | https://www.linkedin.com/in/sandra-puerto/" -ForegroundColor DarkGray
Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan
