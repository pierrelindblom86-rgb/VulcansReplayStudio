using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using VulcansReplayStudio2.Core.Models;
using VulcansReplayStudio2.Core.Services;

namespace VulcansReplayStudio2.App.Views;

public partial class ClipEditorWindow : Window
{
    private readonly ClipEditSession _session = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private bool _isSeeking;
    private bool _isPlaying;
    private int _openGeneration;

    public ClipEditorWindow(string? initialPath = null)
    {
        InitializeComponent();
        _timer.Tick += OnTimerTick;
        Closed += (_, _) => { _timer.Stop(); Player.Close(); };
        if (!string.IsNullOrWhiteSpace(initialPath))
            Loaded += async (_, _) => await OpenAsync(initialPath);
    }

    private async void OnOpenClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open clip",
            Filter = "Video files|*.mp4;*.mkv;*.mov;*.avi;*.webm|All files|*.*"
        };
        if (dialog.ShowDialog(this) == true)
            await OpenAsync(dialog.FileName);
    }

    /// <summary>Probes the duration off the UI thread, then loads the preview.</summary>
    private async Task OpenAsync(string path)
    {
        var generation = ++_openGeneration;
        if (!File.Exists(path))
        {
            SetStatus("The selected file does not exist.", isError: true);
            return;
        }

        // Stop any current playback before the session changes.
        Player.Pause();
        _timer.Stop();
        _isPlaying = false;
        PlayPauseButton.Content = "Play";

        SetStatus("Reading clip information...");
        TimeSpan duration;
        try
        {
            duration = await Task.Run(() => ProbeDuration(path));
        }
        catch (Exception ex)
        {
            if (generation == _openGeneration)
                SetStatus($"Could not read this clip: {ex.Message}", isError: true);
            return;
        }

        // A newer open has started while this probe ran; discard this result.
        if (generation != _openGeneration) return;

        if (duration <= TimeSpan.Zero)
        {
            SetStatus("Could not determine the clip duration. The file may be damaged or unsupported.", isError: true);
            return;
        }

        _session.SourcePath = path;
        _session.SourceDuration = duration;
        _session.ResetTrim();

        Player.Source = new Uri(path, UriKind.Absolute);
        Player.Play();
        Player.Pause();
        Timeline.IsEnabled = true;
        Timeline.Maximum = duration.TotalSeconds;
        Timeline.Value = 0;
        DurationText.Text = "/ " + FormatTime(duration);
        UpdateTrimReadout();
        SetStatus($"Loaded {Path.GetFileName(path)}. The original file is never changed.");
    }

    private static TimeSpan ProbeDuration(string path)
    {
        var ffprobe = FFmpegLocator.Resolve("ffprobe.exe");
        var psi = new ProcessStartInfo
        {
            FileName = ffprobe,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        // Pass the path as a separate argument; no shell string concatenation.
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-show_entries");
        psi.ArgumentList.Add("format=duration");
        psi.ArgumentList.Add("-of");
        psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
        psi.ArgumentList.Add(path);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("ffprobe did not start.");
        // Read stderr concurrently so a full stderr pipe cannot deadlock stdout reading.
        var stderrTask = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        stderrTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0) return TimeSpan.Zero;

        return double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.Zero;
    }

    private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _timer.Stop();
        _isPlaying = false;
        PlayPauseButton.Content = "Play";
        SetStatus("Preview could not play this file. Its format may not be supported by Windows playback.", isError: true);
    }

    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        Player.Pause();
        _timer.Stop();
        _isPlaying = false;
        PlayPauseButton.Content = "Play";
    }

    private void OnPlayPauseClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_session.SourcePath)) return;
        if (_isPlaying)
        {
            Player.Pause();
            _timer.Stop();
            _isPlaying = false;
            PlayPauseButton.Content = "Play";
        }
        else
        {
            Player.Play();
            _timer.Start();
            _isPlaying = true;
            PlayPauseButton.Content = "Pause";
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_isSeeking || !Player.NaturalDuration.HasTimeSpan) return;
        var pos = Player.Position;
        if (pos > _session.SourceDuration) pos = _session.SourceDuration;
        _isSeeking = true;
        Timeline.Value = pos.TotalSeconds;
        _isSeeking = false;
        CurrentTimeText.Text = FormatTime(pos);
    }

    private void OnTimelineValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSeeking || !Timeline.IsEnabled) return;
        var target = TimeSpan.FromSeconds(e.NewValue);
        CurrentTimeText.Text = FormatTime(target);
        if (Player.Source != null && Player.NaturalDuration.HasTimeSpan)
            Player.Position = target;
    }

    private void OnSetInClick(object sender, RoutedEventArgs e)
    {
        if (!RequireSource()) return;
        _session.TrimIn = CurrentPosition();
        UpdateTrimReadout();
    }

    private void OnSetOutClick(object sender, RoutedEventArgs e)
    {
        if (!RequireSource()) return;
        _session.TrimOut = CurrentPosition();
        UpdateTrimReadout();
    }

    private void OnResetTrimClick(object sender, RoutedEventArgs e)
    {
        if (!RequireSource()) return;
        _session.ResetTrim();
        UpdateTrimReadout();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private bool RequireSource()
    {
        if (string.IsNullOrEmpty(_session.SourcePath))
        {
            SetStatus("Open a clip first.", isError: true);
            return false;
        }
        return true;
    }

    private TimeSpan CurrentPosition()
    {
        var pos = Player.Position;
        if (pos < TimeSpan.Zero) pos = TimeSpan.Zero;
        if (pos > _session.SourceDuration) pos = _session.SourceDuration;
        return pos;
    }

    private void UpdateTrimReadout()
    {
        TrimInText.Text = FormatTime(_session.TrimIn);
        TrimOutText.Text = FormatTime(_session.TrimOut);
        var error = _session.Validate();
        if (error == null)
        {
            TrimLengthText.Text = FormatTime(_session.TrimmedDuration);
            SetStatus("Trim range is valid.");
        }
        else
        {
            TrimLengthText.Text = "--";
            SetStatus(error, isError: true);
        }
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError
            ? (System.Windows.Media.Brush)FindResource("ErrorBrush")
            : (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
    }

    private static string FormatTime(TimeSpan t) =>
        $"{(int)t.TotalMinutes:00}:{t.Seconds:00}.{t.Milliseconds:000}";
}
