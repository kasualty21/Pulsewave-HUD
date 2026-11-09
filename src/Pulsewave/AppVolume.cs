using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Pulsewave;

/// <summary>One app's sound on the default playback device. Key groups an app's sessions (its program file, or "system").</summary>
internal sealed record AppAudio(string Key, string Name, string? ProgramPath, double Level, bool Muted, bool Playing);

/// <summary>
/// Per-app volume and mute on the default playback device, through the documented Core Audio
/// session API (the same one the Windows volume mixer uses). Nothing is recorded or listened to.
/// </summary>
internal static class AppVolume
{
    internal const string SystemKey = "system";
    /// <summary>The last Core Audio error, for the read-only probe.</summary>
    internal static string? LastError { get; private set; }

    /// <summary>
    /// Apps with sound sessions, those playing now first. An app with several sessions (or processes
    /// with the same program file) shows once, at its loudest.
    /// </summary>
    public static IReadOnlyList<AppAudio> Read()
    {
        var apps = new List<AppAudio>();
        Each((session, key, name, path, state) =>
        {
            var volume = (ISimpleAudioVolume)session;
            volume.GetMasterVolume(out float level);
            volume.GetMute(out bool muted);
            apps.Add(new AppAudio(key, name, path, Math.Round(level * 100), muted, state == AudioSessionStateActive));
            return true;
        });
        return Group(apps);
    }

    /// <summary>One row per app: its sessions merged (loudest level, muted only if all are, playing if any is).</summary>
    internal static IReadOnlyList<AppAudio> Group(IEnumerable<AppAudio> sessions) =>
        sessions.GroupBy(a => a.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First() with { Level = g.Max(a => a.Level), Muted = g.All(a => a.Muted), Playing = g.Any(a => a.Playing) })
            .OrderByDescending(a => a.Playing).ThenBy(a => a.Key == SystemKey).ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>Changes an app's volume by <paramref name="step"/> points (unmuting when turned up); returns its new level.</summary>
    public static double? Change(string key, double step)
    {
        double? result = null;
        Each((session, sessionKey, _, _, _) =>
        {
            if (!string.Equals(sessionKey, key, StringComparison.OrdinalIgnoreCase)) return true;
            var volume = (ISimpleAudioVolume)session;
            volume.GetMasterVolume(out float level);
            float next = (float)Math.Clamp(Math.Round(level * 100 + step) / 100, 0, 1);
            var context = Guid.Empty;
            volume.SetMasterVolume(next, ref context);
            if (step > 0) volume.SetMute(false, ref context);
            result = Math.Round(next * 100);
            return true;
        });
        return result;
    }

    /// <summary>Sets every session of an app to <paramref name="level"/> (0–100), unmuting above zero.</summary>
    public static void Set(string key, double level)
    {
        float to = (float)Math.Clamp(level / 100, 0, 1);
        Each((session, sessionKey, _, _, _) =>
        {
            if (!string.Equals(sessionKey, key, StringComparison.OrdinalIgnoreCase)) return true;
            var volume = (ISimpleAudioVolume)session;
            var context = Guid.Empty;
            volume.SetMasterVolume(to, ref context);
            if (level > 0) volume.SetMute(false, ref context);
            return true;
        });
    }

    /// <summary>Mutes or unmutes every session of an app.</summary>
    public static void SetMute(string key, bool mute)
    {
        Each((session, sessionKey, _, _, _) =>
        {
            if (!string.Equals(sessionKey, key, StringComparison.OrdinalIgnoreCase)) return true;
            var context = Guid.Empty;
            ((ISimpleAudioVolume)session).SetMute(mute, ref context);
            return true;
        });
    }

    /// <summary>The group key and display name for a session's process.</summary>
    internal static (string Key, string Name, string? Path) Identify(bool systemSounds, string? displayName, string? programPath, string? processName, string? description)
    {
        if (systemSounds) return (SystemKey, "System sounds", null);
        string key = !string.IsNullOrEmpty(programPath) ? programPath! : processName ?? "unknown";
        // A session's own name wins unless it is a resource reference ("@%SystemRoot%\…,-123").
        string name = !string.IsNullOrWhiteSpace(displayName) && !displayName!.StartsWith('@') ? displayName
            : !string.IsNullOrWhiteSpace(description) ? description!
            : !string.IsNullOrEmpty(processName) ? char.ToUpperInvariant(processName![0]) + processName[1..]
            : "App";
        return (key, name, programPath);
    }

    private delegate bool SessionAction(object session, string key, string name, string? path, int state);

    private static void Each(SessionAction action)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioSessionManager2? manager = null;
        IAudioSessionEnumerator? sessions = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (enumerator.GetDefaultAudioEndpoint(0, 1, out device) != 0 || device is null) return; // render, multimedia
            var iid = typeof(IAudioSessionManager2).GUID;
            if (device.Activate(ref iid, ClsctxAll, IntPtr.Zero, out var activated) != 0) return;
            manager = (IAudioSessionManager2)activated;
            if (manager.GetSessionEnumerator(out sessions) != 0 || sessions is null) return;
            sessions.GetCount(out int count);
            var names = new Dictionary<uint, (string? Path, string? Process, string? Description)>();
            for (int i = 0; i < count; i++)
            {
                if (sessions.GetSession(i, out var control) != 0 || control is null) continue;
                try
                {
                    var session = (IAudioSessionControl2)control;
                    session.GetState(out int state);
                    if (state == AudioSessionStateExpired) continue;
                    bool system = session.IsSystemSoundsSession() == 0;
                    session.GetProcessId(out uint pid);
                    if (!system && pid == 0) continue;
                    session.GetDisplayName(out string? display);
                    if (!names.TryGetValue(pid, out var process)) names[pid] = process = Describe(pid);
                    var (key, name, path) = Identify(system, display, process.Path, process.Process, process.Description);
                    if (!action(control, key, name, path, state)) return;
                }
                catch (Exception ex) when (ex is COMException or InvalidCastException) { LastError = ex.Message; } // a session that ended meanwhile
                finally { Marshal.ReleaseComObject(control); }
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException) { LastError = ex.Message; }
        finally
        {
            foreach (var com in new object?[] { sessions, manager, device, enumerator })
                if (com != null) Marshal.ReleaseComObject(com);
        }
    }

    private static (string? Path, string? Process, string? Description) Describe(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            string? path = null, description = null;
            try
            {
                path = process.MainModule?.FileName;
                if (path != null) description = FileVersionInfo.GetVersionInfo(path).FileDescription;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException) { }
            return (path, process.ProcessName, description);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return (null, null, null); }
    }

    private const int AudioSessionStateActive = 1, AudioSessionStateExpired = 2, ClsctxAll = 23;

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

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr groupingParam, uint flags, out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr groupingParam, uint flags, out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        void GetCount(out int count);
        [PreserveSig] int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object session);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        // IAudioSessionControl
        void GetState(out int state);
        void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string? name);
        void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
        void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string? path);
        void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
        void GetGroupingParam(out Guid group);
        void SetGroupingParam(ref Guid group, ref Guid context);
        void RegisterAudioSessionNotification(IntPtr notifications);
        void UnregisterAudioSessionNotification(IntPtr notifications);
        // IAudioSessionControl2
        void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string? id);
        void GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string? id);
        void GetProcessId(out uint processId);
        [PreserveSig] int IsSystemSoundsSession();
        void SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        void SetMasterVolume(float level, ref Guid context);
        void GetMasterVolume(out float level);
        void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
