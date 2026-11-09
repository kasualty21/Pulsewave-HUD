using System.IO;
using System.IO.Compression;
using System.Windows.Media.Imaging;
using DrawingColor = System.Drawing.Color;
using KnownColor = System.Drawing.KnownColor;

namespace Pulsewave.Themes;

public sealed record ImportResult(string ThemeId, ThemeDefinition Theme, IReadOnlyList<string> Notes);

/// <summary>
/// Converts an L-Connect 3 theme (.template, or a .zip with the template, its .h264
/// background and optional fonts) into a Pulsewave theme folder. The template is read as
/// plain data by <see cref="NrbfReader"/>; nothing from it is executed or instantiated.
/// </summary>
public static class LConnectImporter
{
    private const long MaxZipEntryBytes = 512L * 1024 * 1024;
    private const int MaxZipEntries = 200;

    public static ImportResult Import(string sourcePath, string themesRoot)
    {
        Directory.CreateDirectory(themesRoot);
        string staging = Path.Combine(themesRoot, ".import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var notes = new List<string>();
            ThemeDefinition theme;
            if (sourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                theme = ImportZip(sourcePath, staging, notes);
            else
            {
                using var template = File.OpenRead(sourcePath);
                theme = Convert(template, Path.GetFileNameWithoutExtension(sourcePath), staging, notes,
                    names => FindVideoBeside(sourcePath, names));
            }
            ThemeStore.Save(staging, theme);
            string id = ThemeStore.NewId(themesRoot, theme.Name);
            Directory.Move(staging, Path.Combine(themesRoot, id));
            return new ImportResult(id, theme, notes);
        }
        catch
        {
            try { Directory.Delete(staging, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    private static ThemeDefinition ImportZip(string zipPath, string staging, List<string> notes)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entries = zip.Entries.Where(e => e.Length > 0).ToList();
        if (entries.Count > MaxZipEntries || entries.Any(e => e.Length > MaxZipEntryBytes))
            throw new InvalidDataException("The zip is too large to be a theme.");
        var templateEntry = entries.FirstOrDefault(e => e.Name.EndsWith(".template", StringComparison.OrdinalIgnoreCase))
                            ?? throw new InvalidDataException("The zip contains no .template file.");

        foreach (var font in entries.Where(e => e.Name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                                                e.Name.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)))
        {
            string temp = Path.Combine(staging, ".font-" + Path.GetFileName(font.Name));
            using (var source = font.Open()) using (var target = File.Create(temp)) source.CopyTo(target);
            try
            {
                ThemeFonts.Add(temp, font.Name);
                notes.Add("Added font " + Path.GetFileName(font.Name) + ".");
            }
            catch (Exception ex) when (ex is FileFormatException or IOException or ArgumentException or NotSupportedException)
            { notes.Add("Skipped unreadable font " + Path.GetFileName(font.Name) + "."); }
            finally { File.Delete(temp); }
        }

        using var buffer = new MemoryStream();
        using (var stream = templateEntry.Open()) stream.CopyTo(buffer);
        buffer.Position = 0;
        return Convert(buffer, Path.GetFileNameWithoutExtension(templateEntry.Name), staging, notes, names =>
        {
            var videos = entries.Where(e => e.Name.EndsWith(".h264", StringComparison.OrdinalIgnoreCase) ||
                                            e.Name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)).ToList();
            var entry = videos.FirstOrDefault(v => names.Contains(v.Name, StringComparer.OrdinalIgnoreCase)) ?? videos.FirstOrDefault();
            if (entry is null) return null;
            using var stream = entry.Open();
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            return (bytes.ToArray(), entry.Name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase));
        });
    }

    // L-Connect keeps videos beside the template or in its sibling "video" folder,
    // often with an .mp4 next to the .h264; the .mp4 needs no conversion.
    private static (byte[] Bytes, bool IsMp4)? FindVideoBeside(string templatePath, IReadOnlyCollection<string> names)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(templatePath))!;
        var folders = new[] { dir, Path.Combine(dir, "..", "video") }.Where(Directory.Exists);
        foreach (var folder in folders)
            foreach (var name in names)
            {
                string stem = Path.GetFileNameWithoutExtension(name);
                foreach (var (ext, mp4) in new[] { (".mp4", true), (".h264", false) })
                {
                    string candidate = Path.Combine(folder, stem + ext);
                    if (File.Exists(candidate) && new FileInfo(candidate).Length <= MaxZipEntryBytes)
                        return (File.ReadAllBytes(candidate), mp4);
                }
            }
        return null;
    }

    internal static ThemeDefinition Convert(Stream template, string fallbackName, string themeDirectory, List<string> notes,
        Func<IReadOnlyCollection<string>, (byte[] Bytes, bool IsMp4)?> findVideo)
    {
        if (NrbfReader.Read(template) is not NrbfObject root || root.Get("GraphList") is null && root.Get("width") is null)
            throw new InvalidDataException("This is not an L-Connect theme template.");

        var theme = new ThemeDefinition
        {
            Name = root.String("name") is { Length: > 0 } name ? name.Trim() : fallbackName,
            Source = "L-Connect 3 import",
            Width = (int)root.Number("width", 1920) is > 0 and <= 8192 and var w ? w : 1920,
            Height = (int)root.Number("height", 480) is > 0 and <= 8192 and var h ? h : 480
        };
        int imageCount = 0;
        string? SaveImage(object? bitmap, string fileName)
        {
            if (bitmap is not NrbfObject { } b || b.Get("Data") is not byte[] data || data.Length == 0) return null;
            try
            {
                var decoder = BitmapDecoder.Create(new MemoryStream(data), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(decoder.Frames[0]));
                string relative = fileName;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(themeDirectory, relative))!);
                using var output = File.Create(Path.Combine(themeDirectory, relative));
                encoder.Save(output);
                return relative;
            }
            catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException)
            {
                notes.Add("Skipped an image that could not be decoded.");
                return null;
            }
        }

        theme.Preview = SaveImage(root.Get("themePic"), "preview.png");
        theme.BackgroundImage = SaveImage(root.Get("BgImg"), "background.png");

        var videoNames = new List<string>();
        void AddVideoName(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string file = path.Replace('/', '\\').Split('\\').Last();
            if (file.Length > 0 && !videoNames.Contains(file, StringComparer.OrdinalIgnoreCase)) videoNames.Add(file);
        }

        foreach (var item in Items(root.Get("GraphList")))
        {
            string type = item.String("TypeName") ?? "";
            if (type == "Animation")
            {
                AddVideoName(item.String("FilePath")); AddVideoName(item.String("videoName"));
                theme.BackgroundImage ??= SaveImage(item.Get("bitmap"), "background.png");
                continue;
            }
            var element = ConvertElement(item, type, () => SaveImage(item.Get("bitmap"), $"images/image-{++imageCount}.png"));
            if (element is null) { notes.Add($"Skipped unsupported element type '{type}'."); continue; }
            theme.Elements.Add(element);
        }
        foreach (var key in new[] { "videoPath", "videoName", "o_videoPath", "videoPath2", "videoPath3" })
            AddVideoName(root.String(key));

        if (videoNames.Count > 0)
        {
            var video = findVideo(videoNames);
            if (video is null) notes.Add("The background video (" + videoNames[0] + ") was not found; the still background is used instead.");
            else
            {
                string target = Path.Combine(themeDirectory, "background.mp4");
                (int Width, int Height)? size;
                if (video.Value.IsMp4)
                {
                    File.WriteAllBytes(target, video.Value.Bytes);
                    size = H264Mp4Writer.ReadMp4Size(video.Value.Bytes);
                }
                else
                {
                    using var output = File.Create(target);
                    var info = H264Mp4Writer.Convert(video.Value.Bytes, output, root.Number("VideoFrameRate", 30));
                    size = (info.Width, info.Height);
                    notes.Add($"Converted the background video: {info.Width}x{info.Height}, {info.FramesPerSecond:0.##} fps, {info.Seconds:0.#} s.");
                }
                theme.BackgroundVideo = "background.mp4";
                // The panel is natively portrait: a portrait video in a landscape theme is stored
                // turned clockwise, so turn it back.
                if (size is { } s && (s.Width > s.Height) != (theme.Width > theme.Height)) theme.BackgroundVideoRotation = -90;
            }
        }
        return theme;
    }

    internal static IEnumerable<NrbfObject> Items(object? list) => list switch
    {
        List<object?> items => items.OfType<NrbfObject>(),
        NrbfObject o when o.Get("_items") is { } items => Items(items),
        NrbfObject o when o.Members.TryGetValue("Collection`1+items", out var inner) => Items(inner),
        _ => []
    };

    private static ThemeElement? ConvertElement(NrbfObject item, string type, Func<string?> saveImage)
    {
        var data = item.Object("m_data");
        var element = new ThemeElement
        {
            X = item.Number("posX"), Y = item.Number("posY"), Hidden = item.Bool("hide"),
            DataName = data?.String("DataName"), DataFormat = data?.String("SubName"),
            ShowUnit = data?.Bool("ShowUnit") ?? false, Fahrenheit = item.Bool("fahrenheit")
        };
        element.Maximum = DefaultMaximum(element.DataName);
        switch (type)
        {
            case "Image":
                element.Kind = ThemeElementKind.Image;
                element.ImageFile = saveImage();
                if (element.ImageFile is null) return null;
                double zoom = item.Number("zoom_rate", 1) is > 0 and < 20 and var z ? z : 1;
                element.Width = zoom; // scale factor; the renderer multiplies by the image's pixel size
                return element;
            case "Text":
                element.Kind = ThemeElementKind.Text;
                element.Text = data?.String("_Value") ?? data?.String("ValueWithUnit") ?? "";
                element.DataName = null; element.DataFormat = null;
                element.Font = FontOf(item.Object("fontConfig"));
                return element;
            case "Data":
                element.Kind = ThemeElementKind.Data;
                element.Font = FontOf(item.Object("fontConfig"));
                return element;
            case "StatuBar":
                element.Kind = ThemeElementKind.Bar;
                element.Width = item.Number("width"); element.Height = item.Number("height");
                element.CornerRadius = item.Number("radius");
                if (item.Bool("useSubsection")) { element.SegmentWidth = item.Number("SplitBlockWidth"); element.SegmentGap = item.Number("SplitBlankWidth"); }
                ApplyColors(element, item);
                return element;
            case "ArchBar":
                element.Kind = ThemeElementKind.Arc;
                element.Diameter = item.Number("diameter"); element.Thickness = item.Number("archWidth");
                element.Width = element.Height = element.Diameter;
                element.SweepAngle = item.Number("totalAngel", 360) is > 0 and <= 360 and var sweep ? sweep : 360;
                element.StartAngle = -90 + item.Number("startPer") * 3.6;
                element.RoundCaps = item.Bool("round");
                if (item.Bool("useBlock")) { element.SegmentWidth = item.Number("SplitBlockWidth"); element.SegmentGap = item.Number("SplitBlankWidth"); }
                ApplyColors(element, item);
                return element;
            case "Chart":
                element.Kind = ThemeElementKind.Chart;
                element.Width = item.Number("_width"); element.Height = item.Number("_height");
                element.LineColor = ColorOf(item.Get("LineColor"));
                element.FillColor = ColorOf(item.Get("FillColor"));
                element.BorderColor = ColorOf(item.Get("BorderColor"));
                element.LineWidth = item.Number("lineWidth", 1);
                element.BorderWidth = item.Number("borderWidth");
                element.Samples = Math.Clamp((int)(data?.Number("queueLen", 30) ?? 30), 2, 600);
                // Network charts in L-Connect use a different unit scale; follow the data's own peak.
                element.Maximum = element.DataName is "UPSPEED" or "DOWNDSPEED" ? 0 : item.Number("maxValue", 100);
                return element;
            default:
                return null;
        }
    }

    private static void ApplyColors(ThemeElement element, NrbfObject item)
    {
        element.ForeColor = ColorOf(item.Get("FrontColor"), item.Number("FrontAlpha"));
        element.BackColor = item.Bool("trBack") ? "#00000000" : ColorOf(item.Get("BackColor"), item.Number("BackAlpha"));
        element.LineWidth = item.Number("lineWidth", 1);
        if (item.Bool("useGradient")) element.GradientColor = ColorOf(item.Get("GradientColor"));
    }

    private static ThemeFont FontOf(NrbfObject? config)
    {
        var font = new ThemeFont();
        if (config is null) return font;
        font.Family = config.String("name") is { Length: > 0 } family ? family : "Segoe UI";
        font.Size = config.Number("size", 24) is > 0 and <= 1000 and var size ? size : 24;
        font.Bold = config.Bool("isBold");
        font.Italic = config.Bool("IsItalic");
        font.Color = ColorOf(config.Get("color"));
        font.Alignment = (int)(config.Object("alignment")?.Number("index", 0) ?? 0) switch
        {
            1 => ThemeTextAlignment.Center, 2 => ThemeTextAlignment.Right, _ => ThemeTextAlignment.Left
        };
        return font;
    }

    /// <summary>System.Drawing.Color as serialized: a known-color id, an ARGB value, or a name.</summary>
    internal static string ColorOf(object? value, double alpha = 0)
    {
        if (value is not NrbfObject c) return "#00000000";
        int known = (int)c.Number("knownColor"), state = (int)c.Number("state");
        DrawingColor color = known > 0 && Enum.IsDefined(typeof(KnownColor), known) ? DrawingColor.FromKnownColor((KnownColor)known)
            : (state & 2) != 0 ? DrawingColor.FromArgb(unchecked((int)(long)c.Number("value")))
            : c.String("name") is { Length: > 0 } name ? DrawingColor.FromName(name)
            : DrawingColor.Transparent;
        // L-Connect stores bar/arc alpha separately; 0 means "not set" (opaque) in its files.
        byte a = alpha is > 0 and < 255 ? (byte)alpha : color.A;
        return $"#{a:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    internal static double DefaultMaximum(string? data) => data switch
    {
        "CPUCLOCK" => 6000, "GPUCLOCK" => 3200,
        "CPUFAN" or "GPUFAN" or "CASEFAN1" or "CASEFAN2" => 3000, "WATERPUMP" => 4000,
        "CPUPWR" => 250, "GPUPWR" => 450,
        "UPSPEED" or "DOWNDSPEED" => 0,
        _ => 100
    };
}
