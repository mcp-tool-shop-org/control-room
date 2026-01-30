using System.Text.Json;
using ControlRoom.Domain.Model;
using ControlRoom.Infrastructure.Storage.Queries;
using ControlRoom.Tests.Fixtures;

namespace ControlRoom.Tests.Integration;

public class RunQueriesTests
{
    [Fact]
    public void RunQueries_ListRuns_And_LastSuccess()
    {
        using var dbFixture = new TestDatabaseFixture();
        var db = TestDbHelper.CreateDbWithSchema(dbFixture);

        var config = new ThingConfig { Path = "C:/scripts/test.ps1" }.ToJson();
        var thing = TestDbHelper.CreateThing("thing", config);
        TestDbHelper.InsertThing(db, thing);

        var run1 = TestDbHelper.CreateRun(thing.Id, RunStatus.Succeeded, DateTimeOffset.UtcNow.AddMinutes(-2), 0, null);
        var run2 = TestDbHelper.CreateRun(thing.Id, RunStatus.Failed, DateTimeOffset.UtcNow.AddMinutes(-1), 1, TestDbHelper.BuildFailureSummary("fp1", "err1"));
        var run3 = TestDbHelper.CreateRun(thing.Id, RunStatus.Succeeded, DateTimeOffset.UtcNow, 0, null);

        TestDbHelper.InsertRun(db, run1);
        TestDbHelper.InsertRun(db, run2);
        TestDbHelper.InsertRun(db, run3);

        var queries = new RunQueries(db);
        var list = queries.ListRuns();
        list.Should().HaveCount(3);
        list[0].RunId.Should().Be(run3.Id);

        var lastSuccess = queries.GetLastSuccess(thing.Id);
        lastSuccess.Should().NotBeNull();
        lastSuccess!.RunId.Should().Be(run3.Id);
    }

    [Fact]
    public void RunQueries_ListRunEvents_FiltersBySeq()
    {
        using var dbFixture = new TestDatabaseFixture();
        var db = TestDbHelper.CreateDbWithSchema(dbFixture);

        var config = new ThingConfig { Path = "C:/scripts/test.ps1" }.ToJson();
        var thing = TestDbHelper.CreateThing("thing", config);
        TestDbHelper.InsertThing(db, thing);

        var run = TestDbHelper.CreateRun(thing.Id, RunStatus.Succeeded, DateTimeOffset.UtcNow, 0, null);
        TestDbHelper.InsertRun(db, run);

        TestDbHelper.InsertRunEvent(db, new RunEvent(0, run.Id, DateTimeOffset.UtcNow, EventKind.RunStarted, "{}"));
        TestDbHelper.InsertRunEvent(db, new RunEvent(0, run.Id, DateTimeOffset.UtcNow.AddSeconds(1), EventKind.StdOut, "{\"line\":\"ok\"}"));

        var queries = new RunQueries(db);
        var events = queries.ListRunEvents(run.Id);
        events.Should().HaveCount(2);

        var after = queries.ListRunEvents(run.Id, afterSeq: events[0].Seq);
        after.Should().HaveCount(1);
        after[0].Seq.Should().Be(events[1].Seq);
    }

    [Fact]
    public void RunQueries_FailureGroups_And_FingerprintQueries_Work()
    {
        using var dbFixture = new TestDatabaseFixture();
        var db = TestDbHelper.CreateDbWithSchema(dbFixture);

        var config = new ThingConfig { Path = "C:/scripts/a.ps1" }.ToJson();
        var thing1 = TestDbHelper.CreateThing("thing1", config);
        var thing2 = TestDbHelper.CreateThing("thing2", config);
        TestDbHelper.InsertThing(db, thing1);
        TestDbHelper.InsertThing(db, thing2);

        var fp = "fp-common";
        var failed1 = TestDbHelper.CreateRun(thing1.Id, RunStatus.Failed, DateTimeOffset.UtcNow.AddMinutes(-2), 1, TestDbHelper.BuildFailureSummary(fp, "err1"));
        var failed2 = TestDbHelper.CreateRun(thing2.Id, RunStatus.Failed, DateTimeOffset.UtcNow.AddMinutes(-1), 1, TestDbHelper.BuildFailureSummary(fp, "err2"));
        var success = TestDbHelper.CreateRun(thing1.Id, RunStatus.Succeeded, DateTimeOffset.UtcNow, 0, null);

        TestDbHelper.InsertRun(db, failed1);
        TestDbHelper.InsertRun(db, failed2);
        TestDbHelper.InsertRun(db, success);

        var queries = new RunQueries(db);
        var groups = queries.GetAllFailureGroups();
        groups.Should().HaveCount(1);
        groups[0].Fingerprint.Should().Be(fp);
        groups[0].Count.Should().Be(2);
        groups[0].DistinctThingCount.Should().Be(2);

        queries.GetRecurringFailures().Should().HaveCount(1);
        queries.GetRecurrenceCount(fp).Should().Be(2);
        queries.GetMostCommonFailure().Should().NotBeNull();

        var byFp = queries.ListRunsByFingerprint(fp);
        byFp.Should().HaveCount(2);

        var first = queries.GetFirstRunForFingerprint(fp);
        first.Should().NotBeNull();
        first!.RunId.Should().Be(failed1.Id);

        var latestThing = queries.GetLatestThingForFingerprint(fp);
        latestThing.Should().NotBeNull();
        latestThing!.Should().Be(thing2.Id);
    }
}
