using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        foreach (var profile in settings.Profiles) Profiles.Add(profile);
        if (Profiles.Count > 0) SelectedProfile = Profiles[0];
        SaveProfileCommand = new RelayCommand(SaveProfile);
        ApplyProfileCommand = new RelayCommand(ApplyProfile);
        DeleteProfileCommand = new RelayCommand(DeleteProfile);
    }

    private int _selectedRefreshIntervalSeconds;

    public IReadOnlyList<int> AvailableIntervals { get; }
    public ObservableCollection<string> Profiles { get; } = [];
    public IRelayCommand SaveProfileCommand { get; }
    public IRelayCommand ApplyProfileCommand { get; }
    public IRelayCommand DeleteProfileCommand { get; }

    private string _profileName = "";
    private string? _selectedProfile;
    private string _profileMessage = "Profile speichern das Aktualisierungsintervall.";

    public string ProfileName { get => _profileName; set => SetProperty(ref _profileName, value); }
    public string? SelectedProfile { get => _selectedProfile; set => SetProperty(ref _selectedProfile, value); }
    public string ProfileMessage { get => _profileMessage; private set => SetProperty(ref _profileMessage, value); }

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

    private void SaveProfile()
    {
        if (!_settings.SaveProfile(ProfileName))
        {
            ProfileMessage = "Bitte einen Namen mit 1 bis 32 Zeichen eingeben.";
            return;
        }
        RefreshProfiles(ProfileName.Trim());
        ProfileMessage = $"Profil „{SelectedProfile}“ gespeichert.";
    }

    private void ApplyProfile()
    {
        if (!_settings.ApplyProfile(SelectedProfile))
        {
            ProfileMessage = "Profil konnte nicht geladen werden.";
            return;
        }
        SelectedRefreshIntervalSeconds = _settings.RefreshIntervalSeconds;
        ProfileMessage = $"Profil „{SelectedProfile}“ angewendet.";
    }

    private void DeleteProfile()
    {
        if (!_settings.DeleteProfile(SelectedProfile))
        {
            ProfileMessage = "Bitte zuerst ein Profil auswählen.";
            return;
        }
        var deleted = SelectedProfile;
        RefreshProfiles(null);
        ProfileMessage = $"Profil „{deleted}“ gelöscht.";
    }

    private void RefreshProfiles(string? select)
    {
        Profiles.Clear();
        foreach (var profile in _settings.Profiles) Profiles.Add(profile);
        SelectedProfile = select is not null && Profiles.Contains(select) ? select : Profiles.FirstOrDefault();
    }
}
