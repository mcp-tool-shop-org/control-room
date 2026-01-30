using System.Text.Json;
using ControlRoom.Domain.Model;
using ControlRoom.Infrastructure.Storage.Queries;

namespace ControlRoom.Tests.Unit.Infrastructure;

public class RunListItemTests
{
    [Fact]
    public void GetParsedSummary_ReturnsSummary_WhenValidJson()
    {
        var summary = new RunSummary(
            Status: RunStatus.Succeeded,
            Duration: TimeSpan.FromSeconds(1),
            StdOutLines: 1,
            StdErrLines: 0,
            ExitCode: 0,
            FailureFingerprint: null,
            LastStdErrLine: null,
            ArtifactCount: 0);

        var json = JsonSerializer.Serialize(summary);
        var item = new RunListItem(RunId.New(), ThingId.New(), "thing", DateTimeOffset.UtcNow, RunStatus.Succeeded, 0, json);

        item.GetParsedSummary().Should().NotBeNull();
        item.GetParsedSummary()!.Status.Should().Be(RunStatus.Succeeded);
    }

    [Fact]
    public void GetParsedSummary_ReturnsNull_OnInvalidJson()
    {
        var item = new RunListItem(RunId.New(), ThingId.New(), "thing", DateTimeOffset.UtcNow, RunStatus.Failed, 1, "{invalid");
        item.GetParsedSummary().Should().BeNull();
    }
}
