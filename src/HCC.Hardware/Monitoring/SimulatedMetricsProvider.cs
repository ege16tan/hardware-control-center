using HCC.Core.Interfaces;
using HCC.Core.Models;

namespace HCC.Hardware.Monitoring;

public sealed class SimulatedMetricsProvider : IMetricsProvider
{
    private const double TotalMemoryGigabytes = 32;

    public MetricSnapshot ReadCurrent()
    {
        var usedMemory = Random.Shared.Next(6, 25) + Random.Shared.NextDouble();

        return new MetricSnapshot(
            CpuUsagePercent: Random.Shared.Next(8, 77) + Random.Shared.NextDouble(),
            GpuUsagePercent: Random.Shared.Next(3, 92) + Random.Shared.NextDouble(),
            MemoryUsagePercent: usedMemory / TotalMemoryGigabytes * 100,
            UsedMemoryGigabytes: usedMemory,
            TotalMemoryGigabytes: TotalMemoryGigabytes,
            IsSimulated: true,
            Status: "Demo-Werte werden angezeigt.");
    }
}
