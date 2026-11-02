using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using VulcansReplayStudio2.Core.Services;

namespace VulcansReplayStudio2.App.Views;

public partial class ClipPlayerWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool _isSeeking;
    private bool _isFullscreen;
    private WindowState _previousState;
    private WindowStyle _previousStyle;
    private bool _previousTopmost;
    private TimeSpan _duration = TimeSpan.Zero;
    private readonly string _path;
    private Uri? _sourceUri;

    public ClipPlayerWindow(string path)
    {
        InitializeComponent();
        _path = path;
        _timer.Tick += OnTimerTick;
        Player.Volume = VolumeSlider.Value;
        Loaded += async (_, _) => await OpenAsync();
    }

    private async Task OpenAsync()
    {
        if (!File.Exists(_path))
        {
            StatusText.Text = "The clip file could not be found.";
            return;
        }
        StatusText.Text = Path.GetFileName(_path);
        Title = "Clip Player - " + Path.GetFileName(_path);

        try
        {
            _duration = await Task.Run(() => ProbeDuration(_path));
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not read this clip: " + ex.Message;
            return;
        }

        if (_duration <= TimeSpan.Zero)
        {
            StatusText.Text = "This file could not be played: unsupported or damaged video.";
            return;
        }

        ShowDuration(_duration);
        var playPath = _path;
        try
        {
            playPath = await Task.Run(() => PreparePlaybackFile(_path));
        }
        catch (Exception ex)
        {
            Log("Playback copy failed: " + ex.Message);
            StatusText.Text = "This file could not be prepared for playback: " + ex.Message;
            return;
        }
        Log("Selected file: " + _path);
        Log("Playing: " + playPath);
        _sourceUri = new Uri(playPath, UriKind.Absolute);
        Player.Source = _sourceUri;
        Log("Source set: " + Player.Source);
        PlayPauseButton.Content = "Play";
        _timer.Start();
    }

    private static void Log(string message)
    {
        System.Diagnostics.Trace.WriteLine("[ClipPlayer] " + message);
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VulcansReplayStudio2", "Logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "ClipPlayer.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{Environment.CurrentManagedThreadId}] {message}\r\n");
        }
        catch
        {
            // Diagnostics must never crash playback.
        }
    }

    // Windows H.264 decoding (MediaElement) cannot open 4:4:4 streams, which the Replay Studio recordings use.
    // Such clips get a 4:2:0 playback copy in a cache folder. The original recording is never changed.
    private static string PreparePlaybackFile(string path)
    {
        var ffprobe = FFmpegLocator.Resolve("ffprobe.exe");
        var pixFormat = RunTool(ffprobe, "-v error -select_streams v:0 -show_entries stream=pix_fmt -of default=noprint_wrappers=1:nokey=1 \"" + path + "\"").Trim();
        Log("Pixel format: " + pixFormat);
        if (!pixFormat.StartsWith("yuv444", StringComparison.OrdinalIgnoreCase)) return path;

        var info = new FileInfo(path);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(info.FullName + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks))).Substring(0, 24);
        var cacheDir = Path.Combine(Path.GetTempPath(), "VulcansReplayStudio2", "playback");
        Directory.CreateDirectory(cacheDir);
        var target = Path.Combine(cacheDir, key + ".mp4");
        if (File.Exists(target) && new FileInfo(target).Length > 0)
        {
            Log("Using cached playback copy: " + target);
            return target;
        }

        var temp = target + ".tmp.mp4";
        try
        {
            Log("Creating 4:2:0 playback copy: " + target);
            var ffmpeg = FFmpegLocator.Resolve("ffmpeg.exe");
            RunTool(ffmpeg, "-v error -y -i \"" + path + "\" -c:v libx264 -pix_fmt yuv420p -preset veryfast -crf 20 -c:a aac -b:a 160k -movflags +faststart \"" + temp + "\"");
            File.Move(temp, target, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
        return target;
    }

    private static string RunTool(string exe, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException(Path.GetFileName(exe) + " did not start.");
        var stderrTask = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        var stderr = stderrTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0) throw new InvalidOperationException(Path.GetFileName(exe) + " failed (exit " + process.ExitCode + "): " + stderr.Trim());
        return output;
    }

    private void ShowDuration(TimeSpan duration)
    {
        Timeline.Maximum = duration.TotalSeconds;
        Timeline.IsEnabled = true;
        DurationText.Text = "/ " + FormatTime(duration);
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
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-show_entries");
        psi.ArgumentList.Add("format=duration");
        psi.ArgumentList.Add("-of");
        psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
        psi.ArgumentList.Add(path);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("ffprobe did not start.");
        var stderrTask = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        stderrTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0) return TimeSpan.Zero;

        return double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.Zero;
    }

    private void OnMediaOpened(object sender, RoutedEventArgs e)
    {
        Log(State("MediaOpened") + " duration=" + Player.NaturalDuration);
        if (Player.NaturalDuration.HasTimeSpan && _duration <= TimeSpan.Zero)
        {
            _duration = Player.NaturalDuration.TimeSpan;
            ShowDuration(_duration);
        }
    }

    private string State(string where) => $"{where}: pos={Player.Position:c} source={(Player.Source?.ToString() ?? "null")} lbh={Player.LoadedBehavior} ubh={Player.UnloadedBehavior} ui={PlayPauseButton.Content} size={Player.NaturalVideoWidth}x{Player.NaturalVideoHeight}";

    private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        Log(State("MediaFailed") + " exception=" + e.ErrorException);
        _timer.Stop();
        PlayPauseButton.Content = "Play";
        Timeline.IsEnabled = false;
        StatusText.Text = "This file could not be played: " + (e.ErrorException?.Message ?? "unsupported or damaged video.");
    }

    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        // Reload the source so the next Play starts from the beginning; rewinding a finished
        // MediaElement with Position alone does not restart playback.
        Log(State("MediaEnded before reload"));
        Player.Pause();
        Player.Source = null;
        Player.Source = _sourceUri;
        Log(State("MediaEnded after reload"));
        PlayPauseButton.Content = "Play";
        _timer.Stop();
        CurrentTimeText.Text = FormatTime(TimeSpan.Zero);
        _isSeeking = true;
        Timeline.Value = 0;
        _isSeeking = false;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        CurrentTimeText.Text = FormatTime(Player.Position);
        if (!_isSeeking)
        {
            _isSeeking = true;
            Timeline.Value = Math.Min(Player.Position.TotalSeconds, Timeline.Maximum);
            _isSeeking = false;
        }
    }

    private void OnTimelineValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSeeking || _duration <= TimeSpan.Zero) return;
        Player.Position = TimeSpan.FromSeconds(e.NewValue);
        CurrentTimeText.Text = FormatTime(Player.Position);
    }

    private void OnPlayPauseClick(object sender, RoutedEventArgs e)
    {
        if (Player.Source == null) return;
        if (PlayPauseButton.Content as string == "Play")
        {
            Log(State("Play click before"));
            var resumeAt = Player.Position;
            Player.Play();
            // Play() can restart from zero after a pause; resume from where the user paused.
            if (resumeAt > TimeSpan.Zero) Player.Position = resumeAt;
            _timer.Start();
            PlayPauseButton.Content = "Pause";
            Log(State("Play click after"));
        }
        else
        {
            Log(State("Pause click before"));
            Player.Pause();
            PlayPauseButton.Content = "Play";
            Log(State("Pause click after"));
        }
    }

    private void OnSeekBackClick(object sender, RoutedEventArgs e) => Seek(-5);
    private void OnSeekForwardClick(object sender, RoutedEventArgs e) => Seek(5);

    private void Seek(double seconds)
    {
        if (Player.Source == null || _duration <= TimeSpan.Zero) return;
        var target = Player.Position + TimeSpan.FromSeconds(seconds);
        if (target < TimeSpan.Zero) target = TimeSpan.Zero;
        if (target > _duration) target = _duration;
        Log(State("Seek before") + " target=" + target);
        Player.Position = target;
        Log(State("Seek after"));
    }

    private void OnMuteClick(object sender, RoutedEventArgs e)
    {
        Player.IsMuted = !Player.IsMuted;
        MuteButton.Content = Player.IsMuted ? "Unmute" : "Mute";
    }

    private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Player == null) return;
        Player.Volume = e.NewValue;
    }

    private void OnFullscreenClick(object sender, RoutedEventArgs e) => SetFullscreen(!_isFullscreen);

    private void SetFullscreen(bool enable)
    {
        if (enable)
        {
            _previousState = WindowState;
            _previousStyle = WindowStyle;
            _previousTopmost = Topmost;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            _isFullscreen = true;
        }
        else
        {
            WindowStyle = _previousStyle;
            WindowState = _previousState;
            Topmost = _previousTopmost;
            _isFullscreen = false;
        }
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isFullscreen) SetFullscreen(false);
        else if (e.Key == Key.Space) OnPlayPauseClick(sender, e);
        else if (e.Key == Key.Left) Seek(-5);
        else if (e.Key == Key.Right) Seek(5);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _timer.Stop();
        Player.Stop();
        Player.Source = null;
    }

    private static string FormatTime(TimeSpan t) => t.ToString(@"mm\:ss\.fff");
}
