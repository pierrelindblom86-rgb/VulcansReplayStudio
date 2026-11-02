using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using VulcansReplayStudio2.Core.Services;

namespace VulcansReplayStudio2.App.Views;

public partial class SettingsView
{
    private readonly ISettingsService _settings;

    public SettingsView()
    {
        InitializeComponent();
        _settings = App.Services.Get<ISettingsService>();
        ReplayDurationBox.Text = _settings.Settings.ReplayDurationSeconds.ToString();
        HotkeyBox.Text = _settings.Settings.Hotkey;
        StorageBox.Text = _settings.Settings.StoragePath;
        DefaultClipFolderBox.Text = _settings.Settings.DefaultClipFolder;
    }

    private void OnSave(object sender, EventArgs e)
    {
        try
        {
            if (int.TryParse(ReplayDurationBox.Text, out var dur) && dur >= 5 && dur <= 600)
                _settings.Settings.ReplayDurationSeconds = dur;
            _settings.Settings.Hotkey = HotkeyBox.Text;
            _settings.Settings.StoragePath = StorageBox.Text;

            var defaultFolder = DefaultClipFolderBox.Text.Trim();
            if (defaultFolder.Length > 0)
            {
                try { Directory.CreateDirectory(defaultFolder); }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Default Clip Folder could not be created: " + ex.Message, "Default Clip Folder", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                if (!ClipFolderResolver.IsUsable(defaultFolder))
                {
                    System.Windows.MessageBox.Show("Default Clip Folder is not writable: " + defaultFolder, "Default Clip Folder", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
            }
            _settings.Settings.DefaultClipFolder = defaultFolder;

            _settings.SaveAsync().GetAwaiter().GetResult();
            Directory.CreateDirectory(_settings.Settings.StoragePath);
            ReturnToDashboard();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Save failed: " + ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void ReturnToDashboard()
    {
        if (Window.GetWindow(this) is MainWindow main) main.NavDashboard_Click(this, EventArgs.Empty);
    }

    private void OnCancel(object sender, EventArgs e)
    {
        ReturnToDashboard();
    }

    private void OnBrowseDefaultClipFolder(object sender, EventArgs e)
    {
        try
        {
            using var dialog = new FolderBrowserDialog();
            dialog.Description = "Select Default Clip Folder";
            dialog.ShowNewFolderButton = true;
            var current = DefaultClipFolderBox.Text.Trim();
            dialog.SelectedPath = Directory.Exists(current) ? current : _settings.Settings.StoragePath;
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                DefaultClipFolderBox.Text = dialog.SelectedPath;
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Browse failed: " + ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void OnBrowseStorage(object sender, EventArgs e)
    {
        try
        {
            using var dialog = new FolderBrowserDialog();
            dialog.Description = "Select storage location";
            dialog.ShowNewFolderButton = true;
            dialog.SelectedPath = _settings.Settings.StoragePath;
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                StorageBox.Text = dialog.SelectedPath;
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Browse failed: " + ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}