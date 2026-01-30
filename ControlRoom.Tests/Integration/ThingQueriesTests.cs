using ControlRoom.Domain.Model;
using ControlRoom.Infrastructure.Storage.Queries;
using ControlRoom.Tests.Fixtures;

namespace ControlRoom.Tests.Integration;

public class ThingQueriesTests
{
    [Fact]
    public void ThingQueries_InsertListGet_Works()
    {
        using var dbFixture = new TestDatabaseFixture();
        var db = TestDbHelper.CreateDbWithSchema(dbFixture);
        var queries = new ThingQueries(db);

        var config1 = new ThingConfig { Path = "C:/scripts/one.ps1" }.ToJson();
        var config2 = new ThingConfig { Path = "C:/scripts/two.ps1" }.ToJson();

        var thing1 = new Thing(ThingId.New(), "one", ThingKind.LocalScript, config1, DateTimeOffset.UtcNow.AddMinutes(-1));
        var thing2 = new Thing(ThingId.New(), "two", ThingKind.LocalScript, config2, DateTimeOffset.UtcNow);

        queries.InsertThing(thing1);
        queries.InsertThing(thing2);

        var list = queries.ListThings();
        list.Should().HaveCount(2);
        list[0].ThingId.Should().Be(thing2.Id);
        list[1].ThingId.Should().Be(thing1.Id);

        var fetched = queries.GetThing(thing1.Id);
        fetched.Should().NotBeNull();
        fetched!.ThingId.Should().Be(thing1.Id);
        fetched.Name.Should().Be("one");
        fetched.Kind.Should().Be(ThingKind.LocalScript);
    }
}
