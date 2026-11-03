<#
.SYNOPSIS
    Módulo de Utilidades Centrales y Seguridad Defensiva de Isora Sentinel Suite.
.DESCRIPTION
    Proporciona funciones transversales para todos los motores PowerShell:
    - Verificación de privilegios (Administrador / NT AUTHORITY\SYSTEM).
    - Resolución dinámica del binario MpCmdRun.exe de Microsoft Defender.
    - Compuerta criptográfica de integridad SHA-256 (Assert-SentinelIntegrity).
    - Protección contra ataques TOCTOU de enlaces simbólicos y puntos de unión
      intermedios (Test-SafePathHierarchy).
.NOTES
    Autora y Titular: Sandra Gabriela Puerto Torres (SGPT) - Isora Cloud Group
    Licencia: Apache-2.0
#>

<#
.SYNOPSIS
    Exige que el proceso actual se ejecute como Administrador o NT AUTHORITY\SYSTEM.
    Si se ejecuta bajo un usuario estándar sin elevar, lanza una excepción.
#>
function Assert-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $isAdmin  = ([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) -or ($identity.User.Value -eq "S-1-5-18")
    if (-not $isAdmin) {
        throw "Access Denied: This operation requires Administrator (or NT AUTHORITY\SYSTEM) privileges."
    }
}

<#
.SYNOPSIS
    Comprueba sin lanzar excepción si el contexto actual tiene privilegios de Administrador o SYSTEM.
#>
function Test-IsAdmin {
    try {
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        return ([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) -or ($identity.User.Value -eq "S-1-5-18")
    } catch {
        return $false
    }
}

<#
.SYNOPSIS
    Localiza la ruta real y actualizada de MpCmdRun.exe de Microsoft Defender en Windows 10/11.
.DESCRIPTION
    En Windows 11, Defender actualiza su motor en subcarpetas con número de versión dentro de
    C:\ProgramData\Microsoft\Windows Defender\Platform\<version>\MpCmdRun.exe.
#>
function Get-DefenderPlatformPath {
    # 1. Fuente autoritativa: clave InstallLocation del registro de Windows Defender
    try {
        $regDef = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows Defender" -ErrorAction SilentlyContinue
        if ($regDef -and $regDef.InstallLocation) {
            $regExe = Join-Path $regDef.InstallLocation "MpCmdRun.exe"
            if (Test-Path -LiteralPath $regExe) {
                return $regExe
            }
        }
    } catch { }

    # 2. Directorio dinámico de plataforma en ProgramData (ordenado de versión más reciente a más antigua)
    $progData     = if ($env:ProgramData) { $env:ProgramData } else { "C:\ProgramData" }
    $PlatformPath = Join-Path $progData "Microsoft\Windows Defender\Platform"
    if (Test-Path -LiteralPath $PlatformPath) {
        $LatestPlatform = Get-ChildItem -LiteralPath $PlatformPath -Directory -Force -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            Select-Object -First 1
        if ($LatestPlatform) {
            $DynamicPath = Join-Path $LatestPlatform.FullName "MpCmdRun.exe"
            if (Test-Path -LiteralPath $DynamicPath) {
                return $DynamicPath
            }
        }
    }

    # 3. Ruta base clásica en Program Files
    $progFiles = if ($env:ProgramFiles) { $env:ProgramFiles } else { "C:\Program Files" }
    $stdPath   = Join-Path $progFiles "Windows Defender\MpCmdRun.exe"
    if (Test-Path -LiteralPath $stdPath) {
        return $stdPath
    }

    return "C:\Program Files\Windows Defender\MpCmdRun.exe"
}

<#
.SYNOPSIS
    Verifica que ni el archivo/carpeta objetivo ni NINGÚN directorio padre intermedio
    hasta la raíz base ($BaseRoot) sea un enlace simbólico o punto de unión (ReparsePoint).
.DESCRIPTION
    GUÍA DE SEGURIDAD (Anti-TOCTOU para ejecuciones bajo SYSTEM):
    Cuando un servicio corre como NT AUTHORITY\SYSTEM y limpia carpetas dentro de C:\Users\<usuario>,
    verificar únicamente el archivo hoja ($TargetPath) no es suficiente: un atacante local puede
    reemplazar una subcarpeta intermedia (ej. AppData\Local\Temp\sub) por un Directory Junction
    apuntando a C:\Windows\System32 entre el listado inicial (Get-ChildItem) y la eliminación (Remove-Item).
    Esta función recorre cada eslabón desde $TargetPath hasta $BaseRoot justo antes de operar.
#>
function Test-SafePathHierarchy {
    param(
        [Parameter(Mandatory=$true)][string]$TargetPath,
        [Parameter(Mandatory=$false)][string]$BaseRoot = ""
    )

    try {
        $normTarget = [System.IO.Path]::GetFullPath($TargetPath).TrimEnd('\', '/')
        $normRoot   = if ($BaseRoot) { [System.IO.Path]::GetFullPath($BaseRoot).TrimEnd('\', '/') } else { "" }

        # Si se especificó una raíz base, el objetivo debe residir estrictamente dentro de ella
        if ($normRoot -and (-not $normTarget.StartsWith($normRoot, [System.StringComparison]::OrdinalIgnoreCase))) {
            return $false
        }

        $current = $normTarget
        while (-not [string]::IsNullOrEmpty($current)) {
            $attrs = [System.IO.File]::GetAttributes($current)
            if (($attrs -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                return $false
            }

            if ($normRoot -and [string]::Equals($current, $normRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                break
            }

            $parent = [System.IO.Path]::GetDirectoryName($current)
            if ([string]::IsNullOrEmpty($parent) -or [string]::Equals($parent, $current, [System.StringComparison]::OrdinalIgnoreCase)) {
                break
            }
            $current = $parent
        }

        return $true
    } catch {
        return $false
    }
}

<#
.SYNOPSIS
    Compuerta criptográfica que valida los hashes SHA-256 de todos los scripts y módulos
    contra integrity.manifest.json antes de permitir su ejecución bajo SYSTEM.
#>
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
            $relPath      = $fileProp.Name
            $expectedHash = $fileProp.Value
            $targetPath   = Join-Path $baseDir $relPath

            if (-not (Test-Path -LiteralPath $targetPath)) {
                $devPath    = Join-Path $baseDir ("src\" + $relPath)
                $parentPath = Join-Path (Split-Path $baseDir -Parent) $relPath

                if (Test-Path -LiteralPath $devPath) {
                    $targetPath = $devPath
                } elseif (Test-Path -LiteralPath $parentPath) {
                    $targetPath = $parentPath
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

Export-ModuleMember -Function Assert-IsAdmin, Test-IsAdmin, Get-DefenderPlatformPath, Test-SafePathHierarchy, Assert-SentinelIntegrity
