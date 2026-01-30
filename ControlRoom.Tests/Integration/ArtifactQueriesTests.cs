using ControlRoom.Domain.Model;
using ControlRoom.Infrastructure.Storage.Queries;
using ControlRoom.Tests.Fixtures;

namespace ControlRoom.Tests.Integration;

public class ArtifactQueriesTests
{
    [Fact]
    public void ArtifactQueries_ListAndGet_Works()
    {
        using var dbFixture = new TestDatabaseFixture();
        var db = TestDbHelper.CreateDbWithSchema(dbFixture);

        var config = new ThingConfig { Path = "C:/scripts/test.ps1" }.ToJson();
        var thing = TestDbHelper.CreateThing("test", config);
        TestDbHelper.InsertThing(db, thing);

        var run = TestDbHelper.CreateRun(thing.Id, RunStatus.Succeeded, DateTimeOffset.UtcNow, 0, summaryJson: null);
        TestDbHelper.InsertRun(db, run);

        var tempDir = Path.Combine(Path.GetTempPath(), "controlroom-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "artifact.txt");
        File.WriteAllText(filePath, "data");

        var artifact1 = new Artifact(ArtifactId.New(), run.Id, "text/plain", filePath, null, DateTimeOffset.UtcNow);
        var artifact2 = new Artifact(ArtifactId.New(), run.Id, "text/plain", Path.Combine(tempDir, "missing.txt"), null, DateTimeOffset.UtcNow);

        TestDbHelper.InsertArtifact(db, artifact1);
        TestDbHelper.InsertArtifact(db, artifact2);

        var queries = new ArtifactQueries(db);
        var list = queries.ListArtifactsForRun(run.Id);
        list.Should().HaveCount(2);
        list[0].FileName.Should().Be("artifact.txt");
        list[0].FileSizeBytes.Should().BeGreaterThan(0);
        list[1].FileSizeBytes.Should().BeNull();

        var fetched = queries.GetArtifact(artifact1.Id);
        fetched.Should().NotBeNull();
        fetched!.ArtifactId.Should().Be(artifact1.Id);
        fetched.FileName.Should().Be("artifact.txt");
    }
}
