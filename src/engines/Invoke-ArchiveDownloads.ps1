<#
.SYNOPSIS
    Downloads Lifecycle & Storage Tiering Engine - ISO/IEC 27001:2022 Control A.8.10 / A.8.1
.DESCRIPTION
    Systematically identifies unmanaged downloads older than a specified aging threshold
    and safely relocates them into a secure user-accessible archive directory
    (defaults to %USERPROFILE%\Documents\Isora_Sentinel_Archived\Downloads).
    Generates an informative policy notice for the end-user, prevents data loss,
    enforces strict TOCTOU junction protections against arbitrary file moves,
    and records immutable RFC 8259 NDJSON audit logs for SIEM and ISO compliance.
    Supports -AuditOnly switch for non-destructive dry-run simulations.
#>
[CmdletBinding()]
param(
    [int]$DaysOld = 30,
    [string]$CustomArchiveDir = "",
    [switch]$AuditOnly,
    [switch]$IncludeSubdirectories,
    [switch]$SkipIntegrityCheck
)

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Logger.psm1")
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Core.psm1")

Initialize-SentinelLog -EngineName "ISO_Archive_Downloads"

if (-not $SkipIntegrityCheck) {
    try { 
        Assert-SentinelIntegrity 
    } catch { 
        Write-AuditEntry "INTEGRITY CRITICAL (HALTED): $($_.Exception.Message)" -Level "ERROR" -Metadata @{ SecurityViolation = "TamperDetected"; Aborted = "True" }
        throw 
    }
}

Write-AuditEntry "=========================================================="
Write-AuditEntry "   [ISO_Archive_Downloads] - POLICY EXECUTION START"
Write-AuditEntry "   Aging Threshold: $DaysOld days"
if ($AuditOnly) {
    Write-AuditEntry "   MODE: Audit-Only (Dry-Run / No files will be moved)" -Level "WARN"
}
Write-AuditEntry "=========================================================="

$TotalRelocated = 0
$TotalBytes = 0
$TotalIdentified = 0

function New-PolicyNoticeFile {
    param([string]$ArchiveDir)
    try {
        if (-not (Test-Path -LiteralPath $ArchiveDir)) { return }
        $dirItem = Get-Item -LiteralPath $ArchiveDir -Force -ErrorAction SilentlyContinue
        if ($dirItem -and ($dirItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
            Write-AuditEntry "SECURITY: Notice parent directory is a reparse point. Skipping notice creation." -Level "WARN" -Metadata @{ Path = $ArchiveDir }
            return
        }

        $noticePathEs = Join-Path $ArchiveDir "LEAME_POLITICA_TI.txt"
        if (-not (Test-Path -LiteralPath $noticePathEs)) {
            $noticeContentEs = @"
================================================================================
POLITICA DE RETENCION DE ARCHIVOS - TI & CUMPLIMIENTO ISO/IEC 27001
================================================================================
Este equipo cuenta con una directiva corporativa de saneamiento preventivo y
gobernanza de endpoints.

Los archivos que tenian mas de $DaysOld dias de antiguedad en la carpeta de 'Descargas'
fueron trasladados automaticamente a este directorio para:
  1. Proteger sus archivos asegurando su ubicacion en Documentos
     (carpeta respaldada y sincronizada bajo politicas corporativas).
  2. Cumplir con los lineamientos de ciclo de vida de la informacion (ISO/IEC 27001 A.8.10).
  3. Prevenir la saturacion y degradacion del almacenamiento del sistema (ISO/IEC 20000-1 seccion 8.2).

INFORMACION IMPORTANTE:
- Sus archivos NO han sido eliminados.
- Puede consultarlos, moverlos o abrirlos con normalidad cuando los requiera.
- Ultima reubicacion preventiva efectuada el: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

Si requiere soporte o excepciones de retencion, contacte al area de TI / Ciberseguridad.
================================================================================
"@
            try {
                $bytesEs = [System.Text.UTF8Encoding]::new($false).GetBytes($noticeContentEs)
                $fsEs = [System.IO.FileStream]::new($noticePathEs, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write)
                try { $fsEs.Write($bytesEs, 0, $bytesEs.Length) } finally { $fsEs.Dispose() }
            } catch [System.IO.IOException] { }
        }

        $noticePathEn = Join-Path $ArchiveDir "README_IT_POLICY.txt"
        if (-not (Test-Path -LiteralPath $noticePathEn)) {
            $noticeContentEn = @"
================================================================================
FILE RETENTION POLICY - IT GOVERNANCE & ISO/IEC 27001 COMPLIANCE
================================================================================
This endpoint is governed by corporate preventive maintenance and data lifecycle
policies.

Files older than $DaysOld days in the 'Downloads' folder were automatically relocated
to this directory in order to:
  1. Protect your files by safeguarding them under Documents
     (a folder backed up and synchronized according to corporate standards).
  2. Comply with regulatory information lifecycle governance (ISO/IEC 27001 A.8.10).
  3. Prevent system volume saturation and disk degradation (ISO/IEC 20000-1 Section 8.2).

IMPORTANT INFORMATION:
- Your files have NOT been deleted.
- You can access, browse, or move them normally whenever needed.
- Preventive archival relocation executed on: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

If you require IT support or retention exceptions, please contact IT / Cybersecurity.
================================================================================
"@
            try {
                $bytesEn = [System.Text.UTF8Encoding]::new($false).GetBytes($noticeContentEn)
                $fsEn = [System.IO.FileStream]::new($noticePathEn, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write)
                try { $fsEn.Write($bytesEn, 0, $bytesEn.Length) } finally { $fsEn.Dispose() }
            } catch [System.IO.IOException] { }
        }
    } catch {
        Write-AuditEntry "NOTICE WARNING: Could not generate user policy notice files." -Level "WARN" -Metadata @{ Error = $_.Exception.Message }
    }
}

function Process-UserDownloads {
    param(
        [string]$ProfilePath,
        [string]$UserName
    )

    $downloadsPath = Join-Path $ProfilePath "Downloads"
    if (-not (Test-Path -LiteralPath $downloadsPath)) {
        return
    }

    $baseDownloads = Get-Item -LiteralPath $downloadsPath -Force -ErrorAction SilentlyContinue
    if ($baseDownloads -and ($baseDownloads.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
        Write-AuditEntry "SECURITY: User Downloads folder is a reparse point. Skipping for safety." -Level "WARN" -Metadata @{ User = $UserName; Path = $downloadsPath }
        return
    }

    # Determine Destination Archive Directory
    $archiveDir = if ($CustomArchiveDir -and $CustomArchiveDir.Trim() -ne "") {
        if ($CustomArchiveDir -match '%USERPROFILE%') {
            $CustomArchiveDir.Replace('%USERPROFILE%', $ProfilePath)
        } else {
            $CustomArchiveDir
        }
    } else {
        $regArchive = $null
        try {
            $regKey = Get-ItemProperty -Path "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Isora-Sentinel" -Name "DownloadsArchiveDir" -ErrorAction SilentlyContinue
            if ($regKey -and $regKey.DownloadsArchiveDir) {
                $regArchive = $regKey.DownloadsArchiveDir
            }
        } catch { }

        if ($regArchive -and $regArchive.Trim() -ne "") {
            if ($regArchive -match '%USERPROFILE%') {
                $regArchive.Replace('%USERPROFILE%', $ProfilePath)
            } else {
                $regArchive
            }
        } else {
            Join-Path $ProfilePath "Documents\Isora_Sentinel_Archived\Downloads"
        }
    }

    # Defensive check: ensure neither target archive directory nor any parent is a junction/reparse point
    $currCheck = $archiveDir
    $profileNorm = [System.IO.Path]::GetFullPath($ProfilePath).TrimEnd('\', '/')
    while ($currCheck -and $currCheck.Length -ge $profileNorm.Length) {
        if (Test-Path -LiteralPath $currCheck) {
            $item = Get-Item -LiteralPath $currCheck -Force -ErrorAction SilentlyContinue
            if ($item -and ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
                Write-AuditEntry "SECURITY: Target archive path traverses a reparse point (junction). Aborting relocation for user." -Level "ERROR" -Metadata @{ User = $UserName; Path = $currCheck }
                return
            }
        }
        $currCheck = Split-Path $currCheck -Parent
    }

    $cutoffDate = (Get-Date).AddDays(-$DaysOld)
    
    # Enumerate files
    $gciParams = @{
        LiteralPath = $downloadsPath
        File        = $true
        Force       = $true
        ErrorAction = "SilentlyContinue"
    }
    if ($IncludeSubdirectories) {
        $gciParams["Recurse"] = $true
    }

    $files = Get-ChildItem @gciParams
    if (-not $files) {
        return
    }

    $eligibleFiles = @()
    foreach ($f in $files) {
        # Security: Skip symbolic links, junctions, or reparse points
        if ($f.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
            continue
        }
        # Security: Skip OneDrive cloud-only dehydrated placeholders
        if ($f.Attributes -band [System.IO.FileAttributes]::Offline) {
            continue
        }
        # Check aging threshold
        if ($f.LastWriteTime -lt $cutoffDate) {
            $eligibleFiles += $f
        }
    }

    if ($eligibleFiles.Count -eq 0) {
        return
    }

    $script:TotalIdentified += $eligibleFiles.Count
    $userBytes = ($eligibleFiles | Measure-Object -Property Length -Sum).Sum

    if ($AuditOnly) {
        Write-AuditEntry "AUDIT: Found eligible downloads for relocation." -Level "INFO" -Metadata @{
            User            = $UserName
            EligibleCount   = $eligibleFiles.Count
            ReclaimableBytes= $userBytes
            SourcePath      = $downloadsPath
            PlannedArchive  = $archiveDir
        }
        return
    }

    # Ensure Target Directory Exists
    try {
        if (-not (Test-Path -LiteralPath $archiveDir)) {
            New-Item -ItemType Directory -Path $archiveDir -Force | Out-Null
        }
        New-PolicyNoticeFile -ArchiveDir (Split-Path $archiveDir -Parent)
    } catch {
        Write-AuditEntry "I/O FAILURE: Cannot initialize archive directory." -Level "ERROR" -Metadata @{
            User  = $UserName
            Path  = $archiveDir
            Error = $_.Exception.Message
        }
        return
    }

    $userMoved = 0
    foreach ($file in $eligibleFiles) {
        try {
            # Real-time fresh attribute check right before moving
            $freshAttr = [System.IO.File]::GetAttributes($file.FullName)
            if (($freshAttr -band [System.IO.FileAttributes]::ReparsePoint) -or ($freshAttr -band [System.IO.FileAttributes]::Offline)) {
                continue
            }

            $destFile = Join-Path $archiveDir $file.Name
            
            # Defensive collision handling: If file exists, append unique timestamp suffix
            if (Test-Path -LiteralPath $destFile) {
                $baseName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
                $ext = [System.IO.Path]::GetExtension($file.Name)
                $uniqueName = "{0}_{1}{2}" -f $baseName, (Get-Date -Format "yyyyMMdd_HHmmss"), $ext
                $destFile = Join-Path $archiveDir $uniqueName
            }

            Move-Item -LiteralPath $file.FullName -Destination $destFile -Force -ErrorAction Stop
            $userMoved++
            $script:TotalBytes += $file.Length
        } catch {
            Write-AuditEntry "MOVE ERROR: Could not relocate file." -Level "WARN" -Metadata @{
                File  = $file.FullName
                Error = $_.Exception.Message
            }
        }
    }

    if ($userMoved -gt 0) {
        $script:TotalRelocated += $userMoved
        Write-AuditEntry "RELOCATION SUCCESS: Relocated aged downloads." -Level "SUCCESS" -Metadata @{
            User           = $UserName
            MovedCount     = $userMoved
            BytesReclaimed = $userBytes
            ArchiveDir     = $archiveDir
        }
    }
}

try {
    $IsAdmin = Test-IsAdmin
    if ($IsAdmin) {
        Write-AuditEntry "DISCOVERY: Elevated/SYSTEM session - Scanning profiles for aged downloads." -Level "INFO"
        
        $profileMap = @{}
        $ProfileList = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\*" -ErrorAction SilentlyContinue
        if ($ProfileList) {
            foreach ($entry in $ProfileList) {
                if ($entry.ProfileImagePath -and (Test-Path -LiteralPath $entry.ProfileImagePath)) {
                    $uName = Split-Path $entry.ProfileImagePath -Leaf
                    if ($uName -notmatch "^(All Users|Default|Default User|LocalService|NetworkService|Public|systemprofile)$") {
                        $profileMap[$entry.ProfileImagePath] = $uName
                    }
                }
            }
        }
        if (Test-Path "C:\Users") {
            foreach ($d in (Get-ChildItem -Path "C:\Users" -Directory -Force -ErrorAction SilentlyContinue)) {
                if ($d.Name -notmatch "^(All Users|Default|Default User|LocalService|NetworkService|Public)$") {
                    if (-not $profileMap.ContainsKey($d.FullName)) {
                        $profileMap[$d.FullName] = $d.Name
                    }
                }
            }
        }

        foreach ($pPath in $profileMap.Keys) {
            Process-UserDownloads -ProfilePath $pPath -UserName $profileMap[$pPath]
        }
    } else {
        Write-AuditEntry "DISCOVERY: Standard user session - Processing current profile only." -Level "INFO"
        Process-UserDownloads -ProfilePath $env:USERPROFILE -UserName $env:USERNAME
    }
} catch {
    Write-AuditEntry "EXECUTION ERROR: Unhandled exception during downloads archiving." -Level "ERROR" -Metadata @{ Error = $_.Exception.Message }
}

Write-AuditEntry "=========================================================="
if ($AuditOnly) {
    Write-AuditEntry "   [ISO_Archive_Downloads] - AUDIT COMPLETE"
    Write-AuditEntry "   Identified Aged Downloads : $TotalIdentified"
} else {
    Write-AuditEntry "   [ISO_Archive_Downloads] - POLICY APPLIED"
    Write-AuditEntry "   Total Files Relocated     : $TotalRelocated"
    Write-AuditEntry "   Total Storage Reclaimed   : $([math]::Round($TotalBytes / 1MB, 2)) MB ($TotalBytes bytes)"
}
Write-AuditEntry "=========================================================="
