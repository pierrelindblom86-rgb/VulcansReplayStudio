using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VulcansReplayStudio2.Core.Services;

namespace VulcansReplayStudio2.MediaEngine.Buffer;

public class ReplayBufferService : IReplayBufferService
{
    private readonly string _ffmpegPath;
    private Process? _ffmpegProcess;
    private string? _segmentDir;
    private bool _isRunning;
    public bool IsRunning => _isRunning;
    public TimeSpan BufferDuration { get; private set; } = TimeSpan.FromSeconds(120);

    public ReplayBufferService(string? ffmpegPath = null)
    {
        _ffmpegPath = ffmpegPath ?? FindFFmpeg();
    }

    private string FindFFmpeg() => FFmpegLocator.Resolve("ffmpeg.exe");

    public async Task StartAsync(TimeSpan duration, int fps = 60)
    {
        StopInternal();
        BufferDuration = duration;
        _segmentDir = Path.Combine(Path.GetTempPath(), $"vulcans_buffer_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_segmentDir);
        // Segment every 10 seconds for rolling buffer
        var listFile = Path.Combine(_segmentDir, "list.txt");
        // gdigrab -video_size crops from the top-left instead of scaling, so the full desktop is captured.
        var args = $"-f gdigrab -framerate {fps} -i desktop -c:v libx264 -preset ultrafast -g {fps * 10} -f segment -segment_time 10 -segment_wrap 20 -reset_timestamps 1 -strftime 0 \"{Path.Combine(_segmentDir, "seg_%03d.mp4")}\"";
        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        _ffmpegProcess = Process.Start(psi);
        if (_ffmpegProcess == null) throw new Exception("Failed to start FFmpeg for replay buffer");
        // Drain stderr so FFmpeg never blocks on a full pipe.
        var stderr = _ffmpegProcess.StandardError;
        _ = Task.Run(() => { try { stderr.ReadToEnd(); } catch { } });
        _isRunning = true;
        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        StopInternal();
        await Task.CompletedTask;
    }

    public async Task<string?> SaveReplayAsync(string outputPath)
    {
        try
        {
            if (_segmentDir == null || !Directory.Exists(_segmentDir)) return null;
            // Segment names wrap (seg_000..seg_019), so order by write time, not by name.
            var segments = Directory.GetFiles(_segmentDir, "seg_*.mp4")
                .OrderBy(f => File.GetLastWriteTimeUtc(f)).ToList();
            // The newest segment is still being written by FFmpeg and has no finished MP4 index, so it is excluded.
            if (segments.Count > 1) segments.RemoveAt(segments.Count - 1);
            if (segments.Count == 0) return null;
            // Take last N segments based on duration (10s each)
            var neededSegments = Math.Max(1, (int)Math.Ceiling(BufferDuration.TotalSeconds / 10.0));
            if (neededSegments > segments.Count) neededSegments = segments.Count;
            var selected = segments.Skip(segments.Count - neededSegments).ToList();
            // Unique per save, so overlapping saves do not overwrite each other's list.
            var listFile = Path.Combine(_segmentDir, $"concat_{Guid.NewGuid():N}.txt");
            using (var sw = new StreamWriter(listFile))
            {
                foreach (var seg in selected)
                    sw.WriteLine($"file '{seg.Replace("'", "'\\''")}'");
            }
            var args = $"-f concat -safe 0 -i \"{listFile}\" -c copy -y \"{outputPath}\"";
            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return null;
            var stderrTask = proc.StandardError.ReadToEndAsync();
            // ConfigureAwait(false): the UI thread may be blocked waiting on this save, so never resume on it.
            await proc.WaitForExitAsync().ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            if (proc.ExitCode == 0 && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0) return outputPath;
            Trace.WriteLine($"Replay save failed (exit {proc.ExitCode}): {stderr}");
            return null;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Replay save error: {ex}");
            return null;
        }
    }

    private void StopInternal()
    {
        try
        {
            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
            {
                try { _ffmpegProcess.CloseMainWindow(); } catch { }
                Thread.Sleep(1000);
                if (!_ffmpegProcess.HasExited)
                {
                    try { _ffmpegProcess.Kill(true); } catch { }
                }
            }
        }
        catch { }
        finally
        {
            _ffmpegProcess = null;
            _isRunning = false;
        }
        // Remove this session's temporary segments once FFmpeg has released them.
        if (_segmentDir != null)
        {
            try { Directory.Delete(_segmentDir, true); } catch { }
            _segmentDir = null;
        }
    }
}