using System;

namespace VulcansReplayStudio2.Core.Models;

/// <summary>
/// Non-destructive edit state for one source clip. Holds edit choices only;
/// the source file is never modified.
/// </summary>
public sealed class ClipEditSession
{
    public string SourcePath { get; set; } = string.Empty;
    public TimeSpan SourceDuration { get; set; } = TimeSpan.Zero;
    public TimeSpan TrimIn { get; set; } = TimeSpan.Zero;
    public TimeSpan TrimOut { get; set; } = TimeSpan.Zero;

    public TimeSpan TrimmedDuration => TrimOut - TrimIn;

    public void ResetTrim()
    {
        TrimIn = TimeSpan.Zero;
        TrimOut = SourceDuration;
    }

    /// <summary>Returns a user-facing validation message, or null when the session is valid.</summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(SourcePath)) return "No clip is open.";
        if (SourceDuration <= TimeSpan.Zero) return "The clip duration is not known yet.";
        if (TrimIn < TimeSpan.Zero) return "Trim-in cannot be negative.";
        if (TrimOut > SourceDuration) return "Trim-out is past the end of the clip.";
        if (TrimOut <= TrimIn) return "Trim-out must be after trim-in.";
        return null;
    }
}
