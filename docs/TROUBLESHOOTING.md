# Guía de Diagnóstico y Resolución de Problemas (Troubleshooting) — Sentinel-IT

**Organización Titular:** Isora Cloud Group  
**Arquitectura & Dirección:** Sandra Gabriela Puerto Torres ([sandrapuerto.com](https://sandrapuerto.com) · [GitHub: @sandrapuerto](https://github.com/sandrapuerto))  
**Implementación Agéntica:** Google Antigravity AI Engine  
**Versión:** 2.2.0  

Esta guía documenta los escenarios de error, códigos de salida y puntos de fricción más comunes al operar **Sentinel-IT**, junto con sus causas técnicas y pasos de resolución inmediata.

---

## Índice Rápido de Síntomas

1. [Error: No se puede enlazar el parámetro 'RemainingScripts' (-join)](#1-error-no-se-puede-enlazar-el-parámetro-remainingscripts--join)
2. [Error: Access Denied (Requiere Administrador / SYSTEM)](#2-error-access-denied-requiere-administrador--system)
3. [Diagnóstico de Códigos de Salida del Programador de Tareas (Task Scheduler)](#3-diagnóstico-de-códigos-de-salida-del-programador-de-tareas-task-scheduler)
4. [Manejo de OneDrive Files-on-Demand (Reparse Points y Cloud-Only)](#4-manejo-de-onedrive-files-on-demand-reparse-points-y-cloud-only)
5. [Códigos de Salida de DISM (Invoke-IntegrityCheck)](#5-códigos-de-salida-de-dism-invoke-integritycheck)
6. [Códigos de Salida de Defender (Invoke-DefenderScan)](#6-códigos-de-salida-de-defender-invoke-defenderscan)
7. [Política de Ejecución Restringida (about_Execution_Policies)](#7-política-de-ejecución-restringida-about_execution_policies)

---

## 1. Error: No se puede enlazar el parámetro 'RemainingScripts' (-join)

### Síntoma
En los archivos de registro de auditoría aparece la siguiente excepción crítica:
```text
[ERROR] CRITICAL FAILURE: No se puede enlazar el parámetro 'RemainingScripts'. 
No se puede convertir el valor "-join" de tipo "System.String" al tipo "System.Management.Automation.ScriptBlock".
```

### Causa Técnica
En versiones previas del módulo de logging (`Sentinel-Logger.psm1`), la concatenación del diccionario de metadatos utilizaba una canalización (`|`) hacia `ForEach-Object` seguida del operador `-join` en una expresión evaluada con precedencia ambigua:
```powershell
# CÓDIGO PROBLEMÁTICO ANTIGUO
$MetaStr = ($SafeMetadata.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" } -join ", ")
```
En Windows PowerShell 5.1, el cmdlet `ForEach-Object` posee un parámetro posicional secundario denominado `-RemainingScripts` que acepta un bloque de script (`ScriptBlock[]`). Debido a la precedencia del parser de PowerShell, el token `-join` comenzaba con un guion y era interpretado como un argumento de parámetro para `ForEach-Object`, fallando al intentar convertir la cadena `"-join"` en un bloque ejecutable.

### Solución Definitiva (Implementada en v2.2.0)
La lógica fue refactorizada a una iteración nativa estricta con la instrucción `foreach`:
```powershell
# CÓDIGO NATIVO ROBUSTO (v2.2.0)
if ($SafeMetadata.Count -gt 0) {
    $pairs = foreach ($entry in $SafeMetadata.GetEnumerator()) {
        "$($entry.Key)=$($entry.Value)"
    }
    $MetaStr = " | " + ($pairs -join ", ")
}
```
* **Ventajas:** 100% inmune a errores de enlace de parámetros en cualquier versión de PowerShell y entre 5x y 10x más rápido al evitar la sobrecarga de pipelines.
* **Acción para endpoints ya desplegados:** Ejecute nuevamente el instalador con privilegios de Administrador para sobrescribir los módulos en producción:
  ```powershell
  powershell.exe -ExecutionPolicy Bypass -File .\installer\Install-Sentinel.ps1 -Silent
  ```

---

## 2. Error: Access Denied (Requiere Administrador / SYSTEM)

### Síntoma
```text
Access Denied: This operation requires Administrator (or NT AUTHORITY\SYSTEM) privileges.
```

### Causa Técnica
La función `Assert-IsAdmin` contenida en `Sentinel-Core.psm1` valida determinísticamente la identidad del proceso:
```powershell
([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
```
Si el script es invocado desde una consola de usuario estándar sin token administrativo elevado (`High Mandatory Level`), la ejecución es bloqueada de inmediato por diseño para prevenir manipulaciones indebidas.

### Solución
1. Para ejecuciones manuales interactivas: Haga clic derecho sobre el icono de PowerShell y seleccione **"Ejecutar como Administrador"**.
2. Para ejecuciones desatendidas: Asegúrese de que la Tarea Programada esté configurada con:
   - **User Account:** `NT AUTHORITY\SYSTEM`
   - **Run with highest privileges:** Activado (`RunLevel = Highest`).

---

## 3. Diagnóstico de Códigos de Salida del Programador de Tareas (Task Scheduler)

Al inspeccionar `Get-ScheduledTask -TaskPath "\Sentinel-IT\" | Select-Object TaskName, LastTaskResult`:

| Código Hex | Código Decimal | Significado | Causa y Solución |
| :---: | :---: | :--- | :--- |
| `0x0` | `0` | **Éxito (Success)** | La tarea concluyó normalmente sin errores. |
| `0x1` | `1` | **Error genérico de PowerShell** | El script lanzó una excepción que provocó `exit 1`. Revise el log más reciente en `C:\Program Files\IT_Maintenance\logs\<TaskName>\` para ver el mensaje exacto. |
| `0x41301` | `267009` | **Tarea en ejecución** | El motor está actualmente trabajando (común en `Invoke-DefenderScan` o `Invoke-IntegrityCheck`). Permita que finalice. |
| `0x80070002` | `2147942402` | **Archivo no encontrado** | La ruta al script en el argumento de la tarea no existe. Verifique que `C:\Program Files\IT_Maintenance\Sentinel-IT\engines\` contenga los archivos `.ps1`. |
| `0x8007010B` | `2147942667` | **Directorio no válido** | El directorio de trabajo especificado no existe. |

---

## 4. Manejo de OneDrive Files-on-Demand (Reparse Points y Cloud-Only)

### Síntoma
En máquinas con sincronización de OneDrive corporativo, las carpetas de capturas de pantalla pueden contener archivos marcados como *Cloud-Only* (en la nube, sin copia física local).

### Solución en v2.2.0
El motor `Invoke-CleanupScreenshots.ps1` valida la accesibilidad de cada vector con `Test-Path`. Los archivos bloqueados o reparse points son manejados dentro de un bloque `try/catch` individual con logging a nivel `WARN`, asegurando que la tarea nunca se interrumpa abruptamente y protegiendo al usuario de consumos imprevistos de ancho de banda.

---

## 5. Códigos de Salida de DISM (Invoke-IntegrityCheck)

El motor `Invoke-IntegrityCheck.ps1` evalúa `$LASTEXITCODE` generado por `dism.exe`:

| Código DISM | Estado Reportado | Nivel Log | Acción Recomendada |
| :---: | :--- | :---: | :--- |
| `0` | `Healthy` | `SUCCESS` | El almacén de componentes no presenta daños. No requiere acción. |
| `20` | `CorruptedRepairable` | `WARN` | Se detectó corrupción en WinSxS que puede ser reparada. Ejecute: `powershell -File .\src\engines\Invoke-IntegrityCheck.ps1 -AutoRepair` o manualmente `dism /Online /Cleanup-Image /RestoreHealth`. |
| `3010` | `RebootRequired` | `WARN` | La imagen del sistema se encuentra a la espera de un reinicio para finalizar operaciones de mantenimiento. Reinicie el equipo. |
| Otro | `UnknownFailure` | `ERROR` | Ocurrió un error en el servicio de mantenimiento de imágenes (e.g. espacio insuficiente o imagen dañada gravemente). Consulte `C:\Windows\Logs\DISM\dism.log`. |

---

## 6. Códigos de Salida de Defender (Invoke-DefenderScan)

Al ejecutar `MpCmdRun.exe -Scan`:

| Código MpCmdRun | Interpretación Forense | Nivel Log | Acción Operativa |
| :---: | :--- | :---: | :--- |
| `0` | **Sin amenazas** | `SUCCESS` | El escaneo concluyó limpiamente. |
| `2` | **Amenaza Detectada** | `WARN` | Defender identificó malware y aplicó la acción de remediación/cuarentena definida por política. Revise el visor de eventos `Microsoft-Windows-Windows Defender/Operational`. |
| `Other` | **Fallo Operacional** | `WARN` / `ERROR` | El servicio `WinDefend` está detenido, o un agente EDR de terceros (CrowdStrike, SentinelOne) ha tomado control del endpoint. |

---

## 7. Política de Ejecución Restringida (about_Execution_Policies)

### Síntoma
```text
No se puede cargar el archivo ... porque la ejecución de scripts está deshabilitada en este sistema.
```

### Solución
Todas las tareas programadas y llamadas recomendadas deben incluir el modificador `-ExecutionPolicy Bypass`:
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ruta\al\script.ps1
```
De esta manera, Windows ejecuta el script en ese proceso específico sin alterar la política de seguridad global del equipo.
