using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace Pulsewave.Themes;

/// <summary>WCAG contrast helpers used to keep generated text readable on its background.</summary>
public static class Contrast
{
    public static double Luminance(Color c)
    {
        static double Channel(byte v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    public static double Ratio(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    /// <summary>
    /// Keeps the hue of <paramref name="preferred"/> but lightens or darkens it until it
    /// reaches <paramref name="minimum"/> contrast against <paramref name="background"/>.
    /// </summary>
    public static Color Readable(Color preferred, Color background, double minimum = 4.5)
    {
        if (Ratio(preferred, background) >= minimum) return Color.FromRgb(preferred.R, preferred.G, preferred.B);
        var toward = Ratio(Colors.White, background) >= Ratio(Colors.Black, background) ? Colors.White : Colors.Black;
        for (double t = 0.1; t <= 1.0001; t += 0.1)
        {
            var mixed = Mix(preferred, toward, t);
            if (Ratio(mixed, background) >= minimum) return mixed;
        }
        return toward;
    }

    public static Color Mix(Color a, Color b, double fraction)
    {
        fraction = double.IsFinite(fraction) ? Math.Clamp(fraction, 0, 1) : 0;
        byte L(byte x, byte y) => (byte)Math.Round(x + (y - x) * fraction);
        return Color.FromRgb(L(a.R, b.R), L(a.G, b.G), L(a.B, b.B));
    }

    public static string Hex(Color c, byte alpha = 255) => $"#{alpha:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
}
