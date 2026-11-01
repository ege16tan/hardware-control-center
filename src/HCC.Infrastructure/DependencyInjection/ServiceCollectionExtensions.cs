using HCC.Core.Interfaces;
using HCC.Hardware.Monitoring;
using Microsoft.Extensions.DependencyInjection;

namespace HCC.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHardwareControlCenter(this IServiceCollection services)
    {
        services.AddSingleton<HardwareMetricsProvider>();
        services.AddSingleton<IMetricsProvider>(provider => provider.GetRequiredService<HardwareMetricsProvider>());
        return services;
    }
}
