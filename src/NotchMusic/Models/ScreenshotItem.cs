namespace NotchMusic.Models;

public class ScreenshotItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int Width { get; set; }
    public int Height { get; set; }
    public long FileSize { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string CaptureMode { get; set; } = "Region"; // "Region", "FullScreen", "ActiveWindow"
    public bool IsPinned { get; set; } = false;
}
