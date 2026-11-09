using Pulsewave.Themes;

namespace Pulsewave.Regression;

// The app's own sample readings, so tests and the editor preview agree.
internal static class ThemeSamples
{
    public static ThemeDataContext Context() => ThemeSampleData.Uniform();
}
