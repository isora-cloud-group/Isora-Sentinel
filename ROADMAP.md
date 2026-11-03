# Hoja de Ruta (Roadmap) — Isora Sentinel Suite

**Autora y Titular:** Sandra Gabriela Puerto Torres (SGPT)  
**Organización:** Isora Cloud Group  
**Licencia:** Apache License 2.0  

Este documento define la visión estratégica y las mejoras técnicas planificadas a corto, mediano y largo plazo para **Isora Sentinel Suite**, garantizando la trazabilidad arquitectónica tanto para desarrolladores Junior como Senior.

---

## 1. Arquitectura Actual y Consolidación (v2.5.x — Rama Principal `main`)

**Objetivo:** Mantener un agente de gobernanza, auditoría y mantenimiento **100% libre de dependencias externas (Zero-Dependency)** que funcione de forma inmediata en cualquier instalación limpia de Windows 10 y Windows 11 (22H2 / 23H2 / 24H2) usando el runtime preinstalado **.NET Framework 4.8** y compilación nativa.

### Entregables de la Serie v2.5.x:
- [x] **Arquitectura Modular por Capas (`SentinelCore`):**
  - Separación de responsabilidades entre lógica de negocio/seguridad (`SentinelCore.cs`) y vistas de interfaz de usuario (`SentinelTray.cs`, `TaskConfigForm.cs`, `SentinelSetup.cs`, `UninstallForm.cs`).
  - Catálogo centralizado de motores (`SentinelCatalog`) para agregar nuevas rutinas desde un único punto de configuración.
- [x] **Blindaje de Seguridad y Mitigación de Escalada de Privilegios (LPE):**
  - Prevención de inyección de comandos en cancelación de tareas (`.progress.json`) mediante listas blancas estrictas y ejecución exclusiva con `-EncodedCommand` (Base64 UTF-16LE).
  - Protección contra condiciones de carrera TOCTOU (*Time-Of-Check to Time-Of-Use*) con validación completa de jerarquía de directorios contra *ReparsePoints* (*Junctions* y *Symlinks* intermedios) en ejecuciones bajo `NT AUTHORITY\SYSTEM`.
  - Aplicación de listas de control de acceso (ACLs) nativas mediante `System.Security.AccessControl` (`DirectorySecurity` y `WellKnownSidType`).
- [x] **Soporte Avanzado para Cuentas de Usuario Estándar y UAC Estricto (*Over-the-Shoulder*):**
  - Visibilidad bidireccional de progreso en tiempo real entre usuarios estándar, administradores elevados y `NT AUTHORITY\SYSTEM` a través de `C:\ProgramData\Isora-Sentinel\status`.
  - Resolución inteligente de colmenas de registro (`HKLM` / `HKCU` / `HKEY_USERS`) y carpetas de actividad cuando el administrador que eleva UAC es distinto al usuario de escritorio activo.
- [x] **Compatibilidad Profunda con Windows 11 (22H2 / 23H2 / 24H2):**
  - Preservación de políticas de energía para portátiles con *Modern Standby (S0 Low Power Idle)* (`AllowStartIfOnBatteries`, `DontStopIfGoingOnBatteries`, `StartWhenAvailable`).
  - Limpieza de capturas temporales ocultas de la nueva *Herramienta Recortes* (`Microsoft.ScreenSketch_8wekyb3d8bbwe\TempState`).
  - Decodificación automática de XML (`UTF-16LE` / `UTF-8` / `OEM`) en consultas a `schtasks.exe`.
  - Caché determinista de objetos gráficos GDI (`Font`, `Region`) para evitar fugas de handles en sesiones prolongadas.

---

## 2. Edición Moderna en Rama Independiente (`.NET 8 / .NET 9 LTS`)

> **Nota Arquitectónica (Rama Dedicada):**  
> Se creará y mantendrá una **rama separada en el repositorio (`feature/dotnet-modern` / `release/dotnet8-9`)** destinada a una edición de **Isora Sentinel Suite** construida sobre **.NET 8 / .NET 9 LTS** (C# 12/13).

### Propósito de la Dualidad de Ramas:
1. **Rama `main` (.NET Framework 4.8 Nativo):** Orientada a despliegue inmediato sin prerrequisitos (*Zero-Dependency*) en estaciones de trabajo donde no se desea instalar runtimes adicionales y el ejecutable debe mantenerse por debajo de 600 KB.
2. **Rama `feature/dotnet-modern` (.NET 8 / .NET 9):** Orientada a entornos corporativos que ya distribuyen el *.NET Desktop Runtime* moderno mediante MDM/Intune/GPO o prefieren despliegues modernos con las siguientes capacidades:
   - Migración del sistema de compilación a proyectos SDK-style (`.csproj`) compatibles con `dotnet build` y `dotnet publish`.
   - Adopción de características modernas de C# 12/13 (*Nullable Reference Types*, `records` inmutables, `System.Text.Json` con generadores de código fuente).
   - Soporte nativo `ApplicationHighDpiMode.PerMonitorV2` en Windows Forms / evaluación de interfaz en **WinUI 3 / WPF**.
   - Integración directa con la API COM nativa de *Task Scheduler 2.0* sin llamadas a procesos `schtasks.exe`.

---

## 3. Evolución Funcional Futura (v2.6.0 — v3.0.0)

- **Nuevos Motores de Auditoría ISO/IEC 27001:2022:**
  - `Invoke-FirewallAudit.ps1`: Verificación de perfiles de Windows Defender Firewall (Dominio, Privado, Público) y detección de reglas entrantes permisivas (Control A.8.20).
  - `Invoke-WindowsUpdateAudit.ps1`: Verificación de parches de seguridad críticos pendientes y antigüedad de la última actualización acumulativa (Control A.8.8).
  - `Invoke-UsbStorageAudit.ps1`: Auditoría de dispositivos de almacenamiento extraíbles conectados recientemente (Control A.8.1 / Prevención de fuga de datos).
- **Exportación y Reportes Ejecutivos:**
  - Generación de resumen consolidado de cumplimiento en formato HTML/PDF autocontenido desde el monitor de bandeja para auditorías internas de TI.
- **Políticas Centralizadas (GPO / Intune ADMX):**
  - Lectura opcional de configuración desde `HKLM\SOFTWARE\Policies\Isora\Sentinel` para bloquear horarios o parámetros en entornos empresariales administrados.
