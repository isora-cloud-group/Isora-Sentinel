# Contributing to Sentinel-IT

Thank you for your interest in contributing to **Sentinel-IT**! We welcome bug reports, feature requests, engine additions, and documentation improvements.

---

## 1. Development Principles

Sentinel-IT is built for mission-critical enterprise Windows endpoints operating under high privileges (`NT AUTHORITY\SYSTEM` or Local Administrator). Every contribution must strictly uphold these engineering principles:

1. **Zero External Dependencies:**
   Engines must run on native Windows PowerShell 5.1+ and PowerShell 7+ using built-in system tools (e.g., `DISM`, `MpCmdRun.exe`, `Remove-Item`). No third-party modules or compiled binaries are allowed.

2. **Explicit Privilege Verification:**
   Any script or engine performing privileged operations must explicitly call `Assert-IsAdmin` from `Sentinel-Core.psm1`.

3. **Structured Forensic Audit Logging:**
   All actions (purges, scans, failures) must be logged via `Write-AuditEntry` from `Sentinel-Logger.psm1` with appropriate severity levels (`INFO`, `SUCCESS`, `WARN`, `ERROR`) and key-value metadata.

4. **Safe Error Handling:**
   Always use bounded `try/catch` blocks. Never let unhandled exceptions terminate unattended automation tasks without logging forensic details.

---

## 2. Setting Up Your Environment

1. Clone the repository:
   ```powershell
   git clone git@github.com:sandra-puerto/Sentinel-IT.git
   cd Sentinel-IT
   ```

2. Run the Pester test suite to ensure the baseline passes:
   ```powershell
   powershell -ExecutionPolicy Bypass -File tests\Invoke-Tests.ps1
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
