using SentinelIT;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SentinelIT.Installer
{
    /// <summary>
    /// Asistente de desinstalación de Isora Sentinel Suite.
    /// Pensado para dos públicos: usuarios normales (textos simples, sin jerga)
    /// y personal de TI (detalles técnicos disponibles bajo demanda y copiables).
    /// Depende de las clases del proyecto: Theme, Card, StepButton.
    /// </summary>
    public class UninstallForm : Form
    {
        private const string AppName = "Isora Sentinel Suite";
        private const string AppVersion = "2.3.0";
        private const string ArchivedFolderName = "Isora_Sentinel_Archived";

        private enum Outcome { Success, SuccessWithWarnings, Failed }

        private static readonly Color Rose = Color.FromArgb(225, 29, 72);
        private static readonly Color Amber = Color.FromArgb(217, 119, 6);

        // ---- Estado ----
        private float dpi = 1f;
        private int currentPage = 0;
        private string installDir;
        private bool installFound;
        private bool isAdmin;
        private bool locked = false;
        private bool resultReady = false;
        private bool cleanupDone = false;
        private int warningCount = 0;
        private bool optKeepLogs = true;
        private bool optKeepArchived = true;
        private Outcome lastOutcome = Outcome.Success;
        private string[] startArgs;
        private string resultHeaderTitle = "";
        private string resultHeaderSub = "";
        private readonly StringBuilder logBuffer = new StringBuilder();
        private readonly object logLock = new object();

        // ---- Estructura ----
        private Panel sidebar;
        private Panel contentPanel;
        private Panel[] pages;
        private StepButton[] navStepButtons;
        private Label lblHeaderTitle;
        private Label lblHeaderSubtitle;
        private Panel pnlHeader;

        // Página 0: confirmación
        private Panel bodyConfirm;
        private CheckBox chkKeepLogs;
        private CheckBox chkKeepArchived;
        private Button btnCancel;
        private Button btnUninstall;

        // Página 1: progreso
        private ProgressBar prgBar;
        private Label lblProgressStatus;
        private RichTextBox rtbLog;
        private LinkLabel lnkToggleLog;
        private LinkLabel lnkBackToResult;

        // Página 2: resultado
        private Panel bodyResult;
        private Card cardResult;
        private Label lblResultTitle;
        private Label lblResultBody;
        private Button btnFinish;
        private Button btnRetry;
        private Button btnCopy;
        private Button btnOpenLogs;
        private Button btnShowDetails;

        private int Sz(int v) { return (int)Math.Round(v * dpi); }

        public UninstallForm(string[] args)
        {
            startArgs = args;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) { dpi = g.DpiX / 96f; }

            isAdmin = IsAdministrator();
            installDir = LocateInstallDir();
            installFound = LooksInstalled(installDir);

            InitializeComponent();
            ShowPage(0);
        }

        // =====================================================================
        //  UTILIDADES DE ENTORNO
        // =====================================================================
        private static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                {
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }

        private static string LocateInstallDir()
        {
            string[] keys = new string[] { "Isora-Sentinel", "Sentinel-IT" };
            foreach (string k in keys)
            {
                try
                {
                    using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + k))
                    {
                        if (rk != null)
                        {
                            string loc = rk.GetValue("InstallLocation") as string;
                            if (!string.IsNullOrEmpty(loc) && Directory.Exists(loc)) return loc.TrimEnd('\\');
                        }
                    }
                }
                catch { }
            }
            string pf = Environment.GetEnvironmentVariable("ProgramW6432");
            if (string.IsNullOrEmpty(pf)) pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string def1 = Path.Combine(pf, "Isora-Sentinel");
            if (Directory.Exists(def1)) return def1;
            string def2 = Path.Combine(pf, "Sentinel-IT");
            if (Directory.Exists(def2)) return def2;
            return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        }

        /// <summary>
        /// Seguridad: solo se borran archivos si la carpeta realmente parece una instalación
        /// (evita borrar carpetas "lib" o "src" ajenas si el desinstalador se ejecuta desde otro lugar).
        /// </summary>
        private static bool LooksInstalled(string dir)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
                if (Directory.Exists(Path.Combine(dir, ".git"))) return false; // Protección: un repositorio git nunca es un path de instalación
                string[] marks = new string[] { "integrity.manifest.json", "Isora-Sentinel-Tray.exe", "Sentinel-IT-Tray.exe" };
                foreach (string m in marks)
                {
                    if (File.Exists(Path.Combine(dir, m))) return true;
                }
                return Directory.Exists(Path.Combine(dir, "engines"));
            }
            catch { return false; }
        }

        private static string GetArchivedPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ArchivedFolderName);
        }

        // =====================================================================
        //  COMPONENTES BASE (helpers de UI)
        // =====================================================================
        private Label MakeText(string text, float size, FontStyle style, Color color, int indent, int gapAfter)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = Theme.F(size, style);
            l.ForeColor = color;
            l.AutoSize = false;
            l.Margin = new Padding(Sz(indent), 0, 0, Sz(gapAfter));
            return l;
        }

        private Card NewCard(Color accent)
        {
            Card c = new Card();
            c.AccentBar = true;
            c.AccentColor = accent;
            c.Height = Sz(120);
            return c;
        }

        private Panel Spacer()
        {
            Panel s = new Panel();
            s.Height = Sz(14);
            return s;
        }

        /// <summary>Apila controles de arriba hacia abajo (se agregan en orden inverso por la regla de docking).</summary>
        private void StackTop(Panel host, params Control[] topToBottom)
        {
            for (int i = topToBottom.Length - 1; i >= 0; i--)
            {
                topToBottom[i].Dock = DockStyle.Top;
                host.Controls.Add(topToBottom[i]);
            }
        }

        /// <summary>
        /// Calcula el alto real de cada tarjeta según el ancho disponible, de modo que
        /// el texto nunca se corte ni quede espacio vacío al redimensionar la ventana.
        /// </summary>
        private void ReflowCard(Card card)
        {
            int padX = Sz(22);
            int y = Sz(16);
            int w = Math.Max(Sz(160), card.ClientSize.Width - padX * 2);
            foreach (Control c in card.Controls)
            {
                int indent = c.Margin.Left;
                int cw = Math.Max(Sz(100), w - indent);
                Size pref = c.GetPreferredSize(new Size(cw, 0));
                int h = Math.Max(pref.Height, Sz(20));
                c.SetBounds(padX + indent, y, cw, h);
                y += h + c.Margin.Bottom;
            }
            card.Height = y + Sz(10);
        }

        private void ReflowHost(Panel host)
        {
            if (host == null) return;
            host.SuspendLayout();
            foreach (Control c in host.Controls)
            {
                Card card = c as Card;
                if (card != null) ReflowCard(card);
            }
            host.ResumeLayout(true);
        }

        private Button MakeButton(string text, bool primary, Color primaryColor)
        {
            Button b = new Button();
            b.Text = text;
            b.Font = Theme.F(9f, primary ? FontStyle.Bold : FontStyle.Regular);
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.MinimumSize = new Size(Sz(110), Sz(38));
            b.Padding = new Padding(Sz(10), 0, Sz(10), 0);
            b.Margin = new Padding(Sz(8), Sz(6), 0, Sz(6));
            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
            if (primary)
            {
                b.BackColor = primaryColor;
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderSize = 0;
            }
            else
            {
                b.BackColor = Theme.CardBg;
                b.ForeColor = Theme.TextPrimary;
                b.FlatAppearance.BorderColor = Theme.Border;
            }
            return b;
        }

        private FlowLayoutPanel MakeButtonBar()
        {
            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Bottom;
            bar.AutoSize = true;
            bar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            bar.FlowDirection = FlowDirection.RightToLeft;
            bar.WrapContents = true;
            bar.Padding = new Padding(0, Sz(6), 0, 0);
            return bar;
        }

        private void SetHeader(string title, string subtitle)
        {
            lblHeaderTitle.Text = title;
            lblHeaderSubtitle.Text = subtitle;
        }

        // =====================================================================
        //  CONSTRUCCIÓN DE LA VENTANA
        // =====================================================================
        private void InitializeComponent()
        {
            this.AutoScaleMode = AutoScaleMode.None;
            this.Text = Loc.T("Desinstalar " + AppName, "Uninstall " + AppName);
            this.Size = new Size(Sz(900), Sz(620));
            this.MinimumSize = new Size(Sz(760), Sz(540));
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = Theme.F(9f);
            this.BackColor = Theme.ContentBg;
            this.ForeColor = Theme.TextPrimary;

            try
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
                using (Stream stream = asm.GetManifestResourceStream("AppIcon"))
                {
                    if (stream != null) this.Icon = new Icon(stream);
                }
            }
            catch { }

            // --- Panel de contenido (Fill): se agrega PRIMERO para que acople al final ---
            contentPanel = new Panel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.BackColor = Theme.ContentBg;
            contentPanel.Padding = new Padding(Sz(28), Sz(22), Sz(28), Sz(16));
            this.Controls.Add(contentPanel);

            // --- Sidebar (Left) ---
            sidebar = new Panel();
            sidebar.Dock = DockStyle.Left;
            sidebar.Width = Sz(240);
            sidebar.BackColor = Theme.SidebarBg;
            sidebar.Padding = new Padding(Sz(18));
            this.Controls.Add(sidebar);

            Label lblLogo = new Label();
            lblLogo.Text = "ISORA SENTINEL";
            lblLogo.Font = Theme.F(12f, FontStyle.Bold);
            lblLogo.ForeColor = Color.White;
            lblLogo.AutoSize = true;
            lblLogo.Location = new Point(Sz(18), Sz(22));
            sidebar.Controls.Add(lblLogo);

            Label lblBadge = new Label();
            lblBadge.Text = " DESINSTALADOR ";
            lblBadge.Font = Theme.F(8f, FontStyle.Bold);
            lblBadge.ForeColor = Theme.DangerLight;
            lblBadge.BackColor = Color.FromArgb(50, 244, 63, 94);
            lblBadge.AutoSize = true;
            lblBadge.Padding = new Padding(Sz(3), Sz(2), Sz(3), Sz(2));
            lblBadge.Location = new Point(Sz(18), Sz(48));
            sidebar.Controls.Add(lblBadge);

            navStepButtons = new StepButton[3];
            navStepButtons[0] = new StepButton { Number = 1, Title = "Revisar" };
            navStepButtons[1] = new StepButton { Number = 2, Title = "Desinstalar" };
            navStepButtons[2] = new StepButton { Number = 3, Title = "Resultado" };

            int btnY = Sz(105);
            for (int i = 0; i < navStepButtons.Length; i++)
            {
                navStepButtons[i].Location = new Point(Sz(12), btnY);
                navStepButtons[i].Width = sidebar.Width - Sz(24);
                sidebar.Controls.Add(navStepButtons[i]);
                btnY += navStepButtons[i].Height + Sz(6);
            }

            // Pie del sidebar (Bottom): se agrega primero la ayuda, luego el copyright (queda más abajo)
            Label lblHelp = new Label();
            lblHelp.Text = "¿Necesitas ayuda? Consulta al área de TI de tu organización.";
            lblHelp.Font = Theme.F(8.5f);
            lblHelp.ForeColor = Theme.SidebarMuted;
            lblHelp.Dock = DockStyle.Bottom;
            lblHelp.Height = Sz(52);
            sidebar.Controls.Add(lblHelp);

            Label lblCopy = new Label();
            lblCopy.Text = "© 2026 Sandra Gabriela Puerto Torres" + Environment.NewLine + "Isora Cloud Group";
            lblCopy.Font = Theme.F(8f);
            lblCopy.ForeColor = Theme.SidebarMuted;
            lblCopy.Dock = DockStyle.Bottom;
            lblCopy.Height = Sz(40);
            sidebar.Controls.Add(lblCopy);

            // --- Contenedor de páginas (Fill) y encabezado (Top) ---
            Panel pnlPages = new Panel();
            pnlPages.Dock = DockStyle.Fill;
            contentPanel.Controls.Add(pnlPages);

            pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = Sz(72);

            lblHeaderTitle = new Label();
            lblHeaderTitle.Font = Theme.F(15f, FontStyle.Bold);
            lblHeaderTitle.ForeColor = Theme.TextPrimary;
            lblHeaderTitle.AutoSize = false;
            lblHeaderTitle.AutoEllipsis = true;
            lblHeaderTitle.Location = new Point(0, 0);
            lblHeaderTitle.Height = Sz(30);
            pnlHeader.Controls.Add(lblHeaderTitle);

            lblHeaderSubtitle = new Label();
            lblHeaderSubtitle.Font = Theme.F(9.5f);
            lblHeaderSubtitle.ForeColor = Theme.TextMuted;
            lblHeaderSubtitle.AutoSize = false;
            lblHeaderSubtitle.Location = new Point(0, Sz(32));
            lblHeaderSubtitle.Height = Sz(38);
            pnlHeader.Controls.Add(lblHeaderSubtitle);

            pnlHeader.Resize += delegate
            {
                lblHeaderTitle.Width = pnlHeader.ClientSize.Width;
                lblHeaderSubtitle.Width = pnlHeader.ClientSize.Width;
            };
            contentPanel.Controls.Add(pnlHeader);

            pages = new Panel[3];
            pages[0] = BuildPageConfirmation();
            pages[1] = BuildPageProgress();
            pages[2] = BuildPageResult();
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].Dock = DockStyle.Fill;
                pages[i].Visible = false;
                pnlPages.Controls.Add(pages[i]);
            }

            this.Resize += delegate { ApplyResponsive(); };
            this.FormClosing += OnFormClosing;
            this.FormClosed += OnFormClosed;
        }

        /// <summary>Sidebar más angosto en ventanas pequeñas para dar espacio al contenido.</summary>
        private void ApplyResponsive()
        {
            if (sidebar == null || navStepButtons == null) return;
            int target = this.ClientSize.Width < Sz(840) ? Sz(200) : Sz(240);
            if (sidebar.Width != target)
            {
                sidebar.Width = target;
                for (int i = 0; i < navStepButtons.Length; i++) navStepButtons[i].Width = sidebar.Width - Sz(24);
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (locked && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                MessageBox.Show(this,
                    "La desinstalación está en curso. Espera unos segundos a que termine para no dejar el programa instalado a medias.",
                    "Desinstalación en curso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            if (resultReady && lastOutcome != Outcome.Failed && installFound && !cleanupDone)
            {
                cleanupDone = true;
                CleanupRemainingInstallDir(installDir);
            }
        }

        // =====================================================================
        //  PÁGINA 0: REVISAR Y ELEGIR QUÉ CONSERVAR
        // =====================================================================
        private Panel BuildPageConfirmation()
        {
            Panel page = new Panel();

            bodyConfirm = new Panel();
            bodyConfirm.Dock = DockStyle.Fill;
            bodyConfirm.AutoScroll = true;
            bodyConfirm.ClientSizeChanged += delegate { ReflowHost(bodyConfirm); };
            page.Controls.Add(bodyConfirm);

            FlowLayoutPanel bar = MakeButtonBar();
            page.Controls.Add(bar);

            List<Control> stack = new List<Control>();

            // 1) Qué va a pasar
            Card cardIntro = NewCard(Rose);
            cardIntro.Controls.Add(MakeText("Vas a desinstalar " + AppName, 11.5f, FontStyle.Bold, Theme.TextPrimary, 0, 8));
            cardIntro.Controls.Add(MakeText(
                "Se quitará el mantenimiento y la protección automática que " + AppName + " realiza en este equipo.",
                9.5f, FontStyle.Regular, Theme.TextPrimary, 0, 8));
            if (installFound)
            {
                cardIntro.Controls.Add(MakeText(
                    "•  El icono junto al reloj (bandeja del sistema)\n" +
                    "•  Las tareas automáticas de limpieza, análisis de seguridad y revisiones periódicas\n" +
                    "•  Los programas instalados en esta carpeta:",
                    9.5f, FontStyle.Regular, Theme.TextPrimary, 0, 4));
                Label lblPath = MakeText(installDir, 9f, FontStyle.Regular, Theme.TextMuted, 18, 10);
                lblPath.Font = new Font("Consolas", 9f, FontStyle.Regular);
                cardIntro.Controls.Add(lblPath);
            }
            else
            {
                cardIntro.Controls.Add(MakeText(
                    "•  El icono junto al reloj (bandeja del sistema)\n" +
                    "•  Las tareas automáticas de limpieza, análisis de seguridad y revisiones periódicas",
                    9.5f, FontStyle.Regular, Theme.TextPrimary, 0, 10));
            }
            cardIntro.Controls.Add(MakeText(
                "Tus documentos, fotos y demás archivos personales no se eliminan.",
                9.5f, FontStyle.Bold, Theme.Success, 0, 0));
            stack.Add(cardIntro);

            // 2) Aviso: instalación no encontrada
            if (!installFound)
            {
                stack.Add(Spacer());
                Card cardMissing = NewCard(Amber);
                cardMissing.Controls.Add(MakeText("No encontramos los archivos del programa", 10.5f, FontStyle.Bold, Theme.TextPrimary, 0, 6));
                cardMissing.Controls.Add(MakeText(
                    "No hay una instalación de " + AppName + " en:\n" + installDir + "\n\n" +
                    "Si continúas, solo se quitarán las tareas automáticas y las entradas de Windows que hayan quedado. " +
                    "No se borrará ningún archivo de esa carpeta.",
                    9.5f, FontStyle.Regular, Theme.TextPrimary, 0, 0));
                stack.Add(cardMissing);
            }

            // 3) Aviso: faltan permisos de administrador
            if (!isAdmin)
            {
                stack.Add(Spacer());
                Card cardAdmin = NewCard(Amber);
                cardAdmin.Controls.Add(MakeText("Se necesitan permisos de administrador", 10.5f, FontStyle.Bold, Theme.TextPrimary, 0, 6));
                cardAdmin.Controls.Add(MakeText(
                    "Para quitar las tareas automáticas y la configuración de Windows hay que ser administrador. " +
                    "Al continuar, Windows te pedirá confirmación y el asistente se reiniciará con esos permisos. " +
                    "Si no tienes la clave de administrador, pide ayuda al área de TI.",
                    9.5f, FontStyle.Regular, Theme.TextPrimary, 0, 0));
                stack.Add(cardAdmin);
            }

            // 4) Qué conservar
            stack.Add(Spacer());
            Card cardOptions = NewCard(Theme.Accent);
            cardOptions.Controls.Add(MakeText("¿Qué quieres conservar?", 10.5f, FontStyle.Bold, Theme.TextPrimary, 0, 4));
            cardOptions.Controls.Add(MakeText(
                "Lo que dejes marcado se conserva. Desmarca una opción solo si quieres eliminarlo.",
                9f, FontStyle.Regular, Theme.TextMuted, 0, 10));

            chkKeepLogs = new CheckBox();
            chkKeepLogs.Text = "Conservar el historial de actividad";
            chkKeepLogs.Font = Theme.F(9.5f, FontStyle.Bold);
            chkKeepLogs.ForeColor = Theme.TextPrimary;
            chkKeepLogs.Checked = true;
            chkKeepLogs.AutoSize = false;
            chkKeepLogs.Margin = new Padding(0, 0, 0, Sz(2));
            cardOptions.Controls.Add(chkKeepLogs);

            cardOptions.Controls.Add(MakeText(
                "Registro de lo que hizo " + AppName + " en este equipo (carpeta «logs»). " +
                "Recomendado: algunas organizaciones lo necesitan para auditorías (ISO/IEC 27001).",
                9f, FontStyle.Regular, Theme.TextMuted, 24, 12));

            chkKeepArchived = new CheckBox();
            chkKeepArchived.Text = "Conservar los archivos ordenados desde Descargas";
            chkKeepArchived.Font = Theme.F(9.5f, FontStyle.Bold);
            chkKeepArchived.ForeColor = Theme.TextPrimary;
            chkKeepArchived.Checked = true;
            chkKeepArchived.AutoSize = false;
            chkKeepArchived.Margin = new Padding(0, 0, 0, Sz(2));
            cardOptions.Controls.Add(chkKeepArchived);

            cardOptions.Controls.Add(MakeText(
                "Archivos que " + AppName + " movió a tu carpeta Documentos\\" + ArchivedFolderName + ". " +
                "Recomendado: déjalo marcado para no perder nada.",
                9f, FontStyle.Regular, Theme.TextMuted, 24, 0));
            stack.Add(cardOptions);

            StackTop(bodyConfirm, stack.ToArray());

            // Botones: el primero agregado queda a la derecha
            btnUninstall = MakeButton(isAdmin ? "Desinstalar" : "Continuar como administrador", true, Rose);
            btnUninstall.Click += delegate { OnUninstallClick(); };
            bar.Controls.Add(btnUninstall);

            btnCancel = MakeButton("Cancelar", false, Color.Empty);
            btnCancel.Click += delegate { this.Close(); };
            bar.Controls.Add(btnCancel);

            return page;
        }

        private void OnUninstallClick()
        {
            if (!isAdmin)
            {
                if (RelaunchAsAdmin())
                {
                    this.Close();
                }
                else
                {
                    MessageBox.Show(this,
                        "No se concedieron permisos de administrador, así que no se puede continuar.\n\n" +
                        "Vuelve a intentarlo y acepta el aviso de Windows, o pide ayuda al área de TI.",
                        "Permisos necesarios", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            StringBuilder sb = new StringBuilder();
            if (!chkKeepLogs.Checked) sb.Append("•  El historial de actividad (carpeta «logs»)\n");
            if (!chkKeepArchived.Checked) sb.Append("•  Los archivos ordenados desde Descargas (" + GetArchivedPath() + ")\n");
            if (sb.Length > 0)
            {
                DialogResult r = MessageBox.Show(this,
                    "Vas a eliminar de forma permanente:\n\n" + sb.ToString() + "\nEsta acción no se puede deshacer. ¿Quieres continuar?",
                    "Confirmar eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (r != DialogResult.Yes) return;
            }
            StartUninstallation();
        }

        private bool RelaunchAsAdmin()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath);
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                psi.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                if (startArgs != null && startArgs.Length > 0)
                {
                    StringBuilder a = new StringBuilder();
                    for (int i = 0; i < startArgs.Length; i++)
                    {
                        if (i > 0) a.Append(' ');
                        a.Append('"').Append(startArgs[i].Replace("\"", "\\\"")).Append('"');
                    }
                    psi.Arguments = a.ToString();
                }
                Process.Start(psi);
                return true;
            }
            catch (Win32Exception) { return false; } // el usuario rechazó el aviso de UAC
            catch { return false; }
        }

        // =====================================================================
        //  PÁGINA 1: PROGRESO
        // =====================================================================
        private Panel BuildPageProgress()
        {
            Panel page = new Panel();

            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 1;
            t.RowCount = 5;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, Sz(30)));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, Sz(46)));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            lblProgressStatus = new Label();
            lblProgressStatus.Text = "Preparando…";
            lblProgressStatus.Font = Theme.F(11f, FontStyle.Bold);
            lblProgressStatus.ForeColor = Theme.TextPrimary;
            lblProgressStatus.AutoSize = true;
            lblProgressStatus.Margin = new Padding(0, Sz(4), 0, Sz(8));
            t.Controls.Add(lblProgressStatus, 0, 0);

            prgBar = new ProgressBar();
            prgBar.Dock = DockStyle.Top;
            prgBar.Height = Sz(18);
            prgBar.Minimum = 0;
            prgBar.Maximum = 100;
            prgBar.Value = 0;
            prgBar.Margin = new Padding(0, 0, 0, Sz(8));
            t.Controls.Add(prgBar, 0, 1);

            Label lblHint = new Label();
            lblHint.Text = "Esto toma unos segundos. Por favor no apagues el equipo ni cierres esta ventana.";
            lblHint.Font = Theme.F(9.5f);
            lblHint.ForeColor = Theme.TextMuted;
            lblHint.Dock = DockStyle.Fill;
            t.Controls.Add(lblHint, 0, 2);

            FlowLayoutPanel links = new FlowLayoutPanel();
            links.AutoSize = true;
            links.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            links.Dock = DockStyle.Top;
            links.WrapContents = true;

            lnkToggleLog = new LinkLabel();
            lnkToggleLog.Text = "Ver detalles técnicos";
            lnkToggleLog.Font = Theme.F(9.5f);
            lnkToggleLog.AutoSize = true;
            lnkToggleLog.Margin = new Padding(0, 0, Sz(18), Sz(8));
            lnkToggleLog.LinkColor = Theme.Accent;
            lnkToggleLog.Click += delegate
            {
                rtbLog.Visible = !rtbLog.Visible;
                lnkToggleLog.Text = rtbLog.Visible ? "Ocultar detalles técnicos" : "Ver detalles técnicos";
            };
            links.Controls.Add(lnkToggleLog);

            lnkBackToResult = new LinkLabel();
            lnkBackToResult.Text = "← Volver al resultado";
            lnkBackToResult.Font = Theme.F(9.5f);
            lnkBackToResult.AutoSize = true;
            lnkBackToResult.Margin = new Padding(0, 0, 0, Sz(8));
            lnkBackToResult.LinkColor = Theme.Accent;
            lnkBackToResult.Visible = false;
            lnkBackToResult.Click += delegate { ShowPage(2); };
            links.Controls.Add(lnkBackToResult);
            t.Controls.Add(links, 0, 3);

            rtbLog = new RichTextBox();
            rtbLog.Dock = DockStyle.Fill;
            rtbLog.BackColor = Color.FromArgb(15, 23, 42);
            rtbLog.ForeColor = Color.FromArgb(226, 232, 240);
            rtbLog.Font = new Font("Consolas", 9f, FontStyle.Regular);
            rtbLog.ReadOnly = true;
            rtbLog.BorderStyle = BorderStyle.None;
            rtbLog.Visible = false; // oculto por defecto: un usuario normal no necesita verlo
            t.Controls.Add(rtbLog, 0, 4);

            page.Controls.Add(t);
            return page;
        }

        // =====================================================================
        //  PÁGINA 2: RESULTADO
        // =====================================================================
        private Panel BuildPageResult()
        {
            Panel page = new Panel();

            bodyResult = new Panel();
            bodyResult.Dock = DockStyle.Fill;
            bodyResult.AutoScroll = true;
            bodyResult.ClientSizeChanged += delegate { ReflowHost(bodyResult); };
            page.Controls.Add(bodyResult);

            FlowLayoutPanel bar = MakeButtonBar();
            page.Controls.Add(bar);

            cardResult = NewCard(Theme.Success);
            lblResultTitle = MakeText("", 13f, FontStyle.Bold, Theme.Success, 0, 10);
            lblResultBody = MakeText("", 9.5f, FontStyle.Regular, Theme.TextPrimary, 0, 0);
            cardResult.Controls.Add(lblResultTitle);
            cardResult.Controls.Add(lblResultBody);
            StackTop(bodyResult, cardResult);

            btnFinish = MakeButton("Cerrar", true, Theme.Accent);
            btnFinish.Click += delegate { this.Close(); };
            bar.Controls.Add(btnFinish);

            btnRetry = MakeButton("Intentar de nuevo", true, Rose);
            btnRetry.Visible = false;
            btnRetry.Click += delegate { ResetForRetry(); };
            bar.Controls.Add(btnRetry);

            btnShowDetails = MakeButton("Ver detalles técnicos", false, Color.Empty);
            btnShowDetails.Click += delegate { ShowPage(1); };
            bar.Controls.Add(btnShowDetails);

            btnCopy = MakeButton("Copiar detalles", false, Color.Empty);
            btnCopy.Click += delegate { CopyDetails(); };
            bar.Controls.Add(btnCopy);

            btnOpenLogs = MakeButton("Abrir historial", false, Color.Empty);
            btnOpenLogs.Visible = false;
            btnOpenLogs.Click += delegate { OpenLogsFolder(); };
            bar.Controls.Add(btnOpenLogs);

            return page;
        }

        private void CopyDetails()
        {
            try
            {
                string text;
                lock (logLock) { text = logBuffer.ToString(); }
                Clipboard.SetText(text);
                btnCopy.Text = "Copiado ✔";
            }
            catch
            {
                MessageBox.Show(this, "No se pudo copiar al portapapeles. Abre «Ver detalles técnicos» y copia el texto manualmente.",
                    "Copiar detalles", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OpenLogsFolder()
        {
            try
            {
                string logs = Path.Combine(installDir, "logs");
                if (Directory.Exists(logs)) Process.Start("explorer.exe", "\"" + logs + "\"");
            }
            catch { }
        }

        private void ResetForRetry()
        {
            resultReady = false;
            locked = false;
            warningCount = 0;
            rtbLog.Clear();
            lock (logLock) { logBuffer.Length = 0; }
            prgBar.Value = 0;
            btnCopy.Text = "Copiar detalles";
            ShowPage(0);
        }

        // =====================================================================
        //  NAVEGACIÓN
        // =====================================================================
        private void ShowPage(int index)
        {
            currentPage = index;
            for (int i = 0; i < pages.Length; i++) pages[i].Visible = (i == index);

            int step = (index == 1 && resultReady) ? 2 : index;
            for (int i = 0; i < navStepButtons.Length; i++)
            {
                navStepButtons[i].Active = (i == step);
                navStepButtons[i].Done = (i < step);
            }

            if (index == 0)
            {
                SetHeader("Desinstalar " + AppName, "Revisa qué se eliminará y elige qué quieres conservar.");
                this.CancelButton = btnCancel;
                this.AcceptButton = null;
                this.ActiveControl = btnCancel; // el botón seguro tiene el foco por defecto
                ReflowHost(bodyConfirm);
            }
            else if (index == 1)
            {
                if (resultReady)
                {
                    SetHeader("Detalles técnicos", "Registro completo de lo que hizo el desinstalador.");
                    lnkBackToResult.Visible = true;
                    if (!rtbLog.Visible)
                    {
                        rtbLog.Visible = true;
                        lnkToggleLog.Text = "Ocultar detalles técnicos";
                    }
                }
                else
                {
                    SetHeader("Desinstalando…", "Esto toma unos segundos. No cierres esta ventana.");
                    lnkBackToResult.Visible = false;
                }
                this.CancelButton = null;
                this.AcceptButton = null;
            }
            else
            {
                SetHeader(resultHeaderTitle, resultHeaderSub);
                this.CancelButton = btnFinish;
                this.AcceptButton = btnFinish;
                this.ActiveControl = btnFinish;
                ReflowHost(bodyResult);
            }
        }

        // =====================================================================
        //  LOG Y PROGRESO (seguros para llamarse desde el hilo secundario)
        // =====================================================================
        private void Log(string text)
        {
            Log(text, Color.FromArgb(226, 232, 240));
        }

        private void Log(string text, Color color)
        {
            string line = string.Format("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss"), text);
            lock (logLock) { logBuffer.AppendLine(line); }

            if (rtbLog.InvokeRequired)
            {
                rtbLog.BeginInvoke(new Action(delegate { AppendLogLine(line, color); }));
                return;
            }
            AppendLogLine(line, color);
        }

        private void AppendLogLine(string line, Color color)
        {
            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionLength = 0;
            rtbLog.SelectionColor = color;
            rtbLog.AppendText(line + "\n");
            rtbLog.ScrollToCaret();
        }

        private void Warn(string text)
        {
            Interlocked.Increment(ref warningCount);
            Log("[AVISO] " + text, Theme.WarningLight);
        }

        private void SetProgress(int value, string status)
        {
            if (prgBar.InvokeRequired)
            {
                prgBar.BeginInvoke(new Action(delegate { SetProgress(value, status); }));
                return;
            }
            prgBar.Value = Math.Max(0, Math.Min(100, value));
            lblProgressStatus.Text = status;
        }

        // =====================================================================
        //  DESINSTALACIÓN (hilo secundario)
        // =====================================================================
        private void StartUninstallation()
        {
            locked = true;
            resultReady = false;
            warningCount = 0;
            optKeepLogs = chkKeepLogs.Checked;
            optKeepArchived = chkKeepArchived.Checked;
            rtbLog.Clear();
            lock (logLock) { logBuffer.Length = 0; }
            prgBar.Value = 0;
            lblProgressStatus.Text = "Preparando…";
            ShowPage(1);

            ThreadPool.QueueUserWorkItem(delegate
            {
                Outcome outcome = Outcome.Failed;
                string error = null;
                try
                {
                    outcome = RunUninstall();
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    Log("[ERROR] " + ex.GetType().Name + ": " + ex.Message, Theme.DangerLight);
                }

                Thread.Sleep(600);
                try
                {
                    this.BeginInvoke(new Action(delegate { ShowResult(outcome, error); }));
                }
                catch { }
            });
        }

        private Outcome RunUninstall()
        {
            string rule = "============================================================";
            Log(rule, Theme.AccentLight);
            Log(AppName + " v" + AppVersion + " — registro del desinstalador", Theme.AccentLight);
            Log("Equipo: " + Environment.MachineName + "  |  Usuario: " + Environment.UserName + "  |  Administrador: " + (isAdmin ? "sí" : "no"), Theme.SidebarMuted);
            Log("Carpeta de instalación: " + installDir + (installFound ? "" : "  (no se detectó instalación)"), Theme.SidebarMuted);
            Log(rule, Theme.AccentLight);

            // 1. Procesos
            SetProgress(10, "Cerrando el icono de la bandeja del sistema…");
            Log("Cerrando el monitor de la bandeja…");
            string[] procNames = new string[] { "Isora-Sentinel-Tray", "Sentinel-IT-Tray" };
            bool anyProc = false;
            foreach (string pName in procNames)
            {
                Process[] list = null;
                try { list = Process.GetProcessesByName(pName); }
                catch (Exception ex) { Warn("No se pudo buscar el proceso " + pName + ": " + ex.Message); continue; }

                foreach (Process proc in list)
                {
                    anyProc = true;
                    int pid = proc.Id;
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(3000);
                        Log("[OK] Proceso " + pName + " (PID " + pid + ") cerrado.", Theme.SuccessLight);
                    }
                    catch (Exception ex)
                    {
                        Warn("No se pudo cerrar " + pName + " (PID " + pid + "): " + ex.Message);
                    }
                    finally { proc.Dispose(); }
                }
            }
            if (!anyProc) Log("[INFO] El monitor de la bandeja no estaba en ejecución.", Theme.AccentLight);

            // 2. Tareas programadas
            SetProgress(30, "Quitando las tareas automáticas…");
            Log("Quitando tareas del Programador de tareas (cuenta SYSTEM)…");
            string[] taskNames = new string[] {
                "Cleanup_Screenshots", "Cleanup_Temp_Global", "Defender_Scan",
                "Integrity_Check", "BitLocker_Audit", "Event_Audit", "Archive_Downloads", "SystemTray_Monitor"
            };
            int removed = 0, absent = 0;
            foreach (string t in taskNames)
            {
                UnregisterTask("Isora Sentinel\\" + t, ref removed, ref absent);
                UnregisterTask("Isora-Sentinel\\" + t, ref removed, ref absent);
                UnregisterTask("Sentinel-IT\\" + t, ref removed, ref absent);
            }
            Log("[INFO] Tareas eliminadas: " + removed + ". Tareas que no estaban instaladas: " + absent + ".", Theme.AccentLight);

            // Carpetas de tareas vacías (mejor esfuerzo, sin avisos)
            try { RunSchtasks("/Delete /TN \"Isora Sentinel\" /F"); } catch { }
            try { RunSchtasks("/Delete /TN \"Isora-Sentinel\" /F"); } catch { }
            try { RunSchtasks("/Delete /TN \"Sentinel-IT\" /F"); } catch { }

            // 3. Registro de Windows
            SetProgress(55, "Limpiando la configuración de Windows…");
            Log("Quitando la entrada de «Aplicaciones instaladas» (HKLM)…");
            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Isora-Sentinel", false);
                Registry.LocalMachine.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Sentinel-IT", false);
                Log("[OK] Entrada de «Aplicaciones instaladas» eliminada.", Theme.SuccessLight);
            }
            catch (Exception ex) { Warn("No se pudo quitar la entrada de «Aplicaciones instaladas»: " + ex.Message); }

            Log("Quitando el inicio automático de la bandeja (HKLM y HKCU)…");
            string[] runValueNames = new string[] { "SentinelIT_Tray", "IsoraSentinel_Tray", "Isora-Sentinel-Tray", "Sentinel-IT-Tray" };
            try
            {
                using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (rk != null)
                    {
                        foreach (string val in runValueNames)
                        {
                            if (rk.GetValue(val) != null)
                            {
                                rk.DeleteValue(val, false);
                                Log("[OK] Inicio automático (HKLM) eliminado: " + val, Theme.SuccessLight);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Warn("No se pudo quitar el inicio automático en HKLM: " + ex.Message); }

            try
            {
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (rk != null)
                    {
                        foreach (string val in runValueNames)
                        {
                            if (rk.GetValue(val) != null)
                            {
                                rk.DeleteValue(val, false);
                                Log("[OK] Inicio automático (HKCU) eliminado: " + val, Theme.SuccessLight);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Warn("No se pudo quitar el inicio automático en HKCU: " + ex.Message); }

            // 4. Archivos
            SetProgress(80, "Eliminando los archivos del programa…");
            if (!installFound)
            {
                Log("[INFO] No se detectaron archivos del programa; no se borra nada de " + installDir + ".", Theme.AccentLight);
            }
            else
            {
                Log("Eliminando archivos en: " + installDir);
                string[] subDirs = new string[] { "engines", "lib", "src", "backup" };
                foreach (string sd in subDirs)
                {
                    string dp = Path.Combine(installDir, sd);
                    if (!Directory.Exists(dp)) continue;
                    try
                    {
                        Directory.Delete(dp, true);
                        Log("[OK] Carpeta eliminada: " + sd, Theme.SuccessLight);
                    }
                    catch (Exception ex) { Warn("No se pudo eliminar la carpeta " + sd + " (¿algún archivo en uso?): " + ex.Message); }
                }

                string logsPath = Path.Combine(installDir, "logs");
                if (Directory.Exists(logsPath))
                {
                    if (optKeepLogs)
                    {
                        Log("[INFO] Historial de actividad conservado en: " + logsPath, Theme.AccentLight);
                    }
                    else
                    {
                        try
                        {
                            Directory.Delete(logsPath, true);
                            Log("[OK] Historial de actividad eliminado.", Theme.SuccessLight);
                        }
                        catch (Exception ex) { Warn("No se pudo eliminar el historial (logs): " + ex.Message); }
                    }
                }

                string currentExeName = Path.GetFileName(Application.ExecutablePath);
                string[] rootFiles = new string[] {
                    "Isora-Sentinel-Tray.exe", "Sentinel-IT-Tray.exe",
                    "Isora-Sentinel-Setup.exe", "Sentinel-IT-Setup.exe",
                    "Isora-Sentinel-Uninstall.exe", "Sentinel-IT-Uninstall.exe",
                    "integrity.manifest.json", "LICENSE", "app.ico", "app.svg",
                    "Uninstall-Sentinel.ps1"
                };
                foreach (string rf in rootFiles)
                {
                    if (rf.Equals(currentExeName, StringComparison.OrdinalIgnoreCase)) continue;
                    string fp = Path.Combine(installDir, rf);
                    if (!File.Exists(fp)) continue;
                    try
                    {
                        File.Delete(fp);
                        Log("[OK] Archivo eliminado: " + rf, Theme.SuccessLight);
                    }
                    catch (Exception ex) { Warn("No se pudo eliminar " + rf + ": " + ex.Message); }
                }
            }

            // 5. Archivos ordenados desde Descargas
            SetProgress(92, "Finalizando…");
            string archived = GetArchivedPath();
            if (optKeepArchived)
            {
                if (Directory.Exists(archived)) Log("[INFO] Archivos ordenados conservados en: " + archived, Theme.AccentLight);
            }
            else if (Directory.Exists(archived))
            {
                try
                {
                    Directory.Delete(archived, true);
                    Log("[OK] Archivos ordenados desde Descargas eliminados: " + archived, Theme.SuccessLight);
                }
                catch (Exception ex) { Warn("No se pudo eliminar " + archived + ": " + ex.Message); }
            }

            SetProgress(100, warningCount == 0 ? "Desinstalación terminada." : "Desinstalación terminada con avisos.");
            Log(rule, Theme.SuccessLight);
            Log(warningCount == 0 ? "DESINSTALACIÓN COMPLETADA." : "DESINSTALACIÓN COMPLETADA CON " + warningCount + " AVISO(S).",
                warningCount == 0 ? Theme.SuccessLight : Theme.WarningLight);
            Log(rule, Theme.SuccessLight);

            return warningCount == 0 ? Outcome.Success : Outcome.SuccessWithWarnings;
        }

        // =====================================================================
        //  RESULTADO (hilo de UI)
        // =====================================================================
        private void ShowResult(Outcome outcome, string error)
        {
            locked = false;
            resultReady = true;
            lastOutcome = outcome;

            Color accent;
            string title;
            StringBuilder body = new StringBuilder();
            string nl = "\n";

            if (outcome == Outcome.Success)
            {
                accent = Theme.Success;
                title = "✔ Desinstalación completada";
                body.Append(AppName + " se quitó de este equipo. No es necesario reiniciar." + nl + nl);
                body.Append("•  Se cerró el icono de la bandeja y se quitaron las tareas automáticas." + nl);
                body.Append("•  Se eliminaron los programas y su configuración en Windows." + nl);
                if (optKeepLogs && Directory.Exists(Path.Combine(installDir, "logs")))
                    body.Append("•  Se conservó el historial de actividad: " + Path.Combine(installDir, "logs") + nl);
                if (optKeepArchived && Directory.Exists(GetArchivedPath()))
                    body.Append("•  Se conservaron tus archivos ordenados: " + GetArchivedPath() + nl);
                if (!installFound)
                    body.Append("•  No se encontraron archivos del programa, por eso no se borró ninguno." + nl);
            }
            else if (outcome == Outcome.SuccessWithWarnings)
            {
                accent = Amber;
                title = "⚠ Desinstalación completada con avisos";
                body.Append("Se quitó la mayor parte de " + AppName + ", pero " + warningCount +
                            (warningCount == 1 ? " paso no se pudo completar" : " pasos no se pudieron completar") +
                            " (por ejemplo, un archivo en uso)." + nl + nl);
                body.Append("Qué puedes hacer:" + nl);
                body.Append("•  Reinicia el equipo y ejecuta el desinstalador otra vez." + nl);
                body.Append("•  Si el aviso persiste, pásale los detalles técnicos al área de TI (botón «Copiar detalles»).");
            }
            else
            {
                accent = Rose;
                title = "✖ No se pudo completar la desinstalación";
                body.Append("Algo interrumpió el proceso y " + AppName + " puede haber quedado instalado a medias." + nl + nl);
                if (!string.IsNullOrEmpty(error)) body.Append("Motivo: " + error + nl + nl);
                body.Append("Qué puedes hacer:" + nl);
                body.Append("•  Cierra otros programas e inténtalo de nuevo." + nl);
                body.Append("•  Si vuelve a fallar, copia los detalles técnicos y compártelos con el área de TI.");
            }

            cardResult.AccentColor = accent;
            cardResult.Invalidate();
            lblResultTitle.ForeColor = accent;
            lblResultTitle.Text = title;
            lblResultBody.Text = body.ToString();

            btnRetry.Visible = (outcome == Outcome.Failed);
            btnOpenLogs.Visible = (outcome != Outcome.Failed && optKeepLogs && Directory.Exists(Path.Combine(installDir, "logs")));
            btnCopy.Text = "Copiar detalles";

            if (outcome == Outcome.Success) { resultHeaderTitle = "Todo listo"; resultHeaderSub = "Ya puedes cerrar esta ventana."; }
            else if (outcome == Outcome.SuccessWithWarnings) { resultHeaderTitle = "Casi listo"; resultHeaderSub = "Revisa los avisos y sigue las recomendaciones."; }
            else { resultHeaderTitle = "Algo salió mal"; resultHeaderSub = "La desinstalación no pudo terminar."; }

            ShowPage(2);
        }

        // =====================================================================
        //  TAREAS PROGRAMADAS Y LIMPIEZA FINAL
        // =====================================================================
        /// <summary>Devuelve el código de salida de schtasks, o -1 si no terminó a tiempo.</summary>
        private int RunSchtasks(string arguments)
        {
            ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", arguments);
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            using (Process p = Process.Start(psi))
            {
                if (!p.WaitForExit(6000))
                {
                    try { p.Kill(); } catch { }
                    return -1;
                }
                return p.ExitCode;
            }
        }

        private void UnregisterTask(string taskName, ref int removed, ref int absent)
        {
            try
            {
                int q = RunSchtasks("/Query /TN \"" + taskName + "\"");
                if (q == -1) { Warn("Windows tardó demasiado al consultar la tarea " + taskName + "."); return; }
                if (q != 0) { absent++; return; } // no existe: es normal, no es un problema

                int d = RunSchtasks("/Delete /TN \"" + taskName + "\" /F");
                if (d == 0)
                {
                    removed++;
                    Log("[OK] Tarea eliminada: " + taskName, Theme.SuccessLight);
                }
                else
                {
                    Warn("No se pudo eliminar la tarea " + taskName + " (código " + d + ").");
                }
            }
            catch (Exception ex) { Warn("Error al procesar la tarea " + taskName + ": " + ex.Message); }
        }

        /// <summary>
        /// Elimina el propio desinstalador cuando se ejecuta desde la carpeta de instalación.
        /// Importante: NO borra la carpeta de forma recursiva, para respetar el historial que el usuario decidió conservar;
        /// rmdir sin /s solo quita la carpeta si quedó vacía.
        /// </summary>
        private static void CleanupRemainingInstallDir(string dir)
        {
            try
            {
                string exePath = Application.ExecutablePath;
                string exeDir = Path.GetDirectoryName(exePath).TrimEnd('\\');
                if (dir.TrimEnd('\\').Equals(exeDir, StringComparison.OrdinalIgnoreCase))
                {
                    ProcessStartInfo psi = new ProcessStartInfo("cmd.exe");
                    psi.Arguments = "/c ping -n 3 127.0.0.1 >nul & del /f /q \"" + exePath + "\" & rmdir \"" + dir + "\" 2>nul";
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    Process.Start(psi);
                }
                else if (Directory.Exists(dir))
                {
                    string[] files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);
                    if (files.Length == 0)
                    {
                        try { Directory.Delete(dir, true); } catch { }
                    }
                }
            }
            catch { }
        }
    }
}
