using ControlRoom.Domain.Model;
using ControlRoom.Tests.Fixtures;

namespace ControlRoom.Tests.Unit.Domain;

public class RunTests
{
    [Fact]
    public void Run_WithValidData_CreatesSuccessfully()
    {
        // Arrange & Act
        var run = TestDataBuilder.CreateTestRun();

        // Assert
        run.Should().NotBeNull();
        run.Id.Should().NotBe(default);
        run.ThingId.Should().NotBe(default);
        run.Status.Should().Be(RunStatus.Succeeded);
        run.ExitCode.Should().Be(0);
    }

    [Fact]
    public void Run_WithNullExitCode_AllowsFailedStatus()
    {
        // Arrange & Act
        var run = new Run(
            Id: RunId.New(),
            ThingId: ThingId.New(),
            StartedAt: DateTimeOffset.UtcNow,
            EndedAt: null,
            Status: RunStatus.Running,
            ExitCode: null,
            Summary: null
        );

        // Assert
        run.Status.Should().Be(RunStatus.Running);
        run.ExitCode.Should().BeNull();
    }

    [Fact]
    public void Run_IsRecord_SupportsEquality()
    {
        // Arrange
        var id = RunId.New();
        var thingId = ThingId.New();
        var startedAt = DateTimeOffset.UtcNow;

        var run1 = new Run(id, thingId, startedAt, null, RunStatus.Running, null, null);
        var run2 = new Run(id, thingId, startedAt, null, RunStatus.Running, null, null);

        // Act & Assert
        run1.Should().Be(run2);
    }

    [Theory]
    [InlineData(RunStatus.Running)]
    [InlineData(RunStatus.Succeeded)]
    [InlineData(RunStatus.Failed)]
    [InlineData(RunStatus.Canceled)]
    public void Run_WithDifferentStatuses_StoresCorrectly(RunStatus status)
    {
        // Arrange & Act
        var run = TestDataBuilder.CreateTestRun(status: status);

        // Assert
        run.Status.Should().Be(status);
    }
}
