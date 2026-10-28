<#
.SYNOPSIS
    Unit tests for Sentinel-Core utility module.
.DESCRIPTION
    Verifies administrative privilege assertion (Assert-IsAdmin), Microsoft Defender
    executable path resolution (Get-DefenderPlatformPath), and cryptographic manifest
    integrity gating (Assert-SentinelIntegrity) aligned with ISO/IEC 27001:2022.
#>

$ModulePath = Join-Path $PSScriptRoot "..\src\lib\Sentinel-Core.psm1"
Import-Module $ModulePath -Force

Describe "Sentinel-Core Module Tests" {
    Context "Assert-IsAdmin Function" {
        It "Should be exported and callable" {
            $cmd = Get-Command -Module Sentinel-Core -Name Assert-IsAdmin -ErrorAction SilentlyContinue
            $cmd | Should -Not -BeNullOrEmpty
        }

        It "Validates current administrative role deterministically" {
            $isRealAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
            if ($isRealAdmin) {
                { Assert-IsAdmin } | Should -Not -Throw
            } else {
                { Assert-IsAdmin } | Should -Throw
            }
        }
    }

    Context "Get-DefenderPlatformPath Function" {
        It "Should be exported and return a valid path string" {
            $cmd = Get-Command -Module Sentinel-Core -Name Get-DefenderPlatformPath -ErrorAction SilentlyContinue
            $cmd | Should -Not -BeNullOrEmpty
            $path = Get-DefenderPlatformPath
            $path | Should -Not -BeNullOrEmpty
            ($path -match "MpCmdRun\.exe$") | Should -Be $true
        }
    }

    Context "Assert-SentinelIntegrity Function" {
        It "Should be exported and accept -TargetDir and -AllowMissingManifest parameters" {
            $cmd = Get-Command -Module Sentinel-Core -Name Assert-SentinelIntegrity -ErrorAction SilentlyContinue
            $cmd | Should -Not -BeNullOrEmpty
            $cmd.Parameters.ContainsKey("TargetDir") | Should -Be $true
            $cmd.Parameters.ContainsKey("AllowMissingManifest") | Should -Be $true
            $cmd.Parameters.ContainsKey("ManifestPath") | Should -Be $true
        }

        It "Succeeds gracefully when -AllowMissingManifest is passed with non-existent path" {
            $fakeManifest = Join-Path $env:TEMP ("nonexistent_" + [Guid]::NewGuid().ToString("N") + ".json")
            { Assert-SentinelIntegrity -ManifestPath $fakeManifest -AllowMissingManifest } | Should -Not -Throw
        }
    }
}
