using HCC.Core.Interfaces;
using HCC.Core.Models;
using HCC.Infrastructure.Configuration;
using HCC.Infrastructure.Monitoring;
using Xunit;

namespace HCC.Tests;

public sealed class FanControlServiceTests
{
    private static readonly FanCurvePoint[] Curve = [new(40, 20), new(80, 60)];

    [Fact]
    public void UnverifiedChannelCanNeverBeActivated()
    {
        var controller = new FakeFanController();
        using var service = new FanControlService(controller);
        var channel = Channel(FanCapability.ControlCandidate);

        Assert.False(service.TryActivate(channel, Curve, "temp-1", out var error));
        Assert.NotNull(error);
        Assert.Equal(FanControlState.Inactive, service.GetState(channel.Id));
        Assert.Empty(controller.Writes);
        Assert.Empty(controller.Resets);
    }

    [Fact]
    public void VerifiedChannelWritesOnlyAfterExplicitActivationAndApply()
    {
        var controller = new FakeFanController();
        using var service = new FanControlService(controller);
        var channel = Channel(FanCapability.ControlVerified);

        Assert.True(service.TryActivate(channel, Curve, "temp-1", out _));
        Assert.Empty(controller.Writes);

        Assert.True(service.TryApply(Snapshot(channel, Temperature(60)), out var error));
        Assert.Null(error);
        var write = Assert.Single(controller.Writes);
        Assert.Equal(channel.Id, write.ChannelId);
        Assert.Equal(40, write.Percent, 6);
        Assert.Equal(FanControlState.Active, service.GetState(channel.Id));
    }

    [Fact]
    public void MissingTemperatureSensorFallsBackWithoutWriting()
    {
        var controller = new FakeFanController();
        using var service = new FanControlService(controller);
        var channel = Channel(FanCapability.ControlVerified);
        Assert.True(service.TryActivate(channel, Curve, "temp-1", out _));

        Assert.False(service.TryApply(Snapshot(channel, temperature: null), out var error));

        Assert.NotNull(error);
        Assert.Empty(controller.Writes);
        Assert.Single(controller.Resets);
        Assert.Equal(FanControlState.Fallback, service.GetState(channel.Id));
    }

    [Theory]
    [InlineData("Control", "%")]
    [InlineData("Temperature", "K")]
    public void TemperatureSourceWithWrongTypeOrUnitFallsBackWithoutWriting(string type, string unit)
    {
        var controller = new FakeFanController();
        using var service = new FanControlService(controller);
        var channel = Channel(FanCapability.ControlVerified);
        Assert.True(service.TryActivate(channel, Curve, "temp-1", out _));

        var wrong = new HardwareSensorReading("Nuvoton NCT6779D", "Fan #1", type, 55, unit, "temp-1", "Motherboard");
        Assert.False(service.TryApply(Snapshot(channel, wrong), out var error));

        Assert.NotNull(error);
        Assert.Empty(controller.Writes);
        Assert.Single(controller.Resets);
        Assert.Equal(FanControlState.Fallback, service.GetState(channel.Id));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    [InlineData(101)]
    public void InvalidSensorValuesNeverReachTheController(double value)
    {
        var controller = new FakeFanController();
        using var service = new FanControlService(controller);
        var channel = Channel(FanCapability.ControlVerified);
        Assert.True(service.TryActivate(channel, Curve, "temp-1", out _));

        Assert.False(service.TryApply(Snapshot(channel, Temperature(value)), out var error));

        Assert.NotNull(error);
        Assert.Empty(controller.Writes);
        Assert.Single(controller.Resets);
        Assert.Equal(FanControlState.Fallback, service.GetState(channel.Id));
    }

    [Fact]
    public void MissingChannelFallsBackWithoutWriting()
    {
        var controller = new FakeFanController();
        using var service = new FanControlService(controller);
        var channel = Channel(FanCapability.ControlVerified);
        Assert.True(service.TryActivate(channel, Curve, "temp-1", out _));

        Assert.False(service.TryApply(Snapshot(channel: null, Temperature(60)), out _));

        Assert.Empty(controller.Writes);
        Assert.Single(controller.Resets);
        Assert.Equal(FanControlState.Fallback, service.GetState(channel.Id));
    }

    [Fact]
    public void MissingControlSensorFallsBackWithoutWriting()
    {
        var controller = new FakeFanController();
        using var service = new FanControlService(controller);
        var channel = Channel(FanCapability.ControlVerified);
        Assert.True(service.TryActivate(channel, Curve, "temp-1", out _));

        Assert.False(service.TryApply(Snapshot(channel, Temperature(60), includeControlSensor: false), out _));

        Assert.Empty(controller.Writes);
        Assert.Single(controller.Resets);
        Assert.Equal(FanControlState.Fallback, service.GetState(channel.Id));
    }

    [Fact]
    public void WriteFailureSetsErrorAndAttemptsFallback()
    {
        var controller = new FakeFanController { ThrowOnWrite = true };
        using var service = new FanControlService(controller);
        var channel = Channel(FanCapability.ControlVerified);
        Assert.True(service.TryActivate(channel, Curve, "temp-1", out _));

        Assert.False(service.TryApply(Snapshot(channel, Temperature(60)), out var error));

        Assert.NotNull(error);
        Assert.Empty(controller.Writes);
        Assert.Single(controller.Resets);
        Assert.Equal(FanControlState.Error, service.GetState(channel.Id));
        Assert.NotNull(service.GetLastError(channel.Id));
    }

    [Fact]
    public void InvalidCurveIsRejectedAtActivation()
    {
        var controller = new FakeFanController();
        using var service = new FanControlService(controller);
        var channel = Channel(FanCapability.ControlVerified);

        Assert.False(service.TryActivate(channel, [new(40, 20)], "temp-1", out var error));

        Assert.NotNull(error);
        Assert.Empty(controller.Writes);
    }

    [Fact]
    public void DeactivateResetsAndStopsWrites()
    {
        var controller = new FakeFanController();
        using var service = new FanControlService(controller);
        var channel = Channel(FanCapability.ControlVerified);
        Assert.True(service.TryActivate(channel, Curve, "temp-1", out _));
        Assert.True(service.TryApply(Snapshot(channel, Temperature(60)), out _));

        Assert.True(service.TryDeactivate(channel.Id, out _));
        Assert.True(service.TryApply(Snapshot(channel, Temperature(60)), out _));

        Assert.Single(controller.Writes);
        Assert.Single(controller.Resets);
        Assert.Equal(FanControlState.Inactive, service.GetState(channel.Id));
    }

    [Fact]
    public void DisposeResetsActiveChannels()
    {
        var controller = new FakeFanController();
        var service = new FanControlService(controller);
        var channel = Channel(FanCapability.ControlVerified);
        Assert.True(service.TryActivate(channel, Curve, "temp-1", out _));
        Assert.True(service.TryApply(Snapshot(channel, Temperature(60)), out _));

        service.Dispose();

        Assert.Single(controller.Resets);
    }

    [Fact]
    public void DisposeDoesNotThrowWhenResetFails()
    {
        var controller = new FakeFanController { ThrowOnReset = true };
        var service = new FanControlService(controller);
        var channel = Channel(FanCapability.ControlVerified);
        Assert.True(service.TryActivate(channel, Curve, "temp-1", out _));
        Assert.True(service.TryApply(Snapshot(channel, Temperature(60)), out _));

        service.Dispose();
    }

    [Fact]
    public void SavedProfileStartsInactive()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hcc-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var controller = new FakeFanController();
            using var service = new FanControlService(controller);
            var settings = new FanCurveSettings(directory);
            Assert.True(settings.SaveProfile(new FanCurveProfile("Quiet", "fan-1", "temp-1", Curve)));

            var profile = Assert.IsType<FanCurveProfile>(FanCurveSettings.Load(directory).GetProfile("Quiet"));

            Assert.Equal(FanControlState.Inactive, service.GetState(profile.ChannelId));
            Assert.True(service.TryApply(Snapshot(Channel(FanCapability.ControlVerified), Temperature(60)), out _));
            Assert.Empty(controller.Writes);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static FanChannel Channel(FanCapability capability) =>
        new("fan-1", "Nuvoton NCT6779D", "Motherboard", "Fan #1", "fan-1", 1200, "control-1", 55, capability);

    private static HardwareSensorReading Temperature(double value) =>
        new("CPU", "CPU Package", "Temperature", value, "°C", "temp-1", "Cpu");

    private static MetricSnapshot Snapshot(
        FanChannel? channel,
        HardwareSensorReading? temperature,
        bool includeControlSensor = true)
    {
        var sensors = new List<HardwareSensorReading>();
        if (includeControlSensor)
            sensors.Add(new HardwareSensorReading("Nuvoton NCT6779D", "Fan #1", "Control", 55, "%", "control-1", "Motherboard"));
        if (temperature is not null) sensors.Add(temperature);

        return new MetricSnapshot(0, 0, 0, 0, 0,
            Sensors: sensors,
            FanChannels: channel is null ? [] : [channel]);
    }

    private sealed class FakeFanController : IFanController
    {
        public List<(string ChannelId, double Percent)> Writes { get; } = [];
        public List<string> Resets { get; } = [];
        public bool ThrowOnWrite { get; init; }
        public bool ThrowOnReset { get; init; }

        public void SetPercent(FanChannel channel, double percent)
        {
            if (ThrowOnWrite) throw new InvalidOperationException("write failed");
            Writes.Add((channel.Id, percent));
        }

        public void Reset(FanChannel channel)
        {
            if (ThrowOnReset) throw new InvalidOperationException("reset failed");
            Resets.Add(channel.Id);
        }
    }
}
