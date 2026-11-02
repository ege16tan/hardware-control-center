namespace HCC.Core.Models;

public enum FanCapability
{
    NotDetected,
    MonitorOnly,
    ControlCandidate,
    ControlVerified
}

/// <summary>
/// Describes a fan tachometer and any possibly related control sensor.
/// A detected control sensor is only a candidate; it is not proof that writing is safe.
/// </summary>
public sealed record FanChannel(
    string Id,
    string Hardware,
    string HardwareType,
    string DisplayName,
    string RpmSensorId,
    double Rpm,
    string? ControlSensorId,
    double? ControlPercent,
    FanCapability Capability)
{
    public string HardwareDisplayName => $"{Hardware} ({HardwareType})";

    public string CapabilityDisplayName => Capability switch
    {
        FanCapability.ControlCandidate => "Steuer-Sensor erkannt · nicht verifiziert",
        FanCapability.ControlVerified => "Steuerbar · verifiziert",
        FanCapability.NotDetected => "Nicht erkannt",
        _ => "Nur überwacht"
    };

    public string CapabilitySymbol => Capability switch
    {
        FanCapability.ControlVerified => "✔",
        FanCapability.ControlCandidate => "◇",
        _ => "◉"
    };

    public string StatusDisplay => $"{CapabilitySymbol} {CapabilityDisplayName}";

    public string CapabilityExplanation => Capability switch
    {
        FanCapability.ControlCandidate => "Ein Steuer-Sensor wurde erkannt, ist aber nicht am Zielboard verifiziert. Die Drehzahl wird von BIOS/Mainboard bestimmt.",
        FanCapability.ControlVerified => "Dieser Kanal wurde am Zielboard geprüft.",
        _ => "Dieser Lüfter kann nur ausgelesen werden. Die Drehzahl wird von BIOS/Mainboard bestimmt."
    };

    public string SelectionDisplay => $"{DisplayName} · {Hardware}";

    public string ControlValueDisplay => ControlPercent is double percent
        ? $"Control-Messwert: {percent:0} %"
        : "";

    public string ControlShortDisplay => ControlPercent is double percent
        ? $"Steuerung {percent:0} %"
        : "nur lesen";
}
