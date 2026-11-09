using System.IO;
using System.Windows;
using Pulsewave.Themes;
using Pulsewave.Themes.Editor;
using Point = System.Windows.Point;

namespace Pulsewave.Regression;

// Element groups and ready-made widgets in the theme editor: saving, scaling, ordering, and every
// widget passing the template quality gate on a wide and a vertical screen. Off-screen renders only.
internal static class GroupChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("GROUPS: " + message);
        Console.WriteLine("PASS GROUPS: " + message);
    }

    public static void Run(string outputRoot, string? shots = null)
    {
        string root = Path.Combine(outputRoot, "groups-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        // Saved and loaded; blank names dropped; older themes have none.
        var theme = new ThemeDefinition
        {
            Elements =
            [
                new ThemeElement { Kind = ThemeElementKind.Panel, Width = 10, Height = 10, Group = " Clock " },
                new ThemeElement { Kind = ThemeElementKind.Panel, Width = 10, Height = 10, Group = "   " },
                new ThemeElement { Kind = ThemeElementKind.Panel, Width = 10, Height = 10, Group = new string('x', 100) },
            ]
        };
        ThemeStore.Save(root, theme);
        var loaded = ThemeStore.Load(root);
        File.WriteAllText(Path.Combine(root, "theme.json"), "{\"Elements\":[{\"Kind\":\"Panel\",\"Width\":5,\"Height\":5}]}");
        var old = ThemeStore.Load(root);
        Check(loaded.Elements[0].Group == "Clock" && loaded.Elements[1].Group == null && loaded.Elements[2].Group!.Length == 64 && old.Elements[0].Group == null,
            "Group names are saved with the theme; blank ones are dropped and older themes load without groups");

        // Even scaling keeps a group's layout: positions, sizes, text, rings, corners and lines all follow.
        var text = new ThemeElement { Kind = ThemeElementKind.Data, X = 150, Y = 120, Font = new ThemeFont { Size = 20 }, GlowSize = 10 };
        var ring = new ThemeElement { Kind = ThemeElementKind.Arc, X = 120, Y = 140, Diameter = 60, Width = 60, Height = 60, Thickness = 8 };
        var card = new ThemeElement { Kind = ThemeElementKind.Panel, X = 100, Y = 100, Width = 200, Height = 100, CornerRadius = 16, BorderWidth = 1.5 };
        var picture = new ThemeElement { Kind = ThemeElementKind.Image, X = 110, Y = 110, Width = 0 };
        foreach (var e in new[] { text, ring, card, picture }) EditorGeometry.Scale(e, new Point(100, 100), 2);
        Check(text is { X: 200, Y: 140, Font.Size: 40, GlowSize: 20 } && ring is { X: 140, Y: 180, Diameter: 120, Thickness: 16 } &&
              card is { X: 100, Y: 100, Width: 400, Height: 200, CornerRadius: 32, BorderWidth: 3 } && picture is { X: 120, Y: 120, Width: 2 },
            "Scaling a group scales every part about one point, text and ring thickness included");
        var (origin, factor) = EditorGeometry.GroupScale(new Rect(100, 100, 200, 100), new Rect(100, 100, 400, 120), 2);
        var (origin2, factor2) = EditorGeometry.GroupScale(new Rect(100, 100, 200, 100), new Rect(150, 100, 150, 100), 0);
        Check(origin == new Point(100, 100) && factor == 2 && origin2 == new Point(300, 200) && Math.Abs(factor2 - 0.75) < 1e-9 &&
              EditorGeometry.Scaled(new Rect(100, 100, 200, 100), origin, factor) == new Rect(100, 100, 400, 200),
            "Dragging a group's corner scales it evenly from the opposite corner, by whichever side changed more");

        Check(EditorGeometry.ReorderPlan(6, [1, 2], +1).SequenceEqual([(2, 3), (1, 2)]) &&
              EditorGeometry.ReorderPlan(6, [4, 5], +1).Count == 0 &&
              EditorGeometry.ReorderPlan(6, [3, 5], +1).SequenceEqual([(3, 4)]) &&
              EditorGeometry.ReorderPlan(6, [0, 2], -1).SequenceEqual([(2, 1)]),
            "Forward and Backward move a selection together and stop at the ends");

        // Colors: by role on many elements, and swapping a color wherever it is used.
        var label = new ThemeElement { Kind = ThemeElementKind.Data, Font = new ThemeFont { Color = "#FF76F5D9" }, GlowColor = "#8076F5D9" };
        var gauge = new ThemeElement { Kind = ThemeElementKind.Arc, ForeColor = "#FF76F5D9", BackColor = "#33FFFFFF" };
        var plate = new ThemeElement { Kind = ThemeElementKind.Panel, FillColor = "#CC10161E", BorderColor = "#4076F5D9", BorderWidth = 1 };
        var bare = new ThemeElement { Kind = ThemeElementKind.Panel, FillColor = "#FF10161E", BorderColor = "#FFFF0000" }; // no border drawn
        var set = new List<ThemeElement> { label, gauge, plate, bare };
        Check(ThemeColors.Roles(set).SequenceEqual([ThemeColors.Text, ThemeColors.Fill, ThemeColors.Track, ThemeColors.Background, ThemeColors.Border, ThemeColors.Glow]) &&
              ThemeColors.Palette(set).First() == ("#FF76F5D9", 4) && ThemeColors.Palette(set).All(p => p.Color != "#FFFF0000"),
            "Only the colors an element actually shows are listed, most used first, opacity aside");
        int swapped = ThemeColors.Replace(set, "#FF76F5D9", "#FFFF8800");
        Check(swapped == 4 && label.Font!.Color == "#FFFF8800" && label.GlowColor == "#80FF8800" && plate.BorderColor == "#40FF8800" &&
              gauge.BackColor == "#33FFFFFF" && bare.BorderColor == "#FFFF0000",
            "Swapping a color changes every use of it and keeps each use's own opacity");
        int filled = ThemeColors.SetRole(set, ThemeColors.Background, "#FF202A36", "#FF10161E");
        string platePart = plate.FillColor;
        int solid = ThemeColors.SetRole([plate], ThemeColors.Background, "#80000000", "#FF10161E");
        Check(filled == 2 && bare.FillColor == "#FF202A36" && platePart == "#CC202A36" && solid == 1 && plate.FillColor == "#80000000" &&
              ThemeColors.SetRole(set, ThemeColors.Line, "#FF000000") == 0 && ThemeColors.Replace(set, "not a color", "#FF000000") == 0,
            "Setting a part on all elements keeps their opacity unless the opacity was changed too");
        Check(ThemeColors.Alike(new ThemeElement { Kind = ThemeElementKind.Bar, DataName = "CPU_LOAD" }, new ThemeElement { Kind = ThemeElementKind.Bar, DataName = "GPU_LOAD" }) &&
              !ThemeColors.Alike(new ThemeElement { Kind = ThemeElementKind.Bar }, new ThemeElement { Kind = ThemeElementKind.Bar, DataName = "GPU_LOAD" }) &&
              !ThemeColors.Alike(label, gauge),
            "Select similar matches elements of the same kind, keeping plates apart from data bars");

        // Widgets: one group each, unique names, on screen, and clean under the quality gate.
        var withClock = new ThemeDefinition();
        withClock.Elements.AddRange(ThemeWidgets.Place(ThemeWidgets.All[0], withClock));
        var second = ThemeWidgets.Place(ThemeWidgets.All[0], withClock);
        Check(withClock.Elements.All(e => e.Group == "Clock") && second.All(e => e.Group == "Clock 2") &&
              ThemeWidgets.UniqueGroup("Group", []) == "Group",
            "A widget is added as one group, numbered when the theme already has one");

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                foreach (var (w, h) in new[] { (1920, 480), (682, 2560) })
                    foreach (var widget in ThemeWidgets.All)
                    {
                        var t = new ThemeDefinition { Width = w, Height = h, BackgroundColor = "#FF05080C" };
                        t.Elements.AddRange(ThemeWidgets.Place(widget, t));
                        var card0 = t.Elements[0];
                        string? shot = shots == null ? null : Path.Combine(shots, $"widget-{w}x{h}-{widget.Name.Replace(' ', '-')}.png");
                        var problems = TemplateChecks.Problems(t, root, shot);
                        problems.AddRange(TemplateChecks.StressProblems(t, root));
                        Check(problems.Count == 0 && t.Elements.Select(e => e.Group).Distinct().Count() == 1 &&
                              card0.X >= 0 && card0.Y >= 0 && card0.X + card0.Width <= w && card0.Y + card0.Height <= h,
                            $"Widget '{widget.Name}' fits a {w}x{h} screen and passes the quality gate" + (problems.Count > 0 ? ": " + string.Join("; ", problems) : ""));
                    }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw failure;
    }
}
