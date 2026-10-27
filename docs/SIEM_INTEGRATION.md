# Guía de Integración SIEM — Sentinel-IT Suite

**Organización Titular:** Isora Cloud Group  
**Arquitectura & Dirección:** Sandra Gabriela Puerto Torres ([sandrapuerto.com](https://sandrapuerto.com) · [GitHub: @sandrapuerto](https://github.com/sandrapuerto))  
**Implementación Agéntica:** Google Antigravity AI Engine  
**Estándar de Cumplimiento:** ISO/IEC 27001:2022 Control A.8.15 (Registro y Monitoreo)  
**Formato de Eventos:** JSON UTF-8 (RFC 8259) delimitado por saltos de línea (NDJSON)  
**Plataformas Soportadas:** Splunk Enterprise / Cloud, Elastic Stack (ELK), Datadog Log Management, Microsoft Sentinel (Azure Monitor)  

---

## 1. Arquitectura de Ingestión

Sentinel-IT adopta un modelo desacoplado de cero impacto: en lugar de requerir conexiones de red activas o sockets TLS directos dentro de PowerShell, el motor emite eventos estructurados de forma atómica a discos locales en `C:\Program Files\IT_Maintenance\logs\<EngineName>\*.log`. 

Los agentes de reenvío estándares del mercado leen estos archivos en tiempo real y gestionan el buffer, compresión y cifrado TLS hacia la nube del SIEM:

```
┌─────────────────────────────────────────────────────────────┐
│                    Sentinel-IT Engines                      │
│ (NT AUTHORITY\SYSTEM / Zero-Dependency PowerShell 5.1/7+)   │
└──────────────────────────────┬──────────────────────────────┘
                               │
                Emisión Atómica UTF-8 (NDJSON)
                               ▼
┌─────────────────────────────────────────────────────────────┐
│       C:\Program Files\IT_Maintenance\logs\                 │
│       ├── ISO_BitLocker_Audit\*.log                         │
│       ├── ISO_Cleanup_Screenshots\*.log                     │
│       ├── ISO_Cleanup_Temp_Global\*.log                     │
│       ├── ISO_Defender_Scan\*.log                           │
│       ├── ISO_Event_Audit\*.log                             │
│       └── ISO_Integrity_Check\*.log                         │
└──────────────────────────────┬──────────────────────────────┘
                               │
            Monitoreo en Tiempo Real por Shipper
                               ▼
     ┌─────────────────────────┼─────────────────────────┐
     ▼                         ▼                         ▼
  [Splunk]                 [Elastic]                 [Datadog]
Universal Forwarder        Filebeat Agent          Datadog Agent
     │                         │                         │
     └─────────────────────────┼─────────────────────────┘
                               ▼
               ┌───────────────────────────────┐
               │    Enterprise SIEM Central    │
               │   (Alertas, SOC, Dashboards)  │
               └───────────────────────────────┘
```

---

## 2. Habilitación del Modo SIEM (JSON)

Para activar la serialización JSON en todo el endpoint:

```powershell
# Habilitación a nivel de máquina (Persistente ante reinicios)
[System.Environment]::SetEnvironmentVariable("SENTINEL_LOG_FORMAT", "JSON", "Machine")
```

---

## 3. Especificación del Esquema JSON (NDJSON)

Cada entrada de log en modo SIEM es una línea JSON autocontenida:

```json
{
  "Timestamp": "2026-10-27 18:30:15.1234567",
  "Level": "SUCCESS",
  "Host": "SRV-SEC-FIN01",
  "Identity": "NT AUTHORITY\\SYSTEM",
  "PID": 5240,
  "Message": "VOLUME COMPLIANT: C: is fully encrypted with active protection.",
  "Metadata": {
    "MountPoint": "C:",
    "VolumeType": "OperatingSystem",
    "ProtectionStatus": "On",
    "VolumeStatus": "FullyEncrypted",
    "EncryptionPct": "100",
    "KeyProtectors": "Tpm;RecoveryPassword"
  }
}
```

### Diccionario de Campos:
| Campo | Tipo | Obligatorio | Descripción |
| :--- | :---: | :---: | :--- |
| `Timestamp` | String (ISO / Preciso) | Sí | Marca temporal de microsegundos (`yyyy-MM-dd HH:mm:ss.fffffff`). |
| `Level` | String (Enum) | Sí | Nivel de severidad forense: `"INFO"`, `"SUCCESS"`, `"WARN"`, `"ERROR"`. |
| `Host` | String | Sí | Nombre de host del endpoint (`$env:COMPUTERNAME`). |
| `Identity` | String | Sí | Cuenta de seguridad que ejecuta la acción (generalmente `NT AUTHORITY\SYSTEM`). |
| `PID` | Integer | Sí | Identificador de proceso del intérprete de PowerShell. |
| `Message` | String | Sí | Descripción legible del evento de auditoría. |
| `Metadata` | Object | No | Mapa clave-valor con datos técnicos específicos del motor. |

---

## 4. Configuraciones de Agentes

### 4.1. Splunk Universal Forwarder
Agregue en `$SPLUNK_HOME\etc\system\local\inputs.conf`:

```ini
[monitor://C:\Program Files\IT_Maintenance\logs\...\*.log]
disabled = false
index = endpoint_compliance
sourcetype = sentinel:it:json
crcSalt = <SOURCE>
```

En `$SPLUNK_HOME\etc\system\local\props.conf`:
```ini
[sentinel:it:json]
INDEXED_EXTRACTIONS = json
TIMESTAMP_FIELDS = Timestamp
TIME_FORMAT = %Y-%m-%d %H:%M:%S.%7N
SHOULD_LINEMERGE = false
KV_MODE = none
```

---

### 4.2. Elastic Stack (Filebeat)
Agregue en `/etc/filebeat/filebeat.yml` o `C:\Program Files\Filebeat\filebeat.yml`:

```yaml
filebeat.inputs:
  - type: filestream
    id: sentinel-it-logs
    enabled: true
    paths:
      - 'C:\Program Files\IT_Maintenance\logs\*\*.log'
    parsers:
      - ndjson:
          target: ""
          overwrite_keys: true
          add_error_key: true
    fields:
      env: production
      service: sentinel-it
      iso_standard: "ISO-27001-2022"
    fields_under_root: true
```

---

### 4.3. Datadog Agent
Cree el archivo `C:\ProgramData\Datadog\conf.d\sentinel_it.d\conf.yaml`:

```yaml
logs:
  - type: file
    path: "C:\\Program Files\\IT_Maintenance\\logs\\*\\*.log"
    service: "sentinel-it"
    source: "powershell"
    sourcecategory: "compliance"
    tags:
      - "compliance:iso27001"
      - "compliance:iso20000"
```

---

### 4.4. Microsoft Sentinel / Azure Log Analytics
Configure una **Regla de Recopilación de Datos (DCR)** con el Agente de Azure Monitor (AMA):
* **File Pattern:** `C:\Program Files\IT_Maintenance\logs\*\*.log`
* **Custom Table:** `SentinelIT_CL`
* **Transform KQL:**
  ```kql
  source
  | extend Parsed = parse_json(RawData)
  | project
      TimeGenerated = todatetime(Parsed.Timestamp),
      Computer = tostring(Parsed.Host),
      Level = tostring(Parsed.Level),
      Message = tostring(Parsed.Message),
      Identity = tostring(Parsed.Identity),
      PID = toint(Parsed.PID),
      Metadata = Parsed.Metadata
  ```

---

## 5. Reglas de Detección y Consultas SIEM Recomendadas

### 5.1. Detección de Anti-Forense: Registro de Auditoría Borrado (Evento 1102)
* **Splunk (SPL):**
  ```spl
  index=endpoint_compliance sourcetype="sentinel:it:json" Level="ERROR" Message="*ANTI-FORENSICS*"
  | table _time, Host, Identity, Message, Metadata.EventID, Metadata.User
  ```
* **Microsoft Sentinel (KQL):**
  ```kql
  SentinelIT_CL
  | where Level == "ERROR" and Message contains "ANTI-FORENSICS"
  | project TimeGenerated, Computer, Identity, Message, tostring(Metadata.EventID)
  ```

### 5.2. Detección de Desviación Criptográfica: Disco Sin Cifrar (ISO 27001 A.8.24)
* **Splunk (SPL):**
  ```spl
  index=endpoint_compliance sourcetype="sentinel:it:json" Level="WARN" Metadata.ProtectionStatus="Off"
  | stats latest(_time) as LastSeen by Host, Metadata.MountPoint, Metadata.ProtectionStatus
  ```
* **Microsoft Sentinel (KQL):**
  ```kql
  SentinelIT_CL
  | where Level == "WARN" and tostring(Metadata.ProtectionStatus) == "Off"
  | project TimeGenerated, Computer, tostring(Metadata.MountPoint), tostring(Metadata.ProtectionStatus)
  ```

### 5.3. Detección de Ataque de Fuerza Bruta en Endpoint (Evento 4625)
* **Splunk (SPL):**
  ```spl
  index=endpoint_compliance sourcetype="sentinel:it:json" Message="*BRUTE-FORCE ALERT*"
  | table _time, Host, Metadata.Count, Metadata.Threshold
  ```

### 5.4. Saneamiento de Datos Sensibles (DLP / Screenshots Purgados)
* **Splunk (SPL):**
  ```spl
  index=endpoint_compliance sourcetype="sentinel:it:json" Level="SUCCESS" Message="*PURGE: Cleared assets*"
  | stats sum(Metadata.Count) as TotalScreenshotsPurged by Host
  ```
