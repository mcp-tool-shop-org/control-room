using System.Text.Json;
using ControlRoom.Domain.Model;
using ControlRoom.Infrastructure.Storage;

namespace ControlRoom.Tests.Fixtures;

public static class TestDbHelper
{
    public static Db CreateDbWithSchema(TestDatabaseFixture fixture)
    {
        var db = fixture.CreateDb();
        var schemaSql = LoadSchemaSql();
        var migrator = new Migrator(db);
        migrator.EnsureCreated(schemaSql);
        return db;
    }

    public static Thing CreateThing(string name, string configJson)
    {
        return new Thing(
            Id: ThingId.New(),
            Name: name,
            Kind: ThingKind.LocalScript,
            ConfigJson: configJson,
            CreatedAt: DateTimeOffset.UtcNow);
    }

    public static Run CreateRun(ThingId thingId, RunStatus status, DateTimeOffset startedAt, int? exitCode, string? summaryJson)
    {
        return new Run(
            Id: RunId.New(),
            ThingId: thingId,
            StartedAt: startedAt,
            EndedAt: startedAt.AddSeconds(1),
            Status: status,
            ExitCode: exitCode,
            Summary: summaryJson);
    }

    public static void InsertThing(Db db, Thing thing)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO things(thing_id, name, kind, config_json, created_at)
            VALUES ($id, $name, $kind, $cfg, $at)
            """;
        cmd.Parameters.AddWithValue("$id", thing.Id.ToString());
        cmd.Parameters.AddWithValue("$name", thing.Name);
        cmd.Parameters.AddWithValue("$kind", (int)thing.Kind);
        cmd.Parameters.AddWithValue("$cfg", thing.ConfigJson);
        cmd.Parameters.AddWithValue("$at", thing.CreatedAt.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public static void InsertRun(Db db, Run run)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO runs(run_id, thing_id, started_at, ended_at, status, exit_code, summary)
            VALUES ($id, $thing_id, $started_at, $ended_at, $status, $exit_code, $summary)
            """;
        cmd.Parameters.AddWithValue("$id", run.Id.ToString());
        cmd.Parameters.AddWithValue("$thing_id", run.ThingId.ToString());
        cmd.Parameters.AddWithValue("$started_at", run.StartedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$ended_at", run.EndedAt?.ToString("O"));
        cmd.Parameters.AddWithValue("$status", (int)run.Status);
        cmd.Parameters.AddWithValue("$exit_code", (object?)run.ExitCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$summary", (object?)run.Summary ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public static void InsertRunEvent(Db db, RunEvent runEvent)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO run_events(run_id, at, kind, payload_json)
            VALUES ($run_id, $at, $kind, $payload)
            """;
        cmd.Parameters.AddWithValue("$run_id", runEvent.RunId.ToString());
        cmd.Parameters.AddWithValue("$at", runEvent.At.ToString("O"));
        cmd.Parameters.AddWithValue("$kind", (int)runEvent.Kind);
        cmd.Parameters.AddWithValue("$payload", runEvent.PayloadJson);
        cmd.ExecuteNonQuery();
    }

    public static void InsertArtifact(Db db, Artifact artifact)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO artifacts(artifact_id, run_id, media_type, locator, sha256_hex, created_at)
            VALUES ($id, $run_id, $media, $locator, $hash, $at)
            """;
        cmd.Parameters.AddWithValue("$id", artifact.Id.ToString());
        cmd.Parameters.AddWithValue("$run_id", artifact.RunId.ToString());
        cmd.Parameters.AddWithValue("$media", artifact.MediaType);
        cmd.Parameters.AddWithValue("$locator", artifact.Locator);
        cmd.Parameters.AddWithValue("$hash", (object?)artifact.Sha256Hex ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$at", artifact.CreatedAt.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public static string BuildFailureSummary(string fingerprint, string lastStdErrLine)
    {
        var summary = new RunSummary(
            Status: RunStatus.Failed,
            Duration: TimeSpan.FromSeconds(1),
            StdOutLines: 1,
            StdErrLines: 1,
            ExitCode: 1,
            FailureFingerprint: fingerprint,
            LastStdErrLine: lastStdErrLine,
            ArtifactCount: 0);

        return JsonSerializer.Serialize(summary);
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
