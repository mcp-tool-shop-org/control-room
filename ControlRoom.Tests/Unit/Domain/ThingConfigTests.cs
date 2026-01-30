using ControlRoom.Domain.Model;

namespace ControlRoom.Tests.Unit.Domain;

public class ThingConfigTests
{
    [Fact]
    public void ThingConfig_ToJsonAndParse_RoundTrips()
    {
        var config = new ThingConfig
        {
            Path = "C:/scripts/hello.ps1",
            WorkingDir = "C:/scripts",
            Profiles =
            [
                new ThingProfile
                {
                    Id = "smoke",
                    Name = "Smoke",
                    Args = "--fast",
                    Env = new Dictionary<string, string> { ["DEBUG"] = "1" }
                }
            ]
        };

        var json = config.ToJson();
        var parsed = ThingConfig.Parse(json);

        parsed.Schema.Should().Be(ThingConfig.CurrentSchema);
        parsed.Path.Should().Be(config.Path);
        parsed.WorkingDir.Should().Be(config.WorkingDir);
        parsed.Profiles.Should().HaveCount(1);
        parsed.Profiles[0].Id.Should().Be("smoke");
        parsed.Profiles[0].Args.Should().Be("--fast");
        parsed.Profiles[0].Env.Should().ContainKey("DEBUG");
    }

    [Fact]
    public void ThingConfig_ParseSchema1_MigratesToSchema2()
    {
        var legacyJson = """
        {
          "schema": 1,
          "path": "C:/scripts/legacy.ps1",
          "workingDir": "C:/scripts"
        }
        """;

        var parsed = ThingConfig.Parse(legacyJson);

        parsed.Schema.Should().Be(ThingConfig.CurrentSchema);
        parsed.Path.Should().Be("C:/scripts/legacy.ps1");
        parsed.WorkingDir.Should().Be("C:/scripts");
        parsed.Profiles.Should().NotBeEmpty();
        parsed.GetDefaultProfile().Id.Should().Be("default");
    }

    [Fact]
    public void ThingConfig_GetProfile_FallsBackToDefault()
    {
        var config = new ThingConfig
        {
            Path = "C:/scripts/hello.ps1",
            Profiles =
            [
                new ThingProfile { Id = "default", Name = "Default", Args = "" },
                new ThingProfile { Id = "debug", Name = "Debug", Args = "--verbose" }
            ]
        };

        config.GetProfile("missing").Should().BeNull();
        config.GetDefaultProfile().Id.Should().Be("default");
    }
}
