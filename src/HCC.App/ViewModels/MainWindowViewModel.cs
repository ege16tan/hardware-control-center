using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HCC.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private object _currentPage;
    private string _currentTitle = "Dashboard";
    private string _currentSubtitle = "Systemübersicht und Hardwarestatus";
    private bool _isDashboardActive = true;
    private bool _isMonitoringActive;
    private bool _isFanControlActive;
    private bool _isSettingsActive;

    public MainWindowViewModel(
        DashboardViewModel dashboard,
        MonitoringViewModel monitoring,
        FanControlViewModel fanControl,
        AlertViewModel alerts,
        SettingsViewModel settings)
    {
        Dashboard = dashboard;
        Monitoring = monitoring;
        FanControl = fanControl;
        Alerts = alerts;
        Settings = settings;
        _currentPage = dashboard;

        NavigateDashboardCommand = new RelayCommand(() => NavigateTo(Dashboard, "Dashboard", "Systemübersicht und Hardwarestatus"));
        NavigateMonitoringCommand = new RelayCommand(() => NavigateTo(Monitoring, "Monitoring", "Sensoren, Geräte und Lüfterdiagnose"));
        NavigateFanControlCommand = new RelayCommand(() => NavigateTo(FanControl, "Lüftersteuerung", "Kurven verwalten – Steuerung bleibt gesperrt"));
        NavigateSettingsCommand = new RelayCommand(() => NavigateTo(Settings, "Einstellungen", "Intervalle, Profile, Warnschwellen und Darstellung"));
    }

    public DashboardViewModel Dashboard { get; }
    public MonitoringViewModel Monitoring { get; }
    public FanControlViewModel FanControl { get; }
    public AlertViewModel Alerts { get; }
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

    public string CurrentSubtitle
    {
        get => _currentSubtitle;
        private set => SetProperty(ref _currentSubtitle, value);
    }

    public IRelayCommand NavigateDashboardCommand { get; }
    public IRelayCommand NavigateMonitoringCommand { get; }
    public IRelayCommand NavigateFanControlCommand { get; }
    public IRelayCommand NavigateSettingsCommand { get; }

    public bool IsDashboardActive
    {
        get => _isDashboardActive;
        private set => SetProperty(ref _isDashboardActive, value);
    }

    public bool IsMonitoringActive
    {
        get => _isMonitoringActive;
        private set => SetProperty(ref _isMonitoringActive, value);
    }

    public bool IsFanControlActive
    {
        get => _isFanControlActive;
        private set => SetProperty(ref _isFanControlActive, value);
    }

    public bool IsSettingsActive
    {
        get => _isSettingsActive;
        private set => SetProperty(ref _isSettingsActive, value);
    }

    private void NavigateTo(object page, string title, string subtitle)
    {
        CurrentPage = page;
        CurrentTitle = title;
        CurrentSubtitle = subtitle;

        IsDashboardActive = ReferenceEquals(page, Dashboard);
        IsMonitoringActive = ReferenceEquals(page, Monitoring);
        IsFanControlActive = ReferenceEquals(page, FanControl);
        IsSettingsActive = ReferenceEquals(page, Settings);
    }
}
