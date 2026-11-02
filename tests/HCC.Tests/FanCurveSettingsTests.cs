using HCC.Core.Models;
using HCC.Infrastructure.Configuration;
using Xunit;

namespace HCC.Tests;

public sealed class FanCurveSettingsTests
{
    [Fact]
    public void SavesAndLoadsProfilesInTheirOwnFile()
    {
        var directory = CreateTempDirectory();
        try
        {
            var settings = FanCurveSettings.Load(directory);
            var profile = new FanCurveProfile("Quiet", "fan-1", "temp-1", [new(40, 30), new(80, 70)]);

            Assert.True(settings.SaveProfile(profile));
            Assert.True(File.Exists(Path.Combine(directory, "fan-curves.json")));
            Assert.False(File.Exists(Path.Combine(directory, "settings.json")));

            var restored = FanCurveSettings.Load(directory);
            var loaded = restored.GetProfile(" quiet ");

            Assert.NotNull(loaded);
            Assert.Equal("Quiet", loaded.Name);
            Assert.Equal("fan-1", loaded.ChannelId);
            Assert.Equal("temp-1", loaded.TemperatureSensorId);
            Assert.Equal(2, loaded.Points.Count);
            Assert.Null(restored.LastPersistenceError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DeletesProfiles()
    {
        var directory = CreateTempDirectory();
        try
        {
            var settings = new FanCurveSettings(directory);
            Assert.True(settings.SaveProfile(new FanCurveProfile("Quiet", "fan-1", "temp-1", [new(40, 30), new(80, 70)])));
            Assert.True(settings.DeleteProfile("QUIET"));
            Assert.Empty(FanCurveSettings.Load(directory).Profiles);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RejectsInvalidProfilesWithoutSaving()
    {
        var directory = CreateTempDirectory();
        try
        {
            var settings = new FanCurveSettings(directory);

            Assert.False(settings.SaveProfile(null));
            Assert.False(settings.SaveProfile(new FanCurveProfile("", "fan-1", "temp-1", [new(40, 30), new(80, 70)])));
            Assert.False(settings.SaveProfile(new FanCurveProfile("Quiet", "", "temp-1", [new(40, 30), new(80, 70)])));
            Assert.False(settings.SaveProfile(new FanCurveProfile("Quiet", "fan-1", "", [new(40, 30), new(80, 70)])));
            Assert.False(settings.SaveProfile(new FanCurveProfile("Quiet", "fan-1", "temp-1", [new(40, 30)])));
            Assert.Empty(settings.Profiles);
            Assert.NotNull(settings.LastPersistenceError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DiscardsCorruptJsonWithAVisibleError()
    {
        var directory = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, "fan-curves.json"), "{ this is not valid json");

            var settings = FanCurveSettings.Load(directory);

            Assert.Empty(settings.Profiles);
            Assert.NotNull(settings.LastPersistenceError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DiscardsInvalidCurvesFromFileButKeepsValidOnes()
    {
        var directory = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, "fan-curves.json"),
                """
                {"Profiles":[
                  {"Name":"Broken","ChannelId":"fan-1","TemperatureSensorId":"temp-1","Points":[{"TemperatureCelsius":50,"Percent":40}]},
                  {"Name":"Quiet","ChannelId":"fan-1","TemperatureSensorId":"temp-1","Points":[{"TemperatureCelsius":40,"Percent":30},{"TemperatureCelsius":80,"Percent":70}]}
                ]}
                """);

            var settings = FanCurveSettings.Load(directory);

            Assert.Equal(["Quiet"], settings.Profiles);
            Assert.NotNull(settings.LastPersistenceError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RollsBackWhenStorageFails()
    {
        var directory = CreateTempDirectory();
        var blockingFile = Path.Combine(directory, "not-a-directory");
        File.WriteAllText(blockingFile, "block fan-curve directory creation");
        try
        {
            var settings = new FanCurveSettings(blockingFile);

            Assert.False(settings.SaveProfile(new FanCurveProfile("Quiet", "fan-1", "temp-1", [new(40, 30), new(80, 70)])));
            Assert.Empty(settings.Profiles);
            Assert.NotNull(settings.LastPersistenceError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hcc-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
