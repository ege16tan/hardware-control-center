using System.Text.Json;

namespace HCC.Infrastructure.Configuration;

public sealed class MonitoringSettings
{
    private static readonly int[] SupportedIntervals = [1, 2, 5, 10];
    private readonly string _settingsPath;
    private int _refreshIntervalSeconds = 2;
    private readonly Dictionary<string, int> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public MonitoringSettings(string? storageDirectory = null)
    {
        var root = storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HardwareControlCenter");
        _settingsPath = Path.Combine(root, "settings.json");
    }

    public event EventHandler? RefreshIntervalChanged;

    public int RefreshIntervalSeconds
    {
        get => _refreshIntervalSeconds;
        set
        {
            if (!SupportedIntervals.Contains(value) || _refreshIntervalSeconds == value) return;
            _refreshIntervalSeconds = value;
            Save();
            RefreshIntervalChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public static IReadOnlyList<int> AvailableIntervals => SupportedIntervals;
    public IReadOnlyList<string> Profiles => _profiles.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray();

    public bool SaveProfile(string? name)
    {
        var normalizedName = NormalizeProfileName(name);
        if (normalizedName is null) return false;
        _profiles[normalizedName] = RefreshIntervalSeconds;
        Save();
        return true;
    }

    public bool ApplyProfile(string? name)
    {
        var normalizedName = NormalizeProfileName(name);
        return normalizedName is not null && _profiles.TryGetValue(normalizedName, out var interval) && SupportedIntervals.Contains(interval)
            && ApplyRefreshInterval(interval);
    }

    public bool DeleteProfile(string? name)
    {
        var normalizedName = NormalizeProfileName(name);
        if (normalizedName is null || !_profiles.Remove(normalizedName)) return false;
        Save();
        return true;
    }

    public static MonitoringSettings Load(string? storageDirectory = null)
    {
        var settings = new MonitoringSettings(storageDirectory);
        try
        {
            if (File.Exists(settings._settingsPath))
            {
                var saved = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(settings._settingsPath));
                if (saved is not null)
                {
                    if (saved.RefreshIntervalSeconds is int seconds && SupportedIntervals.Contains(seconds)) settings._refreshIntervalSeconds = seconds;
                    foreach (var profile in saved.Profiles ?? [])
                    {
                        var name = NormalizeProfileName(profile.Name);
                        if (name is not null && SupportedIntervals.Contains(profile.RefreshIntervalSeconds)) settings._profiles[name] = profile.RefreshIntervalSeconds;
                    }
                    return settings;
                }
            }
        }
        catch (IOException) { }
        catch (JsonException) { }
        catch (UnauthorizedAccessException) { }
        return settings;
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = _settingsPath + ".tmp";
            var profiles = _profiles.Select(profile => new SavedProfile(profile.Key, profile.Value)).ToList();
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new SavedSettings(_refreshIntervalSeconds, profiles)));
            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private bool ApplyRefreshInterval(int seconds)
    {
        if (!SupportedIntervals.Contains(seconds)) return false;
        if (_refreshIntervalSeconds == seconds) return true;
        RefreshIntervalSeconds = seconds;
        return true;
    }

    private static string? NormalizeProfileName(string? name)
    {
        var normalized = name?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > 32 ? null : normalized;
    }

    private sealed record SavedSettings(int RefreshIntervalSeconds, List<SavedProfile>? Profiles = null);
    private sealed record SavedProfile(string Name, int RefreshIntervalSeconds);
}
