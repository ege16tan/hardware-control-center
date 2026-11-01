using HCC.Hardware.Monitoring;
using Xunit;

namespace HCC.Tests;

public sealed class SimulatedMetricsProviderTests
{
    [Fact]
    public void ReadCurrent_ReturnsUsageValuesWithinExpectedRanges()
    {
        var snapshot = new SimulatedMetricsProvider().ReadCurrent();

        Assert.InRange(snapshot.CpuUsagePercent, 0, 100);
        Assert.InRange(snapshot.GpuUsagePercent, 0, 100);
        Assert.InRange(snapshot.MemoryUsagePercent, 0, 100);
        Assert.InRange(snapshot.UsedMemoryGigabytes, 0, snapshot.TotalMemoryGigabytes);
    }
}
