using ControlRoom.Domain.Model;

namespace ControlRoom.Tests.Unit.Domain;

public class RunEventTests
{
    [Fact]
    public void RunEvent_RecordEquality_Works()
    {
        var runId = RunId.New();
        var at = DateTimeOffset.UtcNow;
        var ev1 = new RunEvent(1, runId, at, EventKind.StdOut, "{}" );
        var ev2 = new RunEvent(1, runId, at, EventKind.StdOut, "{}" );

        ev1.Should().Be(ev2);
    }
}
