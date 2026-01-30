using ControlRoom.Domain.Model;

namespace ControlRoom.Tests.Unit.Domain;

public class RunSummaryTests
{
    [Fact]
    public void RunSummary_ToDisplayString_IncludesDetails()
    {
        var summary = new RunSummary(
            Status: RunStatus.Succeeded,
            Duration: TimeSpan.FromMilliseconds(450),
            StdOutLines: 3,
            StdErrLines: 1,
            ExitCode: 0,
            FailureFingerprint: null,
            LastStdErrLine: null,
            ArtifactCount: 2);

        var display = summary.ToDisplayString();
        display.Should().Contain("Succeeded");
        display.Should().Contain("ms");
        display.Should().Contain("lines");
        display.Should().Contain("errors");
        display.Should().Contain("artifacts");
    }

    [Theory]
    [InlineData(RunStatus.Succeeded, "✓")]
    [InlineData(RunStatus.Failed, "✗")]
    [InlineData(RunStatus.Canceled, "⊘")]
    [InlineData(RunStatus.Running, "⋯")]
    public void RunSummary_ToCopyableString_UsesStatusIcon(RunStatus status, string icon)
    {
        var summary = new RunSummary(
            Status: status,
            Duration: TimeSpan.FromSeconds(1.2),
            StdOutLines: 1,
            StdErrLines: 0,
            ExitCode: null,
            FailureFingerprint: null,
            LastStdErrLine: null,
            ArtifactCount: 0);

        var line = summary.ToCopyableString("thing", DateTimeOffset.Parse("2024-01-01T12:00:00Z"));
        line.Should().StartWith(icon);
        line.Should().Contain("thing");
        line.Should().Contain("out");
    }
}
