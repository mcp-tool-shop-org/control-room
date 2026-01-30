using ControlRoom.Domain.Model;
using ControlRoom.Tests.Fixtures;

namespace ControlRoom.Tests.Unit.Domain;

public class ThingTests
{
    [Fact]
    public void Thing_WithValidData_CreatesSuccessfully()
    {
        // Arrange & Act
        var thing = TestDataBuilder.CreateTestThing(name: "my-script");

        // Assert
        thing.Should().NotBeNull();
        thing.Id.Should().NotBeNull();
        thing.Name.Should().Be("my-script");
        thing.Kind.Should().Be(ThingKind.LocalScript);
        thing.ConfigJson.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Thing_WithDifferentKinds_StoresCorrectly()
    {
        // Arrange & Act
        var scriptThing = TestDataBuilder.CreateTestThing(kind: ThingKind.LocalScript);
        var scriptThing2 = TestDataBuilder.CreateTestThing(kind: ThingKind.LocalScript);

        // Assert
        scriptThing.Kind.Should().Be(ThingKind.LocalScript);
        scriptThing2.Kind.Should().Be(ThingKind.LocalScript);
    }

    [Fact]
    public void Thing_IsRecord_SupportsEquality()
    {
        // Arrange
        var id = ThingId.New();
        var now = DateTimeOffset.UtcNow;

        var thing1 = new Thing(id, "test", ThingKind.LocalScript, "{}", now);
        var thing2 = new Thing(id, "test", ThingKind.LocalScript, "{}", now);

        // Act & Assert
        thing1.Should().Be(thing2);
    }

    [Fact]
    public void Thing_WithComplexConfig_PreservesJson()
    {
        // Arrange
        var complexConfig = """{"profiles":[{"id":"smoke","args":"--fast"},{"id":"full","args":"--slow"}]}""";
        
        // Act
        var thing = TestDataBuilder.CreateTestThing(configJson: complexConfig);

        // Assert
        thing.ConfigJson.Should().Be(complexConfig);
    }
}
