using HCC.Core.Interfaces;
using HCC.Core.Models;
using HCC.Infrastructure.Configuration;
using HCC.Infrastructure.Monitoring;
using Xunit;

namespace HCC.Tests;

public sealed class MetricsSamplerTests
{
    [Fact]
    public async Task RefreshNowAsync_CoalescesConcurrentRequestsAndReadsOnWorker()
    {
        using var provider = new BlockingMetricsProvider();
        var testThreadId = Environment.CurrentManagedThreadId;
        using var settings = new DisposableSettings();
        using var sampler = new MetricsSampler(provider, settings.Value);

        Assert.True(provider.Started.Wait(TimeSpan.FromSeconds(5)));
        var first = sampler.RefreshNowAsync();
        var second = sampler.RefreshNowAsync();
        Assert.Same(first, second);

        provider.Release.Set();
        await Task.WhenAll(first, second);

        Assert.Equal(1, provider.ReadCount);
        Assert.Same(provider.Snapshot, sampler.LatestSnapshot);
        Assert.NotEqual(testThreadId, provider.ReadThreadId);
    }

    [Fact]
    public async Task Polling_ContinuesAfterProviderFailure()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hcc-sampler-{Guid.NewGuid():N}");
        try
        {
            var settings = new MonitoringSettings(directory);
            Assert.True(settings.TrySetRefreshIntervalSeconds(1));
            using var provider = new FailOnceMetricsProvider();
            using var sampler = new MetricsSampler(provider, settings);
            var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var recovered = new TaskCompletionSource<MetricSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            sampler.SamplingFailed += _ => failed.TrySetResult();
            sampler.SnapshotUpdated += snapshot => recovered.TrySetResult(snapshot);
            provider.AllowFirstRead.Set();

            await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var snapshot = await recovered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(2, provider.ReadCount);
            Assert.Same(provider.Snapshot, snapshot);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class BlockingMetricsProvider : IMetricsProvider, IDisposable
    {
        public ManualResetEventSlim Started { get; } = new();
        public ManualResetEventSlim Release { get; } = new();
        public int ReadCount;
        public int ReadThreadId;
        public MetricSnapshot Snapshot { get; } = new(10, 20, 30, 9, 32);

        public MetricSnapshot ReadCurrent()
        {
            Interlocked.Increment(ref ReadCount);
            ReadThreadId = Environment.CurrentManagedThreadId;
            Started.Set();
            Release.Wait(TimeSpan.FromSeconds(5));
            return Snapshot;
        }

        public void Dispose()
        {
            Release.Set();
            Started.Dispose();
            Release.Dispose();
        }
    }

    private sealed class FailOnceMetricsProvider : IMetricsProvider, IDisposable
    {
        public int ReadCount;
        public ManualResetEventSlim AllowFirstRead { get; } = new();
        public MetricSnapshot Snapshot { get; } = new(11, 22, 33, 10, 32);

        public MetricSnapshot ReadCurrent()
        {
            if (Interlocked.Increment(ref ReadCount) == 1)
            {
                AllowFirstRead.Wait(TimeSpan.FromSeconds(5));
                throw new InvalidOperationException("temporary read failure");
            }
            return Snapshot;
        }

        public void Dispose() => AllowFirstRead.Dispose();
    }

    private sealed class DisposableSettings : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hcc-sampler-{Guid.NewGuid():N}");
        public MonitoringSettings Value { get; }

        public DisposableSettings() => Value = new MonitoringSettings(_directory);

        public void Dispose()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
    }
}
