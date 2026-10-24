using System;
using System.Collections.Generic;
using System.Globalization;
using NotchMusic.Models;

namespace NotchMusic.Services.Prayer;

public static class PrayerTimesCalculator
{
    private static readonly string[] HijriMonthNames =
    {
        "", "Muharram", "Safar", "Rabiul Awwal", "Rabiul Akhir",
        "Jumadil Awwal", "Jumadil Akhir", "Rajab", "Sya'ban",
        "Ramadhan", "Syawwal", "Dzulqa'dah", "Dzulhijjah"
    };

    public static string GetHijriDateFormatted(DateTime date)
    {
        try
        {
            var cal = new UmAlQuraCalendar();
            int year = cal.GetYear(date);
            int month = cal.GetMonth(date);
            int day = cal.GetDayOfMonth(date);

            string monthName = (month >= 1 && month <= 12) ? HijriMonthNames[month] : $"Bulan {month}";
            return $"{day} {monthName} {year} H";
        }
        catch
        {
            return string.Empty;
        }
    }

    public static PrayerSchedule CalculateSchedule(DateTime date, double latitude, double longitude, double timezone, string cityName = "Jakarta")
    {
        int y = date.Year, m = date.Month, d = date.Day;
        if (m <= 2)
        {
            y -= 1;
            m += 12;
        }

        double a = Math.Floor(y / 100.0);
        double b = 2 - a + Math.Floor(a / 4.0);
        double jd = Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + d + b - 1524.5;

        double D = jd - 2451545.0;
        double g = NormalizeAngle(357.529 + 0.98560028 * D);
        double q = NormalizeAngle(280.459 + 0.98564736 * D);

        double gRad = DegreesToRadians(g);
        double L = NormalizeAngle(q + 1.915 * Math.Sin(gRad) + 0.020 * Math.Sin(2 * gRad));

        double e = 23.439 - 0.00000036 * D;
        double eRad = DegreesToRadians(e);
        double LRad = DegreesToRadians(L);

        double sinDelta = Math.Sin(eRad) * Math.Sin(LRad);
        double delta = Math.Asin(sinDelta); // in radians

        double alphaRad = Math.Atan2(Math.Cos(eRad) * Math.Sin(LRad), Math.Cos(LRad));
        double alphaDeg = NormalizeAngle(RadiansToDegrees(alphaRad));

        double eotHours = (q - alphaDeg) / 15.0;
        while (eotHours > 12) eotHours -= 24;
        while (eotHours < -12) eotHours += 24;

        double noon = 12.0 + timezone - (longitude / 15.0) - eotHours;
        double latRad = DegreesToRadians(latitude);

        double GetHourAngle(double altDeg)
        {
            double altRad = DegreesToRadians(altDeg);
            double cosW = (Math.Sin(altRad) - Math.Sin(latRad) * Math.Sin(delta)) / (Math.Cos(latRad) * Math.Cos(delta));
            if (cosW > 1.0 || cosW < -1.0) return double.NaN;
            return RadiansToDegrees(Math.Acos(cosW)) / 15.0;
        }

        // Subuh: 20 degrees depression (Standard Indonesia / Kemenag SIHAT)
        double wFajr = GetHourAngle(-20.0);
        // Sunrise & Sunset: Sun altitude -0.8333 degrees
        double wSunrise = GetHourAngle(-0.8333);
        double wSunset = GetHourAngle(-0.8333);
        // Isya: 18 degrees depression (Standard Indonesia / Kemenag SIHAT)
        double wIsha = GetHourAngle(-18.0);

        // Ashar (Shafii: shadow = noon shadow + 1):
        double asrAltRad = Math.Atan(1.0 / (1.0 + Math.Tan(Math.Abs(latRad - delta))));
        double cosWAsr = (Math.Sin(asrAltRad) - Math.Sin(latRad) * Math.Sin(delta)) / (Math.Cos(latRad) * Math.Cos(delta));
        double wAsr = double.IsNaN(cosWAsr) || cosWAsr < -1.0 || cosWAsr > 1.0
            ? 0
            : RadiansToDegrees(Math.Acos(cosWAsr)) / 15.0;

        // Kemenag standard safety buffer (Ihtiyat): +2 minutes (0.03333 hours)
        double ihtiyat = 2.0 / 60.0;

        double fajrH = noon - wFajr + ihtiyat;
        double imsakH = fajrH - (10.0 / 60.0);
        double sunriseH = noon - wSunrise;
        double dhuhrH = noon + ihtiyat;
        double asrH = noon + wAsr + ihtiyat;
        double maghribH = noon + wSunset + ihtiyat;
        double ishaH = noon + wIsha + ihtiyat;

        var items = new List<PrayerTimeItem>
        {
            CreateItem("Imsak", "الإمساك", imsakH, isMandatory: false),
            CreateItem("Subuh", "الفجر", fajrH, isMandatory: true),
            CreateItem("Terbit", "الشروق", sunriseH, isMandatory: false),
            CreateItem("Dzuhur", "الظهر", dhuhrH, isMandatory: true),
            CreateItem("Ashar", "العصر", asrH, isMandatory: true),
            CreateItem("Maghrib", "المغرب", maghribH, isMandatory: true),
            CreateItem("Isya", "العشاء", ishaH, isMandatory: true)
        };

        TimeSpan nowTime = date.TimeOfDay;
        PrayerTimeItem? nextPrayer = null;
        PrayerTimeItem? currentPrayer = null;
        TimeSpan timeUntilNext = TimeSpan.Zero;

        // Determine next mandatory prayer or prayer
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.Time > nowTime)
            {
                if (nextPrayer == null)
                {
                    nextPrayer = item;
                    item.IsNext = true;
                    timeUntilNext = item.Time - nowTime;
                }
            }
            else
            {
                // Has passed
                currentPrayer = item;
            }
        }

        // If all prayers today have passed, next prayer is Subuh tomorrow
        bool isNextPrayerTomorrow = false;
        if (nextPrayer == null && items.Count > 1)
        {
            isNextPrayerTomorrow = true;
            var tomorrowSubuh = CalculateTomorrowSubuhItem(date.AddDays(1), latitude, longitude, timezone);
            var subuh = items[1]; // Subuh
            subuh.Time = tomorrowSubuh.Time;
            subuh.FormattedTime = tomorrowSubuh.FormattedTime;
            subuh.IsNext = true;
            timeUntilNext = (TimeSpan.FromHours(24) - nowTime) + subuh.Time;
            nextPrayer = subuh;
        }

        if (currentPrayer != null)
        {
            currentPrayer.IsCurrent = true;
        }

        // Update countdown text and status badges
        foreach (var item in items)
        {
            if (isNextPrayerTomorrow && item == nextPrayer)
            {
                item.StatusBadge = "Segera";
                item.CountdownText = timeUntilNext.TotalMinutes < 60
                    ? $"{Math.Max(1, (int)timeUntilNext.TotalMinutes)}m lagi"
                    : $"{(int)timeUntilNext.TotalHours}j {timeUntilNext.Minutes}m lagi";
            }
            else if (item.Time <= nowTime)
            {
                item.StatusBadge = "Selesai";
                var elapsed = nowTime - item.Time;
                item.CountdownText = elapsed.TotalMinutes < 60 
                    ? $"{Math.Max(1, (int)elapsed.TotalMinutes)}m lalu" 
                    : $"{(int)elapsed.TotalHours}j lalu";
            }
            else
            {
                var remaining = item.Time - nowTime;
                item.StatusBadge = item == nextPrayer ? "Segera" : "Mendatang";
                item.CountdownText = remaining.TotalMinutes < 60
                    ? $"{Math.Max(1, (int)remaining.TotalMinutes)}m lagi"
                    : $"{(int)remaining.TotalHours}j {remaining.Minutes}m lagi";
            }
        }

        return new PrayerSchedule
        {
            Date = date,
            HijriDate = GetHijriDateFormatted(date),
            CityName = cityName,
            Items = items,
            NextPrayer = nextPrayer,
            CurrentPrayer = currentPrayer,
            TimeUntilNextPrayer = timeUntilNext
        };
    }

    private static PrayerTimeItem CalculateTomorrowSubuhItem(DateTime date, double latitude, double longitude, double timezone)
    {
        int y = date.Year, m = date.Month, d = date.Day;
        if (m <= 2) { y -= 1; m += 12; }
        double a = Math.Floor(y / 100.0);
        double b = 2 - a + Math.Floor(a / 4.0);
        double jd = Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + d + b - 1524.5;
        double D = jd - 2451545.0;
        double g = NormalizeAngle(357.529 + 0.98560028 * D);
        double q = NormalizeAngle(280.459 + 0.98564736 * D);
        double gRad = DegreesToRadians(g);
        double L = NormalizeAngle(q + 1.915 * Math.Sin(gRad) + 0.020 * Math.Sin(2 * gRad));
        double e = 23.439 - 0.00000036 * D;
        double eRad = DegreesToRadians(e);
        double LRad = DegreesToRadians(L);
        double delta = Math.Asin(Math.Sin(eRad) * Math.Sin(LRad));
        double alphaDeg = NormalizeAngle(RadiansToDegrees(Math.Atan2(Math.Cos(eRad) * Math.Sin(LRad), Math.Cos(LRad))));
        double eotHours = (q - alphaDeg) / 15.0;
        while (eotHours > 12) eotHours -= 24;
        while (eotHours < -12) eotHours += 24;
        double noon = 12.0 + timezone - (longitude / 15.0) - eotHours;
        double latRad = DegreesToRadians(latitude);
        double altRad = DegreesToRadians(-20.0);
        double cosW = (Math.Sin(altRad) - Math.Sin(latRad) * Math.Sin(delta)) / (Math.Cos(latRad) * Math.Cos(delta));
        double wFajr = (cosW > 1.0 || cosW < -1.0) ? double.NaN : RadiansToDegrees(Math.Acos(cosW)) / 15.0;
        double fajrH = noon - wFajr + (2.0 / 60.0);
        return CreateItem("Subuh", "الفجر", fajrH, isMandatory: true);
    }

    private static PrayerTimeItem CreateItem(string name, string arabic, double hours, bool isMandatory)
    {
        while (hours < 0) hours += 24;
        while (hours >= 24) hours -= 24;

        int h = (int)Math.Floor(hours);
        int m = (int)Math.Round((hours - h) * 60);
        if (m >= 60)
        {
            h = (h + 1) % 24;
            m = 0;
        }

        var ts = new TimeSpan(h, m, 0);
        return new PrayerTimeItem
        {
            Name = name,
            ArabicName = arabic,
            Time = ts,
            FormattedTime = $"{h:D2}:{m:D2}",
            IsMandatoryPrayer = isMandatory
        };
    }

    private static double NormalizeAngle(double angle)
    {
        angle %= 360.0;
        if (angle < 0) angle += 360.0;
        return angle;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
    private static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;
}
