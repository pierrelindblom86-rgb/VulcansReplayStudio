using System;
using System.Threading;
using System.Threading.Tasks;
using VulcansReplayStudio2.Core.Services;

namespace VulcansReplayStudio2.MediaEngine.Capture;

public class CaptureService : ICaptureService
{
    public bool IsCapturing { get; private set; }
    public event EventHandler<CaptureStatusEventArgs>? StatusChanged;

    public async Task<bool> StartAsync(string source, int width, int height, int fps)
    {
        // Placeholder - will implement actual capture
        StatusChanged?.Invoke(this, new CaptureStatusEventArgs { Message = "Capture not yet implemented - using abstraction", IsError = false });
        IsCapturing = false;
        await Task.CompletedTask;
        return false;
    }

    public async Task StopAsync()
    {
        IsCapturing = false;
        await Task.CompletedTask;
    }
}
