using HCC.Core.Models;

namespace HCC.Core.Interfaces;

public interface IMetricsProvider
{
    MetricSnapshot ReadCurrent();
}
