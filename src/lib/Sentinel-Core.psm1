<#
.SYNOPSIS
    Sentinel-IT Core Utilities Module
.DESCRIPTION
    Provides helper functions for the engines (Privilege checks, Defender path resolution).
#>

function Assert-IsAdmin {
    $IsAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $IsAdmin) {
        throw "Access Denied: This operation requires Administrator (or NT AUTHORITY\SYSTEM) privileges."
    }
}

function Get-DefenderPlatformPath {
    $PlatformPath = "C:\ProgramData\Microsoft\Windows Defender\Platform"
    if (Test-Path $PlatformPath) {
        $LatestPlatform = Get-ChildItem -Path $PlatformPath -Directory | Sort-Object Name -Descending | Select-Object -First 1
        if ($LatestPlatform) {
            $DynamicPath = Join-Path $LatestPlatform.FullName "MpCmdRun.exe"
            if (Test-Path $DynamicPath) {
                return $DynamicPath
            }
        }
    }
    return "C:\Program Files\Windows Defender\MpCmdRun.exe"
}

Export-ModuleMember -Function Assert-IsAdmin, Get-DefenderPlatformPath
