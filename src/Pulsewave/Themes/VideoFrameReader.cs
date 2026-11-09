using System.Runtime.InteropServices;

namespace Pulsewave.Themes;

/// <summary>
/// Decodes a video to 32-bit BGRX frames, one at a time, with Media Foundation's Source
/// Reader (built into Windows). Use from a single thread. Shared by theme playback, the
/// editor's poster frame.
/// </summary>
public sealed class VideoFrameReader : IDisposable
{
    private IMFSourceReader? _reader;
    private readonly int _stride;
    private bool _started;

    public int Width { get; }
    public int Height { get; }

    public VideoFrameReader(string path)
    {
        Check(MFStartup(0x20070, 0));
        _started = true;
        try
        {
            Check(MFCreateAttributes(out var attributes, 1));
            Check(attributes.SetUINT32(MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING, 1)); // decoder output converted to RGB32
            Check(MFCreateSourceReaderFromURL(path, attributes, out var reader));
            _reader = reader;
            Marshal.ReleaseComObject(attributes);
            Check(reader.SetStreamSelection(AllStreams, false));
            Check(reader.SetStreamSelection(FirstVideoStream, true));
            Check(MFCreateMediaType(out var wanted));
            Check(wanted.SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video));
            Check(wanted.SetGUID(MF_MT_SUBTYPE, MFVideoFormat_RGB32));
            Check(reader.SetCurrentMediaType(FirstVideoStream, IntPtr.Zero, wanted));
            Marshal.ReleaseComObject(wanted);
            Check(reader.GetCurrentMediaType(FirstVideoStream, out var actual));
            Check(actual.GetUINT64(MF_MT_FRAME_SIZE, out ulong size));
            Width = (int)(size >> 32); Height = (int)(size & 0xFFFFFFFF);
            _stride = actual.GetUINT32(MF_MT_DEFAULT_STRIDE, out uint s) >= 0 ? unchecked((int)s) : Width * 4;
            Marshal.ReleaseComObject(actual);
            if (Width is <= 0 or > 8192 || Height is <= 0 or > 8192) throw new InvalidOperationException("Invalid video size.");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Decodes the next frame into <paramref name="pixels"/> (Width*Height*4 bytes; null only
    /// advances). Returns false at the end of the video. Timestamps are in 100 ns units.
    /// </summary>
    public bool ReadFrame(byte[]? pixels, out long timestamp)
    {
        if (_reader is null) throw new ObjectDisposedException(nameof(VideoFrameReader));
        Check(_reader.ReadSample(FirstVideoStream, 0, out _, out int flags, out timestamp, out var sample));
        if ((flags & EndOfStream) != 0 || sample is null)
        {
            if (sample != null) Marshal.ReleaseComObject(sample);
            return false;
        }
        try
        {
            if (pixels is null) return true;
            if (pixels.Length < Width * Height * 4) throw new ArgumentException("Frame buffer is too small.", nameof(pixels));
            Check(sample.ConvertToContiguousBuffer(out var buffer));
            try
            {
                Check(buffer.Lock(out IntPtr data, out _, out int length));
                try
                {
                    int row = Width * 4, pitch = Math.Max(row, Math.Abs(_stride));
                    int rows = Math.Min(Height, length / pitch); // never read past the decoder's buffer
                    for (int y = 0; y < rows; y++)
                        Marshal.Copy(data + (_stride < 0 ? Height - 1 - y : y) * pitch, pixels, y * row, row); // negative stride: bottom-up
                }
                finally { buffer.Unlock(); }
            }
            finally { Marshal.ReleaseComObject(buffer); }
            return true;
        }
        finally { Marshal.ReleaseComObject(sample); }
    }

    public void Rewind()
    {
        if (_reader is null) return;
        var start = new PropVariant { Type = 20 }; // VT_I8 position 0
        Check(_reader.SetCurrentPosition(Guid.Empty, ref start));
    }

    /// <summary>Up to <paramref name="count"/> frames spread evenly across the whole video.</summary>
    public static List<byte[]> Sample(string path, int count, out int width, out int height)
    {
        using var reader = new VideoFrameReader(path);
        width = reader.Width; height = reader.Height;
        int total = 0;
        while (total < 100_000 && reader.ReadFrame(null, out _)) total++; // count first; decoding without copying is cheap
        reader.Rewind();
        var frames = new List<byte[]>();
        double step = Math.Max(1, total / (double)Math.Max(1, count));
        for (int i = 0, next = 0; i < total && frames.Count < count; i++)
        {
            bool keep = i == next;
            var pixels = keep ? new byte[reader.Width * reader.Height * 4] : null;
            if (!reader.ReadFrame(pixels, out _)) break;
            if (keep) { frames.Add(pixels!); next = (int)Math.Round(frames.Count * step); }
        }
        return frames;
    }

    public void Dispose()
    {
        if (_reader != null) { Marshal.ReleaseComObject(_reader); _reader = null; }
        if (_started) { _started = false; MFShutdown(); }
    }

    private static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }

    // --- Media Foundation interop ----------------------------------------------------------

    private const int FirstVideoStream = unchecked((int)0xFFFFFFFC);
    private const int AllStreams = unchecked((int)0xFFFFFFFE);
    private const int EndOfStream = 0x2;
    private static readonly Guid MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING = new("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d");
    private static readonly Guid MF_MT_MAJOR_TYPE = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    private static readonly Guid MF_MT_SUBTYPE = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    private static readonly Guid MF_MT_FRAME_SIZE = new("1652c33d-d6b2-4012-b834-72030849a37d");
    private static readonly Guid MF_MT_DEFAULT_STRIDE = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
    private static readonly Guid MFMediaType_Video = new("73646976-0000-0010-8000-00aa00389b71");
    private static readonly Guid MFVideoFormat_RGB32 = new("00000016-0000-0010-8000-00aa00389b71");

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public long Value;
    }

    [DllImport("mfplat.dll")] private static extern int MFStartup(int version, int flags);
    [DllImport("mfplat.dll")] private static extern int MFShutdown();
    [DllImport("mfplat.dll")] private static extern int MFCreateAttributes(out IMFAttributes attributes, int size);
    [DllImport("mfplat.dll")] private static extern int MFCreateMediaType(out IMFMediaType type);
    [DllImport("mfreadwrite.dll", CharSet = CharSet.Unicode)]
    private static extern int MFCreateSourceReaderFromURL(string url, IMFAttributes attributes, out IMFSourceReader reader);

    // Interfaces are declared in vtable order; only the methods used here have real signatures.
    [ComImport, Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFAttributes
    {
        void GetItem(); void GetItemType(); void CompareItem(); void Compare();
        [PreserveSig] int GetUINT32(in Guid key, out uint value);
        [PreserveSig] int GetUINT64(in Guid key, out ulong value);
        void GetDouble(); void GetGUID(); void GetStringLength(); void GetString(); void GetAllocatedString();
        void GetBlobSize(); void GetBlob(); void GetAllocatedBlob(); void GetUnknown(); void SetItem(); void DeleteItem(); void DeleteAllItems();
        [PreserveSig] int SetUINT32(in Guid key, uint value);
        void SetUINT64(); void SetDouble();
        [PreserveSig] int SetGUID(in Guid key, in Guid value);
        void SetString(); void SetBlob(); void SetUnknown(); void LockStore(); void UnlockStore(); void GetCount(); void GetItemByIndex(); void CopyAllItems();
    }

    [ComImport, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFMediaType
    {
        void GetItem(); void GetItemType(); void CompareItem(); void Compare();
        [PreserveSig] int GetUINT32(in Guid key, out uint value);
        [PreserveSig] int GetUINT64(in Guid key, out ulong value);
        void GetDouble(); void GetGUID(); void GetStringLength(); void GetString(); void GetAllocatedString();
        void GetBlobSize(); void GetBlob(); void GetAllocatedBlob(); void GetUnknown(); void SetItem(); void DeleteItem(); void DeleteAllItems();
        [PreserveSig] int SetUINT32(in Guid key, uint value);
        void SetUINT64(); void SetDouble();
        [PreserveSig] int SetGUID(in Guid key, in Guid value);
        void SetString(); void SetBlob(); void SetUnknown(); void LockStore(); void UnlockStore(); void GetCount(); void GetItemByIndex(); void CopyAllItems();
        void GetMajorType(); void IsCompressedFormat(); void IsEqual(); void GetRepresentation(); void FreeRepresentation();
    }

    [ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFSample
    {
        void GetItem(); void GetItemType(); void CompareItem(); void Compare(); void GetUINT32(); void GetUINT64();
        void GetDouble(); void GetGUID(); void GetStringLength(); void GetString(); void GetAllocatedString();
        void GetBlobSize(); void GetBlob(); void GetAllocatedBlob(); void GetUnknown(); void SetItem(); void DeleteItem(); void DeleteAllItems();
        void SetUINT32(); void SetUINT64(); void SetDouble(); void SetGUID();
        void SetString(); void SetBlob(); void SetUnknown(); void LockStore(); void UnlockStore(); void GetCount(); void GetItemByIndex(); void CopyAllItems();
        void GetSampleFlags(); void SetSampleFlags(); void GetSampleTime(); void SetSampleTime(); void GetSampleDuration(); void SetSampleDuration();
        void GetBufferCount(); void GetBufferByIndex();
        [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer buffer);
    }

    [ComImport, Guid("045fa593-8799-42b8-bc8d-8968c6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFMediaBuffer
    {
        [PreserveSig] int Lock(out IntPtr buffer, out int maxLength, out int currentLength);
        [PreserveSig] int Unlock();
    }

    [ComImport, Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFSourceReader
    {
        void GetStreamSelection();
        [PreserveSig] int SetStreamSelection(int stream, [MarshalAs(UnmanagedType.Bool)] bool selected);
        void GetNativeMediaType();
        [PreserveSig] int GetCurrentMediaType(int stream, out IMFMediaType type);
        [PreserveSig] int SetCurrentMediaType(int stream, IntPtr reserved, IMFMediaType type);
        [PreserveSig] int SetCurrentPosition(in Guid timeFormat, ref PropVariant position);
        [PreserveSig] int ReadSample(int stream, int control, out int actualStream, out int flags, out long timestamp, out IMFSample sample);
    }
}
