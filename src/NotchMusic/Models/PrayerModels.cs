using System;
using System.Collections.Generic;

namespace NotchMusic.Models;

public enum PrayerReminderType
{
    Before,
    AtPrayerTime,
    After
}

public class CityLocation
{
    public string Name { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Timezone { get; set; } = 7.0;

    public string DisplayName => $"{Name} ({Province})";

    public override string ToString() => DisplayName;

    public static List<CityLocation> GetIndonesianCities()
    {
        return new List<CityLocation>
        {
            new() { Name = "Jakarta", Province = "DKI Jakarta", Latitude = -6.2088, Longitude = 106.8456, Timezone = 7.0 },
            new() { Name = "Surabaya", Province = "Jawa Timur", Latitude = -7.2575, Longitude = 112.7521, Timezone = 7.0 },
            new() { Name = "Bandung", Province = "Jawa Barat", Latitude = -6.9175, Longitude = 107.6191, Timezone = 7.0 },
            new() { Name = "Medan", Province = "Sumatera Utara", Latitude = 3.5952, Longitude = 98.6722, Timezone = 7.0 },
            new() { Name = "Semarang", Province = "Jawa Tengah", Latitude = -6.9667, Longitude = 110.4167, Timezone = 7.0 },
            new() { Name = "Makassar", Province = "Sulawesi Selatan", Latitude = -5.1477, Longitude = 119.4327, Timezone = 8.0 },
            new() { Name = "Palembang", Province = "Sumatera Selatan", Latitude = -2.9909, Longitude = 104.7565, Timezone = 7.0 },
            new() { Name = "Tangerang", Province = "Banten", Latitude = -6.1783, Longitude = 106.6319, Timezone = 7.0 },
            new() { Name = "Depok", Province = "Jawa Barat", Latitude = -6.4025, Longitude = 106.7942, Timezone = 7.0 },
            new() { Name = "Bekasi", Province = "Jawa Barat", Latitude = -6.2383, Longitude = 106.9756, Timezone = 7.0 },
            new() { Name = "Bogor", Province = "Jawa Barat", Latitude = -6.5971, Longitude = 106.8060, Timezone = 7.0 },
            new() { Name = "Yogyakarta", Province = "DI Yogyakarta", Latitude = -7.7956, Longitude = 110.3695, Timezone = 7.0 },
            new() { Name = "Surakarta (Solo)", Province = "Jawa Tengah", Latitude = -7.5755, Longitude = 110.8243, Timezone = 7.0 },
            new() { Name = "Malang", Province = "Jawa Timur", Latitude = -7.9666, Longitude = 112.6326, Timezone = 7.0 },
            new() { Name = "Cirebon", Province = "Jawa Barat", Latitude = -6.7320, Longitude = 108.5523, Timezone = 7.0 },
            new() { Name = "Serang", Province = "Banten", Latitude = -6.1104, Longitude = 106.1640, Timezone = 7.0 },
            new() { Name = "Denpasar", Province = "Bali", Latitude = -8.6705, Longitude = 115.2126, Timezone = 8.0 },
            new() { Name = "Mataram (Lombok)", Province = "NTB", Latitude = -8.5833, Longitude = 116.1167, Timezone = 8.0 },
            new() { Name = "Kupang", Province = "NTT", Latitude = -10.1772, Longitude = 123.6070, Timezone = 8.0 },
            new() { Name = "Balikpapan", Province = "Kalimantan Timur", Latitude = -1.2379, Longitude = 116.8289, Timezone = 8.0 },
            new() { Name = "Samarinda", Province = "Kalimantan Timur", Latitude = -0.5022, Longitude = 117.1536, Timezone = 8.0 },
            new() { Name = "Banjarmasin", Province = "Kalimantan Selatan", Latitude = -3.3167, Longitude = 114.5900, Timezone = 8.0 },
            new() { Name = "Pontianak", Province = "Kalimantan Barat", Latitude = -0.0263, Longitude = 109.3425, Timezone = 7.0 },
            new() { Name = "Palangka Raya", Province = "Kalimantan Tengah", Latitude = -2.2161, Longitude = 113.9139, Timezone = 7.0 },
            new() { Name = "Tarakan", Province = "Kalimantan Utara", Latitude = 3.3270, Longitude = 117.5925, Timezone = 8.0 },
            new() { Name = "Bandar Lampung", Province = "Lampung", Latitude = -5.4500, Longitude = 105.2667, Timezone = 7.0 },
            new() { Name = "Padang", Province = "Sumatera Barat", Latitude = -0.9471, Longitude = 100.4172, Timezone = 7.0 },
            new() { Name = "Pekanbaru", Province = "Riau", Latitude = 0.5071, Longitude = 101.4478, Timezone = 7.0 },
            new() { Name = "Batam", Province = "Kepulauan Riau", Latitude = 1.1301, Longitude = 104.0529, Timezone = 7.0 },
            new() { Name = "Jambi", Province = "Jambi", Latitude = -1.6101, Longitude = 103.6131, Timezone = 7.0 },
            new() { Name = "Bengkulu", Province = "Bengkulu", Latitude = -3.8004, Longitude = 102.2655, Timezone = 7.0 },
            new() { Name = "Pangkal Pinang", Province = "Bangka Belitung", Latitude = -2.1290, Longitude = 106.1139, Timezone = 7.0 },
            new() { Name = "Banda Aceh", Province = "Aceh", Latitude = 5.5483, Longitude = 95.3238, Timezone = 7.0 },
            new() { Name = "Manado", Province = "Sulawesi Utara", Latitude = 1.4748, Longitude = 124.8421, Timezone = 8.0 },
            new() { Name = "Palu", Province = "Sulawesi Tengah", Latitude = -0.9003, Longitude = 119.8779, Timezone = 8.0 },
            new() { Name = "Kendari", Province = "Sulawesi Tenggara", Latitude = -3.9985, Longitude = 122.5126, Timezone = 8.0 },
            new() { Name = "Gorontalo", Province = "Gorontalo", Latitude = 0.5435, Longitude = 123.0568, Timezone = 8.0 },
            new() { Name = "Mamuju", Province = "Sulawesi Barat", Latitude = -2.6770, Longitude = 118.8895, Timezone = 8.0 },
            new() { Name = "Ambon", Province = "Maluku", Latitude = -3.6954, Longitude = 128.1814, Timezone = 9.0 },
            new() { Name = "Ternate", Province = "Maluku Utara", Latitude = 0.7904, Longitude = 127.3809, Timezone = 9.0 },
            new() { Name = "Jayapura", Province = "Papua", Latitude = -2.5916, Longitude = 140.6690, Timezone = 9.0 },
            new() { Name = "Sorong", Province = "Papua Barat Daya", Latitude = -0.8762, Longitude = 131.2558, Timezone = 9.0 },
            new() { Name = "Manokwari", Province = "Papua Barat", Latitude = -0.8615, Longitude = 134.0620, Timezone = 9.0 }
        };
    }
}

public class PrayerTimeItem
{
    public string Name { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public TimeSpan Time { get; set; }
    public string FormattedTime { get; set; } = string.Empty;
    public bool IsMandatoryPrayer { get; set; } = true;
    public bool IsNext { get; set; }
    public bool IsCurrent { get; set; }
    public string CountdownText { get; set; } = string.Empty;
    public string StatusBadge { get; set; } = string.Empty;
}

public class PrayerSchedule
{
    public DateTime Date { get; set; }
    public string HijriDate { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public List<PrayerTimeItem> Items { get; set; } = new();
    public PrayerTimeItem? NextPrayer { get; set; }
    public PrayerTimeItem? CurrentPrayer { get; set; }
    public TimeSpan TimeUntilNextPrayer { get; set; }
    public string CountdownSummary => NextPrayer != null ? $"{NextPrayer.Name} {NextPrayer.FormattedTime} ({FormatTimeSpan(TimeUntilNextPrayer)})" : "-";

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalMinutes < 1) return "< 1 mnt";
        if (ts.TotalHours < 1) return $"-{Math.Max(1, (int)ts.TotalMinutes)}m";
        return $"-{(int)ts.TotalHours}j {ts.Minutes}m";
    }
}

public class PrayerNotificationEvent
{
    public string PrayerName { get; set; } = string.Empty;
    public string PrayerTimeFormatted { get; set; } = string.Empty;
    public PrayerReminderType Type { get; set; }
    public int Minutes { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool AutoPauseRequested { get; set; }
}
