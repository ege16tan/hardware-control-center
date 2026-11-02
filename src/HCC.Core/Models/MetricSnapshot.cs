namespace HCC.Core.Models;

public sealed record MetricSnapshot(
    double CpuUsagePercent,
    double GpuUsagePercent,
    double MemoryUsagePercent,
    double UsedMemoryGigabytes,
    double TotalMemoryGigabytes,
    IReadOnlyList<HardwareSensorReading>? Sensors = null,
    bool IsSimulated = false,
    string? Status = null,
    string? GpuLoadSensorName = null,
    IReadOnlyList<HardwareDeviceReading>? HardwareDevices = null,
    bool CpuAvailable = true,
    bool GpuAvailable = true,
    bool MemoryAvailable = true,
    IReadOnlyList<FanChannel>? FanChannels = null);
