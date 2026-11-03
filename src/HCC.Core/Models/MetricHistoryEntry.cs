using System.Text.Json.Serialization;

namespace HCC.Core.Models;

public sealed record MetricHistoryEntry(
    DateTimeOffset Timestamp,
    double? CpuUsagePercent,
    double? GpuUsagePercent,
    double? MemoryUsagePercent)
{
    /// <summary>
    /// A sample is only usable when it carries at least one percentage and every
    /// value stays inside the physically meaningful 0–100 % range.
    /// Derived value: never written to JSON, so history and session files stay small.
    /// </summary>
    [JsonIgnore]
    public bool IsValid =>
        (CpuUsagePercent.HasValue || GpuUsagePercent.HasValue || MemoryUsagePercent.HasValue)
        && IsValidPercent(CpuUsagePercent)
        && IsValidPercent(GpuUsagePercent)
        && IsValidPercent(MemoryUsagePercent);

    private static bool IsValidPercent(double? value) => value is null or (>= 0 and <= 100);
}
