namespace NotchMusic.Models;

public class UpdateInfo
{
    public string Version { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Changelog { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ReleasePageUrl { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public bool IsNewerVersion { get; set; }
    public bool IsMajor { get; set; }
    public bool IsMinor { get; set; }
    public bool IsPatch { get; set; }

    public string UpdateTypeLabel =>
        IsMajor ? "Pembaruan Mayor (Major)" :
        IsMinor ? "Pembaruan Minor (Minor)" :
        "Perbaikan / Patch";
}
