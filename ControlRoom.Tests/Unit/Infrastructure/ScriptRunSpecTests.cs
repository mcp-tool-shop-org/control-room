using ControlRoom.Infrastructure.Process;

namespace ControlRoom.Tests.Unit.Infrastructure;

public class ScriptRunSpecTests
{
    [Fact]
    public void ScriptRunSpec_WithAllData_CreatesSuccessfully()
    {
        // Arrange & Act
        var spec = new ScriptRunSpec(
            FilePath: "/path/to/script.ps1",
            Arguments: "--arg1 value1",
            WorkingDirectory: "/path/to/work",
            Env: new Dictionary<string, string> { { "VAR", "value" } }
        );

        // Assert
        spec.FilePath.Should().Be("/path/to/script.ps1");
        spec.Arguments.Should().Be("--arg1 value1");
        spec.WorkingDirectory.Should().Be("/path/to/work");
        spec.Env.Should().ContainKey("VAR");
    }

    [Fact]
    public void ScriptRunSpec_WithNullEnvironment_Allows()
    {
        // Arrange & Act
        var spec = new ScriptRunSpec(
            FilePath: "/path/to/script.ps1",
            Arguments: "",
            WorkingDirectory: null,
            Env: null
        );

        // Assert
        spec.Env.Should().BeNull();
        spec.WorkingDirectory.Should().BeNull();
    }

    [Fact]
    public void ScriptRunSpec_IsRecord_SupportsEquality()
    {
        // Arrange
        var spec1 = new ScriptRunSpec("/path/script.ps1", "--arg", "/path", null);
        var spec2 = new ScriptRunSpec("/path/script.ps1", "--arg", "/path", null);

        // Act & Assert
        spec1.Should().Be(spec2);
    }
}
