using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Pulsewave.Themes;

/// <summary>
/// Plays a theme's background video as a muted loop: a background thread decodes frames with
/// <see cref="VideoFrameReader"/> (Media Foundation), paces them by timestamp and presents them
/// through a WriteableBitmap. WPF's MediaElement is not used because it needs the optional
/// Windows Media Player Legacy feature, which many Windows 11 installs lack.
/// </summary>
public sealed class VideoLoop : IDisposable
{
    private readonly string _path;
    private readonly Dispatcher _ui;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _running = new(true);
    private volatile bool _stopping;
    private WriteableBitmap? _bitmap;
    private byte[]? _pending;
    private int _width, _height;
    private int _framePosted;

    /// <summary>Raised on the UI thread with the bitmap that frames are drawn into.</summary>
    public event Action<WriteableBitmap>? Started;
    /// <summary>Raised on the UI thread when the video cannot be played.</summary>
    public event Action<string>? Failed;
    public int FramesShown { get; private set; }

    public VideoLoop(string path, Dispatcher ui)
    {
        _path = path;
        _ui = ui;
        _thread = new Thread(Run) { IsBackground = true, Name = "Pulsewave theme video" };
        _thread.Start();
    }

    public void Pause() => _running.Reset();
    public void Resume() => _running.Set();

    public void Dispose()
    {
        _stopping = true;
        _running.Set();
        if (Thread.CurrentThread != _thread) _thread.Join(2000);
    }

    private void Run()
    {
        try
        {
            using var reader = new VideoFrameReader(_path);
            _width = reader.Width; _height = reader.Height;
            // Normal priority runs before the Render-priority frame posts that follow.
            _ui.BeginInvoke(DispatcherPriority.Normal, () =>
            {
                if (_stopping) return;
                _bitmap = new WriteableBitmap(_width, _height, 96, 96, PixelFormats.Bgr32, null);
                Started?.Invoke(_bitmap);
            });
            Loop(reader);
        }
        catch (Exception ex) // anything escaping a background thread would end the app
        {
            if (!_stopping) _ui.BeginInvoke(() => Failed?.Invoke(ex.Message));
        }
    }

    private void Loop(VideoFrameReader reader)
    {
        var clock = Stopwatch.StartNew();
        long loopStart = 0, lastStamp = 0; // 100 ns units
        int frameBytes = _width * _height * 4;
        while (!_stopping)
        {
            if (!_running.IsSet)
            {
                _running.Wait();
                clock.Restart(); loopStart = -lastStamp; // resume where it paused
                continue;
            }
            var pixels = Interlocked.Exchange(ref _pending, null) ?? new byte[frameBytes];
            if (!reader.ReadFrame(pixels, out long stamp))
            {
                Interlocked.Exchange(ref _pending, pixels);
                reader.Rewind();
                loopStart = clock.Elapsed.Ticks;
                continue;
            }
            lastStamp = stamp;
            long wait = (loopStart + stamp - clock.Elapsed.Ticks) / TimeSpan.TicksPerMillisecond;
            if (wait > 0) Thread.Sleep((int)Math.Min(wait, 1000));
            // Decide after the wait, so the UI has had the whole frame interval to draw the
            // previous frame; skip this one only if it genuinely has not.
            if (Interlocked.CompareExchange(ref _framePosted, 1, 0) == 0)
                _ui.BeginInvoke(DispatcherPriority.Render, () => Present(pixels));
            else
                Interlocked.Exchange(ref _pending, pixels);
        }
    }

    private void Present(byte[] pixels)
    {
        if (!_stopping && _bitmap != null)
        {
            _bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, _width, _height), pixels, _width * 4, 0);
            FramesShown++;
        }
        Interlocked.Exchange(ref _pending, pixels);
        Volatile.Write(ref _framePosted, 0);
    }
}
