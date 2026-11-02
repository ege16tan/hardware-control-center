using HCC.Core.Models;
using HCC.Hardware.Monitoring;
using Xunit;

namespace HCC.Tests;

public sealed class FanDiscoveryServiceTests
{
    private readonly FanDiscoveryService _service = new();

    [Fact]
    public void PairsSingleFanAndControlIntoACandidate()
    {
        var channels = _service.Discover([
            Sensor("Fan #1", "Fan", 1200, "fan-1", "RPM"),
            Sensor("Fan #1", "Control", 55, "control-1", "%")
        ]);

        var channel = Assert.Single(channels);
        Assert.Equal("fan-1", channel.Id);
        Assert.Equal("control-1", channel.ControlSensorId);
        Assert.Equal(55, channel.ControlPercent);
        Assert.Equal(FanCapability.ControlCandidate, channel.Capability);
    }

    [Fact]
    public void AmbiguousControlsDoNotBecomeACandidate()
    {
        var channels = _service.Discover([
            Sensor("Fan #1", "Fan", 1200, "fan-1", "RPM"),
            Sensor("Fan #1", "Control", 55, "control-1", "%"),
            Sensor("Fan #1", "Control", 60, "control-2", "%")
        ]);

        var channel = Assert.Single(channels);
        Assert.Null(channel.ControlSensorId);
        Assert.Equal(FanCapability.MonitorOnly, channel.Capability);
    }

    [Fact]
    public void FanWithoutControlIsMonitorOnly()
    {
        var channels = _service.Discover([Sensor("CPU Fan", "Fan", 900, "fan-1", "RPM")]);

        var channel = Assert.Single(channels);
        Assert.Null(channel.ControlSensorId);
        Assert.Equal(FanCapability.MonitorOnly, channel.Capability);
    }

    [Fact]
    public void ControlWithoutFanIsNotAChannel()
    {
        var channels = _service.Discover([Sensor("Fan #1", "Control", 55, "control-1", "%")]);

        Assert.Empty(channels);
    }

    [Fact]
    public void ControlOnAnotherHardwareIsNotPaired()
    {
        var channels = _service.Discover([
            Sensor("Fan #1", "Fan", 1200, "fan-1", "RPM", hardware: "Board A"),
            Sensor("Fan #1", "Control", 55, "control-1", "%", hardware: "Board B")
        ]);

        var channel = Assert.Single(channels);
        Assert.Equal(FanCapability.MonitorOnly, channel.Capability);
    }

    [Fact]
    public void ControlOutsidePercentageRangeIsIgnored()
    {
        var channels = _service.Discover([
            Sensor("Fan #1", "Fan", 1200, "fan-1", "RPM"),
            Sensor("Fan #1", "Control", 255, "control-1", "%")
        ]);

        var channel = Assert.Single(channels);
        Assert.Equal(FanCapability.MonitorOnly, channel.Capability);
    }

    [Fact]
    public void KeepsEachFanChannelSeparateAndUsesStableIds()
    {
        var channels = _service.Discover([
            Sensor("Fan #1", "Fan", 1200, "fan-1", "RPM"),
            Sensor("Fan #1", "Control", 55, "control-1", "%"),
            Sensor("Fan #2", "Fan", 800, "fan-2", "RPM"),
            Sensor("Fan #2", "Control", 45, "control-2", "%")
        ]);

        Assert.Equal(2, channels.Count);
        Assert.Contains(channels, channel => channel.Id == "fan-1" && channel.ControlSensorId == "control-1");
        Assert.Contains(channels, channel => channel.Id == "fan-2" && channel.ControlSensorId == "control-2");
    }

    [Fact]
    public void FallsBackToAStableIdWhenNoIdentifierIsProvided()
    {
        var channels = _service.Discover([
            new HardwareSensorReading("Board", "Fan #1", "Fan", 1200, "RPM", Identifier: null, HardwareType: "Motherboard")
        ]);

        var channel = Assert.Single(channels);
        Assert.Equal("Motherboard/Board/Fan/Fan #1", channel.Id);
        Assert.Equal(FanCapability.MonitorOnly, channel.Capability);
    }

    [Fact]
    public void NullInputReturnsNoChannels()
    {
        Assert.Empty(_service.Discover(null));
    }

    private static HardwareSensorReading Sensor(
        string name,
        string type,
        double value,
        string identifier,
        string unit,
        string hardware = "Nuvoton NCT6779D",
        string hardwareType = "Motherboard") =>
        new(hardware, name, type, value, unit, identifier, hardwareType);
}
