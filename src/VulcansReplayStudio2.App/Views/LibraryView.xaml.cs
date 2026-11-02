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
                FilePath = clip.FilePath,
                IsImported = _settings.Settings.ImportedClipPaths.Any(p => ClipLibraryService.SamePath(p, clip.FilePath))
            };
            ClipList.Items.Add(row);
        }
        EmptyText.Visibility = ClipList.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // Prevents a second destructive action (for example a double-click) from starting while one is in progress.
    private bool _busy;

    private void RunExclusive(Action action)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            action();
        }
        finally
        {
            _busy = false;
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

    private void OnRenameClick(object sender, EventArgs e) => RunExclusive(() =>
    {
        if (GetClipRow(sender) is not ClipRow clip) return;
        var input = InputDialog.Show("Rename Clip", "New name:", clip.Name);
        if (input == null) return;
        try
        {
            _library.RenameAsync(clip.FilePath, input).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Rename failed: " + ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        ReloadLibrary();
    });

    // Recorded clip: sent to the Recycle Bin only.
    private void OnDeleteClick(object sender, EventArgs e) => RunExclusive(() =>
    {
        if (GetClipRow(sender) is not ClipRow clip) return;
        if (!File.Exists(clip.FilePath))
        {
            System.Windows.MessageBox.Show("This clip no longer exists:" + Environment.NewLine + clip.FilePath,
                "Delete Clip", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            ReloadLibrary();
            return;
        }
        var result = System.Windows.MessageBox.Show(
            "Send this clip to the Recycle Bin?" + Environment.NewLine + Environment.NewLine
            + "File: " + Path.GetFileName(clip.FilePath) + Environment.NewLine
            + "Location: " + Path.GetDirectoryName(clip.FilePath) + Environment.NewLine + Environment.NewLine
            + "You can restore it from the Recycle Bin.",
            "Delete Clip", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;
        try
        {
            _library.DeleteAsync(clip.FilePath).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Delete failed. The file was not deleted." + Environment.NewLine + ex.Message, "Delete Clip",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        ReloadLibrary();
    });

    // Imported clip: removes only the Library entry. The original file stays where it is.
    private void OnRemoveImportedClick(object sender, EventArgs e) => RunExclusive(() =>
    {
        if (GetClipRow(sender) is not ClipRow clip) return;
        var result = System.Windows.MessageBox.Show(
            "Remove this clip from the Library?" + Environment.NewLine + Environment.NewLine
            + "File: " + Path.GetFileName(clip.FilePath) + Environment.NewLine + Environment.NewLine
            + "The original file will NOT be deleted.",
            "Remove from Library", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
        if (result != System.Windows.MessageBoxResult.Yes) return;
        try
        {
            _library.RemoveImportedAsync(clip.FilePath).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Remove failed: " + ex.Message, "Remove from Library",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        ReloadLibrary();
    });

    // Imported clip: sends the original file to the Recycle Bin and removes its Library entry.
    private void OnDeleteImportedFileClick(object sender, EventArgs e) => RunExclusive(() =>
    {
        if (GetClipRow(sender) is not ClipRow clip) return;
        if (!File.Exists(clip.FilePath))
        {
            System.Windows.MessageBox.Show("The original file no longer exists:" + Environment.NewLine + clip.FilePath,
                "Delete Original File", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            ReloadLibrary();
            return;
        }
        var result = System.Windows.MessageBox.Show(
            "Send the ORIGINAL video file to the Recycle Bin and remove it from the Library?" + Environment.NewLine + Environment.NewLine
            + "File: " + Path.GetFileName(clip.FilePath) + Environment.NewLine
            + "Location: " + Path.GetDirectoryName(clip.FilePath) + Environment.NewLine + Environment.NewLine
            + "You can restore it from the Recycle Bin.",
            "Delete Original File", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;
        try
        {
            _library.DeleteImportedOriginalAsync(clip.FilePath).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Delete failed. The file was not deleted and the Library entry was kept." + Environment.NewLine + ex.Message,
                "Delete Original File", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        ReloadLibrary();
    });

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
    public bool IsImported { get; set; }
    public Visibility RecordedVisibility => IsImported ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ImportedVisibility => IsImported ? Visibility.Visible : Visibility.Collapsed;
}
