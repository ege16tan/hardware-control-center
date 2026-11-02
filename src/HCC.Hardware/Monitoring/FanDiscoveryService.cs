using HCC.Core.Models;

namespace HCC.Hardware.Monitoring;

/// <summary>
/// Builds a read-only fan inventory from the shared metrics sample.
/// It never opens hardware or writes control values.
/// </summary>
public sealed class FanDiscoveryService
{
    private static readonly string[] NonChannelWords =
    [
        "fan", "control", "pwm", "duty", "percent", "percentage", "speed", "rpm"
    ];

    public IReadOnlyList<FanChannel> Discover(IEnumerable<HardwareSensorReading>? sensors)
    {
        if (sensors is null) return [];

        var readings = sensors.Where(sensor => sensor is not null).ToArray();
        var controls = readings
            .Where(sensor => IsSensorType(sensor, "Control")
                && double.IsFinite(sensor.Value)
                && sensor.Value is >= 0 and <= 100)
            .GroupBy(sensor => (sensor.Hardware, sensor.HardwareType, Key: GetChannelKey(sensor.Name)))
            .Where(group => group.Key.Key.Length > 0)
            .ToDictionary(group => group.Key, group => group.ToArray());

        return readings
            .Where(sensor => IsSensorType(sensor, "Fan") && double.IsFinite(sensor.Value) && sensor.Value >= 0)
            .GroupBy(sensor => (sensor.Hardware, sensor.HardwareType, sensorId: GetSensorId(sensor)))
            .Select(group => group.First())
            .Select(sensor => CreateChannel(sensor, controls))
            .OrderBy(channel => channel.Hardware, StringComparer.OrdinalIgnoreCase)
            .ThenBy(channel => channel.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static FanChannel CreateChannel(
        HardwareSensorReading fan,
        IReadOnlyDictionary<(string Hardware, string? HardwareType, string Key), HardwareSensorReading[]> controls)
    {
        var key = GetChannelKey(fan.Name);
        HardwareSensorReading? control = null;
        // A name match is only a hint. Multiple controls for the same channel stay ambiguous.
        if (key.Length > 0
            && controls.TryGetValue((fan.Hardware, fan.HardwareType, key), out var matches)
            && matches.Length == 1)
        {
            control = matches[0];
        }

        var hardwareType = fan.HardwareType ?? "Unbekannt";
        var fanSensorId = GetSensorId(fan);
        var capability = control is null ? FanCapability.MonitorOnly : FanCapability.ControlCandidate;
        return new FanChannel(
            Id: fanSensorId,
            Hardware: fan.Hardware,
            HardwareType: hardwareType,
            DisplayName: fan.Name,
            RpmSensorId: fanSensorId,
            Rpm: fan.Value,
            ControlSensorId: control is null ? null : GetSensorId(control),
            ControlPercent: control?.Value,
            Capability: capability);
    }

    private static bool IsSensorType(HardwareSensorReading sensor, string type) =>
        string.Equals(sensor.Type, type, StringComparison.OrdinalIgnoreCase);

    private static string GetSensorId(HardwareSensorReading sensor) =>
        string.IsNullOrWhiteSpace(sensor.Identifier)
            ? $"{sensor.HardwareType ?? "Unknown"}/{sensor.Hardware}/{sensor.Type}/{sensor.Name}"
            : sensor.Identifier;

    private static string GetChannelKey(string name)
    {
        var words = name
            .Split([' ', '#', '-', '_', '.', '/', '\\', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => !NonChannelWords.Contains(word, StringComparer.OrdinalIgnoreCase));
        return string.Join(' ', words).ToUpperInvariant();
    }
}
