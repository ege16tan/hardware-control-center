using HCC.Core.Models;
using HCC.Infrastructure.Monitoring;
using Xunit;

namespace HCC.Tests;

public sealed class AlertEvaluatorTests
{
    [Fact]
    public void ReportsTemperatureAboveTheThreshold()
    {
        var alerts = AlertEvaluator.Evaluate(Snapshot(Temperature(91)), 85);

        var alert = Assert.Single(alerts);
        Assert.Equal("CPU Package", alert.SensorName);
        Assert.Equal(91, alert.Value);
        Assert.Equal(85, alert.Threshold);
    }

    [Fact]
    public void ValueEqualToTheThresholdDoesNotAlert()
    {
        Assert.Empty(AlertEvaluator.Evaluate(Snapshot(Temperature(85)), 85));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void IgnoresNonFiniteSensorValues(double value)
    {
        Assert.Empty(AlertEvaluator.Evaluate(Snapshot(Temperature(value)), 85));
    }

    [Fact]
    public void IgnoresSensorsThatAreNotTemperatures()
    {
        var snapshot = Snapshot(new HardwareSensorReading("GPU", "Core Load", "Load", 99, "%", "load-1", "GpuNvidia"));

        Assert.Empty(AlertEvaluator.Evaluate(snapshot, 85));
    }

    [Fact]
    public void SortsAlertsByHottestSensorFirst()
    {
        var snapshot = Snapshot(
            Temperature(91, "CPU Package", "cpu-temp"),
            Temperature(105, "GPU Core", "gpu-temp"));

        var alerts = AlertEvaluator.Evaluate(snapshot, 85);

        Assert.Equal(2, alerts.Count);
        Assert.Equal("GPU Core", alerts[0].SensorName);
        Assert.Equal("CPU Package", alerts[1].SensorName);
    }

    [Fact]
    public void ReturnsNoAlertsForMissingDataOrInvalidThreshold()
    {
        Assert.Empty(AlertEvaluator.Evaluate(null, 85));
        Assert.Empty(AlertEvaluator.Evaluate(new MetricSnapshot(0, 0, 0, 0, 0), 85));
        Assert.Empty(AlertEvaluator.Evaluate(Snapshot(Temperature(99)), double.NaN));
    }

    private static MetricSnapshot Snapshot(params HardwareSensorReading[] sensors) =>
        new(0, 0, 0, 0, 0, Sensors: sensors);

    private static HardwareSensorReading Temperature(double value, string name = "CPU Package", string id = "cpu-temp") =>
        new("CPU", name, "Temperature", value, "°C", id, "Cpu");
}
