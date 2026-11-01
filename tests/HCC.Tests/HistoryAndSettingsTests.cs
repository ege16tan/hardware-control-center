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

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hcc-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
