<#
.SYNOPSIS
    Isora Sentinel Core Utilities Module
.DESCRIPTION
    Provides helper functions for the engines (Privilege checks, Defender path resolution).
#>

function Assert-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $isAdmin = ([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) -or ($identity.User.Value -eq "S-1-5-18")
    if (-not $isAdmin) {
        throw "Access Denied: This operation requires Administrator (or NT AUTHORITY\SYSTEM) privileges."
    }
}

function Test-IsAdmin {
    try {
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        return ([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) -or ($identity.User.Value -eq "S-1-5-18")
    } catch {
        return $false
    }
}

function Get-DefenderPlatformPath {
    # 1. Authoritative: Check Windows Defender registry InstallLocation
    try {
        $regDef = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows Defender" -ErrorAction SilentlyContinue
        if ($regDef -and $regDef.InstallLocation) {
            $regExe = Join-Path $regDef.InstallLocation "MpCmdRun.exe"
            if (Test-Path -LiteralPath $regExe) {
                return $regExe
            }
        }
    } catch { }

    # 2. Check dynamic ProgramData Platform directory
    $progData = if ($env:ProgramData) { $env:ProgramData } else { "C:\ProgramData" }
    $PlatformPath = Join-Path $progData "Microsoft\Windows Defender\Platform"
    if (Test-Path -LiteralPath $PlatformPath) {
        $LatestPlatform = Get-ChildItem -LiteralPath $PlatformPath -Directory -Force -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
        if ($LatestPlatform) {
            $DynamicPath = Join-Path $LatestPlatform.FullName "MpCmdRun.exe"
            if (Test-Path -LiteralPath $DynamicPath) {
                return $DynamicPath
            }
        }
    }

    # 3. Check system Program Files
    $progFiles = if ($env:ProgramFiles) { $env:ProgramFiles } else { "C:\Program Files" }
    $stdPath = Join-Path $progFiles "Windows Defender\MpCmdRun.exe"
    if (Test-Path -LiteralPath $stdPath) {
        return $stdPath
    }

    return "C:\Program Files\Windows Defender\MpCmdRun.exe"
}

function Assert-SentinelIntegrity {
    param(
        [Parameter(Mandatory=$false)][string]$ManifestPath,
        [Parameter(Mandatory=$false)][string]$TargetDir,
        [Parameter(Mandatory=$false)][switch]$AllowMissingManifest
    )
    $resolvedManifest = if ($ManifestPath) { 
        $ManifestPath 
    } elseif (Test-Path (Join-Path $PSScriptRoot "..\integrity.manifest.json")) {
        Join-Path $PSScriptRoot "..\integrity.manifest.json"
    } elseif (Test-Path (Join-Path $PSScriptRoot "..\..\integrity.manifest.json")) {
        Join-Path $PSScriptRoot "..\..\integrity.manifest.json"
    } else {
        # Check registry for custom InstallLocation before defaulting
        $installLoc = $null
        try {
            $uninst = Get-ItemProperty "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Isora-Sentinel" -ErrorAction SilentlyContinue
            if ($uninst -and $uninst.InstallLocation) {
                $installLoc = $uninst.InstallLocation
            }
        } catch { }

        $regManifest = if ($installLoc) { Join-Path $installLoc "integrity.manifest.json" } else { "C:\Program Files\Isora-Sentinel\integrity.manifest.json" }
        if (Test-Path -LiteralPath $regManifest) {
            $regManifest
        } else {
            $null
        }
    }

    if (-not $resolvedManifest -or -not (Test-Path -LiteralPath $resolvedManifest)) {
        if ($AllowMissingManifest) {
            return $true
        }
        # In SYSTEM or production environments, missing manifest is a fail-closed integrity violation
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        if ($identity.User.Value -eq "S-1-5-18" -or (Test-Path "C:\Program Files\Isora-Sentinel\integrity.manifest.json")) {
            throw "Security Violation: Integrity manifest missing or inaccessible. Execution blocked under SYSTEM."
        }
        return $true
    }

    try {
        $raw = Get-Content -LiteralPath $resolvedManifest -Raw -Encoding UTF8 | ConvertFrom-Json
        $baseDir = if ($TargetDir -and (Test-Path -LiteralPath $TargetDir)) {
            $TargetDir
        } else {
            Split-Path -Parent $resolvedManifest
        }
        
        if (-not $raw.files) {
            throw "Integrity Violation: Manifest contains invalid or empty file table."
        }

        foreach ($fileProp in $raw.files.PSObject.Properties) {
            $relPath = $fileProp.Name
            $expectedHash = $fileProp.Value
            $targetPath = Join-Path $baseDir $relPath

            if (-not (Test-Path -LiteralPath $targetPath)) {
                $devPath = Join-Path $baseDir ("src\" + $relPath)
                if (Test-Path -LiteralPath $devPath) {
                    $targetPath = $devPath
                } else {
                    throw "Integrity Violation: File missing from Isora Sentinel installation: $relPath"
                }
            }

            $currentHash = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($currentHash -ne $expectedHash.ToLowerInvariant()) {
                throw "Security Violation: Tampering detected in Isora Sentinel module ($relPath). Execution blocked under SYSTEM."
            }
        }
        return $true
    } catch {
        throw "Security Error (Tampering Gate): $($_.Exception.Message)"
    }
}

Export-ModuleMember -Function Assert-IsAdmin, Test-IsAdmin, Get-DefenderPlatformPath, Assert-SentinelIntegrity
