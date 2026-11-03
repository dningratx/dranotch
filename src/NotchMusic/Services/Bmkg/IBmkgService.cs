using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NotchMusic.Models;

namespace NotchMusic.Services.Bmkg;

public interface IBmkgService : IDisposable
{
    BmkgWeatherData? CurrentWeatherData { get; }
    BmkgEarthquakeItem? LatestEarthquake { get; }
    IReadOnlyList<BmkgEarthquakeItem> RecentEarthquakes { get; }
    IReadOnlyList<BmkgEarthquakeItem> FeltEarthquakes { get; }
    bool IsLoadingWeather { get; }
    bool IsLoadingEarthquake { get; }
    string StatusMessage { get; }

    event EventHandler? DataUpdated;
    event EventHandler<BmkgNotificationEvent>? NotificationTriggered;

    void Start();
    void Stop();
    Task RefreshAllAsync(bool forceWeather = false, CancellationToken cancellationToken = default);
    Task<BmkgWeatherData?> RefreshWeatherAsync(CancellationToken cancellationToken = default);
    Task RefreshEarthquakesAsync(CancellationToken cancellationToken = default);
    string GetActiveAdm4Code();
    string GetActiveCityName();
    void TriggerTestEarthquakeAlert();

    // Precise Indonesian Region Hierarchy & Search (Provinsi -> Kab/Kota -> Kecamatan -> Kelurahan/Desa)
    IReadOnlyList<BmkgRegionItem> GetProvinces();
    Task<IReadOnlyList<BmkgRegionItem>> GetRegenciesAsync(string provinceCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BmkgRegionItem>> GetDistrictsAsync(string regencyCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BmkgRegionItem>> GetVillagesAsync(string districtCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BmkgLocationSearchResult>> SearchPreciseLocationsAsync(string query, CancellationToken cancellationToken = default);
    Task<BmkgLocationSearchResult?> ResolveCoordinatesToPreciseAdm4Async(double latitude, double longitude, string? fallbackCityName = null, CancellationToken cancellationToken = default);
}
