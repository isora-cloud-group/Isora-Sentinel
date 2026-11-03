<#
.SYNOPSIS
    Static AST Integrity and Syntax Validation for Isora Sentinel.
.DESCRIPTION
    Validates that all production PowerShell modules and engine scripts
    parse without syntax errors via the System.Management.Automation AST Parser.
#>

BeforeDiscovery {
    $ProjectRoot = (Get-Item (Join-Path $PSScriptRoot "..")).FullName
    $CandidateFiles = Get-ChildItem -Path $ProjectRoot -Recurse -Include *.ps1, *.psm1 |
        Where-Object { $_.FullName -notmatch "[\\/]tests[\\/]" }

    $FilesToValidate = @($CandidateFiles | ForEach-Object {
        @{
            Path = $_.FullName
            Name = $_.FullName.Substring($ProjectRoot.Length)
        }
    })
}

Describe "Isora Sentinel Syntax and AST Integrity" {
    It "File <Name> has valid PowerShell AST syntax" -ForEach $FilesToValidate {
        $tokens = $null
        $parseErrors = $null
        $null = [System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$parseErrors)

        if ($parseErrors.Count -gt 0) {
            $errMessages = ($parseErrors | ForEach-Object { $_.Message }) -join "; "
            throw "Syntax errors detected in $($Name): $errMessages"
        }

        $parseErrors.Count | Should -Be 0
        $rawBytes = [System.IO.File]::ReadAllBytes($Path)
        $hasUtf8Bom = ($rawBytes.Length -ge 3 -and $rawBytes[0] -eq 0xEF -and $rawBytes[1] -eq 0xBB -and $rawBytes[2] -eq 0xBF)
        $hasUtf8Bom | Should -BeTrue
    }
}

