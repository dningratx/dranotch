using System;
using System.Collections.Generic;
using System.Globalization;

namespace NotchMusic.Models;

public class BmkgRegionItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public override string ToString() => Name;
}

public class BmkgLocationSearchResult
{
    public string Adm4Code { get; set; } = string.Empty;
    public string ProvinceCode { get; set; } = string.Empty;
    public string ProvinceName { get; set; } = string.Empty;
    public string RegencyCode { get; set; } = string.Empty;
    public string RegencyName { get; set; } = string.Empty;
    public string DistrictCode { get; set; } = string.Empty;
    public string DistrictName { get; set; } = string.Empty;
    public string VillageName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Timezone { get; set; } = 7.0;

    public string DisplayTitle => !string.IsNullOrWhiteSpace(VillageName) && !string.IsNullOrWhiteSpace(DistrictName)
        ? $"{VillageName}, Kec. {DistrictName}"
        : (!string.IsNullOrWhiteSpace(DistrictName) ? $"Kec. {DistrictName}" : RegencyName);

    public string DisplaySubtitle => $"{RegencyName} • {ProvinceName} ({Adm4Code})";

    public string FullLabel => $"{DisplayTitle} — {RegencyName}, {ProvinceName}";
}

public class BmkgWeatherForecastItem
{
    // Crisp 24x24 Vector Geometries for Weather Icons
    public const string SunGeometry = "M 12,7 A 5,5 0 1 0 12,17 A 5,5 0 1 0 12,7 Z M 12,1.5 V 4.2 M 12,19.8 V 22.5 M 4.58,4.58 L 6.5,6.5 M 17.5,17.5 L 19.42,19.42 M 1.5,12 H 4.2 M 19.8,12 H 22.5 M 4.58,19.42 L 6.5,17.5 M 17.5,6.5 L 19.42,4.58";
    public const string MoonGeometry = "M 19.5,14.5 A 7.5,7.5 0 1 1 10.5,4.5 A 5.8,5.8 0 0 0 19.5,14.5 Z M 17.2,2.8 L 17.9,4.6 L 19.7,5.3 L 17.9,6.0 L 17.2,7.8 L 16.5,6.0 L 14.7,5.3 L 16.5,4.6 Z";
    public const string PartlyCloudyGeometry = "M 6.2,19 H 17.8 A 3.4,3.4 0 0 0 18.2,12.3 A 4.6,4.6 0 0 0 9.2,10.8 A 3.6,3.6 0 0 0 6.2,19 Z M 7.5,5.5 A 3.2,3.2 0 0 1 13.2,7.2 M 8,1.8 V 3.6 M 3.5,3.8 L 4.9,5.2 M 1.8,8.2 H 3.6 M 13.2,3.5 L 11.9,4.9";
    public const string CloudGeometry = "M 5.8,18.5 H 18.2 A 3.8,3.8 0 0 0 18.6,11.0 A 5.2,5.2 0 0 0 8.6,9.2 A 4.2,4.2 0 0 0 5.8,18.5 Z";
    public const string RainGeometry = "M 5.8,15.2 H 18.2 A 3.4,3.4 0 0 0 18.6,8.5 A 4.8,4.8 0 0 0 9.0,6.9 A 3.8,3.8 0 0 0 5.8,15.2 Z M 8.5,17.8 L 7.0,21.5 M 12.5,17.8 L 11.0,21.5 M 16.5,17.8 L 15.0,21.5";
    public const string ThunderGeometry = "M 5.8,14.8 H 18.2 A 3.4,3.4 0 0 0 18.6,8.2 A 4.8,4.8 0 0 0 9.0,6.6 A 3.8,3.8 0 0 0 5.8,14.8 Z M 13.0,15.2 L 10.2,19.2 H 13.2 L 11.8,22.8 L 15.8,18.0 H 12.8 Z";
    public const string FogGeometry = "M 6.2,13.8 H 17.8 A 3.2,3.2 0 0 0 18.2,7.6 A 4.5,4.5 0 0 0 9.2,6.2 A 3.5,3.5 0 0 0 6.2,13.8 Z M 4.0,17.5 H 20.0 M 6.5,20.8 H 17.5";

    public DateTime LocalDateTime { get; set; }
    public DateTime UtcDateTime { get; set; }
    public int TemperatureC { get; set; }
    public int HumidityPercent { get; set; }
    public int WeatherCode { get; set; }
    public string WeatherDesc { get; set; } = "Cerah Berawan";
    public string WeatherDescEn { get; set; } = "Partly Cloudy";
    public double WindSpeedKmh { get; set; }
    public string WindDirectionCode { get; set; } = "N";
    public int WindDirectionDeg { get; set; }
    public int CloudCoverPercent { get; set; }
    public double PrecipitationMm { get; set; }
    public string VisibilityText { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public bool IsCurrentSlot { get; set; }

    public string TimeLabel => LocalDateTime.ToString("HH:mm");

    public string DayLabel
    {
        get
        {
            var today = DateTime.Today;
            if (LocalDateTime.Date == today) return "Hari Ini";
            if (LocalDateTime.Date == today.AddDays(1)) return "Besok";
            if (LocalDateTime.Date == today.AddDays(2)) return "Lusa";
            return LocalDateTime.ToString("ddd, dd MMM", new CultureInfo("id-ID"));
        }
    }

    public string FullTimeLabel => $"{DayLabel} • {TimeLabel}";
    public string TemperatureFormatted => $"{TemperatureC}°C";
    public string HumidityFormatted => $"{HumidityPercent}%";
    public string WindSpeedFormatted => $"{WindSpeedKmh:0.#} km/j";
    public string PrecipitationFormatted => $"{PrecipitationMm:0.#} mm";
    public string CloudCoverFormatted => $"{CloudCoverPercent}%";

    public string WindDirectionLabel => WindDirectionCode?.ToUpperInvariant() switch
    {
        "N" => "Utara",
        "NE" => "Timur Laut",
        "E" => "Timur",
        "SE" => "Tenggara",
        "S" => "Selatan",
        "SW" => "Barat Daya",
        "W" => "Barat",
        "NW" => "Barat Laut",
        _ => string.IsNullOrWhiteSpace(WindDirectionCode) ? "-" : WindDirectionCode
    };

    public string WeatherEmoji => GetWeatherEmoji(WeatherCode, WeatherDesc, LocalDateTime.Hour);
    public string WeatherIconGeometry => GetWeatherIconGeometry(WeatherCode, WeatherDesc, LocalDateTime.Hour);
    public string WeatherIconColor => GetWeatherIconColor(WeatherCode, WeatherDesc, LocalDateTime.Hour);

    public bool IsExtremeWeather =>
        WeatherCode >= 63 ||
        WeatherDesc.Contains("Lebat", StringComparison.OrdinalIgnoreCase) ||
        WeatherDesc.Contains("Petir", StringComparison.OrdinalIgnoreCase) ||
        WeatherDesc.Contains("Ekstrem", StringComparison.OrdinalIgnoreCase) ||
        WeatherDesc.Contains("Badai", StringComparison.OrdinalIgnoreCase);

    public static string GetWeatherEmoji(int code, string? desc, int localHour)
    {
        bool isNight = localHour < 6 || localHour >= 18;
        var d = desc ?? string.Empty;

        if (code == 95 || code == 97 || d.Contains("Petir", StringComparison.OrdinalIgnoreCase))
            return "⛈";
        if (code == 63 || d.Contains("Hujan Lebat", StringComparison.OrdinalIgnoreCase))
            return "🌧";
        if (code == 61 || d.Contains("Hujan Sedang", StringComparison.OrdinalIgnoreCase))
            return "🌧";
        if (code == 60 || d.Contains("Hujan Ringan", StringComparison.OrdinalIgnoreCase) || d.Contains("Hujan Lokal", StringComparison.OrdinalIgnoreCase))
            return "🌦";
        if (d.Contains("Hujan", StringComparison.OrdinalIgnoreCase))
            return "🌧";
        if (code == 45 || code == 10 || d.Contains("Kabut", StringComparison.OrdinalIgnoreCase) || d.Contains("Asap", StringComparison.OrdinalIgnoreCase) || d.Contains("Udara Kabur", StringComparison.OrdinalIgnoreCase))
            return "🌫";
        if (code == 4 || d.Contains("Berawan Tebal", StringComparison.OrdinalIgnoreCase))
            return "☁";
        if (code == 3 || d.Equals("Berawan", StringComparison.OrdinalIgnoreCase))
            return "☁";
        if (code == 2 || d.Contains("Cerah Berawan", StringComparison.OrdinalIgnoreCase))
            return isNight ? "☁" : "⛅";
        if (code == 1 || code == 0 || d.Contains("Cerah", StringComparison.OrdinalIgnoreCase))
            return isNight ? "🌙" : "☀";

        return isNight ? "🌙" : "🌤";
    }

    public static string GetWeatherIconGeometry(int code, string? desc, int localHour)
    {
        bool isNight = localHour < 6 || localHour >= 18;
        var d = desc ?? string.Empty;

        if (code == 95 || code == 97 || d.Contains("Petir", StringComparison.OrdinalIgnoreCase))
            return ThunderGeometry;
        if (code >= 60 || d.Contains("Hujan", StringComparison.OrdinalIgnoreCase))
            return RainGeometry;
        if (code == 45 || code == 10 || d.Contains("Kabut", StringComparison.OrdinalIgnoreCase) || d.Contains("Asap", StringComparison.OrdinalIgnoreCase) || d.Contains("Udara Kabur", StringComparison.OrdinalIgnoreCase))
            return FogGeometry;
        if (code == 4 || code == 3 || d.Contains("Berawan Tebal", StringComparison.OrdinalIgnoreCase) || d.Equals("Berawan", StringComparison.OrdinalIgnoreCase))
            return CloudGeometry;
        if (code == 2 || d.Contains("Cerah Berawan", StringComparison.OrdinalIgnoreCase))
            return isNight ? CloudGeometry : PartlyCloudyGeometry;
        if (code == 1 || code == 0 || d.Contains("Cerah", StringComparison.OrdinalIgnoreCase))
            return isNight ? MoonGeometry : SunGeometry;

        return isNight ? MoonGeometry : PartlyCloudyGeometry;
    }

    public static string GetWeatherIconColor(int code, string? desc, int localHour)
    {
        bool isNight = localHour < 6 || localHour >= 18;
        var d = desc ?? string.Empty;

        if (code == 95 || code == 97 || d.Contains("Petir", StringComparison.OrdinalIgnoreCase))
            return "#FBBF24"; // Bright Amber Gold Lightning
        if (code >= 60 || d.Contains("Hujan", StringComparison.OrdinalIgnoreCase))
            return "#38BDF8"; // Bright Electric Sky Blue Rain
        if (code == 45 || code == 10 || d.Contains("Kabut", StringComparison.OrdinalIgnoreCase) || d.Contains("Asap", StringComparison.OrdinalIgnoreCase) || d.Contains("Udara Kabur", StringComparison.OrdinalIgnoreCase))
            return "#A5B4FC"; // Bright Periwinkle Mist
        if (code == 4 || code == 3 || d.Contains("Berawan Tebal", StringComparison.OrdinalIgnoreCase) || d.Equals("Berawan", StringComparison.OrdinalIgnoreCase))
            return "#BAE6FD"; // Bright Ice Sky Cloud
        if (code == 2 || d.Contains("Cerah Berawan", StringComparison.OrdinalIgnoreCase))
            return isNight ? "#93C5FD" : "#7DD3FC"; // Bright Sky Blue
        if (code == 1 || code == 0 || d.Contains("Cerah", StringComparison.OrdinalIgnoreCase))
            return isNight ? "#FDE047" : "#FFD43B"; // Bright Moon Yellow / Sun Gold

        return isNight ? "#FDE047" : "#FFD43B";
    }
}

public class BmkgWeatherDayGroup
{
    public DateTime Date { get; set; }
    public string DayTitle { get; set; } = string.Empty;
    public string ShortDayTitle { get; set; } = string.Empty;
    public int MinTempC { get; set; }
    public int MaxTempC { get; set; }
    public string DominantWeatherEmoji { get; set; } = "🌤";
    public string DominantWeatherIconGeometry { get; set; } = BmkgWeatherForecastItem.PartlyCloudyGeometry;
    public string DominantWeatherIconColor { get; set; } = "#FFD43B";
    public string DominantWeatherDesc { get; set; } = "Cerah Berawan";
    public List<BmkgWeatherForecastItem> Items { get; set; } = new();

    public string TempRangeFormatted => $"{MinTempC}° – {MaxTempC}°C";
}

public class BmkgWeatherData
{
    public string Adm4Code { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public string DistrictName { get; set; } = string.Empty;
    public string VillageName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string TimezoneName { get; set; } = "Asia/Jakarta";
    public DateTime LastUpdated { get; set; } = DateTime.Now;
    public BmkgWeatherForecastItem? CurrentWeather { get; set; }
    public int TodayMinTempC { get; set; }
    public int TodayMaxTempC { get; set; }
    public List<BmkgWeatherForecastItem> Forecasts { get; set; } = new();
    public List<BmkgWeatherDayGroup> DayGroups { get; set; } = new();

    public double TimezoneOffsetHours
    {
        get
        {
            if (TimezoneName.Contains("Jayapura", StringComparison.OrdinalIgnoreCase)) return 9.0;
            if (TimezoneName.Contains("Makassar", StringComparison.OrdinalIgnoreCase) ||
                TimezoneName.Contains("Pontianak", StringComparison.OrdinalIgnoreCase) == false && Longitude >= 114.5 && Longitude < 130.0) return 8.0;
            return 7.0;
        }
    }

    public string ShortCityLabel
    {
        get
        {
            string cleanCity = string.IsNullOrWhiteSpace(CityName) ? "Jakarta" : CityName
                .Replace("Kota Administrasi ", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Kabupaten Administrasi ", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Kota Adm. ", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Kab. Adm. ", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Kota ", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Kabupaten ", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Kab. ", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            if (!string.IsNullOrWhiteSpace(VillageName) &&
                !VillageName.Equals(cleanCity, StringComparison.OrdinalIgnoreCase))
            {
                return $"{VillageName}, {cleanCity}";
            }

            if (!string.IsNullOrWhiteSpace(DistrictName) &&
                !DistrictName.Equals(cleanCity, StringComparison.OrdinalIgnoreCase))
            {
                return $"{DistrictName}, {cleanCity}";
            }

            return cleanCity;
        }
    }

    public string CompactCityLabel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(VillageName))
                return VillageName;
            if (!string.IsNullOrWhiteSpace(DistrictName))
                return DistrictName;
            return ShortCityLabel;
        }
    }

    public string FullLocationSubtitle
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(VillageName)) parts.Add($"Kel./Desa {VillageName}");
            if (!string.IsNullOrWhiteSpace(DistrictName)) parts.Add($"Kec. {DistrictName}");
            if (!string.IsNullOrWhiteSpace(CityName)) parts.Add(CityName);
            if (!string.IsNullOrWhiteSpace(Province)) parts.Add(Province);
            return parts.Count > 0 ? string.Join(" • ", parts) : "Indonesia";
        }
    }

    public string TodayTempRangeFormatted => $"{TodayMinTempC}° – {TodayMaxTempC}°C";
    public string LastUpdatedFormatted => LastUpdated.ToString("HH:mm");
}

public class BmkgEarthquakeItem
{
    public string Id { get; set; } = string.Empty;
    public string Tanggal { get; set; } = string.Empty;
    public string Jam { get; set; } = string.Empty;
    public DateTimeOffset? EventDateTime { get; set; }
    public string Coordinates { get; set; } = string.Empty;
    public string Lintang { get; set; } = string.Empty;
    public string Bujur { get; set; } = string.Empty;
    public string Magnitude { get; set; } = "0.0";
    public double MagnitudeValue { get; set; }
    public string Kedalaman { get; set; } = string.Empty;
    public string Wilayah { get; set; } = string.Empty;
    public string Potensi { get; set; } = string.Empty;
    public string Dirasakan { get; set; } = string.Empty;
    public string ShakemapFileName { get; set; } = string.Empty;
    public string Category { get; set; } = "Terkini"; // "AutoGempa", "Terkini", "Dirasakan"

    public bool HasDirasakan => !string.IsNullOrWhiteSpace(Dirasakan) && Dirasakan.Trim() != "-";
    public bool HasPotensi => !string.IsNullOrWhiteSpace(Potensi) && Potensi.Trim() != "-";
    public bool HasShakemap => !string.IsNullOrWhiteSpace(ShakemapFileName);

    public string ShakemapUrl => HasShakemap
        ? $"https://data.bmkg.go.id/DataMKG/TEWS/{ShakemapFileName.Trim()}"
        : string.Empty;

    public string GoogleMapsUrl => !string.IsNullOrWhiteSpace(Coordinates)
        ? $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(Coordinates.Trim())}"
        : "https://inatews.bmkg.go.id";

    public string MagnitudeFormatted => $"M {Magnitude}";
    public string DateTimeSummary => $"{Tanggal} • {Jam}";
    public string CoordinateSummary => $"{Lintang}, {Bujur}";

    public string TimeAgoLabel
    {
        get
        {
            if (!EventDateTime.HasValue) return Jam;
            var diff = DateTimeOffset.UtcNow - EventDateTime.Value.ToUniversalTime();
            if (diff.TotalMinutes < 1) return "Baru saja";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} mnt lalu";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} jam {(int)(diff.TotalMinutes % 60)} mnt lalu";
            return $"{(int)diff.TotalDays} hari lalu";
        }
    }

    public bool IsPotentialTsunami =>
        !string.IsNullOrWhiteSpace(Potensi) &&
        Potensi.Contains("tsunami", StringComparison.OrdinalIgnoreCase) &&
        !Potensi.Contains("Tidak berpotensi", StringComparison.OrdinalIgnoreCase);

    public string MagnitudeBadgeBackground
    {
        get
        {
            if (MagnitudeValue >= 6.0) return "#3B1219";
            if (MagnitudeValue >= 5.0) return "#362210";
            return "#142B1E";
        }
    }

    public string MagnitudeBadgeBorder
    {
        get
        {
            if (MagnitudeValue >= 6.0) return "#FF4D6D";
            if (MagnitudeValue >= 5.0) return "#FF9F1C";
            return "#30D158";
        }
    }

    public string MagnitudeBadgeForeground
    {
        get
        {
            if (MagnitudeValue >= 6.0) return "#FF758F";
            if (MagnitudeValue >= 5.0) return "#FFB74D";
            return "#4CD964";
        }
    }

    public string PotensiOrDirasakanSummary
    {
        get
        {
            if (HasDirasakan && HasPotensi)
                return $"{Potensi} • Dirasakan (MMI): {Dirasakan}";
            if (HasDirasakan)
                return $"Dirasakan (MMI): {Dirasakan}";
            if (HasPotensi)
                return Potensi;
            return "Data Gempabumi BMKG";
        }
    }
}

public class BmkgNotificationEvent
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string MagnitudeBadge { get; set; } = string.Empty;
    public string Wilayah { get; set; } = string.Empty;
    public string Jam { get; set; } = string.Empty;
    public string Kedalaman { get; set; } = string.Empty;
    public string Potensi { get; set; } = string.Empty;
    public string ShakemapUrl { get; set; } = string.Empty;
    public bool IsEarthquake { get; set; } = true;
    public bool IsHighSeverity { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
