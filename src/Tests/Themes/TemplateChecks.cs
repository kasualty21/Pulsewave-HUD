using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pulsewave.Themes;
using Color = System.Windows.Media.Color;
using Size = System.Windows.Size;

namespace Pulsewave.Regression;

// Quality gate for the built-in templates: every one must load, find all its fonts, stay on
// screen, keep text from overlapping and keep text readable against the pixels behind it.
internal static class TemplateChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("TEMPLATES: " + message);
        Console.WriteLine("PASS TEMPLATES: " + message);
    }

    public static void Run(string outputRoot)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                string root = Path.Combine(outputRoot, "templates-" + Guid.NewGuid().ToString("N"));
                // The gate itself must catch bad designs, not just pass good ones.
                var bad = new ThemeDefinition
                {
                    BackgroundColor = "#FF202020",
                    Elements =
                    [
                        new ThemeElement { Kind = ThemeElementKind.Text, Text = "FIRST", X = 100, Y = 100, Font = new ThemeFont { Size = 30 } },
                        new ThemeElement { Kind = ThemeElementKind.Text, Text = "SECOND", X = 130, Y = 110, Font = new ThemeFont { Size = 30 } },
                        new ThemeElement { Kind = ThemeElementKind.Data, DataName = "CPULOAD", X = 1880, Y = 300, Font = new ThemeFont { Size = 40 } },
                        new ThemeElement { Kind = ThemeElementKind.Text, Text = "faint", X = 800, Y = 300, Font = new ThemeFont { Size = 14, Color = "#FF303030" } }
                    ]
                };
                var found = Problems(bad, Path.GetTempPath());
                Check(found.Any(p => p.Contains("overlaps")) && found.Any(p => p.Contains("leaves the screen")) && found.Any(p => p.Contains("'faint' contrast")),
                    "The quality gate catches overlapping text, off-screen readouts and low contrast");
                var names = BuiltInTemplates.Names;
                Check(names.Count >= 8, $"{names.Count} built-in templates ship with Pulsewave");
                foreach (var name in names)
                {
                    string dir = BuiltInTemplates.Ensure(BuiltInTemplates.Prefix + name, root)!;
                    var problems = Problems(ThemeStore.Load(dir), dir);
                    problems.AddRange(StressProblems(ThemeStore.Load(dir), dir));
                    Check(problems.Count == 0, $"Template '{name}' passes the quality gate, also with worst-case values" + (problems.Count > 0 ? ": " + string.Join("; ", problems) : ""));
                }
                Check(BuiltInTemplates.Ensure("builtin:../escape", root) == null && BuiltInTemplates.Ensure("builtin:nope", root) == null,
                    "Unknown or path-like template names are refused");
                var listed = BuiltInTemplates.List(root);
                Check(listed.Count == names.Count && listed.All(t => t.Definition != null && File.Exists(Path.Combine(t.Directory, "preview.png"))),
                    "The gallery lists every template with a rendered preview");
                string file = Path.Combine(root, names[0], "theme.json");
                File.WriteAllText(file, "{}");
                BuiltInTemplates.Ensure(BuiltInTemplates.Prefix + names[0], root);
                Check(File.ReadAllText(file) == BuiltInTemplates.Read(names[0]), "A changed or damaged template copy is restored from the app");
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw new Exception("Template checks failed", failure);
    }

    /// <summary>Everything wrong with a theme as rendered with sample values; empty when it is good.</summary>
    public static List<string> Problems(ThemeDefinition theme, string dir, string? shotPath = null)
    {
        var problems = new List<string>();
        var sample = ThemeSampleData.Context();
        var view = Render(theme, dir, sample, out var full);
        if (shotPath != null) ThemeMedia.SavePng(full, shotPath);
        foreach (var font in view.MissingFonts) problems.Add("font not found: " + font);

        var shown = theme.Elements.Where(e => !e.Hidden).ToList();
        // Readouts must be fully visible; decorative panels and lines may bleed off the edges.
        foreach (var e in shown.Where(e => e.Kind is not (ThemeElementKind.Panel or ThemeElementKind.Line)))
        {
            var b = view.BoundsOf(e);
            if (!b.IsEmpty && (b.Left < -1 || b.Top < -1 || b.Right > theme.Width + 1 || b.Bottom > theme.Height + 1))
                problems.Add($"{Name(e)} leaves the screen ({b.Left:0},{b.Top:0} {b.Width:0}x{b.Height:0})");
        }

        var texts = shown.Where(e => e.Kind is ThemeElementKind.Text or ThemeElementKind.Data)
            .Select(e => (Element: e, Box: Ink(view, e))).ToList();
        for (int i = 0; i < texts.Count; i++)
            for (int j = i + 1; j < texts.Count; j++)
                if (texts[i].Box.IntersectsWith(texts[j].Box))
                    problems.Add($"{Name(texts[i].Element)} overlaps {Name(texts[j].Element)}");

        // Contrast against what is drawn behind the text: render without any text, then sample.
        var backdrop = new ThemeDefinition
        {
            Width = theme.Width, Height = theme.Height, BackgroundColor = theme.BackgroundColor, BackgroundImage = theme.BackgroundImage,
            BackgroundFit = theme.BackgroundFit, Elements = theme.Elements.Where(e => e.Kind is not (ThemeElementKind.Text or ThemeElementKind.Data)).ToList()
        };
        Render(backdrop, dir, sample, out var behind);
        var pixels = new byte[theme.Width * theme.Height * 4];
        behind.CopyPixels(pixels, theme.Width * 4, 0);
        foreach (var (e, box) in texts)
        {
            var font = e.Font!;
            var color = ThemeView.ParseColor(font.Color, Colors.White);
            double px = font.Size * 96 / 72;
            bool icon = font.Family.Contains("Icons", StringComparison.OrdinalIgnoreCase);
            double needed = icon || px >= 32 ? 3.0 : 4.5;
            var samples = new List<Color>();
            for (int sx = 1; sx < 12; sx++)
                for (int sy = 1; sy < 6; sy++)
                {
                    int x = (int)Math.Clamp(box.Left + box.Width * sx / 12, 0, theme.Width - 1);
                    int y = (int)Math.Clamp(box.Top + box.Height * sy / 6, 0, theme.Height - 1);
                    int k = (y * theme.Width + x) * 4;
                    samples.Add(Color.FromRgb(pixels[k + 2], pixels[k + 1], pixels[k]));
                }
            samples.Sort((a, b) => Contrast.Luminance(a).CompareTo(Contrast.Luminance(b)));
            double worst = Math.Min(Contrast.Ratio(color, samples[samples.Count / 10]), Contrast.Ratio(color, samples[samples.Count * 9 / 10]));
            if (worst < needed) problems.Add($"{Name(e)} contrast {worst:0.0} < {needed:0.0}");
        }
        return problems;
    }

    /// <summary>
    /// Layout problems with worst-case values (ThemeSampleData.Stress: 100%, 99 °C, 5-digit fan speeds, GB/s, long
    /// titles and names): readouts that leave the screen or collide once their text grows. Colors are not judged here.
    /// </summary>
    public static List<string> StressProblems(ThemeDefinition theme, string dir)
    {
        var problems = new List<string>();
        var view = Render(theme, dir, ThemeSampleData.Stress(), out _);
        var shown = theme.Elements.Where(e => !e.Hidden).ToList();
        foreach (var e in shown.Where(e => e.Kind is not (ThemeElementKind.Panel or ThemeElementKind.Line)))
        {
            var b = view.BoundsOf(e);
            if (!b.IsEmpty && (b.Left < -1 || b.Top < -1 || b.Right > theme.Width + 1 || b.Bottom > theme.Height + 1))
                problems.Add($"stress: {Name(e)} leaves the screen ({b.Left:0},{b.Top:0} {b.Width:0}x{b.Height:0})");
        }
        var texts = shown.Where(e => e.Kind is ThemeElementKind.Text or ThemeElementKind.Data).Select(e => (Element: e, Box: Ink(view, e))).ToList();
        for (int i = 0; i < texts.Count; i++)
            for (int j = i + 1; j < texts.Count; j++)
                if (texts[i].Box.IntersectsWith(texts[j].Box))
                    problems.Add($"stress: {Name(texts[i].Element)} overlaps {Name(texts[j].Element)}");
        return problems;
    }

    // The glyphs' own extent: line boxes include leading above and below, which may touch the next line legitimately.
    private static Rect Ink(ThemeView view, ThemeElement e)
    {
        var b = view.BoundsOf(e);
        double px = (e.Font?.Size ?? 12) * 96 / 72;
        return new Rect(b.Left + 1, b.Top + px * 0.18, Math.Max(1, b.Width - 2), Math.Max(1, b.Height - px * 0.42));
    }

    private static ThemeView Render(ThemeDefinition theme, string dir, ThemeDataContext data, out RenderTargetBitmap bitmap)
    {
        var view = new ThemeView(theme, dir, playVideo: false);
        for (int tick = 60; tick > 0; tick--) view.Update(ThemeSampleData.Context(tick)); // charts fill with history
        view.Update(data);
        view.Measure(new Size(theme.Width, theme.Height));
        view.Arrange(new Rect(0, 0, theme.Width, theme.Height));
        view.UpdateLayout();
        bitmap = new RenderTargetBitmap(theme.Width, theme.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        return view;
    }

    private static string Name(ThemeElement e) => e.Kind switch
    {
        ThemeElementKind.Text => $"text '{e.Text}'",
        ThemeElementKind.Data => $"value {e.DataName}",
        _ => $"{e.Kind.ToString().ToLowerInvariant()} at {e.X:0},{e.Y:0}"
    };
}
