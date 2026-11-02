using HCC.Infrastructure.Configuration;
using Xunit;

namespace HCC.Tests;

public sealed class ThemeSettingsTests
{
    [Fact]
    public void UsesTheDarkThemeByDefault()
    {
        var directory = CreateTempDirectory();
        try
        {
            var settings = new ThemeSettings(directory);

            Assert.Equal(ThemeSettings.Dark, settings.Theme);
            Assert.Null(settings.LastPersistenceError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SavesAndLoadsTheLightTheme()
    {
        var directory = CreateTempDirectory();
        try
        {
            var settings = new ThemeSettings(directory);
            Assert.True(settings.TrySetTheme(ThemeSettings.Light));

            var restored = ThemeSettings.Load(directory);

            Assert.Equal(ThemeSettings.Light, restored.Theme);
            Assert.Null(restored.LastPersistenceError);
            Assert.True(File.Exists(Path.Combine(directory, "theme.json")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Neon")]
    public void RejectsUnknownThemesWithoutChangingTheValue(string? theme)
    {
        var directory = CreateTempDirectory();
        try
        {
            var settings = new ThemeSettings(directory);

            Assert.False(settings.TrySetTheme(theme));
            Assert.Equal(ThemeSettings.Dark, settings.Theme);
            Assert.NotNull(settings.LastPersistenceError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FallsBackToDarkWhenTheFileIsCorrupt()
    {
        var directory = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, "theme.json"), "{ not valid json");

            var settings = ThemeSettings.Load(directory);

            Assert.Equal(ThemeSettings.Dark, settings.Theme);
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
        File.WriteAllText(blockingFile, "block theme directory creation");
        try
        {
            var settings = new ThemeSettings(blockingFile);

            Assert.False(settings.TrySetTheme(ThemeSettings.Light));
            Assert.Equal(ThemeSettings.Dark, settings.Theme);
            Assert.NotNull(settings.LastPersistenceError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RaisesChangedOnlyForASavedTheme()
    {
        var directory = CreateTempDirectory();
        try
        {
            var settings = new ThemeSettings(directory);
            var changes = 0;
            settings.Changed += (_, _) => changes++;

            Assert.True(settings.TrySetTheme(ThemeSettings.Light));
            Assert.True(settings.TrySetTheme(ThemeSettings.Dark));
            Assert.False(settings.TrySetTheme("Neon"));

            Assert.Equal(2, changes);
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
