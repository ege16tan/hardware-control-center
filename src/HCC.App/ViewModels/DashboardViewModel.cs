using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCC.Core.Interfaces;
using HCC.Core.Models;
using HCC.Infrastructure.Configuration;
using HCC.Infrastructure.Monitoring;
using HCC.Infrastructure.Persistence;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace HCC.App.ViewModels;

public sealed class DashboardViewModel : ObservableObject, IDisposable
{
    private const int HistoryLimit = 60;
    private readonly MetricsSampler _metricsSampler;
    private readonly MonitoringSettings _settings;
    private readonly MetricHistoryStore _historyStore;
    private readonly Dispatcher _dispatcher;
    private double _cpuUsage;
    private double _gpuUsage;
    private double _memoryUsage;
    private bool _cpuAvailable;
    private bool _gpuAvailable;
    private bool _memoryAvailable;
    private string _memorySummary = "–";
    private string _dataMode = "Sensoren werden gestartet …";
    private string _gpuUsageSource = "nicht gemeldet";
    private string _status = "Messwerte werden geladen …";
    private string _historySaveStatus = "Verlauf wird geladen …";
    private Brush _statusIndicatorBrush = Brushes.Goldenrod;
    private readonly ObservableCollection<DateTimePoint> _cpuHistory = [];
    private readonly ObservableCollection<DateTimePoint> _gpuHistory = [];
    private readonly ObservableCollection<DateTimePoint> _memoryHistory = [];

    public DashboardViewModel(MetricsSampler metricsSampler, MonitoringSettings settings, MetricHistoryStore historyStore)
    {
        _metricsSampler = metricsSampler;
        _settings = settings;
        _historyStore = historyStore;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        foreach (var entry in _historyStore.LoadRecent(HistoryLimit))
        {
            if (entry.CpuUsagePercent is double cpu) AddHistory(_cpuHistory, entry.Timestamp.LocalDateTime, cpu);
            if (entry.GpuUsagePercent is double gpu) AddHistory(_gpuHistory, entry.Timestamp.LocalDateTime, gpu);
            if (entry.MemoryUsagePercent is double memory) AddHistory(_memoryHistory, entry.Timestamp.LocalDateTime, memory);
        }
        _historySaveStatus = _historyStore.LastError is null ? "lokal gespeichert" : "Verlauf konnte nicht geladen werden";
        Series =
        [
            new LineSeries<DateTimePoint> { Name = "CPU", Values = _cpuHistory, GeometrySize = 0, Fill = null, Stroke = CreateStroke(124, 140, 255) },
            new LineSeries<DateTimePoint> { Name = "GPU", Values = _gpuHistory, GeometrySize = 0, Fill = null, Stroke = CreateStroke(82, 199, 184) },
            new LineSeries<DateTimePoint> { Name = "RAM", Values = _memoryHistory, GeometrySize = 0, Fill = null, Stroke = CreateStroke(232, 180, 91) },
        ];
        XAxes = [new DateTimeAxis(TimeSpan.FromSeconds(30), date => date.ToString("HH:mm:ss"))
        {
            LabelsPaint = CreateTextPaint(),
            SeparatorsPaint = CreateGridPaint(),
        }];
        YAxes = [new Axis
        {
            MinLimit = 0,
            MaxLimit = 100,
            LabelsPaint = CreateTextPaint(),
            SeparatorsPaint = CreateGridPaint(),
        }];
        ChartLegendTextPaint = CreateTextPaint();
        ChartLegendBackgroundPaint = new SolidColorPaint(new SKColor(28, 32, 40));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        _settings.RefreshIntervalChanged += OnRefreshIntervalChanged;
        _metricsSampler.SnapshotUpdated += OnSnapshotUpdated;
        _metricsSampler.SamplingFailed += OnSamplingFailed;
        if (_metricsSampler.LatestSnapshot is { } latest) ApplySnapshot(latest);
        else _ = RefreshAsync();
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

    public bool CpuAvailable { get => _cpuAvailable; private set { if (SetProperty(ref _cpuAvailable, value)) OnPropertyChanged(nameof(CpuUsageText)); } }
    public bool GpuAvailable { get => _gpuAvailable; private set { if (SetProperty(ref _gpuAvailable, value)) OnPropertyChanged(nameof(GpuUsageText)); } }
    public bool MemoryAvailable { get => _memoryAvailable; private set { if (SetProperty(ref _memoryAvailable, value)) OnPropertyChanged(nameof(MemoryUsageText)); } }
    public string CpuUsageText => CpuAvailable ? $"{CpuUsage:0}%" : "–";
    public string GpuUsageText => GpuAvailable ? $"{GpuUsage:0}%" : "–";
    public string MemoryUsageText => MemoryAvailable ? $"{MemoryUsage:0}%" : "–";
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public Brush StatusIndicatorBrush { get => _statusIndicatorBrush; private set => SetProperty(ref _statusIndicatorBrush, value); }

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

    public IAsyncRelayCommand RefreshCommand { get; }
    public ISeries[] Series { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }
    public SolidColorPaint ChartLegendTextPaint { get; }
    public SolidColorPaint ChartLegendBackgroundPaint { get; }

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

    private void OnSnapshotUpdated(MetricSnapshot snapshot) => OnUiThread(() => ApplySnapshot(snapshot));

    private void OnSamplingFailed(Exception exception) => OnUiThread(() =>
    {
        DataMode = "Monitoringfehler";
        Status = $"Messung fehlgeschlagen: {exception.Message}";
        StatusIndicatorBrush = Brushes.OrangeRed;
    });

    private void ApplySnapshot(MetricSnapshot snapshot)
    {
        CpuAvailable = snapshot.CpuAvailable;
        GpuAvailable = snapshot.GpuAvailable;
        MemoryAvailable = snapshot.MemoryAvailable;
        CpuUsage = snapshot.CpuUsagePercent;
        GpuUsage = snapshot.GpuUsagePercent;
        MemoryUsage = snapshot.MemoryUsagePercent;
        MemorySummary = snapshot.MemoryAvailable ? $"{snapshot.UsedMemoryGigabytes:0.0} / {snapshot.TotalMemoryGigabytes:0.0} GB" : "nicht verfügbar";
        DataMode = snapshot.IsSimulated ? "Demo-Modus" : snapshot.Status is null ? "Live-Monitoring" : "Monitoring eingeschränkt";
        Status = snapshot.Status ?? (snapshot.IsSimulated ? "Demo-Werte werden angezeigt." : "Alle unterstützten Messwerte sind verfügbar.");
        StatusIndicatorBrush = snapshot.IsSimulated ? Brushes.DodgerBlue : snapshot.Status is null ? Brushes.LimeGreen : Brushes.Orange;
        GpuUsageSource = snapshot.GpuAvailable ? snapshot.GpuLoadSensorName ?? "Lastsensor" : "nicht verfügbar";
        OnPropertyChanged(nameof(CpuUsageText));
        OnPropertyChanged(nameof(GpuUsageText));
        OnPropertyChanged(nameof(MemoryUsageText));
        var timestamp = DateTimeOffset.Now;
        if (snapshot.CpuAvailable) AddHistory(_cpuHistory, timestamp.LocalDateTime, snapshot.CpuUsagePercent);
        if (snapshot.GpuAvailable) AddHistory(_gpuHistory, timestamp.LocalDateTime, snapshot.GpuUsagePercent);
        if (snapshot.MemoryAvailable) AddHistory(_memoryHistory, timestamp.LocalDateTime, snapshot.MemoryUsagePercent);
        if (snapshot.CpuAvailable || snapshot.GpuAvailable || snapshot.MemoryAvailable)
        {
            var entry = new MetricHistoryEntry(timestamp,
                snapshot.CpuAvailable ? snapshot.CpuUsagePercent : null,
                snapshot.GpuAvailable ? snapshot.GpuUsagePercent : null,
                snapshot.MemoryAvailable ? snapshot.MemoryUsagePercent : null);
            _historySaveStatus = _historyStore.Append(entry) ? "lokal gespeichert" : "Verlauf konnte nicht gespeichert werden";
            OnPropertyChanged(nameof(SamplingDescription));
        }
    }

    private void OnRefreshIntervalChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(SamplingDescription));
    }

    private void OnUiThread(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.BeginInvoke(action);
    }

    public string SamplingDescription => $"Letzte {HistoryLimit} Messungen · {_settings.RefreshIntervalSeconds}-Sekunden-Intervall · {_historySaveStatus}";

    private static void AddHistory(ObservableCollection<DateTimePoint> history, DateTime timestamp, double value)
    {
        history.Add(new DateTimePoint(timestamp, value));
        while (history.Count > HistoryLimit) history.RemoveAt(0);
    }

    private static SolidColorPaint CreateStroke(byte red, byte green, byte blue) =>
        new(new SKColor(red, green, blue)) { StrokeThickness = 2 };

    private static SolidColorPaint CreateTextPaint() => new(new SKColor(197, 203, 213));

    private static SolidColorPaint CreateGridPaint() =>
        new(new SKColor(56, 63, 74)) { StrokeThickness = 1 };
}
