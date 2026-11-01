using CommunityToolkit.Mvvm.ComponentModel;
using HCC.Infrastructure.Configuration;

namespace HCC.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly MonitoringSettings _settings;

    public SettingsViewModel(MonitoringSettings settings)
    {
        _settings = settings;
        _selectedRefreshIntervalSeconds = settings.RefreshIntervalSeconds;
        AvailableIntervals = MonitoringSettings.AvailableIntervals;
    }

    private int _selectedRefreshIntervalSeconds;

    public IReadOnlyList<int> AvailableIntervals { get; }

    public int SelectedRefreshIntervalSeconds
    {
        get => _selectedRefreshIntervalSeconds;
        set
        {
            if (!SetProperty(ref _selectedRefreshIntervalSeconds, value)) return;
            _settings.RefreshIntervalSeconds = value;
        }
    }

    public string Message => "Die Einstellung wird lokal gespeichert und sofort auf Dashboard und Monitoring angewendet.";
}
