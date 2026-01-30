using Moq;
using Microsoft.Data.Sqlite;
using ControlRoom.Domain.Model;
using ControlRoom.Application.UseCases;
using ControlRoom.Infrastructure.Storage;
using ControlRoom.Tests.Fixtures;

namespace ControlRoom.Tests.Unit.Application;

public class RunLocalScriptTests
{
    [Fact]
    public async Task ExecuteAsync_PersistsRunAndSummary()
    {
        using var dbFixture = new TestDatabaseFixture();
        var (db, thing) = CreateDbAndThing(dbFixture);
        var runnerMock = MockScriptRunnerFixture.CreateSuccessfulRunner(stdoutOutput: "ok");
        var useCase = new RunLocalScript(db, runnerMock.Object);

        var runId = await useCase.ExecuteAsync(thing, args: "", CancellationToken.None);

        runId.Should().NotBe(default);
        runnerMock.Verify(
            r => r.RunAsync(It.IsAny<ControlRoom.Infrastructure.Process.ScriptRunSpec>(), It.IsAny<Func<bool, string, Task>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var (status, exitCode, summary) = GetRunSummary(db, runId);
        status.Should().Be((int)RunStatus.Succeeded);
        exitCode.Should().Be(0);
        summary.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_FailedExitCode_PersistsFailedStatus()
    {
        using var dbFixture = new TestDatabaseFixture();
        var (db, thing) = CreateDbAndThing(dbFixture);

        var runnerMock = MockScriptRunnerFixture.CreateFailingRunner(exitCode: 2, errorMessage: "boom");
        var useCase = new RunLocalScript(db, runnerMock.Object);

        var runId = await useCase.ExecuteAsync(thing, args: "", CancellationToken.None);

        var (status, exitCode, summary) = GetRunSummary(db, runId);
        status.Should().Be((int)RunStatus.Failed);
        exitCode.Should().Be(2);
        summary.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_CanceledRun_PersistsCanceledStatus()
    {
        using var dbFixture = new TestDatabaseFixture();
        var (db, thing) = CreateDbAndThing(dbFixture);

        var runnerMock = new Mock<ControlRoom.Infrastructure.Process.IScriptRunner>();
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ControlRoom.Infrastructure.Process.ScriptRunSpec>(), It.IsAny<Func<bool, string, Task>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ControlRoom.Infrastructure.Process.ScriptRunResult(ExitCode: null, WasCanceled: true));

        var useCase = new RunLocalScript(db, runnerMock.Object);
        var runId = await useCase.ExecuteAsync(thing, args: "", CancellationToken.None);

        var (status, exitCode, summary) = GetRunSummary(db, runId);
        status.Should().Be((int)RunStatus.Canceled);
        exitCode.Should().BeNull();
        summary.Should().NotBeNullOrWhiteSpace();
    }

    private static (Db db, Thing thing) CreateDbAndThing(TestDatabaseFixture dbFixture)
    {
        var db = dbFixture.CreateDb();

        var schemaSql = LoadSchemaSql();
        var migrator = new Migrator(db);
        migrator.EnsureCreated(schemaSql);

        var tempDir = Path.Combine(Path.GetTempPath(), "controlroom-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var scriptPath = Path.Combine(tempDir, "test.ps1");
        File.WriteAllText(scriptPath, "Write-Output 'ok'\n");

        var config = new ThingConfig
        {
            Path = scriptPath,
            Profiles = [ThingProfile.Default]
        };

        var thing = new Thing(
            Id: ThingId.New(),
            Name: "test-script",
            Kind: ThingKind.LocalScript,
            ConfigJson: config.ToJson(),
            CreatedAt: DateTimeOffset.UtcNow);

        InsertThing(db, thing);
        return (db, thing);
    }

    private static void InsertThing(Db db, Thing thing)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO things(thing_id, name, kind, config_json, created_at)
            VALUES ($id, $name, $kind, $config, $created_at)
            """;
        cmd.Parameters.AddWithValue("$id", thing.Id.ToString());
        cmd.Parameters.AddWithValue("$name", thing.Name);
        cmd.Parameters.AddWithValue("$kind", (int)thing.Kind);
        cmd.Parameters.AddWithValue("$config", thing.ConfigJson);
        cmd.Parameters.AddWithValue("$created_at", thing.CreatedAt.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    private static (int status, int? exitCode, string? summary) GetRunSummary(Db db, RunId runId)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT status, exit_code, summary
            FROM runs
            WHERE run_id = $run_id
            """;
        cmd.Parameters.AddWithValue("$run_id", runId.ToString());
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException("Run not found in database");

        var status = reader.GetInt32(0);
        var exitCode = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
        var summary = reader.IsDBNull(2) ? null : reader.GetString(2);
        return (status, exitCode, summary);
    }

    private static string LoadSchemaSql()
    {
        var schemaPath = FindUpwards("ControlRoom.Infrastructure", "Storage", "Schema.sql");
        return File.ReadAllText(schemaPath);
    }

    private static string FindUpwards(params string[] parts)
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            var candidate = Path.Combine(new[] { current }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;

            current = Directory.GetParent(current)?.FullName;
        }

        throw new FileNotFoundException("Could not locate Schema.sql", string.Join(Path.DirectorySeparatorChar, parts));
    }
}
