using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pulsewave.Themes;
using Pulsewave.Themes.Editor;
using Color = System.Windows.Media.Color;
using Size = System.Windows.Size;

namespace Pulsewave.Regression;

// Theme editor: document model, geometry and snapping, element bounds, contrast helpers,
// media files and an editor window smoke test.
internal static class EditorChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("EDITOR: " + message);
        Console.WriteLine("PASS EDITOR: " + message);
    }

    public static void Run(string outputRoot)
    {
        string root = Path.Combine(outputRoot, "editor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Exception? failure = null;
        // WPF imaging and the editor window need an STA thread.
        var thread = new Thread(() =>
        {
            try
            {
                Document(root);
                Geometry();
                ContrastRules();
                Primitives();
                Media(root);
                Editor(root);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw new Exception("Editor checks failed", failure);
    }

    private static void Document(string root)
    {
        string dir = Path.Combine(root, "doc");
        Directory.CreateDirectory(dir);
        ThemeStore.Save(dir, new ThemeDefinition { Name = "Doc" });
        var doc = new ThemeDocument(dir);
        doc.Change(t => t.Elements.Add(new ThemeElement { Kind = ThemeElementKind.Text, Text = "A", Font = new ThemeFont() }));
        doc.Change(t => t.Elements[0].X = 50);
        Check(doc.IsDirty && doc.CanUndo && doc.Theme.Elements[0].X == 50, "Edits apply and mark the document unsaved");
        doc.Undo();
        Check(doc.Theme.Elements[0].X == 0 && doc.CanRedo, "Undo restores the previous state");
        doc.Redo();
        Check(doc.Theme.Elements[0].X == 50, "Redo reapplies the edit");
        doc.Change(t => t.Name = "Saved");
        Check(!doc.CanRedo, "A new edit clears redo");
        doc.Save();
        Check(!doc.IsDirty && ThemeStore.Load(dir) is { Name: "Saved", Elements.Count: 1 }, "Save writes theme.json and clears the unsaved flag");
    }

    private static void Geometry()
    {
        var canvas = new Size(1920, 480);
        var snapCenter = EditorGeometry.Snap(new Rect(905, 100, 100, 40), [], canvas, 8);
        Check(snapCenter.Dx == 5 && snapCenter.VerticalGuides.Contains(960), "Moving near the canvas center snaps to it and shows a guide");
        var snapEdge = EditorGeometry.Snap(new Rect(303, 52, 50, 50), [new Rect(100, 50, 200, 30)], canvas, 8);
        Check(snapEdge.Dx == -3 && snapEdge.Dy == -2, "Edges snap to other elements' edges");
        var none = EditorGeometry.Snap(new Rect(500, 200, 50, 50), [], canvas, 8);
        Check(none.Dx == 0 && none.Dy == 0 && none.VerticalGuides.Count == 0, "Nothing snaps outside the tolerance");

        var text = new ThemeElement { Kind = ThemeElementKind.Data, X = 500, Y = 100, Font = new ThemeFont { Size = 40, Alignment = ThemeTextAlignment.Center } };
        EditorGeometry.Resize(text, new Rect(450, 100, 100, 60), new Rect(400, 80, 200, 120));
        Check(text.Font!.Size == 80 && text.X == 500 && text.Y == 80, "Resizing text scales its font and keeps its anchor");
        var arc = new ThemeElement { Kind = ThemeElementKind.Arc, X = 10, Y = 10, Diameter = 100, Thickness = 10 };
        EditorGeometry.Resize(arc, new Rect(5, 5, 110, 110), new Rect(5, 5, 210, 160));
        Check(arc.Diameter == 150 && arc.Width == 150 && arc.X == 10, "Resizing a gauge keeps it round");
        var bar = new ThemeElement { Kind = ThemeElementKind.Bar, X = 0, Y = 0, Width = 100, Height = 20 };
        EditorGeometry.Resize(bar, new Rect(0, 0, 100, 20), new Rect(10, 10, 300, 30));
        Check(bar is { X: 10, Y: 10, Width: 300, Height: 30 }, "Bars take the dragged box");
        var image = new ThemeElement { Kind = ThemeElementKind.Image, Width = 1 };
        EditorGeometry.Resize(image, new Rect(0, 0, 200, 100), new Rect(0, 0, 100, 50));
        Check(image.Width == 0.5, "Pictures scale with the dragged box");

        var view = new ThemeView(new ThemeDefinition
        {
            Elements =
            [
                new ThemeElement { Kind = ThemeElementKind.Text, Text = "WIDE TEXT", X = 960, Y = 50, Font = new ThemeFont { Size = 30, Alignment = ThemeTextAlignment.Center } },
                new ThemeElement { Kind = ThemeElementKind.Arc, X = 100, Y = 100, Diameter = 100, Thickness = 20 }
            ]
        }, Path.GetTempPath(), playVideo: false);
        var b0 = view.BoundsOf(view.Theme.Elements[0]); var b1 = view.BoundsOf(view.Theme.Elements[1]);
        Check(Math.Abs(b0.Left + b0.Width / 2 - 960) < 0.5 && b0.Top == 50 && b0.Width > 50 && b1 == new Rect(90, 90, 120, 120),
            "Element bounds follow the drawn text width and the gauge's stroke");
    }

    // Small analysis frames: 480x120 BGRA (the analyzer's working size).
    private static byte[] Frame(Func<int, int, byte> luma)
    {
        var f = new byte[480 * 120 * 4];
        for (int y = 0; y < 120; y++)
            for (int x = 0; x < 480; x++)
            {
                byte v = luma(x, y); int i = (y * 480 + x) * 4;
                f[i] = f[i + 1] = f[i + 2] = v; f[i + 3] = 255;
            }
        return f;
    }

    private static void ContrastRules()
    {
        Check(Math.Abs(Contrast.Ratio(Colors.White, Colors.Black) - 21) < 0.01, "Contrast ratio matches WCAG (white on black = 21)");
        var fixedColor = Contrast.Readable(Color.FromRgb(120, 120, 130), Color.FromRgb(100, 100, 100));
        Check(Contrast.Ratio(fixedColor, Color.FromRgb(100, 100, 100)) >= 4.5, "Low-contrast text is adjusted until readable");
        Check(Contrast.Readable(Color.FromRgb(255, 170, 95), Colors.Black) == Color.FromRgb(255, 170, 95), "Already readable colors are kept");
    }



    private static string Picture(string root)
    {
        var pixels = Frame((x, y) => x < 240 ? (byte)25 : (byte)(((x / 2 + y / 2) & 1) == 1 ? 230 : 15));
        var bitmap = BitmapSource.Create(480, 120, 96, 96, PixelFormats.Bgra32, null, pixels, 480 * 4);
        string path = Path.Combine(root, "art-" + Guid.NewGuid().ToString("N")[..6] + ".png");
        ThemeMedia.SavePng(bitmap, path);
        return path;
    }

    private static void Primitives()
    {
        var theme = new ThemeDefinition
        {
            Elements =
            [
                new ThemeElement { Kind = ThemeElementKind.Panel, X = 10, Y = 20, Width = 300, Height = 100, CornerStyle = "Cut", CornerRadius = 20,
                    FillColor = "#FF102030", GradientColor = "#FF000000", GradientStyle = "Radial", GlowColor = "#8835D9FF" },
                new ThemeElement { Kind = ThemeElementKind.Panel, X = 400, Y = 20, Width = 200, Height = 100, CornerRadius = 12, FillColor = "#FF102030",
                    GradientColor = "#FF203040", GradientAngle = 0 },
                new ThemeElement { Kind = ThemeElementKind.Line, X = 700, Y = 100, Width = 200, Height = -50, LineColor = "#FFFFFFFF", LineWidth = 4, SegmentWidth = 8, SegmentGap = 4 }
            ]
        };
        var view = new ThemeView(theme, Path.GetTempPath(), playVideo: false);
        var canvas = view.Children.OfType<System.Windows.Controls.Canvas>().Single();
        var cut = canvas.Children.OfType<System.Windows.Shapes.Polygon>().Single();
        var round = canvas.Children.OfType<System.Windows.Shapes.Rectangle>().Single();
        Check(cut.Points.Count == 8 && cut.Fill is RadialGradientBrush && cut.Effect is System.Windows.Media.Effects.DropShadowEffect &&
              round.RadiusX == 12 && round.Fill is LinearGradientBrush { StartPoint.X: 0, EndPoint.X: 1 },
            "Panels draw cut or round corners with radial/linear gradients and glow");
        var drawn = canvas.Children.OfType<System.Windows.Shapes.Line>().Single();
        Check(drawn is { X1: 700, Y1: 100, X2: 900, Y2: 50 } && drawn.StrokeDashArray?.Count == 2 && view.BoundsOf(theme.Elements[2]) == new Rect(698, 48, 204, 54),
            "Lines draw from X,Y by their offset, dashed, with bounds around the stroke");
        var lineCopy = ThemeDocument.Clone(theme.Elements[2]);
        EditorGeometry.Resize(lineCopy, new Rect(698, 48, 204, 54), new Rect(698, 48, 404, 104));
        Check(lineCopy is { X: 700, Width: 400, Height: -100 } && lineCopy.Y == 150, "Resizing a line keeps its direction");
        // Now playing as theme elements, and the text width cap that keeps long titles contained.
        var sample = ThemeSampleData.Uniform();
        var nothing = new ThemeDataContext();
        Check(ThemeData.Read("MEDIA_TITLE", null, false, sample).Text == "Midnight City (Extended Remix Version)" &&
              ThemeData.Read("MEDIA_STATE", null, false, sample).Text.Contains("PLAYING") &&
              ThemeData.Read("MEDIA_TITLE", null, false, nothing).Text == "Nothing playing" && ThemeData.Read("MEDIA_ARTIST", null, false, nothing).Text == "",
            "Now playing readings show the track, or 'Nothing playing' with a blank artist");
        var mediaTheme = new ThemeDefinition
        {
            Elements =
            [
                new ThemeElement { Kind = ThemeElementKind.AlbumArt, X = 20, Y = 20, Width = 80, Height = 80, CornerRadius = 10, FillColor = "#FF222222" },
                new ThemeElement { Kind = ThemeElementKind.Data, DataName = "MEDIA_TITLE", X = 120, Y = 20, Width = 200, Font = new ThemeFont { Size = 20 } }
            ]
        };
        var mediaView = new ThemeView(mediaTheme, Path.GetTempPath(), playVideo: false);
        mediaView.Update(sample);
        mediaView.Measure(new Size(1920, 480)); mediaView.Arrange(new Rect(0, 0, 1920, 480)); mediaView.UpdateLayout();
        var cover = FindDescendant<System.Windows.Controls.Image>(mediaView);
        var title = mediaView.Children.OfType<System.Windows.Controls.Canvas>().Single().Children.OfType<System.Windows.Controls.TextBlock>().Single();
        Check(cover?.Source != null && title.TextTrimming == TextTrimming.CharacterEllipsis && mediaView.BoundsOf(mediaTheme.Elements[1]).Width <= 200,
            "Album art shows the cover, and a long title is capped to its width with an ellipsis");
        mediaView.Update(nothing);
        Check(cover!.Source == null, "Album art falls back to its placeholder when nothing is playing");
        Check(ThemeFonts.Resolve("Bahnschrift SemiBold") is { Found: true } bahn && bahn.Weight == FontWeights.SemiBold &&
              ThemeFonts.Resolve("Bahnschrift Light Condensed") is { Found: true } light && light.Stretch == FontStretches.Condensed,
            "Font names carry weight and width (Bahnschrift SemiBold, Light Condensed)");
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            if (FindDescendant<T>(child) is { } deeper) return deeper;
        }
        return null;
    }

    private static void Media(string root)
    {
        string dir = Path.Combine(root, "media");
        Directory.CreateDirectory(dir);
        string used = ThemeMedia.ImportImage(dir, Picture(root), "background");
        string orphan = ThemeMedia.ImportImage(dir, Picture(root), "background");
        var theme = new ThemeDefinition { BackgroundImage = used };
        ThemeStore.Save(dir, theme);
        File.WriteAllText(Path.Combine(dir, "notes.txt"), "keep");
        ThemeMedia.RemoveUnreferenced(dir, theme);
        Check(orphan != used && File.Exists(Path.Combine(dir, used)) && !File.Exists(Path.Combine(dir, orphan)) &&
              File.Exists(Path.Combine(dir, "theme.json")) && File.Exists(Path.Combine(dir, "notes.txt")),
            "Cleanup removes only unreferenced media");
        string preview = ThemeMedia.SavePreview(dir, theme);
        Check(File.Exists(Path.Combine(dir, preview)), "A gallery preview is rendered on save");

        // A 2:1 picture with distinct horizontal bands: "Cover" must show the same middle band
        // the analysis measured, not the top of the picture.
        var bands = new byte[400 * 200 * 4];
        for (int y = 0; y < 200; y++) for (int x = 0; x < 400; x++)
        {
            int i = (y * 400 + x) * 4; bands[i + 3] = 255;
            if (y < 50) bands[i + 2] = 255; else if (y < 150) bands[i + 1] = 255; else bands[i] = 255; // red, green, blue
        }
        string bandFile = Path.Combine(root, "bands.png");
        ThemeMedia.SavePng(BitmapSource.Create(400, 200, 96, 96, PixelFormats.Bgra32, null, bands, 1600), bandFile);
        var coverTheme = new ThemeDefinition { BackgroundImage = ThemeMedia.ImportImage(dir, bandFile, "bands"), BackgroundFit = "Cover" };
        var view = new ThemeView(coverTheme, dir, playVideo: false);
        view.Measure(new Size(1920, 480)); view.Arrange(new Rect(0, 0, 1920, 480)); view.UpdateLayout();
        var shot = new RenderTargetBitmap(1920, 480, 96, 96, PixelFormats.Pbgra32); shot.Render(view);
        var top = new byte[4]; shot.CopyPixels(new Int32Rect(960, 5, 1, 1), top, 4, 0);
        Check(top[1] > 200 && top[2] < 50, "Cover backgrounds are centered (the middle band shows, not the top)");
    }

    private static void Editor(string root)
    {
        string dir = Path.Combine(root, "editor");
        Directory.CreateDirectory(dir);
        ThemeStore.Save(dir, new ThemeDefinition { Name = "Editor smoke", Elements = [new ThemeElement { Kind = ThemeElementKind.Data, DataName = "CPUTEMP", X = 200, Y = 100, Font = new ThemeFont { Size = 40 } }] });
        var window = new ThemeEditorWindow(dir, new MonitorSettings(), null, null);
        Check(window.Title.Contains("Editor smoke"), "The editor opens a theme with its elements and properties");
        var groups = ThemeDataGroups.Grouped();
        Check(groups.Sum(g => g.Items.Count) == ThemeData.Catalog.Count && groups.Select(g => g.Group).SequenceEqual(ThemeDataGroups.Order.Where(o => groups.Any(g => g.Group == o))) &&
              groups.Single(g => g.Group == "Shortcuts").Items.All(i => i.Name == "APP_NAME") &&
              ThemeDataGroups.Grouped("sunrise").SelectMany(g => g.Items).All(i => i.Name.StartsWith("WEATHER")) && ThemeDataGroups.Grouped("gaming").Count == 1,
            "The data picker shows every reading once, in sections in a set order, and search narrows it by name, label or section");
        Check(ThemeEditorDefaults.Thresholds("CPUTEMP") == (90, 98) && ThemeEditorDefaults.Thresholds("GPUTEMP") == (83, 90) && ThemeEditorDefaults.Thresholds("RAMLOAD") == (85, 95),
            "Turning warning colors on starts from sensible levels for the reading");
        var older = BuiltInTemplates.LegacyNames.Where(n => int.Parse(n[..2]) <= 25).ToList();
        Check(older.Count == 25 && older.All(n => TemplateRefresh.Apply(BuiltInTemplates.Read(n)) == BuiltInTemplates.Read(n)) &&
              !older.Any(n => BuiltInTemplates.Read(n).Contains("\"MEDIA_STATE\"")),
            "The older built-in templates carry the feature refresh (warning colors, Media page taps, track time, readable dates, tinted tiles), and re-running it changes nothing");
        window.Close();
    }

}
