namespace HCC.Core.Models;

public sealed record SensorAlert(
    string SensorId,
    string SensorName,
    string Hardware,
    double Value,
    double Threshold)
{
    public string DisplayText => $"{SensorName} ({Hardware}): {Value:0.0} °C – Warnschwelle {Threshold:0.#} °C";
}
