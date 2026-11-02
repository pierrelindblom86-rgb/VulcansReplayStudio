using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VulcansReplayStudio2.Core.Services;
using VulcansReplayStudio2.Infrastructure.Services;
using System.Windows.Controls;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VulcansReplayStudio2.Core.Services;
using VulcansReplayStudio2.Infrastructure.Services;

namespace VulcansReplayStudio2.App.Views;

public partial class DashboardView
{
    private readonly ICaptureService _capture;
    private readonly IReplayBufferService _buffer;
    private readonly HotkeyService? _hotkey;
    private readonly ISettingsService _settings;
    private readonly IClipLibraryService _library;

    public DashboardView()
    {
        InitializeComponent();
        _capture = App.Services.Get<ICaptureService>();
        _buffer = App.Services.Get<IReplayBufferService>();
        _settings = App.Services.Get<ISettingsService>();
        _library = App.Services.Get<IClipLibraryService>();
        try { _hotkey = App.Services.Get<HotkeyService>(); } catch { _hotkey = null; }

        HotkeyText.Text = _settings.Settings.Hotkey;
        BufferText.Text = _settings.Settings.ReplayDurationSeconds + "s";
        RefreshRecentClips();
    }

    private void OnStartCapture(object sender, EventArgs e)
    {
        try
        {
            var s = _settings.Settings;
            var ok = _capture.StartAsync(s.CaptureSource, s.Width, s.Height, s.Fps).GetAwaiter().GetResult();
            if (ok)
            {
                _buffer.StartAsync(TimeSpan.FromSeconds(s.ReplayDurationSeconds), s.Fps).GetAwaiter().GetResult();
                CaptureStatusText.Text = "Capturing";
                StatusText.Text = "Capturing";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error: " + ex.Message;
        }
    }

    private void OnStopCapture(object sender, EventArgs e)
    {
        try
        {
            _capture.StopAsync().GetAwaiter().GetResult();
            _buffer.StopAsync().GetAwaiter().GetResult();
            CaptureStatusText.Text = "Stopped";
            StatusText.Text = "Stopped";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error: " + ex.Message;
        }
    }

    private async void OnSaveReplay(object sender, EventArgs e)
    {
        if (!_buffer.IsRunning) return;
        var storage = ClipFolderResolver.Resolve(_settings.Settings, out var warning);
        Directory.CreateDirectory(storage);
        var path = Path.Combine(storage, "Replay_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4");
        var result = await _buffer.SaveReplayAsync(path);
        if (!string.IsNullOrEmpty(result))
        {
            await _library.RefreshAsync(_settings.Settings.StoragePath);
            RefreshRecentClips();
            StatusText.Text = warning ?? "Saved: " + Path.GetFileName(result);
        }
    }

    private void OnOpenLibrary(object sender, EventArgs e)
    {
        if (Parent is FrameworkElement fe)
        {
            var win = Window.GetWindow(fe);
            if (win is MainWindow mw) mw.NavLibrary_Click(this, new EventArgs());
        }
    }

    private void RefreshRecentClips()
    {
        try
        {
            var clips = _library.Clips.Take(5).Select(c => c.Name + " (" + c.Duration.ToString(@"mm\:ss") + ", " + (c.FileSizeBytes / 1024 / 1024) + "MB)");
            RecentClipsText.Text = clips.Any() ? string.Join("\n", clips) : "No clips yet";
        }
        catch { }
    }

    private TextBlock StatusText => (TextBlock)FindName("StatusText") ?? new TextBlock();
}