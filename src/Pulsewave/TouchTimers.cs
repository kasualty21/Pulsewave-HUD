using System.Globalization;
using System.Runtime.InteropServices;

namespace Pulsewave;

/// <summary>A countdown timer and a stopwatch for touch themes; time is passed in so they are testable.</summary>
public sealed class TouchTimers
{
    private TimeSpan _length = TimeSpan.FromMinutes(5), _timerUsed, _stopwatchUsed;
    private DateTime? _timerSince, _stopwatchSince;
    private bool _finishedReported;

    public bool TimerRunning => _timerSince != null;
    public bool StopwatchRunning => _stopwatchSince != null;

    // Pomodoro: focus and break lengths that alternate by themselves; null for a plain countdown.
    private (TimeSpan Focus, TimeSpan Break)? _pomodoro;
    private bool _onBreak;

    /// <summary>"FOCUS" or "BREAK" during a Pomodoro, otherwise "".</summary>
    public string Phase => _pomodoro == null ? "" : _onBreak ? "BREAK" : "FOCUS";

    /// <summary>Which focus round of the Pomodoro this is (its break counts with it), from 1; 0 without a Pomodoro.</summary>
    public int Round => _pomodoro == null ? 0 : _round;
    private int _round;

    /// <summary>The countdown is stopped partway: started, not finished, not reset.</summary>
    public bool TimerPaused(DateTime now) => _timerSince == null && _timerUsed > TimeSpan.Zero && TimerLeft(now) > TimeSpan.Zero;

    /// <summary>Whether a one-card theme shows the stopwatch rather than the timer.</summary>
    public bool StopwatchShown { get; private set; }

    public void SwitchShown() => StopwatchShown = !StopwatchShown;

    /// <summary>Starts a Pomodoro: <paramref name="focusMinutes"/> of focus, then <paramref name="breakMinutes"/> of break, alternating.</summary>
    public void StartPomodoro(DateTime now, int focusMinutes, int breakMinutes)
    {
        _pomodoro = (TimeSpan.FromMinutes(focusMinutes), TimeSpan.FromMinutes(breakMinutes));
        _onBreak = false;
        _round = 1;
        _length = _pomodoro.Value.Focus;
        StopwatchShown = false;
        ResetTimer();
        _timerSince = now;
    }

    /// <summary>The shown clock's start / pause (timer or stopwatch).</summary>
    public void ToggleShown(DateTime now) { if (StopwatchShown) ToggleStopwatch(now); else ToggleTimer(now); }

    /// <summary>The shown clock's reset; resetting a stopped Pomodoro's timer ends the Pomodoro.</summary>
    public void ResetShown()
    {
        if (StopwatchShown) { ResetStopwatch(); return; }
        if (_pomodoro != null && _timerSince == null && _timerUsed == TimeSpan.Zero && !_onBreak) { _pomodoro = null; return; }
        if (_pomodoro is { } p) { _onBreak = false; _length = p.Focus; _round = 1; }
        ResetTimer();
    }

    public TimeSpan TimerLeft(DateTime now)
    {
        var left = _length - _timerUsed - (_timerSince is { } since ? now - since : TimeSpan.Zero);
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    public TimeSpan StopwatchElapsed(DateTime now) => _stopwatchUsed + (_stopwatchSince is { } since ? now - since : TimeSpan.Zero);

    /// <summary>Starts or pauses the countdown; a finished timer starts over.</summary>
    public void ToggleTimer(DateTime now)
    {
        if (_timerSince is { } since) { _timerUsed += now - since; _timerSince = null; return; }
        if (TimerLeft(now) <= TimeSpan.Zero) ResetTimer();
        _timerSince = now;
        _finishedReported = false;
    }

    public void ResetTimer() { _timerUsed = TimeSpan.Zero; _timerSince = null; _finishedReported = false; }

    /// <summary>Adds a minute (up to 24 hours), running or not; it ends a Pomodoro (the countdown becomes a plain one).</summary>
    public void AddMinute()
    {
        _length = TimeSpan.FromMinutes(Math.Min(24 * 60, _length.TotalMinutes + 1));
        _pomodoro = null;
        _onBreak = false;
    }

    public void ToggleStopwatch(DateTime now)
    {
        if (_stopwatchSince is { } since) { _stopwatchUsed += now - since; _stopwatchSince = null; }
        else _stopwatchSince = now;
    }

    public void ResetStopwatch() { _stopwatchUsed = TimeSpan.Zero; _stopwatchSince = null; }

    /// <summary>True once when a running countdown reaches zero.</summary>
    public bool Finished(DateTime now)
    {
        if (_timerSince is null || _finishedReported || TimerLeft(now) > TimeSpan.Zero) return false;
        if (_pomodoro is { } p)
        {
            // Focus ends: the break starts at once (and the other way round).
            _onBreak = !_onBreak;
            if (!_onBreak) _round++;
            _length = _onBreak ? p.Break : p.Focus;
            _timerUsed = TimeSpan.Zero;
            _timerSince = now;
            return true;
        }
        _timerUsed = _length;
        _timerSince = null;
        return _finishedReported = true;
    }

    /// <summary>"4:59", "12:05" or "1:02:03".</summary>
    public static string Format(TimeSpan time)
    {
        int seconds = (int)Math.Ceiling(time.TotalSeconds - 0.001);
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : t.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }
}

/// <summary>The default playback device's volume and mute, through the documented Core Audio API.</summary>
public static class AudioVolume
{
    /// <summary>Volume 0–100 and mute, or null when there is no playback device.</summary>
    public static (double Level, bool Muted)? Read()
    {
        try
        {
            var endpoint = Endpoint();
            if (endpoint is null) return null;
            endpoint.GetMasterVolumeLevelScalar(out float level);
            endpoint.GetMute(out bool muted);
            Marshal.ReleaseComObject(endpoint);
            return (Math.Round(level * 100), muted);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException) { return null; }
    }

    /// <summary>Changes the volume by <paramref name="step"/> points (unmuting when turned up).</summary>
    public static void Change(double step)
    {
        try
        {
            var endpoint = Endpoint();
            if (endpoint is null) return;
            endpoint.GetMasterVolumeLevelScalar(out float level);
            var context = Guid.Empty;
            endpoint.SetMasterVolumeLevelScalar((float)Math.Clamp(level + step / 100, 0, 1), ref context);
            if (step > 0) endpoint.SetMute(false, ref context);
            Marshal.ReleaseComObject(endpoint);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException) { }
    }

    /// <summary>Sets the volume to <paramref name="level"/> (0–100), unmuting above zero.</summary>
    public static void Set(double level)
    {
        try
        {
            var endpoint = Endpoint();
            if (endpoint is null) return;
            var context = Guid.Empty;
            endpoint.SetMasterVolumeLevelScalar((float)Math.Clamp(level / 100, 0, 1), ref context);
            if (level > 0) endpoint.SetMute(false, ref context);
            Marshal.ReleaseComObject(endpoint);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException) { }
    }

    public static void ToggleMute()
    {
        try
        {
            var endpoint = Endpoint();
            if (endpoint is null) return;
            endpoint.GetMute(out bool muted);
            var context = Guid.Empty;
            endpoint.SetMute(!muted, ref context);
            Marshal.ReleaseComObject(endpoint);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException) { }
    }

    private static IAudioEndpointVolume? Endpoint()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        try
        {
            if (enumerator.GetDefaultAudioEndpoint(0, 1, out var device) != 0 || device is null) return null; // render, multimedia
            var iid = typeof(IAudioEndpointVolume).GUID;
            int hr = device.Activate(ref iid, 1, IntPtr.Zero, out var volume);
            Marshal.ReleaseComObject(device);
            return hr == 0 ? volume as IAudioEndpointVolume : null;
        }
        finally { Marshal.ReleaseComObject(enumerator); }
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object result);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        void RegisterControlChangeNotify(IntPtr notify);
        void UnregisterControlChangeNotify(IntPtr notify);
        void GetChannelCount(out uint count);
        void SetMasterVolumeLevel(float decibels, ref Guid context);
        void SetMasterVolumeLevelScalar(float level, ref Guid context);
        void GetMasterVolumeLevel(out float decibels);
        void GetMasterVolumeLevelScalar(out float level);
        void SetChannelVolumeLevel(uint channel, float decibels, ref Guid context);
        void SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        void GetChannelVolumeLevel(uint channel, out float decibels);
        void GetChannelVolumeLevelScalar(uint channel, out float level);
        void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
