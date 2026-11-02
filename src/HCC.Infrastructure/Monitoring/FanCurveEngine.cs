using HCC.Core.Models;

namespace HCC.Infrastructure.Monitoring;

/// <summary>Pure fan-curve validation and linear interpolation. No hardware or UI dependencies.</summary>
public static class FanCurveEngine
{
    public const int MinimumPoints = 2;
    public const int MaximumPoints = 8;
    public const double MinimumTemperatureCelsius = 0;
    public const double MaximumTemperatureCelsius = 100;

    public static bool TryValidate(IReadOnlyList<FanCurvePoint>? points, out string? error)
    {
        if (points is null || points.Count < MinimumPoints || points.Count > MaximumPoints)
        {
            error = $"Eine Lüfterkurve braucht {MinimumPoints} bis {MaximumPoints} Punkte.";
            return false;
        }

        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            if (point is null)
            {
                error = $"Punkt {index + 1} fehlt.";
                return false;
            }

            if (!double.IsFinite(point.TemperatureCelsius)
                || point.TemperatureCelsius < MinimumTemperatureCelsius
                || point.TemperatureCelsius > MaximumTemperatureCelsius)
            {
                error = $"Punkt {index + 1}: Die Temperatur muss zwischen 0 und 100 °C liegen.";
                return false;
            }

            if (!double.IsFinite(point.Percent) || point.Percent < 0 || point.Percent > 100)
            {
                error = $"Punkt {index + 1}: Der Lüfterwert muss zwischen 0 und 100 % liegen.";
                return false;
            }

            if (index > 0 && point.TemperatureCelsius <= points[index - 1].TemperatureCelsius)
            {
                error = $"Punkt {index + 1}: Die Temperaturen müssen streng aufsteigend sein.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>Returns false for an invalid curve or sensor value; such values never produce a fan value.</summary>
    public static bool TryEvaluate(
        IReadOnlyList<FanCurvePoint>? points,
        double temperatureCelsius,
        out double percent,
        out string? error)
    {
        percent = 0;
        if (!TryValidate(points, out error)) return false;

        if (!double.IsFinite(temperatureCelsius)
            || temperatureCelsius < MinimumTemperatureCelsius
            || temperatureCelsius > MaximumTemperatureCelsius)
        {
            error = "Der Temperaturwert ist ungültig; es wird kein Lüfterwert berechnet.";
            return false;
        }

        var curve = points!;
        if (temperatureCelsius <= curve[0].TemperatureCelsius)
        {
            percent = curve[0].Percent;
            return true;
        }

        var last = curve[^1];
        if (temperatureCelsius >= last.TemperatureCelsius)
        {
            percent = last.Percent;
            return true;
        }

        for (var index = 1; index < curve.Count; index++)
        {
            if (temperatureCelsius > curve[index].TemperatureCelsius) continue;

            var low = curve[index - 1];
            var high = curve[index];
            percent = low.Percent
                + (high.Percent - low.Percent)
                * (temperatureCelsius - low.TemperatureCelsius)
                / (high.TemperatureCelsius - low.TemperatureCelsius);
            return true;
        }

        percent = last.Percent;
        return true;
    }
}
