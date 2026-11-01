using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCC.Core.Interfaces;
using HCC.Core.Models;
using HCC.Infrastructure.Configuration;

namespace HCC.App.ViewModels;

public sealed class MonitoringViewModel : ObservableObject, IDisposable
{
    private readonly IMetricsProvider _metricsProvider;
    private readonly MonitoringSettings _settings;
    private readonly DispatcherTimer _timer;
    private string _status = "Sensoren werden geladen …";

    public MonitoringViewModel(IMetricsProvider metricsProvider, MonitoringSettings settings)
    {
        _metricsProvider = metricsProvider;
        _settings = settings;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_settings.RefreshIntervalSeconds) };
        RefreshCommand = new RelayCommand(Refresh);
        _timer.Tick += OnTimerTick;
        _settings.RefreshIntervalChanged += OnRefreshIntervalChanged;
        Refresh();
        _timer.Start();
    }

    public ObservableCollection<HardwareSensorReading> Sensors { get; } = [];
    public IRelayCommand RefreshCommand { get; }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _settings.RefreshIntervalChanged -= OnRefreshIntervalChanged;
    }

    private void OnTimerTick(object? sender, EventArgs e) => Refresh();

    private void OnRefreshIntervalChanged(object? sender, EventArgs e)
    {
        _timer.Interval = TimeSpan.FromSeconds(_settings.RefreshIntervalSeconds);
        Refresh();
    }

    private void Refresh()
    {
        try
        {
            var snapshot = _metricsProvider.ReadCurrent();
            Sensors.Clear();
            foreach (var sensor in snapshot.Sensors ?? []) Sensors.Add(sensor);
            Status = snapshot.Status ?? (Sensors.Count == 0 ? "Keine unterstützten Sensoren gefunden." : $"{Sensors.Count} Sensoren erkannt · Aktualisierung alle 2 Sekunden");
        }
        catch (Exception ex)
        {
            Sensors.Clear();
            Status = $"Sensorabfrage fehlgeschlagen: {ex.Message}";
        }
    }
}
