using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pulsewave.Themes;

namespace Pulsewave;

/// <summary>
/// A program, game or link the HUD can open: an .exe, a .lnk or Steam/Epic .url shortcut, a folder,
/// a web address or a shell: path. Stored in the user's settings, never in themes.
/// </summary>
public sealed class AppShortcut
{
    /// <summary>
    /// Up to 48 shortcuts (24 until 0.9.1). The app drawer shows 24 to a page so its tiles keep their
    /// size at 3–4 ft; theme tiles and the tray's "Open a shortcut" address slots 1–48.
    /// </summary>
    public const int MaxCount = 48;
    public const string KeysKind = "Keys", SystemKind = "System";
    public string Name { get; set; } = "";
    /// <summary>"" opens Target; "Keys" sends the key combination in Target (e.g. "Ctrl+Shift+M") to the app in front; "System" runs "Lock" or "Sleep".</summary>
    public string Kind { get; set; } = "";
    public string Target { get; set; } = "";
    public string Arguments { get; set; } = "";
    /// <summary>An .ico/.png/.exe to take the icon from instead of the target's own.</summary>
    public string? IconFile { get; set; }

    /// <summary>A slot kept free so later slots keep their numbers (a tile with nothing assigned yet).</summary>
    public bool IsEmpty => Target.Trim().Length == 0;

    public string DisplayName => IsEmpty ? "" : Name.Trim().Length > 0 ? Name.Trim()
        : Kind is KeysKind ? Target.Trim() : Kind is SystemKind ? Target.Trim() + " PC"
        : Target.Contains("://", StringComparison.Ordinal) ? Target : Path.GetFileNameWithoutExtension(Target);

    public AppShortcut Copy() => new() { Name = Name, Kind = Kind, Target = Target, Arguments = Arguments, IconFile = IconFile };

    /// <summary>A shortcut for a dropped or picked file (.exe, .lnk, .url, folder) or a dropped web/steam:// address.</summary>
    public static AppShortcut? FromDrop(string item)
    {
        item = item.Trim().Trim('"');
        if (item.Length == 0) return null;
        if (Uri.TryCreate(item, UriKind.Absolute, out var uri) && !uri.IsFile && uri.Scheme.Length > 1)
            return new AppShortcut { Name = uri.Scheme is "http" or "https" ? uri.Host : uri.Scheme, Target = item };
        if (!File.Exists(item) && !Directory.Exists(item)) return null;
        string name = Directory.Exists(item) ? new DirectoryInfo(item).Name : Path.GetFileNameWithoutExtension(item);
        return new AppShortcut { Name = name, Target = item };
    }

    /// <summary>
    /// Puts a shortcut in a 1-based slot (empty slots fill the gap) or, with slot 0, the first empty
    /// slot or the end. Returns the slot used, or 0 when the list is full.
    /// </summary>
    public static int Assign(List<AppShortcut> list, AppShortcut shortcut, int slot = 0)
    {
        if (slot < 1) slot = list.FindIndex(s => s.IsEmpty) + 1 is > 0 and var free ? free : list.Count + 1;
        if (slot > MaxCount) return 0;
        while (list.Count < slot) list.Add(new AppShortcut());
        list[slot - 1] = shortcut;
        return slot;
    }

    /// <summary>Drops empty slots at the end; empty slots between shortcuts keep later slot numbers.</summary>
    public static void TrimEnd(List<AppShortcut> list)
    {
        while (list.Count > 0 && list[^1].IsEmpty) list.RemoveAt(list.Count - 1);
    }
}

/// <summary>
/// The Gaming and Productivity shortcut lists: an ordered pick of the user's shortcuts (by 1-based slot) for themes
/// that follow that list, such as Halo Gaming and Halo Productivity. Their tiles show the picks in order (tile 1 is the
/// first pick); their app drawer shows the picks first, then every other shortcut. A list with no picks behaves like
/// the main list, so nothing changes until the user picks.
/// </summary>
public static class ShortcutLists
{
    public const string Gaming = "Gaming", Productivity = "Productivity";
    public static readonly string[] Names = [Gaming, Productivity];

    /// <summary>The list's real name ("Gaming") for a theme's ShortcutList, or null for the main list.</summary>
    public static string? Canonical(string? name) =>
        Names.FirstOrDefault(n => n.Equals(name?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The list's picks that still point at a shortcut, in order, without repeats.</summary>
    public static List<int> Valid(IReadOnlyList<AppShortcut> all, IEnumerable<int>? picks) =>
        (picks ?? []).Where(slot => slot >= 1 && slot <= all.Count && !all[slot - 1].IsEmpty).Distinct().ToList();

    /// <summary>The picks of <paramref name="list"/> in <paramref name="lists"/>, or none for the main list.</summary>
    public static List<int> PicksOf(IReadOnlyDictionary<string, List<int>>? lists, IReadOnlyList<AppShortcut> all, string? list) =>
        Canonical(list) is { } name && lists != null && lists.TryGetValue(name, out var picks) ? Valid(all, picks) : [];

    /// <summary>The slot each tile opens (tile 1 first): the picks, or with none every slot in order.</summary>
    public static List<int> TileSlots(IReadOnlyList<AppShortcut> all, IReadOnlyList<int> picks) =>
        picks.Count > 0 ? picks.ToList() : Enumerable.Range(1, all.Count).ToList();

    /// <summary>The app drawer's order: the picks, then every other shortcut by slot.</summary>
    public static List<int> DrawerSlots(IReadOnlyList<AppShortcut> all, IReadOnlyList<int> picks) =>
        picks.Concat(Enumerable.Range(1, all.Count).Where(slot => !all[slot - 1].IsEmpty && !picks.Contains(slot))).ToList();

    /// <summary>Keeps known lists only, with picks in range and without repeats (older settings have none).</summary>
    public static Dictionary<string, List<int>> Normalize(IDictionary<string, List<int>>? lists)
    {
        var clean = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, picks) in lists ?? new Dictionary<string, List<int>>())
            if (Canonical(key) is { } name && picks != null)
                clean[name] = picks.Where(slot => slot is >= 1 and <= AppShortcut.MaxCount).Distinct().ToList();
        return clean;
    }

    // Keeping picks on the same shortcuts while the main list changes (the Shortcuts window's buttons).

    /// <summary>Slots <paramref name="a"/> and <paramref name="b"/> swapped places.</summary>
    public static void Swapped(IDictionary<string, List<int>> lists, int a, int b)
    {
        foreach (var picks in lists.Values)
            for (int i = 0; i < picks.Count; i++)
                picks[i] = picks[i] == a ? b : picks[i] == b ? a : picks[i];
    }

    /// <summary>Slot <paramref name="slot"/> was removed: later shortcuts moved up one.</summary>
    public static void Removed(IDictionary<string, List<int>> lists, int slot)
    {
        foreach (var picks in lists.Values)
        {
            picks.Remove(slot);
            for (int i = 0; i < picks.Count; i++) if (picks[i] > slot) picks[i]--;
        }
    }

    /// <summary>Slot <paramref name="slot"/> now holds a different shortcut (or none): no list keeps it.</summary>
    public static void Replaced(IDictionary<string, List<int>> lists, int slot)
    {
        foreach (var picks in lists.Values) picks.Remove(slot);
    }
}

/// <summary>Icons for shortcuts, from the documented shell image factory (the same icons Explorer shows).</summary>
public static class ShortcutIcons
{
    /// <summary>The shortcut's icon at about <paramref name="size"/> pixels, or null when the shell has none (web addresses).</summary>
    public static BitmapSource? Load(AppShortcut shortcut, int size = 256)
    {
        string path = !string.IsNullOrWhiteSpace(shortcut.IconFile) ? shortcut.IconFile! : shortcut.Target;
        if (string.IsNullOrWhiteSpace(path)) return null;
        path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if (path.Contains("://", StringComparison.Ordinal)) return null;
        if (Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" && File.Exists(path))
        {
            try
            {
                var picture = new BitmapImage();
                picture.BeginInit();
                picture.CacheOption = BitmapCacheOption.OnLoad;
                picture.DecodePixelWidth = size;
                picture.UriSource = new Uri(path);
                picture.EndInit();
                picture.Freeze();
                return picture;
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException or ArgumentException) { return null; }
        }
        IntPtr bitmap = IntPtr.Zero;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory) != 0 || factory == null) return null;
            int hr = factory.GetImage(new NativeSize { Width = size, Height = size }, IconOnly | BiggerSizeOk, out bitmap);
            Marshal.ReleaseComObject(factory);
            return hr == 0 && bitmap != IntPtr.Zero && ToBitmapSource(bitmap) is { } shell ? Trim(shell) : null;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidCastException) { return null; }
        finally
        {
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
        }
    }

    /// <summary>A Steam game's portrait cover art (2:3) from Steam's local cache, for tall tiles; null for anything else.</summary>
    public static BitmapSource? LoadCover(AppShortcut shortcut, int width = 360)
    {
        string target = shortcut.Target.Trim().Trim('"');
        // A desktop .url shortcut Steam made holds its steam:// link inside.
        if (target.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
            try
            {
                target = File.Exists(target) ? File.ReadLines(target).Take(40).FirstOrDefault(l => l.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))?[4..].Trim() ?? "" : "";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { target = ""; }
        if (SteamLibrary.AppIdOf(target) is not { } id || SteamLibrary.CoverArt(id) is not { } file) return null;
        try
        {
            var picture = new BitmapImage();
            picture.BeginInit();
            picture.CacheOption = BitmapCacheOption.OnLoad;
            picture.DecodePixelWidth = width;
            picture.UriSource = new Uri(file);
            picture.EndInit();
            picture.Freeze();
            return picture;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException or ArgumentException) { return null; }
    }

    /// <summary>
    /// Crops an icon's transparent margin (keeping a little air, and the result square) so every icon
    /// fills its tile the same way, however much padding its program file gave it.
    /// </summary>
    internal static BitmapSource Trim(BitmapSource icon)
    {
        var argb = icon.Format == PixelFormats.Pbgra32 || icon.Format == PixelFormats.Bgra32 ? icon : new FormatConvertedBitmap(icon, PixelFormats.Pbgra32, null, 0);
        int w = argb.PixelWidth, h = argb.PixelHeight, stride = w * 4;
        if (w < 8 || h < 8) return icon;
        var pixels = new byte[stride * h];
        argb.CopyPixels(pixels, stride, 0);
        int left = w, top = h, right = -1, bottom = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (pixels[y * stride + x * 4 + 3] > 24) { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
        if (right < 0) return icon;
        int side = Math.Max(right - left + 1, bottom - top + 1);
        side = Math.Min(Math.Min(w, h), (int)Math.Ceiling(side * 1.04));
        if (side >= Math.Min(w, h) * 0.92) return icon; // already fills it
        int cx = (left + right) / 2, cy = (top + bottom) / 2;
        int x0 = Math.Clamp(cx - side / 2, 0, w - side), y0 = Math.Clamp(cy - side / 2, 0, h - side);
        var cropped = new CroppedBitmap(argb, new System.Windows.Int32Rect(x0, y0, side, side));
        cropped.Freeze();
        return cropped;
    }

    /// <summary>Whether an icon fills its square (opaque corners: a picture or tile icon) rather than having its own shape.</summary>
    internal static bool IsSolid(BitmapSource icon)
    {
        var argb = icon.Format == PixelFormats.Pbgra32 || icon.Format == PixelFormats.Bgra32 ? icon : new FormatConvertedBitmap(icon, PixelFormats.Pbgra32, null, 0);
        int w = argb.PixelWidth, h = argb.PixelHeight;
        if (w < 4 || h < 4) return false;
        var pixel = new byte[4];
        int inset = Math.Max(1, Math.Min(w, h) / 10); // just inside the corner, past any rounding the icon already has
        foreach (var (x, y) in new[] { (inset, h / 2), (w - 1 - inset, h / 2), (w / 2, inset), (w / 2, h - 1 - inset) })
        {
            argb.CopyPixels(new System.Windows.Int32Rect(x, y, 1, 1), pixel, 4, 0);
            if (pixel[3] < 200) return false;
        }
        return true;
    }

    /// <summary>
    /// The icon's main color, for a tile backdrop: the most common hue among its colorful, opaque pixels
    /// (averaged within that hue). Null for grey or mostly transparent icons, which keep the theme's tile.
    /// </summary>
    internal static System.Windows.Media.Color? Accent(BitmapSource icon)
    {
        var argb = icon.Format == PixelFormats.Pbgra32 || icon.Format == PixelFormats.Bgra32 ? icon : new FormatConvertedBitmap(icon, PixelFormats.Pbgra32, null, 0);
        int w = argb.PixelWidth, h = argb.PixelHeight;
        if (w < 2 || h < 2) return null;
        var pixels = new byte[w * h * 4];
        argb.CopyPixels(pixels, w * 4, 0);
        var weight = new double[12]; var r = new double[12]; var g = new double[12]; var b = new double[12];
        double total = 0;
        int step = Math.Max(1, Math.Min(w, h) / 48);
        for (int y = 0; y < h; y += step)
            for (int x = 0; x < w; x += step)
            {
                int i = (y * w + x) * 4;
                double a = pixels[i + 3] / 255.0;
                if (a < 0.5) continue;
                // Premultiplied: undo it so translucent edges do not darken the color.
                double pb = pixels[i] / 255.0 / a, pg = pixels[i + 1] / 255.0 / a, pr = pixels[i + 2] / 255.0 / a;
                double max = Math.Max(pr, Math.Max(pg, pb)), min = Math.Min(pr, Math.Min(pg, pb)), chroma = max - min;
                total += a;
                if (chroma < 0.18 || max < 0.2) continue;
                double hue = max == pr ? (pg - pb) / chroma % 6 : max == pg ? (pb - pr) / chroma + 2 : (pr - pg) / chroma + 4;
                int bin = ((int)Math.Floor((hue < 0 ? hue + 6 : hue) * 2) % 12 + 12) % 12;
                double k = a * chroma;
                weight[bin] += k; r[bin] += pr * k; g[bin] += pg * k; b[bin] += pb * k;
            }
        int best = Array.IndexOf(weight, weight.Max());
        if (total == 0 || weight[best] < total * 0.06) return null;
        byte C(double v) => (byte)Math.Clamp(Math.Round(v / weight[best] * 255), 0, 255);
        return System.Windows.Media.Color.FromRgb(C(r[best]), C(g[best]), C(b[best]));
    }

    // The shell returns a 32-bit premultiplied DIB section; copy its rows (top-down or bottom-up) with alpha intact.
    private static BitmapSource? ToBitmapSource(IntPtr handle)
    {
        var dib = new DibSection();
        if (GetObject(handle, Marshal.SizeOf<DibSection>(), ref dib) == 0 || dib.Bitmap.BitsPixel != 32 || dib.Bitmap.Bits == IntPtr.Zero) return null;
        int width = dib.Bitmap.Width, height = Math.Abs(dib.Bitmap.Height), stride = dib.Bitmap.WidthBytes;
        bool topDown = dib.Header.Height < 0;
        var pixels = new byte[stride * height];
        for (int row = 0; row < height; row++)
            Marshal.Copy(dib.Bitmap.Bits + (topDown ? row : height - 1 - row) * stride, pixels, row * stride, stride);
        bool hasAlpha = false;
        for (int i = 3; i < pixels.Length && !hasAlpha; i += 4) hasAlpha = pixels[i] != 0;
        var source = BitmapSource.Create(width, height, 96, 96, hasAlpha ? PixelFormats.Pbgra32 : PixelFormats.Bgr32, null, pixels, stride);
        source.Freeze();
        return source;
    }

    private const int BiggerSizeOk = 0x1, IconOnly = 0x4;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize { public int Width, Height; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeBitmap { public int Type, Width, Height, WidthBytes; public ushort Planes, BitsPixel; public IntPtr Bits; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size; public int Width, Height; public ushort Planes, BitCount;
        public uint Compression, SizeImage; public int XPelsPerMeter, YPelsPerMeter; public uint ClrUsed, ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DibSection
    {
        public NativeBitmap Bitmap; public BitmapInfoHeader Header;
        public uint BitField0, BitField1, BitField2; public IntPtr Section; public uint Offset;
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(NativeSize size, int flags, out IntPtr bitmap);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr handle, int size, ref DibSection dib);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);
}

/// <summary>Opens shortcuts through the shell, as a double-click in Explorer would.</summary>
public static class ShortcutLauncher
{
    /// <summary>Starts the shortcut (or sends its keys, or locks or sleeps the PC); returns why it could not, or null.</summary>
    public static string? Launch(AppShortcut shortcut)
    {
        if (shortcut.Kind == AppShortcut.KeysKind)
            return KeySender.Send(shortcut.Target) ? null : $"'{shortcut.Target}' is not a key combination Pulsewave can send.";
        if (shortcut.Kind == AppShortcut.SystemKind)
        {
            bool done = shortcut.Target.Trim().ToLowerInvariant() switch
            {
                "lock" => LockWorkStation(),
                "sleep" => SetSuspendState(false, false, false),
                _ => false
            };
            return done ? null : $"'{shortcut.Target}' did not run.";
        }
        string target = Environment.ExpandEnvironmentVariables(shortcut.Target.Trim().Trim('"'));
        if (target.Length == 0) return "This shortcut has no target.";
        bool isFile = !target.Contains("://", StringComparison.Ordinal) && !target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase);
        if (isFile && !File.Exists(target) && !Directory.Exists(target)) return $"'{target}' was not found.";
        try
        {
            // The HUD never takes focus; let the program it opens come to the front.
            AllowSetForegroundWindow(AsfwAny);
            var start = new ProcessStartInfo(target) { UseShellExecute = true, Arguments = shortcut.Arguments ?? "" };
            if (isFile && File.Exists(target) && Path.GetDirectoryName(target) is { Length: > 0 } folder) start.WorkingDirectory = folder;
            Process.Start(start)?.Dispose();
            return null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return ex.Message;
        }
    }

    private const int AsfwAny = -1;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LockWorkStation();

    [DllImport("powrprof.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool SetSuspendState([MarshalAs(UnmanagedType.U1)] bool hibernate, [MarshalAs(UnmanagedType.U1)] bool force,
        [MarshalAs(UnmanagedType.U1)] bool disableWakeEvent);
}

/// <summary>
/// Sends a key combination to the app in front, like a macro key (Discord mute, OBS scene switch):
/// the HUD never takes focus, so the keys reach the game or app being used. Uses SendInput.
/// </summary>
public static class KeySender
{
    /// <summary>Parses a combination such as "Ctrl+Shift+M", "F9" or "MediaPlayPause" (any key, modifiers optional).</summary>
    public static bool TryParse(string? text, out Hotkey keys) => Hotkey.TryParse(text, out keys, requireModifier: false);

    public static bool Send(string? text)
    {
        if (!TryParse(text, out var keys)) return false;
        var modifiers = new List<ushort>();
        if (keys.Modifiers.HasFlag(HotkeyModifiers.Ctrl)) modifiers.Add(0x11);
        if (keys.Modifiers.HasFlag(HotkeyModifiers.Alt)) modifiers.Add(0x12);
        if (keys.Modifiers.HasFlag(HotkeyModifiers.Shift)) modifiers.Add(0x10);
        if (keys.Modifiers.HasFlag(HotkeyModifiers.Win)) modifiers.Add(0x5B);
        var inputs = new List<Input>();
        foreach (var m in modifiers) inputs.Add(Key(m, false));
        inputs.Add(Key((ushort)keys.Key, false));
        inputs.Add(Key((ushort)keys.Key, true));
        for (int i = modifiers.Count - 1; i >= 0; i--) inputs.Add(Key(modifiers[i], true));
        return SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Input>()) == inputs.Count;
    }

    // Navigation keys are "extended" keys; without the flag some apps read them as the number pad.
    private static Input Key(ushort vk, bool up) => new()
    {
        Type = 1,
        Keyboard = new KeyboardInput
        {
            Vk = vk,
            Flags = (up ? 0x2u : 0) | (vk is >= 0x21 and <= 0x2E or 0x5B or 0x5C or >= 0xA6 and <= 0xB7 ? 0x1u : 0)
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput { public ushort Vk, Scan; public uint Flags, Time; public IntPtr ExtraInfo; }

    // INPUT is a union of mouse, keyboard and hardware input; the mouse member is the largest, so pad to it.
    [StructLayout(LayoutKind.Explicit)]
    private struct Input
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public KeyboardInput Keyboard;
        [FieldOffset(8)] private MouseInputPadding _padding;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInputPadding { public int X, Y; public uint Data, Flags, Time; public IntPtr ExtraInfo; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
}
