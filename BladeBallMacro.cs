using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Media;
using System.Text;

[assembly: AssemblyTitle("BladeBall Macro")]
[assembly: AssemblyDescription("Key spam macro with hotkey")]
[assembly: AssemblyProduct("BladeBall Macro")]
[assembly: AssemblyCompany("Leo")]
[assembly: AssemblyCopyright("Leo 2026")]
[assembly: AssemblyVersion("1.0.23.0")]
[assembly: AssemblyFileVersion("1.0.23.0")]

// ================= Design =================
static class Theme
{
    public static readonly Color BG = Color.FromArgb(41, 41, 44);      // = durchsichtiges Glas; nah an der Glasfarbe -> saubere Kanten
    public static readonly Color CARD = BG;                              // Karten sind Glas, nur die Kante ist sichtbar
    public static readonly Color GHOST = BG;                             // ohne Flaeche, ohne Kontur
    public static readonly Color CHIP = BG;                              // Glas-Button: nur feine Kontur
    public static readonly Color ACCENT = Color.FromArgb(242, 242, 245); // Weiss statt Blau
    public static readonly Color OK = Color.FromArgb(52, 211, 125);
    public static readonly Color OFF = Color.FromArgb(235, 87, 87);
    public static readonly Color MUTED = Color.FromArgb(170, 170, 176);
    public static readonly Color TEXT2 = Color.FromArgb(215, 215, 220);
    public static readonly Color EDGE = Color.FromArgb(60, 60, 66);
    public static readonly Color WARN = Color.FromArgb(245, 180, 60);

    public static Button Btn(string text, Rectangle r, Color c, float size)
    {
        RoundButton b = new RoundButton { Text = text, Bounds = r, BackColor = c, ForeColor = Color.White, Font = new Font("Segoe UI", size, FontStyle.Regular), Cursor = Cursors.Hand, TabStop = false };
        return b;
    }
    public static Label Caption(string text, int x, int y)
    {
        return new Label { Text = text, ForeColor = MUTED, BackColor = CARD, Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Regular), AutoSize = true, Location = new Point(x, y) };
    }
}

public class RoundButton : Button
{
    public int Radius = 12;
    public Color HoverColor = Color.Empty;
    public bool Outline = true;
    bool hover, down;
    public void SetHover(bool h) { if (hover != h) { hover = h; if (!h) down = false; Invalidate(); } }
    public RoundButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnBackColorChanged(EventArgs e) { Invalidate(); base.OnBackColorChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    static Color Shift(Color c, int d)
    {
        return Color.FromArgb(c.A, Math.Max(0, Math.Min(255, c.R + d)), Math.Max(0, Math.Min(255, c.G + d)), Math.Max(0, Math.Min(255, c.B + d)));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Color surface = Parent is RoundPanel ? ((RoundPanel)Parent).Fill : (Parent != null ? Parent.BackColor : Theme.BG);
        Graphics g = e.Graphics;
        g.Clear(surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color fill = BackColor;
        bool glass = fill == surface || fill.A == 0;
        if (glass) fill = hover ? (HoverColor != Color.Empty ? HoverColor : Shift(surface, 16)) : surface;
        else if (down) fill = Shift(fill, -18);
        else if (hover) fill = HoverColor != Color.Empty ? HoverColor : Shift(fill, 16);
        int r = Math.Min(Radius, Height / 2);
        int d = r * 2;
        Rectangle rc = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = new GraphicsPath())
        {
            p.AddArc(rc.X, rc.Y, d, d, 180, 90);
            p.AddArc(rc.Right - d, rc.Y, d, d, 270, 90);
            p.AddArc(rc.Right - d, rc.Bottom - d, d, d, 0, 90);
            p.AddArc(rc.X, rc.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
            if (glass && Outline)
                using (Pen pen = new Pen(Color.FromArgb(hover ? 90 : 45, 255, 255, 255))) g.DrawPath(pen, p);
        }
        double lum = (0.299 * fill.R + 0.587 * fill.G + 0.114 * fill.B) / 255.0;
        Color txt = lum > 0.6 ? Color.FromArgb(18, 18, 20) : ForeColor;
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, txt, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

public class RoundPanel : Panel
{
    public Color Fill = Theme.CARD;
    public Color Border = Theme.EDGE;
    public int Radius = 16;
    Color accent = Color.Empty;
    public Color AccentBar { get { return accent; } set { if (accent != value) { accent = value; Invalidate(); } } }
    public RoundPanel() { DoubleBuffered = true; BackColor = Theme.BG; }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        int d = Radius * 2;
        using (GraphicsPath p = new GraphicsPath())
        {
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            using (SolidBrush b = new SolidBrush(Fill)) e.Graphics.FillPath(b, p);
            if (accent != Color.Empty)
                using (Pen pen = new Pen(Color.FromArgb(110, accent))) e.Graphics.DrawPath(pen, p);
            else
                using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(0, 0, Width, Height), Color.FromArgb(60, 255, 255, 255), Color.FromArgb(18, 255, 255, 255), 90f))
                using (Pen pen = new Pen(lb, 1f)) e.Graphics.DrawPath(pen, p); // Glas-Kante: oben hell, unten fast weg
        }
        if (accent != Color.Empty)
            using (SolidBrush ab = new SolidBrush(accent)) e.Graphics.FillRectangle(ab, 0, Radius, 4, Height - Radius * 2);
    }
}

public class Dot : Control
{
    Color c = Color.Gray;
    int glowA = 60;
    public Color DotColor { get { return c; } set { if (c != value) { c = value; Invalidate(); } } }
    public int Glow { get { return glowA; } set { if (glowA != value) { glowA = value; Invalidate(); } } }
    public Dot() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (SolidBrush glow = new SolidBrush(Color.FromArgb(glowA, c))) e.Graphics.FillEllipse(glow, 0, 0, Width - 1, Height - 1);
        using (SolidBrush b = new SolidBrush(c)) e.Graphics.FillEllipse(b, 3, 3, Width - 7, Height - 7);
    }
}

// ================= Glas =================
public static class Glass
{
    [StructLayout(LayoutKind.Sequential)] struct AccentPolicy { public int State; public int Flags; public uint Color; public int Anim; }
    [StructLayout(LayoutKind.Sequential)] struct CompData { public int Attr; public IntPtr Data; public int Size; }
    [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr h, ref CompData d);

    public static bool IsWin11()
    {
        try
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
            {
                int b;
                return k != null && int.TryParse(k.GetValue("CurrentBuildNumber") as string, out b) && b >= 22000;
            }
        }
        catch { return false; }
    }

    // Weichzeichner hinter dem Fenster (Win10/11). Faellt bei alten Systemen still auf "kein Effekt" zurueck.
    public static bool Blur(IntPtr h)
    {
        foreach (int state in new int[] { 4, 3 }) // 4 = Acrylic, 3 = einfacher Blur
        {
            AccentPolicy a = new AccentPolicy { State = state, Flags = 2, Color = 0x66262626 }; // AABBGGRR: neutrales Glas, passend zur Schluesselfarbe
            int size = Marshal.SizeOf(a);
            IntPtr mem = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(a, mem, false);
                CompData d = new CompData { Attr = 19, Data = mem, Size = size };
                if (SetWindowCompositionAttribute(h, ref d) != 0) return true;
            }
            catch { }
            finally { Marshal.FreeHGlobal(mem); }
        }
        return false;
    }

    public static GraphicsPath Rounded(Rectangle r, int radius)
    {
        int d = radius * 2;
        GraphicsPath p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

// Unsichtbares Fenster direkt hinter dem Macro: liefert den Milchglas-Hintergrund
public class GlassBackdrop : Form
{
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    readonly Form main;
    RoundButton hot;

    RoundButton HitButton()
    {
        Point scr = Cursor.Position;
        Control c = main;
        while (true)
        {
            Control child = c.GetChildAtPoint(c.PointToClient(scr), GetChildAtPointSkip.Invisible | GetChildAtPointSkip.Disabled);
            if (child == null) break;
            c = child;
        }
        return c as RoundButton;
    }

    void SetHot(RoundButton b)
    {
        if (hot == b) return;
        if (hot != null) hot.SetHover(false);
        hot = b;
        if (hot != null) hot.SetHover(true);
        Cursor = hot != null ? Cursors.Hand : Cursors.Default;
    }

    public GlassBackdrop(Form owner)
    {
        main = owner;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        Text = "";
        // Klicks in die Glas-Luecken verschieben das Macro-Fenster
        MouseDown += delegate(object s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            RoundButton b = HitButton();
            if (b != null) { b.PerformClick(); return; } // Klick auf einen Glas-Button
            ReleaseCapture();
            SendMessage(main.Handle, 0xA1, 2, 0);       // sonst: Fenster verschieben
        };
        MouseMove += delegate { SetHot(HitButton()); };
        MouseLeave += delegate { SetHot(null); };
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80 | 0x08000000; // TOOLWINDOW | NOACTIVATE
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Glass.Blur(Handle);
        try { int v = 2; DwmSetWindowAttribute(Handle, 33, ref v, 4); } catch { } // runde Ecken (Win11)
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (!Glass.IsWin11() && Width > 0)
            using (GraphicsPath p = Glass.Rounded(new Rectangle(0, 0, Width, Height), 12)) Region = new Region(p);
    }
}

// ================= App-Icon =================
public static class AppIcon
{
    public static Bitmap Draw(int s)
    {
        Bitmap b = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float r = s * 0.24f, d = r * 2;
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddArc(0, 0, d, d, 180, 90);
                p.AddArc(s - 1 - d, 0, d, d, 270, 90);
                p.AddArc(s - 1 - d, s - 1 - d, d, d, 0, 90);
                p.AddArc(0, s - 1 - d, d, d, 90, 90);
                p.CloseFigure();
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(14, 14, 22))) g.FillPath(bg, p);
            }
            // Ball mit Glow
            float m = s * 0.17f;
            RectangleF ball = new RectangleF(m, m, s - 2 * m, s - 2 * m);
            using (Pen glow = new Pen(Color.FromArgb(70, 255, 255, 255), s * 0.05f))
                g.DrawEllipse(glow, m - s * 0.03f, m - s * 0.03f, ball.Width + s * 0.06f, ball.Height + s * 0.06f);
            using (LinearGradientBrush lg = new LinearGradientBrush(ball, Color.FromArgb(78, 78, 86), Color.FromArgb(18, 18, 22), 45f))
                g.FillEllipse(lg, ball);
            // Schwert: Klinge, Parierstange, Griff
            Func<float, float, PointF> P = (x, y) => new PointF(x * s, y * s);
            PointF[] blade = { P(0.385f, 0.555f), P(0.445f, 0.615f), P(0.835f, 0.165f) };
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(110, 0, 20, 60)))
                g.FillPolygon(sh, new PointF[] { P(0.395f, 0.585f), P(0.455f, 0.645f), P(0.85f, 0.19f) });
            using (LinearGradientBrush bb = new LinearGradientBrush(P(0.4f, 0.6f), P(0.83f, 0.17f), Color.FromArgb(225, 225, 230), Color.White))
                g.FillPolygon(bb, blade);
            using (Pen edge = new Pen(Color.FromArgb(150, 200, 200, 205), s * 0.012f))
                g.DrawLine(edge, P(0.415f, 0.585f), P(0.835f, 0.165f));
            using (Pen guard = new Pen(Color.FromArgb(200, 200, 208), s * 0.06f))
            {
                guard.StartCap = LineCap.Round; guard.EndCap = LineCap.Round;
                g.DrawLine(guard, P(0.33f, 0.53f), P(0.47f, 0.67f));
            }
            using (Pen grip = new Pen(Color.FromArgb(30, 30, 34), s * 0.065f))
            {
                grip.StartCap = LineCap.Round; grip.EndCap = LineCap.Round;
                g.DrawLine(grip, P(0.39f, 0.61f), P(0.25f, 0.75f));
            }
            using (SolidBrush pommel = new SolidBrush(Color.FromArgb(200, 200, 208)))
                g.FillEllipse(pommel, 0.195f * s, 0.745f * s, 0.075f * s, 0.075f * s);
        }
        return b;
    }

    public static Icon Make(int s)
    {
        using (Bitmap b = Draw(s)) return Icon.FromHandle(b.GetHicon());
    }

    // .ico-Datei fuer das EXE-Symbol (PNG-Eintraege, Windows 10/11)
    public static byte[] BuildIco()
    {
        int[] sizes = { 256, 48, 32, 16 };
        List<byte[]> pngs = new List<byte[]>();
        foreach (int s in sizes)
            using (Bitmap b = Draw(s))
            using (MemoryStream ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
        MemoryStream m = new MemoryStream();
        BinaryWriter w = new BinaryWriter(m);
        w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
        int off = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            byte dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
            w.Write(dim); w.Write(dim); w.Write((byte)0); w.Write((byte)0);
            w.Write((short)1); w.Write((short)32);
            w.Write(pngs[i].Length); w.Write(off);
            off += pngs[i].Length;
        }
        foreach (byte[] p in pngs) w.Write(p);
        w.Flush();
        return m.ToArray();
    }
}

// ================= In-Game-Overlay =================
// Kleine Statusanzeige ueber Roblox: durchklickbar, stiehlt keinen Fokus.
public class OverlayForm : Form
{
    string text = "";
    Color col = Theme.ACCENT;
    readonly Font font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Regular);
    static readonly Color KEY = Color.FromArgb(255, 0, 255);

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(168, 32);
        BackColor = KEY;
        TransparencyKey = KEY;
        TopMost = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x8000000 | 0x8; // LAYERED | TRANSPARENT | TOOLWINDOW | NOACTIVATE | TOPMOST
            return cp;
        }
    }

    public void SetState(string t, Color c)
    {
        if (t == text && c == col) return;
        text = t;
        col = c;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(KEY);
        g.SmoothingMode = SmoothingMode.None; // keine Farbraender an den Kanten
        int d = Height - 1;
        using (GraphicsPath p = new GraphicsPath())
        {
            p.AddArc(0, 0, d, d, 90, 180);
            p.AddArc(Width - 1 - d, 0, d, d, 270, 180);
            p.CloseFigure();
            using (SolidBrush b = new SolidBrush(Color.FromArgb(16, 16, 24))) g.FillPath(b, p);
            using (Pen pen = new Pen(Color.FromArgb(150, col), 1.5f)) g.DrawPath(pen, p);
        }
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (SolidBrush glow = new SolidBrush(Color.FromArgb(70, col))) g.FillEllipse(glow, 9, 7, 17, 17);
        using (SolidBrush dot = new SolidBrush(col)) g.FillEllipse(dot, 12, 10, 11, 11);
        TextRenderer.DrawText(g, text, font, new Rectangle(31, 0, Width - 38, Height), Color.White,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}

// ================= Hintergrund-Apps =================
public class AppCleanerForm : Form
{
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);

    // Windows-, Treiber- und Roblox-Prozesse werden nie angezeigt
    static readonly HashSet<string> Skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "idle", "system", "registry", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "svchost",
        "dwm", "explorer", "fontdrvhost", "sihost", "ctfmon", "taskhostw", "runtimebroker", "searchhost",
        "searchapp", "searchindexer", "searchprotocolhost", "searchfilterhost", "startmenuexperiencehost",
        "shellexperiencehost", "textinputhost", "securityhealthsystray", "securityhealthservice", "audiodg",
        "conhost", "dllhost", "applicationframehost", "systemsettings", "lockapp", "msmpeng", "nissrv",
        "smartscreen", "widgets", "widgetservice", "phoneexperiencehost", "useroobebroker", "wmiprvse",
        "spoolsv", "taskmgr", "memcompression", "secure system", "mpdefendercoreservice", "sgrmbroker",
        "robloxplayerbeta", "robloxplayerlauncher", "robloxcrashhandler", "bloxstrap", "claude",
        "nvcontainer", "nvdisplay.container", "atieclxx", "atiesrxx", "igfxem", "igfxhk", "igfxtray",
        "rtkauduservice64", "dashost", "wudfhost", "crossdeviceservice", "gamebarpresencewriter" };

    static readonly HashSet<string> Browsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "arc" };

    // Typische FPS-Fresser -> vorausgewaehlt
    static readonly HashSet<string> Suggest = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "steam", "steamwebhelper", "epicgameslauncher", "epicwebhelper", "teams",
        "ms-teams", "eadesktop", "origin", "battle.net", "riotclientservices", "riotclientux", "upc",
        "ubisoftconnect", "medal", "overwolf", "wallpaper32", "wallpaper64", "lively", "nvidia share" };

    Panel list;
    Label info;
    Button go;
    readonly List<KeyValuePair<string, CheckBox>> boxes = new List<KeyValuePair<string, CheckBox>>();

    public AppCleanerForm()
    {
        Text = "Background Apps";
        Icon = AppIcon.Make(64);
        FormBorderStyle = FormBorderStyle.None;
        ClientSize = new Size(360, 470);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.BG;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);
        ShowInTaskbar = false;

        Label title = new Label { Text = "BACKGROUND APPS", Font = new Font("Segoe UI Semibold", 13f, FontStyle.Regular), AutoSize = true, Location = new Point(18, 14), BackColor = Theme.BG };
        Button close = Theme.Btn("✕", new Rectangle(316, 10, 34, 30), Theme.BG, 10f);
        close.Click += delegate { Close(); };
        Label sub = new Label { Text = "Apps using the most memory right now", ForeColor = Theme.MUTED, Font = new Font("Segoe UI", 8.5f), AutoSize = true, Location = new Point(20, 42), BackColor = Theme.BG };

        RoundPanel card = new RoundPanel { Bounds = new Rectangle(20, 68, 320, 290) };
        list = new Panel { Bounds = new Rectangle(4, 6, 312, 278), BackColor = Theme.CARD, AutoScroll = true };
        card.Controls.Add(list);

        info = new Label { Text = "Unsaved work in closed apps may be lost.", ForeColor = Theme.MUTED, Font = new Font("Segoe UI", 8.5f), AutoSize = false, Bounds = new Rectangle(20, 366, 320, 36), BackColor = Theme.BG };
        go = Theme.Btn("CLOSE SELECTED", new Rectangle(20, 406, 214, 44), Theme.ACCENT, 10.5f);
        go.Click += delegate { CloseSelected(); };
        Button refresh = Theme.Btn("REFRESH", new Rectangle(242, 406, 98, 44), Theme.CHIP, 9.5f);
        refresh.Click += delegate { Fill(); };

        Controls.AddRange(new Control[] { title, close, sub, card, info, go, refresh });
        foreach (Control c in new Control[] { this, title, sub })
            c.MouseDown += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 2, 0); } };
        Fill();
    }

    void Fill()
    {
        list.Controls.Clear();
        boxes.Clear();
        int session = 0;
        string self = "";
        using (Process me = Process.GetCurrentProcess()) { session = me.SessionId; self = me.ProcessName; }

        Dictionary<string, long> mem = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (Process p in Process.GetProcesses())
        {
            try
            {
                string n = p.ProcessName;
                if (p.SessionId == session && !Skip.Contains(n) && !n.Equals(self, StringComparison.OrdinalIgnoreCase))
                {
                    long v;
                    mem.TryGetValue(n, out v);
                    mem[n] = v + p.WorkingSet64;
                }
            }
            catch { }
            finally { p.Dispose(); }
        }

        List<KeyValuePair<string, long>> sorted = new List<KeyValuePair<string, long>>(mem);
        sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

        int y = 2, shown = 0;
        foreach (KeyValuePair<string, long> kv in sorted)
        {
            if (kv.Value < 150L * 1024 * 1024 || shown >= 6) break;
            string tag = Browsers.Contains(kv.Key) ? "   · browser" : "";
            CheckBox cb = new CheckBox
            {
                Text = kv.Key + "   " + (kv.Value / 1048576) + " MB" + tag,
                Checked = Suggest.Contains(kv.Key),
                ForeColor = Color.White,
                BackColor = Theme.CARD,
                FlatStyle = FlatStyle.Flat,
                Bounds = new Rectangle(10, y, 280, 26),
                Cursor = Cursors.Hand
            };
            list.Controls.Add(cb);
            boxes.Add(new KeyValuePair<string, CheckBox>(kv.Key, cb));
            y += 30;
            shown++;
        }
        if (shown == 0)
            list.Controls.Add(new Label { Text = "Nothing heavy is running – you're good.", ForeColor = Theme.MUTED, BackColor = Theme.CARD, AutoSize = true, Location = new Point(12, 12) });
    }

    void CloseSelected()
    {
        List<string> names = new List<string>();
        foreach (KeyValuePair<string, CheckBox> b in boxes) if (b.Value.Checked) names.Add(b.Key);
        if (names.Count == 0) { info.Text = "Select at least one app."; return; }

        go.Enabled = false;
        go.Text = "CLOSING...";
        Thread t = new Thread(delegate()
        {
            int session;
            using (Process me = Process.GetCurrentProcess()) session = me.SessionId;
            long freed = 0;
            int apps = 0;
            foreach (string n in names)
            {
                bool any = false;
                foreach (Process p in Process.GetProcessesByName(n))
                {
                    try
                    {
                        if (p.SessionId != session) continue;
                        freed += p.WorkingSet64;
                        any = true;
                        if (p.MainWindowHandle != IntPtr.Zero) p.CloseMainWindow(); // erst normal schliessen
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
                if (any) apps++;
            }
            Thread.Sleep(2500);
            foreach (string n in names)
                foreach (Process p in Process.GetProcessesByName(n))
                {
                    try { if (p.SessionId == session) p.Kill(); } catch { } // Reste (z. B. im Tray) beenden
                    finally { p.Dispose(); }
                }
            Thread.Sleep(400);
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    info.Text = "Closed " + apps + " app(s) · about " + (freed / 1048576) + " MB freed.";
                    go.Enabled = true;
                    go.Text = "CLOSE SELECTED";
                    Fill();
                });
            }
            catch { }
        });
        t.IsBackground = true;
        t.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (Pen p = new Pen(Theme.EDGE)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
    }
}

// ================= Key-Abfrage =================
public class KeyForm : Form
{
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);

    TextBox box;
    Label error;
    CheckBox remember;
    public bool Remember { get { return remember.Checked; } }

    public KeyForm()
    {
        Text = "BladeBall Macro - Key";
        Icon = AppIcon.Make(64);
        FormBorderStyle = FormBorderStyle.None;
        ClientSize = new Size(340, 236);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.BG;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);
        TopMost = true;

        Label t1 = new Label { Text = "BLADE BALL", Font = new Font("Segoe UI Semibold", 15f, FontStyle.Regular), AutoSize = true, Location = new Point(18, 14), BackColor = Theme.BG };
        int w1 = TextRenderer.MeasureText(t1.Text, t1.Font).Width;
        Label t2 = new Label { Text = "MACRO", Font = new Font("Segoe UI Light", 15f, FontStyle.Regular), ForeColor = Theme.TEXT2, AutoSize = true, Location = new Point(14 + w1, 14), BackColor = Theme.BG };
        Button close = Theme.Btn("✕", new Rectangle(296, 10, 34, 30), Theme.BG, 10f);
        close.FlatAppearance.MouseOverBackColor = Theme.OFF;
        close.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

        Label lbl = new Label { Text = "Enter your key to continue", ForeColor = Theme.MUTED, AutoSize = true, Location = new Point(20, 58), BackColor = Theme.BG };

        box = new TextBox { Bounds = new Rectangle(20, 84, 300, 32), BackColor = Theme.CHIP, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 13f), UseSystemPasswordChar = true };

        remember = new CheckBox { Text = "Remember key", Checked = true, ForeColor = Theme.MUTED, BackColor = Theme.BG, FlatStyle = FlatStyle.Flat, Bounds = new Rectangle(20, 124, 200, 22) };
        error = new Label { Text = "", ForeColor = Theme.OFF, AutoSize = false, Bounds = new Rectangle(20, 148, 300, 20), BackColor = Theme.BG };

        Button ok = Theme.Btn("UNLOCK", new Rectangle(20, 176, 300, 42), Theme.ACCENT, 11f);
        ok.Click += delegate { TryUnlock(); };
        AcceptButton = ok;

        Controls.AddRange(new Control[] { t1, t2, close, lbl, box, remember, error, ok });
        foreach (Control c in new Control[] { this, t1, t2, lbl }) c.MouseDown += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 2, 0); } };
        Shown += delegate { box.Focus(); };
    }

    void TryUnlock()
    {
        if (box.Text.Trim() == MacroForm.ACCESS_KEY) { DialogResult = DialogResult.OK; Close(); }
        else { error.Text = "Wrong key"; box.SelectAll(); box.Focus(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (Pen p = new Pen(Theme.EDGE)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
    }
}

// ================= Hauptfenster =================
public class MacroForm : Form
{
    // >>> Change key here, then click "Update / Rebuild" <<<
    public const string ACCESS_KEY = "Hallo";

    // Versionsnummer: bei jedem neuen Update um 1 erhoehen
    public const int BUILD = 23;              // intern fuer Updates, immer +1
    public const string VERSION_NAME = "1.0"; // angezeigte Version

    // >>> Online-Updates fuer Freunde: hier den Raw-Link zu deiner BladeBallMacro.cs auf GitHub eintragen <<<
    // Beispiel: https://raw.githubusercontent.com/DEINNAME/bladeball-macro/main/BladeBallMacro.cs
    public const string UPDATE_URL = "https://raw.githubusercontent.com/leohanz74-cyber/bladeball-macro/main/BladeBallMacro.cs";
    public static bool RememberKey = false;

    // ---------- WinAPI ----------
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion U; }
    class KeyInputs { public INPUT[] Down; public INPUT[] Up; }
    delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint mapType);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, HookProc cb, IntPtr hMod, uint tid);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int n, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr dc, int index);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string name);
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    [DllImport("kernel32.dll")] static extern IntPtr CreateWaitableTimerExW(IntPtr attr, IntPtr name, uint flags, uint access);
    [DllImport("kernel32.dll")] static extern bool SetWaitableTimer(IntPtr h, ref long due, int period, IntPtr cb, IntPtr arg, bool resume);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("avrt.dll")] static extern IntPtr AvSetMmThreadCharacteristicsW(string task, ref uint index);
    [ThreadStatic] static IntPtr waitTimer;
    [ThreadStatic] static bool timerTried;
    static volatile bool hiResTimer = false;
    [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; }
    [DllImport("user32.dll")] static extern int GetMessage(out MSG m, IntPtr h, uint min, uint max);

    const uint KEYEVENTF_EXTENDEDKEY = 0x0001, KEYEVENTF_KEYUP = 0x0002, KEYEVENTF_SCANCODE = 0x0008;

    // ---------- Einstellungen (Standard = beste Settings) ----------
    static readonly string[] ModeNames = { "HOLD", "TOGGLE" };
    static readonly string[] PresetNames = { "AUTO", "ECO", "BALANCED", "MAX" };
    static readonly string[] PresetHints = { "scans your PC", "low-end PCs", "strong PCs", "high-end only" };
    int[] presetCps = { 120, 60, 240, 1000 };

    Keys spamKey = Keys.F, hotKey = Keys.E, hotKey2 = Keys.None;
    volatile bool hd1 = false, hd2 = false; // welcher Hotkey gerade gehalten wird
    volatile int mode = 0;       // 0 Halten, 1 Umschalten
    volatile int preset = 0;     // AUTO
    volatile bool onlyRoblox = true;
    bool boostOn = true;         // Hoechstleistungs-Energieplan
    bool autoMin = true;         // Macro minimiert sich, wenn Roblox aktiv ist
    const string HIGH_PERF = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    const string OWN_PLAN = "b1adeba1-0000-4000-8000-0000000000b0"; // feste Kopie -> keine doppelten Plaene
    string prevScheme = null;
    bool boostActive = false, boostFailed = false;

    // ---------- Laufzeit ----------
    volatile bool running = false;
    volatile bool armed = true;
    volatile bool capturing = false;
    volatile int captureTarget = 0;
    readonly object runLock = new object();
    IntPtr mhook = IntPtr.Zero;
    HookProc mouseProc;
    volatile bool hotDown = false;
    long runStart = 0;
    bool periodOn = false;
    volatile KeyInputs keys;
    long count = 0, lastCount = 0;
    double cpsSmooth = 0;
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly Stopwatch tickWatch = Stopwatch.StartNew();
    readonly ManualResetEventSlim go = new ManualResetEventSlim(false);
    IntPtr hook = IntPtr.Zero;
    HookProc hookProc;
    IntPtr lastFg = IntPtr.Zero;
    bool lastFgRoblox = false;
    readonly object fgLock = new object();
    string settingsPath;
    int refreshHz = 60;
    volatile int autoCps = 120;   // Smart-AUTO: passt sich live an diesen PC an
    int autoMax = 120;
    volatile bool scanning = false;
    bool scannedInRoblox = false;
    volatile int autoFloor = 90;  // SMART geht nie tiefer als das
    string pcInfo = "";

    // ---------- UI ----------
    Label statusText, hintText, statsText, perfValue, scanText;
    Dot dot;
    Button armBtn, spamChip, hotChip, updateBtn;
    Button[] modeBtns = new Button[2], presetBtns = new Button[4];

    Button tabMacro, tabBoost, minBtn, updBtn, fpsModeBtn;
    Label updDesc, fpsModeInfo, fpsModeList;
    RoundPanel fpsCard;
    volatile bool fpsMode = false;
    int closedApps = 0, slowedApps = 0;
    readonly Dictionary<int, ProcessPriorityClass> slowed = new Dictionary<int, ProcessPriorityClass>();
    readonly object slowLock = new object();
    long lastSweepMs = 0;
    bool fpsModeSaved = false, fpsModeKnown = false;
    volatile int updState = 0; // 0 nichts, 1 prueft, 2 aktuell, 3 Update da, 4 Fehler
    volatile int remoteBuild = 0;
    string remoteName = "";
    long lastCheckMs = 0;
    volatile int autoBest = 120;

    // Statistik, Reaktionszeit
    long pressTick = 0;
    volatile bool respPending = false;
    double respMs = -1;
    int clashes = 0;
    double bestClash = 0;
    long ovLastCount = 0, ovLastMs = 0;
    int ovRate = 0;
    Button hot2Chip;
    string remoteSrc = null;
    bool fpsOn = false;
    int prevGameMode = -2, prevDvr = -2, prevCapture = -2; // -2 = noch nicht gesichert, -1 = war nicht gesetzt
    string fpsInfo = "";
    Panel macroPage, boostPage, extrasPage;
    RoundPanel status;
    GlassBackdrop backdrop;
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    Button tabExtras, chatBtn, ovlBtn, ovlPosBtn, sndBtn, panicBtn, cleanBtn;

    // Extras
    volatile bool chatProtect = true, chatOpen = false;
    long chatSince = 0;
    bool overlayOn = true, soundOn = true, panicOn = true;
    int overlayPos = 0; // Blade-Ball-Layout: 0 oben Mitte (frei), 1 oben links unter dem Menue, 2 oben rechts unter den Buttons
    static readonly string[] PosArrows = { "↑", "↖", "↗" };
    OverlayForm overlay;
    SoundPlayer sndOn, sndOff, sndChat;
    int winX = int.MinValue, winY = int.MinValue;

    public MacroForm()
    {
        Text = "BladeBall Macro";
        Icon = AppIcon.Make(64);
        FormBorderStyle = FormBorderStyle.None;
        ClientSize = new Size(380, 580);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.BG;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);
        KeyPreview = true;
        DoubleBuffered = true;

        settingsPath = SettingsPath();
        DetectRefreshRate();
        LoadSettings();
        if (winX != int.MinValue)
        {
            foreach (Screen s in Screen.AllScreens)
                if (s.WorkingArea.Contains(winX + 60, winY + 20)) { StartPosition = FormStartPosition.Manual; Location = new Point(winX, winY); break; }
        }
        LocationChanged += delegate { if (WindowState == FormWindowState.Normal) { winX = Left; winY = Top; } };
        BuildUi();
        BuildSounds();
        RebuildKeys();
        RefreshUi(true);

        // Glas: Hintergrund komplett durchsichtig, Karten leicht durchscheinend
        TransparencyKey = Theme.BG;
        backdrop = new GlassBackdrop(this);
        if (!Glass.IsWin11())
            using (GraphicsPath rp = Glass.Rounded(new Rectangle(0, 0, Width, Height), 12)) Region = new Region(rp);
        LocationChanged += delegate { SyncGlass(); };
        SizeChanged += delegate { SyncGlass(); };
        Activated += delegate { SyncGlass(); };
        Shown += delegate { SyncGlass(); };
        // FPS MODE fasst Energieplan + FPS-Tweaks zusammen (alte Einstellungen werden uebernommen)
        bool startFps = fpsModeKnown ? fpsModeSaved : (boostOn || fpsOn);
        if (fpsOn && !startFps) RemoveFps();
        if (startFps) SetFpsMode(true);
        RefreshUi(true);

        System.Windows.Forms.Timer ui = new System.Windows.Forms.Timer();
        ui.Interval = 100;
        ui.Tick += delegate { Tick(); };
        ui.Start();

        overlay = new OverlayForm();
        System.Windows.Forms.Timer ov = new System.Windows.Forms.Timer();
        ov.Interval = 50;
        ov.Tick += delegate { OverlayTick(); };
        ov.Start();

        KeyDown += OnKeyCapture;
        Thread hk = new Thread(HookThread);
        hk.IsBackground = true;
        hk.Priority = ThreadPriority.Highest; // Hotkey reagiert sofort, auch wenn die UI gerade zeichnet
        hk.Start();

        if (autoCps > autoMax) autoCps = autoMax;
        StartScan();
        CheckOnline(); // still im Hintergrund nach Updates schauen

        Thread worker = new Thread(Loop);
        worker.IsBackground = true;
        worker.Priority = ThreadPriority.Normal; // konkurriert nicht mit Roblox um die CPU
        worker.Start();
    }

    public static string SettingsPath() { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini"); }

    void DetectRefreshRate()
    {
        try
        {
            IntPtr dc = GetDC(IntPtr.Zero);
            int hz = GetDeviceCaps(dc, 116);
            ReleaseDC(IntPtr.Zero, dc);
            if (hz > 1) refreshHz = hz;
        }
        catch { }
        // AUTO: max. 120/s. Mehr bringt im Clash nichts und laesst schwache PCs laggen,
        // weil Roblox oft weniger FPS schafft als der Monitor Hz hat.
        autoMax = Math.Max(60, Math.Min(240, refreshHz)); // Roblox schafft max. 240 FPS
        presetCps[0] = autoMax;
    }

    // ================= UI-Aufbau =================
    void BuildUi()
    {
        Panel bar = new Panel { Bounds = new Rectangle(1, 1, 378, 46), BackColor = Theme.BG };
        PictureBox logo = new PictureBox { Image = AppIcon.Draw(28), Bounds = new Rectangle(18, 9, 28, 28), BackColor = Theme.BG, SizeMode = PictureBoxSizeMode.Normal };
        Label t1 = new Label { Text = "BLADE BALL", Font = new Font("Segoe UI Semibold", 15f, FontStyle.Regular), AutoSize = true, Location = new Point(52, 10), BackColor = Theme.BG };
        int w1 = TextRenderer.MeasureText(t1.Text, t1.Font).Width;
        Label t2 = new Label { Text = "MACRO", Font = new Font("Segoe UI Light", 15f, FontStyle.Regular), ForeColor = Theme.TEXT2, AutoSize = true, Location = new Point(48 + w1, 10), BackColor = Theme.BG };
        Label ver = new Label { Text = "v" + VERSION_NAME, ForeColor = Theme.MUTED, Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Regular), AutoSize = true, Location = new Point(52 + w1 + TextRenderer.MeasureText("MACRO", t2.Font).Width, 18), BackColor = Theme.BG };
        Button min = Theme.Btn("—", new Rectangle(294, 9, 34, 28), Theme.GHOST, 10f);
        Button close = Theme.Btn("✕", new Rectangle(334, 9, 34, 28), Theme.GHOST, 10f);
        ((RoundButton)close).HoverColor = Theme.OFF;
        ((RoundButton)close).Outline = false;
        ((RoundButton)min).Outline = false;
        min.Click += delegate { WindowState = FormWindowState.Minimized; };
        close.Click += delegate { Close(); };
        bar.Controls.AddRange(new Control[] { logo, t1, t2, ver, min, close });
        foreach (Control c in new Control[] { bar, logo, t1, t2, ver }) c.MouseDown += DragWindow;

        // Status (immer sichtbar)
        status = new RoundPanel { Bounds = new Rectangle(20, 52, 340, 104) };
        dot = new Dot { Bounds = new Rectangle(18, 22, 18, 18), BackColor = Theme.CARD };
        statusText = new Label { Font = new Font("Segoe UI Semibold", 21f, FontStyle.Regular), BackColor = Theme.CARD, AutoSize = false, Bounds = new Rectangle(40, 10, 190, 42) };
        hintText = new Label { ForeColor = Theme.TEXT2, BackColor = Theme.CARD, AutoSize = false, Bounds = new Rectangle(18, 54, 310, 22) };
        statsText = new Label { ForeColor = Theme.MUTED, BackColor = Theme.CARD, Font = new Font("Segoe UI", 8.5f), AutoSize = false, Bounds = new Rectangle(18, 76, 310, 20) };
        armBtn = Theme.Btn("ON", new Rectangle(246, 16, 78, 34), Theme.ACCENT, 11f);
        ((RoundButton)armBtn).Radius = 17;
        armBtn.Click += delegate { SetArmed(!armed); };
        statsText.Click += delegate { if (updState == 3) InstallOnline(); };
        status.Controls.AddRange(new Control[] { dot, statusText, hintText, statsText, armBtn });

        // Tabs
        RoundPanel tabs = new RoundPanel { Bounds = new Rectangle(20, 166, 340, 46) };
        tabMacro = Theme.Btn("MACRO", new Rectangle(6, 6, 106, 34), Theme.ACCENT, 10f);
        tabBoost = Theme.Btn("BOOST", new Rectangle(117, 6, 106, 34), Theme.GHOST, 10f);
        tabExtras = Theme.Btn("EXTRAS", new Rectangle(228, 6, 106, 34), Theme.GHOST, 10f);
        tabMacro.Click += delegate { ShowPage(0); };
        tabBoost.Click += delegate { ShowPage(1); };
        tabExtras.Click += delegate { ShowPage(2); };
        foreach (Button tb in new Button[] { tabMacro, tabBoost, tabExtras }) ((RoundButton)tb).Outline = false;
        tabs.Controls.AddRange(new Control[] { tabMacro, tabBoost, tabExtras });

        // ---------- Seite MACRO ----------
        macroPage = new Panel { Bounds = new Rectangle(20, 222, 340, 340), BackColor = Theme.BG };

        RoundPanel modeCard = new RoundPanel { Bounds = new Rectangle(0, 0, 340, 60) };
        for (int i = 0; i < 2; i++)
        {
            int idx = i;
            modeBtns[i] = Theme.Btn(ModeNames[i], new Rectangle(10 + i * 162, 10, 158, 40), Theme.CHIP, 10f);
            modeBtns[i].Click += delegate { SetMode(idx); };
            ((RoundButton)modeBtns[i]).Outline = false;
            modeCard.Controls.Add(modeBtns[i]);
        }

        RoundPanel hc = new RoundPanel { Bounds = new Rectangle(0, 72, 164, 88) };
        hc.Controls.Add(Theme.Caption("HOTKEY", 16, 10));
        hotChip = Theme.Btn("E", new Rectangle(14, 34, 136, 42), Theme.CHIP, 15f);
        hotChip.Click += delegate { StartCapture(2); };
        hc.Controls.Add(hotChip);

        RoundPanel sc = new RoundPanel { Bounds = new Rectangle(176, 72, 164, 88) };
        sc.Controls.Add(Theme.Caption("SPAM KEY", 16, 10));
        spamChip = Theme.Btn("F", new Rectangle(14, 34, 136, 42), Theme.CHIP, 15f);
        spamChip.Click += delegate { StartCapture(1); };
        sc.Controls.Add(spamChip);

        RoundPanel perf = new RoundPanel { Bounds = new Rectangle(0, 172, 340, 96) };
        perf.Controls.Add(Theme.Caption("PERFORMANCE", 16, 12));
        perfValue = new Label { ForeColor = Theme.ACCENT, BackColor = Theme.CARD, Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Regular), AutoSize = false, TextAlign = ContentAlignment.MiddleRight, Bounds = new Rectangle(120, 8, 204, 22) };
        perf.Controls.Add(perfValue);
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            presetBtns[i] = Theme.Btn(PresetNames[i], new Rectangle(10 + i * 80, 42, 76, 42), Theme.CHIP, 9.5f);
            presetBtns[i].Click += delegate { preset = idx; RefreshUi(true); SaveSettings(); };
            ((RoundButton)presetBtns[i]).Outline = false;
            perf.Controls.Add(presetBtns[i]);
        }

        RoundPanel h2Card = new RoundPanel { Bounds = new Rectangle(0, 280, 340, 60) };
        h2Card.Controls.Add(Theme.Caption("2ND HOTKEY", 16, 12));
        scanText = new Label { Text = "e.g. mouse side button", ForeColor = Theme.MUTED, BackColor = Theme.CARD, Font = new Font("Segoe UI", 8f), AutoSize = false, Bounds = new Rectangle(16, 32, 160, 18) };
        hot2Chip = Theme.Btn("NONE", new Rectangle(190, 12, 136, 36), Theme.CHIP, 11f);
        hot2Chip.Click += delegate { StartCapture(3); };
        h2Card.Controls.AddRange(new Control[] { scanText, hot2Chip });
        perfValue.Cursor = Cursors.Hand;
        perfValue.Click += delegate { StartScan(); };
        macroPage.Controls.AddRange(new Control[] { modeCard, hc, sc, perf, h2Card });

        // ---------- Seite BOOST ----------
        boostPage = new Panel { Bounds = new Rectangle(20, 222, 340, 340), BackColor = Theme.BG, Visible = false };
        fpsCard = new RoundPanel { Bounds = new Rectangle(0, 0, 340, 132) };
        Label fpsTitle = new Label { Text = "FPS MODE", Font = new Font("Segoe UI Semibold", 15f, FontStyle.Regular), BackColor = Theme.CARD, AutoSize = true, Location = new Point(18, 12) };
        fpsModeInfo = new Label { ForeColor = Theme.MUTED, BackColor = Theme.CARD, Font = new Font("Segoe UI", 8.5f), AutoSize = false, Bounds = new Rectangle(19, 42, 220, 18) };
        fpsModeList = new Label { ForeColor = Theme.TEXT2, BackColor = Theme.CARD, Font = new Font("Segoe UI", 8.5f), AutoSize = false, Bounds = new Rectangle(19, 66, 310, 58),
            Text = "Max power plan  ·  Windows Game Mode\nFast GPU for Roblox  ·  Background recording off\nCloses junk apps  ·  Slows down background apps" };
        fpsModeBtn = Theme.Btn("OFF", new Rectangle(248, 16, 78, 34), Theme.CHIP, 11f);
        ((RoundButton)fpsModeBtn).Radius = 17;
        fpsModeBtn.Click += delegate { SetFpsMode(!fpsMode); };
        fpsCard.Controls.AddRange(new Control[] { fpsTitle, fpsModeInfo, fpsModeList, fpsModeBtn });
        boostPage.Controls.Add(fpsCard);

        Label dummy;
        boostPage.Controls.Add(Row(144, "Auto-Minimize", "Hides when Roblox is focused", out minBtn, out dummy));
        boostPage.Controls.Add(Row(204, "Online Update", "", out updBtn, out updDesc));
        updBtn.Text = "CHECK";
        updBtn.BackColor = Theme.ACCENT;
        minBtn.Click += delegate { autoMin = !autoMin; RefreshUi(true); SaveSettings(); };
        updBtn.Click += delegate { if (updState == 3) InstallOnline(); else CheckOnline(); };

        updateBtn = Theme.Btn("Rebuild from local .cs", new Rectangle(0, 272, 340, 32), Theme.CHIP, 9f);
        updateBtn.ForeColor = Theme.MUTED;
        updateBtn.Click += delegate { DoUpdate(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BladeBallMacro.cs")); };
        boostPage.Controls.Add(updateBtn);

        // ---------- Seite EXTRAS ----------
        extrasPage = new Panel { Bounds = new Rectangle(20, 222, 340, 340), BackColor = Theme.BG, Visible = false };
        Label d2;
        extrasPage.Controls.Add(Row(0, "Chat Protection", "Pauses while you type (/ or -)", out chatBtn, out d2));
        RoundPanel ovRow = Row(60, "In-Game Overlay", "Status pill in the top bar", out ovlBtn, out d2);
        d2.Width = 170;
        ovlPosBtn = Theme.Btn(PosArrows[0], new Rectangle(196, 12, 58, 32), Theme.CHIP, 12f);
        ((RoundButton)ovlPosBtn).Radius = 16;
        ovRow.Controls.Add(ovlPosBtn);
        extrasPage.Controls.Add(ovRow);
        extrasPage.Controls.Add(Row(120, "Sound Feedback", "Beeps on toggle, chat and panic", out sndBtn, out d2));
        extrasPage.Controls.Add(Row(180, "Panic Key (F8)", "F8 turns the whole macro on/off", out panicBtn, out d2));

        chatBtn.Click += delegate { chatProtect = !chatProtect; chatOpen = false; RefreshUi(true); SaveSettings(); };
        ovlBtn.Click += delegate { overlayOn = !overlayOn; RefreshUi(true); SaveSettings(); };
        ovlPosBtn.Click += delegate { overlayPos = (overlayPos + 1) % 3; RefreshUi(true); SaveSettings(); };
        sndBtn.Click += delegate { soundOn = !soundOn; RefreshUi(true); SaveSettings(); if (soundOn) Play(sndOn); };
        panicBtn.Click += delegate { panicOn = !panicOn; RefreshUi(true); SaveSettings(); };

        Controls.AddRange(new Control[] { bar, status, tabs, macroPage, boostPage, extrasPage });
    }

    RoundPanel Row(int y, string title, string desc, out Button toggle, out Label descLabel)
    {
        RoundPanel p = new RoundPanel { Bounds = new Rectangle(0, y, 340, 56) };
        Label t = new Label { Text = title, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Regular), BackColor = Theme.CARD, AutoSize = true, Location = new Point(16, 8) };
        descLabel = new Label { Text = desc, ForeColor = Theme.MUTED, Font = new Font("Segoe UI", 8.5f), BackColor = Theme.CARD, AutoSize = false, Bounds = new Rectangle(16, 30, 236, 18) };
        toggle = Theme.Btn("OFF", new Rectangle(262, 12, 64, 32), Theme.CHIP, 9.5f);
        ((RoundButton)toggle).Radius = 16;
        p.Controls.AddRange(new Control[] { t, descLabel, toggle });
        return p;
    }

    void ShowPage(int page)
    {
        macroPage.Visible = page == 0;
        boostPage.Visible = page == 1;
        extrasPage.Visible = page == 2;
        SetBack(tabMacro, page == 0 ? Theme.ACCENT : Theme.GHOST);
        SetBack(tabBoost, page == 1 ? Theme.ACCENT : Theme.GHOST);
        SetBack(tabExtras, page == 2 ? Theme.ACCENT : Theme.GHOST);
    }

    static void SetToggle(Button b, bool on)
    {
        SetText(b, on ? "ON" : "OFF");
        SetBack(b, on ? Theme.ACCENT : Theme.CHIP);
    }

    // ---------- PC-Boost ----------
    static string RunPowercfg(string args)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("powercfg", args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            using (Process p = Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);
                return o;
            }
        }
        catch { return ""; }
    }

    static string FindGuid(string s)
    {
        Match m = Regex.Match(s, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        return m.Success ? m.Value.ToLower() : null;
    }

    void EnableBoost()
    {
        if (boostActive) return;
        string cur = FindGuid(RunPowercfg("/getactivescheme"));
        if (cur == null) { boostFailed = true; return; }
        if (cur == HIGH_PERF) { boostActive = true; boostFailed = false; return; } // war schon an
        prevScheme = cur;
        RunPowercfg("/setactive " + HIGH_PERF);
        if (FindGuid(RunPowercfg("/getactivescheme")) != HIGH_PERF)
        {
            // Plan ist versteckt (z. B. Laptops) -> Kopie anlegen und aktivieren
            if (RunPowercfg("/list").ToLower().IndexOf(OWN_PLAN) < 0)
                RunPowercfg("-duplicatescheme " + HIGH_PERF + " " + OWN_PLAN);
            RunPowercfg("/setactive " + OWN_PLAN);
            string now = FindGuid(RunPowercfg("/getactivescheme"));
            if (now == null || now == prevScheme) { boostFailed = true; prevScheme = null; return; }
        }
        boostActive = true;
        boostFailed = false;
    }

    void DisableBoost()
    {
        if (prevScheme != null) RunPowercfg("/setactive " + prevScheme);
        prevScheme = null;
        boostActive = false;
    }

    // ---------- Ton-Feedback (kurze Toene, im Speicher erzeugt) ----------
    static byte[] Tone(int freq, int ms, double vol, int freq2)
    {
        int rate = 44100;
        int n = rate * ms / 1000;
        MemoryStream m = new MemoryStream();
        BinaryWriter w = new BinaryWriter(m);
        w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2);
        w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
        w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write(Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
        int fade = rate * 6 / 1000;
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            double f = freq + (freq2 - freq) * (double)i / n; // leichter Tonverlauf
            phase += 2 * Math.PI * f / rate;
            double env = Math.Min(1.0, Math.Min((double)i / fade, (double)(n - i) / fade));
            w.Write((short)(Math.Sin(phase) * env * vol * short.MaxValue));
        }
        w.Flush();
        return m.ToArray();
    }

    void BuildSounds()
    {
        try
        {
            sndOn = new SoundPlayer(new MemoryStream(Tone(880, 70, 0.22, 1320)));
            sndOff = new SoundPlayer(new MemoryStream(Tone(990, 80, 0.22, 600)));
            sndChat = new SoundPlayer(new MemoryStream(Tone(520, 60, 0.18, 520)));
            sndOn.Load(); sndOff.Load(); sndChat.Load();
        }
        catch { }
    }

    void Play(SoundPlayer p)
    {
        if (!soundOn || p == null) return;
        ThreadPool.QueueUserWorkItem(delegate { try { p.Play(); } catch { } });
    }

    // ---------- Overlay ----------
    void OverlayTick()
    {
        bool front = RobloxForeground();

        // Chat-Schutz: Sicherheits-Timeout und zuruecksetzen, wenn Roblox nicht mehr vorne ist
        if (chatOpen && (!front || clock.ElapsedTicks - Interlocked.Read(ref chatSince) > Stopwatch.Frequency * 30))
            chatOpen = false;

        if (!overlayOn || !front)
        {
            if (overlay.Visible) overlay.Hide();
            return;
        }

        string t;
        Color c;
        if (!armed) { t = "OFF"; c = Theme.MUTED; }
        else if (chatOpen) { t = "CHAT · paused"; c = Theme.WARN; }
        else if (running)
        {
            long now = clock.ElapsedMilliseconds;
            if (now - ovLastMs >= 400)
            {
                long cnt = Interlocked.Read(ref count);
                if (ovLastMs > 0) ovRate = (int)((cnt - ovLastCount) * 1000 / Math.Max(1, now - ovLastMs));
                ovLastCount = cnt;
                ovLastMs = now;
            }
            t = "SPAMMING" + (ovRate > 0 ? " · " + ovRate + "/s" : "");
            c = Theme.OK;
        }
        else { t = "READY · " + KeyName(hotKey); c = Theme.ACCENT; ovLastMs = 0; ovRate = 0; }
        overlay.SetState(t, c);

        Rectangle b = Screen.FromHandle(GetForegroundWindow()).Bounds;
        // In Blade Ball sind unten Block/Ability, Ping und Crates -> nur oben ist Platz
        int x, y;
        if (overlayPos == 1) { x = b.Left + 16; y = b.Top + 72; }                            // unter dem Roblox-Menue
        else if (overlayPos == 2) { x = b.Right - overlay.Width - 16; y = b.Top + 72; }      // unter EXTRA/Stats
        else { x = b.Left + (b.Width - overlay.Width) / 2; y = b.Top + 18; }                 // freie Mitte der Roblox-Leiste
        if (overlay.Left != x || overlay.Top != y) overlay.Location = new Point(x, y);
        if (!overlay.Visible) overlay.Show();
    }

    // ---------- FPS MODE ----------
    // Hintergrund-Programme, die niemand zum Spielen braucht -> werden geschlossen
    static readonly string[] JunkApps = {
        "onedrive", "ms-teams", "teams", "msteams", "widgets", "phoneexperiencehost", "yourphone",
        "epicgameslauncher", "epicwebhelper", "eadesktop", "battle.net", "riotclientservices", "riotclientux",
        "upc", "ubisoftconnect", "overwolf", "medal", "gamebar", "gamebarftserver", "xboxpcapp",
        "nvidia share", "ccxprocess", "creative cloud", "adobeipcbroker", "coresync", "acrotray",
        "capcut", "wallpaper32", "wallpaper64", "lively", "copilot", "cortana" };

    // Programme, die offen bleiben, aber weniger CPU bekommen als Roblox
    static readonly HashSet<string> SlowApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "discord", "spotify",
        "steam", "steamwebhelper", "code", "msedgewebview2" };

    void SetFpsMode(bool on)
    {
        fpsMode = on;
        if (on)
        {
            EnableBoost();
            boostOn = boostActive;
            ApplyFps();
            ThreadPool.QueueUserWorkItem(delegate { CloseJunk(); SlowBackground(); });
        }
        else
        {
            DisableBoost();
            boostOn = false;
            RemoveFps();
            RestoreBackground();
            closedApps = 0;
        }
        RefreshUi(true);
        SaveSettings();
    }

    void CloseJunk()
    {
        int session, n = 0;
        using (Process me = Process.GetCurrentProcess()) session = me.SessionId;
        List<Process> left = new List<Process>();
        foreach (string name in JunkApps)
        {
            foreach (Process p in Process.GetProcessesByName(name))
            {
                try
                {
                    if (p.SessionId != session) { p.Dispose(); continue; }
                    if (p.MainWindowHandle != IntPtr.Zero) p.CloseMainWindow();
                    left.Add(p);
                    n++;
                }
                catch { p.Dispose(); }
            }
        }
        if (left.Count > 0) Thread.Sleep(2000);
        foreach (Process p in left)
        {
            try { if (!p.HasExited) p.Kill(); } catch { }
            p.Dispose();
        }
        closedApps += n;
    }

    // Senkt die Prioritaet von Hintergrund-Apps (nie Roblox) -> Roblox bekommt die CPU zuerst
    void SlowBackground()
    {
        int session;
        using (Process me = Process.GetCurrentProcess()) session = me.SessionId;
        lock (slowLock)
        {
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (p.SessionId == session && SlowApps.Contains(p.ProcessName) && !slowed.ContainsKey(p.Id))
                    {
                        ProcessPriorityClass old = p.PriorityClass;
                        if (old == ProcessPriorityClass.Normal || old == ProcessPriorityClass.AboveNormal)
                        {
                            p.PriorityClass = ProcessPriorityClass.BelowNormal;
                            slowed[p.Id] = old;
                        }
                    }
                }
                catch { }
                finally { p.Dispose(); }
            }
            // beendete Prozesse aus der Liste werfen
            List<int> dead = new List<int>();
            foreach (int id in slowed.Keys)
            {
                try { using (Process p = Process.GetProcessById(id)) { if (p.HasExited) dead.Add(id); } }
                catch { dead.Add(id); }
            }
            foreach (int id in dead) slowed.Remove(id);
            slowedApps = slowed.Count;
        }
    }

    void RestoreBackground()
    {
        lock (slowLock)
        {
            foreach (KeyValuePair<int, ProcessPriorityClass> kv in slowed)
            {
                try { using (Process p = Process.GetProcessById(kv.Key)) p.PriorityClass = kv.Value; } catch { }
            }
            slowed.Clear();
            slowedApps = 0;
        }
    }

    // ---------- FPS-Tweaks ----------
    const string RK_GAMEBAR = @"Software\Microsoft\GameBar";
    const string RK_GCS = @"System\GameConfigStore";
    const string RK_DVR = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";
    const string RK_GPU = @"Software\Microsoft\DirectX\UserGpuPreferences";
    const string GPU_HIGH = "GpuPreference=2;";

    static int GetDword(string key, string name)
    {
        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(key))
        {
            if (k == null) return -1;
            object v = k.GetValue(name);
            return v is int ? (int)v : -1;
        }
    }

    static void SetDword(string key, string name, int value)
    {
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(key)) k.SetValue(name, value, RegistryValueKind.DWord);
    }

    static void RestoreDword(string key, string name, int prev)
    {
        if (prev >= 0) SetDword(key, name, prev);
        else if (prev == -1)
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(key, true)) { if (k != null) k.DeleteValue(name, false); }
    }

    // Alle installierten Roblox-Versionen (normal, Bloxstrap, Programme)
    static List<string> RobloxExes()
    {
        List<string> list = new List<string>();
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] roots = {
            Path.Combine(local, @"Roblox\Versions"),
            Path.Combine(local, @"Bloxstrap\Versions"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Roblox\Versions")
        };
        foreach (string r in roots)
        {
            try
            {
                if (!Directory.Exists(r)) continue;
                foreach (string d in Directory.GetDirectories(r))
                {
                    string exe = Path.Combine(d, "RobloxPlayerBeta.exe");
                    if (File.Exists(exe)) list.Add(exe);
                }
            }
            catch { }
        }
        return list;
    }

    void ApplyFps()
    {
        try
        {
            if (prevGameMode == -2)
            {
                prevGameMode = GetDword(RK_GAMEBAR, "AutoGameModeEnabled");
                prevDvr = GetDword(RK_GCS, "GameDVR_Enabled");
                prevCapture = GetDword(RK_DVR, "AppCaptureEnabled");
            }
            SetDword(RK_GAMEBAR, "AutoGameModeEnabled", 1); // Windows-Spielmodus an
            SetDword(RK_GCS, "GameDVR_Enabled", 0);         // Hintergrund-Aufnahme aus
            SetDword(RK_DVR, "AppCaptureEnabled", 0);

            int n = 0;
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RK_GPU))
            {
                foreach (string exe in RobloxExes())
                {
                    if ((k.GetValue(exe) as string) != GPU_HIGH) k.SetValue(exe, GPU_HIGH, RegistryValueKind.String);
                    n++;
                }
            }
            fpsOn = true;
            fpsInfo = "Active · GPU set for " + n + " Roblox · restart Roblox";
        }
        catch (Exception ex) { fpsOn = false; fpsInfo = ""; MessageBox.Show("FPS Tweaks failed:\n" + ex.Message, "BladeBall Macro"); }
    }

    void RemoveFps()
    {
        try
        {
            RestoreDword(RK_GAMEBAR, "AutoGameModeEnabled", prevGameMode);
            RestoreDword(RK_GCS, "GameDVR_Enabled", prevDvr);
            RestoreDword(RK_DVR, "AppCaptureEnabled", prevCapture);
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RK_GPU, true))
            {
                if (k != null)
                    foreach (string exe in RobloxExes())
                        if ((k.GetValue(exe) as string) == GPU_HIGH) k.DeleteValue(exe, false);
            }
        }
        catch { }
        prevGameMode = prevDvr = prevCapture = -2;
        fpsOn = false;
    }

    void DragWindow(object s, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, 2, 0);
    }

    // Glas-Fenster immer genau hinter dem Macro halten; minimiert = aus (kostet dann nichts)
    void SyncGlass()
    {
        if (backdrop == null) return;
        if (WindowState == FormWindowState.Minimized || !Visible)
        {
            if (backdrop.Visible) backdrop.Hide();
            return;
        }
        if (!backdrop.Visible) backdrop.Show();
        SetWindowPos(backdrop.Handle, Handle, Left, Top, Width, Height, 0x0010); // SWP_NOACTIVATE, direkt hinter dem Macro
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath gp = Glass.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 12))
        using (Pen p = new Pen(Color.FromArgb(60, 255, 255, 255))) e.Graphics.DrawPath(p, gp);
        e.Graphics.SmoothingMode = SmoothingMode.None;
        Rectangle line = new Rectangle(20, 47, 340, 2);
        using (LinearGradientBrush gb = new LinearGradientBrush(line, Color.FromArgb(160, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Horizontal))
            e.Graphics.FillRectangle(gb, line);
    }

    static void SetText(Control c, string t) { if (c.Text != t) c.Text = t; }
    static void SetBack(Control c, Color col) { if (c.BackColor != col) c.BackColor = col; }
    static void SetFore(Control c, Color col) { if (c.ForeColor != col) c.ForeColor = col; }

    void RefreshUi(bool full)
    {
        Color sc;
        if (!armed) { SetText(statusText, "PAUSED"); sc = Theme.MUTED; }
        else if (chatOpen) { SetText(statusText, "CHAT"); sc = Theme.WARN; }
        else if (running) { SetText(statusText, "SPAMMING"); sc = Theme.OK; }
        else { SetText(statusText, "READY"); sc = Theme.ACCENT; }
        SetFore(statusText, sc);
        dot.DotColor = sc;
        if (!full) return;

        SetText(armBtn, armed ? "ON" : "OFF");
        SetBack(armBtn, armed ? Theme.ACCENT : Theme.CHIP);
        for (int i = 0; i < 2; i++) SetBack(modeBtns[i], i == mode ? Theme.ACCENT : Theme.CHIP);
        for (int i = 0; i < 4; i++) SetBack(presetBtns[i], i == preset ? Theme.ACCENT : Theme.CHIP);

        SetToggle(minBtn, autoMin);
        SetText(fpsModeBtn, fpsMode ? "ON" : "OFF");
        SetBack(fpsModeBtn, fpsMode ? Theme.ACCENT : Theme.CHIP);
        fpsCard.AccentBar = Color.Empty;
        SetFore(fpsModeList, fpsMode ? Theme.TEXT2 : Theme.MUTED);
        SetText(fpsModeInfo, !fpsMode ? "One click for max FPS" :
            ("Active · " + closedApps + " closed · " + slowedApps + " slowed" + (boostFailed ? " · no power plan" : "")));
        SetToggle(chatBtn, chatProtect);
        SetToggle(ovlBtn, overlayOn);
        SetText(ovlPosBtn, PosArrows[overlayPos]);
        SetToggle(sndBtn, soundOn);
        SetToggle(panicBtn, panicOn);

        string pv = presetCps[preset] + "/s · " + PresetHints[preset];
        if (preset == 0) pv = scanning ? "scanning..." : "AUTO " + autoCps + "/s · rescan";
        SetText(perfValue, pv);
        if (captureTarget != 2) SetText(hotChip, KeyName(hotKey));
        if (captureTarget != 3) SetText(hot2Chip, hotKey2 == Keys.None ? "NONE" : KeyName(hotKey2));
        if (captureTarget != 1) SetText(spamChip, KeyName(spamKey));

        string h = KeyName(hotKey) + (hotKey2 != Keys.None ? "/" + KeyName(hotKey2) : ""), s = KeyName(spamKey);
        if (mode == 0) SetText(hintText, "Hold " + h + " → spams " + s);
        else SetText(hintText, "Press " + h + " → toggles " + s + " spam");
    }

    void Tick()
    {
        lock (runLock) { if (!running && periodOn) { timeEndPeriod(1); periodOn = false; } }
        bool robloxFront = RobloxForeground(); // haelt den Cache aktuell -> Hotkey reagiert sofort
        if (fpsMode && clock.ElapsedMilliseconds - lastSweepMs > 15000)
        {
            lastSweepMs = clock.ElapsedMilliseconds;
            ThreadPool.QueueUserWorkItem(delegate { if (fpsMode) SlowBackground(); });
        }
        if (robloxFront && !scannedInRoblox && !scanning && !running) { scannedInRoblox = true; StartScan(); }
        if (autoMin && WindowState != FormWindowState.Minimized && robloxFront)
            WindowState = FormWindowState.Minimized;
        if (WindowState == FormWindowState.Minimized) { tickWatch.Restart(); lastCount = Interlocked.Read(ref count); return; } // minimiert = keine UI-Arbeit
        long c = Interlocked.Read(ref count);
        double secs = tickWatch.Elapsed.TotalSeconds;
        tickWatch.Restart();
        double cps = secs > 0 ? (c - lastCount) / secs : 0;
        lastCount = c;
        cpsSmooth = cpsSmooth * 0.6 + cps * 0.4;
        if (cpsSmooth < 1) cpsSmooth = 0;
        if (preset == 0) SetText(perfValue, scanning ? "scanning..." : "AUTO " + autoCps + "/s · rescan");

        RefreshUpdateRow();
        if (fpsMode) SetText(fpsModeInfo, "Active · " + closedApps + " closed · " + slowedApps + " slowed" + (boostFailed ? " · no power plan" : ""));

        if (running) dot.Glow = 50 + (int)(70 * Math.Abs(Math.Sin(clock.ElapsedMilliseconds / 160.0)));
        else dot.Glow = 60;
        if (UPDATE_URL.Length > 0 && !running && updState != 1 && updState != 3 && clock.ElapsedMilliseconds - lastCheckMs > 20 * 60 * 1000)
        {
            lastCheckMs = clock.ElapsedMilliseconds;
            CheckOnline(); // alle 20 Minuten still nachschauen
        }
        if (updState == 3)
        {
            SetText(statsText, "⬆  v" + remoteName + " available – click here to update");
            SetFore(statsText, Theme.OK);
            statsText.Cursor = Cursors.Hand;
            return;
        }
        SetFore(statsText, Theme.MUTED);
        statsText.Cursor = Cursors.Default;
        if (running)
            SetText(statsText, "Live " + (int)cpsSmooth + "/s   ·   clash " + ((clock.ElapsedTicks - Interlocked.Read(ref runStart)) / (double)Stopwatch.Frequency).ToString("0.0") + " s");
        else
            SetText(statsText, "Clashes " + clashes + "   ·   Best " + bestClash.ToString("0.0") + " s   ·   Response " + (respMs >= 0 ? respMs.ToString("0.00") + " ms" : "—"));
        RefreshUi(false);
    }

    static string KeyName(Keys k)
    {
        string s = k.ToString();
        if (s.Length == 2 && s[0] == 'D' && char.IsDigit(s[1])) return s.Substring(1);
        if (s == "XButton1") return "MOUSE4";
        if (s == "XButton2") return "MOUSE5";
        if (s == "MButton") return "MOUSE3";
        return s.ToUpper();
    }

    // ================= Logik =================
    void SetRunning(bool on)
    {
        lock (runLock)
        {
            bool was = running;
            running = on;
            if (on)
            {
                if (!was)
                {
                    Interlocked.Exchange(ref runStart, clock.ElapsedTicks);
                    respPending = true;
                    if (autoCps < autoBest) autoCps = autoBest; // volle Wucht: nicht mit gedrosselter Rate starten
                }
                if (!periodOn) { timeBeginPeriod(1); periodOn = true; }
                go.Set();
            }
            else
            {
                go.Reset();
                if (periodOn) { timeEndPeriod(1); periodOn = false; }
                if (was) RecordClash();
            }
        }
    }

    // Vom Spam-Thread aus stoppen (Loslassen verpasst)
    void StopFromWorker()
    {
        lock (runLock)
        {
            if (!running) return;
            running = false;
            go.Reset();
            RecordClash();
        }
    }

    void RecordClash()
    {
        double secs = (clock.ElapsedTicks - Interlocked.Read(ref runStart)) / (double)Stopwatch.Frequency;
        if (secs < 0.15) return; // kurzes Antippen zaehlt nicht
        clashes++;
        if (secs > bestClash) bestClash = secs;
    }

    void SetArmed(bool on) { armed = on; if (!on) SetRunning(false); Play(on ? sndOn : sndOff); RefreshUi(true); }
    void SetMode(int m) { mode = m; hotDown = false; hd1 = hd2 = false; SetRunning(false); RefreshUi(true); SaveSettings(); }

    void StartCapture(int target)
    {
        SetRunning(false);
        captureTarget = target;
        capturing = true;
        if (target == 1) spamChip.Text = "...";
        else if (target == 3) hot2Chip.Text = "...";
        else hotChip.Text = "...";
        ActiveControl = null;
        Focus();
    }

    void OnKeyCapture(object sender, KeyEventArgs e)
    {
        if (!capturing) return;
        e.SuppressKeyPress = true;
        e.Handled = true;
        Keys k = e.KeyCode;
        if (captureTarget == 3 && (k == Keys.Back || k == Keys.Delete)) hotKey2 = Keys.None; // Entf/Backspace = kein 2. Hotkey
        else if (k != Keys.Escape && k != Keys.ShiftKey && k != Keys.ControlKey && k != Keys.Menu)
        {
            if (captureTarget == 1) { if (k != hotKey && k != hotKey2) { spamKey = k; RebuildKeys(); } }
            else if (captureTarget == 3) { if (k != spamKey && k != hotKey) hotKey2 = k; }
            else if (k != spamKey && k != hotKey2) hotKey = k;
        }
        captureTarget = 0;
        capturing = false;
        RefreshUi(true);
        SaveSettings();
    }

    void RebuildKeys()
    {
        ushort sc = (ushort)MapVirtualKey((uint)spamKey, 0);
        uint ext = IsExtended(spamKey) ? KEYEVENTF_EXTENDEDKEY : 0;
        KeyInputs k = new KeyInputs { Down = new INPUT[1], Up = new INPUT[1] };
        k.Down[0].type = 1; k.Down[0].U.ki.wScan = sc; k.Down[0].U.ki.dwFlags = KEYEVENTF_SCANCODE | ext;
        k.Up[0].type = 1; k.Up[0].U.ki.wScan = sc; k.Up[0].U.ki.dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP | ext;
        keys = k;
    }

    static bool IsExtended(Keys k)
    {
        switch (k)
        {
            case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down:
            case Keys.Insert: case Keys.Delete: case Keys.Home: case Keys.End:
            case Keys.PageUp: case Keys.PageDown: case Keys.RControlKey: case Keys.RMenu:
            case Keys.Divide: case Keys.NumLock:
                return true;
        }
        return false;
    }

    void HookThread()
    {
        hookProc = HookCb;
        mouseProc = MouseCb;
        IntPtr mod = GetModuleHandle(null);
        hook = SetWindowsHookEx(13, hookProc, mod, 0);
        mhook = SetWindowsHookEx(14, mouseProc, mod, 0);
        MSG m;
        while (GetMessage(out m, IntPtr.Zero, 0, 0) > 0) { }
    }

    void HandleHot(int which, bool down, bool up)
    {
        bool before = hd1 || hd2;
        if (down)
        {
            if (which == 1) { if (hd1) return; hd1 = true; } else { if (hd2) return; hd2 = true; }
            if (before) return; // anderer Hotkey haelt schon
            hotDown = true;
            Interlocked.Exchange(ref pressTick, clock.ElapsedTicks);
            if (chatOpen) return; // tippst gerade im Chat
            if (armed && (!onlyRoblox || RobloxForeground()))
            {
                if (mode == 0) SetRunning(true);
                else { SetRunning(!running); Play(running ? sndOn : sndOff); }
            }
        }
        else if (up)
        {
            if (which == 1) hd1 = false; else hd2 = false;
            if (hd1 || hd2) return; // anderer Hotkey haelt noch
            hotDown = false;
            if (mode == 0) SetRunning(false);
        }
    }

    IntPtr HookCb(int n, IntPtr w, IntPtr l)
    {
        if (n >= 0 && !capturing)
        {
            uint vk = (uint)Marshal.ReadInt32(l);
            int scan = Marshal.ReadInt32(l, 4);
            int flags = Marshal.ReadInt32(l, 8);
            int kmsg = w.ToInt32();
            bool kdown = kmsg == 0x100 || kmsg == 0x104;
            if ((flags & 0x10) == 0 && kdown)
            {
                // Panik-Taste F8: ganzes Macro an/aus
                if (panicOn && vk == 0x77 && hotKey != Keys.F8 && spamKey != Keys.F8)
                {
                    try { BeginInvoke((MethodInvoker)delegate { SetArmed(!armed); }); } catch { }
                }
                // Chat-Schutz: "/" oeffnet den Roblox-Chat, Enter/Esc schliesst ihn
                else if (chatProtect && RobloxForeground())
                {
                    if (!chatOpen && IsChatKey(vk, scan, flags))
                    {
                        chatOpen = true;
                        Interlocked.Exchange(ref chatSince, clock.ElapsedTicks);
                        if (running) SetRunning(false);
                        Play(sndChat);
                    }
                    else if (chatOpen && (vk == 0x0D || vk == 0x1B)) chatOpen = false;
                }
            }
            if ((flags & 0x10) == 0 && hotKey2 != Keys.None && vk == (uint)hotKey2)
                HandleHot(2, kdown, kmsg == 0x101 || kmsg == 0x105);
            if ((flags & 0x10) == 0 && vk == (uint)hotKey)
            {
                int msg = w.ToInt32();
                HandleHot(1, msg == 0x100 || msg == 0x104, msg == 0x101 || msg == 0x105);
            }
        }
        return CallNextHookEx(hook, n, w, l);
    }

    // "/" auf US-Tastatur, Shift+7 auf deutscher Tastatur, Numpad-"/"
    // Roblox oeffnet den Chat ueber die physische "/"-Taste der US-Tastatur.
    // Auf deutscher Tastatur ist das die "-"-Taste -> Scancode 0x35 deckt jedes Layout ab.
    static bool IsChatKey(uint vk, int scan, int flags)
    {
        bool extended = (flags & 0x01) != 0;
        if (scan == 0x35 && !extended) return true;          // "/" (US) bzw. "-" (DE)
        if (vk == 0xBF || vk == 0x6F) return true;           // OEM-"/" und Numpad-"/"
        if (vk == 0x37 && (GetAsyncKeyState(0x10) & 0x8000) != 0) return true; // Shift+7 (DE)
        return false;
    }

    // Maustasten als Hotkey: Mausrad-Klick, Seitentasten (Maus 4/5)
    IntPtr MouseCb(int n, IntPtr w, IntPtr l)
    {
        if (n >= 0)
        {
            int msg = w.ToInt32();
            int flags = Marshal.ReadInt32(l, 12);
            if (msg == 0x0201 && chatOpen) chatOpen = false; // Klick ins Spiel beendet das Tippen
            if ((flags & 1) == 0) // eigene simulierte Klicks ignorieren
            {
                Keys btn = Keys.None;
                bool down = false;
                if (msg == 0x020B || msg == 0x020C)
                {
                    int x = (Marshal.ReadInt32(l, 8) >> 16) & 0xFFFF;
                    btn = x == 1 ? Keys.XButton1 : Keys.XButton2;
                    down = msg == 0x020B;
                }
                else if (msg == 0x0207 || msg == 0x0208)
                {
                    btn = Keys.MButton;
                    down = msg == 0x0207;
                }
                if (btn != Keys.None)
                {
                    if (capturing && (captureTarget == 2 || captureTarget == 3))
                    {
                        if (down)
                        {
                            if (captureTarget == 2) { if (btn != hotKey2) hotKey = btn; }
                            else if (btn != hotKey) hotKey2 = btn;
                            capturing = false;
                            BeginInvoke((MethodInvoker)delegate { captureTarget = 0; RefreshUi(true); SaveSettings(); });
                        }
                        return (IntPtr)1;
                    }
                    if (!capturing && btn == hotKey) HandleHot(1, down, !down);
                    else if (!capturing && hotKey2 != Keys.None && btn == hotKey2) HandleHot(2, down, !down);
                }
            }
        }
        return CallNextHookEx(mhook, n, w, l);
    }

    bool RobloxForeground()
    {
        IntPtr fg = GetForegroundWindow();
        lock (fgLock)
        {
            if (fg == lastFg) return lastFgRoblox;
            bool r = false;
            try
            {
                uint pid;
                GetWindowThreadProcessId(fg, out pid);
                using (Process p = Process.GetProcessById((int)pid))
                    r = p.ProcessName.StartsWith("Roblox", StringComparison.OrdinalIgnoreCase);
            }
            catch { }
            lastFg = fg;
            lastFgRoblox = r;
            return r;
        }
    }

    // Präzises Warten: schläft, solange viel Zeit übrig ist, und wartet nur das letzte Stück aktiv
    // Hochpraeziser Windows-Timer (Win10 1803+): wacht auf ~0,1 ms genau auf,
    // ohne die CPU zu belasten. Fallback: alte Methode.
    static IntPtr GetTimer()
    {
        if (!timerTried)
        {
            timerTried = true;
            try { waitTimer = CreateWaitableTimerExW(IntPtr.Zero, IntPtr.Zero, 0x2, 0x1F0003); } catch { waitTimer = IntPtr.Zero; }
            if (waitTimer != IntPtr.Zero) hiResTimer = true;
        }
        return waitTimer;
    }

    void WaitUntil(long target, bool abortOnStop)
    {
        long freq = Stopwatch.Frequency;
        long spinZone = freq / 5000;             // letzte 0,2 ms
        long sleepThreshold = freq * 15 / 10000; // 1,5 ms (Fallback)
        IntPtr t = GetTimer();
        long rem;
        while ((rem = target - clock.ElapsedTicks) > 0)
        {
            if (abortOnStop && !running) return;
            if (t != IntPtr.Zero && rem > spinZone)
            {
                long due = -((rem - spinZone / 2) * 10000000L / freq); // relativ, 100-ns-Einheiten
                if (due >= 0) due = -1;
                if (SetWaitableTimer(t, ref due, 0, IntPtr.Zero, IntPtr.Zero, false)) { WaitForSingleObject(t, 50); continue; }
            }
            if (rem > sleepThreshold) Thread.Sleep(1);
            else Thread.Yield();
        }
    }

    int winPresses = 0;
    long winLate = 0;

    void Loop()
    {
        try { uint idx = 0; AvSetMmThreadCharacteristicsW("Games", ref idx); } catch { }
        int size = Marshal.SizeOf(typeof(INPUT));
        long freq = Stopwatch.Frequency;
        long next = 0;
        while (true)
        {
            go.Wait(); // 0 % CPU, solange nichts passiert
            if (!running) continue;

            if (mode == 0 && clock.ElapsedTicks - Interlocked.Read(ref runStart) > freq / 20
                && (GetAsyncKeyState((int)hotKey) & 0x8000) == 0
                && (hotKey2 == Keys.None || (GetAsyncKeyState((int)hotKey2) & 0x8000) == 0))
            {
                // Loslassen wurde verpasst (z. B. Alt+Tab, Windows-Popup) -> sicher stoppen
                hotDown = false;
                hd1 = hd2 = false;
                StopFromWorker();
                continue;
            }
            if (onlyRoblox && !RobloxForeground()) { Thread.Sleep(15); continue; }

            int p = preset;
            int cps = p == 0 ? autoCps : presetCps[p];
            long interval = freq / cps;
            long now = clock.ElapsedTicks;
            if (next < now || next > now + interval * 2) { next = now; winPresses = 0; winLate = 0; }
            long t0 = next;
            WaitUntil(t0, true);
            if (!running) continue;
            long late = clock.ElapsedTicks - t0;

            // Taste für eine halbe Periode halten -> Roblox erkennt jeden Druck sicher
            KeyInputs k = keys;
            SendInput((uint)k.Down.Length, k.Down, size);
            if (respPending)
            {
                respPending = false; // Zeit vom Tastendruck bis zum ersten gesendeten Parry
                respMs = (clock.ElapsedTicks - Interlocked.Read(ref pressTick)) * 1000.0 / freq;
            }
            long half = t0 + interval / 2;
            WaitUntil(half, false);
            late += Math.Max(0, clock.ElapsedTicks - half);
            SendInput((uint)k.Up.Length, k.Up, size);
            Interlocked.Increment(ref count);
            next = t0 + interval;

            // Smart-AUTO: wenn der PC nicht hinterherkommt -> langsamer, wenn locker -> schneller
            if (p == 0)
            {
                winLate += Math.Max(0, late);
                if (++winPresses >= 40)
                {
                    double ratio = (double)winLate / (40.0 * interval);
                    bool warm = clock.ElapsedTicks - Interlocked.Read(ref runStart) > freq * 3 / 10;
                    if (ratio > 0.30 && warm) autoCps = Math.Max(autoFloor, (int)(autoCps * 0.85));
                    else if (ratio < 0.10) autoCps = Math.Min(autoMax, autoCps + 15);
                    winPresses = 0;
                    winLate = 0;
                }
            }
        }
    }

    // ================= AUTO-Scan =================
    // Testet echte Spam-Raten von 240 abwaerts (ohne Tasten zu druecken) und nimmt
    // die hoechste, die dieser PC gerade sauber halten kann.
    static readonly int[] ScanRates = { 240, 200, 165, 144, 120, 100, 90, 60 };

    void StartScan()
    {
        if (scanning) return;
        scanning = true;
        Thread t = new Thread(Benchmark);
        t.IsBackground = true;
        t.Priority = ThreadPriority.Normal;
        t.Start();
    }

    void Benchmark()
    {
        scanning = true;
        pcInfo = "scanning...";
        Thread.Sleep(500);
        long freq = Stopwatch.Frequency;
        int cores = Environment.ProcessorCount;
        int best = 60;
        timeBeginPeriod(1);
        try
        {
            foreach (int rate in ScanRates)
            {
                if (rate > autoMax) continue;
                pcInfo = "testing " + rate + "/s...";
                long interval = freq / rate;
                long late = 0;
                int cycles = Math.Max(60, rate / 2); // ca. 0,5 s pro Stufe
                long t0 = clock.ElapsedTicks + interval;
                for (int c = 0; c < cycles; c++)
                {
                    WaitUntil(t0, false);
                    late += Math.Max(0, clock.ElapsedTicks - t0);
                    long half = t0 + interval / 2;
                    WaitUntil(half, false);
                    late += Math.Max(0, clock.ElapsedTicks - half);
                    t0 += interval;
                    long now = clock.ElapsedTicks;
                    if (t0 < now) { late += now - t0; t0 = now; }
                }
                double ratio = (double)late / (cycles * (double)interval);
                if (ratio < 0.10) { best = rate; break; }
            }
        }
        finally { timeEndPeriod(1); }

        autoFloor = Math.Min(best, cores <= 2 ? 60 : 90);
        autoCps = best;
        autoBest = best;
        pcInfo = "best: " + best + "/s · " + (hiResTimer ? "hi-res timer" : "std timer") + (RobloxRunning() ? " · in game" : "");
        scanning = false;
    }

    static bool RobloxRunning()
    {
        Process[] ps = Process.GetProcessesByName("RobloxPlayerBeta");
        bool r = ps.Length > 0;
        foreach (Process p in ps) p.Dispose();
        return r;
    }

    // ================= Einstellungen =================
    void LoadSettings()
    {
        try
        {
            if (!File.Exists(settingsPath)) return;
            foreach (string line in File.ReadAllLines(settingsPath))
            {
                string[] kv = line.Split(new char[] { '=' }, 2);
                if (kv.Length != 2) continue;
                int v;
                if (!int.TryParse(kv[1].Trim(), out v)) continue;
                switch (kv[0].Trim())
                {
                    case "spam": spamKey = (Keys)v; break;
                    case "hot": hotKey = (Keys)v; break;
                    case "mode": if (v >= 0 && v <= 1) mode = v; break;
                    case "preset2": if (v >= 0 && v <= 3) preset = v; break;
                    case "boost": boostOn = v == 1; break;
                    case "automin": autoMin = v == 1; break;
                    case "fps": fpsOn = v == 1; break;
                    case "fpsmode": fpsModeSaved = v == 1; fpsModeKnown = true; break;
                    case "pgm": prevGameMode = v; break;
                    case "pdvr": prevDvr = v; break;
                    case "pcap": prevCapture = v; break;
                    case "chat": chatProtect = v == 1; break;
                    case "ovl": overlayOn = v == 1; break;
                    case "ovlpos2": if (v >= 0 && v <= 2) overlayPos = v; break;
                    case "snd": soundOn = v == 1; break;
                    case "panic": panicOn = v == 1; break;
                    case "hot2": hotKey2 = (Keys)v; break;
                    case "wx": winX = v; break;
                    case "wy": winY = v; break;

                }
            }
        }
        catch { }
    }

    void SaveSettings()
    {
        try
        {
            File.WriteAllLines(settingsPath, new string[] {
                "key=" + (RememberKey ? ACCESS_KEY : ""),
                "spam=" + (int)spamKey,
                "hot=" + (int)hotKey,
                "mode=" + mode,
                "preset2=" + preset,
                "boost=" + (boostOn ? 1 : 0),
                "automin=" + (autoMin ? 1 : 0),
                "fps=" + (fpsOn ? 1 : 0),
                "fpsmode=" + (fpsMode ? 1 : 0),
                "pgm=" + prevGameMode,
                "pdvr=" + prevDvr,
                "pcap=" + prevCapture,
                "chat=" + (chatProtect ? 1 : 0),
                "ovl=" + (overlayOn ? 1 : 0),
                "ovlpos2=" + overlayPos,
                "snd=" + (soundOn ? 1 : 0),
                "panic=" + (panicOn ? 1 : 0),
                "hot2=" + (int)hotKey2,
                "wx=" + winX,
                "wy=" + winY
            });
        }
        catch { }
    }

    public static string ReadSavedKey()
    {
        try
        {
            string p = SettingsPath();
            if (!File.Exists(p)) return "";
            foreach (string line in File.ReadAllLines(p))
                if (line.StartsWith("key=")) return line.Substring(4).Trim();
        }
        catch { }
        return "";
    }

    // ================= Online-Update =================
    void CheckOnline()
    {
        if (UPDATE_URL.Length == 0 || updState == 1) return;
        updState = 1;
        Thread t = new Thread(delegate()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
                string s = null;
                // 1. GitHub-API: liefert sofort die neueste Version
                Match u = Regex.Match(UPDATE_URL, @"raw\.githubusercontent\.com/([^/]+)/([^/]+)/([^/]+)/(.+)$");
                if (u.Success)
                {
                    try
                    {
                        using (WebClient wc = new WebClient())
                        {
                            wc.Encoding = Encoding.UTF8;
                            wc.Headers[HttpRequestHeader.UserAgent] = "BladeBallMacro";
                            wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github.raw";
                            s = wc.DownloadString("https://api.github.com/repos/" + u.Groups[1].Value + "/" + u.Groups[2].Value
                                + "/contents/" + u.Groups[4].Value + "?ref=" + u.Groups[3].Value);
                        }
                    }
                    catch { s = null; }
                }
                // 2. Fallback: Raw-Link (kann bis zu 5 Minuten alt sein)
                if (s == null)
                {
                    using (WebClient wc = new WebClient())
                    {
                        wc.Encoding = Encoding.UTF8;
                        wc.Headers[HttpRequestHeader.CacheControl] = "no-cache";
                        s = wc.DownloadString(UPDATE_URL + "?t=" + DateTime.UtcNow.Ticks);
                    }
                }
                Match m = Regex.Match(s, @"public const int BUILD = (\d+);");
                if (!m.Success || s.IndexOf("class MacroForm") < 0) { updState = 4; return; }
                remoteBuild = int.Parse(m.Groups[1].Value);
                Match vn = Regex.Match(s, "VERSION_NAME = \"([^\"]+)\"");
                remoteName = vn.Success ? vn.Groups[1].Value : remoteBuild.ToString();
                if (remoteBuild > BUILD) { remoteSrc = s; updState = 3; }
                else updState = 2;
            }
            catch { updState = 4; }
        });
        t.IsBackground = true;
        t.Start();
    }

    void InstallOnline()
    {
        if (remoteSrc == null) return;
        try
        {
            // Neue Version im Programmordner ablegen, damit "Rebuild" nie auf eine alte Version zurueckfaellt
            string cs = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BladeBallMacro.cs");
            try { File.WriteAllText(cs, remoteSrc, new UTF8Encoding(true)); }
            catch
            {
                cs = Path.Combine(Path.GetTempPath(), "BladeBallMacro_update.cs");
                File.WriteAllText(cs, remoteSrc, new UTF8Encoding(true));
            }
            DoUpdate(cs);
        }
        catch (Exception ex) { MessageBox.Show("Update failed:\n" + ex.Message, "BladeBall Macro"); }
    }

    void RefreshUpdateRow()
    {
        string d;
        if (UPDATE_URL.Length == 0) d = "Not set up yet (see code)";
        else if (updState == 1) d = "Checking...";
        else if (updState == 2) d = "v" + VERSION_NAME + " · up to date";
        else if (updState == 3) d = "v" + remoteName + " available!";
        else if (updState == 4) d = "Check failed – try again";
        else d = "v" + VERSION_NAME + " installed";
        SetText(updDesc, d);
        SetText(updBtn, updState == 3 ? "UPDATE" : (updState == 1 ? "..." : "CHECK"));
        SetBack(updBtn, updState == 3 ? Theme.OK : (UPDATE_URL.Length == 0 ? Theme.CHIP : Theme.ACCENT));
        SetText(tabBoost, updState == 3 ? "BOOST •" : "BOOST");
    }

    // ================= Updater =================
    // Neu bauen ohne .bat und ohne Downloads (weniger Fehlalarme bei Virenscannern)
    void DoUpdate(string cs)
    {
        try
        {
            SaveSettings();
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            if (!File.Exists(cs)) { MessageBox.Show("BladeBallMacro.cs is missing in folder:\n" + dir, "BladeBall Macro"); return; }
            string ico = Path.Combine(Path.GetTempPath(), "BladeBallMacro.ico");
            string icoArg = "";
            try { File.WriteAllBytes(ico, AppIcon.BuildIco()); icoArg = " /win32icon:\"" + ico + "\""; } catch { }

            string win = Environment.GetEnvironmentVariable("WINDIR");
            string csc = Path.Combine(win, @"Microsoft.NET\Framework64\v4.0.30319\csc.exe");
            if (!File.Exists(csc)) csc = Path.Combine(win, @"Microsoft.NET\Framework\v4.0.30319\csc.exe");

            string exe = Application.ExecutablePath;
            string tmp = Path.Combine(dir, "BladeBallMacro_new.exe");
            if (File.Exists(tmp)) File.Delete(tmp);

            ProcessStartInfo psi = new ProcessStartInfo(csc,
                "/nologo /optimize+ /codepage:65001 /target:winexe" + icoArg + " /out:\"" + tmp + "\" /r:System.Windows.Forms.dll /r:System.Drawing.dll \"" + cs + "\"");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            string output;
            using (Process p = Process.Start(psi))
            {
                output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(60000);
            }
            if (!File.Exists(tmp)) { MessageBox.Show("Build failed:\n\n" + output, "BladeBall Macro"); return; }

            // Laufende EXE darf umbenannt werden -> alte weg, neue an ihren Platz
            string old = exe + ".old";
            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);
            File.Move(tmp, exe);
            Process.Start(exe);
            Shutdown();
        }
        catch (Exception ex) { MessageBox.Show("Update failed:\n" + ex.Message); }
    }

    void Shutdown()
    {
        running = false;
        try { KeyInputs k = keys; if (k != null) SendInput((uint)k.Up.Length, k.Up, Marshal.SizeOf(typeof(INPUT))); } catch { }
        if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        if (mhook != IntPtr.Zero) { UnhookWindowsHookEx(mhook); mhook = IntPtr.Zero; }
        if (periodOn) { timeEndPeriod(1); periodOn = false; }
        DisableBoost();
        RestoreBackground();
        Environment.Exit(0);
    }

    protected override void OnFormClosing(FormClosingEventArgs e) { SaveSettings(); base.OnFormClosing(e); }
    protected override void OnFormClosed(FormClosedEventArgs e) { base.OnFormClosed(e); Shutdown(); }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try { string old = Application.ExecutablePath + ".old"; if (File.Exists(old)) File.Delete(old); } catch { }

        if (ReadSavedKey() == ACCESS_KEY) RememberKey = true;
        else
        {
            using (KeyForm kf = new KeyForm())
            {
                if (kf.ShowDialog() != DialogResult.OK) return;
                RememberKey = kf.Remember;
            }
        }
        Application.Run(new MacroForm());
    }
}
