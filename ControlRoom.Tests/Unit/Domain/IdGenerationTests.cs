using ControlRoom.Domain.Model;

namespace ControlRoom.Tests.Unit.Domain;

public class IdGenerationTests
{
    [Fact]
    public void RunId_New_GeneratesUniqueIds()
    {
        // Arrange & Act
        var id1 = RunId.New();
        var id2 = RunId.New();

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void ThingId_New_GeneratesUniqueIds()
    {
        // Arrange & Act
        var id1 = ThingId.New();
        var id2 = ThingId.New();

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void ArtifactId_New_GeneratesUniqueIds()
    {
        // Arrange & Act
        var id1 = ArtifactId.New();
        var id2 = ArtifactId.New();

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void RunId_MultipleGenerations_AllUnique()
    {
        // Arrange & Act
        var ids = Enumerable.Range(0, 100).Select(_ => RunId.New()).ToList();

        // Assert
        ids.Distinct().Should().HaveCount(100);
    }
}
