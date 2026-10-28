<#
.SYNOPSIS
    Asset Sanitization Engine - ISO/IEC 27001:2022 Control A.8.1
.DESCRIPTION
    Purges local and OneDrive screenshot vectors across all user profiles to prevent
    unauthorized data retention and endpoint Data Loss (DLP).
    Supports -AuditOnly switch for dry-run simulation in enterprise staging.
#>
[CmdletBinding()]
param(
    [switch]$AuditOnly
)

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Logger.psm1")
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Core.psm1")

Initialize-SentinelLog -EngineName "ISO_Cleanup_Screenshots"
try { Assert-SentinelIntegrity } catch { Write-AuditEntry "INTEGRITY WARNING: $($_.Exception.Message)" -Level "WARN" }

Write-AuditEntry "=========================================================="
Write-AuditEntry "   [ISO_Cleanup_Screenshots] - SECURITY AUDIT START"
if ($AuditOnly) {
    Write-AuditEntry "   MODE: Audit-Only (Dry-Run / No files will be deleted)" -Level "WARN"
}
Write-AuditEntry "=========================================================="

$TotalPurged = 0
$TotalIdentified = 0

try {
    $IsAdmin = Test-IsAdmin
    $ProfileList = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\*" -ErrorAction SilentlyContinue

    if ($IsAdmin) {
        $profileMap = @{}
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
        $Profiles = @()
        foreach ($pKey in $profileMap.Keys) {
            $Profiles += [PSCustomObject]@{ Name = $profileMap[$pKey]; FullName = $pKey }
        }
        Write-AuditEntry "DISCOVERY: Administrator/SYSTEM session - Scanning all profile objects." -Metadata @{ ProfileCount = $Profiles.Count }
    } else {
        $Profiles = @( [PSCustomObject]@{ Name = $env:USERNAME; FullName = $env:USERPROFILE } )
        Write-AuditEntry "SCOPE: Standard user session - Sanitization scoped to current user profile." -Metadata @{ User = $env:USERNAME }
    }

    foreach ($Profile in $Profiles) {
        $UserName = $Profile.Name
        if ($UserName -match "^(All Users|Default|Default User|LocalService|NetworkService|Public)$") { continue }
        $ProfilePath = $Profile.FullName

        $Vectors = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

        # 1. Authoritative Registry Discovery (User Shell Folders & Shell Folders)
        # Identifies exact user-redirected Pictures and Screenshots locations from Windows Registry:
        # FOLDERID_Screenshots: {B7BEDE81-DF94-4788-BE95-FDE56E14E247}
        # FOLDERID_Pictures:    My Pictures / {0DDD015D-B06C-45D5-8C4C-F59713854639}
        $pKey = $ProfileList | Where-Object { $_.ProfileImagePath -eq $ProfilePath } | Select-Object -First 1
        $sid = if ($pKey) { $pKey.PSChildName } else { $null }

        $regHives = @()
        if ($sid -and (Test-Path "Registry::HKEY_USERS\$sid")) {
            $regHives += "Registry::HKEY_USERS\$sid"
        }
        if ($UserName -eq $env:USERNAME) {
            $regHives += "HKCU:"
        }

        # Offline profile support: if user hive is not currently mounted under HKEY_USERS
        $tempHiveLoaded = $false
        $safeUName = $UserName -replace '[^a-zA-Z0-9]', '_'
        $tempHiveName = "Sentinel_Offline_$safeUName"
        $ntuserDat = Join-Path $ProfilePath "NTUSER.DAT"
        if ($regHives.Count -eq 0 -and (Test-Path -LiteralPath $ntuserDat)) {
            try {
                $null = reg load "HKU\$tempHiveName" "$ntuserDat" 2>&1
                if ($LASTEXITCODE -eq 0) {
                    $tempHiveLoaded = $true
                    $regHives += "Registry::HKEY_USERS\$tempHiveName"
                }
            } catch { }
        }

        try {
            foreach ($hive in $regHives) {
                foreach ($subKey in @("Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders", "Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders")) {
                    $fullReg = Join-Path $hive $subKey
                    if (Test-Path $fullReg) {
                        $regProps = Get-ItemProperty $fullReg -ErrorAction SilentlyContinue
                        if ($regProps) {
                            # A. Direct Screenshots KnownFolder GUID: {B7BEDE81-DF94-4788-BE95-FDE56E14E247}
                            foreach ($name in $regProps.PSObject.Properties.Name) {
                                if ($name -match 'B7BEDE81-DF94-4788-BE95-FDE56E14E247') {
                                    $val = $regProps.$name
                                    if ($val) {
                                        $expanded = $val.Replace('%USERPROFILE%', $ProfilePath)
                                        $expanded = [System.Environment]::ExpandEnvironmentVariables($expanded)
                                        [void]$Vectors.Add($expanded)
                                    }
                                }
                            }

                            # B. Pictures KnownFolder (My Pictures / {0DDD015D-B06C-45D5-8C4C-F59713854639})
                            $picKeys = @('My Pictures', '{0DDD015D-B06C-45D5-8C4C-F59713854639}', '{0ddd015d-b06c-45d5-8c4c-f59713854639}')
                            foreach ($pk in $picKeys) {
                                if ($regProps.$pk) {
                                    $basePic = $regProps.$pk.Replace('%USERPROFILE%', $ProfilePath)
                                    $basePic = [System.Environment]::ExpandEnvironmentVariables($basePic)
                                    foreach ($sub in @('Screenshots', 'Capturas', 'Capturas de pantalla', 'Bilder\Screenshots', "Images\Captures d'écran", 'Immagini\Schermate')) {
                                        [void]$Vectors.Add((Join-Path $basePic $sub))
                                    }
                                }
                            }
                        }
                    }
                }
            }
        } finally {
            if ($tempHiveLoaded) {
                [GC]::Collect()
                [GC]::WaitForPendingFinalizers()
                $null = reg unload "HKU\$tempHiveName" 2>&1
            }
        }

        # 2. Cloud and Sync Storage Roots (Google Drive / Mi unidad, OneDrive, Dropbox, iCloud, etc.)
        $cloudDirs = Get-ChildItem -LiteralPath $ProfilePath -Directory -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match "^(OneDrive.*|Mi unidad|Google Drive|GoogleDrive|My Drive|Dropbox.*|iCloud.*|Nextcloud|ownCloud|Box.*)$" }
        foreach ($cd in $cloudDirs) {
            foreach ($sub in @('Pictures\Screenshots', 'Pictures\Capturas', 'Imágenes\Capturas', 'Imágenes\Capturas de pantalla', 'Screenshots', 'Capturas', 'Capturas de pantalla')) {
                [void]$Vectors.Add((Join-Path $cd.FullName $sub))
            }
        }

        # 3. Standard Local Default Vectors (multi-language fallback)
        foreach ($sub in @(
            'Pictures\Screenshots',
            'Imágenes\Capturas',
            'Imágenes\Capturas de pantalla',
            'Bilder\Screenshots',
            "Images\Captures d'écran",
            'Immagini\Schermate'
        )) {
            [void]$Vectors.Add((Join-Path $ProfilePath $sub))
        }

        # 4. Process each resolved screenshot vector
        foreach ($PathItem in $Vectors) {
            if (Test-Path -LiteralPath $PathItem) {
                $dirItem = Get-Item -LiteralPath $PathItem -Force -ErrorAction SilentlyContinue
                if ($dirItem -and ($dirItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
                    Write-AuditEntry "SECURITY: Reparse point (junction/symlink) detected in vector. Skipping for safety." -Level "WARN" -Metadata @{ Path = $PathItem }
                    continue
                }
                try {
                    $Assets = Get-ChildItem -LiteralPath $PathItem -File -Force -ErrorAction SilentlyContinue
                    if ($Assets -and $Assets.Count -gt 0) {
                        $TotalIdentified += $Assets.Count
                        if ($AuditOnly) {
                            Write-AuditEntry "AUDIT: Found screenshot assets (Dry-Run)." -Level "INFO" -Metadata @{ User = $UserName; Path = $PathItem; Count = $Assets.Count }
                        } else {
                            $vectorPurged = 0
                            foreach ($Asset in $Assets) {
                                $isReparseOrCloud = ($Asset.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -or ($Asset.Attributes -band [System.IO.FileAttributes]::Offline)
                                if (-not $isReparseOrCloud) {
                                    if ($Asset.Attributes -band [System.IO.FileAttributes]::ReadOnly) {
                                        try { [System.IO.File]::SetAttributes($Asset.FullName, [System.IO.FileAttributes]::Normal) } catch { }
                                    }
                                    Remove-Item -LiteralPath $Asset.FullName -Force -ErrorAction SilentlyContinue
                                    $vectorPurged++
                                }
                            }
                            $TotalPurged += $vectorPurged
                            Write-AuditEntry "PURGE: Cleared assets from vector." -Level "SUCCESS" -Metadata @{ User = $UserName; Path = $PathItem; Count = $vectorPurged }
                        }
                    }
                } catch {
                    Write-AuditEntry "I/O FAILURE: Access denied or file lock." -Level "WARN" -Metadata @{ Path = $PathItem; Error = $_.Exception.Message }
                }
            }
        }
    }

    $SummaryLevel = if ($AuditOnly -and $TotalIdentified -gt 0) { "WARN" } else { "SUCCESS" }
    Write-AuditEntry "RESULT: Asset sanitization cycle completed." -Level $SummaryLevel -Metadata @{ 
        AuditOnly       = [string]$AuditOnly; 
        TotalIdentified = $TotalIdentified; 
        TotalPurged     = $TotalPurged 
    }
} catch {
    Write-AuditEntry "CRITICAL FAILURE: $($_.Exception.Message)" -Level "ERROR"
}

Write-AuditEntry "=========================================================="
