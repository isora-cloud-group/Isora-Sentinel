<#
.SYNOPSIS
    Test Orchestrator for Sentinel-IT Suite
.DESCRIPTION
    Executes all Pester unit and AST integration tests.
#>
param(
    [switch]$PassThru
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Sentinel-IT Automated Test Suite (Pester)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

if (-not (Get-Module -ListAvailable -Name Pester)) {
    Write-Error "Pester module is not available on this system. Install via: Install-Module -Name Pester -Scope CurrentUser"
    exit 1
}

Import-Module Pester -Force

$TestFiles = Get-ChildItem -Path $ScriptDir -Filter "*.Tests.ps1" | ForEach-Object { $_.FullName }

Write-Host "Discovered $($TestFiles.Count) test suites in $ScriptDir" -ForegroundColor Yellow

$result = Invoke-Pester -Script $TestFiles -PassThru

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host " Test Summary: Passed=$($result.PassedCount), Failed=$($result.FailedCount), Total=$($result.TotalCount)" -ForegroundColor $(if ($result.FailedCount -eq 0) { "Green" } else { "Red" })
Write-Host "==========================================================" -ForegroundColor Cyan

if ($result.FailedCount -gt 0) {
    exit 1
}

if ($PassThru) {
    return $result
}
