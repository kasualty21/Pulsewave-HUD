using System.IO;
using Pulsewave.Themes;
using Pulsewave.Themes.Editor;

namespace Pulsewave.Regression;

// Color palettes: recoloring keeps opacity and structure, maps the main accent first, never edits the
// theme's files, and every built-in template still passes the quality gate in every palette.
internal static class PaletteChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("PALETTES: " + message);
        Console.WriteLine("PASS PALETTES: " + message);
    }

    private static IEnumerable<System.Windows.Controls.Border> Tiles(System.Windows.DependencyObject node)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(node, i);
            if (child is System.Windows.Controls.Border b) yield return b;
            foreach (var inner in Tiles(child)) yield return inner;
        }
    }

    public static void Run(string outputRoot, string? shots = null)
    {
        var theme = new ThemeDefinition { Width = 1920, Height = 480, BackgroundColor = "#FF0B1521" };
        theme.Elements.AddRange([
            new ThemeElement { Kind = ThemeElementKind.Panel, Width = 400, Height = 200, FillColor = "#CC16232F", BorderColor = "#6676F5D9", BorderWidth = 1.5 },
            new ThemeElement { Kind = ThemeElementKind.Text, Text = "CPU", Font = new ThemeFont { Color = "#FFFFFFFF" } },
            new ThemeElement { Kind = ThemeElementKind.Text, Text = "dim", Font = new ThemeFont { Color = "#FF8A9BA8" } },
            new ThemeElement { Kind = ThemeElementKind.Bar, Width = 300, Height = 16, DataName = "CPULOAD", ForeColor = "#FF76F5D9", BackColor = "#FF22303F" },
            new ThemeElement { Kind = ThemeElementKind.Bar, Width = 300, Height = 16, DataName = "GPULOAD", ForeColor = "#FF70E8CF", BackColor = "#FF22303F" },
            new ThemeElement { Kind = ThemeElementKind.Text, Text = "GPU", Font = new ThemeFont { Color = "#FF9EC6FF" } },
        ]);
        var synth = ThemePalettes.Find("synthwave")!;
        ThemePalettes.Apply(theme, synth);
        var e = theme.Elements;
        Check(theme.BackgroundColor == "#FF120A24" && e[0].FillColor.StartsWith("#CC") && e[0].BorderColor == "#66FF4FD8" &&
              e[1].Font!.Color == "#FFFFF0FB" && e[2].Font!.Color == "#FFCBA9DA" && e[3].ForeColor == "#FFFF4FD8" && e[4].ForeColor == "#FFFF4FD8" &&
              e[3].BackColor == e[4].BackColor && e[3].BackColor != "#FF22303F",
            "A palette swaps backgrounds, text, grey text and accents, keeps each color's opacity, and gives the main accent (and its near shades) the palette's first");
        // The blue GPU label was very readable (10:1); the palette's second accent (#7A5CFF, 3.4:1) is lifted toward its text color.
        var gpu = ThemeView.ParseColor(e[5].Font!.Color, System.Windows.Media.Colors.Black);
        Check(gpu.B > gpu.G && gpu.R > 0x7A && gpu.G > 0x5C && e[5].Font!.Color != "#FFFFF0FB",
            "Accent-colored text keeps the palette's hue but is lifted so it reads as well as the theme's own text did");
        Check(ThemePalettes.Next("") == ThemePalettes.All[0].Name && ThemePalettes.Next(ThemePalettes.All[^1].Name) == "" &&
              ThemePalettes.Next("", -1) == ThemePalettes.All[^1].Name && ThemePalettes.Find("nope") == null && ThemePalettes.All.Count >= 16,
            $"{ThemePalettes.All.Count} palettes; Next color palette steps through them and back to the theme's own colors");

        // Settings: older files have none; a palette is remembered per theme.
        string root = Path.Combine(outputRoot, "palettes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "settings.json");
        File.WriteAllText(file, "{\"ActiveTheme\":\"x\"}");
        var older = SettingsStore.LoadFile(file);
        bool none = older.ThemePalettes.Count == 0;
        older.ThemePalettes["builtin:01-x"] = "Ember";
        older.ThemePalettes["mine"] = "";
        SettingsStore.SaveFile(file, older);
        var loaded = SettingsStore.LoadFile(file);
        Check(none && loaded.ThemePalettes.Count == 1 && loaded.ThemePalettes["builtin:01-x"] == "Ember",
            "Older settings show every theme in its own colors; a chosen palette is saved for its theme");

        // Every template in every palette passes the quality gate (fonts, on-screen, no overlap, contrast), and files stay untouched.
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                string templates = Path.Combine(outputRoot, "palette-templates-" + Guid.NewGuid().ToString("N"));
                if (shots != null) Directory.CreateDirectory(shots);
                int checkedCount = 0;
                var failures = new List<string>();
                foreach (var name in BuiltInTemplates.Names)
                {
                    string dir = BuiltInTemplates.Ensure(BuiltInTemplates.Prefix + name, templates)!;
                    string before = File.ReadAllText(Path.Combine(dir, "theme.json"));
                    foreach (var palette in ThemePalettes.All)
                    {
                        var t = ThemeStore.Load(dir);
                        ThemePalettes.Apply(t, palette);
                        string? shot = shots != null && name == BuiltInTemplates.Names[0] ? Path.Combine(shots, $"palette-{palette.Name.Replace(' ', '-')}.png") : null;
                        var problems = TemplateChecks.Problems(t, dir, shot);
                        if (problems.Count > 0) failures.Add($"{name} in {palette.Name}: {string.Join("; ", problems)}");
                        checkedCount++;
                    }
                    if (File.ReadAllText(Path.Combine(dir, "theme.json")) != before) failures.Add($"{name}'s file changed");
                }
                Check(failures.Count == 0, $"All {checkedCount} template and palette pairs pass the quality gate; theme files are not changed" +
                    (failures.Count > 0 ? ": " + string.Join(" | ", failures.Take(6)) : ""));

                // The Colors page at both shapes: every palette as a tile, the current one marked, a tap picks one.
                foreach (var (w, h) in new[] { (1920, 480), (480, 1802) })
                {
                    var page = new PaletteView(new System.Windows.Size(w, h));
                    string? chosen = null;
                    page.PaletteChosen += name => chosen = name;
                    page.Open("Ember", "Halo");
                    page.Measure(new System.Windows.Size(w, h)); page.Arrange(new System.Windows.Rect(0, 0, w, h)); page.UpdateLayout();
                    var tiles = Tiles(page).Where(b => b.Tag is string tag && tag.StartsWith("palette:")).ToList();
                    var ember = tiles.First(b => (string)b.Tag == "palette:Ember");
                    ember.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
                        { RoutedEvent = System.Windows.UIElement.MouseLeftButtonUpEvent });
                    if (shots != null)
                    {
                        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                        bitmap.Render(page);
                        var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                        using var output = File.Create(Path.Combine(shots, $"colors-{w}x{h}.png"));
                        png.Save(output);
                    }
                    page.Close();
                    Check(tiles.Count == ThemePalettes.All.Count + 1 && ember.BorderThickness.Left == 3 && chosen == "Ember" && tiles.All(t => t.ActualHeight >= 56),
                        $"The {w}x{h} Colors page lists the theme's own colors and every palette as big tiles, marks the current one, and a tap picks one");
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw failure;
    }
}
