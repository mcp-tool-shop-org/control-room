using Moq;
using ControlRoom.Infrastructure.Process;

namespace ControlRoom.Tests.Fixtures;

/// <summary>
/// Factory for creating mock IScriptRunner instances with common configurations.
/// </summary>
public sealed class MockScriptRunnerFixture
{
    /// <summary>
    /// Create a mock that simulates successful script execution
    /// </summary>
    public static Mock<IScriptRunner> CreateSuccessfulRunner(
        int exitCode = 0,
        string? stdoutOutput = null,
        string? stderrOutput = null)
    {
        var mock = new Mock<IScriptRunner>();

        mock.Setup(r => r.RunAsync(It.IsAny<ScriptRunSpec>(), It.IsAny<Func<bool, string, Task>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScriptRunSpec spec, Func<bool, string, Task> onLine, CancellationToken ct) =>
            {
                // Simulate output callbacks
                if (stdoutOutput != null)
                {
                    foreach (var line in stdoutOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        onLine(false, line).Wait();
                    }
                }
                if (stderrOutput != null)
                {
                    foreach (var line in stderrOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        onLine(true, line).Wait();
                    }
                }

                return new ScriptRunResult(ExitCode: exitCode, WasCanceled: false, ResolvedCommandLine: spec.FilePath);
            });

        return mock;
    }

    /// <summary>
    /// Create a mock that simulates script failure (non-zero exit code)
    /// </summary>
    public static Mock<IScriptRunner> CreateFailingRunner(
        int exitCode = 1,
        string errorMessage = "Script failed")
    {
        return CreateSuccessfulRunner(exitCode: exitCode, stderrOutput: errorMessage);
    }

    /// <summary>
    /// Create a mock that simulates cancellation
    /// </summary>
    public static Mock<IScriptRunner> CreateCanceledRunner()
    {
        var mock = new Mock<IScriptRunner>();

        mock.Setup(r => r.RunAsync(It.IsAny<ScriptRunSpec>(), It.IsAny<Func<bool, string, Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        return mock;
    }
}
