<#
.SYNOPSIS
    Enterprise Test Orchestrator for Sentinel-IT Suite.
.DESCRIPTION
    Executes all Pester unit, AST static integrity, and SIEM logging tests.
    Engineered for dual compatibility across Pester 5+, Pester 6+, and legacy environments,
    ensuring deterministic CI/CD pipeline validation under GitHub Actions and local workstations.
.PARAMETER PassThru
    Returns the Pester result object for programmatic inspection.
.NOTES
    Compliance: ISO/IEC 27001:2022 A.8.20 / SOC 2 CC7.2 automated testing gate.
#>
[CmdletBinding()]
param(
    [switch]$PassThru
)

$ErrorActionPreference = "Stop"
$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }

Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan
Write-Host " Sentinel-IT | Automated Test Suite (Pester CI/CD Gate)" -ForegroundColor Cyan
Write-Host " Autora: Sandra Gabriela Puerto Torres | https://isora.com.co" -ForegroundColor Gray
Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan

# Ensure Pester 5+ is available; if invoked under Windows PowerShell 5.1 with legacy Pester 3, delegate to pwsh if available
$pesterMod = Get-Module -ListAvailable -Name Pester | Where-Object { $_.Version.Major -ge 5 } | Sort-Object Version -Descending | Select-Object -First 1

if (-not $pesterMod -and $PSVersionTable.PSVersion.Major -lt 7) {
    $pwshCmd = Get-Command -Name pwsh -ErrorAction SilentlyContinue
    if ($pwshCmd) {
        Write-Host "Pester 5+ required. Delegating execution to PowerShell 7+ ($($pwshCmd.Source))..." -ForegroundColor Yellow
        $scriptPath = if ($PSCommandPath) { $PSCommandPath } else { Join-Path $ScriptDir "Invoke-Tests.ps1" }
        $forwardArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $scriptPath)
        if ($PassThru) { $forwardArgs += "-PassThru" }
        & $pwshCmd.Source @forwardArgs
        exit $LASTEXITCODE
    }
}

if (-not (Get-Module -ListAvailable -Name Pester)) {
    Write-Error "Pester module is not available on this system. Install via: Install-Module -Name Pester -Scope CurrentUser -MinimumVersion 5.0.0"
    exit 1
}

# Load the highest available Pester module (preferring >= 5.0)
$moduleToLoad = if ($pesterMod) { $pesterMod } else { "Pester" }
Import-Module $moduleToLoad -Force

$currentPesterVer = (Get-Module Pester).Version
Write-Host "Loaded Pester version: $currentPesterVer" -ForegroundColor DarkGray

$TestFiles = Get-ChildItem -Path $ScriptDir -Filter "*.Tests.ps1" | ForEach-Object { $_.FullName }

Write-Host "Discovered $($TestFiles.Count) test suites in $ScriptDir" -ForegroundColor Yellow

# Dynamically construct parameters for cross-version Pester compatibility (v4 vs v5/v6)
$pesterCmd = Get-Command -Name Invoke-Pester
$pesterParams = @{
    PassThru = $true
}

if ($pesterCmd.Parameters.ContainsKey('Path')) {
    $pesterParams['Path'] = $TestFiles
} else {
    $pesterParams['Script'] = $TestFiles
}

$result = Invoke-Pester @pesterParams

$passed = if ($null -ne $result.PassedCount) { $result.PassedCount } else { 0 }
$failed = if ($null -ne $result.FailedCount) { $result.FailedCount } else { 0 }
$total  = if ($null -ne $result.TotalCount)  { $result.TotalCount }  else { $passed + $failed }

Write-Host "`n------------------------------------------------------------" -ForegroundColor DarkCyan
Write-Host " Test Summary: Passed=$passed, Failed=$failed, Total=$total" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host " https://isora.com.co" -ForegroundColor Gray
Write-Host "------------------------------------------------------------" -ForegroundColor DarkCyan

if ($failed -gt 0) {
    exit 1
}

if ($PassThru) {
    return $result
}


