using HCC.App.Theming;
using HCC.App.ViewModels;
using HCC.App.Views;
using HCC.Infrastructure.DependencyInjection;
using HCC.Infrastructure.Configuration;
using HCC.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace HCC.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var themeSettings = ThemeSettings.Load();
        ThemeManager.Apply(themeSettings.Theme);

        var services = new ServiceCollection();
        services.AddHardwareControlCenter();
        services.AddSingleton(_ => MonitoringSettings.Load());
        services.AddSingleton(_ => FanCurveSettings.Load());
        services.AddSingleton(_ => AlertSettings.Load());
        services.AddSingleton(_ => themeSettings);
        services.AddSingleton<MetricHistoryStore>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<MonitoringViewModel>();
        services.AddSingleton<FanControlViewModel>();
        services.AddSingleton<AlertViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        _serviceProvider = services.BuildServiceProvider();

        _serviceProvider.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
