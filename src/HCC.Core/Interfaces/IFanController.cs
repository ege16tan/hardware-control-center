using HCC.Core.Models;

namespace HCC.Core.Interfaces;

/// <summary>Writes fan values. Implementations must reject unverified channels and invalid values.</summary>
public interface IFanController
{
    void SetPercent(FanChannel channel, double percent);

    /// <summary>Returns the channel to the BIOS / hardware default.</summary>
    void Reset(FanChannel channel);
}
