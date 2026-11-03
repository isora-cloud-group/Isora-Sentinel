using IsoraSentinel;
// ============================================================================
// Isora Sentinel Suite — Ventana de Tareas Programadas
// Copyright (c) 2026 Sandra Gabriela Puerto Torres
// Licencia: Apache-2.0 (Open Source)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Win32;

namespace IsoraSentinel.Tray
{
    // =========================================================================
    //  TEMA DE LA VENTANA (cuerpo claro + cabecera oscura de marca)
    //  Contrastes sobre su fondo >= 4.5:1 en texto, >= 3:1 en bordes de controles.
    // =========================================================================
    internal static class ConfigTheme
    {
        public static readonly Color PageBg        = Color.FromArgb(241, 245, 249); // Slate 100
        public static readonly Color CardBg        = Color.White;
        public static readonly Color CardBgOff     = Color.FromArgb(248, 250, 252); // Slate 50
        public static readonly Color FieldBg       = Color.FromArgb(241, 245, 249);
        public static readonly Color Border        = Color.FromArgb(203, 213, 225); // Slate 300
        public static readonly Color BorderStrong  = Color.FromArgb(100, 116, 139); // Slate 500 (4.8:1 sobre blanco)
        public static readonly Color Text          = Color.FromArgb(15, 23, 42);    // Slate 900
        public static readonly Color TextMuted     = Color.FromArgb(71, 85, 105);   // Slate 600 (7.5:1)
        public static readonly Color Accent        = Color.FromArgb(3, 105, 161);   // Sky 700
        public static readonly Color AccentHover   = Color.FromArgb(7, 89, 133);    // Sky 800
        public static readonly Color Success       = Color.FromArgb(4, 120, 87);    // Emerald 700
        public static readonly Color SuccessBg     = Color.FromArgb(209, 250, 229); // Emerald 100
        public static readonly Color Warning       = Color.FromArgb(180, 83, 9);    // Amber 700
        public static readonly Color WarningBg     = Color.FromArgb(254, 243, 199); // Amber 100
        public static readonly Color WarningBar    = Color.FromArgb(217, 119, 6);   // Amber 600
        public static readonly Color NeutralBg     = Color.FromArgb(226, 232, 240); // Slate 200
        public static readonly Color OnHeaderMuted = Color.FromArgb(203, 213, 225); // Slate 300 sobre Slate 900

        public static Font F(float size)              { return new Font("Segoe UI", size, FontStyle.Regular); }
        public static Font F(float size, FontStyle s) { return new Font("Segoe UI", size, s); }
    }

    // =========================================================================
    //  MODELO DE ESTADO LEÍDO DEL PROGRAMADOR DE TAREAS
    // =========================================================================
    internal class TaskStateInfo
    {
        public bool Exists = false;
        public bool Enabled = false;
        public bool AccessDenied = false;
        public string Freq = null;    // Diario | Semanal | Mensual | Anual
        public string Time = null;    // HH:mm
        public int Day = -1;          // índice de día (semana: 0=Lunes; mes: 0=día 1)
        public int Month = -1;        // 0=Enero

        public string CustomPath = null;

        public int Mode = 0;
    }

    // =========================================================================
    //  CONTROLES BASE (layout apilado con wrap, sin posiciones fijas)
    // =========================================================================
    internal interface ICfgFlex
    {
        int GetHeight(int width);
    }

    internal static class Gfx
    {
        public static GraphicsPath Pill(RectangleF r)
        {
            float d = r.Height;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 90, 180);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 180);
            p.CloseFigure();
            return p;
        }
    }

    /// <summary>Label con wrap: calcula su altura según el ancho disponible.</summary>
    internal class CfgLabel : Label, ICfgFlex
    {
        public CfgLabel(string text, float size, FontStyle style, Color color)
        {
            AutoSize = false;
            UseMnemonic = false;
            Text = text ?? string.Empty;
            Font = ConfigTheme.F(size, style);
            ForeColor = color;
            BackColor = Color.Transparent;
        }

        public int GetHeight(int width)
        {
            if (width <= 0 || string.IsNullOrEmpty(Text)) return 0;
            Size sz = TextRenderer.MeasureText(Text, Font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            return sz.Height + 4;
        }
    }

    /// <summary>Contenedor vertical: apila hijos al ancho disponible y ajusta su alto.</summary>
    internal class CfgStack : Panel, ICfgFlex
    {
        public int Spacing = 8;
        public bool AutoHeight = true;
        public bool Bordered = true;
        private bool busy;

        public CfgStack()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = ConfigTheme.CardBg;
        }

        private int Arrange(int width, bool apply)
        {
            int x = Padding.Left;
            int y = Padding.Top;
            int w = Math.Max(20, width - Padding.Horizontal);
            bool first = true;

            foreach (Control c in Controls)
            {
                if (c == null || !c.Visible) continue;
                int extra = (c.Tag is int) ? (int)c.Tag : 0;
                if (!first) y += Spacing + extra;
                first = false;

                ICfgFlex f = c as ICfgFlex;
                int h = (f != null) ? f.GetHeight(w) : c.Height;
                if (apply) c.SetBounds(x, y, w, h);
                y += h;
            }
            return y + Padding.Bottom;
        }

        public int GetHeight(int width) { return Arrange(width, false); }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (busy) return;
            busy = true;
            try
            {
                int h = Arrange(ClientSize.Width, true);
                if (AutoHeight && Height != h) Height = h;
            }
            finally { busy = false; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Bordered)
                using (Pen p = new Pen(ConfigTheme.Border))
                    e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    /// <summary>Barra de botones que hace wrap y reporta su altura.</summary>
    internal class FlowBar : FlowLayoutPanel, ICfgFlex
    {
        public FlowBar()
        {
            FlowDirection = FlowDirection.LeftToRight;
            WrapContents = true;
            AutoSize = false;
            BackColor = Color.Transparent;
        }

        public int GetHeight(int width)
        {
            return GetPreferredSize(new Size(Math.Max(50, width), 0)).Height;
        }
    }

    /// <summary>Interruptor (on/off) accesible por teclado, con contraste verificado.</summary>
    internal class ToggleSwitch : Control
    {
        private bool isChecked;
        private bool hover;
        private float animPos = 0f;
        private System.Windows.Forms.Timer animTimer;
        public event EventHandler CheckedChanged;

        public ToggleSwitch(float scale)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.CheckButton;
            Size = new Size((int)Math.Round(40 * scale), (int)Math.Round(22 * scale));

            animTimer = new System.Windows.Forms.Timer();
            animTimer.Interval = 15;
            animTimer.Tick += delegate
            {
                float target = isChecked ? 1f : 0f;
                float diff = target - animPos;
                if (Math.Abs(diff) < 0.08f)
                {
                    animPos = target;
                    animTimer.Stop();
                }
                else
                {
                    animPos += diff * 0.38f;
                }
                Invalidate();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && animTimer != null)
            {
                animTimer.Stop();
                animTimer.Dispose();
                animTimer = null;
            }
            base.Dispose(disposing);
        }

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked == value) return;
                isChecked = value;
                if (IsHandleCreated && Visible)
                {
                    animTimer.Start();
                }
                else
                {
                    animPos = isChecked ? 1f : 0f;
                    Invalidate();
                }
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (Enabled) { Focus(); Checked = !Checked; }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && Enabled) { Checked = !Checked; e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Space || base.IsInputKey(keyData); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        private static Color BlendColor(Color a, Color b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            int r = (int)Math.Round(a.R + (b.R - a.R) * t);
            int g = (int)Math.Round(a.G + (b.G - a.G) * t);
            int bl = (int)Math.Round(a.B + (b.B - a.B) * t);
            return Color.FromArgb(r, g, bl);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = g.DpiX / 96f;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color bg = (Parent != null) ? Parent.BackColor : Color.White;
            using (Brush b = new SolidBrush(bg)) g.FillRectangle(b, ClientRectangle);

            RectangleF track = new RectangleF(1.5f * s, 1.5f * s, Width - 3f * s, Height - 3f * s);
            float t = Math.Max(0f, Math.Min(1f, animPos));
            Color fill, border, knob;
            if (!Enabled)
            {
                fill = Color.FromArgb(226, 232, 240); border = Color.FromArgb(203, 213, 225); knob = Color.FromArgb(148, 163, 184);
            }
            else
            {
                Color onColor = hover ? ConfigTheme.AccentHover : ConfigTheme.Accent;
                Color offFill = hover ? Color.FromArgb(241, 245, 249) : Color.White;
                fill = BlendColor(offFill, onColor, t);
                border = BlendColor(ConfigTheme.BorderStrong, onColor, t);
                knob = BlendColor(ConfigTheme.BorderStrong, Color.White, t);
            }

            using (GraphicsPath gp = Gfx.Pill(track))
            {
                using (Brush b = new SolidBrush(fill)) g.FillPath(b, gp);
                using (Pen p = new Pen(border, 1.5f * s)) g.DrawPath(p, gp);
            }

            float d = track.Height - 6f * s;
            float xOff = track.X + 3f * s;
            float xOn = track.Right - 3f * s - d;
            float x = xOff + (xOn - xOff) * t;
            using (Brush b = new SolidBrush(knob))
                g.FillEllipse(b, x, track.Y + 3f * s, d, d);

            if (Focused && ShowFocusCues)
                using (Pen p = new Pen(ConfigTheme.Accent, 1f))
                {
                    p.DashStyle = DashStyle.Dot;
                    g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
                }
        }
    }

    internal enum BadgeKind { Neutral, Success, Warning }

    /// <summary>Insignia de estado (pastilla con punto y texto).</summary>
    internal class StatusBadge : Control
    {
        private BadgeKind kind = BadgeKind.Neutral;
        private string label = "";

        public StatusBadge()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = ConfigTheme.F(8.25F, FontStyle.Bold);
        }

        public void Set(string text, BadgeKind k)
        {
            label = text; kind = k;
            Invalidate();
        }

        public int PreferredWidth(float scale)
        {
            Size s = TextRenderer.MeasureText(label, Font, new Size(1000, 100),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            return s.Width + (int)Math.Round(32 * scale);
        }

        public int PreferredHeight(float scale) { return (int)Math.Round(22 * scale); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = g.DpiX / 96f;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color bgPage = (Parent != null) ? Parent.BackColor : Color.White;
            using (Brush b = new SolidBrush(bgPage)) g.FillRectangle(b, ClientRectangle);

            Color bg, fg;
            if (kind == BadgeKind.Success) { bg = ConfigTheme.SuccessBg; fg = ConfigTheme.Success; }
            else if (kind == BadgeKind.Warning) { bg = ConfigTheme.WarningBg; fg = ConfigTheme.Warning; }
            else { bg = ConfigTheme.NeutralBg; fg = ConfigTheme.TextMuted; }

            RectangleF r = new RectangleF(0, 0, Width - 1, Height - 1);
            using (GraphicsPath gp = Gfx.Pill(r))
            using (Brush b = new SolidBrush(bg))
                g.FillPath(b, gp);

            float dot = 6f * s;
            using (Brush b = new SolidBrush(fg))
                g.FillEllipse(b, 10f * s, (Height - dot) / 2f, dot, dot);

            Rectangle tr = new Rectangle((int)(22 * s), 0, Width - (int)(26 * s), Height);
            TextRenderer.DrawText(g, label, Font, tr, fg, bg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }
    }

    /// <summary>Fila de opción: interruptor opcional + título + descripción + control a la derecha.</summary>
    internal class ToggleRow : Panel, ICfgFlex
    {
        private ToggleSwitch tg;
        private CfgLabel title;
        private CfgLabel desc;
        private Control right;
        private float dpi;
        public bool ShowDivider = true;

        public ToggleRow(string t, string d, ToggleSwitch toggle, Control rightCtl, float scale)
        {
            dpi = scale;
            DoubleBuffered = true;
            ResizeRedraw = true;
            tg = toggle;
            right = rightCtl;

            title = new CfgLabel(t, 9.75F, FontStyle.Bold, ConfigTheme.Text);
            desc = new CfgLabel(d, 8.75F, FontStyle.Regular, ConfigTheme.TextMuted);
            if (tg != null)
            {
                title.Cursor = Cursors.Hand;
                title.Click += delegate { if (tg.Enabled) tg.Checked = !tg.Checked; };
                tg.AccessibleName = t;
            }

            SuspendLayout();
            int pv = (int)Math.Round(10 * dpi);
            Padding = new Padding(0, pv, 0, pv);
            if (tg != null) Controls.Add(tg);
            Controls.Add(title);
            Controls.Add(desc);
            if (right != null) Controls.Add(right);
            ResumeLayout(true);
        }

        private int LeftX() { return tg != null ? tg.Width + (int)Math.Round(12 * dpi) : 0; }
        private int RightW() { return right != null ? right.Width + (int)Math.Round(12 * dpi) : 0; }

        public int GetHeight(int w)
        {
            int tw = Math.Max(100, w - LeftX() - RightW());
            return title.GetHeight(tw) + 2 + desc.GetHeight(tw) + Padding.Vertical;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int w = ClientSize.Width;
            int lx = LeftX();
            int tw = Math.Max(100, w - lx - RightW());
            int th = title.GetHeight(tw);
            if (tg != null) tg.Location = new Point(0, Padding.Top + Math.Max(0, (th - tg.Height) / 2));
            title.SetBounds(lx, Padding.Top, tw, th);
            desc.SetBounds(lx, Padding.Top + th + 2, tw, desc.GetHeight(tw));
            if (right != null) right.Location = new Point(w - right.Width, Padding.Top);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!ShowDivider) return;
            using (Pen p = new Pen(Color.FromArgb(226, 232, 240)))
                e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }
    }

    // =========================================================================
    //  TARJETA DE RUTINA
    // =========================================================================
    internal class TaskConfigCard : Panel, ICfgFlex
    {
        public string TaskName;
        public string ScriptName;
        public string ControlId;
        public bool InitialExists = false;
        public event EventHandler Changed;

        private float dpi = 1f;
        private bool loading = true;
        private bool accessDenied = false;
        private bool suppress = false;
        private string baseline = "off";
        private string dayMode = "";

        private ToggleSwitch toggle;
        private CfgLabel eyebrow;
        private CfgLabel title;
        private CfgLabel desc;
        private CfgLabel note;
        private StatusBadge badge;
        private Button btnCancel;
        private int activePid = 0;
        private Action<string, int, string> cancelCallback;
        private FlowLayoutPanel schedule;
        private FlowLayoutPanel grpDate;
        private Label lblParam;
        private ComboBox cmbFreq;
        private ComboBox cmbDay;
        private ComboBox cmbMonth;
        private DateTimePicker dtpTime;
        private Panel pathPanel;
        private TextBox txtCustomPath;
        private Button btnBrowse;
        private FlowLayoutPanel grpMode;
        private ComboBox cmbMode;
        private bool suppressModeWarning = false;

        private int Sz(int v) { return (int)Math.Round(v * dpi); }

        public string GetCustomPath()
        {
            return (txtCustomPath != null) ? txtCustomPath.Text.Trim() : "";
        }

        public bool Active
        {
            get { return toggle.Checked; }
            set { toggle.Checked = value; }
        }

        public bool Dirty { get { return Snapshot() != baseline; } }

        public TaskConfigCard(string tName, string sName, string titleText, string cId, string descText,
            string defaultFreq, int defaultDay, int defaultMonth, string defaultTime, float scale, Action<string, int, string> onCancel)
        {
            TaskName = tName;
            ScriptName = sName;
            ControlId = cId;
            dpi = scale;
            cancelCallback = onCancel;

            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = ConfigTheme.CardBg;

            toggle = new ToggleSwitch(dpi);
            toggle.AccessibleName = titleText;
            toggle.Enabled = false;                    // hasta conocer el estado real
            toggle.CheckedChanged += delegate { OnUserChange(); };

            eyebrow = new CfgLabel((cId ?? "").ToUpper(CultureInfo.CurrentCulture), 8F, FontStyle.Bold, ConfigTheme.Accent);
            title = new CfgLabel(titleText, 10.5F, FontStyle.Bold, ConfigTheme.Text);
            title.Cursor = Cursors.Hand;
            title.Click += delegate { if (toggle.Enabled) toggle.Checked = !toggle.Checked; };
            desc = new CfgLabel(descText, 9F, FontStyle.Regular, ConfigTheme.TextMuted);
            note = new CfgLabel("", 9F, FontStyle.Italic, ConfigTheme.TextMuted);
            badge = new StatusBadge();

            // ---------- Panel de programación (fluido, con wrap) ----------
            schedule = new FlowLayoutPanel();
            schedule.FlowDirection = FlowDirection.LeftToRight;
            schedule.WrapContents = true;
            schedule.AutoSize = false;
            schedule.Padding = new Padding(Sz(10), Sz(6), Sz(10), Sz(2));
            schedule.BackColor = ConfigTheme.FieldBg;

            FlowLayoutPanel grpFreq = NewGroup();
            grpFreq.Controls.Add(NewCaption("Frecuencia"));
            cmbFreq = NewCombo(92);
            cmbFreq.Items.AddRange(new object[] { "Diario", "Semanal", "Mensual", "Anual" });
            grpFreq.Controls.Add(cmbFreq);
            schedule.Controls.Add(grpFreq);

            grpDate = NewGroup();
            lblParam = NewCaption("Día");
            grpDate.Controls.Add(lblParam);
            cmbMonth = NewCombo(104);
            cmbMonth.Items.AddRange(new object[] {
                "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
                "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" });
            cmbMonth.Margin = new Padding(0, Sz(2), Sz(6), Sz(6));
            grpDate.Controls.Add(cmbMonth);
            cmbDay = NewCombo(100);
            grpDate.Controls.Add(cmbDay);
            schedule.Controls.Add(grpDate);

            FlowLayoutPanel grpTime = NewGroup();
            grpTime.Margin = new Padding(0);
            grpTime.Controls.Add(NewCaption("Hora"));
            dtpTime = new DateTimePicker();
            dtpTime.Format = DateTimePickerFormat.Custom;
            dtpTime.CustomFormat = "HH:mm";
            dtpTime.ShowUpDown = true;
            dtpTime.Font = ConfigTheme.F(8.75F);
            dtpTime.Width = Sz(72);
            dtpTime.Margin = new Padding(0, Sz(2), 0, Sz(6));
            DateTime parsed;
            if (DateTime.TryParseExact(defaultTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                dtpTime.Value = parsed;
            else
                dtpTime.Value = DateTime.Today.AddHours(3);
            grpTime.Controls.Add(dtpTime);
            schedule.Controls.Add(grpTime);

            cmbFreq.SelectedIndexChanged += delegate { UpdateParamControls(); OnUserChange(); };
            cmbDay.SelectedIndexChanged += delegate { OnUserChange(); };
            cmbMonth.SelectedIndexChanged += delegate { OnUserChange(); };
            dtpTime.ValueChanged += delegate { OnUserChange(); };

            suppress = true;
            int fIdx = 0;
            if (defaultFreq == "Semanal") fIdx = 1;
            else if (defaultFreq == "Mensual") fIdx = 2;
            else if (defaultFreq == "Anual") fIdx = 3;
            cmbFreq.SelectedIndex = fIdx;
            cmbMonth.SelectedIndex = (defaultMonth >= 0 && defaultMonth < cmbMonth.Items.Count) ? defaultMonth : 0;
            if (defaultDay >= 0 && defaultDay < cmbDay.Items.Count) cmbDay.SelectedIndex = defaultDay;
            suppress = false;

            SuspendLayout();
            Controls.Add(toggle);
            Controls.Add(eyebrow);
            Controls.Add(title);
            Controls.Add(badge);
            Controls.Add(desc);
            Controls.Add(schedule);
            Controls.Add(note);

            btnCancel = new Button();
            btnCancel.Text = "Cancelar ejecución";
            btnCancel.UseMnemonic = false;
            btnCancel.Font = ConfigTheme.F(8F, FontStyle.Bold);
            btnCancel.ForeColor = Color.FromArgb(185, 28, 28);
            btnCancel.BackColor = Color.FromArgb(254, 242, 242);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165);
            btnCancel.FlatAppearance.BorderSize = 1;
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.Visible = false;
            btnCancel.Click += delegate
            {
                if (activePid > 0 && cancelCallback != null)
                {
                    cancelCallback(TaskName, activePid, title.Text);
                }
            };
            Controls.Add(btnCancel);

            if (TaskName == "Defender_Scan")
            {
                grpMode = NewGroup();
                grpMode.Controls.Add(NewCaption("Intensidad"));
                cmbMode = NewCombo(110);
                cmbMode.Items.AddRange(new object[] { "Examen rápido", "Examen completo" });
                cmbMode.SelectedIndex = 0;
                cmbMode.SelectedIndexChanged += delegate
                {
                    if (!suppressModeWarning && cmbMode.SelectedIndex == 1)
                    {
                        DialogResult dr = MessageBox.Show(this,
                            "El análisis completo consume una cantidad significativa de recursos de CPU y disco, lo que puede ralentizar el equipo.\n\nSe recomienda ejecutarlo fuera del horario laboral o no utilizar el equipo mientras se ejecuta.\n\n¿Desea confirmar el modo Completo?",
                            "Confirmación de Carga de Trabajo",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning
                        );
                        if (dr != DialogResult.Yes)
                        {
                            suppressModeWarning = true;
                            cmbMode.SelectedIndex = 0;
                            suppressModeWarning = false;
                        }
                    }
                    OnUserChange();
                };
                grpMode.Controls.Add(cmbMode);
                schedule.Controls.Add(grpMode);
            }
            else if (TaskName == "Integrity_Check")
            {
                grpMode = NewGroup();
                grpMode.Controls.Add(NewCaption("Intensidad"));
                cmbMode = NewCombo(110);
                cmbMode.Items.AddRange(new object[] { "Revisión rápida", "Reparación completa" });
                cmbMode.SelectedIndex = 0;
                cmbMode.SelectedIndexChanged += delegate
                {
                    if (!suppressModeWarning && cmbMode.SelectedIndex == 1)
                    {
                        DialogResult dr = MessageBox.Show(this,
                            "La comprobación y reparación completa analiza todo el almacén de componentes de Windows en disco, lo que consume muchos recursos y puede tardar de 15 a 45 minutos ralentizando el equipo.\n\nSe recomienda no utilizar el equipo intensivamente mientras se ejecuta.\n\n¿Desea confirmar la Reparación completa?",
                            "Confirmación de Carga de Trabajo",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning
                        );
                        if (dr != DialogResult.Yes)
                        {
                            suppressModeWarning = true;
                            cmbMode.SelectedIndex = 0;
                            suppressModeWarning = false;
                        }
                    }
                    OnUserChange();
                };
                grpMode.Controls.Add(cmbMode);
                schedule.Controls.Add(grpMode);
            }

            if (TaskName == "Archive_Downloads")
            {
                pathPanel = new Panel();
                pathPanel.BackColor = Color.Transparent;
                pathPanel.Height = Sz(56);

                CfgLabel lblPath = new CfgLabel("Directorio de retención de descargas:", 8.25F, FontStyle.Bold, ConfigTheme.Accent);
                lblPath.SetBounds(0, 0, Sz(320), Sz(18));
                pathPanel.Controls.Add(lblPath);

                txtCustomPath = new TextBox();
                txtCustomPath.Font = ConfigTheme.F(8.75F);
                txtCustomPath.ForeColor = ConfigTheme.Text;
                txtCustomPath.BackColor = Color.White;
                txtCustomPath.BorderStyle = BorderStyle.FixedSingle;
                txtCustomPath.Text = @"%USERPROFILE%\Documents\Isora_Sentinel_Archived\Downloads";
                txtCustomPath.TextChanged += delegate { OnUserChange(); };
                pathPanel.Controls.Add(txtCustomPath);

                btnBrowse = new Button();
                btnBrowse.Text = "Examinar...";
                btnBrowse.Font = ConfigTheme.F(8F);
                btnBrowse.FlatStyle = FlatStyle.Flat;
                btnBrowse.FlatAppearance.BorderColor = ConfigTheme.BorderStrong;
                btnBrowse.FlatAppearance.MouseOverBackColor = ConfigTheme.FieldBg;
                btnBrowse.BackColor = Color.White;
                btnBrowse.ForeColor = ConfigTheme.Text;
                btnBrowse.Cursor = Cursors.Hand;
                btnBrowse.Click += delegate
                {
                    using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                    {
                        fbd.Description = "Seleccione el directorio de destino para descargas archivadas:";
                        if (fbd.ShowDialog() == DialogResult.OK)
                        {
                            txtCustomPath.Text = fbd.SelectedPath;
                        }
                    }
                };
                pathPanel.Controls.Add(btnBrowse);
                Controls.Add(pathPanel);
            }
            ResumeLayout(false);

            UpdateVisual();
        }

        // ---------------- Fábricas de controles ----------------
        private FlowLayoutPanel NewGroup()
        {
            FlowLayoutPanel g = new FlowLayoutPanel();
            g.FlowDirection = FlowDirection.LeftToRight;
            g.WrapContents = false;
            g.AutoSize = true;
            g.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            g.Margin = new Padding(0, 0, Sz(18), 0);
            g.Padding = new Padding(0);
            g.BackColor = Color.Transparent;
            return g;
        }

        private Label NewCaption(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.UseMnemonic = false;
            l.Font = ConfigTheme.F(8.5F, FontStyle.Bold);
            l.ForeColor = ConfigTheme.TextMuted;
            l.BackColor = Color.Transparent;
            l.Margin = new Padding(0, Sz(7), Sz(8), 0);
            return l;
        }

        private ComboBox NewCombo(int width)
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Font = ConfigTheme.F(8.75F);
            c.Width = Sz(width);
            c.Margin = new Padding(0, Sz(2), 0, Sz(6));
            return c;
        }

        // ---------------- Lógica de estado ----------------
        private void UpdateParamControls()
        {
            string freq = cmbFreq.SelectedItem != null ? cmbFreq.SelectedItem.ToString() : "Diario";
            grpDate.Visible = (freq != "Diario");
            cmbMonth.Visible = (freq == "Anual");
            lblParam.Text = (freq == "Anual") ? "Fecha" : "Día";

            if (freq == "Semanal")
            {
                if (dayMode != "W")
                {
                    cmbDay.Items.Clear();
                    cmbDay.Items.AddRange(new object[] { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" });
                    cmbDay.SelectedIndex = 6;
                    dayMode = "W";
                }
                cmbDay.Width = Sz(100);
            }
            else if (freq == "Mensual" || freq == "Anual")
            {
                if (dayMode != "M")
                {
                    cmbDay.Items.Clear();
                    for (int d = 1; d <= 31; d++) cmbDay.Items.Add("Día " + d);
                    cmbDay.SelectedIndex = 0;
                    dayMode = "M";
                }
                cmbDay.Width = Sz(84);
            }
        }

        /// <summary>Resumen del estado actual; se compara contra el estado base para detectar cambios.</summary>
        private string Snapshot()
        {
            if (!toggle.Checked) return "off";
            string pathVal = (txtCustomPath != null) ? txtCustomPath.Text.Trim() : "";
            string modeVal = (cmbMode != null) ? cmbMode.SelectedIndex.ToString() : "0";
            string freq = cmbFreq.SelectedItem != null ? cmbFreq.SelectedItem.ToString() : "Diario";
            string t = dtpTime.Value.ToString("HH:mm");
            if (freq == "Diario") return "on|Diario|" + t + "|" + pathVal + "|" + modeVal;
            if (freq == "Semanal" || freq == "Mensual") return "on|" + freq + "|" + cmbDay.SelectedIndex + "|" + t + "|" + pathVal + "|" + modeVal;
            return "on|Anual|" + cmbMonth.SelectedIndex + "|" + cmbDay.SelectedIndex + "|" + t + "|" + pathVal + "|" + modeVal;
        }

        private void OnUserChange()
        {
            if (suppress) return;
            UpdateVisual();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        private void UpdateVisual()
        {
            bool showSchedule = !loading && toggle.Checked;
            schedule.Visible = showSchedule;
            note.Visible = !showSchedule;
            note.Text = loading
                ? "Consultando el estado en el Programador de tareas…"
                : "Rutina desactivada. Actívela para definir su programación.";

            BackColor = (loading || toggle.Checked) ? ConfigTheme.CardBg : ConfigTheme.CardBgOff;
            UpdateBadge();
            Invalidate();
            Reflow();
        }

        private bool isRunningNow = false;
        private int activePercent = 0;
        private bool cardHover = false;

        protected override void OnMouseEnter(EventArgs e)
        {
            cardHover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            cardHover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        public void UpdateRuntimeState(TaskProgressInfo progress)
        {
            if (loading) return;
            if (progress != null && progress.IsActive())
            {
                isRunningNow = true;
                activePid = progress.Pid;
                activePercent = Math.Max(0, Math.Min(100, progress.Percent));
                badge.Set("En ejecución · " + activePercent + "%", BadgeKind.Warning);
                string stat = !string.IsNullOrEmpty(progress.Status) ? progress.Status : progress.Activity;
                note.Text = "En curso: " + stat + " (" + activePercent + "% completado)";
                note.Font = ConfigTheme.F(8.75F, FontStyle.Bold);
                note.ForeColor = ConfigTheme.Accent;
                note.Visible = true;
                if (btnCancel != null) btnCancel.Visible = true;
                Invalidate();
            }
            else if (isRunningNow)
            {
                isRunningNow = false;
                activePid = 0;
                activePercent = 0;
                note.Font = ConfigTheme.F(9F, FontStyle.Italic);
                note.ForeColor = ConfigTheme.TextMuted;
                if (btnCancel != null) btnCancel.Visible = false;
                UpdateVisual();
            }
        }

        private void UpdateBadge()
        {
            if (isRunningNow) return;
            if (loading) badge.Set("Consultando…", BadgeKind.Neutral);
            else if (accessDenied) badge.Set("Requiere Administrador", BadgeKind.Warning);
            else if (Dirty) badge.Set("Sin guardar", BadgeKind.Warning);
            else if (toggle.Checked) badge.Set("Activa", BadgeKind.Success);
            else if (InitialExists) badge.Set("Desactivada", BadgeKind.Neutral);
            else badge.Set("No programada", BadgeKind.Neutral);
        }

        private void Reflow()
        {
            PerformLayout();
            for (Control c = Parent; c != null && !(c is Form); c = c.Parent) c.PerformLayout();
        }

        public void SetLoading()
        {
            loading = true;
            toggle.Enabled = false;
            UpdateVisual();
        }

        public void ApplyState(TaskStateInfo st)
        {
            suppress = true;
            loading = false;
            toggle.Enabled = true;
            accessDenied = false;
            InitialExists = true;

            if (!string.IsNullOrEmpty(st.Freq))
            {
                for (int i = 0; i < cmbFreq.Items.Count; i++)
                    if (cmbFreq.Items[i].ToString().Equals(st.Freq, StringComparison.OrdinalIgnoreCase))
                    { cmbFreq.SelectedIndex = i; break; }
            }
            if (st.Day >= 0 && st.Day < cmbDay.Items.Count) cmbDay.SelectedIndex = st.Day;
            if (st.Month >= 0 && st.Month < cmbMonth.Items.Count) cmbMonth.SelectedIndex = st.Month;
            if (!string.IsNullOrEmpty(st.Time))
            {
                DateTime dt;
                if (DateTime.TryParseExact(st.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                    dtpTime.Value = dt;
            }
            if (txtCustomPath != null)
            {
                if (!string.IsNullOrEmpty(st.CustomPath))
                {
                    txtCustomPath.Text = st.CustomPath;
                }
                else
                {
                    string regPath = TaskConfigForm.GetRegistryArchiveDir();
                    txtCustomPath.Text = !string.IsNullOrEmpty(regPath) ? regPath : @"%USERPROFILE%\Documents\Isora_Sentinel_Archived\Downloads";
                }
            }

            if (cmbMode != null)
            {
                suppressModeWarning = true;
                cmbMode.SelectedIndex = (st.Mode == 1) ? 1 : 0;
                suppressModeWarning = false;
            }

            toggle.Checked = st.Enabled;
            suppress = false;

            baseline = Snapshot();
            UpdateVisual();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        public void SetAccessDenied()
        {
            suppress = true;
            loading = false;
            accessDenied = true;
            toggle.Enabled = false;
            InitialExists = false;
            toggle.Checked = false;
            suppress = false;

            baseline = Snapshot();
            note.Text = "Permisos insuficientes para consultar el estado. Abra la ventana como Administrador.";
            note.Visible = true;
            UpdateVisual();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        public void SetNotScheduled()
        {
            suppress = true;
            loading = false;
            toggle.Enabled = true;
            accessDenied = false;
            InitialExists = false;
            toggle.Checked = false;
            suppress = false;

            baseline = Snapshot();
            UpdateVisual();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        // ---------------- Comandos para schtasks ----------------
        private string GetTriggerArgs()
        {
            string timeStr = dtpTime.Value.ToString("HH:mm");
            string freq = cmbFreq.SelectedItem != null ? cmbFreq.SelectedItem.ToString() : "Diario";

            if (freq == "Semanal")
            {
                string[] codes = new string[] { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" };
                int idx = (cmbDay.SelectedIndex >= 0 && cmbDay.SelectedIndex < 7) ? cmbDay.SelectedIndex : 6;
                return string.Format("/SC WEEKLY /D {0} /ST {1}", codes[idx], timeStr);
            }
            if (freq == "Mensual")
            {
                int d = (cmbDay.SelectedIndex >= 0 && cmbDay.SelectedIndex < 31) ? cmbDay.SelectedIndex + 1 : 1;
                return string.Format("/SC MONTHLY /D {0} /ST {1}", d, timeStr);
            }
            if (freq == "Anual")
            {
                string[] months = new string[] { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
                int m = (cmbMonth.SelectedIndex >= 0 && cmbMonth.SelectedIndex < 12) ? cmbMonth.SelectedIndex : 0;
                int d = (cmbDay.SelectedIndex >= 0 && cmbDay.SelectedIndex < 31) ? cmbDay.SelectedIndex + 1 : 1;
                return string.Format("/SC MONTHLY /M {0} /D {1} /ST {2}", months[m], d, timeStr);
            }
            return string.Format("/SC DAILY /ST {0}", timeStr);
        }

        /// <summary>
        /// Fragmento de PowerShell para esta tarjeta (vacío si no hay cambios).
        /// <summary>
        /// Construye el comando PowerShell para crear, actualizar o deshabilitar la tarea programada.
        /// Incluye sanitización de rutas personalizadas y aplicación automática de ajustes de batería
        /// y Modern Standby (S0) para portátiles con Windows 11 (<see cref="TaskSchedulerBridge"/>).
        /// </summary>
        public string BuildCommand(string scriptPath)
        {
            if (!Dirty) return "";
            string args;
            string postHarden = "";
            if (toggle.Checked)
            {
                string extraParam = "";
                if (TaskName == "Cleanup_Screenshots" || TaskName == "Cleanup_Temp_Global" || TaskName == "Archive_Downloads")
                {
                    extraParam += " -Apply";
                }
                if (TaskName == "Archive_Downloads" && txtCustomPath != null)
                {
                    string p = SentinelSecurity.SanitizeDirectoryPath(txtCustomPath.Text);
                    if (!string.IsNullOrEmpty(p) &&
                        !string.Equals(p, @"%USERPROFILE%\Documents\Isora_Sentinel_Archived\Downloads", StringComparison.OrdinalIgnoreCase))
                    {
                        extraParam += " -CustomArchiveDir \\\"" + p + "\\\"";
                    }
                }
                else if (TaskName == "Defender_Scan" && cmbMode != null)
                {
                    extraParam = (cmbMode.SelectedIndex == 1) ? " -ScanType 2" : " -ScanType 1";
                }
                else if (TaskName == "Integrity_Check" && cmbMode != null)
                {
                    extraParam = (cmbMode.SelectedIndex == 1) ? " -AutoRepair" : " -QuickCheck";
                }
                args = string.Format(
                    "/Create /TN \"Isora Sentinel\\{0}\" /TR \"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \\\"{1}\\\"{2}\" {3} /RU \"NT AUTHORITY\\SYSTEM\" /RL HIGHEST /F",
                    TaskName, scriptPath, extraParam, GetTriggerArgs());
                postHarden = TaskSchedulerBridge.BuildHardenTaskSettingsPowerShell(TaskName);
            }
            else if (InitialExists)
            {
                // Se deshabilita, no se elimina: así se conserva su programación.
                args = string.Format("/Change /TN \"Isora Sentinel\\{0}\" /DISABLE", TaskName);
            }
            else
            {
                return "";
            }
            return "$p = Start-Process -FilePath 'schtasks.exe' -ArgumentList '" + SentinelSecurity.EscapePowerShellSingleQuoted(args) +
                   "' -WindowStyle Hidden -Wait -PassThru; if ($p.ExitCode -ne 0) { $errs++ } else { " + postHarden + " }; ";
        }

        // ---------------- Layout responsive ----------------
        private int Arrange(int w, bool apply)
        {
            int p = Sz(16), gap = Sz(12);
            int textX = p + toggle.Width + gap;
            int badgeW = badge.PreferredWidth(dpi);
            int badgeH = badge.PreferredHeight(dpi);
            int fullW = Math.Max(Sz(120), w - textX - p);
            int titleW = Math.Max(Sz(100), fullW - badgeW - gap);

            int eh = eyebrow.GetHeight(titleW);
            int th = title.GetHeight(titleW);
            int dh = desc.GetHeight(fullW);

            int yEyebrow = p;
            int yTitle = yEyebrow + eh + 1;
            int yDesc = yTitle + th + 4;
            int yBottom = yDesc + dh + Sz(10);

            int bh = schedule.Visible
                ? schedule.GetPreferredSize(new Size(fullW, 0)).Height
                : note.GetHeight(fullW);
            int pathH = (pathPanel != null && schedule.Visible) ? pathPanel.Height + Sz(8) : 0;
            int total = yBottom + bh + pathH + p;

            if (apply)
            {
                eyebrow.SetBounds(textX, yEyebrow, titleW, eh);
                title.SetBounds(textX, yTitle, titleW, th);
                toggle.Location = new Point(p, yTitle + (th - toggle.Height) / 2);
                badge.SetBounds(w - p - badgeW, yEyebrow, badgeW, badgeH);
                if (btnCancel != null && isRunningNow)
                {
                    int btnW = Sz(130), btnH = Sz(24);
                    btnCancel.SetBounds(w - p - btnW, yEyebrow + badgeH + Sz(3), btnW, btnH);
                }
                desc.SetBounds(textX, yDesc, fullW, dh);
                if (schedule.Visible) schedule.SetBounds(textX, yBottom, fullW, bh);
                else note.SetBounds(textX, yBottom, fullW, bh);

                if (pathPanel != null)
                {
                    pathPanel.Visible = schedule.Visible;
                    if (schedule.Visible)
                    {
                        pathPanel.SetBounds(textX, yBottom + bh + Sz(6), fullW, pathPanel.Height);
                        int btnW = Sz(90);
                        int txtW = Math.Max(Sz(100), fullW - btnW - Sz(8));
                        txtCustomPath.SetBounds(0, Sz(20), txtW, Sz(24));
                        btnBrowse.SetBounds(txtW + Sz(8), Sz(20), btnW, Sz(24));
                    }
                }
            }
            return total;
        }

        public int GetHeight(int width) { return Arrange(width, false); }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (toggle == null || schedule == null) return;
            Arrange(ClientSize.Width, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float s = e.Graphics.DpiX / 96f;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Color borderColor = cardHover ? ConfigTheme.BorderStrong : ConfigTheme.Border;
            using (Pen p = new Pen(borderColor))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);

            Color bar = Color.Empty;
            if (!loading)
            {
                if (isRunningNow || Dirty) bar = ConfigTheme.WarningBar;
                else if (toggle.Checked) bar = ConfigTheme.Accent;
            }
            if (!bar.IsEmpty)
                using (Brush b = new SolidBrush(bar))
                    e.Graphics.FillRectangle(b, 0, 0, (int)Math.Round(4 * s), Height);

            if (isRunningNow)
            {
                int trackH = (int)Math.Max(3, Math.Round(4 * s));
                int trackY = Height - trackH - 1;
                int trackX = (int)Math.Round(4 * s);
                int trackW = Math.Max(10, Width - trackX - 1);
                using (Brush bTrack = new SolidBrush(ConfigTheme.NeutralBg))
                    e.Graphics.FillRectangle(bTrack, trackX, trackY, trackW, trackH);
                int fillW = (int)Math.Round(trackW * (Math.Max(4, activePercent) / 100.0));
                using (Brush bFill = new SolidBrush(ConfigTheme.Accent))
                    e.Graphics.FillRectangle(bFill, trackX, trackY, fillW, trackH);
            }
        }
    }

    // =========================================================================
    //  VENTANA DE TAREAS PROGRAMADAS
    // =========================================================================
    internal class TaskConfigForm : Form
    {
        private static readonly string[] DaysEn = new string[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
        private static readonly string[] MonthsEn = new string[] { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };

        private string installDir;
        private float dpi = 1f;
        private Action<string, bool, Action<int>> runPsAction;
        private Action openSetupAction;
        private List<TaskConfigCard> cards = new List<TaskConfigCard>();
        private ToggleSwitch toggleAutoStart;
        private bool autoStartBaseline;
        private bool applying = false;
        private System.Windows.Forms.Timer pollTimer;

        private Button btnApply;
        private Button btnClose;
        private CfgLabel lblPending;
        private CfgLabel lblElev;

        private int Sz(int v) { return (int)Math.Round(v * dpi); }

        public TaskConfigForm(string dir, Icon appIcon, float scale,
            Action<string, bool, Action<int>> psRunner, Action setupRunner)
        {
            installDir = dir;
            dpi = scale;
            runPsAction = psRunner;
            openSetupAction = setupRunner;

            Text = "Isora Sentinel · Tareas programadas";
            if (appIcon != null) Icon = appIcon;
            BackColor = ConfigTheme.PageBg;
            ForeColor = ConfigTheme.Text;
            Font = ConfigTheme.F(9F);
            StartPosition = FormStartPosition.CenterScreen;

            // Tamaño acotado al área de trabajo (laptops de 768 px de alto)
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            int w = Math.Min(Sz(780), (int)(wa.Width * 0.92));
            int h = Math.Min(Sz(800), (int)(wa.Height * 0.86));
            ClientSize = new Size(w, h);
            MinimumSize = new Size(Math.Min(Sz(520), wa.Width), Math.Min(Sz(460), wa.Height));

            if (!IsAdministrator())
            {
                try
                {
                    ProcessStartInfo psiElevate = new ProcessStartInfo();
                    psiElevate.FileName = Application.ExecutablePath;
                    psiElevate.Arguments = "--config";
                    psiElevate.Verb = "runas";
                    psiElevate.UseShellExecute = true;
                    Process.Start(psiElevate);
                }
                catch { }
                Load += delegate { Close(); };
                return;
            }

            EnsureTaskFolderReadAcl();
            BuildUI();

            pollTimer = new System.Windows.Forms.Timer();
            pollTimer.Interval = 800;
            pollTimer.Tick += delegate { PollRunningTasks(); };
            pollTimer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (pollTimer != null) { pollTimer.Stop(); pollTimer.Dispose(); pollTimer = null; }
            base.OnFormClosed(e);
        }

        private void PollRunningTasks()
        {
            if (IsDisposed || cards == null) return;
            List<TaskProgressInfo> active = TaskProgressInfo.FindAllActiveProgress(installDir);
            foreach (TaskConfigCard card in cards)
            {
                TaskProgressInfo matching = null;
                if (active != null)
                {
                    foreach (TaskProgressInfo inf in active)
                    {
                        if (TaskProgressInfo.MatchesTask(inf.Engine, card.TaskName) ||
                            TaskProgressInfo.MatchesTask(inf.Engine, card.ScriptName))
                        {
                            matching = inf;
                            break;
                        }
                    }
                }
                card.UpdateRuntimeState(matching);
            }
        }

        // ---------------- Construcción de la interfaz ----------------
        private Button NewSmallButton(string text)
        {
            Button b = new Button();
            b.Text = text;
            b.UseMnemonic = false;
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.Padding = new Padding(Sz(8), Sz(2), Sz(8), Sz(2));
            b.Margin = new Padding(0, 0, Sz(8), Sz(6));
            b.Font = ConfigTheme.F(8.75F);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = ConfigTheme.BorderStrong;
            b.FlatAppearance.MouseOverBackColor = ConfigTheme.PageBg;
            b.BackColor = Color.White;
            b.ForeColor = ConfigTheme.Text;
            b.Cursor = Cursors.Hand;
            return b;
        }

        private void BuildUI()
        {
            SuspendLayout();

            // Orden de docking: Fill primero; luego Bottom; luego Top (el último agregado queda más arriba).
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.AutoScroll = true;
            host.BackColor = ConfigTheme.PageBg;
            Controls.Add(host);

            // ---------------- Pie ----------------
            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = Sz(68);
            footer.BackColor = Color.White;
            footer.Padding = new Padding(Sz(28), Sz(12), Sz(28), Sz(12));
            footer.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(ConfigTheme.Border))
                    e.Graphics.DrawLine(p, 0, 0, footer.Width, 0);
            };
            Controls.Add(footer);

            Panel pnlInfo = new Panel();
            pnlInfo.Dock = DockStyle.Fill;
            pnlInfo.BackColor = Color.White;
            footer.Controls.Add(pnlInfo);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Right;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.AutoSize = true;
            buttons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttons.BackColor = Color.White;
            footer.Controls.Add(buttons);

            btnClose = new Button();
            btnClose.Text = Loc.T("Cerrar", "Close");
            btnClose.Size = new Size(Sz(96), Sz(40));
            btnClose.Margin = new Padding(Sz(10), 0, 0, 0);
            btnClose.Font = ConfigTheme.F(9.5F);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 1;
            btnClose.FlatAppearance.BorderColor = ConfigTheme.BorderStrong;
            btnClose.FlatAppearance.MouseOverBackColor = ConfigTheme.PageBg;
            btnClose.BackColor = Color.White;
            btnClose.ForeColor = ConfigTheme.Text;
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += delegate { Close(); };
            buttons.Controls.Add(btnClose);
            CancelButton = btnClose;

            btnApply = new Button();
            btnApply.Text = Loc.T("Guardar cambios", "Save changes");
            btnApply.Size = new Size(Sz(150), Sz(40));
            btnApply.Margin = new Padding(0);
            btnApply.Font = ConfigTheme.F(9.5F, FontStyle.Bold);
            btnApply.FlatStyle = FlatStyle.Flat;
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.FlatAppearance.MouseOverBackColor = ConfigTheme.AccentHover;
            btnApply.ForeColor = Color.White;
            btnApply.Cursor = Cursors.Hand;
            btnApply.Click += delegate { ApplyChanges(); };
            buttons.Controls.Add(btnApply);

            lblElev = new CfgLabel("", 8.5F, FontStyle.Regular, ConfigTheme.TextMuted);
            lblElev.Dock = DockStyle.Fill;
            lblElev.AutoEllipsis = true;
            pnlInfo.Controls.Add(lblElev);

            lblPending = new CfgLabel("", 9.5F, FontStyle.Bold, ConfigTheme.TextMuted);
            lblPending.Dock = DockStyle.Top;
            lblPending.Height = Sz(24);
            lblPending.AutoEllipsis = true;
            pnlInfo.Controls.Add(lblPending);

            // ---------------- Cabecera de marca ----------------
            Panel accentLine = new Panel();
            accentLine.Dock = DockStyle.Top;
            accentLine.Height = Sz(3);
            accentLine.BackColor = TrayTheme.AccentOnDark;
            Controls.Add(accentLine);

            CfgStack header = new CfgStack();
            header.Dock = DockStyle.Top;
            header.Bordered = false;
            header.BackColor = TrayTheme.BgDark;
            header.Spacing = Sz(4);
            header.Padding = new Padding(Sz(28), Sz(18), Sz(28), Sz(18));
            header.Controls.Add(new CfgLabel("ISORA SENTINEL · CONFIGURACIÓN", 8F, FontStyle.Bold, TrayTheme.AccentOnDark));
            header.Controls.Add(new CfgLabel("Tareas programadas", 15F, FontStyle.Bold, Color.White));
            header.Controls.Add(new CfgLabel(
                "Active, desactive o cambie el horario de cada rutina. Los cambios se guardan en el Programador de tareas de Windows y se ejecutan con la cuenta SYSTEM.",
                9.25F, FontStyle.Regular, ConfigTheme.OnHeaderMuted));
            Controls.Add(header);

            // ---------------- Contenido ----------------
            CfgStack stack = new CfgStack();
            stack.Bordered = false;
            stack.BackColor = ConfigTheme.PageBg;
            stack.Spacing = Sz(12);
            stack.Padding = new Padding(0, Sz(20), 0, Sz(24));
            host.Controls.Add(stack);

            host.Resize += delegate
            {
                int cw = Math.Min(host.ClientSize.Width - Sz(48), Sz(880));
                if (cw < 50) return;
                stack.Width = cw;
                stack.Left = (host.ClientSize.Width - cw) / 2;
            };
            stack.SizeChanged += delegate { host.AutoScrollMinSize = new Size(0, stack.Height); };

            // Barra de herramientas
            CfgLabel capRoutines = new CfgLabel("RUTINAS PROGRAMADAS", 8.5F, FontStyle.Bold, ConfigTheme.Accent);
            stack.Controls.Add(capRoutines);

            FlowBar bar = new FlowBar();
            Button btnAll = NewSmallButton("Activar todas");
            btnAll.Click += delegate { foreach (TaskConfigCard c in cards) if (c.Enabled) c.Active = true; };
            Button btnNone = NewSmallButton("Desactivar todas");
            btnNone.Click += delegate { foreach (TaskConfigCard c in cards) if (c.Enabled) c.Active = false; };
            Button btnReload = NewSmallButton("Recargar estado");
            btnReload.Click += delegate { ReloadWithConfirm(); };
            bar.Controls.Add(btnAll);
            bar.Controls.Add(btnNone);
            bar.Controls.Add(btnReload);
            bar.Tag = -4;
            stack.Controls.Add(bar);

            // Rutinas generadas dinámicamente desde el catálogo centralizado (SentinelCatalog.AllModules)
            foreach (SentinelModuleDefinition mod in SentinelCatalog.AllModules)
            {
                AddCard(stack, mod.TaskShortName, mod.ScriptFileName,
                    mod.TitleEs, mod.IsoBadge, mod.DescriptionEs,
                    mod.DefaultFrequency, mod.DefaultDayIndex, 0, mod.DefaultTime);
            }

            // Opciones generales
            CfgStack general = new CfgStack();
            general.Padding = new Padding(Sz(20), Sz(16), Sz(20), Sz(6));
            general.Spacing = Sz(2);
            general.Tag = Sz(4);
            general.Controls.Add(new CfgLabel("OPCIONES GENERALES", 8.5F, FontStyle.Bold, ConfigTheme.Accent));

            toggleAutoStart = new ToggleSwitch(dpi);
            autoStartBaseline = IsAutoStartEnabled();
            toggleAutoStart.Checked = autoStartBaseline;
            toggleAutoStart.CheckedChanged += delegate { UpdatePending(); };
            ToggleRow rowAuto = new ToggleRow(
                "Iniciar el monitor al iniciar sesión",
                "Muestra el icono de Isora Sentinel en el área de notificaciones al entrar a Windows.",
                toggleAutoStart, null, dpi);
            rowAuto.Tag = Sz(6);
            general.Controls.Add(rowAuto);

            Button btnSetup = NewSmallButton("Abrir asistente…");
            btnSetup.Margin = new Padding(0);
            btnSetup.Click += delegate { if (openSetupAction != null) openSetupAction(); };
            ToggleRow rowSetup = new ToggleRow(
                "Asistente de instalación",
                "Cambie la carpeta de instalación, las políticas de integridad o reinstale la suite.",
                null, btnSetup, dpi);
            rowSetup.ShowDivider = false;
            general.Controls.Add(rowSetup);
            stack.Controls.Add(general);

            ResumeLayout(true);
            UpdatePending();
        }

        private void AddCard(CfgStack stack, string tName, string sName, string title, string cId, string desc,
            string defFreq, int defDay, int defMonth, string defTime)
        {
            TaskConfigCard card = new TaskConfigCard(tName, sName, title, cId, desc, defFreq, defDay, defMonth, defTime, dpi,
                delegate(string tN, int pid, string tTitle) { HandleCancelTask(tN, pid, tTitle); });
            card.Changed += delegate { UpdatePending(); };
            cards.Add(card);
            stack.Controls.Add(card);
        }

        private void HandleCancelTask(string taskName, int pid, string titleText)
        {
            DialogResult dr = MessageBox.Show(this,
                "¿Desea cancelar y finalizar de inmediato la ejecución activa de \"" + titleText + "\" (PID: " + pid + ")?\n\n" +
                "Esta operación requiere confirmación de permisos de administrador (UAC) para finalizar el árbol de procesos del sistema de forma segura.",
                "Isora Sentinel · Cancelar Tarea en Ejecución",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (dr != DialogResult.Yes) return;

            string cmd = TaskSchedulerBridge.BuildSafeCancelPowerShellScript(taskName, null);

            runPsAction(cmd, true, delegate(int code)
            {
                if (code < 0) return;
                TaskProgressInfo.SuppressCancelledEngine(taskName, 15);
                PollRunningTasks();
            });
        }

        // ---------------- Estado pendiente ----------------
        private int PendingTasks()
        {
            int n = 0;
            foreach (TaskConfigCard c in cards) if (c.Dirty) n++;
            return n;
        }

        private int PendingCount()
        {
            int n = PendingTasks();
            if (toggleAutoStart != null && toggleAutoStart.Checked != autoStartBaseline) n++;
            return n;
        }

        private void UpdatePending()
        {
            if (lblPending == null) return;
            int n = PendingCount();
            int active = 0;
            foreach (TaskConfigCard c in cards) if (c.Active) active++;

            string left = (n == 0) ? "Sin cambios pendientes" : (n + (n == 1 ? " cambio sin guardar" : " cambios sin guardar"));
            lblPending.Text = left;
            lblPending.ForeColor = (n > 0) ? ConfigTheme.Warning : ConfigTheme.TextMuted;

            bool needElev = PendingTasks() > 0 && !IsAdministrator();
            lblElev.Text = needElev
                ? "Windows pedirá confirmación de administrador (UAC) al guardar."
                : active + " de " + cards.Count + " rutinas activas";

            bool can = (n > 0) && !applying;
            btnApply.Enabled = can;
            btnApply.BackColor = can ? ConfigTheme.Accent : ConfigTheme.NeutralBg;
            btnApply.ForeColor = can ? Color.White : ConfigTheme.TextMuted;
        }

        // ---------------- Lectura del estado actual ----------------
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LoadCurrentStatesAsync();
        }

        private void ReloadWithConfirm()
        {
            if (PendingCount() > 0)
            {
                DialogResult r = MessageBox.Show(this,
                    "Hay cambios sin guardar. Si recarga el estado se descartarán.\n\n¿Desea continuar?",
                    "Isora Sentinel · Tareas programadas", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) return;
            }
            LoadCurrentStatesAsync();
        }

        public void LoadCurrentStatesAsync()
        {
            List<string> names = new List<string>();
            foreach (TaskConfigCard c in cards) { c.SetLoading(); names.Add(c.TaskName); }
            UpdatePending();

            ThreadPool.QueueUserWorkItem(delegate
            {
                Dictionary<string, TaskStateInfo> states = new Dictionary<string, TaskStateInfo>();
                foreach (string n in names) states[n] = QueryTask(n);

                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        if (IsDisposed) return;
                        foreach (TaskConfigCard c in cards)
                        {
                            TaskStateInfo st;
                            if (states.TryGetValue(c.TaskName, out st) && st.Exists) c.ApplyState(st);
                            else if (st != null && st.AccessDenied) c.SetAccessDenied();
                            else c.SetNotScheduled();
                        }
                        UpdatePending();
                    }));
                }
                catch { }
            });
        }

        /// <summary>
        /// Consulta la definición XML de una tarea programada usando decodificación inteligente
        /// (<see cref="SafeProcessRunner.ReadProcessOutputSmartEncoding"/>) para evitar corrupción
        /// de caracteres acentuados o UTF-16LE en Windows 11.
        /// </summary>
        private static TaskStateInfo QueryTask(string taskName)
        {
            TaskStateInfo info = new TaskStateInfo();
            string[] candidatePaths = new string[] {
                "\\Isora Sentinel\\" + taskName,
                "Isora Sentinel\\" + taskName,
                "\\Isora-Sentinel\\" + taskName,
                "Isora-Sentinel\\" + taskName,
                "\\" + taskName,
                taskName
            };

            foreach (string queryPath in candidatePaths)
            {
                try
                {
                    int exitCode;
                    string xml = SafeProcessRunner.ReadProcessOutputSmartEncoding(
                        "schtasks.exe",
                        "/Query /TN \"" + queryPath + "\" /XML",
                        6000,
                        out exitCode);

                    if (exitCode == 0 && !string.IsNullOrEmpty(xml))
                    {
                        info.Exists = true;
                        info.Enabled = true;

                        xml = xml.Replace("\0", "");
                        int lt = xml.IndexOf('<');
                        if (lt > 0) xml = xml.Substring(lt);

                        try { ParseTaskXml(xml, info); } catch { }
                        return info;
                    }
                }
                catch { }
            }
            return info;
        }

        private static void ParseTaskXml(string xml, TaskStateInfo info)
        {
            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = null;
            doc.LoadXml(xml);

            XmlNode argsNode = doc.SelectSingleNode("//*[local-name()='Arguments']");
            if (argsNode != null)
            {
                string argText = argsNode.InnerText ?? "";
                if (argText.IndexOf("-ScanType 2", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    argText.IndexOf("-AutoRepair", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    info.Mode = 1;
                }
                int idx = argText.IndexOf("-CustomArchiveDir", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    string rest = argText.Substring(idx + "-CustomArchiveDir".Length).Trim();
                    if (rest.StartsWith("\""))
                    {
                        int endQuote = rest.IndexOf('\"', 1);
                        if (endQuote > 1) info.CustomPath = rest.Substring(1, endQuote - 1);
                    }
                    else
                    {
                        string[] tokens = rest.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length > 0) info.CustomPath = tokens[0];
                    }
                }
            }

            XmlNode en = doc.SelectSingleNode("//*[local-name()='Settings']/*[local-name()='Enabled']");
            if (en != null && en.InnerText.Trim().Equals("false", StringComparison.OrdinalIgnoreCase))
                info.Enabled = false;

            XmlNode sb = doc.SelectSingleNode("//*[local-name()='StartBoundary']");
            if (sb != null)
            {
                string s = sb.InnerText.Trim();
                if (s.Length >= 16 && s[10] == 'T') info.Time = s.Substring(11, 5);
            }

            if (doc.SelectSingleNode("//*[local-name()='ScheduleByDay']") != null)
            {
                info.Freq = "Diario";
            }
            else if (doc.SelectSingleNode("//*[local-name()='ScheduleByWeek']") != null)
            {
                info.Freq = "Semanal";
                XmlNode d = doc.SelectSingleNode("//*[local-name()='DaysOfWeek']/*");
                if (d != null) info.Day = Array.IndexOf(DaysEn, d.LocalName);
            }
            else if (doc.SelectSingleNode("//*[local-name()='ScheduleByMonth']") != null)
            {
                XmlNode day = doc.SelectSingleNode("//*[local-name()='DaysOfMonth']/*[local-name()='Day']");
                int dn;
                if (day != null && int.TryParse(day.InnerText.Trim(), out dn)) info.Day = dn - 1;

                // schtasks /SC MONTHLY lista los 12 meses: un solo mes indica programación anual.
                XmlNodeList months = doc.SelectNodes("//*[local-name()='Months']/*");
                if (months != null && months.Count == 1)
                {
                    info.Freq = "Anual";
                    info.Month = Array.IndexOf(MonthsEn, months[0].LocalName);
                }
                else
                {
                    info.Freq = "Mensual";
                }
            }
        }

        // ---------------- Guardado ----------------
        private string ResolveEngine(string script)
        {
            return SentinelPaths.ResolveEngineScript(installDir, script);
        }

        private void ApplyChanges()
        {
            // 1) Inicio automático: se escribe en el proceso actual (no elevado), en el HKCU del usuario real.
            if (toggleAutoStart.Checked != autoStartBaseline)
            {
                if (SetAutoStart(toggleAutoStart.Checked)) autoStartBaseline = toggleAutoStart.Checked;
                else toggleAutoStart.Checked = autoStartBaseline;
            }

            // 2) Tareas modificadas
            StringBuilder sb = new StringBuilder("$errs = 0; ");
            List<string> missing = new List<string>();
            int dirtyTasks = 0;

            foreach (TaskConfigCard c in cards)
            {
                if (!c.Dirty) continue;
                string script = ResolveEngine(c.ScriptName);
                if (c.Active && !File.Exists(script)) { missing.Add(c.ScriptName); continue; }
                string cmd = c.BuildCommand(script);
                if (cmd.Length > 0) { sb.Append(cmd); dirtyTasks++; }
            }
            sb.Append("& icacls.exe \\\"$env:windir\\System32\\Tasks\\Isora Sentinel\\\" /grant \\\"*S-1-5-32-545:(OI)(CI)RX\\\" /Q 2>`$null; exit $errs");

            if (missing.Count > 0)
            {
                MessageBox.Show(this,
                    "No se encontraron estos archivos en la carpeta de instalación:\n\n  •  " + string.Join("\n  •  ", missing.ToArray()) +
                    "\n\nRepare la instalación con el asistente y vuelva a intentarlo.",
                    "Isora Sentinel · Archivos faltantes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                UpdatePending();
                return;
            }

            if (dirtyTasks == 0)
            {
                UpdatePending();
                return;
            }

            applying = true;
            btnApply.Text = "Aplicando…";
            UpdatePending();

            runPsAction(sb.ToString(), !IsAdministrator(), delegate(int code)
            {
                if (IsDisposed) return;
                applying = false;
                btnApply.Text = Loc.T("Guardar cambios", "Save changes");

                if (code == 0)
                {
                    LoadCurrentStatesAsync();
                    MessageBox.Show(this, "Los cambios se aplicaron correctamente.",
                        "Isora Sentinel · Tareas programadas", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (code < 0)
                {
                    UpdatePending();
                    MessageBox.Show(this, "No se aplicaron cambios: la solicitud de permisos de administrador se canceló o no pudo iniciarse.",
                        "Isora Sentinel · Tareas programadas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    LoadCurrentStatesAsync();
                    MessageBox.Show(this, "No se pudieron aplicar " + code + " cambio(s). Revise el estado de cada rutina e inténtelo de nuevo.",
                        "Isora Sentinel · Tareas programadas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            });
        }

        // ---------------- Inicio automático (HKCU / HKLM / HKEY_USERS) ----------------
        private bool IsAutoStartEnabled()
        {
            return CrossAccountEnvironment.IsAutoStartEnabled();
        }

        private bool SetAutoStart(bool enable)
        {
            string err;
            bool ok = CrossAccountEnvironment.SetAutoStart(enable, Application.ExecutablePath, out err);
            if (!ok && !string.IsNullOrEmpty(err))
            {
                MessageBox.Show(this, "No se pudo cambiar el inicio automático:\n" + err,
                    "Isora Sentinel · Tareas programadas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return ok;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!applying && e.CloseReason == CloseReason.UserClosing && PendingCount() > 0)
            {
                DialogResult r = MessageBox.Show(this,
                    "Hay cambios sin guardar.\n\n¿Desea cerrar la ventana y descartarlos?",
                    "Isora Sentinel · Tareas programadas", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) e.Cancel = true;
            }
            base.OnFormClosing(e);
        }

        internal static string GetRegistryArchiveDir()
        {
            try
            {
                using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(SentinelCatalog.UninstallRegistrySubKey, false))
                {
                    if (rk != null)
                    {
                        object val = rk.GetValue("DownloadsArchiveDir");
                        if (val != null) return val.ToString();
                    }
                }
            }
            catch { }
            return null;
        }

        private static void EnsureTaskFolderReadAcl()
        {
            SentinelSecurity.ApplyTaskFolderReadAcl();
        }

        private static bool IsAdministrator()
        {
            return SentinelSecurity.IsAdministrator();
        }
    }
}
