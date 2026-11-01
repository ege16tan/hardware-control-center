using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCC.Core.Interfaces;
using HCC.Core.Models;
using HCC.Infrastructure.Configuration;
using HCC.Infrastructure.Persistence;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;

namespace HCC.App.ViewModels;

public sealed class DashboardViewModel : ObservableObject, IDisposable
{
    private const int HistoryLimit = 60;
    private readonly IMetricsProvider _metricsProvider;
    private readonly MonitoringSettings _settings;
    private readonly MetricHistoryStore _historyStore;
    private readonly DispatcherTimer _refreshTimer;
    private double _cpuUsage;
    private double _gpuUsage;
    private double _memoryUsage;
    private string _memorySummary = "–";
    private string _dataMode = "Live-Monitoring";
    private string _gpuUsageSource = "nicht gemeldet";
    private readonly ObservableCollection<DateTimePoint> _cpuHistory = [];
    private readonly ObservableCollection<DateTimePoint> _gpuHistory = [];
    private readonly ObservableCollection<DateTimePoint> _memoryHistory = [];

    public DashboardViewModel(IMetricsProvider metricsProvider, MonitoringSettings settings, MetricHistoryStore historyStore)
    {
        _metricsProvider = metricsProvider;
        _settings = settings;
        _historyStore = historyStore;
        foreach (var entry in _historyStore.LoadRecent(HistoryLimit))
        {
            AddHistory(_cpuHistory, entry.Timestamp.LocalDateTime, entry.CpuUsagePercent);
            AddHistory(_gpuHistory, entry.Timestamp.LocalDateTime, entry.GpuUsagePercent);
            AddHistory(_memoryHistory, entry.Timestamp.LocalDateTime, entry.MemoryUsagePercent);
        }
        Series =
        [
            new LineSeries<DateTimePoint> { Name = "CPU", Values = _cpuHistory, GeometrySize = 0, Fill = null },
            new LineSeries<DateTimePoint> { Name = "GPU", Values = _gpuHistory, GeometrySize = 0, Fill = null },
            new LineSeries<DateTimePoint> { Name = "RAM", Values = _memoryHistory, GeometrySize = 0, Fill = null },
        ];
        XAxes = [new DateTimeAxis(TimeSpan.FromSeconds(30), date => date.ToString("HH:mm:ss"))];
        YAxes = [new Axis { MinLimit = 0, MaxLimit = 100 }];
        RefreshCommand = new RelayCommand(Refresh);
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_settings.RefreshIntervalSeconds) };
        _refreshTimer.Tick += OnRefreshTimerTick;
        _settings.RefreshIntervalChanged += OnRefreshIntervalChanged;

        Refresh();
        _refreshTimer.Start();
    }

    public double CpuUsage
    {
        get => _cpuUsage;
        private set => SetProperty(ref _cpuUsage, value);
    }

    public double GpuUsage
    {
        get => _gpuUsage;
        private set => SetProperty(ref _gpuUsage, value);
    }

    public double MemoryUsage
    {
        get => _memoryUsage;
        private set => SetProperty(ref _memoryUsage, value);
    }

    public string MemorySummary
    {
        get => _memorySummary;
        private set => SetProperty(ref _memorySummary, value);
    }

    public string DataMode
    {
        get => _dataMode;
        private set => SetProperty(ref _dataMode, value);
    }

    public string GpuUsageSource
    {
        get => _gpuUsageSource;
        private set => SetProperty(ref _gpuUsageSource, value);
    }

    public IRelayCommand RefreshCommand { get; }
    public ISeries[] Series { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTimerTick;
        _settings.RefreshIntervalChanged -= OnRefreshIntervalChanged;
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var snapshot = _metricsProvider.ReadCurrent();
        CpuUsage = snapshot.CpuUsagePercent;
        GpuUsage = snapshot.GpuUsagePercent;
        MemoryUsage = snapshot.MemoryUsagePercent;
        MemorySummary = $"{snapshot.UsedMemoryGigabytes:0.0} / {snapshot.TotalMemoryGigabytes:0.0} GB";
        DataMode = snapshot.IsSimulated ? "Demo-Modus" : "Live-Monitoring";
        GpuUsageSource = snapshot.GpuLoadSensorName ?? "kein Lastsensor gefunden";
        var timestamp = DateTimeOffset.Now;
        AddHistory(_cpuHistory, timestamp.LocalDateTime, snapshot.CpuUsagePercent);
        AddHistory(_gpuHistory, timestamp.LocalDateTime, snapshot.GpuUsagePercent);
        AddHistory(_memoryHistory, timestamp.LocalDateTime, snapshot.MemoryUsagePercent);
        _historyStore.Append(new MetricHistoryEntry(timestamp, snapshot.CpuUsagePercent, snapshot.GpuUsagePercent, snapshot.MemoryUsagePercent));
    }

    private void OnRefreshIntervalChanged(object? sender, EventArgs e)
    {
        _refreshTimer.Interval = TimeSpan.FromSeconds(_settings.RefreshIntervalSeconds);
        Refresh();
        OnPropertyChanged(nameof(SamplingDescription));
    }

    public string SamplingDescription => $"Letzte {HistoryLimit} Messungen · {_settings.RefreshIntervalSeconds}-Sekunden-Intervall · lokal gespeichert";

    private static void AddHistory(ObservableCollection<DateTimePoint> history, DateTime timestamp, double value)
    {
        history.Add(new DateTimePoint(timestamp, value));
        while (history.Count > HistoryLimit) history.RemoveAt(0);
    }
}
