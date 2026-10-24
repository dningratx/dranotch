using System.Windows.Forms;
using NotchMusic.Models;

namespace NotchMusic.Services.Tray;

public interface ITrayIconService : IDisposable
{
    void Initialize();
    event Action? ToggleVisibilityRequested;
    event Action? OpenYouTubeMusicRequested;
    event Action? OpenRadioRequested;
    event Action? OpenFloatingBrowserRequested;
    event Action<PanelMode>? ModeChangeRequested;
    event Action? OpenSettingsRequested;
    event Action? ToggleClickThroughRequested;
    event Action? ExitRequested;
    void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info);
}

public class TrayIconService : ITrayIconService
{
    private NotifyIcon? _notifyIcon;
    private bool _isDisposed;

    public event Action? ToggleVisibilityRequested;
    public event Action? OpenYouTubeMusicRequested;
    public event Action? OpenRadioRequested;
    public event Action? OpenFloatingBrowserRequested;
    public event Action<PanelMode>? ModeChangeRequested;
    public event Action? OpenSettingsRequested;
    public event Action? ToggleClickThroughRequested;
    public event Action? ExitRequested;

    public void Initialize()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "DRANOTCH",
            Visible = true
        };

        // Try load app icon
        try
        {
            var iconLoaded = false;

            // 1. Load from embedded WPF resource
            try
            {
                var sri = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
                if (sri?.Stream != null)
                {
                    using (var s = sri.Stream)
                    {
                        _notifyIcon.Icon = new System.Drawing.Icon(s);
                        iconLoaded = true;
                    }
                }
            }
            catch { }

            // 2. Extract icon from running executable
            if (!iconLoaded)
            {
                try
                {
                    var exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath) && System.IO.File.Exists(exePath))
                    {
                        var extracted = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                        if (extracted != null)
                        {
                            _notifyIcon.Icon = extracted;
                            iconLoaded = true;
                        }
                    }
                }
                catch { }
            }

            // 3. Load from disk file
            if (!iconLoaded)
            {
                var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    _notifyIcon.Icon = new System.Drawing.Icon(iconPath);
                    iconLoaded = true;
                }
            }

            if (!iconLoaded)
            {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }
        }
        catch
        {
            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
        }

        var contextMenu = new ContextMenuStrip();

        var ytMusicItem = new ToolStripMenuItem("Buka YouTube Music (Player)", null, (s, e) => OpenYouTubeMusicRequested?.Invoke());
        contextMenu.Items.Add(ytMusicItem);

        var radioItem = new ToolStripMenuItem("Buka Radio Online (Streaming Radio)", null, (s, e) => OpenRadioRequested?.Invoke());
        contextMenu.Items.Add(radioItem);

        var browserItem = new ToolStripMenuItem("Buka Browser Mengambang (Web / Video)", null, (s, e) => OpenFloatingBrowserRequested?.Invoke());
        contextMenu.Items.Add(browserItem);

        var showHideItem = new ToolStripMenuItem("Tampilkan / Sembunyikan Panel", null, (s, e) => ToggleVisibilityRequested?.Invoke());
        contextMenu.Items.Add(showHideItem);

        var modeMenu = new ToolStripMenuItem("Mode Tampilan");
        modeMenu.DropDownItems.Add(new ToolStripMenuItem("Compact", null, (s, e) => ModeChangeRequested?.Invoke(PanelMode.Compact)));
        modeMenu.DropDownItems.Add(new ToolStripMenuItem("Expanded", null, (s, e) => ModeChangeRequested?.Invoke(PanelMode.Expanded)));
        modeMenu.DropDownItems.Add(new ToolStripMenuItem("Lyrics", null, (s, e) => ModeChangeRequested?.Invoke(PanelMode.Lyrics)));
        contextMenu.Items.Add(modeMenu);

        var clickThroughItem = new ToolStripMenuItem("Matikan / Aktifkan Click-Through", null, (s, e) => ToggleClickThroughRequested?.Invoke());
        contextMenu.Items.Add(clickThroughItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var settingsItem = new ToolStripMenuItem("Pengaturan...", null, (s, e) => OpenSettingsRequested?.Invoke());
        contextMenu.Items.Add(settingsItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Keluar", null, (s, e) => ExitRequested?.Invoke());
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => ToggleVisibilityRequested?.Invoke();
    }

    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        try
        {
            if (_notifyIcon != null && _notifyIcon.Visible)
            {
                _notifyIcon.ShowBalloonTip(5000, title, message, icon);
            }
        }
        catch { }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
