using System;
using System.IO;

namespace VulcansReplayStudio2.Core.Services;

/// <summary>
/// Single shared resolver for ffmpeg.exe and ffprobe.exe.
/// Resolves relative to the application base directory, not the working directory.
/// </summary>
public static class FFmpegLocator
{
    public static string BaseDirectory => AppContext.BaseDirectory;

    public static string Resolve(string exeName)
    {
        // Check the app folder itself, then Tools\FFmpeg in each ancestor folder
        // (dev layout: bin\Debug\net9.0-windows -> project root\Tools\FFmpeg).
        var dir = new DirectoryInfo(Path.GetFullPath(BaseDirectory));
        for (var depth = 0; depth < 8 && dir != null; depth++, dir = dir.Parent)
        {
            foreach (var candidate in new[] { Path.Combine(dir.FullName, exeName), Path.Combine(dir.FullName, "Tools", "FFmpeg", exeName) })
            {
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
        }
        throw new FileNotFoundException(
            $"{exeName} not found. Searched relative to '{BaseDirectory}' in: Tools\\FFmpeg.");
    }

    public static bool TryResolve(string exeName, out string path)
    {
        try
        {
            path = Resolve(exeName);
            return true;
        }
        catch
        {
            path = string.Empty;
            return false;
        }
    }
}
