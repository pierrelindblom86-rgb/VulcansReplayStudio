using System.Windows.Interop;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using VulcansReplayStudio2.App.Views;
using VulcansReplayStudio2.Core.Services;
using VulcansReplayStudio2.Infrastructure.Services;
using System.Windows.Controls;
using System;
using System.Windows;
using System.Windows.Input;
using VulcansReplayStudio2.App.Views;
using VulcansReplayStudio2.Core.Services;
using VulcansReplayStudio2.Infrastructure.Services;

namespace VulcansReplayStudio2.App;

public partial class MainWindow : Window
{
    private const int WM_HOTKEY = 0x0312;
    private HotkeyService? _hotkey;
    private HwndSource? _hwndSource;
    private readonly IReplayBufferService _buffer;
    private readonly ICaptureService _capture;
    private readonly IExportService _export;
    private readonly IClipLibraryService _library;
    private readonly ISettingsService _settings;

    public MainWindow()
    {
        InitializeComponent();
        _settings = App.Services.Get<ISettingsService>();
        _buffer = App.Services.Get<IReplayBufferService>();
        _capture = App.Services.Get<ICaptureService>();
        _export = App.Services.Get<IExportService>();
        _library = App.Services.Get<IClipLibraryService>();
        _hotkey = App.Services.Get<HotkeyService>();
        _hotkey.HotkeyPressed += OnHotkeyPressed;
        SourceInitialized += MainWindow_SourceInitialized;

        _capture.StatusChanged += Capture_StatusChanged;

        // Set window placement
        try
        {
            var s = _settings.Settings;
            Left = s.WindowLeft; Top = s.WindowTop; Width = s.WindowWidth; Height = s.WindowHeight;
        }
        catch { }

        ShowDashboard();
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _hotkey!.SetHandle(handle);
        _hwndSource = HwndSource.FromHwnd(handle);
        _hwndSource?.AddHook(WndProc);
        if (!_hotkey.Register(_settings.Settings.Hotkey))
        {
            StatusText.Text = $"Hotkey {_settings.Settings.Hotkey} could not be registered (in use by another app?)";
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            _hotkey?.ProcessHotkeyMessage(msg, wParam);
        }
        return IntPtr.Zero;
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        _ = SaveReplayAsync();
    }

    private void Capture_StatusChanged(object? sender, CaptureStatusEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.IsError) StatusText.Text = $"Error: {e.Message}";
            else StatusText.Text = e.Message;
        });
    }

    private async System.Threading.Tasks.Task SaveReplayAsync()
    {
        if (!_buffer.IsRunning) return;
        var storage = ClipFolderResolver.Resolve(_settings.Settings, out var warning);
        Directory.CreateDirectory(storage);
        var path = Path.Combine(storage, $"Replay_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
        var result = await _buffer.SaveReplayAsync(path);
        if (!string.IsNullOrEmpty(result))
        {
            await _library.RefreshAsync(_settings.Settings.StoragePath);
            StatusText.Text = warning ?? $"Saved: {result}";
        }
    }

    public void NavDashboard_Click(object sender, EventArgs e) => ShowDashboard();
    public void NavLibrary_Click(object sender, EventArgs e)
    {
        PageTitle.Text = "Replay Library";
        ContentArea.Children.Clear();
        ContentArea.Children.Add(new LibraryView());
    }
    public void NavCapture_Click(object sender, EventArgs e)
    {
        PageTitle.Text = "Capture";
        ContentArea.Children.Clear();
        ContentArea.Children.Add(new CaptureView());
    }
    public void NavSettings_Click(object sender, EventArgs e)
    {
        PageTitle.Text = "Settings";
        ContentArea.Children.Clear();
        ContentArea.Children.Add(new SettingsView());
    }

    private void ShowDashboard()
    {
        PageTitle.Text = "Dashboard";
        ContentArea.Children.Clear();
        ContentArea.Children.Add(new DashboardView());
    }

    protected override void OnClosed(EventArgs e)
    {
        try { _hotkey?.Unregister(); } catch { }
        try { _hwndSource?.RemoveHook(WndProc); _hwndSource = null; } catch { }
        try { _buffer.StopAsync().GetAwaiter().GetResult(); } catch { }
        base.OnClosed(e);
    }
}