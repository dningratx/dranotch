using System.Drawing;
using System.Windows.Media.Imaging;
using NotchMusic.Models;

namespace NotchMusic.Services.Screenshot;

public interface IScreenshotService
{
    List<ScreenshotItem> LoadScreenshots();
    ScreenshotItem SaveScreenshot(Bitmap bitmap, string captureMode = "Region", string? customTitle = null);
    ScreenshotItem SaveScreenshot(BitmapSource bitmapSource, string captureMode = "Region", string? customTitle = null);
    bool DeleteScreenshot(string id);
    void ClearAllScreenshots();
    void UpdateScreenshot(ScreenshotItem item);
    string GetScreenshotsFolder();
    int ExportAll(string targetDirectory);
    IReadOnlySet<string> GetDeletedIds();
    void MarkDeleted(string id);
    void MarkDeleted(IEnumerable<string> ids);
    void ClearDeletedIds(IEnumerable<string> idsToClear);
    void SetScreenshots(IEnumerable<ScreenshotItem> items);
    event Action<ScreenshotItem>? ScreenshotCaptured;
    event Action? ScreenshotsChanged;
}
