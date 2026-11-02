using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using VulcansReplayStudio2.Core.Services;

namespace VulcansReplayStudio2.Infrastructure.Services;

public class HotkeyService : IHotkeyService, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NONE = 0x0000;
    private const uint VK_F9 = 0x78;
    private IntPtr _handle = IntPtr.Zero;
    private int _hotkeyId = 9001;
    private bool _registered;
    
    public bool IsRegistered => _registered;
    public string Hotkey { get; private set; } = "F9";
    public event EventHandler? HotkeyPressed;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public void SetHandle(IntPtr handle) { _handle = handle; }

    public bool Register(string hotkey)
    {
        Unregister();
        Hotkey = hotkey;
        if (hotkey != "F9" || _handle == IntPtr.Zero) 
        { 
            _registered = false; 
            return false; 
        }
        try
        {
            _registered = RegisterHotKey(_handle, _hotkeyId, MOD_NONE, VK_F9);
            return _registered;
        }
        catch
        {
            _registered = false;
            return false;
        }
    }

    public void Unregister()
    {
        if (_registered && _handle != IntPtr.Zero)
        {
            try { UnregisterHotKey(_handle, _hotkeyId); } catch { }
        }
        _registered = false;
    }

    public void ProcessHotkeyMessage(int msg, IntPtr wParam)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == _hotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        Unregister();
    }
}