using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Color = System.Windows.Media.Color;

namespace Pulsewave;

/// <summary>One of a device's lighting modes as OpenRGB reports it (hardware effects, Direct, Static…).</summary>
internal sealed record OpenRgbMode(int Index, string Name, int Value, uint Flags, uint SpeedMin, uint SpeedMax,
    uint BrightnessMin, uint BrightnessMax, uint ColorsMin, uint ColorsMax, uint Speed, uint Brightness, uint Direction,
    uint ColorMode, IReadOnlyList<Color> Colors)
{
    public const uint FlagSpeed = 1 << 0, FlagBrightness = 1 << 4, FlagPerLedColor = 1 << 5, FlagModeColor = 1 << 6;
    public const uint ColorModeNone = 0, ColorModePerLed = 1, ColorModeSpecific = 2, ColorModeRandom = 3;
    public bool HasSpeed => (Flags & FlagSpeed) != 0;
    public bool HasBrightness => (Flags & FlagBrightness) != 0 && BrightnessMin != BrightnessMax;
    public bool PerLed => ColorMode == ColorModePerLed;
    /// <summary>Takes a color of its own (a breathing or static color, say).</summary>
    public bool TakesColor => (Flags & FlagModeColor) != 0 && ColorsMax > 0;
}

/// <summary>A lighting device (controller) in OpenRGB.</summary>
internal sealed record OpenRgbDevice(int Index, string Identity, string Name, string Vendor, int Type, int ActiveMode,
    IReadOnlyList<OpenRgbMode> Modes, IReadOnlyList<Color> Colors)
{
    public OpenRgbMode? Active => ActiveMode >= 0 && ActiveMode < Modes.Count ? Modes[ActiveMode] : null;

    public string TypeName => Type switch
    {
        0 => "Motherboard", 1 => "Memory", 2 => "Graphics card", 3 => "Cooler", 4 => "LED strip", 5 => "Keyboard", 6 => "Mouse",
        7 => "Mousemat", 8 => "Headset", 9 => "Headset stand", 10 => "Gamepad", 11 => "Light", 12 => "Speaker", 13 => "Virtual",
        14 => "Storage", 15 => "Case", 16 => "Microphone", 17 => "Accessory", 18 => "Keypad", 19 => "Laptop", 20 => "Monitor",
        _ => "Device"
    };
}

/// <summary>
/// A short connection to the OpenRGB SDK server on this PC (loopback only). Pulsewave sends only the
/// commands listed in <see cref="Allowed"/>: it lists devices and profiles, loads a profile, and sets
/// colors, modes and brightness. It never saves to a device's own memory, saves or deletes profiles,
/// or rescans. Every call shares one deadline; replies are size-checked.
/// </summary>
internal sealed class OpenRgbClient : IDisposable
{
    public const int DefaultPort = 6742;
    public static readonly string[] Addresses = ["127.0.0.1", "127.0.0.2"];
    private const int MaximumPacket = 1024 * 1024;
    private const uint RequestCount = 0, RequestController = 1, ProtocolVersion = 40, SetClientName = 50, DeviceListChanged = 100,
        ProfileList = 150, LoadProfileCommand = 152, UpdateLeds = 1050, SetCustomMode = 1100, UpdateMode = 1101;
    /// <summary>The whole command whitelist. SaveMode (1102), profile save/delete and rescan are deliberately absent.</summary>
    internal static readonly uint[] Allowed = [RequestCount, RequestController, ProtocolVersion, SetClientName, ProfileList,
        LoadProfileCommand, UpdateLeds, SetCustomMode, UpdateMode];
    /// <summary>The newest SDK layout Pulsewave reads and writes (modes with brightness; profiles).</summary>
    public const uint MaxProtocol = 3;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly CancellationTokenSource _deadline;

    /// <summary>The SDK protocol both sides use (1 to 3); profiles need 2.</summary>
    public uint Protocol { get; }

    private OpenRgbClient(TcpClient client, CancellationTokenSource deadline)
    {
        _client = client;
        _deadline = deadline;
        _stream = client.GetStream();
        Send(0, ProtocolVersion, Number(MaxProtocol));
        var reply = Receive(0, ProtocolVersion);
        if (reply.Length != 4) throw new InvalidDataException("Invalid OpenRGB protocol response.");
        Protocol = Math.Min(MaxProtocol, BinaryPrimitives.ReadUInt32LittleEndian(reply));
        if (Protocol < 1) throw new InvalidDataException("OpenRGB SDK protocol 1 or newer is required (OpenRGB 0.5 or later).");
        Send(0, SetClientName, Encoding.UTF8.GetBytes("Lumen HUD\0"));
    }

    /// <summary>Connects to OpenRGB's SDK server at a loopback address; throws when it is not running.</summary>
    public static OpenRgbClient Connect(string address, int port, TimeSpan? deadline = null)
    {
        if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        if (!Addresses.Contains(address)) throw new ArgumentOutOfRangeException(nameof(address), "Only this PC's loopback addresses are allowed.");
        var cts = new CancellationTokenSource(deadline ?? TimeSpan.FromSeconds(6));
        var client = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
        try
        {
            client.ConnectAsync(IPAddress.Parse(address), port, cts.Token).AsTask().GetAwaiter().GetResult();
            return new OpenRgbClient(client, cts);
        }
        catch { client.Dispose(); cts.Dispose(); throw; }
    }

    public void Dispose()
    {
        _stream.Dispose();
        _client.Dispose();
        _deadline.Dispose();
    }

    public IReadOnlyList<OpenRgbDevice> Devices()
    {
        Send(0, RequestCount, []);
        var countPacket = Receive(0, RequestCount);
        if (countPacket.Length != 4) throw new InvalidDataException("Invalid OpenRGB controller count.");
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(countPacket);
        if (count > 64) throw new InvalidDataException("OpenRGB controller count exceeds the limit.");
        var devices = new List<OpenRgbDevice>();
        for (int i = 0; i < count; i++) devices.Add(Device(i));
        return devices;
    }

    public OpenRgbDevice Device(int index)
    {
        Send((uint)index, RequestController, Number(Protocol));
        return Parse(index, Receive((uint)index, RequestController), Protocol);
    }

    /// <summary>The profiles saved in OpenRGB; empty before SDK protocol 2.</summary>
    public IReadOnlyList<string> Profiles()
    {
        if (Protocol < 2) return [];
        Send(0, ProfileList, []);
        var r = new Cursor(Receive(0, ProfileList));
        r.U32(); // data size
        int count = r.Count(256);
        var names = new List<string>();
        for (int i = 0; i < count; i++) names.Add(r.Text());
        return names;
    }

    public void LoadProfile(string name)
    {
        if (Protocol < 2) throw new InvalidOperationException("This OpenRGB is too old for profiles.");
        Send(0, LoadProfileCommand, Encoding.UTF8.GetBytes(name + "\0"));
    }

    /// <summary>Asks OpenRGB to switch the device to the mode it uses for software colors (Direct, Custom or Static).</summary>
    public void UseCustomMode(int device) => Send((uint)device, SetCustomMode, []);

    /// <summary>Sets every LED of the device (the active mode must take per-LED colors).</summary>
    public void SetLeds(int device, IReadOnlyList<Color> colors)
    {
        var body = new byte[4 + 2 + colors.Count * 4];
        BinaryPrimitives.WriteUInt32LittleEndian(body, (uint)body.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(4), checked((ushort)colors.Count));
        for (int i = 0; i < colors.Count; i++) WriteColor(body.AsSpan(6 + i * 4), colors[i]);
        Send((uint)device, UpdateLeds, body);
    }

    /// <summary>Makes <paramref name="mode"/> (with its speed, brightness and colors) the device's active mode.</summary>
    public void SetMode(int device, OpenRgbMode mode) => Send((uint)device, UpdateMode, ModeBody(mode, Protocol));

    internal static byte[] ModeBody(OpenRgbMode mode, uint protocol)
    {
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            w.Write(0u); // data size, filled in below
            w.Write(mode.Index);
            var name = Encoding.UTF8.GetBytes(mode.Name + "\0");
            w.Write(checked((ushort)name.Length)); w.Write(name);
            w.Write(mode.Value); w.Write(mode.Flags); w.Write(mode.SpeedMin); w.Write(mode.SpeedMax);
            if (protocol >= 3) { w.Write(mode.BrightnessMin); w.Write(mode.BrightnessMax); }
            w.Write(mode.ColorsMin); w.Write(mode.ColorsMax); w.Write(mode.Speed);
            if (protocol >= 3) w.Write(mode.Brightness);
            w.Write(mode.Direction); w.Write(mode.ColorMode);
            w.Write(checked((ushort)mode.Colors.Count));
            var color = new byte[4];
            foreach (var c in mode.Colors) { WriteColor(color, c); w.Write(color); }
        }
        var body = stream.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(body, (uint)body.Length);
        return body;
    }

    private static void WriteColor(Span<byte> target, Color c) { target[0] = c.R; target[1] = c.G; target[2] = c.B; target[3] = 0; }

    private static byte[] Number(uint value) { var bytes = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, value); return bytes; }

    private void Send(uint device, uint command, byte[] body)
    {
        if (!Allowed.Contains(command)) throw new InvalidOperationException("OpenRGB command not allowed.");
        var packet = new byte[16 + body.Length];
        "ORGB"u8.CopyTo(packet);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), device);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), command);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), (uint)body.Length);
        body.CopyTo(packet, 16);
        _stream.WriteAsync(packet, _deadline.Token).AsTask().GetAwaiter().GetResult();
    }

    private byte[] Receive(uint device, uint command)
    {
        var header = new byte[16];
        _stream.ReadExactlyAsync(header, _deadline.Token).AsTask().GetAwaiter().GetResult();
        if (!header.AsSpan(0, 4).SequenceEqual("ORGB"u8)) throw new InvalidDataException("Invalid OpenRGB packet signature.");
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8));
        uint size = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12));
        if (size > MaximumPacket) throw new InvalidDataException("OpenRGB packet exceeds the size limit.");
        var body = new byte[(int)size];
        _stream.ReadExactlyAsync(body, _deadline.Token).AsTask().GetAwaiter().GetResult();
        if (id == DeviceListChanged) throw new DeviceListChangedException();
        if (id != command || BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4)) != device)
            throw new InvalidDataException("Unexpected OpenRGB response.");
        return body;
    }

    internal static OpenRgbDevice Parse(int index, byte[] data, uint protocol)
    {
        var r = new Cursor(data);
        if (r.U32() != data.Length) throw new InvalidDataException("Invalid OpenRGB controller block size.");
        int type = unchecked((int)r.U32());
        string name = r.Text(), vendor = r.Text();
        r.Text(); r.Text(); // description and firmware version
        string serial = r.Text(), location = r.Text();
        int modeCount = r.Count(256);
        int active = unchecked((int)r.U32());
        var modes = new List<OpenRgbMode>();
        for (int i = 0; i < modeCount; i++)
        {
            string modeName = r.Text();
            int value = unchecked((int)r.U32());
            uint flags = r.U32(), speedMin = r.U32(), speedMax = r.U32();
            uint brightnessMin = protocol >= 3 ? r.U32() : 0, brightnessMax = protocol >= 3 ? r.U32() : 0;
            uint colorsMin = r.U32(), colorsMax = r.U32(), speed = r.U32();
            uint brightness = protocol >= 3 ? r.U32() : 0;
            uint direction = r.U32(), colorMode = r.U32();
            var colors = r.Colors(4096);
            modes.Add(new OpenRgbMode(i, modeName, value, flags, speedMin, speedMax, brightnessMin, brightnessMax,
                colorsMin, colorsMax, speed, brightness, direction, colorMode, colors));
        }
        int zones = r.Count(256);
        for (int i = 0; i < zones; i++)
        {
            r.Text(); r.Skip(16); // type, LED minimum, maximum and count
            int matrixLength = r.U16();
            if (matrixLength > 0)
            {
                if (matrixLength < 8) throw new InvalidDataException("Invalid OpenRGB matrix length.");
                uint height = r.U32(), width = r.U32();
                if (height > 4096 || width > 4096 || (ulong)height * width * 4 + 8 != (ulong)matrixLength)
                    throw new InvalidDataException("Invalid OpenRGB matrix dimensions.");
                r.Skip(matrixLength - 8);
            }
        }
        int leds = r.Count(4096);
        for (int i = 0; i < leds; i++) { r.Text(); r.Skip(4); }
        var ledColors = r.Colors(4096);
        r.End();
        // Length-prefixed fields prevent delimiter collisions; the list position and colors are left out.
        using var identity = new MemoryStream();
        using (var writer = new BinaryWriter(identity, Encoding.UTF8, true))
        { writer.Write(type); writer.Write(name); writer.Write(vendor); writer.Write(serial); writer.Write(location); }
        return new OpenRgbDevice(index, Convert.ToHexString(SHA256.HashData(identity.ToArray())), name, vendor, type, active, modes, ledColors);
    }

    private sealed class Cursor(byte[] data)
    {
        private int offset;
        public void Skip(int length) { if (length < 0 || length > data.Length - offset) throw new InvalidDataException("Truncated OpenRGB data."); offset += length; }
        public ushort U16() { int start = offset; Skip(2); return BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(start, 2)); }
        public uint U32() { int start = offset; Skip(4); return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(start, 4)); }
        public int Count(int max) { int count = U16(); if (count > max) throw new InvalidDataException("OpenRGB collection exceeds the limit."); return count; }
        public Color[] Colors(int max)
        {
            var colors = new Color[Count(max)];
            for (int i = 0; i < colors.Length; i++) { uint rgb = U32(); colors[i] = Color.FromRgb((byte)rgb, (byte)(rgb >> 8), (byte)(rgb >> 16)); }
            return colors;
        }
        public string Text()
        {
            int length = U16();
            if (length < 1 || length > 4096) throw new InvalidDataException("Invalid OpenRGB string length.");
            int start = offset; Skip(length);
            if (data[offset - 1] != 0 || data.AsSpan(start, length - 1).Contains((byte)0)) throw new InvalidDataException("Invalid OpenRGB string termination.");
            return new UTF8Encoding(false, true).GetString(data, start, length - 1);
        }
        public void End() { if (offset != data.Length) throw new InvalidDataException("Unexpected trailing OpenRGB data."); }
    }
}

/// <summary>OpenRGB's device list changed mid-request (a device was added or rescanned); try again.</summary>
internal sealed class DeviceListChangedException() : IOException("OpenRGB's device list changed; try again.");
