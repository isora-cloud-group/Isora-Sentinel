# System Architecture

Sentinel-IT uses a Hub-and-Spoke module architecture designed for minimal footprint and maximum resilience.

## 1. Execution Context
All scheduled tasks are orchestrated to run as `NT AUTHORITY\SYSTEM` with `RunLevel Highest`. This allows the engines to bypass UAC prompts and access protected OS domains (like the WinSxS Component Store or multi-user profiles).

## 2. Directory Layout (Post-Install)
```text
C:\Program Files\IT_Maintenance\
├── logs\
│   ├── ISO_Cleanup_Screenshots\
│   ├── ISO_Cleanup_Temp_Global\
│   ├── ISO_Defender_Scan\
│   └── ISO_Integrity_Check\
└── Sentinel-IT\
    ├── engines\
    │   └── Invoke-*.ps1
    └── lib\
        ├── Sentinel-Core.psm1
        └── Sentinel-Logger.psm1
```

## 3. The Logging Engine (Sentinel-Logger)
Instead of monolithic logging, `Sentinel-Logger` acts as a microservice. By flipping `$global:SentinelLogFormat = "JSON"`, the output becomes instantly compatible with SIEM ingestors like Datadog, Splunk, and Azure Sentinel.
