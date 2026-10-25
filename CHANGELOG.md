# Changelog

All notable changes to **Sentinel-IT** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [2.1.0] - 2026-10-25

### Added
- Standardized open-source community files: `CODE_OF_CONDUCT.md`, `CONTRIBUTING.md`, and enterprise `SECURITY.md`.
- Automated test suite with Pester unit tests and cross-platform AST syntax validator (`tests/`).
- GitHub Actions CI workflow for script validation and automated testing (`.github/workflows/test.yml`).
- Pull request template (`.github/PULL_REQUEST_TEMPLATE.md`).
- Explicit `.gitignore` for Windows endpoint artifacts, runtime logs, and test results.

### Changed
- Standardized project license to official **MIT License** for unrestricted adoption.
- Enhanced `Sentinel-Logger.psm1` with robust null-metadata handling and explicit UTF-8 encoding.
- Modernized documentation, control mappings (ISO 27001 / ISO 20000-1), and architecture references.

---

## [2.0.0] - 2026-04-10

### Added
- Hub-and-Spoke modular architecture with `src/lib/` and `src/engines/`.
- Dual logging formats: Human-readable text and SIEM-ingestible JSON with timestamp, PID, and identity.
- ISO 27001:2022 and ISO 20000-1 control mapping.
- Silent enterprise installer supporting automated Intune/SCCM deployment.

---

## [1.0.0] - 2025-11-15

### Added
- Initial standalone PowerShell automation scripts for endpoint hygiene and malware scanning.
