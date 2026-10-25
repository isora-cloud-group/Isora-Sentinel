$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = (Get-Item (Join-Path $ScriptDir "..")).FullName

Describe "Sentinel-IT Syntax and AST Integrity" {
    $Files = Get-ChildItem -Path $ProjectRoot -Recurse -Include *.ps1, *.psm1 | Where-Object { $_.FullName -notmatch "\\tests\\" }

    foreach ($file in $Files) {
        $relPath = $file.FullName.Replace($ProjectRoot, "")
        It "File $relPath has valid PowerShell AST syntax" {
            $tokens = $null
            $parseErrors = $null
            $null = [System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$parseErrors)
            if ($parseErrors.Count -gt 0) {
                $errMessages = ($parseErrors | ForEach-Object { $_.Message }) -join "; "
                throw "Syntax errors detected in $($relPath): $errMessages"
            }
            $parseErrors.Count | Should Be 0
        }
    }
}
