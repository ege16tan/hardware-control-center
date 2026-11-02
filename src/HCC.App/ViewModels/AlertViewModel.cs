using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using HCC.Core.Models;
using HCC.Infrastructure.Configuration;
using HCC.Infrastructure.Monitoring;

namespace HCC.App.ViewModels;

public sealed class AlertViewModel : ObservableObject, IDisposable
{
    private readonly MetricsSampler _metricsSampler;
    private readonly AlertSettings _settings;
    private readonly Dispatcher _dispatcher;
    private bool _hasAlert;
    private string _alertText = "";

    public AlertViewModel(MetricsSampler metricsSampler, AlertSettings settings)
    {
        _metricsSampler = metricsSampler;
        _settings = settings;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _metricsSampler.SnapshotUpdated += OnSnapshotUpdated;
        _settings.Changed += OnSettingsChanged;
        if (_metricsSampler.LatestSnapshot is { } latest) Apply(latest);
    }

    public bool HasAlert { get => _hasAlert; private set => SetProperty(ref _hasAlert, value); }
    public string AlertText { get => _alertText; private set => SetProperty(ref _alertText, value); }

    public void Dispose()
    {
        _metricsSampler.SnapshotUpdated -= OnSnapshotUpdated;
        _settings.Changed -= OnSettingsChanged;
    }

    private void OnSnapshotUpdated(MetricSnapshot snapshot) => OnUiThread(() => Apply(snapshot));

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_metricsSampler.LatestSnapshot is { } latest) OnUiThread(() => Apply(latest));
    }

    private void Apply(MetricSnapshot snapshot)
    {
        var alerts = AlertEvaluator.Evaluate(snapshot, _settings.TemperatureWarningCelsius);
        HasAlert = alerts.Count > 0;
        AlertText = alerts.Count switch
        {
            0 => "",
            1 => $"Temperaturwarnung: {alerts[0].DisplayText}",
            _ => $"Temperaturwarnung: {alerts.Count} Sensoren über {_settings.TemperatureWarningCelsius:0.#} °C – höchster Wert {alerts[0].SensorName} mit {alerts[0].Value:0.0} °C."
        };
    }

    private void OnUiThread(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.BeginInvoke(action);
    }
}
