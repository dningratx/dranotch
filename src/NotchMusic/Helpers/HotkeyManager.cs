using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NotchMusic.Helpers;

public class HotkeyManager : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    private readonly Window _window;
    private HwndSource? _source;
    private readonly Dictionary<int, Action> _hotkeyActions = new();
    private int _currentId = 9000;

    public HotkeyManager(Window window)
    {
        _window = window;
        var helper = new WindowInteropHelper(window);
        if (helper.Handle != IntPtr.Zero)
        {
            AttachHook(helper.Handle);
        }
        else
        {
            window.SourceInitialized += (s, e) =>
            {
                var handle = new WindowInteropHelper(window).Handle;
                AttachHook(handle);
            };
        }
    }

    private void AttachHook(IntPtr handle)
    {
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(HwndHook);
    }

    public bool Register(string hotkeyString, Action callback, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            error = "Hotkey kosong";
            return false;
        }

        if (!ParseHotkey(hotkeyString, out var modifiers, out var vk))
        {
            error = $"Format hotkey tidak valid: {hotkeyString}";
            return false;
        }

        var handle = new WindowInteropHelper(_window).Handle;
        if (handle == IntPtr.Zero)
        {
            error = "Handle jendela belum siap";
            return false;
        }

        var id = ++_currentId;
        bool success = RegisterHotKey(handle, id, modifiers, vk);
        if (!success)
        {
            error = $"Konflik: Hotkey '{hotkeyString}' sudah digunakan aplikasi lain.";
            return false;
        }

        _hotkeyActions[id] = callback;
        return true;
    }

    public void UnregisterAll()
    {
        var handle = new WindowInteropHelper(_window).Handle;
        if (handle != IntPtr.Zero)
        {
            foreach (var id in _hotkeyActions.Keys.ToList())
            {
                UnregisterHotKey(handle, id);
            }
        }
        _hotkeyActions.Clear();
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_hotkeyActions.TryGetValue(id, out var action))
            {
                action();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public static bool ParseHotkey(string str, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        var parts = str.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        foreach (var part in parts)
        {
            var p = part.ToUpperInvariant();
            if (p == "CTRL" || p == "CONTROL") modifiers |= MOD_CONTROL;
            else if (p == "ALT") modifiers |= MOD_ALT;
            else if (p == "SHIFT") modifiers |= MOD_SHIFT;
            else if (p == "WIN") modifiers |= MOD_WIN;
            else
            {
                // Key
                if (Enum.TryParse<System.Windows.Forms.Keys>(p, true, out var key))
                {
                    vk = (uint)key;
                }
                else if (p.Length == 1)
                {
                    vk = (uint)p[0];
                }
            }
        }

        return vk != 0;
    }

    public void Dispose()
    {
        UnregisterAll();
        if (_source != null)
        {
            _source.RemoveHook(HwndHook);
            _source = null;
        }
    }
}
