namespace Pulsewave;

/// <summary>
/// Whether the sensor feed as a whole has stopped. HWiNFO's shared values carry no timestamps and single readings can
/// legitimately sit still for minutes, so one unchanged reading is never called stale (see TelemetryEngine). The feed
/// is stale when HWiNFO reports nothing at all, or when every reading has been exactly the same for
/// <see cref="FrozenAfter"/>: on a running PC loads, clocks and power always move, so a fully frozen feed means
/// HWiNFO has hung or stopped updating.
/// </summary>
public sealed class SensorFeed
{
    public static readonly TimeSpan FrozenAfter = TimeSpan.FromSeconds(30);

    private Dictionary<string, double>? _last;
    private DateTime? _sameSince;

    public SensorFeedState State { get; private set; } = SensorFeedState.Live;

    public SensorFeedState Update(SensorSnapshot snapshot, DateTime now)
    {
        var values = snapshot.Values.Where(v => v.Value is { } x && double.IsFinite(x)).ToDictionary(v => v.Key, v => v.Value!.Value, StringComparer.OrdinalIgnoreCase);
        if (snapshot.ExpectedCount > 0 && values.Count == 0)
        {
            (_last, _sameSince) = (null, null);
            return State = SensorFeedState.Offline;
        }
        bool same = values.Count >= 2 && _last != null && _last.Count == values.Count && values.All(v => _last.TryGetValue(v.Key, out var was) && was == v.Value);
        if (!same) _sameSince = now;
        _sameSince ??= now;
        _last = values;
        return State = same && now - _sameSince.Value >= FrozenAfter ? SensorFeedState.Frozen : SensorFeedState.Live;
    }
}

public enum SensorFeedState { Live, Frozen, Offline }
