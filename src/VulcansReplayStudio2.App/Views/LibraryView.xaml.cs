using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using VulcansReplayStudio2.Core.Models;
using VulcansReplayStudio2.Core.Services;
using VulcansReplayStudio2.Infrastructure.Services;

namespace VulcansReplayStudio2.App.Views;

public partial class LibraryView
{
    private readonly IClipLibraryService _library;
    private readonly ISettingsService _settings;

    public LibraryView()
    {
        InitializeComponent();
        _library = App.Services.Get<IClipLibraryService>();
        _settings = App.Services.Get<ISettingsService>();
        ReloadLibrary();
    }

    private void ReloadLibrary()
    {
        _library.RefreshAsync(_settings.Settings.StoragePath).GetAwaiter().GetResult();
        RefreshClips();
    }

    private void RefreshClips()
    {
        ClipList.Items.Clear();
        foreach (var clip in _library.Clips)
        {
            var row = new ClipRow
            {
                Name = clip.Name,
                Duration = clip.Duration.ToString(@"mm\:ss"),
                Size = (clip.FileSizeBytes / 1024 / 1024) + "MB",
                Date = clip.Created.ToString("yyyy-MM-dd"),
                FilePath = clip.FilePath
            };
            ClipList.Items.Add(row);
        }
    }

    private static ClipRow? GetClipRow(object sender)
    {
        // Buttons in the Actions cell are not direct children of the DataGridRow
        // (they sit inside a StackPanel), so walk up the visual tree to find the row.
        if (sender is not DependencyObject start) return null;
        var row = FindAncestor<DataGridRow>(start);
        return row?.Item as ClipRow;
    }

    private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(start);
        while (current != null && current is not T)
        {
            current = VisualTreeHelper.GetParent(current);
        }
        return current as T;
    }

    private void OnImportVideos(object sender, EventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Videos",
            Multiselect = true,
            Filter = "Video files|*.mp4;*.mkv;*.mov;*.avi;*.webm|All files|*.*"
        };
        var defaultFolder = ClipFolderResolver.Resolve(_settings.Settings, out _);
        if (Directory.Exists(defaultFolder)) dialog.InitialDirectory = defaultFolder;
        if (dialog.ShowDialog(System.Windows.Window.GetWindow(this)) != true) return;

        try
        {
            var messages = _library.ImportFiles(dialog.FileNames);
            ReloadLibrary();
            if (messages.Count > 0)
            {
                System.Windows.MessageBox.Show(string.Join(Environment.NewLine, messages), "Import Videos",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            ShowMissingImports();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Import failed: " + ex.Message, "Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void OnEditClick(object sender, EventArgs e)
    {
        if (GetClipRow(sender) is ClipRow clip)
        {
            var editor = new ClipEditorWindow(clip.FilePath) { Owner = System.Windows.Window.GetWindow(this) };
            editor.Show();
        }
    }

    private void OnPlayClick(object sender, EventArgs e)
    {
        if (GetClipRow(sender) is ClipRow clip)
        {
            try
            {
                if (!File.Exists(clip.FilePath))
                {
                    System.Windows.MessageBox.Show("The clip file could not be found:" + Environment.NewLine + clip.FilePath,
                        "Playback Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                var player = new ClipPlayerWindow(clip.FilePath) { Owner = System.Windows.Window.GetWindow(this) };
                player.Show();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Cannot play clip: " + ex.Message, "Playback Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
    }

    private void OnRenameClick(object sender, EventArgs e)
    {
        if (GetClipRow(sender) is ClipRow clip)
        {
            var input = InputDialog.Show("Rename Clip", "New name:", clip.Name);
            if (input == null) return;
            try
            {
                _library.RenameAsync(clip.FilePath, input).GetAwaiter().GetResult();
                RefreshClips();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Rename failed: " + ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private void OnDeleteClick(object sender, EventArgs e)
    {
        if (GetClipRow(sender) is ClipRow clip)
        {
            var isImported = _settings.Settings.ImportedClipPaths.Any(p => ClipLibraryService.SamePath(p, clip.FilePath));
            var prompt = isImported
                ? "Remove '" + clip.Name + "' from the Library? The original file will not be deleted."
                : "Delete '" + clip.Name + "'? This cannot be undone.";
            var result = System.Windows.MessageBox.Show(prompt, "Confirm Delete", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (result != System.Windows.MessageBoxResult.Yes) return;
            try
            {
                _library.DeleteAsync(clip.FilePath).GetAwaiter().GetResult();
                RefreshClips();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Delete failed: " + ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private void OnRefresh(object sender, EventArgs e)
    {
        ReloadLibrary();
        ShowMissingImports();
    }

    // Missing imports stay saved in settings (a drive may reconnect); tell the user rather than dropping them silently.
    private void ShowMissingImports()
    {
        var missing = _library is ClipLibraryService svc ? svc.MissingImportedPaths : null;
        if (missing == null || missing.Count == 0) return;
        System.Windows.MessageBox.Show(
            "These imported clips could not be found and are not shown. They remain saved in settings and will reappear if the file returns to this location:"
            + Environment.NewLine + string.Join(Environment.NewLine, missing),
            "Missing Clips", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
    }

    private void OnOpenFolder(object sender, EventArgs e)
    {
        try
        {
            var path = _settings.Settings.StoragePath;
            if (Directory.Exists(path))
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Cannot open folder: " + ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}

public class ClipRow
{
    public string Name { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}
