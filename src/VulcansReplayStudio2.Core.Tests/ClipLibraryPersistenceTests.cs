using System;
using System.IO;
using System.Threading.Tasks;
using VulcansReplayStudio2.Core.Models;
using VulcansReplayStudio2.Core.Services;
using VulcansReplayStudio2.Infrastructure.Services;
using Xunit;

namespace VulcansReplayStudio2.Core.Tests;

// Uses a temp folder and an in-memory settings service only. Nothing touches the real settings file or the Recycle Bin.
public class ClipLibraryPersistenceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vrs_persist_" + Guid.NewGuid().ToString("N"));

    public ClipLibraryPersistenceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private string MakeFile(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "clip");
        return path;
    }

    private static (ClipLibraryService Service, FakeSettings Settings) CreateWithImported(string importedPath)
    {
        var settings = new FakeSettings();
        settings.Settings.ImportedClipPaths.Add(importedPath);
        return (new ClipLibraryService(null, settings), settings);
    }

    [Fact]
    public async Task RenameImported_SavesNewPathToLibrary()
    {
        var original = MakeFile("import.mp4");
        var (service, settings) = CreateWithImported(original);
        var renamed = Path.Combine(_dir, "renamed.mp4");

        await service.RenameAsync(original, "renamed");

        Assert.True(File.Exists(renamed));
        Assert.False(File.Exists(original));
        Assert.Equal(new[] { renamed }, settings.Settings.ImportedClipPaths);
        Assert.Equal(1, settings.SaveCount);
    }

    [Fact]
    public async Task RenameImported_WhenSaveFails_RestoresFileNameAndEntry()
    {
        var original = MakeFile("import.mp4");
        var (service, settings) = CreateWithImported(original);
        settings.FailSaves = true;

        var ex = await Assert.ThrowsAsync<IOException>(() => service.RenameAsync(original, "renamed"));

        Assert.Contains("keeps its name", ex.Message);
        Assert.True(File.Exists(original));
        Assert.False(File.Exists(Path.Combine(_dir, "renamed.mp4")));
        Assert.Equal(new[] { original }, settings.Settings.ImportedClipPaths);
    }

    [Fact]
    public async Task RemoveImported_SavesRemovalToLibrary_AndKeepsFile()
    {
        var original = MakeFile("import.mp4");
        var (service, settings) = CreateWithImported(original);

        await service.RemoveImportedAsync(original);

        Assert.Empty(settings.Settings.ImportedClipPaths);
        Assert.True(File.Exists(original));
        Assert.Equal(1, settings.SaveCount);
    }

    [Fact]
    public async Task RemoveImported_WhenSaveFails_RestoresEntryAndReportsError()
    {
        var original = MakeFile("import.mp4");
        var (service, settings) = CreateWithImported(original);
        settings.FailSaves = true;

        var ex = await Assert.ThrowsAsync<IOException>(() => service.RemoveImportedAsync(original));

        Assert.Contains("could not be saved", ex.Message);
        Assert.Contains("Simulated disk failure", ex.Message);
        Assert.Equal(new[] { original }, settings.Settings.ImportedClipPaths);
        Assert.True(File.Exists(original));
    }

    private sealed class FakeSettings : ISettingsService
    {
        public AppSettings Settings { get; } = new();
        public bool FailSaves { get; set; }
        public int SaveCount { get; private set; }

        public Task LoadAsync() => Task.CompletedTask;

        public Task SaveAsync()
        {
            if (!FailSaves) SaveCount++;
            return Task.CompletedTask;
        }

        public Task SaveOrThrowAsync()
        {
            if (FailSaves) throw new IOException("Simulated disk failure.");
            SaveCount++;
            return Task.CompletedTask;
        }

        public string GetSettingsPath() => "fake-settings.json";
    }
}
