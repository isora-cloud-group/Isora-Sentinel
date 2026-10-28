<#
.SYNOPSIS
    Generates cryptographic SHA-256 integrity manifest for Sentinel-IT.
.DESCRIPTION
    Scans all library modules, execution engines, and the license file,
    computing deterministic SHA-256 digests into integrity.manifest.json.
#>
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$TargetFiles = @(
    "src/lib/Sentinel-Core.psm1",
    "src/lib/Sentinel-Logger.psm1",
    "src/engines/Invoke-ArchiveDownloads.ps1",
    "src/engines/Invoke-BitLockerAudit.ps1",
    "src/engines/Invoke-CleanupScreenshots.ps1",
    "src/engines/Invoke-CleanupTempGlobal.ps1",
    "src/engines/Invoke-DefenderScan.ps1",
    "src/engines/Invoke-EventAudit.ps1",
    "src/engines/Invoke-IntegrityCheck.ps1",
    "LICENSE"
)

$Hashes = [ordered]@{}
foreach ($relPath in $TargetFiles) {
    $fullPath = Join-Path $ProjectRoot ($relPath -replace '/', '\')
    if (Test-Path -LiteralPath $fullPath) {
        $key = if ($relPath -eq "LICENSE") { "LICENSE" } else { $relPath -replace '^src/', '' }
        $keyNormalized = $key -replace '/', '\'
        $hashVal = (Get-FileHash -Algorithm SHA256 -LiteralPath $fullPath).Hash.ToLowerInvariant()
        $Hashes[$keyNormalized] = $hashVal
    } else {
        Write-Warning "File not found for hashing: $fullPath"
    }
}

$ManifestObj = [ordered]@{
    version      = "2.2.0"
    algorithm    = "SHA256"
    author       = "Sandra Gabriela Puerto Torres"
    brand        = "Isora Cloud Group"
    generated_at = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    files        = $Hashes
}

$ManifestPath = Join-Path $ProjectRoot "integrity.manifest.json"
$ManifestObj | ConvertTo-Json -Depth 4 | Out-File -FilePath $ManifestPath -Encoding utf8 -Force
Write-Host "[SUCCESS] Integrity manifest written to: $ManifestPath" -ForegroundColor Green
