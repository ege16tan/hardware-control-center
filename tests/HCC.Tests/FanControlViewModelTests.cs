using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Threading;
using HCC.App.ViewModels;
using HCC.Core.Interfaces;
using HCC.Core.Models;
using HCC.Infrastructure.Configuration;
using HCC.Infrastructure.Monitoring;
using Xunit;

namespace HCC.Tests;

public sealed class FanControlViewModelTests
{
    private static readonly FanCurvePoint[] Curve = [new(40, 20), new(80, 60)];

    [Fact]
    public void LoadedProfileClearsSelectionsWhenStoredChannelAndSensorAreGone()
    {
        // The view model applies snapshots on a dispatcher, so the scenario runs on a dedicated STA thread whose queue is pumped.
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var directory = Path.Combine(Path.GetTempPath(), $"hcc-tests-{Guid.NewGuid():N}");
                Directory.CreateDirectory(directory);
                try
                {
                    var settings = FanCurveSettings.Load(directory);
                    Assert.True(settings.SaveProfile(new FanCurveProfile("Alt", "fan-1", "temp-1", Curve)));

                    using var sampler = new MetricsSampler(new FixedSnapshotProvider(ReplacementSnapshot()), new MonitoringSettings(directory));
                    using var viewModel = new FanControlViewModel(sampler, settings);

                    PumpUntil(() => viewModel.FanChannels.Count > 0);
                    Assert.Equal("fan-2", viewModel.SelectedChannel?.Id);
                    Assert.Equal("temp-2", viewModel.SelectedTemperatureSensor?.Identifier);

                    viewModel.SelectedProfile = "Alt";
                    viewModel.LoadProfileCommand.Execute(null);

                    Assert.Null(viewModel.SelectedChannel);
                    Assert.Null(viewModel.SelectedTemperatureSensor);
                    Assert.Contains("Kanal", viewModel.CurveMessage);
                    Assert.Contains("Temperatursensor", viewModel.CurveMessage);
                }
                finally
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void TemperaturePickerOnlyOffersCelsiusSources()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var directory = Path.Combine(Path.GetTempPath(), $"hcc-tests-{Guid.NewGuid():N}");
                Directory.CreateDirectory(directory);
                try
                {
                    var settings = FanCurveSettings.Load(directory);
                    using var sampler = new MetricsSampler(new FixedSnapshotProvider(MixedUnitSnapshot()), new MonitoringSettings(directory));
                    using var viewModel = new FanControlViewModel(sampler, settings);

                    PumpUntil(() => viewModel.TemperatureSensors.Count > 0);

                    Assert.Equal(["celsius-1"], viewModel.TemperatureSensors.Select(sensor => sensor.Identifier));
                    Assert.Contains("1 Temperatursensor", viewModel.Status);
                }
                finally
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static MetricSnapshot MixedUnitSnapshot() => new(
        CpuUsagePercent: 10,
        GpuUsagePercent: 10,
        MemoryUsagePercent: 10,
        UsedMemoryGigabytes: 2,
        TotalMemoryGigabytes: 32,
        Sensors:
        [
            new HardwareSensorReading("CPU", "CPU Package", "Temperature", 45, "°C", "celsius-1", "Cpu"),
            new HardwareSensorReading("CPU", "Core Sensor", "Temperature", 318, "K", "kelvin-1", "Cpu"),
            new HardwareSensorReading("Board", "Sensor °F", "Temperature", 113, "°F", "fahrenheit-1", "Motherboard"),
            new HardwareSensorReading("CPU", "CPU Total", "Load", 12, "%", "load-1", "Cpu"),
        ],
        FanChannels:
        [
            new FanChannel("fan-1", "Nuvoton NCT6779D", "SuperIO", "Fan #1", "fan-1", 900, "control-1", 40, FanCapability.ControlCandidate),
        ]);

    private static void PumpUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            Thread.Sleep(10);
        }
    }

    private static MetricSnapshot ReplacementSnapshot() => new(
        CpuUsagePercent: 10,
        GpuUsagePercent: 10,
        MemoryUsagePercent: 10,
        UsedMemoryGigabytes: 2,
        TotalMemoryGigabytes: 32,
        Sensors:
        [
            new HardwareSensorReading("CPU", "CPU Package", "Temperature", 45, "°C", "temp-2", "Cpu"),
        ],
        FanChannels:
        [
            new FanChannel("fan-2", "Nuvoton NCT6779D", "Motherboard", "Fan #2", "fan-2", 900, "control-2", 40, FanCapability.ControlCandidate),
        ]);

    private sealed class FixedSnapshotProvider(MetricSnapshot snapshot) : IMetricsProvider
    {
        public MetricSnapshot ReadCurrent() => snapshot;
    }
}
