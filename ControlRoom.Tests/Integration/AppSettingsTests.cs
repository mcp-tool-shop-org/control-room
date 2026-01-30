using ControlRoom.Infrastructure.Storage;
using ControlRoom.Tests.Fixtures;

namespace ControlRoom.Tests.Integration;

public class AppSettingsTests
{
    [Fact]
    public void AppSettings_SetGetRemove_Works()
    {
        using var dbFixture = new TestDatabaseFixture();
        var db = TestDbHelper.CreateDbWithSchema(dbFixture);
        var settings = new AppSettings(db);

        var state = new WindowState(10, 20, 800, 600, IsMaximized: false);

        settings.Set(AppSettings.Keys.WindowState, state);
        var loaded = settings.Get<WindowState>(AppSettings.Keys.WindowState);

        loaded.Should().NotBeNull();
        loaded!.X.Should().Be(10);
        loaded.Y.Should().Be(20);
        loaded.Width.Should().Be(800);
        loaded.Height.Should().Be(600);
        loaded.IsMaximized.Should().BeFalse();

        // Optimistic write path (same value)
        settings.Set(AppSettings.Keys.WindowState, state);

        settings.Remove(AppSettings.Keys.WindowState);
        var removed = settings.Get<WindowState>(AppSettings.Keys.WindowState);
        removed.Should().BeNull();
    }

    [Fact]
    public void AppSettings_Routes_MapCorrectly()
    {
        AppSettings.Routes.ToRouteString(AppSettings.Routes.Timeline).Should().Be("//timeline");
        AppSettings.Routes.ToRouteString(AppSettings.Routes.Things).Should().Be("//things");
        AppSettings.Routes.ToRouteString(AppSettings.Routes.Failures).Should().Be("//failures");
        AppSettings.Routes.ToRouteString("unknown").Should().BeNull();

        AppSettings.Routes.ToStableId("//timeline").Should().Be(AppSettings.Routes.Timeline);
        AppSettings.Routes.ToStableId("//things?foo=1").Should().Be(AppSettings.Routes.Things);
        AppSettings.Routes.ToStableId("//failures/123").Should().Be(AppSettings.Routes.Failures);
        AppSettings.Routes.ToStableId("").Should().BeNull();
        AppSettings.Routes.ToStableId(null).Should().BeNull();
    }
}
