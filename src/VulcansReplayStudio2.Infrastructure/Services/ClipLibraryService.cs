using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VulcansReplayStudio2.Core.Models;
using VulcansReplayStudio2.Core.Services;

namespace VulcansReplayStudio2.Infrastructure.Services;

public class ClipLibraryService : IClipLibraryService
{
    private static readonly string[] SupportedExtensions = { ".mp4", ".mkv", ".mov", ".avi", ".webm" };

    private readonly string? _ffprobePath;
    private readonly ISettingsService? _settings;
    private string _storagePath = string.Empty;

    public ClipLibraryService(string? ffprobePath = null, ISettingsService? settings = null)
    {
        _ffprobePath = ffprobePath;
        _settings = settings;
    }

    public List<ClipInfo> Clips { get; private set; } = new();

    // Imported paths whose files no longer exist. They stay in settings so a moved drive can be reconnected; they are not shown as clips.
    public List<string> MissingImportedPaths { get; private set; } = new();

    public static string NormalizePath(string path)
    {
        try { return Path.GetFullPath(path.Trim().Trim('"')); }
        catch { return path.Trim().Trim('"'); }
    }

    // Windows paths are case-insensitive, so comparisons use ordinal-ignore-case on normalized paths.
    public static bool SamePath(string a, string b) =>
        string.Equals(NormalizePath(a), NormalizePath(b), StringComparison.OrdinalIgnoreCase);

    private List<string> ImportedPaths
    {
        get
        {
            if (_settings == null) return new List<string>();
            // Older or hand-edited settings files may omit or null the list; keep the app running.
            _settings.Settings.ImportedClipPaths ??= new List<string>();
            return _settings.Settings.ImportedClipPaths;
        }
    }

    public async Task RefreshAsync(string storagePath)
    {
        _storagePath = storagePath;
        Clips.Clear();
        MissingImportedPaths = new List<string>();
        try
        {
            var files = new List<string>();
            var folders = new List<string> { storagePath };
            var defaultFolder = _settings?.Settings.DefaultClipFolder?.Trim();
            if (!string.IsNullOrEmpty(defaultFolder) && !SamePath(defaultFolder, storagePath))
                folders.Add(defaultFolder);
            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder)) continue;
                foreach (var f in Directory.GetFiles(folder, "*.mp4").Select(NormalizePath))
                    if (!files.Any(x => SamePath(x, f))) files.Add(f);
            }

            foreach (var imported in ImportedPaths.ToList())
            {
                var normalized = NormalizePath(imported);
                if (!File.Exists(normalized))
                {
                    MissingImportedPaths.Add(normalized);
                    continue;
                }
                if (!files.Any(f => SamePath(f, normalized)))
                    files.Add(normalized);
            }

            foreach (var file in files)
            {
                var fi = new FileInfo(file);
                Clips.Add(new ClipInfo
                {
                    FilePath = file,
                    Name = Path.GetFileName(file),
                    Created = fi.CreationTime,
                    FileSizeBytes = fi.Length,
                    Duration = ReadDuration(file)
                });
            }
            Clips.Sort((a, b) => b.Created.CompareTo(a.Created));
        }
        catch { }
        await Task.CompletedTask;
    }

    // Validates each file and registers it from its original location. Originals are never moved, copied or overwritten.
    public List<string> ImportFiles(IEnumerable<string> filePaths)
    {
        var messages = new List<string>();
        if (_settings == null) return messages;
        var imported = ImportedPaths;
        var recordingsDir = string.IsNullOrEmpty(_storagePath) ? null : NormalizePath(_storagePath);
        var batch = new List<string>();

        foreach (var raw in filePaths)
        {
            var path = NormalizePath(raw);
            var name = Path.GetFileName(path);
            if (!File.Exists(path))
            {
                messages.Add($"{name}: file not found.");
                continue;
            }
            if (!SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            {
                messages.Add($"{name}: unsupported file type.");
                continue;
            }
            if (recordingsDir != null && string.Equals(Path.GetDirectoryName(path), recordingsDir, StringComparison.OrdinalIgnoreCase))
            {
                messages.Add($"{name}: already in the library (recordings folder).");
                continue;
            }
            if (imported.Any(p => SamePath(p, path)) || batch.Any(p => SamePath(p, path)))
            {
                messages.Add($"{name}: already in the library.");
                continue;
            }
            if (ReadDuration(path) == TimeSpan.Zero)
            {
                messages.Add($"{name}: could not be read as a video.");
                continue;
            }
            batch.Add(path);
            messages.Add($"{name}: imported.");
        }

        if (batch.Count > 0)
        {
            imported.AddRange(batch);
            _settings.SaveAsync().GetAwaiter().GetResult();
        }
        return messages;
    }

    // Reads the real container duration with FFprobe; unreadable or invalid files report zero instead of failing the list.
    private TimeSpan ReadDuration(string file)
    {
        if (string.IsNullOrEmpty(_ffprobePath)) return TimeSpan.Zero;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-v");
            psi.ArgumentList.Add("error");
            psi.ArgumentList.Add("-show_entries");
            psi.ArgumentList.Add("format=duration");
            psi.ArgumentList.Add("-of");
            psi.ArgumentList.Add("csv=p=0");
            psi.ArgumentList.Add(file);
            using var proc = Process.Start(psi);
            if (proc == null) return TimeSpan.Zero;
            var stderrTask = proc.StandardError.ReadToEndAsync();
            var outputTask = proc.StandardOutput.ReadToEndAsync();
            if (!proc.WaitForExit(10000)) { try { proc.Kill(true); } catch { } return TimeSpan.Zero; }
            var output = outputTask.GetAwaiter().GetResult();
            stderrTask.GetAwaiter().GetResult();
            if (proc.ExitCode != 0) return TimeSpan.Zero;
            return double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : TimeSpan.Zero;
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }

    public async Task DeleteAsync(string filePath)
    {
        try
        {
            // Imported clips are original files outside the recordings folder: remove only the Library entry, never the file.
            if (_settings != null && ImportedPaths.Any(p => SamePath(p, filePath)))
            {
                ImportedPaths.RemoveAll(p => SamePath(p, filePath));
                await _settings.SaveAsync();
                return;
            }
            if (File.Exists(filePath)) File.Delete(filePath);
        }
        catch { }
        await Task.CompletedTask;
    }

    public async Task RenameAsync(string filePath, string newName)
    {
        try
        {
            if (!File.Exists(filePath)) return;
            var dir = Path.GetDirectoryName(filePath)!;
            var ext = Path.GetExtension(filePath);
            var newPath = Path.Combine(dir, newName + ext);
            if (newPath != filePath) File.Move(filePath, newPath);
        }
        catch { }
        await Task.CompletedTask;
    }
}
