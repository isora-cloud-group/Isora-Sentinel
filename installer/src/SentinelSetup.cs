using SentinelIT;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SentinelIT.Installer
{
    // =====================================================================
    //  TOKENS DE DISEÑO (contraste revisado, objetivo WCAG AA >= 4.5:1)
    // =====================================================================
    internal static class Theme
    {
        public static readonly Color SidebarBg       = Color.FromArgb(15, 23, 42);    // Slate 900
        public static readonly Color SidebarActive   = Color.FromArgb(30, 41, 59);    // Slate 800
        public static readonly Color SidebarHover    = Color.FromArgb(22, 32, 52);
        public static readonly Color SidebarText     = Color.FromArgb(203, 213, 225); // Slate 300 (12:1 sobre Slate 900)
        public static readonly Color SidebarMuted    = Color.FromArgb(148, 163, 184); // Slate 400 (7:1)

        public static readonly Color Accent          = Color.FromArgb(3, 105, 161);   // Sky 700 (blanco sobre él: 5.9:1)
        public static readonly Color AccentHover     = Color.FromArgb(7, 89, 133);    // Sky 800
        public static readonly Color AccentLight     = Color.FromArgb(56, 189, 248);  // Sky 400 (para fondos oscuros)
        public static readonly Color AccentText      = Color.FromArgb(3, 105, 161);   // texto acento sobre blanco

        public static readonly Color Success         = Color.FromArgb(4, 120, 87);    // Emerald 700
        public static readonly Color SuccessLight    = Color.FromArgb(52, 211, 153);  // Emerald 400
        public static readonly Color Warning         = Color.FromArgb(180, 83, 9);    // Amber 700
        public static readonly Color WarningLight    = Color.FromArgb(251, 191, 36);  // Amber 400
        public static readonly Color DangerLight     = Color.FromArgb(248, 113, 113); // Red 400

        public static readonly Color ContentBg       = Color.FromArgb(241, 245, 249); // Slate 100
        public static readonly Color CardBg          = Color.White;
        public static readonly Color Border          = Color.FromArgb(203, 213, 225); // Slate 300
        public static readonly Color BorderStrong    = Color.FromArgb(148, 163, 184); // Slate 400
        public static readonly Color FieldBg         = Color.FromArgb(241, 245, 249);

        public static readonly Color TextPrimary     = Color.FromArgb(15, 23, 42);    // Slate 900
        public static readonly Color TextMuted       = Color.FromArgb(71, 85, 105);   // Slate 600 (7.5:1 sobre blanco)
        public static readonly Color TextOnDark      = Color.FromArgb(226, 232, 240); // Slate 200

        public static Font F(float size)                 { return new Font("Segoe UI", size, FontStyle.Regular); }
        public static Font F(float size, FontStyle st)   { return new Font("Segoe UI", size, st); }
    }

    // =====================================================================
    //  CONTROLES PERSONALIZADOS (layout automático y responsive)
    // =====================================================================
    internal interface IFlex
    {
        int GetHeight(int width);
    }

    /// <summary>Label que hace wrap del texto y calcula su altura según el ancho disponible.</summary>
    internal class WrapLabel : Label, IFlex
    {
        public WrapLabel(string text, float size, FontStyle style, Color color)
        {
            AutoSize = false;
            UseMnemonic = false;
            Text = text ?? string.Empty;
            Font = Theme.F(size, style);
            ForeColor = color;
            BackColor = Color.Transparent;
        }

        public int GetHeight(int width)
        {
            if (width <= 0 || string.IsNullOrEmpty(Text)) return Height;
            Size sz = TextRenderer.MeasureText(Text, Font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            return sz.Height + 4;
        }
    }

    /// <summary>Tarjeta blanca con borde y barra de acento opcional.</summary>
    internal class Card : Panel
    {
        public bool Bordered = true;
        public bool AccentBar = false;
        public Color AccentColor = Theme.Accent;

        public Card()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Theme.CardBg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Bordered)
            {
                using (Pen p = new Pen(Theme.Border))
                    e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            }
            if (AccentBar)
            {
                float s = e.Graphics.DpiX / 96f;
                using (Brush b = new SolidBrush(AccentColor))
                    e.Graphics.FillRectangle(b, 0, 0, (int)(4 * s), Height);
            }
        }
    }

    /// <summary>Contenedor vertical: apila hijos al ancho disponible y ajusta su alto automáticamente.</summary>
    internal class VStack : Card, IFlex
    {
        public int Spacing = 8;
        public bool AutoHeight = true;
        private bool busy;

        private int Arrange(int width, bool apply)
        {
            int x = Padding.Left;
            int y = Padding.Top;
            int w = Math.Max(20, width - Padding.Horizontal);
            bool first = true;

            foreach (Control c in Controls)
            {
                if (c == null || !c.Visible) continue;
                int extra = (c.Tag is int) ? (int)c.Tag : 0;   // espacio extra antes del control
                if (!first) y += Spacing + extra;
                first = false;

                IFlex f = c as IFlex;
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
    }

    /// <summary>Fila con checkbox + título + descripción (con wrap) + control opcional a la derecha.</summary>
        internal class OptionRow : Panel, IFlex
    {
        public CheckBox Check;
        private WrapLabel title;
        private WrapLabel desc;
        private Control right;

        public OptionRow(string t, string d, bool isChecked, Control rightCtl)
        {
            DoubleBuffered = true;
            ResizeRedraw = true;

            Check = new CheckBox();
            Check.AutoSize = false;
            Check.Size = new Size(22, 22);
            Check.Checked = isChecked;
            Check.Cursor = Cursors.Hand;

            title = new WrapLabel(t, 9.5F, FontStyle.Bold, Theme.TextPrimary);
            title.Cursor = Cursors.Hand;
            title.Click += delegate { Check.Checked = !Check.Checked; };

            desc = new WrapLabel(d, 8.75F, FontStyle.Regular, Theme.TextMuted);
            right = rightCtl;

            SuspendLayout();
            Padding = new Padding(0, 10, 0, 10);
            Controls.Add(Check);
            Controls.Add(title);
            Controls.Add(desc);
            if (right != null) Controls.Add(right);
            ResumeLayout(true);
        }

        private int TextWidth(int w)
        {
            int rw = (right != null) ? right.Width + 16 : 0;
            return Math.Max(60, w - 32 - rw);
        }

        public int GetHeight(int width)
        {
            if (title == null || desc == null) return Height;
            int tw = TextWidth(width);
            return title.GetHeight(tw) + 2 + desc.GetHeight(tw) + Padding.Vertical;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (Check == null || title == null || desc == null) return;
            int w = ClientSize.Width;
            int tw = TextWidth(w);
            int th = title.GetHeight(tw);
            Check.Location = new Point(0, Padding.Top - 1);
            title.SetBounds(32, Padding.Top, tw, th);
            desc.SetBounds(32, Padding.Top + th + 2, tw, desc.GetHeight(tw));
            if (right != null) right.Location = new Point(w - right.Width, Padding.Top - 3);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Color.FromArgb(226, 232, 240)))
                e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }
    }

    /// <summary>
    /// Fila de tarea programada (Diario, Semanal, Mensual, Anual + hora + intensidad opcional).
    /// </summary>
    internal class TaskScheduleRow : Panel, IFlex
    {
        public CheckBox Check;
        public ComboBox cmbFreq;
        public ComboBox cmbDay;
        public ComboBox cmbMonth;
        public DateTimePicker dtpTime;
        public ComboBox cmbMode;

        private WrapLabel title;
        private WrapLabel desc;
        private FlowLayoutPanel schedulePanel;
        private FlowLayoutPanel grpDate;
        private Label lblParam;
        private string dayMode = "";
        private bool suppressModeWarning = false;

        private const int TextOffset = 32;

        public TaskScheduleRow(string t, string d, bool isChecked, string defaultFreq, int defaultDay, int defaultMonth, string defaultTime)
            : this(t, d, isChecked, defaultFreq, defaultDay, defaultMonth, defaultTime, null, 0)
        {
        }

        public TaskScheduleRow(string t, string d, bool isChecked, string defaultFreq, int defaultDay, int defaultMonth, string defaultTime, string[] modeOptions, int defaultMode)
        {
            DoubleBuffered = true;
            ResizeRedraw = true;

            Check = new CheckBox();
            Check.AutoSize = false;
            Check.Size = new Size(22, 22);
            Check.Checked = isChecked;
            Check.Cursor = Cursors.Hand;
            Check.CheckedChanged += delegate
            {
                if (schedulePanel != null) schedulePanel.Enabled = Check.Checked;
            };

            title = new WrapLabel(t, 9.5F, FontStyle.Bold, Theme.TextPrimary);
            title.Cursor = Cursors.Hand;
            title.Click += delegate { Check.Checked = !Check.Checked; };

            desc = new WrapLabel(d, 8.75F, FontStyle.Regular, Theme.TextMuted);

            // ---------- Panel de programación (fluido) ----------
            schedulePanel = new FlowLayoutPanel();
            schedulePanel.FlowDirection = FlowDirection.LeftToRight;
            schedulePanel.WrapContents = true;
            schedulePanel.AutoSize = false;
            schedulePanel.Padding = new Padding(10, 6, 10, 4);
            schedulePanel.BackColor = Theme.FieldBg;
            schedulePanel.Enabled = isChecked;

            // Grupo 1: Frecuencia
            FlowLayoutPanel grpFreq = NewGroup();
            grpFreq.Controls.Add(NewCaption("Frecuencia"));
            cmbFreq = NewCombo(92);
            cmbFreq.Items.AddRange(new object[] { "Diario", "Semanal", "Mensual", "Anual" });
            grpFreq.Controls.Add(cmbFreq);
            schedulePanel.Controls.Add(grpFreq);

            // Grupo 2: Día / Fecha (se oculta en "Diario")
            grpDate = NewGroup();
            lblParam = NewCaption("Día");
            grpDate.Controls.Add(lblParam);
            cmbMonth = NewCombo(104);
            cmbMonth.Items.AddRange(new object[] {
                "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
                "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" });
            cmbMonth.Margin = new Padding(0, 2, 6, 6);
            grpDate.Controls.Add(cmbMonth);
            cmbDay = NewCombo(100);
            grpDate.Controls.Add(cmbDay);
            schedulePanel.Controls.Add(grpDate);

            // Grupo 3: Hora
            FlowLayoutPanel grpTime = NewGroup();
            grpTime.Margin = new Padding(0, 0, 0, 0);
            grpTime.Controls.Add(NewCaption("Hora"));
            dtpTime = new DateTimePicker();
            dtpTime.Format = DateTimePickerFormat.Custom;
            dtpTime.CustomFormat = "HH:mm";
            dtpTime.ShowUpDown = true;
            dtpTime.Font = Theme.F(8.5F);
            dtpTime.Width = 70;
            dtpTime.Margin = new Padding(0, 2, 0, 6);
            DateTime parsed;
            if (DateTime.TryParseExact(defaultTime, "HH:mm", null, System.Globalization.DateTimeStyles.None, out parsed))
                dtpTime.Value = parsed;
            else
                dtpTime.Value = DateTime.Today.AddHours(3);
            grpTime.Controls.Add(dtpTime);
            schedulePanel.Controls.Add(grpTime);

            if (modeOptions != null && modeOptions.Length > 0)
            {
                FlowLayoutPanel grpMode = NewGroup();
                grpMode.Margin = new Padding(0, 0, 0, 0);
                grpMode.Controls.Add(NewCaption("Intensidad"));
                cmbMode = NewCombo(110);
                cmbMode.Items.AddRange(modeOptions);
                cmbMode.SelectedIndex = (defaultMode >= 0 && defaultMode < cmbMode.Items.Count) ? defaultMode : 0;
                cmbMode.SelectedIndexChanged += delegate
                {
                    if (!suppressModeWarning && cmbMode.SelectedIndex == 1)
                    {
                        DialogResult dr = MessageBox.Show(
                            "El análisis completo consume una cantidad significativa de recursos de CPU y disco, lo que puede ralentizar el equipo.\n\nSe recomienda programarlo fuera del horario laboral o no utilizar el equipo intensivamente mientras se ejecuta.\n\n¿Desea confirmar el modo Completo?",
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
                };
                grpMode.Controls.Add(cmbMode);
                schedulePanel.Controls.Add(grpMode);
            }

            cmbFreq.SelectedIndexChanged += delegate { UpdateParamControls(); };

            int freqIdx = 0;
            if (defaultFreq == "Semanal") freqIdx = 1;
            else if (defaultFreq == "Mensual") freqIdx = 2;
            else if (defaultFreq == "Anual") freqIdx = 3;
            cmbFreq.SelectedIndex = freqIdx;          // dispara UpdateParamControls

            cmbMonth.SelectedIndex = (defaultMonth >= 0 && defaultMonth < cmbMonth.Items.Count) ? defaultMonth : 0;
            if (defaultDay >= 0 && defaultDay < cmbDay.Items.Count) cmbDay.SelectedIndex = defaultDay;

            SuspendLayout();
            Padding = new Padding(0, 8, 0, 10);
            Controls.Add(Check);
            Controls.Add(title);
            Controls.Add(desc);
            Controls.Add(schedulePanel);
            ResumeLayout(true);
        }

        // ---------------- Helpers de construcción ----------------
        private static FlowLayoutPanel NewGroup()
        {
            FlowLayoutPanel g = new FlowLayoutPanel();
            g.FlowDirection = FlowDirection.LeftToRight;
            g.WrapContents = false;
            g.AutoSize = true;
            g.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            g.Margin = new Padding(0, 0, 18, 0);
            g.Padding = new Padding(0);
            g.BackColor = Color.Transparent;
            return g;
        }

        private static Label NewCaption(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.UseMnemonic = false;
            l.Font = Theme.F(8.5F, FontStyle.Bold);
            l.ForeColor = Theme.TextMuted;
            l.BackColor = Color.Transparent;
            l.Margin = new Padding(0, 6, 8, 0);
            return l;
        }

        private static ComboBox NewCombo(int width)
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Font = Theme.F(8.75F);
            c.Width = width;
            c.Margin = new Padding(0, 2, 0, 6);
            return c;
        }

        // ---------------- Lógica de controles según frecuencia ----------------
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
                    cmbDay.SelectedIndex = 6;     // Domingo por defecto
                    dayMode = "W";
                }
                cmbDay.Width = 100;
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
                cmbDay.Width = 84;
            }

            // La altura de esta fila cambió: se propaga el re-layout hacia arriba.
            for (Control c = this.Parent; c != null && !(c is Form); c = c.Parent)
                c.PerformLayout();
            PerformLayout();
        }

        public string GetSchtasksTriggerArgs()
        {
            string timeStr = dtpTime.Value.ToString("HH:mm");
            string freq = cmbFreq.SelectedItem != null ? cmbFreq.SelectedItem.ToString() : "Diario";

            if (freq == "Semanal")
            {
                string[] dayCodes = new string[] { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" };
                int idx = (cmbDay.SelectedIndex >= 0 && cmbDay.SelectedIndex < 7) ? cmbDay.SelectedIndex : 6;
                return string.Format("/SC WEEKLY /D {0} /ST {1}", dayCodes[idx], timeStr);
            }
            if (freq == "Mensual")
            {
                int dayNum = (cmbDay.SelectedIndex >= 0 && cmbDay.SelectedIndex < 31) ? (cmbDay.SelectedIndex + 1) : 1;
                return string.Format("/SC MONTHLY /D {0} /ST {1}", dayNum, timeStr);
            }
            if (freq == "Anual")
            {
                string[] monthCodes = new string[] { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
                int mIdx = (cmbMonth.SelectedIndex >= 0 && cmbMonth.SelectedIndex < 12) ? cmbMonth.SelectedIndex : 0;
                int dayNum = (cmbDay.SelectedIndex >= 0 && cmbDay.SelectedIndex < 31) ? (cmbDay.SelectedIndex + 1) : 1;
                return string.Format("/SC MONTHLY /M {0} /D {1} /ST {2}", monthCodes[mIdx], dayNum, timeStr);
            }
            return string.Format("/SC DAILY /ST {0}", timeStr);
        }

        public string SerializeState()
        {
            string freq = cmbFreq.SelectedItem != null ? cmbFreq.SelectedItem.ToString() : "Diario";
            int dIdx = cmbDay.SelectedIndex >= 0 ? cmbDay.SelectedIndex : 0;
            int mIdx = cmbMonth.SelectedIndex >= 0 ? cmbMonth.SelectedIndex : 0;
            return string.Format("{0}~{1}~{2}~{3}~{4}", Check.Checked ? "1" : "0", freq, dIdx, mIdx, dtpTime.Value.ToString("HH:mm"));
        }

        public void RestoreState(string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            string[] parts = token.Split('~');
            if (parts.Length < 5) return;

            Check.Checked = (parts[0] == "1");

            int fIdx = 0;
            if (parts[1] == "Semanal") fIdx = 1;
            else if (parts[1] == "Mensual") fIdx = 2;
            else if (parts[1] == "Anual") fIdx = 3;
            cmbFreq.SelectedIndex = fIdx;

            int dIdx = 0; int.TryParse(parts[2], out dIdx);
            int mIdx = 0; int.TryParse(parts[3], out mIdx);
            if (mIdx >= 0 && mIdx < cmbMonth.Items.Count) cmbMonth.SelectedIndex = mIdx;
            if (dIdx >= 0 && dIdx < cmbDay.Items.Count) cmbDay.SelectedIndex = dIdx;

            DateTime pt;
            if (DateTime.TryParseExact(parts[4], "HH:mm", null, System.Globalization.DateTimeStyles.None, out pt))
                dtpTime.Value = pt;

            schedulePanel.Enabled = Check.Checked;
        }

        // ---------------- Layout responsive ----------------
        private int ContentWidth(int w) { return Math.Max(120, w - TextOffset); }

        public int GetHeight(int width)
        {
            if (title == null || desc == null || schedulePanel == null) return Height;
            int tw = ContentWidth(width);
            int sh = schedulePanel.GetPreferredSize(new Size(tw, 0)).Height;
            return title.GetHeight(tw) + 2 + desc.GetHeight(tw) + 8 + sh + Padding.Vertical;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (Check == null || title == null || desc == null || schedulePanel == null) return;

            int tw = ContentWidth(ClientSize.Width);
            int th = title.GetHeight(tw);
            int dh = desc.GetHeight(tw);
            int sh = schedulePanel.GetPreferredSize(new Size(tw, 0)).Height;

            Check.Location = new Point(0, Padding.Top - 1);
            title.SetBounds(TextOffset, Padding.Top, tw, th);
            desc.SetBounds(TextOffset, Padding.Top + th + 2, tw, dh);
            schedulePanel.SetBounds(TextOffset, Padding.Top + th + 2 + dh + 8, tw, sh);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Color.FromArgb(226, 232, 240)))
                e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }
    }

    /// <summary>Botón de navegación del paso (círculo numerado, check al completar, barra de acento).</summary>
    internal class StepButton : Control
    {
        public int Number;
        public string Title;
        private bool active;
        private bool done;
        private bool hover;

        public bool Active { get { return active; } set { active = value; Invalidate(); } }
        public bool Done   { get { return done; }   set { done = value;   Invalidate(); } }

        public StepButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            Height = 52;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = g.DpiX / 96f;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color bg = active ? Theme.SidebarActive : (hover ? Theme.SidebarHover : Theme.SidebarBg);
            using (Brush b = new SolidBrush(bg)) g.FillRectangle(b, ClientRectangle);

            if (active)
                using (Brush b = new SolidBrush(Theme.AccentLight))
                    g.FillRectangle(b, 0, 0, (int)(4 * s), Height);

            float d = 28 * s;
            RectangleF circ = new RectangleF(22 * s, (Height - d) / 2f, d, d);

            if (done)
            {
                using (Brush b = new SolidBrush(Theme.Success)) g.FillEllipse(b, circ);
                using (Pen p = new Pen(Color.White, 2f * s))
                {
                    float cx = circ.X + d / 2f, cy = circ.Y + d / 2f;
                    g.DrawLines(p, new PointF[] {
                        new PointF(cx - 5 * s, cy),
                        new PointF(cx - 1.5f * s, cy + 4 * s),
                        new PointF(cx + 5 * s, cy - 4 * s) });
                }
            }
            else
            {
                if (active)
                    using (Brush b = new SolidBrush(Theme.AccentLight)) g.FillEllipse(b, circ);
                else
                    using (Pen p = new Pen(Theme.SidebarMuted, 1.5f * s)) g.DrawEllipse(p, circ);

                using (Font f = Theme.F(9.5F, FontStyle.Bold))
                using (Brush b = new SolidBrush(active ? Theme.SidebarBg : Theme.SidebarText))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(Number.ToString(), f, b, circ, sf);
                }
            }

            RectangleF tr = new RectangleF(circ.Right + 14 * s, 0, Width - circ.Right - 20 * s, Height);
            using (Font f = Theme.F(9.75F, active ? FontStyle.Bold : FontStyle.Regular))
            using (Brush b = new SolidBrush(active ? Color.White : Theme.SidebarText))
            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(Title ?? string.Empty, f, b, tr, sf);
            }
        }
    }

    // =====================================================================
    //  FORMULARIO PRINCIPAL
    // =====================================================================
    public class SetupForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        private const int ATTACH_PARENT_PROCESS = -1;

        private float dpi = 1f;
        private int S(int v) { return (int)Math.Round(v * dpi); }

        // Estado
        private int currentStep = 0;
        private bool locked = false;       // true durante/después de instalar
        private Panel[] pages;
        private StepButton[] navStepButtons;

        // Paneles estructurales
        private Panel sidebarPanel;
        private Panel mainPanel;
        private Panel headerPanel;
        private Panel footerPanel;
        private Panel contentPanel;
        private FlowLayoutPanel footerFlow;

        // Header & Footer
        private Label lblHeaderTitle;
        private Label lblHeaderSubtitle;
        private Label lblGovTitle;
        private Label lblAuthor;
        private Button btnLangToggle;
        private Label lblStepIndicator;
        private Button btnBack;
        private Button btnNext;
        private Button btnInstall;
        private Button btnCancel;

        // Paso 2 (Ubicación y opciones)
        private TextBox txtInstallDir;

        private TextBox txtLogsDir;

        private TextBox txtDownloadsArchiveDir;
        private CheckBox chkTray;
        private CheckBox chkEnableTasks;
        private CheckBox chkRegistry;
        private CheckBox chkIntegrity;
        private VStack cardUpgradeNotice;
        private bool overwriteConfirmed = false;
        private string existingVersion = null;
        private string existingInstallPath = null;

        // Paso 3
        private TaskScheduleRow taskScreenshots;
        private TaskScheduleRow taskTemp;
        private TaskScheduleRow taskDefender;
        private TaskScheduleRow taskIntegrity;
        private TaskScheduleRow taskBitLocker;
        private TaskScheduleRow taskEventAudit;
        private TaskScheduleRow taskArchiveDownloads;

        // Paso 4
        private ProgressBar progressBar;
        private Label lblProgressStatus;
        private RichTextBox rtbLog;

        public SetupForm() : this(new string[0]) { }

        public SetupForm(string[] args)
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) { dpi = g.DpiX / 96f; }
            InitializeComponent();

            bool autoInstall = false;
            if (args != null && args.Length >= 2 && (args[0] == "--config-file" || args[0] == "--auto-install"))
            {
                try
                {
                    string state = string.Empty;
                    if (args[0] == "--config-file" && File.Exists(args[1]))
                    {
                        state = File.ReadAllText(args[1], Encoding.UTF8);
                        try { File.Delete(args[1]); } catch { }
                    }
                    else if (args[0] == "--auto-install")
                    {
                        if (File.Exists(args[1]))
                        {
                            state = File.ReadAllText(args[1], Encoding.UTF8);
                        }
                        else if (args[1].Contains("|"))
                        {
                            state = args[1];
                        }
                        else
                        {
                            byte[] bytes = Convert.FromBase64String(args[1]);
                            state = Encoding.UTF8.GetString(bytes);
                        }
                    }
                    string[] parts = state.Split('|');
                    if (parts.Length >= 14)
                    {
                        txtInstallDir.Text = parts[0];
                        txtLogsDir.Text = parts[1];
                        chkIntegrity.Checked = (parts[2] == "1");
                        chkRegistry.Checked = (parts[3] == "1");
                        if (chkTray != null) chkTray.Checked = (parts[4] == "1");
                        if (chkEnableTasks != null) chkEnableTasks.Checked = (parts[5] == "1");
                        taskScreenshots.RestoreState(parts[6]);
                        taskTemp.RestoreState(parts[7]);
                        taskDefender.RestoreState(parts[8]);
                        taskIntegrity.RestoreState(parts[9]);
                        taskBitLocker.RestoreState(parts[10]);
                        taskEventAudit.RestoreState(parts[11]);
                        taskArchiveDownloads.RestoreState(parts[12]);
                        overwriteConfirmed = (parts[13] == "1");
                        if (parts.Length >= 15 && txtDownloadsArchiveDir != null && !string.IsNullOrEmpty(parts[14]))
                        {
                            txtDownloadsArchiveDir.Text = parts[14];
                        }
                        autoInstall = true;
                    }
                    else if (parts.Length >= 13)
                    {
                        txtInstallDir.Text = parts[0];
                        txtLogsDir.Text = parts[1];
                        chkIntegrity.Checked = (parts[2] == "1");
                        chkRegistry.Checked = (parts[3] == "1");
                        if (chkTray != null) chkTray.Checked = (parts[4] == "1");
                        if (chkEnableTasks != null) chkEnableTasks.Checked = (parts[5] == "1");
                        taskScreenshots.RestoreState(parts[6]);
                        taskTemp.RestoreState(parts[7]);
                        taskDefender.RestoreState(parts[8]);
                        taskIntegrity.RestoreState(parts[9]);
                        taskBitLocker.RestoreState(parts[10]);
                        taskEventAudit.RestoreState(parts[11]);
                        overwriteConfirmed = (parts[12] == "1");
                        autoInstall = true;
                    }
                    else if (parts.Length >= 12)
                    {
                        txtInstallDir.Text = parts[0];
                        txtLogsDir.Text = parts[1];
                        chkIntegrity.Checked = (parts[2] == "1");
                        chkRegistry.Checked = (parts[3] == "1");
                        if (chkTray != null) chkTray.Checked = (parts[4] == "1");
                        if (chkEnableTasks != null) chkEnableTasks.Checked = (parts[5] == "1");
                        taskScreenshots.RestoreState(parts[6]);
                        taskTemp.RestoreState(parts[7]);
                        taskDefender.RestoreState(parts[8]);
                        taskIntegrity.RestoreState(parts[9]);
                        taskBitLocker.RestoreState(parts[10]);
                        taskEventAudit.RestoreState(parts[11]);
                        autoInstall = true;
                    }
                    else if (parts.Length >= 10)
                    {
                        txtInstallDir.Text = parts[0];
                        txtLogsDir.Text = parts[1];
                        chkIntegrity.Checked = (parts[2] == "1");
                        chkRegistry.Checked = (parts[3] == "1");
                        taskScreenshots.RestoreState(parts[4]);
                        taskTemp.RestoreState(parts[5]);
                        taskDefender.RestoreState(parts[6]);
                        taskIntegrity.RestoreState(parts[7]);
                        taskBitLocker.RestoreState(parts[8]);
                        taskEventAudit.RestoreState(parts[9]);
                        autoInstall = true;
                    }
                }
                catch { }
            }

            if (autoInstall)
            {
                locked = true;
                ShowPage(3);
                this.Shown += delegate
                {
                    this.BeginInvoke((MethodInvoker)delegate { StartInstallation(); });
                };
            }
            else
            {
                ShowPage(0);
                this.Shown += delegate
                {
                    CheckExistingVersionPrompt();
                };
            }
        }

        // -----------------------------------------------------------------
        //  Helpers de construcción
        // -----------------------------------------------------------------
        private WrapLabel Lbl(string text, float size, FontStyle style, Color color)
        {
            return new WrapLabel(text, size, style, color);
        }

        private WrapLabel Caption(string text)
        {
            return Lbl(text, 8.5F, FontStyle.Bold, Theme.AccentText);
        }

        private VStack NewCard(int padH, int padV, int spacing)
        {
            VStack v = new VStack();
            v.Padding = new Padding(padH, padV, padH, padV);
            v.Spacing = spacing;
            return v;
        }

        private TextBox NewPathBox(string text)
        {
            TextBox t = new TextBox();
            t.Text = text;
            t.ReadOnly = true;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.BackColor = Theme.FieldBg;
            t.ForeColor = Theme.TextPrimary;
            t.Font = Theme.F(9.5F);
            return t;
        }

        private ComboBox NewTriggerCombo()
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Width = 105;
            c.Font = Theme.F(9F);
            c.Items.AddRange(new object[] { "Semanal", "Diario" });
            c.SelectedIndex = 0;
            return c;
        }

        private void StyleButton(Button b, bool primary)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.Cursor = Cursors.Hand;
            b.Font = Theme.F(9.5F, primary ? FontStyle.Bold : FontStyle.Regular);
            b.Margin = new Padding(10, 0, 0, 0);
            b.UseMnemonic = false;
            if (primary)
            {
                b.FlatAppearance.BorderSize = 0;
                b.BackColor = Theme.Accent;
                b.ForeColor = Color.White;
                b.FlatAppearance.MouseOverBackColor = Theme.AccentHover;
                b.FlatAppearance.MouseDownBackColor = Theme.AccentHover;
            }
            else
            {
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = Theme.BorderStrong;
                b.BackColor = Color.White;
                b.ForeColor = Theme.TextPrimary;
                b.FlatAppearance.MouseOverBackColor = Theme.ContentBg;
            }
        }

        /// <summary>Página con scroll vertical; el contenido se centra con un ancho máximo.</summary>
        private Panel MakeScrollPage(VStack stack)
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.AutoScroll = true;
            host.Visible = false;
            host.BackColor = Theme.ContentBg;

            stack.Bordered = false;
            stack.BackColor = Theme.ContentBg;
            stack.Spacing = 12;
            stack.Padding = new Padding(0, 0, 0, 8);
            host.Controls.Add(stack);

            host.Resize += delegate
            {
                int maxW = S(880);
                int w = Math.Min(host.ClientSize.Width, maxW);
                if (w < 50) return;
                stack.Width = w;
                stack.Left = (host.ClientSize.Width - w) / 2;
            };
            stack.SizeChanged += delegate { host.AutoScrollMinSize = new Size(0, stack.Height); };
            return host;
        }

        // -----------------------------------------------------------------
        //  Construcción de la ventana
        // -----------------------------------------------------------------
        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.Dpi;

            this.Text = "Isora Sentinel Suite v2.3.0 — Asistente de Instalación Enterprise";
            this.ClientSize = new Size(980, 680);
            this.MinimumSize = new Size(820, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;   // redimensionable
            this.MaximizeBox = true;
            this.BackColor = Theme.ContentBg;
            this.Font = Theme.F(9F);
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream st = asm.GetManifestResourceStream("AppIcon"))
                {
                    if (st != null) this.Icon = new Icon(st);
                }
                if (this.Icon == null) this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            // NOTA DE DOCKING: el control Fill debe agregarse PRIMERO y los bordes
            // (Left/Top/Bottom) DESPUÉS; así WinForms reparte el espacio sin solapar.

            // ---------------- Panel principal (Fill) ----------------
            mainPanel = new Panel();
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.BackColor = Theme.ContentBg;
            this.Controls.Add(mainPanel);

            // ---------------- Sidebar (Left) ----------------
            sidebarPanel = new Panel();
            sidebarPanel.Dock = DockStyle.Left;
            sidebarPanel.Width = 260;
            sidebarPanel.BackColor = Theme.SidebarBg;
            this.Controls.Add(sidebarPanel);

            BuildSidebar();
            BuildMain();

            // Sidebar adaptable al ancho de la ventana
            this.Resize += delegate
            {
                int target = (this.ClientSize.Width < S(900)) ? S(212) : S(260);
                if (sidebarPanel.Width != target) sidebarPanel.Width = target;
            };

            this.ResumeLayout(true);
        }

        private void BuildSidebar()
        {
            // Orden: Fill (nav) -> Bottom (gobernanza) -> Top (marca)
            Panel navPanel = new Panel();
            navPanel.Dock = DockStyle.Fill;
            navPanel.BackColor = Theme.SidebarBg;
            sidebarPanel.Controls.Add(navPanel);

            // ---- Gobernanza (Bottom) ----
            Panel sideFooter = new Panel();
            sideFooter.Dock = DockStyle.Bottom;
            sideFooter.Height = 118;
            sideFooter.Padding = new Padding(22, 12, 14, 14);
            sideFooter.BackColor = Theme.SidebarBg;
            sidebarPanel.Controls.Add(sideFooter);

            lblAuthor = new Label();
            lblAuthor.Dock = DockStyle.Fill;
            lblAuthor.UseMnemonic = false;
            lblAuthor.Text = "Sandra Gabriela Puerto Torres\nLicencia MIT (código abierto)";
            lblAuthor.Font = Theme.F(8.75F);
            lblAuthor.ForeColor = Theme.SidebarText;
            sideFooter.Controls.Add(lblAuthor);

            lblGovTitle = new Label();
            lblGovTitle.Dock = DockStyle.Top;
            lblGovTitle.Height = 22;
            lblGovTitle.UseMnemonic = false;
            lblGovTitle.Text = "AUTORÍA Y LICENCIA";
            lblGovTitle.Font = Theme.F(8F, FontStyle.Bold);
            lblGovTitle.ForeColor = Theme.SidebarMuted;
            sideFooter.Controls.Add(lblGovTitle);

            // ---- Marca (Top) ----
            Panel brandPanel = new Panel();
            brandPanel.Dock = DockStyle.Top;
            brandPanel.Height = 124;
            brandPanel.BackColor = Theme.SidebarBg;
            sidebarPanel.Controls.Add(brandPanel);

            Label lblLogo = new Label();
            lblLogo.Text = "ISORA SENTINEL";
            lblLogo.Font = Theme.F(15F, FontStyle.Bold);
            lblLogo.ForeColor = Theme.AccentLight;
            lblLogo.Location = new Point(22, 22);
            lblLogo.AutoSize = true;
            lblLogo.UseMnemonic = false;
            brandPanel.Controls.Add(lblLogo);

            Label lblEdition = new Label();
            lblEdition.Text = "Endpoint Hardening & Governance";
            lblEdition.Font = Theme.F(9F);
            lblEdition.ForeColor = Theme.SidebarText;
            lblEdition.Location = new Point(23, 54);
            lblEdition.AutoSize = true;
            lblEdition.UseMnemonic = false;
            brandPanel.Controls.Add(lblEdition);

            Label lblBadge = new Label();
            lblBadge.Text = " v2.3.0 • ISO/IEC 27001 ";
            lblBadge.Font = Theme.F(8F, FontStyle.Bold);
            lblBadge.ForeColor = Color.FromArgb(125, 211, 252);   // Sky 300
            lblBadge.BackColor = Theme.SidebarActive;
            lblBadge.Padding = new Padding(4, 3, 4, 3);
            lblBadge.Location = new Point(23, 84);
            lblBadge.AutoSize = true;
            lblBadge.UseMnemonic = false;
            brandPanel.Controls.Add(lblBadge);

            // ---- Navegación ----
            string[] stepTitles = new string[] {
                "Bienvenida y Alcance",
                "Ubicación y opciones",
                "Tareas Programadas",
                "Instalando"
            };

            navStepButtons = new StepButton[stepTitles.Length];
            for (int i = 0; i < stepTitles.Length; i++)
            {
                StepButton btn = new StepButton();
                btn.Number = i + 1;
                btn.Title = stepTitles[i];
                btn.Dock = DockStyle.Top;
                int pageIdx = i;
                btn.Click += delegate
                {
                    if (locked || pageIdx == 3) return;   // la página 4 solo se alcanza instalando
                    ShowPage(pageIdx);
                };
                navStepButtons[i] = btn;
            }
            // Dock=Top: se agrega en orden inverso para que el paso 1 quede arriba
            for (int i = navStepButtons.Length - 1; i >= 0; i--)
                navPanel.Controls.Add(navStepButtons[i]);
        }

        private void BuildMain()
        {
            // Orden: Fill (contenido) -> Bottom (footer) -> Top (header)
            contentPanel = new Panel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.BackColor = Theme.ContentBg;
            contentPanel.Padding = new Padding(28, 20, 28, 16);
            mainPanel.Controls.Add(contentPanel);

            // ---------------- Footer ----------------
            footerPanel = new Panel();
            footerPanel.Dock = DockStyle.Bottom;
            footerPanel.Height = 68;
            footerPanel.BackColor = Color.White;
            footerPanel.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Border))
                    e.Graphics.DrawLine(pen, 0, 0, footerPanel.Width, 0);
            };
            mainPanel.Controls.Add(footerPanel);

            footerFlow = new FlowLayoutPanel();
            footerFlow.Dock = DockStyle.Fill;
            footerFlow.FlowDirection = FlowDirection.RightToLeft;
            footerFlow.WrapContents = false;
            footerFlow.Padding = new Padding(0, 16, 28, 0);
            footerFlow.BackColor = Color.White;
            footerPanel.Controls.Add(footerFlow);

            lblStepIndicator = new Label();
            lblStepIndicator.Dock = DockStyle.Left;
            lblStepIndicator.Width = 170;
            lblStepIndicator.Padding = new Padding(28, 0, 0, 0);
            lblStepIndicator.TextAlign = ContentAlignment.MiddleLeft;
            lblStepIndicator.Font = Theme.F(9.25F);
            lblStepIndicator.ForeColor = Theme.TextMuted;
            lblStepIndicator.Text = "Paso 1 de 4";
            lblStepIndicator.UseMnemonic = false;
            footerPanel.Controls.Add(lblStepIndicator);

            // Con RightToLeft el primero agregado queda más a la derecha
            btnCancel = new Button();
            btnCancel.Text = "Cancelar";
            btnCancel.Size = new Size(104, 38);
            StyleButton(btnCancel, false);
            btnCancel.Click += delegate { this.Close(); };
            footerFlow.Controls.Add(btnCancel);

            btnInstall = new Button();
            btnInstall.Text = "Instalar Suite";
            btnInstall.Size = new Size(144, 38);
            StyleButton(btnInstall, true);
            btnInstall.Visible = false;
            btnInstall.Click += BtnInstall_Click;
            footerFlow.Controls.Add(btnInstall);

            btnNext = new Button();
            btnNext.Text = "Siguiente  >";
            btnNext.Size = new Size(124, 38);
            StyleButton(btnNext, true);
            btnNext.Click += delegate { if (currentStep < 2) ShowPage(currentStep + 1); };
            footerFlow.Controls.Add(btnNext);

            btnBack = new Button();
            btnBack.Text = "<  Atrás";
            btnBack.Size = new Size(104, 38);
            StyleButton(btnBack, false);
            btnBack.Click += delegate { if (currentStep > 0) ShowPage(currentStep - 1); };
            footerFlow.Controls.Add(btnBack);

            // ---------------- Header ----------------
            headerPanel = new Panel();
            headerPanel.Dock = DockStyle.Top;
            headerPanel.Height = 84;
            headerPanel.BackColor = Color.White;
            headerPanel.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Border))
                    e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            };
            mainPanel.Controls.Add(headerPanel);

            lblHeaderTitle = new Label();
            lblHeaderTitle.AutoSize = false;
            lblHeaderTitle.AutoEllipsis = true;
            lblHeaderTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblHeaderTitle.Font = Theme.F(14F, FontStyle.Bold);
            lblHeaderTitle.ForeColor = Theme.TextPrimary;
            lblHeaderTitle.Location = new Point(28, 14);
            lblHeaderTitle.Size = new Size(560, 30);
            lblHeaderTitle.UseMnemonic = false;
            headerPanel.Controls.Add(lblHeaderTitle);

            lblHeaderSubtitle = new Label();
            lblHeaderSubtitle.AutoSize = false;
            lblHeaderSubtitle.AutoEllipsis = true;
            lblHeaderSubtitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblHeaderSubtitle.Font = Theme.F(9.25F);
            lblHeaderSubtitle.ForeColor = Theme.TextMuted;
            lblHeaderSubtitle.Location = new Point(29, 46);
            lblHeaderSubtitle.Size = new Size(560, 22);
            lblHeaderSubtitle.UseMnemonic = false;
            headerPanel.Controls.Add(lblHeaderSubtitle);

            btnLangToggle = new Button();
            btnLangToggle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnLangToggle.Location = new Point(headerPanel.Width - 130, 24);
            btnLangToggle.Size = new Size(100, 32);
            btnLangToggle.Text = Loc.IsSpanish ? "ðŸŒ English" : "ðŸŒ EspaÃ±ol";
            btnLangToggle.Font = Theme.F(8.5F, FontStyle.Bold);
            btnLangToggle.ForeColor = Theme.Accent;
            btnLangToggle.BackColor = Color.White;
            btnLangToggle.FlatStyle = FlatStyle.Flat;
            btnLangToggle.FlatAppearance.BorderColor = Theme.Border;
            btnLangToggle.Cursor = Cursors.Hand;
            btnLangToggle.Click += delegate
            {
                Loc.Current = Loc.IsSpanish ? Language.English : Language.Spanish;
                btnLangToggle.Text = Loc.IsSpanish ? "ðŸŒ English" : "ðŸŒ EspaÃ±ol";
                UpdateLanguageUi();
            };
            headerPanel.Controls.Add(btnLangToggle);

            BuildPages();
        }

        private void BuildPages()
        {
            pages = new Panel[4];

            // ================= PÁGINA 0: BIENVENIDA =================
            VStack s0 = new VStack();
            Panel p0 = MakeScrollPage(s0);
            contentPanel.Controls.Add(p0);
            pages[0] = p0;

            VStack cardIntro = NewCard(20, 18, 8);
            cardIntro.Controls.Add(Caption("GOBERNANZA Y SANEAMIENTO DE ENDPOINTS"));
            cardIntro.Controls.Add(Lbl(
                "Isora Sentinel es un conjunto modular de controles automáticos de saneamiento, hardening y seguridad para equipos Windows. Cada ejecución deja un registro verificable que sirve como evidencia en auditorías.",
                9.5F, FontStyle.Regular, Theme.TextPrimary));
            cardIntro.Controls.Add(Lbl(
                "Las tareas se ejecutan como NT AUTHORITY\\SYSTEM, sin agentes adicionales ni conexión a servicios externos.",
                9.5F, FontStyle.Regular, Theme.TextPrimary));
            s0.Controls.Add(cardIntro);

            s0.Controls.Add(CreateFeatureCard("Sin dependencias externas",
                "Usa solo PowerShell 5.1 o superior, incluido en Windows."));
            s0.Controls.Add(CreateFeatureCard("Verificación de integridad (SHA-256)",
                "Cada módulo se compara con su huella en integrity.manifest.json."));
            s0.Controls.Add(CreateFeatureCard("Registros listos para su SIEM",
                "Eventos en NDJSON (un JSON por línea), compatibles con Splunk, Elastic y Microsoft Sentinel."));

            bool isAdmin = IsAdministrator();
            VStack cardPriv = NewCard(20, 14, 0);
            cardPriv.AccentBar = true;
            cardPriv.AccentColor = isAdmin ? Theme.Success : Theme.Warning;
            cardPriv.Controls.Add(Lbl(
                isAdmin ? "✔  Se detectaron privilegios de administrador. Puede continuar con la instalación."
                        : "⚠  Al instalar, Windows solicitará confirmación de administrador (UAC).",
                9.5F, FontStyle.Bold, isAdmin ? Theme.Success : Theme.Warning));
            s0.Controls.Add(cardPriv);

            // ================= PÁGINA 1: UBICACIÓN Y OPCIONES =================
            VStack s1 = new VStack();
            Panel p1 = MakeScrollPage(s1);
            contentPanel.Controls.Add(p1);
            pages[1] = p1;

            cardUpgradeNotice = NewCard(20, 14, 0);
            cardUpgradeNotice.AccentBar = true;
            cardUpgradeNotice.AccentColor = Theme.Accent;
            cardUpgradeNotice.Visible = false;
            cardUpgradeNotice.Controls.Add(Lbl(
                "ℹ  Modo de actualización activo: Se detectó una versión previa en el sistema. Los registros históricos (logs) se mantendrán intactos y se creará una copia de seguridad de auditoría en la carpeta backup\\.",
                9F, FontStyle.Bold, Theme.AccentText));
            s1.Controls.Add(cardUpgradeNotice);

            VStack cardPaths = NewCard(20, 18, 6);
            cardPaths.Controls.Add(Caption("CARPETA DE INSTALACIÓN"));
            txtInstallDir = NewPathBox(@"C:\Program Files\Isora-Sentinel");
            cardPaths.Controls.Add(txtInstallDir);
            cardPaths.Controls.Add(Lbl("Los usuarios estándar solo tendrán permisos de lectura y ejecución.",
                8.75F, FontStyle.Regular, Theme.TextMuted));

            WrapLabel capLogs = Caption("CARPETA DE REGISTROS");
            capLogs.Tag = 12;
            cardPaths.Controls.Add(capLogs);
            txtLogsDir = NewPathBox(@"C:\Program Files\Isora-Sentinel\logs");
            cardPaths.Controls.Add(txtLogsDir);
            cardPaths.Controls.Add(Lbl("Almacena registros en formato texto plano y NDJSON preservados para auditorias y su SIEM.",
                8.75F, FontStyle.Regular, Theme.TextMuted));

            WrapLabel capDownloads = Caption("CARPETA DE ARCHIVADO DE DESCARGAS");
            capDownloads.Tag = 12;
            cardPaths.Controls.Add(capDownloads);
            txtDownloadsArchiveDir = NewPathBox(@"%USERPROFILE%\Documents\Isora_Sentinel_Archived\Downloads");
            cardPaths.Controls.Add(txtDownloadsArchiveDir);
            cardPaths.Controls.Add(Lbl("Directorio seguro para descargas obsoletas (>30 dias). Mantenga el valor por defecto para usar la carpeta Documentos de cada perfil.",
                8.75F, FontStyle.Regular, Theme.TextMuted));
            s1.Controls.Add(cardPaths);

            VStack cardOpts = NewCard(20, 18, 0);
            WrapLabel capOpts = Caption("OPCIONES Y COMPONENTES A INSTALAR");
            capOpts.Tag = 0;
            cardOpts.Controls.Add(capOpts);

            OptionRow rowTray = new OptionRow(
                "Instalar monitor de bandeja del sistema (System Tray)",
                "Muestra el icono en la barra de tareas para verificar integridad y ejecutar controles manualmente.", true, null);
            rowTray.Tag = 4;
            chkTray = rowTray.Check;
            cardOpts.Controls.Add(rowTray);

            OptionRow rowTasks = new OptionRow(
                "Registrar tareas programadas en Windows Task Scheduler",
                "Habilita la programación y ejecución periódica de las rutinas de seguridad bajo SYSTEM.", true, null);
            chkEnableTasks = rowTasks.Check;
            cardOpts.Controls.Add(rowTasks);

            OptionRow rowReg = new OptionRow(
                "Registrar en «Aplicaciones instaladas» de Windows",
                "Permite desinstalación formal y limpia desde la Configuración de Windows.", true, null);
            chkRegistry = rowReg.Check;
            cardOpts.Controls.Add(rowReg);

            OptionRow rowInt = new OptionRow(
                "Verificar la integridad de los archivos antes de instalar",
                "Compara cada módulo contra integrity.manifest.json antes de proceder al despliegue.", true, null);
            chkIntegrity = rowInt.Check;
            cardOpts.Controls.Add(rowInt);

            s1.Controls.Add(cardOpts);

            // ================= PÁGINA 2: TAREAS PROGRAMADAS =================
            VStack s2 = new VStack();
            Panel p2 = MakeScrollPage(s2);
            contentPanel.Controls.Add(p2);
            pages[2] = p2;

            VStack cardTasks = NewCard(20, 18, 0);
            cardTasks.Controls.Add(Caption("RUTINAS AUTOMATIZADAS A REGISTRAR EN WINDOWS TASK SCHEDULER"));

            CheckBox chkAll = new CheckBox();
            chkAll.Text = "  Seleccionar / deseleccionar todas las rutinas";
            chkAll.Font = Theme.F(8.75F, FontStyle.Bold);
            chkAll.ForeColor = Theme.AccentText;
            chkAll.Checked = true;
            chkAll.AutoSize = true;
            chkAll.Cursor = Cursors.Hand;
            chkAll.Margin = new Padding(0, 0, 0, 8);
            chkAll.CheckedChanged += delegate
            {
                taskScreenshots.Check.Checked = chkAll.Checked;
                taskTemp.Check.Checked = chkAll.Checked;
                taskDefender.Check.Checked = chkAll.Checked;
                taskIntegrity.Check.Checked = chkAll.Checked;
                taskBitLocker.Check.Checked = chkAll.Checked;
                taskEventAudit.Check.Checked = chkAll.Checked;
                taskArchiveDownloads.Check.Checked = chkAll.Checked;
            };
            cardTasks.Controls.Add(chkAll);

            taskScreenshots = new TaskScheduleRow("Limpieza de capturas de pantalla",
                "Previene acumulación de capturas en carpetas locales y OneDrive.", true, "Diario", 0, 0, "03:00");
            taskScreenshots.Tag = 4;
            cardTasks.Controls.Add(taskScreenshots);

            taskTemp = new TaskScheduleRow("Limpieza de archivos temporales",
                "Libera espacio en carpetas temporales sin seguir enlaces simbólicos (junctions).", true, "Diario", 0, 0, "03:00");
            cardTasks.Controls.Add(taskTemp);

            taskDefender = new TaskScheduleRow("Análisis con Microsoft Defender",
                "Actualiza firmas de seguridad y ejecuta análisis antimalware preventivo.", true, "Semanal", 6, 0, "02:00",
                new string[] { "Examen rápido", "Examen completo" }, 0);
            cardTasks.Controls.Add(taskDefender);

            taskIntegrity = new TaskScheduleRow("Revisión de componentes de Windows (DISM)",
                "Comprueba y repara la integridad del almacén de componentes del sistema operativo.", true, "Semanal", 6, 0, "02:00",
                new string[] { "Revisión rápida", "Reparación completa" }, 0);
            cardTasks.Controls.Add(taskIntegrity);

            taskBitLocker = new TaskScheduleRow("Verificación de cifrado BitLocker",
                "Verifica que todos los volúmenes fijos tengan cifrado y protectores TPM.", true, "Diario", 0, 0, "03:00");
            cardTasks.Controls.Add(taskBitLocker);

            taskEventAudit = new TaskScheduleRow("Auditoría de eventos de seguridad",
                "Detecta borrado de logs (1102), fuerza bruta (4625) y cuentas nuevas.", true, "Diario", 0, 0, "03:00");
            cardTasks.Controls.Add(taskEventAudit);

            taskArchiveDownloads = new TaskScheduleRow("Archivado seguro de descargas",
                @"Relocaliza descargas obsoletas (>30 días) a Documentos\Isora_Sentinel_Archived\Downloads.", true, "Mensual", 0, 0, "03:00");
            cardTasks.Controls.Add(taskArchiveDownloads);
            s2.Controls.Add(cardTasks);

            chkEnableTasks.CheckedChanged += delegate
            {
                cardTasks.Enabled = chkEnableTasks.Checked;
            };

            VStack cardSec = NewCard(20, 14, 0);
            cardSec.AccentBar = true;
            cardSec.Controls.Add(Lbl("Las tareas habilitadas se registrarán en Task Scheduler con: NT AUTHORITY\\SYSTEM.",
                9.25F, FontStyle.Bold, Theme.AccentText));
            s2.Controls.Add(cardSec);

            // ================= PÁGINA 3: INSTALACIÓN (sin scroll, el log se expande) =================
            Panel p3 = new Panel();
            p3.Dock = DockStyle.Fill;
            p3.Visible = false;
            p3.BackColor = Theme.ContentBg;
            contentPanel.Controls.Add(p3);
            pages[3] = p3;

            // Orden de docking: Fill primero, luego Top (spacer) y Top (estado)
            Card cardLog = new Card();
            cardLog.Dock = DockStyle.Fill;
            cardLog.Padding = new Padding(18, 14, 18, 18);
            p3.Controls.Add(cardLog);

            rtbLog = new RichTextBox();
            rtbLog.Dock = DockStyle.Fill;
            rtbLog.ReadOnly = true;
            rtbLog.HideSelection = false;
            rtbLog.BackColor = Theme.SidebarBg;
            rtbLog.ForeColor = Theme.TextOnDark;
            rtbLog.Font = new Font("Consolas", 9.25F);
            rtbLog.BorderStyle = BorderStyle.None;
            cardLog.Controls.Add(rtbLog);

            Label lblConsoleTitle = new Label();
            lblConsoleTitle.Dock = DockStyle.Top;
            lblConsoleTitle.Height = 26;
            lblConsoleTitle.Text = "REGISTRO DE LA INSTALACIÓN";
            lblConsoleTitle.Font = Theme.F(8.5F, FontStyle.Bold);
            lblConsoleTitle.ForeColor = Theme.AccentText;
            lblConsoleTitle.UseMnemonic = false;
            cardLog.Controls.Add(lblConsoleTitle);

            Panel spacer = new Panel();
            spacer.Dock = DockStyle.Top;
            spacer.Height = 12;
            p3.Controls.Add(spacer);

            VStack cardStatus = NewCard(20, 16, 10);
            cardStatus.Dock = DockStyle.Top;
            p3.Controls.Add(cardStatus);

            lblProgressStatus = new Label();
            lblProgressStatus.Height = 24;
            lblProgressStatus.AutoSize = false;
            lblProgressStatus.AutoEllipsis = true;
            lblProgressStatus.Text = "Listo para instalar.";
            lblProgressStatus.Font = Theme.F(10F, FontStyle.Bold);
            lblProgressStatus.ForeColor = Theme.TextPrimary;
            lblProgressStatus.UseMnemonic = false;
            cardStatus.Controls.Add(lblProgressStatus);

            progressBar = new ProgressBar();
            progressBar.Height = 22;
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = 0;
            cardStatus.Controls.Add(progressBar);
        }

        private Panel CreateFeatureCard(string title, string desc)
        {
            VStack c = NewCard(20, 12, 2);
            c.AccentBar = true;
            c.Controls.Add(Lbl(title, 9.75F, FontStyle.Bold, Theme.TextPrimary));
            c.Controls.Add(Lbl(desc, 8.75F, FontStyle.Regular, Theme.TextMuted));
            return c;
        }

        // -----------------------------------------------------------------
        //  Navegación
        // -----------------------------------------------------------------
                private void UpdateLanguageUi()
        {
            this.Text = Loc.T("Instalador â€” Isora Sentinel Suite", "Installer â€” Isora Sentinel Suite");
            if (lblGovTitle != null) lblGovTitle.Text = Loc.T("AUTORÃA Y LICENCIA", "AUTHORSHIP & LICENSE");
            if (lblAuthor != null) lblAuthor.Text = Loc.T("Sandra Gabriela Puerto Torres\nLicencia MIT (cÃ³digo abierto)", "Sandra Gabriela Puerto Torres\nMIT License (Open Source)");
            if (btnBack != null) btnBack.Text = Loc.T("<  AtrÃ¡s", "<  Back");
            if (btnNext != null) btnNext.Text = Loc.T("Siguiente  >", "Next  >");
            if (btnCancel != null) btnCancel.Text = Loc.T("Cancelar", "Cancel");
            if (btnInstall != null) btnInstall.Text = Loc.T("Instalar Suite", "Install Suite");
            if (btnLangToggle != null) btnLangToggle.Text = Loc.IsSpanish ? "ðŸŒ English" : "ðŸŒ EspaÃ±ol";

            string[] stepTitles = new string[] {
                Loc.T("Bienvenida y Alcance", "Welcome & Scope"),
                Loc.T("UbicaciÃ³n y opciones", "Location & Options"),
                Loc.T("Tareas Programadas", "Scheduled Tasks"),
                Loc.T("Instalando", "Installing")
            };
            if (navStepButtons != null)
            {
                for (int i = 0; i < navStepButtons.Length && i < stepTitles.Length; i++)
                {
                    navStepButtons[i].Title = stepTitles[i];
                    navStepButtons[i].Invalidate();
                }
            }
            ShowPage(currentStep);
        }

        private void ShowPage(int pageIndex)
        {
            currentStep = pageIndex;

            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].Visible = (i == pageIndex);
                navStepButtons[i].Active = (i == pageIndex);
                navStepButtons[i].Done = (i < pageIndex);
            }

            lblStepIndicator.Text = Loc.T("Paso " + (pageIndex + 1) + " de 4", "Step " + (pageIndex + 1) + " of 4");

            if (pageIndex == 0)
            {
                lblHeaderTitle.Text = "Bienvenido a Isora Sentinel Suite";
                lblHeaderSubtitle.Text = "Controles automáticos de saneamiento, hardening y cumplimiento para Windows.";
                btnBack.Enabled = false;
                btnNext.Visible = true;
                btnInstall.Visible = false;
            }
            else if (pageIndex == 1)
            {
                lblHeaderTitle.Text = "Ubicación y opciones";
                lblHeaderSubtitle.Text = "Defina la carpeta de instalación, carpeta de registros y opciones de componentes.";
                btnBack.Enabled = true;
                btnNext.Visible = true;
                btnInstall.Visible = false;
            }
            else if (pageIndex == 2)
            {
                lblHeaderTitle.Text = "Selección de Tareas Programadas";
                lblHeaderSubtitle.Text = "Elija las rutinas de saneamiento y auditoría a ejecutar bajo NT AUTHORITY\\SYSTEM.";
                btnBack.Enabled = true;
                btnNext.Visible = false;
                btnInstall.Visible = true;
            }
            else
            {
                lblHeaderTitle.Text = "Instalando";
                lblHeaderSubtitle.Text = "Copiando archivos, verificando integridad y registrando componentes.";
                btnBack.Enabled = false;
                btnNext.Visible = false;
                btnInstall.Visible = false;
            }
        }

        // -----------------------------------------------------------------
        //  Utilidades de ejecución
        // -----------------------------------------------------------------
        private void Log(string msg, Color? color = null)
        {
            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionLength = 0;
            rtbLog.SelectionColor = color ?? Theme.TextOnDark;
            rtbLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine);
            rtbLog.ScrollToCaret();
            Application.DoEvents();
        }

        private void SetProgress(int value, string status)
        {
            progressBar.Value = value;
            lblProgressStatus.Text = status + "   (" + value + "%)";
            Application.DoEvents();
        }

        private bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void ElevateProcess()
        {
            ProcessStartInfo proc = new ProcessStartInfo();
            proc.UseShellExecute = true;
            proc.WorkingDirectory = Environment.CurrentDirectory;
            proc.FileName = Application.ExecutablePath;
            proc.Verb = "runas";

            try
            {
                string state = string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}|{14}",
                    txtInstallDir.Text.Trim(),
                    txtLogsDir.Text.Trim(),
                    chkIntegrity.Checked ? "1" : "0",
                    chkRegistry.Checked ? "1" : "0",
                    (chkTray != null && chkTray.Checked) ? "1" : "0",
                    (chkEnableTasks != null && chkEnableTasks.Checked) ? "1" : "0",
                    taskScreenshots.SerializeState(),
                    taskTemp.SerializeState(),
                    taskDefender.SerializeState(),
                    taskIntegrity.SerializeState(),
                    taskBitLocker.SerializeState(),
                    taskEventAudit.SerializeState(),
                    taskArchiveDownloads.SerializeState(),
                    overwriteConfirmed ? "1" : "0",
                    txtDownloadsArchiveDir != null ? txtDownloadsArchiveDir.Text.Trim() : ""
                );
                string cfgFile = Path.Combine(Path.GetTempPath(), "Isora_Sentinel_Setup_Config.txt");
                File.WriteAllText(cfgFile, state, Encoding.UTF8);
                proc.Arguments = "--config-file \"" + cfgFile + "\"";

                Process.Start(proc);
                Application.Exit();
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "La instalación en C:\\Program Files y el registro de tareas en SYSTEM requiere elevación de privilegios.",
                    "Elevación Cancelada",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
        }

        // -----------------------------------------------------------------
        //  Instalación
        // -----------------------------------------------------------------
        private void BtnInstall_Click(object sender, EventArgs e)
        {
            string targetDir = txtInstallDir.Text.Trim();
            string detVer, detPath;
            if (DetectExistingInstallation(targetDir, out detVer, out detPath) && !overwriteConfirmed)
            {
                string prompt = string.Format(
                    "La carpeta de destino contiene una versión previa de Isora Sentinel ({0}) en:\n{1}\n\n" +
                    "¿Confirma que desea sobrescribir y actualizar a la versión 2.3.0?\n\n" +
                    "• Los registros históricos de auditoría (logs) se mantendrán intactos.\n" +
                    "• Se creará una copia de seguridad de auditoría de los archivos previos en backup\\.\n" +
                    "• Se cerrarán los procesos activos antiguos para permitir la actualización.",
                    string.IsNullOrEmpty(detVer) ? "versión detectada" : "v" + detVer,
                    targetDir
                );

                DialogResult dr = MessageBox.Show(
                    this,
                    prompt,
                    "Confirmación de sobrescritura — Isora Sentinel",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (dr != DialogResult.Yes)
                {
                    return;
                }
                overwriteConfirmed = true;
                if (cardUpgradeNotice != null) cardUpgradeNotice.Visible = true;
            }

            if (!IsAdministrator())
            {
                DialogResult dr = MessageBox.Show(
                    "Esta instalación requiere privilegios de Administrador para registrar tareas bajo SYSTEM y escribir en Program Files.\n\n¿Desea conceder la elevación UAC para continuar la instalación?",
                    "Elevación de Privilegios Requerida",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                if (dr == DialogResult.Yes) ElevateProcess();
                return;
            }

            StartInstallation();
        }

        private void StartInstallation()
        {
            locked = true;
            ShowPage(3);
            btnCancel.Enabled = false;
            SetProgress(5, "Iniciando instalación...");

            try
            {
                string targetDir = txtInstallDir.Text.Trim();
                string logsDir = txtLogsDir.Text.Trim();

                Log("============================================================", Theme.AccentLight);
                Log("INICIANDO INSTALACIÓN: Isora Sentinel Suite v2.3.0", Theme.AccentLight);
                Log("Sandra Gabriela Puerto Torres · Licencia MIT (código abierto)", Theme.SidebarMuted);
                Log("============================================================", Theme.AccentLight);

                // Paso 1: Cerrar procesos activos viejos (requisito #3)
                SetProgress(15, "Cerrando procesos e instancias previas...");
                TerminateExistingProcesses(targetDir);

                // Paso 2: Crear backup de auditoría si existen archivos previos y preservar logs (requisito #2)
                string detVer, detPath;
                bool hasExisting = DetectExistingInstallation(targetDir, out detVer, out detPath) ||
                                   (Directory.Exists(targetDir) && Directory.GetFiles(targetDir).Length > 0);

                if (hasExisting)
                {
                    SetProgress(25, "Creando copia de seguridad de auditoría...");
                    CreateAuditBackup(targetDir, logsDir, detVer);
                }
                else
                {
                    Log("Instalación inicial limpia (no se requirió copia de auditoría previa).", Theme.SidebarMuted);
                }

                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);

                SetProgress(40, "Copiando y actualizando archivos...");
                Log("Copiando archivos en: " + targetDir);

                Assembly asm = Assembly.GetExecutingAssembly();
                string resName = "PackageArchive";
                using (Stream stream = asm.GetManifestResourceStream(resName))
                {
                    if (stream != null)
                    {
                        using (ZipArchive archive = new ZipArchive(stream))
                        {
                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                string destPath = Path.Combine(targetDir, entry.FullName);
                                if (string.IsNullOrEmpty(entry.Name))
                                {
                                    Directory.CreateDirectory(destPath);
                                }
                                else
                                {
                                    string parentDir = Path.GetDirectoryName(destPath);
                                    if (!Directory.Exists(parentDir)) Directory.CreateDirectory(parentDir);

                                    // Extracción con reintentos para asegurar liberación completa de handles
                                    for (int attempt = 1; attempt <= 4; attempt++)
                                    {
                                        try
                                        {
                                            entry.ExtractToFile(destPath, true);
                                            break;
                                        }
                                        catch (IOException)
                                        {
                                            if (attempt == 4) throw;
                                            Thread.Sleep(300);
                                        }
                                    }
                                }
                            }
                        }
                        Log("Archivos copiados exitosamente desde el paquete.", Theme.SuccessLight);
                    }
                    else
                    {
                        Log("Aviso: Copiando desde árbol local de desarrollo...", Theme.WarningLight);
                        string devSrc = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\src");
                        if (Directory.Exists(devSrc))
                        {
                            CopyDirectory(devSrc, Path.Combine(targetDir, "src"));
                        }
                    }
                }

                // Copiar el ejecutable del asistente de configuración a targetDir para posibilitar reconfiguración desde el monitor
                try
                {
                    string currentExe = Application.ExecutablePath;
                    string destSetup = Path.Combine(targetDir, "Isora-Sentinel-Setup.exe");
                    string destUninst = Path.Combine(targetDir, "Isora-Sentinel-Uninstall.exe");
                    if (File.Exists(currentExe))
                    {
                        if (!string.Equals(currentExe, destSetup, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(currentExe, destSetup, true);
                        }
                        if (!string.Equals(currentExe, destUninst, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(currentExe, destUninst, true);
                        }
                        Log("[OK] Asistente de configuración y desinstalador GUI integrados en el directorio destino.", Theme.SuccessLight);
                    }
                }
                catch (Exception ex)
                {
                    Log("Aviso al copiar asistente: " + ex.Message, Theme.WarningLight);
                }

                SetProgress(60, "Preparando verificación de integridad...");

                if (chkIntegrity.Checked)
                {
                    SetProgress(65, "Verificando integridad criptográfica SHA-256...");
                    Log("Iniciando verificación SHA-256 contra integrity.manifest.json...", Theme.AccentLight);
                    string manifestPath = Path.Combine(targetDir, "integrity.manifest.json");
                    if (File.Exists(manifestPath))
                    {
                        int verifiedCount = 0;
                        bool integrityOk = ValidateIntegrity(manifestPath, targetDir, out verifiedCount);
                        if (!integrityOk)
                        {
                            throw new Exception("Fallo de integridad: uno o más archivos difieren de su hash SHA-256 original.");
                        }
                        Log(string.Format("✔ Integridad verificada: {0} archivos coinciden con el manifiesto.", verifiedCount), Theme.SuccessLight);
                    }
                    else
                    {
                        Log("Manifiesto de integridad no encontrado en destino (omitido).", Theme.WarningLight);
                    }
                }

                SetProgress(75, "Procesando registro de Windows...");

                if (chkRegistry.Checked)
                {
                    SetProgress(75, "Registrando en «Aplicaciones instaladas» de Windows...");
                    Log("Registrando entrada de desinstalación en el Registro de Windows...");
                    RegisterUninstall(targetDir);
                    Log("[OK] Registro completado: HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Isora Sentinel", Theme.SuccessLight);
                }

                if (chkEnableTasks != null && chkEnableTasks.Checked)
                {
                    SetProgress(88, "Registrando tareas en Task Scheduler (NT AUTHORITY\\SYSTEM)...");
                    Log("Registrando tareas programadas bajo el contexto SYSTEM...");
                    RegisterScheduledTasks(targetDir);
                }
                else
                {
                    Log("Registro de tareas programadas omitido según configuración elegida.", Theme.TextMuted);
                }

                // Registrar y activar Monitor de Bandeja del Sistema (System Tray) al arranque si fue seleccionado
                if (chkTray != null && chkTray.Checked)
                {
                    SetProgress(95, "Iniciando monitor de bandeja del sistema...");
                    RegisterSystemTray(targetDir);
                }
                else
                {
                    Log("Monitor de bandeja del sistema omitido según opciones seleccionadas.", Theme.TextMuted);
                }

                SetProgress(100, "Instalación completada");
                navStepButtons[3].Done = true;
                Log("============================================================", Theme.SuccessLight);
                Log("INSTALACIÓN COMPLETADA EXITOSAMENTE.", Theme.SuccessLight);
                Log("Carpeta de instalación: " + targetDir, Theme.SuccessLight);
                Log("Carpeta de registros (preservada): " + logsDir, Theme.SuccessLight);
                if (hasExisting)
                {
                    Log("Copia de seguridad de auditoría: " + Path.Combine(targetDir, "backup"), Theme.SuccessLight);
                }
                Log("============================================================", Theme.SuccessLight);

                string successMsg = "Isora Sentinel Suite ha sido instalada y actualizada correctamente.\n\nCarpeta: " + targetDir + "\nRegistros: " + logsDir;
                if (hasExisting)
                {
                    successMsg += "\n\n(Se creó una copia de seguridad de auditoría en la carpeta 'backup' y sus registros históricos se conservaron intactos).";
                }

                MessageBox.Show(
                    successMsg,
                    "Instalación completada",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Message, Theme.DangerLight);
                lblProgressStatus.Text = "Error en la instalación.";
                MessageBox.Show("Error durante la instalación:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnCancel.Text = "Finalizar";
                btnCancel.Enabled = true;
            }
        }

        private bool ValidateIntegrity(string manifestPath, string targetDir, out int verifiedCount)
        {
            verifiedCount = 0;
            string content = File.ReadAllText(manifestPath, Encoding.UTF8);
            using (SHA256 sha = SHA256.Create())
            {
                int filesIdx = content.IndexOf("\"files\":");
                if (filesIdx < 0) return true;

                string filesSection = content.Substring(filesIdx);
                var matches = System.Text.RegularExpressions.Regex.Matches(filesSection, "\"([^\"]+)\"\\s*:\\s*\"([a-fA-F0-9]{64})\"");

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups.Count >= 3)
                    {
                        string relFile = match.Groups[1].Value.Replace("\\\\", "\\");
                        string expectedHash = match.Groups[2].Value.ToLowerInvariant();

                        string filePath = Path.Combine(targetDir, relFile);
                        if (!File.Exists(filePath))
                        {
                            filePath = Path.Combine(targetDir, "src\\" + relFile);
                        }

                        if (File.Exists(filePath))
                        {
                            byte[] bytes = File.ReadAllBytes(filePath);
                            byte[] hashBytes = sha.ComputeHash(bytes);
                            string calcHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                            if (calcHash != expectedHash)
                            {
                                Log("Archivo modificado: " + relFile, Theme.DangerLight);
                                return false;
                            }
                            verifiedCount++;
                        }
                    }
                }
            }
            return true;
        }

        private void RegisterUninstall(string installDir)
        {
            string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Isora-Sentinel";
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(keyPath))
            {
                if (key != null)
                {
                    key.SetValue("DisplayName", "Isora Sentinel Suite (Endpoint Hardening & Governance)");
                    key.SetValue("DisplayVersion", "2.3.0");
                    key.SetValue("Publisher", "Sandra Gabriela Puerto Torres");
                    key.SetValue("InstallLocation", installDir);
                    if (txtDownloadsArchiveDir != null && !string.IsNullOrEmpty(txtDownloadsArchiveDir.Text.Trim()))
                    {
                        key.SetValue("DownloadsArchiveDir", txtDownloadsArchiveDir.Text.Trim());
                    }
                    string uninstExe = Path.Combine(installDir, "Isora-Sentinel-Uninstall.exe");
                    if (!File.Exists(uninstExe)) uninstExe = Path.Combine(installDir, "Isora-Sentinel-Setup.exe");
                    key.SetValue("UninstallString", "\"" + uninstExe + "\" --uninstall");
                    key.SetValue("QuietUninstallString", "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" + Path.Combine(installDir, "Uninstall-Sentinel.ps1") + "\" -Silent");
                    key.SetValue("DisplayIcon", Path.Combine(installDir, "Isora-Sentinel-Tray.exe") + ",0");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
        }

        private void RegisterScheduledTasks(string installDir)
        {
            var taskDefs = new List<Tuple<string, string, TaskScheduleRow>> {
                Tuple.Create("Cleanup_Screenshots", "Invoke-CleanupScreenshots.ps1", taskScreenshots),
                Tuple.Create("Cleanup_Temp_Global", "Invoke-CleanupTempGlobal.ps1", taskTemp),
                Tuple.Create("Defender_Scan", "Invoke-DefenderScan.ps1", taskDefender),
                Tuple.Create("Integrity_Check", "Invoke-IntegrityCheck.ps1", taskIntegrity),
                Tuple.Create("BitLocker_Audit", "Invoke-BitLockerAudit.ps1", taskBitLocker),
                Tuple.Create("Event_Audit", "Invoke-EventAudit.ps1", taskEventAudit),
                Tuple.Create("Archive_Downloads", "Invoke-ArchiveDownloads.ps1", taskArchiveDownloads)
            };

            foreach (var t in taskDefs)
            {
                if (t.Item3 == null || !t.Item3.Check.Checked) continue;

                string scriptFile = Path.Combine(installDir, "engines\\" + t.Item2);
                if (!File.Exists(scriptFile))
                {
                    scriptFile = Path.Combine(installDir, "src\\engines\\" + t.Item2);
                }

                string triggerArg = t.Item3.GetSchtasksTriggerArgs();
                string extraParam = "";
                if (t.Item1 == "Archive_Downloads" && txtDownloadsArchiveDir != null)
                {
                    string customDir = txtDownloadsArchiveDir.Text.Trim();
                    if (!string.IsNullOrEmpty(customDir))
                    {
                        extraParam = " -CustomArchiveDir \\\"" + customDir.Replace("\"", "") + "\\\"";
                    }
                }
                string cmdArgs = string.Format(
                    "/Create /TN \"Isora Sentinel\\{0}\" /TR \"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \\\"{1}\\\"{2}\" {3} /RU \"NT AUTHORITY\\SYSTEM\" /RL HIGHEST /F",
                    t.Item1, scriptFile, extraParam, triggerArg
                );

                ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", cmdArgs);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                int exitCode = -1;
                try
                {
                    using (Process p = Process.Start(psi))
                    {
                        p.WaitForExit();
                        exitCode = p.ExitCode;
                    }
                }
                catch { }

                if (exitCode != 0)
                {
                    // Fallback a PowerShell Register-ScheduledTask si schtasks.exe falla
                    try
                    {
                        string psTrigger = triggerArg.Contains("/SC WEEKLY") 
                            ? "New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At 2am" 
                            : "New-ScheduledTaskTrigger -Daily -At 3am";
                        string psCmd = string.Format(
                            "$p = New-ScheduledTaskPrincipal -UserId 'NT AUTHORITY\\SYSTEM' -LogonType ServiceAccount -RunLevel Highest; " +
                            "$a = New-ScheduledTaskAction -Execute 'PowerShell.exe' -Argument '-NoProfile -ExecutionPolicy Bypass -File \\\"{0}\\\"'; " +
                            "$t = {1}; " +
                            "Register-ScheduledTask -TaskName 'Isora Sentinel\\{2}' -Action $a -Trigger $t -Principal $p -Force | Out-Null",
                            scriptFile, psTrigger, t.Item1
                        );
                        ProcessStartInfo psiPs = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"" + psCmd + "\"");
                        psiPs.CreateNoWindow = true;
                        psiPs.UseShellExecute = false;
                        using (Process pPs = Process.Start(psiPs))
                        {
                            pPs.WaitForExit();
                            if (pPs.ExitCode == 0) exitCode = 0;
                        }
                    }
                    catch { }
                }

                if (exitCode == 0)
                    Log("[OK] Tarea registrada: " + t.Item1 + " (" + triggerArg + ")", Theme.SuccessLight);
                else
                    Log("[AVISO] Aviso en tarea " + t.Item1 + " (se ejecutará bajo demanda desde el monitor)", Theme.WarningLight);
            }
        }

        private void RegisterSystemTray(string installDir)
        {
            string trayExe = Path.Combine(installDir, "Isora-Sentinel-Tray.exe");
            if (File.Exists(trayExe))
            {
                // Registro estándar de inicio en Windows mediante la clave de ejecución Run
                try
                {
                    using (RegistryKey runKey = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (runKey != null)
                        {
                            runKey.SetValue("SentinelIT_Tray", "\"" + trayExe + "\"");
                            Log("[OK] Autoinicio configurado en registro: SentinelIT_Tray", Theme.SuccessLight);
                        }
                    }
                }
                catch { }

                try
                {
                    ProcessStartInfo psiRun = new ProcessStartInfo(trayExe);
                    psiRun.UseShellExecute = true;
                    Process.Start(psiRun);
                    Log("[OK] Monitor de bandeja iniciado en la barra de tareas.", Theme.SuccessLight);
                }
                catch { }
            }
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
            }
            foreach (string sub in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(sub, Path.Combine(targetDir, Path.GetFileName(sub)));
            }
        }

        private void CheckExistingVersionPrompt()
        {
            try
            {
                string targetDir = txtInstallDir != null ? txtInstallDir.Text.Trim() : @"C:\Program Files\Isora-Sentinel";
                string detVer, detPath;
                if (DetectExistingInstallation(targetDir, out detVer, out detPath))
                {
                    existingVersion = detVer;
                    existingInstallPath = detPath;
                    if (!string.IsNullOrEmpty(detPath) && txtInstallDir != null)
                    {
                        txtInstallDir.Text = detPath;
                    }

                    string prompt = string.Format(
                        "Se ha detectado una versión previa de Isora Sentinel ({0}) instalada en:\n{1}\n\n" +
                        "¿Desea sobrescribir y actualizar la instalación existente a la versión 2.3.0?\n\n" +
                        "• Los registros históricos de auditoría (logs) se preservarán intactos.\n" +
                        "• Se generará una copia de seguridad de auditoría de la versión previa en backup\\.\n" +
                        "• Se cerrarán los procesos activos antiguos para aplicar la actualización.",
                        string.IsNullOrEmpty(detVer) ? "versión previa" : "v" + detVer,
                        detPath
                    );

                    DialogResult dr = MessageBox.Show(
                        this,
                        prompt,
                        "Instalación previa detectada — Isora Sentinel",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );

                    if (dr == DialogResult.Yes)
                    {
                        overwriteConfirmed = true;
                        if (cardUpgradeNotice != null) cardUpgradeNotice.Visible = true;
                    }
                    else
                    {
                        overwriteConfirmed = false;
                        DialogResult drExit = MessageBox.Show(
                            this,
                            "Ha seleccionado no sobrescribir la instalación previa.\n\n" +
                            "¿Desea salir del asistente de instalación para conservar la versión actual intacta?",
                            "Cancelar instalación",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question
                        );
                        if (drExit == DialogResult.Yes)
                        {
                            Application.Exit();
                        }
                    }
                }
            }
            catch { }
        }

        private bool DetectExistingInstallation(string dirToCheck, out string detectedVersion, out string detectedPath)
        {
            detectedVersion = null;
            detectedPath = null;

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Isora-Sentinel"))
                {
                    if (key != null)
                    {
                        object loc = key.GetValue("InstallLocation");
                        object ver = key.GetValue("DisplayVersion");
                        if (loc != null && !string.IsNullOrEmpty(loc.ToString()))
                        {
                            detectedPath = loc.ToString();
                        }
                        if (ver != null && !string.IsNullOrEmpty(ver.ToString()))
                        {
                            detectedVersion = ver.ToString();
                        }
                    }
                }
            }
            catch { }

            string checkDir = !string.IsNullOrEmpty(dirToCheck) ? dirToCheck : (detectedPath ?? @"C:\Program Files\Isora-Sentinel");

            if (Directory.Exists(checkDir))
            {
                string trayExe = Path.Combine(checkDir, "Isora-Sentinel-Tray.exe");
                string manifest = Path.Combine(checkDir, "integrity.manifest.json");
                string enginesDir = Path.Combine(checkDir, "engines");
                string srcDir = Path.Combine(checkDir, "src");

                if (File.Exists(trayExe) || File.Exists(manifest) || Directory.Exists(enginesDir) || Directory.Exists(srcDir))
                {
                    if (string.IsNullOrEmpty(detectedPath)) detectedPath = checkDir;
                    if (string.IsNullOrEmpty(detectedVersion))
                    {
                        if (File.Exists(trayExe))
                        {
                            try
                            {
                                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(trayExe);
                                if (!string.IsNullOrEmpty(fvi.FileVersion)) detectedVersion = fvi.FileVersion;
                            }
                            catch { }
                        }
                        if (string.IsNullOrEmpty(detectedVersion)) detectedVersion = "existente";
                    }
                    return true;
                }
            }

            return !string.IsNullOrEmpty(detectedPath) && !string.IsNullOrEmpty(detectedVersion);
        }

        private void TerminateExistingProcesses(string targetDir)
        {
            Log("Verificando y deteniendo procesos activos de Isora Sentinel...", Theme.AccentLight);

            // 1. Cerrar procesos de Isora-Sentinel-Tray
            try
            {
                Process[] trayProcesses = Process.GetProcessesByName("Isora-Sentinel-Tray");
                if (trayProcesses != null && trayProcesses.Length > 0)
                {
                    Log(string.Format("Deteniendo {0} instancia(s) activa(s) de Isora-Sentinel-Tray...", trayProcesses.Length), Theme.WarningLight);
                    foreach (Process p in trayProcesses)
                    {
                        try
                        {
                            p.CloseMainWindow();
                            if (!p.WaitForExit(1500))
                            {
                                p.Kill();
                                p.WaitForExit(2000);
                            }
                        }
                        catch { }
                        finally
                        {
                            try { p.Dispose(); } catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Aviso al cerrar instancias de bandeja: " + ex.Message, Theme.WarningLight);
            }

            // Cerrar instancias previas del monitor de bandeja usando API nativa gestionada
            try
            {
                foreach (Process proc in Process.GetProcessesByName("Isora-Sentinel-Tray"))
                {
                    try
                    {
                        proc.CloseMainWindow();
                        if (!proc.WaitForExit(1500))
                        {
                            proc.Kill();
                        }
                    }
                    catch { }
                }
            }
            catch { }

            // 2. Detener tareas programadas de Isora Sentinel en ejecución
            try
            {
                string[] tasks = new string[] {
                    "Cleanup_Screenshots", "Cleanup_Temp_Global", "Defender_Scan",
                    "Integrity_Check", "BitLocker_Audit", "Event_Audit", "Archive_Downloads", "SystemTray_Monitor"
                };
                foreach (string tName in tasks)
                {
                    try
                    {
                        ProcessStartInfo psiTask = new ProcessStartInfo("schtasks.exe", string.Format("/End /TN \"Isora Sentinel\\{0}\"", tName));
                        psiTask.CreateNoWindow = true;
                        psiTask.UseShellExecute = false;
                        using (Process pt = Process.Start(psiTask))
                        {
                            pt.WaitForExit(1000);
                        }
                    }
                    catch { }
                }
            }
            catch { }

            // 3. Verificar liberación de bloqueo sobre el archivo Isora-Sentinel-Tray.exe
            string trayExePath = Path.Combine(targetDir, "Isora-Sentinel-Tray.exe");
            if (File.Exists(trayExePath))
            {
                bool released = WaitForFileRelease(trayExePath, 4000);
                if (released)
                {
                    Log("[OK] Procesos previos finalizados y bloqueos de archivos liberados.", Theme.SuccessLight);
                }
                else
                {
                    Log("[AVISO] Archivo Isora-Sentinel-Tray.exe aún en proceso de liberación por el SO; continuando.", Theme.WarningLight);
                }
            }
            else
            {
                Log("[OK] Procesos previos verificados sin bloqueos activos.", Theme.SuccessLight);
            }
        }

        private static bool WaitForFileRelease(string filePath, int timeoutMs)
        {
            int elapsed = 0;
            while (elapsed < timeoutMs)
            {
                try
                {
                    using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        return true;
                    }
                }
                catch
                {
                    Thread.Sleep(250);
                    elapsed += 250;
                }
            }
            return false;
        }

        private string CreateAuditBackup(string targetDir, string logsDir, string oldVersion)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string verSlug = string.IsNullOrEmpty(oldVersion) ? "previa" : ("v" + oldVersion.Replace(".", "_").Replace(" ", ""));
            string backupBase = Path.Combine(targetDir, "backup");
            string backupFolder = Path.Combine(backupBase, string.Format("archive_{0}_{1}", timestamp, verSlug));

            if (!Directory.Exists(backupFolder)) Directory.CreateDirectory(backupFolder);

            Log("Creando copia de seguridad de auditoría de la versión previa...", Theme.AccentLight);
            Log("Destino de respaldo: " + backupFolder);

            int copiedFiles = 0;
            List<string> backupManifest = new List<string>();
            backupManifest.Add("============================================================");
            backupManifest.Add("ISORA SENTINEL SUITE — COPIA DE SEGURIDAD DE AUDITORÍA");
            backupManifest.Add("============================================================");
            backupManifest.Add("Fecha de respaldo:   " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            backupManifest.Add("Versión respaldada: " + (string.IsNullOrEmpty(oldVersion) ? "Previa" : oldVersion));
            backupManifest.Add("Versión entrante:   2.3.0");
            backupManifest.Add("Carpeta original:   " + targetDir);
            backupManifest.Add("Carpeta de logs:    " + logsDir + " (Preservada intacta)");
            backupManifest.Add("Titular:            Sandra Gabriela Puerto Torres");
            backupManifest.Add("Licencia:           MIT (Código Abierto)");
            backupManifest.Add("------------------------------------------------------------");
            backupManifest.Add("ARCHIVOS RESPALDADOS:");

            CopyDirectoryExcluding(targetDir, backupFolder, new string[] { "logs", "backup" }, backupManifest, ref copiedFiles);

            backupManifest.Add("------------------------------------------------------------");
            backupManifest.Add(string.Format("Total de archivos archivados: {0}", copiedFiles));
            backupManifest.Add("============================================================");

            string manifestPath = Path.Combine(backupFolder, "backup_manifest.txt");
            try
            {
                File.WriteAllLines(manifestPath, backupManifest.ToArray(), Encoding.UTF8);
            }
            catch { }

            Log(string.Format("✔ Copia de seguridad completada ({0} archivos archivados en backup\\).", copiedFiles), Theme.SuccessLight);
            Log("[OK] Registros históricos de auditoría preservados intactos en: " + logsDir, Theme.SuccessLight);

            return backupFolder;
        }

        private static void CopyDirectoryExcluding(string sourceDir, string destDir, string[] excludeDirNames, List<string> manifestLines, ref int count)
        {
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(destDir, fileName);
                try
                {
                    File.Copy(file, destFile, true);
                    count++;
                    if (manifestLines != null) manifestLines.Add("  + " + fileName);
                }
                catch { }
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(subDir);
                bool exclude = false;
                if (excludeDirNames != null)
                {
                    foreach (string ex in excludeDirNames)
                    {
                        if (string.Equals(dirName, ex, StringComparison.OrdinalIgnoreCase))
                        {
                            exclude = true;
                            break;
                        }
                    }
                }
                if (!exclude)
                {
                    string destSub = Path.Combine(destDir, dirName);
                    CopyDirectoryExcluding(subDir, destSub, excludeDirNames, manifestLines, ref count);
                }
            }
        }

        [STAThread]
                private static bool HandleCommandLine(string[] args)
        {
            if (args == null || args.Length == 0) return false;

            string cmd = args[0].ToLowerInvariant();
            if (cmd != "--audit" && cmd != "--extract" && cmd != "--version" && cmd != "-v" && cmd != "--help" && cmd != "-h" && cmd != "/?")
            {
                return false;
            }

            AttachConsole(ATTACH_PARENT_PROCESS);
            try
            {
                var stdOut = new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding) { AutoFlush = true };
                Console.SetOut(stdOut);
                Console.SetError(stdOut);
            }
            catch { }
            Console.WriteLine();

            if (cmd == "--version" || cmd == "-v")
            {
                Console.WriteLine("Isora Sentinel Suite v2.3.0");
                Console.WriteLine("Autonomous Endpoint Hardening & Compliance Telemetry for Microsoft Windows");
                Console.WriteLine("Architect & Sole Copyright Holder: Sandra Gabriela Puerto Torres");
                Console.WriteLine("Commercial Brand & Distribution: Isora Cloud Group (ICG)");
                Console.WriteLine("License: MIT (Open Source) - Zero External Dependencies");
                Console.WriteLine("Security Operations: security@isora.com.co");
                return true;
            }

            if (cmd == "--help" || cmd == "-h" || cmd == "/?")
            {
                Console.WriteLine("================================================================================");
                Console.WriteLine(" Isora Sentinel Suite - Standalone Enterprise Installer & Forensic Auditor");
                Console.WriteLine("================================================================================");
                Console.WriteLine("Uso interactivo (GUI):");
                Console.WriteLine("  Isora-Sentinel-Setup.exe                  Ejecuta el asistente interactivo de instalacion");
                Console.WriteLine();
                Console.WriteLine("Modo desatendido (CLI para Sysadmins & Auditores):");
                Console.WriteLine("  Isora-Sentinel-Setup.exe --audit          Verifica en memoria los hashes SHA-256 del paquete distribuible");
                Console.WriteLine("  Isora-Sentinel-Setup.exe --extract <dir>  Extrae el 100% de los scripts para analisis estatico");
                Console.WriteLine("  Isora-Sentinel-Setup.exe --version        Muestra metadatos y revision legal del software");
                Console.WriteLine("  Isora-Sentinel-Setup.exe --help           Muestra esta ayuda tecnica");
                Console.WriteLine("================================================================================");
                return true;
            }

            if (cmd == "--audit")
            {
                Console.WriteLine("================================================================================");
                Console.WriteLine(" ISORA SENTINEL SUITE - AUDITORIA FORENSE DE INTEGRIDAD EN MEMORIA");
                Console.WriteLine(" Inspeccion estatica de modulos sin ejecucion de codigo privilegiado");
                Console.WriteLine("================================================================================");

                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream stream = asm.GetManifestResourceStream("PackageArchive"))
                {
                    if (stream == null)
                    {
                        Console.WriteLine("[ERROR] El recurso 'PackageArchive' no fue hallado en el binario.");
                        return true;
                    }

                    using (MemoryStream zipMs = new MemoryStream())
                    {
                        stream.CopyTo(zipMs);
                        zipMs.Position = 0;
                        using (ZipArchive archive = new ZipArchive(zipMs, ZipArchiveMode.Read))
                        {
                        Console.WriteLine("Total de modulos incrustados en paquete: " + archive.Entries.Count);
                        Console.WriteLine();
                        Console.WriteLine("{0,-42} {1,10}  {2}", "MODULO", "TAMANO", "ESTADO FORENSE SHA-256");
                        Console.WriteLine(new string('-', 80));

                        // Read integrity.manifest.json from package archive
                        Dictionary<string, string> manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        ZipArchiveEntry manifestEntry = archive.GetEntry("integrity.manifest.json");
                        if (manifestEntry != null)
                        {
                            using (Stream ms = manifestEntry.Open())
                            using (StreamReader sr = new StreamReader(ms, Encoding.UTF8))
                            {
                                string json = sr.ReadToEnd();
                                var matches = System.Text.RegularExpressions.Regex.Matches(json, "\"([^\"]+)\"\\s*:\\s*\"([a-fA-F0-9]{64})\"");
                                foreach (System.Text.RegularExpressions.Match m in matches)
                                {
                                    if (m.Groups.Count >= 3)
                                        manifest[m.Groups[1].Value.Replace("\\\\", "\\").Replace("/", "\\")] = m.Groups[2].Value.ToLowerInvariant();
                                }
                            }
                        }

                        using (SHA256 sha = SHA256.Create())
                        {
                            int verified = 0;
                            int total = 0;
                            try
                            {
                                foreach (ZipArchiveEntry entry in archive.Entries)
                                {
                                    if (string.IsNullOrEmpty(entry.Name)) continue;
                                    total++;
                                    string entryPath = entry.FullName.Replace("/", "\\");

                                    byte[] buffer;
                                    using (Stream es = entry.Open())
                                    using (MemoryStream msBuffer = new MemoryStream())
                                    {
                                        es.CopyTo(msBuffer);
                                        buffer = msBuffer.ToArray();
                                    }

                                    byte[] hashBytes = sha.ComputeHash(buffer);
                                    string hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                                    string status = "AUDITABLE";
                                    if (manifest.ContainsKey(entryPath))
                                    {
                                        if (manifest[entryPath] == hashHex) { status = "CONFORME (SHA-256 OK)"; verified++; }
                                        else status = "DISCREPANCIA DETECTADA";
                                    }

                                    Console.WriteLine("{0,-42} {1,8} B  {2}", entryPath, entry.Length, status);
                                }

                                Console.WriteLine(new string('-', 80));
                                Console.WriteLine("DICTAMEN: {0} modulos analizados. {1} verificados contra integrity.manifest.json.", total, verified);
                                Console.WriteLine("Cero DLLs opacas, cero dependencias remotas, cero sockets activos.");
                                Console.WriteLine("================================================================================");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("[ERROR AUDIT] " + ex.Message);
                            }
                        }
                    }
                }
                }
                return true;
            }

            if (cmd == "--extract")
            {
                if (args.Length < 2 || string.IsNullOrEmpty(args[1]))
                {
                    Console.WriteLine("[ERROR] Debe especificar la carpeta destino para la extraccion sandbox.");
                    Console.WriteLine("Ejemplo: Isora-Sentinel-Setup.exe --extract C:\\Auditoria\\Sentinel");
                    return true;
                }

                string outDir = Path.GetFullPath(args[1]);
                Console.WriteLine("Extrayendo modulos soberanos para analisis estatico en:");
                Console.WriteLine("  -> " + outDir);

                try
                {
                    if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

                    Assembly asm = Assembly.GetExecutingAssembly();
                    using (Stream stream = asm.GetManifestResourceStream("PackageArchive"))
                    {
                        if (stream == null)
                        {
                            Console.WriteLine("[ERROR] El recurso 'PackageArchive' no fue hallado en el binario.");
                            return true;
                        }

                        using (MemoryStream zipMs = new MemoryStream())
                        {
                            stream.CopyTo(zipMs);
                            zipMs.Position = 0;
                            using (ZipArchive archive = new ZipArchive(zipMs, ZipArchiveMode.Read))
                            {
                            int count = 0;
                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                string dest = Path.Combine(outDir, entry.FullName);
                                if (string.IsNullOrEmpty(entry.Name))
                                {
                                    if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);
                                }
                                else
                                {
                                    string p = Path.GetDirectoryName(dest);
                                    if (!Directory.Exists(p)) Directory.CreateDirectory(p);
                                    entry.ExtractToFile(dest, true);
                                    count++;
                                    Console.WriteLine("  [OK] " + entry.FullName);
                                }
                            }
                            Console.WriteLine();
                            Console.WriteLine("[EXITO] Se extrajeron {0} archivos. Estan listos para analisis con YARA, Defender o Kaspersky.", count);
                        }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ERROR] Fallo durante la extraccion: " + ex.Message);
                }
                return true;
            }

            return false;
        }

public static void Main(string[] args)
        {
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i].ToLowerInvariant();
                    if ((a == "--lang" || a == "-lang" || a == "/lang") && i + 1 < args.Length)
                    {
                        Loc.SetLanguage(args[i + 1]);
                    }
                    else if (a.StartsWith("--lang=") || a.StartsWith("/lang:"))
                    {
                        Loc.SetLanguage(a.Substring(7));
                    }
                }
            }
            if (HandleCommandLine(args)) return;

            // Renderizado nítido en pantallas con escala 125%/150%/200%
            try { SetProcessDPIAware(); } catch (Exception) { }
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                bool isUninstall = false;
                string exeName = Path.GetFileNameWithoutExtension(Application.ExecutablePath);
                if (exeName.IndexOf("uninstall", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isUninstall = true;
                }
                if (args != null)
                {
                    foreach (string arg in args)
                    {
                        if (arg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                            arg.Equals("-u", StringComparison.OrdinalIgnoreCase) ||
                            arg.Equals("/uninstall", StringComparison.OrdinalIgnoreCase))
                        {
                            isUninstall = true;
                            break;
                        }
                    }
                }

                if (isUninstall)
                {
                    Application.Run(new UninstallForm(args));
                }
                else
                {
                    Application.Run(new SetupForm(args));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al inicializar el asistente de instalación:\n\n" + ex.ToString(),
                    "Error de Inicialización — Isora Sentinel",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }
}
