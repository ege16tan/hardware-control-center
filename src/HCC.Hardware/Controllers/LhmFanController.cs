using HCC.Core.Interfaces;
using HCC.Core.Models;

namespace HCC.Hardware.Controllers;

/// <summary>
/// The only place allowed to contain LibreHardwareMonitor write calls.
/// LHM 0.9.6 exposes <c>IControl.SetSoftware</c> and <c>IControl.SetDefault</c>, but their
/// effect, units and BIOS fallback are not verified on the MSI H87-G43. Therefore every
/// write is refused until a channel is documented as verified on that board.
/// </summary>
public sealed class LhmFanController : IFanController
{
    private const string LockedMessage =
        "Lüftersteuerung ist für dieses Mainboard nicht verifiziert; es werden keine Werte geschrieben.";

    public void SetPercent(FanChannel channel, double percent) => throw new NotSupportedException(LockedMessage);

    public void Reset(FanChannel channel) => throw new NotSupportedException(LockedMessage);
}
