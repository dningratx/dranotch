using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using NotchMusic.Models;
using NotchMusic.Services.Settings;

namespace NotchMusic.Services.Bmkg;

public class BmkgService : IBmkgService
{
    private readonly ISettingsService _settingsService;
    private static readonly HttpClient _httpClient;

    private static readonly List<BmkgRegionItem> _provinces = new()
    {
        new() { Code = "11", Name = "Aceh" },
        new() { Code = "12", Name = "Sumatera Utara" },
        new() { Code = "13", Name = "Sumatera Barat" },
        new() { Code = "14", Name = "Riau" },
        new() { Code = "15", Name = "Jambi" },
        new() { Code = "16", Name = "Sumatera Selatan" },
        new() { Code = "17", Name = "Bengkulu" },
        new() { Code = "18", Name = "Lampung" },
        new() { Code = "19", Name = "Kepulauan Bangka Belitung" },
        new() { Code = "21", Name = "Kepulauan Riau" },
        new() { Code = "31", Name = "DKI Jakarta" },
        new() { Code = "32", Name = "Jawa Barat" },
        new() { Code = "33", Name = "Jawa Tengah" },
        new() { Code = "34", Name = "Daerah Istimewa Yogyakarta" },
        new() { Code = "35", Name = "Jawa Timur" },
        new() { Code = "36", Name = "Banten" },
        new() { Code = "51", Name = "Bali" },
        new() { Code = "52", Name = "Nusa Tenggara Barat" },
        new() { Code = "53", Name = "Nusa Tenggara Timur" },
        new() { Code = "61", Name = "Kalimantan Barat" },
        new() { Code = "62", Name = "Kalimantan Tengah" },
        new() { Code = "63", Name = "Kalimantan Selatan" },
        new() { Code = "64", Name = "Kalimantan Timur" },
        new() { Code = "65", Name = "Kalimantan Utara" },
        new() { Code = "71", Name = "Sulawesi Utara" },
        new() { Code = "72", Name = "Sulawesi Tengah" },
        new() { Code = "73", Name = "Sulawesi Selatan" },
        new() { Code = "74", Name = "Sulawesi Tenggara" },
        new() { Code = "75", Name = "Gorontalo" },
        new() { Code = "76", Name = "Sulawesi Barat" },
        new() { Code = "81", Name = "Maluku" },
        new() { Code = "82", Name = "Maluku Utara" },
        new() { Code = "91", Name = "Papua" },
        new() { Code = "92", Name = "Papua Barat" },
        new() { Code = "93", Name = "Papua Selatan" },
        new() { Code = "94", Name = "Papua Tengah" },
        new() { Code = "95", Name = "Papua Pegunungan" },
        new() { Code = "96", Name = "Papua Barat Daya" }
    };

    private static readonly ConcurrentDictionary<string, List<BmkgRegionItem>> _regenciesCache = new();
    private static readonly ConcurrentDictionary<string, List<BmkgRegionItem>> _districtsCache = new();
    private static readonly ConcurrentDictionary<string, List<BmkgRegionItem>> _villagesCache = new();

    private readonly DispatcherTimer _earthquakePollTimer;
    private readonly DispatcherTimer _weatherPollTimer;

    private BmkgWeatherData? _currentWeatherData;
    private BmkgEarthquakeItem? _latestEarthquake;
    private List<BmkgEarthquakeItem> _recentEarthquakes = new();
    private List<BmkgEarthquakeItem> _feltEarthquakes = new();
    private bool _isLoadingWeather;
    private bool _isLoadingEarthquake;
    private string _statusMessage = "Siap memuat data BMKG...";
    private DateTime _lastWeatherFetchTime = DateTime.MinValue;
    private string _lastFetchedAdm4 = string.Empty;
    private bool _isDisposed;

    public BmkgWeatherData? CurrentWeatherData => _currentWeatherData;
    public BmkgEarthquakeItem? LatestEarthquake => _latestEarthquake;
    public IReadOnlyList<BmkgEarthquakeItem> RecentEarthquakes => _recentEarthquakes;
    public IReadOnlyList<BmkgEarthquakeItem> FeltEarthquakes => _feltEarthquakes;
    public bool IsLoadingWeather => _isLoadingWeather;
    public bool IsLoadingEarthquake => _isLoadingEarthquake;
    public string StatusMessage => _statusMessage;

    public event EventHandler? DataUpdated;
    public event EventHandler<BmkgNotificationEvent>? NotificationTriggered;

    static BmkgService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DRANOTCH/1.8 (Windows; BMKG Open Data Integration)");
    }

    public BmkgService(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        // Poll gempa terbaru setiap 3 menit (hemat kuota & di bawah rate limit 60 req/menit BMKG)
        _earthquakePollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(3)
        };
        _earthquakePollTimer.Tick += async (s, e) =>
        {
            if (_settingsService.CurrentSettings.EnableBmkg)
            {
                await RefreshEarthquakesAsync();
            }
        };

        // Poll prakiraan cuaca setiap 20 menit
        _weatherPollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(20)
        };
        _weatherPollTimer.Tick += async (s, e) =>
        {
            if (_settingsService.CurrentSettings.EnableBmkg)
            {
                await RefreshWeatherAsync();
            }
        };
    }

    public void Start()
    {
        _earthquakePollTimer.Start();
        _weatherPollTimer.Start();
        _ = RefreshAllAsync(forceWeather: true);
    }

    public void Stop()
    {
        _earthquakePollTimer.Stop();
        _weatherPollTimer.Stop();
    }

    public IReadOnlyList<BmkgRegionItem> GetProvinces() => _provinces;

    public async Task<IReadOnlyList<BmkgRegionItem>> GetRegenciesAsync(string provinceCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(provinceCode)) return Array.Empty<BmkgRegionItem>();
        provinceCode = provinceCode.Trim();

        if (_regenciesCache.TryGetValue(provinceCode, out var cached) && cached.Count > 0)
            return cached;

        var list = await FetchWilayahListAsync($"https://wilayah.id/api/regencies/{Uri.EscapeDataString(provinceCode)}.json", cancellationToken);
        if (list.Count > 0)
        {
            _regenciesCache[provinceCode] = list;
        }
        return list;
    }

    public async Task<IReadOnlyList<BmkgRegionItem>> GetDistrictsAsync(string regencyCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(regencyCode)) return Array.Empty<BmkgRegionItem>();
        regencyCode = regencyCode.Trim();

        if (_districtsCache.TryGetValue(regencyCode, out var cached) && cached.Count > 0)
            return cached;

        var list = await FetchWilayahListAsync($"https://wilayah.id/api/districts/{Uri.EscapeDataString(regencyCode)}.json", cancellationToken);
        if (list.Count > 0)
        {
            _districtsCache[regencyCode] = list;
        }
        return list;
    }

    public async Task<IReadOnlyList<BmkgRegionItem>> GetVillagesAsync(string districtCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(districtCode)) return Array.Empty<BmkgRegionItem>();
        districtCode = districtCode.Trim();

        if (_villagesCache.TryGetValue(districtCode, out var cached) && cached.Count > 0)
            return cached;

        var list = await FetchWilayahListAsync($"https://wilayah.id/api/villages/{Uri.EscapeDataString(districtCode)}.json", cancellationToken);
        if (list.Count > 0)
        {
            _villagesCache[districtCode] = list;
        }
        return list;
    }

    private static async Task<List<BmkgRegionItem>> FetchWilayahListAsync(string url, CancellationToken cancellationToken)
    {
        var result = new List<BmkgRegionItem>();
        try
        {
            var json = await _httpClient.GetStringAsync(url, cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dataArr.EnumerateArray())
                {
                    var code = GetStr(item, "code", "");
                    var name = GetStr(item, "name", "");
                    if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name))
                    {
                        result.Add(new BmkgRegionItem { Code = code, Name = name });
                    }
                }
            }
        }
        catch { }
        return result;
    }

    public async Task<IReadOnlyList<BmkgLocationSearchResult>> SearchPreciseLocationsAsync(string query, CancellationToken cancellationToken = default)
    {
        var results = new List<BmkgLocationSearchResult>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        var q = query.Trim();

        try
        {
            var url = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(q)}&countrycodes=id&addressdetails=1&limit=6";
            var json = await _httpClient.GetStringAsync(url, cancellationToken);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var seenAdm4 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    double lat = GetDouble(item, "lat", -6.1754);
                    double lon = GetDouble(item, "lon", 106.8272);
                    string placeName = GetStr(item, "name", "");

                    JsonElement addrEl = default;
                    bool hasAddr = item.TryGetProperty("address", out addrEl) && addrEl.ValueKind == JsonValueKind.Object;

                    var resolved = await ResolveNominatimAddressToAdm4Async(hasAddr ? addrEl : null, placeName, q, lat, lon, cancellationToken);
                    if (resolved != null && !string.IsNullOrWhiteSpace(resolved.Adm4Code) && seenAdm4.Add(resolved.Adm4Code))
                    {
                        results.Add(resolved);
                    }
                }
            }
        }
        catch { }

        // Fallback jika Nominatim tidak mengembalikan hasil: cari langsung dari daftar kota Indonesia
        if (results.Count == 0)
        {
            var matchedCities = CityLocation.GetIndonesianCities()
                .Where(c => c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            c.Province.Contains(q, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .ToList();

            foreach (var c in matchedCities)
            {
                var parts = c.BmkgAdm4Code.Split('.');
                results.Add(new BmkgLocationSearchResult
                {
                    Adm4Code = c.BmkgAdm4Code,
                    ProvinceCode = parts.Length >= 1 ? parts[0] : "31",
                    ProvinceName = c.Province,
                    RegencyCode = parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : "31.71",
                    RegencyName = c.Name,
                    DistrictCode = parts.Length >= 3 ? $"{parts[0]}.{parts[1]}.{parts[2]}" : "31.71.01",
                    DistrictName = c.Name,
                    VillageName = c.Name,
                    Latitude = c.Latitude,
                    Longitude = c.Longitude,
                    Timezone = c.Timezone
                });
            }
        }

        return results;
    }

    public async Task<BmkgLocationSearchResult?> ResolveCoordinatesToPreciseAdm4Async(
        double latitude,
        double longitude,
        string? fallbackCityName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={latitude.ToString(CultureInfo.InvariantCulture)}&lon={longitude.ToString(CultureInfo.InvariantCulture)}&zoom=18&addressdetails=1";
            var json = await _httpClient.GetStringAsync(url, cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            JsonElement? addrEl = null;
            if (root.TryGetProperty("address", out var a) && a.ValueKind == JsonValueKind.Object)
            {
                addrEl = a;
            }

            string placeName = GetStr(root, "name", fallbackCityName ?? "");
            var resolved = await ResolveNominatimAddressToAdm4Async(addrEl, placeName, fallbackCityName ?? "", latitude, longitude, cancellationToken);
            if (resolved != null)
            {
                return resolved;
            }
        }
        catch { }

        var nearest = CityLocation.FindBestMatchOrNearest(fallbackCityName, latitude, longitude);
        var parts = nearest.BmkgAdm4Code.Split('.');
        return new BmkgLocationSearchResult
        {
            Adm4Code = nearest.BmkgAdm4Code,
            ProvinceCode = parts.Length >= 1 ? parts[0] : "31",
            ProvinceName = nearest.Province,
            RegencyCode = parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : "31.71",
            RegencyName = nearest.Name,
            DistrictCode = parts.Length >= 3 ? $"{parts[0]}.{parts[1]}.{parts[2]}" : "31.71.01",
            DistrictName = nearest.Name,
            VillageName = nearest.Name,
            Latitude = latitude != 0 ? latitude : nearest.Latitude,
            Longitude = longitude != 0 ? longitude : nearest.Longitude,
            Timezone = nearest.Timezone
        };
    }

    private async Task<BmkgLocationSearchResult?> ResolveNominatimAddressToAdm4Async(
        JsonElement? addrEl,
        string placeName,
        string rawQuery,
        double lat,
        double lon,
        CancellationToken cancellationToken)
    {
        string state = "";
        string isoLvl4 = "";
        string city = "";
        string cityDistrict = "";
        string county = "";
        string municipality = "";
        string district = "";
        string suburb = "";
        string town = "";
        string village = "";
        string neighbourhood = "";

        if (addrEl.HasValue)
        {
            var a = addrEl.Value;
            state = GetStr(a, "state", "");
            isoLvl4 = GetStr(a, "ISO3166-2-lvl4", "");
            city = GetStr(a, "city", "");
            cityDistrict = GetStr(a, "city_district", "");
            county = GetStr(a, "county", "");
            municipality = GetStr(a, "municipality", "");
            district = GetStr(a, "district", "");
            suburb = GetStr(a, "suburb", "");
            town = GetStr(a, "town", "");
            village = GetStr(a, "village", "");
            neighbourhood = GetStr(a, "neighbourhood", "");
        }

        // 1. Tentukan Provinsi
        BmkgRegionItem? matchedProv = MatchProvince(state, city, isoLvl4);
        if (matchedProv == null)
        {
            var nearestCity = CityLocation.FindBestMatchOrNearest(city, lat, lon);
            var provCode = nearestCity.BmkgAdm4Code.Split('.')[0];
            matchedProv = _provinces.FirstOrDefault(p => p.Code == provCode) ?? _provinces.First(p => p.Code == "31");
        }

        // 2. Tentukan Kabupaten / Kota
        var regencies = await GetRegenciesAsync(matchedProv.Code, cancellationToken);
        if (regencies.Count == 0) return null;

        var regencyCandidates = new[] { cityDistrict, city, county, town, municipality, placeName, rawQuery }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        BmkgRegionItem? matchedRegency = MatchRegionByName(regencies, regencyCandidates, stripCityPrefix: true);
        if (matchedRegency == null)
        {
            var nearestCity = CityLocation.FindBestMatchOrNearest(city, lat, lon);
            matchedRegency = MatchRegionByName(regencies, new[] { nearestCity.Name }, stripCityPrefix: true) ?? regencies[0];
        }

        // 3. Tentukan Kecamatan
        var districts = await GetDistrictsAsync(matchedRegency.Code, cancellationToken);
        if (districts.Count == 0) return null;

        var districtCandidates = new[] { district, suburb, municipality, town, cityDistrict, placeName, village, rawQuery }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        BmkgRegionItem? matchedDistrict = MatchRegionByName(districts, districtCandidates, stripCityPrefix: false);

        // Jika belum ketemu kecamatannya namun ada nama kelurahan/desa (misal user ketik langsung nama kelurahan),
        // cek di beberapa kecamatan atau gunakan kecamatan pertama yang cocok
        BmkgRegionItem? matchedVillage = null;
        var villageCandidates = new[] { village, placeName, suburb, neighbourhood, rawQuery }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (matchedDistrict == null && villageCandidates.Count > 0 && districts.Count <= 35)
        {
            foreach (var d in districts)
            {
                var vList = await GetVillagesAsync(d.Code, cancellationToken);
                var vMatch = MatchRegionByName(vList, villageCandidates, stripCityPrefix: false);
                if (vMatch != null)
                {
                    matchedDistrict = d;
                    matchedVillage = vMatch;
                    break;
                }
            }
        }

        matchedDistrict ??= districts[0];

        // 4. Tentukan Kelurahan / Desa
        var villages = await GetVillagesAsync(matchedDistrict.Code, cancellationToken);
        if (villages.Count == 0) return null;

        matchedVillage ??= MatchRegionByName(villages, villageCandidates, stripCityPrefix: false)
                           ?? MatchRegionByName(villages, new[] { matchedDistrict.Name }, stripCityPrefix: false)
                           ?? villages[0];

        double tz = lon >= 130.0 ? 9.0 : (lon >= 114.5 ? 8.0 : 7.0);

        return new BmkgLocationSearchResult
        {
            Adm4Code = matchedVillage.Code,
            ProvinceCode = matchedProv.Code,
            ProvinceName = matchedProv.Name,
            RegencyCode = matchedRegency.Code,
            RegencyName = matchedRegency.Name,
            DistrictCode = matchedDistrict.Code,
            DistrictName = matchedDistrict.Name,
            VillageName = matchedVillage.Name,
            Latitude = lat,
            Longitude = lon,
            Timezone = tz
        };
    }

    private static BmkgRegionItem? MatchProvince(string state, string city, string isoLvl4)
    {
        if (!string.IsNullOrWhiteSpace(isoLvl4))
        {
            var iso = isoLvl4.Trim().ToUpperInvariant();
            string? code = iso switch
            {
                "ID-AC" => "11",
                "ID-SU" => "12",
                "ID-SB" => "13",
                "ID-RI" => "14",
                "ID-JA" => "15",
                "ID-SS" => "16",
                "ID-BE" => "17",
                "ID-LA" => "18",
                "ID-BB" => "19",
                "ID-KR" => "21",
                "ID-JK" => "31",
                "ID-JB" => "32",
                "ID-JT" => "33",
                "ID-YO" => "34",
                "ID-JI" => "35",
                "ID-BT" => "36",
                "ID-BA" => "51",
                "ID-NB" => "52",
                "ID-NT" => "53",
                "ID-KB" => "61",
                "ID-KT" => "62",
                "ID-KS" => "63",
                "ID-KI" => "64",
                "ID-KU" => "65",
                "ID-SA" => "71",
                "ID-ST" => "72",
                "ID-SN" => "73",
                "ID-SG" => "74",
                "ID-GO" => "75",
                "ID-SR" => "76",
                "ID-MA" => "81",
                "ID-MU" => "82",
                "ID-PA" => "91",
                "ID-PB" => "92",
                "ID-PS" => "93",
                "ID-PT" => "94",
                "ID-PP" => "95",
                "ID-PD" => "96",
                _ => null
            };
            if (code != null)
            {
                return _provinces.FirstOrDefault(p => p.Code == code);
            }
        }

        foreach (var candidate in new[] { state, city })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var norm = NormalizeRegionName(candidate, stripCityPrefix: false);
            if (norm.Contains("jakarta", StringComparison.OrdinalIgnoreCase))
                return _provinces.First(p => p.Code == "31");
            if (norm.Contains("yogyakarta", StringComparison.OrdinalIgnoreCase) || norm.Contains("jogja", StringComparison.OrdinalIgnoreCase))
                return _provinces.First(p => p.Code == "34");

            var exact = _provinces.FirstOrDefault(p => NormalizeRegionName(p.Name, false).Equals(norm, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            var partial = _provinces.FirstOrDefault(p =>
                norm.Contains(NormalizeRegionName(p.Name, false), StringComparison.OrdinalIgnoreCase) ||
                NormalizeRegionName(p.Name, false).Contains(norm, StringComparison.OrdinalIgnoreCase));
            if (partial != null) return partial;
        }

        return null;
    }

    private static BmkgRegionItem? MatchRegionByName(IReadOnlyList<BmkgRegionItem> items, IEnumerable<string> candidates, bool stripCityPrefix)
    {
        foreach (var raw in candidates)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var normCandidate = NormalizeRegionName(raw, stripCityPrefix);
            if (string.IsNullOrWhiteSpace(normCandidate)) continue;

            // 1. Exact match after normalization
            var exact = items.FirstOrDefault(i =>
                NormalizeRegionName(i.Name, stripCityPrefix).Equals(normCandidate, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            // 2. If candidate indicates Kota vs Kabupaten specifically
            if (stripCityPrefix && (raw.Contains("Kota ", StringComparison.OrdinalIgnoreCase) || raw.Contains("City", StringComparison.OrdinalIgnoreCase)))
            {
                var kotaMatch = items.FirstOrDefault(i =>
                    i.Name.StartsWith("Kota", StringComparison.OrdinalIgnoreCase) &&
                    NormalizeRegionName(i.Name, true).Contains(normCandidate, StringComparison.OrdinalIgnoreCase));
                if (kotaMatch != null) return kotaMatch;
            }

            // 3. Contains match
            var contains = items.FirstOrDefault(i =>
            {
                var normItem = NormalizeRegionName(i.Name, stripCityPrefix);
                return normItem.Length >= 3 &&
                       (normCandidate.Contains(normItem, StringComparison.OrdinalIgnoreCase) ||
                        normItem.Contains(normCandidate, StringComparison.OrdinalIgnoreCase));
            });
            if (contains != null) return contains;
        }

        return null;
    }

    private static string NormalizeRegionName(string input, bool stripCityPrefix)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var s = input.Trim();
        s = s.Replace("Daerah Khusus Ibukota", "", StringComparison.OrdinalIgnoreCase)
             .Replace("Daerah Istimewa", "", StringComparison.OrdinalIgnoreCase)
             .Replace("Provinsi", "", StringComparison.OrdinalIgnoreCase)
             .Replace("Kecamatan", "", StringComparison.OrdinalIgnoreCase)
             .Replace("Kec.", "", StringComparison.OrdinalIgnoreCase)
             .Replace("Kelurahan", "", StringComparison.OrdinalIgnoreCase)
             .Replace("Kel.", "", StringComparison.OrdinalIgnoreCase)
             .Replace("Desa", "", StringComparison.OrdinalIgnoreCase);

        if (stripCityPrefix)
        {
            s = s.Replace("Kota Administrasi", "", StringComparison.OrdinalIgnoreCase)
                 .Replace("Kabupaten Administrasi", "", StringComparison.OrdinalIgnoreCase)
                 .Replace("Kota Adm.", "", StringComparison.OrdinalIgnoreCase)
                 .Replace("Kab. Adm.", "", StringComparison.OrdinalIgnoreCase)
                 .Replace("Kabupaten", "", StringComparison.OrdinalIgnoreCase)
                 .Replace("Kab.", "", StringComparison.OrdinalIgnoreCase)
                 .Replace("Kota", "", StringComparison.OrdinalIgnoreCase)
                 .Replace("City", "", StringComparison.OrdinalIgnoreCase)
                 .Replace("Regency", "", StringComparison.OrdinalIgnoreCase);
        }

        return s.Trim(' ', ',', '.', '-');
    }

    public string GetActiveCityName()
    {
        var s = _settingsService.CurrentSettings;
        if (s.BmkgFollowPrayerCity)
        {
            return string.IsNullOrWhiteSpace(s.PrayerCity) ? "Jakarta" : s.PrayerCity;
        }
        return string.IsNullOrWhiteSpace(s.BmkgSelectedCity) ? "Jakarta" : s.BmkgSelectedCity;
    }

    public string GetActiveAdm4Code()
    {
        var s = _settingsService.CurrentSettings;
        if (!string.IsNullOrWhiteSpace(s.BmkgCustomAdm4Code))
        {
            return s.BmkgCustomAdm4Code.Trim();
        }

        string targetCity = GetActiveCityName();
        var matched = CityLocation.FindBestMatchOrNearest(targetCity, s.PrayerLatitude, s.PrayerLongitude);
        return matched.BmkgAdm4Code;
    }

    public async Task RefreshAllAsync(bool forceWeather = false, CancellationToken cancellationToken = default)
    {
        if (!_settingsService.CurrentSettings.EnableBmkg)
            return;

        var activeAdm4 = GetActiveAdm4Code();
        bool shouldRefreshWeather = forceWeather ||
                                    _currentWeatherData == null ||
                                    _lastFetchedAdm4 != activeAdm4 ||
                                    (DateTime.Now - _lastWeatherFetchTime).TotalMinutes >= 15;

        var tasks = new List<Task>();
        if (shouldRefreshWeather)
        {
            tasks.Add(RefreshWeatherAsync(cancellationToken));
        }
        tasks.Add(RefreshEarthquakesAsync(cancellationToken));

        await Task.WhenAll(tasks);
    }

    public async Task<BmkgWeatherData?> RefreshWeatherAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoadingWeather) return _currentWeatherData;

        _isLoadingWeather = true;
        _statusMessage = "Memuat prakiraan cuaca BMKG...";
        RaiseDataUpdated();

        try
        {
            var adm4 = GetActiveAdm4Code();
            var url = $"https://api.bmkg.go.id/publik/prakiraan-cuaca?adm4={Uri.EscapeDataString(adm4)}";

            string json;
            try
            {
                json = await _httpClient.GetStringAsync(url, cancellationToken);
            }
            catch
            {
                // Fallback ke kode kota default jika kode adm4 kustom tidak ditemukan
                var s = _settingsService.CurrentSettings;
                var fallbackAdm4 = CityLocation.FindBestMatchOrNearest(GetActiveCityName(), s.PrayerLatitude, s.PrayerLongitude).BmkgAdm4Code;
                if (fallbackAdm4 != adm4)
                {
                    adm4 = fallbackAdm4;
                    url = $"https://api.bmkg.go.id/publik/prakiraan-cuaca?adm4={Uri.EscapeDataString(adm4)}";
                    json = await _httpClient.GetStringAsync(url, cancellationToken);
                }
                else
                {
                    throw;
                }
            }

            var parsed = ParseWeatherJson(json, adm4);
            if (parsed != null)
            {
                _currentWeatherData = parsed;
                _lastWeatherFetchTime = DateTime.Now;
                _lastFetchedAdm4 = adm4;
                _statusMessage = $"Diperbarui {DateTime.Now:HH:mm} • Sumber: BMKG";

                CheckExtremeWeatherNotification(parsed);
            }
        }
        catch (Exception ex)
        {
            _statusMessage = $"Gagal memuat cuaca BMKG: {ex.Message}";
        }
        finally
        {
            _isLoadingWeather = false;
            RaiseDataUpdated();
        }

        return _currentWeatherData;
    }

    public async Task RefreshEarthquakesAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoadingEarthquake) return;

        _isLoadingEarthquake = true;
        RaiseDataUpdated();

        try
        {
            // 1. AutoGempa (Gempa Terbaru)
            var autoGempaTask = FetchSingleEarthquakeAsync("https://data.bmkg.go.id/DataMKG/TEWS/autogempa.json", cancellationToken);
            // 2. 15 Gempa Terkini (M >= 5.0)
            var terkiniTask = FetchEarthquakeListAsync("https://data.bmkg.go.id/DataMKG/TEWS/gempaterkini.json", "Terkini", cancellationToken);
            // 3. 15 Gempa Dirasakan
            var dirasakanTask = FetchEarthquakeListAsync("https://data.bmkg.go.id/DataMKG/TEWS/gempadirasakan.json", "Dirasakan", cancellationToken);

            await Task.WhenAll(autoGempaTask, terkiniTask, dirasakanTask);

            var latest = await autoGempaTask;
            var terkini = await terkiniTask;
            var dirasakan = await dirasakanTask;

            if (latest != null)
            {
                _latestEarthquake = latest;
                CheckNewEarthquakeNotification(latest);
            }

            if (terkini.Count > 0)
            {
                _recentEarthquakes = terkini;
            }

            if (dirasakan.Count > 0)
            {
                _feltEarthquakes = dirasakan;
            }

            if (_currentWeatherData != null)
            {
                _statusMessage = $"Diperbarui {DateTime.Now:HH:mm} • Sumber: BMKG";
            }
            else
            {
                _statusMessage = $"Data gempa diperbarui {DateTime.Now:HH:mm} • Sumber: BMKG";
            }
        }
        catch (Exception ex)
        {
            if (_latestEarthquake == null)
            {
                _statusMessage = $"Gagal memuat data gempa BMKG: {ex.Message}";
            }
        }
        finally
        {
            _isLoadingEarthquake = false;
            RaiseDataUpdated();
        }
    }

    private void CheckNewEarthquakeNotification(BmkgEarthquakeItem latest)
    {
        var settings = _settingsService.CurrentSettings;
        if (string.IsNullOrWhiteSpace(latest.Id))
            return;

        string prevId = settings.LastNotifiedEarthquakeId;
        if (string.Equals(prevId, latest.Id, StringComparison.Ordinal))
            return;

        settings.LastNotifiedEarthquakeId = latest.Id;
        _settingsService.Save();

        if (!settings.BmkgNotifyNewEarthquake)
            return;

        // Pada pemuatan pertama kali (prevId kosong), hanya tampilkan alert jika gempa terjadi <= 90 menit terakhir
        if (string.IsNullOrEmpty(prevId) && latest.EventDateTime.HasValue)
        {
            var age = DateTimeOffset.UtcNow - latest.EventDateTime.Value.ToUniversalTime();
            if (age.TotalMinutes > 90)
            {
                return;
            }
        }

        var notif = new BmkgNotificationEvent
        {
            Id = latest.Id,
            Title = $"⚠️ Info Gempa BMKG • M {latest.Magnitude}",
            Message = $"{latest.Wilayah} • Kedalaman {latest.Kedalaman} ({latest.Jam})",
            MagnitudeBadge = $"M {latest.Magnitude}",
            Wilayah = latest.Wilayah,
            Jam = $"{latest.Tanggal} {latest.Jam}",
            Kedalaman = latest.Kedalaman,
            Potensi = latest.PotensiOrDirasakanSummary,
            ShakemapUrl = latest.ShakemapUrl,
            IsEarthquake = true,
            IsHighSeverity = latest.MagnitudeValue >= 5.0 || latest.IsPotentialTsunami,
            Timestamp = DateTime.Now
        };

        NotificationTriggered?.Invoke(this, notif);
    }

    private void CheckExtremeWeatherNotification(BmkgWeatherData weather)
    {
        var settings = _settingsService.CurrentSettings;
        if (!settings.BmkgNotifyExtremeWeather || weather.CurrentWeather == null)
            return;

        var current = weather.CurrentWeather;
        if (!current.IsExtremeWeather)
            return;

        string alertId = $"{weather.Adm4Code}_{current.LocalDateTime:yyyyMMddHH}_{current.WeatherCode}";
        if (string.Equals(settings.LastNotifiedWeatherAlertId, alertId, StringComparison.Ordinal))
            return;

        settings.LastNotifiedWeatherAlertId = alertId;
        _settingsService.Save();

        var notif = new BmkgNotificationEvent
        {
            Id = alertId,
            Title = $"{current.WeatherEmoji} Peringatan Cuaca BMKG • {weather.ShortCityLabel}",
            Message = $"{current.WeatherDesc} ({current.TemperatureFormatted}, Kelembapan {current.HumidityFormatted}, Angin {current.WindSpeedFormatted})",
            MagnitudeBadge = current.TemperatureFormatted,
            Wilayah = weather.FullLocationSubtitle,
            Jam = current.FullTimeLabel,
            Kedalaman = $"Angin {current.WindSpeedFormatted}",
            Potensi = $"Curah Hujan {current.PrecipitationFormatted}",
            IsEarthquake = false,
            IsHighSeverity = true,
            Timestamp = DateTime.Now
        };

        NotificationTriggered?.Invoke(this, notif);
    }

    public void TriggerTestEarthquakeAlert()
    {
        var eq = _latestEarthquake ?? new BmkgEarthquakeItem
        {
            Id = "TEST_" + DateTime.Now.Ticks,
            Tanggal = DateTime.Now.ToString("dd MMM yyyy"),
            Jam = DateTime.Now.ToString("HH:mm:ss") + " WIB",
            Magnitude = "5.3",
            MagnitudeValue = 5.3,
            Kedalaman = "148 km",
            Wilayah = "1 km BaratDaya TOMOHON-SULUT",
            Potensi = "Tidak berpotensi tsunami",
            Coordinates = "1.31,124.83",
            Lintang = "1.31 LU",
            Bujur = "124.83 BT"
        };

        var notif = new BmkgNotificationEvent
        {
            Id = eq.Id,
            Title = $"⚠️ Info Gempa BMKG • M {eq.Magnitude}",
            Message = $"{eq.Wilayah} • Kedalaman {eq.Kedalaman} ({eq.Jam})",
            MagnitudeBadge = $"M {eq.Magnitude}",
            Wilayah = eq.Wilayah,
            Jam = $"{eq.Tanggal} {eq.Jam}",
            Kedalaman = eq.Kedalaman,
            Potensi = eq.PotensiOrDirasakanSummary,
            ShakemapUrl = eq.ShakemapUrl,
            IsEarthquake = true,
            IsHighSeverity = eq.MagnitudeValue >= 5.0 || eq.IsPotentialTsunami,
            Timestamp = DateTime.Now
        };

        NotificationTriggered?.Invoke(this, notif);
    }

    private static BmkgWeatherData? ParseWeatherJson(string json, string fallbackAdm4)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var result = new BmkgWeatherData
        {
            Adm4Code = fallbackAdm4,
            LastUpdated = DateTime.Now
        };

        if (root.TryGetProperty("lokasi", out var lokasiEl) && lokasiEl.ValueKind == JsonValueKind.Object)
        {
            result.Adm4Code = GetStr(lokasiEl, "adm4", fallbackAdm4);
            result.Province = GetStr(lokasiEl, "provinsi", "");
            result.CityName = GetStr(lokasiEl, "kotkab", "Jakarta");
            result.DistrictName = GetStr(lokasiEl, "kecamatan", "");
            result.VillageName = GetStr(lokasiEl, "desa", "");
            result.Latitude = GetDouble(lokasiEl, "lat", -6.2088);
            result.Longitude = GetDouble(lokasiEl, "lon", 106.8456);
            result.TimezoneName = GetStr(lokasiEl, "timezone", "Asia/Jakarta");
        }

        var forecasts = new List<BmkgWeatherForecastItem>();

        if (root.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array && dataArr.GetArrayLength() > 0)
        {
            var firstData = dataArr[0];
            if (string.IsNullOrWhiteSpace(result.CityName) && firstData.TryGetProperty("lokasi", out var innerLok))
            {
                result.Province = GetStr(innerLok, "provinsi", result.Province);
                result.CityName = GetStr(innerLok, "kotkab", result.CityName);
                result.DistrictName = GetStr(innerLok, "kecamatan", result.DistrictName);
                result.VillageName = GetStr(innerLok, "desa", result.VillageName);
            }

            if (firstData.TryGetProperty("cuaca", out var cuacaOuter) && cuacaOuter.ValueKind == JsonValueKind.Array)
            {
                foreach (var dayArr in cuacaOuter.EnumerateArray())
                {
                    if (dayArr.ValueKind != JsonValueKind.Array) continue;
                    foreach (var itemEl in dayArr.EnumerateArray())
                    {
                        if (itemEl.ValueKind != JsonValueKind.Object) continue;

                        var localDtStr = GetStr(itemEl, "local_datetime", "");
                        var utcDtStr = GetStr(itemEl, "utc_datetime", "");

                        DateTime localDt = DateTime.Now;
                        if (!string.IsNullOrWhiteSpace(localDtStr))
                        {
                            DateTime.TryParse(localDtStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out localDt);
                        }

                        DateTime utcDt = DateTime.UtcNow;
                        if (!string.IsNullOrWhiteSpace(utcDtStr))
                        {
                            DateTime.TryParse(utcDtStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utcDt);
                        }

                        var forecastItem = new BmkgWeatherForecastItem
                        {
                            LocalDateTime = localDt,
                            UtcDateTime = utcDt,
                            TemperatureC = GetInt(itemEl, "t", 28),
                            HumidityPercent = GetInt(itemEl, "hu", 75),
                            WeatherCode = GetInt(itemEl, "weather", 1),
                            WeatherDesc = GetStr(itemEl, "weather_desc", "Cerah Berawan"),
                            WeatherDescEn = GetStr(itemEl, "weather_desc_en", "Partly Cloudy"),
                            WindSpeedKmh = GetDouble(itemEl, "ws", 0),
                            WindDirectionCode = GetStr(itemEl, "wd", "N"),
                            WindDirectionDeg = GetInt(itemEl, "wd_deg", 0),
                            CloudCoverPercent = GetInt(itemEl, "tcc", 0),
                            PrecipitationMm = GetDouble(itemEl, "tp", 0),
                            VisibilityText = GetStr(itemEl, "vs_text", ""),
                            IconUrl = GetStr(itemEl, "image", "")
                        };

                        forecasts.Add(forecastItem);
                    }
                }
            }
        }

        forecasts = forecasts.OrderBy(f => f.LocalDateTime).ToList();
        result.Forecasts = forecasts;

        if (forecasts.Count > 0)
        {
            var now = DateTime.Now;
            var current = forecasts
                .Where(f => f.LocalDateTime <= now.AddMinutes(30))
                .OrderByDescending(f => f.LocalDateTime)
                .FirstOrDefault() ?? forecasts[0];

            current.IsCurrentSlot = true;
            result.CurrentWeather = current;

            var todayItems = forecasts.Where(f => f.LocalDateTime.Date == now.Date).ToList();
            if (todayItems.Count == 0) todayItems = forecasts.Take(8).ToList();

            result.TodayMinTempC = todayItems.Min(f => f.TemperatureC);
            result.TodayMaxTempC = todayItems.Max(f => f.TemperatureC);

            var culture = new CultureInfo("id-ID");
            var groups = forecasts
                .GroupBy(f => f.LocalDateTime.Date)
                .OrderBy(g => g.Key)
                .Take(4)
                .Select(g =>
                {
                    var items = g.OrderBy(x => x.LocalDateTime).ToList();
                    string shortDay = g.Key == now.Date ? "Hari Ini"
                        : g.Key == now.Date.AddDays(1) ? "Besok"
                        : g.Key == now.Date.AddDays(2) ? "Lusa"
                        : g.Key.ToString("dddd", culture);

                    string fullTitle = $"{shortDay} ({g.Key.ToString("ddd, dd MMM", culture)})";
                    var midDayItem = items.FirstOrDefault(x => x.LocalDateTime.Hour >= 11 && x.LocalDateTime.Hour <= 15) ?? items[items.Count / 2];

                    return new BmkgWeatherDayGroup
                    {
                        Date = g.Key,
                        ShortDayTitle = shortDay,
                        DayTitle = fullTitle,
                        MinTempC = items.Min(x => x.TemperatureC),
                        MaxTempC = items.Max(x => x.TemperatureC),
                        DominantWeatherEmoji = midDayItem.WeatherEmoji,
                        DominantWeatherIconGeometry = midDayItem.WeatherIconGeometry,
                        DominantWeatherIconColor = midDayItem.WeatherIconColor,
                        DominantWeatherDesc = midDayItem.WeatherDesc,
                        Items = items
                    };
                })
                .ToList();

            result.DayGroups = groups;
        }

        return result;
    }

    private static async Task<BmkgEarthquakeItem?> FetchSingleEarthquakeAsync(string url, CancellationToken cancellationToken)
    {
        var json = await _httpClient.GetStringAsync(url, cancellationToken);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("Infogempa", out var info) &&
            info.TryGetProperty("gempa", out var gempaEl) &&
            gempaEl.ValueKind == JsonValueKind.Object)
        {
            return ParseEarthquakeElement(gempaEl, "AutoGempa");
        }
        return null;
    }

    private static async Task<List<BmkgEarthquakeItem>> FetchEarthquakeListAsync(string url, string category, CancellationToken cancellationToken)
    {
        var list = new List<BmkgEarthquakeItem>();
        var json = await _httpClient.GetStringAsync(url, cancellationToken);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("Infogempa", out var info) &&
            info.TryGetProperty("gempa", out var gempaArr) &&
            gempaArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var itemEl in gempaArr.EnumerateArray())
            {
                if (itemEl.ValueKind == JsonValueKind.Object)
                {
                    list.Add(ParseEarthquakeElement(itemEl, category));
                }
            }
        }
        return list;
    }

    private static BmkgEarthquakeItem ParseEarthquakeElement(JsonElement el, string category)
    {
        string tanggal = GetStr(el, "Tanggal", "");
        string jam = GetStr(el, "Jam", "");
        string dtStr = GetStr(el, "DateTime", "");
        string coords = GetStr(el, "Coordinates", "");
        string magStr = GetStr(el, "Magnitude", "0.0");

        DateTimeOffset? parsedDt = null;
        if (!string.IsNullOrWhiteSpace(dtStr) &&
            DateTimeOffset.TryParse(dtStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
        {
            parsedDt = dto;
        }

        double.TryParse(magStr.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double magVal);

        string id = !string.IsNullOrWhiteSpace(dtStr)
            ? $"{dtStr}_{coords}_{magStr}"
            : $"{tanggal}_{jam}_{coords}_{magStr}";

        return new BmkgEarthquakeItem
        {
            Id = id,
            Tanggal = tanggal,
            Jam = jam,
            EventDateTime = parsedDt,
            Coordinates = coords,
            Lintang = GetStr(el, "Lintang", ""),
            Bujur = GetStr(el, "Bujur", ""),
            Magnitude = magStr,
            MagnitudeValue = magVal,
            Kedalaman = GetStr(el, "Kedalaman", ""),
            Wilayah = GetStr(el, "Wilayah", ""),
            Potensi = GetStr(el, "Potensi", ""),
            Dirasakan = GetStr(el, "Dirasakan", ""),
            ShakemapFileName = GetStr(el, "Shakemap", ""),
            Category = category
        };
    }

    private static string GetStr(JsonElement el, string propName, string defaultValue)
    {
        if (el.TryGetProperty(propName, out var val))
        {
            if (val.ValueKind == JsonValueKind.String)
                return val.GetString() ?? defaultValue;
            if (val.ValueKind == JsonValueKind.Number)
                return val.GetRawText();
        }
        return defaultValue;
    }

    private static int GetInt(JsonElement el, string propName, int defaultValue)
    {
        if (el.TryGetProperty(propName, out var val))
        {
            if (val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out int i))
                return i;
            if (val.ValueKind == JsonValueKind.Number && val.TryGetDouble(out double d))
                return (int)Math.Round(d);
            if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out int s))
                return s;
        }
        return defaultValue;
    }

    private static double GetDouble(JsonElement el, string propName, double defaultValue)
    {
        if (el.TryGetProperty(propName, out var val))
        {
            if (val.ValueKind == JsonValueKind.Number && val.TryGetDouble(out double d))
                return d;
            if (val.ValueKind == JsonValueKind.String && double.TryParse(val.GetString()?.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double s))
                return s;
        }
        return defaultValue;
    }

    private void RaiseDataUpdated()
    {
        DataUpdated?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _earthquakePollTimer.Stop();
        _weatherPollTimer.Stop();
    }
}
