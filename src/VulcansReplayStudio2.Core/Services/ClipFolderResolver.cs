using System;
using System.IO;
using VulcansReplayStudio2.Core.Models;

namespace VulcansReplayStudio2.Core.Services;

// Chooses where new replay clips are saved and where Import Videos starts.
// The folder is only used when it exists and is writable; otherwise StoragePath is used.
public static class ClipFolderResolver
{
    public static string Resolve(AppSettings settings, out string? warning)
    {
        warning = null;
        var preferred = settings.DefaultClipFolder?.Trim();
        if (string.IsNullOrEmpty(preferred)) return settings.StoragePath;

        if (IsUsable(preferred)) return preferred;

        warning = "The Default Clip Folder \"" + preferred + "\" is missing or not writable. Clips were saved to " + settings.StoragePath + " instead.";
        return settings.StoragePath;
    }

    public static bool IsUsable(string folder)
    {
        try
        {
            if (!Directory.Exists(folder)) return false;
            var probe = Path.Combine(folder, ".vrs_write_test_" + Guid.NewGuid().ToString("N"));
            using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
