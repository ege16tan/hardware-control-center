using HCC.Infrastructure.Configuration;
using Xunit;

namespace HCC.Tests;

public sealed class AlertSettingsTests
{
    [Fact]
    public void UsesEightyFiveDegreesByDefault()
    {
        var directory = CreateTempDirectory();
        try
        {
            Assert.Equal(85, new AlertSettings(directory).TemperatureWarningCelsius);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SavesAndLoadsTheThreshold()
    {
        var directory = CreateTempDirectory();
        try
        {
            var settings = new AlertSettings(directory);
            Assert.True(settings.TrySetTemperatureWarningCelsius(92.5));

            var restored = AlertSettings.Load(directory);

            Assert.Equal(92.5, restored.TemperatureWarningCelsius);
            Assert.Null(restored.LastPersistenceError);
            Assert.True(File.Exists(Path.Combine(directory, "alerts.json")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(151)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RejectsInvalidThresholdsWithoutChangingTheValue(double value)
    {
        var directory = CreateTempDirectory();
        try
        {
            var settings = new AlertSettings(directory);

            Assert.False(settings.TrySetTemperatureWarningCelsius(value));
            Assert.Equal(85, settings.TemperatureWarningCelsius);
            Assert.NotNull(settings.LastPersistenceError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FallsBackToTheDefaultWhenTheFileIsCorrupt()
    {
        var directory = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, "alerts.json"), "{ not valid json");

            var settings = AlertSettings.Load(directory);

            Assert.Equal(85, settings.TemperatureWarningCelsius);
            Assert.NotNull(settings.LastPersistenceError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FallsBackToTheDefaultWhenTheStoredThresholdIsInvalid()
    {
        var directory = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, "alerts.json"), "{\"TemperatureWarningCelsius\":999}");

            var settings = AlertSettings.Load(directory);

            Assert.Equal(85, settings.TemperatureWarningCelsius);
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
        File.WriteAllText(blockingFile, "block alert settings directory creation");
        try
        {
            var settings = new AlertSettings(blockingFile);

            Assert.False(settings.TrySetTemperatureWarningCelsius(95));
            Assert.Equal(85, settings.TemperatureWarningCelsius);
            Assert.NotNull(settings.LastPersistenceError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RaisesChangedOnlyForASavedThreshold()
    {
        var directory = CreateTempDirectory();
        try
        {
            var settings = new AlertSettings(directory);
            var changes = 0;
            settings.Changed += (_, _) => changes++;

            Assert.True(settings.TrySetTemperatureWarningCelsius(90));
            Assert.False(settings.TrySetTemperatureWarningCelsius(151));

            Assert.Equal(1, changes);
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
