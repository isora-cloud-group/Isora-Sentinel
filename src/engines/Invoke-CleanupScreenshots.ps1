<#
.SYNOPSIS
    Asset Sanitization Engine - ISO 27001:2022 Control A.8.1
#>
$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Logger.psm1")
Import-Module (Join-Path $ScriptDir "..\lib\Sentinel-Core.psm1")

Initialize-SentinelLog -EngineName "ISO_Cleanup_Screenshots"

Write-AuditEntry "=========================================================="
Write-AuditEntry "   [ISO_Cleanup_Screenshots] - SECURITY AUDIT START"
Write-AuditEntry "=========================================================="

try {
    Assert-IsAdmin
    $Profiles = Get-ChildItem -Path "C:\Users" -Directory -Force -ErrorAction SilentlyContinue
    Write-AuditEntry "DISCOVERY: Total profile objects identified." -Metadata @{ ProfileCount = $Profiles.Count }

    foreach ($Profile in $Profiles) {
        $UserName = $Profile.Name
        if ($UserName -match "All Users|Default User|LocalService|NetworkService") { continue }
        
        $OneDriveDirs = Get-ChildItem -Path $Profile.FullName -Directory -Filter "OneDrive*" -ErrorAction SilentlyContinue
        $Vectors = @(
            (Join-Path $Profile.FullName "Pictures\Screenshots"),
            (Join-Path $Profile.FullName "Imágenes\Capturas"),
            (Join-Path $Profile.FullName "Imágenes\Capturas de pantalla")
        )

        foreach ($ODir in $OneDriveDirs) {
            $Vectors += (Join-Path $ODir.FullName "Pictures\Screenshots")
            $Vectors += (Join-Path $ODir.FullName "Imágenes\Capturas")
            $Vectors += (Join-Path $ODir.FullName "Imágenes\Capturas de pantalla")
        }

        foreach ($PathItem in $Vectors) {
            if (Test-Path -Path $PathItem) {
                try {
                    $Assets = Get-ChildItem -Path $PathItem -File -Force -ErrorAction SilentlyContinue
                    if ($Assets.Count -gt 0) {
                        Remove-Item -Path "$PathItem\*" -Force -Recurse -ErrorAction Stop
                        Write-AuditEntry "PURGE: Cleared assets from vector." -Level "SUCCESS" -Metadata @{ Path = $PathItem; Count = $Assets.Count }
                    }
                } catch {
                    Write-AuditEntry "I/O FAILURE: Access denied or file lock." -Level "WARN" -Metadata @{ Path = $PathItem; Error = $_.Exception.Message }
                }
            }
        }
    }
} catch {
    Write-AuditEntry "CRITICAL FAILURE: $($_.Exception.Message)" -Level "ERROR"
}

Write-AuditEntry "=========================================================="
