using ControlRoom.Infrastructure.Process;

namespace ControlRoom.Tests.Unit.Infrastructure;

public class ScriptRunResultTests
{
    [Fact]
    public void ScriptRunResult_SuccessfulRun_CreatesCorrectly()
    {
        // Arrange & Act
        var result = new ScriptRunResult(
            ExitCode: 0,
            WasCanceled: false,
            ResolvedCommandLine: "powershell.exe -File script.ps1 --arg1 value1"
        );

        // Assert
        result.ExitCode.Should().Be(0);
        result.WasCanceled.Should().BeFalse();
        result.ResolvedCommandLine.Should().StartWith("powershell.exe");
    }

    [Fact]
    public void ScriptRunResult_FailedRun_HasNonZeroExitCode()
    {
        // Arrange & Act
        var result = new ScriptRunResult(
            ExitCode: 127,
            WasCanceled: false
        );

        // Assert
        result.ExitCode.Should().NotBe(0);
        result.WasCanceled.Should().BeFalse();
    }

    [Fact]
    public void ScriptRunResult_CanceledRun_FlagsCorrectly()
    {
        // Arrange & Act
        var result = new ScriptRunResult(
            ExitCode: null,
            WasCanceled: true
        );

        // Assert
        result.WasCanceled.Should().BeTrue();
        result.ExitCode.Should().BeNull();
    }

    [Fact]
    public void ScriptRunResult_WithNullExitCode_AllowsWhenCanceled()
    {
        // Arrange & Act
        var result = new ScriptRunResult(null, true);

        // Assert
        result.Should().NotBeNull();
        result.ExitCode.Should().BeNull();
    }
}
