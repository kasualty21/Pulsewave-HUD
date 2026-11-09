using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Size = System.Windows.Size;

namespace Pulsewave.Themes;

/// <summary>
/// Files inside a theme folder: imported pictures and videos, poster frames, previews and
/// cleanup. New files always get fresh names, so a video the overlay is playing is never
/// overwritten.
/// </summary>
public static class ThemeMedia
{
    public static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".bmp"];
    public static readonly string[] VideoExtensions = [".mp4", ".h264", ".mov", ".wmv", ".m4v"];

    public static bool IsVideo(string path) => VideoExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static string UniqueName(string themeDirectory, string prefix, string extension)
    {
        for (int n = 1; ; n++)
        {
            string name = n == 1 ? prefix + extension : $"{prefix}-{n}{extension}";
            if (!File.Exists(Path.Combine(themeDirectory, name))) return name;
        }
    }

    /// <summary>Decodes a picture and stores it as PNG; returns the theme-relative name.</summary>
    public static string ImportImage(string themeDirectory, string source, string prefix)
    {
        var frame = Decode(File.ReadAllBytes(source));
        string name = UniqueName(themeDirectory, prefix, ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(themeDirectory, name))!);
        SavePng(frame, Path.Combine(themeDirectory, name));
        return name;
    }

    /// <summary>Copies (or converts raw .h264 to) an MP4 the player can open; returns its name and size.</summary>
    public static (string Name, int Width, int Height) ImportVideo(string themeDirectory, string source)
    {
        string name = UniqueName(themeDirectory, "background", ".mp4");
        string target = Path.Combine(themeDirectory, name);
        try
        {
            if (source.EndsWith(".h264", StringComparison.OrdinalIgnoreCase))
            {
                using var output = File.Create(target);
                H264Mp4Writer.Convert(File.ReadAllBytes(source), output);
            }
            else File.Copy(source, target);
            using var reader = new VideoFrameReader(target); // proves Windows can decode it
            return (name, reader.Width, reader.Height);
        }
        catch
        {
            File.Delete(target);
            throw;
        }
    }

    /// <summary>First frame of a theme video as a PNG, used while the video starts and in previews.</summary>
    public static string SavePoster(string themeDirectory, string video)
    {
        using var reader = new VideoFrameReader(ThemeStore.ResolveFile(themeDirectory, video));
        var pixels = new byte[reader.Width * reader.Height * 4];
        if (!reader.ReadFrame(pixels, out _)) throw new InvalidDataException("The video has no frames.");
        var frame = BitmapSource.Create(reader.Width, reader.Height, 96, 96, PixelFormats.Bgr32, null, pixels, reader.Width * 4);
        string name = UniqueName(themeDirectory, "poster", ".png");
        SavePng(frame, Path.Combine(themeDirectory, name));
        return name;
    }

    /// <summary>Renders the theme with sample values (still background only) and saves preview.png.</summary>
    public static string SavePreview(string themeDirectory, ThemeDefinition theme)
    {
        var view = new ThemeView(theme, themeDirectory, playVideo: false);
        for (int tick = 60; tick >= 0; tick--) view.Update(ThemeSampleData.Context(tick)); // charts get a plausible history
        view.Measure(new Size(theme.Width, theme.Height));
        view.Arrange(new Rect(0, 0, theme.Width, theme.Height));
        view.UpdateLayout();
        var bitmap = new RenderTargetBitmap(theme.Width, theme.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        SavePng(bitmap, Path.Combine(themeDirectory, "preview.png"));
        return "preview.png";
    }

    /// <summary>Deletes media no longer referenced by the theme; files in use are left for next time.</summary>
    public static void RemoveUnreferenced(string themeDirectory, ThemeDefinition theme)
    {
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in new[] { theme.BackgroundVideo, theme.BackgroundImage, theme.Preview }.Concat(theme.Elements.Select(e => e.ImageFile)))
            if (file != null) keep.Add(ThemeStore.ResolveFile(themeDirectory, file));
        foreach (var file in Directory.EnumerateFiles(themeDirectory, "*", SearchOption.AllDirectories))
        {
            // Only media files are candidates; theme.json and anything else stay.
            if (keep.Contains(Path.GetFullPath(file)) ||
                !ImageExtensions.Concat(VideoExtensions).Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) continue;
            try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    public static BitmapSource Decode(byte[] bytes)
    {
        var decoder = BitmapDecoder.Create(new MemoryStream(bytes), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        if (frame.PixelWidth is < 1 or > 16384 || frame.PixelHeight is < 1 or > 16384) throw new InvalidDataException("The picture size is not supported.");
        return frame;
    }

    public static void SavePng(BitmapSource source, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var output = File.Create(path);
        encoder.Save(output);
    }
}
