<#
.SYNOPSIS
    Builds the standalone GUI Installer executable (Sentinel-IT-Setup.exe) for Sentinel-IT.
.DESCRIPTION
    1. Updates cryptographic integrity manifest (integrity.manifest.json).
    2. Packages source modules, license, uninstaller, and manifest into an embedded payload.
    3. Compiles the C# Windows Forms installer using Microsoft .NET Framework csc.exe.
    4. Generates an institutional audit provenance receipt (BUILD_PROVENANCE.json).
    Requires ZERO administrator privileges to compile.
#>
param(
    [string]$OutputName = "Sentinel-IT-Setup.exe"
)

$ErrorActionPreference = "Stop"
$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$ProjectRoot = (Resolve-Path (Join-Path $ScriptDir "..")).Path

Write-Host "============================================================" -ForegroundColor DarkCyan
Write-Host " ISORA CLOUD GROUP | Sentinel-IT GUI Installer Builder" -ForegroundColor Cyan
Write-Host " Arquitectura & Titular: Sandra Gabriela Puerto Torres" -ForegroundColor Gray
Write-Host "============================================================" -ForegroundColor DarkCyan

# 1. Locate Microsoft .NET csc.exe compiler
$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $cscPath)) {
    $cscPath = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $cscPath)) {
    throw "Compilador Microsoft .NET C# (csc.exe) no encontrado en el sistema."
}

$AsmInfo = Join-Path $ScriptDir "src\AssemblyInfo.cs"
$AppManifest = Join-Path $ScriptDir "src\app.manifest"
$AppIcon = Join-Path $ScriptDir "src\app.ico"
$AppSvg  = Join-Path $ScriptDir "src\app.svg"

# 2. Compile System Tray Monitor to temporary staging location (to embed inside Setup)
Write-Host "`n[*] Compilando monitor de bandeja en entorno temporal para incrustación..." -ForegroundColor Yellow
$TempBuildDir = Join-Path $env:TEMP ("SentinelTrayBuild_" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $TempBuildDir -Force | Out-Null
$TraySource = Join-Path $ScriptDir "src\SentinelTray.cs"
$ConfigFormSource = Join-Path $ScriptDir "src\TaskConfigForm.cs"
$TrayExe = Join-Path $TempBuildDir "Sentinel-IT-Tray.exe"

$trayArgs = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/platform:anycpu",
    "/win32manifest:$AppManifest",
    "/reference:System.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Xml.dll",
    "/out:$TrayExe",
    $TraySource,
    $ConfigFormSource,
    $AsmInfo
)
if (Test-Path $AppIcon) {
    $trayArgs += "/resource:$AppIcon,AppIcon"
    $trayArgs += "/win32icon:$AppIcon"
}

$procTray = Start-Process -FilePath $cscPath -ArgumentList $trayArgs -NoNewWindow -Wait -PassThru
if ($procTray.ExitCode -ne 0 -or -not (Test-Path $TrayExe)) {
    throw "Fallo al compilar Sentinel-IT-Tray.exe (código de salida: $($procTray.ExitCode))."
}
Write-Host "[SUCCESS] Monitor de bandeja compilado y listo para incrustar en el instalador ($([math]::Round((Get-Item $TrayExe).Length / 1024, 2)) KB)" -ForegroundColor Green

# 3. Regenerate SHA-256 Integrity Manifest
Write-Host "`n[*] Regenerando manifiesto de integridad SHA-256..." -ForegroundColor Yellow
$manifestScript = Join-Path $ScriptDir "Build-IntegrityManifest.ps1"
& $manifestScript -ProjectRoot $ProjectRoot

# 4. Prepare Embedded Payload Zip
$TempZip = Join-Path $ScriptDir "payload.zip"
if (Test-Path $TempZip) { Remove-Item $TempZip -Force }

Write-Host "[*] Empaquetando payload soberano auto-contenido..." -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$archive = [System.IO.Compression.ZipFile]::Open($TempZip, [System.IO.Compression.ZipArchiveMode]::Create)
$bundledFiles = [System.Collections.Generic.List[PSObject]]::new()

try {
    # Add src/ files
    $srcDir = Join-Path $ProjectRoot "src"
    $srcFiles = Get-ChildItem -Path $srcDir -Recurse -File
    foreach ($file in $srcFiles) {
        $relPath = $file.FullName.Substring($srcDir.Length + 1)
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $relPath) | Out-Null
        $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash
        $bundledFiles.Add([PSCustomObject]@{
            Path = "src\$relPath"
            Size = $file.Length
            SHA256 = $hash
        })
    }

    # Add Sentinel-IT-Tray.exe
    if (Test-Path $TrayExe) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $TrayExe, "Sentinel-IT-Tray.exe") | Out-Null
        $bundledFiles.Add([PSCustomObject]@{
            Path = "Sentinel-IT-Tray.exe"
            Size = (Get-Item $TrayExe).Length
            SHA256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $TrayExe).Hash
        })
    }

    # Add app.ico and app.svg
    if (Test-Path $AppIcon) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $AppIcon, "app.ico") | Out-Null
        $bundledFiles.Add([PSCustomObject]@{
            Path = "app.ico"
            Size = (Get-Item $AppIcon).Length
            SHA256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $AppIcon).Hash
        })
    }
    if (Test-Path $AppSvg) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $AppSvg, "app.svg") | Out-Null
        $bundledFiles.Add([PSCustomObject]@{
            Path = "app.svg"
            Size = (Get-Item $AppSvg).Length
            SHA256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $AppSvg).Hash
        })
    }

    # Add LICENSE
    $licenseFile = Join-Path $ProjectRoot "LICENSE"
    if (Test-Path $licenseFile) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $licenseFile, "LICENSE") | Out-Null
        $bundledFiles.Add([PSCustomObject]@{
            Path = "LICENSE"
            Size = (Get-Item $licenseFile).Length
            SHA256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $licenseFile).Hash
        })
    }

    # Add integrity.manifest.json
    $manifestFile = Join-Path $ProjectRoot "integrity.manifest.json"
    if (Test-Path $manifestFile) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $manifestFile, "integrity.manifest.json") | Out-Null
        $bundledFiles.Add([PSCustomObject]@{
            Path = "integrity.manifest.json"
            Size = (Get-Item $manifestFile).Length
            SHA256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $manifestFile).Hash
        })
    }

    # Add Uninstall-Sentinel.ps1
    $uninstallScript = Join-Path $ScriptDir "Uninstall-Sentinel.ps1"
    if (Test-Path $uninstallScript) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $uninstallScript, "Uninstall-Sentinel.ps1") | Out-Null
        $bundledFiles.Add([PSCustomObject]@{
            Path = "Uninstall-Sentinel.ps1"
            Size = (Get-Item $uninstallScript).Length
            SHA256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $uninstallScript).Hash
        })
    }
} finally {
    $archive.Dispose()
}

$zipSizeKb = (Get-Item $TempZip).Length / 1024
Write-Host "[SUCCESS] Payload empaquetado ($([math]::Round($zipSizeKb, 2)) KB) con $($bundledFiles.Count) modulos soberanos." -ForegroundColor Green

# 5. Determine Target Output Executable Path
$OutputExe = Join-Path $ScriptDir $OutputName
$isLocked = $false
if (Test-Path $OutputExe) {
    try {
        $testStream = [System.IO.File]::Open($OutputExe, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        $testStream.Close()
    } catch {
        $isLocked = $true
    }
}

if ($isLocked) {
    Write-Warning "El archivo '$OutputExe' está en ejecución o bloqueado por otra instancia."
    $OutputExe = Join-Path $ScriptDir "Sentinel-IT-Setup.new.exe"
    Write-Host "-> Redirigiendo salida a binario nuevo: $OutputExe" -ForegroundColor Cyan
}

$CsSource = Join-Path $ScriptDir "src\SentinelSetup.cs"
$AsmInfo = Join-Path $ScriptDir "src\AssemblyInfo.cs"
$AppManifest = Join-Path $ScriptDir "src\app.manifest"
$AppIcon = Join-Path $ScriptDir "src\app.ico"

Write-Host "[*] Compilando ejecutable standalone Windows GUI con csc.exe..." -ForegroundColor Yellow

$cscArgs = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/debug-",
    "/platform:anycpu",
    "/win32manifest:$AppManifest",
    "/reference:System.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.IO.Compression.dll",
    "/reference:System.IO.Compression.FileSystem.dll",
    "/resource:$TempZip,SentinelPayload",
    "/out:$OutputExe",
    $CsSource,
    $AsmInfo
)
if (Test-Path $AppIcon) {
    $cscArgs += "/resource:$AppIcon,AppIcon"
    $cscArgs += "/win32icon:$AppIcon"
}

$proc = Start-Process -FilePath $cscPath -ArgumentList $cscArgs -NoNewWindow -Wait -PassThru

# 5. Clean up temporary payload zip and intermediate build directory
if (Test-Path $TempZip) {
    Remove-Item $TempZip -Force
}
if ($TempBuildDir -and (Test-Path $TempBuildDir)) {
    Remove-Item $TempBuildDir -Recurse -Force -ErrorAction SilentlyContinue
}

# Remove any loose Tray.exe or PDB in installer directory so only Sentinel-IT-Setup.exe remains
$looseTray = Join-Path $ScriptDir "Sentinel-IT-Tray.exe"
if (Test-Path $looseTray) { Remove-Item $looseTray -Force -ErrorAction SilentlyContinue }
$loosePdb = Join-Path $ScriptDir "Sentinel-IT-Setup.pdb"
if (Test-Path $loosePdb) { Remove-Item $loosePdb -Force -ErrorAction SilentlyContinue }
$looseTrayPdb = Join-Path $ScriptDir "Sentinel-IT-Tray.pdb"
if (Test-Path $looseTrayPdb) { Remove-Item $looseTrayPdb -Force -ErrorAction SilentlyContinue }

if ($proc.ExitCode -eq 0 -and (Test-Path $OutputExe)) {
    $exeSizeKb = (Get-Item $OutputExe).Length / 1024
    $exeHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $OutputExe).Hash

    # 6. Generate Institutional Provenance Receipt (BUILD_PROVENANCE.json)
    $provenance = [ordered]@{
        artifact_name = [System.IO.Path]::GetFileName($OutputExe)
        version = "2.2.0"
        timestamp_utc = (Get-Date).ToUniversalTime().ToString("o")
        architect = "Sandra Gabriela Puerto Torres"
        commercial_brand = "Isora Cloud Group"
        compiler = "Microsoft .NET Framework csc.exe v4.0.30319"
        compiler_flags = "/target:winexe /optimize+ /platform:anycpu"
        sha256 = $exeHash
        size_bytes = (Get-Item $OutputExe).Length
        bundled_files = $bundledFiles
    }
    $provJsonPath = Join-Path $ScriptDir "BUILD_PROVENANCE.json"
    $provenance | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $provJsonPath -Encoding UTF8

    # Refresh Windows Shell icon cache so Explorer updates icon without delay
    try {
        $shellCode = @'
using System;
using System.Runtime.InteropServices;
public static class ShellCacheReloader {
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    public static void Reload() {
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); // SHCNE_ASSOCCHANGED
    }
}
'@
        Add-Type -TypeDefinition $shellCode -ErrorAction SilentlyContinue
        [ShellCacheReloader]::Reload()
    } catch {}
    try { & ie4uinit.exe -show 2>$null } catch {}

    Write-Host "`n============================================================" -ForegroundColor DarkCyan
    Write-Host " BUILD SUCCESS: Standalone GUI Installer Created!" -ForegroundColor Green
    Write-Host " Binary Artifact : $OutputExe" -ForegroundColor White
    Write-Host " Note            : Auto-contenido. Incluye Sentinel-IT-Tray.exe incrustado." -ForegroundColor Cyan
    Write-Host " File Size       : $([math]::Round($exeSizeKb, 2)) KB" -ForegroundColor Gray
    Write-Host " SHA-256 Digest  : $exeHash" -ForegroundColor DarkGray
    Write-Host " Provenance Doc  : $provJsonPath" -ForegroundColor DarkGray
    Write-Host "============================================================" -ForegroundColor DarkCyan
} else {
    throw "La compilación falló con código de salida $($proc.ExitCode)."
}
