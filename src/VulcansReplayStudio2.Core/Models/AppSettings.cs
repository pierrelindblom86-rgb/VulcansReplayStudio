using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VulcansReplayStudio2.Core.Models;

public class AppSettings
{
    public string Version { get; set; } = "2.0.0";
    public int ReplayDurationSeconds { get; set; } = 60;
    public string CaptureSource { get; set; } = "Primary";
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public int Fps { get; set; } = 30;
    public string VideoCodec { get; set; } = "h264";
    public int VideoBitrateKbps { get; set; } = 6000;
    public bool RecordSystemAudio { get; set; } = true;
    public bool RecordMicrophone { get; set; } = false;
    public string StoragePath { get; set; } = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "VulcansReplayStudio2");
    // Destination for newly saved replay clips and initial folder for Import Videos. Empty means StoragePath.
    // Changing it never moves existing recordings or imports.
    public string DefaultClipFolder { get; set; } = string.Empty;
    public string Hotkey { get; set; } = "F9";
    // Videos registered from their original locations. The files are never moved or copied.
    public List<string> ImportedClipPaths { get; set; } = new();
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 800;
    public bool WindowMaximized { get; set; } = false;
    public bool FirstRunCompleted { get; set; } = false;
}
