namespace HCC.Core.Models;

public sealed record FanCurvePoint(double TemperatureCelsius, double Percent);

public sealed record FanCurveProfile(
    string Name,
    string ChannelId,
    string TemperatureSensorId,
    IReadOnlyList<FanCurvePoint> Points);
