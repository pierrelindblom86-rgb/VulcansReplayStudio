using System;

namespace VulcansReplayStudio2.Core.Models;

public class ClipInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FilePath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime Created { get; set; } = DateTime.Now;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public long FileSizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
