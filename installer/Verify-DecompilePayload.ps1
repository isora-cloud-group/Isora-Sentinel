<#
.SYNOPSIS
    Legacy alias for Verify-PackageIntegrity.ps1
#>
[CmdletBinding()]
param(
    [string]$InstallerPath = "$PSScriptRoot\Isora-Sentinel-Setup.exe",
    [string]$ExtractSandboxDir = ""
)

& "$PSScriptRoot\Verify-PackageIntegrity.ps1" -InstallerPath $InstallerPath -ExtractSandboxDir $ExtractSandboxDir
