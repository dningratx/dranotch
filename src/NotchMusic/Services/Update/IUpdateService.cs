using NotchMusic.Models;

namespace NotchMusic.Services.Update;

public interface IUpdateService
{
    string CurrentVersion { get; }
    Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
    Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    void LaunchInstallerAndExit(string installerPath);
}
