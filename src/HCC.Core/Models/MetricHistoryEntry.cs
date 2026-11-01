namespace HCC.Core.Models;

public sealed record MetricHistoryEntry(
    DateTimeOffset Timestamp,
    double CpuUsagePercent,
    double GpuUsagePercent,
    double MemoryUsagePercent);
