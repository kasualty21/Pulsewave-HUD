using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace Pulsewave;

/// <summary>
/// The tray windows' look, matching the HUD's Halo style: a deep navy base, tinted card panels, light
/// text with a soft secondary grey, rounded buttons (the main action filled sky blue, deletes rose),
/// dark list headers and scroll bars, and a dark title bar tinted to the window. Each window calls
/// <see cref="Apply"/> once; colors the windows were written with are mapped to Halo's, so their layout
/// code stays as it is.
/// </summary>
internal static class DesktopStyle
{
    public static readonly Color Base = Color.FromArgb(14, 20, 36), Card = Color.FromArgb(22, 29, 48), Field = Color.FromArgb(11, 16, 28),
        Button = Color.FromArgb(38, 48, 72), ButtonHover = Color.FromArgb(52, 64, 94), Text = Color.FromArgb(245, 247, 250),
        Secondary = Color.FromArgb(201, 210, 222), Sky = Color.FromArgb(127, 216, 255), SkyHover = Color.FromArgb(160, 226, 255),
        OnSky = Color.FromArgb(16, 22, 38), Rose = Color.FromArgb(122, 52, 78), Warm = Color.FromArgb(255, 210, 122), Edge = Color.FromArgb(52, 62, 86);

    /// <summary>Styles a window and everything in it, now and as controls are added later.</summary>
    public static void Apply(Forms.Form form)
    {
        form.BackColor = Base;
        form.ForeColor = Text;
        form.HandleCreated += (_, _) => DarkTitleBar(form.Handle);
        if (form.IsHandleCreated) DarkTitleBar(form.Handle);
        Style(form);
    }

    private static void Style(Forms.Control parent)
    {
        foreach (Forms.Control control in parent.Controls) StyleOne(control);
        parent.ControlAdded += (_, e) => { if (e.Control != null) StyleOne(e.Control); };
    }

    private static void StyleOne(Forms.Control c)
    {
        c.ForeColor = MapText(c.ForeColor);
        switch (c)
        {
            case Forms.Button b:
                StyleButton(b);
                break;
            case Forms.ListView list:
                list.BackColor = Field;
                list.ForeColor = Text;
                list.BorderStyle = Forms.BorderStyle.None;
                if (list.View == Forms.View.Details) DarkHeaders(list);
                DarkScrollBars(list);
                break;
            case Forms.ListBox or Forms.TreeView:
                c.BackColor = Field;
                c.ForeColor = Text;
                DarkScrollBars(c);
                break;
            case Forms.TextBox or Forms.NumericUpDown:
                c.BackColor = Field;
                c.ForeColor = Text;
                if (c is Forms.TextBox { Multiline: true }) DarkScrollBars(c);
                break;
            case Forms.ComboBox combo:
                combo.BackColor = Field;
                combo.ForeColor = Text;
                combo.FlatStyle = Forms.FlatStyle.Flat;
                break;
            case Forms.CheckBox or Forms.RadioButton or Forms.Label or Forms.TrackBar:
                c.BackColor = c.BackColor.A == 0 ? c.BackColor : MapBack(c.BackColor, c.Parent?.BackColor ?? Base);
                break;
            default:
                c.BackColor = MapBack(c.BackColor, c.Parent?.BackColor ?? Base);
                if (c is Forms.ScrollableControl { AutoScroll: true }) DarkScrollBars(c);
                break;
        }
        Style(c);
    }

    /// <summary>Rounded flat buttons: the main action (once teal) sky blue with dark text, deletes rose, the rest a soft slate.</summary>
    private static void StyleButton(Forms.Button b)
    {
        var old = b.BackColor;
        bool primary = Near(old, Color.FromArgb(40, 128, 117)), danger = Near(old, Color.FromArgb(110, 52, 52));
        // A colorful button shows something (the Lights color swatch): keep its color, just round it.
        bool keep = !primary && !danger && Saturation(old) > 0.45;
        b.FlatStyle = Forms.FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        if (!keep)
        {
            b.BackColor = primary ? Sky : danger ? Rose : Button;
            b.ForeColor = primary ? OnSky : Text;
            b.FlatAppearance.MouseOverBackColor = primary ? SkyHover : danger ? Color.FromArgb(150, 66, 98) : ButtonHover;
            b.FlatAppearance.MouseDownBackColor = b.FlatAppearance.MouseOverBackColor;
        }
        // A window's default button gets an outline in the border color: make it the button's own.
        b.FlatAppearance.BorderColor = b.BackColor;
        if (primary) b.Font = new Font(b.Font, FontStyle.Bold);
        b.Cursor = Forms.Cursors.Hand;
        void Round()
        {
            if (b.Width < 4 || b.Height < 4) return;
            int r = Math.Min(b.Height, 22);
            using var path = new GraphicsPath();
            path.AddArc(0, 0, r, r, 180, 90);
            path.AddArc(b.Width - r - 1, 0, r, r, 270, 90);
            path.AddArc(b.Width - r - 1, b.Height - r - 1, r, r, 0, 90);
            path.AddArc(0, b.Height - r - 1, r, r, 90, 90);
            path.CloseFigure();
            b.Region = new Region(path);
        }
        b.SizeChanged += (_, _) => Round();
        Round();
    }

    /// <summary>Old backgrounds mapped to Halo's: the window base, card panels, list fields; anything else is left.</summary>
    internal static Color MapBack(Color color, Color parent)
    {
        if (color.A == 0) return color;
        if (color == Forms.Control.DefaultBackColor || color == SystemColors.Control) return parent;
        double l = (Math.Max(color.R, Math.Max(color.G, color.B)) + Math.Min(color.R, Math.Min(color.G, color.B))) / 510.0;
        if (l > 0.4 || Saturation(color) > 0.45) return color; // something colorful on purpose (a swatch, a chart)
        if (Near(color, Color.FromArgb(19, 27, 37))) return Base;
        if (l < 0.09) return Field;
        if (l < 0.18) return Card; // every old panel shade (27,38,50 and its neighbours)
        return Button;
    }

    /// <summary>Old text colors mapped to Halo's: near-white to text, grey-blue to secondary, mint to sky, orange to warm.</summary>
    internal static Color MapText(Color color)
    {
        if (color.A == 0) return color;
        if (Near(color, Color.FromArgb(112, 232, 211)) || Near(color, Color.FromArgb(118, 245, 217)) || Near(color, Color.FromArgb(125, 184, 255))) return Sky;
        if (Near(color, Color.FromArgb(243, 169, 112)) || Near(color, Color.FromArgb(255, 191, 110))) return Warm;
        double l = (Math.Max(color.R, Math.Max(color.G, color.B)) + Math.Min(color.R, Math.Min(color.G, color.B))) / 510.0;
        if (Saturation(color) > 0.6) return color;
        if (l > 0.86 || color == SystemColors.ControlText) return Text;
        if (l > 0.5) return Secondary;
        return color;
    }

    private static bool Near(Color a, Color b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) < 30;

    private static double Saturation(Color c)
    {
        double max = Math.Max(c.R, Math.Max(c.G, c.B)) / 255.0, min = Math.Min(c.R, Math.Min(c.G, c.B)) / 255.0, l = (max + min) / 2;
        return max == min ? 0 : (max - min) / (1 - Math.Abs(2 * l - 1));
    }

    /// <summary>Dark list headers: the card color with secondary text, drawn by the list itself.</summary>
    private static void DarkHeaders(Forms.ListView list)
    {
        if (list.OwnerDraw) return;
        list.OwnerDraw = true;
        list.DrawColumnHeader += (_, e) =>
        {
            using var back = new SolidBrush(Card);
            e.Graphics.FillRectangle(back, e.Bounds);
            using var font = new Font(list.Font, FontStyle.Bold);
            var bounds = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height);
            Forms.TextRenderer.DrawText(e.Graphics, e.Header?.Text, font, bounds, Secondary, Forms.TextFormatFlags.VerticalCenter | Forms.TextFormatFlags.EndEllipsis);
        };
        list.DrawItem += (_, e) => e.DrawDefault = true;
        list.DrawSubItem += (_, e) => e.DrawDefault = true;
        // The header past the last column is Windows' own (light): the last column stretches to fill it.
        bool sizing = false;
        void Fill()
        {
            if (sizing || list.Columns.Count == 0 || list.ClientSize.Width <= 0) return;
            sizing = true;
            int others = list.Columns.Cast<Forms.ColumnHeader>().Take(list.Columns.Count - 1).Sum(c => c.Width);
            int rest = list.ClientSize.Width - others;
            if (rest > 40) list.Columns[^1].Width = rest;
            sizing = false;
        }
        list.Resize += (_, _) => Fill();
        list.ColumnWidthChanged += (_, _) => Fill();
        Fill();
    }

    // Windows' own dark scroll bars and title bar (public uxtheme / DWM calls; older Windows ignores them).
    private static void DarkScrollBars(Forms.Control control)
    {
        void Set() { try { SetWindowTheme(control.Handle, "DarkMode_Explorer", null); } catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { } }
        if (control.IsHandleCreated) Set(); else control.HandleCreated += (_, _) => Set();
    }

    internal static void DarkTitleBar(IntPtr window)
    {
        try
        {
            int on = 1, caption = ToColorRef(Base), text = ToColorRef(Text), border = ToColorRef(Edge);
            DwmSetWindowAttribute(window, 20, ref on, sizeof(int));      // DWMWA_USE_IMMERSIVE_DARK_MODE
            DwmSetWindowAttribute(window, 35, ref caption, sizeof(int)); // DWMWA_CAPTION_COLOR (Windows 11)
            DwmSetWindowAttribute(window, 36, ref text, sizeof(int));    // DWMWA_TEXT_COLOR
            DwmSetWindowAttribute(window, 34, ref border, sizeof(int));  // DWMWA_BORDER_COLOR
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
    }

    private static int ToColorRef(Color c) => c.R | c.G << 8 | c.B << 16;

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr window, string? appName, string? idList);
}
