## Description
A clear and concise summary of what changes this PR introduces.

## Type of Change
- [ ] Bug fix (non-breaking change fixing an issue)
- [ ] New engine / feature (adds functionality mapped to a control)
- [ ] Refactoring (optimizes code without altering behavior)
- [ ] Documentation update
- [ ] Test suite improvement

## Security & Architectural Checklist
- [ ] Explicit privilege verification (`Assert-IsAdmin`) is present if code runs under elevated context.
- [ ] Error handling uses structured `try/catch` and logs via `Write-AuditEntry`.
- [ ] No hardcoded passwords, tokens, API keys, or private user paths are committed.
- [ ] All Pester unit tests pass locally (`tests\Invoke-Tests.ps1`).
- [ ] UTF-8 log output and SIEM metadata structure are preserved.
