using System.Text.Json;

namespace HCC.Infrastructure.Configuration;

/// <summary>Stores the selected UI theme. Theme names are stable tokens, not display labels.</summary>
public sealed class ThemeSettings
{
    public const string Dark = "Dark";
    public const string Light = "Light";

    private readonly string _settingsPath;
    private string _theme = Dark;

    public ThemeSettings(string? storageDirectory = null)
    {
        var root = storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HardwareControlCenter");
        _settingsPath = Path.Combine(root, "theme.json");
    }

    public event EventHandler? Changed;

    public string? LastPersistenceError { get; private set; }
    public string Theme => _theme;

    public static bool IsValidTheme(string? theme) =>
        string.Equals(theme, Dark, StringComparison.OrdinalIgnoreCase)
        || string.Equals(theme, Light, StringComparison.OrdinalIgnoreCase);

    public bool TrySetTheme(string? theme)
    {
        if (!IsValidTheme(theme))
        {
            LastPersistenceError = "Unbekanntes Theme. Erlaubt sind Dark und Light.";
            return false;
        }

        var normalized = string.Equals(theme, Light, StringComparison.OrdinalIgnoreCase) ? Light : Dark;
        if (normalized == _theme) return true;

        var previous = _theme;
        _theme = normalized;
        if (Save())
        {
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        _theme = previous;
        return false;
    }

    public static ThemeSettings Load(string? storageDirectory = null)
    {
        var settings = new ThemeSettings(storageDirectory);
        try
        {
            if (!File.Exists(settings._settingsPath)) return settings;

            var saved = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(settings._settingsPath));
            if (saved is not null && IsValidTheme(saved.Theme))
                settings._theme = string.Equals(saved.Theme, Light, StringComparison.OrdinalIgnoreCase) ? Light : Dark;
            else
                settings.LastPersistenceError = "Das gespeicherte Theme ist ungültig; das dunkle Theme gilt.";
        }
        catch (IOException ex) { settings.LastPersistenceError = $"Theme konnte nicht geladen werden: {ex.Message}"; }
        catch (JsonException ex) { settings.LastPersistenceError = $"Die Theme-Datei ist beschädigt: {ex.Message}"; }
        catch (UnauthorizedAccessException ex) { settings.LastPersistenceError = $"Theme darf nicht gelesen werden: {ex.Message}"; }
        return settings;
    }

    private bool Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = _settingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new SavedSettings(_theme)));
            File.Move(temporaryPath, _settingsPath, overwrite: true);
            LastPersistenceError = null;
            return true;
        }
        catch (IOException ex) { LastPersistenceError = $"Theme konnte nicht gespeichert werden: {ex.Message}"; return false; }
        catch (UnauthorizedAccessException ex) { LastPersistenceError = $"Theme darf nicht gespeichert werden: {ex.Message}"; return false; }
    }

    private sealed record SavedSettings(string Theme);
}
