using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NotchMusic.Models;
using NotchMusic.Services.Settings;

namespace NotchMusic.Services.Prayer;

public interface IPrayerService
{
    PrayerSchedule GetTodaySchedule();
    PrayerSchedule GetScheduleForDate(DateTime date);
    Task<CityLocation?> DetectLocationAsync(CancellationToken cancellationToken = default);
    void UpdateLocation(string cityName, double latitude, double longitude, double timezone);
}

public class PrayerService : IPrayerService
{
    private readonly ISettingsService _settingsService;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(6) };

    public PrayerService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public PrayerSchedule GetTodaySchedule()
    {
        return GetScheduleForDate(DateTime.Now);
    }

    public PrayerSchedule GetScheduleForDate(DateTime date)
    {
        var settings = _settingsService.CurrentSettings;
        return PrayerTimesCalculator.CalculateSchedule(
            date,
            settings.PrayerLatitude,
            settings.PrayerLongitude,
            settings.PrayerTimezone,
            string.IsNullOrWhiteSpace(settings.PrayerCity) ? "Jakarta" : settings.PrayerCity
        );
    }

    public void UpdateLocation(string cityName, double latitude, double longitude, double timezone)
    {
        var settings = _settingsService.CurrentSettings;
        settings.PrayerCity = cityName;
        settings.PrayerLatitude = latitude;
        settings.PrayerLongitude = longitude;
        settings.PrayerTimezone = timezone;
        _settingsService.Save();
    }

    public async Task<CityLocation?> DetectLocationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Try ip-api.com
            var json = await _httpClient.GetStringAsync("http://ip-api.com/json/?fields=status,country,regionName,city,lat,lon,timezone,offset", cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "success")
            {
                string city = root.TryGetProperty("city", out var c) ? c.GetString() ?? "Jakarta" : "Jakarta";
                string region = root.TryGetProperty("regionName", out var r) ? r.GetString() ?? "" : "";
                double lat = root.TryGetProperty("lat", out var latProp) ? latProp.GetDouble() : -6.2088;
                double lon = root.TryGetProperty("lon", out var lonProp) ? lonProp.GetDouble() : 106.8456;
                double offsetSeconds = root.TryGetProperty("offset", out var offProp) ? offProp.GetDouble() : 25200;
                double tz = offsetSeconds / 3600.0;

                return new CityLocation
                {
                    Name = city,
                    Province = region,
                    Latitude = lat,
                    Longitude = lon,
                    Timezone = tz
                };
            }
        }
        catch
        {
        }

        try
        {
            // 2. Fallback: ipapi.co
            var json = await _httpClient.GetStringAsync("https://ipapi.co/json/", cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("city", out var c))
            {
                string city = c.GetString() ?? "Jakarta";
                string region = root.TryGetProperty("region", out var r) ? r.GetString() ?? "" : "";
                double lat = root.TryGetProperty("latitude", out var latProp) ? latProp.GetDouble() : -6.2088;
                double lon = root.TryGetProperty("longitude", out var lonProp) ? lonProp.GetDouble() : 106.8456;
                double tz = 7.0;
                if (root.TryGetProperty("utc_offset", out var offProp))
                {
                    var offsetStr = offProp.GetString();
                    if (!string.IsNullOrEmpty(offsetStr) && double.TryParse(offsetStr.Replace("+", ""), out var parsedOff))
                    {
                        tz = parsedOff / 100.0;
                    }
                }

                return new CityLocation
                {
                    Name = city,
                    Province = region,
                    Latitude = lat,
                    Longitude = lon,
                    Timezone = tz
                };
            }
        }
        catch
        {
        }

        return null;
    }
}
