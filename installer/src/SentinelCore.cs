// ============================================================================
//  ARCHIVO: SentinelCore.cs
//  PROYECTO: Isora Sentinel Suite (Endpoint Auditing & Governance)
//  AUTORA Y TITULAR: Sandra Gabriela Puerto Torres (SGPT) - Isora Cloud Group
//  LICENCIA: Apache License 2.0
// ============================================================================
//  GUÍA PARA DESARROLLADORES (JUNIOR & SENIOR):
//  Este archivo constituye el núcleo arquitectónico compartido ("Core Layer")
//  entre el Monitor de Bandeja (Isora-Sentinel-Tray.exe), el Programador de
//  Tareas (TaskConfigForm) y el Instalador/Desinstalador (Isora-Sentinel-Setup.exe).
//
//  ¿Por qué existe este archivo?
//  1. Principio DRY (Don't Repeat Yourself): Evita duplicar la lógica de UAC,
//     ejecución de PowerShell, lectura del Programador de Tareas y ACLs.
//  2. Seguridad por Diseño (Security by Default): Centraliza la validación
//     contra inyección de comandos, ataques de enlaces simbólicos (ReparsePoints)
//     y endurecimiento de permisos NTFS con APIs nativas de .NET Framework 4.8.
//  3. Compatibilidad con Cuentas Estándar (UAC Over-the-Shoulder) y Windows 11:
//     Resuelve el aislamiento de perfiles cuando un usuario estándar eleva
//     permisos ingresando credenciales de una cuenta Administradora distinta.
// ============================================================================

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace IsoraSentinel
{
    // ========================================================================
    //  1. CATÁLOGO CENTRALIZADO DE MÓDULOS (FUENTE ÚNICA DE VERDAD)
    // ========================================================================

    /// <summary>
    /// Define los metadatos inmutables de un motor de mantenimiento o auditoría.
    /// Si en el futuro necesitas agregar una nueva rutina a Isora Sentinel,
    /// regístrala en <see cref="SentinelCatalog.AllModules"/>.
    /// </summary>
    public sealed class SentinelModuleDefinition
    {
        /// <summary>Identificador interno de la tarea en el Programador de Tareas de Windows.</summary>
        public string TaskShortName { get; private set; }

        /// <summary>Nombre del archivo de script PowerShell ubicado en la carpeta <c>engines\</c>.</summary>
        public string ScriptFileName { get; private set; }

        /// <summary>Control normativo ISO/IEC 27001:2022 o ISO/IEC 20000-1 asociado.</summary>
        public string IsoBadge { get; private set; }

        /// <summary>Título amigable en español para mostrar en la interfaz gráfica.</summary>
        public string TitleEs { get; private set; }

        /// <summary>Descripción clara de la función del módulo para el usuario final.</summary>
        public string DescriptionEs { get; private set; }

        /// <summary>Frecuencia recomendada por defecto ("Diario", "Semanal", "Mensual", "Anual").</summary>
        public string DefaultFrequency { get; private set; }

        /// <summary>Día recomendado por defecto (0=Lunes..6=Domingo en Semanal, o índice 0..30 en Mensual).</summary>
        public int DefaultDayIndex { get; private set; }

        /// <summary>Hora recomendada por defecto en formato de 24 horas ("HH:mm").</summary>
        public string DefaultTime { get; private set; }

        public SentinelModuleDefinition(
            string taskShortName,
            string scriptFileName,
            string isoBadge,
            string titleEs,
            string descriptionEs,
            string defaultFrequency,
            int defaultDayIndex,
            string defaultTime)
        {
            TaskShortName = taskShortName;
            ScriptFileName = scriptFileName;
            IsoBadge = isoBadge;
            TitleEs = titleEs;
            DescriptionEs = descriptionEs;
            DefaultFrequency = defaultFrequency;
            DefaultDayIndex = defaultDayIndex;
            DefaultTime = defaultTime;
        }
    }

    /// <summary>
    /// Catálogo centralizado con las rutinas oficiales de Isora Sentinel Suite.
    /// </summary>
    public static class SentinelCatalog
    {
        /// <summary>Nombre de la carpeta de tareas dentro del Programador de Tareas de Windows.</summary>
        public const string TaskFolderName = "Isora Sentinel";

        /// <summary>Ruta del registro donde se almacena la información de instalación (HKLM).</summary>
        public const string UninstallRegistrySubKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Isora-Sentinel";

        /// <summary>Ruta del registro para el inicio automático de aplicaciones en Windows.</summary>
        public const string RunRegistrySubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>Nombre del valor de registro para el monitor de bandeja del sistema.</summary>
        public const string AutoStartValueName = "IsoraSentinel_Tray";

        /// <summary>Lista oficial de los 7 motores de Isora Sentinel Suite.</summary>
        public static readonly SentinelModuleDefinition[] AllModules = new SentinelModuleDefinition[]
        {
            new SentinelModuleDefinition(
                "Cleanup_Screenshots",
                "Invoke-CleanupScreenshots.ps1",
                "A.8.1 · DISPOSITIVOS",
                "Limpieza de capturas de pantalla",
                "Elimina capturas guardadas en carpetas locales, caché de Recortes de Windows 11 y nubes sincronizadas.",
                "Diario", 0, "12:00"),

            new SentinelModuleDefinition(
                "Cleanup_Temp_Global",
                "Invoke-CleanupTempGlobal.ps1",
                "ISO 20000-1 · CAPACIDAD",
                "Limpieza de archivos temporales",
                "Libera espacio en carpetas temporales del sistema, informes WER y cachés sin seguir enlaces simbólicos.",
                "Semanal", 6, "02:00"),

            new SentinelModuleDefinition(
                "Defender_Scan",
                "Invoke-DefenderScan.ps1",
                "A.8.7 · ANTIMALWARE",
                "Análisis con Microsoft Defender",
                "Actualiza las firmas de seguridad y ejecuta un análisis antimalware.",
                "Semanal", 6, "03:00"),

            new SentinelModuleDefinition(
                "Integrity_Check",
                "Invoke-IntegrityCheck.ps1",
                "A.8.9 · CONFIGURACIÓN",
                "Revisión del almacén de componentes (DISM)",
                "Comprueba la integridad del almacén de componentes de Windows (WinSxS) con DISM.",
                "Semanal", 6, "04:00"),

            new SentinelModuleDefinition(
                "BitLocker_Audit",
                "Invoke-BitLockerAudit.ps1",
                "A.8.24 · CIFRADO",
                "Comprobación de cifrado BitLocker",
                "Verifica que la unidad del sistema tenga el cifrado de disco activo y los protectores TPM habilitados.",
                "Diario", 0, "09:00"),

            new SentinelModuleDefinition(
                "Event_Audit",
                "Invoke-EventAudit.ps1",
                "A.8.15 · REGISTROS",
                "Revisión de eventos de seguridad",
                "Analiza el Visor de eventos en busca de intentos fallidos de inicio de sesión, bloqueos y errores críticos.",
                "Diario", 0, "09:15"),

            new SentinelModuleDefinition(
                "Archive_Downloads",
                "Invoke-ArchiveDownloads.ps1",
                "A.8.10 · CICLO DE VIDA",
                "Archivado seguro de descargas",
                @"Relocaliza descargas de más de 30 días hacia Documentos\Isora_Sentinel_Archived\Downloads protegiéndolas con respaldo corporativo.",
                "Mensual", 0, "03:00")
        };

        /// <summary>
        /// Obtiene el nombre de la tarea programada a partir del nombre de su script PowerShell.
        /// Elimina la necesidad de mantener diccionarios duplicados en cada clase.
        /// </summary>
        public static string GetTaskNameByScript(string scriptFileName)
        {
            if (string.IsNullOrEmpty(scriptFileName)) return null;
            foreach (SentinelModuleDefinition m in AllModules)
            {
                if (string.Equals(m.ScriptFileName, scriptFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return m.TaskShortName;
                }
            }
            return null;
        }

        /// <summary>
        /// Devuelve los nombres cortos de todas las tareas registradas en el catálogo
        /// (más <c>SystemTray_Monitor</c> opcionalmente para rutinas de limpieza/desinstalación).
        /// </summary>
        public static string[] GetAllTaskNames(bool includeLegacyMonitorTask)
        {
            List<string> list = new List<string>(AllModules.Length + 1);
            foreach (SentinelModuleDefinition m in AllModules)
            {
                list.Add(m.TaskShortName);
            }
            if (includeLegacyMonitorTask)
            {
                list.Add("SystemTray_Monitor");
            }
            return list.ToArray();
        }
    }

    /// <summary>
    /// Centraliza la resolución de rutas de instalación, registro y ubicación de motores PowerShell.
    /// Elimina la duplicación de <c>LocateInstallDir</c>, <c>InstallLocationFromRegistry</c> y <c>ResolveEngine</c>
    /// entre <c>SentinelTray</c>, <c>TaskConfigForm</c>, <c>SentinelSetup</c> y <c>UninstallForm</c>.
    /// </summary>
    public static class SentinelPaths
    {
        /// <summary>
        /// Consulta en el Registro de Windows (<c>HKLM</c> 64 bits, 32 bits y <c>HKCU</c>) la ruta de instalación registrada.
        /// </summary>
        public static string GetInstallLocationFromRegistry()
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey rk = baseKey.OpenSubKey(SentinelCatalog.UninstallRegistrySubKey))
                {
                    string v = (rk != null) ? rk.GetValue("InstallLocation") as string : null;
                    if (!string.IsNullOrEmpty(v)) return v.TrimEnd('\\');
                }
                using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(SentinelCatalog.UninstallRegistrySubKey))
                {
                    string v = (rk != null) ? rk.GetValue("InstallLocation") as string : null;
                    if (!string.IsNullOrEmpty(v)) return v.TrimEnd('\\');
                }
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(SentinelCatalog.UninstallRegistrySubKey))
                {
                    string v = (rk != null) ? rk.GetValue("InstallLocation") as string : null;
                    if (!string.IsNullOrEmpty(v)) return v.TrimEnd('\\');
                }
            }
            catch
            {
            }
            return null;
        }

        /// <summary>
        /// Descubre el directorio de instalación activo en el equipo (Registro, Program Files o árbol de desarrollo).
        /// </summary>
        public static string LocateInstallDirectory()
        {
            string regLoc = GetInstallLocationFromRegistry();
            if (!string.IsNullOrEmpty(regLoc) && Directory.Exists(regLoc))
            {
                return regLoc;
            }

            string pf = Environment.GetEnvironmentVariable("ProgramW6432");
            if (string.IsNullOrEmpty(pf))
            {
                pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            }

            string defHyphen = Path.Combine(pf, "Isora-Sentinel");
            if (Directory.Exists(defHyphen)) return defHyphen;

            string defSpace = Path.Combine(pf, "Isora Sentinel");
            if (Directory.Exists(defSpace)) return defSpace;

            return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        }

        /// <summary>
        /// Resuelve la ruta absoluta de un script PowerShell de motor (<c>Invoke-*.ps1</c>),
        /// buscando tanto en entornos de producción (<c>engines\</c>) como de desarrollo (<c>src\engines\</c>).
        /// </summary>
        public static string ResolveEngineScript(string installDir, string scriptName)
        {
            string[] baseDirs = new string[]
            {
                installDir,
                @"C:\Program Files\Isora-Sentinel",
                @"C:\Program Files\Isora Sentinel",
                AppDomain.CurrentDomain.BaseDirectory,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @".."),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\src")
            };

            foreach (string dir in baseDirs)
            {
                if (string.IsNullOrEmpty(dir)) continue;
                string p1 = Path.Combine(dir, @"engines\" + scriptName);
                if (File.Exists(p1)) return p1;
                string p2 = Path.Combine(dir, @"src\engines\" + scriptName);
                if (File.Exists(p2)) return p2;
                string p3 = Path.Combine(dir, scriptName);
                if (File.Exists(p3)) return p3;
            }
            return Path.Combine(installDir ?? @"C:\Program Files\Isora-Sentinel", @"engines\" + scriptName);
        }
    }

    // ========================================================================
    //  2. SEGURIDAD, VALIDACIÓN DE ENTRADAS Y PERMISOS NTFS (ACLs NATIVAS)
    // ========================================================================

    /// <summary>
    /// Proporciona funciones de ciberseguridad defensiva, validación contra inyección
    /// de comandos y gestión nativa de permisos NTFS (ACL) sin depender de <c>icacls.exe</c>.
    /// </summary>
    public static class SentinelSecurity
    {
        /// <summary>
        /// Expresión regular de lista blanca para nombres de motores (ej. "Defender_Scan", "IntegrityCheck").
        /// Bloquea cualquier intento de inyectar comillas, puntos y coma o subexpresiones PowerShell
        /// a través de archivos <c>.progress.json</c> manipulados por un usuario estándar.
        /// </summary>
        private static readonly Regex SafeEngineIdentifierRegex = new Regex(@"^[A-Za-z0-9_\-\.]+$", RegexOptions.Compiled);

        /// <summary>
        /// Determina si el proceso actual se está ejecutando con un token elevado de Administrador.
        /// </summary>
        public static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    if (identity == null) return false;
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica que el nombre de un motor extraído de un archivo JSON sea estrictamente alfanumérico.
        /// Previene ataques de Escalada Local de Privilegios (LPE) cuando un administrador cancela una tarea.
        /// </summary>
        public static bool IsValidSafeEngineName(string engineName)
        {
            if (string.IsNullOrEmpty(engineName) || engineName.Length > 64)
            {
                return false;
            }
            return SafeEngineIdentifierRegex.IsMatch(engineName);
        }

        /// <summary>
        /// Escapa una cadena que irá encerrada entre comillas simples ('...') en PowerShell.
        /// En PowerShell, dentro de comillas simples ninguna variable ($x) ni subexpresión ($())
        /// se interpreta; el único carácter especial es la comilla simple misma, que se escapa duplicándola ('').
        /// </summary>
        public static string EscapePowerShellSingleQuoted(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Replace("'", "''");
        }

        /// <summary>
        /// Sanitiza una ruta de directorio ingresada por el usuario (por ejemplo, para archivar descargas),
        /// eliminando comillas dobles, caracteres de control y secuencias peligrosas.
        /// </summary>
        public static string SanitizeDirectoryPath(string rawPath)
        {
            if (string.IsNullOrEmpty(rawPath)) return string.Empty;
            string cleaned = rawPath.Replace("\"", "").Replace("`", "").Replace("$", "").Replace(";", "").Trim();
            return cleaned;
        }

        /// <summary>
        /// Verifica si un directorio o cualquiera de sus carpetas padre es un punto de reanálisis
        /// (Symlink, Directory Junction o Mount Point) o una raíz crítica del sistema operativo.
        /// Previene borrados o escrituras arbitrarias como SYSTEM/Admin (ataques TOCTOU).
        /// </summary>
        public static bool IsForbiddenOrReparseDirectory(string targetDir, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(targetDir))
            {
                reason = "La ruta especificada está vacía.";
                return true;
            }

            try
            {
                string fullPath = Path.GetFullPath(targetDir).TrimEnd('\\', '/');
                if (fullPath.Length < 6)
                {
                    reason = "No se permite usar una unidad raíz como carpeta de destino.";
                    return true;
                }

                string[] forbiddenExact = new string[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    @"C:\Users",
                    @"C:\Windows",
                    @"C:\Program Files",
                    @"C:\Program Files (x86)",
                    @"C:\ProgramData"
                };

                foreach (string root in forbiddenExact)
                {
                    if (string.IsNullOrEmpty(root)) continue;
                    string normRoot = Path.GetFullPath(root).TrimEnd('\\', '/');
                    if (string.Equals(fullPath, normRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        reason = string.Format("La ruta '{0}' es un directorio protegido del sistema operativo.", fullPath);
                        return true;
                    }
                }

                // Recorrer la jerarquía existente hacia arriba para detectar Symlinks / Junctions intermedios
                DirectoryInfo current = new DirectoryInfo(fullPath);
                while (current != null && current.Parent != null)
                {
                    if (current.Exists)
                    {
                        FileAttributes attrs = current.Attributes;
                        if ((attrs & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                        {
                            reason = string.Format("El directorio '{0}' es un enlace simbólico o punto de unión (ReparsePoint).", current.FullName);
                            return true;
                        }
                    }
                    current = current.Parent;
                }

                return false;
            }
            catch (Exception ex)
            {
                reason = "Ruta inválida: " + ex.Message;
                return true;
            }
        }

        /// <summary>
        /// Aplica permisos NTFS seguros utilizando las APIs nativas de .NET Framework 4.8
        /// (<see cref="DirectorySecurity"/> y <see cref="WellKnownSidType"/>):
        /// <list type="bullet">
        ///   <item><description><c>installDir</c> y <c>logsDir</c>: Control total para <c>SYSTEM</c> y <c>Administrators</c>; solo lectura/ejecución para <c>Users</c> (impide que un usuario estándar modifique scripts que corren como SYSTEM).</description></item>
        ///   <item><description><c>C:\ProgramData\Isora-Sentinel\status</c>: Lectura/escritura de estado compartida para que el monitor de un usuario estándar y las tareas elevadas vean el mismo progreso en tiempo real.</description></item>
        /// </list>
        /// </summary>
        public static void ApplyInstallationDirectoryAcls(string installDir, string logsDir)
        {
            if (!IsAdministrator()) return;

            try
            {
                SecurityIdentifier sidSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
                SecurityIdentifier sidAdmins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                SecurityIdentifier sidUsers  = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

                InheritanceFlags inheritFlags = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
                PropagationFlags propFlags = PropagationFlags.None;

                // 1. Endurecer carpeta de instalación y carpeta de logs (Users = ReadAndExecute)
                string[] readOnlyForUsersDirs = new string[] { installDir, logsDir };
                foreach (string dir in readOnlyForUsersDirs)
                {
                    if (string.IsNullOrEmpty(dir)) continue;
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    DirectoryInfo dInfo = new DirectoryInfo(dir);
                    DirectorySecurity dSec = dInfo.GetAccessControl();
                    // Deshabilitar herencia eliminando reglas heredadas previas
                    dSec.SetAccessRuleProtection(true, false);

                    dSec.AddAccessRule(new FileSystemAccessRule(sidSystem, FileSystemRights.FullControl, inheritFlags, propFlags, AccessControlType.Allow));
                    dSec.AddAccessRule(new FileSystemAccessRule(sidAdmins, FileSystemRights.FullControl, inheritFlags, propFlags, AccessControlType.Allow));
                    dSec.AddAccessRule(new FileSystemAccessRule(sidUsers, FileSystemRights.ReadAndExecute, inheritFlags, propFlags, AccessControlType.Allow));

                    dInfo.SetAccessControl(dSec);
                }

                // 2. Asegurar carpeta compartida de progreso en ProgramData\Isora-Sentinel\status
                // Aquí los usuarios interactivos necesitan Modify para escribir el progreso de "Limpieza rápida de mi cuenta"
                // y para limpiar archivos .progress.json obsoletos.
                string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string sharedStatusDir = Path.Combine(progData, @"Isora-Sentinel\status");
                if (!Directory.Exists(sharedStatusDir))
                {
                    Directory.CreateDirectory(sharedStatusDir);
                }

                DirectoryInfo statusInfo = new DirectoryInfo(sharedStatusDir);
                DirectorySecurity statusSec = statusInfo.GetAccessControl();
                statusSec.AddAccessRule(new FileSystemAccessRule(sidSystem, FileSystemRights.FullControl, inheritFlags, propFlags, AccessControlType.Allow));
                statusSec.AddAccessRule(new FileSystemAccessRule(sidAdmins, FileSystemRights.FullControl, inheritFlags, propFlags, AccessControlType.Allow));
                statusSec.AddAccessRule(new FileSystemAccessRule(sidUsers, FileSystemRights.Modify, inheritFlags, propFlags, AccessControlType.Allow));
                statusInfo.SetAccessControl(statusSec);
            }
            catch
            {
                // En caso de sistemas de archivos no-NTFS o políticas restringidas, no interrumpir el flujo
            }
        }

        /// <summary>
        /// Otorga permiso de lectura a <c>BuiltinUsersSid</c> sobre la carpeta de tareas en System32\Tasks
        /// utilizando <see cref="DirectorySecurity"/> nativo.
        /// </summary>
        public static void ApplyTaskFolderReadAcl()
        {
            if (!IsAdministrator()) return;
            try
            {
                string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string taskDir = Path.Combine(windir, @"System32\Tasks\" + SentinelCatalog.TaskFolderName);
                if (!Directory.Exists(taskDir)) return;

                SecurityIdentifier sidUsers = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                InheritanceFlags inheritFlags = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

                DirectoryInfo dInfo = new DirectoryInfo(taskDir);
                DirectorySecurity dSec = dInfo.GetAccessControl();
                dSec.AddAccessRule(new FileSystemAccessRule(sidUsers, FileSystemRights.ReadAndExecute, inheritFlags, PropagationFlags.None, AccessControlType.Allow));
                dInfo.SetAccessControl(dSec);
            }
            catch
            {
            }
        }
    }

    // ========================================================================
    //  3. EJECUTOR SEGURO DE PROCESOS Y DECODIFICADOR WINDOWS 11
    // ========================================================================

    /// <summary>
    /// Gestiona la ejecución segura de comandos de PowerShell y utilidades del sistema
    /// (<c>schtasks.exe</c>, <c>dism.exe</c>), evitando errores de comillas y problemas
    /// de codificación de caracteres en Windows 11.
    /// </summary>
    public static class SafeProcessRunner
    {
        /// <summary>
        /// Codifica un script de PowerShell en Base64 (UTF-16LE), el formato exigido por el parámetro
        /// <c>-EncodedCommand</c> de <c>powershell.exe</c>.
        /// <para>
        /// <b>Ventaja clave:</b> Al usar Base64, las comillas dobles, comillas simples, saltos de línea,
        /// tildes y caracteres especiales pasan intactos sin que el parser de línea de comandos de Windows
        /// los rompa ni permita inyecciones.
        /// </para>
        /// </summary>
        public static string EncodePowerShellCommand(string script)
        {
            if (string.IsNullOrEmpty(script)) return string.Empty;
            byte[] unicodeBytes = Encoding.Unicode.GetBytes(script);
            return Convert.ToBase64String(unicodeBytes);
        }

        /// <summary>
        /// Ejecuta un script de PowerShell de manera asíncrona en un hilo de fondo usando siempre
        /// <c>-EncodedCommand</c>, soportando elevación UAC (<c>Verb = "runas"</c>) e invocando el
        /// callback <paramref name="onCompleted"/> de forma segura en el hilo de la interfaz gráfica.
        /// </summary>
        /// <param name="script">Script de PowerShell a ejecutar.</param>
        /// <param name="elevate">Si es <c>true</c> y el proceso actual no es Administrador, solicita elevación UAC.</param>
        /// <param name="syncControl">Control o Formulario de Windows Forms para sincronizar el regreso al hilo de UI (opcional).</param>
        /// <param name="onCompleted">Acción que recibe el código de salida (0 = éxito, -1 = cancelado/error).</param>
        /// <param name="onProcessStarted">Callback opcional que recibe la instancia de <see cref="Process"/> apenas inicia.</param>
        public static void RunPowerShellAsync(
            string script,
            bool elevate,
            Control syncControl,
            Action<int> onCompleted,
            Action<Process> onProcessStarted)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                int exitCode = -1;
                Process proc = null;
                try
                {
                    string encoded = EncodePowerShellCommand(script);
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = "powershell.exe";
                    psi.Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand " + encoded;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;

                    bool requireRunAs = elevate && !SentinelSecurity.IsAdministrator();
                    if (requireRunAs)
                    {
                        psi.UseShellExecute = true;
                        psi.Verb = "runas";
                    }
                    else
                    {
                        psi.UseShellExecute = false;
                        psi.CreateNoWindow = true;
                    }

                    proc = Process.Start(psi);
                    if (proc != null)
                    {
                        if (onProcessStarted != null)
                        {
                            try { onProcessStarted(proc); } catch { }
                        }
                        proc.WaitForExit();
                        exitCode = proc.ExitCode;
                    }
                }
                catch (Win32Exception)
                {
                    // El usuario canceló el cuadro de diálogo de Control de Cuentas de Usuario (UAC)
                    exitCode = -1;
                }
                catch
                {
                    exitCode = -1;
                }
                finally
                {
                    if (proc != null)
                    {
                        try { proc.Dispose(); } catch { }
                    }
                }

                if (onCompleted != null)
                {
                    InvokeOnUiThread(syncControl, delegate { onCompleted(exitCode); });
                }
            });
        }

        /// <summary>
        /// Ejecuta un proceso de consola (como <c>schtasks.exe /Query /XML</c>) leyendo los bytes crudos
        /// de salida y detectando automáticamente la codificación correcta (UTF-16LE con o sin BOM,
        /// UTF-8 o la página de códigos OEM activa en Windows 11).
        /// <para>
        /// <b>Por qué es necesario en Windows 11:</b> <c>schtasks.exe /XML</c> declara
        /// <c>&lt;?xml version="1.0" encoding="UTF-16"?&gt;</c> o emite bytes en la página de códigos
        /// OEM (CP850/CP437 en español). Si se fuerza <c>Encoding.UTF8</c>, las vocales con tilde y la 'ñ'
        /// (como "Miércoles", "Sábado" o rutas de carpetas en español) se corrompen con el carácter ''.
        /// </para>
        /// </summary>
        public static string ReadProcessOutputSmartEncoding(string fileName, string arguments, int timeoutMs, out int exitCode)
        {
            exitCode = -1;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(fileName, arguments);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                using (Process p = Process.Start(psi))
                {
                    if (p == null) return string.Empty;

                    using (MemoryStream msOut = new MemoryStream())
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead;
                        Stream baseOut = p.StandardOutput.BaseStream;
                        while ((bytesRead = baseOut.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            msOut.Write(buffer, 0, bytesRead);
                        }

                        if (!p.WaitForExit(timeoutMs))
                        {
                            try { p.Kill(); } catch { }
                        }
                        else
                        {
                            exitCode = p.ExitCode;
                        }

                        byte[] rawBytes = msOut.ToArray();
                        return DecodeSmartBytes(rawBytes);
                    }
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Analiza un arreglo de bytes devuelto por una herramienta de Windows y selecciona
        /// la codificación exacta sin perder tildes ni caracteres especiales.
        /// </summary>
        public static string DecodeSmartBytes(byte[] rawBytes)
        {
            if (rawBytes == null || rawBytes.Length == 0) return string.Empty;

            // 1. Verificar BOM UTF-16LE (FF FE) o UTF-16BE (FE FF)
            if (rawBytes.Length >= 2)
            {
                if (rawBytes[0] == 0xFF && rawBytes[1] == 0xFE)
                {
                    return Encoding.Unicode.GetString(rawBytes, 2, rawBytes.Length - 2);
                }
                if (rawBytes[0] == 0xFE && rawBytes[1] == 0xFF)
                {
                    return Encoding.BigEndianUnicode.GetString(rawBytes, 2, rawBytes.Length - 2);
                }
            }

            // 2. Verificar BOM UTF-8 (EF BB BF)
            if (rawBytes.Length >= 3 && rawBytes[0] == 0xEF && rawBytes[1] == 0xBB && rawBytes[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(rawBytes, 3, rawBytes.Length - 3);
            }

            // 3. Detectar UTF-16LE sin BOM (frecuente en salidas XML de Windows donde cada segundo byte ASCII es 0x00)
            int zeroEven = 0;
            int zeroOdd = 0;
            int sampleLen = Math.Min(rawBytes.Length, 120);
            for (int i = 0; i < sampleLen; i++)
            {
                if (rawBytes[i] == 0)
                {
                    if (i % 2 == 0) zeroEven++;
                    else zeroOdd++;
                }
            }
            if (zeroOdd > sampleLen / 4 && zeroEven == 0)
            {
                return Encoding.Unicode.GetString(rawBytes);
            }

            // 4. Intentar UTF-8 estricto (falla si hay secuencias inválidas de CP850/Windows-1252)
            try
            {
                UTF8Encoding strictUtf8 = new UTF8Encoding(false, true);
                return strictUtf8.GetString(rawBytes);
            }
            catch
            {
                // 5. Los bytes pertenecen a la página de códigos OEM de consola (ej. CP850 en español) o ANSI
                try
                {
                    int oemCp = CultureInfo.CurrentCulture.TextInfo.OEMCodePage;
                    return Encoding.GetEncoding(oemCp).GetString(rawBytes);
                }
                catch
                {
                    return Encoding.Default.GetString(rawBytes);
                }
            }
        }

        /// <summary>
        /// Repara cadenas UTF-8 que fueron leídas erróneamente como Windows-1252 / ISO-8859-1
        /// por el analizador de Windows PowerShell 5.1 cuando un script carece de BOM UTF-8
        /// (por ejemplo: <c>"AnÃ¡lisis"</c> -> <c>"Análisis"</c>, <c>"Â·"</c> -> <c>"·"</c>).
        /// </summary>
        public static string RepairMojibake(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            if (input.IndexOf('\u00C3') < 0 && input.IndexOf('\u00C2') < 0 && input.IndexOf('\u00E2') < 0)
            {
                return input;
            }
            try
            {
                Encoding cp1252 = Encoding.GetEncoding(1252);
                byte[] raw = cp1252.GetBytes(input);
                UTF8Encoding strictUtf8 = new UTF8Encoding(false, true);
                string decoded = strictUtf8.GetString(raw);
                if (!string.IsNullOrEmpty(decoded) && decoded.IndexOf('\uFFFD') < 0)
                {
                    return decoded;
                }
            }
            catch { }
            return input;
        }

        /// <summary>
        /// Ejecuta una acción en el hilo de interfaz de usuario si el control está disponible;
        /// de lo contrario, la ejecuta directamente en el hilo actual.
        /// </summary>
        public static void InvokeOnUiThread(Control syncControl, Action action)
        {
            if (action == null) return;
            try
            {
                if (syncControl != null && !syncControl.IsDisposed && syncControl.IsHandleCreated && syncControl.InvokeRequired)
                {
                    syncControl.BeginInvoke(action);
                    return;
                }
            }
            catch
            {
            }
            action();
        }
    }

    // ========================================================================
    //  4. PUENTE DE TAREAS PROGRAMADAS Y OPTIMIZACIÓN WINDOWS 11 (S0 STANDBY)
    // ========================================================================

    /// <summary>
    /// Construye comandos seguros para el Programador de Tareas de Windows, asegurando que
    /// en equipos portátiles con Windows 11 (Modern Standby S0) las tareas no se desactiven
    /// al usar batería ni se pierdan si el equipo estaba en reposo.
    /// </summary>
    public static class TaskSchedulerBridge
    {
        /// <summary>
        /// Genera el bloque PowerShell que aplica la configuración de energía y resiliencia
        /// sobre una tarea recién creada con <c>schtasks.exe /Create</c>.
        /// <para>
        /// <b>Corrección de bug de Windows 11:</b> <c>schtasks.exe /Create</c> activa por defecto
        /// <c>DisallowStartIfOnBatteries</c> y desactiva <c>StartWhenAvailable</c>. Este método
        /// corrige ambos ajustes inmediatamente después de crear o editar la tarea.
        /// </para>
        /// </summary>
        public static string BuildHardenTaskSettingsPowerShell(string taskShortName)
        {
            if (!SentinelSecurity.IsValidSafeEngineName(taskShortName))
            {
                return string.Empty;
            }

            string safeTask = SentinelSecurity.EscapePowerShellSingleQuoted(taskShortName);
            string safeFolder = "\\" + SentinelCatalog.TaskFolderName + "\\";

            return string.Format(
                "try {{ " +
                "$st = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 2) -MultipleInstances IgnoreNew -ErrorAction SilentlyContinue; " +
                "if ($st) {{ Set-ScheduledTask -TaskPath '{0}' -TaskName '{1}' -Settings $st -ErrorAction SilentlyContinue | Out-Null }} " +
                "}} catch {{ }}; ",
                safeFolder,
                safeTask);
        }

        /// <summary>
        /// Construye el comando PowerShell blindado contra inyección para cancelar una tarea en curso,
        /// detener los subprocesos nativos (<c>MpCmdRun.exe</c>, <c>dism.exe</c>, <c>sfc.exe</c>) y
        /// eliminar los archivos de progreso residuales.
        /// </summary>
        public static string BuildSafeCancelPowerShellScript(string engineName, string progressFilePath)
        {
            // Validación estricta anti-inyección (mitiga CVE/LPE por .progress.json manipulado)
            string safeEngine = SentinelSecurity.IsValidSafeEngineName(engineName)
                ? engineName
                : "General";
            string escapedFile = SentinelSecurity.EscapePowerShellSingleQuoted(progressFilePath ?? string.Empty);

            StringBuilder sb = new StringBuilder();
            sb.Append("$ErrorActionPreference = 'SilentlyContinue'; ");

            // 1. Detener tareas programadas en estado Running y terminar PRIMERO el proceso padre powershell.exe
            //    para impedir que el script (ej. Invoke-DefenderScan.ps1) avance al siguiente paso o reescriba .progress.json.
            sb.Append("Get-ScheduledTask -TaskPath '\\Isora Sentinel\\*' | Where-Object { $_.State -eq 'Running' } | Stop-ScheduledTask; ");
            sb.Append("& schtasks.exe /End /TN 'Isora Sentinel\\Defender_Scan' 2>$null; ");
            sb.Append("& schtasks.exe /End /TN 'Isora Sentinel\\Integrity_Check' 2>$null; ");
            sb.Append(
                "Get-CimInstance Win32_Process -Filter \"Name='powershell.exe'\" | Where-Object { " +
                "  $_.CommandLine -match 'Invoke-DefenderScan|Invoke-IntegrityCheck|Invoke-Cleanup|Invoke-ArchiveDownloads|Invoke-BitLockerAudit|Invoke-EventAudit' " +
                "} | ForEach-Object { & taskkill.exe /F /PID $_.ProcessId /T 2>$null }; ");

            // 2. Limpiar de inmediato todos los archivos .progress.json para reflejar el estado en reposo sin latencia
            if (!string.IsNullOrEmpty(escapedFile))
            {
                sb.AppendFormat("Remove-Item -LiteralPath '{0}' -Force -ErrorAction SilentlyContinue; ", escapedFile);
            }

            sb.AppendFormat(
                "$dirs = @(\"$env:ProgramData\\Isora-Sentinel\\status\", \"$env:LOCALAPPDATA\\Isora-Sentinel\\status\", \"$env:TEMP\\Isora-Sentinel\\status\"); " +
                "Get-ChildItem 'C:\\Users\\*\\AppData\\Local\\Isora-Sentinel\\status' -ErrorAction SilentlyContinue | ForEach-Object {{ $dirs += $_.FullName }}; " +
                "Get-ChildItem 'C:\\Users\\*\\AppData\\Local\\Temp\\Isora-Sentinel\\status' -ErrorAction SilentlyContinue | ForEach-Object {{ $dirs += $_.FullName }}; " +
                "foreach ($d in $dirs) {{ if (Test-Path $d) {{ Get-ChildItem -Path $d -Filter '*.progress.json' -File | Remove-Item -Force }} }}; ",
                safeEngine);

            // 3. Si es Defender, enviar Stop-MpScan y -Scan -Cancel a MpCmdRun.exe y terminar procesos residuales
            sb.AppendFormat(
                "if ('{0}' -match 'Defender|Antimalware|General') {{ " +
                "  try {{ Stop-MpScan -ErrorAction SilentlyContinue }} catch {{ }}; " +
                "  $mp = \"$env:ProgramFiles\\Windows Defender\\MpCmdRun.exe\"; " +
                "  $platRoot = \"$env:ProgramData\\Microsoft\\Windows Defender\\Platform\"; " +
                "  if (Test-Path $platRoot) {{ " +
                "    $latest = Get-ChildItem -Path $platRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1; " +
                "    if ($latest) {{ $cand = Join-Path $latest.FullName 'MpCmdRun.exe'; if (Test-Path $cand) {{ $mp = $cand }} }} " +
                "  }} " +
                "  if (Test-Path $mp) {{ Start-Process -FilePath $mp -ArgumentList '-Scan -Cancel' -WindowStyle Hidden -Wait }} " +
                "  & taskkill.exe /F /IM MpCmdRun.exe /T 2>$null; " +
                "}} ",
                safeEngine);

            // 4. Si es Integridad / DISM / SFC, terminar los procesos hijos pesados
            sb.AppendFormat(
                "if ('{0}' -match 'Integrity|DISM|SFC|General') {{ " +
                "  & taskkill.exe /F /IM dism.exe /T 2>$null; " +
                "  & taskkill.exe /F /IM DismHost.exe /T 2>$null; " +
                "  & taskkill.exe /F /IM TiWorker.exe /T 2>$null; " +
                "  & taskkill.exe /F /IM sfc.exe /T 2>$null; " +
                "}} ",
                safeEngine);

            return sb.ToString();
        }
    }

    // ========================================================================
    //  5. ENTORNO MULTI-CUENTA Y UAC ESTRICTO ("OVER-THE-SHOULDER")
    // ========================================================================

    /// <summary>
    /// Resuelve las diferencias de contexto cuando un usuario estándar de Windows 11 inicia sesión
    /// y eleva permisos UAC proporcionando las credenciales de otra cuenta Administradora.
    /// </summary>
    public static class CrossAccountEnvironment
    {
        /// <summary>
        /// Devuelve todas las carpetas donde pueden existir archivos <c>*.progress.json</c>,
        /// incluyendo los perfiles de usuarios estándar cuando el proceso actual corre elevado como Administrador.
        /// </summary>
        public static string[] GetAllStatusDirectories()
        {
            List<string> dirs = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Action<string> addDir = delegate(string path)
            {
                if (!string.IsNullOrEmpty(path) && !seen.Contains(path))
                {
                    seen.Add(path);
                    dirs.Add(path);
                }
            };

            addDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Isora-Sentinel\status"));
            addDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Isora-Sentinel\status"));
            addDir(Path.Combine(Path.GetTempPath(), @"Isora-Sentinel\status"));

            // Si estamos elevados (p. ej. en TaskConfigForm bajo cuenta Administrador en UAC Over-the-Shoulder),
            // inspeccionar también las carpetas LocalAppData y Temp de los usuarios del equipo
            try
            {
                string usersRoot = @"C:\Users";
                if (Directory.Exists(usersRoot))
                {
                    foreach (string userDir in Directory.GetDirectories(usersRoot))
                    {
                        addDir(Path.Combine(userDir, @"AppData\Local\Isora-Sentinel\status"));
                        addDir(Path.Combine(userDir, @"AppData\Local\Temp\Isora-Sentinel\status"));
                    }
                }
            }
            catch
            {
            }

            return dirs.ToArray();
        }

        /// <summary>
        /// Determina cuál carpeta de registros (<c>logs</c>) abrir cuando el usuario hace clic en
        /// «Ver registros de actividad».
        /// <para>
        /// <b>Corrección de bug en cuentas estándar:</b> Los usuarios estándar escriben sus logs de
        /// limpieza rápida en <c>%LOCALAPPDATA%\Isora-Sentinel\logs</c> porque no tienen permiso de
        /// escritura en <c>C:\Program Files\Isora-Sentinel\logs</c>. Este método compara la fecha del
        /// registro más reciente en ambas ubicaciones y abre la que tenga la actividad más reciente.
        /// </para>
        /// </summary>
        public static string GetBestLogsDirectory(string installDir)
        {
            string systemLogsDir = Path.Combine(installDir ?? @"C:\Program Files\Isora-Sentinel", "logs");
            string userLogsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Isora-Sentinel\logs");

            DateTime sysLatest = GetLatestFileWriteTimeUtc(systemLogsDir);
            DateTime usrLatest = GetLatestFileWriteTimeUtc(userLogsDir);

            if (usrLatest > sysLatest && usrLatest != DateTime.MinValue)
            {
                return userLogsDir;
            }
            if (sysLatest != DateTime.MinValue)
            {
                return systemLogsDir;
            }
            if (Directory.Exists(userLogsDir))
            {
                return userLogsDir;
            }
            if (!Directory.Exists(systemLogsDir))
            {
                try { Directory.CreateDirectory(userLogsDir); return userLogsDir; } catch { }
            }
            return systemLogsDir;
        }

        private static DateTime GetLatestFileWriteTimeUtc(string directoryPath)
        {
            try
            {
                if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
                {
                    return DateTime.MinValue;
                }

                DateTime latest = DateTime.MinValue;
                DirectoryInfo di = new DirectoryInfo(directoryPath);
                foreach (FileInfo fi in di.GetFiles("*.log", SearchOption.AllDirectories))
                {
                    if (fi.LastWriteTimeUtc > latest)
                    {
                        latest = fi.LastWriteTimeUtc;
                    }
                }
                return latest;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Comprueba si el inicio automático del monitor de bandeja está activo en <c>HKCU</c> o <c>HKLM</c>.
        /// </summary>
        public static bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey kCu = Registry.CurrentUser.OpenSubKey(SentinelCatalog.RunRegistrySubKey, false))
                {
                    if (kCu != null && kCu.GetValue(SentinelCatalog.AutoStartValueName) != null)
                        return true;
                }
                using (RegistryKey kLm = Registry.LocalMachine.OpenSubKey(SentinelCatalog.RunRegistrySubKey, false))
                {
                    if (kLm != null && kLm.GetValue(SentinelCatalog.AutoStartValueName) != null)
                        return true;
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// Activa o desactiva el inicio automático del monitor de bandeja sincronizando tanto
        /// <c>HKCU</c> como <c>HKLM</c> y las colmenas montadas en <c>HKEY_USERS</c> cuando se tienen
        /// permisos de Administrador.
        /// <para>
        /// <b>Por qué es importante en UAC Estricto:</b> Si un usuario estándar abre <c>TaskConfigForm</c>
        /// (que corre elevado como <c>Administrador</c>) y desactiva o activa «Iniciar con Windows»,
        /// escribir solo en <c>HKCU</c> modificaría el perfil del Administrador y no el del usuario estándar,
        /// o dejaría la entrada global de <c>HKLM</c> intacta.
        /// </para>
        /// </summary>
        public static bool SetAutoStart(bool enable, string executablePath, out string errorMessage)
        {
            errorMessage = null;
            string quotedPath = "\"" + executablePath + "\"";
            bool anySuccess = false;

            // 1. Actualizar HKCU del proceso actual
            try
            {
                using (RegistryKey kCu = Registry.CurrentUser.OpenSubKey(SentinelCatalog.RunRegistrySubKey, true))
                {
                    if (kCu != null)
                    {
                        if (enable) kCu.SetValue(SentinelCatalog.AutoStartValueName, quotedPath);
                        else kCu.DeleteValue(SentinelCatalog.AutoStartValueName, false);
                        anySuccess = true;
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }

            // 2. Si el proceso está elevado como Administrador, actualizar también HKLM y perfiles en HKEY_USERS
            if (SentinelSecurity.IsAdministrator())
            {
                try
                {
                    using (RegistryKey kLm = Registry.LocalMachine.OpenSubKey(SentinelCatalog.RunRegistrySubKey, true))
                    {
                        if (kLm != null)
                        {
                            if (enable) kLm.SetValue(SentinelCatalog.AutoStartValueName, quotedPath);
                            else kLm.DeleteValue(SentinelCatalog.AutoStartValueName, false);
                            anySuccess = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (errorMessage == null) errorMessage = ex.Message;
                }

                // Sincronizar en las colmenas de usuarios interactivos cargadas en HKEY_USERS
                try
                {
                    foreach (string sid in Registry.Users.GetSubKeyNames())
                    {
                        if (sid.EndsWith("_Classes", StringComparison.OrdinalIgnoreCase) ||
                            sid.StartsWith(".DEFAULT", StringComparison.OrdinalIgnoreCase) ||
                            sid.StartsWith("S-1-5-18", StringComparison.OrdinalIgnoreCase) ||
                            sid.StartsWith("S-1-5-19", StringComparison.OrdinalIgnoreCase) ||
                            sid.StartsWith("S-1-5-20", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        try
                        {
                            using (RegistryKey kUser = Registry.Users.OpenSubKey(sid + @"\" + SentinelCatalog.RunRegistrySubKey, true))
                            {
                                if (kUser != null)
                                {
                                    if (enable) kUser.SetValue(SentinelCatalog.AutoStartValueName, quotedPath);
                                    else kUser.DeleteValue(SentinelCatalog.AutoStartValueName, false);
                                    anySuccess = true;
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }

                // Sincronizar también el acceso directo en CommonStartup
                try
                {
                    string commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
                    if (!string.IsNullOrEmpty(commonStartup) && Directory.Exists(commonStartup))
                    {
                        string lnkPath = Path.Combine(commonStartup, "Isora Sentinel.lnk");
                        if (!enable && File.Exists(lnkPath))
                        {
                            File.Delete(lnkPath);
                        }
                    }
                }
                catch
                {
                }
            }

            return anySuccess;
        }

        /// <summary>
        /// Cierra instancias previas del monitor de bandeja que pertenezcan únicamente a la
        /// misma sesión interactiva de Windows (<c>SessionId</c>), evitando cerrar procesos de
        /// otros usuarios (Fast User Switching en Windows 11).
        /// </summary>
        public static void TerminateOtherTrayInstancesInCurrentSession()
        {
            try
            {
                Process current = Process.GetCurrentProcess();
                int currentPid = current.Id;
                int currentSession = current.SessionId;

                Process[] existing = Process.GetProcessesByName("Isora-Sentinel-Tray");
                foreach (Process p in existing)
                {
                    try
                    {
                        if (p.Id != currentPid && p.SessionId == currentSession)
                        {
                            p.Kill();
                            p.WaitForExit(1500);
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        try { p.Dispose(); } catch { }
                    }
                }
            }
            catch
            {
            }
        }
    }

    // ========================================================================
    //  6. UTILIDADES GRÁFICAS GDI+ SIN FUGAS DE MEMORIA (WINDOWS 11)
    // ========================================================================

    /// <summary>
    /// Funciones auxiliares para gráficos GDI+ en Windows Forms que previenen fugas de
    /// handles nativos (<c>HRGN</c>, <c>HFONT</c>) durante sesiones de larga duración en Windows 11.
    /// </summary>
    public static class GdiHelper
    {
        /// <summary>
        /// Aplica una región de esquinas redondeadas a un control liberando explícitamente
        /// la región (<see cref="Region"/>) anterior si existía.
        /// <para>
        /// <b>Prevención de fuga GDI:</b> En Windows Forms, asignar <c>control.Region = new Region(path)</c>
        /// NO libera el handle <c>HRGN</c> anterior del sistema operativo. Si se llama en cada
        /// evento <c>Resize</c> o <c>Paint</c> sin hacer <c>Dispose()</c> de la región previa,
        /// el proceso agota el límite de 10.000 objetos GDI de Windows 11 y colapsa.
        /// </para>
        /// </summary>
        public static void SetRoundedRegion(Control control, int radius)
        {
            if (control == null || control.IsDisposed || control.Width <= 0 || control.Height <= 0)
            {
                return;
            }

            Region oldRegion = control.Region;
            using (GraphicsPath path = CreateRoundedRectPath(new Rectangle(0, 0, control.Width, control.Height), radius))
            {
                control.Region = new Region(path);
            }

            if (oldRegion != null)
            {
                try { oldRegion.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// Crea un <see cref="GraphicsPath"/> con esquinas redondeadas para dibujar tarjetas, botones o insignias.
        /// El llamador debe envolver el resultado en un bloque <c>using</c>.
        /// </summary>
        public static GraphicsPath CreateRoundedRectPath(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            if (diameter <= 0 || bounds.Width < diameter || bounds.Height < diameter)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
