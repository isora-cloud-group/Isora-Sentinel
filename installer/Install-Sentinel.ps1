<#
.SYNOPSIS
    Enterprise Installer for Isora Sentinel Suite (Bilingual English / Spanish)
.DESCRIPTION
    Deploys the modularized scripts to C:\Program Files\Isora-Sentinel\
    Registers Scheduled Tasks based on user interaction or silent arguments.
    Enforces strict NTFS ACLs (LPE Defense) and enterprise Task Scheduler resilience
    (ExecutionTimeLimit, StartWhenAvailable, and MultipleInstancesPolicy).
    Supports bilingual console localization (-Language "ES" or "EN", auto-detected from OS culture).
#>
param(
    [switch]$Silent,
    [string]$Language = "",
    [string]$InstallDir = "C:\Program Files\Isora-Sentinel",
    [string]$LogsDir = "",
    [switch]$SkipIntegrityCheck,
    [string]$ScheduleScreenshots = "Daily",
    [string]$ScheduleTemp = "Daily",
    [string]$ScheduleDefender = "Weekly",
    [string]$ScheduleIntegrity = "Weekly",
    [string]$ScheduleBitLocker = "Daily",
    [string]$ScheduleEventAudit = "Daily",
    [string]$ScheduleDownloads = "Monthly",
    [string]$DownloadsArchiveDir = "%USERPROFILE%\Documents\Isora_Sentinel_Archived\Downloads",
    [switch]$ApplyScreenshots,
    [switch]$ApplyTemp,
    [switch]$ApplyDownloads
)

# Language resolution: explicit parameter > OS culture > Spanish default
$isEn = if ($Language -match "^(?i:en)") { $true } elseif ($Language -match "^(?i:es)") { $false } else { ((Get-Culture).TwoLetterISOLanguageName -ne "es") }
function Msg([string]$es, [string]$en) { if ($isEn) { return $en } else { return $es } }

Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan
Write-Host " ISORA CLOUD GROUP | Isora Sentinel Suite" -ForegroundColor Cyan
Write-Host " Copyright: Sandra Gabriela Puerto Torres (SGPT)" -ForegroundColor Gray
Write-Host " Operaciones de Seguridad: security@isora.com.co" -ForegroundColor DarkGray
Write-Host " GitHub: https://github.com/sandra-puerto | LinkedIn: https://www.linkedin.com/in/sandra-puerto/" -ForegroundColor DarkGray
Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan

if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host (Msg "[ERROR] Este instalador debe ejecutarse como Administrador." "[ERROR] This installer must be run as Administrator.") -ForegroundColor Red
    Exit
}

Write-Host (Msg "[*] Deteniendo instancias previas en ejecución..." "[*] Stopping previous running instances...")
$candidateProcesses = @("Isora-Sentinel-Tray", "IsoraSentinel-Tray", "IsoraSentinelTray", "SentinelTray", "Isora-Sentinel-Uninstall")
foreach ($pName in $candidateProcesses) {
    Get-Process -Name $pName -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            $_.CloseMainWindow() | Out-Null
            Start-Sleep -Milliseconds 400
            if (!$_.HasExited) { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
        } catch {
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        }
    }
}

# Limpiar entradas de autoinicio obsoletas o corruptas en registro
$runKeys = @("HKLM:\Software\Microsoft\Windows\CurrentVersion\Run", "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run")
$runNames = @("IsoraSentinel_Tray", "Isora-Sentinel-Tray", "SentinelTray")
foreach ($rk in $runKeys) {
    if (Test-Path $rk) {
        foreach ($rn in $runNames) {
            $val = (Get-ItemProperty -Path $rk -Name $rn -ErrorAction SilentlyContinue).$rn
            if ($val -and ($val -match '\(Join-Path' -or !(Test-Path ($val -replace '"','').Trim()))) {
                Remove-ItemProperty -Path $rk -Name $rn -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

# Limpiar colmenas de usuario cargadas en HKEY_USERS
Get-ChildItem -Path "Registry::HKEY_USERS" -ErrorAction SilentlyContinue | ForEach-Object {
    $uRun = Join-Path $_.PSPath "Software\Microsoft\Windows\CurrentVersion\Run"
    if (Test-Path $uRun) {
        foreach ($rn in $runNames) {
            $uVal = (Get-ItemProperty -Path $uRun -Name $rn -ErrorAction SilentlyContinue).$rn
            if ($uVal -and ($uVal -match '\(Join-Path' -or !(Test-Path ($uVal -replace '"','').Trim()))) {
                Remove-ItemProperty -Path $uRun -Name $rn -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

$effectiveLogsDir = if ($LogsDir) { $LogsDir } else { Join-Path $InstallDir "logs" }
if (!(Test-Path -LiteralPath $InstallDir)) { New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null }
if (!(Test-Path -LiteralPath $effectiveLogsDir)) { New-Item -ItemType Directory -Path $effectiveLogsDir -Force | Out-Null }
$LogsDir = $effectiveLogsDir

Write-Host (Msg "[*] Desplegando módulos corporativos..." "[*] Deploying Enterprise Modules...")
Copy-Item -Path "$PSScriptRoot\..\src\*" -Destination $InstallDir -Force -Recurse
if (Test-Path "$PSScriptRoot\..\LICENSE") {
    Copy-Item -Path "$PSScriptRoot\..\LICENSE" -Destination (Join-Path $InstallDir "LICENSE") -Force
}
if (Test-Path "$PSScriptRoot\..\NOTICE") {
    Copy-Item -Path "$PSScriptRoot\..\NOTICE" -Destination (Join-Path $InstallDir "NOTICE") -Force
}
if (Test-Path "$PSScriptRoot\..\integrity.manifest.json") {
    Copy-Item -Path "$PSScriptRoot\..\integrity.manifest.json" -Destination (Join-Path $InstallDir "integrity.manifest.json") -Force
}
Copy-Item -Path "$PSScriptRoot\Uninstall-Sentinel.ps1" -Destination (Join-Path $InstallDir "Uninstall-Sentinel.ps1") -Force

# Copiar ejecutables y recursos de soporte si existen
if (Test-Path "$PSScriptRoot\Isora-Sentinel-Tray.exe") {
    Copy-Item -Path "$PSScriptRoot\Isora-Sentinel-Tray.exe" -Destination (Join-Path $InstallDir "Isora-Sentinel-Tray.exe") -Force
}
if (Test-Path "$PSScriptRoot\src\app.ico") {
    Copy-Item -Path "$PSScriptRoot\src\app.ico" -Destination (Join-Path $InstallDir "app.ico") -Force
} elseif (Test-Path "$PSScriptRoot\app.ico") {
    Copy-Item -Path "$PSScriptRoot\app.ico" -Destination (Join-Path $InstallDir "app.ico") -Force
}

# Crear accesos directos en Menú Inicio y Startup (búsqueda de Windows y autoinicio garantizado)
try {
    $commonPrograms = [Environment]::GetFolderPath([Environment+SpecialFolder]::CommonPrograms)
    if (-not $commonPrograms -or -not (Test-Path $commonPrograms)) {
        $commonPrograms = "C:\ProgramData\Microsoft\Windows\Start Menu\Programs"
    }
    $sentinelStartFolder = Join-Path $commonPrograms "Isora Sentinel"
    if (-not (Test-Path $sentinelStartFolder)) { New-Item -ItemType Directory -Path $sentinelStartFolder -Force | Out-Null }

    $wsh = New-Object -ComObject WScript.Shell
    $trayExe = Join-Path $InstallDir "Isora-Sentinel-Tray.exe"
    $appIco = Join-Path $InstallDir "app.ico"
    if (Test-Path $trayExe) {
        $sc = $wsh.CreateShortcut((Join-Path $sentinelStartFolder "Isora Sentinel.lnk"))
        $sc.TargetPath = $trayExe
        $sc.WorkingDirectory = $InstallDir
        $sc.Description = "Isora Sentinel Suite - Monitor y Observabilidad"
        if (Test-Path $appIco) { $sc.IconLocation = "$appIco,0" }
        $sc.Save()

        # Autoinicio en carpeta Startup
        $commonStartup = [Environment]::GetFolderPath([Environment+SpecialFolder]::CommonStartup)
        if (Test-Path $commonStartup) {
            $scStartup = $wsh.CreateShortcut((Join-Path $commonStartup "Isora Sentinel.lnk"))
            $scStartup.TargetPath = $trayExe
            $scStartup.WorkingDirectory = $InstallDir
            $scStartup.Description = "Isora Sentinel Suite Tray Monitor"
            if (Test-Path $appIco) { $scStartup.IconLocation = "$appIco,0" }
            $scStartup.Save()
        }

        # Autoinicio en registro HKLM Run
        Set-ItemProperty -Path "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "IsoraSentinel_Tray" -Value "`"$trayExe`"" -Force -ErrorAction SilentlyContinue
    }
} catch { }

Write-Host (Msg "[SUCCESS] Módulos desplegados en $InstallDir" "[SUCCESS] Modules deployed to $InstallDir") -ForegroundColor Green

# Enforce strict enterprise NTFS ACLs (ISO 27001 Control A.5.15 / Anti-Tampering / LPE Defense)
Write-Host (Msg "[*] Asegurando listas de control de acceso NTFS (LPE Defense)..." "[*] Hardening NTFS Access Control Lists (LPE Defense)...")
try {
    # *S-1-5-32-544 (Administrators: Full), *S-1-5-18 (SYSTEM: Full), *S-1-5-32-545 (Users: Read/Execute only)
    & icacls.exe "$InstallDir" /grant "*S-1-5-32-544:(OI)(CI)F" "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-545:(OI)(CI)RX" /inheritance:r /T /Q 2>$null
    # Central audit logs must NOT have Modify permissions for unprivileged users (prevents log tampering and junction attacks)
    & icacls.exe "$LogsDir" /grant "*S-1-5-32-544:(OI)(CI)F" "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-545:(OI)(CI)RX" /inheritance:r /T /Q 2>$null
    $sharedStatusDir = Join-Path $env:ProgramData "Isora-Sentinel\status"
    if (-not (Test-Path -LiteralPath $sharedStatusDir)) { New-Item -ItemType Directory -Path $sharedStatusDir -Force | Out-Null }
    & icacls.exe "$sharedStatusDir" /grant "*S-1-5-32-544:(OI)(CI)F" "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-545:(OI)(CI)M" /T /Q 2>$null
    Write-Host (Msg "[SUCCESS] Permisos NTFS asegurados para Administradores/SYSTEM con derechos restringidos para Usuarios." "[SUCCESS] NTFS ACLs locked down to Administrators/SYSTEM with restricted User rights.") -ForegroundColor Green
} catch {
    Write-Host (Msg "[WARN] No se pudo aplicar el aseguramiento estricto de icacls: $($_.Exception.Message)" "[WARN] Could not apply strict icacls hardening: $($_.Exception.Message)") -ForegroundColor Yellow
}

# Register in Windows Add/Remove Programs (Registry)
try {
    $regPath = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Isora-Sentinel"
    if (-not (Test-Path $regPath)) { New-Item -Path $regPath -Force | Out-Null }
    Set-ItemProperty -Path $regPath -Name "DisplayName" -Value "Isora Sentinel Suite (Endpoint Auditing & Governance)" -Force
    Set-ItemProperty -Path $regPath -Name "DisplayVersion" -Value "2.5.0" -Force
    Set-ItemProperty -Path $regPath -Name "Publisher" -Value "Sandra Gabriela Puerto Torres" -Force
    Set-ItemProperty -Path $regPath -Name "InstallLocation" -Value $InstallDir -Force
    Set-ItemProperty -Path $regPath -Name "UninstallString" -Value "PowerShell.exe -NoProfile -ExecutionPolicy Bypass -File `"$InstallDir\Uninstall-Sentinel.ps1`"" -Force
    Set-ItemProperty -Path $regPath -Name "NoModify" -Value 1 -Type DWord -Force
    Set-ItemProperty -Path $regPath -Name "NoRepair" -Value 1 -Type DWord -Force
} catch {
    Write-Host (Msg "[WARN] No se pudo registrar en la clave de desinstalación de Windows: $($_.Exception.Message)" "[WARN] Could not register in Windows Uninstall registry: $($_.Exception.Message)") -ForegroundColor Yellow
}

Write-Host (Msg "`n[*] Registro de Tareas Programadas" "`n[*] Scheduled Task Registration") -ForegroundColor Cyan
$RegisterTasks = "Y"
if (!$Silent) {
    $promptMsg = Msg " ¿Registrar tareas programadas? (S/N) [Por defecto: S]" " Register scheduled tasks? (Y/N) [Default: Y]"
    $RegisterTasks = Read-Host $promptMsg
    if ([string]::IsNullOrWhiteSpace($RegisterTasks)) { $RegisterTasks = "Y" }
}

if ($RegisterTasks -match "^(y|s)") {
    $Principal = New-ScheduledTaskPrincipal -UserId "NT AUTHORITY\SYSTEM" -LogonType ServiceAccount -RunLevel Highest

    # Unattended resilience: timeout hung tasks, wake on available, prevent overlaps
    $TaskSettings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Hours 2) -StartWhenAvailable -MultipleInstancesPolicy IgnoreNew -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 15)

    function Register-SentinelTask {
        param(
            [string]$TaskName, 
            [string]$ScriptFile, 
            [string]$DefaultTrigger,
            [string]$ExtraArgs = ""
        )
        
        Write-Host "`n" + (Msg "Configurando: $TaskName" "Configuring: $TaskName") -ForegroundColor Yellow
        $TriggerInput = $DefaultTrigger
        if (!$Silent) {
            $schedPrompt = Msg " Ingrese periodicidad (Daily, Weekly, Monthly) [Por defecto: $DefaultTrigger]" " Enter schedule (Daily, Weekly, Monthly) [Default: $DefaultTrigger]"
            $InputStr = Read-Host $schedPrompt
            if (![string]::IsNullOrWhiteSpace($InputStr)) { $TriggerInput = $InputStr }
        }

        # Handle existing task edge-case across both naming formats
        foreach ($prefix in @("Isora-Sentinel", "Isora Sentinel")) {
            if (Get-ScheduledTask -TaskName "$prefix\$TaskName" -ErrorAction SilentlyContinue) {
                Unregister-ScheduledTask -TaskName "$prefix\$TaskName" -Confirm:$false -ErrorAction SilentlyContinue
            }
        }

        if ($TriggerInput -match "^(Disabled|None|Off|Desactivada|No)$") { Write-Host (Msg "[INFO] Tarea '$TaskName' omitida/desactivada." "[INFO] Task '$TaskName' skipped/disabled.") -ForegroundColor DarkGray; return }
        $Trigger = switch -Regex ($TriggerInput) {
            "^(Weekly|Semanal)"  { New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At 2am }
            "^(Monthly|Mensual)" { New-ScheduledTaskTrigger -Monthly -DaysOfMonth 1 -At 3am }
            default              { New-ScheduledTaskTrigger -Daily -At 3am }
        }

        $execArg = "-NoProfile -ExecutionPolicy Bypass -File `"$InstallDir\engines\$ScriptFile`""
        if ($ExtraArgs) { $execArg += " $ExtraArgs" }
        $Action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument $execArg
        
        Register-ScheduledTask -TaskName "Isora Sentinel\$TaskName" -Action $Action -Trigger $Trigger -Principal $Principal -Settings $TaskSettings -Force | Out-Null
        Write-Host (Msg "[SUCCESS] Tarea '$TaskName' registrada ($TriggerInput)." "[SUCCESS] Task '$TaskName' registered ($TriggerInput).") -ForegroundColor Green
    }

    $extraScreenshots = if ($ApplyScreenshots) { "-Apply" } else { "" }
    $extraTemp = if ($ApplyTemp) { "-Apply" } else { "" }
    $cleanDownloadsArchiveDir = ($DownloadsArchiveDir -replace '["''`;|&]', '').Trim()
    $extraDownloads = if ($cleanDownloadsArchiveDir) { "-CustomArchiveDir `"$cleanDownloadsArchiveDir`"" } else { "" }
    if ($ApplyDownloads) {
        $extraDownloads = if ($extraDownloads) { "$extraDownloads -Apply" } else { "-Apply" }
    }

    Register-SentinelTask -TaskName "Cleanup_Screenshots" -ScriptFile "Invoke-CleanupScreenshots.ps1" -DefaultTrigger $ScheduleScreenshots -ExtraArgs $extraScreenshots
    Register-SentinelTask -TaskName "Cleanup_Temp_Global" -ScriptFile "Invoke-CleanupTempGlobal.ps1" -DefaultTrigger $ScheduleTemp -ExtraArgs $extraTemp
    Register-SentinelTask -TaskName "Defender_Scan" -ScriptFile "Invoke-DefenderScan.ps1" -DefaultTrigger $ScheduleDefender
    Register-SentinelTask -TaskName "Integrity_Check" -ScriptFile "Invoke-IntegrityCheck.ps1" -DefaultTrigger $ScheduleIntegrity
    Register-SentinelTask -TaskName "BitLocker_Audit" -ScriptFile "Invoke-BitLockerAudit.ps1" -DefaultTrigger $ScheduleBitLocker
    Register-SentinelTask -TaskName "Event_Audit" -ScriptFile "Invoke-EventAudit.ps1" -DefaultTrigger $ScheduleEventAudit
    Register-SentinelTask -TaskName "Archive_Downloads" -ScriptFile "Invoke-ArchiveDownloads.ps1" -DefaultTrigger $ScheduleDownloads -ExtraArgs $extraDownloads
    if ($cleanDownloadsArchiveDir) {
        Set-ItemProperty -Path "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Isora-Sentinel" -Name "DownloadsArchiveDir" -Value $cleanDownloadsArchiveDir -ErrorAction SilentlyContinue
    }
    $sysTaskDir = Join-Path $env:windir "System32\Tasks\Isora Sentinel"
    if (Test-Path -LiteralPath $sysTaskDir) {
        & icacls.exe "$sysTaskDir" /grant "*S-1-5-32-545:(OI)(CI)RX" /Q 2>$null
    }
    
    Write-Host (Msg "`n[SUCCESS] ¡Orquestación completada con éxito!" "`n[SUCCESS] Orchestration Complete!") -ForegroundColor Cyan
} else {
    Write-Host (Msg "`n[INFO] Instalación finalizada. Las tareas no fueron registradas." "`n[INFO] Installation finished. Tasks were not registered.") -ForegroundColor Cyan
}

Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan
Write-Host " ISORA CLOUD GROUP | Isora Sentinel Suite" -ForegroundColor Cyan
Write-Host " Copyright: Sandra Gabriela Puerto Torres (SGPT)" -ForegroundColor White
Write-Host " Operaciones de Seguridad: security@isora.com.co" -ForegroundColor Gray
Write-Host " https://github.com/sandra-puerto | https://www.linkedin.com/in/sandra-puerto/" -ForegroundColor DarkGray
Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan
