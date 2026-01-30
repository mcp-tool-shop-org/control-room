using ControlRoom.Domain.Model;

namespace ControlRoom.Tests.Fixtures;

/// <summary>
/// Builder for creating test domain objects with sensible defaults
/// </summary>
public sealed class TestDataBuilder
{
    /// <summary>
    /// Create a test Thing with default values
    /// </summary>
    public static Thing CreateTestThing(
        string? name = null,
        ThingKind kind = ThingKind.LocalScript,
        string? configJson = null)
    {
        return new Thing(
            Id: ThingId.New(),
            Name: name ?? "test-thing",
            Kind: kind,
            ConfigJson: configJson ?? "{}",
            CreatedAt: DateTimeOffset.UtcNow
        );
    }

    /// <summary>
    /// Create a test Run with default values
    /// </summary>
    public static Run CreateTestRun(
        RunId? id = null,
        ThingId? thingId = null,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? endedAt = null,
        RunStatus status = RunStatus.Succeeded,
        int? exitCode = 0,
        string? summary = null)
    {
        return new Run(
            Id: id ?? RunId.New(),
            ThingId: thingId ?? ThingId.New(),
            StartedAt: startedAt ?? DateTimeOffset.UtcNow.AddMinutes(-5),
            EndedAt: endedAt ?? DateTimeOffset.UtcNow,
            Status: status,
            ExitCode: exitCode,
            Summary: summary
        );
    }

    /// <summary>
    /// Create a test RunEvent with default values
    /// </summary>
    public static RunEvent CreateTestRunEvent(
        long? seq = null,
        RunId? runId = null,
        DateTimeOffset? at = null,
        EventKind kind = EventKind.StdOut,
        string? payloadJson = null)
    {
        return new RunEvent(
            Seq: seq ?? 1,
            RunId: runId ?? RunId.New(),
            At: at ?? DateTimeOffset.UtcNow,
            Kind: kind,
            PayloadJson: payloadJson ?? "test output"
        );
    }

    /// <summary>
    /// Create a test ThingConfig JSON string
    /// </summary>
    public static string CreateTestThingConfigJson(
        string? defaultProfile = null,
        Dictionary<string, string>? profiles = null)
    {
        var profilesDict = profiles ?? new Dictionary<string, string>
        {
            { "default", defaultProfile ?? "" }
        };

        var profilesJson = string.Join(",", profilesDict.Select(p => $"{{\"id\":\"{p.Key}\",\"args\":\"{p.Value}\"}}"));
        return $"{{\"profiles\":[{profilesJson}]}}";
    }
}
