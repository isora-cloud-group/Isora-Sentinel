<#
.SYNOPSIS
    Uninstaller for Isora Sentinel Suite (Bilingual English / Spanish)
.DESCRIPTION
    Safely unregisters scheduled tasks, removes software files, and cleans registry entries.
    Supports -Language "ES" or "EN" (auto-detected from system culture).
#>
param(
    [string]$Language = "",
    [switch]$Silent
)

# Language resolution: explicit parameter > OS culture > Spanish default
$isEn = if ($Language -match "^(?i:en)") { $true } elseif ($Language -match "^(?i:es)") { $false } else { ((Get-Culture).TwoLetterISOLanguageName -ne "es") }
function Msg([string]$es, [string]$en) { if ($isEn) { return $en } else { return $es } }

Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan
Write-Host " ISORA CLOUD GROUP | Isora Sentinel Suite Uninstaller" -ForegroundColor Cyan
Write-Host " Copyright & Arquitectura: Sandra Gabriela Puerto Torres (SGPT)" -ForegroundColor Gray
Write-Host " Operaciones de Seguridad: security@isora.com.co" -ForegroundColor DarkGray
Write-Host " GitHub: https://github.com/sandra-puerto | LinkedIn: https://www.linkedin.com/in/sandra-puerto/" -ForegroundColor DarkGray
Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan

if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host (Msg "[ERROR] Este desinstalador debe ejecutarse como Administrador." "[ERROR] This uninstaller must be run as Administrator.") -ForegroundColor Red
    Exit
}

Write-Host (Msg "[*] Deteniendo proceso del monitor de bandeja si está activo..." "[*] Stopping System Tray monitor process if running...")
Stop-Process -Name "Isora-Sentinel-Tray" -Force -ErrorAction SilentlyContinue
Stop-Process -Name "Sentinel-IT-Tray" -Force -ErrorAction SilentlyContinue

Write-Host (Msg "[*] Eliminando Tareas Programadas..." "[*] Removing Scheduled Tasks...")
$Tasks = @("Cleanup_Screenshots", "Cleanup_Temp_Global", "Defender_Scan", "Integrity_Check", "BitLocker_Audit", "Event_Audit", "Archive_Downloads", "SystemTray_Monitor")
$TaskFolders = @("Isora Sentinel", "Isora-Sentinel", "Sentinel-IT")
foreach ($Folder in $TaskFolders) {
    foreach ($Task in $Tasks) {
        $taskPath = "$Folder\$Task"
        if (Get-ScheduledTask -TaskName $taskPath -ErrorAction SilentlyContinue) {
            Unregister-ScheduledTask -TaskName $taskPath -Confirm:$false
            Write-Host (Msg "  -> Tarea eliminada: $taskPath" "  -> Removed task: $taskPath") -ForegroundColor Green
        }
    }
}

Write-Host (Msg "[*] Eliminando Archivos de Instalación..." "[*] Removing Files...")
$regInstallLoc = $null
try {
    $uninst = Get-ItemProperty "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Isora-Sentinel" -ErrorAction SilentlyContinue
    if ($uninst -and $uninst.InstallLocation) { $regInstallLoc = $uninst.InstallLocation }
} catch { }

$InstallDir = if ($regInstallLoc -and (Test-Path -LiteralPath $regInstallLoc)) { 
    $regInstallLoc 
} elseif ($PSScriptRoot -and (Test-Path -LiteralPath $PSScriptRoot) -and ($PSScriptRoot -notmatch "[\\/]installer$")) { 
    $PSScriptRoot 
} else { 
    "C:\Program Files\Isora-Sentinel" 
}

    # Defensive guard: prevent arbitrary directory wipe if registry or path was manipulated
    $forbiddenRoots = @("C:\", "C:\Windows", "C:\Program Files", "C:\Program Files (x86)", "C:\Users", "C:\ProgramData")
    $normalizedTarget = [System.IO.Path]::GetFullPath($InstallDir).TrimEnd('\', '/')
    $isForbidden = $false
    foreach ($root in $forbiddenRoots) {
        $normRoot = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/')
        if ($normalizedTarget -ieq $normRoot) {
            $isForbidden = $true
            break
        }
    }
    if ($isForbidden -or $normalizedTarget.Length -lt 10 -or ($normalizedTarget -notmatch '(?i)Isora|Sentinel')) {
        Write-Host (Msg "[ERROR] Ubicacion de desinstalacion invalida o protegida: $InstallDir" "[ERROR] Invalid or protected uninstall directory: $InstallDir") -ForegroundColor Red
    } else {
        if (Test-Path -LiteralPath $InstallDir) {
            $dirItem = Get-Item -LiteralPath $InstallDir -Force -ErrorAction SilentlyContinue
            if ($dirItem -and ($dirItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
                Write-Host (Msg "[ERROR] El directorio de instalacion es un reparse point (junction). Omitiendo." "[ERROR] Install directory is a reparse point (junction). Skipping.") -ForegroundColor Red
            } else {
                Remove-Item -LiteralPath $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
                Write-Host (Msg "  -> Directorio eliminado: $InstallDir" "  -> Removed directory: $InstallDir") -ForegroundColor Green
            }
        }
    }

# Clean up Windows Registry entries
try {
    $regKeys = @(
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Isora-Sentinel",
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Sentinel-IT"
    )
    foreach ($rk in $regKeys) {
        if (Test-Path $rk) {
            Remove-Item -Path $rk -Recurse -Force
            Write-Host (Msg "  -> Clave de registro eliminada: $rk" "  -> Removed Windows Uninstall registry entry: $rk") -ForegroundColor Green
        }
    }
    $runHklm = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run"
    $runHkcu = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    $runValues = @("SentinelIT_Tray", "IsoraSentinel_Tray", "Isora-Sentinel-Tray", "Sentinel-IT-Tray")
    foreach ($v in $runValues) {
        if (Get-ItemProperty -Path $runHklm -Name $v -ErrorAction SilentlyContinue) {
            Remove-ItemProperty -Path $runHklm -Name $v -Force -ErrorAction SilentlyContinue
        }
        if (Get-ItemProperty -Path $runHkcu -Name $v -ErrorAction SilentlyContinue) {
            Remove-ItemProperty -Path $runHkcu -Name $v -Force -ErrorAction SilentlyContinue
        }
    }
} catch {
    # Non-fatal if key could not be opened
}

Write-Host (Msg "`n[SUCCESS] Isora Sentinel ha sido desinstalado correctamente." "`n[SUCCESS] Isora Sentinel has been successfully uninstalled.") -ForegroundColor Cyan
Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan
Write-Host " ISORA CLOUD GROUP | Isora Sentinel Suite" -ForegroundColor Cyan
Write-Host " Copyright & Arquitectura: Sandra Gabriela Puerto Torres (SGPT)" -ForegroundColor White
Write-Host " Operaciones de Seguridad: security@isora.com.co" -ForegroundColor Gray
Write-Host " https://github.com/sandra-puerto | https://www.linkedin.com/in/sandra-puerto/" -ForegroundColor DarkGray
