using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Pulsewave.Themes;

public sealed record VideoInfo(int Width, int Height, double FramesPerSecond, int Frames)
{
    public double Seconds => FramesPerSecond > 0 ? Frames / FramesPerSecond : 0;
}

/// <summary>
/// Wraps a raw H.264 Annex B stream (L-Connect's .h264 backgrounds) in an MP4 container so
/// Windows' built-in decoder can play it. No re-encoding: frames are copied as they are.
/// </summary>
public static class H264Mp4Writer
{
    public static VideoInfo Convert(ReadOnlySpan<byte> annexB, Stream output, double fallbackFps = 30)
    {
        var nals = SplitNalUnits(annexB);
        var sps = new List<byte[]>();
        var pps = new List<byte[]>();
        var frames = new List<(List<byte[]> Nals, bool Key)>();
        List<byte[]>? current = null;
        bool currentHasSlice = false, currentKey = false;

        void Flush()
        {
            if (current is { Count: > 0 } && currentHasSlice) frames.Add((current, currentKey));
            current = null; currentHasSlice = false; currentKey = false;
        }

        foreach (var nal in nals)
        {
            int type = nal[0] & 0x1F;
            bool slice = type is 1 or 5;
            // A new frame starts at an access unit delimiter, at parameter sets/SEI after a
            // slice, or at a slice whose first macroblock is 0.
            if (type == 9 || (type is 6 or 7 or 8 && currentHasSlice) || (slice && currentHasSlice && FirstMbIsZero(nal)))
                Flush();
            if (type == 7) { if (!sps.Any(s => s.AsSpan().SequenceEqual(nal))) sps.Add(nal); continue; }
            if (type == 8) { if (!pps.Any(p => p.AsSpan().SequenceEqual(nal))) pps.Add(nal); continue; }
            if (type == 9) continue;
            current ??= new List<byte[]>();
            current.Add(nal);
            if (slice) { currentHasSlice = true; currentKey |= type == 5; }
        }
        Flush();

        if (sps.Count == 0 || pps.Count == 0) throw new InvalidDataException("Video has no H.264 parameter sets.");
        int firstKey = frames.FindIndex(f => f.Key);
        if (firstKey < 0) throw new InvalidDataException("Video has no key frame.");
        frames.RemoveRange(0, firstKey); // players need a key frame first

        var info = ParseSps(sps[0]);
        double fps = info.Fps is > 1 and < 241 ? info.Fps : fallbackFps is > 1 and < 241 ? fallbackFps : 30;
        // Exact rational timing when the stream carries it, else 90 kHz.
        uint timescale = info.Fps is > 1 and < 241 ? info.TimeScale : 90000;
        uint delta = info.Fps is > 1 and < 241 ? info.FrameTicks : (uint)Math.Round(90000 / fps);

        WriteMp4(output, frames, sps, pps, info, timescale, delta);
        return new VideoInfo(info.Width, info.Height, fps, frames.Count);
    }

    /// <summary>Display size of the first visual track in an MP4 (its tkhd box), if found.</summary>
    public static (int Width, int Height)? ReadMp4Size(ReadOnlySpan<byte> mp4)
    {
        ReadOnlySpan<byte> tag = "tkhd"u8;
        for (int at = mp4.IndexOf(tag); at >= 4; )
        {
            uint size = BinaryPrimitives.ReadUInt32BigEndian(mp4[(at - 4)..]);
            int end = at - 4 + (int)size;
            if (size >= 92 && end <= mp4.Length)
            {
                int w = (int)(BinaryPrimitives.ReadUInt32BigEndian(mp4[(end - 8)..]) >> 16);
                int h = (int)(BinaryPrimitives.ReadUInt32BigEndian(mp4[(end - 4)..]) >> 16);
                if (w > 0 && h > 0) return (w, h);
            }
            int next = mp4[(at + 4)..].IndexOf(tag);
            at = next < 0 ? -1 : at + 4 + next;
        }
        return null;
    }

    internal static List<byte[]> SplitNalUnits(ReadOnlySpan<byte> data)
    {
        var result = new List<byte[]>();
        int start = -1;
        for (int i = 0; i + 2 < data.Length; i++)
        {
            if (data[i] != 0 || data[i + 1] != 0 || data[i + 2] != 1) continue;
            if (start >= 0) AddNal(data[start..i], result);
            start = i + 3;
            i += 2;
        }
        if (start >= 0 && start < data.Length) AddNal(data[start..], result);
        return result;
    }

    private static void AddNal(ReadOnlySpan<byte> nal, List<byte[]> result)
    {
        int end = nal.Length;
        while (end > 0 && nal[end - 1] == 0) end--; // zero bytes belong to the next start code
        if (end > 0) result.Add(nal[..end].ToArray());
    }

    private static bool FirstMbIsZero(byte[] nal) => nal.Length > 1 && (nal[1] & 0x80) != 0; // ue(v) == 0 is a single 1 bit

    internal sealed record SpsInfo(int Profile, int Compatibility, int Level, int Width, int Height, uint TimeScale, uint FrameTicks)
    {
        public double Fps => FrameTicks > 0 ? (double)TimeScale / FrameTicks : 0;
    }

    internal static SpsInfo ParseSps(byte[] nal)
    {
        var bits = new BitReader(RemoveEmulationPrevention(nal.AsSpan(1)));
        int profile = bits.Bits(8), compat = bits.Bits(8), level = bits.Bits(8);
        bits.Ue(); // sps id
        int chroma = 1;
        if (profile is 100 or 110 or 122 or 244 or 44 or 83 or 86 or 118 or 128 or 138 or 139 or 134 or 135)
        {
            chroma = bits.Ue();
            if (chroma == 3) bits.Bits(1);
            bits.Ue(); bits.Ue(); bits.Bits(1);
            if (bits.Bits(1) == 1)
                for (int i = 0; i < (chroma == 3 ? 12 : 8); i++)
                    if (bits.Bits(1) == 1)
                    {
                        int last = 8, next = 8;
                        for (int j = 0; j < (i < 6 ? 16 : 64); j++)
                        {
                            if (next != 0) next = (last + bits.Se() + 256) % 256;
                            last = next == 0 ? last : next;
                        }
                    }
        }
        bits.Ue(); // log2_max_frame_num
        int pocType = bits.Ue();
        if (pocType == 0) bits.Ue();
        else if (pocType == 1)
        {
            bits.Bits(1); bits.Se(); bits.Se();
            int cycle = bits.Ue();
            for (int i = 0; i < cycle; i++) bits.Se();
        }
        bits.Ue(); bits.Bits(1);
        int widthMbs = bits.Ue() + 1, heightUnits = bits.Ue() + 1;
        int frameMbsOnly = bits.Bits(1);
        if (frameMbsOnly == 0) bits.Bits(1);
        bits.Bits(1);
        int cropL = 0, cropR = 0, cropT = 0, cropB = 0;
        if (bits.Bits(1) == 1) { cropL = bits.Ue(); cropR = bits.Ue(); cropT = bits.Ue(); cropB = bits.Ue(); }
        int cropX = chroma == 0 || chroma == 3 ? 1 : 2;
        int cropY = (chroma == 1 ? 2 : 1) * (2 - frameMbsOnly);
        int width = widthMbs * 16 - cropX * (cropL + cropR);
        int height = (2 - frameMbsOnly) * heightUnits * 16 - cropY * (cropT + cropB);

        uint timeScale = 0, ticks = 0;
        if (bits.Remaining > 0 && bits.Bits(1) == 1) // VUI
        {
            if (bits.Bits(1) == 1 && bits.Bits(8) == 255) { bits.Bits(16); bits.Bits(16); }
            if (bits.Bits(1) == 1) bits.Bits(1);
            if (bits.Bits(1) == 1) { bits.Bits(4); if (bits.Bits(1) == 1) bits.Bits(24); }
            if (bits.Bits(1) == 1) { bits.Ue(); bits.Ue(); }
            if (bits.Bits(1) == 1)
            {
                uint unitsInTick = bits.U32();
                timeScale = bits.U32();
                ticks = unitsInTick * 2; // a frame is two fields' worth of ticks
            }
        }
        if (width <= 0 || height <= 0 || width > 8192 || height > 8192) throw new InvalidDataException("Video size is invalid.");
        return new SpsInfo(profile, compat, level, width, height, timeScale, ticks);
    }

    private static byte[] RemoveEmulationPrevention(ReadOnlySpan<byte> data)
    {
        var output = new List<byte>(data.Length);
        int zeros = 0;
        foreach (byte b in data)
        {
            if (zeros >= 2 && b == 3) { zeros = 0; continue; }
            output.Add(b);
            zeros = b == 0 ? zeros + 1 : 0;
        }
        return output.ToArray();
    }

    private sealed class BitReader(byte[] data)
    {
        private int _bit;
        public int Remaining => data.Length * 8 - _bit;
        public int Bits(int count) => (int)Read(count); // count <= 31
        public uint U32() => (uint)Read(32);
        private long Read(int count)
        {
            if (count > Remaining) throw new InvalidDataException("Video parameter set is truncated.");
            long value = 0;
            for (int i = 0; i < count; i++, _bit++) value = (value << 1) | (uint)((data[_bit >> 3] >> (7 - (_bit & 7))) & 1);
            return value;
        }
        public int Ue()
        {
            int zeros = 0;
            while (Bits(1) == 0) if (++zeros > 31) throw new InvalidDataException("Video parameter set is malformed.");
            return (int)((1L << zeros) - 1 + Read(zeros));
        }
        public int Se() { int v = Ue(); return (v & 1) == 1 ? (v + 1) / 2 : -(v / 2); }
    }

    // --- MP4 boxes -------------------------------------------------------------------------

    private static void WriteMp4(Stream output, List<(List<byte[]> Nals, bool Key)> frames, List<byte[]> sps, List<byte[]> pps,
        SpsInfo info, uint timescale, uint delta)
    {
        var sizes = frames.Select(f => f.Nals.Sum(n => 4 + n.Length)).ToList();
        long payload = sizes.Sum(s => (long)s);
        if (payload > uint.MaxValue - 64) throw new InvalidDataException("Video is too large.");

        var ftyp = Box("ftyp", w => { w.Write(Ascii("isom")); w.Write(Be(0x200)); foreach (var b in new[] { "isom", "iso2", "avc1", "mp41" }) w.Write(Ascii(b)); });
        output.Write(ftyp);
        // mdat header, then the frames as length-prefixed NAL units.
        output.Write(Be((uint)(payload + 8)));
        output.Write(Ascii("mdat"));
        long chunkOffset = ftyp.Length + 8;
        Span<byte> length = stackalloc byte[4];
        foreach (var frame in frames)
            foreach (var nal in frame.Nals)
            {
                BinaryPrimitives.WriteUInt32BigEndian(length, (uint)nal.Length);
                output.Write(length);
                output.Write(nal);
            }

        ulong mediaDuration = (ulong)frames.Count * delta;
        uint movieDuration = (uint)Math.Min(uint.MaxValue, mediaDuration * 1000 / timescale);
        int[] matrix = [0x10000, 0, 0, 0, 0x10000, 0, 0, 0, 0x40000000];

        var avcC = Box("avcC", w =>
        {
            w.Write((byte)1); w.Write((byte)info.Profile); w.Write((byte)info.Compatibility); w.Write((byte)info.Level);
            w.Write((byte)0xFF); // 4-byte NAL lengths
            w.Write((byte)(0xE0 | sps.Count));
            foreach (var s in sps) { w.Write(Be16((ushort)s.Length)); w.Write(s); }
            w.Write((byte)pps.Count);
            foreach (var p in pps) { w.Write(Be16((ushort)p.Length)); w.Write(p); }
        });
        var avc1 = Box("avc1", w =>
        {
            w.Write(new byte[6]); w.Write(Be16(1)); w.Write(new byte[16]);
            w.Write(Be16((ushort)info.Width)); w.Write(Be16((ushort)info.Height));
            w.Write(Be(0x00480000)); w.Write(Be(0x00480000)); w.Write(Be(0)); w.Write(Be16(1));
            w.Write(new byte[32]); w.Write(Be16(0x18)); w.Write(Be16(0xFFFF));
            w.Write(avcC);
        });
        var stbl = Box("stbl", w =>
        {
            w.Write(FullBox("stsd", 0, 0, x => { x.Write(Be(1)); x.Write(avc1); }));
            w.Write(FullBox("stts", 0, 0, x => { x.Write(Be(1)); x.Write(Be((uint)frames.Count)); x.Write(Be(delta)); }));
            var keys = frames.Select((f, i) => (f.Key, Index: i + 1)).Where(f => f.Key).Select(f => (uint)f.Index).ToList();
            w.Write(FullBox("stss", 0, 0, x => { x.Write(Be((uint)keys.Count)); foreach (var k in keys) x.Write(Be(k)); }));
            w.Write(FullBox("stsc", 0, 0, x => { x.Write(Be(1)); x.Write(Be(1)); x.Write(Be((uint)frames.Count)); x.Write(Be(1)); }));
            w.Write(FullBox("stsz", 0, 0, x => { x.Write(Be(0)); x.Write(Be((uint)frames.Count)); foreach (var s in sizes) x.Write(Be((uint)s)); }));
            w.Write(FullBox("stco", 0, 0, x => { x.Write(Be(1)); x.Write(Be((uint)chunkOffset)); }));
        });
        var minf = Box("minf", w =>
        {
            w.Write(FullBox("vmhd", 0, 1, x => x.Write(new byte[8])));
            w.Write(Box("dinf", x => x.Write(FullBox("dref", 0, 0, y => { y.Write(Be(1)); y.Write(FullBox("url ", 0, 1, _ => { })); }))));
            w.Write(stbl);
        });
        var mdia = Box("mdia", w =>
        {
            w.Write(FullBox("mdhd", 0, 0, x =>
            {
                x.Write(Be(0)); x.Write(Be(0)); x.Write(Be(timescale)); x.Write(Be((uint)Math.Min(uint.MaxValue, mediaDuration)));
                x.Write(Be16(0x55C4)); x.Write(Be16(0)); // language "und"
            }));
            w.Write(FullBox("hdlr", 0, 0, x => { x.Write(Be(0)); x.Write(Ascii("vide")); x.Write(new byte[12]); x.Write(Encoding.ASCII.GetBytes("VideoHandler\0")); }));
            w.Write(minf);
        });
        var moov = Box("moov", w =>
        {
            w.Write(FullBox("mvhd", 0, 0, x =>
            {
                x.Write(Be(0)); x.Write(Be(0)); x.Write(Be(1000)); x.Write(Be(movieDuration));
                x.Write(Be(0x00010000)); x.Write(Be16(0x0100)); x.Write(new byte[10]);
                foreach (var m in matrix) x.Write(Be((uint)m));
                x.Write(new byte[24]); x.Write(Be(2));
            }));
            w.Write(Box("trak", x =>
            {
                x.Write(FullBox("tkhd", 0, 3, y =>
                {
                    y.Write(Be(0)); y.Write(Be(0)); y.Write(Be(1)); y.Write(Be(0)); y.Write(Be(movieDuration));
                    y.Write(new byte[8]); y.Write(Be16(0)); y.Write(Be16(0)); y.Write(Be16(0)); y.Write(Be16(0));
                    foreach (var m in matrix) y.Write(Be((uint)m));
                    y.Write(Be((uint)info.Width << 16)); y.Write(Be((uint)info.Height << 16));
                }));
                x.Write(mdia);
            }));
        });
        output.Write(moov);
    }

    private static byte[] Box(string type, Action<BinaryWriter> body)
    {
        using var content = new MemoryStream();
        using (var w = new BinaryWriter(content, Encoding.ASCII, leaveOpen: true)) body(w);
        var box = new byte[8 + content.Length];
        BinaryPrimitives.WriteUInt32BigEndian(box, (uint)box.Length);
        Encoding.ASCII.GetBytes(type, box.AsSpan(4));
        content.ToArray().CopyTo(box, 8);
        return box;
    }

    private static byte[] FullBox(string type, byte version, uint flags, Action<BinaryWriter> body) =>
        Box(type, w => { w.Write(Be((uint)version << 24 | flags)); body(w); });

    private static byte[] Be(uint value) { var b = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(b, value); return b; }
    private static byte[] Be16(ushort value) { var b = new byte[2]; BinaryPrimitives.WriteUInt16BigEndian(b, value); return b; }
    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);
}
