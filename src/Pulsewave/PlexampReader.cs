using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace Pulsewave;

internal sealed record PlexampTimeline(MediaPlayback State, string Key, string Protocol, string Address, int Port)
{
    public long TimeMs { get; init; }
    public long DurationMs { get; init; }
    /// <summary>Plexamp's own volume 0–100, or null when it doesn't say.</summary>
    public int? Volume { get; init; }
    public bool? Shuffle { get; init; }
    /// <summary>0 off, 1 repeat this track, 2 repeat all.</summary>
    public int? Repeat { get; init; }
    public string PlayQueueId { get; init; } = "";
    public string PlayQueueItemId { get; init; } = "";
    /// <summary>What Plexamp says it can be told (seekTo, shuffle, repeat, volume…).</summary>
    public IReadOnlySet<string> Controllable { get; init; } = new HashSet<string>();
    public bool Can(string control) => Controllable.Contains(control);
}

internal enum PlexampControl { Seek, Shuffle, Repeat, Volume, SkipTo }

/// <summary>A track in Plexamp's play queue.</summary>
internal sealed record PlexampQueueItem(string ItemId, string Title, string Artist, long DurationMs, bool Current);

/// <summary>
/// Plexamp does not register with Windows media controls, but as a Plex player it reports
/// its timeline on 127.0.0.1:32500 without authentication. Track details come from the
/// Plex server named in that timeline and need the user's token.
/// </summary>
internal sealed class PlexampReader : IDisposable
{
    internal const string Source = "Plexamp";
    private const int MaxArtBytes = 8 * 1024 * 1024;
    private static readonly TimeSpan FailureRetry = TimeSpan.FromSeconds(30);
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly Func<long> _clockMs;
    private int _commandId;
    private string _cachedKey = "";
    private NowPlaying? _cached;
    private long _retryAt;
    private string? _token;

    public PlexampReader() : this(() => Environment.TickCount64) { }
    internal PlexampReader(Func<long> clockMs)
    {
        _clockMs = clockMs;
        _http.DefaultRequestHeaders.Add("X-Plex-Client-Identifier", "pulsewave-now-playing");
        _http.DefaultRequestHeaders.Add("X-Plex-Product", "Lumen HUD");
        _http.DefaultRequestHeaders.Add("Accept", "application/xml");
    }

    public string? Token
    {
        get => Volatile.Read(ref _token);
        set => Volatile.Write(ref _token, value);
    }

    public void Dispose() => _http.Dispose();

    /// <summary>Plexamp's local player-control path for a command (the Plex Companion protocol it answers on port 32500).</summary>
    internal static string CommandPath(MediaCommand command, MediaPlayback state) => command switch
    {
        MediaCommand.PlayPause => state == MediaPlayback.Playing ? "/player/playback/pause" : "/player/playback/play",
        MediaCommand.Next => "/player/playback/skipNext",
        _ => "/player/playback/skipPrevious"
    } + "?type=music";

    /// <summary>Sends a command to the local Plexamp player; no token is needed or sent.</summary>
    public async Task<bool> SendAsync(MediaCommand command, MediaPlayback state)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                "http://127.0.0.1:32500" + CommandPath(command, state) + "&commandID=" + Interlocked.Increment(ref _commandId));
            using var response = await _http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>Null when Plexamp is not running, not answering, or stopped.</summary>
    public async Task<PlexampTimeline?> PollAsync(CancellationToken token)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(1.5));
            string xml = await _http.GetStringAsync(
                "http://127.0.0.1:32500/player/timeline/poll?wait=0&commandID=" + Interlocked.Increment(ref _commandId), timeout.Token);
            return ParseTimeline(xml);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Xml.XmlException && !token.IsCancellationRequested)
        {
            return null;
        }
    }

    internal static PlexampTimeline? ParseTimeline(string xml)
    {
        var music = XDocument.Parse(xml).Root?.Elements("Timeline")
            .FirstOrDefault(t => (string?)t.Attribute("type") == "music");
        if (music is null) return null;
        var state = (string?)music.Attribute("state") switch
        {
            "playing" or "buffering" => MediaPlayback.Playing,
            "paused" => MediaPlayback.Paused,
            _ => MediaPlayback.None
        };
        string key = (string?)music.Attribute("key") ?? "";
        if (state == MediaPlayback.None || !key.StartsWith("/library/metadata/", StringComparison.Ordinal)) return null;
        int.TryParse((string?)music.Attribute("port"), out int port);
        static long Long(XAttribute? a) => long.TryParse((string?)a, out long v) && v >= 0 ? v : 0;
        static int? Int(XAttribute? a) => int.TryParse((string?)a, out int v) ? v : null;
        return new(state, key, (string?)music.Attribute("protocol") ?? "", (string?)music.Attribute("address") ?? "", port)
        {
            TimeMs = Long(music.Attribute("time")), DurationMs = Long(music.Attribute("duration")),
            Volume = Int(music.Attribute("volume")) is { } v ? Math.Clamp(v, 0, 100) : null,
            Shuffle = Int(music.Attribute("shuffle")) is { } s ? s != 0 : null,
            Repeat = Int(music.Attribute("repeat")) is { } r ? Math.Clamp(r, 0, 2) : null,
            PlayQueueId = (string?)music.Attribute("playQueueID") ?? "", PlayQueueItemId = (string?)music.Attribute("playQueueItemID") ?? "",
            Controllable = ((string?)music.Attribute("controllable") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
        };
    }

    /// <summary>Plexamp's local player-control path for seeking, shuffle, repeat, volume or jumping in the queue.</summary>
    internal static string? ControlPath(PlexampControl control, long value) => control switch
    {
        PlexampControl.Seek => $"/player/playback/seekTo?type=music&offset={Math.Max(0, value)}",
        PlexampControl.Shuffle => $"/player/playback/setParameters?type=music&shuffle={(value != 0 ? 1 : 0)}",
        PlexampControl.Repeat => $"/player/playback/setParameters?type=music&repeat={Math.Clamp(value, 0, 2)}",
        PlexampControl.Volume => $"/player/playback/setParameters?type=music&volume={Math.Clamp(value, 0, 100)}",
        PlexampControl.SkipTo => value > 0 ? $"/player/playback/skipTo?type=music&playQueueItemID={value}" : null,
        _ => null
    };

    /// <summary>Sends a control to the local Plexamp player (no token); false if it didn't answer or refused.</summary>
    public async Task<bool> ControlAsync(PlexampControl control, long value)
    {
        if (ControlPath(control, value) is not { } path) return false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:32500" + path + "&commandID=" + Interlocked.Increment(ref _commandId));
            using var response = await _http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return false; }
    }

    /// <summary>
    /// The play queue from the Plex server Plexamp names (the token goes only to allowed servers):
    /// the current track and the (at most 30) tracks Plexamp has queued after it. Null when it can't be read.
    /// </summary>
    public async Task<IReadOnlyList<PlexampQueueItem>?> QueueAsync(PlexampTimeline timeline, CancellationToken token = default)
    {
        string? plexToken = Token;
        if (plexToken is null || !long.TryParse(timeline.PlayQueueId, out long id) || !IsAllowedServer(timeline.Protocol, timeline.Address, timeline.Port)) return null;
        try
        {
            var server = new Uri($"{timeline.Protocol}://{timeline.Address}:{timeline.Port}");
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(server, $"/playQueues/{id}?window=31&includeBefore=0&includeAfter=1"));
            request.Headers.Add("X-Plex-Token", plexToken);
            using var response = await _http.SendAsync(request, token);
            if (!response.IsSuccessStatusCode) return null;
            return ParseQueue(await response.Content.ReadAsStringAsync(token), timeline.PlayQueueItemId);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Xml.XmlException && !token.IsCancellationRequested)
        {
            return null;
        }
    }

    internal static IReadOnlyList<PlexampQueueItem> ParseQueue(string xml, string currentItemId)
    {
        var root = XDocument.Parse(xml).Root;
        if (root is null) return [];
        string selected = currentItemId.Length > 0 ? currentItemId : (string?)root.Attribute("playQueueSelectedItemID") ?? "";
        return root.Elements().Where(e => e.Name.LocalName == "Track").Select(t =>
        {
            string itemId = (string?)t.Attribute("playQueueItemID") ?? "";
            string artist = (string?)t.Attribute("originalTitle") is { Length: > 0 } a ? a : (string?)t.Attribute("grandparentTitle") ?? "";
            long.TryParse((string?)t.Attribute("duration"), out long duration);
            return new PlexampQueueItem(itemId, (string?)t.Attribute("title") ?? "", artist, duration, itemId == selected);
        }).Where(i => i.ItemId.Length > 0).ToList();
    }

    // The token only goes to Plex's own TLS names or the local network, never to an
    // arbitrary host that something answering on port 32500 might claim.
    internal static bool IsAllowedServer(string protocol, string address, int port)
    {
        if (port is < 1 or > 65535 || address.Length == 0) return false;
        if (protocol == "https" && address.EndsWith(".plex.direct", StringComparison.OrdinalIgnoreCase)
            && Uri.CheckHostName(address) == UriHostNameType.Dns) return true;
        if (protocol is not ("http" or "https") || !IPAddress.TryParse(address, out var ip)) return false;
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetworkV6) return ip.IsIPv6UniqueLocal || ip.IsIPv6LinkLocal;
        var b = ip.GetAddressBytes();
        return b[0] == 10 || b[0] == 172 && b[1] is >= 16 and <= 31 || b[0] == 192 && b[1] == 168;
    }

    /// <summary>Track details for a timeline; failures become a readable status on the tile.</summary>
    public async Task<NowPlaying> ReadAsync(PlexampTimeline timeline, CancellationToken token)
    {
        string? plexToken = Token;
        string key = $"{timeline.Protocol}://{timeline.Address}:{timeline.Port}{timeline.Key}|{plexToken?.GetHashCode()}";
        if (key == _cachedKey && _cached != null && (_cached.Art != null || _clockMs() < _retryAt || _retryAt == 0))
            return _cached with { State = timeline.State };

        NowPlaying Status(string message, bool retryLater)
        {
            _cachedKey = key;
            _cached = new NowPlaying(timeline.State, Source, message, Source, null);
            _retryAt = retryLater ? _clockMs() + (long)FailureRetry.TotalMilliseconds : 0;
            return _cached;
        }

        if (plexToken is null) return Status("Set your Plex token in the tray menu", retryLater: false);
        if (!IsAllowedServer(timeline.Protocol, timeline.Address, timeline.Port))
            return Status("Plex server address not trusted", retryLater: false);

        var server = new Uri($"{timeline.Protocol}://{timeline.Address}:{timeline.Port}");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(server, timeline.Key));
            request.Headers.Add("X-Plex-Token", plexToken);
            using var response = await _http.SendAsync(request, token);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return Status("Plex token was rejected", retryLater: true);
            response.EnsureSuccessStatusCode();
            string trackXml = await response.Content.ReadAsStringAsync(token);
            var track = ParseTrack(trackXml);
            if (track is null) return Status("Track details unavailable", retryLater: true);
            var art = track.Value.Thumb is { } thumb ? await ReadArtAsync(server, thumb, plexToken, token) : null;
            _cachedKey = key;
            _cached = new NowPlaying(timeline.State, track.Value.Title, track.Value.Artist, Source, art) { Album = ParseAlbum(trackXml) };
            // Missing art retries later; artwork is the part most likely to arrive late or time out.
            _retryAt = art is null ? _clockMs() + (long)FailureRetry.TotalMilliseconds : 0;
            return _cached;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Xml.XmlException && !token.IsCancellationRequested)
        {
            return Status("Plex server unreachable", retryLater: true);
        }
    }

    /// <summary>The track's album (parentTitle), or "".</summary>
    internal static string ParseAlbum(string xml) =>
        (string?)XDocument.Parse(xml).Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Track")?.Attribute("parentTitle") ?? "";

    internal static (string Title, string Artist, string? Thumb)? ParseTrack(string xml)
    {
        var track = XDocument.Parse(xml).Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Track");
        if (track is null) return null;
        string title = (string?)track.Attribute("title") ?? "";
        // originalTitle is the track artist on compilations; grandparentTitle is the album artist.
        string artist = (string?)track.Attribute("originalTitle") is { Length: > 0 } trackArtist
            ? trackArtist : (string?)track.Attribute("grandparentTitle") ?? "";
        string? thumb = new[] { "thumb", "parentThumb", "grandparentThumb" }
            .Select(name => (string?)track.Attribute(name)).FirstOrDefault(v => !string.IsNullOrEmpty(v));
        return (title, artist, thumb);
    }

    private async Task<BitmapSource?> ReadArtAsync(Uri server, string thumb, string plexToken, CancellationToken token)
    {
        try
        {
            var uri = new Uri(server, "/photo/:/transcode?width=192&height=192&minSize=1&upscale=1&url=" + Uri.EscapeDataString(thumb));
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("X-Plex-Token", plexToken);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxArtBytes) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, token)) > 0)
            {
                if (buffer.Length + read > MaxArtBytes) return null;
                buffer.Write(chunk, 0, read);
            }
            buffer.Position = 0;
            return NowPlayingMonitor.DecodeArt(buffer);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
        {
            return null; // Artwork is optional.
        }
    }
}
