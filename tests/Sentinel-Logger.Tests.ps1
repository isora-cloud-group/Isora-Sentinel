$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModulePath = Join-Path $ScriptDir "..\src\lib\Sentinel-Logger.psm1"
Import-Module $ModulePath -Force

Describe "Sentinel-Logger Module Tests" {
    $TestDir = Join-Path $env:TEMP ("SentinelTest_" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $TestDir -Force | Out-Null
    $global:SentinelLogDir = $TestDir

    It "Initialize-SentinelLog creates log directory and timestamped log file" {
        Initialize-SentinelLog -EngineName "TestEngine"
        $global:CurrentLogFile | Should Not BeNullOrEmpty
        Test-Path (Split-Path -Parent $global:CurrentLogFile) | Should Be $true
    }

    Context "Text Log Format" {
        $global:SentinelLogFormat = "TEXT"

        It "Writes formatted text audit entries" {
            Initialize-SentinelLog -EngineName "TextTestEngine"
            Write-AuditEntry -Message "Unit test message" -Level "INFO" -Metadata @{ Key1 = "Val1"; Key2 = 123 }
            Test-Path $global:CurrentLogFile | Should Be $true
            
            $content = Get-Content -Path $global:CurrentLogFile -Raw
            $content | Should Match "Unit test message"
            $content | Should Match "INFO"
            $content | Should Match "Key1=Val1"
        }

        It "Handles empty and null metadata without throwing" {
            { Write-AuditEntry -Message "No metadata message" } | Should Not Throw
            { Write-AuditEntry -Message "Null metadata message" -Metadata $null } | Should Not Throw
        }
    }

    Context "JSON SIEM Log Format" {
        $global:SentinelLogFormat = "JSON"

        It "Outputs valid JSON entries parseable by SIEM ingestion pipelines" {
            Initialize-SentinelLog -EngineName "JsonTestEngine"
            Write-AuditEntry -Message "SIEM test alert" -Level "WARN" -Metadata @{ HostRole = "DC"; CVE = "None" }
            Test-Path $global:CurrentLogFile | Should Be $true
            
            $rawLine = Get-Content -Path $global:CurrentLogFile | Select-Object -Last 1
            $rawLine | Should Not BeNullOrEmpty
            
            $parsed = $rawLine | ConvertFrom-Json
            $parsed.Message | Should Be "SIEM test alert"
            $parsed.Level | Should Be "WARN"
            $parsed.PID | Should Not BeNullOrEmpty
            $parsed.Timestamp | Should Not BeNullOrEmpty
            $parsed.Metadata.HostRole | Should Be "DC"
        }
    }

    # Cleanup
    if (Test-Path $TestDir) {
        Remove-Item -Path $TestDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
