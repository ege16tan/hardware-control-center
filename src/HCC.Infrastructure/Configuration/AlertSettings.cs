using System.Text.Json;

namespace HCC.Infrastructure.Configuration;

public sealed class AlertSettings
{
    public const double DefaultTemperatureWarningCelsius = 85;
    public const double MinimumTemperatureWarningCelsius = 1;
    public const double MaximumTemperatureWarningCelsius = 150;

    private readonly string _settingsPath;
    private double _temperatureWarningCelsius = DefaultTemperatureWarningCelsius;

    public AlertSettings(string? storageDirectory = null)
    {
        var root = storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HardwareControlCenter");
        _settingsPath = Path.Combine(root, "alerts.json");
    }

    public event EventHandler? Changed;

    public string? LastPersistenceError { get; private set; }
    public double TemperatureWarningCelsius => _temperatureWarningCelsius;

    public bool TrySetTemperatureWarningCelsius(double value)
    {
        if (!IsValidTemperature(value))
        {
            LastPersistenceError = $"Die Warnschwelle muss zwischen {MinimumTemperatureWarningCelsius:0} und {MaximumTemperatureWarningCelsius:0} °C liegen.";
            return false;
        }

        if (value == _temperatureWarningCelsius) return true;

        var previous = _temperatureWarningCelsius;
        _temperatureWarningCelsius = value;
        if (Save())
        {
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        _temperatureWarningCelsius = previous;
        return false;
    }

    public static AlertSettings Load(string? storageDirectory = null)
    {
        var settings = new AlertSettings(storageDirectory);
        try
        {
            if (!File.Exists(settings._settingsPath)) return settings;

            var saved = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(settings._settingsPath));
            if (saved is not null && IsValidTemperature(saved.TemperatureWarningCelsius))
                settings._temperatureWarningCelsius = saved.TemperatureWarningCelsius;
            else
                settings.LastPersistenceError = "Die gespeicherte Warnschwelle ist ungültig; der Standardwert gilt.";
        }
        catch (IOException ex) { settings.LastPersistenceError = $"Warnschwellen konnten nicht geladen werden: {ex.Message}"; }
        catch (JsonException ex) { settings.LastPersistenceError = $"Die Warnschwellen-Datei ist beschädigt: {ex.Message}"; }
        catch (UnauthorizedAccessException ex) { settings.LastPersistenceError = $"Warnschwellen dürfen nicht gelesen werden: {ex.Message}"; }
        return settings;
    }

    private bool Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = _settingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new SavedSettings(_temperatureWarningCelsius)));
            File.Move(temporaryPath, _settingsPath, overwrite: true);
            LastPersistenceError = null;
            return true;
        }
        catch (IOException ex) { LastPersistenceError = $"Warnschwellen konnten nicht gespeichert werden: {ex.Message}"; return false; }
        catch (UnauthorizedAccessException ex) { LastPersistenceError = $"Warnschwellen dürfen nicht gespeichert werden: {ex.Message}"; return false; }
    }

    private static bool IsValidTemperature(double value) =>
        double.IsFinite(value)
        && value >= MinimumTemperatureWarningCelsius
        && value <= MaximumTemperatureWarningCelsius;

    private sealed record SavedSettings(double TemperatureWarningCelsius);
}
