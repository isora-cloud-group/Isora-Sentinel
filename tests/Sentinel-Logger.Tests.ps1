<#
.SYNOPSIS
    Enterprise unit tests for Sentinel-Logger module.
.DESCRIPTION
    Validates audit trail integrity, dual-format forensic logging (Text & JSON),
    and SIEM payload serialization aligned with ISO/IEC 27001:2022 Control A.8.15.
#>

$script:ModulePath = Join-Path $PSScriptRoot "..\src\lib\Sentinel-Logger.psm1"
Import-Module $script:ModulePath -Force

Describe "Sentinel-Logger Module Tests" {
    BeforeAll {
        $script:LoggerModule = (Resolve-Path (Join-Path $PSScriptRoot "..\src\lib\Sentinel-Logger.psm1")).Path
        $TestDir = Join-Path $env:TEMP ("SentinelTest_" + [Guid]::NewGuid().ToString("N"))
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

    It "Initialize-SentinelLog creates log directory and timestamped log file" {
        Initialize-SentinelLog -EngineName "TestEngine"
        $global:CurrentLogFile | Should -Not -BeNullOrEmpty
        (Test-Path (Split-Path -Parent $global:CurrentLogFile)) | Should -Be $true
    }

    Context "Text Log Format" {
        BeforeEach {
            $global:SentinelLogFormat = "TEXT"
        }

        It "Writes formatted text audit entries" {
            Initialize-SentinelLog -EngineName "TextTestEngine"
            Write-AuditEntry -Message "Unit test message" -Level "INFO" -Metadata @{ Key1 = "Val1"; Key2 = 123 }
            (Test-Path $global:CurrentLogFile) | Should -Be $true
            
            $content = Get-Content -Path $global:CurrentLogFile -Raw
            $content | Should -Match "Unit test message"
            $content | Should -Match "INFO"
            $content | Should -Match "Key1=Val1"
        }

        It "Regression: Handles multiple metadata keys without RemainingScripts binding error" {
            Initialize-SentinelLog -EngineName "RemainingScriptsRegression"
            {
                Write-AuditEntry -Message "Multi-metadata regression test" -Level "WARN" -Metadata @{
                    ProfileCount = 5
                    Path         = "C:\Test"
                    Status       = "Clean"
                    Code         = 0
                }
            } | Should -Not -Throw

            $content = Get-Content -Path $global:CurrentLogFile -Raw
            $content | Should -Match "ProfileCount=5"
            $content | Should -Match "Path=C:\\Test"
        }

        It "Handles empty and null metadata without throwing" {
            { Write-AuditEntry -Message "No metadata message" } | Should -Not -Throw
            { Write-AuditEntry -Message "Null metadata message" -Metadata $null } | Should -Not -Throw
        }
    }

    Context "Environment Variable Overrides" {
        It "Respects env:SENTINEL_LOG_FORMAT when specified" {
            $prevEnv = $env:SENTINEL_LOG_FORMAT
            try {
                $env:SENTINEL_LOG_FORMAT = "JSON"
                Import-Module $script:LoggerModule -Force
                $global:SentinelLogFormat | Should -Be "JSON"
            } finally {
                $env:SENTINEL_LOG_FORMAT = $prevEnv
                Import-Module $script:LoggerModule -Force
            }
        }
    }

    Context "JSON SIEM Log Format" {
        BeforeEach {
            $global:SentinelLogFormat = "JSON"
        }

        It "Outputs valid JSON entries parseable by SIEM ingestion pipelines" {
            Initialize-SentinelLog -EngineName "JsonTestEngine"
            Write-AuditEntry -Message "SIEM test alert" -Level "WARN" -Metadata @{ HostRole = "DC"; CVE = "None" }
            (Test-Path $global:CurrentLogFile) | Should -Be $true
            
            $rawLine = Get-Content -Path $global:CurrentLogFile | Select-Object -Last 1
            $rawLine | Should -Not -BeNullOrEmpty
            
            $parsed = $rawLine | ConvertFrom-Json
            $parsed.Message | Should -Be "SIEM test alert"
            $parsed.Level | Should -Be "WARN"
            $parsed.PID | Should -Not -BeNullOrEmpty
            $parsed.Timestamp | Should -Not -BeNullOrEmpty
            $parsed.Metadata.HostRole | Should -Be "DC"
        }
    }
}

