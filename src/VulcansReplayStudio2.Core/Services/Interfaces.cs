using System;
using System.Threading;
using System.Threading.Tasks;
using VulcansReplayStudio2.Core.Models;

namespace VulcansReplayStudio2.Core.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    Task LoadAsync();
    Task SaveAsync();
    string GetSettingsPath();
}

public interface IHotkeyService
{
    bool IsRegistered { get; }
    string Hotkey { get; }
    event EventHandler HotkeyPressed;
    bool Register(string hotkey);
    void Unregister();
}

public interface ICaptureService
{
    bool IsCapturing { get; }
    event EventHandler<CaptureStatusEventArgs> StatusChanged;
    Task<bool> StartAsync(string source, int width, int height, int fps);
    Task StopAsync();
}

public class CaptureStatusEventArgs : EventArgs
{
    public string Message { get; set; } = string.Empty;
    public bool IsError { get; set; }
}

public interface IReplayBufferService
{
    bool IsRunning { get; }
    TimeSpan BufferDuration { get; }
    Task StartAsync(TimeSpan duration, int fps = 60);
    Task StopAsync();
    Task<string?> SaveReplayAsync(string outputPath);
}

public interface IClipLibraryService
{
    System.Collections.Generic.List<Models.ClipInfo> Clips { get; }
    Task RefreshAsync(string storagePath);
    Task DeleteAsync(string filePath);
    Task RenameAsync(string filePath, string newName);
    System.Collections.Generic.List<string> ImportFiles(System.Collections.Generic.IEnumerable<string> filePaths);
}

public interface IExportService
{
    Task<bool> ExportAsync(string sourcePath, string outputPath, ExportOptions options, CancellationToken cancellationToken = default, IProgress<double>? progress = null);
}

public class ExportOptions
{
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public int Fps { get; set; } = 60;
    public int BitrateKbps { get; set; } = 6000;
    public string VideoCodec { get; set; } = "libx264";
    public string AudioCodec { get; set; } = "aac";
}
