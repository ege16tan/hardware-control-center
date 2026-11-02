using HCC.Core.Models;
using LibreHardwareMonitor.PawnIo;

namespace HCC.Hardware.Monitoring;

/// <summary>
/// LibreHardwareMonitor 0.9.6 reads the motherboard Super-I/O (fans, board
/// temperatures, CPU MSRs) through PawnIO modules. Without the installed PawnIO
/// driver those sensors stay empty even though every device is detected.
/// This class only reads the driver state; it never installs or writes anything.
/// </summary>
public static class PawnIoDiagnostics
{
    public static readonly Version MinimumVersion = new(2, 0, 0, 0);

    /// <summary>Installed PawnIO version, or <c>null</c> if it is missing or unreadable.</summary>
    public static Version? GetInstalledVersion()
    {
        try
        {
            return PawnIo.IsInstalled ? PawnIo.Version : null;
        }
        catch
        {
            // A registry read failure must never break monitoring.
            return null;
        }
    }

    /// <summary>
    /// A board counts as unreadable when LibreHardwareMonitor detected the
    /// motherboard but neither the board itself nor its Super-I/O chip delivers
    /// sensors. On this platform the Super-I/O sensors hang off a "SuperIO"
    /// sub-device, so an empty motherboard alone is not an error.
    /// </summary>
    public static bool BoardSensorsMissing(IEnumerable<HardwareDeviceReading>? devices)
    {
        if (devices is null) return false;

        var all = devices as IReadOnlyCollection<HardwareDeviceReading> ?? devices.ToArray();
        return all.Any(device => device.Type is "Motherboard" && device.SensorCount == 0)
            && !all.Any(device => device.Type is "SuperIO" && device.SensorCount > 0);
    }

    /// <summary>
    /// Explains why a detected motherboard delivers no sensors, or returns
    /// <c>null</c> when the board delivers sensors.
    /// </summary>
    public static string? DescribeMissingBoardSensors(IEnumerable<HardwareDeviceReading>? devices, Version? installedVersion)
    {
        if (!BoardSensorsMissing(devices)) return null;

        if (installedVersion is null)
            return "Das Mainboard liefert keine Lüfter- und Temperatursensoren: Der PawnIO-Treiber fehlt. "
                 + $"PawnIO {MinimumVersion} oder neuer von https://pawnio.eu installieren, danach HCC neu starten.";

        if (installedVersion < MinimumVersion)
            return $"Das Mainboard liefert keine Sensoren: PawnIO {installedVersion} ist zu alt. "
                 + $"PawnIO {MinimumVersion} oder neuer von https://pawnio.eu installieren, danach HCC neu starten.";

        return "Das Mainboard liefert keine Sensoren, obwohl PawnIO installiert ist. "
             + "Der Super-I/O-Chip wird von LibreHardwareMonitor nicht unterstützt oder die Sensoren sind im BIOS deaktiviert.";
    }
}
