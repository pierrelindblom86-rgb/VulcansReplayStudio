using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;
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

    private bool IsImported(string filePath) => ImportedPaths.Any(p => SamePath(p, filePath));

    // Sends a file to the Windows Recycle Bin. Only fixed local drives are allowed, because a drive without a Recycle Bin
    // would delete permanently. Any failure throws and leaves the file in place.
    private static void SendToRecycleBin(string filePath)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(filePath));
        if (string.IsNullOrEmpty(root) || new DriveInfo(root).DriveType != DriveType.Fixed)
            throw new InvalidOperationException("Only files on fixed local drives can be sent to the Recycle Bin. Nothing was deleted.");
        FileSystem.DeleteFile(filePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
        if (File.Exists(filePath))
            throw new IOException("The file could not be sent to the Recycle Bin. Nothing was deleted.");
    }

    // Recorded clips only. Imported clips must use RemoveImportedAsync or DeleteImportedOriginalAsync.
    public Task DeleteAsync(string filePath)
    {
        if (IsImported(filePath))
            throw new InvalidOperationException("This is an imported clip. Use Remove from Library or Delete original file.");
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The clip file no longer exists.", filePath);
        SendToRecycleBin(filePath);
        return Task.CompletedTask;
    }

    // Removes only the Library entry. The original file is never touched.
    public async Task RemoveImportedAsync(string filePath)
    {
        var removed = RemoveImportedEntries(filePath);
        await PersistRemovedEntriesAsync(removed, "The clip is still listed in the Library.");
    }

    // The Library entry is removed only after the original is safely in the Recycle Bin.
    public async Task DeleteImportedOriginalAsync(string filePath)
    {
        if (!IsImported(filePath))
            throw new InvalidOperationException("This clip is not an imported clip.");
        if (File.Exists(filePath)) SendToRecycleBin(filePath);
        var removed = RemoveImportedEntries(filePath);
        await PersistRemovedEntriesAsync(removed, "The original file was sent to the Recycle Bin and can be restored from there.");
    }

    // Removes matching entries from memory. Returns them with their positions so they can be put back if saving fails.
    private List<(int Index, string Path)> RemoveImportedEntries(string filePath)
    {
        var list = ImportedPaths;
        var removed = new List<(int Index, string Path)>();
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (!SamePath(list[i], filePath)) continue;
            removed.Insert(0, (i, list[i]));
            list.RemoveAt(i);
        }
        return removed;
    }

    // Saves the removal. If saving fails, the entries are restored and the error is reported instead of being ignored.
    private async Task PersistRemovedEntriesAsync(List<(int Index, string Path)> removed, string note)
    {
        if (removed.Count == 0 || _settings == null) return;
        try
        {
            await _settings.SaveOrThrowAsync();
        }
        catch (Exception ex)
        {
            var list = ImportedPaths;
            foreach (var (index, path) in removed)
                list.Insert(Math.Min(index, list.Count), path);
            throw new IOException("The Library could not be saved, so the change was cancelled. " + note + " Details: " + ex.Message, ex);
        }
    }

    public async Task RenameAsync(string filePath, string newName)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The clip file no longer exists.", filePath);
        var name = (newName ?? string.Empty).Trim();
        if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("The name is empty or contains characters that are not allowed in file names.");
        var newPath = Path.Combine(Path.GetDirectoryName(filePath)!, name + Path.GetExtension(filePath));
        if (newPath == filePath) return;
        if (!SamePath(newPath, filePath) && File.Exists(newPath))
            throw new IOException("A file named '" + Path.GetFileName(newPath) + "' already exists in this folder.");
        File.Move(filePath, newPath);

        // Imported clips are tracked by path, so the saved entry must follow the file or the clip is lost after a restart.
        var imported = ImportedPaths;
        var index = imported.FindIndex(p => SamePath(p, filePath));
        if (index < 0) return;
        imported[index] = newPath;
        try
        {
            if (_settings != null) await _settings.SaveOrThrowAsync();
        }
        catch (Exception saveEx)
        {
            imported[index] = filePath;
            try
            {
                File.Move(newPath, filePath);
            }
            catch (Exception rollbackEx)
            {
                throw new IOException("The Library could not be saved, and the file could not be renamed back. The file is now '" + Path.GetFileName(newPath) + "' on disk, but the Library still lists '" + Path.GetFileName(filePath) + "'. Rename the file back manually. Save error: " + saveEx.Message + " Rename-back error: " + rollbackEx.Message, rollbackEx);
            }
            throw new IOException("The Library could not be saved, so the rename was undone and the file keeps its name '" + Path.GetFileName(filePath) + "'. Details: " + saveEx.Message, saveEx);
        }
    }
}
