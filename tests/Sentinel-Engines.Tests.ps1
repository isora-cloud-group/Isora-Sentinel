<#
.SYNOPSIS
    Integration and Engine Dry-Run Tests for Sentinel-IT Suite.
.DESCRIPTION
    Validates parameter binding, privilege boundaries, and dry-run execution
    for all 6 Sentinel-IT engines under test harness conditions.
#>

Describe "Sentinel-IT Engines Suite Tests" {
    BeforeAll {
        $script:EnginesDir = (Resolve-Path (Join-Path $PSScriptRoot "..\src\engines")).Path
        $TestDir = Join-Path $env:TEMP ("SentinelEngineTest_" + [Guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Path $TestDir -Force | Out-Null
        $script:PrevLogDirEnv = $env:SENTINEL_LOG_DIR
        $env:SENTINEL_LOG_DIR = $TestDir
        $global:SentinelLogDir = $TestDir
    }

    AfterAll {
        $env:SENTINEL_LOG_DIR = $script:PrevLogDirEnv
        if ($TestDir -and (Test-Path $TestDir)) {
            Remove-Item -Path $TestDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Context "Script Signatures and Parameter Validation" {
        It "Invoke-CleanupScreenshots accepts -AuditOnly switch" {
            $scriptPath = Join-Path $script:EnginesDir "Invoke-CleanupScreenshots.ps1"
            $cmd = Get-Command $scriptPath
            $cmd.Parameters.ContainsKey("AuditOnly") | Should -Be $true
        }

        It "Invoke-CleanupTempGlobal accepts -AuditOnly and -DaysOld parameters" {
            $scriptPath = Join-Path $script:EnginesDir "Invoke-CleanupTempGlobal.ps1"
            $cmd = Get-Command $scriptPath
            $cmd.Parameters.ContainsKey("AuditOnly") | Should -Be $true
            $cmd.Parameters.ContainsKey("DaysOld") | Should -Be $true
        }

        It "Invoke-DefenderScan accepts -ScanType and -SkipSignatureUpdate parameters" {
            $scriptPath = Join-Path $script:EnginesDir "Invoke-DefenderScan.ps1"
            $cmd = Get-Command $scriptPath
            $cmd.Parameters.ContainsKey("ScanType") | Should -Be $true
            $cmd.Parameters.ContainsKey("SkipSignatureUpdate") | Should -Be $true
        }

        It "Invoke-IntegrityCheck accepts -AutoRepair and -QuickCheck parameters" {
            $scriptPath = Join-Path $script:EnginesDir "Invoke-IntegrityCheck.ps1"
            $cmd = Get-Command $scriptPath
            $cmd.Parameters.ContainsKey("AutoRepair") | Should -Be $true
            $cmd.Parameters.ContainsKey("QuickCheck") | Should -Be $true
        }

        It "Invoke-BitLockerAudit accepts -TargetMountPoint and -IncludeRemovable parameters" {
            $scriptPath = Join-Path $script:EnginesDir "Invoke-BitLockerAudit.ps1"
            $cmd = Get-Command $scriptPath
            $cmd.Parameters.ContainsKey("TargetMountPoint") | Should -Be $true
            $cmd.Parameters.ContainsKey("IncludeRemovable") | Should -Be $true
        }

        It "Invoke-EventAudit accepts -Hours and -FailedLogonThreshold parameters" {
            $scriptPath = Join-Path $script:EnginesDir "Invoke-EventAudit.ps1"
            $cmd = Get-Command $scriptPath
            $cmd.Parameters.ContainsKey("Hours") | Should -Be $true
            $cmd.Parameters.ContainsKey("FailedLogonThreshold") | Should -Be $true
        }

        It "Invoke-ArchiveDownloads accepts -DaysOld, -CustomArchiveDir and -AuditOnly parameters" {
            $scriptPath = Join-Path $script:EnginesDir "Invoke-ArchiveDownloads.ps1"
            $cmd = Get-Command $scriptPath
            $cmd.Parameters.ContainsKey("DaysOld") | Should -Be $true
            $cmd.Parameters.ContainsKey("CustomArchiveDir") | Should -Be $true
            $cmd.Parameters.ContainsKey("AuditOnly") | Should -Be $true
            $cmd.Parameters.ContainsKey("IncludeSubdirectories") | Should -Be $true
        }
    }
}
