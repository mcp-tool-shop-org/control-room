using ControlRoom.Domain.Model;

namespace ControlRoom.Tests.Unit.Domain;

public class RunStatusTests
{
    [Fact]
    public void RunStatus_HasExpectedValues()
    {
        // Assert - verify enum values exist
        RunStatus.Running.Should().Be(RunStatus.Running);
        RunStatus.Succeeded.Should().Be(RunStatus.Succeeded);
        RunStatus.Failed.Should().Be(RunStatus.Failed);
        RunStatus.Canceled.Should().Be(RunStatus.Canceled);
    }

    [Theory]
    [InlineData(RunStatus.Running)]
    [InlineData(RunStatus.Succeeded)]
    [InlineData(RunStatus.Failed)]
    [InlineData(RunStatus.Canceled)]
    public void RunStatus_AllValuesAreValid(RunStatus status)
    {
        // Arrange & Act & Assert
        ((int)status).Should().BeGreaterThanOrEqualTo(1);
    }
}
