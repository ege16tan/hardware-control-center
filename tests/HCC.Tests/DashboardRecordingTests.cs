using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Threading;
using HCC.App.ViewModels;
using HCC.Core.Interfaces;
using HCC.Core.Models;
using HCC.Infrastructure.Configuration;
using HCC.Infrastructure.Monitoring;
using HCC.Infrastructure.Persistence;
using Xunit;

namespace HCC.Tests;

/// <summary>
/// Drives the dashboard recording path exactly like the UI does: commands on an
/// STA thread whose dispatcher queue is pumped, samples from a fake provider.
/// No hardware, no file outside the temp directory.
/// </summary>
public sealed class DashboardRecordingTests
{
    [Fact]
    public void StartAndStopWritesOneSessionWithTheSharedSamples()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), $"hcc-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                var recordingStore = new RecordingSessionStore(directory);
                var settings = new MonitoringSettings(directory);
                using var sampler = new MetricsSampler(new FixedSnapshotProvider(Snapshot()), settings);
                using var viewModel = new DashboardViewModel(sampler, settings, new MetricHistoryStore(directory), recordingStore, new ThemeSettings(directory));

                viewModel.RecordingName = "Integrationstest";
                viewModel.StartRecordingCommand.Execute(null);

                Assert.True(viewModel.IsRecording);
                Assert.True(recordingStore.IsRecording);

                viewModel.RefreshCommand.Execute(null);
                Assert.True(
                    PumpUntil(() => recordingStore.Sessions.Any(session => session.SampleCount >= 1)),
                    "Es wurde kein Messpunkt aufgezeichnet.");

                viewModel.StopRecordingCommand.Execute(null);

                Assert.False(viewModel.IsRecording);
                var session = Assert.Single(viewModel.RecordedSessions);
                Assert.True(session.IsComplete);
                Assert.True(session.SampleCount >= 1);
                Assert.Equal("Integrationstest", session.Name);
                Assert.Equal(session.Id, viewModel.SelectedRecording?.Id);
                Assert.Contains("gespeichert", viewModel.RecordingStatus);

                var entries = recordingStore.LoadEntries(session.Id);
                Assert.True(entries.Count >= 1);
                Assert.All(entries, entry => Assert.True(entry.IsValid));

                viewModel.ShowRecordingCommand.Execute(null);
                Assert.True(viewModel.IsShowingRecording);
                Assert.Contains("Integrationstest", viewModel.SamplingDescription);

                viewModel.ShowLiveCommand.Execute(null);
                Assert.False(viewModel.IsShowingRecording);
                Assert.Contains("Letzte 60 Messungen", viewModel.SamplingDescription);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void StoppingWithoutRunningRecordingReportsInsteadOfCreatingASession()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), $"hcc-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                var recordingStore = new RecordingSessionStore(directory);
                var settings = new MonitoringSettings(directory);
                using var sampler = new MetricsSampler(new FixedSnapshotProvider(Snapshot()), settings);
                using var viewModel = new DashboardViewModel(sampler, settings, new MetricHistoryStore(directory), recordingStore, new ThemeSettings(directory));

                viewModel.StopRecordingCommand.Execute(null);

                Assert.False(viewModel.IsRecording);
                Assert.Empty(viewModel.RecordedSessions);
                Assert.Contains("keine Aufzeichnung", viewModel.RecordingStatus);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static bool PumpUntil(Func<bool> condition, int attempts = 300)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (condition()) return true;
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            Thread.Sleep(10);
        }
        return condition();
    }

    private static MetricSnapshot Snapshot() => new(
        CpuUsagePercent: 25,
        GpuUsagePercent: 30,
        MemoryUsagePercent: 40,
        UsedMemoryGigabytes: 12,
        TotalMemoryGigabytes: 32);

    private sealed class FixedSnapshotProvider(MetricSnapshot snapshot) : IMetricsProvider
    {
        public MetricSnapshot ReadCurrent() => snapshot;
    }
}
