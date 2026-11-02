using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using VulcansReplayStudio2.Core.Models;
using VulcansReplayStudio2.Core.Services;

namespace VulcansReplayStudio2.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VulcansReplayStudio2");
    private readonly string _settingsPath;
    public AppSettings Settings { get; private set; } = new();

    public SettingsService()
    {
        Directory.CreateDirectory(_settingsDir);
        _settingsPath = Path.Combine(_settingsDir, "settings.json");
    }

    public string GetSettingsPath() => _settingsPath;

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath).ConfigureAwait(false);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (loaded != null)
                {
                    Settings = loaded;
                    Validate();
                    return;
                }
            }
        }
        catch { }
        // An unreadable file is kept as a timestamped copy so imported paths are not lost when defaults are saved.
        try
        {
            if (File.Exists(_settingsPath))
                File.Copy(_settingsPath, _settingsPath + ".unreadable-" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak", true);
        }
        catch { }
        Settings = new AppSettings();
        await SaveAsync().ConfigureAwait(false);
    }

    private void Validate()
    {
        if (Settings.ReplayDurationSeconds < 5 || Settings.ReplayDurationSeconds > 600)
            Settings.ReplayDurationSeconds = 60;
        if (Settings.Width < 320) Settings.Width = 1920;
        if (Settings.Height < 240) Settings.Height = 1080;
        if (Settings.Fps < 10 || Settings.Fps > 120) Settings.Fps = 60;
        if (string.IsNullOrEmpty(Settings.CaptureSource)) Settings.CaptureSource = "Primary";
        if (string.IsNullOrEmpty(Settings.Hotkey)) Settings.Hotkey = "F9";
        if (string.IsNullOrEmpty(Settings.StoragePath))
            Settings.StoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "VulcansReplayStudio2");
    }

    public async Task SaveAsync()
    {
        try
        {
            await SaveOrThrowAsync().ConfigureAwait(false);
        }
        catch { }
    }

    public async Task SaveOrThrowAsync()
    {
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
        var backup = _settingsPath + ".bak";
        if (File.Exists(_settingsPath))
            File.Copy(_settingsPath, backup, true);
        await File.WriteAllTextAsync(_settingsPath, json).ConfigureAwait(false);
    }
}