using HCC.Core.Models;
using HCC.Infrastructure.Configuration;
using HCC.Infrastructure.Persistence;
using Xunit;

namespace HCC.Tests;

public sealed class HistoryAndSettingsTests
{
    [Fact]
    public void MetricHistoryStore_PersistsRecentEntriesAndTrimsToConfiguredLimit()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new MetricHistoryStore(directory, maximumEntries: 3, compactionBatchSize: 1);
            for (var second = 1; second <= 5; second++)
            {
                Assert.True(store.Append(new MetricHistoryEntry(DateTimeOffset.UnixEpoch.AddSeconds(second), second, second + 1, second + 2)));
            }

            var restored = new MetricHistoryStore(directory, maximumEntries: 3).LoadRecent(3);

            Assert.Equal(3, restored.Count);
            Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(3), restored[0].Timestamp);
            Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(5), restored[2].Timestamp);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MonitoringSettings_PersistsSupportedRefreshInterval()
    {
        var directory = CreateTempDirectory();
        try
        {
            var settings = MonitoringSettings.Load(directory);
            settings.RefreshIntervalSeconds = 5;

            var restored = MonitoringSettings.Load(directory);

            Assert.Equal(5, restored.RefreshIntervalSeconds);
            restored.RefreshIntervalSeconds = 3;
            Assert.Equal(5, restored.RefreshIntervalSeconds);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MonitoringSettings_SavesAppliesAndDeletesNamedProfiles()
    {
        var directory = CreateTempDirectory();
        try
        {
            var settings = MonitoringSettings.Load(directory);
            settings.RefreshIntervalSeconds = 5;
            Assert.True(settings.SaveProfile("Gaming"));
            settings.RefreshIntervalSeconds = 1;

            var restored = MonitoringSettings.Load(directory);
            Assert.Contains("Gaming", restored.Profiles);
            Assert.True(restored.ApplyProfile(" gaming "));
            Assert.Equal(5, restored.RefreshIntervalSeconds);
            Assert.True(restored.DeleteProfile("GAMING"));
            Assert.Empty(restored.Profiles);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("This profile name is longer than thirty-two characters")]
    public void MonitoringSettings_RejectsInvalidProfileNames(string? name)
    {
        var directory = CreateTempDirectory();
        try
        {
            var settings = new MonitoringSettings(directory);
            Assert.False(settings.SaveProfile(name));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MonitoringSettings_UsesDefaultsWhenProfileFileIsCorrupt()
    {
        var directory = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, "settings.json"), "{ this is not valid json");

            var settings = MonitoringSettings.Load(directory);

            Assert.Equal(2, settings.RefreshIntervalSeconds);
            Assert.Empty(settings.Profiles);
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
