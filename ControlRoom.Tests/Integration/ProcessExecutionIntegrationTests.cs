namespace ControlRoom.Tests.Integration;

/// <summary>
/// Integration tests for process execution with real scripts.
/// These tests verify that the script execution infrastructure works correctly.
/// </summary>
public class ProcessExecutionIntegrationTests
{
    [Fact]
    public async Task ScriptRunner_ExecutesPowerShellScript()
    {
        if (!IsExecutableAvailable("pwsh"))
        {
            // Skip on environments without PowerShell
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "controlroom-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var scriptPath = Path.Combine(tempDir, "hello.ps1");
        await File.WriteAllTextAsync(scriptPath, "Write-Output 'hello-from-test'\n");

        var runner = new ControlRoom.Infrastructure.Process.ScriptRunner();
        var spec = new ControlRoom.Infrastructure.Process.ScriptRunSpec(
            FilePath: scriptPath,
            Arguments: "",
            WorkingDirectory: tempDir,
            Env: null);

        var stdout = new List<string>();
        var stderr = new List<string>();

        var result = await runner.RunAsync(
            spec,
            (isErr, line) =>
            {
                if (isErr) stderr.Add(line);
                else stdout.Add(line);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        result.ExitCode.Should().Be(0);
        stdout.Should().Contain("hello-from-test");
        stderr.Should().BeEmpty();
    }

    private static bool IsExecutableAvailable(string exe)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), exe + ".exe");
                if (File.Exists(candidate))
                    return true;
            }
            catch
            {
                // Ignore malformed PATH entries
            }
        }

        return false;
    }
}


