# Mapeo de Cumplimiento Normativo — ISO/IEC 27001:2022 e ISO/IEC 20000-1:2018

**Organización Titular:** Isora Cloud Group  
**Arquitectura & Dirección:** Sandra Gabriela Puerto Torres ([sandrapuerto.com](https://sandrapuerto.com) · [GitHub: @sandrapuerto](https://github.com/sandrapuerto))  
**Implementación Agéntica:** Google Antigravity AI Engine  
**Versión del Documento:** 2.2.0  
**Propósito:** Proporcionar la matriz de trazabilidad técnica, justificación de control y evidencia de auditoría para evaluadores de acreditación, auditores de SGSI y jurados académicos.

---

## 1. Alcance y Declaración de Aplicabilidad

Sentinel-IT automatiza rutinas de higiene técnica, prevención de fuga de datos (DLP), verificación de integridad y control criptográfico sobre endpoints Windows. Las rutinas están diseñadas para generar **evidencia forense inmutable** que respalde los controles operativos de la organización dentro de su Sistema de Gestión de Seguridad de la Información (SGSI).

> **Aviso de Cumplimiento Regulatorio:** El despliegue de Sentinel-IT satisface los requerimientos técnicos de implementación y auditoría de los controles citados a continuación. La certificación institucional completa requiere además la adopción de políticas corporativas, gestión de riesgos y gobierno organizacional.

---

## 2. Matriz de Mapeo a ISO/IEC 27001:2022 (Anexo A)

| Motor de Ejecución | Control ISO 27001:2022 | Nombre del Control | Riesgo Mitigado | Implementación Técnica en Sentinel-IT | Evidencia Forense Generada |
| :--- | :---: | :--- | :--- | :--- | :--- |
| **`Invoke-CleanupScreenshots.ps1`** | **A.8.1** | Dispositivos de punto final de usuario *(User endpoint devices)* | Fuga involuntaria de información confidencial (PII, credenciales, tokens) almacenada en capturas de pantalla olvidadas en disco o sincronizadas en la nube. | Purgado periódico de vectores locales y de OneDrive (`Pictures\Screenshots`, `Capturas`) en todos los perfiles de usuario. Soporta `-AuditOnly` para simulaciones. | `ProfileCount`, `Path`, `TotalIdentified`, `TotalPurged`, nivel `SUCCESS` o `WARN`. |
| **`Invoke-DefenderScan.ps1`** | **A.8.7** | Protección contra malware *(Protection against malware)* | Infección por ransomware, spyware o troyanos debido a firmas desactualizadas o desactivación de análisis periódicos. | Actualización obligatoria de definiciones de firmas (`-SignatureUpdate`) y ejecución desatendida de análisis mediante `MpCmdRun.exe` (Quick/Full scan). | `EnginePath`, `ExitCode`, `ScanType`, detecciones de amenazas registradas en tiempo real. |
| **`Invoke-EventAudit.ps1`** | **A.8.15** | Registro *(Logging)* | Técnicas de evasión anti-forense (borrado del log de seguridad) y ataques de fuerza bruta que pasan desapercibidos. | Monitoreo del visor de eventos (`Security.evtx`) buscando el Evento 1102 (Log borrado), ráfagas del Evento 4625 (Fuerza bruta) y cambios en cuentas (4720/4726). | Alertas inmediatas en nivel `ERROR` con `EventID`, `User`, `Timestamp` y conteo de anomalías. |
| **`Invoke-IntegrityCheck.ps1`** | **A.8.20** | Seguridad de redes y sistemas *(Networks and systems security)* | Modificación no autorizada de binarios del sistema operativo, rootkits o corrupción silenciosa del almacén de componentes. | Ejecución de `DISM.exe /Online /Cleanup-Image /ScanHealth` para verificar la firma digital e integridad de la imagen del sistema operativo, con opción `-AutoRepair`. | Códigos de retorno de DISM (`0` = Healthy, `20` = Repairable, `3010` = Reboot Needed). |
| **`Invoke-BitLockerAudit.ps1`** | **A.8.24** | Uso de criptografía *(Use of cryptography)* | Acceso a datos en reposo ante robo, extravío o decomiso físico de estaciones de trabajo o servidores. | Inspección de volúmenes del SO y discos de datos fijos verificando `ProtectionStatus = On`, `EncryptionPercentage = 100%` y protectores TPM. | Estado criptográfico por volumen (`MountPoint`, `ProtectionStatus`, `KeyProtectors`). |

---

## 3. Matriz de Mapeo a ISO/IEC 20000-1:2018 (Gestión de Servicios TI)

| Motor de Ejecución | Cláusula ISO 20000-1 | Proceso de Servicio | Riesgo Operativo Mitigado | Implementación Técnica en Sentinel-IT | Evidencia Forense Generada |
| :--- | :---: | :--- | :--- | :--- | :--- |
| **`Invoke-CleanupTempGlobal.ps1`** | **§8.2** | Gestión de la Capacidad *(Capacity Management)* | Agotamiento del almacenamiento en disco del sistema (`C:\`), degradación del rendimiento de I/O y caídas del servicio por espacio insuficiente. | Saneamiento sistemático y seguro de `C:\Windows\Temp`, cachés de Windows Update (`SoftwareDistribution\Download`), colas de errores WER y carpetas Temp de usuarios. | Conteo total de elementos purgados (`TotalCleaned`, `TotalIdentified`), rutas saneadas y registros de éxito. |

---

## 4. Trazabilidad Cruzada con Otros Marcos de Seguridad

### 4.1. SOC 2 Type II (Trust Services Criteria)
* **CC6.1 (Control de Acceso Lógico):** Implementado mediante la frontera de privilegios de `Assert-IsAdmin` y ejecución estricta bajo `NT AUTHORITY\SYSTEM`.
* **CC6.6 (Protección contra Amenazas Lógicas):** Implementado mediante `Invoke-DefenderScan.ps1`.
* **CC6.7 (Protección de Datos en Tránsito y en Reposo):** Implementado mediante `Invoke-BitLockerAudit.ps1` e `Invoke-CleanupScreenshots.ps1`.
* **CC7.1 & CC7.2 (Monitoreo de Infraestructura y Gestión de Vulnerabilidades):** Implementado mediante `Invoke-EventAudit.ps1` e `Invoke-IntegrityCheck.ps1`.

### 4.2. CIS Microsoft Windows Benchmark (v2.0)
* **CIS Control 3.11 (Encrypt Sensitive Data at Rest):** Verificado por `Invoke-BitLockerAudit.ps1`.
* **CIS Control 8.2 (Audit Log Retention):** Supervisado por `Invoke-EventAudit.ps1` (detección del Evento 1102).
* **CIS Control 10.1 (Deploy Automated Anti-Malware):** Operado por `Invoke-DefenderScan.ps1`.

---

## 5. Guía para Auditores: Recolección de Evidencias

Para validar la conformidad operativa durante una auditoría formal:

1. **Evidencia de Ejecución Programada:**
   ```powershell
   Get-ScheduledTask -TaskPath "\Sentinel-IT\" | Select-Object TaskName, State, LastRunTime, LastTaskResult
   ```
2. **Evidencia de Pistas de Auditoría Forense:**
   ```powershell
   Get-ChildItem -Path "C:\Program Files\IT_Maintenance\logs" -Recurse -Filter "*.log" | Select-Object Name, Length, LastWriteTime
   ```
3. **Muestra de Registro Forense NDJSON (para ingestores SIEM):**
   ```powershell
   Get-Content (Get-ChildItem "C:\Program Files\IT_Maintenance\logs\ISO_BitLocker_Audit\*.log" | Select-Object -Last 1).FullName | Select-Object -Last 5
   ```
4. **Verificación de Pruebas Unitarias de Regresión (CI/CD):**
   ```powershell
   powershell.exe -ExecutionPolicy Bypass -File .\tests\Invoke-Tests.ps1
   ```
