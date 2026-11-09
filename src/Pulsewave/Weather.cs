using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace Pulsewave;

/// <summary>The city the weather is for (tray → Weather), found once by name and saved with its coordinates.</summary>
public sealed class WeatherSettings
{
    public string City { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool Fahrenheit { get; set; }

    public bool IsSet => City.Length > 0 && Latitude is >= -90 and <= 90 && Longitude is >= -180 and <= 180;

    public void Normalize()
    {
        City = (City ?? "").Trim();
        if (City.Length > 120) City = City[..120];
        if (!double.IsFinite(Latitude) || Latitude is < -90 or > 90 || !double.IsFinite(Longitude) || Longitude is < -180 or > 180)
            (City, Latitude, Longitude) = ("", 0, 0);
    }
}

/// <summary>A place the geocoder found for a typed name.</summary>
internal sealed record WeatherPlace(string Name, string Region, string Country, double Latitude, double Longitude)
{
    /// <summary>"Paris, Île-de-France, France".</summary>
    public string Label => string.Join(", ", new[] { Name, Region, Country }.Where(s => s.Length > 0).Distinct());
}

internal sealed record WeatherDay(DateTime Date, int Code, double HighC, double LowC);

/// <summary>One hour of the forecast: conditions, temperature and the chance of rain (0–100).</summary>
internal sealed record WeatherHour(DateTime Time, int Code, double TempC, double RainChance, bool IsDay);

/// <summary>Current conditions and the next days, temperatures in °C (shown in °F when chosen).</summary>
internal sealed record WeatherReport(string City, double TempC, double FeelsC, int Code, bool IsDay, double HighC, double LowC,
    DateTime? Sunrise, DateTime? Sunset, IReadOnlyList<WeatherDay> Days, DateTime Fetched, bool Fahrenheit = false)
{
    /// <summary>Tomorrow's sunrise, for "next sun event" after today's sunset.</summary>
    public DateTime? SunriseTomorrow { get; init; }
    /// <summary>The next hours, from the current one.</summary>
    public IReadOnlyList<WeatherHour> Hours { get; init; } = [];
}

/// <summary>
/// Weather from Open-Meteo (free, no account or key). The geocoder gets the typed city name once;
/// forecasts send only the saved coordinates. Nothing else about the PC or user is sent.
/// </summary>
internal static class WeatherClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    static WeatherClient() => Http.DefaultRequestHeaders.UserAgent.ParseAdd("Pulsewave/0.8 (desktop HUD)");

    public static async Task<IReadOnlyList<WeatherPlace>> FindAsync(string name, CancellationToken token = default)
    {
        string url = "https://geocoding-api.open-meteo.com/v1/search?count=8&format=json&language=en&name=" + Uri.EscapeDataString(name.Trim());
        return ParsePlaces(await Http.GetStringAsync(url, token));
    }

    public static async Task<WeatherReport> ForecastAsync(WeatherSettings place, DateTime now, CancellationToken token = default)
    {
        string url = "https://api.open-meteo.com/v1/forecast?latitude=" + place.Latitude.ToString("0.####", CultureInfo.InvariantCulture) +
            "&longitude=" + place.Longitude.ToString("0.####", CultureInfo.InvariantCulture) +
            "&current=temperature_2m,apparent_temperature,weather_code,is_day" +
            "&daily=weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset" +
            "&hourly=temperature_2m,weather_code,precipitation_probability,is_day&forecast_hours=24&timezone=auto&forecast_days=4";
        return ParseForecast(await Http.GetStringAsync(url, token), place.City, now) with { Fahrenheit = place.Fahrenheit };
    }

    internal static IReadOnlyList<WeatherPlace> ParsePlaces(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array) return [];
        return results.EnumerateArray()
            .Where(r => r.TryGetProperty("latitude", out _) && r.TryGetProperty("longitude", out _))
            .Select(r => new WeatherPlace(Str(r, "name"), Str(r, "admin1"), Str(r, "country"), r.GetProperty("latitude").GetDouble(), r.GetProperty("longitude").GetDouble()))
            .ToList();
    }

    internal static WeatherReport ParseForecast(string json, string city, DateTime now)
    {
        using var doc = JsonDocument.Parse(json);
        var current = doc.RootElement.GetProperty("current");
        var daily = doc.RootElement.GetProperty("daily");
        var dates = daily.GetProperty("time").EnumerateArray().Select(d => DateTime.ParseExact(d.GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture)).ToList();
        var codes = Numbers(daily, "weather_code");
        var highs = Numbers(daily, "temperature_2m_max");
        var lows = Numbers(daily, "temperature_2m_min");
        DateTime? Time(string field, int index) =>
            daily.TryGetProperty(field, out var list) && list.GetArrayLength() > index && list[index].GetString() is { } s &&
            DateTime.TryParseExact(s, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;
        var days = dates.Select((d, i) => new WeatherDay(d, (int)codes.ElementAtOrDefault(i), highs.ElementAtOrDefault(i), lows.ElementAtOrDefault(i))).ToList();
        return new WeatherReport(city, current.GetProperty("temperature_2m").GetDouble(), current.GetProperty("apparent_temperature").GetDouble(),
            current.GetProperty("weather_code").GetInt32(), current.TryGetProperty("is_day", out var day) && day.GetInt32() == 1,
            highs.ElementAtOrDefault(0), lows.ElementAtOrDefault(0), Time("sunrise", 0), Time("sunset", 0), days, now) { SunriseTomorrow = Time("sunrise", 1), Hours = ParseHours(doc.RootElement) };
    }

    private static List<WeatherHour> ParseHours(JsonElement root)
    {
        if (!root.TryGetProperty("hourly", out var hourly) || !hourly.TryGetProperty("time", out var times)) return [];
        var codes = Numbers(hourly, "weather_code"); var temps = Numbers(hourly, "temperature_2m");
        var rain = Numbers(hourly, "precipitation_probability"); var day = Numbers(hourly, "is_day");
        var hours = new List<WeatherHour>();
        int i = 0;
        foreach (var t in times.EnumerateArray())
        {
            if (t.GetString() is { } s && DateTime.TryParseExact(s, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at))
                hours.Add(new WeatherHour(at, (int)codes.ElementAtOrDefault(i), temps.ElementAtOrDefault(i),
                    double.IsFinite(rain.ElementAtOrDefault(i)) ? rain.ElementAtOrDefault(i) : 0, day.ElementAtOrDefault(i) != 0));
            i++;
        }
        return hours;
    }

    private static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()!.Trim() : "";

    private static List<double> Numbers(JsonElement e, string name) =>
        e.TryGetProperty(name, out var list) ? list.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.Number ? v.GetDouble() : double.NaN).ToList() : [];

    /// <summary>0 clear by day, 1 cloudy, foggy or night, 2 wet (rain, snow, storms): for coloring the symbol.</summary>
    public static int Kind(int code, bool isDay = true) => code switch
    {
        >= 51 => 2,
        0 or 1 => isDay ? 0 : 1,
        _ => 1
    };

    /// <summary>Whether a code means rain, snow or storms.</summary>
    public static bool Wet(int code) => code >= 51;

    /// <summary>WMO weather code in words.</summary>
    public static string Describe(int code) => code switch
    {
        0 => "Clear", 1 => "Mainly clear", 2 => "Partly cloudy", 3 => "Cloudy",
        45 or 48 => "Fog", >= 51 and <= 55 => "Drizzle", 56 or 57 => "Freezing drizzle",
        61 => "Light rain", 63 => "Rain", 65 => "Heavy rain", 66 or 67 => "Freezing rain",
        71 => "Light snow", 73 => "Snow", 75 => "Heavy snow", 77 => "Snow grains",
        80 or 81 => "Showers", 82 => "Heavy showers", 85 or 86 => "Snow showers",
        95 => "Thunderstorm", 96 or 99 => "Thunderstorm, hail", _ => "—"
    };

    /// <summary>A weather symbol from Segoe UI Symbol (drawn in the text's color).</summary>
    public static string Symbol(int code, bool isDay = true) => code switch
    {
        0 or 1 => isDay ? "☀" : "☾",
        2 => isDay ? "⛅" : "☁",
        3 => "☁",
        45 or 48 => "\U0001F32B",
        >= 51 and <= 67 or >= 80 and <= 82 => "\U0001F327",
        >= 71 and <= 77 or 85 or 86 => "❄",
        >= 95 => "⛈",
        _ => "☁"
    };
}
