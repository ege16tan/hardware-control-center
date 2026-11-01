namespace HCC.Core.Models;

public sealed record HardwareSensorReading(
    string Hardware,
    string Name,
    string Type,
    double Value,
    string Unit);
