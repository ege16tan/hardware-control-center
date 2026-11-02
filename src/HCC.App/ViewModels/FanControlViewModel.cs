using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCC.Core.Models;
using HCC.Infrastructure.Configuration;
using HCC.Infrastructure.Monitoring;

namespace HCC.App.ViewModels;

public sealed class FanControlViewModel : ObservableObject, IDisposable
{
    private readonly MetricsSampler _metricsSampler;
    private readonly FanCurveSettings _settings;
    private readonly Dispatcher _dispatcher;
    private FanChannel? _selectedChannel;
    private HardwareSensorReading? _selectedTemperatureSensor;
    private string? _selectedProfile;
    private string _profileName = "";
    private string _status = "Lüfterdaten werden geladen …";
    private string _curveMessage;
    private string _preview = "";
    private double _previewTemperature = 50;

    public FanControlViewModel(MetricsSampler metricsSampler, FanCurveSettings settings)
    {
        _metricsSampler = metricsSampler;
        _settings = settings;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _curveMessage = settings.LastPersistenceError
            ?? "Kurvenprofile werden gespeichert, aber niemals automatisch aktiviert.";

        AddPointCommand = new RelayCommand(AddPoint);
        RemovePointCommand = new RelayCommand<FanCurvePointRow>(RemovePoint);
        SaveProfileCommand = new RelayCommand(SaveProfile);
        LoadProfileCommand = new RelayCommand(LoadProfile);
        DeleteProfileCommand = new RelayCommand(DeleteProfile);

        CurvePoints.Add(CreateRow("30", "30"));
        CurvePoints.Add(CreateRow("80", "80"));
        RefreshProfiles(null);
        UpdatePreview();

        _metricsSampler.SnapshotUpdated += OnSnapshotUpdated;
        _metricsSampler.SamplingFailed += OnSamplingFailed;
        if (_metricsSampler.LatestSnapshot is { } latest) ApplySnapshot(latest);
    }

    public ObservableCollection<FanChannel> FanChannels { get; } = [];
    public ObservableCollection<HardwareSensorReading> TemperatureSensors { get; } = [];
    public ObservableCollection<FanCurvePointRow> CurvePoints { get; } = [];
    public ObservableCollection<string> Profiles { get; } = [];

    public IRelayCommand AddPointCommand { get; }
    public IRelayCommand<FanCurvePointRow> RemovePointCommand { get; }
    public IRelayCommand SaveProfileCommand { get; }
    public IRelayCommand LoadProfileCommand { get; }
    public IRelayCommand DeleteProfileCommand { get; }

    public string ControlLockedText =>
        "Steuerung gesperrt: Für das MSI H87-G43 ist kein Kanal verifiziert. HCC liest nur; BIOS und Mainboard bestimmen die Drehzahl.";

    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string CurveMessage { get => _curveMessage; private set => SetProperty(ref _curveMessage, value); }
    public string Preview { get => _preview; private set => SetProperty(ref _preview, value); }

    public double PreviewTemperature
    {
        get => _previewTemperature;
        set
        {
            if (!SetProperty(ref _previewTemperature, value)) return;
            UpdatePreview();
        }
    }

    public FanChannel? SelectedChannel
    {
        get => _selectedChannel;
        set => SetProperty(ref _selectedChannel, value);
    }

    public HardwareSensorReading? SelectedTemperatureSensor
    {
        get => _selectedTemperatureSensor;
        set => SetProperty(ref _selectedTemperatureSensor, value);
    }

    public string? SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    public string ProfileName
    {
        get => _profileName;
        set => SetProperty(ref _profileName, value);
    }

    public void Dispose()
    {
        _metricsSampler.SnapshotUpdated -= OnSnapshotUpdated;
        _metricsSampler.SamplingFailed -= OnSamplingFailed;
        foreach (var row in CurvePoints) row.Changed -= OnRowChanged;
    }

    private void OnSnapshotUpdated(MetricSnapshot snapshot) => OnUiThread(() => ApplySnapshot(snapshot));

    private void OnSamplingFailed(Exception exception) => OnUiThread(() =>
        Status = $"Sensorabfrage fehlgeschlagen; letzter Stand bleibt sichtbar: {exception.Message}");

    private void ApplySnapshot(MetricSnapshot snapshot)
    {
        var channelId = SelectedChannel?.Id;
        var sensorId = SelectedTemperatureSensor?.Identifier;

        FanChannels.Clear();
        foreach (var channel in snapshot.FanChannels ?? []) FanChannels.Add(channel);
        SelectedChannel = FanChannels.FirstOrDefault(channel => channel.Id == channelId) ?? FanChannels.FirstOrDefault();

        TemperatureSensors.Clear();
        foreach (var sensor in (snapshot.Sensors ?? [])
                     .Where(sensor => string.Equals(sensor.Type, "Temperature", StringComparison.OrdinalIgnoreCase)
                         && !string.IsNullOrWhiteSpace(sensor.Identifier)))
        {
            TemperatureSensors.Add(sensor);
        }
        SelectedTemperatureSensor = TemperatureSensors.FirstOrDefault(sensor => sensor.Identifier == sensorId)
            ?? TemperatureSensors.FirstOrDefault();

        var problems = new List<string>();
        if (snapshot.Status is not null) problems.Add(snapshot.Status);
        if (FanChannels.Count == 0) problems.Add("Keine Lüfter-Drehzahlsensoren erkannt.");
        if (TemperatureSensors.Count == 0) problems.Add("Kein Temperatursensor erkannt; eine Kurve kann nicht angewendet werden.");
        Status = problems.Count > 0
            ? string.Join(" ", problems)
            : $"{FanChannels.Count} Lüfterkanal/Kanäle und {TemperatureSensors.Count} Temperatursensor(en) erkannt.";
    }

    private void OnUiThread(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.BeginInvoke(action);
    }

    private void AddPoint()
    {
        if (CurvePoints.Count >= FanCurveEngine.MaximumPoints)
        {
            CurveMessage = $"Eine Lüfterkurve darf höchstens {FanCurveEngine.MaximumPoints} Punkte haben.";
            return;
        }

        var last = CurvePoints[^1];
        CurvePoints.Add(CreateRow(
            Increment(last.TemperatureText),
            Increment(last.PercentText)));
        CurveMessage = "Punkt hinzugefügt. Bitte Werte prüfen und Profil speichern.";
        UpdatePreview();
    }

    private void RemovePoint(FanCurvePointRow? row)
    {
        if (row is null) return;
        if (CurvePoints.Count <= FanCurveEngine.MinimumPoints)
        {
            CurveMessage = $"Eine Lüfterkurve braucht mindestens {FanCurveEngine.MinimumPoints} Punkte.";
            return;
        }

        row.Changed -= OnRowChanged;
        CurvePoints.Remove(row);
        CurveMessage = "Punkt entfernt. Bitte Werte prüfen und Profil speichern.";
        UpdatePreview();
    }

    private void SaveProfile()
    {
        if (string.IsNullOrWhiteSpace(ProfileName) || ProfileName.Trim().Length > 32)
        {
            CurveMessage = "Bitte einen Profilnamen mit 1 bis 32 Zeichen eingeben.";
            return;
        }

        if (SelectedChannel is null)
        {
            CurveMessage = "Bitte zuerst einen Lüfterkanal auswählen.";
            return;
        }

        if (SelectedTemperatureSensor?.Identifier is null)
        {
            CurveMessage = "Bitte zuerst eine Temperaturquelle auswählen.";
            return;
        }

        if (!TryBuildCurve(out var points, out var error))
        {
            CurveMessage = error;
            return;
        }

        var profile = new FanCurveProfile(
            ProfileName.Trim(),
            SelectedChannel.Id,
            SelectedTemperatureSensor.Identifier,
            points);

        if (!_settings.SaveProfile(profile))
        {
            CurveMessage = _settings.LastPersistenceError ?? "Profil konnte nicht gespeichert werden.";
            return;
        }

        RefreshProfiles(profile.Name);
        CurveMessage = $"Profil „{profile.Name}“ gespeichert. Die Steuerung bleibt inaktiv.";
    }

    private void LoadProfile()
    {
        if (SelectedProfile is null)
        {
            CurveMessage = "Bitte zuerst ein Profil auswählen.";
            return;
        }

        var profile = _settings.GetProfile(SelectedProfile);
        if (profile is null)
        {
            CurveMessage = _settings.LastPersistenceError ?? "Profil konnte nicht geladen werden.";
            return;
        }

        ProfileName = profile.Name;
        var channel = FanChannels.FirstOrDefault(candidate => candidate.Id == profile.ChannelId);
        SelectedChannel = channel;
        var sensor = TemperatureSensors.FirstOrDefault(candidate => candidate.Identifier == profile.TemperatureSensorId);
        SelectedTemperatureSensor = sensor;

        foreach (var row in CurvePoints) row.Changed -= OnRowChanged;
        CurvePoints.Clear();
        foreach (var point in profile.Points)
            CurvePoints.Add(CreateRow(point.TemperatureCelsius.ToString("0.#"), point.Percent.ToString("0.#")));

        UpdatePreview();
        var missing = channel is null ? " Der gespeicherte Kanal ist derzeit nicht vorhanden." : "";
        missing += sensor is null ? " Der gespeicherte Temperatursensor ist derzeit nicht vorhanden." : "";
        CurveMessage = $"Profil „{profile.Name}“ geladen. Die Steuerung bleibt inaktiv.{missing}";
    }

    private void DeleteProfile()
    {
        if (SelectedProfile is null)
        {
            CurveMessage = "Bitte zuerst ein Profil auswählen.";
            return;
        }

        if (!_settings.DeleteProfile(SelectedProfile))
        {
            CurveMessage = _settings.LastPersistenceError ?? "Profil konnte nicht gelöscht werden.";
            return;
        }

        var deleted = SelectedProfile;
        RefreshProfiles(null);
        CurveMessage = $"Profil „{deleted}“ gelöscht.";
    }

    private void RefreshProfiles(string? select)
    {
        Profiles.Clear();
        foreach (var profile in _settings.Profiles) Profiles.Add(profile);
        SelectedProfile = select is not null && Profiles.Contains(select) ? select : Profiles.FirstOrDefault();
    }

    private void UpdatePreview()
    {
        if (!TryBuildCurve(out var points, out _))
        {
            Preview = "Vorschau: Die Kurve ist unvollständig oder ungültig.";
            return;
        }

        if (!FanCurveEngine.TryEvaluate(points, PreviewTemperature, out var percent, out _))
        {
            Preview = "Vorschau: Der simulierte Temperaturwert ist ungültig.";
            return;
        }

        Preview = $"Vorschau bei simulierten {PreviewTemperature:0} °C: {percent:0} % – wird nicht angewendet.";
    }

    private bool TryBuildCurve(out IReadOnlyList<FanCurvePoint> points, out string error)
    {
        var curve = new List<FanCurvePoint>(CurvePoints.Count);
        foreach (var row in CurvePoints)
        {
            if (!row.TryGetPoint(out var point))
            {
                points = [];
                error = "Bitte für alle Punkte gültige Zahlen eingeben.";
                return false;
            }

            curve.Add(point);
        }

        if (!FanCurveEngine.TryValidate(curve, out var curveError))
        {
            points = [];
            error = curveError!;
            return false;
        }

        points = curve;
        error = "";
        return true;
    }

    private FanCurvePointRow CreateRow(string temperature, string percent)
    {
        var row = new FanCurvePointRow(temperature, percent);
        row.Changed += OnRowChanged;
        return row;
    }

    private void OnRowChanged(object? sender, EventArgs e) => UpdatePreview();

    private static string Increment(string text)
    {
        if (!TryParseNumber(text, out var value)) return text;
        return Math.Min(100, value + 10).ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static bool TryParseNumber(string? text, out double value)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return true;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public sealed class FanCurvePointRow : ObservableObject
    {
        private string _temperatureText;
        private string _percentText;

        public FanCurvePointRow(string temperatureText, string percentText)
        {
            _temperatureText = temperatureText;
            _percentText = percentText;
        }

        public event EventHandler? Changed;

        public string TemperatureText
        {
            get => _temperatureText;
            set
            {
                if (SetProperty(ref _temperatureText, value)) Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public string PercentText
        {
            get => _percentText;
            set
            {
                if (SetProperty(ref _percentText, value)) Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool TryGetPoint(out FanCurvePoint point)
        {
            point = new FanCurvePoint(double.NaN, double.NaN);
            if (!TryParseNumber(TemperatureText, out var temperature) || !TryParseNumber(PercentText, out var percent))
                return false;
            point = new FanCurvePoint(temperature, percent);
            return true;
        }
    }
}
