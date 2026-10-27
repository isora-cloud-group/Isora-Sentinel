# Manual de Uso y Operación Empresarial — Sentinel-IT Suite

**Versión del Software:** 2.2.0  
**Autora & Arquitectura:** Sandra Gabriela Puerto Torres / Isora Cloud Group  
**Entorno Operativo:** Windows 10, Windows 11, Windows Server 2016 / 2019 / 2022  
**Motor de Ejecución:** Windows PowerShell 5.1+ o PowerShell Core 7+  
**Contexto de Seguridad:** `NT AUTHORITY\SYSTEM` / Administrador Local Elevado  

---

## 1. Introducción y Propósito

**Sentinel-IT** es una suite soberana de automatización y gobierno de endpoints para sistemas operativos Microsoft Windows. Diseñada bajo una arquitectura **Hub-and-Spoke**, desacopla la lógica central de bibliotecas y auditoría forense (`src/lib/`) de motores de ejecución especializados (`src/engines/`).

Cada motor implementa controles técnicos mapeados a los estándares internacionales **ISO/IEC 27001:2022** e **ISO/IEC 20000-1:2018**, generando pistas de auditoría forense inmutables tanto en texto plano legible para operadores como en formato JSON estandarizado para ingesta directa en plataformas SIEM corporativas (Splunk, Elastic, Datadog, Microsoft Sentinel).

---

## 2. Requisitos Previos y Matriz de Privilegios

### Requisitos del Sistema
1. **Sistema Operativo:** Windows 10 (versión 1809 o superior), Windows 11 o Windows Server 2016/2019/2022/2025.
2. **PowerShell:** PowerShell 5.1 (incluido de forma nativa en el SO) o PowerShell 7.x (`pwsh`).
3. **Privilegios:** Requiere elevación administrativa (`Run as Administrator` o cuenta de servicio `NT AUTHORITY\SYSTEM`).
4. **Dependencias:** **Cero dependencias externas**. No requiere gestores de paquetes (Chocolatey, Scoop), ni runtimes externos (Node.js, Python, .NET SDK adicional).

---

## 3. Despliegue e Instalación

### 3.1. Instalación Interactiva Local
Para estaciones de trabajo individuales o servidores donde se requiera parametrizar interactivamente las frecuencias de ejecución:

1. Abra una consola de **PowerShell** con privilegios de Administrador (`Ejecutar como Administrador`).
2. Navegue al directorio raíz del proyecto y ejecute:
   ```powershell
   powershell.exe -ExecutionPolicy Bypass -File .\installer\Install-Sentinel.ps1
   ```
3. El instalador:
   - Verificará los privilegios administrativos (`Assert-IsAdmin`).
   - Creará el directorio de despliegue en `C:\Program Files\IT_Maintenance\Sentinel-IT\`.
   - Creará el repositorio de logs forenses en `C:\Program Files\IT_Maintenance\logs\`.
   - Desplegará los módulos y motores de ejecución.
   - Solicitará la confirmación para registrar cada tarea programada en la carpeta `\Sentinel-IT\` del Programador de Tareas.

---

### 3.2. Despliegue Silencioso y Automatizado (Intune / GPO / SCCM / RMM)
Para despliegues masivos desatendidos a través de herramientas de administración de flotas:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\installer\Install-Sentinel.ps1 `
    -Silent `
    -ScheduleScreenshots "Daily" `
    -ScheduleTemp "Daily" `
    -ScheduleDefender "Weekly" `
    -ScheduleIntegrity "Weekly" `
    -ScheduleBitLocker "Daily" `
    -ScheduleEventAudit "Daily"
```

#### Parámetros del Instalador:
| Parámetro | Tipo | Valores Válidos | Valor por Defecto | Descripción |
| :--- | :---: | :---: | :---: | :--- |
| `-Silent` | Switch | Presente / Ausente | Ausente | Ejecuta la instalación sin interactividad del usuario. |
| `-ScheduleScreenshots` | String | `"Daily"`, `"Weekly"` | `"Daily"` | Frecuencia de la tarea `Cleanup_Screenshots`. |
| `-ScheduleTemp` | String | `"Daily"`, `"Weekly"` | `"Daily"` | Frecuencia de la tarea `Cleanup_Temp_Global`. |
| `-ScheduleDefender` | String | `"Daily"`, `"Weekly"` | `"Weekly"` | Frecuencia de la tarea `Defender_Scan`. |
| `-ScheduleIntegrity` | String | `"Daily"`, `"Weekly"` | `"Weekly"` | Frecuencia de la tarea `Integrity_Check`. |
| `-ScheduleBitLocker` | String | `"Daily"`, `"Weekly"` | `"Daily"` | Frecuencia de la tarea `BitLocker_Audit`. |
| `-ScheduleEventAudit` | String | `"Daily"`, `"Weekly"` | `"Daily"` | Frecuencia de la tarea `Event_Audit`. |

---

## 4. Catálogo y Ejecución de Motores

Todos los motores pueden ejecutarse manualmente bajo demanda por un administrador para auditorías inmediatas:

### 4.1. Saneamiento de Capturas de Pantalla (DLP)
* **Script:** `src/engines/Invoke-CleanupScreenshots.ps1`
* **Control ISO:** ISO/IEC 27001:2022 Control A.8.1 (Dispositivos de punto final del usuario)
* **Propósito:** Purga capturas de pantalla de carpetas locales y sincronizadas con OneDrive en todos los perfiles de usuario locales para prevenir fuga de datos sensibles y credenciales expuestas en imágenes.
* **Ejecución Normal:**
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-CleanupScreenshots.ps1
  ```
* **Modo Dry-Run (Simulación sin borrar):**
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-CleanupScreenshots.ps1 -AuditOnly
  ```

---

### 4.2. Recuperación de Capacidad y Limpieza Global de Temporales
* **Script:** `src/engines/Invoke-CleanupTempGlobal.ps1`
* **Control ISO:** ISO/IEC 20000-1:2018 §8.2 (Gestión de la Capacidad)
* **Propósito:** Reclama espacio en disco eliminando archivos temporales obsoletos de Windows (`C:\Windows\Temp`), descargas de parches finalizadas (`SoftwareDistribution\Download`), colas de reporte de errores (`WER\ReportQueue`) y cachés `AppData\Local\Temp` de todos los perfiles.
* **Ejecución Normal:**
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-CleanupTempGlobal.ps1
  ```
* **Modo Dry-Run:**
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-CleanupTempGlobal.ps1 -AuditOnly
  ```

---

### 4.3. Escaneo y Protección Antimalware
* **Script:** `src/engines/Invoke-DefenderScan.ps1`
* **Control ISO:** ISO/IEC 27001:2022 Control A.8.7 (Protección contra Malware)
* **Propósito:** Actualiza firmas de seguridad y despacha el motor nativo de Microsoft Defender (`MpCmdRun.exe`), capturando códigos de terminación (0 = Limpio, 2 = Amenaza detectada y aislada).
* **Escaneo Completo (Por Defecto):**
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-DefenderScan.ps1 -ScanType 2
  ```
* **Escaneo Rápido (Quick Scan para rutinas diarias):**
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-DefenderScan.ps1 -ScanType 1
  ```
* **Omitir Actualización Previa de Firmas:**
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-DefenderScan.ps1 -SkipSignatureUpdate
  ```

---

### 4.4. Auditoría de Integridad del Almacén de Componentes
* **Script:** `src/engines/Invoke-IntegrityCheck.ps1`
* **Control ISO:** ISO/IEC 27001:2022 Control A.8.20 (Seguridad de redes y sistemas)
* **Propósito:** Audita el estado de salud de la imagen de Windows y el almacén `WinSxS` mediante `DISM.exe /Online /Cleanup-Image /ScanHealth`. Evalúa los códigos de salida estándar (0 = Íntegro, 20 = Corrupción reparable, 3010 = Reinicio requerido).
* **Auditoría de Solo Lectura:**
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-IntegrityCheck.ps1
  ```
* **Auditoría con Autorreparación Automática:**
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-IntegrityCheck.ps1 -AutoRepair
  ```

---

### 4.5. Auditoría Criptográfica de Volúmenes (BitLocker)
* **Script:** `src/engines/Invoke-BitLockerAudit.ps1`
* **Control ISO:** ISO/IEC 27001:2022 Control A.8.24 (Uso de Criptografía)
* **Propósito:** Inspecciona todos los discos fijos y del sistema operativo, verificando `ProtectionStatus` (On/Off), porcentaje de cifrado (`100%`) y existencia de protectores TPM o claves de recuperación. Emite alertas SIEM inmediatas si un volumen no cumple la política criptográfica.
* **Auditoría Global:**
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-BitLockerAudit.ps1
  ```
* **Auditoría de un Volumen Específico:**
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-BitLockerAudit.ps1 -TargetMountPoint "C:"
  ```

---

### 4.6. Auditoría de Eventos de Seguridad y Anti-Forense
* **Script:** `src/engines/Invoke-EventAudit.ps1`
* **Control ISO:** ISO/IEC 27001:2022 Control A.8.15 (Registro y Monitoreo)
* **Propósito:** Audita el registro de seguridad de Windows (`Security.evtx`) buscando:
  - **Evento 1102:** Limpieza no autorizada del registro de auditoría (Técnica de evasión anti-forense).
  - **Evento 4625:** Ráfagas de autenticación fallida (ataques de fuerza bruta o passwordspray).
  - **Eventos 4720/4726:** Creación o eliminación de cuentas de usuario locales.
* **Ejecución Estándar (Últimas 24 horas):**
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File .\src\engines\Invoke-EventAudit.ps1 -Hours 24 -FailedLogonThreshold 10
  ```

---

## 5. Configuración de Registro y Variables de Entorno

El módulo `Sentinel-Logger.psm1` puede ser gobernado a nivel corporativo mediante variables de entorno del sistema (propagadas por GPO o scripts de inicio de sesión), eliminando la necesidad de alterar los archivos en disco:

```powershell
# Establecer salida en formato JSON para ingestión por SIEM
[System.Environment]::SetEnvironmentVariable("SENTINEL_LOG_FORMAT", "JSON", "Machine")

# Redirigir la ruta raíz de almacenamiento de logs
[System.Environment]::SetEnvironmentVariable("SENTINEL_LOG_DIR", "D:\EnterpriseLogs\Sentinel", "Machine")
```

### Estructura de Directorios de Registro
```text
C:\Program Files\IT_Maintenance\logs\
├── ISO_BitLocker_Audit\
├── ISO_Cleanup_Screenshots\
├── ISO_Cleanup_Temp_Global\
├── ISO_Defender_Scan\
├── ISO_Event_Audit\
└── ISO_Integrity_Check\
```

---

## 6. Verificación de Salud del Sistema

### 6.1. Ejecución del Suite de Pruebas Automatizadas (CI/CD Gate)
Para verificar la integridad sintáctica (AST) y operativa en la máquina anfitriona:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\tests\Invoke-Tests.ps1
```
*Salida esperada:*
```text
Test Summary: Passed=25, Failed=0, Total=25
```

### 6.2. Inspección del Programador de Tareas
Para comprobar el registro y último resultado de las tareas programadas:

```powershell
Get-ScheduledTask -TaskPath "\Sentinel-IT\" | Format-Table TaskName, State, LastTaskResult
```

---

## 7. Desinstalación Limpia

Para retirar la suite de forma completa, desregistrando las tareas programadas del sistema operativo y eliminando los binarios:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\installer\Uninstall-Sentinel.ps1
```
> **Nota de Seguridad:** La desinstalación preserva el directorio `C:\Program Files\IT_Maintenance\logs\` para salvaguardar la evidencia forense histórica previa. Si se desea purgar los logs, el administrador debe eliminarlos manualmente de forma deliberada.
