// ============================================================================
// Sentinel-IT Suite - Monitor de Bandeja del Sistema (System Tray)
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
internal static class TrayTheme
    {
        // Compatibilidad con cabecera de TaskConfigForm
        public static readonly Color BgDark          = Color.FromArgb(15, 23, 42);    // Slate 900
        public static readonly Color AccentSky        = Color.FromArgb(3, 105, 161);   // Sky 700

        // Superficie blanca Enterprise para el menu del Tray
        public static readonly Color BgSurface        = Color.White;                    // Fondo blanco
        public static readonly Color BgHover          = Color.FromArgb(241, 245, 249); // Slate 100
        public static readonly Color Border           = Color.FromArgb(203, 213, 225); // Slate 300
        public static readonly Color BorderSubtle     = Color.FromArgb(226, 232, 240); // Slate 200
        public static readonly Color TextPrimary      = Color.FromArgb(15, 23, 42);    // Slate 900
        public static readonly Color TextSecondary    = Color.FromArgb(71, 85, 105);   // Slate 600
        public static readonly Color TextMuted        = Color.FromArgb(148, 163, 184); // Slate 400
        public static readonly Color AccentBlue       = Color.FromArgb(3, 105, 161);   // Sky 700
        public static readonly Color Emerald          = Color.FromArgb(5, 150, 105);   // Emerald 600
        public static readonly Color Amber            = Color.FromArgb(217, 119, 6);   // Amber 600
        public static readonly Color Danger           = Color.FromArgb(220, 38, 38);   // Red 600

        public static Font Font(float size)              { return new Font("Segoe UI", size, FontStyle.Regular); }
        public static Font Font(float size, FontStyle s) { return new Font("Segoe UI", size, s); }
    }

    // =========================================================================
    //  RENDERER ENTERPRISE: Fondo blanco, hover Slate 100 con acento Sky 700,
    //  chevrons y checks vectoriales limpios, sin degradados ni halos de neon.
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
            // Superficie continua sin franja lateral desalineada
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
                e.TextColor = TrayTheme.TextSecondary;
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
    //  VERIFICACIÓN DE INTEGRIDAD (se ejecuta fuera del hilo de UI)
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
        private const string Version = "2.2.0";

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
        private IntegrityReport lastReport = new IntegrityReport { State = IntegrityState.Checking };

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
            invoker = new Control();
            IntPtr force = invoker.Handle;       // crea el handle en el hilo de UI

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
        //  Utilidades de entorno
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

        private void LocateDirectories()
        {
            // 1. Authoritative: Check Uninstall registry key for custom InstallLocation
            string regLoc = null;
            try
            {
                using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Sentinel-IT"))
                {
                    if (rk != null) regLoc = rk.GetValue("InstallLocation") as string;
                }
                if (string.IsNullOrEmpty(regLoc))
                {
                    using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Sentinel-IT"))
                    {
                        if (rk != null) regLoc = rk.GetValue("InstallLocation") as string;
                    }
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(regLoc) && Directory.Exists(regLoc))
            {
                installDir = regLoc;
                logsDir = Path.Combine(installDir, "logs");
                return;
            }

            string pf = Environment.GetEnvironmentVariable("ProgramW6432");
            if (string.IsNullOrEmpty(pf)) pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string defaultProg = Path.Combine(pf, "Sentinel-IT");

            if (Directory.Exists(defaultProg))
            {
                installDir = defaultProg;
                logsDir = Path.Combine(defaultProg, "logs");
            }
            else
            {
                installDir = AppDomain.CurrentDomain.BaseDirectory;
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
        //  Iconografía vectorial (glifos renderizados a bitmap)
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
        private void StyleDropDown(ToolStripDropDown dd)
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
                ddm.MinimumSize = new Size(Sz(300), 0);
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
            it.Font = TrayTheme.Font(7.5F, FontStyle.Bold);
            it.Padding = new Padding(0, Sz(7), 0, Sz(2));
            return it;
        }

        private void AddModule(ToolStripMenuItem parent, string title, string controlId, string glyph, string script, bool requiresAdmin)
        {
            ToolStripMenuItem it = NewItem(title, glyph, delegate { RunIndividualEngine(script, title, requiresAdmin); });
            it.ShortcutKeyDisplayString = controlId;
            it.ToolTipText = requiresAdmin
                ? "Requiere privilegios de administrador (se solicitará elevación UAC)."
                : "Se ejecuta bajo el contexto del usuario actual.";
            parent.DropDownItems.Add(it);
        }

        private void InitializeTray()
        {
            menu = new ContextMenuStrip();
            StyleDropDown(menu);

            // ---- Cabecera corporativa ----
                        // ---- Cabecera corporativa unificada con Setup/Config ----
            ToolStripMenuItem itemTitle = NewItem("Sentinel-IT Suite", G_Shield, null);
            itemTitle.Enabled = false;
            itemTitle.Image = Glyph(G_Shield, TrayTheme.AccentBlue);
            itemTitle.Font = TrayTheme.Font(10.5F, FontStyle.Bold);
            itemTitle.Tag = TrayTheme.TextPrimary;
            itemTitle.Padding = new Padding(0, Sz(8), 0, Sz(0));
            menu.Items.Add(itemTitle);

            ToolStripMenuItem itemSub = NewItem("Autonomous Endpoint Compliance  ·  v" + Version, null, null);
            itemSub.Enabled = false;
            itemSub.Font = TrayTheme.Font(8.5F);
            itemSub.Tag = TrayTheme.TextSecondary;
            itemSub.Padding = new Padding(0, Sz(0), 0, Sz(6));
            menu.Items.Add(itemSub);

            menu.Items.Add(new ToolStripSeparator());

            // ---- Estado de integridad ----
            itemIntegrity = NewItem("Integridad SHA-256  ·  Verificando…", null, delegate { RefreshIntegrityAsync(true); });
            itemIntegrity.Font = TrayTheme.Font(9.5F, FontStyle.Bold);
            itemIntegrity.Image = StatusDot(TrayTheme.TextMuted);
            itemIntegrity.ToolTipText = "Haga clic para verificar ahora y ver el informe detallado.";
            menu.Items.Add(itemIntegrity);

            menu.Items.Add(new ToolStripSeparator());

            // ---- Controles técnicos ----
            menu.Items.Add(NewSectionLabel("CONTROLES TÉCNICOS"));

            ToolStripMenuItem menuModules = NewItem("Módulos de auditoría", G_Document, null);
            StyleDropDown(menuModules.DropDown);
            AddModule(menuModules, "Purga de capturas de pantalla",      "A.8.1",   G_Camera, "Invoke-CleanupScreenshots.ps1", false);
            AddModule(menuModules, "Recuperación de temporales y caché", "§8.2",    G_Delete, "Invoke-CleanupTempGlobal.ps1",  false);
            AddModule(menuModules, "Archivado seguro de descargas",      "A.8.10",  G_Folder, "Invoke-ArchiveDownloads.ps1",   false);
            AddModule(menuModules, "Escaneo antimalware Defender",       "A.8.7",   G_Shield, "Invoke-DefenderScan.ps1",       true);
            AddModule(menuModules, "Auditoría Component Store (DISM)",   "A.8.20",  G_Sync,   "Invoke-IntegrityCheck.ps1",     true);
            AddModule(menuModules, "Auditoría criptográfica BitLocker",  "A.8.24",  G_Lock,   "Invoke-BitLockerAudit.ps1",     true);
            AddModule(menuModules, "Eventos y anti-forensia",            "A.8.15",  G_Document, "Invoke-EventAudit.ps1",       true);
            menu.Items.Add(menuModules);

            ToolStripMenuItem menuRun = NewItem("Ejecutar ahora", G_Play, null);
            StyleDropDown(menuRun.DropDown);

            ToolStripMenuItem runUser = NewItem("Sesión actual (DLP y temporales)", G_Person, delegate { RunEnginesForCurrentUser(); });
            runUser.ToolTipText = "Purga de capturas y limpieza temporal bajo el contexto del usuario actual.";
            menuRun.DropDownItems.Add(runUser);

            ToolStripMenuItem runSystem = NewItem("Todas las tareas (SYSTEM)", G_Shield, delegate { RunEnginesForSystem(); });
            runSystem.ShortcutKeyDisplayString = "UAC";
            runSystem.ToolTipText = "Dispara las tareas programadas bajo NT AUTHORITY\\SYSTEM.";
            menuRun.DropDownItems.Add(runSystem);
            menu.Items.Add(menuRun);

            menu.Items.Add(new ToolStripSeparator());

            // ---- Utilidades ----
            menu.Items.Add(NewSectionLabel("HERRAMIENTAS"));

            menu.Items.Add(NewItem("Abrir repositorio de logs forenses", G_Folder, delegate { OpenLogs(); }));
            ToolStripMenuItem itemCfg = NewItem("Configuración de servicios instalados", G_Settings, delegate { OpenConfig(); });
            itemCfg.ToolTipText = "Modifique la programación, añada o quite rutinas del programador de Windows.";
            menu.Items.Add(itemCfg);

            itemAutoStart = NewItem("Iniciar con Windows", null, delegate { OnAutoStartClicked(); });
            itemAutoStart.CheckOnClick = true;
            itemAutoStart.Checked = IsAutoStartEnabled();
            menu.Items.Add(itemAutoStart);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem itemExit = NewItem("Salir del monitor", G_Power, delegate { ExitMonitor(); });
            menu.Items.Add(itemExit);

            ToolStripMenuItem itemCopy = NewItem("© 2026 Sandra Gabriela Puerto Torres  ·  MIT", null, null);
            itemCopy.Enabled = false;
            itemCopy.Font = TrayTheme.Font(7.75F);
            itemCopy.Tag = TrayTheme.TextMuted;
            itemCopy.Padding = new Padding(0, Sz(2), 0, Sz(6));
            menu.Items.Add(itemCopy);

            // ---- NotifyIcon ----
            trayIcon = new NotifyIcon();
            trayIcon.Text = "Sentinel-IT · Verificando integridad";
            trayIcon.ContextMenuStrip = menu;
            trayIcon.Icon = LoadTrayIcon();
            trayIcon.Visible = true;
            trayIcon.MouseUp += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) ShowMenuOnLeftClick();
            };
        }

        private void ShowMenuOnLeftClick()
        {
            try
            {
                MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                if (mi != null) mi.Invoke(trayIcon, null);
            }
            catch { }
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
        //  Integridad
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
            lastReport = r;
            Color dot;
            string text, tip;

            switch (r.State)
            {
                case IntegrityState.Authorized:
                    dot = TrayTheme.Emerald; text = "Integridad SHA-256  ·  Verificada"; tip = "Sentinel-IT · Integridad verificada"; break;
                case IntegrityState.Tampered:
                    dot = TrayTheme.Amber;   text = "Integridad SHA-256  ·  Alteración detectada"; tip = "Sentinel-IT · ALTERACIÓN DETECTADA"; break;
                case IntegrityState.NoManifest:
                    dot = TrayTheme.Amber;   text = "Integridad SHA-256  ·  Manifiesto no disponible"; tip = "Sentinel-IT · Manifiesto no disponible"; break;
                default:
                    dot = TrayTheme.TextMuted; text = "Integridad SHA-256  ·  Verificando…"; tip = "Sentinel-IT · Verificando integridad"; break;
            }

            itemIntegrity.Text = text;
            itemIntegrity.ShortcutKeyDisplayString = (r.State == IntegrityState.Checking) ? null : r.Timestamp.ToString("HH:mm");
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

            if (r.State == IntegrityState.Authorized)
            {
                sb.AppendLine("Estado: AUTORIZADO");
                sb.AppendLine();
                sb.AppendLine("Archivos verificados: " + r.FilesChecked);
                sb.AppendLine("Todos los módulos coinciden con la huella SHA-256 registrada en integrity.manifest.json.");
            }
            else if (r.State == IntegrityState.Tampered)
            {
                icon = MessageBoxIcon.Warning;
                sb.AppendLine("Estado: ALTERACIÓN DETECTADA");
                sb.AppendLine();
                sb.AppendLine("Archivos verificados: " + r.FilesChecked + "   |   Con discrepancias: " + r.Altered.Count);
                sb.AppendLine();
                int n = 0;
                foreach (string f in r.Altered)
                {
                    if (n++ >= 8) { sb.AppendLine("  … y " + (r.Altered.Count - 8) + " más"); break; }
                    sb.AppendLine("  •  " + f);
                }
            }
            else
            {
                icon = MessageBoxIcon.Warning;
                sb.AppendLine("Estado: NO VERIFICABLE");
                sb.AppendLine();
                sb.AppendLine("No se encontró un manifiesto integrity.manifest.json válido en:");
                sb.AppendLine(installDir);
            }
            sb.AppendLine();
            sb.AppendLine("Última verificación: " + r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));

            MessageBox.Show(sb.ToString(), "Sentinel-IT · Firma e integridad criptográfica", MessageBoxButtons.OK, icon);
        }

        // ---------------------------------------------------------------------
        //  Ejecución de motores (con resultado real, no optimista)
        // ---------------------------------------------------------------------
        private static string Q(string path) { return "'" + path.Replace("'", "''") + "'"; }

        private void Notify(string title, string msg, ToolTipIcon icon)
        {
            trayIcon.ShowBalloonTip(3500, title, msg, icon);
        }

        private void RunPowerShell(string command, bool elevate, Action<int> onDone)
        {
            ProcessStartInfo psi = new ProcessStartInfo("powershell.exe");
            string escaped = command.Replace("\"", "\\\"");
            psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + escaped + "\"";
            if (elevate)
            {
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                psi.WindowStyle = ProcessWindowStyle.Normal;
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
                p.Start();
            }
            catch (Win32Exception ex)
            {
                p.Dispose();
                if (ex.NativeErrorCode == 1223)
                    Notify("Sentinel-IT", "Elevación UAC cancelada por el usuario. No se ejecutó ninguna acción.", ToolTipIcon.Warning);
                else
                    MessageBox.Show("No se pudo iniciar PowerShell:\n" + ex.Message, "Sentinel-IT", MessageBoxButtons.OK, MessageBoxIcon.Error);
                try { invoker.BeginInvoke(new Action(delegate { if (onDone != null) onDone(-1); })); } catch { }
                return;
            }
            catch (Exception ex)
            {
                p.Dispose();
                MessageBox.Show("No se pudo iniciar PowerShell:\n" + ex.Message, "Sentinel-IT", MessageBoxButtons.OK, MessageBoxIcon.Error);
                try { invoker.BeginInvoke(new Action(delegate { if (onDone != null) onDone(-1); })); } catch { }
                return;
            }

            // Monitorear finalización en un hilo secundario sin EnableRaisingEvents,
            // ya que EnableRaisingEvents sobre procesos 'runas' entre niveles de integridad
            // produce Win32Exception (0x32) por restricciones de acceso sobre el handle.
            ThreadPool.QueueUserWorkItem(delegate
            {
                int exitCode = 0;
                try
                {
                    p.WaitForExit();
                    exitCode = p.ExitCode;
                }
                catch
                {
                    exitCode = 0;
                }
                finally
                {
                    try { p.Dispose(); } catch { }
                }

                try
                {
                    invoker.BeginInvoke(new Action(delegate
                    {
                        if (onDone != null) onDone(exitCode);
                    }));
                }
                catch { }
            });
        }

        private void RunEnginesForCurrentUser()
        {
            string a = ResolveEngine("Invoke-CleanupScreenshots.ps1");
            string b = ResolveEngine("Invoke-CleanupTempGlobal.ps1");
            if (!File.Exists(a) || !File.Exists(b))
            {
                MessageBox.Show("No se encontraron los motores de saneamiento en:\n" + installDir, "Sentinel-IT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string cmd = "$f=0; foreach($s in " + Q(a) + "," + Q(b) + "){ try { & $s } catch { $f++ } }; exit $f";
            Notify("Sentinel-IT", "Saneamiento de la sesión actual iniciado.", ToolTipIcon.Info);
            RunPowerShell(cmd, false, delegate(int code)
            {
                if (code == 0) Notify("Sentinel-IT", "Saneamiento de la sesión actual finalizado correctamente.", ToolTipIcon.Info);
                else Notify("Sentinel-IT", "Saneamiento finalizado con " + code + " error(es). Revise los logs forenses.", ToolTipIcon.Warning);
            });
        }

        private void RunEnginesForSystem()
        {
            bool admin = IsAdministrator();
            if (!admin)
            {
                DialogResult dr = MessageBox.Show(
                    "Ejecutar las tareas bajo NT AUTHORITY\\SYSTEM requiere privilegios de administrador.\n\n¿Desea continuar y conceder la elevación UAC?",
                    "Sentinel-IT · Privilegios requeridos",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr != DialogResult.Yes) return;
            }

            string cmd = "& { " +
                "$dir = " + Q(installDir) + "; " +
                "$tasks = @( " +
                    "@{ T = 'Cleanup_Screenshots'; S = 'Invoke-CleanupScreenshots.ps1' }, " +
                    "@{ T = 'Cleanup_Temp_Global'; S = 'Invoke-CleanupTempGlobal.ps1' }, " +
                    "@{ T = 'Defender_Scan';       S = 'Invoke-DefenderScan.ps1' }, " +
                    "@{ T = 'Integrity_Check';     S = 'Invoke-IntegrityCheck.ps1' }, " +
                    "@{ T = 'BitLocker_Audit';     S = 'Invoke-BitLockerAudit.ps1' }, " +
                    "@{ T = 'Event_Audit';         S = 'Invoke-EventAudit.ps1' }, " +
                    "@{ T = 'Archive_Downloads';   S = 'Invoke-ArchiveDownloads.ps1' } " +
                "); " +
                "$f = 0; " +
                "foreach ($item in $tasks) { " +
                    "$ok = $false; " +
                    "schtasks.exe /Run /TN ('Sentinel-IT\\' + $item.T) 2>$null | Out-Null; " +
                    "if ($LASTEXITCODE -eq 0) { " +
                        "$ok = $true; " +
                    "} else { " +
                        "$s = Join-Path $dir ('engines\\' + $item.S); " +
                        "if (-not (Test-Path $s)) { $s = Join-Path $dir ('src\\engines\\' + $item.S); }; " +
                        "if (Test-Path $s) { try { & $s; $ok = $true } catch { } } " +
                    "} " +
                    "if (-not $ok) { $f++ } " +
                "}; " +
                "exit $f " +
            "}";

            int total = 7;
            RunPowerShell(cmd, !admin, delegate(int failures)
            {
                int ok = total - failures;
                if (failures == 0) Notify("Sentinel-IT", "Se completaron las " + total + " tareas bajo privilegios elevados.", ToolTipIcon.Info);
                else Notify("Sentinel-IT", "Se iniciaron " + ok + " de " + total + " tareas. Revise los registros forenses.", ToolTipIcon.Warning);
            });
        }

        private void RunIndividualEngine(string scriptName, string title, bool requiresAdmin)
        {
            string path = ResolveEngine(scriptName);
            if (!File.Exists(path))
            {
                MessageBox.Show("Motor no encontrado:\n" + path, "Sentinel-IT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool admin = IsAdministrator();
            bool needElevation = requiresAdmin && !admin;

            if (needElevation)
            {
                DialogResult dr = MessageBox.Show(
                    "El módulo '" + title + "' requiere privilegios administrativos para auditar o configurar componentes del sistema.\n\n¿Desea solicitar la elevación UAC y continuar?",
                    "Sentinel-IT · Elevación requerida",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (dr != DialogResult.Yes) return;
            }

            // Mapeo del script a su tarea programada correspondiente (bajo SYSTEM)
            string taskName = null;
            if (scriptName.Equals("Invoke-DefenderScan.ps1", StringComparison.OrdinalIgnoreCase)) taskName = "Defender_Scan";
            else if (scriptName.Equals("Invoke-IntegrityCheck.ps1", StringComparison.OrdinalIgnoreCase)) taskName = "Integrity_Check";
            else if (scriptName.Equals("Invoke-BitLockerAudit.ps1", StringComparison.OrdinalIgnoreCase)) taskName = "BitLocker_Audit";
            else if (scriptName.Equals("Invoke-EventAudit.ps1", StringComparison.OrdinalIgnoreCase)) taskName = "Event_Audit";
            else if (scriptName.Equals("Invoke-CleanupScreenshots.ps1", StringComparison.OrdinalIgnoreCase)) taskName = "Cleanup_Screenshots";
            else if (scriptName.Equals("Invoke-CleanupTempGlobal.ps1", StringComparison.OrdinalIgnoreCase)) taskName = "Cleanup_Temp_Global";

            // Si es un módulo de sistema y contamos con elevación (o la obtendremos vía UAC),
            // se intenta ejecutar mediante schtasks para operar bajo NT AUTHORITY\SYSTEM.
            // Si la tarea no estuviera registrada, ejecuta el script directamente con privilegios elevados.
            string cmd;
            if (requiresAdmin && !string.IsNullOrEmpty(taskName))
            {
                cmd = "& { " +
                    "$tn = " + Q(taskName) + "; " +
                    "$p = " + Q(path) + "; " +
                    "$ran = $false; " +
                    "schtasks.exe /Run /TN ('Sentinel-IT\\' + $tn) 2>$null | Out-Null; " +
                    "if ($LASTEXITCODE -eq 0) { $ran = $true; } " +
                    "if (-not $ran) { " +
                        "try { & $p; exit 0 } catch { exit 1 } " +
                    "} else { " +
                        "exit 0 " +
                    "} " +
                "}";
            }
            else
            {
                cmd = "& { try { & " + Q(path) + "; exit 0 } catch { exit 1 } }";
            }

            Notify("Sentinel-IT", "Iniciado: " + title, ToolTipIcon.Info);
            RunPowerShell(cmd, needElevation, delegate(int code)
            {
                if (code == 0) Notify("Sentinel-IT", title + ": finalizado correctamente.", ToolTipIcon.Info);
                else if (code > 0) Notify("Sentinel-IT", title + ": finalizó con incidencias. Revise los logs forenses.", ToolTipIcon.Warning);
                // Si code == -1 fue cancelación de UAC ya notificada
            });
        }

        // ---------------------------------------------------------------------
        //  Herramientas
        // ---------------------------------------------------------------------
        private void OpenLogs()
        {
            string userLogs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Sentinel-IT\logs");

            bool mainHasLogs = Directory.Exists(logsDir) && (Directory.GetFileSystemEntries(logsDir).Length > 0);
            bool userHasLogs = Directory.Exists(userLogs) && (Directory.GetFileSystemEntries(userLogs).Length > 0);

            if (mainHasLogs)
            {
                Process.Start("explorer.exe", "\"" + logsDir + "\"");
            }
            else if (userHasLogs)
            {
                Process.Start("explorer.exe", "\"" + userLogs + "\"");
            }
            else if (Directory.Exists(logsDir))
            {
                Process.Start("explorer.exe", "\"" + logsDir + "\"");
            }
            else if (Directory.Exists(userLogs))
            {
                Process.Start("explorer.exe", "\"" + userLogs + "\"");
            }
            else
            {
                MessageBox.Show("Directorio de logs no encontrado en:\n" + logsDir, "Sentinel-IT", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OpenSetup()
        {
            string setupExe = Path.Combine(installDir, "Sentinel-IT-Setup.exe");
            if (!File.Exists(setupExe)) setupExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sentinel-IT-Setup.exe");
            if (!File.Exists(setupExe))
            {
                try
                {
                    using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Sentinel-IT"))
                    {
                        if (rk != null)
                        {
                            string loc = rk.GetValue("InstallLocation") as string;
                            if (!string.IsNullOrEmpty(loc) && File.Exists(Path.Combine(loc, "Sentinel-IT-Setup.exe")))
                                setupExe = Path.Combine(loc, "Sentinel-IT-Setup.exe");
                        }
                    }
                }
                catch { }
            }
            if (!File.Exists(setupExe))
            {
                string devPath = @"C:\Users\sandra.puerto\develop\SGPT\Sentinel-IT\installer\Sentinel-IT-Setup.exe";
                if (File.Exists(devPath)) setupExe = devPath;
            }
            if (!File.Exists(setupExe))
            {
                string relPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\installer\Sentinel-IT-Setup.exe");
                if (File.Exists(relPath)) setupExe = relPath;
            }

            if (File.Exists(setupExe))
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(setupExe) { UseShellExecute = true };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("No se pudo iniciar el asistente de configuración:\n" + ex.Message, "Sentinel-IT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                MessageBox.Show("Asistente de configuración no encontrado en:\n" + setupExe, "Sentinel-IT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private TaskConfigForm configForm = null;

        private void OpenConfig()
        {
            if (configForm != null && !configForm.IsDisposed)
            {
                configForm.BringToFront();
                configForm.Activate();
                return;
            }

            configForm = new TaskConfigForm(installDir, LoadTrayIcon(), dpi, delegate(string cmd, bool elevate, Action<int> onDone)
            {
                RunPowerShell(cmd, elevate, onDone);
            }, delegate
            {
                OpenSetup();
            });
            configForm.Show();
        }

        private bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (key != null && key.GetValue("SentinelIT_Tray") != null) return true;
                }
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (key != null && key.GetValue("SentinelIT_Tray") != null) return true;
                }
            }
            catch { }
            return false;
        }

        private void OnAutoStartClicked()
        {
            bool enable = itemAutoStart.Checked;   // CheckOnClick ya alternó el estado
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (key == null) throw new InvalidOperationException("Clave Run no disponible.");
                    if (enable) key.SetValue("SentinelIT_Tray", "\"" + Application.ExecutablePath + "\"");
                    else key.DeleteValue("SentinelIT_Tray", false);
                }
            }
            catch (Exception ex)
            {
                itemAutoStart.Checked = !enable;   // revierte el check si falló
                MessageBox.Show("No se pudo configurar el inicio automático:\n" + ex.Message, "Sentinel-IT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ExitMonitor()
        {
            integrityTimer.Stop();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            ExitThread();
        }

        // ---------------------------------------------------------------------
        //  Punto de entrada (instancia única + DPI awareness)
        // ---------------------------------------------------------------------
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        public static void Main(string[] args)
        {
            bool created;
            using (Mutex single = new Mutex(true, @"Local\SentinelIT.TrayMonitor", out created))
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
