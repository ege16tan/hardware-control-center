using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HCC.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private object _currentPage;
    private string _currentTitle = "Dashboard";

    public MainWindowViewModel(
        DashboardViewModel dashboard,
        MonitoringViewModel monitoring,
        SettingsViewModel settings)
    {
        Dashboard = dashboard;
        Monitoring = monitoring;
        Settings = settings;
        _currentPage = dashboard;

        NavigateDashboardCommand = new RelayCommand(() => NavigateTo(Dashboard, "Dashboard"));
        NavigateMonitoringCommand = new RelayCommand(() => NavigateTo(Monitoring, "Monitoring"));
        NavigateSettingsCommand = new RelayCommand(() => NavigateTo(Settings, "Einstellungen"));
    }

    public DashboardViewModel Dashboard { get; }
    public MonitoringViewModel Monitoring { get; }
    public SettingsViewModel Settings { get; }

    public object CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string CurrentTitle
    {
        get => _currentTitle;
        private set => SetProperty(ref _currentTitle, value);
    }

    public IRelayCommand NavigateDashboardCommand { get; }
    public IRelayCommand NavigateMonitoringCommand { get; }
    public IRelayCommand NavigateSettingsCommand { get; }

    private void NavigateTo(object page, string title)
    {
        CurrentPage = page;
        CurrentTitle = title;
    }
}
