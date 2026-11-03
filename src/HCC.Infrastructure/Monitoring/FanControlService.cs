using HCC.Core.Interfaces;
using HCC.Core.Models;

namespace HCC.Infrastructure.Monitoring;

public enum FanControlState
{
    Inactive,
    Active,
    Fallback,
    Error
}

/// <summary>
/// Safety values from LUEFTERSTEUERUNG_PLAN.md §5.3 (proposals in the plan, kept as constants
/// so tests and documentation agree). Change them only after a board test.
/// </summary>
public static class FanControlDefaults
{
    public const double MinimumPercent = 30;
    public const double HysteresisCelsius = 2;
    public const int FailureLimit = 3;
    public static readonly TimeSpan WriteInterval = TimeSpan.FromSeconds(3);
}

/// <summary>
/// Safety limits for one <see cref="FanControlService"/>. The minimum is clamped upwards;
/// values above 100 % and non-finite values are rejected before they reach a controller.
/// </summary>
public sealed record FanControlLimits(
    double MinimumPercent = FanControlDefaults.MinimumPercent,
    double HysteresisCelsius = FanControlDefaults.HysteresisCelsius,
    int FailureLimit = FanControlDefaults.FailureLimit,
    TimeSpan? MinimumWriteInterval = null)
{
    public TimeSpan WriteInterval => MinimumWriteInterval ?? FanControlDefaults.WriteInterval;
}

/// <summary>
/// State machine for later fan control. It never starts by itself: activation is an
/// explicit call, every write requires a currently verified channel, and any failure
/// falls back to the BIOS default.
/// </summary>
public sealed class FanControlService : IDisposable
{
    private readonly IFanController _controller;
    private readonly FanControlLimits _limits;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Dictionary<string, ActiveCurve> _active = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FanControlState> _states = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _errors = new(StringComparer.Ordinal);
    private bool _disposed;

    public FanControlService(IFanController controller, FanControlLimits? limits = null, Func<DateTimeOffset>? clock = null)
    {
        _controller = controller;
        _limits = limits ?? new FanControlLimits();
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public FanControlState GetState(string channelId) =>
        _states.GetValueOrDefault(channelId, FanControlState.Inactive);

    public string? GetLastError(string channelId) => _errors.GetValueOrDefault(channelId);

    public bool TryActivate(
        FanChannel? channel,
        IReadOnlyList<FanCurvePoint>? curve,
        string? temperatureSensorId,
        out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (channel is null)
        {
            error = "Bitte zuerst einen Lüfterkanal auswählen.";
            return false;
        }

        if (channel.Capability != FanCapability.ControlVerified || string.IsNullOrWhiteSpace(channel.ControlSensorId))
        {
            error = "Dieser Kanal ist am Zielboard nicht verifiziert. Die Steuerung bleibt gesperrt.";
            return false;
        }

        if (!FanCurveEngine.TryValidate(curve, out error)) return false;

        if (string.IsNullOrWhiteSpace(temperatureSensorId))
        {
            error = "Bitte eine Temperaturquelle auswählen.";
            return false;
        }

        _active[channel.Id] = new ActiveCurve(channel, curve!.ToArray(), temperatureSensorId);
        _states[channel.Id] = FanControlState.Inactive;
        _errors.Remove(channel.Id);
        error = null;
        return true;
    }

    public bool TryDeactivate(string? channelId, out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (channelId is null || !_active.Remove(channelId, out var active))
        {
            error = "Dieser Kanal wird nicht gesteuert.";
            return false;
        }

        _states[channelId] = FanControlState.Inactive;
        var resetError = Reset(active);
        if (resetError is null)
        {
            _errors.Remove(channelId);
            error = null;
            return true;
        }

        _states[channelId] = FanControlState.Error;
        _errors[channelId] = resetError;
        error = resetError;
        return false;
    }

    /// <summary>Applies every active curve to one sample. Invalid or missing data never writes.</summary>
    public bool TryApply(MetricSnapshot snapshot, out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        error = null;

        foreach (var active in _active.Values.ToArray())
        {
            var channel = snapshot.FanChannels?.FirstOrDefault(candidate => candidate.Id == active.Channel.Id);
            if (channel is null)
            {
                Fallback(active, FanControlState.Fallback, "Lüfterkanal wurde nicht mehr erkannt.");
                error ??= _errors[active.Channel.Id];
                continue;
            }

            if (!IsVerifiedWithControlSensor(snapshot, channel))
            {
                Fallback(active, FanControlState.Fallback, "Lüfterkanal ist nicht mehr verifiziert oder der Steuer-Sensor fehlt.");
                error ??= _errors[active.Channel.Id];
                continue;
            }

            var sensor = snapshot.Sensors?.FirstOrDefault(candidate => candidate.Identifier == active.TemperatureSensorId);
            if (sensor is null)
            {
                Fallback(active, FanControlState.Fallback, "Temperaturquelle wurde nicht mehr erkannt.");
                error ??= _errors[active.Channel.Id];
                continue;
            }

            if (!IsCelsiusTemperature(sensor))
            {
                Fallback(active, FanControlState.Fallback, "Die Temperaturquelle liefert kein °C-Temperatursignal.");
                error ??= _errors[active.Channel.Id];
                continue;
            }

            if (!FanCurveEngine.TryEvaluate(active.Curve, sensor.Value, out var percent, out var curveError))
            {
                Fallback(active, FanControlState.Fallback, curveError!);
                error ??= _errors[active.Channel.Id];
                continue;
            }

            var now = _clock();
            if (!ShouldWrite(active, sensor.Value, now))
            {
                // Hysteresis or write interval: keep the previous value, no hardware access.
                _states[channel.Id] = FanControlState.Active;
                continue;
            }

            try
            {
                _controller.SetPercent(channel, Math.Clamp(percent, _limits.MinimumPercent, 100));
                active.ConsecutiveFailures = 0;
                active.LastWriteAt = now;
                active.LastWrittenTemperature = sensor.Value;
                _states[channel.Id] = FanControlState.Active;
                _errors.Remove(channel.Id);
            }
            catch (Exception ex)
            {
                active.ConsecutiveFailures++;
                if (active.ConsecutiveFailures >= _limits.FailureLimit)
                {
                    Fallback(active, FanControlState.Error,
                        $"Schreiben {_limits.FailureLimit}-mal fehlgeschlagen: {ex.Message}");
                }
                else
                {
                    _states[channel.Id] = FanControlState.Active;
                    _errors[channel.Id] = $"Schreiben fehlgeschlagen ({active.ConsecutiveFailures}/{_limits.FailureLimit}): {ex.Message}";
                }

                error ??= _errors[active.Channel.Id];
            }
        }

        return error is null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var active in _active.Values)
        {
            try { _controller.Reset(active.Channel); }
            catch { /* Shutdown must not be blocked by a failing fallback. */ }
        }

        _active.Clear();
    }

    private static bool IsVerifiedWithControlSensor(MetricSnapshot snapshot, FanChannel channel) =>
        channel.Capability == FanCapability.ControlVerified
        && !string.IsNullOrWhiteSpace(channel.ControlSensorId)
        && snapshot.Sensors?.Any(sensor =>
            sensor.Identifier == channel.ControlSensorId
            && string.Equals(sensor.Type, "Control", StringComparison.OrdinalIgnoreCase)) == true;

    private static bool IsCelsiusTemperature(HardwareSensorReading sensor) =>
        string.Equals(sensor.Type, "Temperature", StringComparison.OrdinalIgnoreCase)
        && string.Equals(sensor.Unit, "°C", StringComparison.OrdinalIgnoreCase);

    private void Fallback(ActiveCurve active, FanControlState state, string reason)
    {
        _active.Remove(active.Channel.Id);
        _states[active.Channel.Id] = state;
        var resetError = Reset(active);
        _errors[active.Channel.Id] = resetError is null ? reason : $"{reason} {resetError}";
    }

    private string? Reset(ActiveCurve active)
    {
        try
        {
            _controller.Reset(active.Channel);
            return null;
        }
        catch (Exception ex)
        {
            return $"Rückfall auf BIOS-Automatik fehlgeschlagen: {ex.Message}";
        }
    }

    private bool ShouldWrite(ActiveCurve active, double temperature, DateTimeOffset now)
    {
        if (active.LastWriteAt is not { } lastWrite) return true;
        if (now - lastWrite < _limits.WriteInterval) return false;
        return active.LastWrittenTemperature is not double lastTemperature
            || Math.Abs(temperature - lastTemperature) >= _limits.HysteresisCelsius;
    }

    private sealed class ActiveCurve(FanChannel channel, IReadOnlyList<FanCurvePoint> curve, string temperatureSensorId)
    {
        public FanChannel Channel { get; } = channel;
        public IReadOnlyList<FanCurvePoint> Curve { get; } = curve;
        public string TemperatureSensorId { get; } = temperatureSensorId;
        public int ConsecutiveFailures { get; set; }
        public DateTimeOffset? LastWriteAt { get; set; }
        public double? LastWrittenTemperature { get; set; }
    }
}
