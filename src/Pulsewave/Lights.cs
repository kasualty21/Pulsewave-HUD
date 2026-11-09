using System.IO;
using System.Net.Sockets;
using Color = System.Windows.Media.Color;

namespace Pulsewave;

/// <summary>LED control through OpenRGB, saved in settings.json under "Lights".</summary>
public sealed class LightsSettings
{
    public string Address { get; set; } = "127.0.0.1";
    public int Port { get; set; } = OpenRgbClient.DefaultPort;
    /// <summary>Devices (OpenRGB identities) Pulsewave leaves alone; everything else is controlled.</summary>
    public List<string> SkipDevices { get; set; } = new();
    public string Color { get; set; } = "#00E5FF";
    public int Brightness { get; set; } = 100;
    /// <summary>Effect speed, 0 (slowest) to 100 (fastest) of each device's own range.</summary>
    public int Speed { get; set; } = 50;
    /// <summary>What was applied last ("Profile", "Color" or "Effect") and its name, for Lights on after Off.</summary>
    public string LastKind { get; set; } = "";
    public string LastValue { get; set; } = "";
    public bool Off { get; set; }
    /// <summary>The schedule turned the lights off, so it turns them back on (lights switched off by hand stay off).</summary>
    public bool ScheduledOff { get; set; }
    /// <summary>Where OpenRGB.exe was last seen running, for the Lights page's OpenRGB button ("" = Program Files).</summary>
    public string AppPath { get; set; } = "";

    public LightsSettings Copy()
    {
        var copy = (LightsSettings)MemberwiseClone();
        copy.SkipDevices = SkipDevices.ToList();
        return copy;
    }

    public void Normalize()
    {
        Address = OpenRgbClient.Addresses.Contains(Address) ? Address : "127.0.0.1";
        Port = Port is >= 1 and <= 65535 ? Port : OpenRgbClient.DefaultPort;
        SkipDevices = (SkipDevices ?? new()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().Take(256).ToList();
        Color = Lights.TryColor(Color, out _) ? Color : "#00E5FF";
        Brightness = Math.Clamp(Brightness, 0, 100);
        Speed = Math.Clamp(Speed, 0, 100);
        LastKind = LastKind is "Profile" or "Color" or "Effect" ? LastKind : "";
        LastValue = (LastValue ?? "").Length <= 256 ? LastValue ?? "" : "";
        AppPath = (AppPath ?? "").Length <= 1024 ? AppPath ?? "" : "";
    }
}

internal enum LightsKind { Profile, Color, Brightness, Effect, Off, On }

/// <summary>Something to do to the lights; Value is a profile or effect name.</summary>
/// <param name="Restore">Lights on: devices without the effect get the saved color instead of staying off.</param>
internal sealed record LightsCommand(LightsKind Kind, string Value = "", bool Restore = false);

/// <summary>What OpenRGB has: its devices, saved profiles and protocol.</summary>
internal sealed record LightsState(IReadOnlyList<OpenRgbDevice> Devices, IReadOnlyList<string> Profiles, uint Protocol);

/// <summary>
/// Runs lighting commands one at a time on a background thread, each over a fresh short connection,
/// and says what happened in plain words. Nothing runs unless the user asks (a tap or a click).
/// </summary>
internal sealed class Lights
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<(LightsState? State, string Status)> ReadAsync(LightsSettings settings)
    {
        var s = settings.Copy();
        await _gate.WaitAsync();
        try
        {
            return await Task.Run(() => Retry(() =>
            {
                using var client = OpenRgbClient.Connect(s.Address, s.Port);
                var state = new LightsState(client.Devices(), client.Profiles(), client.Protocol);
                int used = state.Devices.Count(d => !s.SkipDevices.Contains(d.Identity));
                return ((LightsState?)state, state.Devices.Count == 0
                    ? "OpenRGB is running but has no devices. Check its device list (or try the other address)."
                    : $"Connected · {used} of {state.Devices.Count} devices controlled");
            }));
        }
        catch (Exception ex) when (Explain(ex, s) is { } why) { return (null, why); }
        finally { _gate.Release(); }
    }

    /// <summary>Runs a command; Ok is false when nothing could be done (OpenRGB not running, say).</summary>
    public async Task<(bool Ok, string Status)> RunAsync(LightsSettings settings, LightsCommand command)
    {
        var s = settings.Copy();
        await _gate.WaitAsync();
        try
        {
            return await Task.Run(() => Retry(() =>
            {
                using var client = OpenRgbClient.Connect(s.Address, s.Port);
                return Apply(client, s, command);
            }));
        }
        catch (Exception ex) when (Explain(ex, s) is { } why) { return (false, why); }
        finally { _gate.Release(); }
    }

    /// <summary>Notes what was applied, so Lights on after Off brings it back.</summary>
    internal static void Remember(LightsSettings s, LightsCommand command)
    {
        switch (command.Kind)
        {
            case LightsKind.Profile: case LightsKind.Effect:
                s.LastKind = command.Kind.ToString(); s.LastValue = command.Value; s.Off = false; break;
            case LightsKind.Color: s.LastKind = "Color"; s.LastValue = ""; s.Off = false; break;
            case LightsKind.Off: s.Off = true; break;
            case LightsKind.On: s.Off = false; break;
        }
    }

    private static T Retry<T>(Func<T> run)
    {
        try { return run(); }
        catch (DeviceListChangedException) { return run(); }
    }

    internal static string? Explain(Exception ex, LightsSettings s) => ex switch
    {
        SocketException or IOException { InnerException: SocketException } =>
            $"OpenRGB isn't answering at {s.Address}:{s.Port}. Start OpenRGB and its SDK server (SDK Server tab → Start Server), or pick the other address in tray → Lights.",
        OperationCanceledException => "OpenRGB took too long to answer. Try again.",
        DeviceListChangedException => "OpenRGB's device list changed. Try again.",
        InvalidDataException or IOException or InvalidOperationException or ArgumentException => "OpenRGB: " + ex.Message,
        _ => null
    };

    /// <summary>Applies a command to the controlled devices; returns the status to show.</summary>
    internal static (bool Ok, string Status) Apply(OpenRgbClient client, LightsSettings s, LightsCommand command)
    {
        if (command.Kind == LightsKind.Profile)
        {
            if (client.Protocol < 2) return (false, "This OpenRGB is too old for profiles; update OpenRGB.");
            if (!client.Profiles().Contains(command.Value)) return (false, $"OpenRGB has no profile called '{command.Value}'.");
            client.LoadProfile(command.Value);
            // OpenRGB's SDK notes: after a profile loads, read the devices again and send each one's mode
            // (and per-LED colors), so the hardware surely shows what the profile set.
            foreach (var d in client.Devices().Where(d => !s.SkipDevices.Contains(d.Identity)))
                if (d.Active is { } mode)
                {
                    client.SetMode(d.Index, mode);
                    if (mode.PerLed && d.Colors.Count > 0) client.SetLeds(d.Index, d.Colors);
                }
            return (true, $"Loaded profile '{command.Value}'.");
        }
        if (command.Kind == LightsKind.On)
            return s.LastKind switch
            {
                // A profile that OpenRGB no longer lists (renamed, deleted, or kept by another OpenRGB copy)
                // must not leave the lights dark: they come back in the saved color instead.
                "Profile" when client.Protocol < 2 || !client.Profiles().Contains(s.LastValue) => SavedColorInstead(client, s),
                "Profile" => Apply(client, s, new LightsCommand(LightsKind.Profile, s.LastValue)),
                "Effect" => Apply(client, s, new LightsCommand(LightsKind.Effect, s.LastValue, Restore: true)),
                _ => Apply(client, s, new LightsCommand(LightsKind.Color))
            };

        var devices = client.Devices().Where(d => !s.SkipDevices.Contains(d.Identity)).ToList();
        if (devices.Count == 0) return (false, "No devices to control: OpenRGB has none, or all are unticked in tray → Lights.");
        TryColor(s.Color, out var color);
        int done = 0;
        var skipped = new List<string>();
        foreach (var d in devices)
        {
            bool ok = command.Kind switch
            {
                LightsKind.Color => SolidColor(client, d, color, s.Brightness),
                LightsKind.Off => TurnOff(client, d),
                LightsKind.Brightness => Brighten(client, d, s),
                LightsKind.Effect => Effect(client, d, command.Value, color, s) || command.Restore && SolidColor(client, d, color, s.Brightness),
                _ => false
            };
            if (ok) done++; else skipped.Add(d.Name);
        }
        string what = command.Kind switch
        {
            LightsKind.Color => $"Color {s.Color.TrimStart('#')[^6..]}",
            LightsKind.Off => "Lights off",
            LightsKind.Brightness => $"Brightness {s.Brightness}%",
            _ => $"Effect '{command.Value}'"
        };
        string result = $"{what} on {done} of {devices.Count} device{(devices.Count == 1 ? "" : "s")}.";
        if (skipped.Count > 0)
            result += command.Kind == LightsKind.Effect ? $" Not on {Names(skipped)}: no such effect."
                    : command.Kind == LightsKind.Brightness ? $" {Names(skipped)}: no brightness control in this mode."
                    : $" {Names(skipped)} can't show a set color.";
        return (done > 0, result);
    }

    private static (bool Ok, string Status) SavedColorInstead(OpenRgbClient client, LightsSettings s)
    {
        var (ok, status) = Apply(client, s, new LightsCommand(LightsKind.Color));
        return (ok, $"OpenRGB has no profile '{s.LastValue}' now, so the saved color was used. {status}");
    }

    private static string Names(List<string> names) => names.Count <= 3 ? string.Join(", ", names) : $"{names[0]}, {names[1]} and {names.Count - 2} more";

    /// <summary>Software color: Direct (per-LED) mode where the device has one, else a mode that takes one color.</summary>
    internal static bool SolidColor(OpenRgbClient client, OpenRgbDevice d, Color color, int brightness)
    {
        var mode = d.Active;
        if (mode is not { PerLed: true })
        {
            client.UseCustomMode(d.Index);
            d = client.Device(d.Index);
            mode = d.Active;
        }
        if (mode == null) return false;
        // Devices whose mode has its own brightness get it there; the rest get a dimmer color.
        var shown = mode.HasBrightness ? color : Dim(color, brightness);
        if (mode.PerLed)
        {
            if (mode.HasBrightness) client.SetMode(d.Index, mode with { Brightness = Lerp(mode.BrightnessMin, mode.BrightnessMax, brightness) });
            client.SetLeds(d.Index, Enumerable.Repeat(shown, d.Colors.Count).ToList());
            return true;
        }
        if (mode.TakesColor)
        {
            client.SetMode(d.Index, WithColor(mode, shown) with
                { Brightness = mode.HasBrightness ? Lerp(mode.BrightnessMin, mode.BrightnessMax, brightness) : mode.Brightness });
            return true;
        }
        return false;
    }

    /// <summary>A device's own Off mode when it has one, else black.</summary>
    internal static bool TurnOff(OpenRgbClient client, OpenRgbDevice d)
    {
        if (d.Modes.FirstOrDefault(m => m.Name.Equals("Off", StringComparison.OrdinalIgnoreCase)) is { } off)
        {
            client.SetMode(d.Index, off);
            return true;
        }
        return SolidColor(client, d, Colors.Black, 100);
    }

    internal static bool Brighten(OpenRgbClient client, OpenRgbDevice d, LightsSettings s)
    {
        if (d.Active is { HasBrightness: true } mode)
        {
            client.SetMode(d.Index, mode with { Brightness = Lerp(mode.BrightnessMin, mode.BrightnessMax, s.Brightness) });
            return true;
        }
        // A solid color Pulsewave set can be dimmed by sending a darker color.
        return s.LastKind is "Color" or "" && !s.Off && d.Active is { PerLed: true } or { TakesColor: true } &&
               TryColor(s.Color, out var c) && SolidColor(client, d, c, s.Brightness);
    }

    internal static bool Effect(OpenRgbClient client, OpenRgbDevice d, string name, Color color, LightsSettings s)
    {
        if (d.Modes.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is not { } mode) return false;
        var chosen = mode with
        {
            Speed = mode.HasSpeed ? Lerp(mode.SpeedMin, mode.SpeedMax, s.Speed) : mode.Speed,
            Brightness = mode.HasBrightness ? Lerp(mode.BrightnessMin, mode.BrightnessMax, s.Brightness) : mode.Brightness
        };
        if (mode.TakesColor && !mode.PerLed) chosen = WithColor(chosen, color);
        client.SetMode(d.Index, chosen);
        if (chosen.PerLed) client.SetLeds(d.Index, Enumerable.Repeat(mode.HasBrightness ? color : Dim(color, s.Brightness), d.Colors.Count).ToList());
        return true;
    }

    /// <summary>The mode set to show one color (as many copies as it needs).</summary>
    internal static OpenRgbMode WithColor(OpenRgbMode mode, Color color)
    {
        int count = (int)Math.Clamp(Math.Max(mode.Colors.Count, Math.Max(1, (int)mode.ColorsMin)), 1, Math.Max(1, mode.ColorsMax));
        return mode with { ColorMode = OpenRgbMode.ColorModeSpecific, Colors = Enumerable.Repeat(color, count).ToList() };
    }

    /// <summary>The effects the controlled devices offer, most widely available first (software modes left out).</summary>
    internal static List<(string Name, int Devices)> Effects(LightsState state, LightsSettings s) =>
        state.Devices.Where(d => !s.SkipDevices.Contains(d.Identity))
            .SelectMany(d => d.Modes.Select(m => m.Name).Distinct(StringComparer.OrdinalIgnoreCase))
            .Where(n => !n.Equals("Direct", StringComparison.OrdinalIgnoreCase) && !n.Equals("Off", StringComparison.OrdinalIgnoreCase))
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.First(), g.Count())).ToList();

    /// <summary>A point along a device's range; ranges can run backwards (min above max) and still work.</summary>
    internal static uint Lerp(uint min, uint max, int percent) =>
        (uint)Math.Round(min + (max - (double)min) * Math.Clamp(percent, 0, 100) / 100, MidpointRounding.AwayFromZero);

    internal static Color Dim(Color c, int percent)
    {
        double f = Math.Clamp(percent, 0, 100) / 100.0;
        static byte Part(double v) => (byte)Math.Round(v, MidpointRounding.AwayFromZero);
        return Color.FromRgb(Part(c.R * f), Part(c.G * f), Part(c.B * f));
    }

    internal static bool TryColor(string? text, out Color color)
    {
        color = default;
        if (text is not { Length: > 0 }) return false;
        try { color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(text); color.A = 255; return true; }
        catch (Exception ex) when (ex is FormatException or NotSupportedException) { return false; }
    }

    private static class Colors { public static readonly Color Black = Color.FromRgb(0, 0, 0); }
}
