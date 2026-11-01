using HCC.Core.Interfaces;
using HCC.Core.Models;
using HCC.Infrastructure.Configuration;

namespace HCC.Infrastructure.Monitoring;

/// <summary>Reads hardware off the UI thread and publishes one shared sample stream to all views.</summary>
public sealed class MetricsSampler : IDisposable
{
    private readonly object _sync = new();
    private readonly IMetricsProvider _metricsProvider;
    private readonly MonitoringSettings _settings;
    private readonly CancellationTokenSource _shutdown = new();
    private TaskCompletionSource _intervalChanged = NewSignal();
    private Task<MetricSnapshot>? _activeRead;
    private readonly Task _pollingTask;
    private MetricSnapshot? _latestSnapshot;
    private bool _disposed;

    public MetricsSampler(IMetricsProvider metricsProvider, MonitoringSettings settings)
    {
        _metricsProvider = metricsProvider;
        _settings = settings;
        _settings.RefreshIntervalChanged += OnRefreshIntervalChanged;
        _pollingTask = Task.Run(() => PollAsync(_shutdown.Token));
    }

    public event Action<MetricSnapshot>? SnapshotUpdated;
    public event Action<Exception>? SamplingFailed;

    public MetricSnapshot? LatestSnapshot => Volatile.Read(ref _latestSnapshot);

    public Task<MetricSnapshot> RefreshNowAsync()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_activeRead is { IsCompleted: false }) return _activeRead;

            _activeRead = Task.Run(() =>
            {
                try
                {
                    var snapshot = _metricsProvider.ReadCurrent();
                    Volatile.Write(ref _latestSnapshot, snapshot);
                    NotifySubscribers(SnapshotUpdated, snapshot);
                    return snapshot;
                }
                catch (Exception ex)
                {
                    NotifySubscribers(SamplingFailed, ex);
                    throw;
                }
            });
            return _activeRead;
        }
    }

    public void Dispose()
    {
        Task<MetricSnapshot>? activeRead;
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            activeRead = _activeRead;
        }

        _settings.RefreshIntervalChanged -= OnRefreshIntervalChanged;
        _shutdown.Cancel();
        try { _pollingTask.GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { }
        try { activeRead?.GetAwaiter().GetResult(); }
        catch (Exception) { }
        _shutdown.Dispose();
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var intervalChange = Volatile.Read(ref _intervalChanged).Task;
            try { await RefreshNowAsync().ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (Exception) { /* RefreshNowAsync reports failures to subscribers. */ }

            using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var delay = Task.Delay(TimeSpan.FromSeconds(_settings.RefreshIntervalSeconds), waitCancellation.Token);
            try { await Task.WhenAny(delay, intervalChange).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            finally { waitCancellation.Cancel(); }
        }
    }

    private void OnRefreshIntervalChanged(object? sender, EventArgs e)
    {
        var next = NewSignal();
        Interlocked.Exchange(ref _intervalChanged, next).TrySetResult();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void NotifySubscribers<T>(Action<T>? subscribers, T value)
    {
        if (subscribers is null) return;
        foreach (Action<T> subscriber in subscribers.GetInvocationList())
        {
            try { subscriber(value); }
            catch { /* A broken view must not stop hardware sampling for other views. */ }
        }
    }
}
