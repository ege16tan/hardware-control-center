using HCC.Core.Models;

namespace HCC.Infrastructure.Monitoring;

/// <summary>Pure threshold check against one shared sample. No hardware or UI access.</summary>
public static class AlertEvaluator
{
    public static IReadOnlyList<SensorAlert> Evaluate(MetricSnapshot? snapshot, double temperatureWarningCelsius)
    {
        if (snapshot?.Sensors is null || !double.IsFinite(temperatureWarningCelsius)) return [];

        return snapshot.Sensors
            .Where(sensor => string.Equals(sensor.Type, "Temperature", StringComparison.OrdinalIgnoreCase)
                && string.Equals(sensor.Unit, "°C", StringComparison.OrdinalIgnoreCase)
                && double.IsFinite(sensor.Value)
                && sensor.Value > temperatureWarningCelsius)
            .Select(sensor => new SensorAlert(
                sensor.Identifier ?? sensor.DisplayName,
                sensor.Name,
                sensor.Hardware,
                sensor.Value,
                temperatureWarningCelsius))
            .OrderByDescending(alert => alert.Value)
            .ToArray();
    }
}
