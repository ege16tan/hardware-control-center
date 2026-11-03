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
    private const int RecordingPreviewLimit = 720;
    private readonly MetricsSampler _metricsSampler;
    private readonly MonitoringSettings _settings;
    private readonly MetricHistoryStore _historyStore;
    private readonly RecordingSessionStore _recordingStore;
    private readonly Dispatcher _dispatcher;
    private readonly ThemeSettings _themeSettings;
    private ISeries[] _liveSeries = [];
    private ISeries[] _series = [];
    private Axis[] _xAxes = [];
    private Axis[] _yAxes = [];
    private SolidColorPaint? _chartLegendTextPaint;
    private SolidColorPaint? _chartLegendBackgroundPaint;
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
    private bool _isRecording;
    private bool _isShowingRecording;
    private string _recordingName = "";
    private string _recordingStatus = "";
    private RecordingSession? _selectedRecording;
    private RecordingSession? _shownRecording;
    private string _cpuChip = "CPU";
    private string _cpuDetail = "";
    private string _gpuChip = "GPU";
    private string _gpuDetail = "";
    private string _memoryChip = "—";
    private string _memoryDetail = "";
    private string _systemTitle = "System";
    private string _systemSubtitle = "";
    private readonly ObservableCollection<DateTimePoint> _cpuHistory = [];
    private readonly ObservableCollection<DateTimePoint> _gpuHistory = [];
    private readonly ObservableCollection<DateTimePoint> _memoryHistory = [];

    public DashboardViewModel(
        MetricsSampler metricsSampler,
        MonitoringSettings settings,
        MetricHistoryStore historyStore,
        RecordingSessionStore recordingStore,
        ThemeSettings themeSettings)
    {
        _metricsSampler = metricsSampler;
        _settings = settings;
        _historyStore = historyStore;
        _recordingStore = recordingStore;
        _themeSettings = themeSettings;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        foreach (var entry in _historyStore.LoadRecent(HistoryLimit))
        {
            if (entry.CpuUsagePercent is double cpu) AddHistory(_cpuHistory, entry.Timestamp.LocalDateTime, cpu);
            if (entry.GpuUsagePercent is double gpu) AddHistory(_gpuHistory, entry.Timestamp.LocalDateTime, gpu);
            if (entry.MemoryUsagePercent is double memory) AddHistory(_memoryHistory, entry.Timestamp.LocalDateTime, memory);
        }
        _historySaveStatus = _historyStore.LastError is null ? "lokal gespeichert" : "Verlauf konnte nicht geladen werden";
        ApplyChartTheme();
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        StartRecordingCommand = new RelayCommand(StartRecording);
        StopRecordingCommand = new RelayCommand(StopRecording);
        ShowRecordingCommand = new RelayCommand(ShowRecording);
        ShowLiveCommand = new RelayCommand(ShowLive);
        DeleteRecordingCommand = new RelayCommand(DeleteRecording);
        ExportRecordingCommand = new RelayCommand(ExportRecording);
        RefreshRecordedSessions(null);
        RecordingStatus = _recordingStore.LastError
            ?? "Aufzeichnungen speichern nur Messwerte lokal; sie greifen nicht in die Hardware ein.";
        _settings.RefreshIntervalChanged += OnRefreshIntervalChanged;
        _themeSettings.Changed += OnThemeChanged;
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
    public IRelayCommand StartRecordingCommand { get; }
    public IRelayCommand StopRecordingCommand { get; }
    public IRelayCommand ShowRecordingCommand { get; }
    public IRelayCommand ShowLiveCommand { get; }
    public IRelayCommand DeleteRecordingCommand { get; }
    public IRelayCommand ExportRecordingCommand { get; }
    public ISeries[] Series { get => _series; private set => SetProperty(ref _series, value); }
    public ObservableCollection<RecordingSession> RecordedSessions { get; } = [];
    public ObservableCollection<TemperatureOverviewItem> Temperatures { get; } = [];
    public ObservableCollection<FanOverviewItem> Fans { get; } = [];
    public ObservableCollection<HardwareStatusItem> HardwareStatus { get; } = [];

    public string CpuChip { get => _cpuChip; private set => SetProperty(ref _cpuChip, value); }
    public string CpuDetail { get => _cpuDetail; private set => SetProperty(ref _cpuDetail, value); }
    public string GpuChip { get => _gpuChip; private set => SetProperty(ref _gpuChip, value); }
    public string GpuDetail { get => _gpuDetail; private set => SetProperty(ref _gpuDetail, value); }
    public string MemoryChip { get => _memoryChip; private set => SetProperty(ref _memoryChip, value); }
    public string MemoryDetail { get => _memoryDetail; private set => SetProperty(ref _memoryDetail, value); }
    public string SystemTitle { get => _systemTitle; private set => SetProperty(ref _systemTitle, value); }
    public string SystemSubtitle { get => _systemSubtitle; private set => SetProperty(ref _systemSubtitle, value); }

    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            if (SetProperty(ref _isRecording, value)) OnPropertyChanged(nameof(CanStartRecording));
        }
    }

    /// <summary>Only one recording at a time, so the start button mirrors the inverse state.</summary>
    public bool CanStartRecording => !IsRecording;

    public bool IsShowingRecording
    {
        get => _isShowingRecording;
        private set => SetProperty(ref _isShowingRecording, value);
    }

    public string RecordingName
    {
        get => _recordingName;
        set => SetProperty(ref _recordingName, value);
    }

    public string RecordingStatus
    {
        get => _recordingStatus;
        private set => SetProperty(ref _recordingStatus, value);
    }

    public RecordingSession? SelectedRecording
    {
        get => _selectedRecording;
        set => SetProperty(ref _selectedRecording, value);
    }

    public Axis[] XAxes { get => _xAxes; private set => SetProperty(ref _xAxes, value); }
    public Axis[] YAxes { get => _yAxes; private set => SetProperty(ref _yAxes, value); }
    public SolidColorPaint? ChartLegendTextPaint { get => _chartLegendTextPaint; private set => SetProperty(ref _chartLegendTextPaint, value); }
    public SolidColorPaint? ChartLegendBackgroundPaint { get => _chartLegendBackgroundPaint; private set => SetProperty(ref _chartLegendBackgroundPaint, value); }

    public void Dispose()
    {
        _settings.RefreshIntervalChanged -= OnRefreshIntervalChanged;
        _themeSettings.Changed -= OnThemeChanged;
        _metricsSampler.SnapshotUpdated -= OnSnapshotUpdated;
        _metricsSampler.SamplingFailed -= OnSamplingFailed;

        // Closing the application must not leave an open ended session behind.
        if (_recordingStore.IsRecording) _recordingStore.TryStop(out _);
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
        ApplyOverview(DashboardOverviewBuilder.Build(snapshot));
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
            if (IsRecording && !_recordingStore.Append(entry))
                RecordingStatus = _recordingStore.LastError ?? RecordingStatus;
            OnPropertyChanged(nameof(SamplingDescription));
        }
    }

    private void StartRecording()
    {
        if (!_recordingStore.TryStart(RecordingName, out var error))
        {
            RecordingStatus = error ?? "Die Aufzeichnung konnte nicht gestartet werden.";
            return;
        }

        IsRecording = true;
        RecordingName = _recordingStore.ActiveSessionName ?? RecordingName;
        RecordingStatus = $"Aufzeichnung „{RecordingName}“ läuft. Jede Messung wird lokal gespeichert.";
        RefreshRecordedSessions(null);
        ShowLive();
    }

    private void StopRecording()
    {
        var saved = _recordingStore.TryStop(out var session);
        IsRecording = false;
        RefreshRecordedSessions(session?.Id);
        if (session is null)
        {
            RecordingStatus = _recordingStore.LastError ?? "Es läuft keine Aufzeichnung.";
            return;
        }

        RecordingStatus = saved
            ? $"Aufzeichnung „{session.Name}“ mit {session.SampleCount} Messungen gespeichert."
            : $"{_recordingStore.LastError} Die bisherigen Messungen bleiben in der Sitzungsdatei erhalten.";
    }

    private void ShowRecording()
    {
        if (SelectedRecording is null)
        {
            RecordingStatus = "Bitte zuerst eine Aufzeichnung auswählen.";
            return;
        }

        var entries = _recordingStore.LoadEntries(SelectedRecording.Id);
        if (entries.Count == 0)
        {
            RecordingStatus = _recordingStore.LastError ?? "Die Aufzeichnung enthält keine Messwerte.";
            return;
        }

        Series = CreateSeries(
            CreateRecordedPoints(entries, entry => entry.CpuUsagePercent),
            CreateRecordedPoints(entries, entry => entry.GpuUsagePercent),
            CreateRecordedPoints(entries, entry => entry.MemoryUsagePercent));
        _shownRecording = SelectedRecording;
        IsShowingRecording = true;
        OnPropertyChanged(nameof(SamplingDescription));
        RecordingStatus = entries.Count > RecordingPreviewLimit
            ? $"Aufzeichnung „{SelectedRecording.Name}“ geladen; im Diagramm erscheinen die letzten {RecordingPreviewLimit} von {entries.Count} Messungen."
            : $"Aufzeichnung „{SelectedRecording.Name}“ mit {entries.Count} Messungen geladen.";
    }

    private void ShowLive()
    {
        Series = _liveSeries;
        _shownRecording = null;
        IsShowingRecording = false;
        OnPropertyChanged(nameof(SamplingDescription));
    }

    private void DeleteRecording()
    {
        if (SelectedRecording is null)
        {
            RecordingStatus = "Bitte zuerst eine Aufzeichnung auswählen.";
            return;
        }

        var deleted = SelectedRecording;
        if (!_recordingStore.TryDelete(deleted.Id, out var error))
        {
            RecordingStatus = error ?? "Die Aufzeichnung konnte nicht gelöscht werden.";
            return;
        }

        if (_shownRecording?.Id == deleted.Id) ShowLive();
        RefreshRecordedSessions(null);
        RecordingStatus = $"Aufzeichnung „{deleted.Name}“ gelöscht.";
    }

    private void ExportRecording()
    {
        if (SelectedRecording is null)
        {
            RecordingStatus = "Bitte zuerst eine Aufzeichnung auswählen.";
            return;
        }

        RecordingStatus = _recordingStore.TryExportCsv(SelectedRecording.Id, out var path)
            ? $"CSV gespeichert: {path}"
            : _recordingStore.LastError ?? "Die Aufzeichnung konnte nicht exportiert werden.";
    }

    private void RefreshRecordedSessions(string? selectId)
    {
        RecordedSessions.Clear();
        // The running session only becomes selectable once it is stopped; its file
        // is still being appended and would otherwise show a stale sample count.
        foreach (var session in _recordingStore.Sessions.Where(session => session.Id != _recordingStore.ActiveSessionId))
            RecordedSessions.Add(session);
        SelectedRecording = RecordedSessions.FirstOrDefault(session => session.Id == selectId)
            ?? RecordedSessions.FirstOrDefault();
    }

    private void ApplyOverview(DashboardOverview overview)
    {
        Replace(Temperatures, overview.Temperatures);
        Replace(Fans, overview.Fans);
        Replace(HardwareStatus, overview.Hardware);

        CpuChip = overview.CpuChip;
        CpuDetail = overview.CpuDetail;
        GpuChip = overview.GpuChip;
        GpuDetail = overview.GpuDetail;
        MemoryChip = overview.MemoryChip;
        MemoryDetail = overview.MemoryDetail;
        SystemTitle = overview.SystemTitle;
        SystemSubtitle = overview.SystemSubtitle;
    }

    /// <summary>Refills a short read-only list; only an existing selection would notice the new instances.</summary>
    private static void Replace<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
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

    public string SamplingDescription => IsShowingRecording && _shownRecording is { } session
        ? $"Aufzeichnung „{session.Name}“ · {session.SampleCount} Messungen · {session.StartedAt.LocalDateTime:dd.MM.yyyy HH:mm} Uhr · nur Anzeige"
        : $"Letzte {HistoryLimit} Messungen · {_settings.RefreshIntervalSeconds}-Sekunden-Intervall · {_historySaveStatus}";

    private ISeries[] CreateSeries(
        ObservableCollection<DateTimePoint> cpu,
        ObservableCollection<DateTimePoint> gpu,
        ObservableCollection<DateTimePoint> memory) =>
    [
        new LineSeries<DateTimePoint> { Name = "CPU", Values = cpu, GeometrySize = 0, Fill = null, Stroke = CreateStroke(SeriesCpuColor) },
        new LineSeries<DateTimePoint> { Name = "GPU", Values = gpu, GeometrySize = 0, Fill = null, Stroke = CreateStroke(SeriesGpuColor) },
        new LineSeries<DateTimePoint> { Name = "RAM", Values = memory, GeometrySize = 0, Fill = null, Stroke = CreateStroke(SeriesMemoryColor) },
    ];

    /// <summary>LiveCharts paints are plain C# objects and do not follow a theme switch on their own.</summary>
    private void ApplyChartTheme()
    {
        var text = new SolidColorPaint(ChartTextColor);
        var grid = new SolidColorPaint(ChartGridColor) { StrokeThickness = 1 };

        ChartLegendTextPaint = text;
        ChartLegendBackgroundPaint = new SolidColorPaint(ChartBackgroundColor);
        XAxes = [new DateTimeAxis(TimeSpan.FromSeconds(30), date => date.ToString("HH:mm:ss"))
        {
            TextSize = 11,
            LabelsPaint = text,
            SeparatorsPaint = grid,
        }];
        YAxes = [new Axis
        {
            MinLimit = 0,
            MaxLimit = 100,
            MinStep = 25,
            TextSize = 11,
            LabelsPaint = text,
            SeparatorsPaint = grid,
        }];

        _liveSeries = CreateSeries(_cpuHistory, _gpuHistory, _memoryHistory);
        if (!IsShowingRecording) Series = _liveSeries;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => OnUiThread(ApplyChartTheme);

    private bool IsLightTheme => string.Equals(_themeSettings.Theme, ThemeSettings.Light, StringComparison.OrdinalIgnoreCase);

    private SKColor ChartTextColor => IsLightTheme ? new SKColor(90, 100, 116) : new SKColor(197, 203, 213);
    private SKColor ChartGridColor => IsLightTheme ? new SKColor(214, 221, 232) : new SKColor(56, 63, 74);
    private SKColor ChartBackgroundColor => IsLightTheme ? new SKColor(255, 255, 255) : new SKColor(28, 33, 42);
    private SKColor SeriesCpuColor => IsLightTheme ? new SKColor(46, 111, 208) : new SKColor(90, 167, 255);
    private SKColor SeriesGpuColor => IsLightTheme ? new SKColor(31, 156, 140) : new SKColor(86, 197, 150);
    private SKColor SeriesMemoryColor => IsLightTheme ? new SKColor(183, 122, 18) : new SKColor(233, 180, 92);

    private static SolidColorPaint CreateStroke(SKColor color) => new(color) { StrokeThickness = 2 };

    private static ObservableCollection<DateTimePoint> CreateRecordedPoints(
        IReadOnlyList<MetricHistoryEntry> entries,
        Func<MetricHistoryEntry, double?> selector)
    {
        var points = new ObservableCollection<DateTimePoint>();
        // Very long sessions are downsampled to the newest samples so the chart stays responsive.
        var relevant = entries.Count > RecordingPreviewLimit
            ? entries.Skip(entries.Count - RecordingPreviewLimit)
            : entries;
        foreach (var entry in relevant)
        {
            if (selector(entry) is double value) points.Add(new DateTimePoint(entry.Timestamp.LocalDateTime, value));
        }
        return points;
    }

    private static void AddHistory(ObservableCollection<DateTimePoint> history, DateTime timestamp, double value)
    {
        history.Add(new DateTimePoint(timestamp, value));
        while (history.Count > HistoryLimit) history.RemoveAt(0);
    }

}
