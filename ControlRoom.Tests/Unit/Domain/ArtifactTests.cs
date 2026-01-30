using ControlRoom.Domain.Model;

namespace ControlRoom.Tests.Unit.Domain;

public class ArtifactTests
{
    [Fact]
    public void Artifact_RecordEquality_Works()
    {
        var runId = RunId.New();
        var id = ArtifactId.New();
        var at = DateTimeOffset.UtcNow;
        var a1 = new Artifact(id, runId, "text/plain", "file.txt", null, at);
        var a2 = new Artifact(id, runId, "text/plain", "file.txt", null, at);

        a1.Should().Be(a2);
    }
}
