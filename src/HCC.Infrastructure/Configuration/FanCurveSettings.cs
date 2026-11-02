using System.Text.Json;
using HCC.Core.Models;
using HCC.Infrastructure.Monitoring;

namespace HCC.Infrastructure.Configuration;

/// <summary>
/// Stores fan-curve profiles in their own JSON file. A profile never carries an active
/// flag, so loading one can never start fan control.
/// </summary>
public sealed class FanCurveSettings
{
    private readonly string _settingsPath;
    private readonly Dictionary<string, FanCurveProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public FanCurveSettings(string? storageDirectory = null)
    {
        var root = storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HardwareControlCenter");
        _settingsPath = Path.Combine(root, "fan-curves.json");
    }

    public string? LastPersistenceError { get; private set; }
    public IReadOnlyList<string> Profiles => _profiles.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray();

    public FanCurveProfile? GetProfile(string? name)
    {
        var normalizedName = NormalizeProfileName(name);
        return normalizedName is not null && _profiles.TryGetValue(normalizedName, out var profile) ? profile : null;
    }

    public bool SaveProfile(FanCurveProfile? profile)
    {
        if (!TryNormalize(profile, out var normalized, out var error))
        {
            LastPersistenceError = error;
            return false;
        }

        var existed = _profiles.TryGetValue(normalized.Name, out var previous);
        _profiles[normalized.Name] = normalized;
        if (Save()) return true;

        if (existed) _profiles[normalized.Name] = previous!;
        else _profiles.Remove(normalized.Name);
        return false;
    }

    public bool DeleteProfile(string? name)
    {
        var normalizedName = NormalizeProfileName(name);
        if (normalizedName is null || !_profiles.Remove(normalizedName, out var previous)) return false;
        if (Save()) return true;
        _profiles[normalizedName] = previous;
        return false;
    }

    public static FanCurveSettings Load(string? storageDirectory = null)
    {
        var settings = new FanCurveSettings(storageDirectory);
        try
        {
            if (!File.Exists(settings._settingsPath)) return settings;

            var saved = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(settings._settingsPath));
            var discarded = 0;
            foreach (var profile in saved?.Profiles ?? [])
            {
                if (!TryNormalize(profile, out var normalized, out _))
                {
                    discarded++;
                    continue;
                }

                settings._profiles[normalized.Name] = normalized;
            }

            if (discarded > 0)
                settings.LastPersistenceError = $"{discarded} gespeicherte Lüfterkurve(n) wurden wegen ungültiger Daten verworfen.";
        }
        catch (IOException ex) { settings.LastPersistenceError = $"Lüfterkurven konnten nicht geladen werden: {ex.Message}"; }
        catch (JsonException ex) { settings.LastPersistenceError = $"Die Lüfterkurven-Datei ist beschädigt: {ex.Message}"; }
        catch (UnauthorizedAccessException ex) { settings.LastPersistenceError = $"Lüfterkurven dürfen nicht gelesen werden: {ex.Message}"; }
        return settings;
    }

    private bool Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = _settingsPath + ".tmp";
            var profiles = _profiles.Values.ToList();
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new SavedSettings(profiles)));
            File.Move(temporaryPath, _settingsPath, overwrite: true);
            LastPersistenceError = null;
            return true;
        }
        catch (IOException ex) { LastPersistenceError = $"Lüfterkurven konnten nicht gespeichert werden: {ex.Message}"; return false; }
        catch (UnauthorizedAccessException ex) { LastPersistenceError = $"Lüfterkurven dürfen nicht gespeichert werden: {ex.Message}"; return false; }
    }

    private static bool TryNormalize(FanCurveProfile? profile, out FanCurveProfile normalized, out string error)
    {
        normalized = profile!;
        if (profile is null)
        {
            error = "Das Lüfterkurven-Profil fehlt.";
            return false;
        }

        var name = NormalizeProfileName(profile.Name);
        if (name is null)
        {
            error = "Bitte einen Profilnamen mit 1 bis 32 Zeichen eingeben.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(profile.ChannelId) || string.IsNullOrWhiteSpace(profile.TemperatureSensorId))
        {
            error = "Kanal und Temperaturquelle müssen ausgewählt sein.";
            return false;
        }

        if (!FanCurveEngine.TryValidate(profile.Points, out var curveError))
        {
            error = curveError!;
            return false;
        }

        normalized = new FanCurveProfile(name, profile.ChannelId, profile.TemperatureSensorId, profile.Points.ToArray());
        error = "";
        return true;
    }

    private static string? NormalizeProfileName(string? name)
    {
        var normalized = name?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > 32 ? null : normalized;
    }

    private sealed record SavedSettings(List<FanCurveProfile>? Profiles = null);
}
