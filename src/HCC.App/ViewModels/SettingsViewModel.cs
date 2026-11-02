using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCC.App.Theming;
using HCC.Infrastructure.Configuration;

namespace HCC.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly MonitoringSettings _settings;
    private readonly AlertSettings _alertSettings;
    private readonly ThemeSettings _themeSettings;

    public SettingsViewModel(MonitoringSettings settings, AlertSettings alertSettings, ThemeSettings themeSettings)
    {
        _settings = settings;
        _alertSettings = alertSettings;
        _themeSettings = themeSettings;
        _selectedRefreshIntervalSeconds = settings.RefreshIntervalSeconds;
        _alertWarningText = alertSettings.TemperatureWarningCelsius.ToString("0.#", CultureInfo.CurrentCulture);
        _alertMessage = alertSettings.LastPersistenceError
            ?? "Temperaturen über dieser Schwelle lösen einen sichtbaren Alarm aus.";
        _selectedTheme = themeSettings.Theme == ThemeSettings.Light ? "Hell" : "Dunkel";
        _themeMessage = themeSettings.LastPersistenceError
            ?? "Das Theme wird gespeichert und sofort angewendet.";
        AvailableIntervals = MonitoringSettings.AvailableIntervals;
        foreach (var profile in settings.Profiles) Profiles.Add(profile);
        if (Profiles.Count > 0) SelectedProfile = Profiles[0];
        if (settings.LastPersistenceError is not null) _profileMessage = settings.LastPersistenceError;
        SaveProfileCommand = new RelayCommand(SaveProfile);
        ApplyProfileCommand = new RelayCommand(ApplyProfile);
        DeleteProfileCommand = new RelayCommand(DeleteProfile);
        SaveAlertCommand = new RelayCommand(SaveAlert);
    }

    private int _selectedRefreshIntervalSeconds;
    private string _alertWarningText;
    private string _alertMessage;
    private string _selectedTheme;
    private string _themeMessage;

    public IReadOnlyList<int> AvailableIntervals { get; }
    public IReadOnlyList<string> AvailableThemes { get; } = ["Dunkel", "Hell"];
    public ObservableCollection<string> Profiles { get; } = [];
    public IRelayCommand SaveProfileCommand { get; }
    public IRelayCommand ApplyProfileCommand { get; }
    public IRelayCommand DeleteProfileCommand { get; }
    public IRelayCommand SaveAlertCommand { get; }

    private string _profileName = "";
    private string? _selectedProfile;
    private string _profileMessage = "Profile speichern das Aktualisierungsintervall.";

    public string ProfileName { get => _profileName; set => SetProperty(ref _profileName, value); }
    public string? SelectedProfile { get => _selectedProfile; set => SetProperty(ref _selectedProfile, value); }
    public string ProfileMessage { get => _profileMessage; private set => SetProperty(ref _profileMessage, value); }
    public string AlertWarningText { get => _alertWarningText; set => SetProperty(ref _alertWarningText, value); }
    public string AlertMessage { get => _alertMessage; private set => SetProperty(ref _alertMessage, value); }
    public string ThemeMessage { get => _themeMessage; private set => SetProperty(ref _themeMessage, value); }

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            // The ComboBox briefly pushes null while it initializes; do not persist that.
            if (string.IsNullOrEmpty(value) || value == _selectedTheme) return;

            var theme = value == "Hell" ? ThemeSettings.Light : ThemeSettings.Dark;
            if (!_themeSettings.TrySetTheme(theme))
            {
                ThemeMessage = _themeSettings.LastPersistenceError ?? "Theme konnte nicht gespeichert werden.";
                OnPropertyChanged();
                return;
            }

            SetProperty(ref _selectedTheme, value);
            ThemeManager.Apply(theme);
            ThemeMessage = $"Theme „{value}“ gespeichert.";
        }
    }

    public int SelectedRefreshIntervalSeconds
    {
        get => _selectedRefreshIntervalSeconds;
        set
        {
            if (value == _selectedRefreshIntervalSeconds) return;
            if (!_settings.TrySetRefreshIntervalSeconds(value))
            {
                ProfileMessage = _settings.LastPersistenceError ?? "Dieses Aktualisierungsintervall wird nicht unterstützt.";
                OnPropertyChanged();
                return;
            }
            SetProperty(ref _selectedRefreshIntervalSeconds, value);
            ProfileMessage = "Aktualisierungsintervall gespeichert.";
        }
    }

    public string Message => "Die Einstellung wird lokal gespeichert und sofort auf Dashboard und Monitoring angewendet.";

    private void SaveProfile()
    {
        if (string.IsNullOrWhiteSpace(ProfileName) || ProfileName.Trim().Length > 32)
        {
            ProfileMessage = "Bitte einen Namen mit 1 bis 32 Zeichen eingeben.";
            return;
        }
        if (!_settings.SaveProfile(ProfileName))
        {
            ProfileMessage = _settings.LastPersistenceError ?? "Bitte einen Namen mit 1 bis 32 Zeichen eingeben.";
            return;
        }
        RefreshProfiles(ProfileName.Trim());
        ProfileMessage = $"Profil „{SelectedProfile}“ gespeichert.";
    }

    private void ApplyProfile()
    {
        if (!_settings.ApplyProfile(SelectedProfile))
        {
            ProfileMessage = _settings.LastPersistenceError ?? "Profil konnte nicht geladen werden.";
            return;
        }
        SelectedRefreshIntervalSeconds = _settings.RefreshIntervalSeconds;
        ProfileMessage = $"Profil „{SelectedProfile}“ angewendet.";
    }

    private void DeleteProfile()
    {
        if (SelectedProfile is null)
        {
            ProfileMessage = "Bitte zuerst ein Profil auswählen.";
            return;
        }
        if (!_settings.DeleteProfile(SelectedProfile))
        {
            ProfileMessage = _settings.LastPersistenceError ?? "Bitte zuerst ein Profil auswählen.";
            return;
        }
        var deleted = SelectedProfile;
        RefreshProfiles(null);
        ProfileMessage = $"Profil „{deleted}“ gelöscht.";
    }

    private void SaveAlert()
    {
        if (!TryParseNumber(AlertWarningText, out var value))
        {
            AlertMessage = "Bitte eine Zahl zwischen 1 und 150 eingeben.";
            return;
        }

        if (!_alertSettings.TrySetTemperatureWarningCelsius(value))
        {
            AlertMessage = _alertSettings.LastPersistenceError ?? "Warnschwelle konnte nicht gespeichert werden.";
            return;
        }

        AlertWarningText = value.ToString("0.#", CultureInfo.CurrentCulture);
        AlertMessage = $"Warnschwelle auf {value:0.#} °C gespeichert.";
    }

    private static bool TryParseNumber(string? text, out double value)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return true;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private void RefreshProfiles(string? select)
    {
        Profiles.Clear();
        foreach (var profile in _settings.Profiles) Profiles.Add(profile);
        SelectedProfile = select is not null && Profiles.Contains(select) ? select : Profiles.FirstOrDefault();
    }
}
