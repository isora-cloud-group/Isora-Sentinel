# Contributing to Isora Sentinel

Thank you for your interest in contributing to **Isora Sentinel**! We welcome bug reports, feature requests, engine additions, and documentation improvements.

---

## 1. Development Principles

Isora Sentinel is built for Windows endpoints operating under high privileges (`NT AUTHORITY\SYSTEM` or Local Administrator). Every contribution must strictly uphold these engineering principles:

1. **Zero External Dependencies:**
   Engines must run on native Windows PowerShell 5.1+ and PowerShell 7+ using built-in system tools (e.g., `DISM`, `MpCmdRun.exe`, `Remove-Item`). No third-party modules or compiled binaries are allowed.

2. **Explicit Privilege Verification:**
   Any script or engine performing privileged operations must explicitly call `Assert-IsAdmin` from `Sentinel-Core.psm1`.

3. **Safe-by-Default:**
   Engines capable of deleting or moving files must default to safe audit-only mode, requiring an explicit `-Apply` switch for destructive actions.

4. **Structured Audit Logging:**
   All actions (purges, scans, failures) must be logged via `Write-AuditEntry` from `Sentinel-Logger.psm1` with appropriate severity levels (`INFO`, `SUCCESS`, `WARN`, `ERROR`) and key-value metadata.

5. **Safe Error Handling:**
   Always use bounded `try/catch` blocks. Never let unhandled exceptions terminate unattended automation tasks without logging details.

---

## 2. Setting Up Your Environment

1. Clone the repository:
   ```powershell
   git clone https://github.com/sandra-puerto/Isora-Sentinel.git
   cd Isora-Sentinel
   ```

2. Run the Pester test suite to ensure the baseline passes:
   ```powershell
   powershell.exe -ExecutionPolicy Bypass -File tests\Invoke-Tests.ps1
   ```

---

## 3. Submitting Pull Requests

1. **Branch Naming:**
   Use standard prefixes: `feat/`, `fix/`, `docs/`, `refactor/`, `test/`.

2. **Commit Conventions:**
   Follow Conventional Commits:
   ```text
   feat: add BitLocker status verification engine
   fix: handle null metadata gracefully in Sentinel-Logger
   docs: expand ISO 27001 control mapping for Defender scan
   test: add Pester unit tests for Assert-IsAdmin
   ```

3. **Testing:**
   Ensure all tests pass before submitting. Add new Pester tests for any new engine or module functionality.

4. **Review:**
   All pull requests are reviewed with a focus on privilege safety, non-destructive defaults, and SIEM log compatibility.

---

## 4. Code of Conduct

Please note that this project is released with a [Contributor Code of Conduct](CODE_OF_CONDUCT.md). By participating in this project you agree to abide by its terms.

---

## 5. Licensing and Contributions

By contributing to Isora Sentinel, you agree that your contributions will be licensed under the [Apache License, Version 2.0](LICENSE). Please ensure you have the right to submit the work and that it includes appropriate attribution if based on third-party permissive work.
