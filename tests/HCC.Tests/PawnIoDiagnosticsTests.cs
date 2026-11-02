using HCC.Core.Models;
using HCC.Hardware.Monitoring;
using Xunit;

namespace HCC.Tests;

public sealed class PawnIoDiagnosticsTests
{
    [Fact]
    public void SuperIoWithSensorsIsAFullyWorkingBoard()
    {
        var devices = new[]
        {
            Device("MSI H87-G43 (MS-7816)", "Motherboard", 0),
            Device("Nuvoton NCT6779D", "SuperIO", 29)
        };

        Assert.False(PawnIoDiagnostics.BoardSensorsMissing(devices));
        Assert.Null(PawnIoDiagnostics.DescribeMissingBoardSensors(devices, null));
    }

    [Fact]
    public void BoardWithoutSensorsAndWithoutSuperIoIsMissing()
    {
        var devices = new[] { Device("MSI H87-G43 (MS-7816)", "Motherboard", 0) };

        Assert.True(PawnIoDiagnostics.BoardSensorsMissing(devices));
    }

    [Fact]
    public void EmptySuperIoCountsAsMissing()
    {
        var devices = new[]
        {
            Device("MSI H87-G43 (MS-7816)", "Motherboard", 0),
            Device("Nuvoton NCT6779D", "SuperIO", 0)
        };

        Assert.True(PawnIoDiagnostics.BoardSensorsMissing(devices));
    }

    [Fact]
    public void BoardWithOwnSensorsIsNotMissing()
    {
        var devices = new[] { Device("Board", "Motherboard", 12) };

        Assert.False(PawnIoDiagnostics.BoardSensorsMissing(devices));
    }

    [Fact]
    public void MissingDriverIsNamedWithInstallHint()
    {
        var text = PawnIoDiagnostics.DescribeMissingBoardSensors(DevicesWithoutPawnIo, null);

        Assert.NotNull(text);
        Assert.Contains("PawnIO", text);
        Assert.Contains("fehlt", text);
        Assert.Contains("pawnio.eu", text);
    }

    [Fact]
    public void OutdatedDriverAsksForAnUpdate()
    {
        var text = PawnIoDiagnostics.DescribeMissingBoardSensors(DevicesWithoutPawnIo, new Version(1, 9));

        Assert.NotNull(text);
        Assert.Contains("zu alt", text);
    }

    [Fact]
    public void SufficientDriverPointsToBoardSupport()
    {
        var text = PawnIoDiagnostics.DescribeMissingBoardSensors(DevicesWithoutPawnIo, PawnIoDiagnostics.MinimumVersion);

        Assert.NotNull(text);
        Assert.Contains("obwohl PawnIO installiert ist", text);
        Assert.DoesNotContain("fehlt", text);
    }

    private static readonly HardwareDeviceReading[] DevicesWithoutPawnIo =
    [
        Device("MSI H87-G43 (MS-7816)", "Motherboard", 0)
    ];

    private static HardwareDeviceReading Device(string name, string type, int sensorCount) => new(name, type, sensorCount);
}
