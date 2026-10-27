---
name: Feature Request
about: Propose a new compliance engine, enhancement, or architectural capability
title: '[FEAT] <Short proposal title>'
labels: ['enhancement', 'compliance']
assignees: ''
---

## Proposal Overview
A clear and concise description of the proposed capability or enhancement.

## Compliance Standard & Control Objective
- **Standard:** ISO/IEC 27001:2022 / ISO/IEC 20000-1:2018 / SOC 2 / CIS Benchmark
- **Control Reference:** (e.g., A.8.x or SMS clause)
- **Risk Addressed:** What information security or operational risk does this mitigate?

## Proposed Implementation Details
- Engine script name (e.g. `Invoke-NewControlAudit.ps1`)
- Zero-dependency PowerShell approach (using native cmdlets, WMI/CIM, or Windows built-in tools)
- Parameters and switches (`-AuditOnly`, thresholds, etc.)
- Forensic metadata emitted for SIEM ingestion (`Timestamp`, `Level`, `Metadata: ...`)

## Alternative Solutions Considered
Describe any alternative implementations or workarounds you have evaluated.

## Additional Context
Add any relevant documentation, screenshots, or regulatory audit requirements.
