using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VulcansReplayStudio2.Core.Services;
using System.Windows.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VulcansReplayStudio2.Core.Services;

namespace VulcansReplayStudio2.App.Views;

public partial class CaptureView
{
    private readonly ICaptureService _capture;
    private readonly IReplayBufferService _buffer;
    private readonly ISettingsService _settings;
    private readonly IExportService _export;
    private readonly IClipLibraryService _library;

    public CaptureView()
    {
        InitializeComponent();
        _capture = App.Services.Get<ICaptureService>();
        _buffer = App.Services.Get<IReplayBufferService>();
        _settings = App.Services.Get<ISettingsService>();
        _export = App.Services.Get<IExportService>();
        _library = App.Services.Get<IClipLibraryService>();
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
            }
        }
        catch (Exception ex)
        {
            CaptureStatusText.Text = "Error: " + ex.Message;
        }
    }

    private void OnStopCapture(object sender, EventArgs e)
    {
        try
        {
            _capture.StopAsync().GetAwaiter().GetResult();
            _buffer.StopAsync().GetAwaiter().GetResult();
            CaptureStatusText.Text = "Stopped";
        }
        catch (Exception ex)
        {
            CaptureStatusText.Text = "Error: " + ex.Message;
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
            CaptureStatusText.Text = warning ?? "Saved: " + Path.GetFileName(result);
        }
    }
}