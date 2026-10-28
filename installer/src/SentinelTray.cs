using SentinelIT;
// ============================================================================
// Isora Sentinel Suite - Monitor de Bandeja del Sistema (System Tray)
// Copyright (c) 2026 Sandra Gabriela Puerto Torres
// Licencia: MIT (Open Source)
// ============================================================================

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SentinelIT.Tray
{
    // =========================================================================
    //  IDENTIDAD DEL PRODUCTO (un solo lugar; debe coincidir con el instalador
    //  y con TaskConfigForm: usar Branding.TaskFolder en ambos).
    // =========================================================================
    internal static class Branding
    {
        public const string ProductName   = "Isora Sentinel";
        public const string Version       = "2.3.0";
        public const string TaskFolder    = "Isora Sentinel";          // Carpeta en el Programador de tareas
        public const string UninstallKey  = "Isora-Sentinel";          // HKLM\...\Uninstall\<clave>
        public const string AutoStartName = "SentinelIT_Tray";         // Valor en ...\Run (igual que el instalador)
        public const string SetupExe      = "Isora-Sentinel-Setup.exe";
        public const string MutexName     = @"Local\IsoraSentinel.TrayMonitor";
    }

    // =========================================================================
    //  TOKENS DE DISEÑO (superficie blanca; contraste de texto >= 4.5:1)
    // =========================================================================
    internal static class TrayTheme
    {
        // Cabecera oscura de TaskConfigForm
        public static readonly Color BgDark          = Color.FromArgb(15, 23, 42);    // Slate 900
        public static readonly Color AccentSky       = Color.FromArgb(3, 105, 161);   // Sky 700 (compatibilidad)
        public static readonly Color AccentOnDark    = Color.FromArgb(56, 189, 248);  // Sky 400: usar sobre BgDark (Sky 700 da solo 3:1)

        // Menú
        public static readonly Color BgSurface       = Color.White;
        public static readonly Color BgHover         = Color.FromArgb(241, 245, 249); // Slate 100
        public static readonly Color Border          = Color.FromArgb(203, 213, 225); // Slate 300
        public static readonly Color BorderSubtle    = Color.FromArgb(226, 232, 240); // Slate 200
        public static readonly Color TextPrimary     = Color.FromArgb(15, 23, 42);    // Slate 900 (17:1)
        public static readonly Color TextSecondary   = Color.FromArgb(71, 85, 105);   // Slate 600 (7.5:1)
        public static readonly Color TextMuted       = Color.FromArgb(100, 116, 139); // Slate 500 (4.8:1; antes Slate 400 = 2.6:1)
        public static readonly Color AccentBlue      = Color.FromArgb(3, 105, 161);   // Sky 700
        public static readonly Color Emerald         = Color.FromArgb(5, 150, 105);   // Emerald 600
        public static readonly Color Amber           = Color.FromArgb(217, 119, 6);   // Amber 600
        public static readonly Color Danger          = Color.FromArgb(220, 38, 38);   // Red 600

        public static Font Font(float size)              { return new Font("Segoe UI", size, FontStyle.Regular); }
        public static Font Font(float size, FontStyle s) { return new Font("Segoe UI", size, s); }
    }

    // =========================================================================
    //  RENDERER: fondo blanco, hover con barra de acento, chevrons y checks vectoriales
    // =========================================================================
    internal class EnterpriseMenuRenderer : ToolStripProfessionalRenderer
    {
        public EnterpriseMenuRenderer() : base(new EnterpriseColors())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (Brush b = new SolidBrush(TrayTheme.BgSurface))
                e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (!(e.ToolStrip is ToolStripDropDown)) return;
            using (Pen p = new Pen(TrayTheme.Border))
                e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // Superficie continua, sin franja lateral
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            ToolStripItem it = e.Item;
            if (!it.Enabled || !(it.Selected || it.Pressed)) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(4, 1, it.Width - 8, it.Height - 3);

            using (GraphicsPath gp = RoundRect(r, 4))
            using (Brush b = new SolidBrush(TrayTheme.BgHover))
                g.FillPath(b, gp);

            using (Brush b = new SolidBrush(TrayTheme.AccentBlue))
                g.FillRectangle(b, r.X, r.Y + 6, 3, Math.Max(4, r.Height - 12));
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            ToolStripMenuItem mi = e.Item as ToolStripMenuItem;
            if (e.Item.Tag is Color)
            {
                e.TextColor = (Color)e.Item.Tag;
            }
            else if (mi != null && !string.IsNullOrEmpty(mi.ShortcutKeyDisplayString) && e.Text == mi.ShortcutKeyDisplayString)
            {
                e.TextColor = TrayTheme.TextSecondary;   // texto auxiliar a la derecha
            }
            else if (!e.Item.Enabled)
            {
                e.TextColor = TrayTheme.TextMuted;
            }
            else
            {
                e.TextColor = TrayTheme.TextPrimary;
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            if (e.Image == null) return;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(e.Image, e.ImageRectangle);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = e.ImageRectangle;
            int cx = r.Left + r.Width / 2, cy = r.Top + r.Height / 2;
            using (Pen p = new Pen(TrayTheme.AccentBlue, 2f))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                g.DrawLines(p, new Point[] { new Point(cx - 4, cy), new Point(cx - 1, cy + 3), new Point(cx + 4, cy - 3) });
            }
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = e.ArrowRectangle;
            int cx = r.Left + r.Width / 2, cy = r.Top + r.Height / 2;
            Color c = (e.Item.Selected && e.Item.Enabled) ? TrayTheme.AccentBlue : TrayTheme.TextSecondary;
            using (Pen p = new Pen(c, 1.5f))
                g.DrawLines(p, new Point[] { new Point(cx - 2, cy - 4), new Point(cx + 2, cy), new Point(cx - 2, cy + 4) });
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (Pen p = new Pen(TrayTheme.BorderSubtle))
            {
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(p, 12, y, e.Item.Width - 12, y);
            }
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath gp = new GraphicsPath();
            gp.AddArc(r.X, r.Y, d, d, 180, 90);
            gp.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            gp.CloseFigure();
            return gp;
        }
    }

    internal class EnterpriseColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground   { get { return TrayTheme.BgSurface; } }
        public override Color ImageMarginGradientBegin      { get { return TrayTheme.BgSurface; } }
        public override Color ImageMarginGradientMiddle     { get { return TrayTheme.BgSurface; } }
        public override Color ImageMarginGradientEnd        { get { return TrayTheme.BgSurface; } }
        public override Color MenuBorder                    { get { return TrayTheme.Border; } }
        public override Color MenuItemBorder                { get { return Color.Transparent; } }
        public override Color MenuItemSelected              { get { return TrayTheme.BgHover; } }
        public override Color SeparatorDark                 { get { return TrayTheme.BorderSubtle; } }
        public override Color SeparatorLight                { get { return TrayTheme.BorderSubtle; } }
    }

    // =========================================================================
    //  VERIFICACIÓN DE INTEGRIDAD (fuera del hilo de UI)
    // =========================================================================
    internal enum IntegrityState { Checking, Authorized, Tampered, NoManifest }

    internal class IntegrityReport
    {
        public IntegrityState State = IntegrityState.NoManifest;
        public int FilesChecked = 0;
        public List<string> Altered = new List<string>();
        public DateTime Timestamp = DateTime.Now;
    }

    internal static class IntegrityVerifier
    {
        public static IntegrityReport Verify(string installDir)
        {
            IntegrityReport rep = new IntegrityReport();

            string manifest = Path.Combine(installDir, "integrity.manifest.json");
            if (!File.Exists(manifest))
                manifest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\integrity.manifest.json");
            if (!File.Exists(manifest)) return rep;   // NoManifest

            try
            {
                string content = File.ReadAllText(manifest, Encoding.UTF8);
                int filesIdx = content.IndexOf("\"files\":");
                if (filesIdx < 0) return rep;

                string[] lines = content.Substring(filesIdx).Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                using (SHA256 sha = SHA256.Create())
                {
                    foreach (string line in lines)
                    {
                        if (!line.Contains(":") || !line.Contains("\"")) continue;
                        string[] parts = line.Split(':');
                        if (parts.Length != 2) continue;

                        string relFile = parts[0].Trim().Trim('"', ' ', '\t').Replace("\\\\", "\\");
                        string expected = parts[1].Trim().Trim('"', ',', ' ', '\t').ToLowerInvariant();
                        if (expected.Length != 64) continue;

                        string filePath = Path.Combine(installDir, relFile);
                        if (!File.Exists(filePath)) filePath = Path.Combine(installDir, "src\\" + relFile);
                        if (!File.Exists(filePath)) continue;

                        string calc = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(filePath)))
                                                  .Replace("-", "").ToLowerInvariant();
                        rep.FilesChecked++;
                        if (calc != expected) rep.Altered.Add(relFile);
                    }
                }

                if (rep.Altered.Count > 0) rep.State = IntegrityState.Tampered;
                else if (rep.FilesChecked > 0) rep.State = IntegrityState.Authorized;
                else rep.State = IntegrityState.NoManifest;
            }
            catch
            {
                rep.State = IntegrityState.NoManifest;
            }
            return rep;
        }
    }

    // =========================================================================
    //  CONTEXTO PRINCIPAL
    // =========================================================================
    public class SentinelTrayContext : ApplicationContext
    {
        // Códigos de resultado internos (los scripts devuelven 0 = correcto, >0 = incidencias)
        private const int ExitCanceled  = -1;    // El usuario canceló UAC o no se pudo iniciar
        private const int ExitUnknown   = -2;    // El proceso terminó pero no se pudo leer su resultado
        private const int ExitTriggered = 200;   // La tarea programada se inició en segundo plano

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_NULL = 0x0000;

        private Form dummyForm;
        private NotifyIcon trayIcon;
        private ContextMenuStrip menu;
        private ToolStripMenuItem itemIntegrity;
        private ToolStripMenuItem itemAutoStart;
        private System.Windows.Forms.Timer integrityTimer;
        private Control invoker;                 // para volver al hilo de UI
        private string installDir;
        private string logsDir;
        private string iconFont;                 // Segoe Fluent Icons / Segoe MDL2 Assets
        private float dpi = 1f;
        private TaskConfigForm configForm = null;
        private readonly HashSet<string> running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Mapeo script -> tarea programada
        private static readonly Dictionary<string, string> TaskByScript = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Invoke-CleanupScreenshots.ps1", "Cleanup_Screenshots" },
            { "Invoke-CleanupTempGlobal.ps1",  "Cleanup_Temp_Global" },
            { "Invoke-ArchiveDownloads.ps1",   "Archive_Downloads" },
            { "Invoke-DefenderScan.ps1",       "Defender_Scan" },
            { "Invoke-IntegrityCheck.ps1",     "Integrity_Check" },
            { "Invoke-BitLockerAudit.ps1",     "BitLocker_Audit" },
            { "Invoke-EventAudit.ps1",         "Event_Audit" }
        };

        // Glifos (Segoe MDL2 Assets / Segoe Fluent Icons)
        private const string G_Shield   = "\uEA18";
        private const string G_Camera   = "\uE722";
        private const string G_Delete   = "\uE74D";
        private const string G_Sync     = "\uE895";
        private const string G_Lock     = "\uE72E";
        private const string G_Document = "\uE8A5";
        private const string G_Play     = "\uE768";
        private const string G_Person   = "\uE77B";
        private const string G_Folder   = "\uE8B7";
        private const string G_Settings = "\uE713";
        private const string G_Power    = "\uE7E8";
        private const string G_Refresh  = "\uE72C";

        public SentinelTrayContext()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) { dpi = g.DpiX / 96f; }
            dummyForm = new Form();
            dummyForm.ShowInTaskbar = false;
            dummyForm.WindowState = FormWindowState.Minimized;
            dummyForm.FormBorderStyle = FormBorderStyle.None;
            dummyForm.Size = new Size(0, 0);
            IntPtr force = dummyForm.Handle;
            invoker = dummyForm;

            iconFont = PickIconFont();
            LocateDirectories();
            InitializeTray();

            RefreshIntegrityAsync(false);
            integrityTimer = new System.Windows.Forms.Timer();
            integrityTimer.Interval = 30 * 60 * 1000;   // reverificación cada 30 min
            integrityTimer.Tick += delegate { RefreshIntegrityAsync(false); };
            integrityTimer.Start();
        }

        // ---------------------------------------------------------------------
        //  Entorno
        // ---------------------------------------------------------------------
        private int Sz(int v) { return (int)Math.Round(v * dpi); }

        private static string PickIconFont()
        {
            string[] candidates = new string[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" };
            using (InstalledFontCollection fc = new InstalledFontCollection())
            {
                foreach (string name in candidates)
                    foreach (FontFamily f in fc.Families)
                        if (f.Name == name) return name;
            }
            return null;   // sin fuente de iconos: el menú queda solo con texto
        }

        private static string InstallLocationFromRegistry()
        {
            string key = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + Branding.UninstallKey;
            try
            {
                using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(key))
                {
                    string v = (rk != null) ? rk.GetValue("InstallLocation") as string : null;
                    if (!string.IsNullOrEmpty(v)) return v;
                }
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(key))
                {
                    string v = (rk != null) ? rk.GetValue("InstallLocation") as string : null;
                    if (!string.IsNullOrEmpty(v)) return v;
                }
            }
            catch { }
            return null;
        }

        private void LocateDirectories()
        {
            string regLoc = InstallLocationFromRegistry();
            if (!string.IsNullOrEmpty(regLoc) && Directory.Exists(regLoc))
            {
                installDir = regLoc;
                logsDir = Path.Combine(installDir, "logs");
                return;
            }

            string pf = Environment.GetEnvironmentVariable("ProgramW6432");
            if (string.IsNullOrEmpty(pf)) pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string defaultProg = Path.Combine(pf, "Isora Sentinel");

            if (Directory.Exists(defaultProg))
            {
                installDir = defaultProg;
                logsDir = Path.Combine(defaultProg, "logs");
            }
            else
            {
                installDir = AppDomain.CurrentDomain.BaseDirectory;   // entorno de desarrollo
                logsDir = Path.Combine(installDir, "logs");
            }
        }

        private string ResolveEngine(string scriptName)
        {
            string p = Path.Combine(installDir, @"engines\" + scriptName);
            if (!File.Exists(p)) p = Path.Combine(installDir, @"src\engines\" + scriptName);
            if (!File.Exists(p)) p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\src\engines\" + scriptName);
            if (!File.Exists(p)) p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, scriptName);
            return p;
        }

        private static bool IsAdministrator()
        {
            using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        // ---------------------------------------------------------------------
        //  Cuadros de diálogo (siempre al frente, centrados)
        // ---------------------------------------------------------------------
        private DialogResult Msg(string text, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            // Una aplicación de bandeja no tiene ventana en primer plano: sin propietario,
            // el cuadro puede quedar oculto detrás de otras ventanas.
            using (Form owner = new Form())
            {
                owner.FormBorderStyle = FormBorderStyle.None;
                owner.ShowInTaskbar = false;
                owner.StartPosition = FormStartPosition.CenterScreen;
                owner.Size = new Size(1, 1);
                owner.Opacity = 0;
                owner.TopMost = true;
                owner.Show();
                owner.Activate();
                return MessageBox.Show(owner, text, title, buttons, icon);
            }
        }

        /// <summary>Pregunta de confirmación con texto claro; añade el aviso de administrador si hace falta.</summary>
        private bool Confirm(string title, string body, bool needAdmin, bool warn)
        {
            if (string.IsNullOrEmpty(body) && !needAdmin) return true;

            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(body)) { sb.Append(body); sb.Append("\n\n"); }
            if (needAdmin)
            {
                sb.Append("Esta acción necesita permisos de administrador: Windows le pedirá confirmación (UAC). ");
                sb.Append("Si no dispone de ellos, solicite apoyo al área de TI.\n\n");
            }
            sb.Append("¿Desea continuar?");

            return Msg(sb.ToString(), Branding.ProductName + " · " + title,
                MessageBoxButtons.YesNo, warn ? MessageBoxIcon.Warning : MessageBoxIcon.Question) == DialogResult.Yes;
        }

        private void Notify(string title, string msg, ToolTipIcon icon)
        {
            trayIcon.ShowBalloonTip(3500, title, msg, icon);
        }

        // ---------------------------------------------------------------------
        //  Iconografía vectorial
        // ---------------------------------------------------------------------
        private Image Glyph(string glyph, Color color)
        {
            if (iconFont == null) return null;
            int size = Sz(16);
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font f = new Font(iconFont, size * 0.82f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(color))
            using (StringFormat sf = new StringFormat())
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(glyph, f, b, new RectangleF(0, 0, size, size), sf);
            }
            return bmp;
        }

        private Image StatusDot(Color color)
        {
            int size = Sz(16);
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int d = Sz(9);
                int o = (size - d) / 2;
                using (Brush b = new SolidBrush(color))
                    g.FillEllipse(b, o, o, d, d);
                using (Pen p = new Pen(Color.FromArgb(40, Color.Black), 1f))
                    g.DrawEllipse(p, o, o, d, d);
            }
            return bmp;
        }

        // ---------------------------------------------------------------------
        //  Construcción del menú
        // ---------------------------------------------------------------------
        private void StyleDropDown(ToolStripDropDown dd, int minWidth)
        {
            dd.Renderer = new EnterpriseMenuRenderer();
            dd.BackColor = TrayTheme.BgSurface;
            dd.Font = TrayTheme.Font(9.5F);
            dd.ShowItemToolTips = true;
            ToolStripDropDownMenu ddm = dd as ToolStripDropDownMenu;
            if (ddm != null)
            {
                ddm.ShowImageMargin = true;
                ddm.ShowCheckMargin = false;
                ddm.DropShadowEnabled = true;
                ddm.MinimumSize = new Size(Sz(minWidth), 0);
            }
        }

        private ToolStripMenuItem NewItem(string text, string glyph, EventHandler click)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(text);
            it.ImageScaling = ToolStripItemImageScaling.None;
            it.Padding = new Padding(0, Sz(5), 0, Sz(5));
            if (glyph != null) it.Image = Glyph(glyph, TrayTheme.TextSecondary);
            if (click != null) it.Click += click;
            return it;
        }

        private ToolStripMenuItem NewSectionLabel(string text)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(text);
            it.Enabled = false;
            it.Tag = TrayTheme.TextSecondary;
            it.Font = TrayTheme.Font(8F, FontStyle.Bold);
            it.Padding = new Padding(0, Sz(7), 0, Sz(2));
            return it;
        }

        /// <summary>Elemento que ejecuta un módulo; la etiqueta "Administrador" avisa de que Windows pedirá confirmación.</summary>
        private ToolStripMenuItem NewModuleItem(string text, string glyph, string tooltip, bool needsAdmin,
            string title, string script, string args, string confirmBody, bool warn)
        {
            ToolStripMenuItem it = NewItem(text, glyph, delegate { RunModule(title, script, needsAdmin, args, confirmBody, warn); });
            it.ToolTipText = tooltip;
            if (needsAdmin) it.ShortcutKeyDisplayString = "Administrador";
            return it;
        }

        private void InitializeTray()
        {
            menu = new ContextMenuStrip();
            menu.AutoClose = true;
            menu.Closed += delegate
            {
                if (dummyForm != null && !dummyForm.IsDisposed)
                    PostMessage(dummyForm.Handle, WM_NULL, IntPtr.Zero, IntPtr.Zero);
            };
            StyleDropDown(menu, 300);

            // ---- Cabecera ----
            ToolStripMenuItem itemTitle = NewItem("Isora Sentinel Suite", G_Shield, null);
            itemTitle.Enabled = false;
            itemTitle.Image = Glyph(G_Shield, TrayTheme.AccentBlue);
            itemTitle.Font = TrayTheme.Font(10.5F, FontStyle.Bold);
            itemTitle.Tag = TrayTheme.TextPrimary;
            itemTitle.Padding = new Padding(0, Sz(8), 0, Sz(0));
            menu.Items.Add(itemTitle);

            ToolStripMenuItem itemSub = NewItem("Mantenimiento y seguridad del equipo  ·  v" + Branding.Version, null, null);
            itemSub.Enabled = false;
            itemSub.Font = TrayTheme.Font(8.5F);
            itemSub.Tag = TrayTheme.TextSecondary;
            itemSub.Padding = new Padding(0, Sz(0), 0, Sz(6));
            menu.Items.Add(itemSub);

            menu.Items.Add(new ToolStripSeparator());

            // ---- Estado de la instalación (se actualiza tras comprobar) ----
            itemIntegrity = NewItem("Comprobando instalación…", null, delegate { RefreshIntegrityAsync(true); });
            itemIntegrity.Font = TrayTheme.Font(9.5F, FontStyle.Bold);
            itemIntegrity.Image = StatusDot(TrayTheme.TextMuted);
            itemIntegrity.ToolTipText = "Comprobando que los archivos de la instalación no hayan sido modificados.";
            menu.Items.Add(itemIntegrity);

            menu.Items.Add(new ToolStripSeparator());

            // ---- Mantenimiento ----
            menu.Items.Add(NewSectionLabel(Loc.T("MANTENIMIENTO", "MAINTENANCE")));

            // Ejecutar ahora
            ToolStripMenuItem menuRun = NewItem(Loc.T("Ejecutar mantenimiento ahora", "Run maintenance now"), G_Play, null);
            StyleDropDown(menuRun.DropDown, 340);

            ToolStripMenuItem runUser = NewItem("Limpieza rápida de mi cuenta", G_Person, delegate { RunUserCleanup(); });
            runUser.ToolTipText = "Elimina archivos temporales y capturas de pantalla de su usuario. No necesita permisos de administrador.";
            menuRun.DropDownItems.Add(runUser);

            menuRun.DropDownItems.Add(new ToolStripSeparator());

            ToolStripMenuItem runStd = NewItem("Mantenimiento estándar del equipo", G_Sync, delegate { RunSystemMaintenance(false); });
            runStd.ShortcutKeyDisplayString = "Administrador";
            runStd.ToolTipText = "Inicia en segundo plano todas las tareas de mantenimiento con su configuración habitual. Puede seguir trabajando.";
            menuRun.DropDownItems.Add(runStd);

            ToolStripMenuItem runFull = NewItem(Loc.T("Mantenimiento completo del equipo", "Complete system maintenance"), G_Refresh, delegate { RunSystemMaintenance(true); });
            runFull.ShortcutKeyDisplayString = "Administrador";
            runFull.ToolTipText = "Como el estándar, pero con análisis antimalware completo y reparación de archivos de Windows. Puede tardar y ralentizar el equipo.";
            menuRun.DropDownItems.Add(runFull);

            menu.Items.Add(menuRun);

            // Tareas individuales
            ToolStripMenuItem menuTasks = NewItem(Loc.T("Tareas individuales", "Individual tasks"), G_Document, null);
            StyleDropDown(menuTasks.DropDown, 340);

            menuTasks.DropDownItems.Add(NewSectionLabel(Loc.T("LIMPIEZA Y ORDEN", "CLEANUP & ORDER")));
            menuTasks.DropDownItems.Add(NewModuleItem("Eliminar capturas de pantalla", G_Camera,
                "Elimina las capturas de pantalla guardadas en su cuenta, incluidas las carpetas sincronizadas con la nube.",
                false, "Eliminar capturas de pantalla", "Invoke-CleanupScreenshots.ps1", "",
                "Se eliminarán de forma permanente las capturas de pantalla guardadas en su cuenta, incluidas las de carpetas sincronizadas con la nube (OneDrive, Google Drive, Dropbox).\n\nEsta acción no se puede deshacer.",
                true));
            menuTasks.DropDownItems.Add(NewModuleItem("Liberar espacio (archivos temporales)", G_Delete,
                "Borra archivos temporales, informes de errores y cachés para liberar espacio en disco.",
                false, "Liberar espacio", "Invoke-CleanupTempGlobal.ps1", "", null, false));
            menuTasks.DropDownItems.Add(NewModuleItem("Organizar y archivar descargas", G_Folder,
                "Organiza y archiva los archivos de su carpeta Descargas.",
                false, "Organizar descargas", "Invoke-ArchiveDownloads.ps1", "",
                "Se organizarán y archivarán los archivos de su carpeta Descargas.", false));

            menuTasks.DropDownItems.Add(NewSectionLabel(Loc.T("SEGURIDAD", "SECURITY")));
            ToolStripMenuItem mDefender = NewItem("Análisis antimalware (Defender)", G_Shield, null);
            StyleDropDown(mDefender.DropDown, 320);
            mDefender.DropDownItems.Add(NewModuleItem("Análisis rápido (recomendado)", G_Play,
                "Revisa las zonas donde es más habitual encontrar amenazas. Tarda unos minutos y puede seguir trabajando.",
                true, "Análisis rápido de Defender", "Invoke-DefenderScan.ps1", "-ScanType 1", null, false));
            mDefender.DropDownItems.Add(NewModuleItem("Análisis completo", G_Play,
                "Revisa todos los archivos del equipo. Puede tardar horas y ralentizar el equipo.",
                true, "Análisis completo de Defender", "Invoke-DefenderScan.ps1", "-ScanType 2",
                "El análisis completo revisa todos los archivos del equipo.\n\n•  Puede tardar entre una y varias horas.\n•  El equipo puede funcionar más lento mientras se ejecuta.\n\nSe recomienda hacerlo fuera del horario de trabajo.",
                true));
            menuTasks.DropDownItems.Add(mDefender);

            menuTasks.DropDownItems.Add(NewSectionLabel("DIAGNÓSTICO AVANZADO (TI)"));
            ToolStripMenuItem mIntegrity = NewItem("Reparar archivos de Windows", G_Sync, null);
            StyleDropDown(mIntegrity.DropDown, 320);
            mIntegrity.DropDownItems.Add(NewModuleItem("Comprobación rápida", G_Play,
                "Comprueba si Windows tiene archivos dañados (DISM /CheckHealth). Tarda unos segundos.",
                true, "Comprobación rápida de Windows", "Invoke-IntegrityCheck.ps1", "-QuickCheck", null, false));
            mIntegrity.DropDownItems.Add(NewModuleItem("Reparación completa", G_Play,
                "Busca y repara archivos dañados de Windows (DISM /RestoreHealth). Puede tardar de 15 a 45 minutos.",
                true, "Reparación completa de Windows", "Invoke-IntegrityCheck.ps1", "-AutoRepair",
                "La reparación completa analiza y repara los archivos de Windows.\n\n•  Puede tardar entre 15 y 45 minutos.\n•  El equipo puede funcionar más lento mientras se ejecuta.\n•  Puede necesitar conexión a Internet.",
                true));
            menuTasks.DropDownItems.Add(mIntegrity);

            menuTasks.DropDownItems.Add(NewModuleItem("Comprobar cifrado de disco (BitLocker)", G_Lock,
                "Verifica que las unidades del equipo estén cifradas y protegidas.",
                true, "Comprobación de BitLocker", "Invoke-BitLockerAudit.ps1", "", null, false));
            menuTasks.DropDownItems.Add(NewModuleItem("Revisar eventos de seguridad", G_Document,
                "Busca señales de alerta en el registro de Windows: borrado de registros, intentos fallidos de inicio de sesión y cuentas nuevas.",
                true, "Revisión de eventos de seguridad", "Invoke-EventAudit.ps1", "", null, false));
            menu.Items.Add(menuTasks);

            menu.Items.Add(new ToolStripSeparator());

            // ---- Herramientas ----
            menu.Items.Add(NewSectionLabel(Loc.T("HERRAMIENTAS", "TOOLS")));

            ToolStripMenuItem itemLogs = NewItem(Loc.T("Ver registros de actividad", "View activity logs"), G_Folder, delegate { OpenLogs(); });
            itemLogs.ToolTipText = "Abre la carpeta donde se guarda el resultado de cada tarea ejecutada.";
            menu.Items.Add(itemLogs);

            ToolStripMenuItem itemCfg = NewItem("Programar tareas…", G_Settings, delegate { OpenConfig(); });
            itemCfg.ToolTipText = "Elija qué tareas se ejecutan automáticamente, cuándo y con qué frecuencia.";
            menu.Items.Add(itemCfg);

            itemAutoStart = NewItem("Iniciar con Windows", null, delegate { OnAutoStartClicked(); });
            itemAutoStart.ToolTipText = "Abre este icono automáticamente cuando inicie sesión.";
            bool byUser = IsAutoStartForUser();
            bool byMachine = IsAutoStartForMachine();
            if (byMachine && !byUser)
            {
                // Lo configuró la instalación para todos los usuarios: no se puede cambiar desde aquí.
                itemAutoStart.Text = "Iniciar con Windows (lo gestiona TI)";
                itemAutoStart.Checked = true;
                itemAutoStart.Enabled = false;
            }
            else
            {
                itemAutoStart.CheckOnClick = true;
                itemAutoStart.Checked = byUser;
            }
            menu.Items.Add(itemAutoStart);

            menu.Items.Add(new ToolStripSeparator());

            // ---- Pie ----
            menu.Items.Add(NewItem("Acerca de Isora Sentinel…", null, delegate { ShowAbout(); }));

            ToolStripMenuItem itemExit = NewItem(Loc.T("Salir del monitor", "Exit monitor"), G_Power, delegate { ExitMonitor(); });
            itemExit.ToolTipText = "Cierra este icono. Las tareas programadas siguen ejecutándose.";
            itemExit.Padding = new Padding(0, Sz(5), 0, Sz(8));
            menu.Items.Add(itemExit);

            // ---- NotifyIcon ----
            trayIcon = new NotifyIcon();
            trayIcon.Text = "Isora Sentinel · Comprobando instalación";
            trayIcon.Icon = LoadTrayIcon();
            trayIcon.Visible = true;
            trayIcon.MouseUp += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
                {
                    if (dummyForm != null && !dummyForm.IsDisposed)
                        SetForegroundWindow(dummyForm.Handle);
                    menu.Show(Cursor.Position);
                }
            };
        }

        private Icon LoadTrayIcon()
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream stream = asm.GetManifestResourceStream("AppIcon"))
                {
                    if (stream != null) return new Icon(stream);
                }
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"src\app.ico");
                if (!File.Exists(icoPath)) icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(icoPath)) return new Icon(icoPath);
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                return SystemIcons.Shield;
            }
        }

        // ---------------------------------------------------------------------
        //  Estado de la instalación (comprobación de integridad)
        // ---------------------------------------------------------------------
        private void RefreshIntegrityAsync(bool showReport)
        {
            ApplyIntegrity(new IntegrityReport { State = IntegrityState.Checking }, false);
            string dir = installDir;
            ThreadPool.QueueUserWorkItem(delegate
            {
                IntegrityReport r = IntegrityVerifier.Verify(dir);
                try
                {
                    invoker.BeginInvoke(new Action(delegate { ApplyIntegrity(r, showReport); }));
                }
                catch { }
            });
        }

        private void ApplyIntegrity(IntegrityReport r, bool showReport)
        {
            Color dot;
            string text, tip, hint;
            string time = r.Timestamp.ToString("HH:mm");

            switch (r.State)
            {
                case IntegrityState.Authorized:
                    dot = TrayTheme.Emerald;
                    text = "Instalación verificada";
                    tip = "Isora Sentinel · Instalación verificada";
                    hint = "Los archivos coinciden con el registro de la instalación (comprobado a las " + time + "). Haga clic para ver el detalle.";
                    break;
                case IntegrityState.Tampered:
                    dot = TrayTheme.Danger;
                    text = Loc.T("Archivos modificados", "Tampered/modified files");
                    tip = "Isora Sentinel · Archivos modificados";
                    hint = "Algunos archivos no coinciden con el original. Haga clic para ver cuáles.";
                    break;
                case IntegrityState.NoManifest:
                    dot = TrayTheme.Amber;
                    text = Loc.T("No se pudo verificar", "Could not verify");
                    tip = "Isora Sentinel · No se pudo verificar";
                    hint = "No se encontró el archivo de comprobación de la instalación. Haga clic para más información.";
                    break;
                default:
                    dot = TrayTheme.TextMuted;
                    text = "Comprobando instalación…";
                    tip = "Isora Sentinel · Comprobando instalación";
                    hint = "Comprobando que los archivos de la instalación no hayan sido modificados.";
                    break;
            }

            itemIntegrity.Text = text;
            itemIntegrity.ToolTipText = hint;
            itemIntegrity.ShortcutKeyDisplayString = (r.State == IntegrityState.Checking) ? null : time;
            Image old = itemIntegrity.Image;
            itemIntegrity.Image = StatusDot(dot);
            if (old != null) old.Dispose();
            trayIcon.Text = tip;

            if (showReport && r.State != IntegrityState.Checking) ShowIntegrityReport(r);
        }

        private void ShowIntegrityReport(IntegrityReport r)
        {
            StringBuilder sb = new StringBuilder();
            MessageBoxIcon icon = MessageBoxIcon.Information;
            string title = Branding.ProductName + " · Estado de la instalación";

            if (r.State == IntegrityState.Authorized)
            {
                sb.AppendLine("La instalación está en buen estado.");
                sb.AppendLine();
                sb.AppendLine("Se comprobaron " + r.FilesChecked + " archivos y todos coinciden con el registro original de la instalación.");
                sb.AppendLine();
                sb.AppendLine("Detalle técnico: huellas SHA-256 comparadas con integrity.manifest.json.");
            }
            else if (r.State == IntegrityState.Tampered)
            {
                icon = MessageBoxIcon.Warning;
                title = Branding.ProductName + " · Archivos modificados";
                sb.AppendLine("Se encontraron archivos que no coinciden con el original (" + r.Altered.Count + " de " + r.FilesChecked + "):");
                sb.AppendLine();
                int n = 0;
                foreach (string f in r.Altered)
                {
                    if (n++ >= 6) { sb.AppendLine("  ·  y " + (r.Altered.Count - 6) + " más"); break; }
                    sb.AppendLine("  ·  " + f);
                }
                sb.AppendLine();
                sb.AppendLine("Qué hacer:");
                sb.AppendLine("  ·  Si usted no los modificó, informe al área de TI.");
                sb.AppendLine("  ·  Para restaurarlos, vuelva a instalar Isora Sentinel con el asistente de instalación.");
            }
            else
            {
                icon = MessageBoxIcon.Warning;
                title = Branding.ProductName + " · No se pudo verificar";
                sb.AppendLine("No fue posible comprobar la instalación.");
                sb.AppendLine();
                sb.AppendLine("No se encontró el archivo de comprobación (integrity.manifest.json) en:");
                sb.AppendLine(installDir);
                sb.AppendLine();
                sb.AppendLine("Si es una copia de pruebas, puede ignorar este aviso. En un equipo de trabajo, consulte al área de TI.");
            }
            sb.AppendLine();
            sb.AppendLine("Última comprobación: " + r.Timestamp.ToString("dd/MM/yyyy HH:mm:ss"));

            Msg(sb.ToString(), title, MessageBoxButtons.OK, icon);
        }

        private void ShowAbout()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Isora Sentinel Suite  ·  versión " + Branding.Version);
            sb.AppendLine();
            sb.AppendLine("Mantenimiento y seguridad para equipos Windows: limpieza, análisis antimalware, comprobaciones de integridad y auditorías, con un registro de cada ejecución.");
            sb.AppendLine();
            sb.AppendLine("Autora: Sandra Gabriela Puerto Torres");
            sb.AppendLine("Licencia: MIT (código abierto)");
            sb.AppendLine("Instalada en: " + installDir);
            sb.AppendLine();
            sb.AppendLine("Nuestra filosofía: Ubuntu");
            sb.AppendLine("«Umuntu ngumuntu ngabantu» (zulú y xhosa): una persona es persona a través de las demás; «soy porque somos». Creemos que la seguridad y la tecnología cobran sentido cuando sirven para cuidar y respetar a las personas.");
            Msg(sb.ToString(), "Acerca de " + Branding.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ---------------------------------------------------------------------
        //  Ejecución de procesos (resultado real, sin ventanas de consola)
        // ---------------------------------------------------------------------
        private static string Q(string s) { return "'" + s.Replace("'", "''") + "'"; }

        private void PostResult(Action<int> onDone, int code)
        {
            try
            {
                invoker.BeginInvoke(new Action(delegate { if (onDone != null) onDone(code); }));
            }
            catch { }
        }

        /// <summary>
        /// Ejecuta un script de PowerShell. Se envía codificado (-EncodedCommand) para que
        /// las comillas y los espacios de las rutas no puedan romper el comando.
        /// </summary>
        private void RunPowerShell(string command, bool elevate, Action<int> onDone)
        {
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
            ProcessStartInfo psi = new ProcessStartInfo("powershell.exe");
            psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand " + encoded;
            if (elevate)
            {
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                psi.WindowStyle = ProcessWindowStyle.Hidden;
            }
            else
            {
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
            }

            Process p = new Process();
            p.StartInfo = psi;

            try
            {
                if (!p.Start())
                {
                    p.Dispose();
                    PostResult(onDone, ExitUnknown);
                    return;
                }
            }
            catch (Win32Exception ex)
            {
                p.Dispose();
                if (ex.NativeErrorCode == 1223)
                    Notify(Branding.ProductName, "No se realizó ninguna acción: se canceló la solicitud de permisos de administrador.", ToolTipIcon.Warning);
                else
                    Msg("No se pudo iniciar la tarea.\n\nDetalle técnico: " + ex.Message, Branding.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                PostResult(onDone, ExitCanceled);
                return;
            }
            catch (Exception ex)
            {
                p.Dispose();
                Msg("No se pudo iniciar la tarea.\n\nDetalle técnico: " + ex.Message, Branding.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                PostResult(onDone, ExitCanceled);
                return;
            }

            // Se espera en un hilo aparte sin EnableRaisingEvents: sobre procesos 'runas'
            // entre niveles de integridad produce Win32Exception por restricciones de acceso.
            ThreadPool.QueueUserWorkItem(delegate
            {
                int exitCode;
                try
                {
                    p.WaitForExit();
                    exitCode = p.ExitCode;
                }
                catch
                {
                    exitCode = ExitUnknown;   // terminó, pero no se puede leer el resultado: no se da por correcto
                }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
                PostResult(onDone, exitCode);
            });
        }

        private static string DirectRun(string scriptPath, string args)
        {
            return "$global:LASTEXITCODE = 0; try { & " + Q(scriptPath) +
                   (string.IsNullOrEmpty(args) ? "" : " " + args) +
                   "; exit [int]$LASTEXITCODE } catch { exit 1 }";
        }

        // ---------------------------------------------------------------------
        //  Acciones de mantenimiento
        // ---------------------------------------------------------------------
        private bool MissingComponent(string scriptPath)
        {
            if (File.Exists(scriptPath)) return false;
            Msg("No se encontró un componente necesario de Isora Sentinel:\n\n" + Path.GetFileName(scriptPath) +
                "\n\nRepare la instalación con el asistente de instalación o consulte al área de TI.",
                Branding.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return true;
        }

        private bool AlreadyRunning(string title)
        {
            if (!running.Contains(title)) return false;
            Notify(Branding.ProductName, "Ya hay una ejecución en curso: " + title + ".", ToolTipIcon.Info);
            return true;
        }

        private void ReportResult(string title, int code)
        {
            if (code == ExitCanceled) return;   // ya se informó la cancelación
            if (code == ExitTriggered)
                Notify(Branding.ProductName, title + ": se ejecuta en segundo plano. El resultado quedará en los registros de actividad.", ToolTipIcon.Info);
            else if (code == 0)
                Notify(Branding.ProductName, title + ": finalizado correctamente.", ToolTipIcon.Info);
            else if (code == ExitUnknown)
                Notify(Branding.ProductName, title + ": finalizó. Revise los registros de actividad para confirmar el resultado.", ToolTipIcon.Info);
            else
                Notify(Branding.ProductName, title + ": finalizó con incidencias. Abra «Ver registros de actividad» para más detalle.", ToolTipIcon.Warning);
        }

        /// <summary>Ejecuta un módulo individual. Los de seguridad usan la tarea programada (cuenta SYSTEM) cuando no hay parámetros.</summary>
        private void RunModule(string title, string script, bool needsAdmin, string args, string confirmBody, bool warn)
        {
            string path = ResolveEngine(script);
            if (MissingComponent(path)) return;
            if (AlreadyRunning(title)) return;

            bool elevate = needsAdmin && !IsAdministrator();
            if (!Confirm(title, confirmBody, elevate, warn)) return;

            string taskName;
            string cmd;
            if (needsAdmin && string.IsNullOrEmpty(args) && TaskByScript.TryGetValue(script, out taskName))
            {
                cmd = "schtasks.exe /Run /TN " + Q(Branding.TaskFolder + "\\" + taskName) + " 2>$null | Out-Null; " +
                      "if ($LASTEXITCODE -eq 0) { exit " + ExitTriggered + " }; " + DirectRun(path, args);
            }
            else
            {
                cmd = DirectRun(path, args);
            }

            running.Add(title);
            Notify(Branding.ProductName, "Iniciado: " + title + ".", ToolTipIcon.Info);
            RunPowerShell(cmd, elevate, delegate(int code)
            {
                running.Remove(title);
                ReportResult(title, code);
            });
        }

        /// <summary>Limpieza de la cuenta actual: no necesita administrador.</summary>
        private void RunUserCleanup()
        {
            const string title = "Limpieza rápida de mi cuenta";
            string a = ResolveEngine("Invoke-CleanupScreenshots.ps1");
            string b = ResolveEngine("Invoke-CleanupTempGlobal.ps1");
            if (MissingComponent(a) || MissingComponent(b)) return;
            if (AlreadyRunning(title)) return;

            if (!Confirm(title,
                "Se eliminarán archivos temporales y capturas de pantalla de su cuenta.\n\nLas capturas eliminadas no se pueden recuperar.",
                false, true)) return;

            string cmd = "$f = 0; foreach ($s in " + Q(a) + "," + Q(b) + ") { " +
                         "$global:LASTEXITCODE = 0; try { & $s } catch { $f++; continue }; " +
                         "if ($LASTEXITCODE -ne 0) { $f++ } }; exit $f";

            running.Add(title);
            Notify(Branding.ProductName, "Iniciada la limpieza de su cuenta.", ToolTipIcon.Info);
            RunPowerShell(cmd, false, delegate(int code)
            {
                running.Remove(title);
                ReportResult(title, code);
            });
        }

        /// <summary>
        /// Estándar: inicia las tareas programadas en segundo plano (cuenta SYSTEM).
        /// Completo: igual, pero Defender y la reparación de Windows se ejecutan en modo exhaustivo
        /// y el proceso termina cuando acaban (puede tardar).
        /// </summary>
        private void RunSystemMaintenance(bool full)
        {
            string title = full ? Loc.T("Mantenimiento completo del equipo", "Complete system maintenance") : "Mantenimiento estándar del equipo";
            if (AlreadyRunning(title)) return;
            if (AlreadyRunning(full ? "Mantenimiento estándar del equipo" : Loc.T("Mantenimiento completo del equipo", "Complete system maintenance"))) return;

            string[,] jobs = new string[,]
            {
                { "Cleanup_Screenshots", "Invoke-CleanupScreenshots.ps1", "" },
                { "Cleanup_Temp_Global", "Invoke-CleanupTempGlobal.ps1",  "" },
                { "Archive_Downloads",   "Invoke-ArchiveDownloads.ps1",   "" },
                { "BitLocker_Audit",     "Invoke-BitLockerAudit.ps1",     "" },
                { "Event_Audit",         "Invoke-EventAudit.ps1",         "" },
                { "Defender_Scan",       "Invoke-DefenderScan.ps1",       "-ScanType 2" },
                { "Integrity_Check",     "Invoke-IntegrityCheck.ps1",     "-AutoRepair" }
            };
            int total = jobs.GetLength(0);

            string body = full
                ? "El mantenimiento completo ejecuta todas las tareas, con análisis antimalware completo y reparación de archivos de Windows.\n\n•  Puede tardar entre una y varias horas.\n•  El equipo puede funcionar más lento mientras se ejecuta.\n•  Incluye la eliminación de archivos temporales y capturas de pantalla.\n\nSe recomienda hacerlo fuera del horario de trabajo."
                : "Se iniciarán en segundo plano todas las tareas de mantenimiento con su configuración habitual: limpieza, análisis antimalware, comprobación de Windows y auditorías.\n\nIncluye la eliminación de archivos temporales y capturas de pantalla. Puede seguir trabajando mientras se ejecutan.";

            bool elevate = !IsAdministrator();
            if (!Confirm(title, body, elevate, full)) return;

            StringBuilder sb = new StringBuilder("$f = 0; ");
            for (int i = 0; i < total; i++)
            {
                string task = jobs[i, 0];
                string script = ResolveEngine(jobs[i, 1]);
                string heavyArgs = jobs[i, 2];
                bool heavy = full && heavyArgs.Length > 0;

                if (heavy)
                {
                    // Parámetros exhaustivos: no se pueden pasar por la tarea programada, se ejecuta el script directamente.
                    sb.Append("$global:LASTEXITCODE = 0; try { & " + Q(script) + " " + heavyArgs +
                              "; if ($LASTEXITCODE -ne 0) { $f++ } } catch { $f++ }; ");
                }
                else
                {
                    sb.Append("schtasks.exe /Run /TN " + Q(Branding.TaskFolder + "\\" + task) + " 2>$null | Out-Null; ");
                    sb.Append("if ($LASTEXITCODE -ne 0) { $global:LASTEXITCODE = 0; try { & " + Q(script) +
                              "; if ($LASTEXITCODE -ne 0) { $f++ } } catch { $f++ } }; ");
                }
            }
            sb.Append("exit $f");

            running.Add(title);
            Notify(Branding.ProductName,
                full ? "Mantenimiento completo en curso. Puede tardar; puede seguir trabajando."
                     : "Mantenimiento estándar iniciado. Puede seguir trabajando.",
                ToolTipIcon.Info);

            RunPowerShell(sb.ToString(), elevate, delegate(int failures)
            {
                running.Remove(title);
                if (failures == ExitCanceled) return;
                if (failures == ExitUnknown) { ReportResult(title, failures); return; }

                if (failures == 0)
                    Notify(Branding.ProductName,
                        full ? "Mantenimiento completo finalizado. Revise los registros de actividad para ver los resultados."
                             : "Se iniciaron las " + total + " tareas en segundo plano. Los resultados se guardan en los registros de actividad.",
                        ToolTipIcon.Info);
                else
                    Notify(Branding.ProductName,
                        "No se pudieron iniciar " + failures + " de " + total + " tareas. Abra «Ver registros de actividad» para más detalle.",
                        ToolTipIcon.Warning);
            });
        }

        // ---------------------------------------------------------------------
        //  Herramientas
        // ---------------------------------------------------------------------
        private static bool HasEntries(string dir)
        {
            try { return Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length > 0; }
            catch { return false; }
        }

        private void OpenLogs()
        {
            string userLogs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Isora-Sentinel\logs");

            string target = null;
            if (HasEntries(logsDir)) target = logsDir;
            else if (HasEntries(userLogs)) target = userLogs;
            else if (Directory.Exists(logsDir)) target = logsDir;
            else if (Directory.Exists(userLogs)) target = userLogs;

            if (target == null)
            {
                Msg("Todavía no hay registros de actividad.\n\nSe crean la primera vez que se ejecuta una tarea.",
                    Branding.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try { Process.Start("explorer.exe", "\"" + target + "\""); }
            catch (Exception ex)
            {
                Msg("No se pudo abrir la carpeta de registros:\n" + target + "\n\nDetalle técnico: " + ex.Message,
                    Branding.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OpenSetup()
        {
            List<string> candidates = new List<string>();
            candidates.Add(Path.Combine(installDir, Branding.SetupExe));
            candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Branding.SetupExe));
            string reg = InstallLocationFromRegistry();
            if (!string.IsNullOrEmpty(reg)) candidates.Add(Path.Combine(reg, Branding.SetupExe));
            candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\installer\" + Branding.SetupExe));   // árbol de desarrollo

            string setupExe = null;
            foreach (string c in candidates)
                if (File.Exists(c)) { setupExe = c; break; }

            if (setupExe == null)
            {
                Msg("No se encontró el asistente de instalación en este equipo.\n\nVuelva a ejecutar el instalador original (" +
                    Branding.SetupExe + ") o solicite apoyo al área de TI.",
                    Branding.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(setupExe);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Msg("No se pudo abrir el asistente de instalación.\n\nDetalle técnico: " + ex.Message,
                    Branding.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OpenConfig()
        {
            if (configForm != null && !configForm.IsDisposed)
            {
                configForm.WindowState = FormWindowState.Normal;
                configForm.BringToFront();
                configForm.Activate();
                return;
            }

            configForm = new TaskConfigForm(installDir, LoadTrayIcon(), dpi,
                delegate(string cmd, bool elevate, Action<int> onDone)
                {
                    RunPowerShell(cmd, elevate, delegate(int code)
                    {
                        // La ventana recarga el estado real de las tareas al terminar, por eso un resultado ilegible no se trata como error.
                        if (onDone != null) onDone(code == ExitUnknown ? 0 : code);
                    });
                },
                delegate { OpenSetup(); });
            configForm.Show();
            configForm.Activate();
        }

        // ---------------------------------------------------------------------
        //  Inicio automático
        // ---------------------------------------------------------------------
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private static bool IsAutoStartForUser()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                    return key != null && key.GetValue(Branding.AutoStartName) != null;
            }
            catch { return false; }
        }

        private static bool IsAutoStartForMachine()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(RunKey, false))
                    return key != null && key.GetValue(Branding.AutoStartName) != null;
            }
            catch { return false; }
        }

        private void OnAutoStartClicked()
        {
            bool enable = itemAutoStart.Checked;   // CheckOnClick ya alternó el estado
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null) throw new InvalidOperationException("No se pudo abrir la clave de inicio de Windows.");
                    if (enable) key.SetValue(Branding.AutoStartName, "\"" + Application.ExecutablePath + "\"");
                    else key.DeleteValue(Branding.AutoStartName, false);
                }
            }
            catch (Exception ex)
            {
                itemAutoStart.Checked = !enable;   // revierte si falló
                Msg("No se pudo cambiar el inicio automático.\n\nDetalle técnico: " + ex.Message,
                    Branding.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ExitMonitor()
        {
            // Si la ventana de tareas está abierta con cambios sin guardar, se le da la opción de cancelar la salida.
            if (configForm != null && !configForm.IsDisposed)
            {
                configForm.Close();
                if (!configForm.IsDisposed) return;
            }

            integrityTimer.Stop();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            menu.Dispose();
            if (dummyForm != null && !dummyForm.IsDisposed) dummyForm.Dispose();
            ExitThread();
        }

        // ---------------------------------------------------------------------
        //  Punto de entrada (instancia única + DPI)
        // ---------------------------------------------------------------------
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        public static void Main(string[] args)
        {
            bool created;
            using (Mutex single = new Mutex(true, Branding.MutexName, out created))
            {
                if (!created) return;   // ya hay un monitor activo en esta sesión

                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SentinelTrayContext());
                GC.KeepAlive(single);
            }
        }
    }
}
