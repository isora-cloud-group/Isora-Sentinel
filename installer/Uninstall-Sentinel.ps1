<#
.SYNOPSIS
    Uninstaller for Sentinel-IT Suite
#>
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host " Sentinel-IT Uninstaller" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan

if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "[ERROR] This uninstaller must be run as Administrator." -ForegroundColor Red
    Exit
}

Write-Host "[*] Removing Scheduled Tasks..."
$Tasks = @("Cleanup_Screenshots", "Cleanup_Temp_Global", "Defender_Scan", "Integrity_Check")
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
