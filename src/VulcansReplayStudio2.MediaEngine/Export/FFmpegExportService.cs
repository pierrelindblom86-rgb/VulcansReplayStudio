using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VulcansReplayStudio2.Core.Services;

namespace VulcansReplayStudio2.MediaEngine.Export;

public class FFmpegExportService : IExportService
{
    private readonly string _ffmpegPath;

    public FFmpegExportService(string? ffmpegPath = null)
    {
        _ffmpegPath = ffmpegPath ?? FindFFmpeg();
    }

    private string FindFFmpeg() => FFmpegLocator.Resolve("ffmpeg.exe");

    public async Task<bool> ExportAsync(string sourcePath, string outputPath, ExportOptions options, CancellationToken cancellationToken = default, IProgress<double>? progress = null)
    {
        try
        {
            if (!File.Exists(sourcePath)) return false;
            var args = $"-i \"{sourcePath}\" -c:v {options.VideoCodec} -b:v {options.BitrateKbps}k -c:a {options.AudioCodec} -y \"{outputPath}\"";
            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            if (process == null) return false;
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0 && File.Exists(outputPath);
        }
        catch
        {
            return false;
        }
    }
}
