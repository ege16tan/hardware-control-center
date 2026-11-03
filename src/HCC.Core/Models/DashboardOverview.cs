namespace HCC.Core.Models;

/// <summary>One row of the dashboard temperature card. Values are display-ready on purpose.</summary>
public sealed record TemperatureOverviewItem(string Name, string Hardware, double Celsius, double Percent)
{
    public string ValueDisplay => $"{Celsius:0} °C";
}

/// <summary>One row of the dashboard fan card.</summary>
public sealed record FanOverviewItem(string Name, string StateText, string RpmText);

/// <summary>One row of the dashboard hardware status card.</summary>
public sealed record HardwareStatusItem(string Component, string Device, string StatusText, bool IsOk, bool IsWarning);

/// <summary>Everything the dashboard shows next to the live metric cards and the chart.</summary>
public sealed record DashboardOverview(
    IReadOnlyList<TemperatureOverviewItem> Temperatures,
    IReadOnlyList<FanOverviewItem> Fans,
    IReadOnlyList<HardwareStatusItem> Hardware,
    string CpuChip,
    string CpuDetail,
    string GpuChip,
    string GpuDetail,
    string MemoryChip,
    string MemoryDetail,
    string SystemTitle,
    string SystemSubtitle);
