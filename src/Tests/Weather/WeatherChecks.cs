using System.IO;
using Pulsewave.Themes;

namespace Pulsewave.Regression;

// Weather: reading Open-Meteo answers (saved samples, no network), the theme data names, °F,
// and saved settings. The live service is only asked by --weather-probe.
internal static class WeatherChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("WEATHER: " + message);
        Console.WriteLine("PASS WEATHER: " + message);
    }

    private const string Places = """
        {"results":[{"id":2988507,"name":"Paris","latitude":48.85341,"longitude":2.3488,"country":"France","admin1":"Île-de-France"},
                    {"id":4717560,"name":"Paris","latitude":33.66094,"longitude":-95.55551,"country":"United States","admin1":"Texas"},
                    {"id":1,"name":"Broken"}],"generationtime_ms":0.5}
        """;

    private const string Forecast = """
        {"latitude":48.86,"longitude":2.34,"timezone":"Europe/Paris",
         "current":{"time":"2026-11-09T20:00","temperature_2m":16.4,"apparent_temperature":15.2,"weather_code":61,"is_day":0},
         "daily":{"time":["2026-11-09","2026-11-10","2026-11-11","2026-11-12"],"weather_code":[61,3,0,95],
                  "temperature_2m_max":[23.1,17.0,12.6,9.9],"temperature_2m_min":[12.4,13.2,5.5,4.0],
                  "sunrise":["2026-11-09T07:55","2026-11-10T07:57","2026-11-11T07:58","2026-11-12T08:00"],
                  "sunset":["2026-11-09T19:22","2026-11-10T19:20","2026-11-11T19:19","2026-11-12T19:17"]}}
        """;

    public static void Run(string outputRoot)
    {
        var places = WeatherClient.ParsePlaces(Places);
        Check(places.Count == 2 && places[0].Label == "Paris, Île-de-France, France" && places[1] is { Region: "Texas", Longitude: -95.55551 } &&
              WeatherClient.ParsePlaces("{\"generationtime_ms\":1}").Count == 0,
            "City search results give name, region and country; places without coordinates and empty answers are skipped");

        var now = new DateTime(2026, 11, 9, 20, 5, 0);
        var report = WeatherClient.ParseForecast(Forecast, "Paris", now);
        Check(report is { TempC: 16.4, FeelsC: 15.2, Code: 61, IsDay: false, HighC: 23.1, LowC: 12.4 } && report.Days.Count == 4 &&
              report.Sunrise == new DateTime(2026, 11, 9, 7, 55, 0) && report.Sunset == new DateTime(2026, 11, 9, 19, 22, 0),
            "A forecast gives the current temperature, feels like, conditions, today's high and low, sunrise and sunset, and the next days");

        var c = new ThemeDataContext { Now = now, Weather = report };
        string V(string name, bool f = false) => ThemeData.Display(ThemeData.Read(name, null, f, c), true);
        Check(V("WEATHER_TEMP") == "16°" && V("WEATHER_FEELS") == "15°" && V("WEATHER_HIGH") == "23°" && V("WEATHER_LOW") == "12°" &&
              V("WEATHER_TEXT") == "Light rain" && V("WEATHER_ICON") == "\U0001F327" && V("WEATHER_CITY") == "Paris" &&
              V("WEATHER_SUNRISE") == "07:55" && V("WEATHER_SUNSET") == "19:22" && V("Weather") == "16° Light rain" &&
              V("WEATHER_D1_DAY") == "Tue" && V("WEATHER_D1_HIGH") == "17°" && V("WEATHER_D1_LOW") == "13°" && V("WEATHER_D1_ICON") == "☁" &&
              V("WEATHER_D2_DAY") == "Wed" && V("WEATHER_D2_ICON") == "☀",
            "Theme data names show the weather; the L-Connect 'Weather' name now works too");
        var f = new ThemeDataContext { Now = now, Weather = report with { Fahrenheit = true } };
        Check(V("WEATHER_TEMP", f: true) == "62°" && ThemeData.Display(ThemeData.Read("WEATHER_HIGH", null, false, f), true) == "74°" &&
              ThemeData.Display(ThemeData.Read("WEATHER_TEMP", null, false, new ThemeDataContext()), true) == ThemeData.Missing &&
              !ThemeData.Unsupported.Contains("Weather") && ThemeData.IsAvailable("WEATHER_TEMP", new MonitorSettings()),
            "°F comes from the city's setting or the element's own; with no weather yet the values show --");
        Check(WeatherClient.Symbol(0, isDay: false) == "☾" && WeatherClient.Describe(96) == "Thunderstorm, hail" && WeatherClient.Symbol(73) == "❄",
            "Night-time clear skies show a moon; every weather code has words and a symbol");

        string root = Path.Combine(outputRoot, "weather-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "settings.json");
        File.WriteAllText(file, "{\"ActiveTheme\":\"x\"}");
        var older = SettingsStore.LoadFile(file);
        bool none = !older.Weather.IsSet && !older.Weather.Fahrenheit;
        older.Weather = new WeatherSettings { City = "Atlantis", Latitude = 120, Longitude = 10 };
        SettingsStore.SaveFile(file, older);
        bool repaired = !SettingsStore.LoadFile(file).Weather.IsSet;
        older.Weather = new WeatherSettings { City = " Paris ", Latitude = 48.85341, Longitude = 2.3488, Fahrenheit = true };
        SettingsStore.SaveFile(file, older);
        var saved = SettingsStore.LoadFile(file).Weather;
        Check(none && repaired && saved is { City: "Paris", Latitude: 48.85341, Fahrenheit: true, IsSet: true },
            "Older settings have no city; impossible coordinates are dropped; a city is saved with its units");
    }
}
