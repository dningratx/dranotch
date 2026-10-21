using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Settings;

namespace NotchMusic.Services.Update;

public class UpdateService : IUpdateService, IDisposable
{
    private const string DefaultGitHubReleaseUrl = "https://api.github.com/repos/dningratx/dranotch/releases/latest";
    private readonly ISettingsService _settingsService;
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;

    public string CurrentVersion
    {
        get
        {
            var asm = Assembly.GetExecutingAssembly();
            var infoVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(infoVersion))
            {
                return infoVersion.Split('+')[0].Trim();
            }
            var ver = asm.GetName().Version;
            return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.0";
        }
    }

    public UpdateService(ISettingsService settingsService, HttpClient? httpClient = null)
    {
        _settingsService = settingsService;
        if (httpClient != null)
        {
            _httpClient = httpClient;
            _disposeClient = false;
        }
        else
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DRANOTCH-App", CurrentVersion));
            _disposeClient = true;
        }
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var customUrl = _settingsService.CurrentSettings.CustomUpdateUrl;
            var targetUrl = !string.IsNullOrWhiteSpace(customUrl) ? customUrl.Trim() : DefaultGitHubReleaseUrl;

            string jsonContent;

            // Check if target is a local file
            if (File.Exists(targetUrl))
            {
                jsonContent = await File.ReadAllTextAsync(targetUrl, cancellationToken);
            }
            else if (targetUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                var localPath = new Uri(targetUrl).LocalPath;
                if (!File.Exists(localPath)) return null;
                jsonContent = await File.ReadAllTextAsync(localPath, cancellationToken);
            }
            else
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
                if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
                {
                    request.Headers.UserAgent.Add(new ProductInfoHeaderValue("DRANOTCH-App", CurrentVersion));
                }

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            }

            var updateInfo = ParseUpdateJson(jsonContent);
            if (updateInfo != null)
            {
                var (isNewer, isMajor, isMinor, isPatch) = VersionHelper.Compare(CurrentVersion, updateInfo.Version);
                updateInfo.IsNewerVersion = isNewer;
                updateInfo.IsMajor = isMajor;
                updateInfo.IsMinor = isMinor;
                updateInfo.IsPatch = isPatch;

                _settingsService.CurrentSettings.LastUpdateCheck = DateTime.Now;
                _settingsService.Save();
            }

            return updateInfo;
        }
        catch
        {
            return null;
        }
    }

    public UpdateInfo? ParseUpdateJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Check GitHub Release API format (tag_name, body, assets)
            if (root.TryGetProperty("tag_name", out var tagProp))
            {
                var tag = tagProp.GetString() ?? string.Empty;
                var version = tag.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? tag.Substring(1) : tag;

                var title = root.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString())
                    ? nameProp.GetString()!
                    : $"DRANOTCH v{version}";

                var changelog = root.TryGetProperty("body", out var bodyProp) ? (bodyProp.GetString() ?? string.Empty) : string.Empty;
                var releasePageUrl = root.TryGetProperty("html_url", out var htmlUrlProp) ? (htmlUrlProp.GetString() ?? string.Empty) : string.Empty;

                DateTime? publishedAt = null;
                if (root.TryGetProperty("published_at", out var pubProp) && pubProp.TryGetDateTime(out var dt))
                {
                    publishedAt = dt;
                }

                string downloadUrl = string.Empty;
                if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assetsProp.EnumerateArray())
                    {
                        var assetName = asset.TryGetProperty("name", out var an) ? an.GetString() : string.Empty;
                        if (!string.IsNullOrEmpty(assetName) &&
                            assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                            asset.TryGetProperty("browser_download_url", out var dlProp))
                        {
                            downloadUrl = dlProp.GetString() ?? string.Empty;
                            break;
                        }
                    }
                }

                return new UpdateInfo
                {
                    Version = version,
                    Title = title,
                    Changelog = changelog,
                    DownloadUrl = downloadUrl,
                    ReleasePageUrl = releasePageUrl,
                    PublishedAt = publishedAt
                };
            }

            // Standalone manifest format (version, title, changelog, downloadUrl, releasePageUrl)
            if (root.TryGetProperty("version", out var vProp))
            {
                var version = vProp.GetString() ?? string.Empty;
                if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase)) version = version.Substring(1);

                var title = root.TryGetProperty("title", out var tProp) ? (tProp.GetString() ?? $"DRANOTCH v{version}") : $"DRANOTCH v{version}";
                var changelog = root.TryGetProperty("changelog", out var clProp) ? (clProp.GetString() ?? string.Empty) : string.Empty;

                string downloadUrl = string.Empty;
                if (root.TryGetProperty("downloadUrl", out var dlProp))
                    downloadUrl = dlProp.GetString() ?? string.Empty;
                else if (root.TryGetProperty("download_url", out var dlProp2))
                    downloadUrl = dlProp2.GetString() ?? string.Empty;

                string releasePageUrl = string.Empty;
                if (root.TryGetProperty("releasePageUrl", out var rpProp))
                    releasePageUrl = rpProp.GetString() ?? string.Empty;
                else if (root.TryGetProperty("release_page_url", out var rpProp2))
                    releasePageUrl = rpProp2.GetString() ?? string.Empty;

                return new UpdateInfo
                {
                    Version = version,
                    Title = title,
                    Changelog = changelog,
                    DownloadUrl = downloadUrl,
                    ReleasePageUrl = releasePageUrl
                };
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
            throw new ArgumentException("Download URL cannot be empty.", nameof(downloadUrl));

        // If local file path
        if (File.Exists(downloadUrl))
        {
            progress?.Report(100.0);
            return downloadUrl;
        }

        if (downloadUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var localPath = new Uri(downloadUrl).LocalPath;
            if (File.Exists(localPath))
            {
                progress?.Report(100.0);
                return localPath;
            }
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "DRANOTCH_Update");
        Directory.CreateDirectory(tempDir);

        string fileName = "DRANOTCH-Setup.exe";
        try
        {
            var uri = new Uri(downloadUrl);
            var lastSegment = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(lastSegment) && lastSegment.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                fileName = lastSegment;
            }
        }
        catch { }

        var destinationPath = Path.Combine(tempDir, fileName);

        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalRead += bytesRead;

            if (totalBytes > 0)
            {
                double pct = (double)totalRead / totalBytes * 100.0;
                progress?.Report(pct);
            }
        }

        progress?.Report(100.0);
        return destinationPath;
    }

    public void LaunchInstallerAndExit(string installerPath)
    {
        if (!File.Exists(installerPath))
            throw new FileNotFoundException("Installer file not found.", installerPath);

        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true
        };

        Process.Start(psi);

        // Terminate current app cleanly so the installer can overwrite binaries
        if (System.Windows.Application.Current != null)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.Application.Current.Shutdown();
            });
        }
        else
        {
            Environment.Exit(0);
        }
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
    }
}
