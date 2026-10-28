<#
.SYNOPSIS
    Legacy alias for Verify-PackageIntegrity.ps1
#>
[CmdletBinding()]
param(
    [string]$InstallerPath = "$PSScriptRoot\Sentinel-IT-Setup.exe",
    [string]$ExtractSandboxDir = ""
)

& "$PSScriptRoot\Verify-PackageIntegrity.ps1" -InstallerPath $InstallerPath -ExtractSandboxDir $ExtractSandboxDir
