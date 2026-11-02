using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using VulcansReplayStudio2.Core.Services;
using VulcansReplayStudio2.Infrastructure.Services;
using VulcansReplayStudio2.MediaEngine.Buffer;
using VulcansReplayStudio2.MediaEngine.Capture;
using VulcansReplayStudio2.MediaEngine.Export;
using System.Windows.Controls;
using System;
using System.IO;
using System.Windows;
using VulcansReplayStudio2.Core.Services;
using VulcansReplayStudio2.Infrastructure.Services;
using VulcansReplayStudio2.MediaEngine.Buffer;
using VulcansReplayStudio2.MediaEngine.Capture;
using VulcansReplayStudio2.MediaEngine.Export;

namespace VulcansReplayStudio2.App;

public partial class App : System.Windows.Application
{
    public static ServiceContainer Services { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Locate FFmpeg
            var ffmpegPath = FFmpegLocator.Resolve("ffmpeg.exe");
            var ffprobePath = FFmpegLocator.Resolve("ffprobe.exe");
            Log($"FFmpeg located: {ffmpegPath}");
            Log($"FFprobe located: {ffprobePath}");

            // Register services
            var settingsService = new SettingsService();
            Services.Register<ISettingsService>(settingsService);
            Services.Register<IClipLibraryService>(new ClipLibraryService(ffprobePath, settingsService));
            Services.Register<ICaptureService>(new FfmpegCaptureService());
            Services.Register<IReplayBufferService>(new ReplayBufferService(ffmpegPath));
            Services.Register<IExportService>(new FFmpegExportService(ffmpegPath));
            Services.Register<HotkeyService>(new HotkeyService());

            // Load settings
            Services.Get<ISettingsService>().LoadAsync().Wait();
        }
        catch (Exception ex)
        {
            Log($"Startup error: {ex}");
            MessageBox.Show($"Application failed to start: {ex.Message}", "Startup Error", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Services.Dispose();
        }
        catch (Exception ex) { Log($"Dispose error: {ex}"); }
        base.OnExit(e);
    }

    private static void Log(string msg)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VulcansReplayStudio2", "Logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "startup.log"), $"{DateTime.Now:u} {msg}\r\n");
        }
        catch { }
    }
}