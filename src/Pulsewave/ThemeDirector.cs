namespace Pulsewave;

/// <summary>
/// Decides which theme the HUD shows for the moment (0.10). The user's own theme (settings' ActiveTheme, changed by
/// their picks and the playlist) is the base; a game or a schedule period can show another theme over it without
/// changing it, so when they end the user's theme simply shows again.
///
/// Precedence, highest first: a theme the user picks by hand (held until the game or schedule period that was running
/// when they picked it ends), a running game, the schedule, the playlist (paused while a game or a schedule period
/// with a theme is in control).
/// </summary>
public sealed class ThemeDirector
{
    /// <summary>
    /// What is going on now. GameKey: the game session running (null: none); GameTheme: the theme for it ("" or null:
    /// none). PeriodKey: the schedule period showing a theme (rule and its start, so tomorrow's run is a new period);
    /// PeriodTheme: its theme.
    /// </summary>
    public readonly record struct Moment(string? GameKey, string? GameTheme, string? PeriodKey, string? PeriodTheme)
    {
        public bool GameOn => GameKey != null && !string.IsNullOrEmpty(GameTheme);
        public bool PeriodOn => PeriodKey != null && !string.IsNullOrEmpty(PeriodTheme);
    }

    private string? _heldGame, _heldPeriod;

    /// <summary>The theme to show over the user's own, or null to show theirs.</summary>
    public string? Decide(Moment now)
    {
        // A hand pick holds only for the game or period it was made in.
        if (_heldGame != null && _heldGame != now.GameKey) _heldGame = null;
        if (_heldPeriod != null && _heldPeriod != now.PeriodKey) _heldPeriod = null;
        if (now.GameOn) return _heldGame == now.GameKey ? null : now.GameTheme;
        if (now.PeriodOn) return _heldPeriod == now.PeriodKey ? null : now.PeriodTheme;
        return null;
    }

    /// <summary>The user picked a theme: it outranks the game and the schedule period running now, until they end.</summary>
    public void HandPicked(Moment now)
    {
        if (now.GameOn) _heldGame = now.GameKey;
        if (now.PeriodOn) _heldPeriod = now.PeriodKey;
    }

    /// <summary>Whether a game or a schedule period with a theme is running (held by a hand pick or not): the playlist waits.</summary>
    public static bool PausesPlaylist(Moment now) => now.GameOn || now.PeriodOn;

    /// <summary>
    /// The theme for a game: its own pick, else the default. "none" for the game means don't switch for it.
    /// </summary>
    public static string? GameTheme(GamingSettings settings, string? gameKey)
    {
        if (gameKey == null) return null;
        string? own = settings.Themes.GetValueOrDefault(gameKey);
        if (own == GamingSettings.NoTheme) return null;
        return !string.IsNullOrEmpty(own) ? own : string.IsNullOrEmpty(settings.Theme) ? null : settings.Theme;
    }

    /// <summary>
    /// The schedule period showing a theme now: the topmost enabled rule with a theme that covers <paramref name="now"/>
    /// (rules are arranged in the Schedule window), keyed by its place and start.
    /// </summary>
    public static (string Key, string Theme)? SchedulePeriod(IReadOnlyList<ScheduleRule> rules, DateTime now)
    {
        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (string.IsNullOrEmpty(rule.Theme) || !rule.Covers(now) || rule.StartOf(now) is not { } start) continue;
            return ($"{i}@{start:yyyy-MM-ddTHH:mm}", rule.Theme);
        }
        return null;
    }
}
