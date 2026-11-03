using HCC.Core.Interfaces;
using HCC.Hardware.Controllers;
using HCC.Hardware.Monitoring;
using HCC.Infrastructure.Monitoring;
using Microsoft.Extensions.DependencyInjection;

namespace HCC.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHardwareControlCenter(this IServiceCollection services)
    {
        services.AddSingleton<HardwareMetricsProvider>();
        services.AddSingleton<IMetricsProvider>(provider => provider.GetRequiredService<HardwareMetricsProvider>());
        services.AddSingleton<MetricsSampler>();
        services.AddSingleton<IFanController, LhmFanController>();
        // Explicit factory: the safety limits and the clock are constructor defaults,
        // not services, so the container must not try to resolve them.
        services.AddSingleton(provider => new FanControlService(provider.GetRequiredService<IFanController>()));
        return services;
    }
}
