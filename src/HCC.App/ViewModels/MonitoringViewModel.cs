using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCC.Core.Models;
using HCC.Infrastructure.Configuration;
using HCC.Infrastructure.Monitoring;

namespace HCC.App.ViewModels;

public sealed class MonitoringViewModel : ObservableObject, IDisposable
{
    private readonly MetricsSampler _metricsSampler;
    private readonly MonitoringSettings _settings;
    private readonly Dispatcher _dispatcher;
    private string _status = "Sensoren werden geladen …";
    private string _fanStatus = "Lüfter werden geladen …";

    public MonitoringViewModel(MetricsSampler metricsSampler, MonitoringSettings settings)
    {
        _metricsSampler = metricsSampler;
        _settings = settings;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        _settings.RefreshIntervalChanged += OnRefreshIntervalChanged;
        _metricsSampler.SnapshotUpdated += OnSnapshotUpdated;
        _metricsSampler.SamplingFailed += OnSamplingFailed;
        if (_metricsSampler.LatestSnapshot is { } latest) ApplySnapshot(latest);
        else _ = RefreshAsync();
    }

    public ObservableCollection<HardwareSensorReading> Sensors { get; } = [];
    public ObservableCollection<HardwareDeviceReading> HardwareDevices { get; } = [];
    public ObservableCollection<FanChannel> FanChannels { get; } = [];
    public IAsyncRelayCommand RefreshCommand { get; }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string FanStatus { get => _fanStatus; private set => SetProperty(ref _fanStatus, value); }

    public void Dispose()
    {
        _settings.RefreshIntervalChanged -= OnRefreshIntervalChanged;
        _metricsSampler.SnapshotUpdated -= OnSnapshotUpdated;
        _metricsSampler.SamplingFailed -= OnSamplingFailed;
    }

    private async Task RefreshAsync()
    {
        try { await _metricsSampler.RefreshNowAsync(); }
        catch (Exception ex) { OnSamplingFailed(ex); }
    }

    private void OnRefreshIntervalChanged(object? sender, EventArgs e) => OnUiThread(() =>
    {
        if (Sensors.Count > 0) Status = $"{HardwareDevices.Count} Geräte · {Sensors.Count} Sensorwerte · Aktualisierung alle {_settings.RefreshIntervalSeconds} Sekunden";
    });

    private void OnSnapshotUpdated(MetricSnapshot snapshot) => OnUiThread(() => ApplySnapshot(snapshot));

    private void OnSamplingFailed(Exception exception) => OnUiThread(() =>
        Status = $"Sensorabfrage fehlgeschlagen; letzter Stand bleibt sichtbar: {exception.Message}");

    private void ApplySnapshot(MetricSnapshot snapshot)
    {
        Sensors.Clear();
        foreach (var sensor in snapshot.Sensors ?? []) Sensors.Add(sensor);
        HardwareDevices.Clear();
        foreach (var device in snapshot.HardwareDevices ?? []) HardwareDevices.Add(device);
        FanChannels.Clear();
        foreach (var channel in snapshot.FanChannels ?? []) FanChannels.Add(channel);
        FanStatus = FanChannels.Count == 0
            ? "Keine Lüfter-Drehzahlsensoren gefunden."
            : $"{FanChannels.Count} Lüfter-Drehzahlsensor(en) erkannt. Die Steuerung bleibt deaktiviert, bis die Anschlüsse am MSI H87-G43 geprüft wurden.";
        Status = snapshot.Status ?? (Sensors.Count == 0
            ? "Keine unterstützten Sensoren gefunden."
            : $"{HardwareDevices.Count} Geräte · {Sensors.Count} Sensorwerte · Aktualisierung alle {_settings.RefreshIntervalSeconds} Sekunden");
    }

    private void OnUiThread(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.BeginInvoke(action);
    }
}
