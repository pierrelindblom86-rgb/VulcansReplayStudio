using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VulcansReplayStudio2.Core.Services;

namespace VulcansReplayStudio2.MediaEngine.Capture;

public class FfmpegCaptureService : ICaptureService
{
    public bool IsCapturing { get; private set; }
    public event EventHandler<CaptureStatusEventArgs>? StatusChanged;

    public async Task<bool> StartAsync(string source, int width, int height, int fps)
    {
        // Desktop capture is owned by ReplayBufferService; do not start a second FFmpeg process here.
        IsCapturing = true;
        StatusChanged?.Invoke(this, new CaptureStatusEventArgs { Message = $"Capturing {width}x{height}@{fps}fps (ReplayBuffer)", IsError = false });
        await Task.CompletedTask;
        return true;
    }

    public async Task StopAsync()
    {
        IsCapturing = false;
        StatusChanged?.Invoke(this, new CaptureStatusEventArgs { Message = "Stopped", IsError = false });
        await Task.CompletedTask;
    }
}