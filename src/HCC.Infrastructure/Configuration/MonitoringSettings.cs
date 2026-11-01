using System.Text.Json;

namespace HCC.Infrastructure.Configuration;

public sealed class MonitoringSettings
{
    private static readonly int[] SupportedIntervals = [1, 2, 5, 10];
    private readonly string _settingsPath;
    private int _refreshIntervalSeconds = 2;
    private readonly Dictionary<string, int> _profiles = new(StringComparer.OrdinalIgnoreCase);
    public string? LastPersistenceError { get; private set; }

    public MonitoringSettings(string? storageDirectory = null)
    {
        var root = storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HardwareControlCenter");
        _settingsPath = Path.Combine(root, "settings.json");
    }

    public event EventHandler? RefreshIntervalChanged;

    public int RefreshIntervalSeconds
    {
        get => _refreshIntervalSeconds;
        set => TrySetRefreshIntervalSeconds(value);
    }

    public static IReadOnlyList<int> AvailableIntervals => SupportedIntervals;
    public IReadOnlyList<string> Profiles => _profiles.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray();

    public bool SaveProfile(string? name)
    {
        var normalizedName = NormalizeProfileName(name);
        if (normalizedName is null) return false;
        var existed = _profiles.TryGetValue(normalizedName, out var previousInterval);
        _profiles[normalizedName] = RefreshIntervalSeconds;
        if (Save()) return true;
        if (existed) _profiles[normalizedName] = previousInterval;
        else _profiles.Remove(normalizedName);
        return false;
    }

    public bool ApplyProfile(string? name)
    {
        var normalizedName = NormalizeProfileName(name);
        return normalizedName is not null && _profiles.TryGetValue(normalizedName, out var interval) && TrySetRefreshIntervalSeconds(interval);
    }

    public bool DeleteProfile(string? name)
    {
        var normalizedName = NormalizeProfileName(name);
        if (normalizedName is null || !_profiles.Remove(normalizedName, out var previousInterval)) return false;
        if (Save()) return true;
        _profiles[normalizedName] = previousInterval;
        return false;
    }

    public bool TrySetRefreshIntervalSeconds(int seconds)
    {
        if (!SupportedIntervals.Contains(seconds)) return false;
        if (_refreshIntervalSeconds == seconds) return true;
        var previous = _refreshIntervalSeconds;
        _refreshIntervalSeconds = seconds;
        if (!Save())
        {
            _refreshIntervalSeconds = previous;
            return false;
        }
        RefreshIntervalChanged?.Invoke(this, EventArgs.Empty);
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
                        if (profile is null) continue;
                        var name = NormalizeProfileName(profile.Name);
                        if (name is not null && SupportedIntervals.Contains(profile.RefreshIntervalSeconds)) settings._profiles[name] = profile.RefreshIntervalSeconds;
                    }
                    return settings;
                }
            }
        }
        catch (IOException ex) { settings.LastPersistenceError = $"Einstellungen konnten nicht geladen werden: {ex.Message}"; }
        catch (JsonException ex) { settings.LastPersistenceError = $"Einstellungsdatei ist beschädigt: {ex.Message}"; }
        catch (UnauthorizedAccessException ex) { settings.LastPersistenceError = $"Einstellungen dürfen nicht gelesen werden: {ex.Message}"; }
        return settings;
    }

    private bool Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = _settingsPath + ".tmp";
            var profiles = _profiles.Select(profile => new SavedProfile(profile.Key, profile.Value)).ToList();
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new SavedSettings(_refreshIntervalSeconds, profiles)));
            File.Move(temporaryPath, _settingsPath, overwrite: true);
            LastPersistenceError = null;
            return true;
        }
        catch (IOException ex) { LastPersistenceError = $"Einstellungen konnten nicht gespeichert werden: {ex.Message}"; return false; }
        catch (UnauthorizedAccessException ex) { LastPersistenceError = $"Einstellungen dürfen nicht gespeichert werden: {ex.Message}"; return false; }
    }

    private static string? NormalizeProfileName(string? name)
    {
        var normalized = name?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > 32 ? null : normalized;
    }

    private sealed record SavedSettings(int RefreshIntervalSeconds, List<SavedProfile>? Profiles = null);
    private sealed record SavedProfile(string Name, int RefreshIntervalSeconds);
}
