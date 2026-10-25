# Security Policy & Responsible Vulnerability Disclosure

**Maintainer & Architecture:** Sandra Gabriela Puerto Torres / Isora Cloud Group  
**Security Operations:** [security@isora.com.co](mailto:security@isora.com.co)  
**General Inquiries:** [contact@isora.com.co](mailto:contact@isora.com.co) · [contacto@sandrapuerto.com](mailto:contacto@sandrapuerto.com)  

---

## 1. Supported Versions

| Version | Supported          | Security Maintenance Level |
| :--- | :---: | :--- |
| **2.x (Current)** | :white_check_mark: | Full patches, critical vulnerability remediation |
| **1.x** | :white_check_mark: | Critical security patches only |
| **< 1.0** | :x: | End of Life (Upgrade to 2.x recommended) |

---

## 2. Threat Model & Privilege Context

Because Sentinel-IT runs under the highest operating system privileges (`NT AUTHORITY\SYSTEM` or elevated Local Administrator) to perform forensic cleanup and system maintenance, it is subject to strict local security assumptions:

1. **Local Privilege Escalation (LPE) Defense:**
   The installation directory (`C:\Program Files\IT_Maintenance\Sentinel-IT\`) must inherit strict Windows ACLs allowing write permissions only to `BUILTIN\Administrators` and `NT AUTHORITY\SYSTEM`. Standard unprivileged users must have read/execute-only rights.
2. **Execution Integrity:**
   Scheduled tasks invoke scripts via `-ExecutionPolicy Bypass` with fixed target paths. Script tampering by non-privileged accounts must be prevented by operating system ACL enforcement.
3. **Audit Trail Immutability:**
   Forensic logs in `C:\Program Files\IT_Maintenance\logs\` record process execution, user identity, PID, and timestamp. Log directories should be forwarded to an external SIEM to prevent post-compromise tampering.

---

## 3. Reporting a Vulnerability

If you discover a security vulnerability, privilege escalation vector, or boundary weakness within Sentinel-IT, please **do not open a public issue**. Report it immediately and privately via:

> **[security@isora.com.co](mailto:security@isora.com.co)**  
> *(CC: [contacto@sandrapuerto.com](mailto:contacto@sandrapuerto.com))*

### Please include in your report:
* Script or engine affected (e.g., `Install-Sentinel.ps1`, `Sentinel-Logger.psm1`, or an engine).
* Step-by-step reproduction instructions or proof-of-concept (PoC).
* Potential impact (e.g., unauthorized file deletion, arbitrary code execution).
* Any suggested remediation.

---

## 4. Response & Remediation Commitment

* **Acknowledgment:** We will acknowledge receipt within **48 hours**.
* **Assessment & Fix:** We will evaluate the report, prepare a patch, and coordinate testing in an isolated staging environment.
* **Coordinated Disclosure:** We will release a security advisory and patch, giving credit to the reporter (unless anonymity is requested).
