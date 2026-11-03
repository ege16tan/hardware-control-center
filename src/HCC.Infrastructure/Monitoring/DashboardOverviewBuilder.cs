using HCC.Core.Models;

namespace HCC.Infrastructure.Monitoring;

/// <summary>
/// Turns one shared sample into the small text and badge values of the dashboard cards.
/// Pure formatting only: no hardware access, no control claims.
/// </summary>
public static class DashboardOverviewBuilder
{
    public static DashboardOverview Build(MetricSnapshot? snapshot, int maximumTemperatures = 4, int maximumFans = 4)
    {
        if (snapshot is null) return Empty;

        var sensors = snapshot.Sensors ?? [];
        return new DashboardOverview(
            Temperatures: BuildTemperatures(sensors, maximumTemperatures),
            Fans: BuildFans(snapshot.FanChannels, maximumFans),
            Hardware: BuildHardware(snapshot),
            CpuChip: DeviceName(snapshot.HardwareDevices, "Cpu", "CPU"),
            CpuDetail: Detail(sensors, "Cpu"),
            GpuChip: DeviceName(snapshot.HardwareDevices, "Gpu", "GPU"),
            GpuDetail: Detail(sensors, "Gpu"),
            MemoryChip: snapshot.MemoryAvailable ? $"{snapshot.TotalMemoryGigabytes:N0} GB" : "nicht erkannt",
            MemoryDetail: snapshot.MemoryAvailable
                ? $"{snapshot.UsedMemoryGigabytes:N1} von {snapshot.TotalMemoryGigabytes:N1} GB belegt"
                : "Windows meldet keinen Speicher",
            SystemTitle: DeviceName(snapshot.HardwareDevices, "Motherboard", "System"),
            SystemSubtitle: snapshot.IsSimulated
                ? "Demo-Werte"
                : snapshot.Status is null ? "Live-Monitoring" : "Monitoring eingeschränkt");
    }

    private static TemperatureOverviewItem[] BuildTemperatures(IReadOnlyList<HardwareSensorReading> sensors, int maximum) =>
        sensors
            .Where(sensor => IsType(sensor, "Temperature") && IsCelsius(sensor) && double.IsFinite(sensor.Value))
            .OrderBy(sensor => TemperaturePriority(sensor.HardwareType))
            .ThenBy(sensor => sensor.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, maximum))
            .Select(sensor => new TemperatureOverviewItem(
                sensor.Name,
                sensor.Hardware,
                sensor.Value,
                Math.Clamp(sensor.Value, 0, 100)))
            .ToArray();

    private static FanOverviewItem[] BuildFans(IReadOnlyList<FanChannel>? channels, int maximum) =>
        (channels ?? [])
            .Take(Math.Max(0, maximum))
            .Select(channel => new FanOverviewItem(
                channel.DisplayName,
                StateText(channel.Capability),
                channel.Rpm > 0 ? $"{channel.Rpm:N0} RPM" : "—"))
            .ToArray();

    private static HardwareStatusItem[] BuildHardware(MetricSnapshot snapshot)
    {
        var devices = snapshot.HardwareDevices ?? [];
        var cpu = FindDevice(devices, "Cpu");
        var gpu = FindDevice(devices, "Gpu");
        var board = FindDevice(devices, "Motherboard");
        // A board without sensors is a driver or BIOS hint, not a failure of the board itself.
        var boardReady = board is not null && (board.SensorCount > 0
            || devices.Any(device => device.Type.Contains("SuperIO", StringComparison.OrdinalIgnoreCase) && device.SensorCount > 0));
        var cpuReady = snapshot.CpuAvailable && cpu is not null;
        var gpuReady = snapshot.GpuAvailable && gpu is not null;
        var accessOk = snapshot.Status is null;

        return
        [
            new HardwareStatusItem("Prozessor", cpu?.Name ?? "nicht erkannt",
                cpuReady ? "Bereit" : "Nicht erkannt", cpuReady, !cpuReady),
            new HardwareStatusItem("Grafikkarte", gpu?.Name ?? "nicht erkannt",
                gpuReady ? "Bereit" : "Nicht erkannt", gpuReady, !gpuReady),
            new HardwareStatusItem("Mainboard", board?.Name ?? "nicht erkannt",
                boardReady ? "Bereit" : "Prüfen", boardReady, !boardReady),
            new HardwareStatusItem("Hardware-Zugriff", "LibreHardwareMonitor · PawnIO",
                accessOk ? "Verbunden" : "Eingeschränkt", accessOk, !accessOk),
        ];
    }

    /// <summary>Clock and temperature of one device group, e.g. "3,40 GHz · 64 °C".</summary>
    private static string Detail(IReadOnlyList<HardwareSensorReading> sensors, string hardwareType)
    {
        var own = sensors
            .Where(sensor => sensor.HardwareType?.Contains(hardwareType, StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();

        var parts = new List<string>(2);
        if (own.Where(sensor => IsType(sensor, "Clock")).OrderByDescending(sensor => sensor.Value).FirstOrDefault() is { } clock)
            parts.Add($"{clock.Value / 1000d:0.00} GHz");
        if (own.Where(sensor => IsType(sensor, "Temperature") && IsCelsius(sensor)).OrderByDescending(sensor => sensor.Value).FirstOrDefault() is { } temperature)
            parts.Add($"{temperature.Value:0} °C");

        return parts.Count == 0 ? "keine Takt-/Temperaturquelle" : string.Join(" · ", parts);
    }

    private static HardwareDeviceReading? FindDevice(IReadOnlyList<HardwareDeviceReading> devices, string typePart) =>
        devices.FirstOrDefault(device => device.Type.Contains(typePart, StringComparison.OrdinalIgnoreCase));

    private static string DeviceName(IReadOnlyList<HardwareDeviceReading>? devices, string typePart, string fallback) =>
        devices is null ? fallback : FindDevice(devices, typePart)?.Name ?? fallback;

    private static string StateText(FanCapability capability) => capability switch
    {
        FanCapability.ControlVerified => "Verifiziert",
        FanCapability.ControlCandidate => "Nicht verifiziert",
        FanCapability.NotDetected => "Nicht erkannt",
        _ => "Nur Überwachung"
    };

    private static bool IsType(HardwareSensorReading sensor, string type) =>
        string.Equals(sensor.Type, type, StringComparison.OrdinalIgnoreCase);

    private static bool IsCelsius(HardwareSensorReading sensor) =>
        string.Equals(sensor.Unit, "°C", StringComparison.OrdinalIgnoreCase);

    private static int TemperaturePriority(string? hardwareType) => hardwareType switch
    {
        null => 3,
        _ when hardwareType.Contains("Cpu", StringComparison.OrdinalIgnoreCase) => 0,
        _ when hardwareType.Contains("Gpu", StringComparison.OrdinalIgnoreCase) => 1,
        _ when hardwareType.Contains("Motherboard", StringComparison.OrdinalIgnoreCase) => 2,
        _ => 3
    };

    private static readonly DashboardOverview Empty = new(
        [], [], [],
        "CPU", "keine Daten",
        "GPU", "keine Daten",
        "—", "keine Daten",
        "System", "keine Daten");
}

