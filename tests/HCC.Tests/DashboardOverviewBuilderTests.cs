using HCC.Core.Models;
using HCC.Infrastructure.Monitoring;
using Xunit;

namespace HCC.Tests;

public sealed class DashboardOverviewBuilderTests
{
    [Fact]
    public void EmptySnapshotProducesFallbacksInsteadOfThrowing()
    {
        var overview = DashboardOverviewBuilder.Build(null);

        Assert.Empty(overview.Temperatures);
        Assert.Empty(overview.Fans);
        Assert.Empty(overview.Hardware);
        Assert.Equal("CPU", overview.CpuChip);
        Assert.Equal("System", overview.SystemTitle);
    }

    [Fact]
    public void TemperaturesKeepOnlyCelsiusAndPreferCpuThenGpu()
    {
        var snapshot = Snapshot(
            Temp("Board", 30, "Motherboard", "Mainboard"),
            Temp("GPU Core", 70, "GpuNvidia", "GPU"),
            Temp("CPU Package", 55, "Cpu", "CPU"),
            Temp("Kelvin Sensor", 300, "Cpu", "CPU", "K"),
            new HardwareSensorReading("CPU", "CPU Total", "Load", 42, "%", "load-1", "Cpu"));

        var overview = DashboardOverviewBuilder.Build(snapshot, maximumTemperatures: 2);

        Assert.Equal(2, overview.Temperatures.Count);
        Assert.Equal("CPU Package", overview.Temperatures[0].Name);
        Assert.Equal("GPU Core", overview.Temperatures[1].Name);
    }

    [Fact]
    public void TemperaturePercentIsClampedForTheBar()
    {
        var overview = DashboardOverviewBuilder.Build(Snapshot(Temp("CPU Package", 120, "Cpu", "CPU")));

        var item = Assert.Single(overview.Temperatures);
        Assert.Equal(120, item.Celsius);
        Assert.Equal(100, item.Percent);
        Assert.Equal("120 °C", item.ValueDisplay);
    }

    [Fact]
    public void FansShowTheControlStateAndADashWithoutRpm()
    {
        var snapshot = Snapshot() with
        {
            FanChannels =
            [
                Fan("Fan #2", 1240, FanCapability.ControlCandidate),
                Fan("Fan #3", 0, FanCapability.MonitorOnly)
            ]
        };

        var overview = DashboardOverviewBuilder.Build(snapshot);

        Assert.Equal("Nicht verifiziert", overview.Fans[0].StateText);
        Assert.Equal($"{1240d:N0} RPM", overview.Fans[0].RpmText);
        Assert.Equal("Nur Überwachung", overview.Fans[1].StateText);
        Assert.Equal("—", overview.Fans[1].RpmText);
    }

    [Fact]
    public void BoardStatusComesFromSuperIoWhenTheBoardItselfReportsNoSensors()
    {
        var withSuperIo = Snapshot() with
        {
            HardwareDevices =
            [
                Device("MSI H87-G43 (MS-7816)", "Motherboard", 0),
                Device("Nuvoton NCT6779D", "SuperIO", 29),
                Device("Intel Core i7-4770", "Cpu", 35),
                Device("NVIDIA GeForce GTX 1060", "GpuNvidia", 30)
            ]
        };

        var overview = DashboardOverviewBuilder.Build(withSuperIo);

        Assert.Equal("Bereit", overview.Hardware[2].StatusText);
        Assert.True(overview.Hardware[2].IsOk);
        Assert.Equal("Intel Core i7-4770", overview.CpuChip);
        Assert.Equal("MSI H87-G43 (MS-7816)", overview.SystemTitle);
    }

    [Fact]
    public void MissingBoardSensorsAskToCheckInsteadOfClaimingReadiness()
    {
        var withoutSuperIo = Snapshot() with
        {
            HardwareDevices = [Device("MSI H87-G43 (MS-7816)", "Motherboard", 0)]
        };

        var overview = DashboardOverviewBuilder.Build(withoutSuperIo);

        Assert.Equal("Prüfen", overview.Hardware[2].StatusText);
        Assert.True(overview.Hardware[2].IsWarning);
        Assert.Equal("Nicht erkannt", overview.Hardware[0].StatusText);
    }

    [Fact]
    public void DetailLineCombinesClockAndPeakTemperature()
    {
        var snapshot = Snapshot(
            Clock(3400),
            Clock(3900),
            Temp("CPU Package", 64, "Cpu", "CPU"));

        var overview = DashboardOverviewBuilder.Build(snapshot);

        Assert.Equal($"{3900 / 1000d:0.00} GHz · {64:0} °C", overview.CpuDetail);
    }

    [Fact]
    public void RestrictedOrSimulatedSnapshotsSaySo()
    {
        Assert.Equal("Monitoring eingeschränkt", DashboardOverviewBuilder.Build(Snapshot() with { Status = "PawnIO fehlt" }).SystemSubtitle);
        Assert.Equal("Demo-Werte", DashboardOverviewBuilder.Build(Snapshot() with { IsSimulated = true }).SystemSubtitle);
        Assert.Equal("Live-Monitoring", DashboardOverviewBuilder.Build(Snapshot()).SystemSubtitle);
    }

    private static MetricSnapshot Snapshot(params HardwareSensorReading[] sensors) =>
        new(25, 30, 40, 12, 32, Sensors: sensors);

    private static HardwareSensorReading Temp(string name, double value, string hardwareType, string hardware, string unit = "°C") =>
        new(hardware, name, "Temperature", value, unit, $"{hardware}-{name}", hardwareType);

    private static HardwareSensorReading Clock(double megahertz) =>
        new("CPU", "CPU Core #1", "Clock", megahertz, "MHz", "clock-1", "Cpu");

    private static FanChannel Fan(string name, double rpm, FanCapability capability) =>
        new(name, "Nuvoton NCT6779D", "SuperIO", name, name, rpm, null, null, capability);

    private static HardwareDeviceReading Device(string name, string type, int sensorCount) => new(name, type, sensorCount);
}
