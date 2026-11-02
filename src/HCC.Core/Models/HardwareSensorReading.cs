namespace HCC.Core.Models;

public sealed record HardwareSensorReading(
    string Hardware,
    string Name,
    string Type,
    double Value,
    string Unit,
    string? Identifier = null,
    string? HardwareType = null)
{
    public string DisplayName => $"{Hardware} · {Name}";
}
