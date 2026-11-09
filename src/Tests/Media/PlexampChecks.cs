using System.IO;
using System.Text;

namespace Pulsewave.Regression;

// Plexamp parsing, the token's trusted-destination rule and encrypted storage.
// No check here talks to Plexamp or a Plex server.
internal static class PlexampChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("PLEXAMP: " + message);
        Console.WriteLine("PASS PLEXAMP: " + message);
    }

    private static string Timeline(string state, string key = "/library/metadata/12327", string address = "192-168-0-10.abc123.plex.direct") =>
        $"<MediaContainer commandID=\"1\"><Timeline state=\"{state}\" key=\"{key}\" type=\"music\" protocol=\"https\" address=\"{address}\" port=\"32400\"/>" +
        "<Timeline type=\"video\" state=\"stopped\"/></MediaContainer>";

    public static void Run()
    {
        var playing = PlexampReader.ParseTimeline(Timeline("playing"));
        Check(playing is { State: MediaPlayback.Playing, Key: "/library/metadata/12327", Protocol: "https", Port: 32400 },
            "Music timeline yields state, track key and server");
        Check(PlexampReader.ParseTimeline(Timeline("buffering"))?.State == MediaPlayback.Playing, "Buffering counts as playing");
        Check(PlexampReader.ParseTimeline(Timeline("paused"))?.State == MediaPlayback.Paused, "Paused timeline is paused");
        Check(PlexampReader.ParseTimeline(Timeline("stopped")) == null, "Stopped Plexamp reports nothing");
        Check(PlexampReader.ParseTimeline(Timeline("playing", key: "/../../etc")) == null, "Only library metadata keys are followed");

        Check(PlexampReader.IsAllowedServer("https", "192-168-0-10.abc123.plex.direct", 32400), "Plex's own plex.direct TLS names are trusted");
        Check(!PlexampReader.IsAllowedServer("http", "192-168-0-10.abc123.plex.direct", 32400), "plex.direct without TLS is refused");
        Check(!PlexampReader.IsAllowedServer("https", "plex.direct.attacker.example", 32400), "Look-alike host names are refused");
        Check(PlexampReader.IsAllowedServer("http", "192.168.0.101", 32400) && PlexampReader.IsAllowedServer("https", "10.0.0.5", 32400)
              && PlexampReader.IsAllowedServer("http", "172.16.4.2", 32400) && PlexampReader.IsAllowedServer("http", "127.0.0.1", 32400),
            "Private network and loopback servers are trusted");
        Check(!PlexampReader.IsAllowedServer("http", "8.8.8.8", 32400) && !PlexampReader.IsAllowedServer("http", "172.32.0.1", 32400)
              && !PlexampReader.IsAllowedServer("ftp", "192.168.0.101", 32400) && !PlexampReader.IsAllowedServer("http", "192.168.0.101", 0),
            "Public addresses, other schemes and invalid ports never receive the token");

        var compilation = PlexampReader.ParseTrack("<MediaContainer><Track title=\"Song\" originalTitle=\"Guest Artist\" grandparentTitle=\"Various Artists\" parentThumb=\"/library/metadata/1/thumb/2\"/></MediaContainer>");
        Check(compilation is { Title: "Song", Artist: "Guest Artist", Thumb: "/library/metadata/1/thumb/2" },
            "Track artist beats album artist and album art fills in for a missing track thumb");
        var album = PlexampReader.ParseTrack("<MediaContainer><Track title=\"Song\" grandparentTitle=\"M83\" thumb=\"/t\"/></MediaContainer>");
        Check(album is { Artist: "M83", Thumb: "/t" }, "Album artist is used when the track has no own artist");
        Check(PlexampReader.ParseTrack("<MediaContainer/>") == null, "Missing track element yields nothing");

        using (var reader = new PlexampReader())
        {
            var hint = reader.ReadAsync(playing!, CancellationToken.None).GetAwaiter().GetResult();
            Check(hint.State == MediaPlayback.Playing && hint.Title == "Plexamp" && hint.Artist.Contains("Plex token"),
                "Without a token the tile shows Plexamp is playing and how to set it up");
            reader.Token = "abcdefghij1234567890";
            var untrusted = PlexampReader.ParseTimeline(Timeline("playing", address: "8.8.8.8"))!;
            var refused = reader.ReadAsync(untrusted with { Protocol = "http" }, CancellationToken.None).GetAwaiter().GetResult();
            Check(refused.Artist.Contains("not trusted"), "An untrusted server address is reported instead of being contacted");
        }

        Check(PlexTokenStore.IsPlausible("abcDEF123-_xyz") && !PlexTokenStore.IsPlausible("short")
              && !PlexTokenStore.IsPlausible("has space in it") && !PlexTokenStore.IsPlausible(null), "Token format is validated");
        var root = Path.Combine(Environment.GetEnvironmentVariable("PULSEWAVE_TEST_OUTPUT_DIR") ?? Path.GetTempPath(), "plex-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "plex-token.dat");
        const string token = "FixtureToken_1234567890";
        PlexTokenStore.SaveFile(file, token);
        Check(!Encoding.UTF8.GetString(File.ReadAllBytes(file)).Contains(token) && !Encoding.Unicode.GetString(File.ReadAllBytes(file)).Contains(token),
            "Saved token is encrypted on disk");
        Check(PlexTokenStore.LoadFile(file) == token, "Encrypted token reads back for the same Windows user");
        File.WriteAllBytes(file, [1, 2, 3, 4]);
        Check(PlexTokenStore.LoadFile(file) == null, "A damaged token file behaves as no token");
        Check(PlexTokenStore.LoadFile(Path.Combine(root, "missing.dat")) == null, "No token file means no token");
    }
}
