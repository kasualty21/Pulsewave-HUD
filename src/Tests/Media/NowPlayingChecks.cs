using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pulsewave.Regression;

// Covers the now-playing tile without a live media session: wording, session choice,
// artwork decoding and the preference default. Live reads use --media-probe.
internal static class NowPlayingChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("MEDIA: " + message);
        Console.WriteLine("PASS MEDIA: " + message);
    }

    public static void Run()
    {
        var nothing = NowPlayingView.From(NowPlaying.Nothing);
        Check(nothing.Title == "Nothing playing" && nothing.Artist == "" && nothing.Dim, "No session shows a dim 'Nothing playing' state");
        var playing = NowPlayingView.From(new NowPlaying(MediaPlayback.Playing, "  Midnight City ", " M83 ", "Spotify.exe", null));
        Check(playing.Status.Contains("PLAYING") && playing.Title == "Midnight City" && playing.Artist == "M83" && !playing.Dim,
            "Playing track shows trimmed title and artist at full brightness");
        var paused = NowPlayingView.From(new NowPlaying(MediaPlayback.Paused, "", "", "chrome", null));
        Check(paused.Status.Contains("PAUSED") && paused.Title == "Unknown title" && paused.Dim, "Paused session without a title stays labeled and dimmed");

        MediaSessionInfo S(MediaPlayback state, bool current) => new(state, current);
        Check(NowPlayingMonitor.Choose([]) == -1, "No sessions chooses nothing");
        Check(NowPlayingMonitor.Choose([S(MediaPlayback.None, true)]) == -1, "Stopped or closed sessions are ignored");
        Check(NowPlayingMonitor.Choose([S(MediaPlayback.Paused, true), S(MediaPlayback.Playing, false)]) == 1,
            "An audibly playing app beats Windows' paused current session");
        Check(NowPlayingMonitor.Choose([S(MediaPlayback.Playing, false), S(MediaPlayback.Playing, true)]) == 1,
            "Among playing sessions Windows' current one wins");
        Check(NowPlayingMonitor.Choose([S(MediaPlayback.Paused, false), S(MediaPlayback.Paused, true)]) == 1,
            "A paused current session still shows rather than flickering to nothing");
        MediaSessionInfo Plexamp(MediaPlayback state) => new(state, false, IsPreferred: true);
        Check(NowPlayingMonitor.Choose([S(MediaPlayback.Playing, true), S(MediaPlayback.Playing, false), Plexamp(MediaPlayback.Playing)]) == 2,
            "Playing Plexamp beats other playing apps, including Windows' current session");
        Check(NowPlayingMonitor.Choose([S(MediaPlayback.Paused, true), Plexamp(MediaPlayback.Paused)]) == 1,
            "Paused Plexamp beats other paused apps");
        Check(NowPlayingMonitor.Choose([S(MediaPlayback.Playing, false), Plexamp(MediaPlayback.Paused)]) == 0,
            "Any playing app still beats paused Plexamp");

        var pixels = new byte[8 * 8 * 4];
        for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = 200; pixels[i + 3] = 255; }
        var source = BitmapSource.Create(8, 8, 96, 96, PixelFormats.Bgra32, null, pixels, 8 * 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
        using (var png = new MemoryStream())
        {
            encoder.Save(png); png.Position = 0;
            var art = NowPlayingMonitor.DecodeArt(png);
            Check(art.IsFrozen && art.PixelWidth == 192, "Album art decodes frozen at tile resolution for cross-thread use");
        }
        bool corruptRejected = false;
        try { NowPlayingMonitor.DecodeArt(new MemoryStream([1, 2, 3])); } catch (Exception) { corruptRejected = true; }
        Check(corruptRejected, "Corrupt artwork throws for the reader to fall back to the placeholder");

        using (var monitor = new NowPlayingMonitor())
        {
            monitor.Stop();
            Check(ReferenceEquals(monitor.Current, NowPlaying.Nothing), "Monitor is idle and empty until started; Stop is safe when idle");
        }

        var root = Path.Combine(Environment.GetEnvironmentVariable("PULSEWAVE_TEST_OUTPUT_DIR") ?? Path.GetTempPath(), "media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var legacy = Path.Combine(root, "legacy.json");
        File.WriteAllText(legacy, "{\"PanelOpacityPercent\":55}");
        Check(SettingsStore.LoadFile(legacy).ShowNowPlaying, "Settings from older versions turn now playing on by default");
        var settings = SettingsStore.LoadFile(legacy);
        settings.ShowNowPlaying = false;
        SettingsStore.SaveFile(legacy, settings);
        Check(!SettingsStore.LoadFile(legacy).ShowNowPlaying, "Turning now playing off persists");
    }
}
