using System.IO;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Pulsewave;

public enum MediaPlayback { None, Playing, Paused }

public enum MediaCommand { PlayPause, Next, Previous }

/// <summary>Immutable media snapshot; Art is frozen so the UI thread can use it directly.</summary>
public sealed record NowPlaying(MediaPlayback State, string Title, string Artist, string Source, BitmapSource? Art)
{
    /// <summary>The album, when the player reports one.</summary>
    public string Album { get; init; } = "";
    public static readonly NowPlaying Nothing = new(MediaPlayback.None, "", "", "", null);
}

/// <summary>Dashboard wording for a snapshot, separate from WinRT so it is testable.</summary>
public sealed record NowPlayingView(string Status, string Title, string Artist, bool Dim)
{
    public static NowPlayingView From(NowPlaying media) => media.State switch
    {
        MediaPlayback.Playing or MediaPlayback.Paused => new(
            media.State == MediaPlayback.Playing ? "▶  PLAYING" : "❚❚  PAUSED",
            string.IsNullOrWhiteSpace(media.Title) ? "Unknown title" : media.Title.Trim(),
            media.Artist.Trim(),
            media.State == MediaPlayback.Paused),
        _ => new("NOW PLAYING", "Nothing playing", "", true)
    };
}

// IsPreferred marks the user's main player (Plexamp): it wins ties at the same playback state.
internal sealed record MediaSessionInfo(MediaPlayback State, bool IsCurrent, bool IsPreferred = false);

/// <summary>
/// Reads title, artist, playback state and album art through the public Windows
/// media-session API (the same source as the volume flyout), plus Plexamp's local
/// player timeline (see PlexampReader). Metadata only: no audio, screen or window
/// capture. Play/pause and skip are sent only when the user taps a media control.
/// </summary>
public sealed class NowPlayingMonitor : IDisposable
{
    private const int MaxArtBytes = 8 * 1024 * 1024;
    private readonly PlexampReader _plexamp = new();
    private CancellationTokenSource? _cts;
    private NowPlaying _current = NowPlaying.Nothing;

    public NowPlaying Current => Volatile.Read(ref _current);

    private PlexampTimeline? _timeline;

    /// <summary>Plexamp's latest timeline (position, volume, shuffle, repeat, queue) while Plexamp is the player shown; else null.</summary>
    internal PlexampTimeline? Plexamp => Volatile.Read(ref _timeline);

    /// <summary>Seek, shuffle, repeat, volume or jump in the queue on Plexamp; sent only when the user taps.</summary>
    internal Task<bool> PlexampControlAsync(PlexampControl control, long value) => _plexamp.ControlAsync(control, value);

    /// <summary>Plexamp's play queue, or null when it can't be read (no token, not Plexamp, server unreachable).</summary>
    internal async Task<IReadOnlyList<PlexampQueueItem>?> PlexampQueueAsync() =>
        Plexamp is { } timeline ? await _plexamp.QueueAsync(timeline) : null;

    /// <summary>Plex token for Plexamp track details; null shows a setup hint instead.</summary>
    public string? PlexToken { get => _plexamp.Token; set => _plexamp.Token = value; }

    // Called only by the UI thread.
    public void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Run(() => RunAsync(token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        Volatile.Write(ref _current, NowPlaying.Nothing);
        Volatile.Write(ref _timeline, null);
    }

    public void Dispose() { Stop(); _plexamp.Dispose(); }

    /// <summary>
    /// Sends play/pause, next or previous to the player shown now (Plexamp, or the Windows media
    /// session it came from). False when nothing is playing or the player refused.
    /// </summary>
    public async Task<bool> SendAsync(MediaCommand command)
    {
        var shown = Current;
        if (shown.State == MediaPlayback.None) return false;
        try
        {
            if (shown.Source == PlexampReader.Source) return await _plexamp.SendAsync(command, shown.State);
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = manager.GetSessions().FirstOrDefault(s => s.SourceAppUserModelId == shown.Source) ?? manager.GetCurrentSession();
            if (session is null) return false;
            return command switch
            {
                MediaCommand.PlayPause => await session.TryTogglePlayPauseAsync(),
                MediaCommand.Next => await session.TrySkipNextAsync(),
                _ => await session.TrySkipPreviousAsync()
            };
        }
        catch (Exception)
        {
            return false; // the player closed or the media service restarted
        }
    }

    // Prefer what is audibly playing; Windows' "current" session can be a paused app
    // while a browser tab plays. A paused session still shows so the tile does not flicker.
    // Within the same state: preferred player, then Windows' current session, then list order.
    // Playing (8-11) always outranks paused (4-7).
    internal static int Choose(IReadOnlyList<MediaSessionInfo> sessions)
    {
        int best = -1, bestScore = 0;
        for (int i = 0; i < sessions.Count; i++)
        {
            if (sessions[i].State == MediaPlayback.None) continue;
            int score = (sessions[i].State == MediaPlayback.Playing ? 8 : 4)
                        + (sessions[i].IsPreferred ? 2 : 0) + (sessions[i].IsCurrent ? 1 : 0);
            if (score > bestScore) { best = i; bestScore = score; }
        }
        return best;
    }

    private static MediaPlayback Map(GlobalSystemMediaTransportControlsSessionPlaybackStatus? status) => status switch
    {
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => MediaPlayback.Playing,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => MediaPlayback.Paused,
        _ => MediaPlayback.None
    };

    private async Task RunAsync(CancellationToken token)
    {
        GlobalSystemMediaTransportControlsSessionManager? manager = null;
        string artKey = "";
        BitmapSource? art = null;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            try
            {
                // Plexamp does not use Windows media controls; it is one more candidate.
                var plexamp = await _plexamp.PollAsync(token);
                IReadOnlyList<GlobalSystemMediaTransportControlsSession> sessions = [];
                var infos = new List<MediaSessionInfo>();
                try
                {
                    manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(token);
                    sessions = manager.GetSessions();
                    var current = manager.GetCurrentSession();
                    infos.AddRange(sessions.Select(s => new MediaSessionInfo(
                        Map(s.GetPlaybackInfo()?.PlaybackStatus),
                        current != null && s.SourceAppUserModelId == current.SourceAppUserModelId)));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    manager = null; // The media service can restart; Plexamp still works meanwhile.
                    sessions = [];
                }
                if (plexamp != null) infos.Add(new MediaSessionInfo(plexamp.State, false, IsPreferred: true));
                int chosen = Choose(infos);
                Volatile.Write(ref _timeline, plexamp != null && chosen == infos.Count - 1 ? plexamp : null);
                if (chosen < 0) { Publish(NowPlaying.Nothing, token); continue; }
                if (plexamp != null && chosen == infos.Count - 1)
                {
                    Publish(await _plexamp.ReadAsync(plexamp, token), token);
                    continue;
                }

                var session = sessions[chosen];
                var properties = await session.TryGetMediaPropertiesAsync().AsTask(token);
                string title = properties?.Title ?? "";
                string artist = string.IsNullOrWhiteSpace(properties?.Artist) ? properties?.AlbumArtist ?? "" : properties.Artist;
                string source = session.SourceAppUserModelId ?? "";
                // Players often attach the thumbnail after the title; include its presence
                // in the key so late artwork is still picked up, without re-reading every tick.
                string key = source + "\u001f" + title + "\u001f" + artist + "\u001f" + (properties?.Thumbnail != null);
                if (key != artKey)
                {
                    artKey = key;
                    art = await ReadArtAsync(properties?.Thumbnail, token);
                }
                Publish(new NowPlaying(infos[chosen].State, title, artist, source, art) { Album = properties?.AlbumTitle?.Trim() ?? "" }, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            catch (Exception)
            {
                // The media service can restart or refuse a request; reconnect next tick.
                manager = null;
                Publish(NowPlaying.Nothing, token);
            }
        } while (await Tick(timer, token));
    }

    private static async Task<bool> Tick(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }

    // Only replace the snapshot when something changed, so the UI rebuilds nothing on a steady track.
    private void Publish(NowPlaying value, CancellationToken token)
    {
        if (token.IsCancellationRequested || value.Equals(Volatile.Read(ref _current))) return;
        Volatile.Write(ref _current, value);
    }

    private static async Task<BitmapSource?> ReadArtAsync(IRandomAccessStreamReference? reference, CancellationToken token)
    {
        if (reference is null) return null;
        try
        {
            using var stream = await reference.OpenReadAsync().AsTask(token);
            if (stream.Size == 0 || stream.Size > MaxArtBytes) return null;
            using var source = stream.AsStreamForRead();
            var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, token);
            buffer.Position = 0;
            return DecodeArt(buffer);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null; // Artwork is optional; the tile shows its placeholder.
        }
    }

    internal static BitmapSource DecodeArt(Stream stream)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 192; // 2x the 96px tile
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
