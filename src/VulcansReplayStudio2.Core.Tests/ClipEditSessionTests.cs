using VulcansReplayStudio2.Core.Models;
using Xunit;

namespace VulcansReplayStudio2.Core.Tests;

public class ClipEditSessionTests
{
    private static ClipEditSession OpenSession(double durationSeconds = 60)
    {
        return new ClipEditSession
        {
            SourcePath = @"C:\Videos\Replay_test.mp4",
            SourceDuration = TimeSpan.FromSeconds(durationSeconds)
        };
    }

    // ---- Default trim values -------------------------------------------------

    [Fact]
    public void NewSession_DefaultsToEmptySourceAndZeroTimes()
    {
        var session = new ClipEditSession();

        Assert.Equal(string.Empty, session.SourcePath);
        Assert.Equal(TimeSpan.Zero, session.SourceDuration);
        Assert.Equal(TimeSpan.Zero, session.TrimIn);
        Assert.Equal(TimeSpan.Zero, session.TrimOut);
    }

    [Fact]
    public void ResetTrim_SetsTrimInToZeroAndTrimOutToFullDuration()
    {
        var session = OpenSession(60);
        session.TrimIn = TimeSpan.FromSeconds(10);
        session.TrimOut = TimeSpan.FromSeconds(20);

        session.ResetTrim();

        Assert.Equal(TimeSpan.Zero, session.TrimIn);
        Assert.Equal(TimeSpan.FromSeconds(60), session.TrimOut);
        Assert.Null(session.Validate());
    }

    [Fact]
    public void ResetTrim_OnFreshSession_KeepsFullDurationValid()
    {
        var session = OpenSession(30);

        session.ResetTrim();

        Assert.Equal(TimeSpan.FromSeconds(30), session.TrimmedDuration);
        Assert.Null(session.Validate());
    }

    // ---- Valid trim ranges ---------------------------------------------------

    [Fact]
    public void Validate_ReturnsNull_ForFullClipRange()
    {
        var session = OpenSession(60);
        session.TrimIn = TimeSpan.Zero;
        session.TrimOut = TimeSpan.FromSeconds(60);

        Assert.Null(session.Validate());
    }

    [Fact]
    public void Validate_ReturnsNull_ForInteriorRange()
    {
        var session = OpenSession(60);
        session.TrimIn = TimeSpan.FromSeconds(5);
        session.TrimOut = TimeSpan.FromSeconds(25);

        Assert.Null(session.Validate());
        Assert.Equal(TimeSpan.FromSeconds(20), session.TrimmedDuration);
    }

    [Fact]
    public void TrimmedDuration_EqualsTrimOutMinusTrimIn()
    {
        var session = OpenSession(60);
        session.TrimIn = TimeSpan.FromMilliseconds(1500);
        session.TrimOut = TimeSpan.FromMilliseconds(4250);

        Assert.Equal(TimeSpan.FromMilliseconds(2750), session.TrimmedDuration);
    }

    // ---- Start time below zero ----------------------------------------------

    [Fact]
    public void Validate_ReturnsNegativeTrimInMessage_WhenTrimInIsBelowZero()
    {
        var session = OpenSession(60);
        session.TrimIn = TimeSpan.FromMilliseconds(-1);
        session.TrimOut = TimeSpan.FromSeconds(30);

        Assert.Equal("Trim-in cannot be negative.", session.Validate());
    }

    // ---- End time beyond clip duration --------------------------------------

    [Fact]
    public void Validate_ReturnsPastEndMessage_WhenTrimOutExceedsDuration()
    {
        var session = OpenSession(60);
        session.TrimIn = TimeSpan.Zero;
        session.TrimOut = TimeSpan.FromSeconds(60.001);

        Assert.Equal("Trim-out is past the end of the clip.", session.Validate());
    }

    // ---- Start equals end ----------------------------------------------------

    [Fact]
    public void Validate_ReturnsAfterMessage_WhenTrimInEqualsTrimOut()
    {
        var session = OpenSession(60);
        session.TrimIn = TimeSpan.FromSeconds(12);
        session.TrimOut = TimeSpan.FromSeconds(12);

        Assert.Equal("Trim-out must be after trim-in.", session.Validate());
    }

    // ---- Start greater than end ---------------------------------------------

    [Fact]
    public void Validate_ReturnsAfterMessage_WhenTrimInIsGreaterThanTrimOut()
    {
        var session = OpenSession(60);
        session.TrimIn = TimeSpan.FromSeconds(30);
        session.TrimOut = TimeSpan.FromSeconds(10);

        Assert.Equal("Trim-out must be after trim-in.", session.Validate());
    }

    // ---- Zero-duration and missing source -----------------------------------

    [Fact]
    public void Validate_ReturnsNoClipMessage_WhenSourcePathIsEmpty()
    {
        var session = new ClipEditSession
        {
            SourcePath = string.Empty,
            SourceDuration = TimeSpan.FromSeconds(60),
            TrimOut = TimeSpan.FromSeconds(60)
        };

        Assert.Equal("No clip is open.", session.Validate());
    }

    [Fact]
    public void Validate_ReturnsNoClipMessage_WhenSourcePathIsWhitespace()
    {
        var session = OpenSession(60);
        session.SourcePath = "   ";
        session.TrimOut = TimeSpan.FromSeconds(60);

        Assert.Equal("No clip is open.", session.Validate());
    }

    [Fact]
    public void Validate_ReturnsDurationUnknownMessage_ForZeroDurationClip()
    {
        var session = OpenSession(0);
        session.TrimIn = TimeSpan.Zero;
        session.TrimOut = TimeSpan.Zero;

        Assert.Equal("The clip duration is not known yet.", session.Validate());
    }

    [Fact]
    public void ResetTrim_OnZeroDurationClip_LeavesSessionInvalid()
    {
        var session = OpenSession(0);

        session.ResetTrim();

        Assert.Equal(TimeSpan.Zero, session.TrimOut);
        Assert.Equal("The clip duration is not known yet.", session.Validate());
    }

    [Fact]
    public void Validate_ReturnsDurationUnknownMessage_WhenDurationIsNegative()
    {
        var session = OpenSession(-5);
        session.TrimIn = TimeSpan.Zero;
        session.TrimOut = TimeSpan.Zero;

        Assert.Equal("The clip duration is not known yet.", session.Validate());
    }

    // ---- Very short clips ----------------------------------------------------

    [Fact]
    public void Validate_ReturnsNull_ForOneMillisecondClipFullRange()
    {
        var session = new ClipEditSession
        {
            SourcePath = @"C:\Videos\tiny.mp4",
            SourceDuration = TimeSpan.FromMilliseconds(1)
        };
        session.ResetTrim();

        Assert.Null(session.Validate());
        Assert.Equal(TimeSpan.FromMilliseconds(1), session.TrimmedDuration);
    }

    [Fact]
    public void Validate_ReturnsNull_ForOneTickClip()
    {
        var session = new ClipEditSession
        {
            SourcePath = @"C:\Videos\tick.mp4",
            SourceDuration = TimeSpan.FromTicks(1)
        };
        session.ResetTrim();

        Assert.Null(session.Validate());
    }

    [Fact]
    public void Validate_ReturnsNull_ForTwoSecondClipSubrange()
    {
        var session = OpenSession(2);
        session.TrimIn = TimeSpan.FromSeconds(0.5);
        session.TrimOut = TimeSpan.FromSeconds(1.5);

        Assert.Null(session.Validate());
        Assert.Equal(TimeSpan.FromSeconds(1), session.TrimmedDuration);
    }

    // ---- Boundary values at zero and duration -------------------------------

    [Fact]
    public void Validate_AcceptsTrimInExactlyZero()
    {
        var session = OpenSession(60);
        session.TrimIn = TimeSpan.Zero;
        session.TrimOut = TimeSpan.FromMilliseconds(1);

        Assert.Null(session.Validate());
    }

    [Fact]
    public void Validate_AcceptsTrimOutExactlyAtDuration()
    {
        var session = OpenSession(60);
        session.TrimIn = TimeSpan.FromSeconds(59);
        session.TrimOut = TimeSpan.FromSeconds(60);

        Assert.Null(session.Validate());
    }

    [Fact]
    public void Validate_RejectsTrimOutOneTickPastDuration()
    {
        var session = OpenSession(60);
        session.TrimIn = TimeSpan.Zero;
        session.TrimOut = TimeSpan.FromSeconds(60) + TimeSpan.FromTicks(1);

        Assert.Equal("Trim-out is past the end of the clip.", session.Validate());
    }

    [Fact]
    public void Validate_RejectsTrimInOneTickBelowZero()
    {
        var session = OpenSession(60);
        session.TrimIn = TimeSpan.FromTicks(-1);
        session.TrimOut = TimeSpan.FromSeconds(10);

        Assert.Equal("Trim-in cannot be negative.", session.Validate());
    }

    [Fact]
    public void Validate_RejectsTrimInOneTickAfterTrimOut()
    {
        var session = OpenSession(60);
        session.TrimIn = TimeSpan.FromSeconds(10) + TimeSpan.FromTicks(1);
        session.TrimOut = TimeSpan.FromSeconds(10);

        Assert.Equal("Trim-out must be after trim-in.", session.Validate());
    }

    // ---- Rule precedence ----------------------------------------------------

    [Fact]
    public void Validate_ReportsNegativeTrimIn_BeforeOtherRangeErrors()
    {
        // Negative trim-in and trim-out past the end: the negative check runs first.
        var session = OpenSession(60);
        session.TrimIn = TimeSpan.FromSeconds(-1);
        session.TrimOut = TimeSpan.FromSeconds(100);

        Assert.Equal("Trim-in cannot be negative.", session.Validate());
    }

    [Fact]
    public void Validate_ReportsPastEnd_BeforeOrderError()
    {
        // Trim-out past the end while also before trim-in: past-end check runs first.
        var session = OpenSession(60);
        session.TrimIn = TimeSpan.FromSeconds(70);
        session.TrimOut = TimeSpan.FromSeconds(65);

        Assert.Equal("Trim-out is past the end of the clip.", session.Validate());
    }

    // ---- Derived values -----------------------------------------------------

    [Fact]
    public void TrimmedDuration_IsZeroWhenDefaultsAreUsed()
    {
        var session = OpenSession(60);

        Assert.Equal(TimeSpan.Zero, session.TrimmedDuration);
    }
}
