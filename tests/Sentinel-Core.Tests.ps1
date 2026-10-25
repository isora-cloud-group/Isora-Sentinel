$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModulePath = Join-Path $ScriptDir "..\src\lib\Sentinel-Core.psm1"
Import-Module $ModulePath -Force

Describe "Sentinel-Core Module Tests" {
    Context "Assert-IsAdmin Function" {
        It "Should be exported and callable" {
            (Get-Command -Module Sentinel-Core -Name Assert-IsAdmin) | Should Not BeNullOrEmpty
        }

        It "Validates current administrative role deterministically" {
            $isRealAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
            if ($isRealAdmin) {
                { Assert-IsAdmin } | Should Not Throw
            } else {
                { Assert-IsAdmin } | Should Throw "Access Denied"
            }
        }
    }

    Context "Get-DefenderPlatformPath Function" {
        It "Should be exported and return a valid path string" {
            (Get-Command -Module Sentinel-Core -Name Get-DefenderPlatformPath) | Should Not BeNullOrEmpty
            $path = Get-DefenderPlatformPath
            $path | Should Not BeNullOrEmpty
            $path -match "MpCmdRun\.exe$" | Should Be $true
        }
    }
}
