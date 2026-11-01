using System.Text.Json;

namespace HCC.Infrastructure.Configuration;

public sealed class MonitoringSettings
{
    private static readonly int[] SupportedIntervals = [1, 2, 5, 10];
    private readonly string _settingsPath;
    private int _refreshIntervalSeconds = 2;

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

    public static MonitoringSettings Load(string? storageDirectory = null)
    {
        var settings = new MonitoringSettings(storageDirectory);
        try
        {
            if (File.Exists(settings._settingsPath))
            {
                var saved = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(settings._settingsPath));
                if (saved?.RefreshIntervalSeconds is int seconds && SupportedIntervals.Contains(seconds))
                {
                    settings._refreshIntervalSeconds = seconds;
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
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new SavedSettings(_refreshIntervalSeconds)));
            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed record SavedSettings(int RefreshIntervalSeconds);
}
