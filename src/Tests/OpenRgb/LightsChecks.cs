using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;

namespace Pulsewave.Regression;

// LED control through OpenRGB, against a fake SDK server on an ephemeral loopback port: what is sent
// for profiles, colors, brightness, effects and off, the command whitelist, parsing limits, errors,
// settings, and the Lights page laid out at 1920x480 and 682x2560. No real OpenRGB or lights involved.
internal static class LightsChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("LIGHTS: " + message);
        Console.WriteLine("PASS LIGHTS: " + message);
    }

    private const uint PerLed = OpenRgbMode.FlagPerLedColor, Speed = OpenRgbMode.FlagSpeed, Bright = OpenRgbMode.FlagBrightness, ModeColor = OpenRgbMode.FlagModeColor;

    private static OpenRgbMode Mode(int index, string name, uint flags, uint colorMode, uint speedMin = 0, uint speedMax = 0, uint bMin = 0, uint bMax = 0,
        uint colorsMin = 0, uint colorsMax = 0, uint speed = 0, uint brightness = 0, int colors = 0) =>
        new(index, name, 100 + index, flags, speedMin, speedMax, bMin, bMax, colorsMin, colorsMax, speed, brightness, 0, colorMode,
            Enumerable.Repeat(Color.FromRgb(1, 2, 3), colors).ToList());

    private sealed class FakeDevice
    {
        public required string Name;
        public required int Type;
        public required List<OpenRgbMode> Modes;
        public required int Active;
        public required int Custom;
        public required int Leds;
        public List<Color> Colors = new();
    }

    /// <summary>A small OpenRGB SDK server: replies like OpenRGB 0.9 (protocol 3) and records what it is sent.</summary>
    private sealed class FakeServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Thread _thread;
        public readonly List<FakeDevice> Devices;
        public readonly List<string> Profiles = ["Gaming", "Night"];
        public readonly List<(uint Device, uint Command, byte[] Body)> Received = new(), Everything = new();
        public uint Version = 3;
        public bool ChangeListOnce;
        public bool Truncate;
        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public FakeServer(List<FakeDevice> devices)
        {
            Devices = devices;
            _listener.Start();
            _thread = new Thread(Serve) { IsBackground = true };
            _thread.Start();
        }

        public void Dispose() => _listener.Stop();

        private void Serve()
        {
            while (true)
            {
                TcpClient client;
                try { client = _listener.AcceptTcpClient(); } catch (SocketException) { return; } catch (ObjectDisposedException) { return; }
                using (client)
                using (var stream = client.GetStream())
                {
                    try { while (Handle(stream)) { } } catch (IOException) { }
                }
            }
        }

        private bool Handle(NetworkStream stream)
        {
            var header = new byte[16];
            int read = 0;
            while (read < 16) { int n = stream.Read(header, read, 16 - read); if (n == 0) return false; read += n; }
            uint device = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4)), command = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8));
            var body = new byte[BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12))];
            stream.ReadExactly(body);
            lock (Received) { Received.Add((device, command, body)); Everything.Add((device, command, body)); }
            switch (command)
            {
                case 40: Reply(stream, 0, 40, U32(Math.Min(Version, BinaryPrimitives.ReadUInt32LittleEndian(body)))); break;
                case 0:
                    if (ChangeListOnce) { ChangeListOnce = false; Reply(stream, 0, 100, []); }
                    Reply(stream, 0, 0, U32((uint)Devices.Count)); break;
                case 1:
                    var block = Block(Devices[(int)device], BinaryPrimitives.ReadUInt32LittleEndian(body));
                    Reply(stream, device, 1, Truncate ? block[..^3] : block); break;
                case 150:
                    var list = new MemoryStream();
                    var w = new BinaryWriter(list);
                    w.Write(0u); w.Write((ushort)Profiles.Count);
                    foreach (var p in Profiles) Text(w, p);
                    var bytes = list.ToArray();
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)bytes.Length);
                    Reply(stream, 0, 150, bytes); break;
                case 1100: Devices[(int)device].Active = Devices[(int)device].Custom; break;
                case 1101:
                    var mode = ReadMode(body);
                    var d = Devices[(int)device];
                    d.Modes[mode.Index] = mode;
                    d.Active = mode.Index; break;
                case 1050:
                    int count = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(4));
                    Devices[(int)device].Colors = Enumerable.Range(0, count).Select(i => Color.FromRgb(body[6 + i * 4], body[7 + i * 4], body[8 + i * 4])).ToList();
                    break;
            }
            return true;
        }

        public List<(uint Device, uint Command, byte[] Body)> Take()
        {
            lock (Received) { var copy = Received.ToList(); Received.Clear(); return copy; }
        }

        private static void Reply(NetworkStream stream, uint device, uint command, byte[] body)
        {
            var packet = new byte[16 + body.Length];
            "ORGB"u8.CopyTo(packet);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), device);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), command);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), (uint)body.Length);
            body.CopyTo(packet, 16);
            stream.Write(packet);
        }

        private static byte[] U32(uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); return b; }

        private static void Text(BinaryWriter w, string s) { var b = Encoding.UTF8.GetBytes(s + "\0"); w.Write((ushort)b.Length); w.Write(b); }

        private static void WriteColor(BinaryWriter w, Color c) { w.Write(c.R); w.Write(c.G); w.Write(c.B); w.Write((byte)0); }

        private static byte[] Block(FakeDevice d, uint protocol)
        {
            var stream = new MemoryStream();
            var w = new BinaryWriter(stream);
            w.Write(0u); w.Write(d.Type);
            Text(w, d.Name); Text(w, "Vendor"); Text(w, "Description"); Text(w, "1.0"); Text(w, "SN-" + d.Name); Text(w, "HID: " + d.Name);
            w.Write((ushort)d.Modes.Count); w.Write(d.Active);
            foreach (var m in d.Modes)
            {
                Text(w, m.Name); w.Write(m.Value); w.Write(m.Flags); w.Write(m.SpeedMin); w.Write(m.SpeedMax);
                if (protocol >= 3) { w.Write(m.BrightnessMin); w.Write(m.BrightnessMax); }
                w.Write(m.ColorsMin); w.Write(m.ColorsMax); w.Write(m.Speed);
                if (protocol >= 3) w.Write(m.Brightness);
                w.Write(m.Direction); w.Write(m.ColorMode);
                w.Write((ushort)m.Colors.Count);
                foreach (var c in m.Colors) WriteColor(w, c);
            }
            w.Write((ushort)1); Text(w, "Zone"); w.Write(1); w.Write((uint)d.Leds); w.Write((uint)d.Leds); w.Write((uint)d.Leds); w.Write((ushort)0);
            w.Write((ushort)d.Leds);
            for (int i = 0; i < d.Leds; i++) { Text(w, "LED " + i); w.Write((uint)i); }
            w.Write((ushort)d.Leds);
            for (int i = 0; i < d.Leds; i++) WriteColor(w, i < d.Colors.Count ? d.Colors[i] : Color.FromRgb(0, 0, 0));
            var bytes = stream.ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)bytes.Length);
            return bytes;
        }

        /// <summary>Reads an UpdateMode body the way OpenRGB does (protocol 3 layout).</summary>
        public static OpenRgbMode ReadMode(byte[] b)
        {
            int o = 0;
            uint U() { uint v = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o)); o += 4; return v; }
            ushort S() { ushort v = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(o)); o += 2; return v; }
            if (U() != b.Length) throw new InvalidDataException("UpdateMode size mismatch");
            int index = (int)U();
            int len = S(); string name = Encoding.UTF8.GetString(b, o, len - 1); o += len;
            int value = (int)U(); uint flags = U(), smin = U(), smax = U(), bmin = U(), bmax = U(), cmin = U(), cmax = U(), speed = U(), bright = U(), dir = U(), cm = U();
            int n = S();
            var colors = Enumerable.Range(0, n).Select(i => Color.FromRgb(b[o + i * 4], b[o + i * 4 + 1], b[o + i * 4 + 2])).ToList();
            o += n * 4;
            if (o != b.Length) throw new InvalidDataException("UpdateMode trailing data");
            return new OpenRgbMode(index, name, value, flags, smin, smax, bmin, bmax, cmin, cmax, speed, bright, dir, cm, colors);
        }
    }

    private static List<FakeDevice> Rig() =>
    [
        new FakeDevice
        {
            Name = "Strip", Type = 4, Leds = 8, Active = 1, Custom = 0,
            Modes = [Mode(0, "Direct", PerLed, OpenRgbMode.ColorModePerLed), Mode(1, "Rainbow Wave", Speed, OpenRgbMode.ColorModeNone, 0, 100, speed: 50),
                     Mode(2, "Breathing", Speed | Bright | ModeColor, OpenRgbMode.ColorModeSpecific, 0, 100, 0, 255, 1, 1, 50, 255, 1)]
        },
        new FakeDevice
        {
            Name = "GPU", Type = 2, Leds = 1, Active = 0, Custom = 0,
            Modes = [Mode(0, "Static", Bright | ModeColor, OpenRgbMode.ColorModeSpecific, bMin: 0, bMax: 100, colorsMin: 1, colorsMax: 1, brightness: 100, colors: 1),
                     Mode(1, "Breathing", Speed | ModeColor, OpenRgbMode.ColorModeSpecific, 10, 1, colorsMin: 1, colorsMax: 1, speed: 5, colors: 1),
                     Mode(2, "Off", 0, OpenRgbMode.ColorModeNone)]
        },
        new FakeDevice
        {
            Name = "Keyboard", Type = 5, Leds = 4, Active = 0, Custom = 0,
            Modes = [Mode(0, "Direct", PerLed, OpenRgbMode.ColorModePerLed)]
        },
    ];

    public static void Run(string outputRoot, string? shots = null)
    {
        using var server = new FakeServer(Rig());
        int port = server.Port;
        var settings = new LightsSettings { Port = port, Color = "#FF8000", Brightness = 50 };

        // Reading: devices, modes, profiles; identities stay the same across connections.
        IReadOnlyList<OpenRgbDevice> devices, again;
        IReadOnlyList<string> profiles;
        using (var client = OpenRgbClient.Connect("127.0.0.1", port)) { devices = client.Devices(); profiles = client.Profiles(); }
        using (var client = OpenRgbClient.Connect("127.0.0.1", port)) again = client.Devices();
        Check(devices.Count == 3 && devices[0] is { Name: "Strip", Type: 4, ActiveMode: 1 } && devices[0].Colors.Count == 8 &&
              devices[1].Modes[0] is { Name: "Static", HasBrightness: true, TakesColor: true, BrightnessMax: 100 } &&
              devices[0].Modes[1] is { HasSpeed: true, TakesColor: false } && profiles.SequenceEqual(["Gaming", "Night"]) &&
              devices.Select(d => d.Identity).SequenceEqual(again.Select(d => d.Identity)) && devices.Select(d => d.Identity).Distinct().Count() == 3,
            "Devices, their modes and OpenRGB profiles are read; a device keeps its identity between connections");
        settings.SkipDevices.Add(devices[2].Identity);
        server.Take();

        LightsSettings Run1(LightsKind kind, string value, out (bool Ok, string Status) result)
        {
            using (var client = OpenRgbClient.Connect("127.0.0.1", port))
                result = Lights.Apply(client, settings, new LightsCommand(kind, value));
            if (result.Ok) Lights.Remember(settings, new LightsCommand(kind, value));
            // Lighting commands get no reply; the fake server takes connections one at a time, so a
            // request on a new connection is answered only after everything above was handled.
            using (var sync = OpenRgbClient.Connect("127.0.0.1", port)) sync.Devices();
            return settings;
        }

        // Solid color: Direct LEDs (dimmed, as Direct has no brightness) and a Static mode color with its own brightness.
        Run1(LightsKind.Color, "", out var color);
        var sent = server.Take();
        var gpuStatic = server.Devices[1].Modes[0];
        Check(color.Ok && color.Status.StartsWith("Color FF8000 on 2 of 2") && server.Devices[0].Active == 0 &&
              server.Devices[0].Colors.Count == 8 && server.Devices[0].Colors.All(c => c == Color.FromRgb(128, 64, 0)) &&
              server.Devices[1].Active == 0 && gpuStatic.Colors.SequenceEqual([Color.FromRgb(255, 128, 0)]) && gpuStatic.Brightness == 50 &&
              gpuStatic.ColorMode == OpenRgbMode.ColorModeSpecific && gpuStatic.Value == 100 && gpuStatic.SpeedMin == 0 &&
              sent.All(p => p.Device != 2 || p.Command == 1),
            "A color goes to Direct LEDs (dimmed) or a mode's own color (with its brightness); unticked devices are only read");

        Run1(LightsKind.Brightness, "", out var bright30);
        settings.Brightness = 30;
        Run1(LightsKind.Brightness, "", out bright30);
        Check(bright30.Ok && server.Devices[1].Modes[0].Brightness == 30 && server.Devices[0].Colors.All(c => c == Color.FromRgb(77, 38, 0)),
            "Brightness sets a mode's own brightness, or re-sends a dimmer color for Direct LEDs");

        settings.Speed = 100;
        Run1(LightsKind.Effect, "breathing", out var breathe);
        var stripBreath = server.Devices[0].Modes[2];
        var gpuBreath = server.Devices[1].Modes[1];
        Check(breathe.Ok && server.Devices[0].Active == 2 && stripBreath.Speed == 100 && stripBreath.Brightness == 77 &&
              stripBreath.Colors.SequenceEqual([Color.FromRgb(255, 128, 0)]) && server.Devices[1].Active == 1 && gpuBreath.Speed == 1 &&
              settings.LastKind == "Effect" && settings.LastValue == "breathing",
            "An effect is set by name on every device that has it, at the chosen speed (ranges that run backwards too) and color");

        Run1(LightsKind.Effect, "Rainbow Wave", out var rainbow);
        Check(rainbow.Ok && rainbow.Status.Contains("on 1 of 2") && rainbow.Status.Contains("Not on GPU") && server.Devices[0].Active == 1 && server.Devices[1].Active == 1,
            "An effect only some devices have is set on those and the rest are named");

        Run1(LightsKind.Off, "", out var off);
        Check(off.Ok && settings.Off && server.Devices[1].Active == 2 && server.Devices[0].Active == 0 && server.Devices[0].Colors.All(c => c == Color.FromRgb(0, 0, 0)),
            "Off uses a device's own Off mode, or black LEDs");
        Run1(LightsKind.On, "", out var on);
        Check(on.Ok && !settings.Off && server.Devices[0].Active == 1 && server.Devices[1].Active == 0 &&
              server.Devices[1].Modes[0].Colors.SequenceEqual([Color.FromRgb(255, 128, 0)]),
            "On brings back the last effect (Rainbow Wave), and the saved color on a device without it rather than leaving it off");

        server.Take();
        Run1(LightsKind.Profile, "Night", out var night);
        var afterLoad = server.Take();
        var loads = afterLoad.Where(p => p.Command == 152).ToList();
        int loadAt = afterLoad.FindIndex(p => p.Command == 152);
        var resent = afterLoad.Skip(loadAt + 1).Where(p => p.Command is 1101 or 1050).Select(p => (p.Device, p.Command)).ToList();
        Run1(LightsKind.Profile, "Nope", out var nope);
        Check(night.Ok && loads.Count == 1 && Encoding.UTF8.GetString(loads[0].Body) == "Night\0" && !nope.Ok && server.Take().All(p => p.Command != 152) &&
              resent.SequenceEqual([(0u, 1101u), (1u, 1101u)]),
            "A saved OpenRGB profile is loaded by name and each controlled device's mode re-sent; an unknown name sends nothing");

        // The schedule's Lights on after a profile was used, when OpenRGB no longer has that profile.
        Run1(LightsKind.Off, "", out _);
        settings.LastKind = "Profile"; settings.LastValue = "Gone";
        server.Take();
        Run1(LightsKind.On, "", out var onWithoutProfile);
        var onSent = server.Take();
        Check(onWithoutProfile.Ok && onWithoutProfile.Status.Contains("no profile 'Gone'") && !settings.Off && onSent.All(p => p.Command != 152) &&
              server.Devices[0].Colors.Any(c => c != Color.FromRgb(0, 0, 0)),
            "Lights on falls back to the saved color when the last profile is gone from OpenRGB, instead of failing every minute");

        // Whitelist: nothing that writes to device memory, saves/deletes profiles or rescans is ever sent.
        Check(!OpenRgbClient.Allowed.Intersect<uint>([1102, 151, 153, 140, 1000, 1001, 1002]).Any() &&
              server.Everything.Count > 40 && server.Everything.All(p => OpenRgbClient.Allowed.Contains(p.Command)),
            "Only listing, profile loading, color and mode commands exist: no saving to devices or profiles, no rescans");

        var effects = Lights.Effects(new LightsState(again, profiles, 3), settings);
        Check(effects.Select(e => e.Name).SequenceEqual(["Breathing", "Rainbow Wave", "Static"]) && effects[0].Devices == 2,
            "Effects list what the controlled devices have, most widely available first, without Direct and Off");

        // Retry when OpenRGB's device list changes mid-request; clear errors; limits.
        server.ChangeListOnce = true;
        var retried = new Lights().RunAsync(settings, new LightsCommand(LightsKind.Color)).GetAwaiter().GetResult();
        var closed = new TcpListener(IPAddress.Loopback, 0); closed.Start(); int dead = ((IPEndPoint)closed.LocalEndpoint).Port; closed.Stop();
        var down = new Lights().ReadAsync(new LightsSettings { Port = dead }).GetAwaiter().GetResult();
        server.Truncate = true;
        var broken = new Lights().ReadAsync(settings).GetAwaiter().GetResult();
        server.Truncate = false;
        bool remote = false;
        try { OpenRgbClient.Connect("192.168.1.10", port).Dispose(); } catch (ArgumentOutOfRangeException) { remote = true; }
        Check(retried.Ok && down.State == null && down.Status.Contains("isn't answering") && broken.State == null && broken.Status.StartsWith("OpenRGB: ") && remote,
            "A changed device list is retried; OpenRGB not running or a bad reply is explained; only this PC's loopback addresses are used");

        // Settings: defaults for older files, bad values repaired, saved and loaded.
        string root = Path.Combine(outputRoot, "lights-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "settings.json");
        File.WriteAllText(file, "{\"ActiveTheme\":\"x\"}");
        var older = SettingsStore.LoadFile(file);
        older.Lights = new LightsSettings { Address = "8.8.8.8", Port = 0, Brightness = 500, Speed = -4, Color = "nope", LastKind = "Hack", SkipDevices = ["A", "A", " "] };
        SettingsStore.SaveFile(file, older);
        var loaded = SettingsStore.LoadFile(file);
        Check(loaded.Lights is { Address: "127.0.0.1", Port: 6742, Brightness: 100, Speed: 0, Color: "#00E5FF", LastKind: "" } &&
              loaded.Lights.SkipDevices.SequenceEqual(["A"]) && loaded.ActiveTheme == "x" && SettingsStore.LoadFile(file).Lights.Port == 6742,
            "Lights settings get defaults in older settings, and bad values are repaired when saved");
        Check(loaded.Lights.AppPath == "" &&
              OpenRgbApp.Executable(@"D:\Tools\OpenRGB\OpenRGB.exe", _ => true) == @"D:\Tools\OpenRGB\OpenRGB.exe" &&
              OpenRgbApp.Executable(@"D:\Gone\OpenRGB.exe", p => p == OpenRgbApp.DefaultPath) == OpenRgbApp.DefaultPath &&
              OpenRgbApp.Executable(@"C:\Windows\System32\cmd.exe", p => !p.Contains("Program Files")) == null &&
              OpenRgbApp.Executable("", _ => false) == null,
            "Open OpenRGB starts the copy last seen running, else Program Files, and never anything but OpenRGB.exe");
        // Window styles read from OpenRGB on this PC: the main window and one of its hidden popup menus.
        Check(OpenRgbApp.IsMainWindowStyle(unchecked((int)0x86CF0000), 0x100) && !OpenRgbApp.IsMainWindowStyle(unchecked((int)0x86000000), 0x88),
            "Open OpenRGB picks OpenRGB's main window (title bar, not a tool window), not one of its hidden popup menus also titled OpenRGB");

        // The Lights page at both screen shapes.
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                foreach (var (w, h) in new[] { (1920, 480), (480, 1802) })
                {
                    var view = new LightsView(new System.Windows.Size(w, h));
                    view.Open(settings);
                    view.ShowState(new LightsState(again, profiles, 3), "Connected · 2 of 3 devices controlled");
                    view.Measure(new System.Windows.Size(w, h));
                    view.Arrange(new Rect(0, 0, w, h));
                    view.UpdateLayout();
                    var tiles = Tiles(view);
                    bool fits = tiles.All(t => t.Bounds.Right <= w + 0.5 && t.Bounds.Bottom <= h + 0.5 && t.Bounds.Height >= 56);
                    if (shots != null)
                    {
                        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(view);
                        var png = new PngBitmapEncoder();
                        png.Frames.Add(BitmapFrame.Create(bitmap));
                        Directory.CreateDirectory(shots);
                        using var output = File.Create(Path.Combine(shots, $"lights-{w}x{h}.png"));
                        png.Save(output);
                    }
                    int opened = 0;
                    view.OpenAppRequested += () => opened++;
                    var openTile = tiles.FirstOrDefault(t => t.Tile.Tag as string == "Open OpenRGB").Tile;
                    openTile?.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
                        { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
                    view.Close();
                    Check(fits && tiles.Count >= 4 + 10 + 2 + 3, $"The Lights page fits a {w}x{h} screen with big tiles for power, brightness, colors, profiles and effects");
                    Check(openTile != null && opened == 1, $"The {w}x{h} Lights page has an Open OpenRGB tile");
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw failure;
    }

    private static List<(System.Windows.Controls.Border Tile, Rect Bounds)> Tiles(FrameworkElement root)
    {
        var found = new List<(System.Windows.Controls.Border, Rect)>();
        void Walk(DependencyObject node)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);
                if (child is System.Windows.Controls.Border { Cursor: not null } b && b.Visibility == Visibility.Visible)
                    found.Add((b, b.TransformToAncestor(root).TransformBounds(new Rect(0, 0, b.ActualWidth, b.ActualHeight))));
                Walk(child);
            }
        }
        Walk(root);
        return found;
    }
}
